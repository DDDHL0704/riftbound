using System.Reflection;
using System.Text.Json;
using Godot;
using Riftbound.GodotClient.Interaction;
namespace Riftbound.GodotClient.Debug;

public partial class ResourceAbilityInteractionProof : Control
{
    public override void _Ready()
    {
        try
        {
            var root = OS.GetCmdlineUserArgs().First(a => a.StartsWith("--evidence="))[11..];
            using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "player1-before.json")));
            var prompt = json.RootElement.GetProperty("prompt");
            var map = typeof(Main).GetMethod("BuildPromptView", BindingFlags.Static | BindingFlags.NonPublic)!;
            var view = (Godot.Collections.Dictionary)map.Invoke(null, [prompt])!;
            var controller = new PromptInteractionController(); controller.Load(view);
            Check(controller.TrySelectObject("QA-GOLD"), "Gold must be selectable");
            Check(controller.Current is { CanSubmit: true } && controller.CurrentChoices.Count == 0,
                "Gold must require neither a target nor a mode borrowed from Malzahar");
            Check(!controller.TrySelectObject("QA-SENTINEL"), "Gold cannot acquire Malzahar's target");
            var candidate = prompt.GetProperty("candidates").EnumerateArray().Single(c => c.GetProperty("action").GetString() == "ACTIVATE_ABILITY");
            var gold = Command(candidate, controller);
            Check(((IEnumerable<string>)gold["targetObjectIds"]!).Count() == 0, "Gold command must have no targets");
            Check((string)gold["sourceObjectId"]! == "QA-GOLD", "Gold source must survive mapping");
            controller.ClearSelection();
            Check(!controller.TrySelectObject("QA-SLEEPING-GOLD"), "Sleeping gold is not an offered source");
            controller.SelectAction("ACTIVATE_ABILITY");
            Check(controller.TrySelectObject("QA-MALZAHAR"), "Malzahar must be selectable");
            Check(controller.Current is { CanSubmit: false }, "Destruction cost must be mandatory");
            Check(controller.TrySelectObject("QA-SENTINEL") && controller.Current is { CanSubmit: true }, "Friendly sentinel is a valid cost");
            var malz = Command(candidate, controller);
            Check(((IEnumerable<string>)malz["targetObjectIds"]!).SequenceEqual(["QA-SENTINEL"]), "Selected cost must survive command mapping");
            controller.TrySelectSource("QA-GOLD");
            Check(controller.Current is { CanSubmit: true } && controller.Current.TargetIds.Count == 0 && controller.CurrentChoices.Count == 0,
                "Changing source must clear the old destruction target");
            File.WriteAllText(Path.Combine(root, "native-resource-commands.json"), JsonSerializer.Serialize(new { gold, malz }));
            GD.Print("RESOURCE_ABILITY_INTERACTION_PASS"); GetTree().Quit();
        }
        catch (Exception ex) { GD.PushError(ex.ToString()); GetTree().Quit(1); }
    }
    private static Dictionary<string, object?> Command(JsonElement candidate, PromptInteractionController controller)
    {
        var current = controller.Current!;
        var selectionType = typeof(Main).GetNestedType("PromptSelection", BindingFlags.NonPublic)!;
        var selection = Activator.CreateInstance(selectionType, [current.SourceId, current.TargetIds, current.DestinationId, current.Mode, current.OptionalCostIds]);
        return (Dictionary<string, object?>)typeof(Main).GetMethod("CommandFromTemplate", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, [candidate, selection, controller.PromptId, controller.SnapshotTick])!;
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
