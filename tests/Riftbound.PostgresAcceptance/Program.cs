using System.Text.Json;
using Npgsql;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Riftbound.Persistence;

if (args.SequenceEqual(new[] { "--ci-partition-plan" })) { Console.WriteLine(SqlAcceptancePartition.PlanJson()); return; }
var partition = SqlAcceptancePartition.Parse(args);

var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Riftbound")
    ?? throw new InvalidOperationException("A real dedicated PostgreSQL connection is required; this gate cannot return without SQL.");
var settings = new NpgsqlConnectionStringBuilder(connectionString);
if (settings.Database != "riftbound_acceptance" || settings.Host is not ("127.0.0.1" or "localhost"))
    throw new InvalidOperationException("Use the dedicated loopback riftbound_acceptance database; no shared application database is allowed.");
var output = Environment.GetEnvironmentVariable("RIFTBOUND_POSTGRES_EVIDENCE") ?? "var/qa/postgres-acceptance";
Directory.CreateDirectory(output);
var openingRandomnessOnly = args.SequenceEqual(new[] { "--opening-randomness-only" });
var optionalCostsOnly = args.SequenceEqual(new[] { "--optional-costs-only" });
var battlefieldPlayRecoveryOnly = args.SequenceEqual(new[] { "--battlefield-play-recovery-only" });
var battlefieldPlayDamageOnly = args.SequenceEqual(new[] { "--battlefield-play-damage-only" });
var banishEffectPlayOnly = args.SequenceEqual(new[] { "--banish-effect-play-only" });
var hereLocationOnly = args.SequenceEqual(new[] { "--here-location-only" });
var fateWeaverOnly = args.SequenceEqual(new[] { "--fate-weaver-only" });
var multiControllerOrderOnly = args.SequenceEqual(new[] { "--multi-controller-trigger-order-only" });
var emptyObservedRepeatOnly = args.SequenceEqual(new[] { "--empty-observed-repeat-only" });
var observedPlayWindowOnly = args.SequenceEqual(new[] { "--observed-play-window-only" });
var ravenbloomPutHandOnly = args.SequenceEqual(new[] { "--ravenbloom-put-hand-only" });
var attachedPredictOnly = args.SequenceEqual(new[] { "--attached-predict-only" });
var copiedLastBreathOnly = args.SequenceEqual(new[] { "--copied-last-breath-insight-only" });
var copiedDuelOnly = args.SequenceEqual(new[] { "--copied-duel-insight-only" });
var empoweredInsightOnly = args.SequenceEqual(new[] { "--empowered-insight-only" });
var pakaRevealOnly = args.SequenceEqual(new[] { "--paka-move-reveal-only" });
var emptyRevealFollowupOnly = args.SequenceEqual(new[] { "--empty-reveal-followup-only" });
var hweiMoveDiscardTypesOnly = args.SequenceEqual(new[] { "--hwei-move-discard-types-only" });
var destructionGraveyardOnly = args.SequenceEqual(new[] { "--destruction-graveyard-only" });
var conditionalCounterPaymentOnly = args.SequenceEqual(new[] { "--conditional-counter-payment-only" });
var reversalTargetChoiceOnly = args.SequenceEqual(new[] { "--reversal-target-choice-only" });
var spellRemakeDeclarationOnly = args.SequenceEqual(new[] { "--spell-remake-declaration-only" });
var candlelitPrivateLookOnly = args.SequenceEqual(new[] { "--candlelit-private-look-only" });
var candlelitObservedPendingOnly = args.SequenceEqual(new[] { "--candlelit-observed-pending-only" });
var candlelitOptionalObservedOnly = args.SequenceEqual(new[] { "--candlelit-observed-optional-only" });
var candlelitLegendPendingOnly = args.SequenceEqual(new[] { "--candlelit-legend-pending-only" });
var revealedHandPromptOrderOnly = args.SequenceEqual(new[] { "--revealed-hand-prompt-order-only" });
var battlefieldSpellDamageOnly = args.SequenceEqual(new[] { "--battlefield-spell-damage-only" });
var exhaustedLegendConfirmationOnly = args.SequenceEqual(new[] { "--exhausted-legend-confirmation-only" });
var teemoCountedDamageOnly = args.SequenceEqual(new[] { "--teemo-counted-damage-only" });
var controllerDamageBonusOnly = args.SequenceEqual(new[] { "--controller-damage-bonus-only" });
var specialTriggerDamageBonusOnly = args.SequenceEqual(new[] { "--special-trigger-damage-bonus-only" });
var roomSeatJournalOnly = args.SequenceEqual(new[] { "--room-seat-journal-only" });
var battlefieldStartControllerOnly = args.SequenceEqual(new[] { "--battlefield-start-controller-only" });
var battlefieldStartLabYouOnly = args.SequenceEqual(new[] { "--battlefield-start-lab-you-only" });
var deckPendingParentOnly = args.SequenceEqual(new[] { "--deck-pending-parent-only" });
var wildClawReflexiveOnly = args.SequenceEqual(new[] { "--wild-claw-reflexive-only" });
var lightningRushFlowOnly = args.SequenceEqual(new[] { "--lightning-rush-flow-only" });
var finalConquestScoreOnly = args.SequenceEqual(new[] { "--final-conquest-score-only" });
var burnoutActionCompletionOnly = args.SequenceEqual(new[] { "--burnout-action-completion-only" });
var pakaDualTypeRevealOnly = args.SequenceEqual(new[] { "--paka-dual-type-reveal-only" });
if (args.Length != 0 && !openingRandomnessOnly && !optionalCostsOnly && !battlefieldPlayRecoveryOnly && !battlefieldPlayDamageOnly && !banishEffectPlayOnly && !hereLocationOnly && !fateWeaverOnly && !multiControllerOrderOnly && !emptyObservedRepeatOnly && !observedPlayWindowOnly && !ravenbloomPutHandOnly && !attachedPredictOnly && !copiedLastBreathOnly && !copiedDuelOnly && !empoweredInsightOnly && !pakaRevealOnly && !emptyRevealFollowupOnly && !pakaDualTypeRevealOnly && !hweiMoveDiscardTypesOnly && !destructionGraveyardOnly && !conditionalCounterPaymentOnly && !reversalTargetChoiceOnly && !spellRemakeDeclarationOnly && !candlelitPrivateLookOnly && !candlelitObservedPendingOnly && !candlelitOptionalObservedOnly && !candlelitLegendPendingOnly && !revealedHandPromptOrderOnly && !battlefieldSpellDamageOnly && !exhaustedLegendConfirmationOnly && !teemoCountedDamageOnly && !controllerDamageBonusOnly && !specialTriggerDamageBonusOnly && !roomSeatJournalOnly && !battlefieldStartControllerOnly && !battlefieldStartLabYouOnly && !deckPendingParentOnly && !wildClawReflexiveOnly && !lightningRushFlowOnly && !finalConquestScoreOnly && !burnoutActionCompletionOnly && !partition.IsPartition) throw new ArgumentException("Unknown PostgreSQL acceptance selector.");
var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
await using var dataSource = NpgsqlDataSource.Create(connectionString);
await using (var connection = await dataSource.OpenConnectionAsync()) {
    foreach (var path in Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Sql"), "*.sql").Order(StringComparer.Ordinal)) {
        await using var command = new NpgsqlCommand(await File.ReadAllTextAsync(path), connection);
        await command.ExecuteNonQueryAsync();
    }
}
if (burnoutActionCompletionOnly)
{
    await BurnoutActionCompletionSqlAcceptance.RunAsync(dataSource, Path.Combine(output, "burnout-action-completion"));
    Console.WriteLine("SCOPED_POSTGRES_ONLY: burnout-action-completion; original complete SQL suite not selected.");
    return;
}
if (finalConquestScoreOnly)
{
    await FinalConquestScoreSqlAcceptance.RunAsync(dataSource, Path.Combine(output, "final-conquest-score"));
    Console.WriteLine("SCOPED_POSTGRES_ONLY: final-conquest-score; original complete SQL suite not selected.");
    return;
}
if (lightningRushFlowOnly)
{
    await LightningRushFlowSqlAcceptance.RunAsync(dataSource, Path.Combine(output, "lightning-rush-flow"));
    Console.WriteLine("SCOPED_POSTGRES_ONLY: lightning-rush-flow; original complete SQL suite not selected.");
    return;
}
if (wildClawReflexiveOnly)
{
    await WildClawReflexiveSqlAcceptance.RunAsync(dataSource, Path.Combine(output, "wild-claw-reflexive"));
    Console.WriteLine("SCOPED_POSTGRES_ONLY: wild-claw-reflexive; original complete SQL suite not selected.");
    return;
}
if (deckPendingParentOnly)
{
    await DeckPendingParentSqlAcceptance.RunAsync(dataSource, Path.Combine(output, "deck-pending-parent"));
    Console.WriteLine("SCOPED_POSTGRES_ONLY: deck-pending-parent; the retained complete SQL suite was not requested.");
    return;
}
if (battlefieldStartLabYouOnly)
{
    await BattlefieldStartLabYouSqlAcceptance.RunAsync(dataSource, Path.Combine(output, "battlefield-start-lab-you"));
    Console.WriteLine("SCOPED_POSTGRES_ONLY: battlefield-start-lab-you; the retained complete SQL suite was not requested.");
    return;
}
if (battlefieldStartControllerOnly)
{
    await BattlefieldStartControllerSqlAcceptance.RunAsync(dataSource, Path.Combine(output, "battlefield-start-controller"));
    Console.WriteLine("SCOPED_POSTGRES_ONLY: battlefield-start-controller; the retained complete SQL suite was not requested.");
    return;
}
if (roomSeatJournalOnly)
{
    await RoomSeatJournalSqlAcceptance.RunAsync(dataSource, Path.Combine(output, "room-seat-journal"));
    Console.WriteLine("SCOPED_POSTGRES_ONLY: room-seat-journal; the retained complete SQL suite was not requested.");
    return;
}
if (specialTriggerDamageBonusOnly)
{
    await SpecialTriggerDamageBonusSqlAcceptance.RunAsync(dataSource, Path.Combine(output, "special-trigger-damage-bonus"));
    Console.WriteLine("SCOPED_POSTGRES_ONLY: special-trigger-damage-bonus; the retained complete SQL suite was not requested.");
    return;
}
if (controllerDamageBonusOnly)
{
    await ControllerDamageBonusSqlAcceptance.RunAsync(dataSource, Path.Combine(output, "controller-damage-bonus"));
    Console.WriteLine("SCOPED_POSTGRES_ONLY: controller-damage-bonus; the retained complete SQL suite was not requested.");
    return;
}
if (teemoCountedDamageOnly)
{
    await TeemoCountedDamageSqlAcceptance.RunAsync(dataSource, Path.Combine(output, "teemo-counted-damage"));
    Console.WriteLine("SCOPED_POSTGRES_ONLY: teemo-counted-damage; the retained complete SQL suite was not requested.");
    return;
}
if (exhaustedLegendConfirmationOnly)
{
    await ExhaustedLegendConfirmationSqlAcceptance.RunAsync(dataSource, Path.Combine(output, "exhausted-legend-confirmation"));
    Console.WriteLine("SCOPED_POSTGRES_ONLY: exhausted-legend-confirmation; the retained complete SQL suite was not requested.");
    return;
}
if (battlefieldSpellDamageOnly)
{
    await BattlefieldSpellDamageSqlAcceptance.RunAsync(dataSource, Path.Combine(output, "battlefield-spell-damage"));
    Console.WriteLine("SCOPED_POSTGRES_ONLY: battlefield-spell-damage; the retained complete SQL suite was not requested.");
    return;
}
if (revealedHandPromptOrderOnly)
{
    await RevealedHandPromptOrderSqlAcceptance.RunAsync(dataSource, Path.Combine(output, "revealed-hand-prompt-order"));
    Console.WriteLine("SCOPED_POSTGRES_ONLY: revealed-hand-prompt-order; the retained complete SQL suite was not requested.");
    return;
}
if (candlelitLegendPendingOnly)
{
    await CandlelitLegendPendingSqlAcceptance.RunAsync(dataSource, Path.Combine(output, "candlelit-legend-pending"));
    Console.WriteLine("SCOPED_POSTGRES_ONLY: candlelit-legend-pending; the retained complete SQL suite was not requested.");
    return;
}
if (candlelitOptionalObservedOnly)
{
    await CandlelitPrivateLookSqlAcceptance.RunOptionalObservedAsync(dataSource, Path.Combine(output, "candlelit-observed-optional"));
    Console.WriteLine("SCOPED_POSTGRES_ONLY: candlelit-observed-optional; the retained complete SQL suite was not requested.");
    return;
}
if (candlelitObservedPendingOnly)
{
    await CandlelitPrivateLookSqlAcceptance.RunObservedAsync(dataSource, Path.Combine(output, "candlelit-observed-pending"));
    Console.WriteLine("SCOPED_POSTGRES_ONLY: candlelit-observed-pending; the retained complete SQL suite was not requested.");
    return;
}
if (candlelitPrivateLookOnly)
{
    await CandlelitPrivateLookSqlAcceptance.RunAsync(dataSource, Path.Combine(output, "candlelit-private-look"));
    Console.WriteLine("SCOPED_POSTGRES_ONLY: candlelit-private-look; the retained complete SQL suite was not requested.");
    return;
}
if (spellRemakeDeclarationOnly)
{
    await SpellRemakeDeclarationSqlAcceptance.RunAsync(dataSource, Path.Combine(output, "spell-remake-declaration"));
    Console.WriteLine("SCOPED_POSTGRES_ONLY: spell-remake-declaration; the retained complete SQL suite was not requested.");
    return;
}
if (reversalTargetChoiceOnly)
{
    await ReversalTargetChoiceSqlAcceptance.RunAsync(dataSource, Path.Combine(output, "reversal-target-choice"));
    Console.WriteLine("SCOPED_POSTGRES_ONLY: reversal-target-choice; the retained complete SQL suite was not requested.");
    return;
}
if (conditionalCounterPaymentOnly)
{
    await ConditionalCounterPaymentSqlAcceptance.RunAsync(dataSource, Path.Combine(output, "conditional-counter-payment"));
    Console.WriteLine("SCOPED_POSTGRES_ONLY: conditional-counter-payment; the retained complete SQL suite was not requested.");
    return;
}
if (destructionGraveyardOnly)
{
    await DestructionGraveyardSqlAcceptance.RunAsync(dataSource, Path.Combine(output, "destruction-graveyard"));
    Console.WriteLine("SCOPED_POSTGRES_ONLY: destruction-graveyard; the retained complete SQL suite was not requested.");
    return;
}
if (hweiMoveDiscardTypesOnly)
{
    await HweiMoveDiscardTypesSqlAcceptance.RunAsync(dataSource, Path.Combine(output, "hwei-move-discard-types"));
    Console.WriteLine("SCOPED_POSTGRES_ONLY: hwei-move-discard-types; the retained complete SQL suite was not requested.");
    return;
}
if (pakaDualTypeRevealOnly)
{
    await PakaaDualTypeRevealSqlAcceptance.RunAsync(dataSource, Path.Combine(output, "paka-dual-type-reveal"));
    Console.WriteLine("SCOPED_POSTGRES_ONLY: paka-dual-type-reveal; the retained complete SQL suite was not requested.");
    return;
}
if (emptyRevealFollowupOnly)
{
    await EmptyRevealSqlAcceptance.RunAsync(dataSource, Path.Combine(output, "empty-reveal-followup"));
    Console.WriteLine("SCOPED_POSTGRES_ONLY: empty-reveal-followup; the retained complete SQL suite was not requested.");
    return;
}
if (pakaRevealOnly)
{
    await PakaaProtectorSqlAcceptance.RunAsync(dataSource, Path.Combine(output, "paka-move-reveal"));
    Console.WriteLine("SCOPED_POSTGRES_ONLY: paka-move-reveal; the retained complete SQL suite was not requested.");
    return;
}
if (empoweredInsightOnly)
{
    await EmpoweredInsightSqlAcceptance.RunAsync(dataSource, Path.Combine(output, "empowered-insight"));
    Console.WriteLine("SCOPED_POSTGRES_ONLY: empowered-insight; the retained complete SQL suite was not requested.");
    return;
}
if (copiedDuelOnly)
{
    await CopiedDuelInsightSqlAcceptance.RunAsync(dataSource, Path.Combine(output, "copied-duel-insight"));
    Console.WriteLine("SCOPED_POSTGRES_ONLY: copied-duel-insight; the retained complete SQL suite was not requested.");
    return;
}
if (copiedLastBreathOnly)
{
    await CopiedLastBreathInsightSqlAcceptance.RunAsync(dataSource, Path.Combine(output, "copied-last-breath-insight"));
    Console.WriteLine("SCOPED_POSTGRES_ONLY: copied-last-breath-insight; the retained complete SQL suite was not requested.");
    return;
}
if (attachedPredictOnly)
{
    await AttachedPredictSqlAcceptance.RunAsync(dataSource, Path.Combine(output, "attached-predict"));
    Console.WriteLine("SCOPED_POSTGRES_ONLY: attached-predict; the retained complete SQL suite was not requested.");
    return;
}
if (ravenbloomPutHandOnly)
{
    await RavenbloomPutIntoHandSqlAcceptance.RunAsync(dataSource, Path.Combine(output, "ravenbloom-put-hand"));
    Console.WriteLine("SCOPED_POSTGRES_ONLY: ravenbloom-put-hand; the retained complete SQL suite was not requested.");
    return;
}
if (observedPlayWindowOnly)
{
    await ObservedDeckPlayWindowSqlAcceptance.RunAsync(dataSource, Path.Combine(output, "observed-play-window"));
    Console.WriteLine("SCOPED_POSTGRES_ONLY: observed-play-window; the retained complete SQL suite was not requested.");
    return;
}
if (emptyObservedRepeatOnly)
{
    await EmptyObservedDeckRepeatSqlAcceptance.RunAsync(dataSource, Path.Combine(output, "empty-observed-repeat"));
    Console.WriteLine("SCOPED_POSTGRES_ONLY: empty-observed-repeat; the retained complete SQL suite was not requested.");
    return;
}
if (multiControllerOrderOnly)
{
    await MultiControllerTriggerSqlAcceptance.RunAsync(dataSource, Path.Combine(output, "multi-controller-trigger-order"));
    Console.WriteLine("SCOPED_POSTGRES_ONLY: multi-controller-trigger-order; the retained complete SQL suite was not requested.");
    return;
}
if (fateWeaverOnly)
{
    await FateWeaverSqlAcceptance.RunAsync(dataSource, Path.Combine(output, "fate-weaver"));
    Console.WriteLine("SCOPED_POSTGRES_ONLY: fate-weaver; the retained complete SQL suite was not requested.");
    return;
}
if (hereLocationOnly)
{
    await HereLocationSqlAcceptance.RunAsync(dataSource, Path.Combine(output, "here-location-control"));
    Console.WriteLine("SCOPED_POSTGRES_ONLY: here-location-control; the retained complete SQL suite was not requested.");
    return;
}
if (banishEffectPlayOnly)
{
    await BanishEffectPlaySqlAcceptance.RunAsync(dataSource, Path.Combine(output, "banish-effect-play"));
    Console.WriteLine("SCOPED_POSTGRES_ONLY: banish-effect-play; the retained complete SQL suite was not requested.");
    return;
}
if (battlefieldPlayDamageOnly)
{
    await BattlefieldPlayDamageSqlAcceptance.RunAsync(dataSource, Path.Combine(output, "battlefield-play-damage"));
    Console.WriteLine("SCOPED_POSTGRES_ONLY: battlefield-play-damage; the retained complete SQL suite was not requested.");
    return;
}
if (battlefieldPlayRecoveryOnly)
{
    await BattlefieldPlayRecoverySqlAcceptance.RunAsync(dataSource, Path.Combine(output, "battlefield-play-recovery"));
    Console.WriteLine("SCOPED_POSTGRES_ONLY: battlefield-play-recovery; the retained complete SQL suite was not requested.");
    return;
}
if (optionalCostsOnly)
{
    await OptionalCostsSqlAcceptance.RunAsync(dataSource, Path.Combine(output, "optional-costs"));
    Console.WriteLine("SCOPED_POSTGRES_ONLY: optional-costs; the retained complete SQL suite was not requested.");
    return;
}
await partition.RunAsync("private-opening", () => OpeningRandomnessSqlAcceptance.RunAsync(dataSource, Path.Combine(output, "private-opening")));
if (openingRandomnessOnly)
{
    await CardPlayHistoryCausalSqlAcceptance.RunAsync(dataSource, Path.Combine(output, "card-play-causal"));
    Console.WriteLine("SCOPED_POSTGRES_ONLY: private-opening and current card-play-causal; the retained complete SQL suite was not requested.");
    return;
}
await partition.RunAsync("created-token-ready", () => CreatedTokenReadySqlAcceptance.RunAsync(dataSource, Path.Combine(output, "created-token-ready")));
await partition.RunAsync("core-opening", async () =>
{
    var room = "postgres-core-" + Guid.NewGuid().ToString("N");
    var seats = new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" };
    var initial = MatchReplayInitialStateBuilder.FromSeats(room, seats);
    var engine = new CoreRuleEngine(); var journal = new PostgresMatchJournal(dataSource);
    var players = new PostgresMatchPlayerStore(dataSource); var store = new PostgresMatchRecoveryStore(dataSource);
    IMatchSession session = new MatchSession(initial, engine, journal, players);
    var current = initial; var checkpoints = new List<object>(); var submitted = 0;
    var expectedTicks = new HashSet<long>(); var expectedFrames = new HashSet<(long Tick, long Sequence)>();
    long expectedEventSequence = 0;
    var catalog = await OfficialCardCatalog.LoadDefaultAsync();
    var decks = PreconstructedDeckCatalog.Build(catalog);
    await players.SavePlayerSessionAsync(room, "P1", "P1", ReconnectTokenHasher.Hash("audit-p1"), default);
    await players.SavePlayerSessionAsync(room, "P2", "P2", ReconnectTokenHasher.Hash("audit-p2"), default);

    try {
        foreach (var (actor, id) in new[] { ("P1", "jhin-lowcurve"), ("P2", "rumble-lowcurve") }) {
            var deck = decks.Single(d => d.Id == id).Decklist;
            var command = new SubmitDeckCommand(deck.LegendCardNo, deck.ChampionCardNo, deck.MainDeck, deck.RuneDeck, deck.Battlefields);
            var result = await session.SubmitDeckAsync(actor, "deck-" + actor, command, Raw(command), default);
            Check(result.Accepted, result.ErrorMessage); Record(result);
        }
        foreach (var actor in seats.Keys) {
            var result = await session.ReadyAsync(actor, "ready-" + actor, Raw(new ReadyCommand()), default);
            Check(result.Accepted, result.ErrorMessage); Record(result);
        }
        Check(current.Phase == MatchPhases.Mulligan, "Real official deck opening must reach mulligan.");
        var first = current.ActivePlayerId; var second = current.OpeningSecondActionPlayerId!;
        await Submit(first, "mulligan-first", new MulliganCommand([]));
        Check(current.PlayerZones.Values.All(z => z.Hand.Count > 0), "The real opening must create private hands.");
        await ColdRecover("pending-second-mulligan");
        await Submit(second, "mulligan-second", new MulliganCommand([]));
        Check(current.Phase != MatchPhases.Mulligan, "Recovered opening must finish mulligan.");
        await ColdRecover("completed-opening");
        var invalid = new UnsupportedCommand("INVALID_POSTGRES_AUDIT");
        await Submit("P1", "invalid", invalid, accepted: false);
        await ColdRecover("accepted-and-rejected-raw-commands");
        await Submit("P1", "surrender", new SurrenderCommand());
        Check(current.Status == MatchStatuses.Finished, "Real surrender must persist the completed match.");
        await ColdRecover("finished-match");

        var frame = await store.LoadAsync(room, default) ?? throw new InvalidOperationException("Missing durable frame.");
        Check(frame.Commands.Count == submitted && frame.Commands.Count(c => !c.Accepted) == 1, "Actual SQL command identities and rejected command must be retained.");
        Check(frame.Commands.Single(c => c.ClientIntentId == "invalid").RawCommand!.Value.GetProperty("cmdType").GetString() == "INVALID_POSTGRES_AUDIT",
            "Actual SQL must retain the rejected raw payload.");
        var tamperResults = new List<object>();
        await using (var connection = await dataSource.OpenConnectionAsync()) {
            await using var begin = new NpgsqlCommand("select payload::text from state_snapshots where match_id=@room order by state_tick desc,last_event_sequence desc limit 1", connection);
            begin.Parameters.AddWithValue("room", room); var saved = (string)(await begin.ExecuteScalarAsync())!;
            await using var forge = new NpgsqlCommand("update state_snapshots set payload=jsonb_set(payload,'{tick}','999999'::jsonb) where match_id=@room and state_tick=(select max(state_tick) from state_snapshots where match_id=@room)", connection);
            forge.Parameters.AddWithValue("room", room); await forge.ExecuteNonQueryAsync();
            var forged = await store.LoadAsync(room, default);
            Check(forged is not null && !forged.IsConsistent && forged.ValidationErrors.Any(e => e.Contains("tick", StringComparison.OrdinalIgnoreCase)), "SQL-forged authoritative tick must be rejected.");
            tamperResults.Add(new { mutation = "authoritative-tick", rejected = true, errors = forged!.ValidationErrors });
            await using var restore = new NpgsqlCommand("update state_snapshots set payload=@payload::jsonb where match_id=@room and state_tick=(select max(state_tick) from state_snapshots where match_id=@room)", connection);
            restore.Parameters.AddWithValue("payload", saved); restore.Parameters.AddWithValue("room", room); await restore.ExecuteNonQueryAsync();
        }
        await ColdRecover("restored-after-tamper");
        await using var sqlConnection = await dataSource.OpenConnectionAsync();
        await using var countsCommand = new NpgsqlCommand("select (select count(*) from command_log where match_id=@room),(select count(*) from game_events where match_id=@room),(select count(*) from state_snapshots where match_id=@room),(select count(*) from snapshots where match_id=@room),(select count(*) from action_prompts where match_id=@room),version(),(select count(*) from snapshots where match_id=@room and snapshot_tick=(select max(snapshot_tick) from snapshots where match_id=@room)),(select count(*) from action_prompts where match_id=@room and prompt_tick=(select max(prompt_tick) from action_prompts where match_id=@room))", sqlConnection);
        countsCommand.Parameters.AddWithValue("room", room);
        await using var reader = await countsCommand.ExecuteReaderAsync(); Check(await reader.ReadAsync(), "Actual database counts required.");
        var counts = new { commands = reader.GetInt64(0), events = reader.GetInt64(1), states = reader.GetInt64(2), snapshots = reader.GetInt64(3), prompts = reader.GetInt64(4), currentSnapshots = reader.GetInt64(6), currentPrompts = reader.GetInt64(7) };
        await File.WriteAllTextAsync(Path.Combine(output, "sql-counts.json"), JsonSerializer.Serialize(new { submitted, counts }, json));
        Check(counts.commands == submitted && counts.events == expectedEventSequence && counts.events > 0 && counts.states == expectedFrames.Count
            && counts.snapshots == 2 * expectedTicks.Count && counts.prompts == 2 * expectedTicks.Count
            && counts.currentSnapshots == 2 && counts.currentPrompts == 2,
            "SQL rows must retain accepted state/view history and both current player views: " + JsonSerializer.Serialize(counts, json));
        var receipt = new { status = "REAL_POSTGRES_CORE_OPENING_RECOVERY_REPLAY_AND_TAMPER_PASS", databaseVersion = reader.GetString(5), host = settings.Host, port = settings.Port, database = settings.Database,
            executedAtUtc = DateTimeOffset.UtcNow, room, submitted, counts, expectedDistinctTicks = expectedTicks.Count, expectedStateFrames = expectedFrames.Count,
            checkpoints, tamperResults, finalStateHash = MatchStateHasher.Hash(current),
            scope = "Actual core official opening, cold registry recovery, raw rejection, persisted surrender and SQL tamper detection; not full-score victory, all cards, visible clients or database failover." };
        await File.WriteAllTextAsync(Path.Combine(output, "result.json"), JsonSerializer.Serialize(receipt, json));
        Console.WriteLine("POSTGRES_CORE_ACCEPTANCE_PASS: " + submitted + " real commands, " + checkpoints.Count + " recovery/replay checkpoints, SQL tamper rejected");
    } finally {
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var cleanup = new NpgsqlCommand("delete from matches where match_id=@room", connection); cleanup.Parameters.AddWithValue("room", room);
        await cleanup.ExecuteNonQueryAsync();
    }

    JsonElement Raw(GameCommand command) => JsonSerializer.SerializeToElement(command, command.GetType(), json);
    async Task Submit(string actor, string intent, GameCommand command, bool accepted = true) {
        var prompt = session.PromptFor(actor);
        var fields = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(Raw(command), json)!;
        fields["promptId"] = JsonSerializer.SerializeToElement(prompt.PromptId);
        fields["snapshotTick"] = JsonSerializer.SerializeToElement(current.Tick);
        var raw = JsonSerializer.SerializeToElement(fields); var before = MatchStateHasher.Hash(current);
        var result = await session.SubmitAsync(actor, intent, command, raw, default);
        Check(result.Accepted == accepted, result.ErrorMessage);
        if (!accepted) Check(MatchStateHasher.Hash(result.State) == before, "Rejected command must not change durable state.");
        Record(result);
        var rows = await store.LoadAsync(room, default) ?? throw new InvalidOperationException("Actual SQL write missing."); var count = rows.Commands.Count;
        var duplicate = await session.SubmitAsync(actor, intent, command, raw, default);
        Check(duplicate.Accepted == accepted && (await store.LoadAsync(room, default))!.Commands.Count == count, "Duplicate intent must not persist twice.");
    }
    void Record(ResolutionResult result) {
        current = result.State; submitted++; expectedEventSequence += result.Events.Count;
        expectedTicks.Add(current.Tick); expectedFrames.Add((current.Tick, expectedEventSequence));
    }
    async Task ColdRecover(string label) {
        var frame = await store.LoadAsync(room, default) ?? throw new InvalidOperationException("Actual SQL recovery missing.");
        Check(frame.IsConsistent, string.Join("; ", frame.ValidationErrors));
        Check(MatchStateHasher.Hash(frame.AuthoritativeState!) == MatchStateHasher.Hash(current), "Cold SQL recovery must equal live core state.");
        var errors = await MatchActionLogReplayer.ValidateRecoveryFrameAsync(frame, engine, default);
        Check(errors.Count == 0, string.Join("; ", errors));
        var registry = new InMemoryMatchSessionRegistry(engine, journal, store);
        session = await registry.GetOrCreateAsync(room, default);
        foreach (var actor in seats.Keys) Check(session.SnapshotFor(actor).Tick == current.Tick, "Both players must recover the current prompt/snapshot tick.");
        checkpoints.Add(new { label, tick = current.Tick, commands = frame.Commands.Count, events = frame.Events.Count, stateHash = MatchStateHasher.Hash(current), replayMatched = true });
    }
    static void Check(bool valid, string? reason) { if (!valid) throw new InvalidOperationException(reason ?? "PostgreSQL acceptance invariant failed."); }
});

