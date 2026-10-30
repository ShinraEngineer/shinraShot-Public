// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Shinra CC

using System.IO;

namespace ShinraShot.App.Capture;

/// <summary>What to do with a grab once pixels are in hand. (Settings UI lands in slice 5.)</summary>
public sealed class CaptureOptions
{
    /// <summary>Write a PNG to <see cref="SaveDirectory"/> using <see cref="FileNamePattern"/>.</summary>
    public bool SaveToFolder { get; set; } = true;

    /// <summary>Also place the image on the system clipboard.</summary>
    public bool CopyToClipboard { get; set; } = true;

    /// <summary>Target folder for "save to folder" / "save directly".</summary>
    public string SaveDirectory { get; set; } = DefaultDirectory;

    /// <summary><see cref="string.Format(string,object?)"/> pattern; arg 0 is the capture time.</summary>
    public string FileNamePattern { get; set; } = "ShinraShot_{0:yyyy-MM-dd_HH-mm-ss}.png";

    public string BuildFileName() => string.Format(FileNamePattern, DateTime.Now);

    /// <summary>Default save location: <c>Pictures\ShinraShot</c>.</summary>
    public static string DefaultDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "ShinraShot");
}
