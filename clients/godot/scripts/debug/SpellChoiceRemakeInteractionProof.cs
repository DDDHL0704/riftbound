using System.IO.Compression;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Godot;
using Riftbound.GodotClient.Interaction;
using Riftbound.GodotClient.Ui;

namespace Riftbound.GodotClient.Debug;

// Original SQL projections and their accepted next intents; no live state injection.
public partial class SpellChoiceRemakeInteractionProof : Control
{
    public override void _Ready()
    {
        var errors = new List<string>();
        var commands = 0;
        var actualViews = 0;
        var syntheticStartViews = 0;
        try
        {
            var folder = OS.GetCmdlineUserArgs().Single(a => a.StartsWith("--evidence="))[11..];
            var output = OS.GetCmdlineUserArgs().Single(a => a.StartsWith("--output="))[9..];
            const string assemblyArgument = "--client-assembly=";
            var assemblyPath = OS.GetCmdlineUserArgs().Single(a => a.StartsWith(assemblyArgument))[assemblyArgument.Length..];
            using var assembly = new PEReader(File.OpenRead(assemblyPath));
            var metadata = assembly.GetMetadataReader();
            Require(metadata.GetGuid(metadata.GetModuleDefinition().Mvid) == typeof(Main).Module.ModuleVersionId, "Declared binary must be the actual loaded client module.");
            Directory.CreateDirectory(output);
            using var index = JsonDocument.Parse(File.ReadAllText(Path.Combine(folder, "index.json")));
            Require(index.RootElement.GetArrayLength() == 6, "All four ordinary choices and two original edit continuations are required.");
            foreach (var row in index.RootElement.EnumerateArray())
            {
                var name = row.GetProperty("name").GetString()!;
                var bytes = File.ReadAllBytes(row.GetProperty("framePath").GetString()!);
                Require(Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant() == row.GetProperty("frameSha256").GetString(), "Original frame identity.");
                using var zip = new GZipStream(new MemoryStream(bytes), CompressionMode.Decompress);
                using var frame = JsonDocument.Parse(zip);
                var views = frame.RootElement.GetProperty("playerViews");
                foreach (var viewer in new[] { "P1", "P2" })
                {
                    var snapshot = views.GetProperty(viewer).GetProperty("snapshot");
                    Require(snapshot.GetProperty("timing").GetProperty("pendingPayment").GetProperty("choiceKind").GetString() == "SPELL_CHOICE_REMAKE", "Actual public spell-choice window.");
                    Phase(snapshot, viewer, "original"); actualViews++;
                    var start = JsonNode.Parse(snapshot.GetRawText())!;
                    start["timing"]!["phase"] = "TURN_START";
                    using var control = JsonDocument.Parse(start.ToJsonString());
                    Phase(control.RootElement, viewer, "synthetic-turn-start"); syntheticStartViews++;
                }
                var prompt = views.GetProperty("P2").GetProperty("prompt");
                var expected = row.GetProperty("expectedOriginalNextRawCommand");
                var opponent = new PromptInteractionController();
                opponent.Load(Map(views.GetProperty("P1").GetProperty("prompt")));
                Require(!opponent.Actions.Any(a => a.Name == "PAY_COST" && a.Enabled), "Opponent cannot operate the controller's decision.");
                var view = Map(prompt);
                var controller = new PromptInteractionController(); controller.Load(view);
                var bar = GD.Load<PackedScene>("res://scenes/components/ActionBar.tscn").Instantiate<ActionBar>(); AddChild(bar);
                void Refresh() => bar.ShowSelection(controller.Current!, controller.CurrentChoices, controller.CurrentStepLabel, controller.CurrentStepRequired);
                bar.ActionSelected += id => { Require(controller.SelectAction(id), "Actual action button."); Refresh(); };
                bar.ChoiceSelected += (role, id) => { Require(controller.TrySelectChoice(role, id), "Actual server choice button."); Refresh(); };
                bar.ShowPrompt(view["message"].AsString(), controller.Actions);
                var action = controller.Actions.Single(a => a.Name == "PAY_COST");
                Require(action.Enabled && action.Label == "为法术做出新的选择", "Retain the correct server action label.");
                bar.GetNode<HBoxContainer>("%ActionChoices").GetChildren().OfType<Button>().Single(b => b.Text == action.Label).EmitSignal(BaseButton.SignalName.Pressed);
                var submit = bar.GetNode<Button>("%SubmitButton");
                Require(submit.Disabled && !controller.Current!.CanSubmit, "No payment choice is selected implicitly.");
                Require(!controller.TrySelectChoice("target", "FORGED"), "Only an actual server choice may be selected.");
                var choice = expected.GetProperty("paymentChoiceIds").EnumerateArray().Single().GetString()!;
                var choices = controller.CurrentChoices.ToArray();
                var selected = Array.FindIndex(choices, c => c.Id == choice);
                Require(selected >= 0 && controller.CurrentStepRole == "target", "Original accepted decision has a current native button.");
                bar.GetNode<HBoxContainer>("%StepChoices").GetChildren().OfType<Button>().ElementAt(selected).EmitSignal(BaseButton.SignalName.Pressed);
                Check(controller.Current!.Summary.Contains(choice.StartsWith("EDIT:", StringComparison.Ordinal) ? "继续选择" : "立即继续原结算"), name + ": summary must distinguish editing from completing the decision.");
                Dictionary<string, object?>? payload = null;
                var candidate = prompt.GetProperty("candidates").EnumerateArray().Single(c => c.GetProperty("action").GetString() == "PAY_COST");
                bar.SubmitRequested += selection => {
                    var type = typeof(Main).GetNestedType("PromptSelection", BindingFlags.NonPublic)!;
                    var value = Activator.CreateInstance(type, [selection.SourceId, selection.TargetIds.ToArray(), selection.DestinationId, selection.Mode, selection.OptionalCostIds.ToArray()]);
                    payload = (Dictionary<string, object?>)Method("CommandFromTemplate").Invoke(null, [candidate, value, controller.PromptId, controller.SnapshotTick])!;
                };
                Require(!submit.Disabled && controller.Current!.CanSubmit, "Explicit original decision enables confirmation.");
                submit.EmitSignal(BaseButton.SignalName.Pressed);
                File.WriteAllText(Path.Combine(output, name + "-native-command.json"), JsonSerializer.Serialize(payload));
                Require(payload is not null && JsonNode.DeepEquals(JsonNode.Parse(expected.GetRawText()), JsonSerializer.SerializeToNode(payload)), "Production button command must match the entire original accepted SQL intent, including prompt/tick and exact option.");
                commands++; bar.Free();

                void Phase(JsonElement snapshot, string viewer, string scope)
                {
                    var phase = MatchPhasePresentation.Build(snapshot, viewer, id => id);
                    Check(phase["title"].AsString() == "法术结算 · 重新选择", name + ": " + viewer + " " + scope + " spell-choice heading.");
                    Check(phase["detail"].AsString().Contains("保留原选择") && phase["detail"].AsString().Contains("重新选择") && phase["detail"].AsString().Contains("合法"), name + ": " + viewer + " " + scope + " keep/remake and complete-legality guidance.");
                    Require(phase["actor"].AsString() == (viewer == "P2" ? "我方完成选择" : "对手完成选择") && phase["activeWindow"].AsBool(), "Actual choice owner remains independent of the last responder and turn owner.");
                }
            }
            Require(commands == 6 && actualViews == 12 && syntheticStartViews == 12, "All original branches and explicit presentation controls ran.");
            File.WriteAllText(Path.Combine(output, "receipt.json"), JsonSerializer.Serialize(new {
                status = errors.Count == 0 ? "PASS_ORIGINAL_SQL_NATIVE_SPELL_CHOICE_CONTROLS_AND_PRESENTATION" : "FAIL_NATIVE_SPELL_CHOICE_PRESENTATION_ORIGINAL_INPUTS_RETAINED",
                commands, actualViews, syntheticStartViews, errors,
                actualClientAssemblySha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(assemblyPath))).ToLowerInvariant(),
                actualClientMvid = typeof(Main).Module.ModuleVersionId, displayDriver = DisplayServer.GetName(),
                syntheticTurnStartIsNotLiveGameEvidence = true, visibleOrPhysicalWindowsAcceptance = false }));
            foreach (var error in errors) GD.Print("SPELL_CHOICE_REMAKE_FAILURE: " + error);
            GD.Print($"SPELL_CHOICE_REMAKE_{(errors.Count == 0 ? "PASS" : "FAIL")}: {commands} original accepted native commands, {actualViews} actual projections, {syntheticStartViews} synthetic TURN_START controls, {errors.Count} failures");
            GetTree().Quit(errors.Count == 0 ? 0 : 1);
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
        void Check(bool condition, string message) { if (!condition) errors.Add(message); }
    }
    private static MethodInfo Method(string name) => typeof(Main).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic)!;
    private static Godot.Collections.Dictionary Map(JsonElement prompt) => (Godot.Collections.Dictionary)Method("BuildPromptView").Invoke(null, [prompt])!;
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
