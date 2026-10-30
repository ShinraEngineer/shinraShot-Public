// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Shinra CC

namespace ShinraShot.Core.Capture;

/// <summary>
/// How a finished capture is handled before any menu is shown. Part of the persisted
/// settings schema (see <c>ShinraShot.Core.Settings.AppSettings</c>); the App layer's
/// capture flow applies it.
/// </summary>
public enum DefaultCaptureAction
{
    /// <summary>Show the post-capture action menu (Greenshot default).</summary>
    Ask,

    /// <summary>Open the capture straight in the editor.</summary>
    OpenInEditor,

    /// <summary>Copy the capture to the system clipboard.</summary>
    CopyToClipboard,

    /// <summary>Auto-save the capture to the configured folder.</summary>
    SaveToFolder,
}
