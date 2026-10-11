using System.Text.Json;
using Godot;
using Riftbound.GodotClient.Ui;
namespace Riftbound.GodotClient.Debug;
public partial class MovementThresholdInteractionProof:Control
{
    public override void _Ready()
    {
        try {
            var root=OS.GetCmdlineUserArgs().Single(a=>a.StartsWith("--evidence="))[11..];
            using var prompt=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"assignment-prompt.json")));
            var p=prompt.RootElement;var candidate=p.GetProperty("candidates").EnumerateArray().Single(c=>c.GetProperty("action").GetString()=="ASSIGN_COMBAT_DAMAGE");
            var action=new Godot.Collections.Dictionary { ["action"]="ASSIGN_COMBAT_DAMAGE",["enabled"]=true,["candidateJson"]=candidate.GetRawText() };
            Check(SpecialPromptCommandBuilder.TryReadDamageAssignmentPrompt(action,out var assignments,out var reason),reason);
            Check(assignments.SelectMany(a=>a.Targets).Any(t=>t.TargetObjectId=="D" && t.CannotTakeDamage),"Server immunity is retained");
            var overlay=GD.Load<PackedScene>("res://scenes/overlays/DamageAssignmentOverlay.tscn").Instantiate<DamageAssignmentOverlay>();AddChild(overlay);
            Check(overlay.ShowPrompt(action,out reason),reason);
            Check(Labels(overlay).Any(l=>l.Text.Contains("无法受到伤害 · 可跳过分配")),"Immunity guidance rendered");
            overlay.GetNode<Button>("%SuggestButton").EmitSignal(BaseButton.SignalName.Pressed);
            Dictionary<string,object?>? payload=null;
            overlay.Confirmed+=selections=> {
                Check(selections.All(s=>s.TargetObjectId!="D"),"Suggested assignment skips immune Bulwark");
                Check(SpecialPromptCommandBuilder.TryBuildDamageAssignmentPayload(action,selections,out var command,out _,out var failure),failure);
                command["promptId"]=p.GetProperty("promptId").GetString();command["snapshotTick"]=p.GetProperty("snapshotTick").GetInt64();payload=command;
            };
            var submit=overlay.GetNode<Button>("%ConfirmButton");Check(!submit.Disabled,"Assignment is submittable");submit.EmitSignal(BaseButton.SignalName.Pressed);
            Check(payload is not null,"Production overlay produced command");File.WriteAllText(Path.Combine(root,"assignment-command.json"),JsonSerializer.Serialize(payload));
            GD.Print("MOVEMENT_THRESHOLD_PASS: immune label, server suggestion, production damage assignment command");GetTree().Quit();
        } catch(Exception e) {GD.PushError(e.ToString());GetTree().Quit(1);}
    }
    private static IEnumerable<Label> Labels(Node node) {foreach(var child in node.GetChildren()) {if(child is Label label)yield return label;foreach(var label2 in Labels(child))yield return label2;}}
    private static void Check(bool condition,string message) {if(!condition)throw new InvalidOperationException(message);}
}
