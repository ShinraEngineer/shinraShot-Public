// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Shinra CC

namespace ShinraShot.Core.Settings;

/// <summary>
/// Resolves where ShinraShot keeps its per-user configuration:
/// <list type="bullet">
///   <item><description>Windows: <c>%APPDATA%\ShinraShot</c></description></item>
///   <item><description>Linux: <c>$XDG_CONFIG_HOME/shinrashot</c>, else <c>~/.config/shinrashot</c></description></item>
/// </list>
/// </summary>
public static class SettingsPaths
{
    /// <summary>The ShinraShot configuration directory for the current user (not created here).</summary>
    public static string ConfigDirectory
    {
        get
        {
            if (OperatingSystem.IsLinux())
            {
                string? xdg = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
                string baseDir = !string.IsNullOrWhiteSpace(xdg)
                    ? xdg
                    : Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
                return Path.Combine(baseDir, "shinrashot");
            }

            // Windows (and any other host): roaming application data.
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppInfo.Name);
        }
    }

    /// <summary>Absolute path of the settings file (<c>settings.json</c>).</summary>
    public static string SettingsFile => Path.Combine(ConfigDirectory, "settings.json");
}
