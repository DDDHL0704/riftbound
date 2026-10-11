using System.Reflection;
using System.Text.Json;
using Godot;
using Riftbound.GodotClient.Interaction;
using Riftbound.Contracts;
using Riftbound.GodotClient.Ui;

namespace Riftbound.GodotClient.Debug;

public partial class CommandedMovementInteractionProof : Control
{
    public override void _Ready()
    {
        try {
            var root = OS.GetCmdlineUserArgs().Single(a => a.StartsWith("--evidence="))[11..];
            foreach (var branch in new[] { "D", "D2" }) {
                var dir = Path.Combine(root, branch);
                Play(dir);
                using var prompt = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, "prompt.json")));
                var selected = "MOVE:" + branch;
                var view = (Godot.Collections.Dictionary)typeof(Main).GetMethod("BuildPromptView", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [prompt.RootElement])!;
                var controller = new PromptInteractionController(); controller.Load(view);
                var bar = GD.Load<PackedScene>("res://scenes/components/ActionBar.tscn").Instantiate<ActionBar>(); AddChild(bar);
                bar.ActionSelected += id => controller.SelectAction(id); bar.ShowPrompt(view["message"].AsString(), controller.Actions);
                var label = controller.Actions.Single(a => a.Name == "PAY_COST").Label;
                Check(label == "选择移动单位", "Server labels the replacement choice");
                bar.GetNode<HBoxContainer>("%ActionChoices").GetChildren().OfType<Button>().Single(b => b.Text == label).EmitSignal(BaseButton.SignalName.Pressed);
                Check(controller.TrySelectChoice("target", selected), "Select the server's replacement option");
                Check(controller.CurrentChoices.All(c => c.Id is "MOVE:D" or "MOVE:D2"), "Only legal opponent units");

                using var snapshot = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, "snapshot.json")));
                Check(snapshot.RootElement.GetProperty("players").GetProperty("P1").GetProperty("objects").GetProperty("A").GetProperty("location").GetProperty("battlefieldObjectId").GetString() == "BF2", "First movement is visible before opponent choice");
                var phase = MatchPhasePresentation.Build(snapshot.RootElement, "P2", id => id);
                Check(phase["title"].AsString() == "法术结算 · 选择移动单位", "Replacement phase");
                Check(phase["detail"].AsString().Contains("选择你控制"), "Mandatory and optional decisions are explained");
                bar.ShowSelection(controller.Current!, controller.CurrentChoices, controller.CurrentStepLabel, controller.CurrentStepRequired);
                var candidate = prompt.RootElement.GetProperty("candidates").EnumerateArray().Single(c => c.GetProperty("action").GetString() == "PAY_COST");
                Dictionary<string, object?>? command = null;
                bar.SubmitRequested += choice => {
                    var type = typeof(Main).GetNestedType("PromptSelection", BindingFlags.NonPublic)!;
                    var state = Activator.CreateInstance(type, [choice.SourceId, choice.TargetIds.ToArray(), choice.DestinationId, choice.Mode, choice.OptionalCostIds.ToArray()]);
                    command = (Dictionary<string, object?>)typeof(Main).GetMethod("CommandFromTemplate", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [candidate, state, controller.PromptId, controller.SnapshotTick])!;
                };
                var submit = bar.GetNode<Button>("%SubmitButton"); Check(!submit.Disabled, "Decision can be submitted"); submit.EmitSignal(BaseButton.SignalName.Pressed);
                Check(command is not null, "Production submit generates a command");
                File.WriteAllText(Path.Combine(dir, "command.json"), JsonSerializer.Serialize(command)); bar.Free();
            }
            GD.Print("COMMANDED_MOVEMENT_PASS: two production plays and two opponent movement commands"); GetTree().Quit();
        } catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
    private void Play(string dir)
    {
        var json=new JsonSerializerOptions(JsonSerializerDefaults.Web);
                using var prompt=JsonDocument.Parse(File.ReadAllText(Path.Combine(dir,"play-prompt.json")));
                var p=prompt.RootElement;
                var candidate=p.GetProperty("candidates").EnumerateArray().Single(c=>c.GetProperty("action").GetString()=="PLAY_CARD");
                var overlay=new PlayCardOverlay {TableMode=true}; AddChild(overlay);
                PlayCostPreviewRequestDto? request=null;
                overlay.PreviewRequested+=value=>request=value;
                Check(overlay.Open(candidate,p.GetProperty("promptId").GetString()!,p.GetProperty("snapshotTick").GetInt64(),_=>null,"AOE"),"Open production play composer");
                Check(!overlay.TrySelectTableObject("HIDDEN-OPPONENT-CARD"),"Hidden target is rejected");
                var destination = "BATTLEFIELD:BF2";
                var destinations = (string[])typeof(PlayCardOverlay).GetField("_destinations",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(overlay)!;
                var picker = (OptionButton)typeof(PlayCardOverlay).GetField("_destination",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(overlay)!;
                var index = Array.IndexOf(destinations,destination); Check(index >= 0,"Server offers the movement destination");
                picker.Select(index); picker.EmitSignal(OptionButton.SignalName.ItemSelected,index);
                var ids = new[]{"A"};
                foreach(var id in ids) Check(overlay.TrySelectTableObject(id),"Each enemy unit is selectable");
                Check(request is not null && request.Command.TargetObjectIds.SequenceEqual(ids) && request.Command.Destination == destination,"Selected targets and destination reach payment preview");
                File.WriteAllText(Path.Combine(dir,"play-preview.json"),JsonSerializer.Serialize(request,json));
                var quote=JsonSerializer.Deserialize<PlayCostQuoteDto>(File.ReadAllText(Path.Combine(dir,"play-quote.json")),json)!;
                overlay.ApplyQuote(quote with {RequestId=request!.RequestId});
                Dictionary<string,object?>? command=null;
                overlay.Confirmed+=value=>{
                    command=value;
                    // Main adds the composer identity before dispatch to the server.
                    command["promptId"]=overlay.PromptId; command["snapshotTick"]=overlay.SnapshotTick;
                };
                var submit=(Button)typeof(PlayCardOverlay).GetField("_confirm",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(overlay)!;
                Check(!submit.Disabled,"Server payment quote enables submit"); submit.EmitSignal(BaseButton.SignalName.Pressed);
                Check(command is not null,"Production submit emits command");
                File.WriteAllText(Path.Combine(dir,"play-command.json"),JsonSerializer.Serialize(command,json)); overlay.Free();
    }
    private static void Check(bool valid, string message) { if (!valid) throw new InvalidOperationException(message); }
}
