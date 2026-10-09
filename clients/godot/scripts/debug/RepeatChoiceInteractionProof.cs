using System.Collections;
using System.Reflection;
using System.Text.Json;
using Godot;
using Riftbound.Contracts;
using Riftbound.GodotClient.Interaction;
using Riftbound.GodotClient.Ui;

namespace Riftbound.GodotClient.Debug;

public partial class RepeatChoiceInteractionProof : Control
{
    public override async void _Ready()
    {
        try
        {
            GetWindow().ContentScaleSize = new Vector2I(560, 1000);
            var root = OS.GetCmdlineUserArgs().First(a => a.StartsWith("--evidence="))[11..];
            var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
            using var candidate = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "native-candidate.json")));
            var quote = JsonSerializer.Deserialize<PlayCostQuoteDto>(File.ReadAllText(Path.Combine(root, "native-quote.json")), json)!;
            var overlay = new PlayCardOverlay { TableMode = true };
            AddChild(overlay);
            PlayCostPreviewRequestDto? request = null;
            Dictionary<string, object?>? command = null;
            overlay.PreviewRequested += value => request = value;
            overlay.Confirmed += value => command = value;
            Check(overlay.Open(candidate.RootElement, quote.PromptId, quote.SnapshotTick, id => id == "CARD" ? new Godot.Collections.Dictionary
            { ["visible"] = true, ["objectId"] = "CARD", ["cardName"] = "火箭轰击", ["cardNo"] = "SFD·077/221", ["imagePath"] = CachedRocketImage(), ["zone"] = "HAND", ["owner"] = "self" } : null), "Open real candidate");
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var source = (OptionButton)typeof(PlayCardOverlay).GetField("_source", flags)!.GetValue(overlay)!;
            var requirements = (List<JsonElement>)typeof(PlayCardOverlay).GetField("_requirements", flags)!.GetValue(overlay)!;
            var modeIndex = requirements.FindIndex(r => r.GetProperty("mode").GetString() == "BASE_UNIT_DAMAGE_4");
            source.Select(modeIndex); source.EmitSignal(OptionButton.SignalName.ItemSelected, (long)modeIndex);
            var targets = (List<(OptionButton Picker, string[] Ids, bool Required)>)typeof(PlayCardOverlay).GetField("_targets", flags)!.GetValue(overlay)!;
            var initialIndex = Array.IndexOf(targets[0].Ids, "U1");
            targets[0].Picker.Select(initialIndex); targets[0].Picker.EmitSignal(OptionButton.SignalName.ItemSelected, (long)initialIndex);
            var optional = (Dictionary<string, CheckBox>)typeof(PlayCardOverlay).GetField("_optional", flags)!.GetValue(overlay)!;
            optional["ECHO"].ButtonPressed = true;
            var repeats = (IList)typeof(PlayCardOverlay).GetField("_repeats", flags)!.GetValue(overlay)!;
            Check(repeats.Count == 1, "Echo creates exactly one additional execution");
            var entry = repeats[0]!;
            var entryMode = (OptionButton)entry.GetType().GetField("Mode")!.GetValue(entry)!;
            var modes = (JsonElement[])entry.GetType().GetField("Modes")!.GetValue(entry)!;
            var secondMode = Array.FindIndex(modes, r => r.GetProperty("mode").GetString() == "DESTROY_EQUIPMENT");
            entryMode.Select(secondMode); entryMode.EmitSignal(OptionButton.SignalName.ItemSelected, (long)secondMode);
            var secondTargets = (List<(OptionButton Picker, string[] Ids, bool Required)>)entry.GetType().GetField("Targets")!.GetValue(entry)!;
            var equipmentIndex = Array.IndexOf(secondTargets[0].Ids, "E1");
            secondTargets[0].Picker.GrabFocus();
            Check(overlay.TableTargets.Contains("E1"), "Focused repeat target highlights its legal table objects");
            Check(overlay.TrySelectTableObject("E1"), "Clicking the table chooses the focused repeat target");
            Check(overlay.TableSelectedObjects.Contains("E1") && overlay.TableSelectedObjects.Contains("U1"), "Both executions stay selected");
            Check(request!.Command.RepeatChoices![0].Mode == "DESTROY_EQUIPMENT", "Preview includes independent repeat mode");
            Check(request.Command.RepeatChoices[0].TargetObjectIds.SequenceEqual(["E1"]), "Preview includes repeat target");
            var confirm = (Button)overlay.FindChild("ConfirmPlayCardButton", true, false);
            Check(confirm.Disabled, "Must await server quote");
            overlay.ApplyQuote(quote with { RequestId = request.RequestId, PromptId = request.PromptId, SnapshotTick = request.SnapshotTick });
            Check(!confirm.Disabled, "Authoritative quote enables play");
            for (var i = 0; i < 3; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Check(confirm.GetGlobalRect().End.Y <= GetViewportRect().End.Y, "Confirmation remains visible");
            if (OS.GetCmdlineUserArgs().Contains("--visual-proof"))
            {
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                GetViewport().GetTexture().GetImage().SavePng(Path.Combine(root, "native-repeat-choice.png"));
            }
            confirm.EmitSignal(BaseButton.SignalName.Pressed);
            Check(command is not null, "Submit emits a command");
            Check(overlay.PromptId == quote.PromptId && overlay.SnapshotTick == quote.SnapshotTick, "Overlay preserves the actual backend prompt identity");
            // Main.SubmitPromptPayloadAsync adds this envelope before transport.
            command!["promptId"] = overlay.PromptId;
            command["snapshotTick"] = overlay.SnapshotTick;
            File.WriteAllText(Path.Combine(root, "native-command.json"), JsonSerializer.Serialize(command, json));

            using var deckPrompt = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "deck-choice-prompt.json")));
            var map = typeof(Main).GetMethod("BuildPromptView", BindingFlags.Static | BindingFlags.NonPublic)!;
            var view = (Godot.Collections.Dictionary)map.Invoke(null, [deckPrompt.RootElement])!;
            var controller = new PromptInteractionController(); controller.Load(view);
            Check(controller.Current is { ActionName: "CHOOSE_CARDS", CanSubmit: false }, "Private choice automatically opens and requires selection");
            Check(!controller.TrySelectObject("D3"), "Cannot select a card outside the viewed top two");
            Check(controller.TrySelectObject("D2"), "Can select the second viewed card");
            Check(controller.Current is { CanSubmit: true }, "Selection enables confirm");
            var choiceCandidate = deckPrompt.RootElement.GetProperty("candidates").EnumerateArray().Single(c => c.GetProperty("action").GetString() == "CHOOSE_CARDS");
            var selectionType = typeof(Main).GetNestedType("PromptSelection", BindingFlags.NonPublic)!;
            var selection = Activator.CreateInstance(selectionType, [null, new[] { "D2" }, null, null, Array.Empty<string>()]);
            var build = typeof(Main).GetMethod("CommandFromTemplate", BindingFlags.Static | BindingFlags.NonPublic)!;
            var payload = (Dictionary<string, object?>)build.Invoke(null, [choiceCandidate, selection, controller.PromptId, controller.SnapshotTick])!;
            Check(payload is not null && ((IEnumerable<string>)payload["chosenObjectIds"]!).SequenceEqual(["D2"]), "Template preserves actual private selection");
            File.WriteAllText(Path.Combine(root, "native-deck-choice-command.json"), JsonSerializer.Serialize(payload, json));
            using var optionalPrompt = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "deck-optional-prompt.json")));
            controller.Load((Godot.Collections.Dictionary)map.Invoke(null, [optionalPrompt.RootElement])!);
            Check(controller.Current is { ActionName: "CHOOSE_CARDS", CanSubmit: true }, "Optional look selection can be declined without selecting a card");
            var optionalCandidate = optionalPrompt.RootElement.GetProperty("candidates").EnumerateArray().Single(c => c.GetProperty("action").GetString() == "CHOOSE_CARDS");
            var declineSelection = Activator.CreateInstance(selectionType, [null, Array.Empty<string>(), null, null, Array.Empty<string>()]);
            var declinedPayload = (Dictionary<string, object?>)build.Invoke(null, [optionalCandidate, declineSelection, controller.PromptId, controller.SnapshotTick])!;
            Check(declinedPayload is not null && !((IEnumerable<string>)declinedPayload["chosenObjectIds"]!).Any(), "Optional empty choice builds a submit command");
            File.WriteAllText(Path.Combine(root, "native-deck-decline-command.json"), JsonSerializer.Serialize(declinedPayload, json));
            GD.Print("REPEAT_CHOICE_INTERACTION_PASS: independent modes/targets, server quote, private deck selection, command templates");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }
    private static string CachedRocketImage()
    {
        const string url = "https://cdn.playloltcg.com/lol/card/20251217/0d5b2e3722054d83b42b3450d6f75ecf.png";
        var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(url))).ToLowerInvariant();
        var path = ProjectSettings.GlobalizePath("user://official-card-cache/" + hash + ".png");
        return File.Exists(path) ? path : "";
    }
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
