using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text.Json;
using Godot;
using Riftbound.GodotClient.Interaction;
using Riftbound.GodotClient.Ui;

namespace Riftbound.GodotClient.Debug;

public partial class WildClawInteractionProof : Control
{
    public override void _Ready()
    {
        try
        {
            var root = OS.GetCmdlineUserArgs().Single(a => a.StartsWith("--evidence="))[11..];
            using var index = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "index.json")));
            var map = typeof(Main).GetMethod("BuildPromptView", BindingFlags.Static | BindingFlags.NonPublic)!;
            var build = typeof(Main).GetMethod("CommandFromTemplate", BindingFlags.Static | BindingFlags.NonPublic)!;
            var type = typeof(Main).GetNestedType("PromptSelection", BindingFlags.NonPublic)!;
            File.WriteAllText(Path.Combine(root, "actual-loaded-client-assemblies.json"), JsonSerializer.Serialize(new {
                baseDirectory = AppContext.BaseDirectory,
                assemblies = new[] { typeof(Main).Assembly, typeof(Riftbound.Contracts.SnapshotDto).Assembly }.Select(a => {
                    var diskPath = ProjectSettings.GlobalizePath("res://.godot/mono/temp/bin/Debug/" + a.GetName().Name + ".dll");
                    using var stream = File.OpenRead(diskPath); using var pe = new PEReader(stream);
                    var metadata = pe.GetMetadataReader(); var diskMvid = metadata.GetGuid(metadata.GetModuleDefinition().Mvid);
                    Check(a.ManifestModule.ModuleVersionId == diskMvid, "The actual running module MVID must equal the explicitly pinned disk candidate.");
                    return new { name = a.GetName().Name, actualReportedLocation = a.Location, mvid = a.ManifestModule.ModuleVersionId,
                        candidateDiskPath = diskPath, candidateDiskMvid = diskMvid,
                        candidateDiskSha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(diskPath))).ToLowerInvariant(),
                        diskHashDirectlyRepresentsReportedLoadedPath = a.Location.Length > 0 && a.Location == diskPath };
                }).ToArray()
            }, new JsonSerializerOptions { WriteIndented = true }));
            var count = 0; var headings = 0;
            foreach (var row in index.RootElement.EnumerateArray())
            {
                using var original = JsonDocument.Parse(File.ReadAllText(row.GetProperty("originalCommandPath").GetString()!));
                using var prompt = JsonDocument.Parse(File.ReadAllText(row.GetProperty("promptPath").GetString()!));
                var action = original.RootElement.GetProperty("cmdType").GetString()!;
                Check(action is "PAY_COST" or "CHOOSE_CARDS", "Only actual SQL choice intents belong to this proof.");
                var candidate = prompt.RootElement.GetProperty("candidates").EnumerateArray().Single(c => c.GetProperty("action").GetString() == action);
                var view = (Godot.Collections.Dictionary)map.Invoke(null, [prompt.RootElement])!;
                var controller = new PromptInteractionController(); controller.Load(view);
                var bar = GD.Load<PackedScene>("res://scenes/components/ActionBar.tscn").Instantiate<ActionBar>(); AddChild(bar);
                bar.ActionSelected += id => Check(controller.SelectAction(id), "The actual server action is available.");
                bar.ChoiceSelected += (role, id) => Check(controller.TrySelectChoice(role, id), "The real button selects an advertised choice.");
                controller.SelectionChanged += state => bar.ShowSelection(state, controller.CurrentChoices, controller.CurrentStepLabel, controller.CurrentStepRequired);
                controller.SelectionCleared += bar.ClearSelectionDisplay;
                bar.ShowPrompt(view["message"].AsString(), controller.Actions);
                var label = controller.Actions.Single(a => a.Name == action).Label;
                bar.GetNode<HBoxContainer>("%ActionChoices").GetChildren().OfType<Button>().Single(b => b.Text == label).EmitSignal(BaseButton.SignalName.Pressed);
                var field = action == "PAY_COST" ? "paymentChoiceIds" : "chosenObjectIds";
                var selected = original.RootElement.GetProperty(field).EnumerateArray().Select(v => v.GetString()!).ToArray();
                var submit = bar.GetNode<Button>("%SubmitButton");
                if (action == "PAY_COST") Check(submit.Disabled && !controller.Current!.CanSubmit, "An explicit create, decline or observation decision is required.");
                Check(!controller.TrySelectChoice("target", "NOT_A_SERVER_CHOICE"), "A fabricated choice is never selectable.");
                foreach (var id in selected)
                {
                    if (controller.Current!.TargetIds.Contains(id)) continue;
                    var position = controller.CurrentChoices.ToList().FindIndex(c => c.Id == id);
                    Check(position >= 0, "The exact original choice has a native button.");
                    bar.GetNode<HBoxContainer>("%StepChoices").GetChild<Button>(position).EmitSignal(BaseButton.SignalName.Pressed);
                }
                Check(controller.Current!.TargetIds.SequenceEqual(selected) && !submit.Disabled, "The original ordered selection enables confirmation.");
                Dictionary<string, object?>? command = null;
                bar.SubmitRequested += selection => {
                    var value = Activator.CreateInstance(type, [selection.SourceId, selection.TargetIds.ToArray(), selection.DestinationId, selection.Mode, selection.OptionalCostIds.ToArray()]);
                    command = (Dictionary<string, object?>)build.Invoke(null, [candidate, value, controller.PromptId, controller.SnapshotTick])!;
                };
                bar.SetPending(true); submit.EmitSignal(BaseButton.SignalName.Pressed); Check(command is null, "Network-pending confirmation cannot emit an intent.");
                bar.SetPending(false); submit.EmitSignal(BaseButton.SignalName.Pressed); Check(command is not null, "The actual submit button emits the production template.");
                var wire = ClientCommandSerializer.Serialize(command!);
                foreach (var name in new[] { "cmdType", action == "PAY_COST" ? "paymentId" : "choiceId", action == "PAY_COST" ? "paymentWindow" : "choiceWindow", "promptId" })
                    Check(wire.GetProperty(name).GetString() == original.RootElement.GetProperty(name).GetString(), "Original command and prompt identities are exact.");
                Check(wire.GetProperty("snapshotTick").GetInt64() == original.RootElement.GetProperty("snapshotTick").GetInt64()
                    && wire.GetProperty(field).EnumerateArray().Select(v => v.GetString()).SequenceEqual(selected), "Original tick and choices are exact.");
                File.WriteAllText(row.GetProperty("outputCommandPath").GetString()!, wire.GetRawText());
                using var snapshot = JsonDocument.Parse(File.ReadAllText(row.GetProperty("snapshotPath").GetString()!));
                if (snapshot.RootElement.GetProperty("timing").TryGetProperty("pendingPayment", out var payment)
                    && payment.ValueKind == JsonValueKind.Object
                    && payment.TryGetProperty("choiceKind", out var kind) && kind.GetString() == "DECK_REFLEXIVE_CREATE")
                {
                    Check(label == "决定是否创建强化技能", "The action describes creation, not a destruction replacement.");
                    var actor = row.GetProperty("playerId").GetString()!;
                    foreach (var viewer in new[] { actor, actor == "alice" ? "bob" : "alice" }) {
                        var phase = MatchPhasePresentation.Build(snapshot.RootElement, viewer, id => id);
                        Check(phase["title"].AsString() == "内嵌强化 · 创建选择", "The actual current snapshot has the matching native phase heading.");
                        Check(phase["actor"].AsString() == (viewer == actor ? "我方完成选择" : "对手完成选择"), "The decision owner is exact for both viewers.");
                        headings++;
                    }
                }
                command = null;
                controller.Load(new Godot.Collections.Dictionary { ["promptId"] = "new-server-window", ["snapshotTick"] = controller.SnapshotTick + 1,
                    ["actions"] = new Godot.Collections.Array<Godot.Collections.Dictionary>() });
                submit.EmitSignal(BaseButton.SignalName.Pressed); Check(command is null && controller.Current is null, "A fresh server window invalidates stale submission.");
                bar.Free(); count++;
            }
            Check(count > 0 && headings > 0, "Real SQL choices and real creation headings must execute.");
            GD.Print($"WILD_CLAW_NATIVE_INTERACTION_PASS: {count} original production button commands; {headings} owner-specific creation headings; hidden, pending and stale guards");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }
    private static void Check(bool valid, string message) { if (!valid) throw new InvalidOperationException(message); }
}
