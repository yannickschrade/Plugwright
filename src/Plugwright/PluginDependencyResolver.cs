namespace Plugwright;

/// <summary>
/// Checks the dependencies between plugin units and puts the units into dependency order.
/// </summary>
/// <remarks>
/// A unit depends on another unit if it declares a <see cref="PluginDependencyAttribute"/> for it, or if it references
/// the other unit's assembly. An optional dependency that is missing is no problem; one that is present orders and shares
/// types like a required one. Problems are reported through a callback, which either throws or marks the unit as failed.
/// </remarks>
internal static class PluginDependencyResolver
{
    /// <summary>
    /// Resolves the dependencies of all units and returns them in an order where every unit comes after its dependencies.
    /// Apart from that, the original order is kept.
    /// </summary>
    /// <param name="units">The units in the order they were added.</param>
    /// <param name="fail">Called once for every unit with a dependency problem.</param>
    public static List<PluginUnit> Resolve(List<PluginUnit> units, Action<PluginUnit, PluginState, string> fail)
    {
        var unitsById = units.ToDictionary(unit => unit.Id, StringComparer.OrdinalIgnoreCase);

        // Plugins that are turned off are never loaded, so their own dependencies do not matter.
        foreach (var unit in units.Where(unit => unit.State is null))
        {
            if (FindDependencies(unit, unitsById) is ({ } state, { } problem))
            {
                fail(unit, state, problem);
            }
        }

        return Sort(units, fail);
    }

    /// <summary>
    /// Fills <see cref="PluginUnit.DependsOn"/>, <see cref="PluginUnit.OptionallyDependsOn"/> and <see cref="PluginUnit.Metadata"/>.
    /// </summary>
    /// <returns>A description of the first problem, or <see langword="null"/>.</returns>
    private static (PluginState? State, string? Problem) FindDependencies(PluginUnit unit, Dictionary<string, PluginUnit> unitsById)
    {
        List<PluginDependency> dependencies = [];

        // A dependency is optional only if every declaration of it is; a package dependency in the .nuspec is required.
        var optionalIds = unit.Info.Metadata.Dependencies
            .GroupBy(dependency => dependency.PluginId, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.All(dependency => dependency.IsOptional))
            .Select(group => group.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var declared in unit.Info.Metadata.Dependencies)
        {
            var isOptional = optionalIds.Contains(declared.PluginId);
            var dependency = declared.IsOptional == isOptional
                ? declared
                : new PluginDependency(declared.PluginId, declared.MinVersion) { VersionRange = declared.VersionRange };

            if (!unitsById.TryGetValue(dependency.PluginId, out var other))
            {
                if (isOptional)
                {
                    dependencies.Add(dependency);
                    continue;
                }

                return (PluginState.DependencyUnavailable, $"It depends on plugin '{dependency.PluginId}', which is not available.");
            }

            // A plugin that is turned off or failed is not loaded: there is no version to check; its dependents are skipped later.
            if (other.State is not null)
            {
                dependencies.Add(dependency);
                AddDependency(unit, other, isOptional);
                continue;
            }

            if (dependency.VersionRange is not null)
            {
                if (unit.Info.SatisfiesVersionRange?.Invoke(dependency, other.Metadata.Version) is not true)
                {
                    return (PluginState.DependencyUnavailable, $"It requires plugin '{dependency.PluginId}' version {dependency.VersionRange}, "
                        + $"but version {other.Metadata.Version} is available.");
                }
            }
            else if (dependency.MinVersion is not null)
            {
                if (!SemanticVersion.TryParse(dependency.MinVersion, out var minVersion))
                {
                    return (PluginState.Failed, $"Its dependency on plugin '{dependency.PluginId}' has an invalid minimum version '{dependency.MinVersion}'.");
                }

                if (!SemanticVersion.TryParse(other.Metadata.Version, out var availableVersion) || availableVersion.CompareTo(minVersion) < 0)
                {
                    return (PluginState.DependencyUnavailable, $"It requires plugin '{dependency.PluginId}' version {dependency.MinVersion} or later, "
                        + $"but version {other.Metadata.Version} is available.");
                }
            }

            dependencies.Add(dependency);
            AddDependency(unit, other, isOptional);
        }

        foreach (var reference in unit.Info.References)
        {
            if (!unitsById.TryGetValue(reference.Name!, out var other) || other == unit)
            {
                continue;
            }

            if (other.State is null && reference.Version is not null && reference.Version > other.Info.AssemblyVersion)
            {
                return (PluginState.DependencyUnavailable, $"It was compiled against assembly version {reference.Version} of plugin '{other.Id}', "
                    + $"but assembly version {other.Info.AssemblyVersion} is available.");
            }

            if (!dependencies.Any(dependency => string.Equals(dependency.PluginId, other.Id, StringComparison.OrdinalIgnoreCase)))
            {
                dependencies.Add(new PluginDependency(other.Id));
            }

            AddDependency(unit, other, optionalIds.Contains(other.Id));
        }

        unit.Metadata = new PluginMetadata(unit.Info.Metadata.Id, unit.Info.Metadata.Version)
        {
            Description = unit.Info.Metadata.Description,
            Authors = unit.Info.Metadata.Authors,
            Dependencies = dependencies,
        };

        if (unit.IsInCode && unit.DependsOn.Concat(unit.OptionallyDependsOn).FirstOrDefault(other => !other.IsInCode) is { } folderDependency)
        {
            return (PluginState.DependencyUnavailable, $"It depends on plugin '{folderDependency.Id}' from {folderDependency.Source}. "
                + "Plugins added in code can only depend on plugins added in code.");
        }

        return (null, null);
    }

    private static void AddDependency(PluginUnit unit, PluginUnit other, bool isOptional)
    {
        var list = isOptional ? unit.OptionallyDependsOn : unit.DependsOn;
        if (!list.Contains(other))
        {
            list.Add(other);
        }
    }

    /// <summary>
    /// Depth-first topological sort that keeps the original order where dependencies allow it.
    /// Every unit in a cycle is reported.
    /// </summary>
    private static List<PluginUnit> Sort(List<PluginUnit> units, Action<PluginUnit, PluginState, string> fail)
    {
        List<PluginUnit> sorted = [];
        HashSet<PluginUnit> done = [];
        List<PluginUnit> path = [];

        foreach (var unit in units)
        {
            Visit(unit);
        }

        return sorted;

        void Visit(PluginUnit unit)
        {
            if (done.Contains(unit))
            {
                return;
            }

            var index = path.IndexOf(unit);
            if (index >= 0)
            {
                var cycle = path[index..];
                var description = string.Join(" -> ", cycle.Append(unit).Select(member => member.Id));
                foreach (var member in cycle.Where(member => !member.Failed))
                {
                    fail(member, PluginState.Failed, $"Plugins depend on each other in a cycle: {description}.");
                }

                return;
            }

            path.Add(unit);
            foreach (var dependency in unit.DependsOn.Concat(unit.OptionallyDependsOn))
            {
                Visit(dependency);
            }

            path.RemoveAt(path.Count - 1);
            done.Add(unit);
            sorted.Add(unit);
        }
    }
}
