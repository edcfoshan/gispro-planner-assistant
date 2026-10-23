using System;

namespace GisProAddinWorkbench.AddIn.Infrastructure;

internal readonly record struct SemanticVersion(Version Core, string Prerelease);

internal static class SemanticVersionComparer
{
    public static bool TryParse(string value, out SemanticVersion version)
    {
        var buildMetadata = value.IndexOf('+');
        if (buildMetadata >= 0)
        {
            value = value[..buildMetadata];
        }

        var prereleaseSeparator = value.IndexOf('-');
        var coreText = prereleaseSeparator < 0 ? value : value[..prereleaseSeparator];
        var prerelease = prereleaseSeparator < 0
            ? string.Empty
            : value[(prereleaseSeparator + 1)..];

        if (Version.TryParse(coreText, out var parsed))
        {
            var core = new Version(parsed.Major, parsed.Minor,
                parsed.Build < 0 ? 0 : parsed.Build,
                parsed.Revision < 0 ? 0 : parsed.Revision);
            version = new SemanticVersion(core, prerelease);
            return true;
        }

        version = default;
        return false;
    }

    public static int Compare(SemanticVersion left, SemanticVersion right)
    {
        var core = left.Core.CompareTo(right.Core);
        return core != 0 ? core : ComparePrerelease(left.Prerelease, right.Prerelease);
    }

    private static int ComparePrerelease(string left, string right)
    {
        if (string.Equals(left, right, StringComparison.Ordinal))
        {
            return 0;
        }

        // SemVer: a stable version is newer than its prerelease.
        if (left.Length == 0)
        {
            return 1;
        }

        if (right.Length == 0)
        {
            return -1;
        }

        var leftParts = left.Split('.');
        var rightParts = right.Split('.');
        for (var index = 0; index < Math.Min(leftParts.Length, rightParts.Length); index++)
        {
            var leftIsNumeric = int.TryParse(leftParts[index], out var leftNumber);
            var rightIsNumeric = int.TryParse(rightParts[index], out var rightNumber);
            if (leftIsNumeric && rightIsNumeric)
            {
                var number = leftNumber.CompareTo(rightNumber);
                if (number != 0)
                {
                    return number;
                }
            }
            else if (leftIsNumeric)
            {
                return -1;
            }
            else if (rightIsNumeric)
            {
                return 1;
            }
            else
            {
                var text = string.CompareOrdinal(leftParts[index], rightParts[index]);
                if (text != 0)
                {
                    return text;
                }
            }
        }

        return leftParts.Length.CompareTo(rightParts.Length);
    }
}
