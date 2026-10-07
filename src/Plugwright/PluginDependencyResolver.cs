namespace Plugwright;

/// <summary>
/// Checks the dependencies between plugin units and puts the units into dependency order.
/// </summary>
/// <remarks>
/// A unit depends on another unit if it declares a <see cref="PluginDependencyAttribute"/> for it, or if it references
/// the other unit's assembly. Problems are reported through a callback, which either throws or marks the unit as failed.
/// </remarks>
internal static class PluginDependencyResolver
{
    /// <summary>
    /// Resolves the dependencies of all units and returns them in an order where every unit comes after its dependencies.
    /// Apart from that, the original order is kept.
    /// </summary>
    /// <param name="units">The units in the order they were added.</param>
    /// <param name="fail">Called once for every unit with a dependency problem.</param>
    public static List<PluginUnit> Resolve(List<PluginUnit> units, Action<PluginUnit, string> fail)
    {
        var unitsById = units.ToDictionary(unit => unit.Id, StringComparer.OrdinalIgnoreCase);

        foreach (var unit in units)
        {
            if (FindDependencies(unit, unitsById) is { } problem)
            {
                fail(unit, problem);
            }
        }

        return Sort(units, fail);
    }

    /// <summary>
    /// Fills <see cref="PluginUnit.DependsOn"/> and <see cref="PluginUnit.Metadata"/>.
    /// </summary>
    /// <returns>A description of the first problem, or <see langword="null"/>.</returns>
    private static string? FindDependencies(PluginUnit unit, Dictionary<string, PluginUnit> unitsById)
    {
        List<PluginDependency> dependencies = [];

        foreach (var dependency in unit.Info.Metadata.Dependencies)
        {
            if (!unitsById.TryGetValue(dependency.PluginId, out var other))
            {
                return $"It depends on plugin '{dependency.PluginId}', which is not available.";
            }

            if (dependency.VersionRange is not null)
            {
                if (unit.Info.SatisfiesVersionRange?.Invoke(dependency, other.Metadata.Version) is not true)
                {
                    return $"It requires plugin '{dependency.PluginId}' version {dependency.VersionRange}, "
                        + $"but version {other.Metadata.Version} is available.";
                }
            }
            else if (dependency.MinVersion is not null)
            {
                if (!SemanticVersion.TryParse(dependency.MinVersion, out var minVersion))
                {
                    return $"Its dependency on plugin '{dependency.PluginId}' has an invalid minimum version '{dependency.MinVersion}'.";
                }

                if (!SemanticVersion.TryParse(other.Metadata.Version, out var availableVersion) || availableVersion.CompareTo(minVersion) < 0)
                {
                    return $"It requires plugin '{dependency.PluginId}' version {dependency.MinVersion} or later, "
                        + $"but version {other.Metadata.Version} is available.";
                }
            }

            dependencies.Add(dependency);
            AddDependency(unit, other);
        }

        foreach (var reference in unit.Info.References)
        {
            if (!unitsById.TryGetValue(reference.Name!, out var other) || other == unit)
            {
                continue;
            }

            if (reference.Version is not null && reference.Version > other.Info.AssemblyVersion)
            {
                return $"It was compiled against assembly version {reference.Version} of plugin '{other.Id}', "
                    + $"but assembly version {other.Info.AssemblyVersion} is available.";
            }

            if (!dependencies.Any(dependency => string.Equals(dependency.PluginId, other.Id, StringComparison.OrdinalIgnoreCase)))
            {
                dependencies.Add(new PluginDependency(other.Id));
            }

            AddDependency(unit, other);
        }

        unit.Metadata = new PluginMetadata(unit.Info.Metadata.Id, unit.Info.Metadata.Version)
        {
            Description = unit.Info.Metadata.Description,
            Authors = unit.Info.Metadata.Authors,
            Dependencies = dependencies,
        };

        if (unit.IsInCode && unit.DependsOn.FirstOrDefault(other => !other.IsInCode) is { } folderDependency)
        {
            return $"It depends on plugin '{folderDependency.Id}' from {folderDependency.Source}. "
                + "Plugins added in code can only depend on plugins added in code.";
        }

        return null;
    }

    private static void AddDependency(PluginUnit unit, PluginUnit other)
    {
        if (!unit.DependsOn.Contains(other))
        {
            unit.DependsOn.Add(other);
        }
    }

    /// <summary>
    /// Depth-first topological sort that keeps the original order where dependencies allow it.
    /// Every unit in a cycle is reported.
    /// </summary>
    private static List<PluginUnit> Sort(List<PluginUnit> units, Action<PluginUnit, string> fail)
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
                    fail(member, $"Plugins depend on each other in a cycle: {description}.");
                }

                return;
            }

            path.Add(unit);
            foreach (var dependency in unit.DependsOn)
            {
                Visit(dependency);
            }

            path.RemoveAt(path.Count - 1);
            done.Add(unit);
            sorted.Add(unit);
        }
    }
}
