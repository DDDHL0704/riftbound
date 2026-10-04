using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

// CN 355-359, 356.1.b: an instruction to play still performs a play and pays
// additional costs. A private hand choice is made during resolution (355.10).
public sealed class OfficialEffectReplayTests
{
    [Theory]
    [InlineData("OGN·198/298", "GRAVEYARD")]
    [InlineData("OGN·102/298", "BASE")]
    public async Task EffectOpensRecoverablePlayInsteadOfMovingUnitWithoutPayment(string parent, string sourceZone)
    {
        var result = await ResolveParent(Position(parent, sourceZone), ["UNIT"]);
        Assert.DoesNotContain("UNIT", result.State.PlayerZones["P1"].Base);
        Assert.NotNull(result.State.PendingEffectPlay);
        Assert.Contains(CommandTypes.PlayCard, result.Prompts["P1"].EnabledActions());
    }

    [Fact]
    public async Task HelpArrivesDoesNotPublishTheHandChoiceAsAParentTarget()
    {
        var state = Position("SFD·111/221", "HAND");
        var result = await ResolveParent(state, []);
        Assert.NotNull(result.State.PendingEffectPlay);
        Assert.Contains(CommandTypes.PlayCard, result.Prompts["P1"].EnabledActions());
        Assert.DoesNotContain("UNIT", JsonSerializer.Serialize(result.Prompts["P2"]));
    }

