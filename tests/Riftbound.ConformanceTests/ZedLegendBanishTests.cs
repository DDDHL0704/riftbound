using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;

namespace Riftbound.ConformanceTests;

public sealed class ZedLegendBanishTests
{
    private static MatchState Legend(MatchState state, string no = "VEN·143", string player = "P1")
    {
        var zones = state.PlayerZones.ToDictionary(p => p.Key, p => p.Value with {
            LegendZone = p.Value.LegendZone.Where(id => id != "LEGEND").ToArray() });
        zones[player] = zones[player] with { LegendZone = ["LEGEND"] };
        return state with { PlayerZones = zones,
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["LEGEND"] = new("LEGEND", cardNo: no, ownerId: player, controllerId: player, tags: ["传奇"]) },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["LEGEND"] = new(player, "LEGEND") } };
    }

    [Theory]
    [InlineData("VEN·143")] [InlineData("VEN·191")] [InlineData("VEN·191*")]
    public async Task FlowBanishesTheOwnedSpellAndTheEarnedSkillWaitsForResponses(string no)
    {
        var game = new VenBasicFlowSpellTests.Driver(Legend(VenFlowCombatKeywordTests.Position(true), no));
        await game.Act("P1", new PlayCardCommand("SPELL", "VEN·012", ["ALLY"]));
        Assert.False(game.State.CardObjects["LEGEND"].IsEmpowered);
        await game.Top();
        Assert.Contains("SPELL", game.State.PlayerZones["P1"].Banished);
        Assert.False(game.State.CardObjects["LEGEND"].IsEmpowered);
        Assert.Equal("CONTROLLER_OWNED_BANISH_EMPOWER_SOURCE", Assert.Single(game.State.StackItems).EffectKind);
        await game.Top();
        Assert.True(game.State.CardObjects["LEGEND"].IsEmpowered);
        await game.Replay();
    }

    [Fact]
    public async Task ACounteredFlowSpellStillBanishesAndTriggersItsOwnersLegend()
    {
        var initial = AttackDamageDistributionTests.Spell(Legend(VenFlowCombatKeywordTests.Position(true)), "COUNTER", "OGN·045/298", "P2");
        var game = new VenBasicFlowSpellTests.Driver(initial);
        await game.Act("P1", new PlayCardCommand("SPELL", "VEN·012", ["ALLY"]));
        var id = Assert.Single(game.State.StackItems).StackItemId;
        await game.Act("P1", new PassPriorityCommand());
        await game.Act("P2", new PlayCardCommand("COUNTER", "OGN·045/298", [id]));
        await game.Top();
        Assert.Contains("SPELL", game.State.PlayerZones["P1"].Banished);
        Assert.Equal("CONTROLLER_OWNED_BANISH_EMPOWER_SOURCE", Assert.Single(game.State.StackItems).EffectKind);
        await game.Top(); Assert.True(game.State.CardObjects["LEGEND"].IsEmpowered);
        await game.Replay();
    }

    [Fact]
    public async Task AnOrdinarySpellGoingToGraveyardDoesNotTrigger()
    {
        var game = new VenBasicFlowSpellTests.Driver(Legend(VenFlowCombatKeywordTests.Position(false)));
        await game.Act("P1", new PlayCardCommand("SPELL", "VEN·012", ["ALLY"])); await game.Top();
        Assert.Contains("SPELL", game.State.PlayerZones["P1"].Graveyard);
        Assert.Empty(game.State.StackItems); Assert.False(game.State.CardObjects["LEGEND"].IsEmpowered);
        await game.Replay();
    }

    [Theory]
    [InlineData("P1", "P1", true)] [InlineData("P2", "P1", false)] [InlineData("P2", "P2", false)]
    public async Task BanishThenReplayRequiresBothTheResponsibleActorAndOwner(string cardOwner, string legendPlayer, bool triggers)
    {
        var initial = Legend(BanishEffectPlayTests.Position(owner: cardOwner), player: legendPlayer);
        var game = new StandbyDefendObserverTests.Driver(initial);
        await game.Act("P1", new PlayCardCommand("HUNT", BanishEffectPlayTests.Hunt, ["HOST"]));
        await game.Top();
        Assert.NotNull(game.State.PendingEffectPlay);
        await game.Act(cardOwner, new PlayCardCommand("HOST", "OGN·096/298", [], Destination: "BATTLEFIELD:F"));
        Assert.Equal(triggers, game.State.StackItems.Any(item => item.EffectKind == "CONTROLLER_OWNED_BANISH_EMPOWER_SOURCE"));
        if (triggers) { await game.Top(); Assert.True(game.State.CardObjects["LEGEND"].IsEmpowered); }
        else Assert.False(game.State.CardObjects["LEGEND"].IsEmpowered);
        await BanishEffectPlayTests.Replay(initial, game);
    }

    [Fact]
    public async Task AChangedBanishWitnessCannotPassRecovery()
    {
        var played = await Act(Legend(VenFlowCombatKeywordTests.Position(true)), "P1", new PlayCardCommand("SPELL", "VEN·012", ["ALLY"]));
        var state = (await Top(played.State)).State;
        var item = Assert.Single(state.StackItems); var context = item.EmpowerOtherObserver!;
        foreach (var forged in new[] { context with { ControllerId = "P2" }, context with { CardNo = "INVALID" },
            context with { Observed = context.Observed with { Generation = context.Observed.Generation + 100 } },
            context with { Observed = new("P2-DRAW-1", context.Observed.Generation) } })
            Assert.Contains("invalid empower observer stack", OfficialInsightAndSpellLockTests.Errors(state with { StackItems = [item with { EmpowerOtherObserver = forged }] }));
    }
    [Fact]
    public async Task DelayedReplacementKeepsItsControllerWhenAnotherPlayerLaterDealsLethalDamage()
    {
        var state = Legend(DeathObserverAuditTests.NoReplacement());
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["D"] = state.CardObjects["D"] with { OwnerId = "P1", ControllerId = "P2", Power = 6, Damage = 0 } },
            RunePools = new Dictionary<string, RunePool>(state.RunePools) {
                ["P1"] = new(20, 20, new Dictionary<string, int> { [RuneTrait.Red] = 20 }),
                ["P2"] = new(20, 20, new Dictionary<string, int> { [RuneTrait.Red] = 20 }) } };
        state = AttackDamageDistributionTests.Spell(state, "PUNISH", "UNL-007/219", "P1");
        state = AttackDamageDistributionTests.Spell(state, "DUMMY", "OGN·048/298", "P1");
        state = AttackDamageDistributionTests.Spell(state, "PING", "OGN·133/298", "P2");
        var game = new VenBasicFlowSpellTests.Driver(state);
        await game.Act("P1", new PlayCardCommand("PUNISH", "UNL-007/219", ["D"])); await game.Top();
        Assert.Equal(4, game.State.CardObjects["D"].Damage);
        var origin = Assert.Single(game.State.CardObjects["D"].BanishReplacementOrigins!);
        Assert.Equal("P1", origin.ControllerId);
        await game.Act("P1", new PlayCardCommand("DUMMY", "OGN·048/298", []));
        await game.Act("P1", new PassPriorityCommand());
        await game.Act("P2", new PlayCardCommand("PING", "OGN·133/298", [])); await game.Top();
        Assert.Contains("D", game.State.PlayerZones["P1"].Banished);
        Assert.Contains(game.State.StackItems, item => item.EffectKind == "CONTROLLER_OWNED_BANISH_EMPOWER_SOURCE");
        await game.Top(); Assert.True(game.State.CardObjects["LEGEND"].IsEmpowered);
        await game.Replay();
    }

    [Fact]
    public async Task DelayedReplacementMetadataExpiresAndCannotBeForged()
    {
        var state = Legend(DeathObserverAuditTests.NoReplacement());
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["D"] = state.CardObjects["D"] with { Power = 6, Damage = 0 } },
            RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(20, 20, new Dictionary<string, int> { [RuneTrait.Red] = 20 }) } };
        state = AttackDamageDistributionTests.Spell(state, "PUNISH", "UNL-007/219", "P1");
        var game = new VenBasicFlowSpellTests.Driver(state);
        await game.Act("P1", new PlayCardCommand("PUNISH", "UNL-007/219", ["D"])); await game.Top();
        var target = game.State.CardObjects["D"]; var origin = Assert.Single(target.BanishReplacementOrigins!);
        foreach (var invalid in new[] { origin with { ControllerId = "unknown" }, origin with { ControllerId = "P2" }, origin with { AppliedTick = game.State.Tick + 100 },
            origin with { SourceObjectId = "missing" } })
            Assert.Contains("invalid banish replacement origin", OfficialInsightAndSpellLockTests.Errors(game.State with {
                CardObjects = new Dictionary<string, CardObjectState>(game.State.CardObjects) { ["D"] = target with { BanishReplacementOrigins = [invalid] } } }));
        await game.Act("P1", new EndTurnCommand());
        Assert.Null(game.State.CardObjects["D"].BanishReplacementOrigins);
        Assert.DoesNotContain("BANISH_IF_DESTROYED_THIS_TURN", game.State.CardObjects["D"].UntilEndOfTurnEffects);
        await game.Replay();
    }

    [Fact]
    public async Task BanishingTwoOwnedCardsCreatesTwoIndependentSkills()
    {
        var state = Legend(Ven103AnyGraveyardReturnConsumerTests.UnitEntryPosition("VEN·019"));
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["GRAVE-2"] = OfficialGarenConquestBattlefieldCountTests.Printed("GRAVE-2", "OGN·219/298", "P1"),
            ["SARC"] = OfficialGarenConquestBattlefieldCountTests.Printed("SARC", "UNL-148/219", "P1") },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P1"] = state.PlayerZones["P1"] with { Hand = [..state.PlayerZones["P1"].Hand, "SARC"],
                    Graveyard = [..state.PlayerZones["P1"].Graveyard, "GRAVE-2"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) {
                ["GRAVE-2"] = new("P1", "GRAVEYARD"), ["SARC"] = new("P1", "HAND") } };
        var game = new VenBasicFlowSpellTests.Driver(state);
        await game.Act("P1", new PlayCardCommand("SARC", "UNL-148/219", [], Destination: "BASE"));
        await game.Top();
        Assert.Contains("GRAVE", game.State.PlayerZones["P1"].Banished);
        Assert.Contains("GRAVE-2", game.State.PlayerZones["P1"].Banished);
        var earned = game.State.TriggerQueue.Where(t => t.EffectKind == "CONTROLLER_OWNED_BANISH_EMPOWER_SOURCE").ToArray();
        Assert.Equal(2, earned.Length);
        await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: earned.Select(t => t.TriggerId).ToArray()));
        Assert.Equal(2, game.State.StackItems.Count(i => i.EffectKind == "CONTROLLER_OWNED_BANISH_EMPOWER_SOURCE"));
        await game.Top(); Assert.True(game.State.CardObjects["LEGEND"].IsEmpowered);
        await game.Top(); Assert.True(game.State.CardObjects["LEGEND"].IsEmpowered);
        await game.Replay();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task BanishingATokenIsNotBanishingAnOwnedCardEvenWhenItCopiesAPrintedFace(bool copied)
    {
        var initial = Legend(BanishEffectPlayTests.Position());
        var tokenNo = P6TokenFactoryCatalog.ImageTokenCardNo;
        var token = P6TokenFactoryCatalog.GetAll().Single(t => t.CardNo == tokenNo).CreateObject("HOST", "P1", "P1");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["HOST"] = copied ? initial.CardObjects["HOST"] with { TokenFactoryCardNo = tokenNo,
                CopyFaceTransitions = [new(initial.Tick, initial.CardObjects["HOST"].ObjectGeneration, tokenNo, "OGN·096/298")] } : token } };
        var game = new StandbyDefendObserverTests.Driver(initial);
        await game.Act("P1", new PlayCardCommand("HUNT", BanishEffectPlayTests.Hunt, ["HOST"])); await game.Top();
        Assert.False(game.State.CardObjects.ContainsKey("HOST"));
        Assert.False(game.State.CardObjects["LEGEND"].IsEmpowered);
        Assert.DoesNotContain(game.State.StackItems, i => i.EffectKind == "CONTROLLER_OWNED_BANISH_EMPOWER_SOURCE");
        await BanishEffectPlayTests.Replay(initial, game);
    }

    [Fact]
    public async Task MultipleDelayedReplacementsRemainDistinctAndCanBeOrdered()
    {
        var state = Legend(DeathObserverAuditTests.NoReplacement());
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["D"] = state.CardObjects["D"] with { OwnerId = "P1", ControllerId = "P2", Power = 10, Damage = 0 } },
            RunePools = new Dictionary<string, RunePool>(state.RunePools) {
                ["P1"] = new(20, 20, new Dictionary<string, int> { [RuneTrait.Red] = 20 }),
                ["P2"] = new(20, 20, new Dictionary<string, int> { [RuneTrait.Red] = 20 }) } };
        state = AttackDamageDistributionTests.Spell(state, "PUNISH-1", "UNL-007/219", "P1");
        state = AttackDamageDistributionTests.Spell(state, "PUNISH-2", "UNL-007/219", "P1");
        state = AttackDamageDistributionTests.Spell(state, "DUMMY", "OGN·048/298", "P1");
        state = AttackDamageDistributionTests.Spell(state, "PING", "OGN·133/298", "P2");
        var game = new VenBasicFlowSpellTests.Driver(state);
        foreach (var id in new[] { "PUNISH-1", "PUNISH-2" }) {
            await game.Act("P1", new PlayCardCommand(id, "UNL-007/219", ["D"])); await game.Top();
        }
        Assert.Equal(8, game.State.CardObjects["D"].Damage);
        Assert.Equal(2, game.State.CardObjects["D"].BanishReplacementOrigins!.Count);
        await game.Act("P1", new PlayCardCommand("DUMMY", "OGN·048/298", []));
        await game.Act("P1", new PassPriorityCommand());
        await game.Act("P2", new PlayCardCommand("PING", "OGN·133/298", [])); await game.Top();
        var request = Assert.IsType<RuleChoiceContinuation>(game.State.PendingRuleChoice).Request;
        Assert.Equal("P2", request.PlayerId);
        Assert.Contains(request.Options, option => option.Id == "BANISH:D:0");
        Assert.Contains(request.Options, option => option.Id == "BANISH:D:1");
        await game.Act("P2", new PayCostCommand(request.Id, "RULE_REPLACEMENT", ["BANISH:D:1"]));
        Assert.Contains("D", game.State.PlayerZones["P1"].Banished);
        Assert.Contains(game.State.StackItems, item => item.EffectKind == "CONTROLLER_OWNED_BANISH_EMPOWER_SOURCE");
        await game.Top(); Assert.True(game.State.CardObjects["LEGEND"].IsEmpowered);
        await game.Replay();
    }

    [Fact]
    public async Task AStolenFlowSpellKeepsTheOriginalPlayersDelayedBanish()
    {
        var initial = Legend(LightningRushDeckDrawFlowTests.Position(graveyard: true));
        initial = AttackDamageDistributionTests.Spell(initial, "REVERSAL", "OGN·080/298", "P2");
        var game = new StandbyDefendObserverTests.Driver(initial);
        await game.Act("P1", new PlayCardCommand("RUSH", "VEN·156", []));
        var id = Assert.Single(game.State.StackItems).StackItemId;
        await game.Act("P1", new PassPriorityCommand());
        await game.Act("P2", new PlayCardCommand("REVERSAL", "OGN·080/298", [id])); await game.Top();
        Assert.Equal("P2", Assert.Single(game.State.StackItems).ControllerId);
        await game.Top();
        Assert.Contains("RUSH", game.State.PlayerZones["P1"].Banished);
        var trigger = Assert.Single(game.State.StackItems);
        Assert.Equal("CONTROLLER_OWNED_BANISH_EMPOWER_SOURCE", trigger.EffectKind);
        Assert.Equal("P1", trigger.ControllerId);
        await game.Top(); Assert.True(game.State.CardObjects["LEGEND"].IsEmpowered);
        await BanishEffectPlayTests.Replay(initial, game);
    }

}