await partition.RunAsync("battle-history", () => BattleHistorySqlAcceptance.RunAsync(dataSource, Path.Combine(output, "battle-history")));
await partition.RunAsync("masked-attendant", () => MaskedAttendantSqlAcceptance.RunAsync(dataSource, Path.Combine(output, "masked-attendant")));
await partition.RunAsync("granted-predict", () => GrantedPredictSqlAcceptance.RunAsync(dataSource, Path.Combine(output, "granted-predict")));
await partition.RunAsync("printed-predict", () => PrintedPredictSqlAcceptance.RunAsync(dataSource, Path.Combine(output, "printed-predict")));
await partition.RunAsync("sivir-optional", () => SivirOptionalSqlAcceptance.RunAsync(dataSource, Path.Combine(output, "sivir-optional")));
await partition.RunAsync("rune-destination", () => RuneRecycleDestinationSqlAcceptance.RunAsync(dataSource, Path.Combine(output, "rune-destination")));
await partition.RunAsync("sivir-payment", () => SivirPaymentSqlAcceptance.RunAsync(dataSource, Path.Combine(output, "sivir-payment")));
await partition.RunAsync("conquest-token", () => ConquestTokenSqlAcceptance.RunAsync(dataSource, Path.Combine(output, "conquest-token")));
await partition.RunAsync("recycled-card-boon", () => RecycledCardBoonSqlAcceptance.RunAsync(dataSource, Path.Combine(output, "recycled-card-boon")));
await partition.RunAsync("recycle-boon-lifecycle", () => RecycleBoonLifecycleSqlAcceptance.RunAsync(dataSource, Path.Combine(output, "recycle-boon-lifecycle")));
await partition.RunAsync("attached-recycle-lifecycle", () => AttachedRecycleLifecycleSqlAcceptance.RunAsync(dataSource, Path.Combine(output, "attached-recycle-lifecycle")));
await partition.RunAsync("typed-play-history", () => CardPlayHistorySqlAcceptance.RunAsync(dataSource, Path.Combine(output, "typed-play-history")));
await partition.RunAsync("porobot", () => PatchedPorobotSqlAcceptance.RunAsync(dataSource, Path.Combine(output, "porobot")));
await partition.RunAsync("typed-play-history-causal", () => CardPlayHistoryCausalSqlAcceptance.RunAsync(dataSource, Path.Combine(output, "typed-play-history-causal")));
await partition.RunAsync("clairvoyance", () => ClairvoyanceSqlAcceptance.RunAsync(dataSource, Path.Combine(output, "clairvoyance")));
await partition.RunAsync("insight-repeat", () => InsightRepeatSqlAcceptance.RunAsync(dataSource, Path.Combine(output, "insight-repeat")));
await partition.RunAsync("combat-assignment", () => CombatAssignmentSqlAcceptance.RunAsync(dataSource, Path.Combine(output, "combat-assignment")));
await partition.RunAsync("confirmed-play-origin", () => ConfirmedPlayOriginSqlAcceptance.RunAsync(dataSource, Path.Combine(output, "confirmed-play-origin")));
await partition.RunAsync("physical-origin-cost", () => PhysicalOriginCostSqlAcceptance.RunAsync(dataSource, Path.Combine(output, "physical-origin-cost")));
await partition.RunAsync("granted-origin-haste", () => GrantedOriginHasteSqlAcceptance.RunAsync(dataSource, Path.Combine(output, "granted-origin-haste")));
await partition.RunAsync("opponent-battlefield-control", () => OpponentBattlefieldControlSqlAcceptance.RunAsync(dataSource, Path.Combine(output, "opponent-battlefield-control")));
await partition.RunAsync("optional-sacrifice", () => OptionalSacrificeSqlAcceptance.RunAsync(dataSource, Path.Combine(output, "optional-sacrifice")));
await partition.RunAsync("granted-token-haste", () => GrantedTokenHasteSqlAcceptance.RunAsync(dataSource, Path.Combine(output, "granted-token-haste")));
await partition.RunAsync("optional-costs", () => OptionalCostsSqlAcceptance.RunAsync(dataSource, Path.Combine(output, "optional-costs")));
await partition.RunAsync("battlefield-play-recovery", () => BattlefieldPlayRecoverySqlAcceptance.RunAsync(dataSource, Path.Combine(output, "battlefield-play-recovery")));
await partition.RunAsync("battlefield-play-damage", () => BattlefieldPlayDamageSqlAcceptance.RunAsync(dataSource, Path.Combine(output, "battlefield-play-damage")));
await partition.RunAsync("banish-effect-play", () => BanishEffectPlaySqlAcceptance.RunAsync(dataSource, Path.Combine(output, "banish-effect-play")));
await partition.RunAsync("here-location-control", () => HereLocationSqlAcceptance.RunAsync(dataSource, Path.Combine(output, "here-location-control")));
await partition.RunAsync("fate-weaver", () => FateWeaverSqlAcceptance.RunAsync(dataSource, Path.Combine(output, "fate-weaver")));
await partition.RunAsync("multi-controller-trigger-order", () => MultiControllerTriggerSqlAcceptance.RunAsync(dataSource, Path.Combine(output, "multi-controller-trigger-order")));
await partition.RunAsync("empty-observed-repeat", () => EmptyObservedDeckRepeatSqlAcceptance.RunAsync(dataSource, Path.Combine(output, "empty-observed-repeat")));
await partition.RunAsync("observed-play-window", () => ObservedDeckPlayWindowSqlAcceptance.RunAsync(dataSource, Path.Combine(output, "observed-play-window")));
await partition.RunAsync("ravenbloom-put-hand", () => RavenbloomPutIntoHandSqlAcceptance.RunAsync(dataSource, Path.Combine(output, "ravenbloom-put-hand")));
await partition.RunAsync("attached-predict", () => AttachedPredictSqlAcceptance.RunAsync(dataSource, Path.Combine(output, "attached-predict")));
await partition.RunAsync("copied-last-breath-insight", () => CopiedLastBreathInsightSqlAcceptance.RunAsync(dataSource, Path.Combine(output, "copied-last-breath-insight")));
await partition.RunAsync("copied-duel-insight", () => CopiedDuelInsightSqlAcceptance.RunAsync(dataSource, Path.Combine(output, "copied-duel-insight")));
await partition.RunAsync("empowered-insight", () => EmpoweredInsightSqlAcceptance.RunAsync(dataSource, Path.Combine(output, "empowered-insight")));
await partition.RunAsync("paka-move-reveal", () => PakaaProtectorSqlAcceptance.RunAsync(dataSource, Path.Combine(output, "paka-move-reveal")));
await partition.RunAsync("empty-reveal-followup", () => EmptyRevealSqlAcceptance.RunAsync(dataSource, Path.Combine(output, "empty-reveal-followup")));
await partition.RunAsync("paka-dual-type-reveal", () => PakaaDualTypeRevealSqlAcceptance.RunAsync(dataSource, Path.Combine(output, "paka-dual-type-reveal")));
await partition.RunAsync("hwei-move-discard-types", () => HweiMoveDiscardTypesSqlAcceptance.RunAsync(dataSource, Path.Combine(output, "hwei-move-discard-types")));
await partition.RunAsync("destruction-graveyard", () => DestructionGraveyardSqlAcceptance.RunAsync(dataSource, Path.Combine(output, "destruction-graveyard")));
await partition.RunAsync("conditional-counter-payment", () => ConditionalCounterPaymentSqlAcceptance.RunAsync(dataSource, Path.Combine(output, "conditional-counter-payment")));

