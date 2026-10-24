using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class WildClawSourceIdentityTests
{
    [Fact]
    public async Task DeclaredReentryPositionHasAPhysicalBattlefieldAndKeepsTheEarlierReflexiveBinding()
    {
        var initial = MatchReplayInitialStateBuilder.FromSeats("wild-claw-declared-reentry", new Dictionary<string, string> { ["alice"] = "P1", ["bob"] = "P2" });
        var journal = new SourceJournal(); var session = new MatchSession(initial, new CoreRuleEngine(), journal);
        var current = await session.SeedScenarioAsync("alice", "explicit-setup", "wild-claw-reentry",
            JsonSerializer.SerializeToElement(new { cmdType = "DEV_SEED_SCENARIO", scenarioId = "wild-claw-reentry" }), default);
        Assert.True(current.Accepted, current.ErrorMessage); Assert.Equal("FIELD", current.State.ObjectLocations["FIELD"].BattlefieldObjectId);
        async Task Act(string actor, GameCommand command) {
            var prompt = session.PromptFor(actor);
            var raw = JsonSerializer.SerializeToNode(command, command.GetType(), new JsonSerializerOptions(JsonSerializerDefaults.Web))!.AsObject();
            raw["promptId"] = prompt.PromptId; raw["snapshotTick"] = current.State.Tick;
            current = await session.SubmitAsync(actor, "original-" + journal.Entries.Count, command, JsonSerializer.SerializeToElement(raw), default);
            Assert.True(current.Accepted, current.ErrorMessage); FullGameEndToEndTests.AssertNoHiddenZoneLeak(current);
            OfficialGraveyardRecastTests.Restore(current.State); ArmamentMightTests.PublicRecovery(current.State);
        }
        async Task Top() { await Act(current.State.PriorityPlayerId!, new PassPriorityCommand()); await Act(current.State.PriorityPlayerId!, new PassPriorityCommand()); }
        await Act("alice", new PlayCardCommand("PARENT", WildClawPendingReflexiveTests.WildClaw, [])); await Top();
        var choice = current.State.PendingCardChoice!; await Act("alice", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["P1_FIRST"]));
        var create = current.State.PendingRuleChoice!; await Act("alice", new PayCostCommand(create.Request.Id, "RULE_REPLACEMENT", ["CREATE"]));
        await Act("alice", new PlayCardCommand("P1_FIRST", "OGN·096/298", []));
        var binding = Assert.Single(current.State.StackItems).DeckPlayReflexive!.Played;
        await Act("alice", new PlayCardCommand("RESPONSE", "UNL-184/219", ["P1_FIRST"])); await Top();
        var candidate = Assert.Single(ResolutionResult.BuildPrompts(current.State)["alice"].Candidates!, c => c.Action == CommandTypes.PlayCard);
        Assert.Contains(candidate.Destinations!, d => d.Id == "BATTLEFIELD:FIELD");
        await Act("alice", new PlayCardCommand("P1_FIRST", "OGN·096/298", [], Destination: "BATTLEFIELD:FIELD"));
        Assert.Equal(binding, Assert.Single(current.State.StackItems).DeckPlayReflexive!.Played);
        await Top(); Assert.False(current.State.CardObjects["P1_FIRST"].IsEmpowered);
        Assert.Equal(0, journal.Entries[0].StartedTick); Assert.Equal("DEV_SEED_SCENARIO:wild-claw-reentry", journal.Entries[0].CommandType);
        var commands = journal.Entries.Select(e => new RecoveredCommand(e.PlayerId, e.ClientIntentId, e.CommandType, e.RawCommand,
            e.StartedTick, e.CompletedTick, e.StartedEventSequence, e.CompletedEventSequence, e.Accepted, e.ErrorMessage) { ErrorCode = e.ErrorCode, RuleInputs = e.RuleInputs }).ToArray();
        var events = journal.Entries.SelectMany(e => e.Events.Select((value, index) => new RecoveredEvent(e.StartedEventSequence + index + 1, e.CompletedTick, index, value))).ToArray();
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, commands, current.State, new CoreRuleEngine(), default, events);
        Assert.True(replay.IsMatch, string.Join("; ", replay.Errors));
    }

    [Fact]
    public async Task ExactCn37785HasItsOwnPrintingAndPrintedOrangePowerInAdditionToTheSevenMana()
    {
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "wild-claw-source.json")));
        var source = document.RootElement; var raw = source.GetProperty("rawApiCard");
        Assert.Equal("CN", source.GetProperty("region").GetString());
        Assert.Equal(JsonValueKind.Null, source.GetProperty("publicationDate").ValueKind);
        Assert.Equal(JsonValueKind.Null, source.GetProperty("effectiveDate").ValueKind);
        Assert.Equal("VEN·089/166 SC", source.GetProperty("printedImageCardNo").GetString());
        Assert.Equal("VEN-089/166", source.GetProperty("separateGlobalIdentity").GetString());
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(); var card = Assert.Single(catalog.Cards, c => c.CardNo == WildClawPendingReflexiveTests.WildClaw);
        Assert.Equal(37785, card.Id); Assert.Equal(raw.GetProperty("cardEffect").GetString(), card.CardEffect);
        Assert.Equal("狂野钩爪", card.CardName); Assert.Equal("spell", card.CardCategory);
        Assert.Equal(7, card.Energy); Assert.Equal(1, card.ReturnEnergy);
        Assert.Equal(new[] { "orange" }, card.CardColorList);
        var cost = PrintedPowerCostRules.ForCard(card.CardNo); Assert.Equal(1, cost.Amount); Assert.Equal(new[] { "orange" }, cost.Traits);
        var initial = WildClawPendingReflexiveTests.Position();
        var command = new PlayCardCommand("PARENT", card.CardNo, []);
        var quote = new CoreRuleEngine().PreviewPlayCard(initial, "P1", PlayCostPreviewTests.Request(initial, command));
        Assert.True(quote.CanPay, quote.Message); Assert.Equal(7, quote.Cost!.Mana);
        Assert.Equal(1, quote.Cost.PowerByTrait["orange"]);
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await driver.Act("P1", command);
        Assert.Equal(13, driver.State.RunePools["P1"].Mana); Assert.Equal(19, driver.State.RunePools["P1"].TotalPower);
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Fact]
    public async Task OneNewFunctionalIdentityPreservesEveryPriorFunctionalUnitIdAndMembership()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync();
        // Keep the exact original N46 membership as later printings are added.
        using var historical = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(
            AppContext.BaseDirectory, "Fixtures", "wild-claw-historical-1036-printings.json")));
        var retainedCardNos = historical.RootElement.GetProperty("cardNos").EnumerateArray()
            .Select(cardNo => cardNo.GetString()!).ToHashSet(StringComparer.Ordinal);
        Assert.Equal(1036, retainedCardNos.Count);
        var wildClawScope = catalog.Cards.Where(c => retainedCardNos.Contains(c.CardNo)).ToArray();
        Assert.Equal(retainedCardNos.OrderBy(cardNo => cardNo, StringComparer.Ordinal),
            wildClawScope.Select(card => card.CardNo).OrderBy(cardNo => cardNo, StringComparer.Ordinal));
        var oldCards = wildClawScope.Where(c => c.CardNo != WildClawPendingReflexiveTests.WildClaw).ToArray();
        Assert.Equal(1035, oldCards.Length); Assert.Equal(1036, wildClawScope.Length);
        var oldUnits = FunctionalUnitBuilder.Build(oldCards); var currentUnits = FunctionalUnitBuilder.Build(wildClawScope);
        Assert.Equal(834, oldUnits.Count); Assert.Equal(835, currentUnits.Count);
        foreach (var old in oldUnits) {
            var current = Assert.Single(currentUnits, c => c.Id == old.Id);
            Assert.Equal(JsonSerializer.Serialize(old), JsonSerializer.Serialize(current));
        }
        Assert.Single(currentUnits, c => !oldUnits.Any(o => o.Id == c.Id));
    }

    [Fact]
    public async Task ActualPrevious1035CatalogIdentityCannotBeRelabelledAsTheCurrentInputsOrReplayedAsAnOldDevelopmentGame()
    {
        using var source = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "wild-claw-source.json")));
        var prior = source.RootElement.GetProperty("previous1035RuntimeCatalogContentSha256").GetString()!;
        var current = RuleInputIdentity.Current; Assert.NotEqual(prior, current.CatalogSha256);
        var identity = new RuleInputIdentity(1, "CN", "zh-CN", current.AuthoritySha256, prior, current.ImplementationSha256);
        var state = MatchReplayInitialStateBuilder.FromSeats("prior1035-current1036-no-migration", new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" }) with { RuleInputs = identity };
        var rejected = await new CoreRuleEngine().ResolveAsync(state, new("prior-catalog", "P1", CommandTypes.Pass), new PassCommand(), default);
        Assert.False(rejected.Accepted); Assert.Equal(ErrorCodes.RecoveryInconsistent, rejected.ErrorCode);
        Assert.Same(state, rejected.State); Assert.Empty(rejected.Events);
        Assert.Throws<MatchSessionException>(() => new MatchSession(state, new CoreRuleEngine(), NoopMatchJournal.Instance));
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(state, [], state, new CoreRuleEngine(), default);
        Assert.False(replay.IsMatch); Assert.NotEmpty(replay.Errors);
    }
    private sealed class SourceJournal : IMatchJournal
    {
        internal List<MatchJournalEntry> Entries { get; } = [];
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken token) { Entries.Add(entry); return ValueTask.CompletedTask; }
    }
}
