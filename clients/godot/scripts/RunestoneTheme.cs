using Godot;

namespace Riftbound.GodotClient;

internal static class RunestoneTheme
{
    public static readonly Color BasaltBlack = new(0.035f, 0.043f, 0.043f, 1f);
    public static readonly Color Basalt = new(0.082f, 0.098f, 0.094f, 1f);
    public static readonly Color BasaltLift = new(0.135f, 0.151f, 0.137f, 1f);
    public static readonly Color Stone = new(0.235f, 0.247f, 0.216f, 1f);
    public static readonly Color Rune = new(0.42f, 0.78f, 0.76f, 1f);
    public static readonly Color RuneDim = new(0.18f, 0.45f, 0.43f, 0.72f);
    public static readonly Color Brass = new(0.73f, 0.57f, 0.29f, 1f);
    public static readonly Color BrassDim = new(0.44f, 0.34f, 0.17f, 1f);
    public static readonly Color Ink = new(0.83f, 0.86f, 0.78f, 1f);
    public static readonly Color MutedInk = new(0.56f, 0.61f, 0.54f, 1f);
    public static readonly Color Warning = new(0.88f, 0.68f, 0.34f, 1f);

    public static StyleBoxFlat FrameStyle(RunestoneSurface surface, int borderWidth = 1)
    {
        var (background, border, radius) = SurfaceColors(surface);
        var style = new StyleBoxFlat
        {
            BgColor = background,
            BorderColor = border
        };
        style.SetBorderWidthAll(borderWidth);
        style.SetCornerRadiusAll(radius);
        return style;
    }

    public static StyleBoxFlat ButtonStyle(Color background, Color border)
    {
        var style = new StyleBoxFlat
        {
            BgColor = background,
            BorderColor = border
        };
        style.SetBorderWidthAll(1);
        style.SetCornerRadiusAll(4);
        return style;
    }

    public static void ApplyToTree(Node node)
    {
        ApplyToNode(node);
        foreach (var child in node.GetChildren())
        {
            ApplyToTree(child);
        }
    }

    private static void ApplyToNode(Node node)
    {
        switch (node)
        {
            case Label label:
                label.AddThemeColorOverride("font_color", Ink);
                label.AddThemeColorOverride("font_shadow_color", BasaltBlack);
                label.AddThemeConstantOverride("shadow_offset_x", 1);
                label.AddThemeConstantOverride("shadow_offset_y", 1);
                break;
            case RichTextLabel richText:
                richText.AddThemeColorOverride("default_color", Ink);
                richText.AddThemeStyleboxOverride("normal", FrameStyle(RunestoneSurface.Chrome));
                break;
            case Button button:
                ApplyButtonTheme(button);
                break;
            case LineEdit lineEdit:
                lineEdit.AddThemeColorOverride("font_color", Ink);
                lineEdit.AddThemeColorOverride("font_placeholder_color", MutedInk);
                lineEdit.AddThemeStyleboxOverride("normal", ButtonStyle(Basalt, BrassDim));
                lineEdit.AddThemeStyleboxOverride("focus", ButtonStyle(BasaltLift, RuneDim));
                break;
            case PanelContainer panel:
                panel.AddThemeStyleboxOverride("panel", FrameStyle(RunestoneSurface.Chrome));
                break;
        }

        if (node is BoxContainer box)
        {
            box.AddThemeConstantOverride("separation", 6);
        }
    }

    private static void ApplyButtonTheme(Button button)
    {
        button.AddThemeColorOverride("font_color", Ink);
        button.AddThemeColorOverride("font_hover_color", Colors.White);
        button.AddThemeColorOverride("font_pressed_color", Colors.White);
        button.AddThemeColorOverride("font_disabled_color", MutedInk);
        button.AddThemeStyleboxOverride("normal", ButtonStyle(Basalt, BrassDim));
        button.AddThemeStyleboxOverride("hover", ButtonStyle(BasaltLift, RuneDim));
        button.AddThemeStyleboxOverride("pressed", ButtonStyle(new Color(0.18f, 0.22f, 0.19f, 1f), Brass));
        button.AddThemeStyleboxOverride("disabled", ButtonStyle(new Color(0.07f, 0.08f, 0.075f, 0.86f), new Color(0.2f, 0.18f, 0.14f, 0.7f)));
        button.AddThemeStyleboxOverride("focus", ButtonStyle(BasaltLift, Rune));
    }

    private static (Color Background, Color Border, int Radius) SurfaceColors(RunestoneSurface surface)
    {
        return surface switch
        {
            RunestoneSurface.Table => (new Color(0.055f, 0.065f, 0.06f, 0.96f), Brass, 4),
            RunestoneSurface.Rail => (new Color(0.075f, 0.087f, 0.08f, 0.92f), BrassDim, 3),
            RunestoneSurface.Zone => (new Color(0.07f, 0.082f, 0.076f, 0.82f), RuneDim, 3),
            RunestoneSurface.Slot => (new Color(0.04f, 0.047f, 0.044f, 0.48f), new Color(0.28f, 0.35f, 0.3f, 0.72f), 3),
            RunestoneSurface.Card => (new Color(0.16f, 0.15f, 0.125f, 1f), Brass, 5),
            RunestoneSurface.CardBack => (new Color(0.045f, 0.068f, 0.072f, 1f), Rune, 5),
            RunestoneSurface.Stack => (new Color(0.06f, 0.07f, 0.066f, 0.94f), BrassDim, 3),
            RunestoneSurface.Result => (new Color(0.12f, 0.1f, 0.065f, 0.96f), Brass, 4),
            _ => (new Color(0.07f, 0.08f, 0.076f, 0.9f), BrassDim, 3)
        };
    }
}
