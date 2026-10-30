// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Shinra CC

using ShinraShot.App.Editor.Model;

namespace ShinraShot.App.Editor;

/// <summary>
/// The editable annotation document: the ordered element list plus an undo/redo history
/// kept as deep-cloned snapshots (simple and reliable for the element counts involved).
/// </summary>
public sealed class EditorDocument
{
    public EditorDocument(int width, int height)
    {
        Width = width;
        Height = height;
    }

    public int Width { get; }
    public int Height { get; }

    public List<AnnotationElement> Elements { get; private set; } = new();

    private readonly Stack<List<AnnotationElement>> _undo = new();
    private readonly Stack<List<AnnotationElement>> _redo = new();

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    /// <summary>Snapshot the current elements before a mutating operation.</summary>
    public void PushUndo()
    {
        _undo.Push(CloneList(Elements));
        _redo.Clear();
    }

    public void Undo()
    {
        if (_undo.Count == 0) return;
        _redo.Push(CloneList(Elements));
        Elements = _undo.Pop();
    }

    public void Redo()
    {
        if (_redo.Count == 0) return;
        _undo.Push(CloneList(Elements));
        Elements = _redo.Pop();
    }

    private static List<AnnotationElement> CloneList(List<AnnotationElement> src)
    {
        var list = new List<AnnotationElement>(src.Count);
        foreach (var e in src) list.Add(e.Clone());
        return list;
    }
}
