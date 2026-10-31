using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class Ven069PhysicalEmpowerConsumerTests
{
    private const string No = "VEN·069";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    [Fact]
    public async Task ExactCn37762HasThePrintedBluePlayCostAndManaOnlyEmpower()
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadDefaultAsync()).Cards, c => c.CardNo == No);
        Assert.Equal(37762, card.Id); Assert.Equal("梅尔", card.CardName); Assert.Equal("奥法觉醒", card.SubTitle);
        Assert.Equal(4, card.Energy); Assert.Equal(1, card.ReturnEnergy); Assert.Equal(4, card.Power);
        Assert.Equal([RuneTrait.Blue], card.CardColorList); Assert.Equal(["hero_unit"], card.CardCategoryList);
        var cost = Assert.Single(Assert.Single(EmpowerParser.Parse(card.CardEffect)).Alternatives);
        Assert.Equal(3, cost.Mana); Assert.Equal(0, cost.Power); Assert.Empty(cost.TypedPower);
        Assert.True(cost.SupportedResources); Assert.False(cost.Exhaust); Assert.Empty(cost.AdditionalCost);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task PhysicalRunePaymentsKeepEntryDrawAndManaOnlyEmpowerSeparateFromChosenNegativeMight(bool empower)
    {
        var initial = Position(); var game = new Driver(initial);
        try
        {
            await Play(game, 7); Assert.Equal(3, game.State.RunePools["P1"].Mana);
            Assert.Equal(0, Draws(game)); Assert.False(game.State.CardObjects["UNIT"].IsEmpowered);
            var generation = game.State.CardObjects["UNIT"].ObjectGeneration;
            await game.Top(); Assert.Equal(1, Draws(game)); Assert.Contains("P1-DRAW-1", game.State.PlayerZones["P1"].Hand);
            Assert.Equal(3, game.State.RunePools["P1"].Mana); Assert.Equal(0, game.State.RunePools["P1"].TotalPower);
            if (empower)
            {
                await game.Act("P1", Activate()); Assert.Equal(0, game.State.RunePools["P1"].Mana);
                Assert.Equal(0, game.State.RunePools["P1"].TotalPower); Assert.False(game.State.CardObjects["UNIT"].IsEmpowered);
                var activation = Assert.Single(game.State.StackItems).EmpowerActivation!;
                Assert.Equal(3, activation.PaidMana); Assert.Equal(generation, activation.Source.Generation);
                Assert.Equal(1, Draws(game)); await game.Top(); Assert.True(game.State.CardObjects["UNIT"].IsEmpowered);
            }
            Assert.Equal(4, game.State.CardObjects["UNIT"].Power); Assert.Equal(1, Draws(game));
            await game.Act("P1", new TapRuneCommand("MEL-RUNE-8"));
            var mana = game.State.RunePools["P1"].Mana;
            await game.Act("P1", new PlayCardCommand("DEBUFF", "OGN·095/298", ["ENEMY"]));
            Assert.Equal(mana - 1, game.State.RunePools["P1"].Mana); Assert.Equal(0, game.State.RunePools["P1"].TotalPower);
            await game.Top(); Assert.Equal(empower ? 3 : 4, game.State.CardObjects["ENEMY"].Power);
            var modifier = Assert.Single(game.State.CardObjects["ENEMY"].UntilEndOfTurnPowerModifiers);
            Assert.Equal(empower ? -2 : -1, modifier.RequestedPowerDelta);
            if (empower)
            {
                var captured = Assert.IsType<ChosenNegativeMightContext>(modifier.ChosenNegativeMight);
                Assert.Equal(-1, captured.OriginalDelta); var contributor = Assert.Single(captured.Contributors);
                Assert.Equal("UNIT", contributor.Source.ObjectId); Assert.Equal(generation, contributor.Source.Generation);
                Assert.Equal(No, contributor.CardNo); Assert.True(contributor.WasEmpowered);
            }
            else Assert.Null(modifier.ChosenNegativeMight);
            Assert.Equal(2, Draws(game)); // One entry draw, then OGN095's own printed draw.
            Assert.Equal(generation, game.State.CardObjects["UNIT"].ObjectGeneration);
            Assert.Equal(empower, game.State.CardObjects["UNIT"].IsEmpowered);
            Assert.Empty(game.State.StackItems); Assert.Empty(game.State.TriggerQueue); await game.Replay();
        }
        finally { await Save("physical-play-empower-" + empower, initial, game); }
    }

    [Fact]
    public async Task AnInsufficientManaIntentStaysRejectedAfterARealRuneSuppliesTheThirdMana()
    {
        var initial = Position(); var game = new Driver(initial);
        try
        {
            await Play(game, 6); await game.Top(); Assert.Equal(2, game.State.RunePools["P1"].Mana);
            var generation = game.State.CardObjects["UNIT"].ObjectGeneration;
            var rejected = await Reject(game, ErrorCodes.InsufficientCost); var count = game.Commands().Length;
            Assert.Equal(2, game.State.RunePools["P1"].Mana); Assert.False(game.State.CardObjects["UNIT"].IsEmpowered);
            await game.Act("P1", new TapRuneCommand("MEL-RUNE-7")); Assert.Equal(3, game.State.RunePools["P1"].Mana);
            Assert.Equal(0, game.State.RunePools["P1"].TotalPower); await game.ActRetained(rejected, false);
            Assert.Equal(count + 1, game.Commands().Length); Assert.Equal(ErrorCodes.InsufficientCost, game.LastRetainedResult!.ErrorCode);
            Assert.Equal(3, game.State.RunePools["P1"].Mana); Assert.Empty(game.State.StackItems);
            await game.Act("P1", Activate()); Assert.Equal(0, game.State.RunePools["P1"].Mana);
            Assert.Equal(3, Assert.Single(game.State.StackItems).EmpowerActivation!.PaidMana);
            await game.Top(); Assert.True(game.State.CardObjects["UNIT"].IsEmpowered);
            Assert.Equal(generation, game.State.CardObjects["UNIT"].ObjectGeneration); Assert.Equal(1, Draws(game)); await game.Replay();
        }
        finally { await Save("retained-insufficient-physical-mana", initial, game); }
    }

    [Fact]
    public async Task NativeEmpowerCannotPreemptItsAlreadyPendingEntryDraw()
    {
        var initial = Position(); var game = new Driver(initial);
        try
        {
            await Play(game, 7); var entry = Assert.Single(game.State.StackItems).StackItemId;
            await Reject(game, ErrorCodes.InvalidTarget);
            Assert.Equal(entry, Assert.Single(game.State.StackItems).StackItemId); Assert.Equal(0, Draws(game));
            Assert.Equal(3, game.State.RunePools["P1"].Mana); Assert.False(game.State.CardObjects["UNIT"].IsEmpowered);
            await game.Top(); Assert.Equal(1, Draws(game)); Assert.Empty(game.State.StackItems);
            await game.Act("P1", Activate()); Assert.Equal(0, game.State.RunePools["P1"].Mana);
            await game.Top(); Assert.True(game.State.CardObjects["UNIT"].IsEmpowered); Assert.Equal(1, Draws(game)); await game.Replay();
        }
        finally { await Save("entry-draw-before-native-empower", initial, game); }
    }

    [Theory]
    [InlineData(1, 2, -3, 2)] [InlineData(2, 3, -4, 1)]
    public async Task ActuallyPaidSongInstancesEachContributeTheirOwnChosenNegativeMightReplacement(
        int copies, int contributors, int requestedDelta, int expectedPower)
    {
        var initial = Position(10); var cards = initial.CardObjects.ToDictionary();
        var locations = initial.ObjectLocations.ToDictionary(); var songs = Enumerable.Range(1, copies).Select(i => "MEL-SONG-" + i).ToArray();
        foreach (var id in songs)
        {
            cards[id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, "SFD·059/221", "P1");
            locations[id] = new("P1", "BASE");
        }
        // Songs already exist in this midgame fixture. Text copies and the
        // host's empowered status are acquired only through actual commands.
        initial = initial with { CardObjects = cards, ObjectLocations = locations,
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) { ["P1"] = initial.PlayerZones["P1"] with {
                Base = [..initial.PlayerZones["P1"].Base, ..songs] } } };
        var game = new Driver(initial);
        try
        {
            await Play(game, 7); await game.Top(); await game.Act("P1", Activate()); await game.Top();
            Assert.True(game.State.CardObjects["UNIT"].IsEmpowered); Assert.Equal(0, game.State.RunePools["P1"].Mana);
            var generation = game.State.CardObjects["UNIT"].ObjectGeneration;
            for (var i = 0; i < copies; i++)
            {
                var rune = "MEL-RUNE-" + (8 + i);
                await game.Act("P1", new TapRuneCommand(rune)); await game.Act("P1", new RecycleRuneCommand(rune));
                Assert.Equal(1, game.State.RunePools["P1"].Mana); Assert.Equal(1, game.State.RunePools["P1"].PowerByTrait[RuneTrait.Green]);
                await game.Act("P1", new AssembleEquipmentCommand(songs[i], "UNIT", ["ASSEMBLE_GREEN"]));
                Assert.Equal(0, game.State.RunePools["P1"].Mana); Assert.Equal(0, game.State.RunePools["P1"].TotalPower);
                await game.Top(); var gear = game.State.CardObjects[songs[i]];
                Assert.Equal("UNIT", gear.AttachedToObjectId); Assert.Equal(No, gear.AttachedRuleText!.CardNo);
                Assert.Equal(generation, gear.AttachedRuleText.Unit.Generation); Assert.True(gear.AttachmentRevision > 0);
                Assert.Equal(1, Draws(game)); Assert.Equal(4, game.State.CardObjects["UNIT"].Power);
            }
            await game.Act("P1", new TapRuneCommand("MEL-RUNE-10"));
            await game.Act("P1", new PlayCardCommand("DEBUFF", "OGN·095/298", ["ENEMY"])); await game.Top();
            Assert.Equal(0, game.State.RunePools["P1"].Mana); Assert.Equal(0, game.State.RunePools["P1"].TotalPower);
            Assert.Equal(expectedPower, game.State.CardObjects["ENEMY"].Power);
            var modifier = Assert.Single(game.State.CardObjects["ENEMY"].UntilEndOfTurnPowerModifiers);
            Assert.Equal(requestedDelta, modifier.RequestedPowerDelta);
            var context = Assert.IsType<ChosenNegativeMightContext>(modifier.ChosenNegativeMight);
            Assert.Equal(-1, context.OriginalDelta); Assert.Equal(contributors, context.Contributors.Count);
            Assert.Equal(contributors, context.Contributors.Select(c => c.InstanceId).Distinct(StringComparer.Ordinal).Count());
            Assert.All(context.Contributors, c => {
                Assert.Equal(No, c.CardNo); Assert.True(c.WasEmpowered); Assert.Equal("P1", c.ControllerId);
                Assert.Equal("UNIT", c.Source.ObjectId); Assert.Equal(generation, c.Source.Generation);
            });
            Assert.Single(context.Contributors, c => c.Origin is null);
            Assert.Equal(songs, context.Contributors.Where(c => c.Origin is not null).Select(c => c.Origin!.Equipment.ObjectId).Order());
            Assert.Equal(2, Draws(game)); Assert.True(game.State.CardObjects["UNIT"].IsEmpowered);
            Assert.Empty(game.State.StackItems); Assert.Empty(game.State.TriggerQueue); await game.Replay();
        }
        finally { await Save("physical-paid-song-instances-" + copies, initial, game); }
    }

    private static MatchState Position(int runeCount = 8)
    {
        var state = VenUnitEntryBasicTests.Position(No); var cards = state.CardObjects.ToDictionary();
        var locations = state.ObjectLocations.ToDictionary(); var runes = Enumerable.Range(1, runeCount).Select(i => "MEL-RUNE-" + i).ToArray();
        foreach (var id in runes)
        {
            cards[id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, id == "MEL-RUNE-1" ? "UNL-R03" : "UNL-R02", "P1");
            locations[id] = new("P1", "BASE");
        }
        cards["ENEMY"] = cards["ENEMY"] with { Power = 5 };
        cards["DEBUFF"] = OfficialGarenConquestBattlefieldCountTests.Printed("DEBUFF", "OGN·095/298", "P1"); locations["DEBUFF"] = new("P1", "HAND");
        return state with { RoomId = "VEN069-PHYSICAL-TURN-FIVE", CardObjects = cards, ObjectLocations = locations,
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) { ["P1"] = state.PlayerZones["P1"] with {
                Base = [..state.PlayerZones["P1"].Base, ..runes], Hand = [..state.PlayerZones["P1"].Hand, "DEBUFF"] } },
            RunePools = new Dictionary<string, RunePool> { ["P1"] = new(0, 0), ["P2"] = new(0, 0) } };
    }
    private static async Task Play(Driver game, int taps)
    {
        await game.TapRunes("P1", Enumerable.Range(1, taps).Select(i => "MEL-RUNE-" + i));
        Assert.Equal(taps, game.State.RunePools["P1"].Mana); Assert.Equal(0, game.State.RunePools["P1"].TotalPower);
        await game.Act("P1", new RecycleRuneCommand("MEL-RUNE-1")); Assert.Equal(1, game.State.RunePools["P1"].PowerByTrait[RuneTrait.Blue]);
        await game.Act("P1", new PlayCardCommand("UNIT", No, []));
        Assert.Equal(taps - 4, game.State.RunePools["P1"].Mana); Assert.Equal(0, game.State.RunePools["P1"].TotalPower);
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Base); Assert.False(game.State.CardObjects["UNIT"].IsEmpowered);
    }
    private static ActivateAbilityCommand Activate() => new("UNIT", "EMPOWER:0:0", []);
    private static int Draws(Driver game) => game.Events().Count(e => e.Event.Kind == "CARD_DRAWN"
        && Equals(e.Event.Payload.GetValueOrDefault("playerId"), "P1"));
    private static async Task<ConformanceCommand> Reject(Driver game, string code)
    {
        var node = JsonSerializer.SerializeToNode(Activate(), Json)!.AsObject();
        var command = new ConformanceCommand("P1", "ven069-rejected-" + game.Commands().Length, JsonSerializer.SerializeToElement(node, Json));
        await game.ActRetained(command, false); Assert.Equal(code, game.LastRetainedResult!.ErrorCode); return command;
    }
    private static async Task Save(string label, MatchState initial, Driver game)
    {
        if (Environment.GetEnvironmentVariable("RIFTBOUND_VEN069_EVIDENCE") is not { Length: > 0 } root) return;
        Directory.CreateDirectory(root); await File.WriteAllTextAsync(Path.Combine(root, label + ".json"), JsonSerializer.Serialize(new {
            scope = "Exact base CN37762 in an explicit turn-five fixture, zero starting pools and eight/ten physical runes. Actual play/entry draw, mana-only native Empower, zero/one/two actually paid text copies and chosen negative Might; Songs pre-exist unattached. Per-command hidden/recovery/dedup/original replay checks remain enabled. No ordinary opening, alternate-printing or whole-card/L2/L3 claim.",
            initial, final = game.State, commands = game.Commands(), events = game.Events(), ruleInputs = RuleInputIdentity.Current }, Json));
    }
}
