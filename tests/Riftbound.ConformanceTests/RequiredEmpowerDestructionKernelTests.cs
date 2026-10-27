using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class RequiredEmpowerDestructionKernelTests
{
    [Fact]
    public async Task GuardianReplacementPaysTheRequiredDestructionWithoutDestroyingTheProtectedUnit()
    {
        var initial = ArmamentMightTests.Position("SFD·051/221", attached: true) with { Tick = 0 };
        initial = Add(initial, "UNIT", "VEN·124", "HAND");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["HOST"] = initial.CardObjects["HOST"] with { Damage = 2 } } };
        var game = new Driver(initial);
        try
        {
            await game.Act("P1", new PlayCardCommand("UNIT", "VEN·124", []));
            var generation = game.State.CardObjects["HOST"].ObjectGeneration;
            await game.Act("P1", new ActivateAbilityCommand("UNIT", "EMPOWER:0:0", [], OptionalCosts: ["DESTROY_FRIENDLY_UNIT:HOST"]));
            Assert.Contains("HOST", game.State.PlayerZones["P1"].Base);
            Assert.DoesNotContain("HOST", game.State.PlayerZones["P1"].Graveyard);
            Assert.Contains("GEAR", game.State.PlayerZones["P1"].Graveyard);
            Assert.Equal(0, game.State.CardObjects["HOST"].Damage); Assert.True(game.State.CardObjects["HOST"].IsExhausted);
            Assert.Equal(new ObjectBinding("HOST", generation), Assert.Single(game.State.StackItems).EmpowerActivation!.DestructionCost!.Unit);
            Assert.False(game.State.CardObjects["UNIT"].IsEmpowered); await game.Top();
            Assert.True(game.State.CardObjects["UNIT"].IsEmpowered); await game.Replay();
        }
        finally { await Save("guardian-replacement", initial, game); }
    }

    [Fact]
    public async Task ActualEmpoweredLastBreathCostRetainsItsSingleTwoRuneTriggerAndOriginalHistory()
    {
        var initial = Add(VenUnitEntryBasicTests.Position("VEN·124"), "COST", "VEN·078", "HAND");
        initial = Add(Add(initial, "DORMANT-1", "UNL-R04", "RUNE_DECK"), "DORMANT-2", "UNL-R04", "RUNE_DECK");
        var game = new Driver(initial);
        try
        {
            await game.Act("P1", new PlayCardCommand("COST", "VEN·078", []));
            await game.Act("P1", new ActivateAbilityCommand("COST", "EMPOWER:0:0", [])); await game.Top();
            Assert.True(game.State.CardObjects["COST"].IsEmpowered);
            await game.Act("P1", new PlayCardCommand("UNIT", "VEN·124", []));
            var mana = game.State.RunePools["P1"].Mana; var power = game.State.RunePools["P1"].TotalPower;
            await game.Act("P1", new ActivateAbilityCommand("UNIT", "EMPOWER:0:0", [], OptionalCosts: ["DESTROY_FRIENDLY_UNIT:COST"]));
            Assert.Contains("COST", game.State.PlayerZones["P1"].Graveyard);
            Assert.False(game.State.CardObjects["UNIT"].IsEmpowered);
            Assert.Equal(mana, game.State.RunePools["P1"].Mana); Assert.Equal(power, game.State.RunePools["P1"].TotalPower);
            Assert.Equal(1, game.State.StackItems.Count(s => s.EffectKind == TriggerKinds.UnitLastBreathCallRuneOne)
                + game.State.TriggerQueue.Count(t => t.EffectKind == TriggerKinds.UnitLastBreathCallRuneOne));
            for (var i = 0; i < 3 && game.State.StackItems.Count > 0; i++) await game.Top();
            Assert.Empty(game.State.StackItems); Assert.Empty(game.State.TriggerQueue);
            Assert.Empty(game.State.PlayerZones["P1"].RuneDeck); Assert.True(game.State.CardObjects["UNIT"].IsEmpowered);
            foreach (var id in new[] { "DORMANT-1", "DORMANT-2" })
            { Assert.Contains(id, game.State.PlayerZones["P1"].Base); Assert.True(game.State.CardObjects[id].IsExhausted); }
            var called = Assert.Single(game.Events(), e => e.Event.Kind == "RUNES_CALLED"
                && Equals(e.Event.Payload.GetValueOrDefault("reason"), TriggerKinds.UnitLastBreathCallRuneOne));
            Assert.Equal(2, called.Event.Payload["count"]); await game.Replay();
        }
        finally { await Save("actual-last-breath-cost", initial, game); }
    }

    [Fact]
    public async Task ARealCreatedMinionCanPayAnotherRequiredCostAfterItCeasesToExist()
    {
        var initial = Add(Add(VenUnitEntryBasicTests.Position("VEN·124"), "COST", "VEN·128", "HAND"), "SECOND", "VEN·124", "HAND");
        var game = new Driver(initial);
        try
        {
            await game.Act("P1", new PlayCardCommand("COST", "VEN·128", []));
            await game.Act("P1", new ActivateAbilityCommand("COST", "EMPOWER:0:0", [])); await game.Top();
            await game.Act("P1", new PlayCardCommand("UNIT", "VEN·124", []));
            await game.Act("P1", new ActivateAbilityCommand("UNIT", "EMPOWER:0:0", [], OptionalCosts: ["DESTROY_FRIENDLY_UNIT:COST"]));
            for (var i = 0; i < 4 && game.State.StackItems.Count > 0; i++) await game.Top();
            Assert.Empty(game.State.StackItems); Assert.Empty(game.State.TriggerQueue);
            var minions = game.State.PlayerZones["P1"].Base.Where(id => game.State.CardObjects[id].Tags.Contains(CardObjectTags.MinionTokenFamily)).ToArray();
            Assert.Equal(2, minions.Length); var paymentId = minions[0];
            var generation = game.State.CardObjects[paymentId].ObjectGeneration;
            await game.Act("P1", new PlayCardCommand("SECOND", "VEN·124", []));
            await game.Act("P1", new ActivateAbilityCommand("SECOND", "EMPOWER:0:0", [], OptionalCosts: ["DESTROY_FRIENDLY_UNIT:" + paymentId]));
            Assert.False(game.State.CardObjects.ContainsKey(paymentId)); Assert.False(game.State.ObjectLocations.ContainsKey(paymentId));
            Assert.DoesNotContain(paymentId, game.State.PlayerZones["P1"].Base); Assert.DoesNotContain(paymentId, game.State.PlayerZones["P1"].Graveyard);
            var item = Assert.Single(game.State.StackItems); var context = item.EmpowerActivation!; var receipt = context.DestructionCost!;
            Assert.Equal(new ObjectBinding(paymentId, generation), receipt.Unit);
            Assert.True(P6TokenFactoryCatalog.IsTokenFactory(receipt.TokenFactoryCardNo));
            Assert.Equal(context.AcquiredTick, receipt.TokenCeasedAtTick);
            foreach (var bad in new[] {
                receipt with { TokenFactoryCardNo = "UNKNOWN-FACTORY" }, receipt with { TokenFactoryCardNo = "SFD·T03" },
                receipt with { TokenCeasedAtTick = context.AcquiredTick + 1 }, receipt with { TokenCeasedAtTick = -1 },
                receipt with { TokenFactoryCardNo = null }, receipt with { Unit = null! } })
                Assert.Contains("invalid captured empower activation", MatchRecoveryValidator.ValidateComponentStateAndViews(game.State with {
                    StackItems = [item with { EmpowerActivation = context with { DestructionCost = bad } }] }));
            var forged = game.State with { StackItems = [item with { EmpowerActivation = context with {
                DestructionCost = receipt with { Unit = new("FORGED-PAID-TOKEN", generation) } } }] };
            var audit = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, game.Commands(), forged, new CoreRuleEngine(), default, game.Events());
            Assert.False(audit.IsMatch); Assert.NotEmpty(audit.Errors);
            Assert.False(game.State.CardObjects["SECOND"].IsEmpowered); await game.Top();
            Assert.True(game.State.CardObjects["SECOND"].IsEmpowered); await game.Replay();
        }
        finally { await Save("actual-created-token-cost", initial, game); }
    }

    [Theory]
    [InlineData("missing")] [InlineData("duplicate")] [InlineData("two")] [InlineData("enemy")]
    [InlineData("gear")] [InlineData("graveyard")] [InlineData("hand-prefix")] [InlineData("target")]
    public async Task ActualInvalidCommandsLeaveTheWholePaymentAndOriginalStateUnchanged(string kind)
    {
        var initial = VenUnitEntryBasicTests.Position("VEN·124"); var game = new Driver(initial);
        try
        {
            await game.Act("P1", new PlayCardCommand("UNIT", "VEN·124", []));
            var before = MatchStateHasher.Hash(game.State); var count = game.Events().Length;
            string[] costs = kind switch {
                "missing" or "target" => [], "duplicate" => ["DESTROY_FRIENDLY_UNIT:ALLY", "DESTROY_FRIENDLY_UNIT:ALLY"],
                "two" => ["DESTROY_FRIENDLY_UNIT:ALLY", "DESTROY_FRIENDLY_UNIT:UNIT"],
                "enemy" => ["DESTROY_FRIENDLY_UNIT:ENEMY"], "gear" => ["DESTROY_FRIENDLY_UNIT:GEAR"],
                "graveyard" => ["DESTROY_FRIENDLY_UNIT:GRAVE"], _ => ["DISCARD_HAND_CARD:ALLY"] };
            var command = new ActivateAbilityCommand("UNIT", "EMPOWER:0:0", kind == "target" ? ["ALLY"] : [], OptionalCosts: costs);
            var raw = JsonSerializer.SerializeToNode(command, Json)!.AsObject();
            raw["promptId"] = game.PromptFor("P1").PromptId; raw["snapshotTick"] = game.State.Tick;
            await game.ActRetained(new("P1", "invalid-friendly-cost-" + kind, JsonSerializer.SerializeToElement(raw, Json)), false);
            Assert.Equal(before, MatchStateHasher.Hash(game.State)); Assert.Equal(count, game.Events().Length);
            Assert.Contains("ALLY", game.State.PlayerZones["P1"].Base); Assert.False(game.State.CardObjects["UNIT"].IsEmpowered);
            Assert.Empty(game.State.StackItems); await game.Replay();
        }
        finally { await Save("invalid-command-" + kind, initial, game); }
    }

    private static MatchState Add(MatchState state, string id, string no, string zone)
    {
        var zones = new Dictionary<string, PlayerZones>(state.PlayerZones); var player = zones["P1"];
        zones["P1"] = zone == "HAND" ? player with { Hand = [..player.Hand, id] } : player with { RuneDeck = [..player.RuneDeck, id] };
        return state with { PlayerZones = zones,
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) { [id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, no, "P1") },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { [id] = new("P1", zone) } };
    }

    private static async Task Save(string name, MatchState initial, Driver game)
    {
        if (Environment.GetEnvironmentVariable("RIFTBOUND_VEN124_EVIDENCE") is not { Length: > 0 } folder) return;
        Directory.CreateDirectory(folder);
        await File.WriteAllTextAsync(Path.Combine(folder, name + ".json"), JsonSerializer.Serialize(new {
            initial, final = game.State, commands = game.Commands(), events = game.Events(), ruleInputs = RuleInputIdentity.Current,
            scope = "Explicit local current-source fixture; real play/resource/cost commands, shared replacement/death pipeline, original journal/recovery/dedup/replay. No complete opening, SQL/network or whole-card certification."
        }, Json));
    }

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
}
