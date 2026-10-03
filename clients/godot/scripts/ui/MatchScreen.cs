using System;
using Godot;

namespace Riftbound.GodotClient.Ui;

public partial class MatchScreen : AppScreen
{
    public event Action<Godot.Collections.Dictionary>? CardActivated;
    public event Action? ReconnectRequested;
    public event Action? ReturnToLobbyRequested;

    private Label _turnHeadline = null!;
    private Label _turnDetail = null!;
    private ActionBar _actionBar = null!;
    private HBoxContainer _connectionBanner = null!;
    private Label _connectionMessage = null!;
    private Button _reconnectButton = null!;
    private MatchTableRenderer? _renderer;
    private Godot.Collections.Array<Godot.Collections.Dictionary>? _lastSections;

    public ActionBar ActionBar => _actionBar;

    public override void _Ready()
    {
        _turnHeadline = GetNode<Label>("%TurnHeadline");
        _turnDetail = GetNode<Label>("%TurnDetail");
        _actionBar = GetNode<ActionBar>("%ActionBar");
        _connectionBanner = new HBoxContainer { Visible = false };
        _connectionMessage = new Label { SizeFlagsHorizontal = SizeFlags.ExpandFill, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _connectionMessage.AddThemeColorOverride("font_color", MinimalTheme.Selected);
        _connectionBanner.AddChild(_connectionMessage);
        _reconnectButton = new Button { Text = "重新连接", CustomMinimumSize = new Vector2(110, 40) };
        _reconnectButton.Pressed += () => ReconnectRequested?.Invoke();
        _connectionBanner.AddChild(_reconnectButton);
        var returnButton = new Button { Text = "返回大厅", CustomMinimumSize = new Vector2(110, 40) };
        returnButton.Pressed += () => ReturnToLobbyRequested?.Invoke();
        _connectionBanner.AddChild(returnButton);
        var layout = GetNode<VBoxContainer>("MatchLayout");
        layout.AddChild(_connectionBanner);
        layout.MoveChild(_connectionBanner, 0);
        _renderer = new MatchTableRenderer(this, card => CardActivated?.Invoke(card));

        ApplyTheme();
        RenderSections(_lastSections ?? []);
    }

    public void SetConnectionStatus(bool connected, bool recovering)
    {
        if (!IsNodeReady()) return;
        _connectionBanner.Visible = !connected;
        _reconnectButton.Disabled = recovering;
        _connectionMessage.Text = recovering
            ? "连接中断，正在恢复对局… 当前桌面为断线前的局面。"
            : "已与服务器断开。重新连接后将同步最新局面。";
        _actionBar.Visible = connected;
        if (!connected)
            SetTurnStatus(recovering ? "正在恢复连接" : "连接已断开", "同步最新局面后可继续行动。", actionable: false);
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
        foreach (var path in new[] { "%BattlefieldOne", "%BattlefieldTwo" })
        {
            var battlefield = MinimalTheme.Panel(new Color(MinimalTheme.TableSurface, 0.66f));
            battlefield.BorderColor = new Color(MinimalTheme.Selected, 0.28f);
            GetNode<PanelContainer>(path).AddThemeStyleboxOverride("panel", battlefield);
        }
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
        if (!actionable)
        {
            _actionBar.SetWaiting(detail);
        }
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
            _actionBar.SetWaiting("等待服务端提供下一步行动。");
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
