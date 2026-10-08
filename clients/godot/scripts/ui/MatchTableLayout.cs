using Godot;

namespace Riftbound.GodotClient.Ui;

// Native table geometry is kept separate from snapshot projection and game input.
internal sealed class MatchTableLayout
{
    public VBoxContainer Root { get; }
    public Label TurnHeadline { get; }
    public Label TurnDetail { get; }
    public Label Score { get; }
    public Label Round { get; }
    public CheckButton ReduceMotion { get; }
    public Label OpponentSummary { get; }
    public Label SelfSummary { get; }
    public Label SelfHandCount { get; }
    public HBoxContainer OpponentHand { get; }
    public HBoxContainer OpponentPublicZones { get; }
    public HBoxContainer SelfPublicZones { get; }
    public FanHandContainer SelfHand { get; }
    public TableDropZone BaseZone { get; }
    public PanelContainer InspectPanel { get; }
    public PanelContainer ChainPanel { get; }
    public PanelContainer ActionPanel { get; }
    public Battlefield[] Battlefields { get; }
    public Button BaseDestination { get; }
    public VBoxContainer Intel { get; }
    public Control Composer { get; }
    public VBoxContainer Rail { get; }
    public TextureRect InspectArt { get; }
    public Label InspectName { get; }
    public Label InspectText { get; }
    public HFlowContainer CardActions { get; }
    public VBoxContainer Chain { get; }
    public VBoxContainer History { get; }
    public ActionBar Actions { get; }

    public MatchTableLayout(Control host)
    {
        Root = new VBoxContainer { Name = "MatchLayout" };
        host.AddChild(Root); Root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        Root.OffsetLeft = Root.OffsetTop = 8; Root.OffsetRight = Root.OffsetBottom = -8;
        Root.AddThemeConstantOverride("separation", 6);

        var header = Panel(Root, new Color("111e34"));
        var head = Row(header, 18);
        var brand = Column(head); brand.CustomMinimumSize = new Vector2(190, 0);
        Label(brand, "符文战场", 18, MinimalTheme.Text);
        Score = Label(head, "我方  0     :     0  对手", 23, MinimalTheme.Selected);
        Score.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        Score.HorizontalAlignment = HorizontalAlignment.Center;
        Round = Label(head, "等待对局", 14, MinimalTheme.TextSecondary);
        ReduceMotion = new CheckButton { Text = "减少动画", TooltipText = "保留战况文字，关闭闪动反馈" };
        ReduceMotion.AddThemeFontSizeOverride("font_size", 12); head.AddChild(ReduceMotion);

        var main = Row(Root, 12); main.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        var playArea = Column(main, 6);
        playArea.SizeFlagsHorizontal = playArea.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        var boardScroll = new ScrollContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        playArea.AddChild(boardScroll);
        var table = Column(boardScroll, 8); table.SizeFlagsHorizontal = table.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        var opponent = Panel(table, new Color("17273e"));
        var enemyRow = Row(opponent, 12);
        OpponentSummary = Label(enemyRow, "对手", 13, MinimalTheme.TextSecondary);
        OpponentSummary.CustomMinimumSize = new Vector2(112, 0);
        OpponentPublicZones = CardStrip(enemyRow, 78);
        OpponentHand = Row(enemyRow);

        var fields = Row(table, 10); fields.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        Battlefields = [BuildBattlefield(fields, 0), BuildBattlefield(fields, 1)];

        var self = Panel(playArea, new Color("17273e")); BaseZone = self;
        var selfRow = Row(self, 12);
        var selfIdentity = Column(selfRow, 4); selfIdentity.CustomMinimumSize = new Vector2(112, 0);
        SelfSummary = Label(selfIdentity, "我方", 13, MinimalTheme.Text);
        BaseDestination = new Button { Text = "我方基地", CustomMinimumSize = new Vector2(108, 36) };
        selfIdentity.AddChild(BaseDestination);
        SelfPublicZones = CardStrip(selfRow, 78);

        var hand = Panel(playArea, new Color("101c30"));
        var handColumn = Column(hand, 5);
        var handHeader = Row(handColumn);
        var handTitle = Label(handHeader, "手牌 · 拖动或点选出牌 · 悬停预览，右键看完整卡牌", 12, MinimalTheme.TextSecondary); handTitle.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        SelfHandCount = Label(handHeader, "", 12, MinimalTheme.TextSecondary);
        var handScroll = new ScrollContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Auto, VerticalScrollMode = ScrollContainer.ScrollMode.Disabled };
        handColumn.AddChild(handScroll);
        SelfHand = new FanHandContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; handScroll.AddChild(SelfHand);

