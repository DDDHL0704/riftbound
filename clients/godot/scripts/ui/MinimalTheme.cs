using Godot;

namespace Riftbound.GodotClient.Ui;

public static class MinimalTheme
{
    public static readonly Color AppBackground = new("171a1f");
    public static readonly Color TableSurface = new("20242b");
    public static readonly Color Surface = new("292e36");
    public static readonly Color SurfaceRaised = new("343a44");
    public static readonly Color Border = new("515966");
    public static readonly Color Text = new("f4f5f7");
    public static readonly Color TextSecondary = new("aeb5bf");
    public static readonly Color Selectable = new("54c58a");
    public static readonly Color Selected = new("f2b84b");
    public static readonly Color Hostile = new("e26464");
    public static readonly Color Waiting = new("7e9bb8");

    public static void Apply(Control root)
    {
        ApplyNode(root);
        foreach (var child in root.GetChildren())
        {
            if (child is Control control)
            {
                Apply(control);
            }
        }
    }

    public static StyleBoxFlat Panel(Color background)
    {
        var style = new StyleBoxFlat
        {
            BgColor = background,
            BorderColor = Border
        };
        style.SetBorderWidthAll(1);
        style.SetCornerRadiusAll(6);
        style.SetContentMarginAll(8);
        return style;
    }

    public static StyleBoxFlat Outline(OfficialCardVisualState state)
    {
        var color = state switch
        {
            OfficialCardVisualState.Selectable => Selectable,
            OfficialCardVisualState.Selected => Selected,
            OfficialCardVisualState.LegalTarget => Selectable,
            OfficialCardVisualState.HostileTarget => Hostile,
            OfficialCardVisualState.Disabled => new Color(Border, 0.38f),
            OfficialCardVisualState.Hidden => Waiting,
            _ => new Color(Border, 0.72f)
        };
        var width = state is OfficialCardVisualState.Selected or OfficialCardVisualState.HostileTarget ? 3 : 2;
        var style = new StyleBoxFlat
        {
            BgColor = Colors.Transparent,
            BorderColor = color
        };
        style.SetBorderWidthAll(width);
        style.SetCornerRadiusAll(7);
        return style;
    }

    private static void ApplyNode(Control control)
    {
        switch (control)
        {
            case Label label:
                label.AddThemeColorOverride("font_color", Text);
                label.AddThemeFontSizeOverride("font_size", 16);
                break;
            case LineEdit lineEdit:
                lineEdit.AddThemeColorOverride("font_color", Text);
                lineEdit.AddThemeColorOverride("font_placeholder_color", TextSecondary);
                lineEdit.AddThemeStyleboxOverride("normal", Panel(Surface));
                lineEdit.AddThemeStyleboxOverride("focus", BorderedPanel(SurfaceRaised, Selected, 2));
                break;
            case OptionButton optionButton:
                ApplyButton(optionButton);
                break;
            case Button button:
                ApplyButton(button);
                break;
            case PanelContainer panel:
                panel.AddThemeStyleboxOverride("panel", Panel(Surface));
                break;
        }

        if (control is BoxContainer box)
        {
            box.AddThemeConstantOverride("separation", 8);
        }
    }

    private static void ApplyButton(Button button)
    {
        button.CustomMinimumSize = new Vector2(button.CustomMinimumSize.X, Mathf.Max(40, button.CustomMinimumSize.Y));
        button.AddThemeColorOverride("font_color", Text);
        button.AddThemeColorOverride("font_hover_color", Text);
        button.AddThemeColorOverride("font_pressed_color", Text);
        button.AddThemeColorOverride("font_disabled_color", new Color(TextSecondary, 0.55f));
        button.AddThemeFontSizeOverride("font_size", 16);
        button.AddThemeStyleboxOverride("normal", Panel(Surface));
        button.AddThemeStyleboxOverride("hover", BorderedPanel(SurfaceRaised, TextSecondary, 1));
        button.AddThemeStyleboxOverride("pressed", BorderedPanel(TableSurface, Selected, 2));
        button.AddThemeStyleboxOverride("focus", BorderedPanel(SurfaceRaised, Selected, 2));
        button.AddThemeStyleboxOverride("disabled", BorderedPanel(AppBackground, new Color(Border, 0.4f), 1));
    }

    private static StyleBoxFlat BorderedPanel(Color background, Color border, int width)
    {
        var style = Panel(background);
        style.BorderColor = border;
        style.SetBorderWidthAll(width);
        return style;
    }
}
