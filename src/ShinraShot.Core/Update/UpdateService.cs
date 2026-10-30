// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Shinra CC

using System.Security.Cryptography;
using System.Text.Json;

namespace ShinraShot.Core.Update;

/// <summary>
/// Over-the-network update checker. Fetches the published <c>update.json</c> manifest, compares its
/// version against the running build, and (when newer) hands back the download for this platform.
/// A second call downloads that file, with progress and an optional checksum check. No install is
/// performed here; the UI opens the downloaded exe/package so the user stays in control.
/// </summary>
public sealed class UpdateService
{
    /// <summary>
    /// Stable public manifest URL. GitHub's <c>releases/latest/download/&lt;asset&gt;</c> always
    /// resolves to the newest non-pre-release release's asset, so this never needs editing per
    /// release and is reachable without authentication.
    /// </summary>
    public const string DefaultFeedUrl =
        "https://github.com/ShinraEngineer/shinraShot-Public/releases/latest/download/update.json";

    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    private static readonly HttpClient Http = CreateClient();

    private readonly string _feedUrl;
    private readonly string _currentVersion;

    public UpdateService(string? feedUrl = null, string? currentVersion = null)
    {
        // Precedence: explicit argument, then the SHINRASHOT_UPDATE_FEED override, then the default.
        _feedUrl = !string.IsNullOrWhiteSpace(feedUrl) ? feedUrl!
            : Environment.GetEnvironmentVariable("SHINRASHOT_UPDATE_FEED") is { Length: > 0 } env ? env
            : DefaultFeedUrl;
        _currentVersion = string.IsNullOrWhiteSpace(currentVersion) ? AppInfo.Version : currentVersion!;
    }

    private static HttpClient CreateClient()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd($"{AppInfo.Name}/{AppInfo.Version}");
        return http;
    }

    /// <summary>Fetch the manifest and decide whether a newer build is available for this platform.</summary>
    public async Task<UpdateCheckResult> CheckAsync(bool includePreRelease = false, CancellationToken ct = default)
    {
        UpdateManifest? manifest;
        try
        {
            await using Stream stream = await Http.GetStreamAsync(_feedUrl, ct);
            manifest = await JsonSerializer.DeserializeAsync<UpdateManifest>(stream, JsonOpts, ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return UpdateCheckResult.Failed(_currentVersion, "Could not reach the update server. " + ex.Message);
        }

        if (manifest is null || string.IsNullOrWhiteSpace(manifest.Version))
            return UpdateCheckResult.Failed(_currentVersion, "The update manifest was missing or invalid.");

        bool newer = VersionCompare.IsNewer(manifest.Version, _currentVersion, includePreRelease);
        UpdateAsset? asset = SelectAsset(manifest);

        return new UpdateCheckResult
        {
            Success = true,
            UpdateAvailable = newer && asset is not null,
            CurrentVersion = _currentVersion,
            LatestVersion = manifest.Version,
            Notes = manifest.Notes,
            DownloadUrl = asset?.Url,
            DownloadFileName = asset is null ? null : FileNameFromUrl(asset.Url),
            DownloadSize = asset?.Size ?? 0,
            Sha256 = asset?.Sha256,
            Message = newer && asset is null
                ? "A newer version is out, but no download is listed for this platform."
                : null,
        };
    }

    /// <summary>
    /// Download the update to <paramref name="targetDir"/>, reporting progress in [0,1], and verify
    /// the checksum when the manifest carries one. Returns the full path of the downloaded file.
    /// </summary>
    public async Task<string> DownloadAsync(
        UpdateCheckResult result, string targetDir, IProgress<double>? progress = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(result.DownloadUrl))
            throw new InvalidOperationException("This update has no download URL.");

        Directory.CreateDirectory(targetDir);
        string dest = Path.Combine(targetDir, result.DownloadFileName ?? "ShinraShot-update");

        using (HttpResponseMessage resp = await Http.GetAsync(result.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, ct))
        {
            resp.EnsureSuccessStatusCode();
            long? total = resp.Content.Headers.ContentLength ?? (result.DownloadSize > 0 ? result.DownloadSize : null);

            await using Stream src = await resp.Content.ReadAsStreamAsync(ct);
            await using var dst = new FileStream(dest, FileMode.Create, FileAccess.Write, FileShare.None);

            var buffer = new byte[81920];
            long read = 0;
            int n;
            while ((n = await src.ReadAsync(buffer, ct)) > 0)
            {
                await dst.WriteAsync(buffer.AsMemory(0, n), ct);
                read += n;
                if (total is > 0)
                    progress?.Report(Math.Clamp((double)read / total.Value, 0, 1));
            }
        }

        if (!string.IsNullOrWhiteSpace(result.Sha256))
        {
            string actual = await Sha256HexAsync(dest, ct);
            if (!string.Equals(actual, result.Sha256!.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                TryDelete(dest);
                throw new InvalidOperationException("The downloaded file failed its checksum check and was discarded.");
            }
        }

        progress?.Report(1);
        return dest;
    }

    private static UpdateAsset? SelectAsset(UpdateManifest manifest)
    {
        foreach (string key in UpdatePlatform.PreferredKeys())
            if (manifest.Assets.TryGetValue(key, out UpdateAsset? asset) && !string.IsNullOrWhiteSpace(asset.Url))
                return asset;
        return null;
    }

    private static string FileNameFromUrl(string url)
    {
        try
        {
            string path = new Uri(url).AbsolutePath;
            string name = Path.GetFileName(path);
            return string.IsNullOrEmpty(name) ? "ShinraShot-update" : name;
        }
        catch
        {
            return "ShinraShot-update";
        }
    }

    private static async Task<string> Sha256HexAsync(string path, CancellationToken ct)
    {
        await using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        byte[] hash = await SHA256.HashDataAsync(fs, ct);
        return Convert.ToHexStringLower(hash);
    }

    private static void TryDelete(string path)
    {
        try { File.Delete(path); } catch { /* best effort */ }
    }
}
