using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Plugwright;

/// <summary>
/// A semantic version (<c>1.2.0</c>, <c>2.0.0-beta.1</c>) for comparing plugin versions.
/// </summary>
/// <remarks>
/// Follows SemVer 2.0 precedence. Like NuGet, it also accepts a fourth number (<c>1.2.0.5</c>) and fewer than three
/// (<c>1.2</c> means <c>1.2.0</c>). Build metadata after <c>+</c> is ignored.
/// </remarks>
internal sealed class SemanticVersion : IComparable<SemanticVersion>
{
    private readonly int[] _numbers;
    private readonly string[] _prereleaseLabels;

    private SemanticVersion(int[] numbers, string[] prereleaseLabels)
    {
        _numbers = numbers;
        _prereleaseLabels = prereleaseLabels;
    }

    public static bool TryParse(string? text, [NotNullWhen(true)] out SemanticVersion? version)
    {
        version = null;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var withoutMetadata = text.Split('+', 2)[0];
        var parts = withoutMetadata.Split('-', 2);

        var numberParts = parts[0].Split('.');
        if (numberParts.Length is < 1 or > 4)
        {
            return false;
        }

        var numbers = new int[4];
        for (var i = 0; i < numberParts.Length; i++)
        {
            if (!int.TryParse(numberParts[i], NumberStyles.None, CultureInfo.InvariantCulture, out numbers[i]))
            {
                return false;
            }
        }

        string[] labels = [];
        if (parts.Length == 2)
        {
            labels = parts[1].Split('.');
            if (labels.Any(label => label.Length == 0 || !label.All(c => char.IsAsciiLetterOrDigit(c) || c == '-')))
            {
                return false;
            }
        }

        version = new SemanticVersion(numbers, labels);
        return true;
    }

    public int CompareTo(SemanticVersion? other)
    {
        if (other is null)
        {
            return 1;
        }

        for (var i = 0; i < _numbers.Length; i++)
        {
            var result = _numbers[i].CompareTo(other._numbers[i]);
            if (result != 0)
            {
                return result;
            }
        }

        // A version without prerelease labels is higher than the same version with labels.
        if (_prereleaseLabels.Length == 0 || other._prereleaseLabels.Length == 0)
        {
            return other._prereleaseLabels.Length.CompareTo(_prereleaseLabels.Length);
        }

        for (var i = 0; i < Math.Min(_prereleaseLabels.Length, other._prereleaseLabels.Length); i++)
        {
            var result = CompareLabels(_prereleaseLabels[i], other._prereleaseLabels[i]);
            if (result != 0)
            {
                return result;
            }
        }

        return _prereleaseLabels.Length.CompareTo(other._prereleaseLabels.Length);
    }

    // Numeric labels compare numerically and are lower than alphanumeric labels.
    private static int CompareLabels(string left, string right)
    {
        var leftIsNumber = int.TryParse(left, NumberStyles.None, CultureInfo.InvariantCulture, out var leftNumber);
        var rightIsNumber = int.TryParse(right, NumberStyles.None, CultureInfo.InvariantCulture, out var rightNumber);

        return (leftIsNumber, rightIsNumber) switch
        {
            (true, true) => leftNumber.CompareTo(rightNumber),
            (true, false) => -1,
            (false, true) => 1,
            _ => string.CompareOrdinal(left, right),
        };
    }
}
