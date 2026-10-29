using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class VenTemporaryEffectSpellTests
{
    public static TheoryData<string, long, int, int> Cards => new() {
        { "VEN·031", 37719, 1, 0 }, { "VEN·040", 37729, 1, 0 },
        { "VEN·116", 37816, 3, 0 }, { "VEN·126", 37826, 2, 1 }
    };

    [Theory]
    [MemberData(nameof(Cards))]
    public async Task OfficialSourcesSupplyThePrintedCostAndTheOneUnitTarget(string no, long id, int mana, int power)
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, c => c.CardNo == no);
        Assert.Equal(id, card.Id); Assert.Equal(mana, card.Energy); Assert.Equal(power, card.ReturnEnergy ?? 0);
        Assert.True(CardBehaviorRegistry.TryGetByCardNo(no, out var behavior));
        Assert.Equal(mana, behavior.ManaCost); Assert.Equal(1, behavior.RequiredTargetCount);
        Assert.Equal(power, PrintedPowerCostRules.ForCard(no).Amount);
        Assert.Equal(no is "VEN·040" or "VEN·126", behavior.CanPlayDuringPriority);
    }

    [Theory]
    [MemberData(nameof(Cards))]
    public async Task UnpayableOrNonUnitTargetsAreRejectedWithoutAnyStateChange(string no, long id, int mana, int power)
    {
        _ = id; _ = mana; _ = power;
        var state = Position(no);
        foreach (var target in new[] { "GEAR", "GRAVE", "BF", "UNIT" })
            await Reject(state, "P1", new PlayCardCommand("UNIT", no, [target]));
        await Reject(state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(0, 0) } },
            "P1", new PlayCardCommand("UNIT", no, ["ALLY"]));
    }

    [Theory]
    [InlineData("VEN·031", false)] [InlineData("VEN·031", true)]
    [InlineData("VEN·116", false)] [InlineData("VEN·116", true)]
    public async Task NormalAndFlowPlaysApplyTheTemporaryEffectAndExpireAtTurnEnd(string no, bool flow)
    {
        var state = Position(no);
        if (flow) state = state with { PlayerZones = state.PlayerZones.ToDictionary(p => p.Key, p => p.Key == "P1"
            ? p.Value with { Hand = [], Graveyard = [..p.Value.Graveyard, "UNIT"] } : p.Value),
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["UNIT"] = new("P1", "GRAVEYARD") } };
        var game = new Driver(state);
        await game.Act("P1", new PlayCardCommand("UNIT", no, ["ALLY"])); await game.Top();
        Assert.Contains("UNIT", flow ? game.State.PlayerZones["P1"].Banished : game.State.PlayerZones["P1"].Graveyard);
        Assert.Equal(no == "VEN·116" ? 17 : flow ? 18 : 19, game.State.RunePools["P1"].Mana);
        Assert.Equal(5, Might(game.State, "ALLY"));
        Assert.Equal(no == "VEN·031", game.State.CardObjects["ALLY"].UntilEndOfTurnEffects.Contains("ENEMY_SPELL_SKILL_TARGET_PROTECTION_THIS_TURN"));
        await game.Act("P1", new EndTurnCommand());
        Assert.Equal(4, Might(game.State, "ALLY")); Assert.Empty(game.State.CardObjects["ALLY"].UntilEndOfTurnEffects);
        await game.Replay();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task DragonFormReplacesBaseMightAndPreservesExistingOrLaterModifiers(bool buffFirst)
    {
        var state = AttackDamageDistributionTests.Spell(Position("VEN·116"), "DOUBLE", "VEN·081", "P1");
        var game = new Driver(state);
        if (buffFirst) { await game.Act("P1", new PlayCardCommand("DOUBLE", "VEN·081", ["ALLY"])); await game.Top(); }
        await game.Act("P1", new PlayCardCommand("UNIT", "VEN·116", ["ALLY"])); await game.Top();
        if (!buffFirst) { await game.Act("P1", new PlayCardCommand("DOUBLE", "VEN·081", ["ALLY"])); await game.Top(); }
        Assert.Equal(11, Might(game.State, "ALLY"));
        var obj = SnapshotObject(game.State, "P1", "ALLY"); Assert.Equal(5, obj.GetProperty("basePower").GetInt32());
        await game.Act("P1", new EndTurnCommand()); Assert.Equal(4, Might(game.State, "ALLY")); await game.Replay();
    }

    [Fact]
    public async Task ShroudBlocksEnemySpellsAndSkillsAndStillAllowsFriendlyTargets()
    {
        var state = AttackDamageDistributionTests.Spell(Position("VEN·031"), "FRIENDLY", "OGN·004/298", "P1");
        state = AttackDamageDistributionTests.Spell(state, "ENEMY-SPELL", "OGN·004/298", "P2");
        var game = new Driver(state);
        await game.Act("P1", new PlayCardCommand("UNIT", "VEN·031", ["ALLY"])); await game.Top();
        await game.Act("P1", new PlayCardCommand("FRIENDLY", "OGN·004/298", ["ALLY"]));
        await game.Act("P1", new PassPriorityCommand());
        await Reject(game.State, "P2", new PlayCardCommand("ENEMY-SPELL", "OGN·004/298", ["ALLY"]));
        var candidate = ResolutionResult.BuildPrompts(game.State)["P2"].Candidates?.FirstOrDefault(c => c.Action == CommandTypes.PlayCard
            && c.Sources?.Any(source => source.Id == "ENEMY-SPELL") == true);
        Assert.True(candidate is null || candidate.Targets?.All(target => target.Id != "ALLY") != false);
        await game.Act("P2", new PassPriorityCommand()); await game.Replay();
    }

    [Theory]
    [InlineData("VEN·031")] [InlineData("VEN·116")] [InlineData("VEN·126")]
    public async Task ReturningTheTargetBeforeResolutionCannotApplyTheEffectToItsNewHandGeneration(string no)
    {
        var game = new Driver(AttackDamageDistributionTests.Spell(Position(no), "RETURN", "OGN·104/298", "P1"));
        await game.Act("P1", new PlayCardCommand("UNIT", no, ["ALLY"]));
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["ALLY"])); await game.Top(); await game.Top();
        Assert.Contains("ALLY", game.State.PlayerZones["P1"].Hand); Assert.Empty(game.State.CardObjects["ALLY"].UntilEndOfTurnEffects);
        Assert.Equal(4, game.State.CardObjects["ALLY"].Power); await game.Replay();
    }

    [Fact]
    public async Task FocusMantraRespondsToARedEnemySpellAndRechecksThatThreatAtResolution()
    {
        var state = LocalDestructionRecallTests.At(Position("VEN·040"), "ALLY", "P1", "BATTLEFIELD", "BF");
        state = AttackDamageDistributionTests.Spell(state, "RAY", "OGN·009/298", "P2")
            with { ActivePlayerId = "P2", TurnPlayerId = "P2" };
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["RAY"] = OfficialGarenConquestBattlefieldCountTests.Printed("RAY", "OGN·009/298", "P2") } };
        var game = new Driver(state);
        await game.Act("P2", new PlayCardCommand("RAY", "OGN·009/298", ["ALLY"]));
        await game.Act("P2", new PassPriorityCommand());
        var quote = new CoreRuleEngine().PreviewPlayCard(game.State, "P1", PlayCostPreviewTests.Request(game.State,
            new PlayCardCommand("UNIT", "VEN·040", ["ALLY"])));
        Assert.True(quote.CanPay, quote.Message + "\n" + JsonSerializer.Serialize(game.State.StackItems));
        await game.Act("P1", new PlayCardCommand("UNIT", "VEN·040", ["ALLY"])); await game.Top();
        Assert.Equal(8, Might(game.State, "ALLY")); await game.Top();
        Assert.Equal(3, game.State.CardObjects["ALLY"].Damage); await game.Replay();
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task FocusMantraAllowsCombatAgainstARedEnemyAndRejectsOtherTraits(bool red)
    {
        var state = LocalDestructionRecallTests.At(Position("VEN·040"), "ENEMY", "P2", "BATTLEFIELD", "OTHER");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["ENEMY"] = OfficialGarenConquestBattlefieldCountTests.Printed("ENEMY", red ? "OGN·001/298" : "OGN·219/298", "P2") } };
        var game = new Driver(state);
        await game.Act("P1", new MoveUnitCommand("ALLY", "BASE", "BATTLEFIELD:OTHER", []));
        Assert.True(game.State.CardObjects["ALLY"].IsAttacking);
        var command = new PlayCardCommand("UNIT", "VEN·040", ["ALLY"]);
        var quote = new CoreRuleEngine().PreviewPlayCard(game.State, "P1", PlayCostPreviewTests.Request(game.State, command));
        Assert.Equal(red, quote.CanPay);
        if (red) { await game.Act("P1", command); await game.Top(); Assert.Equal(8, Might(game.State, "ALLY")); }
        else await Reject(game.State, "P1", command);
        await game.Replay();
    }

    [Theory]
    [InlineData(1, 8, 1)] [InlineData(3, 3, 2)]
    public async Task ASevenPointShieldSpendsOneFiniteBudgetAcrossSmallOrLargeHits(int hits, int damage, int expectedDamage)
    {
        var state = LocalDestructionRecallTests.At(Position("VEN·126"), "ALLY", "P1", "BATTLEFIELD", "BF");
        for (var i = 0; i < hits; i++) state = AttackDamageDistributionTests.Spell(state, "HIT" + i, damage == 8 ? "OGS·022/024" : "OGN·009/298", "P1");
        var game = new Driver(state); await game.Act("P1", new PlayCardCommand("UNIT", "VEN·126", ["ALLY"])); await game.Top();
        for (var i = 0; i < hits; i++) { await game.Act("P1", new PlayCardCommand("HIT" + i, damage == 8 ? "OGS·022/024" : "OGN·009/298", ["ALLY"])); await game.Top(); }
        Assert.Equal(expectedDamage, game.State.CardObjects["ALLY"].Damage);
        Assert.DoesNotContain(game.State.CardObjects["ALLY"].UntilEndOfTurnEffects, e => e.StartsWith("DAMAGE_PREVENTION_THIS_TURN:"));
        Assert.Equal(7, game.Events().Where(e => e.Event.Kind == "DAMAGE_APPLIED").Sum(e => Convert.ToInt32(e.Event.Payload.GetValueOrDefault("preventedDamage"))));
        await game.Replay();
    }

    [Theory]
    [InlineData("DOUBLE", 1, 0)] [InlineData("FINITE", 0, 3)]
    public async Task TheAffectedPlayerChoosesWhetherDoublingOrFinitePreventionAppliesFirst(string first, int expectedDamage, int remaining)
    {
        var state = LocalDestructionRecallTests.At(Position("VEN·126"), "ALLY", "P1", "BATTLEFIELD", "BF");
        state = AttackDamageDistributionTests.Spell(state, "LOTUS", "UNL-013/219", "P1");
        state = AttackDamageDistributionTests.Spell(state, "HIT", "VEN·059", "P1");
        var game = new Driver(state);
        await game.Act("P1", new PlayCardCommand("UNIT", "VEN·126", ["ALLY"])); await game.Top();
        await game.Act("P1", new PlayCardCommand("LOTUS", "UNL-013/219", ["ALLY"])); await game.Top();
        await game.Act("P1", new PlayCardCommand("HIT", "VEN·059", ["ALLY"])); await game.Top();
        var choice = Assert.IsType<RuleChoiceContinuation>(game.State.PendingRuleChoice);
        Assert.Equal("P1", choice.Request.PlayerId); Assert.Equal("DAMAGE_REPLACEMENT_ORDER", choice.Request.Kind);
        var option = first == "DOUBLE" ? "DOUBLE" : choice.Request.Options.Single(o => o.Id.StartsWith("DAMAGE_PREVENTION_THIS_TURN:")).Id;
        await game.Act("P1", new PayCostCommand(choice.Request.Id, "RULE_REPLACEMENT", [option]));
        Assert.Equal(expectedDamage, game.State.CardObjects["ALLY"].Damage);
        Assert.Equal(remaining > 0, game.State.CardObjects["ALLY"].UntilEndOfTurnEffects.Any(e => e.EndsWith(":" + remaining) && e.StartsWith("DAMAGE_PREVENTION_THIS_TURN:")));
        await game.Replay();
    }

    [Fact]
    public async Task CounteringTheThreatBeforeFocusResolvesRemovesItsTargetEligibility()
    {
        var state = LocalDestructionRecallTests.At(Position("VEN·040"), "ALLY", "P1", "BATTLEFIELD", "BF");
        state = AttackDamageDistributionTests.Spell(state, "RAY", "OGN·009/298", "P2");
        state = AttackDamageDistributionTests.Spell(state, "COUNTER", "OGN·045/298", "P1");
        state = state with { ActivePlayerId = "P2", TurnPlayerId = "P2",
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["RAY"] = OfficialGarenConquestBattlefieldCountTests.Printed("RAY", "OGN·009/298", "P2") } };
        var game = new Driver(state);
        await game.Act("P2", new PlayCardCommand("RAY", "OGN·009/298", ["ALLY"]));
        var ray = Assert.Single(game.State.StackItems).StackItemId;
        await game.Act("P2", new PassPriorityCommand());
        await game.Act("P1", new PlayCardCommand("UNIT", "VEN·040", ["ALLY"]));
        await game.Act("P1", new PlayCardCommand("COUNTER", "OGN·045/298", [ray])); await game.Top(); await game.Top();
        Assert.Equal(4, Might(game.State, "ALLY")); Assert.Equal(0, game.State.CardObjects["ALLY"].Damage); await game.Replay();
    }

    [Fact]
    public async Task TwoShieldGrantsRetainTheirIndependentBudgetsAndBothExpireAtTurnEnd()
    {
        var state = AttackDamageDistributionTests.Spell(Position("VEN·126"), "SECOND", "VEN·126", "P1");
        state = AttackDamageDistributionTests.Spell(state, "HIT", "OGS·022/024", "P1");
        var game = new Driver(state);
        foreach (var id in new[] { "UNIT", "SECOND" }) { await game.Act("P1", new PlayCardCommand(id, "VEN·126", ["ALLY"])); await game.Top(); }
        await game.Act("P1", new PlayCardCommand("HIT", "OGS·022/024", ["ALLY"])); await game.Top();
        var choice = Assert.IsType<RuleChoiceContinuation>(game.State.PendingRuleChoice);
        Assert.Equal(2, choice.Request.Options.Count);
        await game.Act("P1", new PayCostCommand(choice.Request.Id, "RULE_REPLACEMENT", [choice.Request.Options[0].Id]));
        Assert.Equal(0, game.State.CardObjects["ALLY"].Damage);
        Assert.Contains(game.State.CardObjects["ALLY"].UntilEndOfTurnEffects, e => e.StartsWith("DAMAGE_PREVENTION_THIS_TURN:") && e.EndsWith(":6"));
        await game.Act("P1", new EndTurnCommand()); Assert.Empty(game.State.CardObjects["ALLY"].UntilEndOfTurnEffects);
        await game.Replay();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task CombatPromptsAndBothManualAssignmentsUseTheUnspentShieldBudget(bool spentThree)
    {
        var state = LocalDestructionRecallTests.At(Position("VEN·126"), "ENEMY", "P2", "BATTLEFIELD", "OTHER");
        state = AttackDamageDistributionTests.Spell(state, "RAY", "OGN·009/298", "P1");
        var cards = state.CardObjects.ToDictionary(); var zones = state.PlayerZones.ToDictionary(); var locations = state.ObjectLocations.ToDictionary();
        foreach (var id in new[] { "A2", "A3", "ENEMY2" }) {
            var player = id == "ENEMY2" ? "P2" : "P1";
            cards[id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, "OGN·219/298", player);
            locations[id] = new(player, player == "P1" ? "BASE" : "BATTLEFIELD", player == "P2" ? "OTHER" : null);
            zones[player] = player == "P1" ? zones[player] with { Base = [..zones[player].Base, id] }
                : zones[player] with { Battlefields = [..zones[player].Battlefields, id] };
        }
        var game = new Driver(state with { CardObjects = cards, PlayerZones = zones, ObjectLocations = locations });
        await game.Act("P1", new PlayCardCommand("UNIT", "VEN·126", ["ENEMY"])); await game.Top();
        if (spentThree) { await game.Act("P1", new PlayCardCommand("RAY", "OGN·009/298", ["ENEMY"])); await game.Top(); }
        await game.Act("P1", new MoveUnitCommand("ALLY", "BASE", "BATTLEFIELD:OTHER", [], ["ALLY", "A2", "A3"]));
        await game.Act(game.State.FocusPlayerId!, new PassFocusCommand()); await game.Act(game.State.FocusPlayerId!, new PassFocusCommand());
        await game.Act("P1", new DeclareBattleCommand("OTHER", ["ALLY", "A2", "A3"], ["ENEMY", "ENEMY2"], ["COMBAT_ASSIGNMENT"]));
        var meta = ResolutionResult.BuildPrompts(game.State)["P1"].View!.Metadata!;
        var thresholds = Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(meta["lethalDamageThreshold"]);
        Assert.Equal(spentThree ? 8 : 11, thresholds["ENEMY"]);
        await Reject(game.State, "P1", new AssignCombatDamageCommand("battle:OTHER", "OTHER",
            [new("ALLY", "ENEMY", 4), new("A2", "ENEMY2", 4), new("A3", "ENEMY2", 4)]));
        var assignments = spentThree ? new CombatDamageAssignmentDto[] { new("ALLY", "ENEMY", 4), new("A2", "ENEMY", 4), new("A3", "ENEMY2", 4) }
            : [new("ALLY", "ENEMY", 4), new("A2", "ENEMY", 4), new("A3", "ENEMY", 3), new("A3", "ENEMY2", 1)];
        await game.Act("P1", new AssignCombatDamageCommand("battle:OTHER", "OTHER", assignments));
        await game.Act("P2", new AssignCombatDamageCommand("battle:OTHER", "OTHER", [new("ENEMY", "ALLY", 4), new("ENEMY2", "A2", 4)]));
        Assert.Contains("ENEMY", game.State.PlayerZones["P2"].Graveyard); await game.Replay();
    }

    [Theory]
    [InlineData("VEN·116", false)] [InlineData("VEN·116", true)]
    [InlineData("VEN·126", false)] [InlineData("VEN·126", true)]
    public async Task RecoveryRejectsChangedOrDeletedNumericEffectsEvenWhenAllViewsAreRebuilt(string no, bool remove)
    {
        var game = new Driver(Position(no));
        await game.Act("P1", new PlayCardCommand("UNIT", no, ["ALLY"])); await game.Top();
        var card = game.State.CardObjects["ALLY"];
        var state = game.State with { CardObjects = new Dictionary<string, CardObjectState>(game.State.CardObjects) {
            ["ALLY"] = card with { UntilEndOfTurnEffects = remove ? [] : card.UntilEndOfTurnEffects.Select(e => e[..^1] + "9").ToArray() } } };
        var seq = game.Events()[^1].Sequence; var snapshots = ResolutionResult.BuildSnapshots(state); var prompts = ResolutionResult.BuildPrompts(state);
        var views = state.Seats.Keys.ToDictionary(p => p, p => new RecoveredPlayerView(p, state.Tick, seq, snapshots[p], state.Tick, seq, prompts[p]));
        var spectator = MatchReplayRedactor.BuildSpectatorFrame(state.RoomId, state.Tick, seq, [], state);
        var errors = MatchRecoveryValidator.Validate(state.RoomId, seq, game.Commands(), game.Events(), views, state, state.Tick, spectator);
        Assert.Contains(errors, e => e.Contains(no == "VEN·116" ? "base Might replacement" : "finite damage prevention budget"));
    }

    private static MatchState Position(string no) => VenUnitEntryBasicTests.Position(no);
    private static JsonElement SnapshotObject(MatchState state, string player, string id)
        => JsonSerializer.SerializeToElement(ResolutionResult.BuildSnapshots(state)[player], new JsonSerializerOptions(JsonSerializerDefaults.Web)).GetProperty("players")
            .GetProperty(player).GetProperty("objects").GetProperty(id);
    private static int Might(MatchState state, string id)
        => SnapshotObject(state, state.CardObjects[id].ControllerId!, id).GetProperty("effectivePower").GetInt32();
    private static async Task Reject(MatchState state, string player, GameCommand command)
    {
        var before = MatchStateHasher.Hash(state); var views = JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(state));
        var result = await new CoreRuleEngine().ResolveAsync(state, new("invalid-temporary-effect", player, command.CmdType), command, default);
        Assert.False(result.Accepted); Assert.Empty(result.Events); Assert.Equal(before, MatchStateHasher.Hash(result.State));
        Assert.Equal(views, JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(result.State)));
    }
}
