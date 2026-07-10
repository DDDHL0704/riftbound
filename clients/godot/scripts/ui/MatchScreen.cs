using System;
using Godot;

namespace Riftbound.GodotClient.Ui;

public partial class MatchScreen : AppScreen
{
    public event Action<Godot.Collections.Dictionary>? CardActivated;

    private Label _turnHeadline = null!;
    private Label _turnDetail = null!;
    private Label _actionStatus = null!;
    private MatchTableRenderer? _renderer;
    private Godot.Collections.Array<Godot.Collections.Dictionary>? _lastSections;

    public override void _Ready()
    {
        _turnHeadline = GetNode<Label>("%TurnHeadline");
        _turnDetail = GetNode<Label>("%TurnDetail");
        _actionStatus = GetNode<Label>("%ActionStatus");
        _renderer = new MatchTableRenderer(this, card => CardActivated?.Invoke(card));

        ApplyTheme();
        RenderSections(_lastSections ?? []);
    }

    public void ApplyTheme()
    {
        MinimalTheme.Apply(this);
        foreach (var path in new[]
                 {
                     "%TurnStatus",
                     "%OpponentArea",
                     "%BattlefieldOne",
                     "%BattlefieldTwo",
                     "%SelfArea",
                     "%HandArea",
                     "%ActionBarHost"
                 })
        {
            GetNode<PanelContainer>(path)
                .AddThemeStyleboxOverride("panel", MinimalTheme.Panel(MinimalTheme.Surface));
        }

        GetNode<PanelContainer>("%ActionBarHost")
            .AddThemeStyleboxOverride("panel", MinimalTheme.Panel(MinimalTheme.TableSurface));
    }

    public void RenderSections(
        Godot.Collections.Array<Godot.Collections.Dictionary> sections)
    {
        _lastSections = sections;
        if (_renderer is null)
        {
            return;
        }

        var table = FindWireTable(sections);
        if (table is null)
        {
            _renderer.Clear();
            SetTurnStatus("等待对局", "房间准备完成后，战场会显示在这里。", actionable: false);
            return;
        }

        _renderer.Render(table);
        var turnState = ReadString(table, "turnState");
        var status = FriendlyTurnStatus(turnState);
        SetTurnStatus(status.Headline, status.Detail, status.Actionable);
    }

    public void SetTurnStatus(string headline, string detail, bool actionable)
    {
        if (!IsNodeReady())
        {
            return;
        }

        _turnHeadline.Text = headline;
        _turnDetail.Text = detail;
        _turnHeadline.AddThemeColorOverride(
            "font_color",
            actionable ? MinimalTheme.Selectable : MinimalTheme.Text);
        _turnDetail.AddThemeColorOverride("font_color", MinimalTheme.TextSecondary);
        _actionStatus.Text = actionable
            ? "可以行动，等待服务端候选。"
            : "等待服务端提供下一步行动。";
        _actionStatus.AddThemeColorOverride(
            "font_color",
            actionable ? MinimalTheme.Selectable : MinimalTheme.Waiting);
    }

    public void ClearPromptStates()
    {
        _renderer?.ClearPromptStates();
    }

    public void SetObjectState(string objectId, OfficialCardVisualState state)
    {
        _renderer?.SetObjectState(objectId, state);
    }

    public override void SetScreenVisible(bool visible)
    {
        base.SetScreenVisible(visible);
        if (!visible)
        {
            ClearPromptStates();
        }
    }

    private static Godot.Collections.Dictionary? FindWireTable(
        Godot.Collections.Array<Godot.Collections.Dictionary> sections)
    {
        foreach (var section in sections)
        {
            if (string.Equals(ReadString(section, "kind"), "wireTable", StringComparison.Ordinal))
            {
                return section;
            }
        }

        return null;
    }

    private static (string Headline, string Detail, bool Actionable) FriendlyTurnStatus(string state)
    {
        return state.ToUpperInvariant() switch
        {
            "MULLIGAN" => ("起手调整", "等待服务端提供起手牌选择。", false),
            "TURN_START" => ("回合开始", "正在处理回合开始状态。", false),
            "MAIN" or "MAIN_ACTION" or "NEUTRAL_OPEN" =>
                ("主要行动阶段", "等待服务端确认当前行动权。", false),
            "NEUTRAL_CLOSED" => ("行动结算中", "当前行动窗口已关闭。", false),
            "SPELL_DUEL_OPEN" => ("法术对决", "等待服务端提供对决行动。", false),
            "SPELL_DUEL_CLOSED" => ("法术对决结算中", "正在结算法术对决。", false),
            "TURN_END" => ("回合结束", "正在处理回合结束状态。", false),
            "FINISHED" => ("对局结束", "最终结果即将显示。", false),
            _ => ("对局进行中", "等待服务端更新当前阶段。", false)
        };
    }

    private static string ReadString(Godot.Collections.Dictionary source, string key)
    {
        return source.TryGetValue(key, out var value) ? value.AsString() : string.Empty;
    }
}
