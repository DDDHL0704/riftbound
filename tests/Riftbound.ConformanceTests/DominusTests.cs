using System.Text;
using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;

namespace Riftbound.ConformanceTests;

public sealed class DominusTests
{
    private const string Card = "VEN·142";
    private const string MarkerPrefix = "GRANTED_ACTIVATION|";
    private static PlayCardCommand Play(string target = "HOST") => new("SPELL", Card, [target]);

    private static MatchState Position()
    {
        var cards = new Dictionary<string, CardObjectState>();
        var zones = new Dictionary<string, PlayerZones>();
        var locations = new Dictionary<string, ObjectLocationState>();
        void Put(string id, string card, string player, string zone) {
            cards[id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, card, player);
            locations[id] = new(player, zone);
        }
        Put("HOST", "OGN·001/298", "P1", "BASE");
        Put("ENEMY", "OGN·001/298", "P2", "BASE");
        Put("SPELL", Card, "P1", "HAND");
        foreach (var player in new[] { "P1", "P2" }) {
            var deck = Enumerable.Range(1, 3).Select(i => player + "-DRAW-" + i).ToArray();
            foreach (var id in deck) Put(id, "OGN·001/298", player, "MAIN_DECK");
            zones[player] = PlayerZones.Empty with { Base = [player == "P1" ? "HOST" : "ENEMY"],
                Hand = player == "P1" ? ["SPELL"] : [], MainDeck = deck };
        }
        cards["HOST"] = cards["HOST"] with { Power = 4, IsExhausted = true };
        return new MatchState("DOMINUS", 0, 5, "P1", new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" },
            status: MatchStatuses.InProgress, readyPlayerIds: ["P1", "P2"], turnPlayerId: "P1", phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen, playerZones: zones, cardObjects: cards, objectLocations: locations,
            runePools: new Dictionary<string, RunePool> { ["P1"] = new(20, 20), ["P2"] = new(20, 20) }, seed: 424242)
            with { CardPlays = new(5, [], []) };
    }

    private static string Ability(MatchState state, string unit = "HOST")
        => CurrentPowerTargetTests.Snapshot(state)
            .GetProperty("players").GetProperty(state.CardObjects[unit].ControllerId!).GetProperty("objects").GetProperty(unit)
            .GetProperty("untilEndOfTurnEffects").EnumerateArray().Select(e => e.GetString()!)
            .First(e => e.StartsWith("GRANTED:", StringComparison.Ordinal));

    private static async Task<MatchState> Granted(string unit = "HOST")
    {
        var played = await Act(Position(), "P1", Play(unit));
        return (await Top(played.State)).State;
    }

    private static MatchState ToHand(MatchState state, string id, string player)
    {
        var zones = state.PlayerZones[player];
        return state with { PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
            [player] = zones with { Base = zones.Base.Where(card => card != id).ToArray(),
                Battlefields = zones.Battlefields.Where(card => card != id).ToArray(),
                Graveyard = zones.Graveyard.Where(card => card != id).ToArray(),
                Hand = zones.Hand.Where(card => card != id).Append(id).ToArray() } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { [id] = new(player, "HAND") } };
    }

