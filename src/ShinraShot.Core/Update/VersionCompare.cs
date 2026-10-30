// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Shinra CC

namespace ShinraShot.Core.Update;

/// <summary>
/// Semantic-version comparison for update checks. Ported from the sibling app's update logic so
/// the two behave the same: a leading "v" is ignored, a GA release outranks a pre-release of the
/// same base version, and pre-releases are only ever considered newer when the early-access
/// channel is on.
/// </summary>
public static class VersionCompare
{
    /// <summary>
    /// True when <paramref name="candidate"/> is a newer version than <paramref name="current"/>.
    /// When <paramref name="includePreRelease"/> is false, a pre-release candidate is never newer.
    /// </summary>
    public static bool IsNewer(string candidate, string current, bool includePreRelease = false)
    {
        var (baseA, preA) = Parse(candidate);
        var (baseB, preB) = Parse(current);

        // A pre-release candidate is ignored unless the early-access channel is on.
        if (!includePreRelease && preA is not null)
            return false;

        int max = Math.Max(baseA.Length, baseB.Length);
        for (int i = 0; i < max; i++)
        {
            int a = i < baseA.Length ? baseA[i] : 0;
            int b = i < baseB.Length ? baseB[i] : 0;
            if (a > b) return true;
            if (a < b) return false;
        }

        // Base versions equal: GA outranks a pre-release; otherwise compare the numeric suffix.
        if (preA is null && preB is not null) return true;   // GA newer than RC of same base
        if (preA is not null && preB is null) return false;  // RC older than GA of same base
        if (preA is null && preB is null) return false;      // identical GA versions
        return PreReleaseNumber(preA!) > PreReleaseNumber(preB!);
    }

    private static (int[] baseParts, string? pre) Parse(string version)
    {
        string v = (version ?? string.Empty).Trim();
        if (v.StartsWith('v') || v.StartsWith('V'))
            v = v[1..];

        int dash = v.IndexOf('-');
        string basePart = dash >= 0 ? v[..dash] : v;
        string? pre = dash >= 0 ? v[(dash + 1)..] : null;

        int[] parts = basePart.Split('.')
            .Select(p => int.TryParse(p, out int n) ? n : 0)
            .ToArray();

        return (parts, string.IsNullOrEmpty(pre) ? null : pre);
    }

    // "rc.2" -> 2, "beta" -> 0. Good enough for the simple "channel.N" tags the releases use.
    private static int PreReleaseNumber(string pre)
    {
        string[] parts = pre.Split('.');
        return parts.Length > 1 && int.TryParse(parts[1], out int n) ? n : 0;
    }
}
