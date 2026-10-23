using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class Ven032EmpoweredSelfConsumerTests
{
    private const string No = "VEN·032";
    private const string Text = "{{强化12}}。你每控制一枚符文，此技能的费用便减少{{1}}。（支付此费用：强化我。仅在未强化时可用。）\n{{已强化>}} 我获得{{S}}+3。";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    [Fact]
    public async Task ExactCnPrintingAndItsWholePrintedEmpowerProfileAreRegistered()
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadDefaultAsync()).Cards, c => c.CardNo == No);
        Assert.Equal(37720, card.Id); Assert.Equal("霜衣狼母", card.CardName); Assert.Equal(Text, card.CardEffect);
        Assert.Equal(3, card.Energy); Assert.Equal(3, card.Power); Assert.Null(card.ReturnEnergy);
        Assert.Equal([RuneTrait.Green], card.CardColorList); Assert.Equal("犬形", card.Tag);
        Assert.True(CardBehaviorRegistry.TryGetByCardNo(No, out var behavior)); Assert.True(behavior.PlaysSourceToBaseAsUnit);
        Assert.Equal(3, behavior.ManaCost); Assert.Equal(3, behavior.SourceUnitPower); Assert.Equal("犬形", behavior.SourceUnitTags);
        var spec = Assert.Single(EmpowerParser.Parse(Text)); var cost = Assert.Single(spec.Alternatives);
        Assert.True(spec.ReduceManaPerRune); Assert.True(cost.SupportedResources); Assert.False(cost.Exhaust);
        Assert.Equal(12, cost.Mana); Assert.Equal(0, cost.Power); Assert.Empty(cost.TypedPower);
        var self = EmpowerParser.Self(Text); Assert.Equal(3, self.Might); Assert.Empty(self.Keywords);
    }

    [Theory]
    [InlineData("BASE")] [InlineData("BATTLEFIELD:BF")]
    public async Task RealPlayAndActivationPaySeparatelyAndPermanentMightSurvivesBothTurns(string destination)
    {
        var initial = Position(); var driver = await Play(initial, destination);
        Assert.Equal(37, driver.State.RunePools["P1"].Mana); Assert.Equal(3, Might(driver.State));
        var generation = driver.State.CardObjects["UNIT"].ObjectGeneration;
        await driver.Act("P1", Activate());
        Assert.False(driver.State.CardObjects["UNIT"].IsEmpowered); Assert.Equal(3, Might(driver.State));
        Assert.Equal(25, driver.State.RunePools["P1"].Mana);
        Assert.Equal(12, Assert.Single(driver.State.StackItems).EmpowerActivation!.PaidMana);
        Assert.Empty(driver.State.StackItems.Single().TargetObjectIds); JsonRestore(initial, driver);
        await driver.Top(); CheckSelf(driver.State);
        await driver.Act("P1", new EndTurnCommand()); CheckSelf(driver.State);
        await driver.Act("P2", new EndTurnCommand()); CheckSelf(driver.State);
        Assert.Equal(generation, driver.State.CardObjects["UNIT"].ObjectGeneration);
        await Export("permanent-" + destination.Replace(':', '-'), initial, driver);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(5)] [InlineData(11)] [InlineData(12)] [InlineData(13)]
    public async Task ActualActivationCountsOnlyControlledFieldRunesAndFloorsItsManaAtZero(int count)
    {
        var initial = Position(count); var driver = await Play(initial);
        await driver.Act("P1", Activate()); var context = Assert.Single(driver.State.StackItems).EmpowerActivation!;
        Assert.Equal(count, context.ControlledRunes); Assert.Equal(Math.Max(0, 12 - count), context.PaidMana);
        Assert.Equal(37 - Math.Max(0, 12 - count), driver.State.RunePools["P1"].Mana);
        for (var i = 0; i < count; i++) Assert.Equal(i % 2 == 0, driver.State.CardObjects["RUNE-" + i].IsExhausted);
        Assert.Contains("UNRELEASED", driver.State.PlayerZones["P1"].RuneDeck);
        JsonRestore(initial, driver); await driver.Top(); CheckSelf(driver.State);
        await Export("controlled-runes-" + count, initial, driver);
    }

    [Fact]
    public async Task ActualActivationCountsRuneControlRatherThanOwnership()
    {
        var initial = Position(1); var cards = initial.CardObjects.ToDictionary();
        cards["RUNE-0"] = cards["RUNE-0"] with { OwnerId = "P2" };
        cards["OPPONENT-RUNE"] = cards["OPPONENT-RUNE"] with { OwnerId = "P1" };
        initial = initial with { CardObjects = cards }; var driver = await Play(initial);
        await driver.Act("P1", Activate()); var context = Assert.Single(driver.State.StackItems).EmpowerActivation!;
        Assert.Equal(1, context.ControlledRunes); Assert.Equal(11, context.PaidMana); Assert.Equal(26, driver.State.RunePools["P1"].Mana);
        Assert.Equal("P2", driver.State.CardObjects["RUNE-0"].OwnerId); Assert.Equal("P1", driver.State.CardObjects["RUNE-0"].ControllerId);
        Assert.Equal("P1", driver.State.CardObjects["OPPONENT-RUNE"].OwnerId); Assert.Equal("P2", driver.State.CardObjects["OPPONENT-RUNE"].ControllerId);
        JsonRestore(initial, driver); await driver.Top(); CheckSelf(driver.State);
        await Export("rune-control-versus-ownership", initial, driver);
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task RealReturnBeforeOrAfterResolutionCannotEmpowerTheNewHandIncarnation(bool before)
    {
        var initial = AddSpell(Position(5), "RETURN", "OGN·104/298", "P1"); var driver = await Play(initial);
        var generation = driver.State.CardObjects["UNIT"].ObjectGeneration;
        await driver.Act("P1", Activate()); if (!before) { await driver.Top(); CheckSelf(driver.State); }
        await driver.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["UNIT"])); await driver.Top();
        Assert.Contains("UNIT", driver.State.PlayerZones["P1"].Hand); Assert.Equal(generation + 1, driver.State.CardObjects["UNIT"].ObjectGeneration);
        Assert.False(driver.State.CardObjects["UNIT"].IsEmpowered); Assert.Null(driver.State.CardObjects["UNIT"].RuleDerivedTags);
        Assert.Equal(3, Might(driver.State)); Assert.Equal(29, driver.State.RunePools["P1"].Mana);
        Assert.Equal(6, driver.State.PlayerZones["P1"].Base.Count(id => driver.State.CardObjects[id].Tags.Contains(CardObjectTags.RuneCard)));
        if (before) {
            var old = Assert.Single(driver.State.StackItems).EmpowerActivation!;
            Assert.Equal(generation, old.Source.Generation); Assert.Equal(5, old.ControlledRunes); Assert.Equal(7, old.PaidMana);
            foreach (var view in new[] { ResolutionResult.BuildSnapshots(driver.State)["P2"], ResolutionResult.BuildSpectatorSnapshot(driver.State) }) {
                var row = JsonSerializer.SerializeToElement(view, Json).GetProperty("stack").EnumerateArray().Single();
                Assert.Equal("HIDDEN", row.GetProperty("sourceObjectId").GetString()); Assert.Equal("HIDDEN", row.GetProperty("sourceVisibility").GetString());
                Assert.Equal(No, row.GetProperty("cardNo").GetString()); Assert.False(row.TryGetProperty("empowerActivation", out _));
            }
            JsonRestore(initial, driver); await driver.Top(); Assert.False(driver.State.CardObjects["UNIT"].IsEmpowered);
        }
        Assert.Empty(driver.State.StackItems); Assert.Equal(3, driver.State.CardObjects["UNIT"].Power);
        await Export("return-" + before, initial, driver);
    }

    [Theory]
    [InlineData("mana")] [InlineData("controller")] [InlineData("repeated")] [InlineData("target")]
    public async Task IllegalActivationIsAnActualLoggedRejectionWithoutPartialPaymentOrStatus(string branch)
    {
        var initial = Position();
        if (branch == "mana") initial = initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) {
            ["P1"] = initial.RunePools["P1"] with { Mana = 14 } } };
        if (branch == "controller") initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["ENEMY"] = Printed("ENEMY", No, "P2") } };
        var driver = await Play(initial);
        if (branch == "repeated") { await driver.Act("P1", Activate()); await driver.Top(); }
        var command = new ActivateAbilityCommand(branch == "controller" ? "ENEMY" : "UNIT", "EMPOWER:0:0", branch == "target" ? ["UNIT"] : []);
        var node = JsonSerializer.SerializeToNode(command, Json)!.AsObject();
        node["promptId"] = ResolutionResult.BuildPrompts(driver.State)["P1"].PromptId; node["snapshotTick"] = driver.State.Tick;
        await driver.ActRetained(new("P1", "ven032-reject-" + branch, JsonSerializer.SerializeToElement(node, Json)), false);
        Assert.Equal(branch == "mana" ? "资源不足，无法支付强化费用。" : branch == "target"
            ? "强化来源不是目标，请选择当前存在的技能与费用。" : "请在你的开放主阶段选择一名尚未强化的公开单位。",
            driver.Commands().Last().ErrorMessage);
        await Export("illegal-" + branch, initial, driver);
    }

    [Theory]
    [InlineData("paid-mana")] [InlineData("controlled-runes")] [InlineData("coordinated")]
    public async Task TheUntouchedOriginalLogRejectsInventedDiscountAndPoolSnapshots(string mutation)
    {
        var initial = Position(5); var driver = await Play(initial); await driver.Act("P1", Activate()); JsonRestore(initial, driver);
        var item = Assert.Single(driver.State.StackItems); var context = item.EmpowerActivation!;
        Assert.Equal(7, context.PaidMana); Assert.Equal(5, context.ControlledRunes); Assert.Equal(30, driver.State.RunePools["P1"].Mana);
        var changed = mutation == "paid-mana" ? context with { PaidMana = 8 }
            : mutation == "controlled-runes" ? context with { ControlledRunes = 6 }
            : context with { ControlledRunes = 6, PaidMana = 6 };
        var forged = driver.State with { StackItems = [item with { EmpowerActivation = changed }] };
        if (mutation == "coordinated") forged = forged with { RunePools = new Dictionary<string, RunePool>(forged.RunePools) {
            ["P1"] = forged.RunePools["P1"] with { Mana = 31 } } };
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, driver.Commands(), forged, new CoreRuleEngine(), default, driver.Events());
        Assert.False(replay.IsMatch); Assert.NotEmpty(replay.Errors);
        Assert.Equal(7, driver.State.StackItems.Single().EmpowerActivation!.PaidMana); Assert.Equal(30, driver.State.RunePools["P1"].Mana);
        await Export("discount-forgery-" + mutation, initial, driver);
    }

    [Fact]
    public async Task ActualNegativeMightAndExpirationKeepThePermanentSelfBonusAndRawArithmeticSeparate()
    {
        var initial = AddSpell(AddSpell(Position(), "OPEN", "VEN·081", "P1"), "NEGATIVE", "UNL-066/219", "P2");
        var driver = await Play(initial); await driver.Act("P1", Activate()); await driver.Top(); CheckSelf(driver.State);
        await driver.Act("P1", new PlayCardCommand("OPEN", "VEN·081", ["ALLY"])); await driver.Act("P1", new PassPriorityCommand());
        await driver.Act("P2", new PlayCardCommand("NEGATIVE", "UNL-066/219", ["UNIT"])); await driver.Top();
        Assert.True(driver.State.CardObjects["UNIT"].IsEmpowered); Assert.Equal(-7, driver.State.CardObjects["UNIT"].Power); Assert.Equal(0, Might(driver.State));
        Assert.Equal(-10, Assert.Single(driver.State.CardObjects["UNIT"].UntilEndOfTurnPowerModifiers).PowerDelta);
        await driver.Top(); await driver.Act("P1", new EndTurnCommand()); CheckSelf(driver.State);
        await Export("negative-might-expiration", initial, driver);
    }

    private static MatchState Position(int runes = 0)
    {
        var state = VenEmpoweredSelfConsumerTests.Position(No); var cards = state.CardObjects.ToDictionary(); var locations = state.ObjectLocations.ToDictionary(); var zones = state.PlayerZones.ToDictionary();
        for (var i = 0; i < runes; i++) {
            var id = "RUNE-" + i; cards[id] = Printed(id, "UNL-R02", "P1") with { IsExhausted = i % 2 == 0 };
            locations[id] = new("P1", "BASE"); zones["P1"] = zones["P1"] with { Base = [..zones["P1"].Base, id] };
        }
        cards["UNRELEASED"] = Printed("UNRELEASED", "UNL-R02", "P1"); locations["UNRELEASED"] = new("P1", "RUNE_DECK"); zones["P1"] = zones["P1"] with { RuneDeck = ["UNRELEASED"] };
        cards["OPPONENT-RUNE"] = Printed("OPPONENT-RUNE", "UNL-R02", "P2"); locations["OPPONENT-RUNE"] = new("P2", "BASE"); zones["P2"] = zones["P2"] with { Base = [..zones["P2"].Base, "OPPONENT-RUNE"] };
        return state with { RoomId = "VEN032-EMPOWERED-SELF", CardObjects = cards, ObjectLocations = locations, PlayerZones = zones };
    }
    private static CardObjectState Printed(string id, string no, string owner) => OfficialGarenConquestBattlefieldCountTests.Printed(id, no, owner);
    private static ActivateAbilityCommand Activate() => new("UNIT", "EMPOWER:0:0", []);
    private static async Task<Driver> Play(MatchState initial, string destination = "BASE")
    { var driver = new Driver(initial); await driver.Act("P1", new PlayCardCommand("UNIT", No, [], Destination: destination)); return driver; }
    private static int Might(MatchState state) => CleaveTemporaryAssaultTests.Might(state, "P1", "UNIT");
    private static void CheckSelf(MatchState state)
    {
        var unit = state.CardObjects["UNIT"]; Assert.True(unit.IsEmpowered); Assert.Equal(3, unit.Power); Assert.Equal(6, Might(state));
        Assert.Empty(unit.UntilEndOfTurnEffects); Assert.Empty(unit.UntilEndOfTurnPowerModifiers);
    }
    private static MatchState AddSpell(MatchState state, string id, string no, string owner) => state with {
        CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) { [id] = Printed(id, no, owner) },
        ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { [id] = new(owner, "HAND") },
        PlayerZones = state.PlayerZones.ToDictionary(p => p.Key, p => p.Key == owner ? p.Value with { Hand = [..p.Value.Hand, id] } : p.Value) };
    private static void JsonRestore(MatchState initial, Driver driver)
    {
        var state = driver.State; var sequence = driver.Commands().Last().CompletedEventSequence;
        var snapshots = ResolutionResult.BuildSnapshots(state); var prompts = ResolutionResult.BuildPrompts(state);
        var views = state.Seats.Keys.ToDictionary(p => p, p => new RecoveredPlayerView(p, state.Tick, sequence, snapshots[p], state.Tick, sequence, prompts[p]));
        var frame = new MatchRecoveryFrame(state.RoomId, state.Tick, sequence, driver.Commands(), driver.Events(), views, [], state, initial,
            MatchReplayRedactor.BuildSpectatorFrame(state.RoomId, state.Tick, sequence, [], state)) { OpeningSeed = initial.Seed };
        var decoded = JsonSerializer.Deserialize<MatchRecoveryFrame>(JsonSerializer.Serialize(frame, Json), Json)!;
        Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(decoded.AuthoritativeState!));
        Assert.Empty(MatchRecoveryValidator.Validate(decoded.RoomId, decoded.LastEventSequence, decoded.Commands, decoded.Events,
            decoded.PlayerViews, decoded.AuthoritativeState, decoded.CurrentTick, decoded.SpectatorReplayFrame));
        var restored = MatchSession.Restore(decoded, new CoreRuleEngine(), driver);
        foreach (var player in state.Seats.Keys) {
            Assert.Equal(MatchStateHasher.HashValue(snapshots[player]), MatchStateHasher.HashValue(restored.SnapshotFor(player)));
            Assert.Equal(MatchStateHasher.HashValue(prompts[player]), MatchStateHasher.HashValue(restored.PromptFor(player)));
        }
    }
    private static async Task Export(string name, MatchState initial, Driver driver)
    {
        await driver.Replay(); JsonRestore(initial, driver);
        if (Environment.GetEnvironmentVariable("RIFTBOUND_VEN032_EVIDENCE") is not { Length: > 0 } output) return;
        Directory.CreateDirectory(output); File.WriteAllText(Path.Combine(output, name + ".json"), JsonSerializer.Serialize(new {
            initial, final = driver.State, commands = driver.Commands(), events = driver.Events()
        }, Json));
    }
}