    [Fact]
    public async Task OfficialPrintingIsAdmittedWithSwiftSpeedAndThePrintedFourManaCost()
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, c => c.CardNo == Card);
        Assert.Equal(37845, card.Id); Assert.Equal(4, card.Energy); Assert.Equal(0, card.ReturnEnergy ?? 0);
        Assert.Contains("战力翻倍", card.CardEffect); Assert.Contains("让我变为活跃状态", card.CardEffect);
        Assert.True(CardBehaviorRegistry.TryGetByCardNo(Card, out var behavior));
        Assert.Equal(4, behavior.ManaCost); Assert.True(behavior.CanPlayDuringSpellDuel);
    }

    [Fact]
    public async Task RealPlayDoublesOnceAndRepeatablePaidActivationResolvesAfterBothPlayersRespond()
    {
        var driver = new VenBasicFlowSpellTests.Driver(Position());
        await driver.Act("P1", Play());
        Assert.Equal(16, driver.State.RunePools["P1"].Mana);
        Assert.Equal(4, driver.State.CardObjects["HOST"].Power);
        await driver.Top();
        Assert.Equal(8, driver.State.CardObjects["HOST"].Power);
        Assert.True(driver.State.CardObjects["HOST"].IsExhausted);
        var ability = Ability(driver.State);
        Assert.Contains(driver.PromptFor("P1").Candidates!, c => c.Action == CommandTypes.ActivateAbility);
        await driver.Act("P1", new ActivateAbilityCommand("HOST", ability, []));
        Assert.Equal(18, driver.State.RunePools["P1"].Power);
        Assert.True(driver.State.CardObjects["HOST"].IsExhausted);
        Assert.NotNull(Assert.Single(driver.State.StackItems).TemporaryActivation);
        await driver.Top();
        Assert.False(driver.State.CardObjects["HOST"].IsExhausted);
        await driver.Act("P1", new ActivateAbilityCommand("HOST", ability, []));
        await driver.Top();
        Assert.Equal(16, driver.State.RunePools["P1"].Power);
        Assert.Equal(8, driver.State.CardObjects["HOST"].Power);
        Assert.Single(driver.State.CardObjects["HOST"].UntilEndOfTurnEffects, e => e.StartsWith(MarkerPrefix));
        await driver.Act("P1", new EndTurnCommand());
        Assert.Equal(4, driver.State.CardObjects["HOST"].Power);
        Assert.DoesNotContain(driver.State.CardObjects["HOST"].UntilEndOfTurnEffects, e => e.StartsWith(MarkerPrefix));
    }

    [Theory]
    [InlineData("source")] [InlineData("id")] [InlineData("target")] [InlineData("extra-cost")]
    [InlineData("no-power")] [InlineData("restricted-power")] [InlineData("controller")]
    [InlineData("generation")] [InlineData("hidden")] [InlineData("closed")]
    public async Task IllegalActivationIsRejectedWithoutChangingStateViewsOrPrompts(string branch)
    {
        var state = await Granted();
        var command = new ActivateAbilityCommand("HOST", Ability(state), []);
        if (branch == "source") command = command with { SourceObjectId = "ENEMY" };
        if (branch == "id") command = command with { AbilityId = "GRANTED:FORGED" };
        if (branch == "target") command = command with { TargetObjectIds = ["ENEMY"] };
        if (branch == "extra-cost") command = command with { OptionalCosts = ["UNRECOGNIZED"] };
        if (branch is "no-power" or "restricted-power") state = state with {
            RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(20, 0) {
                UnitOrEquipmentPlayOnlyPower = branch == "restricted-power" ? 20 : 0 } } };
        if (branch is "controller" or "generation" or "hidden") state = state with {
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["HOST"] = state.CardObjects["HOST"] with {
                    ControllerId = branch == "controller" ? "P2" : "P1",
                    ObjectGeneration = branch == "generation" ? 1 : state.CardObjects["HOST"].ObjectGeneration,
                    IsFaceDown = branch == "hidden" } } };
        if (branch == "closed") state = state with { TimingState = TimingStates.NeutralClosed };
        await Reject(state, "P1", command);
    }

    [Theory]
    [InlineData("missing")] [InlineData("equipment")] [InlineData("hidden")] [InlineData("no-mana")]
    public async Task InvalidSpellTargetOrPaymentIsRejectedWithoutMutation(string branch)
    {
        var state = Position(); var command = Play(branch == "missing" ? "MISSING" : "HOST");
        if (branch is "equipment" or "hidden") state = state with {
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["HOST"] = state.CardObjects["HOST"] with { IsFaceDown = branch == "hidden",
                    Tags = branch == "equipment" ? [CardObjectTags.EquipmentCard] : [CardObjectTags.UnitCard] } } };
        if (branch == "no-mana") state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(3, 20) } };
        await Reject(state, "P1", command);
    }

    [Fact]
    public async Task EnemyUnitReceivesTheAbilityButOnlyItsCurrentControllerCanActivateIt()
    {
        var state = await Granted("ENEMY"); var ability = Ability(state, "ENEMY");
        await Reject(state, "P1", new ActivateAbilityCommand("ENEMY", ability, []));
        var ended = await Act(state, "P1", new EndTurnCommand());
        Assert.DoesNotContain(ended.State.CardObjects["ENEMY"].UntilEndOfTurnEffects, e => e.StartsWith(MarkerPrefix));
        ArmamentMightTests.PublicRecovery(ended.State);
        // Grant lasts only the spell's turn, so exercise control in that same turn.
        state = state with { ActivePlayerId = "P2", TurnPlayerId = "P2" };
        var activated = await Act(state, "P2", new ActivateAbilityCommand("ENEMY", ability, []));
        Assert.Equal(18, activated.State.RunePools["P2"].Power);
        ArmamentMightTests.PublicRecovery((await Top(activated.State)).State);
    }

    [Fact]
    public async Task RecycledRunesPayTwoGenericPowerAndTheNoTargetSelfEffectDoesNotPayWard()
    {
        var state = EmpowerTests.Runes(await Granted(), 2);
        state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(0, 0) },
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["HOST"] = state.CardObjects["HOST"] with { Tags = [CardObjectTags.UnitCard, CardObjectTags.Spellshield] } } };
        var activated = await Act(state, "P1", new ActivateAbilityCommand("HOST", Ability(state), [],
            ["RECYCLE_RUNE:EMPOWER-RUNE-0", "RECYCLE_RUNE:EMPOWER-RUNE-1"]));
        Assert.Equal(0, activated.State.RunePools["P1"].TotalPower);
        Assert.Contains("EMPOWER-RUNE-0", activated.State.PlayerZones["P1"].RuneDeck);
        Assert.Contains("EMPOWER-RUNE-1", activated.State.PlayerZones["P1"].RuneDeck);
        var resolved = await Top(activated.State);
        Assert.False(resolved.State.CardObjects["HOST"].IsExhausted);
        ArmamentMightTests.PublicRecovery(resolved.State);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task QueuedActivationKeepsItsCapturedIdentityWhenTheUnitLeaves(bool reentered)
    {
        var state = await Granted();
        state = (await Act(state, "P1", new ActivateAbilityCommand("HOST", Ability(state), []))).State;
        state = reentered ? state : ToHand(state, "HOST", "P1");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["HOST"] = state.CardObjects["HOST"] with { ObjectGeneration = state.CardObjects["HOST"].ObjectGeneration + 1,
                UntilEndOfTurnEffects = [], UntilEndOfTurnPowerModifier = 0, UntilEndOfTurnPowerModifiers = [],
                Power = 4, IsExhausted = true } } };
        ArmamentMightTests.PublicRecovery(state);
        var resolved = await Top(state);
        Assert.True(resolved.State.CardObjects["HOST"].IsExhausted);
        Assert.DoesNotContain(resolved.Events, e => e.Kind == "UNIT_READIED");
        ArmamentMightTests.PublicRecovery(resolved.State);
    }

    [Theory]
    [InlineData("generation")] [InlineData("turn")] [InlineData("spell")] [InlineData("stack")] [InlineData("encoding")] [InlineData("duplicate")]
    public async Task RecoveryRejectsForgedOrDuplicatedTemporaryGrant(string branch)
    {
        var state = await Granted(); var unit = state.CardObjects["HOST"];
        var marker = Assert.Single(unit.UntilEndOfTurnEffects, e => e.StartsWith(MarkerPrefix));
        var grant = JsonSerializer.Deserialize<TemporaryActivatedAbilityGrant>(Encoding.UTF8.GetString(Convert.FromBase64String(marker[MarkerPrefix.Length..])))!;
        grant = branch switch {
            "generation" => grant with { Unit = grant.Unit with { Generation = grant.Unit.Generation + 1 } },
            "turn" => grant with { TurnNumber = grant.TurnNumber + 1 },
            "spell" => grant with { CardNo = "OGN·001/298" },
            "stack" => grant with { CreatingStackItemId = "FORGED" }, _ => grant };
        var forged = branch == "encoding" ? MarkerPrefix + "invalid" : MarkerPrefix + Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(grant)));
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["HOST"] = unit with { UntilEndOfTurnEffects = branch == "duplicate" ? [marker, marker] : [forged] } } };
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(state), e => e.Contains("temporary activated ability grant"));
    }

    [Fact]
    public async Task PublicViewsExposeOnlyStableAbilityIdentityAndNeverThePrivateGrantReceipt()
    {
        var state = await Granted();
        state = ToHand(state, "SPELL", "P1");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["SPELL"] = state.CardObjects["SPELL"] with { ObjectGeneration = state.CardObjects["SPELL"].ObjectGeneration + 1 } } };
        state = (await Act(state, "P1", new ActivateAbilityCommand("HOST", Ability(state), []))).State;
        var view = JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(state)["P2"]);
        Assert.DoesNotContain(MarkerPrefix, view); Assert.DoesNotContain("CreatingStackItemId", view);
        Assert.DoesNotContain("temporaryActivation", view); Assert.DoesNotContain("\"SPELL\"", view);
        Assert.Contains("grantedAbilityId", view);
        ArmamentMightTests.PublicRecovery(state);
        var snapshots = ResolutionResult.BuildSnapshots(state); var prompts = ResolutionResult.BuildPrompts(state);
        var recovered = state.Seats.Keys.ToDictionary(id => id, id => new RecoveredPlayerView(id, state.Tick, 0, snapshots[id], state.Tick, 0, prompts[id]));
        var spectator = MatchReplayRedactor.BuildSpectatorFrame(state.RoomId, state.Tick, 0, [], state);
        foreach (var snapshot in snapshots.Values.Append(spectator.SpectatorSnapshot)) {
            var effects = Assert.IsAssignableFrom<IEnumerable<Dictionary<string, object?>>>(snapshot.Timing["continuousEffects"]);
            var modifier = Assert.Single(effects, effect => Equals(effect.GetValueOrDefault("sourceObjectId"), "HIDDEN")
                && Equals(effect.GetValueOrDefault("layer"), ContinuousEffectLayers.PowerModifier));
            modifier["sourceObjectId"] = "HOST";
            Assert.NotEmpty(MatchRecoveryValidator.ValidateComponentStateAndViews(state, recovered, spectator));
            modifier["sourceObjectId"] = "HIDDEN";
        }
    }

    [Fact]
    public async Task CounteredSpellPaysItsCostButNeverDoublesOrGrantsTheActivation()
    {
        var initial = AttackDamageDistributionTests.Spell(Position(), "COUNTER", "OGN·045/298", "P2");
        var driver = new VenBasicFlowSpellTests.Driver(initial);
        await driver.Act("P1", Play());
        var spell = Assert.Single(driver.State.StackItems).StackItemId;
        await driver.Act("P1", new PassPriorityCommand());
        await driver.Act("P2", new PlayCardCommand("COUNTER", "OGN·045/298", [spell]));
        await driver.Top();
        Assert.Equal(16, driver.State.RunePools["P1"].Mana);
        Assert.Equal(4, driver.State.CardObjects["HOST"].Power);
        Assert.Empty(driver.State.CardObjects["HOST"].UntilEndOfTurnEffects);
        Assert.Contains("SPELL", driver.State.PlayerZones["P1"].Graveyard);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task RealReturnToHandResponsePreventsGrantOrQueuedSelfReady(bool activated)
    {
        var initial = OfficialCopyIdentityTests.AddSpell(Position(), "RETURN", "OGN·104/298");
        var driver = new VenBasicFlowSpellTests.Driver(initial);
        await driver.Act("P1", Play());
        if (activated) {
            await driver.Top();
            await driver.Act("P1", new ActivateAbilityCommand("HOST", Ability(driver.State), []));
        }
        await driver.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["HOST"]));
        await driver.Top();
        Assert.Contains("HOST", driver.State.PlayerZones["P1"].Hand);
        await driver.Top();
        Assert.Empty(driver.State.CardObjects["HOST"].UntilEndOfTurnEffects);
        Assert.Equal(0, driver.State.CardObjects["HOST"].UntilEndOfTurnPowerModifier);
        Assert.DoesNotContain(driver.Events(), e => e.Event.Kind == "UNIT_READIED" && Equals(e.Event.Payload.GetValueOrDefault("targetObjectId"), "HOST"));
    }

    [Fact]
    public async Task TwoActualCastsDoubleCurrentMightAndGrantIndependentAbilities()
    {
        var driver = new VenBasicFlowSpellTests.Driver(OfficialCopyIdentityTests.AddSpell(Position(), "SECOND", Card));
        await driver.Act("P1", Play()); await driver.Top();
        await driver.Act("P1", new PlayCardCommand("SECOND", Card, ["HOST"])); await driver.Top();
        Assert.Equal(16, driver.State.CardObjects["HOST"].Power);
        var effects = driver.State.CardObjects["HOST"].UntilEndOfTurnEffects.Where(e => e.StartsWith(MarkerPrefix)).ToArray();
        Assert.Equal(2, effects.Length); Assert.NotEqual(effects[0], effects[1]);
        await driver.Act("P1", new ActivateAbilityCommand("HOST", Ability(driver.State), [])); await driver.Top();
        Assert.Equal(16, driver.State.CardObjects["HOST"].Power);
        await driver.Act("P1", new EndTurnCommand()); Assert.Equal(4, driver.State.CardObjects["HOST"].Power);
    }

    [Fact]
    public async Task SpellOnlyCounterCannotSelectTheGrantedActivatedAbility()
    {
        var state = AttackDamageDistributionTests.Spell(await Granted(), "COUNTER", "OGN·045/298", "P2");
        state = (await Act(state, "P1", new ActivateAbilityCommand("HOST", Ability(state), []))).State;
        var skill = Assert.Single(state.StackItems).StackItemId;
        state = (await Act(state, "P1", new PassPriorityCommand())).State;
        await Reject(state, "P2", new PlayCardCommand("COUNTER", "OGN·045/298", [skill]));
    }

    private static async Task Reject(MatchState state, string player, GameCommand command)
    {
        var hash = MatchStateHasher.Hash(state); var snapshots = JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(state));
        var prompts = JsonSerializer.Serialize(ResolutionResult.BuildPrompts(state));
        var result = await new CoreRuleEngine().ResolveAsync(state, new("illegal-dominus", player, command.CmdType), command, default);
        Assert.False(result.Accepted); Assert.Empty(result.Events);
        Assert.Equal(hash, MatchStateHasher.Hash(result.State));
        Assert.Equal(snapshots, JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(result.State)));
        Assert.Equal(prompts, JsonSerializer.Serialize(ResolutionResult.BuildPrompts(result.State)));
    }
}
