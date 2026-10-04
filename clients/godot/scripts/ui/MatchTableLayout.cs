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
    public Label OpponentSummary { get; }
    public Label SelfSummary { get; }
    public Label SelfHandCount { get; }
    public HBoxContainer OpponentHand { get; }
    public HBoxContainer OpponentPublicZones { get; }
    public HBoxContainer SelfPublicZones { get; }
    public HBoxContainer SelfHand { get; }
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
        Root.OffsetLeft = Root.OffsetTop = 12; Root.OffsetRight = Root.OffsetBottom = -12;
        Root.AddThemeConstantOverride("separation", 10);

        var header = Panel(Root, new Color("12232d"));
        var head = Row(header, 18);
        var brand = Column(head); brand.CustomMinimumSize = new Vector2(190, 0);
        Label(brand, "符文战场", 20, MinimalTheme.Text);
        Label(brand, "双人对战  /  中国区规则", 11, MinimalTheme.TextSecondary);
        Score = Label(head, "我方  0     :     0  对手", 27, MinimalTheme.Selected);
        Score.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        Score.HorizontalAlignment = HorizontalAlignment.Center;
        Round = Label(head, "等待对局", 14, MinimalTheme.TextSecondary);

        var main = Row(Root, 12); main.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        var boardScroll = new ScrollContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        main.AddChild(boardScroll);
        var table = Column(boardScroll, 8); table.SizeFlagsHorizontal = table.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        var opponent = Panel(table, new Color("192b36"));
        var enemyRow = Row(opponent, 12);
        OpponentSummary = Label(enemyRow, "对手", 13, MinimalTheme.TextSecondary);
        OpponentSummary.CustomMinimumSize = new Vector2(112, 0);
        OpponentPublicZones = CardStrip(enemyRow, 78);
        OpponentHand = Row(enemyRow);

        var fields = Row(table, 10); fields.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        Battlefields = [BuildBattlefield(fields, 0), BuildBattlefield(fields, 1)];

        var self = Panel(table, new Color("162d36"));
        var selfRow = Row(self, 12);
        var selfIdentity = Column(selfRow, 4); selfIdentity.CustomMinimumSize = new Vector2(112, 0);
        SelfSummary = Label(selfIdentity, "我方", 13, MinimalTheme.Text);
        BaseDestination = new Button { Text = "我方基地", CustomMinimumSize = new Vector2(108, 36) };
        selfIdentity.AddChild(BaseDestination);
        SelfPublicZones = CardStrip(selfRow, 78);

        var hand = Panel(table, new Color("101e28"));
        var handColumn = Column(hand, 5);
        var handHeader = Row(handColumn);
        var handTitle = Label(handHeader, "手牌", 14, MinimalTheme.Selected); handTitle.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        SelfHandCount = Label(handHeader, "点选出牌 · 右键查看", 12, MinimalTheme.TextSecondary);
        SelfHand = CardStrip(handColumn, 124); SelfHand.Alignment = BoxContainer.AlignmentMode.Center;

        Rail = Column(main, 8); Rail.CustomMinimumSize = new Vector2(268, 0);
        var status = Panel(Rail, new Color("1c353e"));
        var statusColumn = Column(status, 6);
        TurnHeadline = Label(statusColumn, "等待对局", 22, MinimalTheme.Selectable);
        TurnDetail = Label(statusColumn, "同步最新局面后可继续行动。", 13, MinimalTheme.TextSecondary, wrap: true);
        var intelScroll = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        Rail.AddChild(intelScroll);
        Intel = Column(intelScroll, 8); Intel.SizeFlagsHorizontal = Intel.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        var inspect = Panel(Intel, new Color("13242e"));
        var inspectColumn = Column(inspect, 4);
        InspectName = Label(inspectColumn, "卡牌详情", 15, MinimalTheme.Selected);
        InspectArt = new TextureRect
        {
            CustomMinimumSize = new Vector2(0, 174), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = Control.MouseFilterEnum.Ignore
        };
        inspectColumn.AddChild(InspectArt);
        var textScroll = new ScrollContainer { CustomMinimumSize = new Vector2(0, 62), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        inspectColumn.AddChild(textScroll);
        InspectText = Label(textScroll, "悬停查看卡牌\n点选卡牌可直接行动", 12, MinimalTheme.TextSecondary, wrap: true);
        CardActions = new HFlowContainer(); inspectColumn.AddChild(CardActions);
        var chainPanel = Panel(Intel, new Color("152731"));
        var chainColumn = Column(chainPanel, 5);
        Label(chainColumn, "结算链", 15, MinimalTheme.Selected);
        Chain = ScrollingColumn(chainColumn, 78);
        var historyPanel = Panel(Intel, new Color("12222c")); historyPanel.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        var historyColumn = Column(historyPanel, 5);
        Label(historyColumn, "最近战况", 15, MinimalTheme.Text);
        History = ScrollingColumn(historyColumn, 52); History.GetParent<ScrollContainer>().SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        Composer = new Control { Visible = false, SizeFlagsVertical = Control.SizeFlags.ExpandFill }; Rail.AddChild(Composer);

        var actionPanel = Panel(Root, new Color("152833")); actionPanel.CustomMinimumSize = new Vector2(0, 100);
        Actions = GD.Load<PackedScene>("res://scenes/components/ActionBar.tscn").Instantiate<ActionBar>(); actionPanel.AddChild(Actions);
    }

    private static Battlefield BuildBattlefield(Container parent, int index)
    {
        var panel = Panel(parent, new Color("142730"));
        panel.SizeFlagsHorizontal = panel.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        var backdrop = new TextureRect { ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered, MouseFilter = Control.MouseFilterEnum.Ignore,
            Modulate = new Color(0.65f, 0.8f, 0.88f, 0.14f) };
        panel.AddChild(backdrop);
        var content = Column(panel, 4);
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

    internal static PanelContainer Panel(Node parent, Color color)
    {
        var panel = new PanelContainer(); parent.AddChild(panel);
        var style = MinimalTheme.Panel(color); style.BorderColor = new Color("304652");
        style.SetContentMarginAll(10); style.SetCornerRadiusAll(8);
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
        var box = new VBoxContainer(); box.AddThemeConstantOverride("separation", spacing); parent.AddChild(box); return box;
    }
    internal static HBoxContainer Row(Node parent, int spacing = 6)
    {
        var box = new HBoxContainer(); box.AddThemeConstantOverride("separation", spacing); parent.AddChild(box); return box;
    }
    private static HBoxContainer CardStrip(Node parent, float height)
    {
        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(0, height), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Auto, VerticalScrollMode = ScrollContainer.ScrollMode.Disabled };
        parent.AddChild(scroll);
        var row = Row(scroll); row.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; row.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter; return row;
    }
    private static VBoxContainer ScrollingColumn(Node parent, float height)
    {
        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(0, height), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        parent.AddChild(scroll); var column = Column(scroll, 7); column.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; return column;
    }

    internal sealed record Battlefield(PanelContainer Panel, Label Name, Label State, Container Site,
        Container OpponentUnits, Container SelfUnits, Container Standby, Button Destination, Label Force, TextureRect Backdrop);
}
