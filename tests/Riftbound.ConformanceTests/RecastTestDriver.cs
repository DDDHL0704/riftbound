using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
namespace Riftbound.ConformanceTests;

internal static class RecastTestDriver
{
    internal static async Task<ResolutionResult> Open(ResolutionResult result)
    {
        var events = result.Events.ToList();
        for (var i=0; i<12 && result.State.PendingEffectPlay is null; i++)
        {
            Assert.NotEmpty(result.State.StackItems);
            result = await OfficialGraveyardRecastTests.Top(result.State); events.AddRange(result.Events);
        }
        Assert.NotNull(result.State.PendingEffectPlay);
        return result with { Events=events };
    }
    internal static async Task<ResolutionResult> Complete(ResolutionResult result, string source, string[] targets)
    {
        result = await Open(result); var events=result.Events.ToList();
        result = await OfficialGraveyardRecastTests.Act(result.State, result.State.PendingEffectPlay!.PlayerId,
            new PlayCardCommand(source,result.State.CardObjects[source].CardNo!,targets));
        events.AddRange(result.Events);
        for(var i=0;i<12 && result.State.StackItems.Count>0;i++)
        {
            result = await OfficialGraveyardRecastTests.Top(result.State);events.AddRange(result.Events);
            if(result.State.PendingCardChoice is { } choice)
            {
                result=await OfficialGraveyardRecastTests.Act(result.State,choice.PlayerId,new ChooseCardsCommand(choice.ChoiceId,choice.ChoiceWindow,[]));events.AddRange(result.Events);
            }
        }
        Assert.Empty(result.State.StackItems);
        return result with {Events=events};
    }
}
