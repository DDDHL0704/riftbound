using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Riftbound.GodotClient.Ui;

public partial class MulliganOverlay : Control
{
    public event Action<IReadOnlyList<string>>? Confirmed;
    public event Action? Cancelled;

    private readonly List<string> _sourceIds = [];
    private readonly Dictionary<string, Godot.Collections.Dictionary> _cardsBySourceId = new(StringComparer.Ordinal);
    private readonly HashSet<string> _selectedSourceIds = new(StringComparer.Ordinal);
    private PackedScene _officialCardScene = null!;
    private HBoxContainer _cards = null!;
    private Label _summary = null!;
    private Button _cancelButton = null!;
    private Button _confirmButton = null!;
    private int _minSelectionCount;
    private int _maxSelectionCount;
    private bool _canUsePrompt;

    public IReadOnlyList<string> SelectedObjectIds => _sourceIds.Where(_selectedSourceIds.Contains).ToArray();

    public bool CanUsePrompt => _canUsePrompt;

    public override void _Ready()
    {
        _cards = GetNode<HBoxContainer>("%MulliganCards");
        _summary = GetNode<Label>("%SelectionSummary");
        _cancelButton = GetNode<Button>("%CancelButton");
        _confirmButton = GetNode<Button>("%ConfirmButton");
        _officialCardScene = GD.Load<PackedScene>("res://scenes/components/OfficialCardView.tscn")
            ?? throw new InvalidOperationException("OfficialCardView scene is missing.");
        _cancelButton.Pressed += Cancel;
        _confirmButton.Pressed += Confirm;

        ApplyTheme();
        HidePrompt();
    }

    public void ApplyTheme()
    {
        MinimalTheme.Apply(this);
        GetNode<PanelContainer>("%MulliganPanel")
            .AddThemeStyleboxOverride("panel", MinimalTheme.Panel(MinimalTheme.SurfaceRaised));
        GetNode<Label>("%MulliganTitle").AddThemeFontSizeOverride("font_size", 24);
        _summary?.AddThemeColorOverride("font_color", MinimalTheme.TextSecondary);
    }

    public bool ShowPrompt(
        Godot.Collections.Dictionary action,
        IReadOnlyList<Godot.Collections.Dictionary> visibleHandCards,
        out string reason)
    {
        reason = string.Empty;
        Reset();
        var enabled = ReadBool(action, "enabled");
        _minSelectionCount = ReadInt(action, "minSelectionCount", -1);
        _maxSelectionCount = ReadInt(action, "maxSelectionCount", -1);
        if (_minSelectionCount < 0 || _maxSelectionCount < _minSelectionCount)
        {
            reason = "minSelectionCount or maxSelectionCount is missing";
            ShowDisabled();
            return false;
        }

        foreach (var choice in ReadChoices(action, "sourceChoices"))
        {
            var sourceId = ReadString(choice, "id");
            if (string.IsNullOrWhiteSpace(sourceId) || _sourceIds.Contains(sourceId, StringComparer.Ordinal))
            {
                reason = "sourceChoices is missing a unique source id";
                ShowDisabled();
                return false;
            }

            _sourceIds.Add(sourceId);
        }

        if (_sourceIds.Count == 0)
        {
            reason = "sourceChoices is missing";
            ShowDisabled();
            return false;
        }

        foreach (var card in visibleHandCards)
        {
            var sourceId = ReadString(card, "objectId");
            if (_sourceIds.Contains(sourceId, StringComparer.Ordinal)
                && ReadBool(card, "visible")
                && !ReadBool(card, "faceDown"))
            {
                _cardsBySourceId[sourceId] = card.Duplicate(true);
            }
        }

        if (_sourceIds.Any(sourceId => !_cardsBySourceId.ContainsKey(sourceId)))
        {
            reason = "visible hand card is missing for a server mulligan source";
            ShowDisabled();
            return false;
        }

        _canUsePrompt = enabled;
        RenderCards();
        Visible = true;
        MoveToFront();
        Refresh();
        FocusFirstControl();
        return enabled;
    }