        Rail = Column(main, 8); Rail.CustomMinimumSize = new Vector2(280, 0);
        var status = Panel(Rail, new Color("1b304b"));
        var statusColumn = Column(status, 6);
        TurnHeadline = Label(statusColumn, "等待对局", 22, MinimalTheme.Selectable);
        TurnDetail = Label(statusColumn, "同步最新局面后可继续行动。", 13, MinimalTheme.TextSecondary, wrap: true);
        var intelScroll = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        Rail.AddChild(intelScroll);
        Intel = Column(intelScroll, 8); Intel.SizeFlagsHorizontal = Intel.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        var inspect = Panel(Intel, new Color("14233a")); InspectPanel = inspect; inspect.Visible = false;
        var inspectColumn = Column(inspect, 4);
        InspectName = Label(inspectColumn, "卡牌详情", 15, MinimalTheme.Selected);
        InspectArt = new TextureRect
        {
            CustomMinimumSize = new Vector2(0, 344), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = Control.MouseFilterEnum.Ignore
        };
        inspectColumn.AddChild(InspectArt);
        var textScroll = new ScrollContainer { CustomMinimumSize = new Vector2(0, 62), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        inspectColumn.AddChild(textScroll);
        InspectText = Label(textScroll, "悬停查看卡牌\n点选卡牌可直接行动", 12, MinimalTheme.TextSecondary, wrap: true);
        CardActions = new HFlowContainer(); inspectColumn.AddChild(CardActions);
        var chainPanel = Panel(Intel, new Color("192b43")); ChainPanel = chainPanel; Intel.MoveChild(chainPanel, 0);
        var chainColumn = Column(chainPanel, 5);
        Label(chainColumn, "结算链", 15, MinimalTheme.Selected);
        Chain = ScrollingColumn(chainColumn, 78);
        var historyPanel = Panel(Intel, new Color("14233a"));
        var historyColumn = Column(historyPanel, 5);
        Label(historyColumn, "最近战况", 15, MinimalTheme.Text);
        History = ScrollingColumn(historyColumn, 52); History.GetParent<ScrollContainer>().SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        Composer = new Control { Visible = false, SizeFlagsVertical = Control.SizeFlags.ExpandFill }; Rail.AddChild(Composer);

        var actionPanel = Panel(Root, new Color("14233a")); ActionPanel = actionPanel; actionPanel.CustomMinimumSize = new Vector2(0, 60);
        Actions = GD.Load<PackedScene>("res://scenes/components/ActionBar.tscn").Instantiate<ActionBar>(); actionPanel.AddChild(Actions);
        Actions.SelectionVisibilityChanged += selected => actionPanel.CustomMinimumSize = new Vector2(0, selected ? 108 : 60);
    }

    private static Battlefield BuildBattlefield(Container parent, int index)
    {
        var panel = Panel(parent, new Color("1a2b44"));
        panel.SizeFlagsHorizontal = panel.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        var backdrop = new TextureRect { ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered, MouseFilter = Control.MouseFilterEnum.Ignore,
            Modulate = new Color(0.65f, 0.8f, 0.88f, 0.08f) };
        panel.AddChild(backdrop);
        var content = Column(panel, 2);
        var header = Row(content);
        var site = new HBoxContainer(); header.AddChild(site);
        var titles = Column(header, 2); titles.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        var name = Label(titles, $"战场 {index + 1}", 18, MinimalTheme.Text);
        name.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        var state = Label(titles, "尚未控制", 12, MinimalTheme.TextSecondary);
        var destination = new Button { Text = "选择此处", Visible = false, CustomMinimumSize = new Vector2(92, 36) }; header.AddChild(destination);
        var enemy = CardStrip(content, 76);
        enemy.Alignment = BoxContainer.AlignmentMode.Center;
        enemy.GetParent<ScrollContainer>().SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        var divider = Row(content, 5);
        var standby = Row(divider, 5); standby.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        var force = Label(divider, "", 13, MinimalTheme.TextSecondary);
        var own = CardStrip(content, 76);
        own.Alignment = BoxContainer.AlignmentMode.Center;
        own.GetParent<ScrollContainer>().SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        return new Battlefield(panel, name, state, site, enemy, own, standby, destination, force, backdrop);
    }

    internal static TableDropZone Panel(Node parent, Color color)
    {
        var panel = new TableDropZone(); parent.AddChild(panel);
        var style = MinimalTheme.Panel(color); style.BorderColor = new Color("293c55");
        style.SetContentMarginAll(4); style.SetCornerRadiusAll(4);
        panel.AddThemeStyleboxOverride("panel", style); return panel;
    }
    internal static Label Label(Node parent, string text, int size = 14, Color? color = null, bool wrap = false)
    {
        var label = new Label { Text = text, VerticalAlignment = VerticalAlignment.Center, MouseFilter = Control.MouseFilterEnum.Ignore };
        label.AddThemeFontSizeOverride("font_size", size); label.AddThemeColorOverride("font_color", color ?? MinimalTheme.Text);
        if (wrap) { label.AutowrapMode = TextServer.AutowrapMode.WordSmart; label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; }
        parent.AddChild(label); return label;
    }
    internal static VBoxContainer Column(Node parent, int spacing = 5)
    {
        var box = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Pass }; box.AddThemeConstantOverride("separation", spacing); parent.AddChild(box); return box;
    }
    internal static HBoxContainer Row(Node parent, int spacing = 6)
    {
        var box = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Pass }; box.AddThemeConstantOverride("separation", spacing); parent.AddChild(box); return box;
    }
    private static HBoxContainer CardStrip(Node parent, float height)
    {
        var scroll = new ScrollContainer { MouseFilter = Control.MouseFilterEnum.Pass, CustomMinimumSize = new Vector2(0, height), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Auto, VerticalScrollMode = ScrollContainer.ScrollMode.Disabled };
        parent.AddChild(scroll);
        var row = Row(scroll); row.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; row.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter; return row;
    }
    private static VBoxContainer ScrollingColumn(Node parent, float height)
    {
        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(0, height), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        parent.AddChild(scroll); var column = Column(scroll, 7); column.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; return column;
    }

    internal sealed record Battlefield(TableDropZone Panel, Label Name, Label State, Container Site,
        Container OpponentUnits, Container SelfUnits, Container Standby, Button Destination, Label Force, TextureRect Backdrop);
}
