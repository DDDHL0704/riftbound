using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Godot;
using Riftbound.GodotClient.Interaction;
using Riftbound.GodotClient.Ui;

namespace Riftbound.GodotClient.Debug;

public partial class RuleChoiceNativePromptProof : Control
{
    public override void _Ready()
    {
        try
        {
            var root = OS.GetCmdlineUserArgs().Single(a => a.StartsWith("--evidence="))[11..];
            var map = typeof(Main).GetMethod("BuildPromptView", BindingFlags.Static | BindingFlags.NonPublic)!;
            var build = typeof(Main).GetMethod("CommandFromTemplate", BindingFlags.Static | BindingFlags.NonPublic)!;
            var selectionType = typeof(Main).GetNestedType("PromptSelection", BindingFlags.NonPublic)!;
            var commands = 0;
            var headings = 0;
            foreach (var path in Directory.GetFiles(root, "defend-step-*-selection.json", SearchOption.AllDirectories).Order())
            {
                using var expected = JsonDocument.Parse(File.ReadAllText(path));
                var action = expected.RootElement.GetProperty("cmdType").GetString();
                if (action is not ("PAY_COST" or "HIDE_CARD")) continue;
                var prefix = path[..^"-selection.json".Length];
                using var prompt = JsonDocument.Parse(File.ReadAllText(prefix + "-prompt.json"));
                var candidate = prompt.RootElement.GetProperty("candidates").EnumerateArray().Single(c => c.GetProperty("action").GetString() == action);
                var view = (Godot.Collections.Dictionary)map.Invoke(null, [prompt.RootElement])!;
                var controller = new PromptInteractionController();
                controller.Load(view);
                var bar = GD.Load<PackedScene>("res://scenes/components/ActionBar.tscn").Instantiate<ActionBar>();
                AddChild(bar);
                void Refresh() => bar.ShowSelection(controller.Current!, controller.CurrentChoices, controller.CurrentStepLabel, controller.CurrentStepRequired);
                bar.ActionSelected += id => { Check(controller.SelectAction(id), "Actual action is available"); Refresh(); };
                bar.ChoiceSelected += (role, id) => { Check(controller.TrySelectChoice(role, id), "Actual choice button selects the advertised intent"); Refresh(); };
                bar.ShowPrompt(view["message"].AsString(), controller.Actions);
                var label = controller.Actions.Single(a => a.Name == action).Label;
                bar.GetNode<HBoxContainer>("%ActionChoices").GetChildren().OfType<Button>().Single(b => b.Text == label).EmitSignal(BaseButton.SignalName.Pressed);
                var submit = bar.GetNode<Button>("%SubmitButton");
                Check(submit.Disabled && !controller.Current!.CanSubmit, "An unchosen payment or required destination cannot be confirmed early");
                if (action == "PAY_COST")
                {
                    var id = expected.RootElement.GetProperty("paymentChoiceIds").EnumerateArray().Single().GetString()!;
                    Check(controller.CurrentChoices.Any(c => c.Id == id), "The real rune/typed-payment choice has a rendered button");
                    ClickChoice("target", id);
                    Check(controller.Current!.TargetIds.SequenceEqual([id]), "Exactly one atomic decision is sent");
                    foreach (var side in new[] { ("-snapshot.json", "P1", "我方完成选择"), ("-opponent-snapshot.json", "P2", "对手完成选择") })
                    {
                        var snapshot = JsonNode.Parse(File.ReadAllText(prefix + side.Item1))!;
                        foreach (var phaseName in new[] { "MAIN", "TURN_START" })
                        {
                            snapshot["timing"]!["phase"] = phaseName;
                            using var doc = JsonDocument.Parse(snapshot.ToJsonString());
                            var phase = MatchPhasePresentation.Build(doc.RootElement, side.Item2, id => id);
                            Check(phase["title"].AsString() == "指示物急速 · 支付选择", "Token payment is not labelled destruction replacement");
                            Check(phase["actor"].AsString() == side.Item3, "Public payment owner controls the heading for both viewers");
                            Check(phase["detail"].AsString().Contains("单位进场前") && phase["activeWindow"].AsBool(), "Heading describes the pre-entry choice");
                            headings++;
                        }
                        // Presentation-only controls keep the established public kinds distinct.
                        foreach (var other in new[] { ("DESTRUCTION_REPLACEMENT", "摧毁替换 · 等待选择"),
                            ("MOVEMENT_SELECTION", "法术结算 · 选择移动单位"), ("TARGET_GROUP", "法术结算 · 重选目标"),
                            ("FIRST_DEATH_OCCURRENCE", "首次死亡 · 选择事件") })
                        {
                            snapshot["timing"]!["pendingPayment"]!["choiceKind"] = other.Item1;
                            using var doc = JsonDocument.Parse(snapshot.ToJsonString());
                            Check(MatchPhasePresentation.Build(doc.RootElement, side.Item2, id => id)["title"].AsString() == other.Item2, "Existing choice kind heading remains exact");
                        }
                    }
                }
                else
                {
                    var source = expected.RootElement.GetProperty("sourceObjectId").GetString()!;
                    if (controller.Current!.SourceId != source) ClickChoice("source", source);
                    Check(submit.Disabled, "Selecting only the HIDE source does not bypass its destination");
                    var fee = expected.RootElement.GetProperty("optionalCosts").EnumerateArray().Single().GetString()!;
                    ClickChoice("destination", expected.RootElement.GetProperty("destination").GetString()!);
                    if (!controller.Current!.OptionalCostIds.Contains(fee))
                    {
                        Check(submit.Disabled, "A multi-choice HIDE fee must be selected explicitly");
                        ClickChoice("optionalCost", fee);
                    }
                    Check(controller.Current!.OptionalCostIds.SequenceEqual([fee]), "The authorized placement cost is preserved");
                }
                Dictionary<string, object?>? payload = null;
                bar.SubmitRequested += selected => {
                    var selection = Activator.CreateInstance(selectionType, [selected.SourceId, selected.TargetIds.ToArray(),
                        selected.DestinationId, selected.Mode, selected.OptionalCostIds.ToArray()]);
                    payload = (Dictionary<string, object?>)build.Invoke(null, [candidate, selection, controller.PromptId, controller.SnapshotTick])!;
                };
                Check(!submit.Disabled && controller.Current!.CanSubmit, "All mandatory choices enable native confirmation");
                submit.EmitSignal(BaseButton.SignalName.Pressed);
                Check(payload is not null && payload["cmdType"] as string == action, "Actual native confirmation builds the intended command");
                Check(payload!.ContainsKey("promptId") && payload.ContainsKey("snapshotTick"), "Current server identity is retained");
                File.WriteAllText(prefix + "-command.json", JsonSerializer.Serialize(payload));
                commands++;
                bar.Free();

                void ClickChoice(string role, string id)
                {
                    Check(controller.CurrentStepRole == role, "Only the current required role is selectable");
                    var choices = controller.CurrentChoices.ToArray();
                    var index = Array.FindIndex(choices, c => c.Id == id);
                    Check(index >= 0, "The chosen server id has a current button");
                    bar.GetNode<HBoxContainer>("%StepChoices").GetChild<Button>(index).EmitSignal(BaseButton.SignalName.Pressed);
                }
            }
            Check(commands == 13 && headings == 40, "All ten atomic payment and three HIDE intents, both viewers and phases ran");
            GD.Print($"RULE_CHOICE_NATIVE_PROMPT_PASS: {commands} production button commands, {headings} token headings, required HIDE location/fees, existing heading controls");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
