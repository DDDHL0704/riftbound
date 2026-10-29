using System.Reflection;
using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class VenConquestSourceHeroesTests
{
    private static MatchState Position(string no, bool copied = false)
    {
        var state = ConquestTokenTriggerTests.Position(no);
        state = state with {
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["UNIT"] = OfficialGarenConquestBattlefieldCountTests.Printed("UNIT", no, "P1") },
            RunePools = new Dictionary<string, RunePool> {
                ["P1"] = new(20, 20, new Dictionary<string, int> { [RuneTrait.Red] = 4, [RuneTrait.Orange] = 4 }),
                ["P2"] = new(20, 20) } };
        if (!copied) return state;
        return state with {
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["GEAR"] = new("GEAR", cardNo: "SFD·059/221", ownerId: "P1", controllerId: "P1",
                    tags: [CardObjectTags.EquipmentCard, CardEquipmentKeywordNames.Weapon], attachedToObjectId: "UNIT") {
                    AttachmentRevision = 1, AttachedRuleText = new(new("UNIT", 0), no, 1, state.Tick, 0) } },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P1"] = state.PlayerZones["P1"] with { Base = [..state.PlayerZones["P1"].Base, "GEAR"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["GEAR"] = new("P1", "BASE") } };
    }

    private static async Task Conquer(Driver game)
    {
        await game.Act("P1", new MoveUnitCommand("UNIT", "BASE", "BATTLEFIELD:BF", []));
        await game.Act("P1", new PassFocusCommand()); await game.Act("P2", new PassFocusCommand());
    }

    [Theory]
    [InlineData("OGN·039/298", false)] [InlineData("OGN·039a/298", false)] [InlineData("VEN·SP1", false)]
    [InlineData("OGN·039/298", true)] [InlineData("OGN·039a/298", true)] [InlineData("VEN·SP1", true)]
    public async Task StalwartPrintingsPayTheRealHasteCostAndEnterReadyOnlyWhenChosen(string no, bool haste)
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, c => c.CardNo == no);
        Assert.Equal("九死一生", card.SubTitle); Assert.Equal(4, card.Energy); Assert.Equal(4, card.Power);
        Assert.Null(card.ReturnEnergy); Assert.Equal(0, PrintedPowerCostRules.ForCard(no).Amount);
        Assert.Contains("当我征服一处战场时，抽一张牌", card.CardEffect);
        var state = VenUnitEntryBasicTests.Position(no) with { RunePools = new Dictionary<string, RunePool> {
            ["P1"] = new(5, 0, new Dictionary<string, int> { [RuneTrait.Red] = 1 }), ["P2"] = RunePool.Empty } };
        var game = new Driver(state);
        await game.Act("P1", new PlayCardCommand("UNIT", no, [], OptionalCosts: haste ? ["HASTE_READY"] : [], Destination: "BASE"));
        Assert.Equal(!haste, game.State.CardObjects["UNIT"].IsExhausted);
        Assert.Equal(haste ? 0 : 1, game.State.RunePools["P1"].Mana);
        Assert.Equal(haste ? 0 : 1, game.State.RunePools["P1"].PowerByTrait.GetValueOrDefault(RuneTrait.Red));
        Assert.Empty(game.State.StackItems); await game.Replay();
    }

    [Theory]
    [InlineData("OGN·039/298")] [InlineData("OGN·039a/298")] [InlineData("VEN·SP1")]
    public async Task UnpaidHasteIsRejectedWithoutSpendingThePrintedCost(string no)
    {
        var state = VenUnitEntryBasicTests.Position(no) with { RunePools = new Dictionary<string, RunePool> {
            ["P1"] = new(4, 0), ["P2"] = RunePool.Empty } };
        var cmd = new PlayCardCommand("UNIT", no, [], OptionalCosts: ["HASTE_READY"], Destination: "BASE");
        var rejected = await new CoreRuleEngine().ResolveAsync(state, new("unpaid-haste", "P1", cmd.CmdType), cmd, default);
        Assert.False(rejected.Accepted); Assert.Empty(rejected.Events);
        Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(rejected.State));
    }

    [Theory]
    [InlineData("OGN·039/298")] [InlineData("OGN·039a/298")] [InlineData("VEN·SP1")]
    public async Task ConquestDrawWaitsForTwoResponsesAndRemainsHiddenFromTheOpponent(string no)
    {
        var game = new Driver(Position(no)); await Conquer(game);
        Assert.Empty(game.State.PlayerZones["P1"].Hand);
        Assert.Single(game.State.StackItems, i => i.ConquestSource?.Kind == TriggerKinds.UnitConquestDrawOne);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "CARD_DRAWN");
        await game.Act(game.State.PriorityPlayerId!, new PassPriorityCommand());
        Assert.Empty(game.State.PlayerZones["P1"].Hand);
        await game.Act(game.State.PriorityPlayerId!, new PassPriorityCommand());
        Assert.Equal(new[] { "DRAW1" }, game.State.PlayerZones["P1"].Hand);
        Assert.DoesNotContain("DRAW1", JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(game.State)["P2"]));
        await game.Replay();
    }

    [Theory]
    [InlineData("OGN·039/298")] [InlineData("VEN·SP1")]
    public async Task ReturningTheSourceDoesNotCancelTheDrawOrExposeItsHiddenIdentity(string no)
    {
        var game = new Driver(AttackDamageDistributionTests.Spell(Position(no), "BOUNCE", "OGN·104/298", "P1"));
        await Conquer(game); await game.Act("P1", new PlayCardCommand("BOUNCE", "OGN·104/298", ["UNIT"]));
        await game.Top(); Assert.Contains("UNIT", game.State.PlayerZones["P1"].Hand);
        Assert.Single(game.State.StackItems);
        var stack = JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(game.State)["P2"].Stack);
        Assert.DoesNotContain("\"UNIT\"", stack); Assert.DoesNotContain("-UNIT-", stack);
        var before = game.Events().Length; await game.Top();
        Assert.DoesNotContain("\"UNIT\"", JsonSerializer.Serialize(game.Events().Skip(before)));
        Assert.Contains("DRAW1", game.State.PlayerZones["P1"].Hand); await game.Replay();
    }

    [Fact]
    public async Task CopiedConquestTextCreatesTwoIndependentDrawSkills()
    {
        var game = new Driver(Position("VEN·SP1", copied: true));
        await game.Act("P1", new MoveUnitCommand("UNIT", "BASE", "BATTLEFIELD:BF", []));
        await game.Act("P1", new PassFocusCommand()); await game.Act("P2", new PassFocusCommand());
        var earned = game.State.TriggerQueue.Where(t => t.ConquestSource is not null).ToArray(); Assert.Equal(2, earned.Length);
        await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: earned.Select(t => t.TriggerId).ToArray()));
        Assert.Equal(2, game.State.StackItems.Count); Assert.Contains(game.State.StackItems, i => i.ConquestSource?.Origin is not null);
        await game.Top(); Assert.Single(game.State.PlayerZones["P1"].Hand);
        await game.Top(); Assert.Equal(new[] { "DRAW1", "DRAW2" }, game.State.PlayerZones["P1"].Hand); await game.Replay();
    }

    [Fact]
    public async Task HoldActivationOfConquestAlsoUsesARespondableDrawSkill()
    {
        var state = OfficialHoldSequenceTests.AddUnit(OfficialHoldSequenceTests.State("OGN·286/298"), "UNIT", "VEN·SP1", "F", "P1");
        var game = new Driver(state); await game.Act("P1", new PassPriorityCommand());
        Assert.Single(game.State.StackItems, i => i.HeldContext?.Kind == "ACTIVATE_CONQUEST");
        await game.Top(); Assert.Single(game.State.StackItems, i => i.ConquestSource?.Reason == "BATTLEFIELD_HELD");
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "CARD_DRAWN");
        await game.Top();
        // The hold skill draws once, then the normal turn-start draw resumes.
        Assert.Equal(2, game.Events().Count(e => e.Event.Kind == "CARD_DRAWN"));
        Assert.Single(game.Events(), e => e.Event.Kind == "UNIT_TRIGGER_RESOLVED"
            && Equals(e.Event.Payload.GetValueOrDefault("trigger"), TriggerKinds.UnitConquestDrawOne));
        await game.Replay();
    }

    [Theory]
    [InlineData("OGN·164/298")] [InlineData("OGN·164a/298")]
    [InlineData("SFD·232/221")] [InlineData("SFD·232*/221")] [InlineData("VEN·SP4")]
    public async Task BossPrintingsWaitForResponsesBeforeGrantingTheirConquestBoon(string no)
    {
        var game = new Driver(Position(no)); var power = game.State.CardObjects["UNIT"].Power;
        await Conquer(game); Assert.DoesNotContain(CardObjectTags.Boon, game.State.CardObjects["UNIT"].Tags);
        Assert.Single(game.State.StackItems, i => i.ConquestSource?.Kind == TriggerKinds.UnitConquestGrantSelfBoon);
        await game.Top(); Assert.Contains(CardObjectTags.Boon, game.State.CardObjects["UNIT"].Tags);
        Assert.Equal(power + 1, game.State.CardObjects["UNIT"].Power); await game.Replay();
    }

    [Theory]
    [InlineData("OGN·164/298")] [InlineData("OGN·164a/298")]
    [InlineData("SFD·232/221")] [InlineData("SFD·232*/221")] [InlineData("VEN·SP4")]
    public async Task BossConquestBoonDoesNotFollowItsSourceIntoHand(string no)
    {
        var game = new Driver(AttackDamageDistributionTests.Spell(Position(no), "BOUNCE", "OGN·104/298", "P1"));
        await Conquer(game); await game.Act("P1", new PlayCardCommand("BOUNCE", "OGN·104/298", ["UNIT"])); await game.Top();
        var power = game.State.CardObjects["UNIT"].Power; await game.Top();
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Hand);
        Assert.DoesNotContain(CardObjectTags.Boon, game.State.CardObjects["UNIT"].Tags);
        Assert.Equal(power, game.State.CardObjects["UNIT"].Power); await game.Replay();
    }

    [Fact]
    public async Task RecoveryRejectsForgedDrawAndBoonConditions()
    {
        foreach (var no in new[] { "VEN·SP1", "OGN·164/298" })
        {
            var game = new Driver(Position(no)); await Conquer(game);
            var state = game.State; var item = Assert.Single(state.StackItems); var c = item.ConquestSource!;
            foreach (var forged in new[] { c with { SourceWasEmpowered = true }, c with { ConfirmedPlayIds = ["made-up"] },
                c with { Kind = TriggerKinds.UnitConquestEmpoweredGainScore }, c with { CapturedTick = state.Tick + 1 } })
                Assert.Contains("invalid conquest source stack", OfficialInsightAndSpellLockTests.Errors(state with {
                    StackItems = [item with { ConquestSource = forged }] }));
        }
    }

    [Theory]
    [InlineData("VEN·SP1", "OGN·247/298", "red", "blue")]
    [InlineData("VEN·SP4", "OGN·269/298", "orange", "yellow")]
    public async Task ChinaStandardDeckAcceptsTheVenHeroPrinting(string printing, string legend, string firstColor, string secondColor)
    {
        const OfficialDeckFormat format = OfficialDeckFormat.ChinaStandard20260724;
        var catalog = await OfficialCardCatalog.LoadRuntimeAsync();
        var allowed = catalog with { Cards = catalog.Cards.Where(c => ChinaStandardBanList.IsAllowed(c, format)).ToArray() };
        string Rune(string color) => allowed.Cards.Where(c => c.CardCategory == "rune" && c.CardColorList.Contains(color))
            .OrderBy(c => c.CardNo, StringComparer.Ordinal).First().CardNo;
        var builder = typeof(PreconstructedDeckCatalog).GetMethod("BuildLowCurveDeck", BindingFlags.NonPublic | BindingFlags.Static)!;
        var deck = (OfficialDecklist)builder.Invoke(null, [allowed, allowed.Cards.ToDictionary(c => c.CardNo, StringComparer.Ordinal),
            legend, printing, Array.Empty<string>(),
            new[] { "OGN·277/298", "OGN·278/298", "OGN·294/298" },
            Enumerable.Repeat(Rune(firstColor), 6).Concat(Enumerable.Repeat(Rune(secondColor), 6)).ToArray()])!;
        var validation = OfficialDeckValidator.Validate(deck, catalog, format);
        Assert.True(validation.IsValid, string.Join("; ", validation.Errors));
    }
}
