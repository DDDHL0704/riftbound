using System.Reflection;
using System.Text.Json;
using Godot;
using Riftbound.GodotClient.Ui;

namespace Riftbound.GodotClient.Debug;

public partial class CombatAssignmentInteractionProof : Control
{
    public override void _Ready()
    {
        try
        {
            var root = OS.GetCmdlineUserArgs().Single(a => a.StartsWith("--evidence="))[11..]; var count = 0;
            foreach (var branch in new[] { "multiple-defenders", "choose-first-attacker", "choose-second-attacker" })
            {
                var dir = Path.Combine(root, branch);
                foreach (var step in JsonSerializer.Deserialize<string[]>(File.ReadAllText(Path.Combine(dir, "steps.json")))!)
                {
                    using var promptDoc = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, step + "-prompt.json")));
                    using var selected = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, step + "-selection.json")));
                    var prompt = promptDoc.RootElement;
                    var view = (Godot.Collections.Dictionary)typeof(Main).GetMethod("BuildPromptView", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [prompt])!;
                    var action = view["actions"].AsGodotArray().Select(v => v.AsGodotDictionary())
                        .Single(a => a["action"].AsString() == "ASSIGN_COMBAT_DAMAGE");
                    var overlay = GD.Load<PackedScene>("res://scenes/overlays/DamageAssignmentOverlay.tscn").Instantiate<DamageAssignmentOverlay>();
                    AddChild(overlay); Check(overlay.ShowPrompt(action, out var reason), reason);
                    Check(overlay.GetNode<Button>("%ConfirmButton").Disabled, "Empty allocation cannot be confirmed.");
                    var steppers = (Dictionary<string, SpinBox>)typeof(DamageAssignmentOverlay)
                        .GetField("_steppers", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(overlay)!;
                    foreach (var group in selected.RootElement.GetProperty("assignments").EnumerateArray()
                        .GroupBy(item => item.GetProperty("targetObjectId").GetString()!))
                        steppers[group.Key].Value = group.Sum(item => item.GetProperty("damage").GetInt32());
                    Dictionary<string, object?>? command = null;
                    overlay.Confirmed += assignments =>
                    {
                        Check(SpecialPromptCommandBuilder.TryBuildDamageAssignmentPayload(action, assignments, out var payload, out _, out var error), error);
                        payload["promptId"] = prompt.GetProperty("promptId").GetString();
                        payload["snapshotTick"] = prompt.GetProperty("snapshotTick").GetInt64(); command = payload;
                    };
                    var confirm = overlay.GetNode<Button>("%ConfirmButton");
                    Check(!confirm.Disabled, "The independently chosen allocation is valid."); confirm.EmitSignal(BaseButton.SignalName.Pressed);
                    Check(command is not null, "The production allocation control emitted a command.");
                    File.WriteAllText(Path.Combine(dir, step + "-command.json"), ClientCommandSerializer.Serialize(command!).GetRawText());
                    overlay.Free(); count++;
                }
            }
            GD.Print($"COMBAT_ASSIGNMENT_NATIVE_PASS: {count} actual production allocation controls and outgoing serialized commands"); GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }

    private static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }
}
