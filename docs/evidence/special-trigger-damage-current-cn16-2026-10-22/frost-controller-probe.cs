using System.IO.Compression;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;

if (args.Length != 1 || File.Exists(args[0])) throw new Exception("Unused bounded probe result required.");
AssemblyLoadContext.Default.Resolving += (context, name) => { var p = Path.Combine(AppContext.BaseDirectory, name.Name + ".dll"); return File.Exists(p) ? context.LoadFromAssemblyPath(p) : null; };
var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(AppContext.BaseDirectory, "Riftbound.ConformanceTests.dll"));
var helper = assembly.GetType("Riftbound.ConformanceTests.FrostHoldTests", true)!;
var state = (MatchState)helper.GetMethod("Position", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, new object[] { false, true })!;
state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
    ["F"] = state.CardObjects["F"] with { ControllerId = "P2" },
    ["OTHER"] = state.CardObjects["OTHER"] with { ControllerId = null } } };
var initialErrors = MatchRecoveryValidator.Validate(state.RoomId, 0, [], [], new Dictionary<string, RecoveredPlayerView>(), state, state.Tick);
var before = state.BattlefieldStates["F"];
var engine = new CoreRuleEngine(); var events = new List<GameEvent>();
var command = new PassPriorityCommand();
var opened = await engine.ResolveAsync(state, new("frost-controller-source-probe-start", "P1", command.CmdType), command, default);
events.AddRange(opened.Events);
var captured = opened.State.StackItems.SingleOrDefault(i => i.BattlefieldStart?.BattlefieldObjectId == "F");
var capturedField = opened.State.BattlefieldStates["F"];
var final = opened;
for (var i = 0; i < 8 && final.Accepted && final.State.StackItems.Count > 0 && final.State.PriorityPlayerId is { } actor; i++)
{
    final = await engine.ResolveAsync(final.State, new("frost-controller-source-probe-pass-" + i, actor, command.CmdType), command, default);
    events.AddRange(final.Events);
}
var confirmed = initialErrors.Count == 0 && opened.Accepted && before.ControllerId == "P2" && !before.Contested
    && before.OccupantControllerIds.SequenceEqual(new[] { "P2" }) && capturedField.ControllerId == "P2" && captured?.ControllerId == "P1";
var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
var result = new { status = confirmed ? "CONFIRMED_BOUNDED_FROST_CONTROLLED_FIELD_SKILL_CONTROLLER_COUNTEREXAMPLE" : "OBSERVATION_ONLY_FROST_CONTROLLER_NOT_CONFIRMED", actualRuleInputs = RuleInputIdentity.Current,
    originalFixtureSource = "FrostHoldTests.Position(false,true)", exactCardNo = "UNL-212/219", initialErrors, originalInitial = state, fieldBefore = before,
    originalPhysicalBattlefieldOwnerZoneNotMoved = true, actualTurnPlayer = state.TurnPlayerId, expectedCapturedControllerByCurrent190_6_a = "P2", opened.Accepted, opened.ErrorMessage,
    capturedField, actualCapturedItem = captured, finalAccepted = final.Accepted, originalFinal = final.State, allActualEvents = events,
    actualTargetDestroyed = final.State.PlayerZones["P2"].Graveyard.Contains("TARGET"), actualP1ElderOnField = final.State.PlayerZones["P1"].Base.Contains("ELDER"),
    changedProductionSources = false, allOldFrostMethodsTheoryRowsAndAssertionsRetained = true, wholeCardOrSourceControlFamilyCertification = false };
await using (var file = new FileStream(args[0], FileMode.CreateNew)) await using (var compressed = new GZipStream(file, CompressionLevel.SmallestSize)) await JsonSerializer.SerializeAsync(compressed, result, json);
Console.WriteLine(JsonSerializer.Serialize(new { result.status, result.initialErrors, actualController = captured?.ControllerId, initialFieldController = before.ControllerId, capturedFieldController = capturedField.ControllerId, result.actualTargetDestroyed, result.actualRuleInputs }, json));
