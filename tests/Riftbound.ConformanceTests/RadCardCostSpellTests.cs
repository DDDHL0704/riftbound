using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class RadCardCostSpellTests
{
    private const string Silence = "RAD·041/167";
    private const string Toss = "RAD·056/167";

    [Theory]
    [InlineData(Silence, 39534, 1, 2)]
    [InlineData(Toss, 39552, 3, 1)]
    public async Task EachPrintingPreservesItsOfficialCostsAndCompleteText(string no, long id, int mana, int power)
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, c => c.CardNo == no);
        Assert.Equal(id, card.Id);
        Assert.Equal(mana, card.Energy);
        Assert.Equal(power, card.ReturnEnergy);
        Assert.Equal(no == Silence ? "缄默术" : "随手投掷", card.CardName);
        Assert.Equal(no == Silence
            ? "{{反应}}（可在任意时机打出，甚至先于其他法术和技能的结算。）\n无效化一个符能费用不低于{{A}}{{A}}的法术。"
            : "{{迅捷}}（可在你的回合或法术对决中打出。）\n选择一件友方装备。对战场上的一名单位造成等同于该装备法力费用的伤害。", card.CardEffect);
    }

    public static IEnumerable<object[]> CounterableSpells() => new[] { "OGN·022/298", "OGN·233/298", "OGN·122/298" }
        .SelectMany(no => new[] { "P1", "P2" }.Select(actor => new object[] { no, actor }));

    [Theory]
    [MemberData(nameof(CounterableSpells))]
    public async Task TwoOrMorePrintedPowerCanBeCounteredIncludingAnOwnSpell(string threat, string actor)
    {
        var initial = HandDiscardObserverTests.Put(VenUnitEntryBasicTests.Position(Silence), "THREAT", threat, actor, "HAND")
            with { TurnPlayerId = actor, ActivePlayerId = actor };
        var game = new Driver(initial);
        await game.Act(actor, new PlayCardCommand("THREAT", threat, []));
        if (actor != "P1") await game.Act(actor, new PassPriorityCommand());
        var target = Assert.Single(game.State.StackItems).StackItemId;
        Assert.Contains(CounterTargets(game.State, "P1"), id => id == target);
        var command = new PlayCardCommand("UNIT", Silence, [target]);
        var quote = new CoreRuleEngine().PreviewPlayCard(game.State, "P1", PlayCostPreviewTests.Request(game.State, command));
        Assert.True(quote.CanPay, quote.Message);
        Assert.Equal(1, quote.Cost!.Mana);
        Assert.Equal(2, quote.Cost.GenericPower + quote.Cost.PowerByTrait.Values.Sum());
        var pool = game.State.RunePools["P1"];
        await game.Act("P1", command);
        Assert.Equal(pool.Mana - 1, game.State.RunePools["P1"].Mana);
        Assert.Equal(pool.TotalPower - 2, game.State.RunePools["P1"].TotalPower);
        Assert.Equal(2, game.State.StackItems.Count);
        await game.Top();
        Assert.Empty(game.State.StackItems);
        Assert.Contains("THREAT", game.State.PlayerZones[actor].Graveyard);
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Graveyard);
        Assert.Contains("GEAR", game.State.PlayerZones["P1"].Base);
        Assert.Equal(initial.CardObjects["ALLY"].Power, game.State.CardObjects["ALLY"].Power);
        Assert.Single(game.Events(), e => e.Event.Kind == "STACK_ITEM_COUNTERED");
        await game.Replay();
    }

    [Theory]
    [InlineData("VEN·049")]
    [InlineData("OGN·144/298")]
    public async Task APrintedPowerCostBelowTwoIsNotOfferedOrAccepted(string threat)
    {
        var initial = HandDiscardObserverTests.Put(VenUnitEntryBasicTests.Position(Silence), "THREAT", threat, "P2", "HAND")
            with { TurnPlayerId = "P2", ActivePlayerId = "P2" };
        var game = new Driver(initial);
        await game.Act("P2", new PlayCardCommand("THREAT", threat, []));
        await game.Act("P2", new PassPriorityCommand());
        var target = Assert.Single(game.State.StackItems).StackItemId;
        Assert.DoesNotContain(target, CounterTargets(game.State, "P1"));
        await Reject(game.State, "P1", new PlayCardCommand("UNIT", Silence, [target]));
        await game.Replay();
    }

    [Fact]
    public async Task PayingTwoWardPowerDoesNotChangeTheThreatsPrintedPowerCost()
    {
        const string buff = "OGN·207/298";
        var initial = HandDiscardObserverTests.Put(VenUnitEntryBasicTests.Position(Silence), "THREAT", buff, "P2", "HAND");
        initial = initial with { TurnPlayerId = "P2", ActivePlayerId = "P2",
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["ALLY"] = initial.CardObjects["ALLY"] with { Tags = [CardObjectTags.UnitCard, "法盾2"] } } };
        var game = new Driver(initial);
        var command = new PlayCardCommand("THREAT", buff, ["ALLY"]);
        var quote = new CoreRuleEngine().PreviewPlayCard(game.State, "P2", PlayCostPreviewTests.Request(game.State, command));
        Assert.True(quote.CanPay, quote.Message);
        Assert.Equal(2, quote.Cost!.GenericPower + quote.Cost.PowerByTrait.Values.Sum());
        await game.Act("P2", command);
        Assert.Equal(18, game.State.RunePools["P2"].TotalPower);
        await game.Act("P2", new PassPriorityCommand());
        var target = Assert.Single(game.State.StackItems).StackItemId;
        Assert.DoesNotContain(target, CounterTargets(game.State, "P1"));
        await Reject(game.State, "P1", new PlayCardCommand("UNIT", Silence, [target]));
        await game.Top();
        Assert.Equal(initial.CardObjects["ALLY"].Power + 3, game.State.CardObjects["ALLY"].Power);
        await game.Replay();
    }

    [Fact]
    public async Task AConfirmedAbilityIsNotAStackSpellTarget()
    {
        var state = await AbilityCounterAuditTests.PendingAzir(Silence);
        var target = Assert.Single(state.StackItems).StackItemId;
        Assert.DoesNotContain(target, CounterTargets(state, "P2"));
        await Reject(state, "P2", new PlayCardCommand("RESPONSE", Silence, [target]));
    }

    public static IEnumerable<object[]> EquipmentReferences() => new[] { "OGN·040/298", "SFD·022/221", "OGN·017/298" }
        .SelectMany(no => new[] { "BASE", "BATTLEFIELD" }.Select(zone => new object[] { no, zone }));

    [Theory]
    [MemberData(nameof(EquipmentReferences))]
    public async Task TheEquipmentAtEitherLocationSuppliesItsManaCostAndRemainsInPlay(string equipment, string zone)
    {
        var initial = TossPosition(equipment, zone);
        var game = new Driver(initial);
        var command = new PlayCardCommand("UNIT", Toss, ["GEAR", "ENEMY"]);
        var quote = new CoreRuleEngine().PreviewPlayCard(initial, "P1", PlayCostPreviewTests.Request(initial, command));
        Assert.True(quote.CanPay, quote.Message);
        Assert.Equal(3, quote.Cost!.Mana);
        Assert.Equal(1, quote.Cost.GenericPower + quote.Cost.PowerByTrait.Values.Sum());
        await game.Act("P1", command);
        Assert.Equal(17, game.State.RunePools["P1"].Mana);
        Assert.Equal(19, game.State.RunePools["P1"].TotalPower);
        Assert.Equal(0, game.State.CardObjects["ENEMY"].Damage);
        await game.Top();
        Assert.Equal(initial.CardObjects["GEAR"].ManaCost, game.State.CardObjects["ENEMY"].Damage);
        Assert.Equal(initial.ObjectLocations["GEAR"], game.State.ObjectLocations["GEAR"]);
        Assert.Equal(initial.CardObjects["GEAR"].IsExhausted, game.State.CardObjects["GEAR"].IsExhausted);
        Assert.Equal(initial.CardObjects["GEAR"].AttachedToObjectId, game.State.CardObjects["GEAR"].AttachedToObjectId);
        Assert.Equal(initial.CardObjects["GEAR"].ObjectGeneration, game.State.CardObjects["GEAR"].ObjectGeneration);
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Graveyard);
        Assert.Empty(game.State.StackItems);
        await game.Replay();
    }

    [Fact]
    public async Task AZeroCostEquipmentTokenStillSuppliesInformationForDamageBonuses()
    {
        var initial = WithAnnie(TossPosition());
        Assert.True(P6TokenFactoryCatalog.TryGetByCardNo("UNL·T05", out var factory));
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["GEAR"] = factory.CreateObject("GEAR", "P1", "P1") } };
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("UNIT", Toss, ["GEAR", "ENEMY"]));
        await game.Top();
        Assert.Equal(1, game.State.CardObjects["ENEMY"].Damage);
        Assert.Contains("GEAR", game.State.PlayerZones["P1"].Base);
        await game.Replay();
    }

    [Fact]
    public async Task AnIllegalEquipmentReferenceSuppliesNoDamageEvenWithAnnie()
    {
        var initial = WithAnnie(TossPosition());
        Assert.True(P6TokenFactoryCatalog.TryGetByCardNo("UNL·T05", out var factory));
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["GEAR"] = factory.CreateObject("GEAR", "P1", "P1") } };
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("UNIT", Toss, ["GEAR", "ENEMY"]));
        await game.Act("P1", new ActivateAbilityCommand("GEAR", P4ActivatedAbilityCatalog.GoldTokenUnlResourceAbilityId, []));
        Assert.DoesNotContain("GEAR", game.State.CardObjects.Keys);
        Assert.Single(game.State.StackItems);
        await game.Top();
        Assert.Equal(0, game.State.CardObjects["ENEMY"].Damage);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "DAMAGE_APPLIED");
        await game.Replay();
    }

    [Fact]
    public async Task AFriendlyBattlefieldUnitCanBeTheDamageRecipient()
    {
        var initial = RadUnitEffectTestFixture.Place(TossPosition(), "ALLY", "P1", "BATTLEFIELD", "BF");
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("UNIT", Toss, ["GEAR", "ALLY"]));
        await game.Top();
        Assert.Equal(2, game.State.CardObjects["ALLY"].Damage);
        Assert.Equal(0, game.State.CardObjects["ENEMY"].Damage);
        await game.Replay();
    }

    public static IEnumerable<object[]> InvalidCommands() => new[] { Silence, Toss }
        .SelectMany(no => new[] { "mana", "power", "source", "graveyard", "missing", "extra" }
            .Select(reason => new object[] { no, reason }));

    [Theory]
    [MemberData(nameof(InvalidCommands))]
    public async Task IllegalResourcesSourcesAndTargetCountsLeaveTheStateUnchanged(string no, string reason)
    {
        var state = no == Toss ? TossPosition() : VenUnitEntryBasicTests.Position(no);
        if (no == Silence)
        {
            state = HandDiscardObserverTests.Put(state, "THREAT", "OGN·022/298", "P1", "HAND");
            var game = new Driver(state);
            await game.Act("P1", new PlayCardCommand("THREAT", "OGN·022/298", []));
            state = game.State;
        }
        var targets = no == Toss ? new[] { "GEAR", "ENEMY" } : [state.StackItems.Single().StackItemId];
        if (reason is "mana" or "power") state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) {
            ["P1"] = reason == "mana" ? new(0, 20) : new(20, 0) } };
        if (reason == "source") state = RadUnitEffectTestFixture.Place(state, "UNIT", "P2", "HAND");
        if (reason == "graveyard") state = RadUnitEffectTestFixture.Place(state, "UNIT", "P1", "GRAVEYARD");
        if (reason == "missing") targets = [];
        if (reason == "extra") targets = [..targets, "ALLY"];
        await Reject(state, "P1", new PlayCardCommand("UNIT", no, targets));
    }

    [Theory]
    [InlineData("enemy-equipment")]
    [InlineData("unit-reference")]
    [InlineData("hidden-equipment")]
    [InlineData("base-target")]
    [InlineData("equipment-target")]
    [InlineData("battlefield-target")]
    public async Task TossRequiresAPublicFriendlyEquipmentThenAPublicBattlefieldUnit(string reason)
    {
        var state = TossPosition();
        if (reason == "enemy-equipment") state = RadUnitEffectTestFixture.Place(state, "GEAR", "P2", "BASE");
        if (reason == "hidden-equipment") state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["GEAR"] = state.CardObjects["GEAR"] with { IsFaceDown = true } } };
        if (reason == "base-target") state = RadUnitEffectTestFixture.Place(state, "ENEMY", "P2", "BASE");
        await Reject(state, "P1", new PlayCardCommand("UNIT", Toss, [
            reason == "unit-reference" ? "ALLY" : "GEAR",
            reason == "equipment-target" ? "GEAR" : reason == "battlefield-target" ? "OTHER" : "ENEMY"]));
    }

    private static MatchState TossPosition(string equipment = "SFD·022/221", string zone = "BASE")
    {
        var state = VenUnitEntryBasicTests.Position(Toss);
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["GEAR"] = OfficialGarenConquestBattlefieldCountTests.Printed("GEAR", equipment, "P1") } };
        state = RadUnitEffectTestFixture.Place(state, "GEAR", "P1", zone, zone == "BATTLEFIELD" ? "BF" : null);
        if (zone == "BATTLEFIELD")
        {
            state = RadUnitEffectTestFixture.Place(state, "ALLY", "P1", "BATTLEFIELD", "BF");
            state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["GEAR"] = state.CardObjects["GEAR"] with { AttachedToObjectId = "ALLY" } } };
        }
        return RadUnitEffectTestFixture.Place(state, "ENEMY", "P2", "BATTLEFIELD", "OTHER");
    }

    private static MatchState WithAnnie(MatchState state)
        => RadUnitEffectTestFixture.Place(HandDiscardObserverTests.Put(state, "ANNIE", "OGS·001/024", "P1", "HAND"), "ANNIE", "P1", "BASE");

    private static IEnumerable<string> CounterTargets(MatchState state, string player)
        => (ResolutionResult.BuildPrompts(state)[player].Candidates ?? [])
            .Where(c => c.Action == CommandTypes.PlayCard && c.Sources?.Any(s => s.Id is "UNIT" or "RESPONSE") == true)
            .SelectMany(c => c.Targets ?? []).Select(c => c.Id);

    private static async Task Reject(MatchState state, string player, PlayCardCommand command)
    {
        var hash = MatchStateHasher.Hash(state);
        var snapshots = JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(state));
        var prompts = JsonSerializer.Serialize(ResolutionResult.BuildPrompts(state));
        var quote = new CoreRuleEngine().PreviewPlayCard(state, player, PlayCostPreviewTests.Request(state, command));
        Assert.False(quote.CanPay);
        Assert.Equal(hash, MatchStateHasher.Hash(state));
        var result = await new CoreRuleEngine().ResolveAsync(state, new("invalid-cost-spell", player, command.CmdType), command, default);
        Assert.False(result.Accepted);
        Assert.Empty(result.Events);
        Assert.Equal(hash, MatchStateHasher.Hash(result.State));
        Assert.Equal(snapshots, JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(result.State)));
        Assert.Equal(prompts, JsonSerializer.Serialize(ResolutionResult.BuildPrompts(result.State)));
    }
}
