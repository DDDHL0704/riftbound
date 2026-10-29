using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class VenStaticLimitUnitTests
{
    public static TheoryData<string, int, int, int> Cards => new() {
        { "VEN·004", 3, 0, 3 }, { "VEN·025", 5, 0, 5 }, { "VEN·029", 2, 0, 4 },
        { "VEN·036", 7, 2, 8 }, { "VEN·053", 2, 0, 2 }
    };

    [Theory]
    [MemberData(nameof(Cards))]
    public async Task EveryUnitPaysItsPrintedCostAndEntersExhausted(string no, int mana, int power, int might)
    {
        var initial = Position(no);
        var command = new PlayCardCommand("UNIT", no, []);
        var quote = new CoreRuleEngine().PreviewPlayCard(initial, "P1", PlayCostPreviewTests.Request(initial, command));
        Assert.True(quote.CanPay, quote.Message); Assert.Equal(mana, quote.Cost!.Mana);
        Assert.Equal(power, quote.Cost.GenericPower + quote.Cost.PowerByTrait.Values.Sum());
        var game = new Driver(initial);
        await game.Act("P1", command);
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Base);
        Assert.Equal(might, game.State.CardObjects["UNIT"].Power); Assert.True(game.State.CardObjects["UNIT"].IsExhausted);
        Assert.Empty(game.State.StackItems); await game.Replay();
    }

    [Theory]
    [MemberData(nameof(Cards))]
    public async Task UnpayableOrTargetedUnitPlaysLeaveTheWholeStateUnchanged(string no, int mana, int power, int might)
    {
        _ = mana; _ = power; _ = might;
        var initial = Position(no);
        await Reject(initial, "P1", new PlayCardCommand("UNIT", no, ["ALLY"]));
        await Reject(initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) { ["P1"] = new(0, 0) } },
            "P1", new PlayCardCommand("UNIT", no, []));
    }

    [Theory]
    [InlineData("P1", 1, false)] [InlineData("P1", 3, false)] [InlineData("P1", 5, false)] [InlineData("P1", 7, true)]
    [InlineData("P2", 2, false)] [InlineData("P2", 4, false)] [InlineData("P2", 6, false)] [InlineData("P2", 8, true)]
    public async Task ElderPoroCountsItsControllersOwnTurnsIncludingBothSeatOrders(string player, int turn, bool allowed)
    {
        var state = Position("VEN·029") with { TurnNumber = turn, TurnPlayerId = player, ActivePlayerId = player, CardPlays = new(turn, [], []) };
        if (player == "P2") state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["UNIT"] = OfficialGarenConquestBattlefieldCountTests.Printed("UNIT", "VEN·029", "P2") },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P1"] = state.PlayerZones["P1"] with { Hand = [] }, ["P2"] = state.PlayerZones["P2"] with { Hand = ["UNIT"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["UNIT"] = new("P2", "HAND") } };
        var command = new PlayCardCommand("UNIT", "VEN·029", []);
        var quote = new CoreRuleEngine().PreviewPlayCard(state, player, PlayCostPreviewTests.Request(state, command));
        Assert.Equal(allowed, quote.CanPay);
        var candidates = ResolutionResult.BuildPrompts(state)[player].Candidates ?? [];
        Assert.Equal(allowed, candidates.Any(c => c.Action == CommandTypes.PlayCard && c.Sources?.Any(source => source.Id == "UNIT") == true));
        if (allowed) { var game = new Driver(state); await game.Act(player, command); await game.Replay(); }
        else await Reject(state, player, command);
    }

    [Theory]
    [InlineData(6, "P2", 4)] [InlineData(7, "P2", 0)] [InlineData(8, "P2", 0)] [InlineData(7, "P1", 4)]
    public async Task SacredElderPreventsOnlyEnemySpellDamageWithAtLeastSevenControlledRunes(int runes, string caster, int damage)
    {
        var state = WithSourceInPlay(Position("VEN·025"));
        state = LocalDestructionRecallTests.At(state, "UNIT", "P1", "BATTLEFIELD", "BF");
        state = AddRunes(state, "P1", runes, inDeck: false);
        state = AttackDamageDistributionTests.Spell(state, "DAMAGE", "VEN·059", caster);
        state = state with { TurnPlayerId = caster, ActivePlayerId = caster };
        var game = new Driver(state);
        await game.Act(caster, new PlayCardCommand("DAMAGE", "VEN·059", ["UNIT"])); await game.Top();
        Assert.Equal(damage, game.State.CardObjects["UNIT"].Damage);
        Assert.Contains(game.Events(), e => e.Event.Kind == "DAMAGE_APPLIED" && Equals(e.Event.Payload.GetValueOrDefault("damage"), damage));
        await game.Replay();
    }

    [Fact]
    public async Task ASeventhRuneRecycledInResponseRemovesTheEldersProtectionBeforeDamage()
    {
        var state = WithSourceInPlay(Position("VEN·025"));
        state = LocalDestructionRecallTests.At(state, "UNIT", "P1", "BATTLEFIELD", "BF");
        state = AddRunes(state, "P1", 7, inDeck: false);
        state = AttackDamageDistributionTests.Spell(state, "DAMAGE", "VEN·059", "P2") with { TurnPlayerId = "P2", ActivePlayerId = "P2" };
        var game = new Driver(state);
        await game.Act("P2", new PlayCardCommand("DAMAGE", "VEN·059", ["UNIT"]));
        await game.Act("P2", new PassPriorityCommand());
        await game.Act("P1", new RecycleRuneCommand("P1-RUNE-7"));
        await game.Act("P1", new PassPriorityCommand());
        Assert.Equal(4, game.State.CardObjects["UNIT"].Damage); await game.Replay();
    }

    [Theory]
    [InlineData("OGN·128/298")]
    [InlineData("VEN·083")]
    public async Task UnitsInstructedByASpellStillDealTheirOwnDamageToTheElder(string spell)
    {
        var state = AddRunes(WithSourceInPlay(Position("VEN·025")), "P1", 7, inDeck: false);
        state = AttackDamageDistributionTests.Spell(state, "DUEL", spell, "P2") with { TurnPlayerId = "P2", ActivePlayerId = "P2" };
        var game = new Driver(state);
        await game.Act("P2", new PlayCardCommand("DUEL", spell, ["ENEMY", "UNIT"])); await game.Top();
        Assert.Equal(4, game.State.CardObjects["UNIT"].Damage);
        Assert.Contains("ENEMY", game.State.PlayerZones["P2"].Graveyard); await game.Replay();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task EldersControllerOrdersSpellImmunityAndFinitePreventionBeforeUnitDamage(bool immunityFirst)
    {
        var state = LocalDestructionRecallTests.At(WithSourceInPlay(Position("VEN·025")), "UNIT", "P1", "BATTLEFIELD", "BF");
        state = AddRunes(state, "P1", 7, inDeck: false);
        state = AttackDamageDistributionTests.Spell(state, "SHIELD", "VEN·126", "P2");
        state = AttackDamageDistributionTests.Spell(state, "HIT", "VEN·059", "P2");
        state = AttackDamageDistributionTests.Spell(state, "DUEL", "VEN·083", "P2")
            with { TurnPlayerId = "P2", ActivePlayerId = "P2" };
        var game = new Driver(state);
        await game.Act("P2", new PlayCardCommand("SHIELD", "VEN·126", ["UNIT"])); await game.Top();
        var budget = Assert.Single(game.State.CardObjects["UNIT"].UntilEndOfTurnEffects,
            effect => effect.StartsWith("DAMAGE_PREVENTION_THIS_TURN:", StringComparison.Ordinal));
        Assert.EndsWith(":7", budget);
        await game.Act("P2", new PlayCardCommand("HIT", "VEN·059", ["UNIT"])); await game.Top();
        var choice = Assert.IsType<RuleChoiceContinuation>(game.State.PendingRuleChoice);
        Assert.Equal("P1", choice.Request.PlayerId);
        Assert.Equal("DAMAGE_REPLACEMENT_ORDER", choice.Request.Kind);
        var option = immunityFirst ? "ALL" : choice.Request.Options.Single(o =>
            o.Id.StartsWith("DAMAGE_PREVENTION_THIS_TURN:", StringComparison.Ordinal)).Id;
        await game.Act("P1", new PayCostCommand(choice.Request.Id, "RULE_REPLACEMENT", [option]));
        Assert.EndsWith(immunityFirst ? ":7" : ":3", Assert.Single(game.State.CardObjects["UNIT"].UntilEndOfTurnEffects,
            effect => effect.StartsWith("DAMAGE_PREVENTION_THIS_TURN:", StringComparison.Ordinal)));
        Assert.Equal(0, game.State.CardObjects["UNIT"].Damage);
        await game.Act("P2", new PlayCardCommand("DUEL", "VEN·083", ["ENEMY", "UNIT"])); await game.Top();
        var remaining = game.State.CardObjects["UNIT"].UntilEndOfTurnEffects
            .Where(effect => effect.StartsWith("DAMAGE_PREVENTION_THIS_TURN:", StringComparison.Ordinal)).ToArray();
        if (immunityFirst) Assert.EndsWith(":3", Assert.Single(remaining));
        else Assert.Empty(remaining);
        Assert.Equal(immunityFirst ? 0 : 1, game.State.CardObjects["UNIT"].Damage);
        Assert.Contains("ENEMY", game.State.PlayerZones["P2"].Graveyard);
        await game.Replay();
    }

    [Theory]
    [InlineData("BATTLEFIELD", "P1", 1)] [InlineData("BATTLEFIELD", "P2", 1)] [InlineData("BASE", "P1", 2)]
    public async Task ChimeraLimitsBothPlayersNormalChannelOnlyWhileAtABattlefield(string zone, string actor, int count)
    {
        var state = WithSourceInPlay(Position("VEN·036"));
        if (zone == "BATTLEFIELD") state = LocalDestructionRecallTests.At(state, "UNIT", "P1", zone, "BF");
        state = AddRunes(state, actor, 4, inDeck: true);
        var previous = actor == "P1" ? "P2" : "P1";
        state = state with { TurnPlayerId = previous, ActivePlayerId = previous };
        var game = new Driver(state);
        await game.Act(previous, new EndTurnCommand());
        var called = game.State.PlayerZones[actor].Base.Where(id => id.StartsWith(actor + "-RUNE-", StringComparison.Ordinal)).ToArray();
        Assert.Equal(count, called.Length); Assert.All(called, id => Assert.False(game.State.CardObjects[id].IsExhausted));
        await game.Replay();
    }

    [Fact]
    public async Task ChimerasStageLimitIncludesExtraRunesButDoesNotLimitASpellsCallInstruction()
    {
        var state = WithSourceInPlay(Position("VEN·036"));
        state = LocalDestructionRecallTests.At(state, "UNIT", "P1", "BATTLEFIELD", "BF");
        state = AddRunes(state, "P2", 6, inDeck: true);
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["WYVERN"] = OfficialGarenConquestBattlefieldCountTests.Printed("WYVERN", "RAD·092/167", "P2") },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) { ["P2"] = state.PlayerZones["P2"] with { Base = [..state.PlayerZones["P2"].Base, "WYVERN"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["WYVERN"] = new("P2", "BASE") } };
        var game = new Driver(state);
        await game.Act("P1", new EndTurnCommand());
        Assert.Equal(5, game.State.PlayerZones["P2"].RuneDeck.Count);
        Assert.False(game.State.CardObjects["P2-RUNE-1"].IsExhausted);
        // The separately played spell still gets its ordinary extra dormant rune.
        state = AttackDamageDistributionTests.Spell(game.State, "CALL", "OGN·134/298", "P2");
        state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P2"] = new(20, 20) } };
        var call = await new CoreRuleEngine().ResolveAsync(state, new("call-outside-stage", "P2", CommandTypes.PlayCard), new PlayCardCommand("CALL", "OGN·134/298", []), default);
        Assert.True(call.Accepted, call.ErrorMessage);
        var done = await AttackDamageDistributionTests.Top(call.State);
        Assert.True(done.Accepted, done.ErrorMessage);
        Assert.Equal(3, done.State.PlayerZones["P2"].RuneDeck.Count);
        Assert.True(done.State.CardObjects["P2-RUNE-2"].IsExhausted);
        Assert.True(done.State.CardObjects["P2-RUNE-3"].IsExhausted);
        await game.Replay();
    }

    [Theory]
    [InlineData("BATTLEFIELD", 1)] [InlineData("BASE", 0)] [InlineData("OTHER", 0)]
    public async Task SurferChangesTheSharedSidesAssignmentPriorityOnlyAtItsOwnBattlefield(string location, int expectedPriority)
    {
        var state = OfficialPooledCombatDamageTests.Position(singleAttacker: true, defenderTwoPower: 4, bulwark: true);
        if (location == "BATTLEFIELD") state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["A1"] = OfficialGarenConquestBattlefieldCountTests.Printed("A1", "VEN·004", "P1") with { IsAttacking = true } } };
        else {
            state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["SURFER"] = OfficialGarenConquestBattlefieldCountTests.Printed("SURFER", "VEN·004", "P1") },
                PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) { ["P1"] = state.PlayerZones["P1"] with {
                    Base = location == "BASE" ? ["SURFER"] : [], Battlefields = location == "OTHER" ? [..state.PlayerZones["P1"].Battlefields, "SURFER"] : state.PlayerZones["P1"].Battlefields } },
                ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["SURFER"] = new("P1", location == "BASE" ? "BASE" : "BATTLEFIELD", location == "OTHER" ? "OTHER" : null) } };
        }
        var prompt = ResolutionResult.BuildPrompts(state)["P1"];
        var meta = prompt.View!.Metadata!;
        var priorities = Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(meta["targetPriority"]);
        Assert.Equal(expectedPriority, priorities["D1"]);
        var command = new AssignCombatDamageCommand("battle:BF", "BF", [new("A1", "D2", location == "BATTLEFIELD" ? 3 : 2)]);
        if (location == "BATTLEFIELD") {
            var result = await new CoreRuleEngine().ResolveAsync(state, new("surfer-assignment", "P1", command.CmdType), command, default);
            Assert.True(result.Accepted, result.ErrorMessage);
        } else await Reject(state, "P1", command);
    }

    [Theory]
    [InlineData(1, true)] [InlineData(3, true)] [InlineData(5, false)]
    public async Task OtterpusReplacesConquestForTheScoringPlayersFirstTwoTurns(int turn, bool draws)
    {
        var state = WithSourceInPlay(Position("VEN·053")) with { TurnNumber = turn, CardPlays = new(turn, [], []) };
        var game = new Driver(state);
        await game.Act("P1", new MoveUnitCommand("ALLY", "BASE", "BATTLEFIELD:OTHER", []));
        await game.Act("P1", new PassFocusCommand()); await game.Act("P2", new PassFocusCommand());
        Assert.Equal(draws ? 0 : 1, game.State.PlayerScores.GetValueOrDefault("P1"));
        Assert.Equal(draws, game.State.PlayerZones["P1"].Hand.Contains("P1-DRAW-1"));
        Assert.Contains(BattlefieldTaskMarkers.ScoreGainedThisTurn("OTHER", "P1"), game.State.UntilEndOfTurnEffects);
        await game.Replay();
    }

    [Fact]
    public async Task OtterpusAlsoReplacesAnOpponentsHoldAndTheDrawCompletesBurnoutBeforeTheNormalDraw()
    {
        var state = WithSourceInPlay(Position("VEN·053")) with { TurnNumber = 1, CardPlays = new(1, [], []), RngCursor = 9 };
        var oldDeck = state.PlayerZones["P2"].MainDeck;
        state = state with { PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) { ["P2"] = state.PlayerZones["P2"] with {
            MainDeck = [], Graveyard = oldDeck } },
            ObjectLocations = state.ObjectLocations.ToDictionary(p => p.Key, p => oldDeck.Contains(p.Key) ? new ObjectLocationState("P2", "GRAVEYARD") : p.Value) };
        var game = new Driver(state);
        await game.Act("P1", new EndTurnCommand());
        Assert.Equal(0, game.State.PlayerScores.GetValueOrDefault("P2"));
        Assert.Equal(1, game.State.PlayerScores.GetValueOrDefault("P1"));
        Assert.Equal(2, game.State.PlayerZones["P2"].Hand.Count);
        Assert.Equal(10, game.State.RngCursor);
        Assert.Single(game.Events(), e => e.Event.Kind == "BATTLEFIELD_SCORE_REPLACED_BY_DRAW");
        await game.Replay();
    }

    private static MatchState Position(string no)
        => VenUnitEntryBasicTests.Position(no) with { TurnNumber = 7, CardPlays = new(7, [], []) };

    private static MatchState WithSourceInPlay(MatchState state)
        => state with { PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) { ["P1"] = state.PlayerZones["P1"] with {
            Hand = [], Base = [..state.PlayerZones["P1"].Base, "UNIT"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["UNIT"] = new("P1", "BASE") } };

    private static MatchState AddRunes(MatchState state, string player, int count, bool inDeck)
    {
        var cards = state.CardObjects.ToDictionary(); var zones = state.PlayerZones.ToDictionary(); var locations = state.ObjectLocations.ToDictionary();
        var ids = Enumerable.Range(1, count).Select(i => player + "-RUNE-" + i).ToArray();
        foreach (var id in ids) { cards[id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, "OGN·007/298", player); locations[id] = new(player, inDeck ? "RUNE_DECK" : "BASE"); }
        zones[player] = zones[player] with { Base = inDeck ? zones[player].Base : [..zones[player].Base, ..ids], RuneDeck = inDeck ? ids : zones[player].RuneDeck };
        return state with { CardObjects = cards, PlayerZones = zones, ObjectLocations = locations };
    }

    private static async Task Reject(MatchState state, string player, GameCommand command)
    {
        var before = MatchStateHasher.Hash(state); var snapshots = JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(state));
        var result = await new CoreRuleEngine().ResolveAsync(state, new("invalid-static-unit", player, command.CmdType), command, default);
        Assert.False(result.Accepted); Assert.Empty(result.Events); Assert.Equal(before, MatchStateHasher.Hash(result.State));
        Assert.Equal(snapshots, JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(result.State)));
    }
}
