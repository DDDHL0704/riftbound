using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class LeeSinPenitentBoonTests
{
    private const string Lee = "OGN·078/298";

    private static MatchState Position(string no = Lee, int boons = 0)
    {
        var state = VenUnitEntryBasicTests.Position(no);
        var unit = state.CardObjects["UNIT"];
        return state with {
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P1"] = state.PlayerZones["P1"] with { Hand = [], Base = [..state.PlayerZones["P1"].Base, "UNIT"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["UNIT"] = new("P1", "BASE") },
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["UNIT"] = unit with { Power = unit.Power + boons, BoonCount = boons,
                    Tags = unit.Tags.Concat(boons > 0 ? [CardObjectTags.Boon] : []).Distinct().Order().ToArray() } } };
    }

    private static P4ActivatedAbilityDefinition Ability(string no = Lee)
        => Assert.Single(P4ActivatedAbilityCatalog.GetAll(), a => a.SourceCardNo == no && a.Kind == ActivatedAbilityKinds.GrantSelfBoon);
    private static ActivateAbilityCommand Activate(string no = Lee) => new("UNIT", Ability(no).AbilityId, []);
    private static MatchState Spell(MatchState state, string id, string no)
        => AttackDamageDistributionTests.Spell(state, id, no, "P1");
    private static JsonElement UnitView(MatchState state, string viewer = "P1", string id = "UNIT")
        => JsonSerializer.SerializeToElement(viewer == ProjectionViewers.Spectator
                ? ResolutionResult.BuildSpectatorSnapshot(state) : ResolutionResult.BuildSnapshots(state)[viewer],
                new JsonSerializerOptions(JsonSerializerDefaults.Web))
            .GetProperty("players").GetProperty("P1").GetProperty("objects").GetProperty(id);
    private static bool PromptOffers(MatchState state, string no = Lee)
        => (ResolutionResult.BuildPrompts(state)["P1"].Candidates ?? []).Where(c => c.Action == CommandTypes.ActivateAbility && c.Enabled)
            .Any(c => c.Sources?.Any(s => s.Id == "UNIT") == true
                && JsonSerializer.Serialize(c.Metadata).Contains(Ability(no).AbilityId, StringComparison.Ordinal));
    private static void AssertBoons(MatchState state, int count, int power)
    {
        Assert.Equal(count, state.CardObjects["UNIT"].BoonCount);
        Assert.Equal(power, state.CardObjects["UNIT"].Power);
        Assert.Equal(count > 0, state.CardObjects["UNIT"].Tags.Contains(CardObjectTags.Boon));
        foreach (var viewer in new[] { "P1", "P2", ProjectionViewers.Spectator })
            Assert.Equal(count, UnitView(state, viewer).GetProperty("boonCount").GetInt32());
        ArmamentMightTests.PublicRecovery(state);
    }

    [Theory]
    [InlineData(Lee)] [InlineData("OGN·078a/298")]
    public async Task PrintedSkillOnlyExhaustsItselfAndWaitsForResponsesWithNoResourceCost(string no)
    {
        var official = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, c => c.CardNo == no);
        var parsed = Assert.Single(ActivatedAbilityParser.Parse(official.CardEffect), a => a.Kind == ActivatedAbilityKinds.GrantSelfBoon);
        Assert.True(parsed.ExhaustsSourceAsCost); Assert.Equal(0, parsed.RequiredTargetCount);
        Assert.Equal(0, parsed.ManaCost); Assert.Equal(0, parsed.PowerCost);
        var initial = Position(no) with { RunePools = new Dictionary<string, RunePool> { ["P1"] = RunePool.Empty, ["P2"] = RunePool.Empty } };
        var game = new Driver(initial); Assert.True(PromptOffers(game.State, no));
        await game.Act("P1", Activate(no));
        Assert.True(game.State.CardObjects["UNIT"].IsExhausted);
        Assert.False(game.State.CardObjects["ALLY"].IsExhausted);
        Assert.Empty(Assert.Single(game.State.StackItems).TargetObjectIds);
        Assert.NotNull(Assert.Single(game.State.StackItems).SimpleActivation);
        Assert.Equal(initial.RunePools["P1"], game.State.RunePools["P1"]);
        AssertBoons(game.State, 0, 5);
        await game.Act(game.State.PriorityPlayerId!, new PassPriorityCommand()); AssertBoons(game.State, 0, 5);
        await game.Act(game.State.PriorityPlayerId!, new PassPriorityCommand()); AssertBoons(game.State, 1, 6);
        Assert.False(PromptOffers(game.State, no));
        await Reject(game.State, "P1", Activate(no));
        await game.Replay();
    }

    [Theory]
    [InlineData(Lee)] [InlineData("OGN·078a/298")]
    public async Task RealReadySpellsPermitRepeatedActivationsAndTheCountersSurviveTurnEndAndColdRecovery(string no)
    {
        var initial = Spell(Spell(Position(no), "READY1", "UNL-009/219"), "READY2", "UNL-009/219");
        var game = new Driver(initial);
        for (var i = 1; i <= 3; i++)
        {
            await game.Act("P1", Activate(no)); await game.Top(); AssertBoons(game.State, i, 5 + i);
            if (i < 3)
            {
                await game.Act("P1", new PlayCardCommand("READY" + i, "UNL-009/219", ["UNIT"])); await game.Top();
                Assert.False(game.State.CardObjects["UNIT"].IsExhausted); Assert.True(PromptOffers(game.State, no));
            }
        }
        Assert.Equal(3, game.Events().Count(e => e.Event.Kind == "BOON_GRANTED"));
        Assert.Single(game.Events(), e => e.Event.Kind == "OBJECT_TAG_ADDED"
            && Equals(e.Event.Payload.GetValueOrDefault("tag"), CardObjectTags.Boon));
        await game.Act("P1", new EndTurnCommand()); AssertBoons(game.State, 3, 8);
        await game.ColdCheckpoint(); await game.Replay();
    }

    [Theory]
    [InlineData("exhausted")] [InlineData("opponent")] [InlineData("target")]
    [InlineData("extra-cost")] [InlineData("closed")] [InlineData("duel-open")] [InlineData("duel-closed")]
    public async Task IllegalCostsSourcesAndTimingAreRejectedWithoutChangingState(string branch)
    {
        var state = Position(); var command = Activate(); var player = "P1";
        if (branch == "exhausted") state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["UNIT"] = state.CardObjects["UNIT"] with { IsExhausted = true } } };
        if (branch == "opponent") player = "P2";
        if (branch == "target") command = command with { TargetObjectIds = ["UNIT"] };
        if (branch == "extra-cost") command = command with { OptionalCosts = ["SPEND_MANA:1"] };
        if (branch == "closed") state = state with { TimingState = TimingStates.NeutralClosed, PriorityPlayerId = "P1" };
        if (branch.StartsWith("duel-")) state = state with { FocusPlayerId = "P1",
            TimingState = branch == "duel-open" ? TimingStates.SpellDuelOpen : TimingStates.SpellDuelClosed };
        if (branch is "exhausted" or "closed" or "duel-open" or "duel-closed") Assert.False(PromptOffers(state));
        await Reject(state, player, command);
    }

    [Fact]
    public async Task AnActualReturnResponseRemovesAllCountersAndTheOldSkillCannotBuffTheNewObject()
    {
        var game = new Driver(Spell(Position(boons: 3), "RETURN", "OGN·104/298"));
        await game.Act("P1", Activate());
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["UNIT"])); await game.Top();
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Hand);
        Assert.Equal(0, game.State.CardObjects["UNIT"].BoonCount); Assert.Equal(5, game.State.CardObjects["UNIT"].Power);
        Assert.Single(game.State.StackItems);
        var grants = game.Events().Count(e => e.Event.Kind == "BOON_GRANTED");
        await game.Top(); Assert.Equal(grants, game.Events().Count(e => e.Event.Kind == "BOON_GRANTED"));
        Assert.Equal(0, game.State.CardObjects["UNIT"].BoonCount); await game.Replay();
    }

    [Fact]
    public async Task ExternalBoonGrantsStackAndEachCounterReceivesEachMercyBonus()
    {
        var game = new Driver(Spell(Spell(Position(), "MERCY1", "OGN·053/298"), "MERCY2", "OGN·053/298"));
        await game.Act("P1", Activate()); await game.Top();
        for (var i = 1; i <= 2; i++)
        {
            await game.Act("P1", new PlayCardCommand("MERCY" + i, "OGN·053/298", ["UNIT"])); await game.Top();
            AssertBoons(game.State, 1 + i, 6 + i);
            Assert.Equal(6 + i + (1 + i) * i, UnitView(game.State).GetProperty("effectivePower").GetInt32());
        }
        await game.Act("P1", new EndTurnCommand());
        AssertBoons(game.State, 3, 8); Assert.Equal(8, UnitView(game.State).GetProperty("effectivePower").GetInt32());
        await game.Replay();
    }

    [Fact]
    public async Task OrdinaryUnitsStillOnlyReceiveOneBoonFromRepeatedGrants()
    {
        var initial = Spell(Spell(Position(), "MERCY1", "OGN·053/298"), "MERCY2", "OGN·053/298");
        var game = new Driver(initial); var power = initial.CardObjects["ALLY"].Power;
        for (var i = 1; i <= 2; i++)
        {
            await game.Act("P1", new PlayCardCommand("MERCY" + i, "OGN·053/298", ["ALLY"])); await game.Top();
            Assert.Equal(1, game.State.CardObjects["ALLY"].BoonCount); Assert.Equal(power + 1, game.State.CardObjects["ALLY"].Power);
        }
        Assert.Single(game.Events(), e => e.Event.Kind == "BOON_GRANTED"); await game.Replay();
    }

    [Fact]
    public async Task PublicActionConsumesOneCounterReadiesAndThenGrantsAnother()
    {
        var initial = Spell(Position(boons: 3), "ACTION", "OGN·153/298");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["UNIT"] = initial.CardObjects["UNIT"] with { IsExhausted = true } } };
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("ACTION", "OGN·153/298", ["UNIT"])); await game.Top();
        Assert.Single(game.Events(), e => e.Event.Kind == "BOON_CONSUMED");
        Assert.False(game.State.CardObjects["UNIT"].IsExhausted); AssertBoons(game.State, 3, 8);
        await game.Act("P1", Activate()); await game.Top(); AssertBoons(game.State, 4, 9); await game.Replay();
    }

    [Fact]
    public async Task SourceBoonCostsConsumeOneAtATimeAndKeepTheTagUntilTheLastCounter()
    {
        var state = Position("OGN·164/298", 3); var game = new Driver(state);
        for (var remaining = 2; remaining >= 0; remaining--)
        {
            await game.Act("P1", new ActivateAbilityCommand("UNIT", "SOURCE-BOON-MIGHT:0", []));
            Assert.Equal(remaining, game.State.CardObjects["UNIT"].BoonCount);
            Assert.Equal(remaining > 0, game.State.CardObjects["UNIT"].Tags.Contains(CardObjectTags.Boon));
            await game.Top();
        }
        await Reject(game.State, "P1", new ActivateAbilityCommand("UNIT", "SOURCE-BOON-MIGHT:0", [])); await game.Replay();
    }

    [Theory]
    [InlineData(2, false)] [InlineData(3, false)] [InlineData(2, true)]
    public async Task AnyNumberBoonCostCanConsumeSeveralCountersFromTheSameUnit(int leeCount, bool withAlly)
    {
        var initial = HandDiscardObserverTests.Put(Position(boons: 3), "ALBUS", "OGN·230/298", "P1", "HAND");
        var cards = initial.CardObjects.ToDictionary(); var locations = initial.ObjectLocations.ToDictionary();
        var deck = Enumerable.Range(1, 4).Select(i => "RUNE-" + i).ToArray();
        foreach (var id in deck)
        {
            cards[id] = new(id, cardNo: "UNL-R01", ownerId: "P1", controllerId: "P1",
                tags: [CardObjectTags.RuneCard, "COLOR:red"]);
            locations[id] = new("P1", "RUNE_DECK");
        }
        if (withAlly) cards["ALLY"] = cards["ALLY"] with { BoonCount = 1, Power = cards["ALLY"].Power + 1,
            Tags = [..cards["ALLY"].Tags, CardObjectTags.Boon] };
        initial = initial with { CardObjects = cards, ObjectLocations = locations,
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
                ["P1"] = initial.PlayerZones["P1"] with { RuneDeck = deck } } };
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("ALBUS", "OGN·230/298", [], Destination: "BASE"));
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        Assert.Equal(withAlly ? 4 : 3, choice.MaxCount);
        await Reject(game.State, "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow,
            Enumerable.Repeat("UNIT", 4).ToArray()));
        if (withAlly)
            await Reject(game.State, "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ALLY", "ALLY"]));
        var selected = Enumerable.Repeat("UNIT", leeCount).Concat(withAlly ? ["ALLY"] : []).ToArray();
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, selected));
        var receipt = Assert.Single(game.State.StackItems).TriggerCost!;
        Assert.Equal(selected, receipt.ConsumedBoons!.Select(binding => binding.ObjectId));
        AssertBoons(game.State, 3 - leeCount, 8 - leeCount);
        Assert.Equal(selected.Length, game.Events().Count(e => e.Event.Kind == "BOON_CONSUMED"));
        Assert.Equal(4, game.State.PlayerZones["P1"].RuneDeck.Count);
        await game.ColdCheckpoint(); await game.Top();
        Assert.Equal(4 - selected.Length, game.State.PlayerZones["P1"].RuneDeck.Count);
        await game.Replay();
    }

    [Fact]
    public async Task TagOnlyBoonFixturesKeepTheirHashAcrossRecoveryAndUpgradeOnTheNextGrant()
    {
        var initial = Position();
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["UNIT"] = initial.CardObjects["UNIT"] with { Power = 6,
                Tags = [..initial.CardObjects["UNIT"].Tags, CardObjectTags.Boon] } } };
        Assert.Equal(1, UnitView(initial).GetProperty("boonCount").GetInt32());
        var game = new Driver(initial); await game.ColdCheckpoint();
        await game.Act("P1", Activate()); await game.Top();
        AssertBoons(game.State, 2, 7); await game.Replay();
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(3)]
    public void OtherLeeSinChecksPresenceOfABoonWithoutMultiplyingItsTwoMightAura(int count)
    {
        var state = LocalDestructionRecallTests.At(Position(boons: count), "UNIT", "P1", "BATTLEFIELD", "BF");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["AURA"] = OfficialGarenConquestBattlefieldCountTests.Printed("AURA", "OGN·151/298", "P1") },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P1"] = state.PlayerZones["P1"] with { Battlefields = [..state.PlayerZones["P1"].Battlefields, "AURA"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["AURA"] = new("P1", "BATTLEFIELD", "BF") } };
        Assert.Equal(5 + count + (count > 0 ? 2 : 0), UnitView(state).GetProperty("effectivePower").GetInt32());
        ArmamentMightTests.PublicRecovery(state);
    }

    [Fact]
    public void HiddenUnitsDoNotExposeTheirBoonCount()
    {
        var state = Position(boons: 3); state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["UNIT"] = state.CardObjects["UNIT"] with { IsFaceDown = true } } };
        Assert.False(UnitView(state, "P2").TryGetProperty("boonCount", out _));
        Assert.False(UnitView(state, ProjectionViewers.Spectator).TryGetProperty("boonCount", out _));
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(3)]
    public void BilgewaterBullyStillGainsRoamExactlyWhenItHasABoon(int count)
    {
        var state = Position("OGN·125/298", count);
        var effects = ResolutionResult.BuildSnapshots(state)["P1"].Timing["continuousEffects"];
        var json = JsonSerializer.SerializeToElement(effects, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Equal(count > 0, json.EnumerateArray().Any(e => e.GetProperty("sourceObjectId").GetString() == "UNIT"
            && e.GetProperty("effectId").GetString()!.EndsWith(":游走", StringComparison.Ordinal)));
        ArmamentMightTests.PublicRecovery(state);
    }

    [Fact]
    public async Task TheLegendGrantAlsoAddsACounterToAnAlreadyBuffedPenitent()
    {
        var state = Position(boons: 2);
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["LEGEND"] = OfficialGarenConquestBattlefieldCountTests.Printed("LEGEND", "OGN·257/298", "P1") },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P1"] = state.PlayerZones["P1"] with { LegendZone = ["LEGEND"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["LEGEND"] = new("P1", "LEGEND") } };
        var game = new Driver(state);
        await game.Act("P1", new LegendActCommand("LEGEND", LegendActionAbilityCatalog.LeeSinLegendAbilityId,
            ["UNIT"], ["SPEND_MANA:1"]));
        if (game.State.StackItems.Count > 0) await game.Top();
        AssertBoons(game.State, 3, 8); await game.Replay();
    }

    [Fact]
    public async Task ACopyUsesTheSamePrintedSelfBoonAbilityAndUnlimitedPermission()
    {
        var state = Position(boons: 2);
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["UNIT"] = state.CardObjects["UNIT"] with { TokenFactoryCardNo = P6TokenFactoryCatalog.ImageTokenCardNo,
                CopyFaceTransitions = [new(0, 0, P6TokenFactoryCatalog.ImageTokenCardNo, Lee)] } } };
        var game = new Driver(state); await game.Act("P1", Activate()); await game.Top();
        AssertBoons(game.State, 3, 8); await game.Replay();
    }

    [Fact]
    public void RecoveryRejectsForgedPublicCounterValues()
    {
        var state = Position(boons: 3); var snapshots = ResolutionResult.BuildSnapshots(state); var prompts = ResolutionResult.BuildPrompts(state);
        var views = state.Seats.Keys.ToDictionary(id => id, id => new RecoveredPlayerView(id, state.Tick, 0, snapshots[id], state.Tick, 0, prompts[id]));
        var spectator = MatchReplayRedactor.BuildSpectatorFrame(state.RoomId, state.Tick, 0, [], state);
        foreach (var snapshot in snapshots.Values.Append(spectator.SpectatorSnapshot))
        {
            var player = Assert.IsType<Dictionary<string, object?>>(snapshot.Players["P1"]);
            var unit = Assert.IsType<Dictionary<string, object?>>(Assert.IsType<Dictionary<string, object?>>(player["objects"])["UNIT"]);
            unit["boonCount"] = 99;
            Assert.NotEmpty(MatchRecoveryValidator.ValidateComponentStateAndViews(state, views, spectator));
            unit["boonCount"] = 3;
        }
    }

    [Theory]
    [InlineData("OGN·151/298", "powerDelta")]
    [InlineData("OGN·125/298", "powerDelta")]
    [InlineData("OGN·125/298", "sourceCardNo")]
    [InlineData("OGN·125/298", "sourcePath")]
    [InlineData("OGN·125/298", "condition")]
    [InlineData("OGN·125/298", "layerEngineStatus")]
    [InlineData("OGN·125/298", "sourceDependencyObjectIds")]
    [InlineData("OGN·125/298", "deferredLayerEngineResiduals")]
    public void RecoveryRejectsForgedBoonDependentAuraViews(string no, string key)
    {
        var state = Position(no, 1);
        if (no == "OGN·151/298")
        {
            state = LocalDestructionRecallTests.At(state, "UNIT", "P1", "BATTLEFIELD", "BF");
            state = LocalDestructionRecallTests.At(state, "ALLY", "P1", "BATTLEFIELD", "BF");
            state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["ALLY"] = state.CardObjects["ALLY"] with { BoonCount = 1, Power = state.CardObjects["ALLY"].Power + 1,
                    Tags = [..state.CardObjects["ALLY"].Tags, CardObjectTags.Boon] } } };
        }
        ArmamentMightTests.PublicRecovery(state);
        var snapshots = ResolutionResult.BuildSnapshots(state); var prompts = ResolutionResult.BuildPrompts(state);
        var views = state.Seats.Keys.ToDictionary(id => id, id => new RecoveredPlayerView(id, state.Tick, 0, snapshots[id], state.Tick, 0, prompts[id]));
        var spectator = MatchReplayRedactor.BuildSpectatorFrame(state.RoomId, state.Tick, 0, [], state);
        foreach (var snapshot in snapshots.Values.Append(spectator.SpectatorSnapshot))
        {
            var effects = Assert.IsAssignableFrom<IEnumerable<Dictionary<string, object?>>>(snapshot.Timing["continuousEffects"]);
            var effect = effects.First(e => Equals(e.GetValueOrDefault("sourceCardNo"), no));
            var original = effect[key];
            effect[key] = key == "powerDelta" ? 99 : original is string ? "FORGED" : new[] { "FORGED" };
            Assert.NotEmpty(MatchRecoveryValidator.ValidateComponentStateAndViews(state, views, spectator));
            effect[key] = original;
        }
    }

    private static async Task Reject(MatchState state, string player, GameCommand command)
    {
        var result = await new CoreRuleEngine().ResolveAsync(state, new("illegal-penitent", player, command.CmdType), command, default);
        Assert.False(result.Accepted); Assert.Empty(result.Events); Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(result.State));
    }
}
