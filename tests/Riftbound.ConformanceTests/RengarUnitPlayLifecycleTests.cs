using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

// Explicit tick0 midgame positions. They do not certify a deck, opening,
// original native history or the whole source/response cards.
public sealed class RengarUnitPlayLifecycleTests
{
    private static MatchState Position() => RengarUnitPlayConfirmationTests.WithLegend(
        PermanentPlayTargetTests.Position("UNL-111/219"));

    private static MatchState WithReturn(MatchState state)
    {
        state = AttackDamageDistributionTests.Spell(state, "RETURN", "OGN·104/298", "P1");
        var cards = state.CardObjects.ToDictionary(p => p.Key, p => p.Value);
        foreach (var id in state.PlayerZones["P1"].RuneDeck)
            cards[id] = cards[id] with { CardNo = "UNL-R01", Tags = [CardObjectTags.RuneCard, "COLOR:red"] };
        return state with { CardObjects = cards };
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AnActualUnitTokenPlayChoosesEitherItselfOrAnotherUnit(bool itself)
    {
        var initial = AttackDamageDistributionTests.Spell(Position(), "SANDCRAFT", "SFD·031/221", "P1");
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await driver.Act("P1", new PlayCardCommand("SANDCRAFT", "SFD·031/221", [])); await driver.Top();
        var token = Assert.Single(driver.State.CardObjects.Values,
            c => c.TokenFactoryCardNo == P6TokenFactoryCatalog.SandSoldierTokenCardNo);
        var choice = Assert.IsType<PendingCardChoiceState>(driver.State.PendingCardChoice);
        Assert.Equal("LEGEND", choice.SourceObjectId);
        Assert.Equal(1, choice.RequiredCount); Assert.Equal(1, choice.MaxCount);
        Assert.Contains(token.ObjectId, choice.LegalObjectIds); Assert.Contains("HOST", choice.LegalObjectIds);
        var item = Assert.Single(driver.State.StackItems);
        Assert.Equal(token.ObjectId, item.UnitPlayMight!.PlayedUnit.ObjectId);
        Assert.Equal(P6TokenFactoryCatalog.SandSoldierTokenCardNo, item.UnitPlayMight.PlayedUnitTokenFactoryCardNo);
        var target = itself ? token.ObjectId : "HOST"; var power = driver.State.CardObjects[target].Power;
        await driver.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [target]));
        await driver.Top();
        Assert.Equal(power + 1, driver.State.CardObjects[target].Power);
        Assert.Empty(driver.State.StackItems);
        await BanishEffectPlayTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task APlayedTokenMayCeaseWhileTheLegendStillResolvesOrItsChosenTokenTargetFizzles(bool targetToken)
    {
        var initial = WithReturn(AttackDamageDistributionTests.Spell(Position(), "SANDCRAFT", "SFD·031/221", "P1"));
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await driver.Act("P1", new PlayCardCommand("SANDCRAFT", "SFD·031/221", [])); await driver.Top();
        var token = Assert.Single(driver.State.CardObjects.Values,
            c => c.TokenFactoryCardNo == P6TokenFactoryCatalog.SandSoldierTokenCardNo).ObjectId;
        var choice = Assert.IsType<PendingCardChoiceState>(driver.State.PendingCardChoice);
        await driver.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [targetToken ? token : "HOST"]));
        var power = driver.State.CardObjects["HOST"].Power;
        await driver.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", [token])); await driver.Top();
        Assert.False(driver.State.CardObjects.ContainsKey(token));
        Assert.DoesNotContain(token, driver.State.PlayerZones["P1"].Hand);
        Assert.Equal("LEGEND", Assert.Single(driver.State.StackItems).SourceObjectId);
        var resolved = await driver.Top();
        Assert.Equal(power + (targetToken ? 0 : 1), driver.State.CardObjects["HOST"].Power);
        Assert.DoesNotContain(resolved.Events, e => e.Kind == "POWER_MODIFIED_UNTIL_END_OF_TURN"
            && Equals(e.Payload.GetValueOrDefault("targetObjectId"), token));
        Assert.Empty(driver.State.StackItems);
        await BanishEffectPlayTests.Replay(initial, driver);
    }

    [Fact]
    public async Task ReturningThePlayedPhysicalUnitDoesNotRemoveTheIndependentLegendSkill()
    {
        var initial = WithReturn(Position()); var driver = new StandbyDefendObserverTests.Driver(initial);
        await driver.Act("P1", new PlayCardCommand("UNIT", "UNL-111/219", []));
        var choice = driver.State.PendingCardChoice!;
        await driver.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["HOST"]));
        var captured = Assert.Single(driver.State.StackItems).UnitPlayMight!.PlayedUnit;
        await driver.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["UNIT"])); await driver.Top();
        Assert.Contains("UNIT", driver.State.PlayerZones["P1"].Hand);
        Assert.True(driver.State.CardObjects["UNIT"].ObjectGeneration > captured.Generation);
        Assert.Equal("LEGEND", Assert.Single(driver.State.StackItems).SourceObjectId);
        var power = driver.State.CardObjects["HOST"].Power;
        await driver.Top();
        Assert.Equal(power + 1, driver.State.CardObjects["HOST"].Power);
        await BanishEffectPlayTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task WardIsPaidOrDeclinedForTheLegendWithoutUndoingTheUnit(bool declared, bool pay)
    {
        var initial = LocalDestructionRecallTests.At(Position(), "HOST", "P2", "BASE");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["HOST"] = initial.CardObjects["HOST"] with { OwnerId = "P2", ControllerId = "P2",
                Tags = [CardObjectTags.UnitCard, CardObjectTags.Spellshield] } } };
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await driver.Act("P1", new PlayCardCommand("UNIT", "UNL-111/219", declared ? ["HOST"] : []));
        var available = driver.State.RunePools["P1"].TotalPower;
        if (!declared)
        {
            var choice = driver.State.PendingCardChoice!;
            await BanishEffectPlayTests.Reject(driver.State, "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, []));
            await BanishEffectPlayTests.Reject(driver.State, "P2", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["HOST"]));
            await driver.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["HOST"]));
        }
        var payment = Assert.IsType<PendingPaymentState>(driver.State.PendingPayment);
        Assert.Equal("TRIGGER_TARGET_COST", payment.PaymentWindow); Assert.Equal(1, payment.PowerCost);
        Assert.Equal(available, driver.State.RunePools["P1"].TotalPower);
        var power = driver.State.CardObjects["HOST"].Power;
        await driver.Act("P1", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, [pay ? "PAY" : "DECLINE"]));
        if (pay) await driver.Top();
        Assert.Equal(available - (pay ? 1 : 0), driver.State.RunePools["P1"].TotalPower);
        Assert.Equal(power + (pay ? 1 : 0), driver.State.CardObjects["HOST"].Power);
        Assert.Contains("UNIT", driver.State.PlayerZones["P1"].Base);
        Assert.Empty(driver.State.StackItems);
        await BanishEffectPlayTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("source-print")]
    [InlineData("source-generation")]
    [InlineData("played-unit")]
    [InlineData("played-generation")]
    [InlineData("played-print")]
    [InlineData("token-origin")]
    [InlineData("target-origin")]
    [InlineData("tick")]
    [InlineData("controller")]
    [InlineData("orphan")]
    [InlineData("optional")]
    public async Task RebuiltViewsCannotAuthorizeAnInvalidCapturedOriginOrOptionalTarget(string mutation)
    {
        var driver = new StandbyDefendObserverTests.Driver(Position());
        await driver.Act("P1", new PlayCardCommand("UNIT", "UNL-111/219", []));
        var state = driver.State;
        Assert.Empty(OfficialInsightAndSpellLockTests.Errors(state));
        var item = Assert.Single(state.StackItems); var context = item.UnitPlayMight!;
        var changed = mutation switch {
            "missing" => item with { UnitPlayMight = null },
            "source-print" => item with { UnitPlayMight = context with { CardNo = "SFD·201/221" } },
            "source-generation" => item with { UnitPlayMight = context with { SourceGeneration = context.SourceGeneration + 1 } },
            "played-unit" => item with { UnitPlayMight = context with { PlayedUnit = new("HOST", 0) } },
            "played-generation" => item with { UnitPlayMight = context with { PlayedUnit = context.PlayedUnit with { Generation = context.PlayedUnit.Generation + 1 } } },
            "played-print" => item with { UnitPlayMight = context with { PlayedCardNo = "OGN·001/298" } },
            "token-origin" => item with { UnitPlayMight = context with { PlayedUnitTokenFactoryCardNo = "SFD·T02" } },
            "target-origin" => item with { UnitPlayMight = context with { TargetTokenFactoryCardNo = "SFD·T02" } },
            "tick" => item with { UnitPlayMight = context with { CapturedTick = 0 } },
            "controller" => item with { ControllerId = "P2" },
            _ => item
        };
        var forged = state with { StackItems = [changed] };
        if (mutation == "orphan") forged = forged with { PendingCardChoice = null };
        if (mutation == "optional") forged = forged with { PendingCardChoice = state.PendingCardChoice! with { RequiredCount = 0 } };
        Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(forged));
    }

    [Theory]
    [InlineData("pointer")]
    [InlineData("missing-pointer")]
    [InlineData("controller")]
    [InlineData("source")]
    [InlineData("effect")]
    [InlineData("missing-legend")]
    [InlineData("missing-body")]
    [InlineData("unconfirmed-body")]
    [InlineData("rengar-print")]
    public async Task DeferredOrderingCannotBorrowAnotherBodyOrLoseItsActualCompanion(string mutation)
    {
        var (_, driver) = await RengarUnitPlayConfirmationTests.OpenConcurrentPlay();
        var state = driver.State; Assert.Empty(OfficialInsightAndSpellLockTests.Errors(state));
        var own = Assert.Single(state.TriggerQueue, t => t.PermanentPlayStackItemId is not null);
        var changed = mutation switch {
            "pointer" => own with { PermanentPlayStackItemId = "missing" },
            "missing-pointer" => own with { PermanentPlayStackItemId = null },
            "controller" => own with { ControllerId = "P2" },
            "source" => own with { SourceObjectId = "HOST" },
            "effect" => own with { EffectKind = "UNIT_PLAYED_POWER_PLUS_1" },
            _ => own
        };
        var forged = state with { TriggerQueue = state.TriggerQueue.Select(t => t.TriggerId == own.TriggerId ? changed : t).ToArray() };
        if (mutation == "missing-legend") forged = forged with { TriggerQueue = forged.TriggerQueue.Where(t => t.UnitPlayMight is null).ToArray() };
        if (mutation == "missing-body") forged = forged with { StackItems = [] };
        if (mutation == "unconfirmed-body") forged = forged with { StackItems = [Assert.Single(state.StackItems) with { SourceConfirmed = false }] };
        if (mutation == "rengar-print") forged = forged with { TriggerQueue = forged.TriggerQueue.Select(t => t.UnitPlayMight is null
            ? t : t with { SourceCardNo = "SFD·201/221" }).ToArray() };
        Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(forged));
        await BanishEffectPlayTests.Reject(forged, "P1", new OrderTriggersCommand(OrderedTriggerIds: forged.TriggerQueue.Select(t => t.TriggerId).ToArray()));
    }

    [Fact]
    public async Task ACoordinatedHistoricalTokenTargetForgeryStillFailsTheOriginalLogAuditWithoutWrites()
    {
        var initial = WithReturn(AttackDamageDistributionTests.Spell(Position(), "SANDCRAFT", "SFD·031/221", "P1"));
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await driver.Act("P1", new PlayCardCommand("SANDCRAFT", "SFD·031/221", [])); await driver.Top();
        var token = Assert.Single(driver.State.CardObjects.Values,
            c => c.TokenFactoryCardNo == P6TokenFactoryCatalog.SandSoldierTokenCardNo).ObjectId;
        var choice = driver.State.PendingCardChoice!;
        await driver.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [token]));
        await driver.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", [token])); await driver.Top();
        Assert.False(driver.State.CardObjects.ContainsKey(token));
        await BanishEffectPlayTests.Replay(initial, driver);
        var item = Assert.Single(driver.State.StackItems);
        var forged = driver.State with { StackItems = [item with { TargetObjectIds = ["invented-ceased-token"],
            TargetGenerations = new Dictionary<string, long> { ["invented-ceased-token"] = 0 } }] };
        // A historical token witness can be structurally valid. It cannot
        // replace the target chosen in the unchanged accepted action log.
        Assert.Empty(OfficialInsightAndSpellLockTests.Errors(forged));
        var events = driver.Events(); var commands = driver.Commands(); var sequence = events.Length;
        Assert.All(commands, c => Assert.Equal(driver.State.RuleInputs, c.RuleInputs));
        var snapshots = ResolutionResult.BuildSnapshots(forged); var prompts = ResolutionResult.BuildPrompts(forged);
        var views = forged.Seats.Keys.ToDictionary(id => id, id => new RecoveredPlayerView(id, forged.Tick, sequence,
            snapshots[id], forged.Tick, sequence, prompts[id]));
        var spectator = MatchReplayRedactor.BuildSpectatorFrame(forged.RoomId, forged.Tick, sequence, [], forged);
        var frame = new MatchRecoveryFrame(forged.RoomId, forged.Tick, sequence, commands, events, views, [], forged, initial, spectator)
            { OpeningSeed = initial.Seed };
        var log = JsonSerializer.Serialize(new { frame.Commands, frame.Events }); var writes = new Writes();
        var error = await Assert.ThrowsAsync<MatchSessionException>(async () =>
            await new InMemoryMatchSessionRegistry(new CoreRuleEngine(), writes, new Store(frame)).GetOrCreateAsync(frame.RoomId, default));
        Assert.Equal(ErrorCodes.RecoveryInconsistent, error.Code);
        Assert.Contains("action-log audit failed", error.Message, StringComparison.Ordinal);
        Assert.Equal(0, writes.Count);
        Assert.Equal(log, JsonSerializer.Serialize(new { frame.Commands, frame.Events }));
    }

    private sealed class Writes : IMatchJournal
    {
        internal int Count;
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken token)
        { Count++; return ValueTask.CompletedTask; }
    }

    private sealed class Store(MatchRecoveryFrame frame) : IMatchRecoveryStore
    {
        public ValueTask<MatchRecoveryFrame?> LoadAsync(string roomId, CancellationToken token)
            => ValueTask.FromResult<MatchRecoveryFrame?>(frame);
    }
}
