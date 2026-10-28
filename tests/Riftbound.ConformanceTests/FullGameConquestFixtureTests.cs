using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed partial class FullGameEndToEndTests
{
    // These bounded prefixes reuse the five original full-game setups and
    // consumer assertions. Their journal starts at the controlled midgame
    // fixture; it does not certify that fixture as an accepted opening history.
    [Theory]
    [InlineData("vayne")]
    [InlineData("zaun")]
    [InlineData("shirana")]
    [InlineData("moonveil")]
    [InlineData("minefield")]
    public async Task ControlledConquestFixturesChangeEnemyControlAndReplayConsumerPrefix(string source)
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var p1Deck = source switch
        {
            "vayne" => BuildVayneConquestPayReturnOfficialDeck(catalog),
            "zaun" => BuildBattlefieldConquerDiscardDrawOfficialDeck(catalog),
            "shirana" => BuildBattlefieldConquerConsumeBoonDrawOfficialDeck(catalog),
            "moonveil" => BuildBattlefieldConquerReadyEquipmentOfficialDeck(catalog),
            "minefield" => BuildBattlefieldConquerMillOfficialDeck(catalog),
            _ => throw new ArgumentOutOfRangeException(nameof(source))
        };
        var p2Deck = source == "vayne"
            ? BuildSourceLoneBattleStaticAuraDefenderOfficialDeck(catalog)
            : BuildRumbleFriendlyMechanicalStaticAuraDefenderOfficialDeck(catalog);
        var room = $"conquest-fixture-prefix-{source}";
        var opening = source switch
        {
            "vayne" => (await DriveOfficialLowCurveDecksToEstablishedBattlefieldsAsync(
                BuildSeatedInitialState(room, LowCurveReplaySeed), NoopMatchJournal.Instance, p1Deck, p2Deck)).Result,
            "zaun" => (await DriveOfficialDecksToBattlefieldConquerDiscardDrawOpeningAsync(room, p1Deck, p2Deck)).OpeningResult,
            "shirana" => (await DriveOfficialDecksToBattlefieldConquerConsumeBoonDrawOpeningAsync(room, p1Deck, p2Deck)).OpeningResult,
            "moonveil" => (await DriveOfficialDecksToBattlefieldConquerReadyEquipmentOpeningAsync(room, p1Deck, p2Deck)).OpeningResult,
            "minefield" => (await DriveOfficialDecksToBattlefieldConquerMillOpeningAsync(room, p1Deck, p2Deck)).OpeningResult,
            _ => throw new ArgumentOutOfRangeException(nameof(source))
        };
        var initialState = source switch
        {
            "vayne" => BuildVayneConquestPayReturnBattleInitialState(opening.State),
            "zaun" => BuildBattlefieldConquerDiscardDrawMidgameInitialState(opening.State),
            "shirana" => BuildBattlefieldConquerConsumeBoonDrawMidgameInitialState(opening.State),
            "moonveil" => BuildBattlefieldConquerReadyEquipmentMidgameInitialState(opening.State),
            "minefield" => BuildBattlefieldConquerMillMidgameInitialState(opening.State),
            _ => throw new ArgumentOutOfRangeException(nameof(source))
        };
        var battlefieldId = BattlefieldObjectIdForPlayer(initialState, "P1");
        var previousController = initialState.CardObjects[battlefieldId].ControllerId;
        var journal = new RecordingMatchJournal();
        var current = AcceptedCurrentResult(initialState);
        RecordConquestFixturePrefix(source, "initial", current);
        Assert.Empty(OfficialInsightAndSpellLockTests.Errors(initialState));
        var deserialized = JsonSerializer.Deserialize<MatchState>(JsonSerializer.Serialize(initialState))!;
        RecordConquestFixturePrefix(source, "initial-cold", current with { State = deserialized });
        var restored = OfficialInsightAndSpellLockTests.Restore(initialState);
        Assert.Equal(MatchStateHasher.Hash(initialState), MatchStateHasher.Hash(restored));
        var session = new MatchSession(restored, new CoreRuleEngine(), journal);
        current = AcceptedCurrentResult(restored);
        var triggered = source switch
        {
            "vayne" => await SubmitVayneConquestReturnDeclareBattleAsync(session, current, "P1", room),
            "zaun" => await SubmitBattlefieldConquerDiscardDrawDeclareBattleAsync(session, current, "P1", room),
            "shirana" => await SubmitBattlefieldConquerConsumeBoonDrawDeclareBattleAsync(session, current, "P1", room),
            "moonveil" => await SubmitBattlefieldConquerReadyEquipmentDeclareBattleAsync(session, current, "P1", room),
            "minefield" => await SubmitBattlefieldConquerMillDeclareBattleAsync(session, current, "P1", room),
            _ => throw new ArgumentOutOfRangeException(nameof(source))
        };
        RecordConquestFixturePrefix(source, "triggered", triggered);
        switch (source)
        {
            case "vayne":
                AssertVayneConquestReturnPaymentOpened(triggered);
                var paid = await PayVayneConquestReturnPaymentAsync(session, triggered, "P1", room + "-pay");
                RecordConquestFixturePrefix(source, "paid", paid);
                AssertVayneConquestReturnPaid(triggered, paid);
                triggered = paid;
                break;
            case "zaun": AssertBattlefieldConquerDiscardDrawResolved(current, triggered); break;
            case "shirana": AssertBattlefieldConquerConsumeBoonDrawResolved(current, triggered); break;
            case "moonveil": AssertBattlefieldConquerReadyEquipmentResolved(current, triggered); break;
            case "minefield":
                AssertBattlefieldConquerMillPending(current, triggered);
                triggered = await ResolveDriverPendingEffectsAsync(session, triggered, room + "-mill-response");
                RecordConquestFixturePrefix(source, "responded", triggered);
                AssertBattlefieldConquerMillResolved(current, triggered);
                break;
        }

        Assert.Equal("P2", previousController);
        Assert.Equal("P1", triggered.State.CardObjects[battlefieldId].ControllerId);
        Assert.True(triggered.State.PlayerScores["P1"] > initialState.PlayerScores["P1"]);
        AssertNoHiddenZoneLeak(triggered);
        OfficialInsightAndSpellLockTests.Restore(triggered.State);
        await AssertActionLogReplaysToFinalStateHashOnlyAsync(initialState, journal, triggered);
        RecordConquestFixturePrefix(source, "replayed", triggered, journal, replayVerified: true);
    }

    private static void RecordConquestFixturePrefix(string source, string stage, ResolutionResult result,
        RecordingMatchJournal? journal = null, bool replayVerified = false)
    {
        var directory = Environment.GetEnvironmentVariable("RIFTBOUND_CONQUEST_FIXTURE_EVIDENCE");
        if (directory is null) return;
        var output = Path.Combine(directory, source);
        Directory.CreateDirectory(output);
        File.WriteAllText(Path.Combine(output, stage + ".json"), JsonSerializer.Serialize(new
        {
            result.Accepted,
            result.State,
            result.Events,
            ComponentErrors = OfficialInsightAndSpellLockTests.Errors(result.State),
            RecoveredCommands = journal?.Entries.Select(ToRecoveredCommand).ToArray(),
            RecoveredEvents = journal is null ? null : ToRecoveredEvents(journal.Entries),
            PrefixReplayVerified = replayVerified
        }));
    }
}
