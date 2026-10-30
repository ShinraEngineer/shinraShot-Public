// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Shinra CC

using System.Diagnostics;
using System.Runtime.Versioning;
using ShinraShot.Core.Imaging;

namespace ShinraShot.Core.Platform.Linux;

/// <summary>
/// Puts an image on the system clipboard as <c>image/png</c> by handing the encoded PNG to
/// the platform's clipboard helper — <c>wl-copy</c> on Wayland, else <c>xclip</c> on X11.
///
/// Those helpers are the pragmatic, correct mechanism on Linux: an X11 selection owner must
/// stay resident to serve paste requests, which <c>xclip</c>/<c>wl-copy</c> do by forking a
/// background process. We feed the PNG on stdin and deliberately do not wait for the child —
/// it lives on (reparented) to own the selection after ShinraShot exits.
///
/// PNG bytes are required: Core has no image encoder, and the app already encodes the PNG
/// (via Skia) before copying, so they are always supplied in practice.
/// </summary>
[SupportedOSPlatform("linux")]
public sealed class LinuxClipboardImage : IClipboardImage
{
    public void SetImage(CapturedImage image, byte[]? pngBytes = null)
    {
        if (pngBytes is not { Length: > 0 })
            throw new InvalidOperationException(
                "The Linux clipboard needs encoded PNG bytes; none were supplied.");

        var (exe, args) = ResolveHelper();

        var psi = new ProcessStartInfo(exe)
        {
            RedirectStandardInput = true,
            UseShellExecute = false,
        };
        foreach (string a in args)
            psi.ArgumentList.Add(a);

        Process proc;
        try
        {
            proc = Process.Start(psi)
                   ?? throw new InvalidOperationException($"Could not start '{exe}'.");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to run the clipboard helper '{exe}'. Install 'xclip' (X11) or " +
                "'wl-clipboard' (Wayland).", ex);
        }

        // Write the PNG and close stdin (EOF tells the helper it has the whole image).
        // Do not WaitForExit: the helper stays alive to own the selection.
        using var stdin = proc.StandardInput.BaseStream;
        stdin.Write(pngBytes, 0, pngBytes.Length);
        stdin.Flush();
    }

    /// <summary>Pick the clipboard helper: wl-copy on Wayland, otherwise xclip.</summary>
    private static (string Exe, string[] Args) ResolveHelper()
    {
        bool wayland = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY"));

        if (wayland && OnPath("wl-copy"))
            return ("wl-copy", new[] { "--type", "image/png" });
        if (OnPath("xclip"))
            return ("xclip", new[] { "-selection", "clipboard", "-t", "image/png" });
        if (OnPath("wl-copy"))   // last resort on X11 if that is all there is
            return ("wl-copy", new[] { "--type", "image/png" });

        throw new InvalidOperationException(
            "No clipboard helper found. Install 'xclip' (X11) or 'wl-clipboard' (Wayland).");
    }

    /// <summary>True when <paramref name="name"/> is an executable on <c>$PATH</c>.</summary>
    private static bool OnPath(string name)
    {
        string? path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(path))
            return false;
        foreach (string dir in path.Split(':', StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                string candidate = System.IO.Path.Combine(dir, name);
                if (File.Exists(candidate))
                    return true;
            }
            catch { /* skip unreadable PATH entries */ }
        }
        return false;
    }
}
