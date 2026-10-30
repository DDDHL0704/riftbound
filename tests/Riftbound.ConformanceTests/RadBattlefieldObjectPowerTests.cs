using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class RadBattlefieldObjectPowerTests
{
    private const string Ranger = "RAD·128/167";
    private const string Kind = StaticAuraKinds.SourceBattlefieldFriendlyObjectThresholdPower;

    [Fact]
    public async Task TheOfficialPrintingPreservesItsWholeTextAndPrintedCharacteristics()
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, c => c.CardNo == Ranger);
        Assert.Equal(39633, card.Id);
        Assert.Equal("坚定游骑", card.CardName);
        Assert.Equal(4, card.Energy);
        Assert.Null(card.ReturnEnergy);
        Assert.Equal(3, card.Power);
        Assert.Equal("{{壁垒}}（必须先为我分配战斗伤害。）\n只要我所在的战场有不少于两个其他友方单位和/或装备，我便具有{{S}}+2。", card.CardEffect);
        var aura = Assert.Single(RuleTextParser.Parse(card).StaticAuras);
        Assert.Equal(Kind, aura.Kind);
        Assert.Equal(2, aura.RequiredParticipantCount);
        Assert.Equal(2, aura.PowerDeltaPerParticipant);
        Assert.Equal(StaticAuraParticipantScopes.SameBattlefieldOtherFriendlyPublicUnitsOrEquipment, aura.ParticipantScope);
    }

    [Theory]
    [InlineData("两", 2)]
    [InlineData("三", 3)]
    [InlineData("4", 4)]
    public void TheParserPreservesTheThresholdAndFixedBonus(string text, int threshold)
    {
        var aura = Assert.Single(StaticAuraParser.Parse($"只要我所在的战场有不少于{text}个其他友方单位和/或装备，我便具有{{{{S}}}}+4。"));
        Assert.Equal(Kind, aura.Kind);
        Assert.Equal(threshold, aura.RequiredParticipantCount);
        Assert.Equal(4, aura.PowerDeltaPerParticipant);
    }

    [Theory]
    [InlineData(0, 0, 3)]
    [InlineData(1, 0, 3)]
    [InlineData(2, 0, 5)]
    [InlineData(3, 0, 5)]
    [InlineData(1, 1, 5)]
    [InlineData(0, 2, 9)]
    public void OtherUnitsAndEquipmentContributeOnceAndTheBonusDoesNotScalePastTheThreshold(int units, int equipment, int power)
    {
        var state = Position(units, equipment);
        Assert.Equal(power, Power(state));
        Assert.Equal(3, state.CardObjects["UNIT"].Power);
        var aura = Auras(state);
        if (units + equipment < 2) Assert.Empty(aura);
        else
        {
            var effect = Assert.Single(aura);
            Assert.Equal(2, effect.PowerDelta);
            Assert.Equal(units + equipment, effect.ParticipantObjectIds!.Count);
            Assert.DoesNotContain("UNIT", effect.ParticipantObjectIds);
        }
        ArmamentMightTests.PublicRecovery(state);
    }

    [Theory]
    [InlineData("BASE", 3)]
    [InlineData("BATTLEFIELD:BF", 5)]
    public async Task ARealUnitPlayPaysItsCostAndImmediatelyUsesTheCurrentBattlefield(string destination, int power)
    {
        var state = RadUnitEffectTestFixture.Place(Position(2, 0), "UNIT", "P1", "HAND");
        var game = new Driver(state);
        await game.Act("P1", new PlayCardCommand("UNIT", Ranger, [], Destination: destination));
        Assert.Equal(16, game.State.RunePools["P1"].Mana);
        Assert.Equal(20, game.State.RunePools["P1"].TotalPower);
        Assert.True(game.State.CardObjects["UNIT"].IsExhausted);
        Assert.Contains(CardCombatKeywordNames.Bulwark, game.State.CardObjects["UNIT"].Tags);
        Assert.Equal(power, Power(game.State));
        Assert.Empty(game.State.StackItems);
        Assert.Null(game.State.PendingCardChoice);
        await game.Replay();
    }

    [Theory]
    [InlineData("BASE")]
    [InlineData("HAND")]
    [InlineData("GRAVEYARD")]
    [InlineData("other-battlefield")]
    [InlineData("opponent")]
    [InlineData("hidden")]
    public void ObjectsThatAreNotPublicFriendlyParticipantsHereDoNotMeetTheThreshold(string reason)
    {
        var state = Position(2, 0);
        if (reason is "BASE" or "HAND" or "GRAVEYARD")
            state = RadUnitEffectTestFixture.Place(state, "A2", "P1", reason);
        if (reason == "other-battlefield") state = RadUnitEffectTestFixture.Place(state, "A2", "P1", "BATTLEFIELD", "OTHER");
        if (reason == "opponent") state = RadUnitEffectTestFixture.Place(state, "A2", "P2", "BATTLEFIELD", "BF");
        if (reason == "hidden") state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["A2"] = state.CardObjects["A2"] with { IsFaceDown = true } } };
        Assert.Equal(3, Power(state));
        Assert.Empty(Auras(state));
    }

    [Theory]
    [InlineData("BASE")]
    [InlineData("HAND")]
    [InlineData("GRAVEYARD")]
    public void TheSourceMustOccupyABattlefieldAndDoesNotCountFriendsAtBase(string zone)
    {
        var state = RadUnitEffectTestFixture.Place(Position(2, 0), "UNIT", "P1", zone);
        if (zone == "BASE")
            foreach (var id in new[] { "A1", "A2" }) state = RadUnitEffectTestFixture.Place(state, id, "P1", "BASE");
        Assert.Empty(Auras(state));
        if (zone == "BASE") Assert.Equal(3, Power(state));
        ArmamentMightTests.PublicRecovery(state);
    }

    [Fact]
    public void TheCurrentControllerCountsFriendsRegardlessOfPhysicalOwnership()
    {
        var state = Position(2, 0);
        foreach (var id in new[] { "UNIT", "A1", "A2" })
            state = RadUnitEffectTestFixture.Place(state, id, "P2", "BATTLEFIELD", "BF");
        Assert.Equal(5, Power(state, "P2"));
        Assert.Equal("P1", state.CardObjects["UNIT"].OwnerId);
        ArmamentMightTests.PublicRecovery(state);
        state = RadUnitEffectTestFixture.Place(state, "A2", "P1", "BATTLEFIELD", "BF");
        Assert.Equal(3, Power(state, "P2"));
        ArmamentMightTests.PublicRecovery(state);
    }

    [Fact]
    public async Task ReturningAParticipantRemovesTheBonusWhenTheReturnResolves()
    {
        var state = HandDiscardObserverTests.Put(Position(2, 0), "RETURN", "OGN·104/298", "P1", "HAND");
        var game = new Driver(state);
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["A2"]));
        Assert.Equal(5, Power(game.State));
        await game.Top();
        Assert.Contains("A2", game.State.PlayerZones["P1"].Hand);
        Assert.Equal(3, Power(game.State));
        Assert.Empty(Auras(game.State));
        await game.Replay();
    }

    [Fact]
    public async Task ActuallyCopiedPrintedTextAddsOneFixedBonusAndLosesItWhenTheArmamentLeaves()
    {
        var state = (await AttachedRuleTextTests.Attach(AttachedRuleTextTests.Position(Ranger, 0))).State;
        foreach (var id in new[] { "HOST", "GEAR" })
            state = RadUnitEffectTestFixture.Place(state, id, "P1", "BATTLEFIELD", "F");
        for (var i = 1; i <= 2; i++)
            state = RadUnitEffectTestFixture.Place(HandDiscardObserverTests.Put(state, "A" + i, "OGN·219/298", "P1", "HAND"),
                "A" + i, "P1", "BATTLEFIELD", "F");
        Assert.Equal(7, ArmamentMightTests.Might(state));
        ArmamentMightTests.PublicRecovery(state);
        state = HandDiscardObserverTests.Put(state, "RETURN", "SFD·135/221", "P1", "HAND");
        var game = new Driver(state);
        await game.Act("P1", new PlayCardCommand("RETURN", "SFD·135/221", ["GEAR"]));
        Assert.Equal(7, ArmamentMightTests.Might(game.State));
        await game.Top();
        Assert.Contains("GEAR", game.State.PlayerZones["P1"].Hand);
        Assert.Null(game.State.CardObjects["GEAR"].AttachedRuleText);
        Assert.Equal(5, ArmamentMightTests.Might(game.State));
        await game.Replay();
    }

    [Theory]
    [InlineData("mana")]
    [InlineData("graveyard")]
    [InlineData("target")]
    [InlineData("enemy-field")]
    public async Task IllegalPlaysPreserveStateAndDoNotCreateTheAura(string reason)
    {
        var state = RadUnitEffectTestFixture.Place(Position(2, 0), "UNIT", "P1", reason == "graveyard" ? "GRAVEYARD" : "HAND");
        if (reason == "mana") state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(3, 20) } };
        var command = new PlayCardCommand("UNIT", Ranger, reason == "target" ? ["A1"] : [],
            Destination: reason == "enemy-field" ? "BATTLEFIELD:OTHER" : "BATTLEFIELD:BF");
        var hash = MatchStateHasher.Hash(state);
        var snapshots = JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(state));
        var prompts = JsonSerializer.Serialize(ResolutionResult.BuildPrompts(state));
        var result = await new CoreRuleEngine().ResolveAsync(state, new("illegal-ranger", "P1", command.CmdType), command, default);
        Assert.False(result.Accepted);
        Assert.Empty(result.Events);
        Assert.Equal(hash, MatchStateHasher.Hash(result.State));
        Assert.Equal(snapshots, JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(result.State)));
        Assert.Equal(prompts, JsonSerializer.Serialize(ResolutionResult.BuildPrompts(result.State)));
        Assert.Empty(Auras(result.State));
    }

    private static MatchState Position(int units, int equipment)
    {
        var state = VenUnitEntryBasicTests.Position(Ranger);
        state = RadUnitEffectTestFixture.Place(state, "UNIT", "P1", "BATTLEFIELD", "BF");
        for (var i = 1; i <= units; i++)
            state = RadUnitEffectTestFixture.Place(HandDiscardObserverTests.Put(state, "A" + i, "OGN·219/298", "P1", "HAND"),
                "A" + i, "P1", "BATTLEFIELD", "BF");
        for (var i = 1; i <= equipment; i++)
        {
            var id = "E" + i;
            state = RadUnitEffectTestFixture.Place(HandDiscardObserverTests.Put(state, id, "SFD·022/221", "P1", "HAND"),
                id, "P1", "BATTLEFIELD", "BF");
            state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                [id] = state.CardObjects[id] with { AttachedToObjectId = units > 0 ? "A1" : "UNIT", AttachmentRevision = 1 } } };
        }
        return state;
    }

    private static int Power(MatchState state, string controller = "P1")
        => CurrentPowerTargetTests.Snapshot(state).GetProperty("players").GetProperty(controller)
            .GetProperty("objects").GetProperty("UNIT").GetProperty("effectivePower").GetInt32();

    private static ContinuousEffectState[] Auras(MatchState state)
        => state.ContinuousEffects.Where(e => e.EffectKind == Kind).ToArray();
}
