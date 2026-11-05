using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;
using static Riftbound.ConformanceTests.OfficialGarenConquestBattlefieldCountTests;

namespace Riftbound.ConformanceTests;

public sealed class PrintedExhaustAbilityTests
{
    internal static MatchState Position(string cardNo)
    {
        var state = ArmamentMightTests.Position();
        state = OfficialHoldSequenceTests.AddUnit(state, "ALLY", "OGN·219/298", "F", "P1");
        state = LocalDestructionRecallTests.At(state, "ALLY", "P1", "BASE");
        state = OfficialHoldSequenceTests.AddUnit(state, "ENEMY", "OGN·088/298", "OTHER", "P2");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["HOST"] = Printed("HOST", cardNo, "P1") with { IsExhausted = false },
            ["ALLY"] = Printed("ALLY", "OGN·219/298", "P1") with { IsExhausted = true },
            ["ENEMY"] = Printed("ENEMY", "OGN·088/298", "P2") },
            RunePools = new Dictionary<string, RunePool>(state.RunePools) {
                ["P1"] = new(20, 20, new Dictionary<string, int> {
                    ["red"] = 10, ["green"] = 10, ["blue"] = 10,
                    ["orange"] = 10, ["purple"] = 10, ["yellow"] = 10 }),
                ["P2"] = new(20, 20, new Dictionary<string, int> { ["blue"] = 10, ["yellow"] = 10 }) } };
        return cardNo is "OGN·068/298" or "ARC-002/006"
            ? LocalDestructionRecallTests.At(state, "HOST", "P1", "BATTLEFIELD", "F") : state;
    }

    private static IReadOnlyDictionary<string, object?> Requirement(MatchState state)
        => Assert.Single((ResolutionResult.BuildPrompts(state)["P1"].Candidates ?? [])
            .Where(candidate => candidate.Enabled && candidate.Action == CommandTypes.ActivateAbility)
            .SelectMany(candidate => candidate.Metadata?.GetValueOrDefault("sourceRequirements")
                is IEnumerable<IReadOnlyDictionary<string, object?>> rows ? rows : []),
            row => Equals(row.GetValueOrDefault("sourceObjectId"), "HOST")
                && row.GetValueOrDefault("abilityId") is string id && id.StartsWith("BORROWED:NATIVE:"));

    internal static string Ability(MatchState state) => Assert.IsType<string>(Requirement(state)["abilityId"]);

    [Theory]
    [InlineData("UNL-161/219", "ALLY", 0, 2)]
    [InlineData("OGN·017/298", "ENEMY", 2, 0)]
    [InlineData("OGN·068/298", "ENEMY", 3, 0)]
    [InlineData("ARC-002/006", "ENEMY", 3, 0)]
    [InlineData("OGN·124/298", "ALLY", 0, 0)]
    public async Task PrintedSkillNeedsNoBorrowerAndPaysBeforeItsIndependentStackItemResolves(
        string cardNo, string target, int damage, int might)
    {
        var game = new Driver(Position(cardNo));
        var before = game.State;
        var requirement = Requirement(before);
        Assert.Equal("横置技能", requirement["displayName"]);
        var ability = Ability(before);
        await game.Act("P1", new ActivateAbilityCommand("HOST", ability, [target]));
        Assert.Equal(before.RunePools["P1"].Mana, game.State.RunePools["P1"].Mana);
        Assert.Equal(before.RunePools["P1"].TotalPower, game.State.RunePools["P1"].TotalPower);
        Assert.Equal(0, game.State.CardObjects[target].Damage);
        Assert.Equal(0, game.State.CardObjects[target].UntilEndOfTurnPowerModifier);
        Assert.DoesNotContain(CardObjectTags.Boon, game.State.CardObjects[target].Tags);
        var item = Assert.Single(game.State.StackItems);
        var context = Assert.IsType<BorrowedExhaustContext>(item.BorrowedExhaust);
        Assert.True(context.Native);
        Assert.Equal(context.Borrower, context.Donor);
        if (cardNo == "UNL-161/219") Assert.Contains("HOST", game.State.PlayerZones["P1"].Graveyard);
        else Assert.True(game.State.CardObjects["HOST"].IsExhausted);
        foreach (var forged in new[] {
            item with { BorrowedExhaust = context with { Native = false } },
            item with { BorrowedExhaust = context with { AbilityIndex = 99 } },
            item with { BorrowedExhaust = context with { Donor = context.Donor with { PhysicalCardNo = "FORGED" } } } })
            Assert.Contains("invalid captured borrowed skill", OfficialInsightAndSpellLockTests.Errors(game.State with { StackItems = [forged] }));
        await game.Top();
        Assert.Empty(game.State.StackItems);
        Assert.Equal(damage, game.State.CardObjects[target].Damage);
        Assert.Equal(might, game.State.CardObjects[target].UntilEndOfTurnPowerModifier);
        Assert.Equal(cardNo == "OGN·124/298", game.State.CardObjects[target].Tags.Contains(CardObjectTags.Boon));
        if (cardNo == "OGN·124/298")
        {
            Assert.Equal(1, game.State.CardObjects[target].BoonCount);
            Assert.Equal(before.CardObjects[target].Power + 1, game.State.CardObjects[target].Power);
        }
        await game.Replay();
    }

    [Theory]
    [InlineData("OGN·068/298")]
    [InlineData("ARC-002/006")]
    public async Task CaitlynRequiresABattlefieldButMayTargetAnEnemyAtAnotherBattlefield(string cardNo)
    {
        var initial = Position(cardNo);
        var id = Ability(initial);
        var inBase = LocalDestructionRecallTests.At(initial, "HOST", "P1", "BASE");
        await Reject(inBase, new ActivateAbilityCommand("HOST", id, ["ENEMY"]));
        var friendlyHere = LocalDestructionRecallTests.At(initial, "ALLY", "P1", "BATTLEFIELD", "F");
        await Reject(friendlyHere, new ActivateAbilityCommand("HOST", id, ["ALLY"]));
        var game = new Driver(initial);
        await game.Act("P1", new ActivateAbilityCommand("HOST", id, ["ENEMY"]));
        await game.Top();
        Assert.Equal(3, game.State.CardObjects["ENEMY"].Damage);
        await game.Replay();
    }

    [Theory]
    [InlineData("OGN·017/298", "ALLY")]
    [InlineData("OGN·124/298", "ENEMY")]
    [InlineData("UNL-161/219", "GEAR")]
    public async Task IllegalTargetsLeaveResourcesSourceAndAllStateUntouched(string cardNo, string target)
    {
        var initial = Position(cardNo);
        if (cardNo == "OGN·124/298") initial = initial with {
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["ENEMY"] = initial.CardObjects["ENEMY"] with { IsExhausted = true } } };
        await Reject(initial, new ActivateAbilityCommand("HOST", Ability(initial), [target]));
    }

    [Fact]
    public async Task IronBallistaMayAlsoDamageAFriendlyBattlefieldUnit()
    {
        var game = new Driver(LocalDestructionRecallTests.At(Position("OGN·017/298"), "ALLY", "P1", "BATTLEFIELD", "F"));
        await game.Act("P1", new ActivateAbilityCommand("HOST", Ability(game.State), ["ALLY"]));
        await game.Top();
        Assert.Equal(2, game.State.CardObjects["ALLY"].Damage);
        await game.Replay();
    }

    [Fact]
    public async Task ArenaBarCannotTargetAnActiveUnitOrStackBoonOnAnOrdinaryUnit()
    {
        var initial = Position("OGN·124/298");
        var id = Ability(initial);
        var active = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["ALLY"] = initial.CardObjects["ALLY"] with { IsExhausted = false } } };
        await Reject(active, new ActivateAbilityCommand("HOST", id, ["ALLY"]));
        var game = new Driver(OfficialHoldSequenceTests.AddLegend(initial, "VEN·149"));
        await game.Act("P1", new ActivateAbilityCommand("HOST", id, ["ALLY"]));
        await game.Top();
        Assert.Contains(CardObjectTags.Boon, game.State.CardObjects["ALLY"].Tags);
        var might = CurrentPowerTargetTests.Snapshot(game.State).GetProperty("players").GetProperty("P1")
            .GetProperty("objects").GetProperty("ALLY").GetProperty("effectivePower").GetInt32();
        await game.Act("P1", new LegendActCommand("LEGEND", LegendActionAbilityCatalog.JayceLegendReadyOneAbilityId, ["HOST"], ["SPEND_MANA:1"]));
        await game.Top();
        Assert.False(game.State.CardObjects["HOST"].IsExhausted);
        await game.Act("P1", new ActivateAbilityCommand("HOST", Ability(game.State), ["ALLY"]));
        await game.Top();
        Assert.Equal(might, CurrentPowerTargetTests.Snapshot(game.State).GetProperty("players").GetProperty("P1")
            .GetProperty("objects").GetProperty("ALLY").GetProperty("effectivePower").GetInt32());
        await game.Replay();
    }

    [Theory]
    [InlineData("OGN·017/298")]
    [InlineData("OGN·068/298")]
    [InlineData("OGN·124/298")]
    public async Task OrdinarySkillsCannotBeUsedDuringAClosedResponseWindow(string cardNo)
    {
        var initial = AttackDamageDistributionTests.Spell(Position(cardNo), "OLDER", "OGN·048/298", "P1");
        var id = Ability(initial);
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("OLDER", "OGN·048/298", []));
        await Reject(game.State, new ActivateAbilityCommand("HOST", id, [cardNo == "OGN·124/298" ? "ALLY" : "ENEMY"]));
        await game.Replay();
    }

    [Theory]
    [InlineData("OGN·068/298")]
    [InlineData("ARC-002/006")]
    public async Task RemovingCaitlynInResponseLeavesNoMightForHerAlreadyCapturedSkill(string cardNo)
    {
        var initial = AttackDamageDistributionTests.Spell(Position(cardNo), "LEAVE", "OGN·104/298", "P1");
        var game = new Driver(initial);
        await game.Act("P1", new ActivateAbilityCommand("HOST", Ability(initial), ["ENEMY"]));
        await game.Act("P1", new PlayCardCommand("LEAVE", "OGN·104/298", ["HOST"]));
        await game.Top();
        Assert.Contains("HOST", game.State.PlayerZones["P1"].Hand);
        await game.Top();
        Assert.Equal(0, game.State.CardObjects["ENEMY"].Damage);
        await game.Replay();
    }

    [Theory]
    [InlineData("OGN·068/298")]
    [InlineData("ARC-002/006")]
    public async Task CaitlynUsesHerMightAfterResponsesRatherThanTheActivationTimeValue(string cardNo)
    {
        var game = new Driver(AttackDamageDistributionTests.Spell(Position(cardNo), "BUFF", "SFD·034/221", "P1"));
        await game.Act("P1", new ActivateAbilityCommand("HOST", Ability(game.State), ["ENEMY"]));
        await game.Act("P1", new PlayCardCommand("BUFF", "SFD·034/221", ["HOST"]));
        await game.Top();
        Assert.Equal(2, game.State.CardObjects["HOST"].UntilEndOfTurnPowerModifier);
        await game.Top();
        Assert.Equal(5, game.State.CardObjects["ENEMY"].Damage);
        await game.Replay();
    }

    [Fact]
    public async Task ScryingShellUsesAnOpenSpellDuelAndPaysItsDestructionBeforeResolving()
    {
        var initial = AttackDamageDistributionTests.Spell(Position("UNL-161/219"), "READY", "UNL-009/219", "P1");
        var game = new Driver(initial);
        var id = Ability(initial);
        await game.Act("P1", new PlayCardCommand("READY", "UNL-009/219", ["ALLY"]));
        await Reject(game.State, new ActivateAbilityCommand("HOST", id, ["ALLY"]));
        await game.Top();
        await game.Act("P1", new MoveUnitCommand("ALLY", "BASE", "BATTLEFIELD:OTHER"));
        if (game.State.FocusPlayerId == "P2") await game.Act("P2", new PassFocusCommand());
        Assert.Equal(TimingStates.SpellDuelOpen, game.State.TimingState);
        await game.Act("P1", new ActivateAbilityCommand("HOST", Ability(game.State), ["ALLY"]));
        Assert.Single(game.State.StackItems);
        Assert.Contains("HOST", game.State.PlayerZones["P1"].Graveyard);
        Assert.Equal(0, game.State.CardObjects["ALLY"].UntilEndOfTurnPowerModifier);
        await game.Top();
        Assert.Empty(game.State.StackItems);
        Assert.Equal(2, game.State.CardObjects["ALLY"].UntilEndOfTurnPowerModifier);
        await game.Replay();
    }

    [Theory]
    [InlineData("UNL-001/219")]
    [InlineData("RAD·027/167")]
    [InlineData("OGN·078/298")]
    public void OtherNativeCatalogsAndLeeSinDoNotGainDuplicateGenericSkills(string cardNo)
    {
        var initial = Position(cardNo);
        var rows = (ResolutionResult.BuildPrompts(initial)["P1"].Candidates ?? [])
            .Where(c => c.Action == CommandTypes.ActivateAbility)
            .SelectMany(c => c.Metadata?.GetValueOrDefault("sourceRequirements")
                is IEnumerable<IReadOnlyDictionary<string, object?>> requirements ? requirements : []);
        Assert.DoesNotContain(rows, row => Equals(row.GetValueOrDefault("sourceObjectId"), "HOST")
            && row.GetValueOrDefault("abilityId") is string id && id.StartsWith("BORROWED:NATIVE:"));
    }

    private static async Task Reject(MatchState initial, ActivateAbilityCommand command)
    {
        var hash = MatchStateHasher.Hash(initial);
        var result = await new CoreRuleEngine().ResolveAsync(initial,
            new("reject-native-" + command.SourceObjectId, "P1", command.CmdType), command, default);
        Assert.False(result.Accepted);
        Assert.Empty(result.Events);
        Assert.Equal(hash, MatchStateHasher.Hash(result.State));
    }
}
