using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class RadRunebladeAnvilTests
{
    private const string Anvil = "RAD·012/167";
    private const string Skill = P4ActivatedAbilityCatalog.RunebladeAnvilAbilityId;

    [Fact]
    public async Task ExactPrintingPlaysToBaseAndEarnsASeparateBanishSkill()
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, c => c.CardNo == Anvil);
        Assert.Equal(39502, card.Id); Assert.Equal(1, card.Energy); Assert.Equal(0, card.ReturnEnergy ?? 0);
        Assert.Contains("或当此牌离场时", card.CardEffect);
        var game = new Driver(Position("HAND"));
        await game.Act("P1", new PlayCardCommand("ANVIL", Anvil, [], Destination: "BASE"));
        Assert.Equal(29, game.State.RunePools["P1"].Mana);
        Assert.False(game.State.CardObjects["ANVIL"].IsExhausted);
        Assert.Contains("ANVIL", game.State.PlayerZones["P1"].Base);
        Assert.Empty(game.State.TimedBanishPlayGrants ?? []);
        Assert.Equal("PLAY_OR_LEAVE", Assert.Single(game.State.StackItems).UnitEvent!.Timing);
        await game.Top();
        AssertGrant(game.State, "P1", "TOP");
        await game.Replay();
    }

    [Fact]
    public async Task SelfDestructionIsAnEffectAfterThePrintedExhaustionAndResourceCost()
    {
        var game = new Driver(Position());
        await game.Act("P1", new ActivateAbilityCommand("ANVIL", Skill, []));
        Assert.True(game.State.CardObjects["ANVIL"].IsExhausted);
        Assert.Equal(28, game.State.RunePools["P1"].Mana);
        Assert.Equal(19, game.State.RunePools["P1"].TotalPower);
        Assert.Contains("ANVIL", game.State.PlayerZones["P1"].Base);
        Assert.Null(Assert.Single(game.State.StackItems).UnitEvent);
        await game.Top();
        Assert.Contains("ANVIL", game.State.PlayerZones["P1"].Graveyard);
        Assert.Empty(game.State.TimedBanishPlayGrants ?? []);
        Assert.NotNull(Assert.Single(game.State.StackItems).UnitEvent);
        await game.Top(); AssertGrant(game.State, "P1", "TOP");
        await game.Replay();
    }

    [Theory]
    [InlineData("mana")] [InlineData("trait")] [InlineData("exhausted")]
    [InlineData("opponent")] [InlineData("target")] [InlineData("instance")]
    public async Task InvalidActivationCannotSpendAnythingOrEarnADeparture(string branch)
    {
        var state = Position();
        if (branch is "mana" or "trait") state = state with {
            RunePools = new Dictionary<string, RunePool>(state.RunePools) {
                ["P1"] = branch == "mana" ? new(1, 10) : new(30, 0, new Dictionary<string, int> { [RuneTrait.Blue] = 10 }) } };
        if (branch == "exhausted") state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["ANVIL"] = state.CardObjects["ANVIL"] with { IsExhausted = true } } };
        var command = new ActivateAbilityCommand("ANVIL", Skill, branch == "target" ? ["ALLY"] : []);
        if (branch == "instance") command = command with { AbilityInstanceId = "ATTACHED:missing:0:1" };
        await RadGearTestPosition.Reject(state, branch == "opponent" ? "P2" : "P1", command);
    }

    [Theory]
    [InlineData("SFD·135/221", "HAND")]
    [InlineData("RAD·124/167", "BANISHED")]
    [InlineData("RAD·101/167", "GRAVEYARD")]
    public async Task AllActualDepartureKindsCaptureTheOldGenerationBeforeReset(string spell, string destination)
    {
        var state = AttackDamageDistributionTests.Spell(Position(), "SPELL", spell, "P1");
        var game = new Driver(state);
        var generation = state.CardObjects["ANVIL"].ObjectGeneration;
        await game.Act("P1", new PlayCardCommand("SPELL", spell, ["ANVIL"])); await game.Top();
        Assert.Equal(destination, game.State.ObjectLocations["ANVIL"].Zone);
        Assert.True(game.State.CardObjects["ANVIL"].ObjectGeneration > generation);
        var skill = Assert.Single(game.State.StackItems);
        Assert.Equal(generation, skill.UnitEvent!.SourceGeneration);
        Assert.Equal("P1", skill.ControllerId);
        // Balefire burns two cards after the departure; capture must survive it.
        var top = game.State.PlayerZones["P1"].MainDeck[0];
        await game.Top(); AssertGrant(game.State, "P1", top);
        await game.Replay();
    }

    [Fact]
    public async Task TheGrantStillPaysTheFullCardCostAndIsConsumedByASuccessfulPlay()
    {
        var game = new Driver(Position("HAND"));
        await game.Act("P1", new PlayCardCommand("ANVIL", Anvil, [], Destination: "BASE")); await game.Top();
        var mana = game.State.RunePools["P1"].Mana;
        await game.Act("P1", new PlayCardCommand("TOP", "OGN·048/298", []));
        Assert.Equal(mana - 2, game.State.RunePools["P1"].Mana);
        Assert.Empty(game.State.TimedBanishPlayGrants ?? []);
        await game.Top(); Assert.Contains("TOP", game.State.PlayerZones["P1"].Graveyard);
        await game.Replay();
    }

    [Fact]
    public async Task EnteringMyEndingPhaseExpiresThePermissionWithoutReturningTheCard()
    {
        var game = new Driver(Position("HAND"));
        await game.Act("P1", new PlayCardCommand("ANVIL", Anvil, [], Destination: "BASE")); await game.Top();
        AssertGrant(game.State, "P1", "TOP");
        await game.Act("P1", new EndTurnCommand());
        Assert.Empty(game.State.TimedBanishPlayGrants ?? []);
        Assert.Contains("TOP", game.State.PlayerZones["P1"].Banished);
        await game.Replay();
    }

    [Fact]
    public async Task AnEmptyDeckDoesNotInventAPermissionOrBurnOut()
    {
        var state = Position();
        var deck = state.PlayerZones["P1"].MainDeck.ToHashSet(StringComparer.Ordinal);
        state = state with { PlayerZones = state.PlayerZones.ToDictionary(p => p.Key,
            p => p.Key == "P1" ? p.Value with { MainDeck = [] } : p.Value),
            CardObjects = state.CardObjects.Where(p => !deck.Contains(p.Key)).ToDictionary(),
            ObjectLocations = state.ObjectLocations.Where(p => !deck.Contains(p.Key)).ToDictionary() };
        var game = new Driver(state);
        await game.Act("P1", new ActivateAbilityCommand("ANVIL", Skill, [])); await game.Top(); await game.Top();
        Assert.Empty(game.State.TimedBanishPlayGrants ?? []);
        Assert.Equal(0, game.State.PlayerScores.GetValueOrDefault("P2"));
        await game.Replay();
    }

    [Fact]
    public async Task DepartingBorrowedEquipmentUsesItsCapturedControllerRatherThanOwner()
    {
        var state = Position() with { CardObjects = new Dictionary<string, CardObjectState>(Position().CardObjects) {
            ["ANVIL"] = Position().CardObjects["ANVIL"] with { OwnerId = "P2" } } };
        var game = new Driver(state);
        await game.Act("P1", new ActivateAbilityCommand("ANVIL", Skill, [])); await game.Top();
        Assert.Contains("ANVIL", game.State.PlayerZones["P2"].Graveyard);
        await game.Top(); AssertGrant(game.State, "P1", "TOP");
        await game.Replay();
    }

    [Fact]
    public async Task RestoreRejectsReboundOrMixedPermissionProvenance()
    {
        var game = new Driver(Position());
        await game.Act("P1", new ActivateAbilityCommand("ANVIL", Skill, [])); await game.Top(); await game.Top();
        var grant = Assert.Single(game.State.TimedBanishPlayGrants!);
        Assert.Equal(MatchStateHasher.Hash(game.State), MatchStateHasher.Hash(OfficialInsightAndSpellLockTests.Restore(game.State)));
        foreach (var forged in new[] {
            grant with { UnitEvent = null }, grant with { ResolvingStackItemId = "ordered-fake" },
            grant with { UnitEvent = grant.UnitEvent! with { SourceObjectId = "ALLY" } },
            grant with { Card = grant.Card with { Generation = grant.Card.Generation + 1 } },
            grant with { UnitEvent = grant.UnitEvent! with { Context = grant.UnitEvent.Context with { CapturedTick = game.State.Tick + 1 } } } })
            Assert.Contains("invalid timed banish play permission", OfficialInsightAndSpellLockTests.Errors(
                game.State with { TimedBanishPlayGrants = [forged with { Id = TimedBanishPlayRules.Id(forged) }] }));
        await game.Replay();
    }

    [Fact]
    public async Task AnOpponentsTurnGrantSurvivesUntilEnteringMyEndingPhase()
    {
        var state = Position() with { TurnPlayerId = "P2", ActivePlayerId = "P2" };
        state = AttackDamageDistributionTests.Spell(state, "RETURN", "SFD·135/221", "P2");
        var game = new Driver(state);
        await game.Act("P2", new PlayCardCommand("RETURN", "SFD·135/221", ["ANVIL"])); await game.Top(); await game.Top();
        AssertGrant(game.State, "P1", "TOP");
        Assert.Null(Assert.Single(game.State.TimedBanishPlayGrants!).ExpiringTurnNumber);
        await game.Act("P2", new EndTurnCommand());
        AssertGrant(game.State, "P1", "TOP");
        Assert.Equal(game.State.TurnNumber, Assert.Single(game.State.TimedBanishPlayGrants!).ExpiringTurnNumber);
        await game.Act("P1", new EndTurnCommand());
        Assert.Empty(game.State.TimedBanishPlayGrants ?? []);
        await game.Replay();
    }

    [Fact]
    public async Task SimultaneousReturnedSourcesEarnIndependentPermissionsInChosenOrder()
    {
        var state = RadGearTestPosition.Put(Position(), "SECOND", Anvil, "P1", "BASE");
        state = AttackDamageDistributionTests.Spell(state, "RETURN", "SFD·147/221", "P1");
        state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) {
            ["P1"] = new(30, 0, new Dictionary<string, int> { [RuneTrait.Purple] = 10 }) } };
        var game = new Driver(state);
        await game.Act("P1", new PlayCardCommand("RETURN", "SFD·147/221", [])); await game.Top();
        Assert.Equal(2, game.State.TriggerQueue.Count(t => t.UnitEvent is not null));
        Assert.All(ResolutionResult.OrderTriggerViews(game.State, "P2"), row => {
            Assert.Equal("HIDDEN", row["sourceObjectId"]);
            Assert.Equal("CAPTURED_UNIT_EVENT_ABILITY", row["effectKind"]);
        });
        Assert.All(ResolutionResult.OrderTriggerViews(game.State, "P1"), row =>
            Assert.Contains(row["sourceObjectId"], new object[] { "ANVIL", "SECOND" }));
        await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: game.State.TriggerQueue.Select(t => t.TriggerId).Reverse().ToArray()));
        await game.Top(); await game.Top();
        var grants = game.State.TimedBanishPlayGrants!;
        Assert.Equal(2, grants.Count); Assert.Equal(2, grants.Select(g => g.Card).Distinct().Count());
        Assert.Equal(new[] { "ANVIL", "SECOND" }, grants.Select(g => g.UnitEvent!.SourceObjectId).Order().ToArray());
        Assert.Contains("ANVIL", game.State.PlayerZones["P1"].Hand);
        Assert.Contains("SECOND", game.State.PlayerZones["P1"].Hand);
        await game.Replay();
    }

    [Fact]
    public async Task ARecycledSourceStillEarnsTheCapturedBanishSkill()
    {
        var state = Position();
        var keep = new List<string>();
        foreach (var player in new[] { "P1", "P2" })
            foreach (var kind in new[] { "UNIT", "GEAR", "RUNE", "HAND" })
                for (var i = 0; i < 2; i++)
                {
                    var id = $"{player}-{kind}-{i}";
                    var no = kind == "GEAR" ? "OGN·077/298" : kind == "RUNE" ? "UNL-R06" : "OGN·219/298";
                    state = RadGearTestPosition.Put(state, id, no, player, kind == "HAND" ? "HAND" : "BASE");
                    keep.Add(id);
                }
        state = AttackDamageDistributionTests.Spell(state, "RECYCLE", "OGN·244/298", "P1");
        state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(30, 30) } };
        var game = new Driver(state);
        await game.Act("P1", new PlayCardCommand("RECYCLE", "OGN·244/298", keep)); await game.Top();
        Assert.Contains("ANVIL", game.State.PlayerZones["P1"].MainDeck);
        Assert.Equal("PLAY_OR_LEAVE", Assert.Single(game.State.StackItems).UnitEvent!.Timing);
        await game.Top(); AssertGrant(game.State, "P1", "TOP");
        await game.Replay();
    }

    private static void AssertGrant(MatchState state, string player, string id)
    {
        var grant = Assert.Single(state.TimedBanishPlayGrants!);
        Assert.Null(grant.Movement); Assert.NotNull(grant.UnitEvent);
        Assert.Equal(player, grant.ControllerId); Assert.Equal(id, grant.Card.ObjectId);
        Assert.Contains(id, state.PlayerZones[player].Banished);
        Assert.DoesNotContain(id, state.PlayerZones[player].Hand);
    }

    private static MatchState Position(string zone = "BASE")
    {
        var state = RadGearTestPosition.Put(RadGearTestPosition.Create(), "ANVIL", Anvil, "P1", zone);
        state = RadGearTestPosition.Put(state, "TOP", "OGN·048/298", "P1", "MAIN_DECK");
        return state with {
            PlayerZones = state.PlayerZones.ToDictionary(p => p.Key,
                p => p.Key == "P1" ? p.Value with { MainDeck = ["TOP", ..p.Value.MainDeck.Where(id => id != "TOP")] } : p.Value),
            RunePools = new Dictionary<string, RunePool>(state.RunePools) {
                ["P1"] = new(30, 0, new Dictionary<string, int> { [RuneTrait.Red] = 10, [RuneTrait.Yellow] = 10 }) } };
    }
}
