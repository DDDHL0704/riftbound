using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class Ven043ZeroMightEmpowerConsumerTests
{
    private const string No = "VEN·043";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    [Fact]
    public async Task ExactCn37733KeepsZeroPrintedMightAndItsSeparateWardAndSevenManaEmpower()
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadDefaultAsync()).Cards, c => c.CardNo == No);
        Assert.Equal(37733, card.Id); Assert.Equal("钢爪", card.CardName);
        Assert.Equal("{{法盾}}（对手必须支付{{A}}才能将我选作法术或技能的目标。）\n{{强化7}}（支付{{7}}：强化我。仅在未强化时可用。）\n{{已强化>}} 我获得{{S}}+7。", card.CardEffect);
        Assert.Equal(1, card.Energy); Assert.Null(card.ReturnEnergy); Assert.Equal(0, card.Power);
        Assert.Equal([RuneTrait.Green], card.CardColorList); Assert.Equal("猫科", card.Tag); Assert.Equal("皮尔特沃夫", card.Region);
        Assert.True(CardBehaviorRegistry.TryGetByCardNo(No, out var behavior)); Assert.True(behavior.PlaysSourceToBaseAsUnit);
        Assert.Equal(1, behavior.ManaCost); Assert.Equal(0, behavior.SourceUnitPower); Assert.Equal(0, PrintedPowerCostRules.ForCard(No).Amount);
        var cost = Assert.Single(Assert.Single(EmpowerParser.Parse(card.CardEffect)).Alternatives);
        Assert.True(cost.SupportedResources); Assert.Equal(7, cost.Mana); Assert.Equal(0, cost.Power);
        Assert.Empty(cost.TypedPower); Assert.False(cost.Exhaust);
        Assert.Equal(7, EmpowerParser.Self(card.CardEffect).Might); Assert.Empty(EmpowerParser.Self(card.CardEffect).Keywords);
    }

    [Theory]
    [InlineData("P1")] [InlineData("P2")]
    public async Task ActualOneManaPlayKeepsZeroMightAliveAndSevenManaEmpowerIsPermanent(string owner)
    {
        var initial = Position(owner); var game = new Driver(initial);
        try
        {
            await Tap(game, owner, 1, 8); await Play(game, owner);
            Assert.Equal(7, game.State.RunePools[owner].Mana); var generation = game.State.CardObjects["UNIT"].ObjectGeneration;
            await game.Act(owner, new ActivateAbilityCommand("UNIT", "EMPOWER:0:0", []));
            Assert.Equal(0, game.State.RunePools[owner].Mana); Assert.Equal(0, game.State.RunePools[owner].TotalPower);
            Assert.False(game.State.CardObjects["UNIT"].IsEmpowered); Assert.Equal(0, Might(game.State, owner));
            var activation = Assert.Single(game.State.StackItems).EmpowerActivation!;
            Assert.Equal(7, activation.PaidMana); Assert.Equal(generation, activation.Source.Generation);
            await game.Top(); Check(game.State, owner, true);
            await game.Act(owner, new EndTurnCommand()); Check(game.State, owner, true);
            await game.Act(game.State.TurnPlayerId!, new EndTurnCommand()); Check(game.State, owner, true);
            Assert.Equal(generation, game.State.CardObjects["UNIT"].ObjectGeneration); await game.Replay();
        }
        finally { await Save("physical-permanent-" + owner, initial, game); }
    }

    [Theory]
    [InlineData("P1")] [InlineData("P2")]
    public async Task SixManaRejectsTheActualIntentAndNewManaNeedsAFreshIntent(string owner)
    {
        var initial = Position(owner); var game = new Driver(initial);
        try
        {
            await Tap(game, owner, 1, 7); await Play(game, owner); Assert.Equal(6, game.State.RunePools[owner].Mana);
            var early = Retain(game, owner, new ActivateAbilityCommand("UNIT", "EMPOWER:0:0", []), "steelclaw-early-" + owner);
            await game.ActRetained(early, false); Assert.Equal(ErrorCodes.InsufficientCost, game.LastRetainedResult!.ErrorCode);
            Assert.Equal(6, game.State.RunePools[owner].Mana); Check(game.State, owner, false);
            await game.Act(owner, new TapRuneCommand(Rune(owner, 8))); Assert.Equal(7, game.State.RunePools[owner].Mana);
            var count = game.Commands().Length; await game.ActRetained(early, false);
            Assert.Equal(count, game.Commands().Length); Assert.Equal(ErrorCodes.InsufficientCost, game.LastRetainedResult!.ErrorCode);
            Assert.Equal(7, game.State.RunePools[owner].Mana); Check(game.State, owner, false);
            await game.Act(owner, new ActivateAbilityCommand("UNIT", "EMPOWER:0:0", []));
            Assert.Equal(0, game.State.RunePools[owner].Mana); Assert.False(game.State.CardObjects["UNIT"].IsEmpowered);
            await game.Top(); Check(game.State, owner, true); await game.Replay();
        }
        finally { await Save("rejected-intent-" + owner, initial, game); }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ActualEnemyReactionMustPayThePrintedWardBothBeforeAndAfterEmpower(bool empowered)
    {
        var initial = Position("P1", 12, true); var game = new Driver(initial);
        try
        {
            await Tap(game, "P1", 1, 8); await Play(game, "P1");
            if (empowered) { await game.Act("P1", new ActivateAbilityCommand("UNIT", "EMPOWER:0:0", [])); await game.Top(); }
            await Tap(game, "P1", 9, 4); await game.Act("P1", new PlayCardCommand("OPEN", "VEN·081", ["ALLY"]));
            await game.Act("P1", new PassPriorityCommand()); await Tap(game, "P2", 1, 7);
            var command = new PlayCardCommand("NEGATIVE", "UNL-066/219", ["UNIT"]);
            var quote = new CoreRuleEngine().PreviewPlayCard(game.State, "P2", PlayCostPreviewTests.Request(game.State, command));
            Assert.NotNull(quote.Cost); Assert.Equal(7, quote.Cost.Mana); Assert.Equal(1, quote.Cost.GenericPower);
            await game.ActRetained(Retain(game, "P2", command, "steelclaw-ward-unpaid-" + empowered), false);
            Assert.Contains("NEGATIVE", game.State.PlayerZones["P2"].Hand); Assert.Equal(7, game.State.RunePools["P2"].Mana);
            Assert.Equal(0, game.State.RunePools["P2"].TotalPower); Check(game.State, "P1", empowered);
            await game.Act("P2", new RecycleRuneCommand(Rune("P2", 8)));
            Assert.Equal(1, game.State.RunePools["P2"].TotalPower); await game.Act("P2", command);
            Assert.Equal(0, game.State.RunePools["P2"].Mana); Assert.Equal(0, game.State.RunePools["P2"].TotalPower);
            Assert.Equal(1, game.Events().Last(e => e.Event.Kind == "COST_PAID").Event.Payload["spellshieldTaxPower"]);
            await game.Top(); Assert.Equal(-10, game.State.CardObjects["UNIT"].Power); Assert.Equal(0, Might(game.State, "P1"));
            Assert.Contains("UNIT", game.State.PlayerZones["P1"].Base); await game.Top();
            await game.Act("P1", new EndTurnCommand()); Check(game.State, "P1", empowered); await game.Replay();
        }
        finally { await Save("printed-ward-" + empowered, initial, game); }
    }

    private static MatchState Position(string owner, int ownRunes = 8, bool response = false)
    {
        var state = VenUnitEntryBasicTests.Position(No); var cards = state.CardObjects.ToDictionary();
        var locations = state.ObjectLocations.ToDictionary(); var zones = state.PlayerZones.ToDictionary();
        cards["UNIT"] = OfficialGarenConquestBattlefieldCountTests.Printed("UNIT", No, owner); locations["UNIT"] = new(owner, "HAND");
        zones["P1"] = zones["P1"] with { Hand = zones["P1"].Hand.Where(id => id != "UNIT").ToArray() };
        zones[owner] = zones[owner] with { Hand = [..zones[owner].Hand, "UNIT"] };
        foreach (var actor in response ? new[] { owner, "P2" } : new[] { owner })
        {
            var count = actor == owner ? ownRunes : 8; var added = new List<string>();
            for (var i = 1; i <= count; i++)
            { var id = Rune(actor, i); cards[id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, "UNL-R02", actor); locations[id] = new(actor, "BASE"); added.Add(id); }
            zones[actor] = zones[actor] with { Base = [..zones[actor].Base, ..added] };
        }
        if (response)
            foreach (var (id, no, actor) in new[] { ("OPEN", "VEN·081", "P1"), ("NEGATIVE", "UNL-066/219", "P2") })
            { cards[id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, no, actor); locations[id] = new(actor, "HAND"); zones[actor] = zones[actor] with { Hand = [..zones[actor].Hand, id] }; }
        return state with { RoomId = "VEN043-EXPLICIT-TURN-FIVE-" + owner, ActivePlayerId = owner, TurnPlayerId = owner,
            CardObjects = cards, ObjectLocations = locations, PlayerZones = zones,
            RunePools = new Dictionary<string, RunePool> { ["P1"] = RunePool.Empty, ["P2"] = RunePool.Empty } };
    }
    private static string Rune(string actor, int number) => "VEN043-" + actor + "-RUNE-" + number;
    private static async Task Tap(Driver game, string actor, int first, int count)
    { await game.TapRunes(actor, Enumerable.Range(first, count).Select(i => Rune(actor, i))); }
    private static async Task Play(Driver game, string owner)
    { await game.Act(owner, new PlayCardCommand("UNIT", No, [])); Check(game.State, owner, false); }
    private static void Check(MatchState state, string owner, bool empowered)
    {
        Assert.Contains("UNIT", state.PlayerZones[owner].Base); Assert.DoesNotContain("UNIT", state.PlayerZones[owner].Graveyard);
        Assert.Equal(0, state.CardObjects["UNIT"].Power); Assert.Equal(0, state.CardObjects["UNIT"].Damage);
        Assert.Equal(empowered, state.CardObjects["UNIT"].IsEmpowered); Assert.Equal(empowered ? 7 : 0, Might(state, owner));
        Assert.Contains(CardObjectTags.Spellshield, state.CardObjects["UNIT"].Tags); Assert.Empty(state.CardObjects["UNIT"].UntilEndOfTurnPowerModifiers);
    }
    private static int Might(MatchState state, string owner) => JsonSerializer.SerializeToElement(ResolutionResult.BuildSnapshots(state)[owner], Json)
        .GetProperty("players").GetProperty(owner).GetProperty("objects").GetProperty("UNIT").GetProperty("effectivePower").GetInt32();
    private static ConformanceCommand Retain(Driver game, string actor, GameCommand command, string intent)
    {
        var raw = JsonSerializer.SerializeToNode(command, command.GetType(), Json)!.AsObject();
        raw["promptId"] = game.PromptFor(actor).PromptId; raw["snapshotTick"] = game.State.Tick;
        return new(actor, intent, JsonSerializer.SerializeToElement(raw, Json));
    }
    private static async Task Save(string label, MatchState initial, Driver game)
    {
        if (Environment.GetEnvironmentVariable("RIFTBOUND_VEN043_EVIDENCE") is not { Length: > 0 } folder) return;
        Directory.CreateDirectory(folder); await File.WriteAllTextAsync(Path.Combine(folder, label + ".json"), JsonSerializer.Serialize(new {
            scope = "CN37733 explicit turn-five fixture with zero pools and physical green runes. Actual normal one-mana play, native seven-mana Empower, rejected-intent timing and printed Ward payments; no forced empowered state or source/profile injection. Existing private projection/current recovery/dedup/original replay assertions remain. No canonical opening, SQL/network, latest full L2/L3, applicability or whole-card claim.",
            initial, final = game.State, commands = game.Commands(), events = game.Events(), ruleInputs = RuleInputIdentity.Current }, Json));
    }
}
