using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Godot;
using Riftbound.Contracts;

namespace Riftbound.GodotClient.Ui;

/// <summary>Composes an intent from the selected card's server-authored choices.</summary>
public partial class PlayCardOverlay : Control
{
    public event Action<Dictionary<string, object?>>? Confirmed;
    public event Action<PlayCostPreviewRequestDto>? PreviewRequested;
    public string PromptId { get; private set; } = string.Empty;
    public long SnapshotTick { get; private set; } = -1;
    private OptionButton _source = null!;
    private VBoxContainer _choices = null!;
    private Label _cost = null!;
    private Label _status = null!;
    private Button _confirm = null!;
    private OfficialCardView _preview = null!;
    private readonly List<JsonElement> _requirements = [];
    private readonly List<(OptionButton Picker, string[] Ids, bool Required)> _targets = [];
    private readonly Dictionary<string, CheckBox> _optional = new(StringComparer.Ordinal);
    private OptionButton? _destination;
    private string[] _destinations = [];
    private OptionButton? _printed;
    private string[] _printedChoices = [];
    private Func<string, Godot.Collections.Dictionary?>? _cardView;
    private bool _composable;
    private bool _submitting;
    private string _quoteRequestId = string.Empty;
    private PlayCostQuoteDto? _quote;
    private string _rejection = string.Empty;
    private int _powerShortfall;
    private readonly HashSet<string> _resourceIds = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _resourcePower = new(StringComparer.Ordinal);
    private string[][] _legalSelections = [];

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        ZIndex = 100;
        MouseFilter = MouseFilterEnum.Stop;
        var shade = new ColorRect { Color = new Color(0.02f, 0.03f, 0.05f, 0.9f) };
        AddChild(shade); shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var center = new CenterContainer(); AddChild(center); center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(990, 600) }; center.AddChild(panel);
        var margin = new MarginContainer(); panel.AddChild(margin);
        foreach (var edge in new[] { "left", "right", "top", "bottom" }) margin.AddThemeConstantOverride("margin_" + edge, 24);
        var layout = new VBoxContainer(); layout.AddThemeConstantOverride("separation", 14); margin.AddChild(layout);
        var title = new Label { Text = "打出卡牌" }; title.AddThemeFontSizeOverride("font_size", 26); layout.AddChild(title);
        var body = new HBoxContainer(); body.AddThemeConstantOverride("separation", 28); layout.AddChild(body);
        var left = new VBoxContainer { CustomMinimumSize = new Vector2(240, 0) }; body.AddChild(left);
        _preview = GD.Load<PackedScene>("res://scenes/components/OfficialCardView.tscn").Instantiate<OfficialCardView>();
        _preview.CustomMinimumSize = new Vector2(240, 334); left.AddChild(_preview);
        var costScroll = new ScrollContainer { CustomMinimumSize = new Vector2(240, 155), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        left.AddChild(costScroll);
        _cost = new Label { CustomMinimumSize = new Vector2(220, 0), SizeFlagsHorizontal = SizeFlags.ExpandFill, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        costScroll.AddChild(_cost);
        var right = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(630, 0) }; body.AddChild(right);
        _source = new OptionButton { CustomMinimumSize = new Vector2(0, 42), FitToLongestItem = false }; right.AddChild(_source);
        _source.ItemSelected += _ => Rebuild();
        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(0, 365), SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        right.AddChild(scroll);
        _choices = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; _choices.AddThemeConstantOverride("separation", 10); scroll.AddChild(_choices);
        _status = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart }; layout.AddChild(_status);
        var footer = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End }; layout.AddChild(footer);
        var cancel = new Button { Text = "取消  Esc", CustomMinimumSize = new Vector2(120, 42) }; footer.AddChild(cancel); cancel.Pressed += Hide;
        _confirm = new Button { Name = "ConfirmPlayCardButton", Text = "确认打出", CustomMinimumSize = new Vector2(160, 42) }; footer.AddChild(_confirm);
        _confirm.Pressed += Submit;
        MinimalTheme.Apply(panel); Hide();
    }

    public bool Open(JsonElement candidate, string promptId, long tick,
        Func<string, Godot.Collections.Dictionary?> cardView, string? sourceId = null)
    {
        if (!candidate.TryGetProperty("metadata", out var metadata)
            || !metadata.TryGetProperty("sourceRequirements", out var requirements)) return false;
        _requirements.Clear(); _source.Clear(); _cardView = cardView;
        foreach (var requirement in requirements.EnumerateArray())
        {
            if (sourceId is not null && Text(requirement, "sourceObjectId") != sourceId) continue;
            _requirements.Add(requirement.Clone());
            var mode = Text(requirement, "modeLabel");
            if (mode == "默认") mode = "";
            _source.AddItem(Text(requirement, "displayName") + (mode.Length > 0 ? " · " + mode : ""));
        }
        if (_requirements.Count == 0) return false;
        PromptId = promptId; SnapshotTick = tick;
        _submitting = false; _rejection = string.Empty; _quote = null;
        _source.Select(0); Rebuild(); Show(); _source.GrabFocus(); return true;
    }

    private void Rebuild()
    {
        foreach (var node in _choices.GetChildren()) { _choices.RemoveChild(node); node.QueueFree(); }
        _targets.Clear(); _optional.Clear(); _destination = null; _printed = null;
        var requirement = _requirements[_source.Selected];
        RefreshCardPreview();
        _cost.Text = $"卡面费用  {Number(requirement, "manaCost")} 法力 · {Number(requirement, "printedPowerCost")} 符能\n"
            + $"当前最低法力  {Number(requirement, "minimumManaCost")}\n\n可用资源  {Number(requirement, "availableMana")} 法力 · {Number(requirement, "availablePower")} 符能";
        _composable = !requirement.TryGetProperty("composable", out var composable) || composable.GetBoolean();
        _powerShortfall = Number(requirement, "minimumPrintedPowerShortfall");
        _legalSelections = requirement.TryGetProperty("legalTargetSelections", out var legal)
            && legal.ValueKind == JsonValueKind.Array
            ? legal.EnumerateArray().Select(selection => selection.EnumerateArray().Select(id => id.GetString() ?? "").ToArray()).ToArray() : [];
        if (Choices(requirement, "destinationChoices") is { Length: > 0 } destinations)
        {
            _destination = Picker("入场位置", destinations, false); _destinations = destinations.Select(x => x.Id).ToArray();
        }
        if (Choices(requirement, "printedPowerChoices") is { Length: > 0 } printed)
        {
            _printed = Picker("卡面符能支付", printed, true, "自动选择可支付的特性");
            _printedChoices = printed.Select(x => x.Id).Prepend("").ToArray();
        }
        if (requirement.TryGetProperty("targetChoicesByIndex", out var byIndex))
        {
            foreach (var entry in byIndex.EnumerateObject().OrderBy(x => int.Parse(x.Name)))
            {
                var required = int.Parse(entry.Name) < Number(requirement, "minTargetCount");
                var choices = ReadChoices(entry.Value);
                var picker = Picker($"目标 {int.Parse(entry.Name) + 1}" + (required ? " · 必选" : " · 可选"), choices, true, "请选择目标");
                _targets.Add((picker, choices.Select(x => x.Id).Prepend("").ToArray(), required));
            }
        }
        var resources = Choices(requirement, "paymentResourceChoices");
        var resourceIds = resources.Select(x => x.Id).ToHashSet(StringComparer.Ordinal);
        _resourceIds.Clear(); _resourceIds.UnionWith(resourceIds);
        _resourcePower.Clear();
        if (requirement.TryGetProperty("paymentResourcePowerByChoice", out var resourcePower))
            foreach (var entry in resourcePower.EnumerateObject())
                _resourcePower[entry.Name] = Number(entry.Value, "power")
                    + (entry.Value.TryGetProperty("powerByTrait", out var typed)
                        ? typed.EnumerateObject().Sum(trait => trait.Value.GetInt32()) : 0);
        AddChecks("额外费用", Choices(requirement, "optionalCostChoices")
            .Where(x => !resourceIds.Contains(x.Id) && !x.Id.StartsWith("PRINTED_POWER:", StringComparison.Ordinal)).ToArray());
        AddChecks("支付时使用资源", resources);
        MinimalTheme.Apply(_choices); Refresh();
    }

    public void RefreshCardPreview()
    {
        if (_source.Selected < 0 || _source.Selected >= _requirements.Count) return;
        if (_cardView?.Invoke(Text(_requirements[_source.Selected], "sourceObjectId")) is { } card)
            _preview.Display(card, OfficialCardVisualState.Normal);
        else _preview.Clear();
    }

    private OptionButton Picker(string label, (string Id, string Label)[] choices, bool empty, string emptyLabel = "不选择")
    {
        _choices.AddChild(new Label { Text = label });
        var picker = new OptionButton { CustomMinimumSize = new Vector2(0, 40), FitToLongestItem = false, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        if (empty) picker.AddItem(emptyLabel);
        foreach (var choice in choices) picker.AddItem(choice.Label);
        _choices.AddChild(picker); picker.Select(0); picker.ItemSelected += _ => Refresh(); return picker;
    }

    private void AddChecks(string label, (string Id, string Label)[] choices)
    {
        if (choices.Length == 0) return;
        _choices.AddChild(new Label { Text = label });
        foreach (var choice in choices)
        {
            if (_optional.ContainsKey(choice.Id)) continue;
            var check = new CheckBox { Text = choice.Label, ClipText = true, TooltipText = choice.Label, CustomMinimumSize = new Vector2(0, 36) };
            _choices.AddChild(check); _optional[choice.Id] = check; check.Toggled += _ => Refresh();
        }
    }

    private void Refresh(bool clearFeedback = true, bool requestQuote = true)
    {
        if (clearFeedback) _rejection = string.Empty;
        var missing = _targets.Count(target => target.Required && target.Picker.Selected <= 0);
        var targets = _targets.Where(x => x.Picker.Selected > 0).Select(x => x.Ids[x.Picker.Selected]).ToArray();
        var legalCombination = _legalSelections.Length == 0 || _legalSelections.Any(selection => selection.SequenceEqual(targets));
        var selectedResources = _optional.Count(x => _resourceIds.Contains(x.Key) && x.Value.ButtonPressed);
        var selectedResourcePower = _optional.Where(x => _resourceIds.Contains(x.Key) && x.Value.ButtonPressed)
            .Sum(x => _resourcePower.GetValueOrDefault(x.Key));
        if (requestQuote && !_submitting && PreviewRequested is not null)
        {
            _quote = null;
            _quoteRequestId = Guid.NewGuid().ToString("N");
            _cost.Text = missing == 0 && legalCombination ? "正在核对最终费用…" : "完成选择后显示最终费用。";
            if (_composable && missing == 0 && legalCombination)
                PreviewRequested.Invoke(new(_quoteRequestId, PromptId, SnapshotTick, BuildCommand()));
        }
        var needsQuote = PreviewRequested is not null;
        _confirm.Disabled = _submitting || !_composable || missing > 0 || !legalCombination
            || (needsQuote ? _quote?.CanPay != true : selectedResourcePower < _powerShortfall);
        _source.Disabled = _submitting;
        foreach (var control in _choices.GetChildren().OfType<BaseButton>()) control.Disabled = _submitting;
        _confirm.Text = _submitting ? "正在提交…" : needsQuote && _quote is null && missing == 0 && legalCombination ? "核对费用中…" : "确认打出";
        _status.Text = _submitting ? "正在等待对局确认，请稍候。"
            : _rejection.Length > 0 ? _rejection
            : !_composable ? "该效果的选择流程尚未完成，暂不能从客户端打出。"
            : missing > 0 ? $"还需选择 {missing} 个目标。"
            : !legalCombination ? "当前目标组合不可用，请调整选择。"
            : needsQuote ? _quote?.Message ?? "正在核对最终费用…"
            : _powerShortfall > 0 ? $"支付前需补充 {_powerShortfall} 符能 · 所选 {selectedResources} 个资源可提供 {selectedResourcePower} 符能。"
            : "确认后支付费用；取消可返回战场。";
        _status.AddThemeColorOverride("font_color", _confirm.Disabled || _rejection.Length > 0 ? MinimalTheme.Selected : MinimalTheme.TextSecondary);
    }

    public void ApplyQuote(PlayCostQuoteDto quote)
    {
        if (!Visible || quote.RequestId != _quoteRequestId || quote.PromptId != PromptId || quote.SnapshotTick != SnapshotTick) return;
        _quote = quote;
        _cost.Text = quote.Message;
        if (quote.Cost is { } cost)
        {
            static string Traits(IReadOnlyDictionary<string, int> traits) => string.Join(" · ", traits.Where(x => x.Value > 0).Select(x => $"{TraitName(x.Key)} {x.Value}"));
            _cost.Text = $"最终应付  {cost.Mana} 法力\n{Traits(cost.PowerByTrait)}"
                + (cost.GenericPower > 0 ? $" · 任意符能 {cost.GenericPower}" : "")
                + (cost.Experience > 0 ? $"\n经验 {cost.Experience}" : "")
                + $"\n卡面  {cost.PrintedMana} 法力 · {cost.PrintedPower} 符能";
            foreach (var item in cost.Adjustments)
                _cost.Text += $"\n{item.Label}  " + (item.Mana != 0 ? $"{item.Mana:+#;-#;0} 法力 " : "")
                    + (item.Power != 0 ? $"{item.Power:+#;-#;0} 符能" : "");
            if (cost.MissingMana + cost.MissingPower + cost.MissingExperience > 0)
                _cost.Text += $"\n还缺  {cost.MissingMana} 法力 · {cost.MissingPower} 符能"
                    + (cost.MissingExperience > 0 ? $" · {cost.MissingExperience} 经验" : "");
            else _cost.Text += $"\n支付后  {cost.RemainingMana} 法力 · {cost.RemainingRainbowPower + (cost.RemainingPowerByTrait?.Values.Sum() ?? 0)} 符能";
        }
        Refresh(clearFeedback: false, requestQuote: false);
    }

    private static string TraitName(string trait) => trait switch
    {
        "red" => "红色符能", "blue" => "蓝色符能", "green" => "绿色符能",
        "yellow" => "黄色符能", "purple" => "紫色符能", "orange" => "橙色符能", _ => trait
    };

    public void ApplyReceipt(string promptId, long tick, bool accepted, string message)
    {
        if (!Visible || PromptId != promptId || SnapshotTick != tick) return;
        _submitting = false;
        if (accepted) { Hide(); return; }
        _rejection = message;
        Refresh(clearFeedback: false, requestQuote: false);
    }

    private void Submit()
    {
        if (_confirm.Disabled) return;
        var command = BuildCommand();
        _submitting = true;
        Refresh();
        Confirmed?.Invoke(new Dictionary<string, object?>
        {
            ["cmdType"] = "PLAY_CARD", ["sourceObjectId"] = command.SourceObjectId,
            ["cardNo"] = command.CardNo, ["mode"] = command.Mode,
            ["destination"] = command.Destination, ["targetObjectIds"] = command.TargetObjectIds,
            ["optionalCosts"] = command.OptionalCosts
        });
    }

    private PlayCardCommand BuildCommand()
    {
        var requirement = _requirements[_source.Selected];
        var optional = _optional.Where(x => x.Value.ButtonPressed).Select(x => x.Key).ToList();
        if (_printed is not null && _printed.Selected > 0) optional.Add(_printedChoices[_printed.Selected]);
        return new(Text(requirement, "sourceObjectId"), Text(requirement, "cardNo"),
            _targets.Where(x => x.Picker.Selected > 0).Select(x => x.Ids[x.Picker.Selected]).ToArray(),
            Text(requirement, "mode"), optional.ToArray(), _destination is null ? "" : _destinations[_destination.Selected]);
    }

    private static string Text(JsonElement value, string key) => value.TryGetProperty(key, out var found) && found.ValueKind == JsonValueKind.String ? found.GetString() ?? "" : "";
    private static int Number(JsonElement value, string key) => value.TryGetProperty(key, out var found) && found.TryGetInt32(out var number) ? number : 0;
    public override void _UnhandledInput(InputEvent input)
    {
        if (Visible && input.IsActionPressed("ui_cancel_selection"))
        {
            Hide(); GetViewport().SetInputAsHandled();
        }
    }

    private (string Id, string Label)[] Choices(JsonElement value, string key)
        => value.TryGetProperty(key, out var found) ? ReadChoices(found) : [];
    private (string Id, string Label)[] ReadChoices(JsonElement value)
        => value.ValueKind == JsonValueKind.Array ? value.EnumerateArray().Select(x =>
        {
            var id = Text(x, "id");
            var objectId = id.StartsWith("RECYCLE_RUNE:", StringComparison.Ordinal) ? id[13..] : id;
            var card = _cardView?.Invoke(objectId);
            var label = Text(x, "label");
            if (card is not null && card.TryGetValue("cardName", out var name))
                label = id.StartsWith("RECYCLE_RUNE:", StringComparison.Ordinal) ? $"回收「{name.AsString()}」获得符能" : name.AsString();
            return (id, label);
        }).ToArray() : [];
}
