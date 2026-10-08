using System.Text.Json;
using Godot;
using Riftbound.Contracts;
using Riftbound.GodotClient.Ui;

namespace Riftbound.GodotClient.Debug;

// Uses actual server candidates, never local rule guesses, for the native controls.
public partial class RuneHeroInteractionProof : Control
{
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    public override void _Ready()
    {
        try
        {
            var session = new PlayerSessionSettings("Player", "ROOM", "test-player-key-16", "saved-token", ServerUrl: "http://127.0.0.1:15103");
            Check(PlayerSessionSettings.WithConnectionTarget(session, "player", "ROOM", "http://127.0.0.1:15103/").ReconnectToken == "saved-token", "Entering the saved room must retain its reconnect credential");
            Check(PlayerSessionSettings.WithConnectionTarget(session, "Player", "OTHER", session.ServerUrl).ReconnectToken is null, "Another room must not reuse a reconnect credential");
            Check(PlayerSessionSettings.WithConnectionTarget(session, "Other", "ROOM", session.ServerUrl).ReconnectToken is null, "Another identity must not reuse a reconnect credential");
            Check(PlayerSessionSettings.WithConnectionTarget(session, "Player", "ROOM", "http://127.0.0.1:15104").ReconnectToken is null, "Another server must not reuse a reconnect credential");
            var root = OS.GetCmdlineUserArgs().First(x => x.StartsWith("--evidence="))[11..];
            using var promptJson = JsonDocument.Parse(File.ReadAllText(root + "/qa1-prompt.json"));
            using var opponentJson = JsonDocument.Parse(File.ReadAllText(root + "/qa2-prompt.json"));
            var first = promptJson.RootElement.GetProperty("payload");
            var prompt = first.GetProperty("candidates").EnumerateArray().Any(c => c.GetProperty("action").GetString() == "TAP_RUNE")
                ? first : opponentJson.RootElement.GetProperty("payload");
            var candidates = prompt.GetProperty("candidates").EnumerateArray().ToArray();
            string[] Sources(string action) => candidates.First(c => c.GetProperty("action").GetString() == action)
                .GetProperty("sources").EnumerateArray().Select(s => s.GetProperty("id").GetString()!).ToArray();
            var tap = Sources("TAP_RUNE"); var recycle = Sources("RECYCLE_RUNE");
            Check(tap.Length >= 2, "Official opening must provide multiple ready runes");
            var panel = new RuneActionPanel(); AddChild(panel);
            panel.Load("RUNES", 1, tap, recycle);
            var count = 0; string[] submitted = [];
            panel.Requested += (_, ids) => { count++; submitted = ids; };
            Check(!panel.Toggle("opponent-or-unknown"), "Unknown rune cannot be selected");
            Check(panel.Toggle(tap[0]) && panel.Toggle(tap[1]) && panel.Selected.Count == 2, "Table toggles must retain both runes");
            Check(panel.Request("TAP_RUNE", panel.Selected.ToArray()), "Selected ready runes can be submitted as one request");
            Check(count == 1 && submitted.Length == 2, "Batch must emit one request with both identities");
            Check(!panel.Request("TAP_RUNE", [tap[0]]) && !panel.Toggle(tap[0]), "Pending resource command must block duplicates and edits");
            panel.ApplyReceipt("OLD", 0, true, "old");
            Check(panel.IsSubmitting, "Stale receipt must not unlock a pending command");
            panel.ApplyReceipt("RUNES", 1, false, "测试拒绝");
            Check(!panel.IsSubmitting && panel.Selected.Count == 2, "Rejection must preserve the batch selection");
            panel.Load("NEXT", 2, [tap[1]], recycle);
            Check(!panel.Visible && panel.Selected.Count == 0, "Changed prompt must clear stale selection");
            Check(!panel.Request("TAP_RUNE", [tap[0]]), "Previously legal rune cannot authorize a new request");
            Check(panel.Request("RECYCLE_RUNE", [recycle[0]]), "Direct single recycle needs no action wizard");
            panel.ApplyReceipt("NEXT", 2, true, "ok");
            Check(!panel.IsSubmitting && !panel.Visible, "Accepted direct action must leave the table idle");
            panel.SelectAll(true); Check(panel.Selected.Count == recycle.Length, "Select all uses server-authorized recycle choices");
            panel.Cancel(); Check(panel.Selected.Count == 0 && !panel.Visible, "Cancel must clear the entire selection");
            Check(panel.Request("TAP_RUNE", [tap[1]]), "Direct tap can start from the current prompt");
            panel.Load("AUTHORITATIVE", 3, [], recycle);
            Check(!panel.IsSubmitting, "A new authoritative prompt must release the old pending action");
            Check(panel.Request("RECYCLE_RUNE", [recycle[0]]), "The next resource action can begin before a late receipt");
            panel.ApplyReceipt("NEXT", 2, true, "late");
            Check(panel.IsSubmitting, "Late previous receipt must not unlock the next resource action");
            panel.ApplyReceipt("AUTHORITATIVE", 3, true, "ok");

            using var heroJson = JsonDocument.Parse(File.ReadAllText(root + "/hero-candidate.json"));
            var hero = heroJson.RootElement.GetProperty("metadata").GetProperty("sourceRequirements").EnumerateArray()
                .First(r => r.GetProperty("sourceZone").GetString() == "CHAMPION");
            var heroId = hero.GetProperty("sourceObjectId").GetString()!;
            var overlay = new PlayCardOverlay { TableMode = true }; AddChild(overlay);
            PlayCostPreviewRequestDto? request = null; overlay.PreviewRequested += value => request = value;
            Check(overlay.Open(heroJson.RootElement, "HERO", 3, _ => null, heroId), "Champion click must open the normal play composer");
            Check(request?.Command.SourceObjectId == heroId, "Champion identity must reach the cost-preview request");
            Check(!overlay.Open(heroJson.RootElement, "HERO", 3, _ => null, "other-hero"), "Opponent/unknown hero cannot open a legal play composer");
            GD.Print("RUNE_HERO_INTERACTION_PASS"); GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }
}
