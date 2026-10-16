using System.Reflection;
using System.Text.Json;
using Godot;
using Riftbound.GodotClient.Interaction;
using Riftbound.GodotClient.Ui;

namespace Riftbound.GodotClient.Debug;

public partial class CreatedTokenReadyInteractionProof : Control
{
    public override void _Ready()
    {
        try
        {
            var root = OS.GetCmdlineUserArgs().Single(a => a.StartsWith("--evidence="))[11..];
            var flags = BindingFlags.Static | BindingFlags.NonPublic;
            var map = typeof(Main).GetMethod("BuildPromptView", flags)!;
            var build = typeof(Main).GetMethod("CommandFromTemplate", flags)!;
            var type = typeof(Main).GetNestedType("PromptSelection", BindingFlags.NonPublic)!;
            var count = 0;
            foreach (var path in Directory.GetFiles(root, "defend-step-*-selection.json", SearchOption.AllDirectories).Order())
            {
                using var expected = JsonDocument.Parse(File.ReadAllText(path));
                var action = expected.RootElement.GetProperty("cmdType").GetString();
                if (action is not ("PAY_COST" or "CHOOSE_CARDS")) continue;
                var prefix = path[..^"-selection.json".Length];
                using var prompt = JsonDocument.Parse(File.ReadAllText(prefix + "-prompt.json"));
                var candidate = prompt.RootElement.GetProperty("candidates").EnumerateArray().Single(c => c.GetProperty("action").GetString() == action);
                var view = (Godot.Collections.Dictionary)map.Invoke(null, [prompt.RootElement])!;
                var controller = new PromptInteractionController(); controller.Load(view);
                var bar = GD.Load<PackedScene>("res://scenes/components/ActionBar.tscn").Instantiate<ActionBar>(); AddChild(bar);
                void Refresh() => bar.ShowSelection(controller.Current!, controller.CurrentChoices, controller.CurrentStepLabel, controller.CurrentStepRequired);
                bar.ActionSelected += id => { Check(controller.SelectAction(id), "Actual action is available"); Refresh(); };
                bar.ChoiceSelected += (role, id) => { Check(controller.TrySelectChoice(role, id), "A legal server choice is selectable"); Refresh(); };
                bar.ShowPrompt(view["message"].AsString(), controller.Actions);
                var label = controller.Actions.Single(a => a.Name == action).Label;
                bar.GetNode<HBoxContainer>("%ActionChoices").GetChildren().OfType<Button>().Single(b => b.Text == label).EmitSignal(BaseButton.SignalName.Pressed);
                var field = action == "PAY_COST" ? "paymentChoiceIds" : "chosenObjectIds";
                var selected = expected.RootElement.GetProperty(field).EnumerateArray().Select(x => x.GetString()!).ToArray();
                var submit = bar.GetNode<Button>("%SubmitButton");
                if (action == "PAY_COST") Check(submit.Disabled && !controller.Current!.CanSubmit, "Payment requires an explicit decision");
                else Check(controller.CurrentStepLabel.Contains("本次") && !controller.CurrentStepLabel.Contains("放弃"),
                    "Zero targets are a valid mandatory ability; do not label this choice as abandoning the skill");
                foreach (var id in selected)
                {
                    if (controller.Current!.TargetIds.Contains(id)) continue;
                    Check(controller.CurrentStepRole == "target", "The current target/decision step is active");
                    var choices = controller.CurrentChoices.ToArray(); var index = Array.FindIndex(choices, c => c.Id == id);
                    Check(index >= 0, "The bound token or payment has a current native button");
                    bar.GetNode<HBoxContainer>("%StepChoices").GetChild<Button>(index).EmitSignal(BaseButton.SignalName.Pressed);
                }
                Check(controller.Current!.TargetIds.SequenceEqual(selected), "Only the explicit server-authorized choices survive");
                Dictionary<string, object?>? command = null;
                bar.SubmitRequested += selection => {
                    var value = Activator.CreateInstance(type, [selection.SourceId, selection.TargetIds.ToArray(), selection.DestinationId,
                        selection.Mode, selection.OptionalCostIds.ToArray()]);
                    command = (Dictionary<string, object?>)build.Invoke(null, [candidate, value, controller.PromptId, controller.SnapshotTick])!;
                };
                Check(!submit.Disabled && controller.Current.CanSubmit, "Explicit payment/choice enables native confirmation");
                submit.EmitSignal(BaseButton.SignalName.Pressed);
                Check(command is not null, "Production native submit emitted a command");
                var wire = ClientCommandSerializer.Serialize(command!);
                Check(wire.GetProperty(field).EnumerateArray().Select(x => x.GetString()).SequenceEqual(selected), "Wire choices match the actual native selection");
                Check(wire.GetProperty("promptId").GetString() == prompt.RootElement.GetProperty("promptId").GetString()
                    && wire.GetProperty("snapshotTick").GetInt64() == prompt.RootElement.GetProperty("snapshotTick").GetInt64(), "Exact current prompt/tick retained");
                File.WriteAllText(prefix + "-command.json", wire.GetRawText()); count++; bar.Free();
            }
            Check(count > 0, "The proof must execute real exported choices");
            GD.Print($"CREATED_TOKEN_READY_NATIVE_PASS: {count} production payment/choice button commands; server readback and full replay remain separate");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }
    private static void Check(bool condition, string reason) { if (!condition) throw new InvalidOperationException(reason); }
}
