// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Shinra CC

namespace ShinraShot.Core;

/// <summary>
/// Build- and brand-level constants shared across every ShinraShot assembly.
/// </summary>
public static class AppInfo
{
    /// <summary>Display name of the product.</summary>
    public const string Name = "ShinraShot";

    /// <summary>Publisher / vendor name.</summary>
    public const string Vendor = "Shinra CC";

    /// <summary>Marketing version (kept in sync with <c>Directory.Build.props</c>).</summary>
    public const string Version = "0.2.0";

    /// <summary>Reverse-DNS-ish application id, used for config dirs and desktop files.</summary>
    public const string AppId = "cc.shinra.shinrashot";
}