    public void HidePrompt()
    {
        Reset();
        Visible = false;
    }

    public bool ConfirmCurrent()
    {
        if (!_canUsePrompt || _confirmButton.Disabled)
        {
            return false;
        }

        Confirm();
        return true;
    }

    public void ResetSelection()
    {
        Cancel();
    }

    private void RenderCards()
    {
        ClearChildren(_cards);
        foreach (var sourceId in _sourceIds)
        {
            var cardView = _officialCardScene.Instantiate<OfficialCardView>();
            cardView.CustomMinimumSize = new Vector2(154, 215);
            cardView.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
            cardView.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
            cardView.Activated += _ => Toggle(sourceId);
            _cards.AddChild(cardView);
            cardView.Display(
                _cardsBySourceId[sourceId],
                _selectedSourceIds.Contains(sourceId) ? OfficialCardVisualState.Selected : OfficialCardVisualState.Selectable);
        }
    }

    private void Toggle(string sourceId)
    {
        if (!_canUsePrompt)
        {
            return;
        }

        if (_selectedSourceIds.Contains(sourceId))
        {
            _selectedSourceIds.Remove(sourceId);
        }
        else if (_selectedSourceIds.Count < _maxSelectionCount)
        {
            _selectedSourceIds.Add(sourceId);
        }

        RenderCards();
        Refresh();
    }

    private void Refresh()
    {
        var count = _selectedSourceIds.Count;
        _summary.Text = $"选择 {count} 张（可选 {_minSelectionCount} 至 {_maxSelectionCount} 张）";
        _confirmButton.Disabled = !_canUsePrompt || count < _minSelectionCount || count > _maxSelectionCount;
        _cancelButton.Disabled = false;
    }

    private void FocusFirstControl()
    {
        var firstCard = _cards.GetChildren().OfType<OfficialCardView>().FirstOrDefault();
        if (firstCard is not null)
        {
            firstCard.GrabFocus();
            return;
        }

        _cancelButton.GrabFocus();
    }

    private void ShowDisabled()
    {
        _canUsePrompt = false;
        ClearChildren(_cards);
        _summary.Text = "服务端尚未提供可用的起手选择数据。";
        _confirmButton.Disabled = true;
        _cancelButton.Disabled = false;
        Visible = true;
        MoveToFront();
    }

    private void Confirm()
    {
        if (!_canUsePrompt || _confirmButton.Disabled)
        {
            return;
        }

        Confirmed?.Invoke(SelectedObjectIds);
    }

    private void Cancel()
    {
        HidePrompt();
        Cancelled?.Invoke();
    }

    private void Reset()
    {
        _sourceIds.Clear();
        _cardsBySourceId.Clear();
        _selectedSourceIds.Clear();
        _minSelectionCount = 0;
        _maxSelectionCount = -1;
        _canUsePrompt = false;
        if (IsNodeReady())
        {
            ClearChildren(_cards);
            _summary.Text = string.Empty;
            _confirmButton.Disabled = true;
            _cancelButton.Disabled = false;
        }
    }

    private static IEnumerable<Godot.Collections.Dictionary> ReadChoices(Godot.Collections.Dictionary action, string key)
    {
        if (action.TryGetValue(key, out var value)
            && value.As<Godot.Collections.Array<Godot.Collections.Dictionary>>() is { } choices)
        {
            return choices;
        }

        return [];
    }

    private static string ReadString(Godot.Collections.Dictionary source, string key)
    {
        return source.TryGetValue(key, out var value) ? value.AsString() : string.Empty;
    }

    private static bool ReadBool(Godot.Collections.Dictionary source, string key)
    {
        return source.TryGetValue(key, out var value) && value.AsBool();
    }

    private static int ReadInt(Godot.Collections.Dictionary source, string key, int fallback)
    {
        return source.TryGetValue(key, out var value) ? value.AsInt32() : fallback;
    }

    private static void ClearChildren(Node parent)
    {
        foreach (var child in parent.GetChildren())
        {
            parent.RemoveChild(child);
            child.QueueFree();
        }
    }
}
