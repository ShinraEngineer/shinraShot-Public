// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Shinra CC

namespace ShinraShot.Core.Update;

/// <summary>
/// Outcome of an update check: whether the manifest was reached, whether a newer build exists for
/// this platform, and the download to fetch if so.
/// </summary>
public sealed class UpdateCheckResult
{
    /// <summary>True when the manifest was fetched and parsed. False means a network/parse error.</summary>
    public bool Success { get; init; }

    /// <summary>True when a newer build exists and has a download for this platform.</summary>
    public bool UpdateAvailable { get; init; }

    public string CurrentVersion { get; init; } = string.Empty;
    public string? LatestVersion { get; init; }
    public string? Notes { get; init; }

    public string? DownloadUrl { get; init; }
    public string? DownloadFileName { get; init; }
    public long DownloadSize { get; init; }
    public string? Sha256 { get; init; }

    /// <summary>A human-readable note for the UI (an error reason, or "no build for this platform").</summary>
    public string? Message { get; init; }

    public static UpdateCheckResult Failed(string currentVersion, string message) =>
        new() { Success = false, CurrentVersion = currentVersion, Message = message };
}
