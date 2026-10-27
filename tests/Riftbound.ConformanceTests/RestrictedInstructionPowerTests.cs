using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;
using Profile = Riftbound.ConformanceTests.LegendResourceBridgeVerifierTests.BridgeProfile;

namespace Riftbound.ConformanceTests;

// CN430 permits acquiring resources while paying; each resource keeps its own
// use restriction. Bullet Time's CN204.3.a instruction is separate from play.
public sealed class RestrictedInstructionPowerTests
{
    public static IEnumerable<object[]> TimingCases()
        => from row in LegendResourceBridgeVerifierTests.PowerSuccessProfiles()
           from before in new[] { false, true } select new object[] { row[0], before };

    [Theory]
    [MemberData(nameof(TimingCases))]
    public async Task RestrictedPowerAcquiredBeforeOrDuringResolutionCannotPayTheInstruction(Profile profile, bool before)
    {
        var initial = Position(profile); var game = new Driver(initial);
        try
        {
            if (before) await game.Act("P1", new LegendActCommand("LEGEND", profile.AbilityId, [], []));
            await game.Act("P1", new PlayCardCommand("CARD", "OGN·268/298", ["FIELD"]));
            await game.Top();
            Assert.Equal("INSTRUCTION_POWER_PAYMENT", game.State.PendingRuleChoice!.Request.Kind);
            if (!before) await BankDuringResolution(game, profile);
            AssertRestrictedPool(game.State.RunePools["P1"], profile);
            Assert.True(game.State.CardObjects["LEGEND"].IsExhausted);
            Assert.Equal(0, game.State.CardObjects["TARGET"].Damage);
            Assert.DoesNotContain(game.State.PendingRuleChoice!.Request.Options, o => o.Id == "SPEND_POWER:1");

            var pending = game.State.PendingRuleChoice;
            await Reject(game, new PayCostCommand(pending.Request.Id, "RULE_REPLACEMENT", ["SPEND_POWER:1"]));
            await Decide(game, "RECYCLE_RUNE:PAY-RUNE");
            Assert.Equal(1, game.State.RunePools["P1"].TotalPower);
            Assert.Equal(2, game.State.RunePools["P1"].TotalStoredPower);
            Assert.Contains(game.State.PendingRuleChoice!.Request.Options, o => o.Id == "SPEND_POWER:1");
            Assert.DoesNotContain(game.State.PendingRuleChoice.Request.Options, o => o.Id == "SPEND_POWER:2");
            await Decide(game, "SPEND_POWER:1");

            Assert.Null(game.State.PendingRuleChoice); Assert.Empty(game.State.StackItems);
            AssertRestrictedPool(game.State.RunePools["P1"], profile);
            Assert.Equal(1, game.State.CardObjects["TARGET"].Damage);
            Assert.Contains("CARD", game.State.PlayerZones["P1"].Graveyard);
            Assert.Contains("PAY-RUNE", game.State.PlayerZones["P1"].RuneDeck);
            var paid = Assert.Single(game.Events(), e => e.Event.Kind == "COST_PAID"
                && Equals(e.Event.Payload.GetValueOrDefault("paymentWindow"), "INSTRUCTION_POWER_PAYMENT")).Event;
            // The public receipt omits the default Other purpose.
            Assert.DoesNotContain("powerPurpose", paid.Payload.Keys); Assert.Equal(1, paid.Payload["totalPowerCost"]);
            Assert.Single(game.Events(), e => e.Event.Kind == "POWER_GAINED"
                && Equals(e.Event.Payload.GetValueOrDefault("sourceObjectId"), "LEGEND"));
            Assert.Single(game.Events(), e => e.Event.Kind == "RUNE_RECYCLED");
            await game.Replay();
        }
        finally { await Export(profile, before ? "before" : "during", initial, game); }
    }

