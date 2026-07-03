using Godot;

namespace Riftbound.GodotClient;

internal static class RunestoneTheme
{
    public static readonly Color BasaltBlack = new(0.015f, 0.015f, 0.014f, 1f);
    public static readonly Color Basalt = new(0.055f, 0.055f, 0.052f, 1f);
    public static readonly Color BasaltLift = new(0.105f, 0.105f, 0.098f, 1f);
    public static readonly Color Stone = new(0.225f, 0.22f, 0.2f, 1f);
    public static readonly Color Rune = new(0.88f, 0.85f, 0.76f, 1f);
    public static readonly Color RuneDim = new(0.68f, 0.65f, 0.56f, 0.58f);
    public static readonly Color Brass = new(0.63f, 0.52f, 0.34f, 1f);
    public static readonly Color BrassDim = new(0.38f, 0.31f, 0.21f, 0.76f);
    public static readonly Color Ink = new(0.9f, 0.87f, 0.78f, 1f);
    public static readonly Color MutedInk = new(0.58f, 0.56f, 0.5f, 1f);
    public static readonly Color Warning = new(0.86f, 0.25f, 0.18f, 1f);
    public static readonly Color Crimson = new(0.58f, 0.055f, 0.052f, 1f);
    public static readonly Color CrimsonDim = new(0.34f, 0.042f, 0.042f, 0.62f);
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
            case OptionButton optionButton:
                ApplyButtonTheme(optionButton);
                optionButton.AddThemeColorOverride("font_focus_color", Colors.White);
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
        button.AddThemeStyleboxOverride("normal", ButtonStyle(new Color(0.045f, 0.044f, 0.041f, 0.9f), new Color(Ivory.R, Ivory.G, Ivory.B, 0.34f)));
        button.AddThemeStyleboxOverride("hover", ButtonStyle(new Color(0.08f, 0.077f, 0.068f, 0.96f), new Color(Ivory.R, Ivory.G, Ivory.B, 0.64f)));
        button.AddThemeStyleboxOverride("pressed", ButtonStyle(new Color(0.105f, 0.04f, 0.036f, 1f), Brass));
        button.AddThemeStyleboxOverride("disabled", ButtonStyle(new Color(0.035f, 0.035f, 0.033f, 0.74f), new Color(0.16f, 0.15f, 0.13f, 0.72f)));
        button.AddThemeStyleboxOverride("focus", ButtonStyle(BasaltLift, Brass));
    }

    private static (Color Background, Color Border, int Radius) SurfaceColors(RunestoneSurface surface)
    {
        return surface switch
        {
            RunestoneSurface.Table => (new Color(0.012f, 0.012f, 0.011f, 0.76f), new Color(Ivory.R, Ivory.G, Ivory.B, 0.64f), 3),
            RunestoneSurface.Rail => (new Color(0.018f, 0.018f, 0.017f, 0.76f), new Color(Ivory.R, Ivory.G, Ivory.B, 0.34f), 2),
            RunestoneSurface.Zone => (new Color(Ivory.R, Ivory.G, Ivory.B, 0.075f), new Color(Ivory.R, Ivory.G, Ivory.B, 0.48f), 2),
            RunestoneSurface.Slot => (new Color(Ivory.R, Ivory.G, Ivory.B, 0.04f), new Color(Ivory.R, Ivory.G, Ivory.B, 0.36f), 2),
            RunestoneSurface.Card => (new Color(0.052f, 0.05f, 0.046f, 0.98f), new Color(Ivory.R, Ivory.G, Ivory.B, 0.58f), 4),
            RunestoneSurface.CardBack => (new Color(0.024f, 0.022f, 0.021f, 0.98f), new Color(Crimson.R, Crimson.G, Crimson.B, 0.72f), 4),
            RunestoneSurface.Stack => (new Color(0.016f, 0.016f, 0.015f, 0.78f), new Color(Ivory.R, Ivory.G, Ivory.B, 0.26f), 2),
            RunestoneSurface.Result => (new Color(0.014f, 0.014f, 0.013f, 0.94f), new Color(Ivory.R, Ivory.G, Ivory.B, 0.74f), 3),
            _ => (new Color(0.026f, 0.026f, 0.024f, 0.84f), new Color(Ivory.R, Ivory.G, Ivory.B, 0.3f), 2)
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
