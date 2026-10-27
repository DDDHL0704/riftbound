using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class ImposedCopiedMovementResourceTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private const string TaxReason = "MAGESEEKER_PATROL_MOVE_TAX_STATIC";

    [Theory]
    [InlineData(0, "decline-before")] [InlineData(1, "decline-before")] [InlineData(2, "decline-before")]
    [InlineData(0, "decline-after")] [InlineData(1, "decline-after")] [InlineData(2, "decline-after")]
    [InlineData(0, "pay")] [InlineData(1, "pay")] [InlineData(2, "pay")]
    public async Task ActualCopiedMoveIncomeCannotPrepayItsTaxOrReplayCommittedRuneResources(int copies, string decision)
    {
        var initial = Position(copies); var game = new Driver(initial); var observations = new List<object>();
        ResolutionResult? prematureCore = null;
        try
        {
            await CopiedMovementObserverTests.Assemble(game, copies);
            Assert.Equal(0, game.State.RunePools["P2"].Mana); Assert.Equal(0, game.State.RunePools["P2"].TotalPower);
            var start = game.Events().LastOrDefault()?.Sequence ?? 0;
            var movePrompt = Assert.Single(game.PromptFor("P2").Candidates!, c => c.Action == CommandTypes.MoveUnit);
            var costs = Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(movePrompt.Metadata!["powerPerExtraUnitByDestination"]);
            Assert.Equal(1, costs["BATTLEFIELD:F1"]);
            await game.Act("P2", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F1", SourceObjectIds: ["HOST", "SECOND"]));
            UnmovedWithoutIncome(game, start); Assert.Equal(0, game.State.RunePools["P2"].TotalPower);
            await Choose(game, "TAP_RUNE:R0"); UnmovedWithoutIncome(game, start);
            Assert.Equal(1, game.State.RunePools["P2"].Mana);
            await Choose(game, "TAP_RUNE:R1"); UnmovedWithoutIncome(game, start);
            Assert.Equal(2, game.State.RunePools["P2"].Mana); Assert.Equal(0, game.State.RunePools["P2"].TotalPower);
            Assert.True(game.State.CardObjects["R0"].IsExhausted); Assert.True(game.State.CardObjects["R1"].IsExhausted);
            Assert.DoesNotContain(game.State.PendingRuleChoice!.Request.Options, o => o.Id == "PAY"
                || o.Id.Contains(P4ActivatedAbilityCatalog.JhinMoveResourceAbilityId, StringComparison.Ordinal));
            var request = game.State.PendingRuleChoice.Request;
            var premature = new PayCostCommand(request.Id, "RULE_REPLACEMENT", ["PAY"]);
            var before = MatchStateHasher.Hash(game.State);
            prematureCore = await new CoreRuleEngine().ResolveAsync(game.State, new("premature-move-pay", "P2", CommandTypes.PayCost), premature, default);
            Assert.False(prematureCore.Accepted); Assert.Equal(ErrorCodes.InvalidTarget, prematureCore.ErrorCode); Assert.Empty(prematureCore.Events);
            Assert.Equal(before, MatchStateHasher.Hash(prematureCore.State));
            var wire = JsonSerializer.SerializeToNode(premature, premature.GetType(), Json)!.AsObject();
            wire["promptId"] = game.PromptFor("P2").PromptId; wire["snapshotTick"] = game.State.Tick;
            var rejected = new ConformanceCommand("P2", "premature-move-pay", JsonSerializer.SerializeToElement(wire, Json));
            await game.ActRetained(rejected, false); Assert.Equal(ErrorCodes.InvalidTarget, game.LastRetainedResult!.ErrorCode);
            observations.Add(new { phase = "initial-rejection", response = game.LastRetainedResult });

            var recycled = decision != "decline-before";
            if (recycled)
            {
                await Choose(game, "RECYCLE_RUNE:R0"); UnmovedWithoutIncome(game, start);
                Assert.Equal(2, game.State.RunePools["P2"].Mana); Assert.Equal(1, game.State.RunePools["P2"].TotalPower);
                Assert.Contains("R0", game.State.PlayerZones["P2"].RuneDeck);
                Assert.Contains(game.State.PendingRuleChoice!.Request.Options, o => o.Id == "PAY");
                await RejectAgain(game, rejected, "funded-pending", observations);
            }
            await Choose(game, decision == "pay" ? "PAY" : "DECLINE");
            Assert.Null(game.State.PendingRuleChoice);
            var paid = decision == "pay"; var earned = paid ? copies + 1 : 0;
            Assert.Equal(2 + earned, game.State.RunePools["P2"].Mana);
            Assert.Equal(paid ? earned : recycled ? 1 : 0, game.State.RunePools["P2"].TotalPower);
            var events = game.Events().Where(e => e.Sequence > start).ToArray();
            Assert.Equal(2, events.Count(e => e.Event.Kind == "RUNE_TAPPED"));
            Assert.Equal(recycled ? 1 : 0, events.Count(e => e.Event.Kind == "RUNE_RECYCLED"));
            Assert.Equal(paid ? 1 : 0, events.Count(e => e.Event.Kind == "COST_PAID" && Equals(e.Event.Payload.GetValueOrDefault("reason"), TaxReason)));
            Assert.Equal(earned, events.Count(e => IsHost(e.Event, "MANA_GAINED")));
            Assert.Equal(earned, events.Count(e => IsHost(e.Event, "POWER_GAINED")));
            Assert.Equal(earned, events.Count(e => IsHost(e.Event, "TRIGGER_QUEUED")));
            foreach (var id in new[] { "HOST", "SECOND" })
            {
                Assert.Equal(paid ? "BATTLEFIELD" : "BASE", game.State.ObjectLocations[id].Zone);
                Assert.Equal(paid ? 1 : 0, game.State.CardObjects[id].MovementCountThisTurn);
                Assert.Equal(paid, game.State.CardObjects[id].IsExhausted);
            }
            Assert.DoesNotContain(game.State.TriggerQueue, t => t.SourceObjectId == "HOST");
            Assert.DoesNotContain(game.State.StackItems, s => s.SourceObjectId == "HOST");
            if (paid)
            {
                var tax = Assert.Single(events, e => e.Event.Kind == "COST_PAID" && Equals(e.Event.Payload.GetValueOrDefault("reason"), TaxReason));
                var moved = Assert.Single(events, e => IsHost(e.Event, "UNIT_MOVED_TO_BATTLEFIELD"));
                Assert.True(tax.Sequence < moved.Sequence);
                Assert.All(events.Where(e => IsHost(e.Event, "MANA_GAINED") || IsHost(e.Event, "POWER_GAINED")), e => Assert.True(e.Sequence > moved.Sequence));
            }
            else UnmovedWithoutIncome(game, start);
            await RejectAgain(game, rejected, "completed-parent", observations); await game.Replay();
        }
        finally
        {
            if (Environment.GetEnvironmentVariable("RIFTBOUND_IMPOSED_COPIED_EVIDENCE") is { Length: > 0 } output)
            {
                Directory.CreateDirectory(output);
                await File.WriteAllTextAsync(Path.Combine(output, $"copies-{copies}-{decision}.json"), JsonSerializer.Serialize(new {
                    scope = "Real Jhin text copies and physical rune commands inside S7, explicit turn5 fixture. Original command/event replay, normal frame and accepted-command dedup; rejected PAY retries across later resources and completed parent. Full last response retained if an assertion fails before driver state update. No ordinary opening, SQL/network/restart or whole-card certification.",
                    initial, driverState = game.State, commands = game.Commands(), events = game.Events(), prematureCore,
                    lastRetainedResponse = game.LastRetainedResult, observations }, Json));
            }
        }
    }

    private static bool IsHost(GameEvent e, string kind) => e.Kind == kind && Equals(e.Payload.GetValueOrDefault("sourceObjectId"), "HOST");
    private static void UnmovedWithoutIncome(Driver game, long start)
    {
        Assert.Equal("BASE", game.State.ObjectLocations["HOST"].Zone);
        Assert.Equal("BASE", game.State.ObjectLocations["SECOND"].Zone);
        Assert.Equal(0, game.State.CardObjects["HOST"].MovementCountThisTurn);
        Assert.False(game.State.CardObjects["HOST"].IsExhausted);
        Assert.DoesNotContain(game.Events().Where(e => e.Sequence > start), e => IsHost(e.Event, "TRIGGER_QUEUED")
            || IsHost(e.Event, "MANA_GAINED") || IsHost(e.Event, "POWER_GAINED"));
    }
    private static async Task Choose(Driver game, string choice)
    {
        var request = game.State.PendingRuleChoice!.Request; Assert.Equal("P2", request.PlayerId);
        Assert.Equal("IMPOSED_MOVEMENT_POWER_PAYMENT", request.Kind); Assert.Contains(request.Options, o => o.Id == choice);
        await game.Act("P2", new PayCostCommand(request.Id, "RULE_REPLACEMENT", [choice]));
    }
    private static async Task RejectAgain(Driver game, ConformanceCommand rejected, string phase, List<object> observations)
    {
        var count = game.Commands().Length; var before = MatchStateHasher.Hash(game.State);
        await game.ActRetained(rejected, false);
        observations.Add(new { phase, before, after = MatchStateHasher.Hash(game.State), commandCountBefore = count,
            commandCountAfter = game.Commands().Length, response = game.LastRetainedResult });
        Assert.Equal(ErrorCodes.InvalidTarget, game.LastRetainedResult!.ErrorCode);
        Assert.Equal(count, game.Commands().Length); Assert.Equal(before, MatchStateHasher.Hash(game.State));
    }
    private static MatchState Position(int copies)
    {
        var state = CopiedMovementObserverTests.Position(P4ActivatedAbilityCatalog.JhinCardNo, copies);
        state = Add(state, "PATROL", "UNL-163/219", "P1", "BATTLEFIELD", "F1");
        state = Add(state, "SECOND", "OGN·219/298", "P2", "BASE");
        state = Add(state, "R0", "OGN·007/298", "P2", "BASE");
        state = Add(state, "R1", "OGN·042/298", "P2", "BASE");
        return state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) {
            ["P2"] = new(copies, 0, new Dictionary<string, int> { [RuneTrait.Green] = copies }) } };
    }
    private static MatchState Add(MatchState s, string id, string no, string player, string zone, string? field = null)
        => s with {
            CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) { [id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, no, player) },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(s.ObjectLocations) { [id] = new(player, zone, field) },
            PlayerZones = new Dictionary<string, PlayerZones>(s.PlayerZones) { [player] = zone == "BASE"
                ? s.PlayerZones[player] with { Base = [..s.PlayerZones[player].Base, id] }
                : s.PlayerZones[player] with { Battlefields = [..s.PlayerZones[player].Battlefields, id] } } };
}
