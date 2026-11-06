using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed partial class VenBorrowedDependentAbilityFaqTests
{
    [Theory]
    [InlineData("ALLY")]
    [InlineData("GEAR")]
    [InlineData("UNIT")]
    [InlineData("PACK-STANDBY")]
    public async Task BorrowedPackReturnsAnotherFriendlyUnitEquipmentOrFacedownCard(string target)
    {
        var initial = BorrowedBombPosition("OGN·181/298");
        initial = RadUnitEffectTestFixture.Place(initial, "ALLY", "P1", "BATTLEFIELD", "BF");
        initial = AttackDamageDistributionTests.Spell(initial, "PACK-STANDBY", "OGN·053/298", "P1");
        var game = new Driver(initial);
        await game.Act("P1", new HideCardCommand("PACK-STANDBY", "OGN·053/298", "BATTLEFIELD:BF", ["STANDBY_A"]));
        var before = game.State;
        var id = TemplateId(before, "正面朝下的卡牌返回");
        foreach (var illegal in new[] { "HOST", "ENEMY", "BF", "GRAVE", "RETURN" })
            await RadGearTestPosition.Reject(before, "P1", new ActivateAbilityCommand("HOST", id, [illegal]));
        await RadGearTestPosition.Reject(before, "P2", new ActivateAbilityCommand("HOST", id, [target]));
        await game.Act("P1", new ActivateAbilityCommand("HOST", id, [target]));
        Assert.DoesNotContain(target, game.State.PlayerZones["P1"].Hand);
        await game.Top();
        Assert.Contains(target, game.State.PlayerZones["P1"].Hand);
        Assert.False(game.State.CardObjects[target].IsFaceDown);
        Assert.True(game.State.CardObjects[target].ObjectGeneration > before.CardObjects[target].ObjectGeneration);
        Assert.Equal(before.RunePools["P1"].Mana, game.State.RunePools["P1"].Mana);
        Assert.DoesNotContain(JsonSerializer.Serialize(target), JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(game.State)["P2"]));
        if (target == "PACK-STANDBY")
        {
            var returned = game.Events().Last(e => e.Event.Kind == "CARD_RETURNED_TO_HAND").Event;
            Assert.DoesNotContain("OGN·053/298", JsonSerializer.Serialize(returned));
        }
        await game.Replay();
    }

    private static MatchState BorrowedTeemoReturnPosition(string donor, string zone, string controller)
    {
        var state = HandDiscardObserverTests.Put(BorrowedBombPosition(donor), "TEEMO", "OGN·197/298", controller, "BASE");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["TEEMO"] = state.CardObjects["TEEMO"] with { OwnerId = "P1" } } };
        if (zone == "CHAMPION")
            return state with { PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                [controller] = state.PlayerZones[controller] with {
                    Base = state.PlayerZones[controller].Base.Where(id => id != "TEEMO").ToArray(), ChampionZone = ["TEEMO"] } },
                ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["TEEMO"] = new(controller, "CHAMPION") } };
        return RadUnitEffectTestFixture.Place(state, "TEEMO", controller, zone, zone == "BATTLEFIELD" ? "OTHER" : null);
    }

    [Theory]
    [InlineData("OGN·263/298", "CHAMPION", "P1")]
    [InlineData("OGN·263a/298", "BASE", "P1")]
    [InlineData("OGN·307/298", "BATTLEFIELD", "P1")]
    [InlineData("OGN·307*/298", "BASE", "P2")]
    [InlineData("OGN·263/298", "BATTLEFIELD", "P2")]
    public async Task BorrowedNamedReturnUsesOwnershipAcrossHeroAndFieldZones(string donor, string zone, string controller)
    {
        var initial = BorrowedTeemoReturnPosition(donor, zone, controller);
        var id = TemplateId(initial, "提莫单位放入手牌");
        foreach (var illegal in new[] { "HOST", "UNIT", "ALLY", "GEAR", "GRAVE" })
            await RadGearTestPosition.Reject(initial, "P1", new ActivateAbilityCommand("HOST", id, [illegal]));
        await RadGearTestPosition.Reject(initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) { ["P1"] = new(0, 20) } },
            "P1", new ActivateAbilityCommand("HOST", id, ["TEEMO"]));
        await RadGearTestPosition.Reject(initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["TEEMO"] = initial.CardObjects["TEEMO"] with { OwnerId = "P2" } } },
            "P1", new ActivateAbilityCommand("HOST", id, ["TEEMO"]));
        var game = new Driver(initial);
        await game.Act("P1", new ActivateAbilityCommand("HOST", id, ["TEEMO"]));
        Assert.Equal(initial.RunePools["P1"].Mana - 1, game.State.RunePools["P1"].Mana);
        await game.Top();
        Assert.Contains("TEEMO", game.State.PlayerZones["P1"].Hand);
        Assert.DoesNotContain("TEEMO", game.State.PlayerZones["P2"].Base);
        Assert.DoesNotContain("TEEMO", game.State.PlayerZones["P2"].Battlefields);
        Assert.DoesNotContain("TEEMO", game.State.PlayerZones["P1"].ChampionZone);
        Assert.Equal("P1", game.State.CardObjects["TEEMO"].ControllerId);
        await game.Replay();
    }

    [Theory]
    [InlineData("UNL-185/219", "none")]
    [InlineData("UNL-228/219", "source")]
    [InlineData("UNL-228*/219", "target")]
    [InlineData("UNL-185/219", "move")]
    public async Task BorrowedBattlefieldReturnCreatesItsCoinAfterARealResponse(string donor, string response)
    {
        var initial = RadUnitEffectTestFixture.Place(BorrowedBombPosition(donor), "ALLY", "P1", "BATTLEFIELD", "BF");
        initial = AttackDamageDistributionTests.Spell(initial, "FLASH", "OGS·011/024", "P1");
        var id = TemplateId(initial, "友方单位返回手牌，然后");
        foreach (var illegal in new[] { "HOST", "UNIT", "GEAR", "ENEMY", "BF", "GRAVE" })
            await RadGearTestPosition.Reject(initial, "P1", new ActivateAbilityCommand("HOST", id, [illegal]));
        await RadGearTestPosition.Reject(initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) { ["P1"] = new(0, 20) } },
            "P1", new ActivateAbilityCommand("HOST", id, ["ALLY"]));
        var game = new Driver(initial);
        await game.Act("P1", new ActivateAbilityCommand("HOST", id, ["ALLY"]));
        if (response != "none")
        {
            await game.Act("P1", response == "move" ? new PlayCardCommand("FLASH", "OGS·011/024", ["ALLY"])
                : new PlayCardCommand("RETURN", "OGN·104/298", [response == "source" ? "HOST" : "ALLY"]));
            await game.Top();
        }
        await game.Top();
        var coin = Assert.Single(game.State.CardObjects.Values, c => c.TokenFactoryCardNo is { } no
            && P6TokenFactoryCatalog.TryGetByCardNo(no, out var factory) && factory.TokenFamilyName == "金币");
        Assert.True(coin.IsExhausted);
        Assert.Equal("P1", coin.OwnerId);
        Assert.Contains(coin.ObjectId, game.State.PlayerZones["P1"].Base);
        Assert.Equal(response != "move", game.State.PlayerZones["P1"].Hand.Contains("ALLY"));
        if (response == "move") Assert.Contains("ALLY", game.State.PlayerZones["P1"].Base);
        await game.Replay();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BorrowedReturnReplacementStillCreatesACoin(bool empowered)
    {
        var initial = HandDiscardObserverTests.Put(BorrowedBombPosition("UNL-185/219"), "TARGET", "VEN·086", "P1", "BASE");
        initial = RadUnitEffectTestFixture.Place(initial, "TARGET", "P1", "BATTLEFIELD", "BF");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["TARGET"] = initial.CardObjects["TARGET"] with { IsEmpowered = empowered } } };
        var game = new Driver(initial);
        await game.Act("P1", new ActivateAbilityCommand("HOST", TemplateId(initial, "友方单位返回手牌，然后"), ["TARGET"]));
        await game.Top();
        Assert.Equal(!empowered, game.State.PlayerZones["P1"].Hand.Contains("TARGET"));
        if (empowered) Assert.Equal(initial.CardObjects["TARGET"].Power + 3, game.State.CardObjects["TARGET"].Power);
        Assert.Single(game.State.CardObjects.Values, c => c.TokenFactoryCardNo is { } no
            && P6TokenFactoryCatalog.TryGetByCardNo(no, out var factory) && factory.TokenFamilyName == "金币");
        await game.Replay();
    }

    [Fact]
    public async Task BorrowedPackReturnsControlledForeignEquipmentToItsOwner()
    {
        var initial = BorrowedBombPosition("OGN·181/298");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["GEAR"] = initial.CardObjects["GEAR"] with { OwnerId = "P2" } } };
        var game = new Driver(initial);
        await game.Act("P1", new ActivateAbilityCommand("HOST", TemplateId(initial, "正面朝下的卡牌返回"), ["GEAR"]));
        await game.Top();
        Assert.Contains("GEAR", game.State.PlayerZones["P2"].Hand);
        Assert.DoesNotContain("GEAR", game.State.PlayerZones["P1"].Base);
        Assert.DoesNotContain("GEAR", JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(game.State)["P1"]));
        await game.Replay();
    }

    [Theory]
    [InlineData("OGN·181/298")]
    [InlineData("UNL-185/219")]
    public async Task BorrowedReturnOfATokenUsesItsCapturedFactoryThenCeasesNormally(string donor)
    {
        var initial = BorrowedBombPosition(donor);
        Assert.True(P6TokenFactoryCatalog.TryGetByCardNo("SFD·T01", out var factory));
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["TOKEN"] = factory.CreateObject("TOKEN", "P1", "P1") } };
        initial = RadUnitEffectTestFixture.Place(initial, "TOKEN", "P1", "BATTLEFIELD", "BF");
        var game = new Driver(initial);
        await game.Act("P1", new ActivateAbilityCommand("HOST", TemplateId(initial, donor == "OGN·181/298"
            ? "正面朝下的卡牌返回" : "友方单位返回手牌，然后"), ["TOKEN"]));
        var item = Assert.Single(game.State.StackItems);
        Assert.Equal(factory.CardNo, item.BorrowedExhaust!.TargetTokenFactories!["TOKEN"]);
        Assert.Contains("invalid captured borrowed skill", OfficialInsightAndSpellLockTests.Errors(game.State with {
            StackItems = [item with { BorrowedExhaust = item.BorrowedExhaust with { TargetTokenFactories = null } }] }));
        await game.Top();
        Assert.False(game.State.CardObjects.ContainsKey("TOKEN"));
        Assert.DoesNotContain("TOKEN", game.State.PlayerZones["P1"].Hand);
        Assert.Contains(game.Events(), e => e.Event.Kind == "TOKEN_CEASED_TO_EXIST");
        await game.Replay();
    }

    [Theory]
    [InlineData("CHAMPION", "P1")]
    [InlineData("BASE", "P2")]
    [InlineData("BATTLEFIELD", "P2")]
    public async Task NativeNamedReturnUsesTheSameOwnerDestinationAndZoneReset(string zone, string controller)
    {
        var initial = BorrowedTeemoReturnPosition("OGN·263/298", zone, controller);
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["UNIT"] = initial.CardObjects["UNIT"] with { IsExhausted = false } } };
        var game = new Driver(initial);
        var requirement = ResolutionResult.BuildPrompts(initial)["P1"].Candidates!
            .Where(c => c.Action == CommandTypes.LegendAct)
            .SelectMany(c => (IEnumerable<IReadOnlyDictionary<string, object?>>)c.Metadata!["sourceRequirements"]!)
            .Single(r => Equals(r["sourceObjectId"], "UNIT") && Equals(r["abilityId"], LegendActionAbilityCatalog.TeemoLegendAbilityId));
        var choices = Assert.IsAssignableFrom<IReadOnlyDictionary<string, IReadOnlyList<ActionPromptChoiceDto>>>(requirement["targetChoicesByIndex"]);
        Assert.Contains(choices["0"], choice => choice.Id == "TEEMO");
        await game.Act("P1", new LegendActCommand("UNIT", LegendActionAbilityCatalog.TeemoLegendAbilityId, ["TEEMO"], ["SPEND_MANA:1"]));
        Assert.NotNull(Assert.Single(game.State.StackItems).LegendFieldEffect);
        Assert.DoesNotContain("TEEMO", game.State.PlayerZones["P1"].Hand);
        await game.Top();
        Assert.Contains("TEEMO", game.State.PlayerZones["P1"].Hand);
        Assert.True(game.State.CardObjects["TEEMO"].ObjectGeneration > initial.CardObjects["TEEMO"].ObjectGeneration);
        Assert.DoesNotContain("TEEMO", game.State.PlayerZones["P2"].Base);
        Assert.DoesNotContain("TEEMO", game.State.PlayerZones["P2"].Battlefields);
        await game.Replay();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OwnedNamedReturnPaysEnemyWardAndHonorsEnemyTargetProtection(bool native)
    {
        var initial = BorrowedTeemoReturnPosition("OGN·263/298", "BASE", "P2");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["UNIT"] = initial.CardObjects["UNIT"] with { IsExhausted = !native },
            ["TEEMO"] = initial.CardObjects["TEEMO"] with { Tags = [..initial.CardObjects["TEEMO"].Tags, CardObjectTags.Spellshield] } } };
        GameCommand command = native
            ? new LegendActCommand("UNIT", LegendActionAbilityCatalog.TeemoLegendAbilityId, ["TEEMO"], ["SPEND_MANA:1"])
            : new ActivateAbilityCommand("HOST", TemplateId(initial, "提莫单位放入手牌"), ["TEEMO"]);
        await RadGearTestPosition.Reject(initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) { ["P1"] = new(20, 0) } }, "P1", command);
        await RadGearTestPosition.Reject(initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["TEEMO"] = initial.CardObjects["TEEMO"] with { UntilEndOfTurnEffects = ["ENEMY_SPELL_SKILL_TARGET_PROTECTION_THIS_TURN"] } } }, "P1", command);
        var game = new Driver(initial);
        await game.Act("P1", command);
        Assert.Equal(initial.RunePools["P1"].TotalPower - 1, game.State.RunePools["P1"].TotalPower);
        Assert.DoesNotContain("TEEMO", game.State.PlayerZones["P1"].Hand);
        await game.Top();
        Assert.Contains("TEEMO", game.State.PlayerZones["P1"].Hand);
        await game.Replay();
    }
}
