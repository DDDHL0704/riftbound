using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class OfficialPrintedTimingTests
{
    [Theory]
    [InlineData("OGS·011/024", false, true)] // 闪现
    [InlineData("OGN·104/298", false, true)] // 择日再战
    [InlineData("OGN·207/298", false, true)] // 荣耀召唤
    [InlineData("OGN·058/298", false, true)] // 训练有素
    [InlineData("UNL-061/219", false, true)] // 台前作秀
    [InlineData("SFD·022/221", false, true)] // 灵便 card permission, 819.1.b
    [InlineData("OGN·024/298", true, false)] // 虚空索敌
    [InlineData("OGN·173/298", true, false)] // 驭风而行
    [InlineData("OGN·105/298", false, false)] // 星芒凝汇
    [InlineData("SFD·125/221", false, false)] // 大力仙灵
    [InlineData("UNL-002/219", false, false)] // Ambush reminder is conditional
    [InlineData("SFD·082/221", false, false)] // Swift applies to an activated skill
    public void PrintedPermissionsApplyToBothOpenDuelsAndClosedWindows(string cardNo, bool swift, bool reaction)
    {
        Assert.True(CardBehaviorRegistry.TryGetByCardNo(cardNo, out var definition));
        var profile = CardPermissionKeywordRules.BuildProfile(definition);
        Assert.Equal(swift, profile.HasSwift);
        Assert.Equal(reaction, profile.HasReaction);
        var state = new MatchState("PRINTED-TIMING", 1, 3, "P1", new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" },
            status: MatchStatuses.InProgress, phase: MatchPhases.Main, timingState: TimingStates.SpellDuelOpen,
            focusPlayerId: "P2");
        Assert.Equal(swift || reaction, CardPermissionKeywordRules.EvaluatePlayTiming(state, "P2", definition).IsAllowed);
        Assert.False(CardPermissionKeywordRules.EvaluatePlayTiming(state, "P1", definition).IsAllowed);
        foreach (var closed in new[] { TimingStates.NeutralClosed, TimingStates.SpellDuelClosed })
        {
            var closedState = state with { TimingState = closed, FocusPlayerId = null, PriorityPlayerId = "P2",
                StackItems = [new("pending", "P1", "source", "test", timingContext: TimingStates.SpellDuelOpen)] };
            Assert.Equal(reaction, CardPermissionKeywordRules.EvaluatePlayTiming(closedState, "P2", definition).IsAllowed);
            Assert.False(CardPermissionKeywordRules.EvaluatePlayTiming(closedState, "P1", definition).IsAllowed);
        }
    }
}
