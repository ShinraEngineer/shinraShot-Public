// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Shinra CC

using System.Text.Json;
using System.Text.Json.Serialization;

namespace ShinraShot.Core.Settings;

/// <summary>
/// Loads and saves <see cref="AppSettings"/> as JSON. Reads are fail-safe: a missing or
/// corrupt file yields defaults rather than throwing, so a bad edit never blocks startup.
/// Writes are atomic (temp file + replace) so a crash mid-write cannot truncate the file.
/// </summary>
public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions s_json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters =
        {
            new JsonStringEnumConverter(),
            new HotkeyJsonConverter(),
        },
    };

    /// <summary>Absolute path of the settings file this store reads and writes.</summary>
    public string FilePath { get; }

    /// <summary>Use the default per-user location, or an explicit path (tests, diagnostics).</summary>
    public SettingsStore(string? filePath = null) => FilePath = filePath ?? SettingsPaths.SettingsFile;

    /// <summary>Load settings, or return defaults if the file is absent or unreadable.</summary>
    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(FilePath))
                return new AppSettings();

            string json = File.ReadAllText(FilePath);
            return JsonSerializer.Deserialize<AppSettings>(json, s_json) ?? new AppSettings();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return new AppSettings();
        }
    }

    /// <summary>Serialize and persist settings, creating the config directory as needed.</summary>
    public void Save(AppSettings settings)
    {
        string dir = Path.GetDirectoryName(FilePath)!;
        Directory.CreateDirectory(dir);

        string json = JsonSerializer.Serialize(settings, s_json);
        string tmp = FilePath + ".tmp";
        File.WriteAllText(tmp, json);
        File.Move(tmp, FilePath, overwrite: true);
    }
}