    [Fact]
    public async Task HarrowingPaysPrintedPowerAndPreservesPendingChoiceOnFailure()
    {
        var setup = Position("OGN·198/298", "GRAVEYARD");
        setup = setup with { CardObjects = new Dictionary<string, CardObjectState>(setup.CardObjects)
            { ["UNIT"] = setup.CardObjects["UNIT"] with { CardNo = "SFD·143/221", ManaCost = 4 } } };
        var opened = await ResolveParent(setup, ["UNIT"]);
        var state = opened.State with { RunePools = new Dictionary<string, RunePool>(opened.State.RunePools) { ["P1"] = RunePool.Empty } };
        var command = new PlayCardCommand("UNIT", "SFD·143/221", []);
        var engine = new CoreRuleEngine();
        var before = MatchStateHasher.Hash(state);
        var quote = engine.PreviewPlayCard(state, "P1", PlayCostPreviewTests.Request(state, command));
        Assert.NotNull(quote.Cost);
        Assert.Equal(0, quote.Cost.Mana);
        Assert.False(quote.CanPay);
        var refused = await engine.ResolveAsync(state, new("child", "P1", CommandTypes.PlayCard), command, default);
        Assert.False(refused.Accepted);
        Assert.Equal(before, MatchStateHasher.Hash(refused.State));
        state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(0, 1) } };
        var restored = JsonSerializer.Deserialize<MatchState>(JsonSerializer.Serialize(state))!;
        Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(restored));
        Assert.Equal(JsonSerializer.Serialize(ResolutionResult.BuildPrompts(state)), JsonSerializer.Serialize(ResolutionResult.BuildPrompts(restored)));
        var played = await engine.ResolveAsync(restored, new("child", "P1", CommandTypes.PlayCard), command, default);
        Assert.True(played.Accepted, played.ErrorMessage);
        Assert.Null(played.State.PendingEffectPlay);
        Assert.Contains("UNIT", played.State.PlayerZones["P1"].Base);
        Assert.Contains("CARD", played.State.PlayerZones["P1"].Graveyard);
        Assert.Equal(0, played.State.RunePools["P1"].TotalPower);
        Assert.True(played.State.CardObjects["UNIT"].IsExhausted);
        var repeated = await engine.ResolveAsync(played.State, new("again", "P1", CommandTypes.PlayCard), command, default);
        Assert.False(repeated.Accepted);
        Assert.Equal(MatchStateHasher.Hash(played.State), MatchStateHasher.Hash(repeated.State));
    }

    [Fact]
    public async Task HelpArrivesPaysDifferenceAndRequiresControlledBattlefield()
    {
        var opened = await ResolveParent(Position("SFD·111/221", "HAND"), []);
        var state = opened.State;
        var engine = new CoreRuleEngine();
        var invalid = await engine.ResolveAsync(state, new("base", "P1", CommandTypes.PlayCard), new PlayCardCommand("UNIT", "SFD·125/221", []), default);
        Assert.False(invalid.Accepted);
        var play = new PlayCardCommand("UNIT", "SFD·125/221", [], Destination: "BATTLEFIELD:FIELD");
        var quote = engine.PreviewPlayCard(state, "P1", PlayCostPreviewTests.Request(state, play));
        Assert.Equal(1, quote.Cost!.Mana);
        var result = await engine.ResolveAsync(state, new("field", "P1", CommandTypes.PlayCard), play, default);
        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Contains("UNIT", result.State.PlayerZones["P1"].Battlefields);
        Assert.Equal("FIELD", result.State.ObjectLocations["UNIT"].BattlefieldObjectId);
        Assert.Equal(state.RunePools["P1"].Mana - 1, result.State.RunePools["P1"].Mana);
        Assert.Null(result.State.PendingEffectPlay);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HelpArrivesCanBeDeclinedEvenWhenNoControlledField(bool removeControl)
    {
        var state = Position("SFD·111/221", "HAND");
        if (removeControl) state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects)
            { ["FIELD"] = state.CardObjects["FIELD"] with { ControllerId = null } } };
        var opened = await ResolveParent(state, []);
        var pending = Assert.IsType<PendingEffectPlayState>(opened.State.PendingEffectPlay);
        var result = await new CoreRuleEngine().ResolveAsync(opened.State, new("decline", "P1", CommandTypes.ChooseCards),
            new ChooseCardsCommand(pending.ChoiceId, "EFFECT_PLAY", []), default);
        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Contains("UNIT", result.State.PlayerZones["P1"].Hand);
        Assert.Contains("CARD", result.State.PlayerZones["P1"].Graveyard);
        Assert.Null(result.State.PendingEffectPlay);
    }

    [Fact]
    public async Task RescueOwnerReplaysAndStillPaysHasteAdditionalCosts()
    {
        var setup = Position("OGN·102/298", "BASE");
        setup = setup with { CardObjects = new Dictionary<string, CardObjectState>(setup.CardObjects)
            { ["UNIT"] = setup.CardObjects["UNIT"] with { CardNo = "SFD·143/221", OwnerId = "P2", Damage = 2,
                Power = 6, UntilEndOfTurnPowerModifier = 2, UntilEndOfTurnEffects = ["OLD"], IsExhausted = true } } };
        var opened = await ResolveParent(setup, ["UNIT"]);
        Assert.Equal("P2", opened.State.PendingEffectPlay!.PlayerId);
        Assert.Contains("UNIT", opened.State.PlayerZones["P2"].Banished);
        Assert.Equal(0, opened.State.CardObjects["UNIT"].Damage);
        var state = opened.State with { RunePools = new Dictionary<string, RunePool>(opened.State.RunePools) { ["P2"] = new(1, 1) } };
        var command = new PlayCardCommand("UNIT", "SFD·143/221", [], OptionalCosts: [HasteOptionalCostNames.HasteReady]);
        var engine = new CoreRuleEngine();
        var quote = engine.PreviewPlayCard(state, "P2", PlayCostPreviewTests.Request(state, command));
        Assert.True(quote.CanPay, quote.Message);
        Assert.Equal(1, quote.Cost!.Mana);
        var result = await engine.ResolveAsync(state, new("owner", "P2", CommandTypes.PlayCard), command, default);
        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Contains("UNIT", result.State.PlayerZones["P2"].Base);
        Assert.DoesNotContain("UNIT", result.State.PlayerZones["P1"].Base);
        Assert.Equal("P2", result.State.CardObjects["UNIT"].ControllerId);
        Assert.Equal(0, result.State.CardObjects["UNIT"].Damage);
        Assert.Empty(result.State.CardObjects["UNIT"].UntilEndOfTurnEffects);
        Assert.False(result.State.CardObjects["UNIT"].IsExhausted);
        Assert.Equal(0, result.State.RunePools["P2"].Mana);
        Assert.Equal(0, result.State.RunePools["P2"].TotalPower);
        Assert.Equal(setup.CardObjects["UNIT"].ObjectGeneration + 2, result.State.CardObjects["UNIT"].ObjectGeneration);
    }

    [Fact]
    public async Task SourceGenerationAndWrongActorCannotExecuteARecoveredEffect()
    {
        var opened = await ResolveParent(Position("OGN·198/298", "GRAVEYARD"), ["UNIT"]);
        var state = opened.State;
        var command = new PlayCardCommand("UNIT", "SFD·125/221", []);
        var engine = new CoreRuleEngine();
        Assert.False((await engine.ResolveAsync(state, new("wrong", "P2", CommandTypes.PlayCard), command, default)).Accepted);
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects)
            { ["UNIT"] = state.CardObjects["UNIT"] with { ObjectGeneration = 3 } } };
        var result = await engine.ResolveAsync(state, new("stale", "P1", CommandTypes.PlayCard), command, default);
        Assert.False(result.Accepted);
        Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(result.State));
    }

    [Fact]
    public async Task ReplayedUnitQueuesItsPlayAbilityAndParentCompletesFirst()
    {
        var setup = Position("OGN·198/298", "GRAVEYARD");
        setup = setup with { CardObjects = new Dictionary<string, CardObjectState>(setup.CardObjects)
            { ["UNIT"] = setup.CardObjects["UNIT"] with { CardNo = "OGN·087/298" } } };
        var opened = await ResolveParent(setup, ["UNIT"]);
        var engine = new CoreRuleEngine();
        var played = await engine.ResolveAsync(opened.State, new("replay", "P1", CommandTypes.PlayCard), new PlayCardCommand("UNIT", "OGN·087/298", []), default);
        Assert.True(played.Accepted, played.ErrorMessage);
        Assert.Contains("CARD", played.State.PlayerZones["P1"].Graveyard);
        Assert.Contains("UNIT", played.State.PlayerZones["P1"].Base);
        Assert.DoesNotContain("DRAW", played.State.PlayerZones["P1"].Hand);
        Assert.True(Assert.Single(played.State.StackItems).SourceConfirmed);
        var done = await PermanentConfirmationAssert.ResolveAfterPlayAsync(engine, played);
        Assert.Contains("DRAW", done.State.PlayerZones["P1"].Hand);
        Assert.Empty(done.State.StackItems);
    }

    [Fact]
    public async Task PendingEffectSurvivesSessionRestoreDuplicateIntentAndActionLogReplay()
    {
        var opened = await ResolveParent(Position("SFD·111/221", "HAND"), []);
        Assert.DoesNotContain("\"UNIT\"", JsonSerializer.Serialize(opened.Snapshots["P2"]));
        Assert.DoesNotContain("\"UNIT\"", JsonSerializer.Serialize(opened.Prompts["P2"]));
        var restored = JsonSerializer.Deserialize<MatchState>(JsonSerializer.Serialize(opened.State))!;
        var journal = new ReplayJournal();
        var engine = new CoreRuleEngine();
        var session = new MatchSession(restored, engine, journal);
        var prompt = session.PromptFor("P1");
        var command = new PlayCardCommand("UNIT", "SFD·125/221", [], Destination: "BATTLEFIELD:FIELD");
        var raw = JsonSerializer.SerializeToElement(new { cmdType = "PLAY_CARD", sourceObjectId = "UNIT", cardNo = "SFD·125/221",
            targetObjectIds = Array.Empty<string>(), destination = "BATTLEFIELD:FIELD", promptId = prompt.PromptId, snapshotTick = restored.Tick });
        var first = await session.SubmitAsync("P1", "recovered-play", command, raw, default);
        Assert.True(first.Accepted, first.ErrorMessage);
        var duplicate = await session.SubmitAsync("P1", "recovered-play", command, raw, default);
        Assert.True(duplicate.Accepted, duplicate.ErrorMessage);
        Assert.Equal(first.State.Tick, duplicate.State.Tick);
        Assert.Equal(MatchStateHasher.Hash(first.State), MatchStateHasher.Hash(duplicate.State));
        Assert.Single(journal.Entries);
        var commands = journal.Entries.Select(e => new RecoveredCommand(e.PlayerId, e.ClientIntentId, e.CommandType,
            e.RawCommand, e.StartedTick, e.CompletedTick, e.StartedEventSequence, e.CompletedEventSequence, e.Accepted, e.ErrorMessage)).ToArray();
        var events = journal.Entries.SelectMany(e => e.Events.Select((ev, index) => new RecoveredEvent(e.StartedEventSequence + index + 1, e.CompletedTick, index, ev))).ToArray();
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(restored, commands, first.State, engine, default, events);
        Assert.Empty(replay.Errors);
    }

    [Fact]
    public async Task ImpossibleMandatoryPaymentCanResumeButPayablePlayCannotBeSkipped()
    {
        var setup = Position("OGN·198/298", "GRAVEYARD");
        setup = setup with { CardObjects = new Dictionary<string, CardObjectState>(setup.CardObjects)
            { ["UNIT"] = setup.CardObjects["UNIT"] with { CardNo = "SFD·143/221" } } };
        var opened = await ResolveParent(setup, ["UNIT"]);
        var pending = opened.State.PendingEffectPlay!;
        var decline = new ChooseCardsCommand(pending.ChoiceId, "EFFECT_PLAY", []);
        var engine = new CoreRuleEngine();
        Assert.False((await engine.ResolveAsync(opened.State, new("no-skip", "P1", CommandTypes.ChooseCards), decline, default)).Accepted);
        var state = opened.State with { RunePools = new Dictionary<string, RunePool>(opened.State.RunePools) { ["P1"] = RunePool.Empty } };
        Assert.Contains(CommandTypes.ChooseCards, ResolutionResult.BuildPrompts(state)["P1"].EnabledActions());
        var result = await engine.ResolveAsync(state, new("unable", "P1", CommandTypes.ChooseCards), decline, default);
        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Null(result.State.PendingEffectPlay);
        Assert.Contains("UNIT", result.State.PlayerZones["P1"].Graveyard);
        Assert.Contains("CARD", result.State.PlayerZones["P1"].Graveyard);
    }

    private sealed class ReplayJournal : IMatchJournal
    {
        internal List<MatchJournalEntry> Entries { get; } = [];
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken token) { Entries.Add(entry); return ValueTask.CompletedTask; }
    }

    internal static MatchState Position(string parent, string sourceZone)
    {
        var state = OfficialPermanentConfirmationTests.Position(parent, false);
        var cards = state.CardObjects.ToDictionary(x => x.Key, x => x.Value);
        cards["CARD"] = cards["CARD"] with { Tags = [CardObjectTags.SpellCard] };
        cards["UNIT"] = new("UNIT", cardNo: "SFD·125/221", power: 4, tags: [CardObjectTags.UnitCard], ownerId: "P1", controllerId: "P1");
        cards["FIELD"] = new("FIELD", cardNo: "OGN·294/298", tags: ["CARD_TYPE:BATTLEFIELD"], ownerId: "P1", controllerId: "P1");
        return state with { CardObjects = cards, PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones)
        { ["P1"] = state.PlayerZones["P1"] with {
            Hand = sourceZone == "HAND" ? ["CARD", "UNIT"] : ["CARD"],
            Base = sourceZone == "BASE" ? ["UNIT"] : [],
            Graveyard = sourceZone == "GRAVEYARD" ? ["UNIT"] : [], Battlefields = ["FIELD"] } } };
    }

    internal static async Task<ResolutionResult> ResolveParent(MatchState state, string[] targets)
    {
        var engine = new CoreRuleEngine();
        var result = await engine.ResolveAsync(state, new("parent", "P1", CommandTypes.PlayCard),
            new PlayCardCommand("CARD", state.CardObjects["CARD"].CardNo!, targets), default);
        Assert.True(result.Accepted, result.ErrorMessage);
        for (var i = 0; i < 2; i++) {
            result = await engine.ResolveAsync(result.State, new($"pass{i}", result.State.PriorityPlayerId!, CommandTypes.PassPriority), new PassPriorityCommand(), default);
            Assert.True(result.Accepted, result.ErrorMessage);
        }
        return result;
    }
}
