using System.Text.Json;
using Godot;
namespace Riftbound.GodotClient.Ui;

public partial class PlayCardOverlay
{
    private readonly List<(OptionButton Picker, string[] Ids)> _movementDestinations = [];
    private OptionButton? _focusedMovement;
    private IEnumerable<(OptionButton Picker, string[] Ids)> AllMovementDestinations
        => _movementDestinations.Concat(_repeats.SelectMany(r => r.MovementDestinations));

    private (OptionButton? Picker, string[] Ids) CurrentMovementDestination()
    {
        var choices=AllMovementDestinations.ToArray();
        return choices.FirstOrDefault(c=>c.Picker==_focusedMovement && _focusedMovement is not null) is {Picker:not null} focused
            ? focused : choices.FirstOrDefault(c=>c.Picker.Selected<=0) is {Picker:not null} incomplete
            ? incomplete : choices.FirstOrDefault();
    }
    private static IReadOnlyList<string>? MovementChoices(IEnumerable<(OptionButton Picker,string[] Ids)> choices)
    {
        var values=choices.Select(c=>c.Ids[Math.Max(0,c.Picker.Selected)]).ToArray();
        return values.Length==0?null:values;
    }
    private List<(OptionButton Picker,string[] Ids)> BuildMovementDestinations(JsonElement requirement,Node parent,IReadOnlyList<string>? initial=null)
    {
        var result=new List<(OptionButton Picker,string[] Ids)>();
        if(!requirement.TryGetProperty("movementDestinationChoicesByIndex",out var byIndex)||byIndex.ValueKind!=JsonValueKind.Object)return result;
        foreach(var entry in byIndex.EnumerateObject().OrderBy(p=>int.Parse(p.Name))) {
            var index=int.Parse(entry.Name);var choices=ReadChoices(entry.Value);
            parent.AddChild(new Label {Text=$"第 {index+1} 次移动终点"});
            var picker=new OptionButton {FitToLongestItem=false,CustomMinimumSize=new Vector2(0,38)};
            picker.AddItem("请选择终点");foreach(var choice in choices)picker.AddItem(choice.Label);
            var ids=choices.Select(c=>c.Id).Prepend("").ToArray();parent.AddChild(picker);
            picker.Select(Math.Max(0,Array.IndexOf(ids,initial?.ElementAtOrDefault(index)??"")));
            picker.ItemSelected+=_=>Refresh();picker.FocusEntered+=()=>{_focusedMovement=picker;TableSelectionChanged?.Invoke();};
            result.Add((picker,ids));
        }
        return result;
    }
}
