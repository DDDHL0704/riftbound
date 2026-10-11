using Riftbound.Contracts;
namespace Riftbound.Engine;
public sealed partial class CoreRuleEngine
{
    private const string MovementScoreKind="UNIT_MOVED_ORDINAL_SCORE";
    private static MovementScoreSpec? MovementScoreDefinition(string? no)
        => CardBehaviorRegistry.TryGetByCardNo(no??"",out var behavior)?behavior.MovementScore:null;
    private static StackResolutionResult ResolveMovementScore(MatchState state,StackItemState item)
    {
        var amount=MovementScoreDefinition(item.MovementObserver!.CardNo)!.ScoreAmount;
        var scores=NormalizeScoresForSeats(state);scores[item.ControllerId]=checked(scores[item.ControllerId]+amount);
        var winner=WinningPlayerId(scores,EffectiveWinningScore(state));
        var events=new List<GameEvent> {new("SCORE_GAINED","移动触发技能获得分数",new Dictionary<string,object?> {
            ["playerId"]=item.ControllerId,["amount"]=amount,["score"]=scores[item.ControllerId],
            ["reason"]=MovementScoreKind,["sourceObjectId"]=item.SourceObjectId,["stackItemId"]=item.StackItemId })};
        if(winner is not null) events.Add(new("MATCH_WON","达到获胜分数并获胜",new Dictionary<string,object?> {
            ["winnerPlayerId"]=winner,["winningScore"]=EffectiveWinningScore(state)}));
        return NoopStackResolutionResult(state) with {PlayerScores=scores,WinnerPlayerId=winner,Events=events};
    }
}
