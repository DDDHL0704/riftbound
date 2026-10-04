using System.IO;
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
}
