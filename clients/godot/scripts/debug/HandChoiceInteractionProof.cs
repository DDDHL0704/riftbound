using System.Reflection;
using System.Text.Json;
using Godot;
using Riftbound.GodotClient.Interaction;

namespace Riftbound.GodotClient.Debug;

// Exercises the real server candidate through the desktop's production mapping and command builder.
public partial class HandChoiceInteractionProof : Control
{
    public override void _Ready()
    {
        try
        {
            var root = OS.GetCmdlineUserArgs().First(a => a.StartsWith("--evidence="))[11..];
            using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "hand-choice-prompt.json")));
            var map = typeof(Main).GetMethod("BuildPromptView", BindingFlags.Static | BindingFlags.NonPublic)!;
            var view = (Godot.Collections.Dictionary)map.Invoke(null, [json.RootElement])!;
            var controller = new PromptInteractionController();
            controller.Load(view);
            Check(controller.Current is { ActionName: "CHOOSE_HAND_CARDS", CanSubmit: false }, "Forced hand choice must open automatically");
            Check(!controller.TrySelectObject("DRAW1"), "A hidden deck card cannot be selected");
            Check(controller.TrySelectObject("H2"), "Clicking the second legal hand card must select it");
            Check(controller.Current is { CanSubmit: true }, "Valid selection must enable confirmation");
            var candidate = json.RootElement.GetProperty("candidates").EnumerateArray()
                .Single(c => c.GetProperty("action").GetString() == "CHOOSE_HAND_CARDS");
            var selectionType = typeof(Main).GetNestedType("PromptSelection", BindingFlags.NonPublic)!;
            var selection = Activator.CreateInstance(selectionType,
                [null, new[] { "H2" }, null, null, Array.Empty<string>()]);
            var build = typeof(Main).GetMethod("CommandFromTemplate", BindingFlags.Static | BindingFlags.NonPublic)!;
            var payload = (Dictionary<string, object?>)build.Invoke(null,
                [candidate, selection, controller.PromptId, controller.SnapshotTick])!;
            Check(payload["cmdType"] as string == "CHOOSE_HAND_CARDS", "Must build a hand-choice command");
            Check(((IEnumerable<string>)payload["chosenObjectIds"]!).SequenceEqual(["H2"]), "Must submit the player's selection, not the first card");
            Check(payload.ContainsKey("choiceId") && payload.ContainsKey("choiceWindow")
                && payload.ContainsKey("promptId") && payload.ContainsKey("snapshotTick"), "Server window identity must survive mapping");
            File.WriteAllText(Path.Combine(root, "native-hand-choice-command.json"), JsonSerializer.Serialize(payload));
            controller.Load(new Godot.Collections.Dictionary { ["promptId"] = "next", ["snapshotTick"] = 4L,
                ["actions"] = new Godot.Collections.Array<Godot.Collections.Dictionary>() });
            Check(controller.Current is null, "A new server prompt must clear stale choices");
            GD.Print("HAND_CHOICE_INTERACTION_PASS: real server prompt -> card click -> guarded chosenObjectIds");
            GetTree().Quit();
        }
        catch (Exception exception)
        {
            GD.PushError(exception.ToString());
            GetTree().Quit(1);
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
