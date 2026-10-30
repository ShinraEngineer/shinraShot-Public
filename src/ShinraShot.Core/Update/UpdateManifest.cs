// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Shinra CC

using System.Text.Json.Serialization;

namespace ShinraShot.Core.Update;

/// <summary>
/// The published <c>update.json</c> manifest. One per release, hosted at a stable public URL
/// (the public repo's <c>releases/latest/download/update.json</c>), listing the latest version
/// and a download per platform. This mirrors the manifest-driven update flow used by the sibling
/// apps (Mako keeps the same <c>update.json</c> shape in its data folder).
/// </summary>
public sealed class UpdateManifest
{
    /// <summary>Latest released version, e.g. "0.2.0". A leading "v" is tolerated.</summary>
    [JsonPropertyName("version")] public string Version { get; set; } = string.Empty;

    /// <summary>Release date (ISO, informational only).</summary>
    [JsonPropertyName("published")] public string? Published { get; set; }

    /// <summary>Short human-readable release notes shown next to the download.</summary>
    [JsonPropertyName("notes")] public string? Notes { get; set; }

    /// <summary>Per-platform download, keyed by platform id ("windows", "linux-deb", "linux-rpm").</summary>
    [JsonPropertyName("assets")] public Dictionary<string, UpdateAsset> Assets { get; set; } = new();
}

/// <summary>One downloadable artifact in an <see cref="UpdateManifest"/>.</summary>
public sealed class UpdateAsset
{
    /// <summary>Absolute download URL.</summary>
    [JsonPropertyName("url")] public string Url { get; set; } = string.Empty;

    /// <summary>Size in bytes, used to drive a progress bar when the server omits Content-Length.</summary>
    [JsonPropertyName("size")] public long Size { get; set; }

    /// <summary>Lower-case hex SHA-256, verified after download when present.</summary>
    [JsonPropertyName("sha256")] public string? Sha256 { get; set; }
}
