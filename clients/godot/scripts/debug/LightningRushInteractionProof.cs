using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text.Json;
using Godot;
using Riftbound.Contracts;
using Riftbound.GodotClient.Interaction;
using Riftbound.GodotClient.Ui;

namespace Riftbound.GodotClient.Debug;

// Actual controls consume the original SQL prompts. Prices are calculated by
// the frozen backend from the original state and the native-generated request.
// This is control/replay equivalence, not a live or visible-client claim.
public partial class LightningRushInteractionProof : Control
{
    public override void _Ready()
    {
        try {
            var root = OS.GetCmdlineUserArgs().Single(a => a.StartsWith("--evidence="))[11..];
            using var index = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "index.json")));
            var prefix = index.RootElement.GetProperty("quoteCommandPrefix").EnumerateArray().Select(v => v.GetString()!).ToArray();
            var identity = index.RootElement.GetProperty("actualRuleInputs").GetProperty("identitySha256").GetString();
            var map = typeof(Main).GetMethod("BuildPromptView", BindingFlags.Static | BindingFlags.NonPublic)!;
            var build = typeof(Main).GetMethod("CommandFromTemplate", BindingFlags.Static | BindingFlags.NonPublic)!;
            var selectionType = typeof(Main).GetNestedType("PromptSelection", BindingFlags.NonPublic)!;
            WriteLoadedClient(root);
            var count = 0; var plays = 0; var prices = 0; var flowOrigins = 0;
            foreach (var row in index.RootElement.GetProperty("entries").EnumerateArray()) {
                using var original = JsonDocument.Parse(File.ReadAllText(row.GetProperty("originalCommandPath").GetString()!));
                using var prompt = JsonDocument.Parse(File.ReadAllText(row.GetProperty("promptPath").GetString()!));
                var raw = original.RootElement; var action = raw.GetProperty("cmdType").GetString()!;
                var candidate = prompt.RootElement.GetProperty("candidates").EnumerateArray().Single(c => c.GetProperty("action").GetString() == action);
                var promptId = prompt.RootElement.GetProperty("promptId").GetString()!; var tick = prompt.RootElement.GetProperty("snapshotTick").GetInt64();
                Dictionary<string, object?>? command = null;
                if (action == "PLAY_CARD") {
                    var overlay = new PlayCardOverlay(); AddChild(overlay); var allowPrices = false; var emits = 0;
                    PlayCostQuoteDto? lastQuote = null;
                    overlay.PreviewRequested += request => {
                        if (!allowPrices) return;
                        var requestPath = Path.Combine(root, $"native-price-request-{count:D2}-{prices:D3}.json");
                        File.WriteAllText(requestPath, JsonSerializer.Serialize(request, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
                        var start = new System.Diagnostics.ProcessStartInfo(prefix[0]) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
                        foreach (var argument in prefix.Skip(1).Concat(new[] { "--quote", row.GetProperty("beforeFramePath").GetString()!, row.GetProperty("playerId").GetString()!, requestPath })) start.ArgumentList.Add(argument);
                        using var process = System.Diagnostics.Process.Start(start)!;
                        var response = process.StandardOutput.ReadToEnd(); var error = process.StandardError.ReadToEnd();
                        if (!process.WaitForExit(15000)) { process.Kill(true); throw new InvalidOperationException("Owned frozen price process timed out."); }
                        Check(process.ExitCode == 0, "Actual frozen backend price request failed: " + error);
                        File.WriteAllText(Path.Combine(root, $"native-price-response-{count:D2}-{prices:D3}.json"), response);
                        using var document = JsonDocument.Parse(response);
                        Check(document.RootElement.GetProperty("loaded").GetProperty("actualRuleInputs").GetProperty("identitySha256").GetString() == identity, "Every actual backend price uses the recorded candidate.");
                        lastQuote = document.RootElement.GetProperty("quote").Deserialize<PlayCostQuoteDto>(new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
                        Check(lastQuote.RequestId == request.RequestId && lastQuote.PromptId == request.PromptId && lastQuote.SnapshotTick == request.SnapshotTick, "Actual native request/quote identities are unmodified.");
                        prices++; overlay.ApplyQuote(lastQuote);
                    };
                    var source = raw.GetProperty("sourceObjectId").GetString()!;
                    Check(overlay.Open(candidate, promptId, tick, _ => null, source), "The original server source opens its production composer.");
                    var confirm = (Button)overlay.FindChild("ConfirmPlayCardButton", true, false);
                    Check(confirm.Disabled, "No price approval cannot submit a card.");
                    allowPrices = true; Check(overlay.Open(candidate, promptId, tick, _ => null, source), "Reopen with the actual frozen price endpoint.");
                    var targets = (List<(OptionButton Picker, string[] Ids, bool Required)>)typeof(PlayCardOverlay).GetField("_targets", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(overlay)!;
                    var chosen = Strings(raw, "targetObjectIds"); Check(chosen.Length <= targets.Count, "All original casting targets have real pickers.");
                    for (var i = 0; i < chosen.Length; i++) {
                        var position = Array.IndexOf(targets[i].Ids, chosen[i]); Check(position > 0, "Only a server-advertised target may be chosen.");
                        targets[i].Picker.Select(position); targets[i].Picker.EmitSignal(OptionButton.SignalName.ItemSelected, (long)position);
                    }
                    var optional = (Dictionary<string, CheckBox>)typeof(PlayCardOverlay).GetField("_optional", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(overlay)!;
                    foreach (var cost in Strings(raw, "optionalCosts")) { Check(optional.ContainsKey(cost), "The exact paid original cost has a production checkbox."); optional[cost].ButtonPressed = true; }
                    var requirement = candidate.GetProperty("metadata").GetProperty("sourceRequirements").EnumerateArray().First(r => r.GetProperty("sourceObjectId").GetString() == source);
                    if (requirement.GetProperty("sourceZone").GetString() == "GRAVEYARD") {
                        var origin = (Label)typeof(PlayCardOverlay).GetField("_origin", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(overlay)!;
                        Check(origin.Visible && origin.Text.Contains("流转", StringComparison.Ordinal), "The real composer explains the graveyard Flow origin."); flowOrigins++;
                    }
                    Check(lastQuote is { CanPay: true } && !confirm.Disabled, "Only the actual current legal and affordable selection enables confirmation.");
                    overlay.Confirmed += value => { emits++; command = value; };
                    confirm.EmitSignal(BaseButton.SignalName.Pressed); confirm.EmitSignal(BaseButton.SignalName.Pressed);
                    Check(emits == 1 && command is not null, "Production confirmation emits exactly once while pending.");
                    overlay.ApplyReceipt("STALE", tick - 1, true, "late"); confirm.EmitSignal(BaseButton.SignalName.Pressed); Check(emits == 1, "A stale receipt cannot unlock duplicate submission.");
                    allowPrices = false; overlay.Open(candidate, "NEW-SERVER-WINDOW", tick + 1, _ => null, source);
                    overlay.ApplyQuote(lastQuote!); Check(confirm.Disabled, "The earlier real quote cannot authorize a new prompt/tick.");
                    command!["promptId"] = promptId; command["snapshotTick"] = tick; overlay.Free(); plays++;
                } else {
                    Check(action is "PAY_COST" or "CHOOSE_CARDS" or "PASS_PRIORITY", "Only actual gameplay intents are tested as native controls.");
                    var view = (Godot.Collections.Dictionary)map.Invoke(null, [prompt.RootElement])!;
                    var controller = new PromptInteractionController(); controller.Load(view);
                    var bar = GD.Load<PackedScene>("res://scenes/components/ActionBar.tscn").Instantiate<ActionBar>(); AddChild(bar);
                    bar.ActionSelected += id => Check(controller.SelectAction(id), "Actual original action is available.");
                    bar.ChoiceSelected += (role, id) => Check(controller.TrySelectChoice(role, id), "Real native choice is server-advertised.");
                    controller.SelectionChanged += state => bar.ShowSelection(state, controller.CurrentChoices, controller.CurrentStepLabel, controller.CurrentStepRequired);
                    controller.SelectionCleared += bar.ClearSelectionDisplay; bar.ShowPrompt(view["message"].AsString(), controller.Actions);
                    var label = controller.Actions.Single(a => a.Name == action).Label;
                    var actionButton = action == "PASS_PRIORITY"
                        ? (Button)typeof(ActionBar).GetField("_primaryAction", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(bar)!
                        : bar.GetNode<HBoxContainer>("%ActionChoices").GetChildren().OfType<Button>().Single(b => b.Text == label);
                    Check(!actionButton.Disabled, "The existing production action button is enabled.");
                    actionButton.EmitSignal(BaseButton.SignalName.Pressed);
                    Check(!controller.TrySelectChoice("target", "FORGED-CHOICE"), "A fabricated card or payment choice is never selectable.");
                    foreach (var id in Strings(raw, action == "PAY_COST" ? "paymentChoiceIds" : "chosenObjectIds")) {
                        if (controller.Current!.TargetIds.Contains(id)) continue;
                        var position = controller.CurrentChoices.ToList().FindIndex(c => c.Id == id); Check(position >= 0, "The exact original choice has a native button.");
                        bar.GetNode<HBoxContainer>("%StepChoices").GetChild<Button>(position).EmitSignal(BaseButton.SignalName.Pressed);
                    }
                    var submit = bar.GetNode<Button>("%SubmitButton"); Check(!submit.Disabled, "The original selection is submittable.");
                    bar.SubmitRequested += selection => {
                        var value = Activator.CreateInstance(selectionType, [selection.SourceId, selection.TargetIds.ToArray(), selection.DestinationId, selection.Mode, selection.OptionalCostIds.ToArray()]);
                        command = (Dictionary<string, object?>)build.Invoke(null, [candidate, value, controller.PromptId, controller.SnapshotTick])!; bar.SetPending(true);
                    };
                    bar.SetPending(true); submit.EmitSignal(BaseButton.SignalName.Pressed); Check(command is null, "Pending input cannot submit.");
                    bar.SetPending(false); submit.EmitSignal(BaseButton.SignalName.Pressed); Check(command is not null, "The actual button emits the production command.");
                    controller.Load(new Godot.Collections.Dictionary { ["promptId"] = "NEW-SERVER-WINDOW", ["snapshotTick"] = tick + 1,
                        ["actions"] = new Godot.Collections.Array<Godot.Collections.Dictionary>() });
                    Check(controller.Current is null, "A fresh server window clears the original selection."); bar.Free();
                }
                var wire = ClientCommandSerializer.Serialize(command!);
                Check(wire.GetProperty("cmdType").GetString() == action && wire.GetProperty("promptId").GetString() == promptId
                    && wire.GetProperty("snapshotTick").GetInt64() == tick, "Every emitted packet retains its actual original command and prompt identity.");
                File.WriteAllText(row.GetProperty("outputCommandPath").GetString()!, wire.GetRawText()); count++;
            }
            Check(count > 0 && plays > 0 && prices > 0 && flowOrigins > 0, "Original production choices, plays, actual backend prices and Flow cues all execute.");
            File.WriteAllText(Path.Combine(root, "native-result.json"), JsonSerializer.Serialize(new { commands = count, playComposers = plays, actualBackendPrices = prices,
                actualFlowOriginLabels = flowOrigins, offlineControlAndReplayEquivalenceOnly = true, visiblePairAndPhysicalWindowsRemainOpen = true }));
            GD.Print($"LIGHTNING_RUSH_NATIVE_INTERACTION_PASS: {count} original button packets; {plays} play composers; {prices} actual frozen backend prices; {flowOrigins} Flow origins");
            GetTree().Quit();
        } catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }

    private static string[] Strings(JsonElement raw, string field) => raw.TryGetProperty(field, out var value) && value.ValueKind == JsonValueKind.Array
        ? value.EnumerateArray().Select(v => v.GetString()!).ToArray() : [];
    private static void Check(bool value, string reason) { if (!value) throw new InvalidOperationException(reason); }
    private static void WriteLoadedClient(string root) {
        File.WriteAllText(Path.Combine(root, "actual-loaded-client-assemblies.json"), JsonSerializer.Serialize(new {
            baseDirectory = AppContext.BaseDirectory, assemblies = new[] { typeof(Main).Assembly, typeof(SnapshotDto).Assembly }.Select(a => {
                var path = ProjectSettings.GlobalizePath("res://.godot/mono/temp/bin/Debug/" + a.GetName().Name + ".dll");
                using var stream = File.OpenRead(path); using var pe = new PEReader(stream); var metadata = pe.GetMetadataReader();
                var mvid = metadata.GetGuid(metadata.GetModuleDefinition().Mvid); Check(mvid == a.ManifestModule.ModuleVersionId, "Actual running module and pinned candidate MVID agree.");
                return new { name = a.GetName().Name, actualReportedLocation = a.Location, mvid = a.ManifestModule.ModuleVersionId,
                    candidateDiskPath = path, candidateDiskMvid = mvid, candidateDiskSha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant(),
                    diskHashDirectlyRepresentsReportedLoadedPath = a.Location.Length > 0 && a.Location == path };
            }).ToArray() }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