    [Theory]
    [MemberData(nameof(LegendResourceBridgeVerifierTests.PowerSuccessProfiles), MemberType = typeof(LegendResourceBridgeVerifierTests))]
    public async Task SurrenderKeepsTheConfirmedRestrictedResourceWithoutCommittingTheParentInstruction(Profile profile)
    {
        var initial = Position(profile); var game = new Driver(initial);
        try
        {
            await game.Act("P1", new PlayCardCommand("CARD", "OGN·268/298", ["FIELD"]));
            await game.Top(); await BankDuringResolution(game, profile);
            var origin = game.State.PendingRuleChoice!.Origin;
            Assert.False(origin.CardObjects["LEGEND"].IsExhausted);
            Assert.Equal(0, origin.RunePools["P1"].TotalStoredPower);
            AssertRestrictedPool(game.State.RunePools["P1"], profile);
            await game.Act("P1", new SurrenderCommand());
            Assert.Equal(MatchStatuses.Finished, game.State.Status);
            Assert.Null(game.State.PendingRuleChoice);
            Assert.True(game.State.CardObjects["LEGEND"].IsExhausted);
            AssertRestrictedPool(game.State.RunePools["P1"], profile);
            Assert.Equal(0, game.State.CardObjects["TARGET"].Damage);
            Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "COST_PAID"
                && Equals(e.Event.Payload.GetValueOrDefault("paymentWindow"), "INSTRUCTION_POWER_PAYMENT"));
            Assert.Single(game.Events(), e => e.Event.Kind == "POWER_GAINED"
                && Equals(e.Event.Payload.GetValueOrDefault("sourceObjectId"), "LEGEND"));
            await game.Replay();
        }
        finally { await Export(profile, "surrender", initial, game); }
    }

    private static MatchState Position(Profile profile)
    {
        var state = OfficialHoldSequenceTests.AddLegend(InstructionPowerPaymentTimingTests.Position(), profile.CardNo);
        return state with {
            RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(1, 0) },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P1"] = state.PlayerZones["P1"] with { Base = [..state.PlayerZones["P1"].Base, "PAY-RUNE"] } },
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["PAY-RUNE"] = OfficialGarenConquestBattlefieldCountTests.Printed("PAY-RUNE", "UNL-R01", "P1") },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["PAY-RUNE"] = new("P1", "BASE") }
        };
    }

    private static async Task BankDuringResolution(Driver game, Profile profile)
    {
        var option = Assert.Single(game.State.PendingRuleChoice!.Request.Options,
            o => o.Id == $"LEGEND_RESOURCE:LEGEND:{profile.AbilityId}");
        await Decide(game, option.Id);
        Assert.DoesNotContain(game.State.PendingRuleChoice!.Request.Options, o => o.Id == option.Id);
        Assert.Empty(OfficialInsightAndSpellLockTests.Errors(game.State));
        var pool = game.State.RunePools["P1"];
        var unrestricted = pool with { Power = pool.Power + 1, SpellOnlyPower = 0, EquipmentOnlyPower = 0 };
        Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(game.State with {
            RunePools = new Dictionary<string, RunePool>(game.State.RunePools) { ["P1"] = unrestricted } }));
        Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(game.State with {
            CardObjects = new Dictionary<string, CardObjectState>(game.State.CardObjects) {
                ["LEGEND"] = game.State.CardObjects["LEGEND"] with { IsExhausted = false } } }));
        var pending = game.State.PendingRuleChoice;
        Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(game.State with {
            PendingRuleChoice = pending with { Answers = pending.Answers.Take(pending.Answers.Count - 1).ToArray() } }));
    }

    private static Task Decide(Driver game, string choice)
        => game.Act("P1", new PayCostCommand(game.State.PendingRuleChoice!.Request.Id, "RULE_REPLACEMENT", [choice]));

    private static Task Reject(Driver game, GameCommand command)
    {
        var raw = JsonSerializer.SerializeToNode(command, command.GetType(), Web)!.AsObject();
        raw["promptId"] = ResolutionResult.BuildPrompts(game.State)["P1"].PromptId; raw["snapshotTick"] = game.State.Tick;
        return game.ActRetained(new("P1", "unavailable-instruction-power", JsonSerializer.SerializeToElement(raw, Web)), false);
    }

    private static void AssertRestrictedPool(RunePool pool, Profile profile)
        => Assert.Equal(new RunePool(0, 0, spellOnlyPower: profile.AbilityId == LegendActionAbilityCatalog.KaisaLegendAbilityId ? 1 : 0,
            equipmentOnlyPower: profile.AbilityId == LegendActionAbilityCatalog.OrnnLegendAbilityId ? 1 : 0), pool);

    private static async Task Export(Profile profile, string branch, MatchState initial, Driver game)
    {
        if (Environment.GetEnvironmentVariable("RIFTBOUND_RESTRICTED_LEGEND_EVIDENCE") is not { Length: > 0 } output) return;
        Directory.CreateDirectory(output);
        await File.WriteAllTextAsync(Path.Combine(output, "instruction-" + profile.CardNo.Replace('/', '_') + "-" + branch + ".json"),
            JsonSerializer.Serialize(new { scope = "Five exact CN legend printings, real instruction continuation, independent restricted resource child, ordinary rune payment, surrender, original journal/recovery/dedup. Explicit turn-three position; no complete opening or external acceptance.",
                initial, state = game.State, commands = game.Commands(), events = game.Events() }, new JsonSerializerOptions(Web) { WriteIndented = true }));
    }

    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);
}
