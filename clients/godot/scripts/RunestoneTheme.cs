using Godot;

namespace Riftbound.GodotClient;

internal static class RunestoneTheme
{
    public static readonly Color BasaltBlack = new(0.015f, 0.015f, 0.014f, 1f);
    public static readonly Color Basalt = new(0.055f, 0.055f, 0.052f, 1f);
    public static readonly Color BasaltLift = new(0.105f, 0.105f, 0.098f, 1f);
    public static readonly Color Stone = new(0.225f, 0.22f, 0.2f, 1f);
    public static readonly Color Rune = new(0.86f, 0.79f, 0.62f, 1f);
    public static readonly Color RuneDim = new(0.64f, 0.58f, 0.43f, 0.58f);
    public static readonly Color Brass = new(0.84f, 0.62f, 0.25f, 1f);
    public static readonly Color BrassDim = new(0.48f, 0.36f, 0.18f, 0.86f);
    public static readonly Color Ink = new(0.9f, 0.87f, 0.78f, 1f);
    public static readonly Color MutedInk = new(0.58f, 0.56f, 0.5f, 1f);
    public static readonly Color Warning = new(0.86f, 0.25f, 0.18f, 1f);
    public static readonly Color Crimson = new(0.66f, 0.075f, 0.07f, 1f);
    public static readonly Color CrimsonDim = new(0.42f, 0.055f, 0.055f, 0.72f);
    public static readonly Color Ivory = new(0.88f, 0.84f, 0.74f, 1f);
    public static readonly Color Steel = new(0.24f, 0.25f, 0.24f, 1f);

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
        style.SetContentMarginAll(SurfacePadding(surface));
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
        style.SetContentMargin(Side.Left, 8);
        style.SetContentMargin(Side.Right, 8);
        style.SetContentMargin(Side.Top, 4);
        style.SetContentMargin(Side.Bottom, 4);
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
                richText.AddThemeColorOverride("font_shadow_color", BasaltBlack);
                richText.AddThemeStyleboxOverride("normal", FrameStyle(RunestoneSurface.Chrome));
                break;
            case CheckBox checkBox:
                checkBox.AddThemeColorOverride("font_color", Ink);
                checkBox.AddThemeColorOverride("font_hover_color", Colors.White);
                checkBox.AddThemeColorOverride("font_pressed_color", Colors.White);
                checkBox.AddThemeColorOverride("font_disabled_color", MutedInk);
                break;
            case Button button:
                ApplyButtonTheme(button);
                break;
            case LineEdit lineEdit:
                lineEdit.AddThemeColorOverride("font_color", Ink);
                lineEdit.AddThemeColorOverride("font_placeholder_color", MutedInk);
                lineEdit.AddThemeStyleboxOverride("normal", ButtonStyle(Basalt, Steel));
                lineEdit.AddThemeStyleboxOverride("focus", ButtonStyle(BasaltLift, Crimson));
                break;
            case PanelContainer panel:
                panel.AddThemeStyleboxOverride("panel", FrameStyle(RunestoneSurface.Chrome));
                break;
        }

        if (node is BoxContainer box)
        {
            box.AddThemeConstantOverride("separation", 5);
        }
    }

    private static void ApplyButtonTheme(Button button)
    {
        button.AddThemeColorOverride("font_color", Ink);
        button.AddThemeColorOverride("font_hover_color", Colors.White);
        button.AddThemeColorOverride("font_pressed_color", Colors.White);
        button.AddThemeColorOverride("font_disabled_color", MutedInk);
        button.AddThemeStyleboxOverride("normal", ButtonStyle(new Color(0.06f, 0.057f, 0.052f, 0.94f), Steel));
        button.AddThemeStyleboxOverride("hover", ButtonStyle(new Color(0.13f, 0.12f, 0.105f, 0.98f), Crimson));
        button.AddThemeStyleboxOverride("pressed", ButtonStyle(new Color(0.16f, 0.06f, 0.045f, 1f), Brass));
        button.AddThemeStyleboxOverride("disabled", ButtonStyle(new Color(0.035f, 0.035f, 0.033f, 0.74f), new Color(0.16f, 0.15f, 0.13f, 0.72f)));
        button.AddThemeStyleboxOverride("focus", ButtonStyle(BasaltLift, Brass));
    }

    private static (Color Background, Color Border, int Radius) SurfaceColors(RunestoneSurface surface)
    {
        return surface switch
        {
            RunestoneSurface.Table => (new Color(0.025f, 0.024f, 0.022f, 0.98f), Brass, 4),
            RunestoneSurface.Rail => (new Color(0.045f, 0.043f, 0.039f, 0.94f), Steel, 3),
            RunestoneSurface.Zone => (new Color(0.035f, 0.035f, 0.032f, 0.86f), new Color(0.47f, 0.43f, 0.34f, 0.78f), 3),
            RunestoneSurface.Slot => (new Color(0.02f, 0.02f, 0.018f, 0.58f), new Color(0.62f, 0.58f, 0.48f, 0.42f), 3),
            RunestoneSurface.Card => (new Color(0.12f, 0.105f, 0.082f, 1f), Brass, 5),
            RunestoneSurface.CardBack => (new Color(0.035f, 0.024f, 0.022f, 1f), Crimson, 5),
            RunestoneSurface.Stack => (new Color(0.028f, 0.028f, 0.026f, 0.94f), BrassDim, 3),
            RunestoneSurface.Result => (new Color(0.105f, 0.062f, 0.045f, 0.98f), Brass, 4),
            _ => (new Color(0.04f, 0.04f, 0.036f, 0.9f), Steel, 3)
        };
    }

    private static float SurfacePadding(RunestoneSurface surface)
    {
        return surface switch
        {
            RunestoneSurface.Table => 3f,
            RunestoneSurface.Rail => 3f,
            RunestoneSurface.Zone => 3f,
            RunestoneSurface.Card => 2f,
            RunestoneSurface.CardBack => 2f,
            RunestoneSurface.Stack => 4f,
            RunestoneSurface.Result => 8f,
            RunestoneSurface.Chrome => 8f,
            _ => 3f
        };
    }
}
