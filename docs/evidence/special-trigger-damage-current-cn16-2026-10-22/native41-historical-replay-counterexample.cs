using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using Riftbound.Contracts;
using Riftbound.Engine;

if (args.Length != 3 || File.Exists(args[2])) throw new Exception("Exact retained native frame, original read-only source observation and fresh diagnostic output required.");
var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
using var zip = new GZipStream(File.OpenRead(args[0]), CompressionMode.Decompress); using var original = JsonDocument.Parse(zip);
var frame = original.RootElement.Deserialize<MatchRecoveryFrame>(json)!;
using var observation = JsonDocument.Parse(File.ReadAllText(args[1]));
var firstStored = observation.RootElement.GetProperty("actualEarlyHistoricalAuthority")[0]; var expectedSeats = firstStored.GetProperty("seats");
if (frame.RoomId != "RB-CXM5MH" || frame.Commands.Count != 41 || frame.Events.Count != 263 || firstStored.GetProperty("sequence").GetInt64() != 1 || firstStored.GetProperty("tick").GetInt64() != 1) throw new Exception("Exact first actual native historical command and state required.");
if (RuleInputIdentity.Current.IdentitySha256 != "ec217d6fa40c76c0fa4da67cefa8a2f7b184a196bdcdef03e26acfcddede1468") throw new Exception("Only the actual original fixed7a executing rules.");
var first = frame.Commands[0]; var raw = first.RawCommand!.Value;
var replay = new MatchSession(frame.ReplayInitialState!, new CoreRuleEngine(), NoopMatchJournal.Instance);
var step = await replay.SubmitDeckAsync(first.PlayerId, first.ClientIntentId, (SubmitDeckCommand)GameCommandJsonMapper.Map(raw), raw, default);
var actualSeats = JsonSerializer.SerializeToElement(step.State.Seats, json);
var different = !JsonNode.DeepEquals(JsonNode.Parse(expectedSeats.GetRawText()), JsonNode.Parse(actualSeats.GetRawText()));
var finalOnlyErrors = await MatchActionLogReplayer.ValidateRecoveryFrameAsync(frame, new CoreRuleEngine(), default);
var proof = new { atUtc = DateTimeOffset.UtcNow, status = different && step.Accepted && step.State.Tick == 1 ? "CONFIRMED_NATIVE_FIRST_DECK_HISTORICAL_SEAT_REPLAY_COUNTEREXAMPLE" : "OBSERVATION_ONLY_NOT_CONFIRMED",
    frame.RoomId, commands = frame.Commands.Count, events = frame.Events.Count, actualRuleInputs = RuleInputIdentity.Current,
    firstOriginalAcceptedCommand = first, exactOriginalStoredFirstStateSeatMap = expectedSeats, actualFirstReplayedStateSeatMap = actualSeats,
    originalBlankReplayInitialSeatMap = frame.ReplayInitialState!.Seats, actualFirstReplayedTick = step.State.Tick, firstStepAccepted = step.Accepted,
    finalOnlyRecoveryValidationErrors = finalOnlyErrors, finalOnlyValidationPassDoesNotCertifyEveryHistoricalState = finalOnlyErrors.Count == 0 && different,
    databaseAccessedOrModifiedByThisOfflineProbe = false, originalAccepted41ActionsReissuedToLiveRoom = false,
    originalInitialMetadataSeedCommandsEventsAndSnapshotsRewritten = false, hardwareUiNormalScoreVictoryObservedSeparately = true,
    allHistoricalReplayOrWholeProductAcceptanceClosed = false };
await File.WriteAllTextAsync(args[2], JsonSerializer.Serialize(proof, json));
Console.WriteLine(JsonSerializer.Serialize(new { proof.status, proof.exactOriginalStoredFirstStateSeatMap, proof.actualFirstReplayedStateSeatMap, proof.finalOnlyRecoveryValidationErrors }, json));
