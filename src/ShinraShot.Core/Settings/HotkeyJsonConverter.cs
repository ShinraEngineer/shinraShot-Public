// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Shinra CC

using System.Text.Json;
using System.Text.Json.Serialization;

namespace ShinraShot.Core.Settings;

/// <summary>
/// Serializes a <see cref="Hotkey"/> as a human-readable string (e.g. <c>"Ctrl+PrintScreen"</c>)
/// so <c>settings.json</c> stays legible and hand-editable. An empty string is the unbound hotkey.
/// </summary>
public sealed class HotkeyJsonConverter : JsonConverter<Hotkey>
{
    public override Hotkey Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        string? text = reader.TokenType == JsonTokenType.Null ? null : reader.GetString();
        // Be forgiving: an unparseable stored value falls back to unbound rather than throwing.
        return Hotkey.TryParse(text, out var hk) ? hk : Hotkey.None;
    }

    public override void Write(Utf8JsonWriter writer, Hotkey value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToString());
}
