// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Shinra CC

namespace ShinraShot.Core.Update;

/// <summary>
/// Picks which manifest asset applies to the running OS. Windows wants the self-contained exe;
/// Linux wants the package matching the distro family (.rpm on RHEL/Fedora/SUSE, .deb on
/// Debian/Ubuntu), falling back to the other if the preferred one is absent from the manifest.
/// </summary>
public static class UpdatePlatform
{
    public const string Windows = "windows";
    public const string LinuxDeb = "linux-deb";
    public const string LinuxRpm = "linux-rpm";

    /// <summary>Asset keys to try, in order of preference, for the current OS.</summary>
    public static IReadOnlyList<string> PreferredKeys()
    {
        if (OperatingSystem.IsWindows())
            return new[] { Windows };

        if (OperatingSystem.IsLinux())
            return PrefersRpm() ? new[] { LinuxRpm, LinuxDeb } : new[] { LinuxDeb, LinuxRpm };

        return Array.Empty<string>();
    }

    private static bool PrefersRpm()
    {
        try
        {
            foreach (string line in File.ReadLines("/etc/os-release"))
            {
                if (!line.StartsWith("ID=") && !line.StartsWith("ID_LIKE="))
                    continue;

                string value = line.Split('=', 2)[1].Trim().Trim('"').ToLowerInvariant();
                if (value.Contains("rhel") || value.Contains("fedora") || value.Contains("centos") || value.Contains("suse"))
                    return true;
                if (value.Contains("debian") || value.Contains("ubuntu"))
                    return false;
            }
        }
        catch
        {
            // Unreadable os-release: fall through to the .deb default.
        }

        return false;
    }
}
