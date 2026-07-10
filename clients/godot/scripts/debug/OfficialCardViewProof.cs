using System.IO;
using System.Linq;
using Godot;
using Riftbound.GodotClient.Ui;

namespace Riftbound.GodotClient.Debug;

public partial class OfficialCardViewProof : Control
{
    private static readonly (string Label, OfficialCardVisualState State)[] States =
    {
        ("正常", OfficialCardVisualState.Normal),
        ("可选择", OfficialCardVisualState.Selectable),
        ("已选择", OfficialCardVisualState.Selected),
        ("合法目标", OfficialCardVisualState.LegalTarget),
        ("对手目标", OfficialCardVisualState.HostileTarget),
        ("不可用", OfficialCardVisualState.Disabled),
        ("隐藏信息", OfficialCardVisualState.Hidden)
    };

    public override async void _Ready()
    {
        var background = new ColorRect
        {
            Color = MinimalTheme.AppBackground,
            MouseFilter = MouseFilterEnum.Ignore
        };
        background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(background);

        var margin = new MarginContainer();
        margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", 40);
        margin.AddThemeConstantOverride("margin_top", 40);
        margin.AddThemeConstantOverride("margin_right", 40);
        margin.AddThemeConstantOverride("margin_bottom", 40);
        AddChild(margin);

        var content = new VBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Center
        };
        content.AddThemeConstantOverride("separation", 28);
        margin.AddChild(content);

        var title = new Label
        {
            Text = "官方卡面组件 · 规则状态验收",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        title.AddThemeFontSizeOverride("font_size", 26);
        content.AddChild(title);

        var row = new HBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Center,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        row.AddThemeConstantOverride("separation", 18);
        content.AddChild(row);

        var imagePath = FindCachedOfficialCard();
        for (var index = 0; index < States.Length; index++)
        {
            var item = new VBoxContainer
            {
                CustomMinimumSize = new Vector2(154, 0),
                Alignment = BoxContainer.AlignmentMode.Center
            };
            item.AddThemeConstantOverride("separation", 12);
            row.AddChild(item);

            var card = GD.Load<PackedScene>("res://scenes/components/OfficialCardView.tscn")
                .Instantiate<OfficialCardView>();
            card.CustomMinimumSize = new Vector2(154, 215);
            card.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
            card.SizeFlagsVertical = SizeFlags.ShrinkCenter;
            var hidden = States[index].State == OfficialCardVisualState.Hidden;
            card.Display(new Godot.Collections.Dictionary
            {
                ["visible"] = !hidden,
                ["faceDown"] = hidden,
                ["cardNo"] = "042/221",
                ["cardName"] = "Official card",
                ["previewSummary"] = "Official card face · complete aspect ratio",
                ["imagePath"] = imagePath,
                ["count"] = index == 1 ? 3 : 1
            }, States[index].State);
            item.AddChild(card);

            var label = new Label
            {
                Text = States[index].Label,
                HorizontalAlignment = HorizontalAlignment.Center,
                CustomMinimumSize = new Vector2(154, 30)
            };
            label.AddThemeColorOverride("font_color", MinimalTheme.TextSecondary);
            label.AddThemeFontSizeOverride("font_size", 16);
            item.AddChild(label);
        }

        MinimalTheme.Apply(this);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await CaptureWhenRequested();
    }

    private static string FindCachedOfficialCard()
    {
        var cacheRoot = ProjectSettings.GlobalizePath("user://official-card-cache");
        if (!Directory.Exists(cacheRoot))
        {
            return string.Empty;
        }

        return Directory.EnumerateFiles(cacheRoot)
            .Where(path => path.EndsWith(".png")
                || path.EndsWith(".jpg")
                || path.EndsWith(".jpeg")
                || path.EndsWith(".webp"))
            .OrderBy(path => path)
            .FirstOrDefault() ?? string.Empty;
    }

    private async System.Threading.Tasks.Task CaptureWhenRequested()
    {
        const string prefix = "--riftbound-proof-capture=";
        var argument = OS.GetCmdlineUserArgs()
            .FirstOrDefault(value => value.StartsWith(prefix));
        if (argument is null)
        {
            return;
        }

        await ToSignal(GetTree().CreateTimer(1.0), Godot.Timer.SignalName.Timeout);
        var resourcePath = argument[prefix.Length..];
        var absolutePath = ProjectSettings.GlobalizePath(resourcePath);
        Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);
        using var image = GetViewport().GetTexture().GetImage();
        var error = image.SavePng(absolutePath);
        if (error != Error.Ok)
        {
            GD.PushError($"Unable to save proof screenshot: {error}");
        }

        GetTree().Quit(error == Error.Ok ? 0 : 1);
    }
}
