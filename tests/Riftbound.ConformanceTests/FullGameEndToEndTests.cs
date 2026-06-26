using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class FullGameEndToEndTests
{
    private const string JhinLegendCardNo = "UNL-181/219";
    private const string JhinChampionCardNo = "UNL-022/219";
    private const string RumbleLegendCardNo = "SFD·181/221";
    private const string RumbleChampionCardNo = "SFD·026/221";
    private const string PoppyLegendCardNo = "UNL-203/219";
    private const string PoppyChampionCardNo = "UNL-116/219";
    private const string LilliaLegendCardNo = "UNL-189/219";
    private const string LilliaChampionCardNo = "UNL-082/219";
    private const string MutantKittenCardNo = "UNL-036/219";
    private const string LeblancCardNo = "UNL-090/219";
    private const string VexLegendCardNo = "UNL-232/219";
    private const string VexChampionCardNo = "UNL-055/219";
    private const string ShadowCardNo = "UNL-194/219";
    private const string CrimsonSignetTreantCardNo = "UNL-029/219";
    private const string BaronNashorOtherFriendlyStaticAuraCardNo = "UNL-147/219";
    private const string ScarletPigeonSourceCombatStaticAuraCardNo = "UNL-154/219";
    private const string ReliableSiegeDogSourceSameLocationStaticAuraCardNo = "SFD·159/221";
    private const string SettSameBattlefieldBoonCountStaticAuraCardNo = "OGN·240/298";
    private const string LeeSinSameBattlefieldOtherFriendlyFilteredStaticAuraCardNo = "OGN·151/298";
    private const string ArenaRookieGrantBoonCardNo = "OGN·136/298";
    private const string GarenSameBattlefieldStaticAuraCardNo = "OGS·013/024";
    private const string FarronCaptainSameBattlefieldStaticKeywordCardNo = "OGN·015/298";
    private const string TaricSameBattlefieldStaticKeywordCardNo = "OGN·074/298";
    private const string WildclawBeastmasterCardNo = "UNL-057/219";
    private const string AscendedBelieverCardNo = "UNL-004/219";
    private const string DemaciaEnvoyCardNo = "UNL-092/219";
    private const string ForgottenMonumentBattlefieldCardNo = "SFD·209/221";
    private const string WinningScoreIncreaseBattlefieldCardNo = "OGN·276/298";
    private const string BandleTreeBattlefieldCardNo = "OGN·278/298";
    private const string FirstTurnExtraRuneBattlefieldCardNo = "OGN·284/298";
    private const string TrifarianTrainingGroundsBattlefieldAllUnitsStaticAuraCardNo = "OGN·294/298";
    private const string HasteReadyOptionalCost = "HASTE_READY";
    private const string TeemoSelfPowerCardNo = "OGN·197/298";
    private const string PakaaCubCardNo = "OGN·135/298";
    private const long LowCurveReplaySeed = 424242;
    private static readonly int[] BattlefieldAllUnitsStaticAuraDriverSeeds =
    [
        2, 3, 4, 10, 11, 15, 20, 21, 23, 24, 26, 28, 29, 35, 36, 42, 46, 49, 51, 52,
        60, 68, 69, 70, 73, 79, 80, 81, 86, 87, 90, 92, 93, 99, 100, 101, 109, 112, 116, 119
    ];
    private static readonly int[] OtherFriendlyStaticAuraDriverSeeds = [7, 11, 17, 23, 31, 42, 101, 404, 20260624, 424242];
    private static readonly int[] ShadowResponseDriverSeeds = [7, 11, 17, 23, 31, 42, 101, 404, 20260624, 424242];
    private static readonly int[] StandbyDriverSeeds = [424242, 7, 11, 17, 23, 31, 42, 101, 404, 20260624];

    [Fact]
    public async Task OfficialLowCurveDecksSkipNoLegalBattleAndReachMatchResultThroughServerPrompts()
    {
        var (session, result) = await DriveOfficialLowCurveDecksToNoLegalBattleSkipAsync(
            "b0-full-game-official-low-curve-room");

        var winner = OpponentOf(result.State, result.State.ActivePlayerId);
        var surrender = await session.SubmitAsync(
            result.State.ActivePlayerId,
            "b0-surrender-after-battle",
            new SurrenderCommand(),
            RawCommand(CommandTypes.Surrender),
            CancellationToken.None);
        AssertAccepted(surrender);
        Assert.Equal(MatchStatuses.Finished, surrender.State.Status);
        Assert.Equal(winner, surrender.State.WinnerPlayerId);
        Assert.Contains(surrender.Events, gameEvent => string.Equals(gameEvent.Kind, "MATCH_WON", StringComparison.Ordinal));
        AssertNoHiddenZoneLeak(surrender);
    }

    [Fact]
    public async Task OfficialLowCurveDecksReopenContestedBattleAfterSkippedCombatantsReadyAcrossTurns()
    {
        var (_, battleReady, battleResult) = await DriveOfficialLowCurveDecksToBattleCloseAsync(
            "b0-full-game-official-low-curve-reopen-room");

        AssertNoHiddenZoneLeak(battleResult);
        Assert.Contains(battleResult.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLE_DECLARED", StringComparison.Ordinal));
        Assert.Contains(battleResult.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLE_CLOSED", StringComparison.Ordinal));
        Assert.DoesNotContain(battleResult.State.PendingTaskQueue.Tasks, task =>
            string.Equals(task.Kind, "START_BATTLE", StringComparison.Ordinal)
            && string.Equals(task.BattlefieldObjectId, battleReady.State.PendingTaskQueue.Tasks.Single(activeTask =>
                string.Equals(activeTask.TaskId, battleReady.State.PendingTaskQueue.ActiveTaskId, StringComparison.Ordinal)).BattlefieldObjectId, StringComparison.Ordinal));
    }

    [Fact]
    public async Task OfficialLowCurveDecksReachScoreVictoryAfterRealBattleThroughServerPrompts()
    {
        var (session, _, battleResult) = await DriveOfficialLowCurveDecksToBattleCloseAsync(
            "b0-full-game-official-low-curve-score-room");

        var result = await DriveBattleCloseToScoreVictoryAsync(
            session,
            battleResult,
            "b0-score");

        AssertScoreVictory(result);
    }

    [Fact]
    public async Task DistinctOfficialLowCurveDecksReachScoreVictoryAfterRealBattleThroughServerPrompts()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var p1Deck = BuildLowCurveOfficialDeck(catalog, JhinLegendCardNo, JhinChampionCardNo);
        var p2Deck = BuildLowCurveOfficialDeck(catalog, RumbleLegendCardNo, RumbleChampionCardNo);
        Assert.NotEqual(p1Deck.LegendCardNo, p2Deck.LegendCardNo);
        Assert.NotEqual(p1Deck.ChampionCardNo, p2Deck.ChampionCardNo);

        var (session, _, battleResult) = await DriveOfficialLowCurveDecksToBattleCloseAsync(
            "b0-full-game-distinct-low-curve-score-room",
            p1Deck,
            p2Deck);

        var result = await DriveBattleCloseToScoreVictoryAsync(
            session,
            battleResult,
            "b0-distinct-score");

        AssertScoreVictory(result);
    }

    [Fact]
    public async Task OfficialLowCurvePostBattleScoreVictoryActionLogReplaysToFinalStateHash()
    {
        var (_, _, battleResult) = await DriveOfficialLowCurveDecksToBattleCloseAsync(
            "b0-full-game-official-low-curve-score-room");
        var initialState = battleResult.State;
        var journal = new RecordingMatchJournal();
        var session = new MatchSession(initialState, new CoreRuleEngine(), journal);

        var result = await DriveBattleCloseToScoreVictoryAsync(
            session,
            battleResult,
            "b0-replay-score");

        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(
            initialState,
            journal.Entries.Select(ToRecoveredCommand).ToArray(),
            result.State,
            new CoreRuleEngine(),
            CancellationToken.None,
            ToRecoveredEvents(journal.Entries));

        Assert.True(replay.IsMatch, string.Join("; ", replay.Errors));
        Assert.Equal(MatchStateHasher.Hash(result.State), replay.ExpectedStateHash);
        Assert.Equal(replay.ExpectedStateHash, replay.ReplayedStateHash);
        Assert.Empty(replay.Errors);
        Assert.Contains(journal.Entries, entry => string.Equals(entry.CommandType, CommandTypes.EndTurn, StringComparison.Ordinal));
        AssertScoreVictory(result);
    }

    [Fact]
    public async Task OfficialLowCurveFullGameScoreVictoryActionLogReplaysToFinalStateHash()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var deck = BuildLowCurveOfficialDeck(catalog);

        await AssertFullGameScoreVictoryActionLogReplaysToFinalStateHashAsync(
            "b0-full-game-official-low-curve-replay-room",
            "b0-full-replay-score",
            deck,
            deck);
    }

    [Fact]
    public async Task DistinctOfficialLowCurveFullGameScoreVictoryActionLogReplaysToFinalStateHash()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var p1Deck = BuildLowCurveOfficialDeck(catalog, JhinLegendCardNo, JhinChampionCardNo);
        var p2Deck = BuildLowCurveOfficialDeck(catalog, RumbleLegendCardNo, RumbleChampionCardNo);
        Assert.NotEqual(p1Deck.LegendCardNo, p2Deck.LegendCardNo);
        Assert.NotEqual(p1Deck.ChampionCardNo, p2Deck.ChampionCardNo);

        await AssertFullGameScoreVictoryActionLogReplaysToFinalStateHashAsync(
            "b0-full-game-distinct-low-curve-replay-room",
            "b0-full-distinct-replay-score",
            p1Deck,
            p2Deck);
    }

    [Fact]
    public async Task OfficialDeckMidgameResolvesCrimsonSignetTreantConquestRepeatAndScoreVictoryActionLogReplaysToFinalStateHash()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var p1Deck = BuildCrimsonSignetTreantOfficialDeck(catalog);
        var p2Deck = BuildSlowBattlefieldLowCurveOfficialDeck(catalog, RumbleLegendCardNo, RumbleChampionCardNo);
        var openingInitialState = BuildSeatedInitialState("b0-full-game-treant-conquest-replay-room", LowCurveReplaySeed);
        var (_, openingResult) = await DriveOfficialLowCurveDecksToNoLegalBattleSkipAsync(
            openingInitialState,
            NoopMatchJournal.Instance,
            p1Deck,
            p2Deck);
        var initialState = BuildCrimsonSignetTreantMidgameInitialState(openingResult.State);
        var journal = new RecordingMatchJournal();
        var session = new MatchSession(initialState, new CoreRuleEngine(), journal);
        var current = AcceptedCurrentResult(initialState);
        current = await DriveSpecificUnitToOwnBattlefieldAsync(
            session,
            current,
            "P1",
            CrimsonSignetTreantCardNo,
            "b0-treant-stage-source");
        current = await DriveOpponentUnitToBattlefieldAsync(
            session,
            current,
            "P2",
            "P1",
            "b0-treant-stage-defender");

        var battleResult = await DriveContestedBattlefieldToCrimsonSignetTreantConquestAsync(
            session,
            current,
            "P1",
            "b0-treant-conquest");

        AssertCrimsonSignetTreantConquestRepeat(battleResult);

        var result = await DriveBattleCloseToScoreVictoryAsync(
            session,
            battleResult,
            "b0-treant-score");

        await AssertActionLogReplaysToFinalStateHashOnlyAsync(initialState, journal, result);
        Assert.Contains(journal.Entries, entry => string.Equals(entry.CommandType, CommandTypes.PlayCard, StringComparison.Ordinal));
        Assert.Contains(journal.Entries, entry => string.Equals(entry.CommandType, CommandTypes.MoveUnit, StringComparison.Ordinal));
        Assert.Contains(journal.Entries, entry => string.Equals(entry.CommandType, CommandTypes.DeclareBattle, StringComparison.Ordinal));
        Assert.Contains(journal.Entries, entry => string.Equals(entry.CommandType, CommandTypes.EndTurn, StringComparison.Ordinal));
        AssertScoreVictory(result);
    }

    [Fact]
    public async Task OfficialDeckMidgameAppliesSameBattlefieldStaticAuraAndScoreVictoryActionLogReplaysToFinalStateHash()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var p1Deck = BuildSameBattlefieldStaticAuraOfficialDeck(catalog);
        var p2Deck = BuildSlowBattlefieldLowCurveOfficialDeck(catalog, RumbleLegendCardNo, RumbleChampionCardNo);
        var openingInitialState = BuildSeatedInitialState("b0-full-game-same-battlefield-static-aura-replay-room", LowCurveReplaySeed);
        var (_, openingResult) = await DriveOfficialLowCurveDecksToNoLegalBattleSkipAsync(
            openingInitialState,
            NoopMatchJournal.Instance,
            p1Deck,
            p2Deck);
        var initialState = BuildSameBattlefieldStaticAuraMidgameInitialState(openingResult.State);
        var journal = new RecordingMatchJournal();
        var session = new MatchSession(initialState, new CoreRuleEngine(), journal);
        var current = AcceptedCurrentResult(initialState);
        current = await DriveSpecificUnitToOwnBattlefieldAsync(
            session,
            current,
            "P1",
            GarenSameBattlefieldStaticAuraCardNo,
            "b0-same-battlefield-aura-stage-source");
        current = await DriveSpecificUnitToOwnBattlefieldAsync(
            session,
            current,
            "P1",
            DemaciaEnvoyCardNo,
            "b0-same-battlefield-aura-stage-ally");
        current = await DriveOpponentUnitToBattlefieldAsync(
            session,
            current,
            "P2",
            "P1",
            "b0-same-battlefield-aura-stage-defender");

        var battleResult = await DriveContestedBattlefieldToSameBattlefieldStaticAuraBattleAsync(
            session,
            current,
            "P1",
            "b0-same-battlefield-aura-battle");

        AssertSameBattlefieldStaticAuraDamage(battleResult);

        var result = await DriveBattleCloseToScoreVictoryAsync(
            session,
            battleResult,
            "b0-same-battlefield-aura-score");

        await AssertActionLogReplaysToFinalStateHashOnlyAsync(initialState, journal, result);
        Assert.Contains(journal.Entries, entry => string.Equals(entry.CommandType, CommandTypes.PlayCard, StringComparison.Ordinal));
        Assert.Contains(journal.Entries, entry => string.Equals(entry.CommandType, CommandTypes.MoveUnit, StringComparison.Ordinal));
        Assert.Contains(journal.Entries, entry => string.Equals(entry.CommandType, CommandTypes.DeclareBattle, StringComparison.Ordinal));
        Assert.Contains(journal.Entries, entry => string.Equals(entry.CommandType, CommandTypes.EndTurn, StringComparison.Ordinal));
        AssertScoreVictory(result);
    }

    [Fact]
    public async Task OfficialDeckMidgameAppliesBattlefieldAllUnitsStaticAuraAndScoreVictoryActionLogReplaysToFinalStateHash()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var p1Deck = BuildBattlefieldAllUnitsStaticAuraOfficialDeck(catalog);
        var p2Deck = BuildSlowBattlefieldLowCurveOfficialDeck(catalog, RumbleLegendCardNo, RumbleChampionCardNo);
        var (openingInitialState, openingResult) = await DriveOfficialDecksToBattlefieldAllUnitsStaticAuraOpeningAsync(
            "b0-full-game-battlefield-all-units-static-aura-replay-room",
            p1Deck,
            p2Deck);
        var initialState = BuildBattlefieldAllUnitsStaticAuraMidgameInitialState(openingResult.State);
        var journal = new RecordingMatchJournal();
        var session = new MatchSession(initialState, new CoreRuleEngine(), journal);
        var current = AcceptedCurrentResult(initialState);
        current = await DriveSpecificUnitToPlayerBattlefieldAsync(
            session,
            current,
            "P1",
            WildclawBeastmasterCardNo,
            "P1",
            "b0-battlefield-all-units-aura-stage-attacker",
            TrifarianTrainingGroundsBattlefieldAllUnitsStaticAuraCardNo);
        current = await DriveOpponentUnitToBattlefieldAsync(
            session,
            current,
            "P2",
            "P1",
            "b0-battlefield-all-units-aura-stage-defender",
            TrifarianTrainingGroundsBattlefieldAllUnitsStaticAuraCardNo);

        var battleResult = await DriveContestedBattlefieldToBattlefieldAllUnitsStaticAuraBattleAsync(
            session,
            current,
            "P1",
            "b0-battlefield-all-units-aura-battle");

        AssertBattlefieldAllUnitsStaticAuraDamage(battleResult);

        var result = await DriveBattleCloseToScoreVictoryAsync(
            session,
            battleResult,
            "b0-battlefield-all-units-aura-score");

        await AssertActionLogReplaysToFinalStateHashOnlyAsync(initialState, journal, result);
        Assert.Contains(journal.Entries, entry => string.Equals(entry.CommandType, CommandTypes.PlayCard, StringComparison.Ordinal));
        Assert.Contains(journal.Entries, entry => string.Equals(entry.CommandType, CommandTypes.MoveUnit, StringComparison.Ordinal));
        Assert.Contains(journal.Entries, entry => string.Equals(entry.CommandType, CommandTypes.DeclareBattle, StringComparison.Ordinal));
        Assert.Contains(journal.Entries, entry => string.Equals(entry.CommandType, CommandTypes.EndTurn, StringComparison.Ordinal));
        AssertScoreVictory(result);
    }

    [Fact]
    public async Task OfficialDeckMidgameAppliesSourceSameLocationStaticAuraAndScoreVictoryActionLogReplaysToFinalStateHash()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var p1Deck = BuildSourceSameLocationStaticAuraOfficialDeck(catalog);
        var p2Deck = BuildSlowBattlefieldLowCurveOfficialDeck(catalog, RumbleLegendCardNo, RumbleChampionCardNo);
        var openingInitialState = BuildSeatedInitialState("b0-full-game-source-same-location-static-aura-replay-room", LowCurveReplaySeed);
        var (_, openingResult) = await DriveOfficialLowCurveDecksToNoLegalBattleSkipAsync(
            openingInitialState,
            NoopMatchJournal.Instance,
            p1Deck,
            p2Deck);
        var initialState = BuildSourceSameLocationStaticAuraMidgameInitialState(openingResult.State);
        var journal = new RecordingMatchJournal();
        var session = new MatchSession(initialState, new CoreRuleEngine(), journal);
        var current = AcceptedCurrentResult(initialState);
        current = await DriveSpecificUnitToOwnBattlefieldAsync(
            session,
            current,
            "P1",
            ReliableSiegeDogSourceSameLocationStaticAuraCardNo,
            "b0-source-same-location-aura-stage-source");
        current = await DriveSpecificUnitToOwnBattlefieldAsync(
            session,
            current,
            "P1",
            DemaciaEnvoyCardNo,
            "b0-source-same-location-aura-stage-ally");
        current = await DriveOpponentUnitToBattlefieldAsync(
            session,
            current,
            "P2",
            "P1",
            "b0-source-same-location-aura-stage-defender");

        var battleResult = await DriveContestedBattlefieldToSourceSameLocationStaticAuraBattleAsync(
            session,
            current,
            "P1",
            "b0-source-same-location-aura-battle");

        AssertSourceSameLocationStaticAuraDamage(battleResult);

        var result = await DriveBattleCloseToScoreVictoryAsync(
            session,
            battleResult,
            "b0-source-same-location-aura-score");

        await AssertActionLogReplaysToFinalStateHashOnlyAsync(initialState, journal, result);
        Assert.Contains(journal.Entries, entry => string.Equals(entry.CommandType, CommandTypes.PlayCard, StringComparison.Ordinal));
        Assert.Contains(journal.Entries, entry => string.Equals(entry.CommandType, CommandTypes.MoveUnit, StringComparison.Ordinal));
        Assert.Contains(journal.Entries, entry => string.Equals(entry.CommandType, CommandTypes.DeclareBattle, StringComparison.Ordinal));
        Assert.Contains(journal.Entries, entry => string.Equals(entry.CommandType, CommandTypes.EndTurn, StringComparison.Ordinal));
        AssertScoreVictory(result);
    }

    [Fact]
    public async Task OfficialDeckMidgameAppliesSameBattlefieldBoonCountStaticAuraAndScoreVictoryActionLogReplaysToFinalStateHash()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var p1Deck = BuildSameBattlefieldBoonCountStaticAuraOfficialDeck(catalog);
        var p2Deck = BuildSlowBattlefieldLowCurveOfficialDeck(catalog, RumbleLegendCardNo, RumbleChampionCardNo);
        var openingInitialState = BuildSeatedInitialState("b0-full-game-same-battlefield-boon-count-static-aura-replay-room", LowCurveReplaySeed);
        var (_, openingResult) = await DriveOfficialLowCurveDecksToNoLegalBattleSkipAsync(
            openingInitialState,
            NoopMatchJournal.Instance,
            p1Deck,
            p2Deck);
        var initialState = BuildSameBattlefieldBoonCountStaticAuraMidgameInitialState(openingResult.State);
        var journal = new RecordingMatchJournal();
        var session = new MatchSession(initialState, new CoreRuleEngine(), journal);
        var current = AcceptedCurrentResult(initialState);
        current = await DriveSpecificUnitToOwnBattlefieldAsync(
            session,
            current,
            "P1",
            DemaciaEnvoyCardNo,
            "b0-same-battlefield-boon-count-aura-stage-boon-target");
        current = await DriveSpecificUnitToOwnBaseGrantingBoonToBattlefieldUnitAsync(
            session,
            current,
            "P1",
            ArenaRookieGrantBoonCardNo,
            DemaciaEnvoyCardNo,
            "b0-same-battlefield-boon-count-aura-grant-boon");
        current = await DriveSpecificUnitToOwnBattlefieldAsync(
            session,
            current,
            "P1",
            SettSameBattlefieldBoonCountStaticAuraCardNo,
            "b0-same-battlefield-boon-count-aura-stage-source");
        current = await DriveOpponentUnitToBattlefieldAsync(
            session,
            current,
            "P2",
            "P1",
            "b0-same-battlefield-boon-count-aura-stage-defender");

        var battleResult = await DriveContestedBattlefieldToSameBattlefieldBoonCountStaticAuraBattleAsync(
            session,
            current,
            "P1",
            "b0-same-battlefield-boon-count-aura-battle");

        AssertSameBattlefieldBoonCountStaticAuraDamage(battleResult);

        var result = await DriveBattleCloseToScoreVictoryAsync(
            session,
            battleResult,
            "b0-same-battlefield-boon-count-aura-score");

        await AssertActionLogReplaysToFinalStateHashOnlyAsync(initialState, journal, result);
        Assert.Contains(journal.Entries, entry => string.Equals(entry.CommandType, CommandTypes.PlayCard, StringComparison.Ordinal));
        Assert.Contains(journal.Entries, entry => string.Equals(entry.CommandType, CommandTypes.MoveUnit, StringComparison.Ordinal));
        Assert.Contains(journal.Entries, entry => string.Equals(entry.CommandType, CommandTypes.DeclareBattle, StringComparison.Ordinal));
        Assert.Contains(journal.Entries, entry => string.Equals(entry.CommandType, CommandTypes.EndTurn, StringComparison.Ordinal));
        AssertScoreVictory(result);
    }

    [Fact]
    public async Task OfficialDeckMidgameAppliesSameBattlefieldOtherFriendlyFilteredStaticAuraAndScoreVictoryActionLogReplaysToFinalStateHash()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var p1Deck = BuildSameBattlefieldOtherFriendlyFilteredStaticAuraOfficialDeck(catalog);
        var p2Deck = BuildSlowBattlefieldLowCurveOfficialDeck(catalog, RumbleLegendCardNo, RumbleChampionCardNo);
        var openingInitialState = BuildSeatedInitialState("b0-full-game-same-battlefield-other-friendly-filtered-static-aura-replay-room", LowCurveReplaySeed);
        var (_, openingResult) = await DriveOfficialLowCurveDecksToNoLegalBattleSkipAsync(
            openingInitialState,
            NoopMatchJournal.Instance,
            p1Deck,
            p2Deck);
        var initialState = BuildSameBattlefieldOtherFriendlyFilteredStaticAuraMidgameInitialState(openingResult.State);
        var journal = new RecordingMatchJournal();
        var session = new MatchSession(initialState, new CoreRuleEngine(), journal);
        var current = AcceptedCurrentResult(initialState);
        current = await DriveSpecificUnitToOwnBattlefieldAsync(
            session,
            current,
            "P1",
            DemaciaEnvoyCardNo,
            "b0-same-battlefield-other-friendly-filtered-aura-stage-target");
        current = await DriveSpecificUnitToOwnBaseGrantingBoonToBattlefieldUnitAsync(
            session,
            current,
            "P1",
            ArenaRookieGrantBoonCardNo,
            DemaciaEnvoyCardNo,
            "b0-same-battlefield-other-friendly-filtered-aura-grant-boon");
        current = await DriveSpecificUnitToOwnBattlefieldAsync(
            session,
            current,
            "P1",
            LeeSinSameBattlefieldOtherFriendlyFilteredStaticAuraCardNo,
            "b0-same-battlefield-other-friendly-filtered-aura-stage-source");
        current = await DriveOpponentUnitToBattlefieldAsync(
            session,
            current,
            "P2",
            "P1",
            "b0-same-battlefield-other-friendly-filtered-aura-stage-defender");

        var battleResult = await DriveContestedBattlefieldToSameBattlefieldOtherFriendlyFilteredStaticAuraBattleAsync(
            session,
            current,
            "P1",
            "b0-same-battlefield-other-friendly-filtered-aura-battle");

        AssertSameBattlefieldOtherFriendlyFilteredStaticAuraDamage(battleResult);

        var result = await DriveBattleCloseToScoreVictoryAsync(
            session,
            battleResult,
            "b0-same-battlefield-other-friendly-filtered-aura-score");

        await AssertActionLogReplaysToFinalStateHashOnlyAsync(initialState, journal, result);
        Assert.Contains(journal.Entries, entry => string.Equals(entry.CommandType, CommandTypes.PlayCard, StringComparison.Ordinal));
        Assert.Contains(journal.Entries, entry => string.Equals(entry.CommandType, CommandTypes.MoveUnit, StringComparison.Ordinal));
        Assert.Contains(journal.Entries, entry => string.Equals(entry.CommandType, CommandTypes.DeclareBattle, StringComparison.Ordinal));
        Assert.Contains(journal.Entries, entry => string.Equals(entry.CommandType, CommandTypes.EndTurn, StringComparison.Ordinal));
        AssertScoreVictory(result);
    }

    [Fact]
    public async Task OfficialDeckMidgameAppliesOtherFriendlyStaticAuraAndScoreVictoryActionLogReplaysToFinalStateHash()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var p1Deck = BuildOtherFriendlyStaticAuraOfficialDeck(catalog);
        var p2Deck = BuildSlowBattlefieldLowCurveOfficialDeck(catalog, RumbleLegendCardNo, RumbleChampionCardNo);
        var (openingInitialState, openingResult) = await DriveOfficialDecksToOtherFriendlyStaticAuraOpeningAsync(
            "b0-full-game-other-friendly-static-aura-replay-room",
            p1Deck,
            p2Deck);
        var initialState = BuildOtherFriendlyStaticAuraMidgameInitialState(openingResult.State);
        var journal = new RecordingMatchJournal();
        var session = new MatchSession(initialState, new CoreRuleEngine(), journal);
        var current = AcceptedCurrentResult(initialState);
        current = await DriveSpecificUnitToOwnBaseAsync(
            session,
            current,
            "P1",
            BaronNashorOtherFriendlyStaticAuraCardNo,
            "b0-other-friendly-aura-stage-source");
        current = await DriveSpecificUnitToOwnBattlefieldAsync(
            session,
            current,
            "P1",
            WildclawBeastmasterCardNo,
            "b0-other-friendly-aura-stage-ally");
        current = await DriveOpponentUnitToBattlefieldAsync(
            session,
            current,
            "P2",
            "P1",
            "b0-other-friendly-aura-stage-defender");

        var battleResult = await DriveContestedBattlefieldToOtherFriendlyStaticAuraBattleAsync(
            session,
            current,
            "P1",
            "b0-other-friendly-aura-battle");

        AssertOtherFriendlyStaticAuraDamage(battleResult);

        var result = await DriveBattleCloseToScoreVictoryAsync(
            session,
            battleResult,
            "b0-other-friendly-aura-score");

        await AssertActionLogReplaysToFinalStateHashOnlyAsync(initialState, journal, result);
        Assert.Contains(journal.Entries, entry => string.Equals(entry.CommandType, CommandTypes.PlayCard, StringComparison.Ordinal));
        Assert.Contains(journal.Entries, entry => string.Equals(entry.CommandType, CommandTypes.MoveUnit, StringComparison.Ordinal));
        Assert.Contains(journal.Entries, entry => string.Equals(entry.CommandType, CommandTypes.DeclareBattle, StringComparison.Ordinal));
        Assert.Contains(journal.Entries, entry => string.Equals(entry.CommandType, CommandTypes.EndTurn, StringComparison.Ordinal));
        AssertScoreVictory(result);
    }

    [Fact]
    public async Task OfficialDeckMidgameAppliesSourceCombatStaticAuraAndScoreVictoryActionLogReplaysToFinalStateHash()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var p1Deck = BuildSourceCombatStaticAuraOfficialDeck(catalog);
        var p2Deck = BuildSlowBattlefieldLowCurveOfficialDeck(catalog, RumbleLegendCardNo, RumbleChampionCardNo);
        var openingInitialState = BuildSeatedInitialState("b0-full-game-source-combat-static-aura-replay-room", LowCurveReplaySeed);
        var (_, openingResult) = await DriveOfficialLowCurveDecksToNoLegalBattleSkipAsync(
            openingInitialState,
            NoopMatchJournal.Instance,
            p1Deck,
            p2Deck);
        var initialState = BuildSourceCombatStaticAuraMidgameInitialState(openingResult.State);
        var journal = new RecordingMatchJournal();
        var session = new MatchSession(initialState, new CoreRuleEngine(), journal);
        var current = AcceptedCurrentResult(initialState);
        current = await DriveSpecificUnitToOwnBattlefieldAsync(
            session,
            current,
            "P1",
            ScarletPigeonSourceCombatStaticAuraCardNo,
            "b0-source-combat-aura-stage-source");
        current = await DriveSpecificUnitToOwnBattlefieldAsync(
            session,
            current,
            "P1",
            DemaciaEnvoyCardNo,
            "b0-source-combat-aura-stage-ally");
        current = await DriveOpponentUnitToBattlefieldAsync(
            session,
            current,
            "P2",
            "P1",
            "b0-source-combat-aura-stage-defender");

        var battleResult = await DriveContestedBattlefieldToSourceCombatStaticAuraBattleAsync(
            session,
            current,
            "P1",
            "b0-source-combat-aura-battle");

        AssertSourceCombatStaticAuraDamage(battleResult);

        var result = await DriveBattleCloseToScoreVictoryAsync(
            session,
            battleResult,
            "b0-source-combat-aura-score");

        await AssertActionLogReplaysToFinalStateHashOnlyAsync(initialState, journal, result);
        Assert.Contains(journal.Entries, entry => string.Equals(entry.CommandType, CommandTypes.PlayCard, StringComparison.Ordinal));
        Assert.Contains(journal.Entries, entry => string.Equals(entry.CommandType, CommandTypes.MoveUnit, StringComparison.Ordinal));
        Assert.Contains(journal.Entries, entry => string.Equals(entry.CommandType, CommandTypes.DeclareBattle, StringComparison.Ordinal));
        Assert.Contains(journal.Entries, entry => string.Equals(entry.CommandType, CommandTypes.EndTurn, StringComparison.Ordinal));
        AssertScoreVictory(result);
    }

    [Fact]
    public async Task OfficialDeckMidgameAppliesSameBattlefieldStaticKeywordAndScoreVictoryActionLogReplaysToFinalStateHash()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var p1Deck = BuildSameBattlefieldStaticKeywordOfficialDeck(catalog);
        var p2Deck = BuildSlowBattlefieldLowCurveOfficialDeck(catalog, RumbleLegendCardNo, RumbleChampionCardNo);
        var openingInitialState = BuildSeatedInitialState("b0-full-game-same-battlefield-static-keyword-replay-room", LowCurveReplaySeed);
        var (_, openingResult) = await DriveOfficialLowCurveDecksToNoLegalBattleSkipAsync(
            openingInitialState,
            NoopMatchJournal.Instance,
            p1Deck,
            p2Deck);
        var initialState = BuildSameBattlefieldStaticKeywordMidgameInitialState(openingResult.State);
        var journal = new RecordingMatchJournal();
        var session = new MatchSession(initialState, new CoreRuleEngine(), journal);
        var current = AcceptedCurrentResult(initialState);
        current = await DriveSpecificUnitToOwnBattlefieldAsync(
            session,
            current,
            "P1",
            FarronCaptainSameBattlefieldStaticKeywordCardNo,
            "b0-same-battlefield-keyword-stage-source");
        current = await DriveSpecificUnitToOwnBattlefieldAsync(
            session,
            current,
            "P1",
            AscendedBelieverCardNo,
            "b0-same-battlefield-keyword-stage-ally");
        current = await DriveOpponentUnitToBattlefieldAsync(
            session,
            current,
            "P2",
            "P1",
            "b0-same-battlefield-keyword-stage-defender");

        var battleResult = await DriveContestedBattlefieldToSameBattlefieldStaticKeywordBattleAsync(
            session,
            current,
            "P1",
            "b0-same-battlefield-keyword-battle");

        AssertSameBattlefieldStaticKeywordDamage(battleResult);

        var result = await DriveBattleCloseToScoreVictoryAsync(
            session,
            battleResult,
            "b0-same-battlefield-keyword-score");

        await AssertActionLogReplaysToFinalStateHashOnlyAsync(initialState, journal, result);
        Assert.Contains(journal.Entries, entry => string.Equals(entry.CommandType, CommandTypes.PlayCard, StringComparison.Ordinal));
        Assert.Contains(journal.Entries, entry => string.Equals(entry.CommandType, CommandTypes.MoveUnit, StringComparison.Ordinal));
        Assert.Contains(journal.Entries, entry => string.Equals(entry.CommandType, CommandTypes.DeclareBattle, StringComparison.Ordinal));
        Assert.Contains(journal.Entries, entry => string.Equals(entry.CommandType, CommandTypes.EndTurn, StringComparison.Ordinal));
        AssertScoreVictory(result);
    }

    [Fact]
    public async Task OfficialDeckMidgameAppliesSameBattlefieldSteadfastStaticKeywordAndScoreVictoryActionLogReplaysToFinalStateHash()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var p1Deck = BuildSameBattlefieldSteadfastStaticKeywordOfficialDeck(catalog);
        var p2Deck = BuildSlowBattlefieldLowCurveOfficialDeck(catalog, RumbleLegendCardNo, RumbleChampionCardNo);
        var openingInitialState = BuildSeatedInitialState("b0-full-game-same-battlefield-steadfast-static-keyword-replay-room", LowCurveReplaySeed);
        var (_, openingResult) = await DriveOfficialLowCurveDecksToNoLegalBattleSkipAsync(
            openingInitialState,
            NoopMatchJournal.Instance,
            p1Deck,
            p2Deck);
        var initialState = BuildSameBattlefieldSteadfastStaticKeywordMidgameInitialState(openingResult.State);
        var journal = new RecordingMatchJournal();
        var session = new MatchSession(initialState, new CoreRuleEngine(), journal);
        var current = AcceptedCurrentResult(initialState);
        current = await DriveSpecificUnitToPlayerBattlefieldAsync(
            session,
            current,
            "P1",
            TaricSameBattlefieldStaticKeywordCardNo,
            "P2",
            "b0-same-battlefield-steadfast-stage-source");
        current = await DriveSpecificUnitToPlayerBattlefieldAsync(
            session,
            current,
            "P1",
            LeblancCardNo,
            "P2",
            "b0-same-battlefield-steadfast-stage-ally");
        current = await DriveOpponentUnitToBattlefieldAsync(
            session,
            current,
            "P2",
            "P2",
            "b0-same-battlefield-steadfast-stage-attacker");

        var battleResult = await DriveContestedBattlefieldToSameBattlefieldSteadfastBattleAsync(
            session,
            current,
            "P2",
            "P1",
            "b0-same-battlefield-steadfast-battle");

        AssertSameBattlefieldSteadfastStaticKeywordDamage(battleResult);

        var result = await DriveBattleCloseToScoreVictoryAsync(
            session,
            battleResult,
            "b0-same-battlefield-steadfast-score");

        await AssertActionLogReplaysToFinalStateHashOnlyAsync(initialState, journal, result);
        Assert.Contains(journal.Entries, entry => string.Equals(entry.CommandType, CommandTypes.PlayCard, StringComparison.Ordinal));
        Assert.Contains(journal.Entries, entry => string.Equals(entry.CommandType, CommandTypes.MoveUnit, StringComparison.Ordinal));
        Assert.Contains(journal.Entries, entry => string.Equals(entry.CommandType, CommandTypes.DeclareBattle, StringComparison.Ordinal));
        Assert.True(
            journal.Entries.Count(entry => string.Equals(entry.CommandType, CommandTypes.DeclareBattle, StringComparison.Ordinal)) >= 2,
            "Expected Taric's surviving contested battlefield to produce a follow-up server-authored battle declaration.");
        Assert.Contains(
            journal.Entries.SelectMany(entry => entry.Events),
            gameEvent => string.Equals(gameEvent.Kind, "BATTLE_SKIPPED", StringComparison.Ordinal)
                && gameEvent.Payload.TryGetValue("reason", out var reason)
                && string.Equals(reason as string, "NO_LEGAL_COMBATANTS", StringComparison.Ordinal));
        Assert.Contains(journal.Entries, entry => string.Equals(entry.CommandType, CommandTypes.EndTurn, StringComparison.Ordinal));
        AssertScoreVictory(result);
    }

    [Fact]
    public async Task OfficialDeckMidgameOrdersTaricBulwarkBeforeBackRowInDamageAssignmentAndScoreVictoryActionLogReplaysToFinalStateHash()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var p1Deck = BuildSameBattlefieldSteadfastStaticKeywordOfficialDeck(catalog);
        var p2Deck = BuildTaricBulwarkAssignmentAttackerOfficialDeck(catalog);
        var openingInitialState = BuildSeatedInitialState("b0-full-game-taric-bulwark-assignment-replay-room", LowCurveReplaySeed);
        var (_, openingResult) = await DriveOfficialLowCurveDecksToNoLegalBattleSkipAsync(
            openingInitialState,
            NoopMatchJournal.Instance,
            p1Deck,
            p2Deck);
        var initialState = BuildTaricBulwarkDamageAssignmentMidgameInitialState(openingResult.State);
        var journal = new RecordingMatchJournal();
        var session = new MatchSession(initialState, new CoreRuleEngine(), journal);
        var current = AcceptedCurrentResult(initialState);
        current = await DriveSpecificUnitToPlayerBattlefieldAsync(
            session,
            current,
            "P1",
            TaricSameBattlefieldStaticKeywordCardNo,
            "P2",
            "b0-taric-bulwark-stage-taric");
        current = await DriveSpecificUnitToPlayerBattlefieldAsync(
            session,
            current,
            "P1",
            LeblancCardNo,
            "P2",
            "b0-taric-bulwark-stage-leblanc");
        current = await DriveSpecificUnitToPlayerBattlefieldAsync(
            session,
            current,
            "P2",
            WildclawBeastmasterCardNo,
            "P2",
            "b0-taric-bulwark-stage-attacker");

        var (assignmentOpened, battleResult) = await DriveContestedBattlefieldToTaricBulwarkDamageAssignmentAsync(
            session,
            current,
            "P2",
            "P1",
            "b0-taric-bulwark-battle");

        AssertTaricBulwarkDamageAssignmentOrdering(assignmentOpened, battleResult);

        var result = await DriveBattleCloseToScoreVictoryAsync(
            session,
            battleResult,
            "b0-taric-bulwark-score");

        await AssertActionLogReplaysToFinalStateHashOnlyAsync(initialState, journal, result);
        Assert.Contains(journal.Entries, entry => string.Equals(entry.CommandType, CommandTypes.PlayCard, StringComparison.Ordinal));
        Assert.Contains(journal.Entries, entry => string.Equals(entry.CommandType, CommandTypes.MoveUnit, StringComparison.Ordinal));
        Assert.Contains(journal.Entries, entry => string.Equals(entry.CommandType, CommandTypes.DeclareBattle, StringComparison.Ordinal));
        Assert.Contains(journal.Entries, entry => string.Equals(entry.CommandType, CommandTypes.AssignCombatDamage, StringComparison.Ordinal));
        Assert.Contains(journal.Entries, entry => string.Equals(entry.CommandType, CommandTypes.EndTurn, StringComparison.Ordinal));
        AssertScoreVictory(result);
    }

    [Fact]
    public async Task StandbyHeavyOfficialLowCurveFullGameScoreVictoryActionLogReplaysToFinalStateHash()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var p1Deck = BuildLowCurveOfficialDeck(catalog, JhinLegendCardNo, JhinChampionCardNo);
        var p2Deck = BuildLowCurveOfficialDeck(catalog, PoppyLegendCardNo, PoppyChampionCardNo);
        Assert.NotEqual(p1Deck.LegendCardNo, p2Deck.LegendCardNo);
        Assert.NotEqual(p1Deck.ChampionCardNo, p2Deck.ChampionCardNo);

        await AssertFullGameScoreVictoryActionLogReplaysToFinalStateHashAsync(
            "b0-full-game-standby-heavy-low-curve-replay-room",
            "b0-full-standby-heavy-replay-score",
            p1Deck,
            p2Deck);
    }

    [Fact]
    public async Task StandbyOfficialDecksHideRevealAndScoreVictoryActionLogReplaysToFinalStateHash()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var deck = BuildStandbyOfficialDeck(catalog);

        var (initialState, journal, hidden, revealed, battleResult, result) =
            await DriveOfficialStandbyDecksToHideRevealScoreVictoryForReplayAsync(
                "b0-full-game-standby-hide-reveal-replay-room",
                deck,
                deck);

        await AssertActionLogReplaysToFinalStateHashAsync(initialState, journal, result);
        Assert.Contains(journal.Entries, entry => string.Equals(entry.CommandType, CommandTypes.HideCard, StringComparison.Ordinal));
        Assert.Contains(journal.Entries, entry => string.Equals(entry.CommandType, CommandTypes.RevealCard, StringComparison.Ordinal));
        Assert.Contains(journal.Entries, entry => string.Equals(entry.CommandType, CommandTypes.MoveUnit, StringComparison.Ordinal));
        Assert.Contains(journal.Entries, entry => string.Equals(entry.CommandType, CommandTypes.DeclareBattle, StringComparison.Ordinal));
        Assert.Contains(hidden.Events, gameEvent => string.Equals(gameEvent.Kind, "CARD_HIDDEN", StringComparison.Ordinal));
        var hiddenEvent = Assert.Single(hidden.Events, gameEvent => string.Equals(gameEvent.Kind, "CARD_HIDDEN", StringComparison.Ordinal));
        Assert.DoesNotContain("cardNo", hiddenEvent.Payload.Keys);
        Assert.True(Assert.IsType<bool>(hiddenEvent.Payload["isFaceDown"]));
        Assert.Contains(revealed.Events, gameEvent => string.Equals(gameEvent.Kind, "CARD_REVEALED", StringComparison.Ordinal));
        Assert.Contains(battleResult.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLE_CLOSED", StringComparison.Ordinal));
        AssertScoreVictory(result);
    }

    [Fact]
    public async Task StandbyOfficialDecksBattlefieldExtraStandbyHideAndScoreVictoryActionLogReplaysToFinalStateHash()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var deck = BuildBattlefieldExtraStandbyOfficialDeck(catalog);

        var (initialState, journal, hidden, battleResult, result, hiddenObjectId, battlefieldObjectId) =
            await DriveOfficialStandbyDecksToBattlefieldExtraStandbyHideScoreVictoryForReplayAsync(
                "b0-full-game-battlefield-extra-standby-replay-room",
                deck,
                deck);

        await AssertActionLogReplaysToFinalStateHashAsync(initialState, journal, result);
        Assert.Contains(journal.Entries, entry => string.Equals(entry.CommandType, CommandTypes.HideCard, StringComparison.Ordinal));
        Assert.DoesNotContain(journal.Entries, entry => string.Equals(entry.CommandType, CommandTypes.RevealCard, StringComparison.Ordinal));
        Assert.Contains(journal.Entries, entry => string.Equals(entry.CommandType, CommandTypes.DeclareBattle, StringComparison.Ordinal));
        Assert.Contains(journal.Entries, entry => string.Equals(entry.CommandType, CommandTypes.EndTurn, StringComparison.Ordinal));

        var hiddenEvent = Assert.Single(hidden.Events, gameEvent => string.Equals(gameEvent.Kind, "CARD_HIDDEN", StringComparison.Ordinal));
        Assert.DoesNotContain("cardNo", hiddenEvent.Payload.Keys);
        Assert.Equal("BATTLEFIELD", Assert.IsType<string>(hiddenEvent.Payload["destinationZone"]));
        Assert.Equal(battlefieldObjectId, Assert.IsType<string>(hiddenEvent.Payload["battlefieldObjectId"]));
        Assert.Equal(BandleTreeBattlefieldCardNo, Assert.IsType<string>(hiddenEvent.Payload["battlefieldCardNo"]));
        var standbyPlayerId = Assert.IsType<string>(hiddenEvent.Payload["playerId"]);

        Assert.Contains(hidden.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "BATTLEFIELD_EXTRA_STANDBY_ARRANGED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, hiddenObjectId, StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["battlefieldObjectId"] as string, battlefieldObjectId, StringComparison.Ordinal));
        Assert.Contains(hiddenObjectId, hidden.State.PlayerZones[standbyPlayerId].Battlefields, StringComparer.Ordinal);
        Assert.True(hidden.State.CardObjects[hiddenObjectId].IsFaceDown);
        Assert.Equal("BATTLEFIELD", hidden.State.ObjectLocations[hiddenObjectId].Zone);
        Assert.Equal(battlefieldObjectId, hidden.State.ObjectLocations[hiddenObjectId].BattlefieldObjectId);

        Assert.Contains(battleResult.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLE_CLOSED", StringComparison.Ordinal));
        AssertScoreVictory(result);
    }

    [Fact]
    public async Task OfficialDecksResolveMultiDefenderBattleDamageAssignmentActionLogReplaysToFinalStateHash()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var deck = BuildDamageAssignmentOfficialDeck(catalog);
        var initialState = BuildSeatedInitialState("b0-full-game-damage-assignment-replay-room", LowCurveReplaySeed);
        var journal = new RecordingMatchJournal();

        var (_, assignmentOpened, battleResult) = await DriveOfficialDecksToDamageAssignmentBattleCloseAsync(
            initialState,
            journal,
            deck,
            deck);

        await AssertActionLogReplaysToFinalStateHashAsync(initialState, journal, battleResult);
        Assert.Contains(assignmentOpened.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLE_DAMAGE_ASSIGNMENT_OPENED", StringComparison.Ordinal));
        Assert.Contains(journal.Entries, entry => string.Equals(entry.CommandType, CommandTypes.DeclareBattle, StringComparison.Ordinal));
        Assert.Contains(journal.Entries, entry => string.Equals(entry.CommandType, CommandTypes.AssignCombatDamage, StringComparison.Ordinal));
        Assert.Contains(battleResult.Events, gameEvent => string.Equals(gameEvent.Kind, "DAMAGE_APPLIED", StringComparison.Ordinal));
        AssertNoHiddenZoneLeak(assignmentOpened);
        AssertNoHiddenZoneLeak(battleResult);
    }

    [Fact]
    public async Task OfficialDecksResolveMultiDefenderBattleDamageAssignmentScoreVictoryActionLogReplaysToFinalStateHash()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var deck = BuildDamageAssignmentOfficialDeck(catalog);
        var initialState = BuildSeatedInitialState("b0-full-game-damage-assignment-score-replay-room", LowCurveReplaySeed);
        var journal = new RecordingMatchJournal();

        var (session, assignmentOpened, battleResult) = await DriveOfficialDecksToDamageAssignmentBattleCloseAsync(
            initialState,
            journal,
            deck,
            deck);

        var result = await DriveBattleCloseToScoreVictoryAsync(
            session,
            battleResult,
            "b0-damage-assignment-score");

        await AssertActionLogReplaysToFinalStateHashAsync(initialState, journal, result);
        Assert.Contains(assignmentOpened.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLE_DAMAGE_ASSIGNMENT_OPENED", StringComparison.Ordinal));
        Assert.Contains(journal.Entries, entry => string.Equals(entry.CommandType, CommandTypes.DeclareBattle, StringComparison.Ordinal));
        Assert.Contains(journal.Entries, entry => string.Equals(entry.CommandType, CommandTypes.AssignCombatDamage, StringComparison.Ordinal));
        Assert.Contains(journal.Entries, entry => string.Equals(entry.CommandType, CommandTypes.EndTurn, StringComparison.Ordinal));
        Assert.Contains(battleResult.Events, gameEvent => string.Equals(gameEvent.Kind, "DAMAGE_APPLIED", StringComparison.Ordinal));
        Assert.Contains(battleResult.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLE_CLOSED", StringComparison.Ordinal));
        AssertScoreVictory(result);
        AssertNoHiddenZoneLeak(assignmentOpened);
        AssertNoHiddenZoneLeak(battleResult);
        AssertNoHiddenZoneLeak(result);
    }

    [Fact]
    public async Task OfficialDecksResolveShadowBattleResponseActivationActionLogReplaysToFinalStateHash()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var deck = BuildShadowResponseOfficialDeck(catalog);

        var (initialState, journal, _, openedResponse, activated, stackResolved, battleResult, targetObjectId) =
            await DriveOfficialDecksToShadowResponseBattleCloseForReplayAsync(
                "b0-full-game-shadow-response-replay-room",
                deck,
                deck);

        await AssertActionLogReplaysToFinalStateHashAsync(initialState, journal, battleResult);
        Assert.Contains(openedResponse.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLE_RESPONSE_PRIORITY_OPENED", StringComparison.Ordinal));
        Assert.Contains(journal.Entries, entry => string.Equals(entry.CommandType, CommandTypes.DeclareBattle, StringComparison.Ordinal));
        Assert.Contains(journal.Entries, entry => string.Equals(entry.CommandType, CommandTypes.ActivateAbility, StringComparison.Ordinal));
        Assert.Contains(activated.Events, gameEvent => string.Equals(gameEvent.Kind, "ABILITY_ACTIVATED", StringComparison.Ordinal));
        Assert.Contains(stackResolved.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "ABILITY_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["abilityId"] as string, P4ActivatedAbilityCatalog.ShadowStunAbilityId, StringComparison.Ordinal));
        Assert.Contains("STUNNED", stackResolved.State.CardObjects[targetObjectId].UntilEndOfTurnEffects);
        AssertNoHiddenZoneLeak(openedResponse);
        AssertNoHiddenZoneLeak(activated);
        AssertNoHiddenZoneLeak(stackResolved);
        AssertNoHiddenZoneLeak(battleResult);
    }

    [Fact]
    public async Task OfficialDecksResolveShadowBattleResponseActivationScoreVictoryActionLogReplaysToFinalStateHash()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var deck = BuildShadowResponseOfficialDeck(catalog);

        var (initialState, journal, session, openedResponse, activated, stackResolved, battleResult, targetObjectId) =
            await DriveOfficialDecksToShadowResponseBattleCloseForReplayAsync(
                "b0-full-game-shadow-response-score-replay-room",
                deck,
                deck);

        var result = await DriveBattleCloseToScoreVictoryAsync(
            session,
            battleResult,
            "b0-shadow-score");

        await AssertActionLogReplaysToFinalStateHashAsync(initialState, journal, result);
        Assert.Contains(openedResponse.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLE_RESPONSE_PRIORITY_OPENED", StringComparison.Ordinal));
        Assert.Contains(journal.Entries, entry => string.Equals(entry.CommandType, CommandTypes.DeclareBattle, StringComparison.Ordinal));
        Assert.Contains(journal.Entries, entry => string.Equals(entry.CommandType, CommandTypes.ActivateAbility, StringComparison.Ordinal));
        Assert.Contains(journal.Entries, entry => string.Equals(entry.CommandType, CommandTypes.EndTurn, StringComparison.Ordinal));
        Assert.Contains(activated.Events, gameEvent => string.Equals(gameEvent.Kind, "ABILITY_ACTIVATED", StringComparison.Ordinal));
        Assert.Contains(stackResolved.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "ABILITY_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["abilityId"] as string, P4ActivatedAbilityCatalog.ShadowStunAbilityId, StringComparison.Ordinal));
        Assert.Contains("STUNNED", stackResolved.State.CardObjects[targetObjectId].UntilEndOfTurnEffects);
        Assert.Contains(battleResult.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLE_CLOSED", StringComparison.Ordinal));
        AssertScoreVictory(result);
        AssertNoHiddenZoneLeak(openedResponse);
        AssertNoHiddenZoneLeak(activated);
        AssertNoHiddenZoneLeak(stackResolved);
        AssertNoHiddenZoneLeak(battleResult);
        AssertNoHiddenZoneLeak(result);
    }

    [Fact]
    public async Task OfficialDecksResolveStandbyReactionDuringShadowResponseActionLogReplaysToFinalStateHash()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var deck = BuildStandbyReactionOfficialDeck(catalog);

        var (initialState, journal, _, hidden, openedResponse, activated, revealed, teemoResolved, shadowResolved, battleResult, teemoObjectId, targetObjectId) =
            await DriveOfficialDecksToStandbyReactionShadowBattleCloseForReplayAsync(
                "b0-full-game-standby-reaction-shadow-replay-room",
                deck,
                deck);

        await AssertActionLogReplaysToFinalStateHashAsync(initialState, journal, battleResult);
        Assert.Contains(journal.Entries, entry => string.Equals(entry.CommandType, CommandTypes.HideCard, StringComparison.Ordinal));
        Assert.Contains(journal.Entries, entry => string.Equals(entry.CommandType, CommandTypes.ActivateAbility, StringComparison.Ordinal));
        Assert.Contains(journal.Entries, entry => string.Equals(entry.CommandType, CommandTypes.RevealCard, StringComparison.Ordinal));
        Assert.Contains(hidden.Events, gameEvent => string.Equals(gameEvent.Kind, "CARD_HIDDEN", StringComparison.Ordinal));
        var hiddenEvent = Assert.Single(hidden.Events, gameEvent => string.Equals(gameEvent.Kind, "CARD_HIDDEN", StringComparison.Ordinal));
        Assert.DoesNotContain("cardNo", hiddenEvent.Payload.Keys);
        Assert.Contains(openedResponse.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLE_RESPONSE_PRIORITY_OPENED", StringComparison.Ordinal));
        Assert.Contains(activated.Events, gameEvent => string.Equals(gameEvent.Kind, "ABILITY_ACTIVATED", StringComparison.Ordinal));
        Assert.Contains(revealed.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "STACK_ITEM_ADDED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["effectKind"] as string, "TEEMO_PLAY_UNIT_SELF_POWER_PLUS_3", StringComparison.Ordinal));
        Assert.Contains(teemoResolved.Events, gameEvent => string.Equals(gameEvent.Kind, "POWER_MODIFIED_UNTIL_END_OF_TURN", StringComparison.Ordinal));
        Assert.Contains(teemoObjectId, teemoResolved.State.PlayerZones[teemoResolved.State.ObjectLocations[teemoObjectId].PlayerId].Base, StringComparer.Ordinal);
        Assert.False(teemoResolved.State.CardObjects[teemoObjectId].IsFaceDown);
        Assert.Equal(4, teemoResolved.State.CardObjects[teemoObjectId].Power);
        Assert.Contains(shadowResolved.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "ABILITY_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["abilityId"] as string, P4ActivatedAbilityCatalog.ShadowStunAbilityId, StringComparison.Ordinal));
        Assert.Contains("STUNNED", shadowResolved.State.CardObjects[targetObjectId].UntilEndOfTurnEffects);
        Assert.Contains(battleResult.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLE_CLOSED", StringComparison.Ordinal));
        AssertNoHiddenZoneLeak(hidden);
        AssertNoHiddenZoneLeak(openedResponse);
        AssertNoHiddenZoneLeak(activated);
        AssertNoHiddenZoneLeak(revealed);
        AssertNoHiddenZoneLeak(teemoResolved);
        AssertNoHiddenZoneLeak(shadowResolved);
        AssertNoHiddenZoneLeak(battleResult);
    }

    [Fact]
    public async Task OfficialDecksResolveStandbyReactionDuringShadowResponseScoreVictoryActionLogReplaysToFinalStateHash()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var deck = BuildStandbyReactionOfficialDeck(catalog);

        var (initialState, journal, session, hidden, openedResponse, activated, revealed, teemoResolved, shadowResolved, battleResult, teemoObjectId, targetObjectId) =
            await DriveOfficialDecksToStandbyReactionShadowBattleCloseForReplayAsync(
                "b0-full-game-standby-reaction-shadow-score-replay-room",
                deck,
                deck);

        var result = await DriveBattleCloseToScoreVictoryAsync(
            session,
            battleResult,
            "b0-standby-reaction-shadow-score");

        await AssertActionLogReplaysToFinalStateHashAsync(initialState, journal, result);
        Assert.Contains(journal.Entries, entry => string.Equals(entry.CommandType, CommandTypes.HideCard, StringComparison.Ordinal));
        Assert.Contains(journal.Entries, entry => string.Equals(entry.CommandType, CommandTypes.ActivateAbility, StringComparison.Ordinal));
        Assert.Contains(journal.Entries, entry => string.Equals(entry.CommandType, CommandTypes.RevealCard, StringComparison.Ordinal));
        Assert.Contains(journal.Entries, entry => string.Equals(entry.CommandType, CommandTypes.EndTurn, StringComparison.Ordinal));
        Assert.Contains(hidden.Events, gameEvent => string.Equals(gameEvent.Kind, "CARD_HIDDEN", StringComparison.Ordinal));
        var hiddenEvent = Assert.Single(hidden.Events, gameEvent => string.Equals(gameEvent.Kind, "CARD_HIDDEN", StringComparison.Ordinal));
        Assert.DoesNotContain("cardNo", hiddenEvent.Payload.Keys);
        Assert.Contains(openedResponse.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLE_RESPONSE_PRIORITY_OPENED", StringComparison.Ordinal));
        Assert.Contains(activated.Events, gameEvent => string.Equals(gameEvent.Kind, "ABILITY_ACTIVATED", StringComparison.Ordinal));
        Assert.Contains(revealed.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "STACK_ITEM_ADDED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["effectKind"] as string, "TEEMO_PLAY_UNIT_SELF_POWER_PLUS_3", StringComparison.Ordinal));
        Assert.Contains(teemoResolved.Events, gameEvent => string.Equals(gameEvent.Kind, "POWER_MODIFIED_UNTIL_END_OF_TURN", StringComparison.Ordinal));
        Assert.Contains(teemoObjectId, teemoResolved.State.PlayerZones[teemoResolved.State.ObjectLocations[teemoObjectId].PlayerId].Base, StringComparer.Ordinal);
        Assert.False(teemoResolved.State.CardObjects[teemoObjectId].IsFaceDown);
        Assert.Equal(4, teemoResolved.State.CardObjects[teemoObjectId].Power);
        Assert.Contains(shadowResolved.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "ABILITY_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["abilityId"] as string, P4ActivatedAbilityCatalog.ShadowStunAbilityId, StringComparison.Ordinal));
        Assert.Contains("STUNNED", shadowResolved.State.CardObjects[targetObjectId].UntilEndOfTurnEffects);
        Assert.Contains(battleResult.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLE_CLOSED", StringComparison.Ordinal));
        AssertScoreVictory(result);
        AssertNoHiddenZoneLeak(hidden);
        AssertNoHiddenZoneLeak(openedResponse);
        AssertNoHiddenZoneLeak(activated);
        AssertNoHiddenZoneLeak(revealed);
        AssertNoHiddenZoneLeak(teemoResolved);
        AssertNoHiddenZoneLeak(shadowResolved);
        AssertNoHiddenZoneLeak(battleResult);
        AssertNoHiddenZoneLeak(result);
    }

    [Fact]
    public async Task StandbyHeavyOfficialLowCurveDecksReachScoreVictoryAfterRealBattleThroughServerPrompts()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var p1Deck = BuildLowCurveOfficialDeck(catalog, JhinLegendCardNo, JhinChampionCardNo);
        var p2Deck = BuildLowCurveOfficialDeck(catalog, PoppyLegendCardNo, PoppyChampionCardNo);
        Assert.NotEqual(p1Deck.LegendCardNo, p2Deck.LegendCardNo);
        Assert.NotEqual(p1Deck.ChampionCardNo, p2Deck.ChampionCardNo);

        var (session, _, battleResult) = await DriveOfficialLowCurveDecksToBattleCloseAsync(
            "b0-full-game-standby-heavy-low-curve-score-room",
            p1Deck,
            p2Deck);

        var result = await DriveBattleCloseToScoreVictoryAsync(
            session,
            battleResult,
            "b0-standby-heavy-score");

        AssertScoreVictory(result);
    }

    [Fact]
    public async Task OfficialDecksResolveMultiDefenderBattleDamageAssignmentThroughServerPrompts()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var deck = BuildDamageAssignmentOfficialDeck(catalog);

        var (_, assignmentOpened, battleResult) = await DriveOfficialDecksToDamageAssignmentBattleCloseAsync(
            "b0-full-game-damage-assignment-room",
            deck,
            deck);

        Assert.Contains(assignmentOpened.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLE_DAMAGE_ASSIGNMENT_OPENED", StringComparison.Ordinal));
        Assert.Equal(PromptTypes.AssignCombatDamage, assignmentOpened.Prompts[assignmentOpened.State.ActivePlayerId].View?.Type);
        Assert.Contains(battleResult.Events, gameEvent => string.Equals(gameEvent.Kind, "DAMAGE_APPLIED", StringComparison.Ordinal));
        Assert.Contains(battleResult.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLE_CLOSED", StringComparison.Ordinal));
        Assert.False(battleResult.State.BattleState.IsActive);
        AssertNoHiddenZoneLeak(assignmentOpened);
        AssertNoHiddenZoneLeak(battleResult);
    }

    [Fact]
    public async Task OfficialDecksResolveShadowBattleResponseActivationThroughServerPrompts()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var deck = BuildShadowResponseOfficialDeck(catalog);

        var (_, openedResponse, activated, stackResolved, battleResult, targetObjectId) =
            await DriveOfficialDecksToShadowResponseBattleCloseAsync(
                "b0-full-game-shadow-response-room",
                deck,
                deck);

        Assert.Contains(openedResponse.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLE_RESPONSE_PRIORITY_OPENED", StringComparison.Ordinal));
        Assert.Equal(PromptTypes.StackPriority, openedResponse.Prompts[openedResponse.State.PriorityPlayerId!].View?.Type);
        Assert.Contains(CommandTypes.ActivateAbility, openedResponse.Prompts[openedResponse.State.PriorityPlayerId!].Actions);

        Assert.Contains(activated.Events, gameEvent => string.Equals(gameEvent.Kind, "ABILITY_ACTIVATED", StringComparison.Ordinal));
        Assert.Contains(activated.Events, gameEvent => string.Equals(gameEvent.Kind, "STACK_ITEM_ADDED", StringComparison.Ordinal));
        Assert.Single(activated.State.StackItems);

        Assert.Contains(stackResolved.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "ABILITY_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["abilityId"] as string, P4ActivatedAbilityCatalog.ShadowStunAbilityId, StringComparison.Ordinal));
        Assert.Contains("STUNNED", stackResolved.State.CardObjects[targetObjectId].UntilEndOfTurnEffects);

        Assert.Contains(battleResult.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLE_RESPONSE_PRIORITY_CLOSED", StringComparison.Ordinal));
        Assert.Contains(battleResult.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLE_CLOSED", StringComparison.Ordinal));
        Assert.False(battleResult.State.BattleState.IsActive);
        AssertNoHiddenZoneLeak(openedResponse);
        AssertNoHiddenZoneLeak(activated);
        AssertNoHiddenZoneLeak(stackResolved);
        AssertNoHiddenZoneLeak(battleResult);
    }

    private static async ValueTask<ResolutionResult> DriveBattleCloseToScoreVictoryAsync(
        MatchSession session,
        ResolutionResult battleResult,
        string intentPrefix)
    {
        var result = battleResult;
        var scoreEvents = result.Events.Count(gameEvent => string.Equals(gameEvent.Kind, "SCORE_GAINED", StringComparison.Ordinal));
        for (var turnIndex = 0; turnIndex < 24 && !string.Equals(result.State.Status, MatchStatuses.Finished, StringComparison.Ordinal); turnIndex++)
        {
            if (string.Equals(result.State.TimingState, TimingStates.SpellDuelOpen, StringComparison.Ordinal)
                || !string.IsNullOrWhiteSpace(result.State.FocusPlayerId))
            {
                result = await PassOpenSpellDuelAsync(session, result, $"{intentPrefix}-pass-focus-{turnIndex}");
                scoreEvents += result.Events.Count(gameEvent => string.Equals(gameEvent.Kind, "SCORE_GAINED", StringComparison.Ordinal));
            }

            if (string.Equals(result.State.PendingTaskQueue.Phase, "BATTLE_TASKS", StringComparison.Ordinal)
                && result.Prompts[result.State.ActivePlayerId].Actions.Contains(CommandTypes.DeclareBattle, StringComparer.Ordinal))
            {
                result = await SubmitFirstDeclareBattleCandidateAsync(
                    session,
                    result,
                    $"{intentPrefix}-clear-pending-battle-{turnIndex}");
                result = await PassOpenBattleResponseAsync(session, result, $"{intentPrefix}-clear-pending-battle-response-{turnIndex}");
                result = await ResolveOpenBattleDamageAssignmentsAsync(session, result, $"{intentPrefix}-clear-pending-battle-assign-{turnIndex}");
                result = await PassOpenBattleResponseAsync(session, result, $"{intentPrefix}-clear-pending-battle-response-after-assignment-{turnIndex}");
                AssertNoHiddenZoneLeak(result);
                scoreEvents += result.Events.Count(gameEvent => string.Equals(gameEvent.Kind, "SCORE_GAINED", StringComparison.Ordinal));
                continue;
            }

            if (!string.Equals(result.State.Phase, MatchPhases.Main, StringComparison.Ordinal)
                || !string.Equals(result.State.TimingState, TimingStates.NeutralOpen, StringComparison.Ordinal)
                || result.State.PendingTaskQueue.HasTasks)
            {
                throw new InvalidOperationException(JsonSerializer.Serialize(new
                {
                    MatchStatus = result.State.Status,
                    MatchPhase = result.State.Phase,
                    result.State.TimingState,
                    result.State.ActivePlayerId,
                    result.State.TurnPlayerId,
                    result.State.FocusPlayerId,
                    PendingTaskPhase = result.State.PendingTaskQueue.Phase,
                    result.State.PendingTaskQueue.ActiveTaskId,
                    TaskKinds = result.State.PendingTaskQueue.Tasks.Select(task => task.Kind).ToArray(),
                    PromptActions = result.Prompts[result.State.ActivePlayerId].Actions
                }));
            }

            Assert.Equal(result.State.TurnPlayerId, result.State.ActivePlayerId);
            result = await EndTurnAsync(session, result.State.ActivePlayerId, $"{intentPrefix}-end-turn-{turnIndex}");
            AssertNoHiddenZoneLeak(result);
            scoreEvents += result.Events.Count(gameEvent => string.Equals(gameEvent.Kind, "SCORE_GAINED", StringComparison.Ordinal));
        }

        Assert.True(scoreEvents > 0, "Expected the prompt-driven game to gain battlefield score before match win.");
        return result;
    }

    private static async ValueTask AssertFullGameScoreVictoryActionLogReplaysToFinalStateHashAsync(
        string roomId,
        string scoreIntentPrefix,
        OfficialDecklist p1Deck,
        OfficialDecklist p2Deck)
    {
        var initialState = BuildSeatedInitialState(roomId, LowCurveReplaySeed);
        var journal = new RecordingMatchJournal();
        var (session, _, battleResult) = await DriveOfficialLowCurveDecksToBattleCloseAsync(
            initialState,
            journal,
            p1Deck,
            p2Deck);

        var result = await DriveBattleCloseToScoreVictoryAsync(
            session,
            battleResult,
            scoreIntentPrefix);

        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(
            initialState,
            journal.Entries.Select(ToRecoveredCommand).ToArray(),
            result.State,
            new CoreRuleEngine(),
            CancellationToken.None,
            ToRecoveredEvents(journal.Entries));

        Assert.True(replay.IsMatch, string.Join("; ", replay.Errors));
        Assert.Equal(MatchStateHasher.Hash(result.State), replay.ExpectedStateHash);
        Assert.Equal(replay.ExpectedStateHash, replay.ReplayedStateHash);
        Assert.Empty(replay.Errors);
        Assert.Contains(journal.Entries, entry => string.Equals(entry.CommandType, CommandTypes.SubmitDeck, StringComparison.Ordinal));
        Assert.Contains(journal.Entries, entry => string.Equals(entry.CommandType, CommandTypes.Ready, StringComparison.Ordinal));
        Assert.Contains(journal.Entries, entry => string.Equals(entry.CommandType, CommandTypes.Mulligan, StringComparison.Ordinal));
        Assert.Contains(journal.Entries, entry => string.Equals(entry.CommandType, CommandTypes.DeclareBattle, StringComparison.Ordinal));
        Assert.Contains(journal.Entries, entry => string.Equals(entry.CommandType, CommandTypes.EndTurn, StringComparison.Ordinal));
        AssertScoreVictory(result);
    }

    private static async ValueTask AssertActionLogReplaysToFinalStateHashAsync(
        MatchState initialState,
        RecordingMatchJournal journal,
        ResolutionResult result)
    {
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(
            initialState,
            journal.Entries.Select(ToRecoveredCommand).ToArray(),
            result.State,
            new CoreRuleEngine(),
            CancellationToken.None,
            ToRecoveredEvents(journal.Entries));

        Assert.True(replay.IsMatch, string.Join("; ", replay.Errors));
        Assert.Equal(MatchStateHasher.Hash(result.State), replay.ExpectedStateHash);
        Assert.Equal(replay.ExpectedStateHash, replay.ReplayedStateHash);
        Assert.Empty(replay.Errors);
        Assert.Contains(journal.Entries, entry => string.Equals(entry.CommandType, CommandTypes.SubmitDeck, StringComparison.Ordinal));
        Assert.Contains(journal.Entries, entry => string.Equals(entry.CommandType, CommandTypes.Ready, StringComparison.Ordinal));
        Assert.Contains(journal.Entries, entry => string.Equals(entry.CommandType, CommandTypes.Mulligan, StringComparison.Ordinal));
    }

    private static async ValueTask AssertActionLogReplaysToFinalStateHashOnlyAsync(
        MatchState initialState,
        RecordingMatchJournal journal,
        ResolutionResult result)
    {
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(
            initialState,
            journal.Entries.Select(ToRecoveredCommand).ToArray(),
            result.State,
            new CoreRuleEngine(),
            CancellationToken.None,
            ToRecoveredEvents(journal.Entries));

        Assert.True(replay.IsMatch, string.Join("; ", replay.Errors));
        Assert.Equal(MatchStateHasher.Hash(result.State), replay.ExpectedStateHash);
        Assert.Equal(replay.ExpectedStateHash, replay.ReplayedStateHash);
        Assert.Empty(replay.Errors);
    }

    private static void AssertScoreVictory(ResolutionResult result)
    {
        Assert.Equal(MatchStatuses.Finished, result.State.Status);
        Assert.False(string.IsNullOrWhiteSpace(result.State.WinnerPlayerId));
        var winEvent = Assert.Single(
            result.Events,
            gameEvent => string.Equals(gameEvent.Kind, "MATCH_WON", StringComparison.Ordinal));
        var winningScore = Assert.IsType<int>(winEvent.Payload["winningScore"]);
        Assert.True(
            result.State.PlayerScores[result.State.WinnerPlayerId!] >= winningScore,
            $"Expected winner score to satisfy winningScore={winningScore}; scores={JsonSerializer.Serialize(result.State.PlayerScores)}.");
        AssertNoHiddenZoneLeak(result);
    }

    private static void AssertCrimsonSignetTreantConquestRepeat(ResolutionResult result)
    {
        var conqueredEvent = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_CONQUERED", StringComparison.Ordinal)
            && gameEvent.Payload.TryGetValue("sourceObjectId", out var sourceObjectId)
            && sourceObjectId is string source
            && result.State.CardObjects.TryGetValue(source, out var sourceObject)
            && string.Equals(sourceObject.CardNo, CrimsonSignetTreantCardNo, StringComparison.Ordinal));
        var treantObjectId = Assert.IsType<string>(conqueredEvent.Payload["sourceObjectId"]);

        var conquestTriggers = result.Events
            .Where(gameEvent =>
                string.Equals(gameEvent.Kind, "UNIT_CONQUEST_EFFECT_ACTIVATED", StringComparison.Ordinal)
                && string.Equals(gameEvent.Payload["sourceObjectId"] as string, treantObjectId, StringComparison.Ordinal)
                && string.Equals(gameEvent.Payload["effectId"] as string, TriggerKinds.UnitConquestGrantFriendlyBoon, StringComparison.Ordinal)
                && string.Equals(gameEvent.Payload["reason"] as string, "BATTLEFIELD_CONQUERED", StringComparison.Ordinal))
            .ToArray();
        Assert.Equal(2, conquestTriggers.Length);

        var boonEvents = result.Events
            .Where(gameEvent =>
                string.Equals(gameEvent.Kind, "BOON_GRANTED", StringComparison.Ordinal)
                && string.Equals(gameEvent.Payload["sourceObjectId"] as string, treantObjectId, StringComparison.Ordinal)
                && string.Equals(gameEvent.Payload["abilityId"] as string, TriggerKinds.UnitConquestGrantFriendlyBoon, StringComparison.Ordinal))
            .ToArray();
        Assert.Equal(2, boonEvents.Length);
        var boonTargetObjectId = Assert.IsType<string>(boonEvents[0].Payload["targetObjectId"]);
        Assert.Equal(boonTargetObjectId, Assert.IsType<string>(boonEvents[1].Payload["targetObjectId"]));
        Assert.False(Assert.IsType<bool>(boonEvents[0].Payload["alreadyHadBoon"]));
        Assert.True(Assert.IsType<bool>(boonEvents[1].Payload["alreadyHadBoon"]));

        var boonTarget = result.State.CardObjects[boonTargetObjectId];
        Assert.Contains(CardObjectTags.Boon, boonTarget.Tags);
        AssertNoHiddenZoneLeak(result);
    }

    private static void AssertSameBattlefieldStaticAuraDamage(ResolutionResult result)
    {
        var attackerDamageEvent = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "DAMAGE_APPLIED", StringComparison.Ordinal)
            && gameEvent.Payload.TryGetValue("combatRole", out var combatRole)
            && string.Equals(combatRole as string, "ATTACKER", StringComparison.Ordinal)
            && gameEvent.Payload.TryGetValue("staticPowerBonus", out var staticPowerBonus)
            && staticPowerBonus is 1);
        Assert.Equal(2, attackerDamageEvent.Payload["basePower"]);
        Assert.Equal(1, attackerDamageEvent.Payload["staticPowerBonus"]);
        Assert.Equal(3, attackerDamageEvent.Payload["combatPower"]);
        Assert.Equal(3, attackerDamageEvent.Payload["damage"]);
        Assert.Contains(result.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLE_CLOSED", StringComparison.Ordinal));
        AssertNoHiddenZoneLeak(result);
    }

    private static void AssertBattlefieldAllUnitsStaticAuraDamage(ResolutionResult result)
    {
        var attackerDamageEvent = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "DAMAGE_APPLIED", StringComparison.Ordinal)
            && gameEvent.Payload.TryGetValue("combatRole", out var combatRole)
            && string.Equals(combatRole as string, "ATTACKER", StringComparison.Ordinal)
            && gameEvent.Payload.TryGetValue("basePower", out var basePower)
            && basePower is 7
            && gameEvent.Payload.TryGetValue("staticPowerBonus", out var staticPowerBonus)
            && staticPowerBonus is 1);
        Assert.Equal(7, attackerDamageEvent.Payload["basePower"]);
        Assert.Equal(1, attackerDamageEvent.Payload["staticPowerBonus"]);
        Assert.Equal(8, attackerDamageEvent.Payload["combatPower"]);
        Assert.Equal(8, attackerDamageEvent.Payload["damage"]);
        Assert.Contains(result.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLE_CLOSED", StringComparison.Ordinal));
        AssertNoHiddenZoneLeak(result);
    }

    private static void AssertSourceSameLocationStaticAuraDamage(ResolutionResult result)
    {
        var attackerDamageEvent = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "DAMAGE_APPLIED", StringComparison.Ordinal)
            && gameEvent.Payload.TryGetValue("combatRole", out var combatRole)
            && string.Equals(combatRole as string, "ATTACKER", StringComparison.Ordinal)
            && gameEvent.Payload.TryGetValue("basePower", out var basePower)
            && basePower is 2
            && gameEvent.Payload.TryGetValue("staticPowerBonus", out var staticPowerBonus)
            && staticPowerBonus is 1);
        Assert.Equal(2, attackerDamageEvent.Payload["basePower"]);
        Assert.Equal(1, attackerDamageEvent.Payload["staticPowerBonus"]);
        Assert.Equal(3, attackerDamageEvent.Payload["combatPower"]);
        Assert.Equal(3, attackerDamageEvent.Payload["damage"]);
        Assert.Contains(result.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLE_CLOSED", StringComparison.Ordinal));
        AssertNoHiddenZoneLeak(result);
    }

    private static void AssertSameBattlefieldBoonCountStaticAuraDamage(ResolutionResult result)
    {
        var attackerDamageEvent = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "DAMAGE_APPLIED", StringComparison.Ordinal)
            && gameEvent.Payload.TryGetValue("combatRole", out var combatRole)
            && string.Equals(combatRole as string, "ATTACKER", StringComparison.Ordinal)
            && gameEvent.Payload.TryGetValue("basePower", out var basePower)
            && basePower is 5
            && gameEvent.Payload.TryGetValue("staticPowerBonus", out var staticPowerBonus)
            && staticPowerBonus is 1);
        Assert.Equal(5, attackerDamageEvent.Payload["basePower"]);
        Assert.Equal(1, attackerDamageEvent.Payload["staticPowerBonus"]);
        Assert.Equal(6, attackerDamageEvent.Payload["combatPower"]);
        Assert.Equal(6, attackerDamageEvent.Payload["damage"]);
        Assert.Contains(result.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLE_CLOSED", StringComparison.Ordinal));
        AssertNoHiddenZoneLeak(result);
    }

    private static void AssertSameBattlefieldOtherFriendlyFilteredStaticAuraDamage(ResolutionResult result)
    {
        var attackerDamageEvent = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "DAMAGE_APPLIED", StringComparison.Ordinal)
            && gameEvent.Payload.TryGetValue("combatRole", out var combatRole)
            && string.Equals(combatRole as string, "ATTACKER", StringComparison.Ordinal)
            && gameEvent.Payload.TryGetValue("basePower", out var basePower)
            && basePower is 3
            && gameEvent.Payload.TryGetValue("staticPowerBonus", out var staticPowerBonus)
            && staticPowerBonus is 2);
        Assert.Equal(3, attackerDamageEvent.Payload["basePower"]);
        Assert.Equal(2, attackerDamageEvent.Payload["staticPowerBonus"]);
        Assert.Equal(5, attackerDamageEvent.Payload["combatPower"]);
        Assert.Equal(5, attackerDamageEvent.Payload["damage"]);
        Assert.Contains(result.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLE_CLOSED", StringComparison.Ordinal));
        AssertNoHiddenZoneLeak(result);
    }

    private static void AssertOtherFriendlyStaticAuraDamage(ResolutionResult result)
    {
        var attackerDamageEvent = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "DAMAGE_APPLIED", StringComparison.Ordinal)
            && gameEvent.Payload.TryGetValue("combatRole", out var combatRole)
            && string.Equals(combatRole as string, "ATTACKER", StringComparison.Ordinal)
            && gameEvent.Payload.TryGetValue("staticPowerBonus", out var staticPowerBonus)
            && staticPowerBonus is 2);
        Assert.Equal(7, attackerDamageEvent.Payload["basePower"]);
        Assert.Equal(2, attackerDamageEvent.Payload["staticPowerBonus"]);
        Assert.Equal(9, attackerDamageEvent.Payload["combatPower"]);
        Assert.Equal(9, attackerDamageEvent.Payload["damage"]);
        Assert.Contains(result.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLE_CLOSED", StringComparison.Ordinal));
        AssertNoHiddenZoneLeak(result);
    }

    private static void AssertSourceCombatStaticAuraDamage(ResolutionResult result)
    {
        var attackerDamageEvent = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "DAMAGE_APPLIED", StringComparison.Ordinal)
            && gameEvent.Payload.TryGetValue("combatRole", out var combatRole)
            && string.Equals(combatRole as string, "ATTACKER", StringComparison.Ordinal)
            && gameEvent.Payload.TryGetValue("basePower", out var basePower)
            && basePower is 3
            && gameEvent.Payload.TryGetValue("staticPowerBonus", out var staticPowerBonus)
            && staticPowerBonus is 2);
        Assert.Equal(3, attackerDamageEvent.Payload["basePower"]);
        Assert.Equal(2, attackerDamageEvent.Payload["staticPowerBonus"]);
        Assert.Equal(5, attackerDamageEvent.Payload["combatPower"]);
        Assert.Equal(5, attackerDamageEvent.Payload["damage"]);
        Assert.Contains(result.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLE_CLOSED", StringComparison.Ordinal));
        AssertNoHiddenZoneLeak(result);
    }

    private static void AssertSameBattlefieldStaticKeywordDamage(ResolutionResult result)
    {
        var attackerDamageEvent = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "DAMAGE_APPLIED", StringComparison.Ordinal)
            && gameEvent.Payload.TryGetValue("combatRole", out var combatRole)
            && string.Equals(combatRole as string, "ATTACKER", StringComparison.Ordinal)
            && gameEvent.Payload.TryGetValue("keyword", out var keyword)
            && string.Equals(keyword as string, CardCombatKeywordNames.Assault, StringComparison.Ordinal)
            && gameEvent.Payload.TryGetValue("keywordBonus", out var keywordBonus)
            && keywordBonus is 1);
        Assert.Equal(1, attackerDamageEvent.Payload["basePower"]);
        Assert.Equal(1, attackerDamageEvent.Payload["keywordBonus"]);
        Assert.False(attackerDamageEvent.Payload.ContainsKey("staticPowerBonus"));
        Assert.Equal(2, attackerDamageEvent.Payload["combatPower"]);
        Assert.Equal(2, attackerDamageEvent.Payload["damage"]);
        Assert.Contains(result.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLE_CLOSED", StringComparison.Ordinal));
        AssertNoHiddenZoneLeak(result);
    }

    private static void AssertSameBattlefieldSteadfastStaticKeywordDamage(ResolutionResult result)
    {
        var defenderDamageEvent = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "DAMAGE_APPLIED", StringComparison.Ordinal)
            && gameEvent.Payload.TryGetValue("combatRole", out var combatRole)
            && string.Equals(combatRole as string, "DEFENDER", StringComparison.Ordinal)
            && gameEvent.Payload.TryGetValue("keyword", out var keyword)
            && string.Equals(keyword as string, CardCombatKeywordNames.Steadfast, StringComparison.Ordinal)
            && gameEvent.Payload.TryGetValue("keywordBonus", out var keywordBonus)
            && keywordBonus is 1);
        Assert.Equal(4, defenderDamageEvent.Payload["basePower"]);
        Assert.Equal(1, defenderDamageEvent.Payload["keywordBonus"]);
        Assert.False(defenderDamageEvent.Payload.ContainsKey("staticPowerBonus"));
        Assert.Equal(5, defenderDamageEvent.Payload["combatPower"]);
        Assert.Equal(5, defenderDamageEvent.Payload["damage"]);
        Assert.Contains(result.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLE_CLOSED", StringComparison.Ordinal));
        AssertNoHiddenZoneLeak(result);
    }

    private static void AssertTaricBulwarkDamageAssignmentOrdering(
        ResolutionResult assignmentOpened,
        ResolutionResult battleResult)
    {
        var taricObjectId = FindBattlefieldUnitByCardNo(
            assignmentOpened.State,
            "P1",
            TaricSameBattlefieldStaticKeywordCardNo)
            ?? throw new InvalidOperationException("B0 Taric bulwark assignment assertion could not find Taric.");
        var battlefieldId = assignmentOpened.State.ObjectLocations[taricObjectId].BattlefieldObjectId
            ?? throw new InvalidOperationException("B0 Taric bulwark assignment assertion could not locate Taric's battlefield.");
        var leblancObjectId = FindBattlefieldUnitByCardNo(
            assignmentOpened.State,
            "P1",
            LeblancCardNo,
            battlefieldId)
            ?? throw new InvalidOperationException("B0 Taric bulwark assignment assertion could not find LeBlanc.");
        var attackerObjectId = FindBattlefieldUnitByCardNo(
            assignmentOpened.State,
            "P2",
            WildclawBeastmasterCardNo,
            battlefieldId)
            ?? throw new InvalidOperationException("B0 Taric bulwark assignment assertion could not find Wildclaw Beastmaster.");

        Assert.Contains(CardCombatKeywordNames.Bulwark, assignmentOpened.State.CardObjects[taricObjectId].Tags);
        Assert.Contains(CardCombatKeywordNames.BackRow, assignmentOpened.State.CardObjects[leblancObjectId].Tags);
        var assignmentPrompt = assignmentOpened.Prompts["P2"];
        Assert.Equal(PromptTypes.AssignCombatDamage, assignmentPrompt.View?.Type);
        var metadata = assignmentPrompt.View?.Metadata
            ?? throw new InvalidOperationException("B0 Taric bulwark assignment prompt missing metadata.");
        var legalTargets = StringListMap(metadata["legalTargets"]);
        Assert.Equal([taricObjectId, leblancObjectId], legalTargets[attackerObjectId]);
        var lethalThreshold = IntMap(metadata["lethalDamageThreshold"]);
        Assert.Equal(5, lethalThreshold[taricObjectId]);
        Assert.Equal(5, lethalThreshold[leblancObjectId]);

        var taricDamageIndex = EventIndex(battleResult.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "DAMAGE_APPLIED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, attackerObjectId, StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, taricObjectId, StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["combatRole"] as string, "ATTACKER", StringComparison.Ordinal));
        var taricDamageEvent = battleResult.Events[taricDamageIndex];
        Assert.Equal("BULWARK_FIRST", taricDamageEvent.Payload["assignmentRole"]);
        Assert.Equal(1, taricDamageEvent.Payload["assignmentIndex"]);
        Assert.Equal(5, taricDamageEvent.Payload["damage"]);

        var leblancDamageIndex = EventIndex(battleResult.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "DAMAGE_APPLIED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, attackerObjectId, StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, leblancObjectId, StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["combatRole"] as string, "ATTACKER", StringComparison.Ordinal));
        var leblancDamageEvent = battleResult.Events[leblancDamageIndex];
        Assert.True(taricDamageIndex < leblancDamageIndex);
        Assert.Equal("BACK_ROW_LAST", leblancDamageEvent.Payload["assignmentRole"]);
        Assert.Equal(2, leblancDamageEvent.Payload["assignmentIndex"]);
        Assert.Equal(2, leblancDamageEvent.Payload["damage"]);
        Assert.Contains(battleResult.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLE_CLOSED", StringComparison.Ordinal));
        AssertNoHiddenZoneLeak(assignmentOpened);
        AssertNoHiddenZoneLeak(battleResult);
    }

    private static async ValueTask<(MatchSession Session, ResolutionResult BattleReady, ResolutionResult BattleResult)> DriveOfficialLowCurveDecksToBattleCloseAsync(
        string roomId)
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var deck = BuildLowCurveOfficialDeck(catalog);
        return await DriveOfficialLowCurveDecksToBattleCloseAsync(roomId, deck, deck);
    }

    private static async ValueTask<(MatchSession Session, ResolutionResult BattleReady, ResolutionResult BattleResult)> DriveOfficialLowCurveDecksToBattleCloseAsync(
        string roomId,
        OfficialDecklist p1Deck,
        OfficialDecklist p2Deck)
    {
        var (session, skipped) = await DriveOfficialLowCurveDecksToNoLegalBattleSkipAsync(roomId, p1Deck, p2Deck);
        return await DriveSkippedOfficialLowCurveDecksToBattleCloseAsync(session, skipped);
    }

    private static async ValueTask<(MatchSession Session, ResolutionResult BattleReady, ResolutionResult BattleResult)> DriveOfficialLowCurveDecksToBattleCloseAsync(
        MatchState initialState,
        IMatchJournal journal,
        OfficialDecklist p1Deck,
        OfficialDecklist p2Deck)
    {
        var (session, skipped) = await DriveOfficialLowCurveDecksToNoLegalBattleSkipAsync(
            initialState,
            journal,
            p1Deck,
            p2Deck);
        return await DriveSkippedOfficialLowCurveDecksToBattleCloseAsync(session, skipped);
    }

    private static async ValueTask<(MatchSession Session, ResolutionResult BattleReady, ResolutionResult BattleResult)> DriveSkippedOfficialLowCurveDecksToBattleCloseAsync(
        MatchSession session,
        ResolutionResult skipped)
    {
        var current = skipped;
        ResolutionResult? battleReady = null;
        var skippedBattleCount = 0;
        for (var turnIndex = 0; turnIndex < 4; turnIndex++)
        {
            var turnStart = await EndTurnAsync(
                session,
                current.State.ActivePlayerId,
                $"b0-end-after-no-legal-battle-skip-{turnIndex}");
            AssertNoHiddenZoneLeak(turnStart);
            Assert.DoesNotContain(turnStart.State.UntilEndOfTurnEffects, effectId =>
                effectId.StartsWith(BattlefieldTaskMarkers.BattleSkippedPrefix, StringComparison.Ordinal));
            Assert.Equal(TimingStates.SpellDuelOpen, turnStart.State.TimingState);
            Assert.NotNull(turnStart.State.FocusPlayerId);
            Assert.Equal("SPELL_DUEL_TASKS", turnStart.State.PendingTaskQueue.Phase);
            Assert.Contains(turnStart.Events, gameEvent => string.Equals(gameEvent.Kind, "SPELL_DUEL_STARTED", StringComparison.Ordinal));

            current = await PassOpenSpellDuelAsync(session, turnStart, $"b0-reopen-pass-focus-{turnIndex}");
            AssertNoHiddenZoneLeak(current);
            if (string.Equals(current.State.PendingTaskQueue.Phase, "BATTLE_TASKS", StringComparison.Ordinal)
                && current.Prompts[current.State.ActivePlayerId].Actions.Contains("DECLARE_BATTLE", StringComparer.Ordinal))
            {
                battleReady = current;
                break;
            }

            skippedBattleCount++;
            Assert.Contains(current.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLE_SKIPPED", StringComparison.Ordinal));
            Assert.Equal("IDLE", current.State.PendingTaskQueue.Phase);
            Assert.DoesNotContain("DECLARE_BATTLE", current.Prompts["P1"].Actions);
            Assert.DoesNotContain("DECLARE_BATTLE", current.Prompts["P2"].Actions);
        }

        Assert.True(skippedBattleCount > 0);
        Assert.NotNull(battleReady);
        Assert.Equal("BATTLE_TASKS", battleReady.State.PendingTaskQueue.Phase);
        Assert.Equal("START_BATTLE", battleReady.State.PendingTaskQueue.Tasks.Single(task =>
            string.Equals(task.TaskId, battleReady.State.PendingTaskQueue.ActiveTaskId, StringComparison.Ordinal)).Kind);
        Assert.Equal(PromptTypes.BattleDeclaration, battleReady.Prompts[battleReady.State.ActivePlayerId].View?.Type);
        Assert.Contains("DECLARE_BATTLE", battleReady.Prompts[battleReady.State.ActivePlayerId].Actions);

        var battleResult = await SubmitFirstDeclareBattleCandidateAsync(
            session,
            battleReady,
            "b0-declare-reopened-official-battle");
        return (session, battleReady, battleResult);
    }

    private static async ValueTask<(MatchSession Session, ResolutionResult AssignmentOpened, ResolutionResult BattleResult)> DriveOfficialDecksToDamageAssignmentBattleCloseAsync(
        string roomId,
        OfficialDecklist p1Deck,
        OfficialDecklist p2Deck)
    {
        var initialState = BuildSeatedInitialState(roomId, LowCurveReplaySeed);
        return await DriveOfficialDecksToDamageAssignmentBattleCloseAsync(
            initialState,
            NoopMatchJournal.Instance,
            p1Deck,
            p2Deck);
    }

    private static async ValueTask<(MatchSession Session, ResolutionResult AssignmentOpened, ResolutionResult BattleResult)> DriveOfficialDecksToDamageAssignmentBattleCloseAsync(
        MatchState initialState,
        IMatchJournal journal,
        OfficialDecklist p1Deck,
        OfficialDecklist p2Deck)
    {
        var session = new MatchSession(initialState, new CoreRuleEngine(), journal);
        var p1Submit = await SubmitDeckAsync(session, "P1", p1Deck, "b0-damage-submit-p1");
        var p2Submit = await SubmitDeckAsync(session, "P2", p2Deck, "b0-damage-submit-p2");
        AssertAccepted(p1Submit);
        AssertAccepted(p2Submit);

        AssertAccepted(await session.ReadyAsync("P1", "b0-damage-ready-p1", RawCommand(CommandTypes.Ready), CancellationToken.None));
        var ready = await session.ReadyAsync("P2", "b0-damage-ready-p2", RawCommand(CommandTypes.Ready), CancellationToken.None);
        AssertAccepted(ready);
        AssertNoHiddenZoneLeak(ready);

        var activePlayerId = ready.State.ActivePlayerId;
        var secondPlayerId = ready.State.OpeningSecondActionPlayerId!;
        var activeMulligan = await session.SubmitAsync(
            activePlayerId,
            "b0-damage-mulligan-active",
            new MulliganCommand([]),
            RawCommand(new MulliganCommand([])),
            CancellationToken.None);
        AssertAccepted(activeMulligan);
        AssertNoHiddenZoneLeak(activeMulligan);

        var current = await session.SubmitAsync(
            secondPlayerId,
            "b0-damage-mulligan-second",
            new MulliganCommand([]),
            RawCommand(new MulliganCommand([])),
            CancellationToken.None);
        AssertAccepted(current);
        AssertNoHiddenZoneLeak(current);
        Assert.Equal(MatchPhases.Main, current.State.Phase);

        var battlefieldOwnerId = current.State.ActivePlayerId;
        current = await TapAllAvailableRunesAsync(session, battlefieldOwnerId, current, "b0-damage-owner-tap");
        current = await TryPlayFirstUnitAsync(session, battlefieldOwnerId, current, "b0-damage-owner-play-attacker", playUnitToBattlefield: true);

        var invadingPlayerId = OpponentOf(current.State, battlefieldOwnerId);
        current = await EndTurnAsync(session, battlefieldOwnerId, "b0-damage-end-owner-setup");
        AssertNoHiddenZoneLeak(current);

        current = await DriveTwoAssignmentDefendersOntoBattlefieldAsync(
            session,
            current,
            invadingPlayerId,
            battlefieldOwnerId);

        var assignmentOpened = await DriveContestedBattlefieldToDamageAssignmentAsync(
            session,
            current,
            battlefieldOwnerId,
            invadingPlayerId);
        var battleResult = await ResolveOpenBattleDamageAssignmentsAsync(
            session,
            assignmentOpened,
            "b0-damage-assignment");
        return (session, assignmentOpened, battleResult);
    }

    private static async ValueTask<(
        MatchSession Session,
        ResolutionResult OpenedResponse,
        ResolutionResult Activated,
        ResolutionResult StackResolved,
        ResolutionResult BattleResult,
        string TargetObjectId)> DriveOfficialDecksToShadowResponseBattleCloseAsync(
        string roomId,
        OfficialDecklist p1Deck,
        OfficialDecklist p2Deck)
    {
        var failures = new List<string>();
        foreach (var seed in ShadowResponseDriverSeeds)
        {
            try
            {
                return await DriveOfficialDecksToShadowResponseBattleCloseAsync(
                    $"{roomId}-{seed}",
                    p1Deck,
                    p2Deck,
                    seed);
            }
            catch (InvalidOperationException ex) when (ex.Message.StartsWith("B0 shadow-response driver", StringComparison.Ordinal))
            {
                failures.Add($"{seed}: {ex.Message}");
            }
            catch (MatchSessionException ex) when (ex.Message.Contains("对局已经结束", StringComparison.Ordinal))
            {
                failures.Add($"{seed}: match ended before Shadow response path");
            }
        }

        throw new InvalidOperationException(
            "B0 shadow-response driver could not find a deterministic official-deck Shadow response path. "
            + string.Join(" | ", failures));
    }

    private static async ValueTask<(
        MatchSession Session,
        ResolutionResult OpenedResponse,
        ResolutionResult Activated,
        ResolutionResult StackResolved,
        ResolutionResult BattleResult,
        string TargetObjectId)> DriveOfficialDecksToShadowResponseBattleCloseAsync(
        string roomId,
        OfficialDecklist p1Deck,
        OfficialDecklist p2Deck,
        int seed)
    {
        var initialState = BuildSeatedInitialState(roomId, seed);
        return await DriveOfficialDecksToShadowResponseBattleCloseAsync(
            initialState,
            NoopMatchJournal.Instance,
            p1Deck,
            p2Deck);
    }

    private static async ValueTask<(
        MatchSession Session,
        ResolutionResult OpenedResponse,
        ResolutionResult Activated,
        ResolutionResult StackResolved,
        ResolutionResult BattleResult,
        string TargetObjectId)> DriveOfficialDecksToShadowResponseBattleCloseAsync(
        MatchState initialState,
        IMatchJournal journal,
        OfficialDecklist p1Deck,
        OfficialDecklist p2Deck)
    {
        var session = new MatchSession(initialState, new CoreRuleEngine(), journal);
        var p1Submit = await SubmitDeckAsync(session, "P1", p1Deck, "b0-shadow-submit-p1");
        var p2Submit = await SubmitDeckAsync(session, "P2", p2Deck, "b0-shadow-submit-p2");
        AssertAccepted(p1Submit);
        AssertAccepted(p2Submit);

        AssertAccepted(await session.ReadyAsync("P1", "b0-shadow-ready-p1", RawCommand(CommandTypes.Ready), CancellationToken.None));
        var ready = await session.ReadyAsync("P2", "b0-shadow-ready-p2", RawCommand(CommandTypes.Ready), CancellationToken.None);
        AssertAccepted(ready);
        AssertNoHiddenZoneLeak(ready);

        var activePlayerId = ready.State.ActivePlayerId;
        var secondPlayerId = ready.State.OpeningSecondActionPlayerId!;
        var activeMulligan = await session.SubmitAsync(
            activePlayerId,
            "b0-shadow-mulligan-active",
            new MulliganCommand([]),
            RawCommand(new MulliganCommand([])),
            CancellationToken.None);
        AssertAccepted(activeMulligan);
        AssertNoHiddenZoneLeak(activeMulligan);

        var current = await session.SubmitAsync(
            secondPlayerId,
            "b0-shadow-mulligan-second",
            new MulliganCommand([]),
            RawCommand(new MulliganCommand([])),
            CancellationToken.None);
        AssertAccepted(current);
        AssertNoHiddenZoneLeak(current);
        Assert.Equal(MatchPhases.Main, current.State.Phase);

        var battlefieldOwnerId = current.State.ActivePlayerId;
        current = await TapAllAvailableRunesAsync(session, battlefieldOwnerId, current, "b0-shadow-owner-tap");
        current = await TryPlayFirstUnitAsync(session, battlefieldOwnerId, current, "b0-shadow-owner-play-attacker", playUnitToBattlefield: true);

        var shadowControllerId = OpponentOf(current.State, battlefieldOwnerId);
        current = await EndTurnAsync(session, battlefieldOwnerId, "b0-shadow-end-owner-setup");
        AssertNoHiddenZoneLeak(current);

        current = await DriveShadowOntoBattlefieldAsync(
            session,
            current,
            shadowControllerId,
            battlefieldOwnerId);

        var openedResponse = await DriveContestedBattlefieldToShadowResponseAsync(
            session,
            current,
            battlefieldOwnerId,
            shadowControllerId);
        var (activated, targetObjectId) = await ActivateCurrentShadowResponseAsync(
            session,
            openedResponse,
            openedResponse.State.PriorityPlayerId!,
            "b0-shadow-activate-response");
        var stackResolved = await ResolveCurrentStackOnlyAsync(session, activated, "b0-shadow-resolve-stack");
        var battleResult = await PassOpenBattleResponseAsync(session, stackResolved, "b0-shadow-pass-returned-response");
        return (session, openedResponse, activated, stackResolved, battleResult, targetObjectId);
    }

    private static async ValueTask<(
        MatchState InitialState,
        RecordingMatchJournal Journal,
        MatchSession Session,
        ResolutionResult Hidden,
        ResolutionResult OpenedResponse,
        ResolutionResult Activated,
        ResolutionResult Revealed,
        ResolutionResult TeemoResolved,
        ResolutionResult ShadowResolved,
        ResolutionResult BattleResult,
        string TeemoObjectId,
        string TargetObjectId)> DriveOfficialDecksToStandbyReactionShadowBattleCloseForReplayAsync(
        string roomId,
        OfficialDecklist p1Deck,
        OfficialDecklist p2Deck)
    {
        var failures = new List<string>();
        foreach (var seed in ShadowResponseDriverSeeds.Concat(StandbyDriverSeeds).Distinct())
        {
            var initialState = BuildSeatedInitialState($"{roomId}-{seed}", seed);
            var journal = new RecordingMatchJournal();
            try
            {
                var result = await DriveOfficialDecksToStandbyReactionShadowBattleCloseAsync(
                    initialState,
                    journal,
                    p1Deck,
                    p2Deck);
                return (
                    initialState,
                    journal,
                    result.Session,
                    result.Hidden,
                    result.OpenedResponse,
                    result.Activated,
                    result.Revealed,
                    result.TeemoResolved,
                    result.ShadowResolved,
                    result.BattleResult,
                    result.TeemoObjectId,
                    result.TargetObjectId);
            }
            catch (InvalidOperationException ex) when (
                ex.Message.StartsWith("B0 standby-reaction driver", StringComparison.Ordinal)
                || ex.Message.StartsWith("B0 shadow-response driver", StringComparison.Ordinal)
                || ex.Message.StartsWith("B0 auto-driver", StringComparison.Ordinal))
            {
                failures.Add($"{seed}: {ex.Message}");
            }
            catch (MatchSessionException ex) when (ex.Message.Contains("对局已经结束", StringComparison.Ordinal))
            {
                failures.Add($"{seed}: match ended before standby reaction response path");
            }
        }

        throw new InvalidOperationException(
            "B0 standby-reaction driver could not find a deterministic official-deck standby reaction response path. "
            + string.Join(" | ", failures));
    }

    private static async ValueTask<(
        MatchSession Session,
        ResolutionResult Hidden,
        ResolutionResult OpenedResponse,
        ResolutionResult Activated,
        ResolutionResult Revealed,
        ResolutionResult TeemoResolved,
        ResolutionResult ShadowResolved,
        ResolutionResult BattleResult,
        string TeemoObjectId,
        string TargetObjectId)> DriveOfficialDecksToStandbyReactionShadowBattleCloseAsync(
        MatchState initialState,
        IMatchJournal journal,
        OfficialDecklist p1Deck,
        OfficialDecklist p2Deck)
    {
        var session = new MatchSession(initialState, new CoreRuleEngine(), journal);
        var p1Submit = await SubmitDeckAsync(session, "P1", p1Deck, "b0-standby-reaction-submit-p1");
        var p2Submit = await SubmitDeckAsync(session, "P2", p2Deck, "b0-standby-reaction-submit-p2");
        AssertAccepted(p1Submit);
        AssertAccepted(p2Submit);

        AssertAccepted(await session.ReadyAsync("P1", "b0-standby-reaction-ready-p1", RawCommand(CommandTypes.Ready), CancellationToken.None));
        var ready = await session.ReadyAsync("P2", "b0-standby-reaction-ready-p2", RawCommand(CommandTypes.Ready), CancellationToken.None);
        AssertAccepted(ready);
        AssertNoHiddenZoneLeak(ready);

        var activePlayerId = ready.State.ActivePlayerId;
        var secondPlayerId = ready.State.OpeningSecondActionPlayerId!;
        var activeMulligan = await session.SubmitAsync(
            activePlayerId,
            "b0-standby-reaction-mulligan-active",
            new MulliganCommand([]),
            RawCommand(new MulliganCommand([])),
            CancellationToken.None);
        AssertAccepted(activeMulligan);
        AssertNoHiddenZoneLeak(activeMulligan);

        var current = await session.SubmitAsync(
            secondPlayerId,
            "b0-standby-reaction-mulligan-second",
            new MulliganCommand([]),
            RawCommand(new MulliganCommand([])),
            CancellationToken.None);
        AssertAccepted(current);
        AssertNoHiddenZoneLeak(current);
        Assert.Equal(MatchPhases.Main, current.State.Phase);

        var battlefieldOwnerId = current.State.ActivePlayerId;
        var setup = await DriveStandbyReactionOwnerSetupAsync(
            session,
            current,
            battlefieldOwnerId,
            "b0-standby-reaction-owner-setup");
        current = setup.Current;
        var hidden = setup.Hidden;
        var teemoObjectId = setup.TeemoObjectId;

        var shadowControllerId = OpponentOf(current.State, battlefieldOwnerId);
        current = await EndTurnAsync(session, battlefieldOwnerId, "b0-standby-reaction-end-owner-setup");
        AssertNoHiddenZoneLeak(current);

        current = await DriveShadowOntoBattlefieldAsync(
            session,
            current,
            shadowControllerId,
            battlefieldOwnerId);

        var openedResponse = await DriveContestedBattlefieldToShadowResponseAsync(
            session,
            current,
            battlefieldOwnerId,
            shadowControllerId);
        var (activated, targetObjectId) = await ActivateCurrentShadowResponseAsync(
            session,
            openedResponse,
            openedResponse.State.PriorityPlayerId!,
            "b0-standby-reaction-activate-shadow");
        var standbyPriority = await PassPriorityUntilAsync(
            session,
            activated,
            battlefieldOwnerId,
            "b0-standby-reaction-pass-to-standby");
        var (revealed, revealedObjectId) = await SubmitStandbyReactionRevealCandidateAsync(
            session,
            standbyPriority,
            battlefieldOwnerId,
            TeemoSelfPowerCardNo,
            "b0-standby-reaction-reveal-teemo");
        Assert.Equal(teemoObjectId, revealedObjectId);

        var teemoResolved = await ResolveOneStackItemPassPassAsync(session, revealed, "b0-standby-reaction-resolve-teemo");
        var shadowResolved = await ResolveCurrentStackOnlyAsync(session, teemoResolved, "b0-standby-reaction-resolve-shadow");
        var battleResult = await PassOpenBattleResponseAsync(session, shadowResolved, "b0-standby-reaction-pass-returned-response");
        return (session, hidden, openedResponse, activated, revealed, teemoResolved, shadowResolved, battleResult, teemoObjectId, targetObjectId);
    }

    private static async ValueTask<(
        ResolutionResult Current,
        ResolutionResult Hidden,
        string TeemoObjectId)> DriveStandbyReactionOwnerSetupAsync(
        MatchSession session,
        ResolutionResult current,
        string battlefieldOwnerId,
        string intentPrefix)
    {
        var result = current;
        ResolutionResult? hidden = null;
        string? teemoObjectId = null;
        for (var turnIndex = 0; turnIndex < 40; turnIndex++)
        {
            if (!string.Equals(result.State.ActivePlayerId, battlefieldOwnerId, StringComparison.Ordinal))
            {
                result = await EndTurnAsync(session, result.State.ActivePlayerId, $"{intentPrefix}-wait-for-owner-{turnIndex}");
                AssertNoHiddenZoneLeak(result);
                continue;
            }

            result = await TapAllAvailableRunesAsync(session, battlefieldOwnerId, result, $"{intentPrefix}-tap-{turnIndex}");
            if (hidden is null)
            {
                var hideCandidate = EnabledCandidate(result.Prompts[battlefieldOwnerId], CommandTypes.HideCard);
                var canHideTeemo = hideCandidate?.Sources?.Any(source =>
                    result.State.CardObjects.TryGetValue(source.Id, out var cardObject)
                    && string.Equals(cardObject.CardNo, TeemoSelfPowerCardNo, StringComparison.Ordinal)) == true;
                if (canHideTeemo)
                {
                    (hidden, teemoObjectId) = await SubmitHideSpecificCardCandidateAsync(
                        session,
                        result,
                        battlefieldOwnerId,
                        TeemoSelfPowerCardNo,
                        $"{intentPrefix}-hide-teemo-{turnIndex}");
                    result = hidden;
                }
            }

            if (hidden is not null
                && EnabledCandidate(result.Prompts[battlefieldOwnerId], CommandTypes.PlayCard) is not null)
            {
                try
                {
                    result = await TryPlayFirstUnitAsync(
                        session,
                        battlefieldOwnerId,
                        result,
                        $"{intentPrefix}-play-attacker-{turnIndex}",
                        playUnitToBattlefield: true);
                    return (result, hidden, teemoObjectId!);
                }
                catch (InvalidOperationException ex) when (ex.Message.StartsWith("B0 auto-driver", StringComparison.Ordinal))
                {
                    // Keep looking on the next natural turn; the prompt may have exposed only sources this narrow route cannot use.
                }
            }

            result = await EndTurnAsync(session, battlefieldOwnerId, $"{intentPrefix}-end-owner-{turnIndex}");
            AssertNoHiddenZoneLeak(result);
        }

        throw new InvalidOperationException("B0 standby-reaction driver could not stage hidden Teemo plus a battlefield attacker.");
    }

    private static async ValueTask<(
        MatchState InitialState,
        RecordingMatchJournal Journal,
        ResolutionResult Hidden,
        ResolutionResult Revealed,
        ResolutionResult BattleResult,
        ResolutionResult Result)> DriveOfficialStandbyDecksToHideRevealScoreVictoryForReplayAsync(
        string roomId,
        OfficialDecklist p1Deck,
        OfficialDecklist p2Deck)
    {
        var failures = new List<string>();
        foreach (var seed in StandbyDriverSeeds)
        {
            var initialState = BuildSeatedInitialState($"{roomId}-{seed}", seed);
            var journal = new RecordingMatchJournal();
            try
            {
                var result = await DriveOfficialStandbyDecksToHideRevealScoreVictoryAsync(
                    initialState,
                    journal,
                    p1Deck,
                    p2Deck);
                return (initialState, journal, result.Hidden, result.Revealed, result.BattleResult, result.Result);
            }
            catch (InvalidOperationException ex) when (ex.Message.StartsWith("B0 standby driver", StringComparison.Ordinal))
            {
                failures.Add($"{seed}: {ex.Message}");
            }
            catch (MatchSessionException ex) when (ex.Message.Contains("对局已经结束", StringComparison.Ordinal))
            {
                failures.Add($"{seed}: match ended before standby hide/reveal score path");
            }
        }

        throw new InvalidOperationException(
            "B0 standby driver could not find a deterministic official-deck hide/reveal score path. "
            + string.Join(" | ", failures));
    }

    private static async ValueTask<(
        MatchState InitialState,
        RecordingMatchJournal Journal,
        ResolutionResult Hidden,
        ResolutionResult BattleResult,
        ResolutionResult Result,
        string HiddenObjectId,
        string BattlefieldObjectId)> DriveOfficialStandbyDecksToBattlefieldExtraStandbyHideScoreVictoryForReplayAsync(
        string roomId,
        OfficialDecklist p1Deck,
        OfficialDecklist p2Deck)
    {
        var failures = new List<string>();
        foreach (var seed in BattlefieldExtraStandbyDriverSeeds())
        {
            var initialState = BuildSeatedInitialState($"{roomId}-{seed}", seed);
            var journal = new RecordingMatchJournal();
            try
            {
                var result = await DriveOfficialStandbyDecksToHideRevealScoreVictoryAsync(
                    initialState,
                    journal,
                    p1Deck,
                    p2Deck,
                    useBattlefieldExtraStandby: true);
                return (
                    initialState,
                    journal,
                    result.Hidden,
                    result.BattleResult,
                    result.Result,
                    result.HiddenObjectId,
                    result.ExtraStandbyBattlefieldObjectId!);
            }
            catch (InvalidOperationException ex) when (ex.Message.StartsWith("B0 standby driver", StringComparison.Ordinal))
            {
                if (failures.Count < 40)
                {
                    failures.Add($"{seed}: {ex.Message}");
                }
            }
            catch (MatchSessionException ex) when (ex.Message.Contains("对局已经结束", StringComparison.Ordinal))
            {
                if (failures.Count < 40)
                {
                    failures.Add($"{seed}: match ended before battlefield extra-standby score path");
                }
            }
        }

        throw new InvalidOperationException(
            "B0 standby driver could not find a deterministic official-deck battlefield extra-standby score path. "
            + string.Join(" | ", failures));
    }

    private static IEnumerable<int> BattlefieldExtraStandbyDriverSeeds()
    {
        foreach (var seed in StandbyDriverSeeds)
        {
            yield return seed;
        }

        var knownSeeds = StandbyDriverSeeds.ToHashSet();
        for (var seed = 0; seed < 512; seed++)
        {
            if (!knownSeeds.Contains(seed))
            {
                yield return seed;
            }
        }
    }

    private static async ValueTask<(
        ResolutionResult Hidden,
        ResolutionResult Revealed,
        ResolutionResult BattleResult,
        ResolutionResult Result,
        string HiddenObjectId,
        string? ExtraStandbyBattlefieldObjectId)> DriveOfficialStandbyDecksToHideRevealScoreVictoryAsync(
        MatchState initialState,
        IMatchJournal journal,
        OfficialDecklist p1Deck,
        OfficialDecklist p2Deck,
        bool useBattlefieldExtraStandby = false)
    {
        var session = new MatchSession(initialState, new CoreRuleEngine(), journal);
        var p1Submit = await SubmitDeckAsync(session, "P1", p1Deck, "b0-standby-submit-p1");
        var p2Submit = await SubmitDeckAsync(session, "P2", p2Deck, "b0-standby-submit-p2");
        AssertAccepted(p1Submit);
        AssertAccepted(p2Submit);

        AssertAccepted(await session.ReadyAsync("P1", "b0-standby-ready-p1", RawCommand(CommandTypes.Ready), CancellationToken.None));
        var ready = await session.ReadyAsync("P2", "b0-standby-ready-p2", RawCommand(CommandTypes.Ready), CancellationToken.None);
        AssertAccepted(ready);
        AssertNoHiddenZoneLeak(ready);

        var activePlayerId = ready.State.ActivePlayerId;
        var secondPlayerId = ready.State.OpeningSecondActionPlayerId!;
        var activeMulligan = await session.SubmitAsync(
            activePlayerId,
            "b0-standby-mulligan-active",
            new MulliganCommand([]),
            RawCommand(new MulliganCommand([])),
            CancellationToken.None);
        AssertAccepted(activeMulligan);
        AssertNoHiddenZoneLeak(activeMulligan);

        var current = await session.SubmitAsync(
            secondPlayerId,
            "b0-standby-mulligan-second",
            new MulliganCommand([]),
            RawCommand(new MulliganCommand([])),
            CancellationToken.None);
        AssertAccepted(current);
        AssertNoHiddenZoneLeak(current);
        Assert.Equal(MatchPhases.Main, current.State.Phase);

        var standbyPlayerId = current.State.ActivePlayerId;
        current = await TapAllAvailableRunesAsync(session, standbyPlayerId, current, "b0-standby-hide-tap");
        ResolutionResult hidden;
        string hiddenObjectId;
        string? extraStandbyBattlefieldObjectId = null;
        if (useBattlefieldExtraStandby)
        {
            (hidden, hiddenObjectId, extraStandbyBattlefieldObjectId) = await SubmitFirstBattlefieldExtraStandbyHideCardCandidateAsync(
                session,
                current,
                standbyPlayerId,
                "b0-standby-hide-card");
        }
        else
        {
            (hidden, hiddenObjectId) = await SubmitFirstHideCardCandidateAsync(
                session,
                current,
                standbyPlayerId,
                "b0-standby-hide-card");
        }

        ResolutionResult revealed;
        if (useBattlefieldExtraStandby)
        {
            revealed = hidden;
        }
        else
        {
            var (ordinaryReveal, revealedObjectId) = await SubmitFirstRevealCardCandidateAsync(
                session,
                hidden,
                standbyPlayerId,
                "b0-standby-reveal-card");
            Assert.Equal(hiddenObjectId, revealedObjectId);
            Assert.Contains(revealedObjectId, ordinaryReveal.State.PlayerZones[standbyPlayerId].Base, StringComparer.Ordinal);
            Assert.False(ordinaryReveal.State.CardObjects[revealedObjectId].IsFaceDown);
            revealed = ordinaryReveal;
        }

        current = await EndTurnAsync(session, standbyPlayerId, "b0-standby-end-after-reveal");
        AssertNoHiddenZoneLeak(current);
        var opponentId = current.State.ActivePlayerId;
        current = await PreparePlayerBoardAsync(session, opponentId, current, "standby-opponent", playUnitToBattlefield: false);
        current = await EndTurnAsync(session, opponentId, "b0-standby-end-opponent-setup");
        AssertNoHiddenZoneLeak(current);
        if (!string.Equals(current.State.ActivePlayerId, standbyPlayerId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("B0 standby driver expected standby player to regain the turn after opponent setup.");
        }

        current = await PreparePlayerBoardAsync(session, standbyPlayerId, current, "standby-owner", playUnitToBattlefield: true);
        current = await EndTurnAsync(session, standbyPlayerId, "b0-standby-end-owner-setup");
        AssertNoHiddenZoneLeak(current);
        if (!string.Equals(current.State.ActivePlayerId, opponentId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("B0 standby driver expected opponent to regain the turn after standby owner setup.");
        }

        current = await MoveBaseUnitToOpponentBattlefieldAsync(session, opponentId, current);
        current = await PassOpenSpellDuelAsync(session, current, "b0-standby-pass-focus");
        Assert.Contains(current.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLE_SKIPPED", StringComparison.Ordinal));

        var (_, _, battleResult) = await DriveSkippedOfficialLowCurveDecksToBattleCloseAsync(session, current);
        var result = await DriveBattleCloseToScoreVictoryAsync(session, battleResult, "b0-standby-score");
        return (hidden, revealed, battleResult, result, hiddenObjectId, extraStandbyBattlefieldObjectId);
    }

    private static async ValueTask<(ResolutionResult Result, string SourceObjectId)> SubmitFirstHideCardCandidateAsync(
        MatchSession session,
        ResolutionResult current,
        string playerId,
        string intentId)
    {
        var candidate = EnabledCandidate(current.Prompts[playerId], CommandTypes.HideCard)
            ?? throw new InvalidOperationException($"B0 standby driver could not find HIDE_CARD for {playerId}.");
        var sourceObjectId = candidate.Sources?.FirstOrDefault()?.Id
            ?? throw new InvalidOperationException("B0 standby driver could not find a standby hide source.");
        var cardNo = current.State.CardObjects.TryGetValue(sourceObjectId, out var cardObject)
            ? cardObject.CardNo
            : null;
        if (string.IsNullOrWhiteSpace(cardNo))
        {
            throw new InvalidOperationException("B0 standby driver could not resolve the standby hide card number.");
        }

        var destination = candidate.Destinations?.FirstOrDefault(choice => string.Equals(choice.Id, "STANDBY", StringComparison.Ordinal))?.Id
            ?? candidate.Destinations?.FirstOrDefault()?.Id
            ?? "STANDBY";
        var optionalCost = candidate.OptionalCosts?.FirstOrDefault(choice => string.Equals(choice.Id, "STANDBY_A", StringComparison.Ordinal))?.Id
            ?? candidate.OptionalCosts?.FirstOrDefault()?.Id
            ?? throw new InvalidOperationException("B0 standby driver could not find a standby hide optional cost.");
        var command = new HideCardCommand(sourceObjectId, cardNo, destination, [optionalCost]);
        var result = await session.SubmitAsync(
            playerId,
            intentId,
            command,
            RawCommand(command),
            CancellationToken.None);
        AssertAccepted(result);
        AssertNoHiddenZoneLeak(result);
        return (result, sourceObjectId);
    }

    private static async ValueTask<(ResolutionResult Result, string SourceObjectId, string BattlefieldObjectId)> SubmitFirstBattlefieldExtraStandbyHideCardCandidateAsync(
        MatchSession session,
        ResolutionResult current,
        string playerId,
        string intentId)
    {
        var candidate = EnabledCandidate(current.Prompts[playerId], CommandTypes.HideCard)
            ?? throw new InvalidOperationException($"B0 standby driver could not find HIDE_CARD for {playerId}.");
        var sourceObjectId = candidate.Sources?.FirstOrDefault()?.Id
            ?? throw new InvalidOperationException("B0 standby driver could not find a standby hide source.");
        var cardNo = current.State.CardObjects.TryGetValue(sourceObjectId, out var cardObject)
            ? cardObject.CardNo
            : null;
        if (string.IsNullOrWhiteSpace(cardNo))
        {
            throw new InvalidOperationException("B0 standby driver could not resolve the standby hide card number.");
        }

        var destination = candidate.Destinations?
            .FirstOrDefault(choice => choice.Id.StartsWith("BATTLEFIELD:", StringComparison.Ordinal))?.Id
            ?? throw new InvalidOperationException("B0 standby driver could not find a battlefield extra-standby destination.");
        var battlefieldObjectId = destination["BATTLEFIELD:".Length..];
        Assert.False(string.IsNullOrWhiteSpace(battlefieldObjectId));
        Assert.True(current.State.CardObjects.TryGetValue(battlefieldObjectId, out var battlefieldObject));
        Assert.Equal(BandleTreeBattlefieldCardNo, battlefieldObject.CardNo);

        var optionalCost = candidate.OptionalCosts?.FirstOrDefault(choice => string.Equals(choice.Id, "STANDBY_A", StringComparison.Ordinal))?.Id
            ?? candidate.OptionalCosts?.FirstOrDefault()?.Id
            ?? throw new InvalidOperationException("B0 standby driver could not find a standby hide optional cost.");
        var command = new HideCardCommand(sourceObjectId, cardNo, destination, [optionalCost]);
        var result = await session.SubmitAsync(
            playerId,
            intentId,
            command,
            RawCommand(command),
            CancellationToken.None);
        AssertAccepted(result);
        AssertNoHiddenZoneLeak(result);
        return (result, sourceObjectId, battlefieldObjectId);
    }

    private static async ValueTask<(ResolutionResult Result, string SourceObjectId)> SubmitFirstRevealCardCandidateAsync(
        MatchSession session,
        ResolutionResult current,
        string playerId,
        string intentId)
    {
        var candidate = EnabledCandidate(current.Prompts[playerId], CommandTypes.RevealCard)
            ?? throw new InvalidOperationException($"B0 standby driver could not find REVEAL_CARD for {playerId}.");
        var sourceObjectId = candidate.Sources?.FirstOrDefault()?.Id
            ?? throw new InvalidOperationException("B0 standby driver could not find a standby reveal source.");
        var cardNo = current.State.CardObjects.TryGetValue(sourceObjectId, out var cardObject)
            ? cardObject.CardNo
            : null;
        if (string.IsNullOrWhiteSpace(cardNo))
        {
            throw new InvalidOperationException("B0 standby driver could not resolve the standby reveal card number.");
        }

        var mode = candidate.Modes?.FirstOrDefault(choice => string.Equals(choice.Id, "STANDBY_REVEAL", StringComparison.Ordinal))?.Id
            ?? candidate.Modes?.FirstOrDefault()?.Id
            ?? "STANDBY_REVEAL";
        var destination = candidate.Destinations?.FirstOrDefault(choice => string.Equals(choice.Id, "BASE", StringComparison.Ordinal))?.Id
            ?? candidate.Destinations?.FirstOrDefault()?.Id
            ?? "BASE";
        var optionalCost = candidate.OptionalCosts?.FirstOrDefault(choice => string.Equals(choice.Id, "STANDBY_REVEAL_0", StringComparison.Ordinal))?.Id
            ?? candidate.OptionalCosts?.FirstOrDefault()?.Id
            ?? throw new InvalidOperationException("B0 standby driver could not find a standby reveal optional cost.");
        var command = new RevealCardCommand(
            sourceObjectId,
            cardNo,
            [],
            Mode: mode,
            OptionalCosts: [optionalCost],
            Destination: destination);
        var result = await session.SubmitAsync(
            playerId,
            intentId,
            command,
            RawCommand(command),
            CancellationToken.None);
        AssertAccepted(result);
        AssertNoHiddenZoneLeak(result);
        return (result, sourceObjectId);
    }

    private static async ValueTask<(ResolutionResult Result, string SourceObjectId)> SubmitHideSpecificCardCandidateAsync(
        MatchSession session,
        ResolutionResult current,
        string playerId,
        string expectedCardNo,
        string intentId)
    {
        var candidate = EnabledCandidate(current.Prompts[playerId], CommandTypes.HideCard)
            ?? throw new InvalidOperationException($"B0 standby-reaction driver could not find HIDE_CARD for {playerId}.");
        var sourceObjectId = (candidate.Sources ?? [])
            .Select(source => source.Id)
            .FirstOrDefault(sourceObjectId => current.State.CardObjects.TryGetValue(sourceObjectId, out var cardObject)
                && string.Equals(cardObject.CardNo, expectedCardNo, StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"B0 standby-reaction driver could not find {expectedCardNo} in HIDE_CARD sources.");
        var destination = candidate.Destinations?.FirstOrDefault(choice => string.Equals(choice.Id, "STANDBY", StringComparison.Ordinal))?.Id
            ?? throw new InvalidOperationException("B0 standby-reaction driver could not find STANDBY hide destination.");
        var optionalCost = candidate.OptionalCosts?.FirstOrDefault(choice => string.Equals(choice.Id, "STANDBY_A", StringComparison.Ordinal))?.Id
            ?? throw new InvalidOperationException("B0 standby-reaction driver could not find STANDBY_A hide optional cost.");
        var command = new HideCardCommand(sourceObjectId, expectedCardNo, destination, [optionalCost]);
        var result = await session.SubmitAsync(
            playerId,
            intentId,
            command,
            RawCommand(command),
            CancellationToken.None);
        AssertAccepted(result);
        AssertNoHiddenZoneLeak(result);
        return (result, sourceObjectId);
    }

    private static async ValueTask<(ResolutionResult Result, string SourceObjectId)> SubmitStandbyReactionRevealCandidateAsync(
        MatchSession session,
        ResolutionResult current,
        string playerId,
        string expectedCardNo,
        string intentId)
    {
        var candidate = EnabledCandidate(current.Prompts[playerId], CommandTypes.RevealCard)
            ?? throw new InvalidOperationException($"B0 standby-reaction driver could not find REVEAL_CARD for {playerId}.");
        var sourceObjectId = (candidate.Sources ?? [])
            .Select(source => source.Id)
            .FirstOrDefault(sourceObjectId => current.State.CardObjects.TryGetValue(sourceObjectId, out var cardObject)
                && string.Equals(cardObject.CardNo, expectedCardNo, StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"B0 standby-reaction driver could not find {expectedCardNo} in REVEAL_CARD sources.");
        var mode = candidate.Modes?.FirstOrDefault(choice => string.Equals(choice.Id, "STANDBY_REACTION", StringComparison.Ordinal))?.Id
            ?? throw new InvalidOperationException("B0 standby-reaction driver could not find STANDBY_REACTION reveal mode.");
        var destination = candidate.Destinations?.FirstOrDefault(choice => string.Equals(choice.Id, "STACK", StringComparison.Ordinal))?.Id
            ?? throw new InvalidOperationException("B0 standby-reaction driver could not find STACK reveal destination.");
        var optionalCost = candidate.OptionalCosts?.FirstOrDefault(choice => string.Equals(choice.Id, "STANDBY_REVEAL_0", StringComparison.Ordinal))?.Id
            ?? throw new InvalidOperationException("B0 standby-reaction driver could not find STANDBY_REVEAL_0 optional cost.");
        var command = new RevealCardCommand(
            sourceObjectId,
            expectedCardNo,
            [],
            Mode: mode,
            OptionalCosts: [optionalCost],
            Destination: destination);
        var result = await session.SubmitAsync(
            playerId,
            intentId,
            command,
            RawCommand(command),
            CancellationToken.None);
        AssertAccepted(result);
        AssertNoHiddenZoneLeak(result);
        return (result, sourceObjectId);
    }

    private static async ValueTask<(
        MatchState InitialState,
        RecordingMatchJournal Journal,
        MatchSession Session,
        ResolutionResult OpenedResponse,
        ResolutionResult Activated,
        ResolutionResult StackResolved,
        ResolutionResult BattleResult,
        string TargetObjectId)> DriveOfficialDecksToShadowResponseBattleCloseForReplayAsync(
        string roomId,
        OfficialDecklist p1Deck,
        OfficialDecklist p2Deck)
    {
        var failures = new List<string>();
        foreach (var seed in ShadowResponseDriverSeeds)
        {
            var initialState = BuildSeatedInitialState($"{roomId}-{seed}", seed);
            var journal = new RecordingMatchJournal();
            try
            {
                var result = await DriveOfficialDecksToShadowResponseBattleCloseAsync(
                    initialState,
                    journal,
                    p1Deck,
                    p2Deck);
                return (initialState, journal, result.Session, result.OpenedResponse, result.Activated, result.StackResolved, result.BattleResult, result.TargetObjectId);
            }
            catch (InvalidOperationException ex) when (ex.Message.StartsWith("B0 shadow-response driver", StringComparison.Ordinal))
            {
                failures.Add($"{seed}: {ex.Message}");
            }
            catch (MatchSessionException ex) when (ex.Message.Contains("对局已经结束", StringComparison.Ordinal))
            {
                failures.Add($"{seed}: match ended before Shadow response path");
            }
        }

        throw new InvalidOperationException(
            "B0 shadow-response replay driver could not find a deterministic official-deck Shadow response path. "
            + string.Join(" | ", failures));
    }

    private static async ValueTask<ResolutionResult> DriveShadowOntoBattlefieldAsync(
        MatchSession session,
        ResolutionResult current,
        string shadowControllerId,
        string battlefieldOwnerId)
    {
        var result = current;
        for (var turnIndex = 0; turnIndex < 40; turnIndex++)
        {
            if (!string.Equals(result.State.ActivePlayerId, shadowControllerId, StringComparison.Ordinal))
            {
                result = await EndTurnAsync(session, result.State.ActivePlayerId, $"b0-shadow-wait-for-controller-{turnIndex}");
                AssertNoHiddenZoneLeak(result);
                continue;
            }

            result = await TapAllAvailableRunesAsync(session, shadowControllerId, result, $"b0-shadow-controller-tap-{turnIndex}");
            var pool = result.State.RunePools[shadowControllerId];
            if (!PlayerHandContainsCardNo(result.State, shadowControllerId, ShadowCardNo)
                || pool.Mana < 4)
            {
                result = await EndTurnAsync(session, shadowControllerId, $"b0-shadow-wait-for-card-resources-{turnIndex}");
                AssertNoHiddenZoneLeak(result);
                continue;
            }

            var battlefieldDestination = BattlefieldDestinationFor(result.State, battlefieldOwnerId);
            result = await PlaySpecificUnitToBattlefieldAsync(
                session,
                shadowControllerId,
                result,
                ShadowCardNo,
                battlefieldDestination,
                "b0-shadow-play-shadow-to-battlefield");
            result = await PassOpenSpellDuelAsync(session, result, "b0-shadow-pass-shadow-contest");
            return result;
        }

        throw new InvalidOperationException("B0 shadow-response driver could not stage Shadow with response resources.");
    }

    private static async ValueTask<ResolutionResult> DriveContestedBattlefieldToShadowResponseAsync(
        MatchSession session,
        ResolutionResult current,
        string battlefieldOwnerId,
        string shadowControllerId)
    {
        var result = current;
        for (var turnIndex = 0; turnIndex < 12; turnIndex++)
        {
            if (string.Equals(result.State.PendingTaskQueue.Phase, "BATTLE_TASKS", StringComparison.Ordinal)
                && result.Prompts[result.State.ActivePlayerId].Actions.Contains(CommandTypes.DeclareBattle, StringComparer.Ordinal))
            {
                var declared = await SubmitShadowResponseDeclareBattleAsync(
                    session,
                    result,
                    battlefieldOwnerId,
                    shadowControllerId,
                    $"b0-shadow-declare-response-battle-{turnIndex}");
                AssertAccepted(declared);
                return declared;
            }

            result = await EndTurnAsync(session, result.State.ActivePlayerId, $"b0-shadow-end-to-reopen-{turnIndex}");
            AssertNoHiddenZoneLeak(result);
            if (string.Equals(result.State.TimingState, TimingStates.SpellDuelOpen, StringComparison.Ordinal)
                || !string.IsNullOrWhiteSpace(result.State.FocusPlayerId))
            {
                result = await PassOpenSpellDuelAsync(session, result, $"b0-shadow-reopen-pass-focus-{turnIndex}");
                AssertNoHiddenZoneLeak(result);
            }

            if (string.Equals(result.State.PendingTaskQueue.Phase, "BATTLE_TASKS", StringComparison.Ordinal)
                && result.Prompts[result.State.ActivePlayerId].Actions.Contains(CommandTypes.DeclareBattle, StringComparer.Ordinal))
            {
                var declared = await SubmitShadowResponseDeclareBattleAsync(
                    session,
                    result,
                    battlefieldOwnerId,
                    shadowControllerId,
                    "b0-shadow-declare-response-battle");
                AssertAccepted(declared);
                return declared;
            }
        }

        throw new InvalidOperationException("B0 shadow-response driver could not open a Shadow response battle task.");
    }

    private static async ValueTask<(MatchSession Session, ResolutionResult Result)> DriveOfficialLowCurveDecksToNoLegalBattleSkipAsync(
        string roomId)
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var deck = BuildLowCurveOfficialDeck(catalog);
        return await DriveOfficialLowCurveDecksToNoLegalBattleSkipAsync(roomId, deck, deck);
    }

    private static async ValueTask<(MatchSession Session, ResolutionResult Result)> DriveOfficialLowCurveDecksToNoLegalBattleSkipAsync(
        string roomId,
        OfficialDecklist p1Deck,
        OfficialDecklist p2Deck)
    {
        var session = new MatchSession(roomId, new CoreRuleEngine());
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");
        return await DriveOfficialLowCurveDecksToNoLegalBattleSkipAsync(session, p1Deck, p2Deck);
    }

    private static async ValueTask<(MatchSession Session, ResolutionResult Result)> DriveOfficialLowCurveDecksToNoLegalBattleSkipAsync(
        MatchState initialState,
        IMatchJournal journal,
        OfficialDecklist p1Deck,
        OfficialDecklist p2Deck)
    {
        var session = new MatchSession(initialState, new CoreRuleEngine(), journal);
        return await DriveOfficialLowCurveDecksToNoLegalBattleSkipAsync(session, p1Deck, p2Deck);
    }

    private static async ValueTask<(MatchState InitialState, ResolutionResult OpeningResult)> DriveOfficialDecksToOtherFriendlyStaticAuraOpeningAsync(
        string roomId,
        OfficialDecklist p1Deck,
        OfficialDecklist p2Deck)
    {
        var failures = new List<string>();
        foreach (var seed in OtherFriendlyStaticAuraDriverSeeds)
        {
            var initialState = BuildSeatedInitialState($"{roomId}-{seed}", seed);
            try
            {
                var (_, result) = await DriveOfficialLowCurveDecksToNoLegalBattleSkipAsync(
                    initialState,
                    NoopMatchJournal.Instance,
                    p1Deck,
                    p2Deck);
                return (initialState, result);
            }
            catch (InvalidOperationException ex) when (ex.Message.StartsWith("B0 auto-driver", StringComparison.Ordinal))
            {
                failures.Add($"{seed}: {ex.Message}");
            }
        }

        throw new InvalidOperationException(
            $"B0 other-friendly static aura opening driver could not find a stable official opening seed: {string.Join(" | ", failures)}");
    }

    private static async ValueTask<(MatchState InitialState, ResolutionResult OpeningResult)> DriveOfficialDecksToBattlefieldAllUnitsStaticAuraOpeningAsync(
        string roomId,
        OfficialDecklist p1Deck,
        OfficialDecklist p2Deck)
    {
        var failures = new List<string>();
        foreach (var seed in BattlefieldAllUnitsStaticAuraDriverSeeds)
        {
            var initialState = BuildSeatedInitialState($"{roomId}-{seed}", seed);
            try
            {
                var (_, result) = await DriveOfficialLowCurveDecksToNoLegalBattleSkipAsync(
                    initialState,
                    NoopMatchJournal.Instance,
                    p1Deck,
                    p2Deck);
                var selectedBattlefieldCardNo = result.State.PlayerZones["P1"].Battlefields
                    .Select(objectId => result.State.CardObjects.TryGetValue(objectId, out var cardObject) ? cardObject.CardNo : null)
                    .FirstOrDefault(cardNo => !string.IsNullOrWhiteSpace(cardNo));
                if (string.Equals(
                    selectedBattlefieldCardNo,
                    TrifarianTrainingGroundsBattlefieldAllUnitsStaticAuraCardNo,
                    StringComparison.Ordinal))
                {
                    return (initialState, result);
                }

                failures.Add($"{seed}: selected battlefield {selectedBattlefieldCardNo ?? "<missing>"}");
            }
            catch (InvalidOperationException ex) when (ex.Message.StartsWith("B0 auto-driver", StringComparison.Ordinal))
            {
                failures.Add($"{seed}: {ex.Message}");
            }
        }

        throw new InvalidOperationException(
            $"B0 battlefield all-units static aura opening driver could not find a stable official opening seed: {string.Join(" | ", failures)}");
    }

    private static async ValueTask<(MatchSession Session, ResolutionResult Result)> DriveOfficialLowCurveDecksToNoLegalBattleSkipAsync(
        MatchSession session,
        OfficialDecklist p1Deck,
        OfficialDecklist p2Deck)
    {
        var p1Submit = await SubmitDeckAsync(session, "P1", p1Deck, "b0-submit-p1");
        var p2Submit = await SubmitDeckAsync(session, "P2", p2Deck, "b0-submit-p2");
        AssertAccepted(p1Submit);
        AssertAccepted(p2Submit);

        AssertAccepted(await session.ReadyAsync("P1", "b0-ready-p1", RawCommand(CommandTypes.Ready), CancellationToken.None));
        var ready = await session.ReadyAsync("P2", "b0-ready-p2", RawCommand(CommandTypes.Ready), CancellationToken.None);
        AssertAccepted(ready);
        AssertNoHiddenZoneLeak(ready);

        var activePlayerId = ready.State.ActivePlayerId;
        var secondPlayerId = ready.State.OpeningSecondActionPlayerId!;
        var activeMulligan = await session.SubmitAsync(
            activePlayerId,
            "b0-mulligan-active",
            new MulliganCommand([]),
            RawCommand(new MulliganCommand([])),
            CancellationToken.None);
        AssertAccepted(activeMulligan);
        AssertNoHiddenZoneLeak(activeMulligan);

        var secondMulligan = await session.SubmitAsync(
            secondPlayerId,
            "b0-mulligan-second",
            new MulliganCommand([]),
            RawCommand(new MulliganCommand([])),
            CancellationToken.None);
        AssertAccepted(secondMulligan);
        AssertNoHiddenZoneLeak(secondMulligan);
        Assert.Equal(MatchPhases.Main, secondMulligan.State.Phase);

        var result = secondMulligan;
        result = await PreparePlayerBoardAsync(session, result.State.ActivePlayerId, result, "first", playUnitToBattlefield: true);
        var nextPlayerId = OpponentOf(result.State, result.State.ActivePlayerId);
        result = await EndTurnAsync(session, result.State.ActivePlayerId, "b0-end-first-player");
        AssertNoHiddenZoneLeak(result);

        Assert.Equal(nextPlayerId, result.State.ActivePlayerId);
        result = await PreparePlayerBoardAsync(session, result.State.ActivePlayerId, result, "second", playUnitToBattlefield: false);
        result = await MoveBaseUnitToOpponentBattlefieldAsync(session, result.State.ActivePlayerId, result);
        result = await PassOpenSpellDuelAsync(session, result, "b0-initial-pass-focus");
        Assert.Contains(result.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLE_SKIPPED", StringComparison.Ordinal));
        Assert.False(result.State.PendingTaskQueue.HasTasks);
        Assert.False(result.State.PendingTaskQueue.IsBlocking);
        Assert.Equal("IDLE", result.State.PendingTaskQueue.Phase);
        Assert.DoesNotContain(result.State.PendingTaskQueue.Tasks, task =>
            string.Equals(task.Kind, "START_BATTLE", StringComparison.Ordinal));
        Assert.DoesNotContain("DECLARE_BATTLE", result.Prompts["P1"].Actions);
        Assert.DoesNotContain("DECLARE_BATTLE", result.Prompts["P2"].Actions);
        Assert.True(result.Prompts[result.State.ActivePlayerId].Actionable);
        Assert.DoesNotContain("WAIT", result.Prompts[result.State.ActivePlayerId].Actions);
        AssertNoHiddenZoneLeak(result);
        return (session, result);
    }

    private static async ValueTask<ResolutionResult> DriveTwoAssignmentDefendersOntoBattlefieldAsync(
        MatchSession session,
        ResolutionResult current,
        string invadingPlayerId,
        string battlefieldOwnerId)
    {
        var result = current;
        for (var turnIndex = 0; turnIndex < 18; turnIndex++)
        {
            if (!string.Equals(result.State.ActivePlayerId, invadingPlayerId, StringComparison.Ordinal))
            {
                result = await EndTurnAsync(session, result.State.ActivePlayerId, $"b0-damage-wait-for-invader-{turnIndex}");
                AssertNoHiddenZoneLeak(result);
                continue;
            }

            result = await TapAllAvailableRunesAsync(session, invadingPlayerId, result, $"b0-damage-invader-tap-{turnIndex}");
            if (!PlayerHandContainsCardNo(result.State, invadingPlayerId, MutantKittenCardNo)
                || !PlayerHandContainsCardNo(result.State, invadingPlayerId, LeblancCardNo)
                || result.State.RunePools[invadingPlayerId].Mana < 6)
            {
                result = await EndTurnAsync(session, invadingPlayerId, $"b0-damage-wait-for-defenders-{turnIndex}");
                AssertNoHiddenZoneLeak(result);
                continue;
            }

            var battlefieldDestination = BattlefieldDestinationFor(result.State, battlefieldOwnerId);
            result = await PlaySpecificUnitToBaseAndMoveToBattlefieldAsync(
                session,
                invadingPlayerId,
                result,
                MutantKittenCardNo,
                battlefieldDestination,
                "b0-damage-play-move-kitten");
            result = await PassOpenSpellDuelAsync(session, result, "b0-damage-pass-kitten-contest");
            Assert.Contains(result.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLE_SKIPPED", StringComparison.Ordinal));
            Assert.Equal(invadingPlayerId, result.State.ActivePlayerId);

            result = await PlaySpecificUnitToBaseAndMoveToBattlefieldAsync(
                session,
                invadingPlayerId,
                result,
                LeblancCardNo,
                battlefieldDestination,
                "b0-damage-play-move-leblanc");
            result = await PassOpenSpellDuelAsync(session, result, "b0-damage-pass-leblanc-contest");
            Assert.Equal(invadingPlayerId, result.State.ActivePlayerId);
            return result;
        }

        throw new InvalidOperationException("B0 damage-assignment driver could not stage two assignment defenders.");
    }

    private static async ValueTask<ResolutionResult> DriveContestedBattlefieldToDamageAssignmentAsync(
        MatchSession session,
        ResolutionResult current,
        string battlefieldOwnerId,
        string invadingPlayerId)
    {
        var result = current;
        for (var turnIndex = 0; turnIndex < 12; turnIndex++)
        {
            result = await EndTurnAsync(session, result.State.ActivePlayerId, $"b0-damage-end-to-reopen-{turnIndex}");
            AssertNoHiddenZoneLeak(result);
            if (string.Equals(result.State.TimingState, TimingStates.SpellDuelOpen, StringComparison.Ordinal)
                || !string.IsNullOrWhiteSpace(result.State.FocusPlayerId))
            {
                result = await PassOpenSpellDuelAsync(session, result, $"b0-damage-reopen-pass-focus-{turnIndex}");
                AssertNoHiddenZoneLeak(result);
            }

            if (string.Equals(result.State.PendingTaskQueue.Phase, "BATTLE_TASKS", StringComparison.Ordinal)
                && result.Prompts[result.State.ActivePlayerId].Actions.Contains(CommandTypes.DeclareBattle, StringComparer.Ordinal))
            {
                var declared = await SubmitMultiDefenderDeclareBattleAsync(
                    session,
                    result,
                    battlefieldOwnerId,
                    invadingPlayerId,
                    "b0-damage-declare-multi-defender-battle");
                AssertAccepted(declared);
                return declared;
            }
        }

        throw new InvalidOperationException("B0 damage-assignment driver could not open a multi-defender battle task.");
    }

    private static async ValueTask<ResolutionResult> DriveSpecificUnitToOwnBattlefieldAsync(
        MatchSession session,
        ResolutionResult current,
        string playerId,
        string cardNo,
        string intentPrefix)
    {
        return await DriveSpecificUnitToPlayerBattlefieldAsync(
            session,
            current,
            playerId,
            cardNo,
            playerId,
            intentPrefix);
    }

    private static async ValueTask<ResolutionResult> DriveSpecificUnitToOwnBaseAsync(
        MatchSession session,
        ResolutionResult current,
        string playerId,
        string cardNo,
        string intentPrefix)
    {
        var result = current;
        for (var turnIndex = 0; turnIndex < 24; turnIndex++)
        {
            if (string.Equals(result.State.TimingState, TimingStates.SpellDuelOpen, StringComparison.Ordinal)
                || !string.IsNullOrWhiteSpace(result.State.FocusPlayerId))
            {
                result = await PassOpenSpellDuelAsync(session, result, $"{intentPrefix}-pass-focus-{turnIndex}");
                AssertNoHiddenZoneLeak(result);
                continue;
            }

            if (string.Equals(result.State.PendingTaskQueue.Phase, "BATTLE_TASKS", StringComparison.Ordinal)
                && result.Prompts[result.State.ActivePlayerId].Actions.Contains(CommandTypes.DeclareBattle, StringComparer.Ordinal))
            {
                result = await SubmitFirstDeclareBattleCandidateAsync(
                    session,
                    result,
                    $"{intentPrefix}-clear-existing-battle-{turnIndex}");
                AssertNoHiddenZoneLeak(result);
                continue;
            }

            if (!string.Equals(result.State.Phase, MatchPhases.Main, StringComparison.Ordinal)
                || !string.Equals(result.State.TimingState, TimingStates.NeutralOpen, StringComparison.Ordinal)
                || result.State.PendingTaskQueue.HasTasks)
            {
                throw new InvalidOperationException($"B0 base staging driver cannot stage {cardNo}: {DescribeState(result.State)}");
            }

            if (!string.Equals(result.State.ActivePlayerId, playerId, StringComparison.Ordinal))
            {
                result = await EndTurnAsync(session, result.State.ActivePlayerId, $"{intentPrefix}-wait-active-{turnIndex}");
                AssertNoHiddenZoneLeak(result);
                continue;
            }

            result = await TapAllAvailableRunesAsync(session, playerId, result, $"{intentPrefix}-tap-{turnIndex}");
            var sourceObjectId = FindHandCardObjectByCardNo(result.State, playerId, cardNo);
            var playCandidate = EnabledCandidate(result.Prompts[playerId], CommandTypes.PlayCard);
            var canPlaySource = sourceObjectId is not null
                && playCandidate?.Sources?.Any(choice => string.Equals(choice.Id, sourceObjectId, StringComparison.Ordinal)) == true;
            if (sourceObjectId is not null
                && canPlaySource
                && result.State.RunePools[playerId].Mana >= result.State.CardObjects[sourceObjectId].ManaCost)
            {
                var played = await PlaySpecificUnitToBaseAsync(
                    session,
                    playerId,
                    result,
                    cardNo,
                    intentPrefix);
                Assert.NotNull(FindBaseUnitByCardNo(played.State, playerId, cardNo));
                return played;
            }

            result = await EndTurnAsync(session, playerId, $"{intentPrefix}-wait-resources-{turnIndex}");
            AssertNoHiddenZoneLeak(result);
        }

        throw new InvalidOperationException($"B0 base staging driver could not stage {cardNo} for {playerId}.");
    }

    private static async ValueTask<ResolutionResult> DriveSpecificUnitToOwnBaseGrantingBoonToBattlefieldUnitAsync(
        MatchSession session,
        ResolutionResult current,
        string playerId,
        string cardNo,
        string targetCardNo,
        string intentPrefix)
    {
        var result = current;
        for (var turnIndex = 0; turnIndex < 24; turnIndex++)
        {
            if (string.Equals(result.State.TimingState, TimingStates.SpellDuelOpen, StringComparison.Ordinal)
                || !string.IsNullOrWhiteSpace(result.State.FocusPlayerId))
            {
                result = await PassOpenSpellDuelAsync(session, result, $"{intentPrefix}-pass-focus-{turnIndex}");
                AssertNoHiddenZoneLeak(result);
                continue;
            }

            if (string.Equals(result.State.PendingTaskQueue.Phase, "BATTLE_TASKS", StringComparison.Ordinal)
                && result.Prompts[result.State.ActivePlayerId].Actions.Contains(CommandTypes.DeclareBattle, StringComparer.Ordinal))
            {
                result = await SubmitFirstDeclareBattleCandidateAsync(
                    session,
                    result,
                    $"{intentPrefix}-clear-existing-battle-{turnIndex}");
                AssertNoHiddenZoneLeak(result);
                continue;
            }

            if (!string.Equals(result.State.Phase, MatchPhases.Main, StringComparison.Ordinal)
                || !string.Equals(result.State.TimingState, TimingStates.NeutralOpen, StringComparison.Ordinal)
                || result.State.PendingTaskQueue.HasTasks)
            {
                throw new InvalidOperationException($"B0 targeted boon staging driver cannot stage {cardNo}: {DescribeState(result.State)}");
            }

            if (!string.Equals(result.State.ActivePlayerId, playerId, StringComparison.Ordinal))
            {
                result = await EndTurnAsync(session, result.State.ActivePlayerId, $"{intentPrefix}-wait-active-{turnIndex}");
                AssertNoHiddenZoneLeak(result);
                continue;
            }

            result = await TapAllAvailableRunesAsync(session, playerId, result, $"{intentPrefix}-tap-{turnIndex}");
            var sourceObjectId = FindHandCardObjectByCardNo(result.State, playerId, cardNo);
            var targetObjectId = FindBattlefieldUnitByCardNo(result.State, playerId, targetCardNo)
                ?? throw new InvalidOperationException($"B0 targeted boon staging driver could not find battlefield target {targetCardNo}.");
            var playCandidate = EnabledCandidate(result.Prompts[playerId], CommandTypes.PlayCard);
            var canPlaySource = sourceObjectId is not null
                && playCandidate?.Sources?.Any(choice => string.Equals(choice.Id, sourceObjectId, StringComparison.Ordinal)) == true;
            var canTarget = playCandidate?.Targets?.Any(choice => string.Equals(choice.Id, targetObjectId, StringComparison.Ordinal)) == true;
            if (sourceObjectId is not null
                && canPlaySource
                && canTarget
                && result.State.RunePools[playerId].Mana >= result.State.CardObjects[sourceObjectId].ManaCost)
            {
                var play = await session.SubmitAsync(
                    playerId,
                    $"{intentPrefix}-play-{turnIndex}",
                    new PlayCardCommand(sourceObjectId, cardNo, [targetObjectId], Destination: "BASE"),
                    RawCommand(new PlayCardCommand(sourceObjectId, cardNo, [targetObjectId], Destination: "BASE")),
                    CancellationToken.None);
                AssertAccepted(play);
                AssertNoHiddenZoneLeak(play);
                var resolved = await ResolveStackPassPassAsync(session, play, $"{intentPrefix}-resolve-{turnIndex}");
                Assert.Contains(CardObjectTags.Boon, resolved.State.CardObjects[targetObjectId].Tags);
                Assert.NotNull(FindBaseUnitByCardNo(resolved.State, playerId, cardNo));
                AssertNoHiddenZoneLeak(resolved);
                return resolved;
            }

            result = await EndTurnAsync(session, playerId, $"{intentPrefix}-wait-resources-{turnIndex}");
            AssertNoHiddenZoneLeak(result);
        }

        throw new InvalidOperationException($"B0 targeted boon staging driver could not stage {cardNo} for {playerId}.");
    }

    private static async ValueTask<ResolutionResult> DriveSpecificUnitToPlayerBattlefieldAsync(
        MatchSession session,
        ResolutionResult current,
        string playerId,
        string cardNo,
        string battlefieldOwnerId,
        string intentPrefix,
        string? battlefieldCardNo = null)
    {
        var result = current;
        for (var turnIndex = 0; turnIndex < 36; turnIndex++)
        {
            if (string.Equals(result.State.TimingState, TimingStates.SpellDuelOpen, StringComparison.Ordinal)
                || !string.IsNullOrWhiteSpace(result.State.FocusPlayerId))
            {
                result = await PassOpenSpellDuelAsync(session, result, $"{intentPrefix}-pass-focus-{turnIndex}");
                AssertNoHiddenZoneLeak(result);
                continue;
            }

            if (string.Equals(result.State.PendingTaskQueue.Phase, "BATTLE_TASKS", StringComparison.Ordinal)
                && result.Prompts[result.State.ActivePlayerId].Actions.Contains(CommandTypes.DeclareBattle, StringComparer.Ordinal))
            {
                result = await SubmitFirstDeclareBattleCandidateAsync(
                    session,
                    result,
                    $"{intentPrefix}-clear-existing-battle-{turnIndex}");
                AssertNoHiddenZoneLeak(result);
                continue;
            }

            if (!string.Equals(result.State.Phase, MatchPhases.Main, StringComparison.Ordinal)
                || !string.Equals(result.State.TimingState, TimingStates.NeutralOpen, StringComparison.Ordinal)
                || result.State.PendingTaskQueue.HasTasks)
            {
                throw new InvalidOperationException($"B0 Treant driver cannot stage {cardNo}: {DescribeState(result.State)}");
            }

            if (!string.Equals(result.State.ActivePlayerId, playerId, StringComparison.Ordinal))
            {
                result = await EndTurnAsync(session, result.State.ActivePlayerId, $"{intentPrefix}-wait-active-{turnIndex}");
                AssertNoHiddenZoneLeak(result);
                continue;
            }

            result = await TapAllAvailableRunesAsync(session, playerId, result, $"{intentPrefix}-tap-{turnIndex}");
            var sourceObjectId = FindHandCardObjectByCardNo(result.State, playerId, cardNo);
            var playCandidate = EnabledCandidate(result.Prompts[playerId], CommandTypes.PlayCard);
            var runePool = result.State.RunePools[playerId];
            var canPlaySource = sourceObjectId is not null
                && playCandidate?.Sources?.Any(choice => string.Equals(choice.Id, sourceObjectId, StringComparison.Ordinal)) == true;
            var canPayHasteReady = canPlaySource
                && playCandidate?.OptionalCosts?.Any(choice => string.Equals(choice.Id, HasteReadyOptionalCost, StringComparison.Ordinal)) == true
                && runePool.Mana >= 5
                && runePool.PowerByTrait.TryGetValue(RuneTrait.Red, out var redPower)
                && redPower >= 1;
            if (sourceObjectId is not null
                && canPlaySource
                && (canPayHasteReady || runePool.Mana >= 4))
            {
                var battlefieldDestination = string.IsNullOrWhiteSpace(battlefieldCardNo)
                    ? BattlefieldDestinationFor(result.State, battlefieldOwnerId)
                    : BattlefieldDestinationForCardNo(result.State, battlefieldOwnerId, battlefieldCardNo);
                var played = await PlaySpecificUnitToBaseAsync(
                    session,
                    playerId,
                    result,
                    cardNo,
                    intentPrefix,
                    canPayHasteReady ? [HasteReadyOptionalCost] : []);
                var moved = await DriveBaseUnitToBattlefieldWhenReadyAsync(
                    session,
                    played,
                    playerId,
                    cardNo,
                    battlefieldDestination,
                    $"{intentPrefix}-move-when-ready");
                Assert.NotNull(FindBattlefieldUnitByCardNo(
                    moved.State,
                    playerId,
                    cardNo,
                    battlefieldDestination["BATTLEFIELD:".Length..]));
                return moved;
            }

            result = await EndTurnAsync(session, playerId, $"{intentPrefix}-wait-resources-{turnIndex}");
            AssertNoHiddenZoneLeak(result);
        }

        throw new InvalidOperationException($"B0 Treant driver could not stage {cardNo} for {playerId}.");
    }

    private static async ValueTask<ResolutionResult> DriveOpponentUnitToBattlefieldAsync(
        MatchSession session,
        ResolutionResult current,
        string opponentId,
        string battlefieldOwnerId,
        string intentPrefix,
        string? battlefieldCardNo = null)
    {
        var result = current;
        for (var turnIndex = 0; turnIndex < 24; turnIndex++)
        {
            if (string.Equals(result.State.TimingState, TimingStates.SpellDuelOpen, StringComparison.Ordinal)
                || !string.IsNullOrWhiteSpace(result.State.FocusPlayerId))
            {
                result = await PassOpenSpellDuelAsync(session, result, $"{intentPrefix}-pass-focus-{turnIndex}");
                AssertNoHiddenZoneLeak(result);
                continue;
            }

            if (string.Equals(result.State.PendingTaskQueue.Phase, "BATTLE_TASKS", StringComparison.Ordinal)
                && result.Prompts[result.State.ActivePlayerId].Actions.Contains(CommandTypes.DeclareBattle, StringComparer.Ordinal))
            {
                result = await SubmitFirstDeclareBattleCandidateAsync(
                    session,
                    result,
                    $"{intentPrefix}-clear-existing-battle-{turnIndex}");
                AssertNoHiddenZoneLeak(result);
                continue;
            }

            if (!string.Equals(result.State.Phase, MatchPhases.Main, StringComparison.Ordinal)
                || !string.Equals(result.State.TimingState, TimingStates.NeutralOpen, StringComparison.Ordinal)
                || result.State.PendingTaskQueue.HasTasks)
            {
                throw new InvalidOperationException($"B0 Treant defender driver cannot stage a defender: {DescribeState(result.State)}");
            }

            if (!string.Equals(result.State.ActivePlayerId, opponentId, StringComparison.Ordinal))
            {
                result = await EndTurnAsync(session, result.State.ActivePlayerId, $"{intentPrefix}-wait-active-{turnIndex}");
                AssertNoHiddenZoneLeak(result);
                continue;
            }

            result = await TapAllAvailableRunesAsync(session, opponentId, result, $"{intentPrefix}-tap-{turnIndex}");
            if (EnabledCandidate(result.Prompts[opponentId], CommandTypes.PlayCard) is null)
            {
                result = await EndTurnAsync(session, opponentId, $"{intentPrefix}-wait-play-{turnIndex}");
                AssertNoHiddenZoneLeak(result);
                continue;
            }

            result = await TryPlayFirstUnitAsync(session, opponentId, result, $"{intentPrefix}-play", playUnitToBattlefield: false);
            result = await DriveAnyBaseUnitToBattlefieldWhenReadyAsync(
                session,
                result,
                opponentId,
                string.IsNullOrWhiteSpace(battlefieldCardNo)
                    ? BattlefieldDestinationFor(result.State, battlefieldOwnerId)
                    : BattlefieldDestinationForCardNo(result.State, battlefieldOwnerId, battlefieldCardNo),
                $"{intentPrefix}-move-when-ready");
            result = await PassOpenSpellDuelAsync(session, result, $"{intentPrefix}-pass-contest");
            return result;
        }

        throw new InvalidOperationException($"B0 Treant driver could not stage a defender for {opponentId}.");
    }

    private static async ValueTask<ResolutionResult> DriveContestedBattlefieldToCrimsonSignetTreantConquestAsync(
        MatchSession session,
        ResolutionResult current,
        string treantPlayerId,
        string intentPrefix)
    {
        var result = current;
        for (var turnIndex = 0; turnIndex < 20; turnIndex++)
        {
            if (string.Equals(result.State.TimingState, TimingStates.SpellDuelOpen, StringComparison.Ordinal)
                || !string.IsNullOrWhiteSpace(result.State.FocusPlayerId))
            {
                result = await PassOpenSpellDuelAsync(session, result, $"{intentPrefix}-pass-focus-{turnIndex}");
                AssertNoHiddenZoneLeak(result);
                continue;
            }

            if (string.Equals(result.State.PendingTaskQueue.Phase, "BATTLE_TASKS", StringComparison.Ordinal)
                && result.Prompts[result.State.ActivePlayerId].Actions.Contains(CommandTypes.DeclareBattle, StringComparer.Ordinal))
            {
                if (!string.Equals(result.State.ActivePlayerId, treantPlayerId, StringComparison.Ordinal))
                {
                    result = await SubmitFirstDeclareBattleCandidateAsync(
                        session,
                        result,
                        $"{intentPrefix}-clear-other-battle-{turnIndex}");
                    AssertNoHiddenZoneLeak(result);
                    continue;
                }

                return await SubmitCrimsonSignetTreantDeclareBattleAsync(
                    session,
                    result,
                    treantPlayerId,
                    $"{intentPrefix}-declare-{turnIndex}");
            }

            if (!string.Equals(result.State.Phase, MatchPhases.Main, StringComparison.Ordinal)
                || !string.Equals(result.State.TimingState, TimingStates.NeutralOpen, StringComparison.Ordinal)
                || result.State.PendingTaskQueue.HasTasks)
            {
                throw new InvalidOperationException($"B0 Treant driver cannot advance to conquest: {DescribeState(result.State)}");
            }

            result = await EndTurnAsync(session, result.State.ActivePlayerId, $"{intentPrefix}-end-to-reopen-{turnIndex}");
            AssertNoHiddenZoneLeak(result);
        }

        throw new InvalidOperationException($"B0 Treant driver could not open a legal Treant battle task: {DescribeState(result.State)}");
    }

    private static async ValueTask<ResolutionResult> DriveContestedBattlefieldToSameBattlefieldStaticAuraBattleAsync(
        MatchSession session,
        ResolutionResult current,
        string auraControllerId,
        string intentPrefix)
    {
        var result = current;
        for (var turnIndex = 0; turnIndex < 20; turnIndex++)
        {
            if (string.Equals(result.State.TimingState, TimingStates.SpellDuelOpen, StringComparison.Ordinal)
                || !string.IsNullOrWhiteSpace(result.State.FocusPlayerId))
            {
                result = await PassOpenSpellDuelAsync(session, result, $"{intentPrefix}-pass-focus-{turnIndex}");
                AssertNoHiddenZoneLeak(result);
                continue;
            }

            if (string.Equals(result.State.PendingTaskQueue.Phase, "BATTLE_TASKS", StringComparison.Ordinal)
                && result.Prompts[result.State.ActivePlayerId].Actions.Contains(CommandTypes.DeclareBattle, StringComparer.Ordinal))
            {
                if (!string.Equals(result.State.ActivePlayerId, auraControllerId, StringComparison.Ordinal))
                {
                    result = await SubmitFirstDeclareBattleCandidateAsync(
                        session,
                        result,
                        $"{intentPrefix}-clear-other-battle-{turnIndex}");
                    AssertNoHiddenZoneLeak(result);
                    continue;
                }

                return await SubmitSameBattlefieldStaticAuraDeclareBattleAsync(
                    session,
                    result,
                    auraControllerId,
                    $"{intentPrefix}-declare-{turnIndex}");
            }

            if (!string.Equals(result.State.Phase, MatchPhases.Main, StringComparison.Ordinal)
                || !string.Equals(result.State.TimingState, TimingStates.NeutralOpen, StringComparison.Ordinal)
                || result.State.PendingTaskQueue.HasTasks)
            {
                throw new InvalidOperationException($"B0 same-battlefield static aura driver cannot advance to battle: {DescribeState(result.State)}");
            }

            result = await EndTurnAsync(session, result.State.ActivePlayerId, $"{intentPrefix}-end-to-reopen-{turnIndex}");
            AssertNoHiddenZoneLeak(result);
        }

        throw new InvalidOperationException($"B0 same-battlefield static aura driver could not open a legal battle task: {DescribeState(result.State)}");
    }

    private static async ValueTask<ResolutionResult> DriveContestedBattlefieldToOtherFriendlyStaticAuraBattleAsync(
        MatchSession session,
        ResolutionResult current,
        string auraControllerId,
        string intentPrefix)
    {
        var result = current;
        for (var turnIndex = 0; turnIndex < 20; turnIndex++)
        {
            if (string.Equals(result.State.TimingState, TimingStates.SpellDuelOpen, StringComparison.Ordinal)
                || !string.IsNullOrWhiteSpace(result.State.FocusPlayerId))
            {
                result = await PassOpenSpellDuelAsync(session, result, $"{intentPrefix}-pass-focus-{turnIndex}");
                AssertNoHiddenZoneLeak(result);
                continue;
            }

            if (string.Equals(result.State.PendingTaskQueue.Phase, "BATTLE_TASKS", StringComparison.Ordinal)
                && result.Prompts[result.State.ActivePlayerId].Actions.Contains(CommandTypes.DeclareBattle, StringComparer.Ordinal))
            {
                if (!string.Equals(result.State.ActivePlayerId, auraControllerId, StringComparison.Ordinal))
                {
                    result = await SubmitFirstDeclareBattleCandidateAsync(
                        session,
                        result,
                        $"{intentPrefix}-clear-other-battle-{turnIndex}");
                    AssertNoHiddenZoneLeak(result);
                    continue;
                }

                return await SubmitOtherFriendlyStaticAuraDeclareBattleAsync(
                    session,
                    result,
                    auraControllerId,
                    $"{intentPrefix}-declare-{turnIndex}");
            }

            if (!string.Equals(result.State.Phase, MatchPhases.Main, StringComparison.Ordinal)
                || !string.Equals(result.State.TimingState, TimingStates.NeutralOpen, StringComparison.Ordinal)
                || result.State.PendingTaskQueue.HasTasks)
            {
                throw new InvalidOperationException($"B0 other-friendly static aura driver cannot advance to battle: {DescribeState(result.State)}");
            }

            result = await EndTurnAsync(session, result.State.ActivePlayerId, $"{intentPrefix}-end-to-reopen-{turnIndex}");
            AssertNoHiddenZoneLeak(result);
        }

        throw new InvalidOperationException($"B0 other-friendly static aura driver could not open a legal battle task: {DescribeState(result.State)}");
    }

    private static async ValueTask<ResolutionResult> DriveContestedBattlefieldToBattlefieldAllUnitsStaticAuraBattleAsync(
        MatchSession session,
        ResolutionResult current,
        string battlefieldOwnerId,
        string intentPrefix)
    {
        var result = current;
        for (var turnIndex = 0; turnIndex < 20; turnIndex++)
        {
            if (string.Equals(result.State.TimingState, TimingStates.SpellDuelOpen, StringComparison.Ordinal)
                || !string.IsNullOrWhiteSpace(result.State.FocusPlayerId))
            {
                result = await PassOpenSpellDuelAsync(session, result, $"{intentPrefix}-pass-focus-{turnIndex}");
                AssertNoHiddenZoneLeak(result);
                continue;
            }

            if (string.Equals(result.State.PendingTaskQueue.Phase, "BATTLE_TASKS", StringComparison.Ordinal)
                && result.Prompts[result.State.ActivePlayerId].Actions.Contains(CommandTypes.DeclareBattle, StringComparer.Ordinal))
            {
                if (!string.Equals(result.State.ActivePlayerId, battlefieldOwnerId, StringComparison.Ordinal))
                {
                    result = await SubmitFirstDeclareBattleCandidateAsync(
                        session,
                        result,
                        $"{intentPrefix}-clear-other-battle-{turnIndex}");
                    AssertNoHiddenZoneLeak(result);
                    continue;
                }

                return await SubmitBattlefieldAllUnitsStaticAuraDeclareBattleAsync(
                    session,
                    result,
                    battlefieldOwnerId,
                    $"{intentPrefix}-declare-{turnIndex}");
            }

            if (!string.Equals(result.State.Phase, MatchPhases.Main, StringComparison.Ordinal)
                || !string.Equals(result.State.TimingState, TimingStates.NeutralOpen, StringComparison.Ordinal)
                || result.State.PendingTaskQueue.HasTasks)
            {
                throw new InvalidOperationException($"B0 battlefield all-units static aura driver cannot advance to battle: {DescribeState(result.State)}");
            }

            result = await EndTurnAsync(session, result.State.ActivePlayerId, $"{intentPrefix}-end-to-reopen-{turnIndex}");
            AssertNoHiddenZoneLeak(result);
        }

        throw new InvalidOperationException($"B0 battlefield all-units static aura driver could not open a legal battle task: {DescribeState(result.State)}");
    }

    private static async ValueTask<ResolutionResult> DriveContestedBattlefieldToSourceSameLocationStaticAuraBattleAsync(
        MatchSession session,
        ResolutionResult current,
        string auraControllerId,
        string intentPrefix)
    {
        var result = current;
        for (var turnIndex = 0; turnIndex < 20; turnIndex++)
        {
            if (string.Equals(result.State.TimingState, TimingStates.SpellDuelOpen, StringComparison.Ordinal)
                || !string.IsNullOrWhiteSpace(result.State.FocusPlayerId))
            {
                result = await PassOpenSpellDuelAsync(session, result, $"{intentPrefix}-pass-focus-{turnIndex}");
                AssertNoHiddenZoneLeak(result);
                continue;
            }

            if (string.Equals(result.State.PendingTaskQueue.Phase, "BATTLE_TASKS", StringComparison.Ordinal)
                && result.Prompts[result.State.ActivePlayerId].Actions.Contains(CommandTypes.DeclareBattle, StringComparer.Ordinal))
            {
                if (!string.Equals(result.State.ActivePlayerId, auraControllerId, StringComparison.Ordinal))
                {
                    result = await SubmitFirstDeclareBattleCandidateAsync(
                        session,
                        result,
                        $"{intentPrefix}-clear-other-battle-{turnIndex}");
                    AssertNoHiddenZoneLeak(result);
                    continue;
                }

                return await SubmitSourceSameLocationStaticAuraDeclareBattleAsync(
                    session,
                    result,
                    auraControllerId,
                    $"{intentPrefix}-declare-{turnIndex}");
            }

            if (!string.Equals(result.State.Phase, MatchPhases.Main, StringComparison.Ordinal)
                || !string.Equals(result.State.TimingState, TimingStates.NeutralOpen, StringComparison.Ordinal)
                || result.State.PendingTaskQueue.HasTasks)
            {
                throw new InvalidOperationException($"B0 source-same-location static aura driver cannot advance to battle: {DescribeState(result.State)}");
            }

            result = await EndTurnAsync(session, result.State.ActivePlayerId, $"{intentPrefix}-end-to-reopen-{turnIndex}");
            AssertNoHiddenZoneLeak(result);
        }

        throw new InvalidOperationException($"B0 source-same-location static aura driver could not open a legal battle task: {DescribeState(result.State)}");
    }

    private static async ValueTask<ResolutionResult> DriveContestedBattlefieldToSameBattlefieldBoonCountStaticAuraBattleAsync(
        MatchSession session,
        ResolutionResult current,
        string auraControllerId,
        string intentPrefix)
    {
        var result = current;
        for (var turnIndex = 0; turnIndex < 20; turnIndex++)
        {
            if (string.Equals(result.State.TimingState, TimingStates.SpellDuelOpen, StringComparison.Ordinal)
                || !string.IsNullOrWhiteSpace(result.State.FocusPlayerId))
            {
                result = await PassOpenSpellDuelAsync(session, result, $"{intentPrefix}-pass-focus-{turnIndex}");
                AssertNoHiddenZoneLeak(result);
                continue;
            }

            if (string.Equals(result.State.PendingTaskQueue.Phase, "BATTLE_TASKS", StringComparison.Ordinal)
                && result.Prompts[result.State.ActivePlayerId].Actions.Contains(CommandTypes.DeclareBattle, StringComparer.Ordinal))
            {
                if (!string.Equals(result.State.ActivePlayerId, auraControllerId, StringComparison.Ordinal))
                {
                    result = await SubmitFirstDeclareBattleCandidateAsync(
                        session,
                        result,
                        $"{intentPrefix}-clear-other-battle-{turnIndex}");
                    AssertNoHiddenZoneLeak(result);
                    continue;
                }

                return await SubmitSameBattlefieldBoonCountStaticAuraDeclareBattleAsync(
                    session,
                    result,
                    auraControllerId,
                    $"{intentPrefix}-declare-{turnIndex}");
            }

            if (!string.Equals(result.State.Phase, MatchPhases.Main, StringComparison.Ordinal)
                || !string.Equals(result.State.TimingState, TimingStates.NeutralOpen, StringComparison.Ordinal)
                || result.State.PendingTaskQueue.HasTasks)
            {
                throw new InvalidOperationException($"B0 same-battlefield boon-count static aura driver cannot advance to battle: {DescribeState(result.State)}");
            }

            result = await EndTurnAsync(session, result.State.ActivePlayerId, $"{intentPrefix}-end-to-reopen-{turnIndex}");
            AssertNoHiddenZoneLeak(result);
        }

        throw new InvalidOperationException($"B0 same-battlefield boon-count static aura driver could not open a legal battle task: {DescribeState(result.State)}");
    }

    private static async ValueTask<ResolutionResult> DriveContestedBattlefieldToSameBattlefieldOtherFriendlyFilteredStaticAuraBattleAsync(
        MatchSession session,
        ResolutionResult current,
        string auraControllerId,
        string intentPrefix)
    {
        var result = current;
        for (var turnIndex = 0; turnIndex < 20; turnIndex++)
        {
            if (string.Equals(result.State.TimingState, TimingStates.SpellDuelOpen, StringComparison.Ordinal)
                || !string.IsNullOrWhiteSpace(result.State.FocusPlayerId))
            {
                result = await PassOpenSpellDuelAsync(session, result, $"{intentPrefix}-pass-focus-{turnIndex}");
                AssertNoHiddenZoneLeak(result);
                continue;
            }

            if (string.Equals(result.State.PendingTaskQueue.Phase, "BATTLE_TASKS", StringComparison.Ordinal)
                && result.Prompts[result.State.ActivePlayerId].Actions.Contains(CommandTypes.DeclareBattle, StringComparer.Ordinal))
            {
                if (!string.Equals(result.State.ActivePlayerId, auraControllerId, StringComparison.Ordinal))
                {
                    result = await SubmitFirstDeclareBattleCandidateAsync(
                        session,
                        result,
                        $"{intentPrefix}-clear-other-battle-{turnIndex}");
                    AssertNoHiddenZoneLeak(result);
                    continue;
                }

                return await SubmitSameBattlefieldOtherFriendlyFilteredStaticAuraDeclareBattleAsync(
                    session,
                    result,
                    auraControllerId,
                    $"{intentPrefix}-declare-{turnIndex}");
            }

            if (!string.Equals(result.State.Phase, MatchPhases.Main, StringComparison.Ordinal)
                || !string.Equals(result.State.TimingState, TimingStates.NeutralOpen, StringComparison.Ordinal)
                || result.State.PendingTaskQueue.HasTasks)
            {
                throw new InvalidOperationException($"B0 same-battlefield other-friendly-filtered static aura driver cannot advance to battle: {DescribeState(result.State)}");
            }

            result = await EndTurnAsync(session, result.State.ActivePlayerId, $"{intentPrefix}-end-to-reopen-{turnIndex}");
            AssertNoHiddenZoneLeak(result);
        }

        throw new InvalidOperationException($"B0 same-battlefield other-friendly-filtered static aura driver could not open a legal battle task: {DescribeState(result.State)}");
    }

    private static async ValueTask<ResolutionResult> DriveContestedBattlefieldToSourceCombatStaticAuraBattleAsync(
        MatchSession session,
        ResolutionResult current,
        string auraControllerId,
        string intentPrefix)
    {
        var result = current;
        for (var turnIndex = 0; turnIndex < 20; turnIndex++)
        {
            if (string.Equals(result.State.TimingState, TimingStates.SpellDuelOpen, StringComparison.Ordinal)
                || !string.IsNullOrWhiteSpace(result.State.FocusPlayerId))
            {
                result = await PassOpenSpellDuelAsync(session, result, $"{intentPrefix}-pass-focus-{turnIndex}");
                AssertNoHiddenZoneLeak(result);
                continue;
            }

            if (string.Equals(result.State.PendingTaskQueue.Phase, "BATTLE_TASKS", StringComparison.Ordinal)
                && result.Prompts[result.State.ActivePlayerId].Actions.Contains(CommandTypes.DeclareBattle, StringComparer.Ordinal))
            {
                if (!string.Equals(result.State.ActivePlayerId, auraControllerId, StringComparison.Ordinal))
                {
                    result = await SubmitFirstDeclareBattleCandidateAsync(
                        session,
                        result,
                        $"{intentPrefix}-clear-other-battle-{turnIndex}");
                    AssertNoHiddenZoneLeak(result);
                    continue;
                }

                return await SubmitSourceCombatStaticAuraDeclareBattleAsync(
                    session,
                    result,
                    auraControllerId,
                    $"{intentPrefix}-declare-{turnIndex}");
            }

            if (!string.Equals(result.State.Phase, MatchPhases.Main, StringComparison.Ordinal)
                || !string.Equals(result.State.TimingState, TimingStates.NeutralOpen, StringComparison.Ordinal)
                || result.State.PendingTaskQueue.HasTasks)
            {
                throw new InvalidOperationException($"B0 source-combat static aura driver cannot advance to battle: {DescribeState(result.State)}");
            }

            result = await EndTurnAsync(session, result.State.ActivePlayerId, $"{intentPrefix}-end-to-reopen-{turnIndex}");
            AssertNoHiddenZoneLeak(result);
        }

        throw new InvalidOperationException($"B0 source-combat static aura driver could not open a legal battle task: {DescribeState(result.State)}");
    }

    private static async ValueTask<ResolutionResult> DriveContestedBattlefieldToSameBattlefieldStaticKeywordBattleAsync(
        MatchSession session,
        ResolutionResult current,
        string auraControllerId,
        string intentPrefix)
    {
        var result = current;
        for (var turnIndex = 0; turnIndex < 20; turnIndex++)
        {
            if (string.Equals(result.State.TimingState, TimingStates.SpellDuelOpen, StringComparison.Ordinal)
                || !string.IsNullOrWhiteSpace(result.State.FocusPlayerId))
            {
                result = await PassOpenSpellDuelAsync(session, result, $"{intentPrefix}-pass-focus-{turnIndex}");
                AssertNoHiddenZoneLeak(result);
                continue;
            }

            if (string.Equals(result.State.PendingTaskQueue.Phase, "BATTLE_TASKS", StringComparison.Ordinal)
                && result.Prompts[result.State.ActivePlayerId].Actions.Contains(CommandTypes.DeclareBattle, StringComparer.Ordinal))
            {
                if (!string.Equals(result.State.ActivePlayerId, auraControllerId, StringComparison.Ordinal))
                {
                    result = await SubmitFirstDeclareBattleCandidateAsync(
                        session,
                        result,
                        $"{intentPrefix}-clear-other-battle-{turnIndex}");
                    AssertNoHiddenZoneLeak(result);
                    continue;
                }

                return await SubmitSameBattlefieldStaticKeywordDeclareBattleAsync(
                    session,
                    result,
                    auraControllerId,
                    $"{intentPrefix}-declare-{turnIndex}");
            }

            if (!string.Equals(result.State.Phase, MatchPhases.Main, StringComparison.Ordinal)
                || !string.Equals(result.State.TimingState, TimingStates.NeutralOpen, StringComparison.Ordinal)
                || result.State.PendingTaskQueue.HasTasks)
            {
                throw new InvalidOperationException($"B0 same-battlefield static keyword driver cannot advance to battle: {DescribeState(result.State)}");
            }

            result = await EndTurnAsync(session, result.State.ActivePlayerId, $"{intentPrefix}-end-to-reopen-{turnIndex}");
            AssertNoHiddenZoneLeak(result);
        }

        throw new InvalidOperationException($"B0 same-battlefield static keyword driver could not open a legal battle task: {DescribeState(result.State)}");
    }

    private static async ValueTask<ResolutionResult> DriveContestedBattlefieldToSameBattlefieldSteadfastBattleAsync(
        MatchSession session,
        ResolutionResult current,
        string attackingPlayerId,
        string defendingPlayerId,
        string intentPrefix)
    {
        var result = current;
        for (var turnIndex = 0; turnIndex < 20; turnIndex++)
        {
            if (string.Equals(result.State.TimingState, TimingStates.SpellDuelOpen, StringComparison.Ordinal)
                || !string.IsNullOrWhiteSpace(result.State.FocusPlayerId))
            {
                result = await PassOpenSpellDuelAsync(session, result, $"{intentPrefix}-pass-focus-{turnIndex}");
                AssertNoHiddenZoneLeak(result);
                continue;
            }

            if (string.Equals(result.State.PendingTaskQueue.Phase, "BATTLE_TASKS", StringComparison.Ordinal)
                && result.Prompts[result.State.ActivePlayerId].Actions.Contains(CommandTypes.DeclareBattle, StringComparer.Ordinal))
            {
                if (!string.Equals(result.State.ActivePlayerId, attackingPlayerId, StringComparison.Ordinal))
                {
                    result = await SubmitFirstDeclareBattleCandidateAsync(
                        session,
                        result,
                        $"{intentPrefix}-clear-other-battle-{turnIndex}");
                    AssertNoHiddenZoneLeak(result);
                    continue;
                }

                return await SubmitSameBattlefieldSteadfastDeclareBattleAsync(
                    session,
                    result,
                    attackingPlayerId,
                    defendingPlayerId,
                    $"{intentPrefix}-declare-{turnIndex}");
            }

            if (!string.Equals(result.State.Phase, MatchPhases.Main, StringComparison.Ordinal)
                || !string.Equals(result.State.TimingState, TimingStates.NeutralOpen, StringComparison.Ordinal)
                || result.State.PendingTaskQueue.HasTasks)
            {
                throw new InvalidOperationException($"B0 same-battlefield steadfast driver cannot advance to battle: {DescribeState(result.State)}");
            }

            result = await EndTurnAsync(session, result.State.ActivePlayerId, $"{intentPrefix}-end-to-reopen-{turnIndex}");
            AssertNoHiddenZoneLeak(result);
        }

        throw new InvalidOperationException($"B0 same-battlefield steadfast driver could not open a legal battle task: {DescribeState(result.State)}");
    }

    private static async ValueTask<(ResolutionResult AssignmentOpened, ResolutionResult BattleResult)> DriveContestedBattlefieldToTaricBulwarkDamageAssignmentAsync(
        MatchSession session,
        ResolutionResult current,
        string attackingPlayerId,
        string defendingPlayerId,
        string intentPrefix)
    {
        var result = current;
        for (var turnIndex = 0; turnIndex < 20; turnIndex++)
        {
            if (string.Equals(result.State.TimingState, TimingStates.SpellDuelOpen, StringComparison.Ordinal)
                || !string.IsNullOrWhiteSpace(result.State.FocusPlayerId))
            {
                result = await PassOpenSpellDuelAsync(session, result, $"{intentPrefix}-pass-focus-{turnIndex}");
                AssertNoHiddenZoneLeak(result);
                continue;
            }

            if (string.Equals(result.State.PendingTaskQueue.Phase, "BATTLE_TASKS", StringComparison.Ordinal)
                && result.Prompts[result.State.ActivePlayerId].Actions.Contains(CommandTypes.DeclareBattle, StringComparer.Ordinal))
            {
                if (!string.Equals(result.State.ActivePlayerId, attackingPlayerId, StringComparison.Ordinal))
                {
                    result = await SubmitFirstDeclareBattleCandidateAsync(
                        session,
                        result,
                        $"{intentPrefix}-clear-other-battle-{turnIndex}");
                    AssertNoHiddenZoneLeak(result);
                    continue;
                }

                return await SubmitTaricBulwarkDamageAssignmentBattleAsync(
                    session,
                    result,
                    attackingPlayerId,
                    defendingPlayerId,
                    $"{intentPrefix}-declare-{turnIndex}");
            }

            if (!string.Equals(result.State.Phase, MatchPhases.Main, StringComparison.Ordinal)
                || !string.Equals(result.State.TimingState, TimingStates.NeutralOpen, StringComparison.Ordinal)
                || result.State.PendingTaskQueue.HasTasks)
            {
                throw new InvalidOperationException($"B0 Taric bulwark assignment driver cannot advance to battle: {DescribeState(result.State)}");
            }

            result = await EndTurnAsync(session, result.State.ActivePlayerId, $"{intentPrefix}-end-to-reopen-{turnIndex}");
            AssertNoHiddenZoneLeak(result);
        }

        throw new InvalidOperationException($"B0 Taric bulwark assignment driver could not open a legal battle task: {DescribeState(result.State)}");
    }

    private static async ValueTask<ResolutionResult> PreparePlayerBoardAsync(
        MatchSession session,
        string playerId,
        ResolutionResult current,
        string label,
        bool playUnitToBattlefield)
    {
        var result = current;
        result = await TapAllAvailableRunesAsync(session, playerId, result, $"b0-{label}-tap");
        result = await TryPlayFirstUnitAsync(session, playerId, result, $"b0-{label}-play-unit", playUnitToBattlefield);
        return result;
    }

    private static async ValueTask<ResolutionResult> PlaySpecificUnitToBaseAndMoveToBattlefieldAsync(
        MatchSession session,
        string playerId,
        ResolutionResult current,
        string cardNo,
        string battlefieldDestination,
        string intentPrefix)
    {
        var sourceObjectId = FindHandCardObjectByCardNo(current.State, playerId, cardNo)
            ?? throw new InvalidOperationException($"B0 damage-assignment driver could not find {cardNo} in {playerId}'s hand.");
        var play = await session.SubmitAsync(
            playerId,
            $"{intentPrefix}-play",
            new PlayCardCommand(sourceObjectId, cardNo, [], Destination: "BASE"),
            RawCommand(new PlayCardCommand(sourceObjectId, cardNo, [], Destination: "BASE")),
            CancellationToken.None);
        AssertAccepted(play);
        AssertNoHiddenZoneLeak(play);

        var resolved = await ResolveStackPassPassAsync(session, play, $"{intentPrefix}-resolve");
        var baseObjectId = resolved.State.PlayerZones[playerId].Base
            .FirstOrDefault(objectId => resolved.State.CardObjects.TryGetValue(objectId, out var cardObject)
                && string.Equals(cardObject.CardNo, cardNo, StringComparison.Ordinal)
                && !cardObject.IsExhausted
                && !cardObject.IsFaceDown)
            ?? throw new InvalidOperationException($"B0 damage-assignment driver could not find ready base {cardNo} for {playerId}.");
        var move = await session.SubmitAsync(
            playerId,
            $"{intentPrefix}-move",
            new MoveUnitCommand(baseObjectId, "BASE", battlefieldDestination, []),
            RawCommand(new MoveUnitCommand(baseObjectId, "BASE", battlefieldDestination, [])),
            CancellationToken.None);
        AssertAccepted(move);
        AssertNoHiddenZoneLeak(move);
        return move;
    }

    private static async ValueTask<ResolutionResult> PlaySpecificUnitToBaseAsync(
        MatchSession session,
        string playerId,
        ResolutionResult current,
        string cardNo,
        string intentPrefix,
        IReadOnlyList<string>? optionalCosts = null)
    {
        var sourceObjectId = FindHandCardObjectByCardNo(current.State, playerId, cardNo)
            ?? throw new InvalidOperationException($"B0 Treant driver could not find {cardNo} in {playerId}'s hand.");
        var play = await session.SubmitAsync(
            playerId,
            $"{intentPrefix}-play",
            new PlayCardCommand(sourceObjectId, cardNo, [], OptionalCosts: optionalCosts, Destination: "BASE"),
            RawCommand(new PlayCardCommand(sourceObjectId, cardNo, [], OptionalCosts: optionalCosts, Destination: "BASE")),
            CancellationToken.None);
        AssertAccepted(play);
        AssertNoHiddenZoneLeak(play);
        return await ResolveStackPassPassAsync(session, play, $"{intentPrefix}-resolve");
    }

    private static async ValueTask<ResolutionResult> DriveBaseUnitToBattlefieldWhenReadyAsync(
        MatchSession session,
        ResolutionResult current,
        string playerId,
        string cardNo,
        string battlefieldDestination,
        string intentPrefix)
    {
        var result = current;
        for (var turnIndex = 0; turnIndex < 12; turnIndex++)
        {
            if (string.Equals(result.State.TimingState, TimingStates.SpellDuelOpen, StringComparison.Ordinal)
                || !string.IsNullOrWhiteSpace(result.State.FocusPlayerId))
            {
                result = await PassOpenSpellDuelAsync(session, result, $"{intentPrefix}-pass-focus-{turnIndex}");
                AssertNoHiddenZoneLeak(result);
                continue;
            }

            if (string.Equals(result.State.PendingTaskQueue.Phase, "BATTLE_TASKS", StringComparison.Ordinal)
                && result.Prompts[result.State.ActivePlayerId].Actions.Contains(CommandTypes.DeclareBattle, StringComparer.Ordinal))
            {
                result = await SubmitFirstDeclareBattleCandidateAsync(
                    session,
                    result,
                    $"{intentPrefix}-clear-existing-battle-{turnIndex}");
                AssertNoHiddenZoneLeak(result);
                continue;
            }

            if (!string.Equals(result.State.Phase, MatchPhases.Main, StringComparison.Ordinal)
                || !string.Equals(result.State.TimingState, TimingStates.NeutralOpen, StringComparison.Ordinal)
                || result.State.PendingTaskQueue.HasTasks)
            {
                throw new InvalidOperationException($"B0 Treant driver cannot move staged {cardNo}: {DescribeState(result.State)}");
            }

            if (!string.Equals(result.State.ActivePlayerId, playerId, StringComparison.Ordinal))
            {
                result = await EndTurnAsync(session, result.State.ActivePlayerId, $"{intentPrefix}-wait-active-{turnIndex}");
                AssertNoHiddenZoneLeak(result);
                continue;
            }

            var baseObjectId = result.State.PlayerZones[playerId].Base.FirstOrDefault(objectId =>
                IsReadyUnit(result.State, objectId)
                && result.State.CardObjects.TryGetValue(objectId, out var cardObject)
                && string.Equals(cardObject.CardNo, cardNo, StringComparison.Ordinal));
            if (baseObjectId is not null)
            {
                var move = await session.SubmitAsync(
                    playerId,
                    $"{intentPrefix}-move-{turnIndex}",
                    new MoveUnitCommand(baseObjectId, "BASE", battlefieldDestination, []),
                    RawCommand(new MoveUnitCommand(baseObjectId, "BASE", battlefieldDestination, [])),
                    CancellationToken.None);
                AssertAccepted(move);
                AssertNoHiddenZoneLeak(move);
                return move;
            }

            result = await EndTurnAsync(session, playerId, $"{intentPrefix}-wait-ready-{turnIndex}");
            AssertNoHiddenZoneLeak(result);
        }

        throw new InvalidOperationException($"B0 Treant driver could not ready and move base {cardNo} for {playerId}.");
    }

    private static async ValueTask<ResolutionResult> DriveAnyBaseUnitToBattlefieldWhenReadyAsync(
        MatchSession session,
        ResolutionResult current,
        string playerId,
        string battlefieldDestination,
        string intentPrefix)
    {
        var result = current;
        for (var turnIndex = 0; turnIndex < 12; turnIndex++)
        {
            if (string.Equals(result.State.TimingState, TimingStates.SpellDuelOpen, StringComparison.Ordinal)
                || !string.IsNullOrWhiteSpace(result.State.FocusPlayerId))
            {
                result = await PassOpenSpellDuelAsync(session, result, $"{intentPrefix}-pass-focus-{turnIndex}");
                AssertNoHiddenZoneLeak(result);
                continue;
            }

            if (string.Equals(result.State.PendingTaskQueue.Phase, "BATTLE_TASKS", StringComparison.Ordinal)
                && result.Prompts[result.State.ActivePlayerId].Actions.Contains(CommandTypes.DeclareBattle, StringComparer.Ordinal))
            {
                result = await SubmitFirstDeclareBattleCandidateAsync(
                    session,
                    result,
                    $"{intentPrefix}-clear-existing-battle-{turnIndex}");
                AssertNoHiddenZoneLeak(result);
                continue;
            }

            if (!string.Equals(result.State.Phase, MatchPhases.Main, StringComparison.Ordinal)
                || !string.Equals(result.State.TimingState, TimingStates.NeutralOpen, StringComparison.Ordinal)
                || result.State.PendingTaskQueue.HasTasks)
            {
                throw new InvalidOperationException($"B0 opponent staging driver cannot move a base unit: {DescribeState(result.State)}");
            }

            if (!string.Equals(result.State.ActivePlayerId, playerId, StringComparison.Ordinal))
            {
                result = await EndTurnAsync(session, result.State.ActivePlayerId, $"{intentPrefix}-wait-active-{turnIndex}");
                AssertNoHiddenZoneLeak(result);
                continue;
            }

            var baseObjectId = result.State.PlayerZones[playerId].Base.FirstOrDefault(objectId => IsReadyUnit(result.State, objectId));
            if (baseObjectId is not null)
            {
                var move = await session.SubmitAsync(
                    playerId,
                    $"{intentPrefix}-move-{turnIndex}",
                    new MoveUnitCommand(baseObjectId, "BASE", battlefieldDestination, []),
                    RawCommand(new MoveUnitCommand(baseObjectId, "BASE", battlefieldDestination, [])),
                    CancellationToken.None);
                AssertAccepted(move);
                AssertNoHiddenZoneLeak(move);
                return move;
            }

            result = await EndTurnAsync(session, playerId, $"{intentPrefix}-wait-ready-{turnIndex}");
            AssertNoHiddenZoneLeak(result);
        }

        throw new InvalidOperationException($"B0 opponent staging driver could not ready and move a base unit for {playerId}.");
    }

    private static async ValueTask<ResolutionResult> PlaySpecificUnitToBattlefieldAsync(
        MatchSession session,
        string playerId,
        ResolutionResult current,
        string cardNo,
        string battlefieldDestination,
        string intentPrefix)
    {
        var sourceObjectId = FindHandCardObjectByCardNo(current.State, playerId, cardNo)
            ?? throw new InvalidOperationException($"B0 shadow-response driver could not find {cardNo} in {playerId}'s hand.");
        var play = await session.SubmitAsync(
            playerId,
            $"{intentPrefix}-play",
            new PlayCardCommand(sourceObjectId, cardNo, [], Destination: battlefieldDestination),
            RawCommand(new PlayCardCommand(sourceObjectId, cardNo, [], Destination: battlefieldDestination)),
            CancellationToken.None);
        AssertAccepted(play);
        AssertNoHiddenZoneLeak(play);

        var resolved = await ResolveStackPassPassAsync(session, play, $"{intentPrefix}-resolve");
        var battlefieldObjectId = resolved.State.PlayerZones[playerId].Battlefields
            .FirstOrDefault(objectId => IsObjectLocatedAtBattlefield(resolved.State, objectId, battlefieldDestination)
                && resolved.State.CardObjects.TryGetValue(objectId, out var cardObject)
                && string.Equals(cardObject.CardNo, cardNo, StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"B0 shadow-response driver could not find battlefield {cardNo} for {playerId}.");
        Assert.False(resolved.State.CardObjects[battlefieldObjectId].IsExhausted);
        return resolved;
    }

    private static async ValueTask<ResolutionResult> SubmitMultiDefenderDeclareBattleAsync(
        MatchSession session,
        ResolutionResult current,
        string battlefieldOwnerId,
        string invadingPlayerId,
        string intentId)
    {
        Assert.Equal(battlefieldOwnerId, current.State.ActivePlayerId);
        var playerId = current.State.ActivePlayerId;
        var candidate = EnabledCandidate(current.Prompts[playerId], CommandTypes.DeclareBattle)
            ?? throw new InvalidOperationException($"B0 damage-assignment driver could not find DECLARE_BATTLE for {playerId}.");
        var battlefieldId = candidate.Destinations?.FirstOrDefault()?.Id
            ?? throw new InvalidOperationException("B0 damage-assignment driver could not find battle destination.");
        var attackerObjectId = candidate.Sources?.FirstOrDefault()?.Id
            ?? throw new InvalidOperationException("B0 damage-assignment driver could not find battle attacker.");
        var defenderObjectIds = current.State.PlayerZones[invadingPlayerId].Battlefields
            .Where(objectId => IsObjectLocatedAtBattlefield(current.State, objectId, battlefieldId))
            .Where(objectId => IsReadyUnit(current.State, objectId))
            .Where(objectId => current.State.CardObjects.TryGetValue(objectId, out var cardObject)
                && (string.Equals(cardObject.CardNo, MutantKittenCardNo, StringComparison.Ordinal)
                    || string.Equals(cardObject.CardNo, LeblancCardNo, StringComparison.Ordinal)))
            .OrderBy(objectId => current.State.CardObjects[objectId].CardNo, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(2, defenderObjectIds.Length);

        var command = new DeclareBattleCommand(
            battlefieldId,
            [attackerObjectId],
            defenderObjectIds,
            OptionalCosts: ["COMBAT_ASSIGNMENT"]);
        return await session.SubmitAsync(
            playerId,
            intentId,
            command,
            JsonSerializer.SerializeToElement(new
            {
                cmdType = CommandTypes.DeclareBattle,
                battlefieldId,
                attackerObjectIds = new[] { attackerObjectId },
                defenderObjectIds,
                optionalCosts = new[] { "COMBAT_ASSIGNMENT" }
            }),
            CancellationToken.None);
    }

    private static async ValueTask<ResolutionResult> SubmitShadowResponseDeclareBattleAsync(
        MatchSession session,
        ResolutionResult current,
        string battlefieldOwnerId,
        string shadowControllerId,
        string intentId)
    {
        Assert.Equal(battlefieldOwnerId, current.State.ActivePlayerId);
        var playerId = current.State.ActivePlayerId;
        var candidate = EnabledCandidate(current.Prompts[playerId], CommandTypes.DeclareBattle)
            ?? throw new InvalidOperationException($"B0 shadow-response driver could not find DECLARE_BATTLE for {playerId}.");
        var battlefieldId = candidate.Destinations?.FirstOrDefault()?.Id
            ?? throw new InvalidOperationException("B0 shadow-response driver could not find battle destination.");
        var attackerObjectId = candidate.Sources?.FirstOrDefault()?.Id
            ?? throw new InvalidOperationException("B0 shadow-response driver could not find battle attacker.");
        var shadowObjectId = current.State.PlayerZones[shadowControllerId].Battlefields
            .FirstOrDefault(objectId => IsObjectLocatedAtBattlefield(current.State, objectId, battlefieldId)
                && IsReadyUnit(current.State, objectId)
                && current.State.CardObjects.TryGetValue(objectId, out var cardObject)
                && string.Equals(cardObject.CardNo, ShadowCardNo, StringComparison.Ordinal))
            ?? throw new InvalidOperationException("B0 shadow-response driver could not find ready Shadow defender.");

        var command = new DeclareBattleCommand(
            battlefieldId,
            [attackerObjectId],
            [shadowObjectId],
            OptionalCosts: ["COMBAT_ASSIGNMENT"]);
        var declared = await session.SubmitAsync(
            playerId,
            intentId,
            command,
            JsonSerializer.SerializeToElement(new
            {
                cmdType = CommandTypes.DeclareBattle,
                battlefieldId,
                attackerObjectIds = new[] { attackerObjectId },
                defenderObjectIds = new[] { shadowObjectId },
                optionalCosts = new[] { "COMBAT_ASSIGNMENT" }
            }),
            CancellationToken.None);
        if (declared.Accepted)
        {
            Assert.Contains(declared.Events, gameEvent =>
                string.Equals(gameEvent.Kind, "BATTLE_RESPONSE_PRIORITY_OPENED", StringComparison.Ordinal));
            Assert.Equal(shadowControllerId, declared.State.PriorityPlayerId);
        }

        return declared;
    }

    private static async ValueTask<(ResolutionResult Result, string TargetObjectId)> ActivateCurrentShadowResponseAsync(
        MatchSession session,
        ResolutionResult current,
        string playerId,
        string intentId)
    {
        var prompt = current.Prompts[playerId];
        Assert.Equal(PromptTypes.StackPriority, prompt.View?.Type);
        var candidate = EnabledCandidate(prompt, CommandTypes.ActivateAbility)
            ?? throw new InvalidOperationException($"B0 shadow-response driver could not find ACTIVATE_ABILITY for {playerId}.");
        var metadata = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(candidate.Metadata);
        var requirement = Assert.Single(
            Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(metadata["sourceRequirements"]),
            entry => string.Equals(
                entry["abilityId"] as string,
                P4ActivatedAbilityCatalog.ShadowStunAbilityId,
                StringComparison.Ordinal));
        var sourceObjectId = Assert.IsType<string>(requirement["sourceObjectId"]);
        var targetChoicesByIndex = Assert.IsAssignableFrom<IReadOnlyDictionary<string, IReadOnlyList<ActionPromptChoiceDto>>>(
            requirement["targetChoicesByIndex"]);
        var targetObjectId = Assert.Single(targetChoicesByIndex["0"]).Id;
        var optionalCosts = ActivateAbilityPaymentResourceChoicesForRequirement(requirement);
        var command = new ActivateAbilityCommand(
            sourceObjectId,
            P4ActivatedAbilityCatalog.ShadowStunAbilityId,
            [targetObjectId],
            optionalCosts);
        var result = await session.SubmitAsync(
            playerId,
            intentId,
            command,
            JsonSerializer.SerializeToElement(new
            {
                cmdType = CommandTypes.ActivateAbility,
                sourceObjectId,
                abilityId = P4ActivatedAbilityCatalog.ShadowStunAbilityId,
                targetObjectIds = new[] { targetObjectId },
                optionalCosts
            }),
            CancellationToken.None);
        AssertAccepted(result);
        AssertNoHiddenZoneLeak(result);
        return (result, targetObjectId);
    }

    private static IReadOnlyList<string> ActivateAbilityPaymentResourceChoicesForRequirement(
        IReadOnlyDictionary<string, object?> requirement)
    {
        var powerCost = Assert.IsType<int>(requirement["powerCost"]);
        var availablePower = Assert.IsType<int>(requirement["availablePower"]);
        if (availablePower >= powerCost)
        {
            return [];
        }

        var paymentResourceChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(
            requirement["paymentResourceChoices"]);
        var choice = paymentResourceChoices.FirstOrDefault()
            ?? throw new InvalidOperationException("B0 shadow-response driver expected a payment resource choice for Shadow power cost.");
        return [choice.Id];
    }

    private static async ValueTask<ResolutionResult> PassOpenBattleResponseAsync(
        MatchSession session,
        ResolutionResult current,
        string intentPrefix)
    {
        var result = current;
        for (var index = 0; index < 8; index++)
        {
            if (!result.State.BattleState.IsActive || string.IsNullOrWhiteSpace(result.State.PriorityPlayerId))
            {
                return result;
            }

            if (result.State.StackItems.Count > 0)
            {
                result = await ResolveStackPassPassAsync(session, result, $"{intentPrefix}-stack-{index}");
                continue;
            }

            var priorityPlayerId = result.State.PriorityPlayerId;
            result = await session.SubmitAsync(
                priorityPlayerId,
                $"{intentPrefix}-{index}",
                new PassPriorityCommand(),
                RawCommand(new PassPriorityCommand()),
                CancellationToken.None);
            AssertAccepted(result);
            AssertNoHiddenZoneLeak(result);
        }

        throw new InvalidOperationException("B0 shadow-response driver exceeded battle response pass guard.");
    }

    private static async ValueTask<ResolutionResult> ResolveOpenBattleDamageAssignmentsAsync(
        MatchSession session,
        ResolutionResult current,
        string intentPrefix)
    {
        var result = current;
        for (var index = 0; index < 4; index++)
        {
            var assigningPlayerId = PlayerWithEnabledCandidate(result, CommandTypes.AssignCombatDamage);
            if (assigningPlayerId is null)
            {
                return result;
            }

            result = await SubmitCurrentBattleDamageAssignmentAsync(
                session,
                result,
                assigningPlayerId,
                $"{intentPrefix}-{index}");
            AssertNoHiddenZoneLeak(result);
        }

        throw new InvalidOperationException("B0 damage-assignment driver exceeded ASSIGN_COMBAT_DAMAGE guard.");
    }

    private static async ValueTask<ResolutionResult> SubmitCurrentBattleDamageAssignmentAsync(
        MatchSession session,
        ResolutionResult current,
        string playerId,
        string intentId)
    {
        var prompt = current.Prompts[playerId];
        Assert.Equal(PromptTypes.AssignCombatDamage, prompt.View?.Type);
        var view = Assert.IsType<PromptViewDto>(prompt.View);
        var metadata = view.Metadata
            ?? throw new InvalidOperationException("ASSIGN_COMBAT_DAMAGE prompt missing metadata.");
        var battleId = Assert.IsType<string>(metadata["battleId"]);
        var battlefieldId = Assert.IsType<string>(metadata["battlefieldId"]);
        var damagePool = IntMap(metadata["assignableDamagePool"]);
        var legalTargets = StringListMap(metadata["legalTargets"]);
        var lethalThreshold = IntMap(metadata["lethalDamageThreshold"]);
        var assignments = new List<CombatDamageAssignmentDto>();
        foreach (var (sourceObjectId, damage) in damagePool)
        {
            if (damage <= 0 || !legalTargets.TryGetValue(sourceObjectId, out var targets) || targets.Count == 0)
            {
                continue;
            }

            var remainingDamage = damage;
            for (var targetIndex = 0; targetIndex < targets.Count && remainingDamage > 0; targetIndex++)
            {
                var targetObjectId = targets[targetIndex];
                var isLastTarget = targetIndex == targets.Count - 1;
                var assignDamage = isLastTarget
                    ? remainingDamage
                    : Math.Min(remainingDamage, Math.Max(0, lethalThreshold.GetValueOrDefault(targetObjectId)));
                if (assignDamage <= 0)
                {
                    continue;
                }

                assignments.Add(new CombatDamageAssignmentDto(sourceObjectId, targetObjectId, assignDamage));
                remainingDamage -= assignDamage;
            }
        }

        Assert.NotEmpty(assignments);
        var command = new AssignCombatDamageCommand(battleId, battlefieldId, assignments);
        var result = await session.SubmitAsync(
            playerId,
            intentId,
            command,
            RawCommand(command),
            CancellationToken.None);
        AssertAccepted(result);
        return result;
    }

    private static async ValueTask<ResolutionResult> SubmitFirstDeclareBattleCandidateAsync(
        MatchSession session,
        ResolutionResult current,
        string intentId)
    {
        var playerId = current.State.ActivePlayerId;
        var candidate = EnabledCandidate(current.Prompts[playerId], CommandTypes.DeclareBattle)
            ?? throw new InvalidOperationException($"B0 auto-driver could not find an enabled DECLARE_BATTLE candidate for {playerId}.");
        var battlefieldId = candidate.Destinations?.FirstOrDefault()?.Id
            ?? throw new InvalidOperationException("B0 auto-driver could not find a DECLARE_BATTLE battlefield choice.");
        var attackerObjectId = candidate.Sources?.FirstOrDefault()?.Id
            ?? throw new InvalidOperationException("B0 auto-driver could not find a DECLARE_BATTLE attacker choice.");
        var defenderObjectId = candidate.Targets?.FirstOrDefault()?.Id
            ?? throw new InvalidOperationException("B0 auto-driver could not find a DECLARE_BATTLE defender choice.");
        var command = new DeclareBattleCommand(
            battlefieldId,
            [attackerObjectId],
            [defenderObjectId],
            OptionalCosts: ["COMBAT_ASSIGNMENT"]);
        var result = await session.SubmitAsync(
            playerId,
            intentId,
            command,
            JsonSerializer.SerializeToElement(new
            {
                cmdType = CommandTypes.DeclareBattle,
                battlefieldId,
                attackerObjectIds = new[] { attackerObjectId },
                defenderObjectIds = new[] { defenderObjectId },
                optionalCosts = new[] { "COMBAT_ASSIGNMENT" }
            }),
            CancellationToken.None);
        AssertAccepted(result);
        return result;
    }

    private static async ValueTask<ResolutionResult> SubmitCrimsonSignetTreantDeclareBattleAsync(
        MatchSession session,
        ResolutionResult current,
        string treantPlayerId,
        string intentId)
    {
        Assert.Equal(treantPlayerId, current.State.ActivePlayerId);
        var playerId = current.State.ActivePlayerId;
        var opponentId = OpponentOf(current.State, playerId);
        var candidate = EnabledCandidate(current.Prompts[playerId], CommandTypes.DeclareBattle)
            ?? throw new InvalidOperationException($"B0 Treant driver could not find DECLARE_BATTLE for {playerId}.");
        var treantObjectId = FindBattlefieldUnitByCardNo(
            current.State,
            playerId,
            CrimsonSignetTreantCardNo,
            readyOnly: true)
            ?? throw new InvalidOperationException("B0 Treant driver could not find a ready Crimson Signet Treant attacker.");
        var battlefieldId = current.State.ObjectLocations[treantObjectId].BattlefieldObjectId
            ?? throw new InvalidOperationException("B0 Treant driver could not locate Treant's battlefield.");
        var legalSourceIds = candidate.Sources?.Select(choice => choice.Id).ToHashSet(StringComparer.Ordinal)
            ?? [];
        var legalTargetIds = candidate.Targets?.Select(choice => choice.Id).ToHashSet(StringComparer.Ordinal)
            ?? [];
        var legalDestinationIds = candidate.Destinations?.Select(choice => choice.Id).ToHashSet(StringComparer.Ordinal)
            ?? [];
        Assert.Contains(treantObjectId, legalSourceIds);
        Assert.Contains(battlefieldId, legalDestinationIds);

        var defenderObjectId = FindReadyBattlefieldDefender(
            current.State,
            opponentId,
            battlefieldId,
            legalTargetIds,
            maxPowerExclusive: current.State.CardObjects[treantObjectId].Power)
            ?? throw new InvalidOperationException(
                $"B0 Treant driver could not find a legal ready defender below Treant power: {DescribeState(current.State)}");
        var command = new DeclareBattleCommand(
            battlefieldId,
            [treantObjectId],
            [defenderObjectId],
            OptionalCosts: ["COMBAT_ASSIGNMENT"]);
        var declared = await session.SubmitAsync(
            playerId,
            intentId,
            command,
            JsonSerializer.SerializeToElement(new
            {
                cmdType = CommandTypes.DeclareBattle,
                battlefieldId,
                attackerObjectIds = new[] { treantObjectId },
                defenderObjectIds = new[] { defenderObjectId },
                optionalCosts = new[] { "COMBAT_ASSIGNMENT" }
            }),
            CancellationToken.None);
        AssertAccepted(declared);
        AssertNoHiddenZoneLeak(declared);

        var result = await PassOpenBattleResponseAsync(session, declared, $"{intentId}-battle-response");
        result = await ResolveOpenBattleDamageAssignmentsAsync(session, result, $"{intentId}-assign-damage");
        result = await PassOpenBattleResponseAsync(session, result, $"{intentId}-battle-response-after-assignment");
        AssertNoHiddenZoneLeak(result);
        return result;
    }

    private static async ValueTask<ResolutionResult> SubmitSameBattlefieldStaticAuraDeclareBattleAsync(
        MatchSession session,
        ResolutionResult current,
        string auraControllerId,
        string intentId)
    {
        Assert.Equal(auraControllerId, current.State.ActivePlayerId);
        var playerId = current.State.ActivePlayerId;
        var opponentId = OpponentOf(current.State, playerId);
        var candidate = EnabledCandidate(current.Prompts[playerId], CommandTypes.DeclareBattle)
            ?? throw new InvalidOperationException($"B0 same-battlefield static aura driver could not find DECLARE_BATTLE for {playerId}.");
        var auraSourceObjectId = FindBattlefieldUnitByCardNo(
            current.State,
            playerId,
            GarenSameBattlefieldStaticAuraCardNo)
            ?? throw new InvalidOperationException("B0 same-battlefield static aura driver could not find Garen source.");
        var battlefieldId = current.State.ObjectLocations[auraSourceObjectId].BattlefieldObjectId
            ?? throw new InvalidOperationException("B0 same-battlefield static aura driver could not locate Garen's battlefield.");
        var attackerObjectId = FindBattlefieldUnitByCardNo(
            current.State,
            playerId,
            DemaciaEnvoyCardNo,
            battlefieldId,
            readyOnly: true)
            ?? throw new InvalidOperationException("B0 same-battlefield static aura driver could not find a ready boosted ally.");
        var legalSourceIds = candidate.Sources?.Select(choice => choice.Id).ToHashSet(StringComparer.Ordinal)
            ?? [];
        var legalTargetIds = candidate.Targets?.Select(choice => choice.Id).ToHashSet(StringComparer.Ordinal)
            ?? [];
        var legalDestinationIds = candidate.Destinations?.Select(choice => choice.Id).ToHashSet(StringComparer.Ordinal)
            ?? [];
        Assert.Contains(attackerObjectId, legalSourceIds);
        Assert.Contains(battlefieldId, legalDestinationIds);

        var staticAura = Assert.Single(current.State.ContinuousEffects, effect =>
            string.Equals(effect.Layer, ContinuousEffectLayers.StaticAura, StringComparison.Ordinal)
            && string.Equals(effect.SourceObjectId, auraSourceObjectId, StringComparison.Ordinal)
            && string.Equals(effect.TargetObjectId, attackerObjectId, StringComparison.Ordinal));
        Assert.Equal(GarenSameBattlefieldStaticAuraCardNo, staticAura.SourceCardNo);
        Assert.Equal(StaticAuraKinds.SameBattlefieldOtherFriendlyUnitsPowerPlusOne, staticAura.EffectKind);
        Assert.Equal(1, staticAura.PowerDelta);
        Assert.Equal("CoreRuleEngine.ResolveSameBattlefieldOtherFriendlyUnitsPowerBonus", staticAura.SourcePath);
        Assert.DoesNotContain(
            current.State.ContinuousEffects,
            effect => string.Equals(effect.SourceObjectId, auraSourceObjectId, StringComparison.Ordinal)
                && string.Equals(effect.TargetObjectId, auraSourceObjectId, StringComparison.Ordinal));

        var defenderObjectId = FindReadyBattlefieldDefender(
            current.State,
            opponentId,
            battlefieldId,
            legalTargetIds)
            ?? throw new InvalidOperationException(
                $"B0 same-battlefield static aura driver could not find a legal ready defender: {DescribeState(current.State)}");
        var command = new DeclareBattleCommand(
            battlefieldId,
            [attackerObjectId],
            [defenderObjectId],
            OptionalCosts: ["COMBAT_ASSIGNMENT"]);
        var declared = await session.SubmitAsync(
            playerId,
            intentId,
            command,
            JsonSerializer.SerializeToElement(new
            {
                cmdType = CommandTypes.DeclareBattle,
                battlefieldId,
                attackerObjectIds = new[] { attackerObjectId },
                defenderObjectIds = new[] { defenderObjectId },
                optionalCosts = new[] { "COMBAT_ASSIGNMENT" }
            }),
            CancellationToken.None);
        AssertAccepted(declared);
        AssertNoHiddenZoneLeak(declared);

        var result = await PassOpenBattleResponseAsync(session, declared, $"{intentId}-battle-response");
        result = await ResolveOpenBattleDamageAssignmentsAsync(session, result, $"{intentId}-assign-damage");
        result = await PassOpenBattleResponseAsync(session, result, $"{intentId}-battle-response-after-assignment");
        AssertNoHiddenZoneLeak(result);
        return result;
    }

    private static async ValueTask<ResolutionResult> SubmitBattlefieldAllUnitsStaticAuraDeclareBattleAsync(
        MatchSession session,
        ResolutionResult current,
        string battlefieldOwnerId,
        string intentId)
    {
        Assert.Equal(battlefieldOwnerId, current.State.ActivePlayerId);
        var playerId = current.State.ActivePlayerId;
        var opponentId = OpponentOf(current.State, playerId);
        var candidate = EnabledCandidate(current.Prompts[playerId], CommandTypes.DeclareBattle)
            ?? throw new InvalidOperationException($"B0 battlefield all-units static aura driver could not find DECLARE_BATTLE for {playerId}.");
        var attackerObjectId = FindBattlefieldUnitByCardNo(
            current.State,
            playerId,
            WildclawBeastmasterCardNo,
            readyOnly: true)
            ?? throw new InvalidOperationException("B0 battlefield all-units static aura driver could not find a ready attacker.");
        var battlefieldId = current.State.ObjectLocations[attackerObjectId].BattlefieldObjectId
            ?? throw new InvalidOperationException("B0 battlefield all-units static aura driver could not locate the attacker's battlefield.");
        Assert.True(current.State.CardObjects.TryGetValue(battlefieldId, out var battlefield));
        Assert.Equal(TrifarianTrainingGroundsBattlefieldAllUnitsStaticAuraCardNo, battlefield.CardNo);
        var legalSourceIds = candidate.Sources?.Select(choice => choice.Id).ToHashSet(StringComparer.Ordinal)
            ?? [];
        var legalTargetIds = candidate.Targets?.Select(choice => choice.Id).ToHashSet(StringComparer.Ordinal)
            ?? [];
        var legalDestinationIds = candidate.Destinations?.Select(choice => choice.Id).ToHashSet(StringComparer.Ordinal)
            ?? [];
        Assert.Contains(attackerObjectId, legalSourceIds);
        Assert.Contains(battlefieldId, legalDestinationIds);

        var defenderObjectId = FindReadyBattlefieldDefender(
            current.State,
            opponentId,
            battlefieldId,
            legalTargetIds)
            ?? throw new InvalidOperationException(
                $"B0 battlefield all-units static aura driver could not find a legal ready defender: {DescribeState(current.State)}");

        var attackerStaticAura = Assert.Single(current.State.ContinuousEffects, effect =>
            string.Equals(effect.Layer, ContinuousEffectLayers.StaticAura, StringComparison.Ordinal)
            && string.Equals(effect.SourceObjectId, battlefieldId, StringComparison.Ordinal)
            && string.Equals(effect.TargetObjectId, attackerObjectId, StringComparison.Ordinal));
        AssertBattlefieldAllUnitsStaticAura(attackerStaticAura, battlefieldId, attackerObjectId, defenderObjectId, basePower: 7);

        var defenderStaticAura = Assert.Single(current.State.ContinuousEffects, effect =>
            string.Equals(effect.Layer, ContinuousEffectLayers.StaticAura, StringComparison.Ordinal)
            && string.Equals(effect.SourceObjectId, battlefieldId, StringComparison.Ordinal)
            && string.Equals(effect.TargetObjectId, defenderObjectId, StringComparison.Ordinal));
        AssertBattlefieldAllUnitsStaticAura(defenderStaticAura, battlefieldId, attackerObjectId, defenderObjectId, current.State.CardObjects[defenderObjectId].Power);

        var command = new DeclareBattleCommand(
            battlefieldId,
            [attackerObjectId],
            [defenderObjectId],
            OptionalCosts: ["COMBAT_ASSIGNMENT"]);
        var declared = await session.SubmitAsync(
            playerId,
            intentId,
            command,
            JsonSerializer.SerializeToElement(new
            {
                cmdType = CommandTypes.DeclareBattle,
                battlefieldId,
                attackerObjectIds = new[] { attackerObjectId },
                defenderObjectIds = new[] { defenderObjectId },
                optionalCosts = new[] { "COMBAT_ASSIGNMENT" }
            }),
            CancellationToken.None);
        AssertAccepted(declared);
        AssertNoHiddenZoneLeak(declared);

        var result = await PassOpenBattleResponseAsync(session, declared, $"{intentId}-battle-response");
        result = await ResolveOpenBattleDamageAssignmentsAsync(session, result, $"{intentId}-assign-damage");
        result = await PassOpenBattleResponseAsync(session, result, $"{intentId}-battle-response-after-assignment");
        AssertNoHiddenZoneLeak(result);
        return result;
    }

    private static void AssertBattlefieldAllUnitsStaticAura(
        ContinuousEffectState staticAura,
        string battlefieldId,
        string attackerObjectId,
        string defenderObjectId,
        int basePower)
    {
        Assert.Equal(TrifarianTrainingGroundsBattlefieldAllUnitsStaticAuraCardNo, staticAura.SourceCardNo);
        Assert.Equal(StaticAuraKinds.BattlefieldAllUnitsPowerPlusOne, staticAura.EffectKind);
        Assert.Equal(1, staticAura.PowerDelta);
        Assert.Equal(basePower, staticAura.BasePower);
        Assert.Equal(basePower + 1, staticAura.EffectivePower);
        Assert.Equal("CoreRuleEngine.ResolveBattlefieldAllUnitsPowerBonus", staticAura.SourcePath);
        Assert.Equal("SOURCE_BATTLEFIELD_ALL_UNITS_POWER_PLUS_ONE_AND_PARTICIPANT_UNIT_AT_BATTLEFIELD", staticAura.Condition);
        Assert.Equal("DERIVED_FROM_CURRENT_BATTLEFIELD_OBJECT_LOCATIONS", staticAura.Lifecycle);
        var participantObjectIds = Assert.IsAssignableFrom<IReadOnlyList<string>>(staticAura.ParticipantObjectIds);
        Assert.Contains(attackerObjectId, participantObjectIds);
        Assert.Contains(defenderObjectId, participantObjectIds);
        var sourceDependencyObjectIds = Assert.IsAssignableFrom<IReadOnlyList<string>>(staticAura.SourceDependencyObjectIds);
        Assert.Contains(battlefieldId, sourceDependencyObjectIds);
    }

    private static async ValueTask<ResolutionResult> SubmitSourceSameLocationStaticAuraDeclareBattleAsync(
        MatchSession session,
        ResolutionResult current,
        string auraControllerId,
        string intentId)
    {
        Assert.Equal(auraControllerId, current.State.ActivePlayerId);
        var playerId = current.State.ActivePlayerId;
        var opponentId = OpponentOf(current.State, playerId);
        var candidate = EnabledCandidate(current.Prompts[playerId], CommandTypes.DeclareBattle)
            ?? throw new InvalidOperationException($"B0 source-same-location static aura driver could not find DECLARE_BATTLE for {playerId}.");
        var sourceObjectId = FindBattlefieldUnitByCardNo(
            current.State,
            playerId,
            ReliableSiegeDogSourceSameLocationStaticAuraCardNo,
            readyOnly: true)
            ?? throw new InvalidOperationException("B0 source-same-location static aura driver could not find a ready Reliable Siege Dog.");
        var battlefieldId = current.State.ObjectLocations[sourceObjectId].BattlefieldObjectId
            ?? throw new InvalidOperationException("B0 source-same-location static aura driver could not locate Reliable Siege Dog's battlefield.");
        var allyObjectId = FindBattlefieldUnitByCardNo(
            current.State,
            playerId,
            DemaciaEnvoyCardNo,
            battlefieldId)
            ?? throw new InvalidOperationException("B0 source-same-location static aura driver could not find a same-location friendly unit.");
        var legalSourceIds = candidate.Sources?.Select(choice => choice.Id).ToHashSet(StringComparer.Ordinal)
            ?? [];
        var legalTargetIds = candidate.Targets?.Select(choice => choice.Id).ToHashSet(StringComparer.Ordinal)
            ?? [];
        var legalDestinationIds = candidate.Destinations?.Select(choice => choice.Id).ToHashSet(StringComparer.Ordinal)
            ?? [];
        Assert.Contains(sourceObjectId, legalSourceIds);
        Assert.Contains(battlefieldId, legalDestinationIds);

        var staticAura = Assert.Single(current.State.ContinuousEffects, effect =>
            string.Equals(effect.Layer, ContinuousEffectLayers.StaticAura, StringComparison.Ordinal)
            && string.Equals(effect.SourceObjectId, sourceObjectId, StringComparison.Ordinal)
            && string.Equals(effect.TargetObjectId, sourceObjectId, StringComparison.Ordinal));
        Assert.Equal(ReliableSiegeDogSourceSameLocationStaticAuraCardNo, staticAura.SourceCardNo);
        Assert.Equal(StaticAuraKinds.SourceSameLocationOtherFriendlyUnitPower, staticAura.EffectKind);
        Assert.Equal(1, staticAura.PowerDelta);
        Assert.Equal(2, staticAura.BasePower);
        Assert.Equal(3, staticAura.EffectivePower);
        Assert.Equal("CoreRuleEngine.ResolveSourceSameLocationOtherFriendlyUnitPowerBonus", staticAura.SourcePath);
        Assert.Equal("SOURCE_AND_OTHER_FRIENDLY_PUBLIC_UNITS_AT_SAME_LOCATION", staticAura.Condition);
        Assert.Equal("RECOMPUTED_FROM_CURRENT_SAME_LOCATION_FRIENDLY_UNIT_LOCATIONS", staticAura.Lifecycle);
        var participantObjectIds = Assert.IsAssignableFrom<IReadOnlyList<string>>(staticAura.ParticipantObjectIds);
        Assert.Contains(allyObjectId, participantObjectIds);
        Assert.DoesNotContain(sourceObjectId, participantObjectIds);
        var sourceDependencyObjectIds = Assert.IsAssignableFrom<IReadOnlyList<string>>(staticAura.SourceDependencyObjectIds);
        Assert.Contains(sourceObjectId, sourceDependencyObjectIds);
        var targetDependencyObjectIds = Assert.IsAssignableFrom<IReadOnlyList<string>>(staticAura.TargetDependencyObjectIds);
        Assert.Contains(sourceObjectId, targetDependencyObjectIds);
        var participantDependencyObjectIds = Assert.IsAssignableFrom<IReadOnlyList<string>>(staticAura.ParticipantDependencyObjectIds);
        Assert.Contains(allyObjectId, participantDependencyObjectIds);

        var defenderObjectId = FindReadyBattlefieldDefender(
            current.State,
            opponentId,
            battlefieldId,
            legalTargetIds)
            ?? throw new InvalidOperationException(
                $"B0 source-same-location static aura driver could not find a legal ready defender: {DescribeState(current.State)}");
        var command = new DeclareBattleCommand(
            battlefieldId,
            [sourceObjectId],
            [defenderObjectId],
            OptionalCosts: ["COMBAT_ASSIGNMENT"]);
        var declared = await session.SubmitAsync(
            playerId,
            intentId,
            command,
            JsonSerializer.SerializeToElement(new
            {
                cmdType = CommandTypes.DeclareBattle,
                battlefieldId,
                attackerObjectIds = new[] { sourceObjectId },
                defenderObjectIds = new[] { defenderObjectId },
                optionalCosts = new[] { "COMBAT_ASSIGNMENT" }
            }),
            CancellationToken.None);
        AssertAccepted(declared);
        AssertNoHiddenZoneLeak(declared);

        var result = await PassOpenBattleResponseAsync(session, declared, $"{intentId}-battle-response");
        result = await ResolveOpenBattleDamageAssignmentsAsync(session, result, $"{intentId}-assign-damage");
        result = await PassOpenBattleResponseAsync(session, result, $"{intentId}-battle-response-after-assignment");
        AssertNoHiddenZoneLeak(result);
        return result;
    }

    private static async ValueTask<ResolutionResult> SubmitSameBattlefieldBoonCountStaticAuraDeclareBattleAsync(
        MatchSession session,
        ResolutionResult current,
        string auraControllerId,
        string intentId)
    {
        Assert.Equal(auraControllerId, current.State.ActivePlayerId);
        var playerId = current.State.ActivePlayerId;
        var opponentId = OpponentOf(current.State, playerId);
        var candidate = EnabledCandidate(current.Prompts[playerId], CommandTypes.DeclareBattle)
            ?? throw new InvalidOperationException($"B0 same-battlefield boon-count static aura driver could not find DECLARE_BATTLE for {playerId}.");
        var sourceObjectId = FindBattlefieldUnitByCardNo(
            current.State,
            playerId,
            SettSameBattlefieldBoonCountStaticAuraCardNo,
            readyOnly: true)
            ?? throw new InvalidOperationException("B0 same-battlefield boon-count static aura driver could not find a ready Sett.");
        var battlefieldId = current.State.ObjectLocations[sourceObjectId].BattlefieldObjectId
            ?? throw new InvalidOperationException("B0 same-battlefield boon-count static aura driver could not locate Sett's battlefield.");
        var boonParticipantObjectId = FindBattlefieldUnitByCardNo(
            current.State,
            playerId,
            DemaciaEnvoyCardNo,
            battlefieldId)
            ?? throw new InvalidOperationException("B0 same-battlefield boon-count static aura driver could not find a same-battlefield boon participant.");
        Assert.Contains(CardObjectTags.Boon, current.State.CardObjects[boonParticipantObjectId].Tags);

        var legalSourceIds = candidate.Sources?.Select(choice => choice.Id).ToHashSet(StringComparer.Ordinal)
            ?? [];
        var legalTargetIds = candidate.Targets?.Select(choice => choice.Id).ToHashSet(StringComparer.Ordinal)
            ?? [];
        var legalDestinationIds = candidate.Destinations?.Select(choice => choice.Id).ToHashSet(StringComparer.Ordinal)
            ?? [];
        Assert.Contains(sourceObjectId, legalSourceIds);
        Assert.Contains(battlefieldId, legalDestinationIds);

        var staticAura = Assert.Single(current.State.ContinuousEffects, effect =>
            string.Equals(effect.Layer, ContinuousEffectLayers.StaticAura, StringComparison.Ordinal)
            && string.Equals(effect.SourceObjectId, sourceObjectId, StringComparison.Ordinal)
            && string.Equals(effect.TargetObjectId, sourceObjectId, StringComparison.Ordinal));
        Assert.Equal(SettSameBattlefieldBoonCountStaticAuraCardNo, staticAura.SourceCardNo);
        Assert.Equal(StaticAuraKinds.SameBattlefieldFriendlyFilteredUnitCountToSourcePower, staticAura.EffectKind);
        Assert.Equal(1, staticAura.PowerDelta);
        Assert.Equal(5, staticAura.BasePower);
        Assert.Equal(6, staticAura.EffectivePower);
        Assert.Equal("CoreRuleEngine.ResolveSameBattlefieldFriendlyFilteredUnitCountToSourcePowerBonus", staticAura.SourcePath);
        Assert.Equal("SOURCE_AND_FRIENDLY_FILTERED_PUBLIC_UNITS_AT_SAME_BATTLEFIELD", staticAura.Condition);
        Assert.Equal("RECOMPUTED_FROM_CURRENT_SAME_BATTLEFIELD_FILTERED_FRIENDLY_UNIT_LOCATIONS", staticAura.Lifecycle);
        var participantObjectIds = Assert.IsAssignableFrom<IReadOnlyList<string>>(staticAura.ParticipantObjectIds);
        Assert.Contains(boonParticipantObjectId, participantObjectIds);
        Assert.DoesNotContain(sourceObjectId, participantObjectIds);
        var sourceDependencyObjectIds = Assert.IsAssignableFrom<IReadOnlyList<string>>(staticAura.SourceDependencyObjectIds);
        Assert.Contains(sourceObjectId, sourceDependencyObjectIds);
        var targetDependencyObjectIds = Assert.IsAssignableFrom<IReadOnlyList<string>>(staticAura.TargetDependencyObjectIds);
        Assert.Contains(sourceObjectId, targetDependencyObjectIds);
        var participantDependencyObjectIds = Assert.IsAssignableFrom<IReadOnlyList<string>>(staticAura.ParticipantDependencyObjectIds);
        Assert.Contains(boonParticipantObjectId, participantDependencyObjectIds);

        var defenderObjectId = FindReadyBattlefieldDefender(
            current.State,
            opponentId,
            battlefieldId,
            legalTargetIds)
            ?? throw new InvalidOperationException(
                $"B0 same-battlefield boon-count static aura driver could not find a legal ready defender: {DescribeState(current.State)}");
        var command = new DeclareBattleCommand(
            battlefieldId,
            [sourceObjectId],
            [defenderObjectId],
            OptionalCosts: ["COMBAT_ASSIGNMENT"]);
        var declared = await session.SubmitAsync(
            playerId,
            intentId,
            command,
            JsonSerializer.SerializeToElement(new
            {
                cmdType = CommandTypes.DeclareBattle,
                battlefieldId,
                attackerObjectIds = new[] { sourceObjectId },
                defenderObjectIds = new[] { defenderObjectId },
                optionalCosts = new[] { "COMBAT_ASSIGNMENT" }
            }),
            CancellationToken.None);
        AssertAccepted(declared);
        AssertNoHiddenZoneLeak(declared);

        var result = await PassOpenBattleResponseAsync(session, declared, $"{intentId}-battle-response");
        result = await ResolveOpenBattleDamageAssignmentsAsync(session, result, $"{intentId}-assign-damage");
        result = await PassOpenBattleResponseAsync(session, result, $"{intentId}-battle-response-after-assignment");
        AssertNoHiddenZoneLeak(result);
        return result;
    }

    private static async ValueTask<ResolutionResult> SubmitSameBattlefieldOtherFriendlyFilteredStaticAuraDeclareBattleAsync(
        MatchSession session,
        ResolutionResult current,
        string auraControllerId,
        string intentId)
    {
        Assert.Equal(auraControllerId, current.State.ActivePlayerId);
        var playerId = current.State.ActivePlayerId;
        var opponentId = OpponentOf(current.State, playerId);
        var candidate = EnabledCandidate(current.Prompts[playerId], CommandTypes.DeclareBattle)
            ?? throw new InvalidOperationException($"B0 same-battlefield other-friendly-filtered static aura driver could not find DECLARE_BATTLE for {playerId}.");
        var sourceObjectId = FindBattlefieldUnitByCardNo(
            current.State,
            playerId,
            LeeSinSameBattlefieldOtherFriendlyFilteredStaticAuraCardNo)
            ?? throw new InvalidOperationException("B0 same-battlefield other-friendly-filtered static aura driver could not find Lee Sin.");
        var battlefieldId = current.State.ObjectLocations[sourceObjectId].BattlefieldObjectId
            ?? throw new InvalidOperationException("B0 same-battlefield other-friendly-filtered static aura driver could not locate Lee Sin's battlefield.");
        var attackerObjectId = FindBattlefieldUnitByCardNo(
            current.State,
            playerId,
            DemaciaEnvoyCardNo,
            battlefieldId,
            readyOnly: true)
            ?? throw new InvalidOperationException("B0 same-battlefield other-friendly-filtered static aura driver could not find a ready boon attacker.");
        Assert.Contains(CardObjectTags.Boon, current.State.CardObjects[attackerObjectId].Tags);

        var legalSourceIds = candidate.Sources?.Select(choice => choice.Id).ToHashSet(StringComparer.Ordinal)
            ?? [];
        var legalTargetIds = candidate.Targets?.Select(choice => choice.Id).ToHashSet(StringComparer.Ordinal)
            ?? [];
        var legalDestinationIds = candidate.Destinations?.Select(choice => choice.Id).ToHashSet(StringComparer.Ordinal)
            ?? [];
        Assert.Contains(attackerObjectId, legalSourceIds);
        Assert.Contains(battlefieldId, legalDestinationIds);

        var staticAura = Assert.Single(current.State.ContinuousEffects, effect =>
            string.Equals(effect.Layer, ContinuousEffectLayers.StaticAura, StringComparison.Ordinal)
            && string.Equals(effect.SourceObjectId, sourceObjectId, StringComparison.Ordinal)
            && string.Equals(effect.TargetObjectId, attackerObjectId, StringComparison.Ordinal));
        Assert.Equal(LeeSinSameBattlefieldOtherFriendlyFilteredStaticAuraCardNo, staticAura.SourceCardNo);
        Assert.Equal(StaticAuraKinds.SameBattlefieldOtherFriendlyFilteredUnitsPower, staticAura.EffectKind);
        Assert.Equal(2, staticAura.PowerDelta);
        Assert.Equal(3, staticAura.BasePower);
        Assert.Equal(5, staticAura.EffectivePower);
        Assert.Equal("CoreRuleEngine.ResolveSameBattlefieldOtherFriendlyFilteredUnitsPowerBonus", staticAura.SourcePath);
        Assert.Equal("SOURCE_AND_OTHER_FRIENDLY_FILTERED_PUBLIC_UNITS_AT_SAME_BATTLEFIELD", staticAura.Condition);
        Assert.Equal("DERIVED_FROM_CURRENT_SAME_BATTLEFIELD_FILTERED_FRIENDLY_UNIT_LOCATIONS", staticAura.Lifecycle);
        var participantObjectIds = Assert.IsAssignableFrom<IReadOnlyList<string>>(staticAura.ParticipantObjectIds);
        Assert.Contains(attackerObjectId, participantObjectIds);
        Assert.DoesNotContain(sourceObjectId, participantObjectIds);
        var sourceDependencyObjectIds = Assert.IsAssignableFrom<IReadOnlyList<string>>(staticAura.SourceDependencyObjectIds);
        Assert.Contains(sourceObjectId, sourceDependencyObjectIds);
        var targetDependencyObjectIds = Assert.IsAssignableFrom<IReadOnlyList<string>>(staticAura.TargetDependencyObjectIds);
        Assert.Contains(attackerObjectId, targetDependencyObjectIds);
        var participantDependencyObjectIds = Assert.IsAssignableFrom<IReadOnlyList<string>>(staticAura.ParticipantDependencyObjectIds);
        Assert.Contains(attackerObjectId, participantDependencyObjectIds);

        var defenderObjectId = FindReadyBattlefieldDefender(
            current.State,
            opponentId,
            battlefieldId,
            legalTargetIds)
            ?? throw new InvalidOperationException(
                $"B0 same-battlefield other-friendly-filtered static aura driver could not find a legal ready defender: {DescribeState(current.State)}");
        var command = new DeclareBattleCommand(
            battlefieldId,
            [attackerObjectId],
            [defenderObjectId],
            OptionalCosts: ["COMBAT_ASSIGNMENT"]);
        var declared = await session.SubmitAsync(
            playerId,
            intentId,
            command,
            JsonSerializer.SerializeToElement(new
            {
                cmdType = CommandTypes.DeclareBattle,
                battlefieldId,
                attackerObjectIds = new[] { attackerObjectId },
                defenderObjectIds = new[] { defenderObjectId },
                optionalCosts = new[] { "COMBAT_ASSIGNMENT" }
            }),
            CancellationToken.None);
        AssertAccepted(declared);
        AssertNoHiddenZoneLeak(declared);

        var result = await PassOpenBattleResponseAsync(session, declared, $"{intentId}-battle-response");
        result = await ResolveOpenBattleDamageAssignmentsAsync(session, result, $"{intentId}-assign-damage");
        result = await PassOpenBattleResponseAsync(session, result, $"{intentId}-battle-response-after-assignment");
        AssertNoHiddenZoneLeak(result);
        return result;
    }

    private static async ValueTask<ResolutionResult> SubmitOtherFriendlyStaticAuraDeclareBattleAsync(
        MatchSession session,
        ResolutionResult current,
        string auraControllerId,
        string intentId)
    {
        Assert.Equal(auraControllerId, current.State.ActivePlayerId);
        var playerId = current.State.ActivePlayerId;
        var opponentId = OpponentOf(current.State, playerId);
        var candidate = EnabledCandidate(current.Prompts[playerId], CommandTypes.DeclareBattle)
            ?? throw new InvalidOperationException($"B0 other-friendly static aura driver could not find DECLARE_BATTLE for {playerId}.");
        var auraSourceObjectId = FindBaseUnitByCardNo(
            current.State,
            playerId,
            BaronNashorOtherFriendlyStaticAuraCardNo)
            ?? throw new InvalidOperationException("B0 other-friendly static aura driver could not find Baron Nashor source.");
        var attackerObjectId = FindBattlefieldUnitByCardNo(
            current.State,
            playerId,
            WildclawBeastmasterCardNo,
            readyOnly: true)
            ?? throw new InvalidOperationException("B0 other-friendly static aura driver could not find a ready boosted Wildclaw Beastmaster.");
        var battlefieldId = current.State.ObjectLocations[attackerObjectId].BattlefieldObjectId
            ?? throw new InvalidOperationException("B0 other-friendly static aura driver could not locate Wildclaw Beastmaster's battlefield.");
        var legalSourceIds = candidate.Sources?.Select(choice => choice.Id).ToHashSet(StringComparer.Ordinal)
            ?? [];
        var legalTargetIds = candidate.Targets?.Select(choice => choice.Id).ToHashSet(StringComparer.Ordinal)
            ?? [];
        var legalDestinationIds = candidate.Destinations?.Select(choice => choice.Id).ToHashSet(StringComparer.Ordinal)
            ?? [];
        Assert.Contains(attackerObjectId, legalSourceIds);
        Assert.Contains(battlefieldId, legalDestinationIds);

        var staticAura = Assert.Single(current.State.ContinuousEffects, effect =>
            string.Equals(effect.Layer, ContinuousEffectLayers.StaticAura, StringComparison.Ordinal)
            && string.Equals(effect.SourceObjectId, auraSourceObjectId, StringComparison.Ordinal)
            && string.Equals(effect.TargetObjectId, attackerObjectId, StringComparison.Ordinal));
        Assert.Equal(BaronNashorOtherFriendlyStaticAuraCardNo, staticAura.SourceCardNo);
        Assert.Equal(StaticAuraKinds.OtherFriendlyUnitsPower, staticAura.EffectKind);
        Assert.Equal(2, staticAura.PowerDelta);
        Assert.Equal("CoreRuleEngine.ResolveOtherFriendlyUnitsPowerBonus", staticAura.SourcePath);
        Assert.DoesNotContain(
            current.State.ContinuousEffects,
            effect => string.Equals(effect.SourceObjectId, auraSourceObjectId, StringComparison.Ordinal)
                && string.Equals(effect.TargetObjectId, auraSourceObjectId, StringComparison.Ordinal));

        var defenderObjectId = FindReadyBattlefieldDefender(
            current.State,
            opponentId,
            battlefieldId,
            legalTargetIds)
            ?? throw new InvalidOperationException(
                $"B0 other-friendly static aura driver could not find a legal ready defender: {DescribeState(current.State)}");
        var command = new DeclareBattleCommand(
            battlefieldId,
            [attackerObjectId],
            [defenderObjectId],
            OptionalCosts: ["COMBAT_ASSIGNMENT"]);
        var declared = await session.SubmitAsync(
            playerId,
            intentId,
            command,
            JsonSerializer.SerializeToElement(new
            {
                cmdType = CommandTypes.DeclareBattle,
                battlefieldId,
                attackerObjectIds = new[] { attackerObjectId },
                defenderObjectIds = new[] { defenderObjectId },
                optionalCosts = new[] { "COMBAT_ASSIGNMENT" }
            }),
            CancellationToken.None);
        AssertAccepted(declared);
        AssertNoHiddenZoneLeak(declared);

        var result = await PassOpenBattleResponseAsync(session, declared, $"{intentId}-battle-response");
        result = await ResolveOpenBattleDamageAssignmentsAsync(session, result, $"{intentId}-assign-damage");
        result = await PassOpenBattleResponseAsync(session, result, $"{intentId}-battle-response-after-assignment");
        AssertNoHiddenZoneLeak(result);
        return result;
    }

    private static async ValueTask<ResolutionResult> SubmitSourceCombatStaticAuraDeclareBattleAsync(
        MatchSession session,
        ResolutionResult current,
        string auraControllerId,
        string intentId)
    {
        Assert.Equal(auraControllerId, current.State.ActivePlayerId);
        var playerId = current.State.ActivePlayerId;
        var opponentId = OpponentOf(current.State, playerId);
        var candidate = EnabledCandidate(current.Prompts[playerId], CommandTypes.DeclareBattle)
            ?? throw new InvalidOperationException($"B0 source-combat static aura driver could not find DECLARE_BATTLE for {playerId}.");
        var sourceObjectId = FindBattlefieldUnitByCardNo(
            current.State,
            playerId,
            ScarletPigeonSourceCombatStaticAuraCardNo,
            readyOnly: true)
            ?? throw new InvalidOperationException("B0 source-combat static aura driver could not find a ready Scarlet Pigeon.");
        var battlefieldId = current.State.ObjectLocations[sourceObjectId].BattlefieldObjectId
            ?? throw new InvalidOperationException("B0 source-combat static aura driver could not locate Scarlet Pigeon's battlefield.");
        var allyObjectId = FindBattlefieldUnitByCardNo(
            current.State,
            playerId,
            DemaciaEnvoyCardNo,
            battlefieldId,
            readyOnly: true)
            ?? throw new InvalidOperationException("B0 source-combat static aura driver could not find a ready joint attacker.");
        var legalSourceIds = candidate.Sources?.Select(choice => choice.Id).ToHashSet(StringComparer.Ordinal)
            ?? [];
        var legalTargetIds = candidate.Targets?.Select(choice => choice.Id).ToHashSet(StringComparer.Ordinal)
            ?? [];
        var legalDestinationIds = candidate.Destinations?.Select(choice => choice.Id).ToHashSet(StringComparer.Ordinal)
            ?? [];
        Assert.Contains(sourceObjectId, legalSourceIds);
        Assert.Contains(allyObjectId, legalSourceIds);
        Assert.Contains(battlefieldId, legalDestinationIds);

        var defenderObjectId = FindReadyBattlefieldDefender(
            current.State,
            opponentId,
            battlefieldId,
            legalTargetIds)
            ?? throw new InvalidOperationException(
                $"B0 source-combat static aura driver could not find a legal ready defender: {DescribeState(current.State)}");
        var command = new DeclareBattleCommand(
            battlefieldId,
            [sourceObjectId, allyObjectId],
            [defenderObjectId],
            OptionalCosts: ["COMBAT_ASSIGNMENT"]);
        var declared = await session.SubmitAsync(
            playerId,
            intentId,
            command,
            JsonSerializer.SerializeToElement(new
            {
                cmdType = CommandTypes.DeclareBattle,
                battlefieldId,
                attackerObjectIds = new[] { sourceObjectId, allyObjectId },
                defenderObjectIds = new[] { defenderObjectId },
                optionalCosts = new[] { "COMBAT_ASSIGNMENT" }
            }),
            CancellationToken.None);
        AssertAccepted(declared);
        AssertNoHiddenZoneLeak(declared);

        var battleDeclared = Assert.Single(declared.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLE_DECLARED", StringComparison.Ordinal));
        Assert.Equal([sourceObjectId, allyObjectId], Assert.IsType<string[]>(battleDeclared.Payload["attackerObjectIds"]));
        Assert.Equal([defenderObjectId], Assert.IsType<string[]>(battleDeclared.Payload["defenderObjectIds"]));
        if (declared.State.BattleState.IsActive)
        {
            var staticAura = Assert.Single(declared.State.ContinuousEffects, effect =>
                string.Equals(effect.Layer, ContinuousEffectLayers.StaticAura, StringComparison.Ordinal)
                && string.Equals(effect.SourceObjectId, sourceObjectId, StringComparison.Ordinal)
                && string.Equals(effect.TargetObjectId, sourceObjectId, StringComparison.Ordinal));
            Assert.Equal(ScarletPigeonSourceCombatStaticAuraCardNo, staticAura.SourceCardNo);
            Assert.Equal(StaticAuraKinds.SourceAttackingWithAnotherUnitPower, staticAura.EffectKind);
            Assert.Equal(2, staticAura.PowerDelta);
            Assert.Equal(3, staticAura.BasePower);
            Assert.Equal(5, staticAura.EffectivePower);
            Assert.Equal("CoreRuleEngine.ResolveSourceAttackingWithAnotherUnitPowerBonus", staticAura.SourcePath);
            Assert.Equal("SOURCE_ATTACKING_WITH_REQUIRED_ATTACKER_COUNT", staticAura.Condition);
            Assert.Equal("RECOMPUTED_FROM_CURRENT_BATTLE_ATTACKER_LOCATIONS", staticAura.Lifecycle);
            var participantObjectIds = Assert.IsAssignableFrom<IReadOnlyList<string>>(staticAura.ParticipantObjectIds);
            Assert.Contains(sourceObjectId, participantObjectIds);
            Assert.Contains(allyObjectId, participantObjectIds);
        }

        var result = await PassOpenBattleResponseAsync(session, declared, $"{intentId}-battle-response");
        result = await ResolveOpenBattleDamageAssignmentsAsync(session, result, $"{intentId}-assign-damage");
        result = await PassOpenBattleResponseAsync(session, result, $"{intentId}-battle-response-after-assignment");
        AssertNoHiddenZoneLeak(result);
        return result;
    }

    private static async ValueTask<ResolutionResult> SubmitSameBattlefieldStaticKeywordDeclareBattleAsync(
        MatchSession session,
        ResolutionResult current,
        string auraControllerId,
        string intentId)
    {
        Assert.Equal(auraControllerId, current.State.ActivePlayerId);
        var playerId = current.State.ActivePlayerId;
        var opponentId = OpponentOf(current.State, playerId);
        var candidate = EnabledCandidate(current.Prompts[playerId], CommandTypes.DeclareBattle)
            ?? throw new InvalidOperationException($"B0 same-battlefield static keyword driver could not find DECLARE_BATTLE for {playerId}.");
        var auraSourceObjectId = FindBattlefieldUnitByCardNo(
            current.State,
            playerId,
            FarronCaptainSameBattlefieldStaticKeywordCardNo)
            ?? throw new InvalidOperationException("B0 same-battlefield static keyword driver could not find Farron source.");
        var battlefieldId = current.State.ObjectLocations[auraSourceObjectId].BattlefieldObjectId
            ?? throw new InvalidOperationException("B0 same-battlefield static keyword driver could not locate Farron's battlefield.");
        var attackerObjectId = FindBattlefieldUnitByCardNo(
            current.State,
            playerId,
            AscendedBelieverCardNo,
            battlefieldId,
            readyOnly: true)
            ?? throw new InvalidOperationException("B0 same-battlefield static keyword driver could not find a ready granted-keyword ally.");
        var legalSourceIds = candidate.Sources?.Select(choice => choice.Id).ToHashSet(StringComparer.Ordinal)
            ?? [];
        var legalTargetIds = candidate.Targets?.Select(choice => choice.Id).ToHashSet(StringComparer.Ordinal)
            ?? [];
        var legalDestinationIds = candidate.Destinations?.Select(choice => choice.Id).ToHashSet(StringComparer.Ordinal)
            ?? [];
        Assert.Contains(attackerObjectId, legalSourceIds);
        Assert.Contains(battlefieldId, legalDestinationIds);

        var ruleTextAura = Assert.Single(current.State.ContinuousEffects, effect =>
            string.Equals(effect.Layer, ContinuousEffectLayers.RuleText, StringComparison.Ordinal)
            && string.Equals(effect.SourceObjectId, auraSourceObjectId, StringComparison.Ordinal)
            && string.Equals(effect.TargetObjectId, attackerObjectId, StringComparison.Ordinal));
        Assert.StartsWith("RULE_TEXT:SAME_BATTLEFIELD_OTHER_FRIENDLY_UNITS_KEYWORD:", ruleTextAura.EffectId, StringComparison.Ordinal);
        Assert.EndsWith($":{CardCombatKeywordNames.Assault}", ruleTextAura.EffectId, StringComparison.Ordinal);
        Assert.Equal("OBJECT", ruleTextAura.Scope);
        Assert.Equal("WHILE_SOURCE_AND_TARGET_AT_SAME_BATTLEFIELD", ruleTextAura.Duration);
        Assert.DoesNotContain(
            current.State.ContinuousEffects,
            effect => string.Equals(effect.SourceObjectId, auraSourceObjectId, StringComparison.Ordinal)
                && string.Equals(effect.TargetObjectId, auraSourceObjectId, StringComparison.Ordinal));

        var defenderObjectId = FindReadyBattlefieldDefender(
            current.State,
            opponentId,
            battlefieldId,
            legalTargetIds)
            ?? throw new InvalidOperationException(
                $"B0 same-battlefield static keyword driver could not find a legal ready defender: {DescribeState(current.State)}");
        var command = new DeclareBattleCommand(
            battlefieldId,
            [attackerObjectId],
            [defenderObjectId],
            OptionalCosts: ["COMBAT_ASSIGNMENT"]);
        var declared = await session.SubmitAsync(
            playerId,
            intentId,
            command,
            JsonSerializer.SerializeToElement(new
            {
                cmdType = CommandTypes.DeclareBattle,
                battlefieldId,
                attackerObjectIds = new[] { attackerObjectId },
                defenderObjectIds = new[] { defenderObjectId },
                optionalCosts = new[] { "COMBAT_ASSIGNMENT" }
            }),
            CancellationToken.None);
        AssertAccepted(declared);
        AssertNoHiddenZoneLeak(declared);

        var result = await PassOpenBattleResponseAsync(session, declared, $"{intentId}-battle-response");
        result = await ResolveOpenBattleDamageAssignmentsAsync(session, result, $"{intentId}-assign-damage");
        result = await PassOpenBattleResponseAsync(session, result, $"{intentId}-battle-response-after-assignment");
        AssertNoHiddenZoneLeak(result);
        return result;
    }

    private static async ValueTask<ResolutionResult> SubmitSameBattlefieldSteadfastDeclareBattleAsync(
        MatchSession session,
        ResolutionResult current,
        string attackingPlayerId,
        string defendingPlayerId,
        string intentId)
    {
        Assert.Equal(attackingPlayerId, current.State.ActivePlayerId);
        var candidate = EnabledCandidate(current.Prompts[attackingPlayerId], CommandTypes.DeclareBattle)
            ?? throw new InvalidOperationException($"B0 same-battlefield steadfast driver could not find DECLARE_BATTLE for {attackingPlayerId}.");
        var auraSourceObjectId = FindBattlefieldUnitByCardNo(
            current.State,
            defendingPlayerId,
            TaricSameBattlefieldStaticKeywordCardNo)
            ?? throw new InvalidOperationException("B0 same-battlefield steadfast driver could not find Taric source.");
        var battlefieldId = current.State.ObjectLocations[auraSourceObjectId].BattlefieldObjectId
            ?? throw new InvalidOperationException("B0 same-battlefield steadfast driver could not locate Taric's battlefield.");
        var defenderObjectId = FindBattlefieldUnitByCardNo(
            current.State,
            defendingPlayerId,
            LeblancCardNo,
            battlefieldId,
            readyOnly: true)
            ?? throw new InvalidOperationException("B0 same-battlefield steadfast driver could not find a ready granted-keyword defender.");
        var legalSourceIds = candidate.Sources?.Select(choice => choice.Id).ToHashSet(StringComparer.Ordinal)
            ?? [];
        var legalTargetIds = candidate.Targets?.Select(choice => choice.Id).ToHashSet(StringComparer.Ordinal)
            ?? [];
        var legalDestinationIds = candidate.Destinations?.Select(choice => choice.Id).ToHashSet(StringComparer.Ordinal)
            ?? [];
        Assert.Contains(defenderObjectId, legalTargetIds);
        Assert.Contains(battlefieldId, legalDestinationIds);

        var ruleTextAura = Assert.Single(current.State.ContinuousEffects, effect =>
            string.Equals(effect.Layer, ContinuousEffectLayers.RuleText, StringComparison.Ordinal)
            && string.Equals(effect.SourceObjectId, auraSourceObjectId, StringComparison.Ordinal)
            && string.Equals(effect.TargetObjectId, defenderObjectId, StringComparison.Ordinal));
        Assert.StartsWith("RULE_TEXT:SAME_BATTLEFIELD_OTHER_FRIENDLY_UNITS_KEYWORD:", ruleTextAura.EffectId, StringComparison.Ordinal);
        Assert.EndsWith($":{CardCombatKeywordNames.Steadfast}", ruleTextAura.EffectId, StringComparison.Ordinal);
        Assert.Equal("OBJECT", ruleTextAura.Scope);
        Assert.Equal("WHILE_SOURCE_AND_TARGET_AT_SAME_BATTLEFIELD", ruleTextAura.Duration);
        Assert.DoesNotContain(
            current.State.ContinuousEffects,
            effect => string.Equals(effect.SourceObjectId, auraSourceObjectId, StringComparison.Ordinal)
                && string.Equals(effect.TargetObjectId, auraSourceObjectId, StringComparison.Ordinal));

        var attackerObjectId = legalSourceIds
            .Where(objectId => IsReadyUnit(current.State, objectId))
            .Where(objectId => current.State.ObjectLocations.TryGetValue(objectId, out var location)
                && string.Equals(location.Zone, "BATTLEFIELD", StringComparison.Ordinal)
                && string.Equals(location.BattlefieldObjectId, battlefieldId, StringComparison.Ordinal))
            .OrderBy(objectId => current.State.CardObjects[objectId].Power)
            .ThenBy(objectId => current.State.CardObjects[objectId].CardNo, StringComparer.Ordinal)
            .ThenBy(objectId => objectId, StringComparer.Ordinal)
            .FirstOrDefault()
            ?? throw new InvalidOperationException(
                $"B0 same-battlefield steadfast driver could not find a legal ready attacker: {DescribeState(current.State)}");
        var command = new DeclareBattleCommand(
            battlefieldId,
            [attackerObjectId],
            [defenderObjectId],
            OptionalCosts: ["COMBAT_ASSIGNMENT"]);
        var declared = await session.SubmitAsync(
            attackingPlayerId,
            intentId,
            command,
            JsonSerializer.SerializeToElement(new
            {
                cmdType = CommandTypes.DeclareBattle,
                battlefieldId,
                attackerObjectIds = new[] { attackerObjectId },
                defenderObjectIds = new[] { defenderObjectId },
                optionalCosts = new[] { "COMBAT_ASSIGNMENT" }
            }),
            CancellationToken.None);
        AssertAccepted(declared);
        AssertNoHiddenZoneLeak(declared);

        var result = await PassOpenBattleResponseAsync(session, declared, $"{intentId}-battle-response");
        result = await ResolveOpenBattleDamageAssignmentsAsync(session, result, $"{intentId}-assign-damage");
        result = await PassOpenBattleResponseAsync(session, result, $"{intentId}-battle-response-after-assignment");
        AssertNoHiddenZoneLeak(result);
        return result;
    }

    private static async ValueTask<(ResolutionResult AssignmentOpened, ResolutionResult BattleResult)> SubmitTaricBulwarkDamageAssignmentBattleAsync(
        MatchSession session,
        ResolutionResult current,
        string attackingPlayerId,
        string defendingPlayerId,
        string intentId)
    {
        Assert.Equal(attackingPlayerId, current.State.ActivePlayerId);
        var candidate = EnabledCandidate(current.Prompts[attackingPlayerId], CommandTypes.DeclareBattle)
            ?? throw new InvalidOperationException($"B0 Taric bulwark assignment driver could not find DECLARE_BATTLE for {attackingPlayerId}.");
        var taricObjectId = FindBattlefieldUnitByCardNo(
            current.State,
            defendingPlayerId,
            TaricSameBattlefieldStaticKeywordCardNo,
            readyOnly: true)
            ?? throw new InvalidOperationException("B0 Taric bulwark assignment driver could not find ready Taric.");
        var battlefieldId = current.State.ObjectLocations[taricObjectId].BattlefieldObjectId
            ?? throw new InvalidOperationException("B0 Taric bulwark assignment driver could not locate Taric's battlefield.");
        var leblancObjectId = FindBattlefieldUnitByCardNo(
            current.State,
            defendingPlayerId,
            LeblancCardNo,
            battlefieldId,
            readyOnly: true)
            ?? throw new InvalidOperationException("B0 Taric bulwark assignment driver could not find ready LeBlanc.");
        var attackerObjectId = FindBattlefieldUnitByCardNo(
            current.State,
            attackingPlayerId,
            WildclawBeastmasterCardNo,
            battlefieldId,
            readyOnly: true)
            ?? throw new InvalidOperationException("B0 Taric bulwark assignment driver could not find ready Wildclaw Beastmaster attacker.");
        var legalSourceIds = candidate.Sources?.Select(choice => choice.Id).ToHashSet(StringComparer.Ordinal)
            ?? [];
        var legalTargetIds = candidate.Targets?.Select(choice => choice.Id).ToHashSet(StringComparer.Ordinal)
            ?? [];
        var legalDestinationIds = candidate.Destinations?.Select(choice => choice.Id).ToHashSet(StringComparer.Ordinal)
            ?? [];
        Assert.Contains(attackerObjectId, legalSourceIds);
        Assert.Contains(taricObjectId, legalTargetIds);
        Assert.Contains(leblancObjectId, legalTargetIds);
        Assert.Contains(battlefieldId, legalDestinationIds);

        var ruleTextAura = Assert.Single(current.State.ContinuousEffects, effect =>
            string.Equals(effect.Layer, ContinuousEffectLayers.RuleText, StringComparison.Ordinal)
            && string.Equals(effect.SourceObjectId, taricObjectId, StringComparison.Ordinal)
            && string.Equals(effect.TargetObjectId, leblancObjectId, StringComparison.Ordinal));
        Assert.EndsWith($":{CardCombatKeywordNames.Steadfast}", ruleTextAura.EffectId, StringComparison.Ordinal);

        var command = new DeclareBattleCommand(
            battlefieldId,
            [attackerObjectId],
            [leblancObjectId, taricObjectId],
            OptionalCosts: ["COMBAT_ASSIGNMENT"]);
        var declared = await session.SubmitAsync(
            attackingPlayerId,
            intentId,
            command,
            JsonSerializer.SerializeToElement(new
            {
                cmdType = CommandTypes.DeclareBattle,
                battlefieldId,
                attackerObjectIds = new[] { attackerObjectId },
                defenderObjectIds = new[] { leblancObjectId, taricObjectId },
                optionalCosts = new[] { "COMBAT_ASSIGNMENT" }
            }),
            CancellationToken.None);
        AssertAccepted(declared);
        AssertNoHiddenZoneLeak(declared);

        var assignmentOpened = await PassOpenBattleResponseAsync(session, declared, $"{intentId}-battle-response");
        Assert.Equal(PromptTypes.AssignCombatDamage, assignmentOpened.Prompts[attackingPlayerId].View?.Type);
        var battleResult = await ResolveOpenBattleDamageAssignmentsAsync(session, assignmentOpened, $"{intentId}-assign-damage");
        battleResult = await PassOpenBattleResponseAsync(session, battleResult, $"{intentId}-battle-response-after-assignment");
        AssertNoHiddenZoneLeak(battleResult);
        return (assignmentOpened, battleResult);
    }

    private static async ValueTask<ResolutionResult> TapAllAvailableRunesAsync(
        MatchSession session,
        string playerId,
        ResolutionResult current,
        string intentPrefix)
    {
        var result = current;
        for (var index = 0; index < 20; index++)
        {
            var prompt = result.Prompts[playerId];
            var candidate = EnabledCandidate(prompt, CommandTypes.TapRune);
            var sourceObjectId = candidate?.Sources?.FirstOrDefault()?.Id;
            if (sourceObjectId is null)
            {
                return result;
            }

            result = await session.SubmitAsync(
                playerId,
                $"{intentPrefix}-{index}",
                new TapRuneCommand(sourceObjectId),
                RawCommand(new TapRuneCommand(sourceObjectId)),
                CancellationToken.None);
            AssertAccepted(result);
            AssertNoHiddenZoneLeak(result);
        }

        throw new InvalidOperationException("B0 auto-driver exceeded tap-rune guard.");
    }

    private static async ValueTask<ResolutionResult> TryPlayFirstUnitAsync(
        MatchSession session,
        string playerId,
        ResolutionResult current,
        string intentPrefix,
        bool playUnitToBattlefield)
    {
        var playCandidate = EnabledCandidate(current.Prompts[playerId], CommandTypes.PlayCard);
        if (playCandidate?.Sources is not { Count: > 0 } sources)
        {
            throw new InvalidOperationException($"B0 auto-driver could not find an enabled PLAY_CARD source for {playerId}.");
        }

        var destination = playUnitToBattlefield
            ? BattlefieldDestinationFor(current.State, playerId)
            : playCandidate.Destinations?.FirstOrDefault(choice => string.Equals(choice.Id, "BASE", StringComparison.Ordinal))?.Id ?? "BASE";
        for (var index = 0; index < sources.Count; index++)
        {
            var sourceObjectId = sources[index].Id;
            if (!current.State.CardObjects.TryGetValue(sourceObjectId, out var cardObject)
                || string.IsNullOrWhiteSpace(cardObject.CardNo)
                || IsDriverStandbyUnit(cardObject))
            {
                continue;
            }

            var attempted = await session.SubmitAsync(
                playerId,
                $"{intentPrefix}-attempt-{index}",
                new PlayCardCommand(sourceObjectId, cardObject.CardNo, [], Destination: destination),
                RawCommand(new PlayCardCommand(sourceObjectId, cardObject.CardNo, [], Destination: destination)),
                CancellationToken.None);
            if (!attempted.Accepted)
            {
                continue;
            }

            AssertNoHiddenZoneLeak(attempted);
            return await ResolveStackPassPassAsync(session, attempted, $"{intentPrefix}-resolve-{index}");
        }

        throw new InvalidOperationException($"B0 auto-driver could not play any exposed PLAY_CARD source for {playerId}.");
    }

    private static async ValueTask<ResolutionResult> ResolveStackPassPassAsync(
        MatchSession session,
        ResolutionResult current,
        string intentPrefix)
    {
        var result = current;
        for (var index = 0; index < 20; index++)
        {
            if (result.State.StackItems.Count == 0 && string.IsNullOrWhiteSpace(result.State.PriorityPlayerId))
            {
                return result;
            }

            var priorityPlayerId = result.State.PriorityPlayerId;
            if (string.IsNullOrWhiteSpace(priorityPlayerId))
            {
                throw new InvalidOperationException("B0 auto-driver found stack items without a priority player.");
            }

            result = await session.SubmitAsync(
                priorityPlayerId,
                $"{intentPrefix}-pass-priority-{index}",
                new PassPriorityCommand(),
                RawCommand(new PassPriorityCommand()),
                CancellationToken.None);
            AssertAccepted(result);
            AssertNoHiddenZoneLeak(result);
        }

        throw new InvalidOperationException("B0 auto-driver exceeded stack pass guard.");
    }

    private static async ValueTask<ResolutionResult> PassPriorityUntilAsync(
        MatchSession session,
        ResolutionResult current,
        string playerId,
        string intentPrefix)
    {
        var result = current;
        for (var index = 0; index < 4; index++)
        {
            if (string.Equals(result.State.PriorityPlayerId, playerId, StringComparison.Ordinal))
            {
                return result;
            }

            var priorityPlayerId = result.State.PriorityPlayerId;
            if (string.IsNullOrWhiteSpace(priorityPlayerId) || result.State.StackItems.Count == 0)
            {
                throw new InvalidOperationException($"B0 standby-reaction driver could not pass priority to {playerId}.");
            }

            var stackCountBefore = result.State.StackItems.Count;
            result = await session.SubmitAsync(
                priorityPlayerId,
                $"{intentPrefix}-pass-priority-{index}",
                new PassPriorityCommand(),
                RawCommand(new PassPriorityCommand()),
                CancellationToken.None);
            AssertAccepted(result);
            AssertNoHiddenZoneLeak(result);
            if (result.State.StackItems.Count < stackCountBefore
                && !string.Equals(result.State.PriorityPlayerId, playerId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"B0 standby-reaction driver resolved the stack before {playerId} received priority.");
            }
        }

        throw new InvalidOperationException($"B0 standby-reaction driver exceeded priority pass guard for {playerId}.");
    }

    private static async ValueTask<ResolutionResult> ResolveOneStackItemPassPassAsync(
        MatchSession session,
        ResolutionResult current,
        string intentPrefix)
    {
        var result = current;
        var stackCountBefore = result.State.StackItems.Count;
        if (stackCountBefore == 0)
        {
            throw new InvalidOperationException("B0 standby-reaction driver expected a stack item to resolve.");
        }

        for (var index = 0; index < 6; index++)
        {
            var priorityPlayerId = result.State.PriorityPlayerId;
            if (string.IsNullOrWhiteSpace(priorityPlayerId))
            {
                throw new InvalidOperationException("B0 standby-reaction driver found stack items without a priority player.");
            }

            result = await session.SubmitAsync(
                priorityPlayerId,
                $"{intentPrefix}-pass-priority-{index}",
                new PassPriorityCommand(),
                RawCommand(new PassPriorityCommand()),
                CancellationToken.None);
            AssertAccepted(result);
            AssertNoHiddenZoneLeak(result);
            if (result.State.StackItems.Count < stackCountBefore)
            {
                return result;
            }
        }

        throw new InvalidOperationException("B0 standby-reaction driver exceeded single stack item pass guard.");
    }

    private static async ValueTask<ResolutionResult> ResolveCurrentStackOnlyAsync(
        MatchSession session,
        ResolutionResult current,
        string intentPrefix)
    {
        var result = current;
        for (var index = 0; index < 20; index++)
        {
            if (result.State.StackItems.Count == 0)
            {
                return result;
            }

            var priorityPlayerId = result.State.PriorityPlayerId;
            if (string.IsNullOrWhiteSpace(priorityPlayerId))
            {
                throw new InvalidOperationException("B0 auto-driver found stack items without a priority player.");
            }

            result = await session.SubmitAsync(
                priorityPlayerId,
                $"{intentPrefix}-pass-priority-{index}",
                new PassPriorityCommand(),
                RawCommand(new PassPriorityCommand()),
                CancellationToken.None);
            AssertAccepted(result);
            AssertNoHiddenZoneLeak(result);
        }

        throw new InvalidOperationException("B0 auto-driver exceeded current stack pass guard.");
    }

    private static string BattlefieldDestinationFor(MatchState state, string playerId)
    {
        var battlefieldObjectId = state.PlayerZones[playerId].Battlefields
            .FirstOrDefault(objectId => state.CardObjects.TryGetValue(objectId, out var cardObject)
                && cardObject.Tags.Contains("CARD_TYPE:BATTLEFIELD", StringComparer.Ordinal))
            ?? throw new InvalidOperationException($"B0 auto-driver could not find a battlefield card for {playerId}.");
        return $"BATTLEFIELD:{battlefieldObjectId}";
    }

    private static string BattlefieldDestinationForCardNo(MatchState state, string playerId, string battlefieldCardNo)
    {
        var battlefieldObjectId = state.PlayerZones[playerId].Battlefields
            .FirstOrDefault(objectId => state.CardObjects.TryGetValue(objectId, out var cardObject)
                && string.Equals(cardObject.CardNo, battlefieldCardNo, StringComparison.Ordinal)
                && cardObject.Tags.Contains("CARD_TYPE:BATTLEFIELD", StringComparer.Ordinal))
            ?? throw new InvalidOperationException($"B0 auto-driver could not find battlefield {battlefieldCardNo} for {playerId}.");
        return $"BATTLEFIELD:{battlefieldObjectId}";
    }

    private static bool IsObjectLocatedAtBattlefield(MatchState state, string objectId, string battlefieldDestination)
    {
        var normalizedBattlefieldObjectId = battlefieldDestination.StartsWith("BATTLEFIELD:", StringComparison.Ordinal)
            ? battlefieldDestination["BATTLEFIELD:".Length..]
            : battlefieldDestination;
        return state.ObjectLocations.TryGetValue(objectId, out var location)
            && string.Equals(location.Zone, "BATTLEFIELD", StringComparison.Ordinal)
            && string.Equals(location.BattlefieldObjectId, normalizedBattlefieldObjectId, StringComparison.Ordinal);
    }

    private static async ValueTask<ResolutionResult> EndTurnAsync(
        MatchSession session,
        string playerId,
        string intentId)
    {
        var result = await session.SubmitAsync(
            playerId,
            intentId,
            new EndTurnCommand(),
            RawCommand(new EndTurnCommand()),
            CancellationToken.None);
        AssertAccepted(result);
        return result;
    }

    private static async ValueTask<ResolutionResult> MoveBaseUnitToOpponentBattlefieldAsync(
        MatchSession session,
        string playerId,
        ResolutionResult current)
    {
        var opponentId = OpponentOf(current.State, playerId);
        var opponentBattlefieldObjectId = current.State.PlayerZones[opponentId].Battlefields
            .FirstOrDefault(objectId => current.State.CardObjects.TryGetValue(objectId, out var cardObject)
                && cardObject.Tags.Contains("CARD_TYPE:BATTLEFIELD", StringComparer.Ordinal))
            ?? throw new InvalidOperationException($"B0 auto-driver could not find opponent battlefield for {playerId}.");
        return await MoveBaseUnitToBattlefieldAsync(
            session,
            playerId,
            current,
            $"BATTLEFIELD:{opponentBattlefieldObjectId}",
            "b0-move-unit-to-opponent-battlefield");
    }

    private static async ValueTask<ResolutionResult> MoveBaseUnitToBattlefieldAsync(
        MatchSession session,
        string playerId,
        ResolutionResult current,
        string battlefieldDestination,
        string intentId)
    {
        var zones = current.State.PlayerZones[playerId];
        var sourceObjectId = zones.Base.FirstOrDefault(objectId => IsReadyUnit(current.State, objectId))
            ?? throw new InvalidOperationException(
                $"B0 auto-driver could not find a ready base unit for {playerId}: "
                + JsonSerializer.Serialize(zones.Base.Select(objectId =>
                {
                    current.State.CardObjects.TryGetValue(objectId, out var cardObject);
                    return new
                    {
                        ObjectId = objectId,
                        cardObject?.CardNo,
                        Tags = cardObject?.Tags,
                        cardObject?.IsExhausted,
                        cardObject?.IsFaceDown
                    };
                }).ToArray()));

        var result = await session.SubmitAsync(
            playerId,
            intentId,
            new MoveUnitCommand(sourceObjectId, "BASE", battlefieldDestination, []),
            RawCommand(new MoveUnitCommand(sourceObjectId, "BASE", battlefieldDestination, [])),
            CancellationToken.None);
        AssertAccepted(result);
        AssertNoHiddenZoneLeak(result);
        return result;
    }

    private static async ValueTask<ResolutionResult> PassOpenSpellDuelAsync(
        MatchSession session,
        ResolutionResult current,
        string intentPrefix)
    {
        var result = current;
        for (var index = 0; index < 20; index++)
        {
            if (!string.Equals(result.State.TimingState, TimingStates.SpellDuelOpen, StringComparison.Ordinal)
                && string.IsNullOrWhiteSpace(result.State.FocusPlayerId))
            {
                return result;
            }

            var focusPlayerId = result.State.FocusPlayerId;
            if (string.IsNullOrWhiteSpace(focusPlayerId))
            {
                throw new InvalidOperationException("B0 auto-driver found spell duel without a focus player.");
            }

            result = await session.SubmitAsync(
                focusPlayerId,
                $"{intentPrefix}-{index}",
                new PassFocusCommand(),
                RawCommand(new PassFocusCommand()),
                CancellationToken.None);
            AssertAccepted(result);
            AssertNoHiddenZoneLeak(result);
        }

        throw new InvalidOperationException("B0 auto-driver exceeded spell-duel pass guard.");
    }

    private static ActionPromptCandidateDto? EnabledCandidate(ActionPromptDto prompt, string action)
    {
        return prompt.Candidates?.FirstOrDefault(candidate =>
            candidate.Enabled && string.Equals(candidate.Action, action, StringComparison.Ordinal));
    }

    private static string? PlayerWithEnabledCandidate(ResolutionResult result, string action)
    {
        return result.Prompts
            .Where(entry => EnabledCandidate(entry.Value, action) is not null)
            .Select(entry => entry.Key)
            .FirstOrDefault();
    }

    private static bool IsReadyUnit(MatchState state, string objectId)
    {
        return state.CardObjects.TryGetValue(objectId, out var cardObject)
            && cardObject.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal)
            && !IsDriverStandbyUnit(cardObject)
            && !cardObject.IsExhausted
            && !cardObject.IsFaceDown;
    }

    private static string? FindBattlefieldUnitByCardNo(
        MatchState state,
        string playerId,
        string cardNo,
        string? battlefieldId = null,
        bool readyOnly = false)
    {
        return state.PlayerZones[playerId].Battlefields.FirstOrDefault(objectId =>
            state.CardObjects.TryGetValue(objectId, out var cardObject)
            && string.Equals(cardObject.CardNo, cardNo, StringComparison.Ordinal)
            && state.ObjectLocations.TryGetValue(objectId, out var location)
            && string.Equals(location.Zone, "BATTLEFIELD", StringComparison.Ordinal)
            && (string.IsNullOrWhiteSpace(battlefieldId)
                || string.Equals(location.BattlefieldObjectId, battlefieldId, StringComparison.Ordinal))
            && (!readyOnly || IsReadyUnit(state, objectId)));
    }

    private static string? FindBaseUnitByCardNo(
        MatchState state,
        string playerId,
        string cardNo)
    {
        return state.PlayerZones[playerId].Base.FirstOrDefault(objectId =>
            state.CardObjects.TryGetValue(objectId, out var cardObject)
            && string.Equals(cardObject.CardNo, cardNo, StringComparison.Ordinal)
            && state.ObjectLocations.TryGetValue(objectId, out var location)
            && string.Equals(location.Zone, "BASE", StringComparison.Ordinal)
            && !cardObject.IsFaceDown);
    }

    private static string? FindReadyBattlefieldDefender(
        MatchState state,
        string playerId,
        string battlefieldId,
        IReadOnlySet<string>? legalTargetIds = null,
        int? maxPowerExclusive = null)
    {
        return state.PlayerZones[playerId].Battlefields
            .Where(objectId => IsReadyUnit(state, objectId))
            .Where(objectId => legalTargetIds is null || legalTargetIds.Contains(objectId))
            .Where(objectId => state.ObjectLocations.TryGetValue(objectId, out var location)
                && string.Equals(location.Zone, "BATTLEFIELD", StringComparison.Ordinal)
                && string.Equals(location.BattlefieldObjectId, battlefieldId, StringComparison.Ordinal))
            .Where(objectId => !maxPowerExclusive.HasValue || state.CardObjects[objectId].Power < maxPowerExclusive.Value)
            .OrderBy(objectId => state.CardObjects[objectId].Power)
            .ThenBy(objectId => state.CardObjects[objectId].CardNo, StringComparer.Ordinal)
            .ThenBy(objectId => objectId, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    private static bool PlayerHandContainsCardNo(MatchState state, string playerId, string cardNo)
    {
        return FindHandCardObjectByCardNo(state, playerId, cardNo) is not null;
    }

    private static string? FindHandCardObjectByCardNo(MatchState state, string playerId, string cardNo)
    {
        return state.PlayerZones.TryGetValue(playerId, out var zones)
            ? zones.Hand.FirstOrDefault(objectId => state.CardObjects.TryGetValue(objectId, out var cardObject)
                && string.Equals(cardObject.CardNo, cardNo, StringComparison.Ordinal))
            : null;
    }

    private static IReadOnlyDictionary<string, int> IntMap(object? value)
    {
        return value switch
        {
            IReadOnlyDictionary<string, int> typed => typed,
            IReadOnlyDictionary<string, object?> objects => objects.ToDictionary(
                entry => entry.Key,
                entry => Assert.IsType<int>(entry.Value),
                StringComparer.Ordinal),
            _ => throw new InvalidOperationException($"Expected string/int metadata map, got {value?.GetType().FullName ?? "null"}.")
        };
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> StringListMap(object? value)
    {
        return value switch
        {
            IReadOnlyDictionary<string, IReadOnlyList<string>> typed => typed,
            IReadOnlyDictionary<string, string[]> arrays => arrays.ToDictionary(
                entry => entry.Key,
                entry => (IReadOnlyList<string>)entry.Value,
                StringComparer.Ordinal),
            IReadOnlyDictionary<string, object?> objects => objects.ToDictionary(
                entry => entry.Key,
                entry => entry.Value switch
                {
                    IReadOnlyList<string> list => list,
                    _ => throw new InvalidOperationException($"Expected string list metadata for {entry.Key}.")
                },
                StringComparer.Ordinal),
            _ => throw new InvalidOperationException($"Expected string/list metadata map, got {value?.GetType().FullName ?? "null"}.")
        };
    }

    private static int EventIndex(
        IReadOnlyList<GameEvent> events,
        Predicate<GameEvent> predicate)
    {
        for (var index = 0; index < events.Count; index++)
        {
            if (predicate(events[index]))
            {
                return index;
            }
        }

        throw new InvalidOperationException("Expected event was not found.");
    }

    private static bool IsDriverStandbyUnit(CardObjectState cardObject)
    {
        return cardObject.Tags.Contains(CardObjectTags.Standby, StringComparer.Ordinal)
            || (CardBehaviorRegistry.TryGetByCardNo(cardObject.CardNo ?? string.Empty, out var behavior)
                && behavior.SourceUnitTags
                    .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Contains(CardObjectTags.Standby, StringComparer.Ordinal));
    }

    private static string OpponentOf(MatchState state, string playerId)
    {
        return state.Seats.Keys.Single(seatPlayerId => !string.Equals(seatPlayerId, playerId, StringComparison.Ordinal));
    }

    private static string DescribeState(MatchState state)
    {
        return JsonSerializer.Serialize(new
        {
            state.Status,
            state.Phase,
            state.TimingState,
            state.ActivePlayerId,
            state.TurnPlayerId,
            state.FocusPlayerId,
            state.PriorityPlayerId,
            Scores = state.PlayerScores,
            RunePools = state.RunePools,
            PendingTaskPhase = state.PendingTaskQueue.Phase,
            state.PendingTaskQueue.ActiveTaskId,
            TaskKinds = state.PendingTaskQueue.Tasks.Select(task => task.Kind).ToArray()
        });
    }

    private static async ValueTask<ResolutionResult> SubmitDeckAsync(
        MatchSession session,
        string playerId,
        OfficialDecklist deck,
        string intentId)
    {
        return await session.SubmitDeckAsync(
            playerId,
            intentId,
            new SubmitDeckCommand(
                deck.LegendCardNo,
                deck.ChampionCardNo,
                deck.MainDeck,
                deck.RuneDeck,
                deck.Battlefields),
            JsonSerializer.SerializeToElement(new
            {
                cmdType = CommandTypes.SubmitDeck,
                legendCardNo = deck.LegendCardNo,
                championCardNo = deck.ChampionCardNo,
                mainDeck = deck.MainDeck,
                runeDeck = deck.RuneDeck,
                battlefields = deck.Battlefields
            }),
            CancellationToken.None);
    }

    private static OfficialDecklist BuildLowCurveOfficialDeck(OfficialCardCatalog catalog)
    {
        return BuildLowCurveOfficialDeck(catalog, JhinLegendCardNo, JhinChampionCardNo);
    }

    private static OfficialDecklist BuildLowCurveOfficialDeck(
        OfficialCardCatalog catalog,
        string legendCardNo,
        string championCardNo)
    {
        return BuildLowCurveOfficialDeck(catalog, legendCardNo, championCardNo, []);
    }

    private static OfficialDecklist BuildLowCurveOfficialDeck(
        OfficialCardCatalog catalog,
        string legendCardNo,
        string championCardNo,
        IReadOnlyList<string> requiredMainDeckCardNos)
    {
        var legend = catalog.Cards.Single(card => string.Equals(card.CardNo, legendCardNo, StringComparison.Ordinal));
        var allowedColors = legend.CardColorList.ToHashSet(StringComparer.Ordinal);
        var cardsByNo = catalog.Cards
            .Where(card => !string.IsNullOrWhiteSpace(card.CardNo))
            .ToDictionary(card => card.CardNo, StringComparer.Ordinal);
        var implementedLowCurveUnits = CardBehaviorRegistry.GetAll()
            .Where(behavior => behavior.PlaysSourceToBaseAsUnit)
            .Where(behavior => behavior.RequiredTargetCount == 0 && behavior.MinTargetCount <= 0)
            .Where(behavior => string.IsNullOrWhiteSpace(behavior.Mode))
            .Where(behavior => behavior.ManaCost <= 2)
            .Select(behavior => behavior.CardNo)
            .Distinct(StringComparer.Ordinal)
            .Where(cardsByNo.ContainsKey)
            .Select(cardNo => cardsByNo[cardNo])
            .Where(card => IsMainDeckCandidate(card, allowedColors))
            .OrderBy(card => card.Energy ?? 0)
            .ThenBy(card => card.CardNo, StringComparer.Ordinal)
            .ToArray();

        var mainDeck = new List<string> { championCardNo };
        var nameCounts = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            [cardsByNo[championCardNo].CardName] = 1
        };
        foreach (var cardNo in requiredMainDeckCardNos)
        {
            Assert.True(cardsByNo.TryGetValue(cardNo, out var requiredCard), $"Required card {cardNo} was not found in the official catalog.");
            Assert.True(IsRequiredMainDeckCandidate(requiredCard, allowedColors), $"Required card {cardNo} is not legal for {legendCardNo}.");
            mainDeck.Add(cardNo);
            nameCounts[requiredCard.CardName] = nameCounts.TryGetValue(requiredCard.CardName, out var current) ? current + 1 : 1;
        }

        foreach (var card in implementedLowCurveUnits)
        {
            while (mainDeck.Count < OfficialDeckValidator.MinimumMainDeckCount
                && (!nameCounts.TryGetValue(card.CardName, out var count)
                    || count < OfficialDeckValidator.DefaultMaxCopiesByName))
            {
                mainDeck.Add(card.CardNo);
                nameCounts[card.CardName] = nameCounts.TryGetValue(card.CardName, out var current) ? current + 1 : 1;
            }

            if (mainDeck.Count >= OfficialDeckValidator.MinimumMainDeckCount)
            {
                break;
            }
        }

        Assert.Equal(OfficialDeckValidator.MinimumMainDeckCount, mainDeck.Count);

        var runeDeck = catalog.Cards
            .Where(card => string.Equals(card.CardCategoryName, "符文", StringComparison.Ordinal))
            .Where(card => TraitsAllowed(card, allowedColors))
            .OrderBy(card => card.CardNo, StringComparer.Ordinal)
            .Select(card => card.CardNo)
            .Take(OfficialDeckValidator.RuneDeckCount)
            .ToArray();
        Assert.Equal(OfficialDeckValidator.RuneDeckCount, runeDeck.Length);

        var battlefields = catalog.Cards
            .Where(card => string.Equals(card.CardCategoryName, "战场", StringComparison.Ordinal))
            .GroupBy(card => card.CardName, StringComparer.Ordinal)
            .Select(group => group.OrderBy(card => card.CardNo, StringComparer.Ordinal).First())
            .OrderBy(card => card.CardNo, StringComparer.Ordinal)
            .Take(OfficialDeckValidator.BattlefieldCount)
            .Select(card => card.CardNo)
            .ToArray();
        Assert.Equal(OfficialDeckValidator.BattlefieldCount, battlefields.Length);

        var deck = new OfficialDecklist(legendCardNo, championCardNo, mainDeck, runeDeck, battlefields);
        var validation = OfficialDeckValidator.Validate(deck, catalog);
        Assert.True(validation.IsValid, string.Join("; ", validation.Errors));
        return deck;
    }

    private static OfficialDecklist BuildDamageAssignmentOfficialDeck(OfficialCardCatalog catalog)
    {
        return BuildLowCurveOfficialDeck(
            catalog,
            LilliaLegendCardNo,
            LilliaChampionCardNo,
            [
                MutantKittenCardNo,
                MutantKittenCardNo,
                MutantKittenCardNo,
                LeblancCardNo,
                LeblancCardNo,
                LeblancCardNo
            ]);
    }

    private static OfficialDecklist BuildShadowResponseOfficialDeck(OfficialCardCatalog catalog)
    {
        return BuildLowCurveOfficialDeck(
            catalog,
            VexLegendCardNo,
            VexChampionCardNo,
            [
                ShadowCardNo,
                ShadowCardNo,
                ShadowCardNo
            ]);
    }

    private static OfficialDecklist BuildCrimsonSignetTreantOfficialDeck(OfficialCardCatalog catalog)
    {
        var deck = BuildLowCurveOfficialDeck(
            catalog,
            JhinLegendCardNo,
            JhinChampionCardNo,
            [
                CrimsonSignetTreantCardNo
            ]);
        var legend = catalog.Cards.Single(card => string.Equals(card.CardNo, deck.LegendCardNo, StringComparison.Ordinal));
        var allowedColors = legend.CardColorList.ToHashSet(StringComparer.Ordinal);
        var redFirstRuneDeck = catalog.Cards
            .Where(card => string.Equals(card.CardCategoryName, "符文", StringComparison.Ordinal))
            .Where(card => TraitsAllowed(card, allowedColors))
            .OrderBy(card => card.CardColorList.Contains(RuneTrait.Red, StringComparer.Ordinal) ? 0 : 1)
            .ThenBy(card => card.CardNo, StringComparer.Ordinal)
            .Select(card => card.CardNo)
            .Take(OfficialDeckValidator.RuneDeckCount)
            .ToArray();
        Assert.Equal(OfficialDeckValidator.RuneDeckCount, redFirstRuneDeck.Length);

        var tunedDeck = deck with { RuneDeck = redFirstRuneDeck };
        var validation = OfficialDeckValidator.Validate(tunedDeck, catalog);
        Assert.True(validation.IsValid, string.Join("; ", validation.Errors));
        return WithSlowBattlefields(catalog, tunedDeck);
    }

    private static OfficialDecklist BuildSameBattlefieldStaticAuraOfficialDeck(OfficialCardCatalog catalog)
    {
        return WithSlowBattlefields(
            catalog,
            BuildLowCurveOfficialDeck(
                catalog,
                PoppyLegendCardNo,
                PoppyChampionCardNo,
                [
                    GarenSameBattlefieldStaticAuraCardNo,
                    DemaciaEnvoyCardNo
                ]));
    }

    private static OfficialDecklist BuildOtherFriendlyStaticAuraOfficialDeck(OfficialCardCatalog catalog)
    {
        return WithSlowBattlefields(
            catalog,
            BuildLowCurveOfficialDeck(
                catalog,
                VexLegendCardNo,
                VexChampionCardNo,
                [
                    BaronNashorOtherFriendlyStaticAuraCardNo,
                    WildclawBeastmasterCardNo
                ]));
    }

    private static OfficialDecklist BuildBattlefieldAllUnitsStaticAuraOfficialDeck(OfficialCardCatalog catalog)
    {
        var deck = BuildLowCurveOfficialDeck(
            catalog,
            VexLegendCardNo,
            VexChampionCardNo,
            [WildclawBeastmasterCardNo]);
        var selectedBattlefields = new List<string>
        {
            TrifarianTrainingGroundsBattlefieldAllUnitsStaticAuraCardNo,
            WinningScoreIncreaseBattlefieldCardNo,
            FirstTurnExtraRuneBattlefieldCardNo
        };

        Assert.Equal(OfficialDeckValidator.BattlefieldCount, selectedBattlefields.Count);
        var tunedDeck = deck with { Battlefields = selectedBattlefields };
        var validation = OfficialDeckValidator.Validate(tunedDeck, catalog);
        Assert.True(validation.IsValid, string.Join("; ", validation.Errors));
        return tunedDeck;
    }

    private static OfficialDecklist BuildSourceCombatStaticAuraOfficialDeck(OfficialCardCatalog catalog)
    {
        return WithSlowBattlefields(
            catalog,
            BuildLowCurveOfficialDeck(
                catalog,
                PoppyLegendCardNo,
                PoppyChampionCardNo,
                [
                    ScarletPigeonSourceCombatStaticAuraCardNo,
                    DemaciaEnvoyCardNo
                ]));
    }

    private static OfficialDecklist BuildSourceSameLocationStaticAuraOfficialDeck(OfficialCardCatalog catalog)
    {
        return WithSlowBattlefields(
            catalog,
            BuildLowCurveOfficialDeck(
                catalog,
                PoppyLegendCardNo,
                PoppyChampionCardNo,
                [
                    ReliableSiegeDogSourceSameLocationStaticAuraCardNo,
                    DemaciaEnvoyCardNo
                ]));
    }

    private static OfficialDecklist BuildSameBattlefieldBoonCountStaticAuraOfficialDeck(OfficialCardCatalog catalog)
    {
        return WithSlowBattlefields(
            catalog,
            BuildLowCurveOfficialDeck(
                catalog,
                PoppyLegendCardNo,
                PoppyChampionCardNo,
                [
                    SettSameBattlefieldBoonCountStaticAuraCardNo,
                    ArenaRookieGrantBoonCardNo,
                    DemaciaEnvoyCardNo
                ]));
    }

    private static OfficialDecklist BuildSameBattlefieldOtherFriendlyFilteredStaticAuraOfficialDeck(OfficialCardCatalog catalog)
    {
        return WithSlowBattlefields(
            catalog,
            BuildLowCurveOfficialDeck(
                catalog,
                PoppyLegendCardNo,
                PoppyChampionCardNo,
                [
                    LeeSinSameBattlefieldOtherFriendlyFilteredStaticAuraCardNo,
                    ArenaRookieGrantBoonCardNo,
                    DemaciaEnvoyCardNo
                ]));
    }

    private static OfficialDecklist BuildSameBattlefieldStaticKeywordOfficialDeck(OfficialCardCatalog catalog)
    {
        return WithSlowBattlefields(
            catalog,
            BuildLowCurveOfficialDeck(
                catalog,
                JhinLegendCardNo,
                JhinChampionCardNo,
                [
                    FarronCaptainSameBattlefieldStaticKeywordCardNo,
                    AscendedBelieverCardNo
                ]));
    }

    private static OfficialDecklist BuildSameBattlefieldSteadfastStaticKeywordOfficialDeck(OfficialCardCatalog catalog)
    {
        return WithSlowBattlefields(
            catalog,
            BuildLowCurveOfficialDeck(
                catalog,
                LilliaLegendCardNo,
                LilliaChampionCardNo,
                [
                    TaricSameBattlefieldStaticKeywordCardNo,
                    LeblancCardNo
                ]));
    }

    private static OfficialDecklist BuildTaricBulwarkAssignmentAttackerOfficialDeck(OfficialCardCatalog catalog)
    {
        return WithSlowBattlefields(
            catalog,
            BuildLowCurveOfficialDeck(
                catalog,
                LilliaLegendCardNo,
                LilliaChampionCardNo,
                [
                    WildclawBeastmasterCardNo
                ]));
    }

    private static OfficialDecklist BuildSlowBattlefieldLowCurveOfficialDeck(
        OfficialCardCatalog catalog,
        string legendCardNo,
        string championCardNo)
    {
        return WithSlowBattlefields(catalog, BuildLowCurveOfficialDeck(catalog, legendCardNo, championCardNo));
    }

    private static OfficialDecklist WithSlowBattlefields(OfficialCardCatalog catalog, OfficialDecklist deck)
    {
        var selectedBattlefields = new List<string>
        {
            ForgottenMonumentBattlefieldCardNo,
            WinningScoreIncreaseBattlefieldCardNo,
            FirstTurnExtraRuneBattlefieldCardNo
        };

        Assert.Equal(OfficialDeckValidator.BattlefieldCount, selectedBattlefields.Count);
        var tunedDeck = deck with { Battlefields = selectedBattlefields };
        var validation = OfficialDeckValidator.Validate(tunedDeck, catalog);
        Assert.True(validation.IsValid, string.Join("; ", validation.Errors));
        return tunedDeck;
    }

    private static OfficialDecklist BuildStandbyReactionOfficialDeck(OfficialCardCatalog catalog)
    {
        return BuildLowCurveOfficialDeck(
            catalog,
            VexLegendCardNo,
            VexChampionCardNo,
            [
                ShadowCardNo,
                ShadowCardNo,
                ShadowCardNo,
                TeemoSelfPowerCardNo,
                TeemoSelfPowerCardNo,
                TeemoSelfPowerCardNo
            ]);
    }

    private static OfficialDecklist BuildStandbyOfficialDeck(OfficialCardCatalog catalog)
    {
        return BuildLowCurveOfficialDeck(
            catalog,
            PoppyLegendCardNo,
            PoppyChampionCardNo,
            [
                PakaaCubCardNo,
                PakaaCubCardNo,
                PakaaCubCardNo
            ]);
    }

    private static OfficialDecklist BuildBattlefieldExtraStandbyOfficialDeck(OfficialCardCatalog catalog)
    {
        var deck = BuildStandbyOfficialDeck(catalog);
        var selectedBattlefields = new List<string>
        {
            BandleTreeBattlefieldCardNo,
            WinningScoreIncreaseBattlefieldCardNo,
            FirstTurnExtraRuneBattlefieldCardNo
        };

        Assert.Equal(OfficialDeckValidator.BattlefieldCount, selectedBattlefields.Count);
        var tunedDeck = deck with { Battlefields = selectedBattlefields };
        var validation = OfficialDeckValidator.Validate(tunedDeck, catalog);
        Assert.True(validation.IsValid, string.Join("; ", validation.Errors));
        return tunedDeck;
    }

    private static bool IsMainDeckCandidate(OfficialCard card, HashSet<string> allowedColors)
    {
        return card.CardCategoryName is "单位" or "英雄单位"
            && !card.CardCategoryName.StartsWith("专属", StringComparison.Ordinal)
            && card.CardGroupLimit != 1
            && !card.CardEffect.Contains("{{唯我}}", StringComparison.Ordinal)
            && TraitsAllowed(card, allowedColors);
    }

    private static bool IsRequiredMainDeckCandidate(OfficialCard card, HashSet<string> allowedColors)
    {
        return card.CardCategoryName is "单位" or "英雄单位" or "专属单位"
            && card.CardGroupLimit != 1
            && !card.CardEffect.Contains("{{唯我}}", StringComparison.Ordinal)
            && TraitsAllowed(card, allowedColors);
    }

    private static bool TraitsAllowed(OfficialCard card, HashSet<string> allowedColors)
    {
        return card.CardColorList.All(color => string.Equals(color, "colorless", StringComparison.Ordinal)
            || allowedColors.Contains(color));
    }

    private static JsonElement RawCommand(string cmdType)
    {
        return JsonSerializer.SerializeToElement(new { cmdType });
    }

    private static JsonElement RawCommand(GameCommand command)
    {
        return command switch
        {
            ReadyCommand => RawCommand(command.CmdType),
            PassPriorityCommand => RawCommand(command.CmdType),
            PassFocusCommand => RawCommand(command.CmdType),
            EndTurnCommand => RawCommand(command.CmdType),
            SurrenderCommand => RawCommand(command.CmdType),
            MulliganCommand mulligan => JsonSerializer.SerializeToElement(new
            {
                cmdType = mulligan.CmdType,
                handObjectIds = mulligan.HandObjectIds
            }),
            TapRuneCommand tapRune => JsonSerializer.SerializeToElement(new
            {
                cmdType = tapRune.CmdType,
                sourceObjectId = tapRune.SourceObjectId
            }),
            PlayCardCommand playCard => JsonSerializer.SerializeToElement(new
            {
                cmdType = playCard.CmdType,
                sourceObjectId = playCard.SourceObjectId,
                cardNo = playCard.CardNo,
                targetObjectIds = playCard.TargetObjectIds,
                mode = playCard.Mode,
                optionalCosts = playCard.OptionalCosts ?? [],
                destination = playCard.Destination
            }),
            HideCardCommand hideCard => JsonSerializer.SerializeToElement(new
            {
                cmdType = hideCard.CmdType,
                sourceObjectId = hideCard.SourceObjectId,
                cardNo = hideCard.CardNo,
                destination = hideCard.Destination,
                optionalCosts = hideCard.OptionalCosts ?? []
            }),
            RevealCardCommand revealCard => JsonSerializer.SerializeToElement(new
            {
                cmdType = revealCard.CmdType,
                sourceObjectId = revealCard.SourceObjectId,
                cardNo = revealCard.CardNo,
                targetObjectIds = revealCard.TargetObjectIds,
                mode = revealCard.Mode,
                optionalCosts = revealCard.OptionalCosts ?? [],
                destination = revealCard.Destination
            }),
            MoveUnitCommand moveUnit => JsonSerializer.SerializeToElement(new
            {
                cmdType = moveUnit.CmdType,
                sourceObjectId = moveUnit.SourceObjectId,
                origin = moveUnit.Origin,
                destination = moveUnit.Destination,
                optionalCosts = moveUnit.OptionalCosts ?? []
            }),
            DeclareBattleCommand declareBattle => JsonSerializer.SerializeToElement(new
            {
                cmdType = declareBattle.CmdType,
                battlefieldId = declareBattle.BattlefieldId,
                attackerObjectIds = declareBattle.AttackerObjectIds ?? [],
                defenderObjectIds = declareBattle.DefenderObjectIds ?? [],
                optionalCosts = declareBattle.OptionalCosts ?? [],
                battlefieldTargetObjectIds = declareBattle.BattlefieldTargetObjectIds ?? []
            }),
            ActivateAbilityCommand activateAbility => JsonSerializer.SerializeToElement(new
            {
                cmdType = activateAbility.CmdType,
                sourceObjectId = activateAbility.SourceObjectId,
                abilityId = activateAbility.AbilityId,
                targetObjectIds = activateAbility.TargetObjectIds,
                optionalCosts = activateAbility.OptionalCosts ?? []
            }),
            AssignCombatDamageCommand assignCombatDamage => JsonSerializer.SerializeToElement(new
            {
                cmdType = assignCombatDamage.CmdType,
                battleId = assignCombatDamage.BattleId,
                battlefieldId = assignCombatDamage.BattlefieldId,
                assignments = (assignCombatDamage.Assignments ?? []).Select(assignment => new
                {
                    sourceObjectId = assignment.SourceObjectId,
                    targetObjectId = assignment.TargetObjectId,
                    damage = assignment.Damage
                }).ToArray()
            }),
            _ => RawCommand(command.CmdType)
        };
    }

    private static void AssertAccepted(ResolutionResult result)
    {
        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Null(result.ErrorCode);
        AssertNoHiddenZoneLeak(result);
    }

    private static void AssertNoHiddenZoneLeak(ResolutionResult result)
    {
        foreach (var viewerId in result.State.Seats.Keys)
        {
            var snapshotJson = JsonSerializer.Serialize(result.Snapshots[viewerId]);
            foreach (var (playerId, zones) in result.State.PlayerZones)
            {
                if (string.Equals(playerId, viewerId, StringComparison.Ordinal))
                {
                    continue;
                }

                foreach (var objectId in zones.Hand.Concat(zones.MainDeck).Concat(zones.RuneDeck))
                {
                    Assert.DoesNotContain(objectId, snapshotJson, StringComparison.Ordinal);
                }
            }
        }
    }

    private static RecoveredCommand ToRecoveredCommand(MatchJournalEntry entry)
    {
        return new RecoveredCommand(
            entry.PlayerId,
            entry.ClientIntentId,
            entry.CommandType,
            entry.RawCommand?.Clone(),
            entry.StartedTick,
            entry.CompletedTick,
            entry.StartedEventSequence,
            entry.CompletedEventSequence,
            entry.Accepted,
            entry.ErrorMessage);
    }

    private static IReadOnlyList<RecoveredEvent> ToRecoveredEvents(IEnumerable<MatchJournalEntry> entries)
    {
        var recoveredEvents = new List<RecoveredEvent>();
        foreach (var entry in entries)
        {
            for (var index = 0; index < entry.Events.Count; index++)
            {
                recoveredEvents.Add(new RecoveredEvent(
                    entry.StartedEventSequence + index + 1,
                    entry.CompletedTick,
                    index,
                    entry.Events[index]));
            }
        }

        return recoveredEvents;
    }

    private static MatchState BuildSeatedInitialState(string roomId, long seed)
    {
        return MatchReplayInitialStateBuilder.FromSeats(
            roomId,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "P1",
                ["P2"] = "P2"
            }) with
        {
            Seed = seed
        };
    }

    private static MatchState BuildCrimsonSignetTreantMidgameInitialState(MatchState state)
    {
        return BuildSpecificCardsMidgameInitialState(
            state,
            "P1",
            [CrimsonSignetTreantCardNo],
            new RunePool(
                mana: 5,
                power: 0,
                new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    [RuneTrait.Red] = 1
                }));
    }

    private static MatchState BuildSameBattlefieldStaticAuraMidgameInitialState(MatchState state)
    {
        return BuildSpecificCardsMidgameInitialState(
            state,
            "P1",
            [GarenSameBattlefieldStaticAuraCardNo, DemaciaEnvoyCardNo],
            new RunePool(mana: 6, power: 0, new Dictionary<string, int>(StringComparer.Ordinal)));
    }

    private static MatchState BuildOtherFriendlyStaticAuraMidgameInitialState(MatchState state)
    {
        return BuildSpecificCardsMidgameInitialState(
            state,
            "P1",
            [BaronNashorOtherFriendlyStaticAuraCardNo, WildclawBeastmasterCardNo],
            new RunePool(mana: 16, power: 0, new Dictionary<string, int>(StringComparer.Ordinal)));
    }

    private static MatchState BuildBattlefieldAllUnitsStaticAuraMidgameInitialState(MatchState state)
    {
        return BuildSpecificCardsMidgameInitialState(
            state,
            "P1",
            [WildclawBeastmasterCardNo],
            new RunePool(mana: 8, power: 0, new Dictionary<string, int>(StringComparer.Ordinal)));
    }

    private static MatchState BuildSourceCombatStaticAuraMidgameInitialState(MatchState state)
    {
        return BuildSpecificCardsMidgameInitialState(
            state,
            "P1",
            [ScarletPigeonSourceCombatStaticAuraCardNo, DemaciaEnvoyCardNo],
            new RunePool(mana: 7, power: 0, new Dictionary<string, int>(StringComparer.Ordinal)));
    }

    private static MatchState BuildSourceSameLocationStaticAuraMidgameInitialState(MatchState state)
    {
        return BuildSpecificCardsMidgameInitialState(
            state,
            "P1",
            [ReliableSiegeDogSourceSameLocationStaticAuraCardNo, DemaciaEnvoyCardNo],
            new RunePool(mana: 6, power: 0, new Dictionary<string, int>(StringComparer.Ordinal)));
    }

    private static MatchState BuildSameBattlefieldBoonCountStaticAuraMidgameInitialState(MatchState state)
    {
        return BuildSpecificCardsMidgameInitialState(
            state,
            "P1",
            [SettSameBattlefieldBoonCountStaticAuraCardNo, ArenaRookieGrantBoonCardNo, DemaciaEnvoyCardNo],
            new RunePool(mana: 10, power: 0, new Dictionary<string, int>(StringComparer.Ordinal)));
    }

    private static MatchState BuildSameBattlefieldOtherFriendlyFilteredStaticAuraMidgameInitialState(MatchState state)
    {
        return BuildSpecificCardsMidgameInitialState(
            state,
            "P1",
            [LeeSinSameBattlefieldOtherFriendlyFilteredStaticAuraCardNo, ArenaRookieGrantBoonCardNo, DemaciaEnvoyCardNo],
            new RunePool(mana: 12, power: 0, new Dictionary<string, int>(StringComparer.Ordinal)));
    }

    private static MatchState BuildSameBattlefieldStaticKeywordMidgameInitialState(MatchState state)
    {
        return BuildSpecificCardsMidgameInitialState(
            state,
            "P1",
            [FarronCaptainSameBattlefieldStaticKeywordCardNo, AscendedBelieverCardNo],
            new RunePool(mana: 7, power: 0, new Dictionary<string, int>(StringComparer.Ordinal)));
    }

    private static MatchState BuildSameBattlefieldSteadfastStaticKeywordMidgameInitialState(MatchState state)
    {
        return BuildSpecificCardsMidgameInitialState(
            state,
            "P1",
            [TaricSameBattlefieldStaticKeywordCardNo, LeblancCardNo],
            new RunePool(mana: 8, power: 0, new Dictionary<string, int>(StringComparer.Ordinal)));
    }

    private static MatchState BuildTaricBulwarkDamageAssignmentMidgameInitialState(MatchState state)
    {
        return BuildSpecificCardsForPlayersMidgameInitialState(
            state,
            new Dictionary<string, (IReadOnlyList<string> CardNos, RunePool RunePool)>(StringComparer.Ordinal)
            {
                ["P1"] = (
                    [TaricSameBattlefieldStaticKeywordCardNo, LeblancCardNo],
                    new RunePool(mana: 8, power: 0, new Dictionary<string, int>(StringComparer.Ordinal))),
                ["P2"] = (
                    [WildclawBeastmasterCardNo],
                    new RunePool(mana: 6, power: 0, new Dictionary<string, int>(StringComparer.Ordinal)))
            });
    }

    private static MatchState BuildSpecificCardsMidgameInitialState(
        MatchState state,
        string playerId,
        IReadOnlyList<string> cardNos,
        RunePool runePool)
    {
        return BuildSpecificCardsForPlayersMidgameInitialState(
            state,
            new Dictionary<string, (IReadOnlyList<string> CardNos, RunePool RunePool)>(StringComparer.Ordinal)
            {
                [playerId] = (cardNos, runePool)
            });
    }

    private static MatchState BuildSpecificCardsForPlayersMidgameInitialState(
        MatchState state,
        IReadOnlyDictionary<string, (IReadOnlyList<string> CardNos, RunePool RunePool)> playerSetups)
    {
        var activeSetupPlayerId = playerSetups.Keys.First();
        var runePools = state.RunePools.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        foreach (var seatPlayerId in state.Seats.Keys)
        {
            runePools[seatPlayerId] = playerSetups.TryGetValue(seatPlayerId, out var setup)
                ? setup.RunePool
                : RunePool.Empty;
        }

        var playerZones = state.PlayerZones.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        var objectLocations = state.ObjectLocations.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        foreach (var (setupPlayerId, setup) in playerSetups)
        {
            var zones = playerZones[setupPlayerId];
            var selectedObjectIds = new List<string>();
            foreach (var cardNo in setup.CardNos)
            {
                var objectId = zones.Hand
                    .Concat(zones.MainDeck)
                    .Concat(zones.Base)
                    .FirstOrDefault(candidateObjectId => !selectedObjectIds.Contains(candidateObjectId, StringComparer.Ordinal)
                        && state.CardObjects.TryGetValue(candidateObjectId, out var cardObject)
                        && string.Equals(cardObject.CardNo, cardNo, StringComparison.Ordinal))
                    ?? throw new InvalidOperationException($"B0 midgame setup could not find {cardNo} in {setupPlayerId} official deck zones.");
                selectedObjectIds.Add(objectId);
            }

            playerZones[setupPlayerId] = zones with
            {
                MainDeck = zones.MainDeck.Where(objectId => !selectedObjectIds.Contains(objectId, StringComparer.Ordinal)).ToArray(),
                Hand = zones.Hand
                    .Where(objectId => !selectedObjectIds.Contains(objectId, StringComparer.Ordinal))
                    .Concat(selectedObjectIds)
                    .ToArray(),
                Base = zones.Base.Where(objectId => !selectedObjectIds.Contains(objectId, StringComparer.Ordinal)).ToArray()
            };
            foreach (var objectId in selectedObjectIds)
            {
                objectLocations[objectId] = new ObjectLocationState(setupPlayerId, "HAND");
            }
        }

        return state with
        {
            Status = MatchStatuses.InProgress,
            ActivePlayerId = activeSetupPlayerId,
            TurnPlayerId = activeSetupPlayerId,
            Phase = MatchPhases.Main,
            TimingState = TimingStates.NeutralOpen,
            FocusPlayerId = null,
            PassedFocusPlayerIds = [],
            PriorityPlayerId = null,
            PassedPriorityPlayerIds = [],
            StackItems = [],
            WinnerPlayerId = null,
            RunePools = runePools,
            PlayerZones = playerZones,
            ObjectLocations = objectLocations,
            PlayerScores = state.Seats.Keys.ToDictionary(playerId => playerId, _ => 0, StringComparer.Ordinal),
            PlayerCardsPlayedThisTurn = state.Seats.Keys.ToDictionary(playerId => playerId, _ => 0, StringComparer.Ordinal)
        };
    }

    private static ResolutionResult AcceptedCurrentResult(MatchState state)
    {
        return new ResolutionResult(
            true,
            null,
            state,
            [],
            ResolutionResult.BuildSnapshots(state),
            ResolutionResult.BuildPrompts(state));
    }

    private sealed class RecordingMatchJournal : IMatchJournal
    {
        public List<MatchJournalEntry> Entries { get; } = [];

        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken cancellationToken)
        {
            Entries.Add(entry);
            return ValueTask.CompletedTask;
        }
    }
}