await partition.RunAsync("reversal-target-choice", () => ReversalTargetChoiceSqlAcceptance.RunAsync(dataSource, Path.Combine(output, "reversal-target-choice")));
await partition.RunAsync("spell-remake-declaration", () => SpellRemakeDeclarationSqlAcceptance.RunAsync(dataSource, Path.Combine(output, "spell-remake-declaration")));
await partition.RunAsync("candlelit-private-look", () => CandlelitPrivateLookSqlAcceptance.RunAsync(dataSource, Path.Combine(output, "candlelit-private-look")));
await partition.RunAsync("candlelit-legend-pending", () => CandlelitLegendPendingSqlAcceptance.RunAsync(dataSource, Path.Combine(output, "candlelit-legend-pending")));

await partition.RunAsync("revealed-hand-prompt-order", () => RevealedHandPromptOrderSqlAcceptance.RunAsync(dataSource, Path.Combine(output, "revealed-hand-prompt-order")));
await partition.RunAsync("battlefield-spell-damage", () => BattlefieldSpellDamageSqlAcceptance.RunAsync(dataSource, Path.Combine(output, "battlefield-spell-damage")));

await partition.RunAsync("exhausted-legend-confirmation", () => ExhaustedLegendConfirmationSqlAcceptance.RunAsync(dataSource, Path.Combine(output, "exhausted-legend-confirmation")));

