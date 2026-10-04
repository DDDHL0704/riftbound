using System;
using System.IO;
using Riftbound.Contracts;
using System.Linq;
using System.Text.Json;
using Godot;
using Riftbound.GodotClient.Ui;

namespace Riftbound.GodotClient.Debug;

// Reads the candidate exported by OfficialPrintedPowerCostTests; no invented legal choices.
public partial class PlayCardOverlayProof : Control
{
    public override void _Ready()
    {
        string? Argument(string prefix) => OS.GetCmdlineUserArgs().FirstOrDefault(x => x.StartsWith(prefix))?[prefix.Length..];
        var path = Argument("--candidate=");
        if (path is null) { GD.PushError("Pass --candidate=<server candidate JSON>"); GetTree().Quit(1); return; }
        using var candidate = JsonDocument.Parse(File.ReadAllText(path));
        var overlay = new PlayCardOverlay(); AddChild(overlay);
        if (OS.GetCmdlineUserArgs().Contains("--verify-preview"))
        {
            VerifyPreviewFreshness(overlay, candidate.RootElement);
            return;
        }
        overlay.Confirmed += command =>
        {
            var payload = JsonSerializer.Serialize(command);
            GD.Print("PLAY_CARD_PROOF_COMMAND=" + payload);
            if (Argument("--command-output=") is { } output) File.WriteAllText(output, payload);
            overlay.ApplyReceipt("PAYMENT-PROOF", 1, true, "");
        };
        overlay.Open(candidate.RootElement, "PAYMENT-PROOF", 1, id => id is "CARD" or "blue" or "green" ? new Godot.Collections.Dictionary
        {
            ["visible"] = true, ["objectId"] = "CARD", ["cardName"] = id == "blue" ? "灵光符文" : id == "green" ? "翠意符文" : "炉火斗篷",
            ["cardNo"] = "SFD·190/221", ["imagePath"] = Argument("--card-image=") ?? "",
            ["zone"] = "HAND", ["owner"] = "self", ["previewSummary"] = "炉火斗篷 · 装备"
        } : null);
    }

    private void VerifyPreviewFreshness(PlayCardOverlay overlay, JsonElement candidate)
    {
        PlayCostPreviewRequestDto? latest = null;
        overlay.PreviewRequested += request => latest = request;
        void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        try
        {
            Check(overlay.Open(candidate, "FIRST", 1, _ => null), "Initial candidate must open");
            var old = latest!;
            Check(overlay.Open(candidate, "SECOND", 2, _ => null), "New prompt must open");
            var current = latest!;
            var confirm = (Button)overlay.FindChild("ConfirmPlayCardButton", true, false);
            Check(confirm.Disabled, "No quote must disable submission");
            overlay.ApplyQuote(new(old.RequestId, old.PromptId, old.SnapshotTick, true, true, "Old approval"));
            Check(confirm.Disabled, "Old approval must not enable submission");
            overlay.ApplyQuote(new(current.RequestId, current.PromptId, current.SnapshotTick, true, false, "Insufficient"));
            Check(confirm.Disabled, "Insufficient quote must disable submission");
            overlay.ApplyQuote(new(current.RequestId, current.PromptId, current.SnapshotTick, true, true, "Current approval"));
            Check(!confirm.Disabled, "Current approval must enable submission");
            overlay.ApplyQuote(new(old.RequestId, current.PromptId, current.SnapshotTick, true, false, "Old selection"));
            overlay.ApplyQuote(new(current.RequestId, old.PromptId, current.SnapshotTick, true, false, "Old prompt"));
            overlay.ApplyQuote(new(current.RequestId, current.PromptId, old.SnapshotTick, true, false, "Old tick"));
            Check(!confirm.Disabled, "Late responses must not replace the current result");
            overlay.Open(candidate, "SECOND", 2, _ => null);
            Check(confirm.Disabled, "Reopening starts a new request");
            overlay.ApplyQuote(new(current.RequestId, current.PromptId, current.SnapshotTick, true, true, "Previous opening"));
            Check(confirm.Disabled, "Previous opening cannot authorize this submission");
            GD.Print("PLAY_COST_PREVIEW_FRESHNESS_PASS");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }
}
