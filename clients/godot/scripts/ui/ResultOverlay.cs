using System;
using Godot;

namespace Riftbound.GodotClient.Ui;

public partial class ResultOverlay : Control
{
    public event Action? ReturnLobbyRequested;

    private Label _headline = null!;
    private Label _winner = null!;
    private Label _score = null!;
    private Label _reason = null!;
    private Button _returnButton = null!;

    public override void _Ready()
    {
        _headline = GetNode<Label>("%ResultHeadline");
        _winner = GetNode<Label>("%WinnerSummary");
        _score = GetNode<Label>("%ScoreSummary");
        _reason = GetNode<Label>("%ReasonSummary");
        _returnButton = GetNode<Button>("%ReturnButton");
        _returnButton.Pressed += OnReturnPressed;

        ApplyTheme();
        HideResult();
    }

    public void ApplyTheme()
    {
        MinimalTheme.Apply(this);
        GetNode<PanelContainer>("%ResultPanel")
            .AddThemeStyleboxOverride("panel", MinimalTheme.Panel(MinimalTheme.SurfaceRaised));
        GetNode<Label>("%ResultKicker").AddThemeColorOverride("font_color", MinimalTheme.TextSecondary);
        _headline?.AddThemeFontSizeOverride("font_size", 40);
        _winner?.AddThemeFontSizeOverride("font_size", 20);
        _score?.AddThemeFontSizeOverride("font_size", 20);
        _reason?.AddThemeColorOverride("font_color", MinimalTheme.TextSecondary);
    }

    public void ShowResult(Godot.Collections.Dictionary result)
    {
        if (!IsNodeReady())
        {
            return;
        }

        var outcome = SafeOutcome(ReadString(result, "outcome", "对局结束"));
        var winner = SafeWinner(ReadString(result, "winner", string.Empty));
        var score = ReadInt(result, "score", 0);
        var reason = ReadString(result, "reason", "服务端确认对局结束");

        _headline.Text = outcome;
        _headline.AddThemeColorOverride(
            "font_color",
            outcome == "胜利" ? MinimalTheme.Selectable : outcome == "失败" ? MinimalTheme.Hostile : MinimalTheme.Text);
        _winner.Text = string.IsNullOrWhiteSpace(winner) ? "胜者：未公布" : $"胜者：{winner}";
        _score.Text = score > 0 ? $"胜利分数：{score}" : "胜利分数：未公布";
        _reason.Text = $"原因：{reason}";
        _returnButton.Disabled = false;

        Visible = true;
        MoveToFront();
        _returnButton.GrabFocus();
    }

    public void HideResult()
    {
        Visible = false;
        if (IsNodeReady())
        {
            _returnButton.Disabled = false;
        }
    }

    private void OnReturnPressed()
    {
        _returnButton.Disabled = true;
        ReturnLobbyRequested?.Invoke();
    }

    private static string SafeOutcome(string value)
    {
        return value switch
        {
            "胜利" => "胜利",
            "失败" => "失败",
            _ => "对局结束"
        };
    }

    private static string SafeWinner(string value)
    {
        return value switch
        {
            "你" => "你",
            "对手" => "对手",
            _ => string.Empty
        };
    }

    private static string ReadString(
        Godot.Collections.Dictionary dictionary,
        string key,
        string fallback)
    {
        return dictionary.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value.AsString())
            ? value.AsString()
            : fallback;
    }

    private static int ReadInt(
        Godot.Collections.Dictionary dictionary,
        string key,
        int fallback)
    {
        return dictionary.TryGetValue(key, out var value)
            ? Math.Max(0, value.AsInt32())
            : fallback;
    }
}
