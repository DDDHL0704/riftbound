using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Riftbound.GodotClient.Ui;

public partial class PlayCardOverlay
{
    public bool TableMode { get; set; }
    public event Action? TableSelectionChanged;
    public bool IsSubmitting => _submitting;
    public IEnumerable<string> TableTargets => _submitting ? [] :
        (_targets.FirstOrDefault(target => target.Picker.Selected <= 0).Ids ?? _targets.FirstOrDefault().Ids ?? []).Where(id => id.Length > 0);
    public IEnumerable<string> TableDestinations => _submitting ? [] : _destinations;
    public string? TableDestination => _destination is { Selected: >= 0 } ? _destinations[_destination.Selected] : null;
    public IEnumerable<string> TableSelectedObjects => _requirements.Count == 0 ? [] :
        _targets.Where(target => target.Picker.Selected > 0).Select(target => target.Ids[target.Picker.Selected])
            .Prepend(Text(_requirements[_source.Selected], "sourceObjectId"));

    public bool TrySelectTableObject(string objectId)
    {
        if (!Visible || _submitting || string.IsNullOrEmpty(objectId)) return false;
        var target = _targets.FirstOrDefault(choice => choice.Picker.Selected <= 0 && choice.Ids.Contains(objectId, StringComparer.Ordinal));
        if (target.Picker is null) target = _targets.FirstOrDefault(choice => choice.Ids.Contains(objectId, StringComparer.Ordinal));
        if (target.Picker is null) return false;
        target.Picker.Select(Array.IndexOf(target.Ids, objectId)); Refresh(); return true;
    }

    public bool TrySelectTableDestination(string id)
    {
        if (!Visible || _submitting || _destination is null) return false;
        var index = Array.IndexOf(_destinations, id);
        if (index < 0) return false;
        _destination.Select(index); Refresh(); return true;
    }

    private void BuildTableUi()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var panel = new PanelContainer(); AddChild(panel); panel.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var layout = MatchTableLayout.Column(panel, 8);
        MatchTableLayout.Label(layout, "打出卡牌", 22, MinimalTheme.Selected);
        MatchTableLayout.Label(layout, "直接点击牌桌上的目标或入场位置", 12, MinimalTheme.TextSecondary);
        _origin = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart }; layout.AddChild(_origin);
        _source = new OptionButton { FitToLongestItem = false, CustomMinimumSize = new Vector2(0, 40) }; layout.AddChild(_source);
        _source.ItemSelected += _ => Rebuild();
        _preview = GD.Load<PackedScene>("res://scenes/components/OfficialCardView.tscn").Instantiate<OfficialCardView>();
        _preview.CustomMinimumSize = new Vector2(88, 122); _preview.SizeFlagsHorizontal = SizeFlags.ShrinkCenter; layout.AddChild(_preview);
        var costScroll = new ScrollContainer { CustomMinimumSize = new Vector2(0, 104), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        layout.AddChild(costScroll);
        _cost = MatchTableLayout.Label(costScroll, "", 13, MinimalTheme.Text, true);
        var options = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        layout.AddChild(options); _choices = MatchTableLayout.Column(options, 6); _choices.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _status = MatchTableLayout.Label(layout, "", 13, MinimalTheme.TextSecondary, true);
        var footer = MatchTableLayout.Row(layout);
        _cancel = new Button { Text = "取消  Esc", SizeFlagsHorizontal = SizeFlags.ExpandFill }; footer.AddChild(_cancel); _cancel.Pressed += Hide;
        _confirm = new Button { Name = "ConfirmPlayCardButton", Text = "确认打出", SizeFlagsHorizontal = SizeFlags.ExpandFill }; footer.AddChild(_confirm);
        _confirm.Pressed += Submit;
        MinimalTheme.Apply(panel);
    }
}
