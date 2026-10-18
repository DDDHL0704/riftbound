using System.Reflection;
using System.Text.Json;
using Godot;
using Riftbound.GodotClient.Ui;

namespace Riftbound.GodotClient.Debug;

public partial class MultiControllerTriggerOrderInteractionProof : Control
{
    public override void _Ready()
    {
        try
        {
            var root = OS.GetCmdlineUserArgs().Single(arg => arg.StartsWith("--evidence="))[11..];
            var commands = 0;
            foreach (var branch in new[] { "P1-False", "P1-True", "P2-False", "P2-True" })
            foreach (var phase in new[] { "first", "second" })
            {
                var name = branch + "-" + phase;
                using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, name + "-prompt.json")));
                var view = (Godot.Collections.Dictionary)typeof(Main)
                    .GetMethod("BuildPromptView", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [document.RootElement])!;
                var action = view["actions"].AsGodotArray().Select(value => value.AsGodotDictionary())
                    .Single(candidate => candidate["action"].AsString() == "ORDER_TRIGGERS");
                Check(SpecialPromptCommandBuilder.TryReadOrderTriggers(action, out var items, out var reason), reason);
                var overlay = GD.Load<PackedScene>("res://scenes/overlays/TriggerOrderOverlay.tscn")
                    .Instantiate<TriggerOrderOverlay>();
                AddChild(overlay);
                Check(overlay.ShowPrompt(action, out reason), reason);
                var before = overlay.OrderedTriggerIds.ToArray();
                var own = Enumerable.Range(0, items.Count).Where(index => items[index].CanReorder).ToArray();
                var foreign = Enumerable.Range(0, items.Count).Except(own).ToArray();
                Check(own.Length == 2 && foreign.Length == 2, "Each controller owns exactly its two trigger choices.");
                var rows = overlay.GetNode<VBoxContainer>("%TriggerRows");
                foreach (var index in foreign)
                foreach (var buttonName in new[] { "MoveUpButton", "MoveDownButton" })
                {
                    var button = rows.GetChild(index).GetNode<Button>(buttonName);
                    Check(button.Disabled, "Other controller move buttons must be disabled.");
                    button.EmitSignal(BaseButton.SignalName.Pressed);
                    Check(overlay.OrderedTriggerIds.SequenceEqual(before), "A forced signal cannot move another controller's trigger.");
                }
                var illegal = before.ToArray();
                (illegal[foreign[0]], illegal[foreign[1]]) = (illegal[foreign[1]], illegal[foreign[0]]);
                Check(!SpecialPromptCommandBuilder.TryBuildOrderTriggersPayload(action, illegal, out _, out _, out _),
                    "The native command adapter independently refuses another controller's changed order.");
                var reverse = phase == "second" || branch.EndsWith("True", StringComparison.Ordinal);
                var expected = before.ToArray();
                if (reverse)
                {
                    var down = rows.GetChild(own[0]).GetNode<Button>("MoveDownButton");
                    Check(!down.Disabled, "The current controller can move its own adjacent skills.");
                    down.EmitSignal(BaseButton.SignalName.Pressed);
                    (expected[own[0]], expected[own[1]]) = (expected[own[1]], expected[own[0]]);
                }
                Check(overlay.OrderedTriggerIds.SequenceEqual(expected), "Own choice preserves every other controller's order.");
                Dictionary<string, object?>? command = null;
                overlay.Confirmed += ids => {
                    Check(SpecialPromptCommandBuilder.TryBuildOrderTriggersPayload(action, ids, out var payload, out _, out var failure), failure);
                    payload["promptId"] = document.RootElement.GetProperty("promptId").GetString();
                    payload["snapshotTick"] = document.RootElement.GetProperty("snapshotTick").GetInt64();
                    command = payload;
                };
                overlay.GetNode<Button>("%ConfirmButton").EmitSignal(BaseButton.SignalName.Pressed);
                Check(command is not null, "The production confirm button emits the bounded order command.");
                File.WriteAllText(Path.Combine(root, name + "-command.json"), JsonSerializer.Serialize(command));
                overlay.Free();
                commands++;
            }
            GD.Print($"MULTI_CONTROLLER_TRIGGER_ORDER_PASS: {commands} production overlay commands; foreign buttons and adapter guarded");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }

    private static void Check(bool valid, string message)
    {
        if (!valid) throw new InvalidOperationException(message);
    }
}
