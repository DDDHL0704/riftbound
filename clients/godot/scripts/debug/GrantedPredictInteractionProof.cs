using System.Reflection;
using System.Text.Json;
using Godot;
using Riftbound.Contracts;
using Riftbound.GodotClient.Interaction;
using Riftbound.GodotClient.Ui;

namespace Riftbound.GodotClient.Debug;

public partial class GrantedPredictInteractionProof : Control
{
    public override async void _Ready()
    {
        try {
            var catalog=await new OfficialCardCatalogService().LoadSnapshotAsync("res://data/card-catalog.zh-CN.json");
            Check(catalog["SFD·065/221"].EffectText.Contains("机械") && catalog["OGN·100/298"].EffectText.Contains("其他友方单位"),"Native catalog retains both applicable grant texts");
            var root=OS.GetCmdlineUserArgs().Single(a=>a.StartsWith("--evidence="))[11..]; var count=0;
            var printed = OS.GetCmdlineUserArgs().Contains("--printed-predict");
            var porobot = OS.GetCmdlineUserArgs().Contains("--porobot");
            if (porobot) Check(catalog["VEN·058"].CardCategoryName.Contains("单位") && catalog["VEN·058"].CardCategoryName.Contains("装备"), "Native Porobot retains both official types");
            foreach(var branch in porobot ? new[]{"equipment-0","equipment-2","equipment-3","equipment-4"}
                : printed ? new[]{"unit-keep","unit-recycle","equipment-keep","equipment-recycle"} : new[]{"keep","recycle"}) {
                var dir=Path.Combine(root,branch);
                foreach(var step in JsonSerializer.Deserialize<string[]>(File.ReadAllText(Path.Combine(dir,"steps.json")))!.Where(s=>!s.StartsWith("setup-"))) { Produce(dir,step);count++; }
            }
            GD.Print($"GRANTED_PREDICT_PASS: {count} production play, response and optional deck-choice commands");GetTree().Quit();
        } catch(Exception error) {GD.PushError(error.ToString());GetTree().Quit(1);}
    }
    private void Produce(string dir,string step)
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(dir,step+"-prompt.json")));
        using var selected=JsonDocument.Parse(File.ReadAllText(Path.Combine(dir,step+"-selection.json")));
        var prompt=doc.RootElement;var fallback=selected.RootElement;var action=fallback.GetProperty("cmdType").GetString();
        var candidate=prompt.GetProperty("candidates").EnumerateArray().Single(c=>c.GetProperty("action").GetString()==action);
        Dictionary<string,object?>? command=null;
        if(action=="PLAY_CARD") {
            var overlay=new PlayCardOverlay{TableMode=true};AddChild(overlay);
            PlayCostPreviewRequestDto? preview=null;overlay.PreviewRequested+=request=>preview=request;
            Check(overlay.Open(candidate,prompt.GetProperty("promptId").GetString()!,prompt.GetProperty("snapshotTick").GetInt64(),_=>null,fallback.GetProperty("sourceObjectId").GetString()),"Production play composer opens");
            Check(preview is not null && preview.Command.TargetObjectIds.Count==0,"Unit play needs no preselected deck card");
            var quote=JsonSerializer.Deserialize<PlayCostQuoteDto>(File.ReadAllText(Path.Combine(dir,step+"-quote.json")),new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
            overlay.ApplyQuote(quote with {RequestId=preview!.RequestId});
            overlay.Confirmed+=value=>{command=value;command["promptId"]=overlay.PromptId;command["snapshotTick"]=overlay.SnapshotTick;};
            var confirm=(Button)typeof(PlayCardOverlay).GetField("_confirm",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(overlay)!;
            Check(!confirm.Disabled,"Quoted play is enabled");confirm.EmitSignal(BaseButton.SignalName.Pressed);overlay.Free();
        } else {
            var view=(Godot.Collections.Dictionary)typeof(Main).GetMethod("BuildPromptView",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[prompt])!;
            var controller=new PromptInteractionController();controller.Load(view);
            var bar=GD.Load<PackedScene>("res://scenes/components/ActionBar.tscn").Instantiate<ActionBar>();AddChild(bar);
            bar.ActionSelected+=id=>controller.SelectAction(id);bar.ShowPrompt(view["message"].AsString(),controller.Actions);
            if(action=="PASS_PRIORITY") {
                var pass=(Button)typeof(ActionBar).GetField("_primaryAction",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(bar)!;
                Check(!pass.Disabled,"Response pass is enabled");pass.EmitSignal(BaseButton.SignalName.Pressed);
            } else {
                var label=controller.Actions.Single(a=>a.Name=="CHOOSE_CARDS").Label;
                bar.GetNode<HBoxContainer>("%ActionChoices").GetChildren().OfType<Button>().Single(b=>b.Text==label).EmitSignal(BaseButton.SignalName.Pressed);
                bar.ChoiceSelected += (role, id) => { Check(controller.TrySelectChoice(role, id), "Viewed top card is selectable"); bar.ShowSelection(controller.Current!, controller.CurrentChoices, controller.CurrentStepLabel, controller.CurrentStepRequired); };
                bar.ShowSelection(controller.Current!,controller.CurrentChoices,controller.CurrentStepLabel,controller.CurrentStepRequired);
                foreach(var target in fallback.GetProperty("chosenObjectIds").EnumerateArray()) {
                    var choices = controller.CurrentChoices.ToArray();
                    var index = Array.FindIndex(choices, c => c.Id == target.GetString());
                    Check(index >= 0, "Server offers the viewed top card");
                    bar.GetNode<HBoxContainer>("%StepChoices").GetChildren().OfType<Button>().ElementAt(index).EmitSignal(BaseButton.SignalName.Pressed);
                }
            }
            bar.ShowSelection(controller.Current!,controller.CurrentChoices,controller.CurrentStepLabel,controller.CurrentStepRequired);
            bar.SubmitRequested+=selection=>{
                var type=typeof(Main).GetNestedType("PromptSelection",BindingFlags.NonPublic)!;
                var value=Activator.CreateInstance(type,[selection.SourceId,selection.TargetIds.ToArray(),selection.DestinationId,selection.Mode,selection.OptionalCostIds.ToArray()]);
                command=(Dictionary<string,object?>)typeof(Main).GetMethod("CommandFromTemplate",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[candidate,value,controller.PromptId,controller.SnapshotTick])!;
            };
            var submit=bar.GetNode<Button>("%SubmitButton");Check(!submit.Disabled,"Production confirmation is enabled");submit.EmitSignal(BaseButton.SignalName.Pressed);bar.Free();
        }
        Check(command is not null,"Production controls emitted a command");File.WriteAllText(Path.Combine(dir,step+"-command.json"),JsonSerializer.Serialize(command));
    }
    private static void Check(bool condition,string message) {if(!condition)throw new InvalidOperationException(message);}
}
