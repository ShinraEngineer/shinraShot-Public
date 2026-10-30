// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Shinra CC

using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace ShinraShot.App.Capture;

/// <summary>Shared capture destinations used by both the action menu and the editor.</summary>
public static class ImageSink
{
    /// <summary>Show a Save-As dialog over <paramref name="owner"/> and write the PNG; returns the path, or null if cancelled.</summary>
    public static async Task<string?> SaveAsAsync(TopLevel owner, byte[] png, string? suggestedName = null)
    {
        var file = await owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save screenshot",
            SuggestedFileName = suggestedName ?? $"ShinraShot_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.png",
            DefaultExtension = "png",
            ShowOverwritePrompt = true,
            FileTypeChoices = new[]
            {
                new FilePickerFileType("PNG image") { Patterns = new[] { "*.png" }, MimeTypes = new[] { "image/png" } },
            },
        });

        if (file is null) return null;

        await using var stream = await file.OpenWriteAsync();
        await stream.WriteAsync(png);
        return file.TryGetLocalPath() ?? file.Name;
    }
}