await partition.RunAsync("teemo-counted-damage", () => TeemoCountedDamageSqlAcceptance.RunAsync(dataSource, Path.Combine(output, "teemo-counted-damage")));

await partition.RunAsync("controller-damage-bonus", () => ControllerDamageBonusSqlAcceptance.RunAsync(dataSource, Path.Combine(output, "controller-damage-bonus")));

await partition.RunAsync("special-trigger-damage-bonus", () => SpecialTriggerDamageBonusSqlAcceptance.RunAsync(dataSource, Path.Combine(output, "special-trigger-damage-bonus")));

await partition.RunAsync("room-seat-journal", () => RoomSeatJournalSqlAcceptance.RunAsync(dataSource, Path.Combine(output, "room-seat-journal")));

await partition.RunAsync("battlefield-start-controller", () => BattlefieldStartControllerSqlAcceptance.RunAsync(dataSource, Path.Combine(output, "battlefield-start-controller")));

await partition.RunAsync("battlefield-start-lab-you", () => BattlefieldStartLabYouSqlAcceptance.RunAsync(dataSource, Path.Combine(output, "battlefield-start-lab-you")));

await partition.RunAsync("deck-pending-parent", () => DeckPendingParentSqlAcceptance.RunAsync(dataSource, Path.Combine(output, "deck-pending-parent")));
await partition.RunAsync("wild-claw-reflexive", () => WildClawReflexiveSqlAcceptance.RunAsync(dataSource, Path.Combine(output, "wild-claw-reflexive")));
await partition.RunAsync("lightning-rush-flow", () => LightningRushFlowSqlAcceptance.RunAsync(dataSource, Path.Combine(output, "lightning-rush-flow")));
await partition.RunAsync("final-conquest-score", () => FinalConquestScoreSqlAcceptance.RunAsync(dataSource, Path.Combine(output, "final-conquest-score")));
await partition.RunAsync("burnout-action-completion", () => BurnoutActionCompletionSqlAcceptance.RunAsync(dataSource, Path.Combine(output, "burnout-action-completion")));

await partition.CompleteAsync(output);
