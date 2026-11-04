using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed partial class VenBorrowedDependentAbilityFaqTests
{
    [Theory]
    [InlineData("OGN·098/298", false)]
    [InlineData("OGN·098/298", true)]
    [InlineData("OGN·253/298", false)]
    [InlineData("OGN·253/298", true)]
    [InlineData("OGN·302/298", false)]
    [InlineData("OGN·302/298", true)]
    [InlineData("OGN·302*/298", false)]
    [InlineData("OGN·302*/298", true)]
    public async Task BorrowedReactionManaKeepsEncourageInTheEffectAndPreservesThePendingSpell(string donor, bool encourage)
    {
        var initial = BorrowedBombPosition(donor);
        initial = AttackDamageDistributionTests.Spell(initial, "CARD", "OGN·048/298", "P1");
        var game = new Driver(initial);
        var id = TemplateId(initial, "费用资源");
        if (encourage)
            await game.Act("P1", new PlayCardCommand("CARD", "OGN·048/298", []));
        var before = game.State;
        await game.Act("P1", new ActivateAbilityCommand("HOST", id, []));
        var expected = donor == "OGN·098/298" || encourage ? 1 : 0;
        Assert.Equal(before.RunePools["P1"].Mana + expected, game.State.RunePools["P1"].Mana);
        Assert.Equal(before.StackItems, game.State.StackItems);
        Assert.Equal(before.PriorityPlayerId, game.State.PriorityPlayerId);
        Assert.Equal(before.TimingState, game.State.TimingState);
        Assert.True(game.State.CardObjects["HOST"].IsExhausted);
        Assert.True(game.State.CardObjects["UNIT"].IsExhausted);
        await RejectBorrowedResource(game.State, "HOST", id, []);
        if (encourage) await game.Top();
        await game.Replay();
    }

    [Theory]
    [InlineData("OGN·068/298")]
    [InlineData("ARC-002/006")]
    public async Task BorrowedSourceMightDamageIgnoresTheInstructionAndBonusesWhenTheBorrowingTokenCeases(string donor)
    {
        var initial = CurrentControllerDamageBonusTests.Annie(BorrowedMightDamagePosition(donor), "P1");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["HOST"] = initial.CardObjects["HOST"] with { TokenFactoryCardNo = P6TokenFactoryCatalog.ImageTokenCardNo,
                CopyFaceTransitions = [new(0, initial.CardObjects["HOST"].ObjectGeneration,
                    P6TokenFactoryCatalog.ImageTokenCardNo, initial.CardObjects["HOST"].CardNo!)] } } };
        var game = new Driver(initial);
        await game.Act("P1", new ActivateAbilityCommand("HOST", TemplateId(initial, "等同于我战力"), ["ENEMY"]));
        await game.Act("P1", new PlayCardCommand("BOON", "UNL-031/219", ["HOST"]));
        await game.Top();
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["HOST"]));
        await game.Top();
        Assert.False(game.State.CardObjects.ContainsKey("HOST"));
        Assert.NotNull(Assert.Single(game.State.StackItems).BorrowedExhaust!.Borrower.CeasedSource);
        await game.Top();
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "DAMAGE_APPLIED");
        Assert.Equal(0, game.State.CardObjects["ENEMY"].Damage);
        Assert.Contains("ENEMY", game.State.PlayerZones["P2"].Battlefields);
        await game.Replay();
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(3, false)]
    [InlineData(0, true)]
    [InlineData(3, true)]
    public async Task BorrowedSourceMightDamageUsesSharedBonusesAndRealPreventionEvenAtZeroMight(int might, bool prevent)
    {
        var initial = CurrentControllerDamageBonusTests.Annie(BorrowedMightDamagePosition("OGN·068/298"), "P1");
        initial = AttackDamageDistributionTests.Spell(initial, "PREVENT", "OGN·145/298", "P1");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["HOST"] = initial.CardObjects["HOST"] with { Power = might },
            ["OTHER"] = initial.CardObjects["OTHER"] with { CardNo = "OGN·296/298" } } };
        var game = new Driver(initial);
        await game.Act("P1", new ActivateAbilityCommand("HOST", TemplateId(initial, "等同于我战力"), ["ENEMY"]));
        if (prevent)
        {
            await game.Act("P1", new PlayCardCommand("PREVENT", "OGN·145/298", []));
            await game.Top();
        }
        await game.Top();
        var damage = game.Events().Last(e => e.Event.Kind == "DAMAGE_APPLIED").Event;
        Assert.Equal(prevent ? 0 : might + 2, damage.Payload["damage"]);
        Assert.Equal(prevent ? might + 2 : 0, damage.Payload.GetValueOrDefault("preventedDamage") ?? 0);
        if (prevent || might == 0) Assert.Equal(prevent ? 0 : 2, game.State.CardObjects["ENEMY"].Damage);
        else Assert.Contains("ENEMY", game.State.PlayerZones["P2"].Graveyard);
        await game.Replay();
    }

    [Fact]
    public async Task BorrowedSourceMightDamageRunsOrdinaryLethalDamageCleanup()
    {
        var initial = HandDiscardObserverTests.Put(BorrowedMightDamagePosition("OGN·068/298"), "FRAGILE", "VEN·026", "P2", "BASE");
        initial = RadUnitEffectTestFixture.Place(initial, "FRAGILE", "P2", "BATTLEFIELD", "OTHER");
        var game = new Driver(initial);
        await game.Act("P1", new ActivateAbilityCommand("HOST", TemplateId(initial, "等同于我战力"), ["FRAGILE"]));
        await game.Top();
        Assert.Contains("FRAGILE", game.State.PlayerZones["P2"].Graveyard);
        Assert.DoesNotContain("FRAGILE", game.State.PlayerZones["P2"].Battlefields);
        Assert.Contains(game.Events(), e => e.Event.Kind == "UNIT_DESTROYED");
        await game.Replay();
    }

    [Theory]
    [InlineData("OGN·253/298")]
    [InlineData("OGN·302/298")]
    [InlineData("OGN·302*/298")]
    public async Task BorrowedEncourageDoesNotCountTheBorrowersOwnActualPlay(string donor)
    {
        var initial = HandDiscardObserverTests.Put(BorrowedBombPosition(donor), "AURA", "RAD·019/167", "P1", "BASE");
        initial = RadUnitEffectTestFixture.Place(initial, "HOST", "P1", "HAND");
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("HOST", "OGN·111/298", []));
        Assert.False(game.State.CardObjects["HOST"].IsExhausted);
        Assert.Equal(1, game.State.PlayerCardsPlayedThisTurn["P1"]);
        Assert.Equal("HOST", Assert.Single(game.State.CardPlays.Finalized).Source.ObjectId);
        var before = game.State.RunePools["P1"];
        await game.Act("P1", new ActivateAbilityCommand("HOST", TemplateId(game.State, "费用资源"), []));
        Assert.Equal(before, game.State.RunePools["P1"]);
        Assert.True(game.State.CardObjects["HOST"].IsExhausted);
        await game.Replay();
    }

    private static MatchState BorrowedMightDamagePosition(string donor)
    {
        var state = BorrowedBombPosition(donor);
        state = RadUnitEffectTestFixture.Place(state, "HOST", "P1", "BATTLEFIELD", "BF");
        state = RadUnitEffectTestFixture.Place(state, "ENEMY", "P2", "BATTLEFIELD", "OTHER");
        state = AttackDamageDistributionTests.Spell(state, "BOON", "UNL-031/219", "P1");
        state = AttackDamageDistributionTests.Spell(state, "TARGET-RETURN", "OGN·104/298", "P2");
        return state;
    }

    [Theory]
    [InlineData("OGN·068/298", "none")]
    [InlineData("ARC-002/006", "none")]
    [InlineData("OGN·068/298", "boon")]
    [InlineData("ARC-002/006", "boon")]
    [InlineData("OGN·068/298", "boon-return")]
    [InlineData("ARC-002/006", "boon-return")]
    [InlineData("OGN·068/298", "target-return")]
    [InlineData("ARC-002/006", "target-return")]
    public async Task BorrowedSourceMightDamageChecksTheBorrowersBattlefieldAndCurrentAvailableMight(string donor, string response)
    {
        var initial = BorrowedMightDamagePosition(donor);
        var id = TemplateId(initial, "等同于我战力");
        foreach (var target in new[] { "HOST", "ALLY", "UNIT", "GEAR", "MISSING" })
            await RejectBorrowedResource(initial, "HOST", id, [target]);
        await RejectBorrowedResource(RadUnitEffectTestFixture.Place(initial, "HOST", "P1", "BASE"), "HOST", id, ["ENEMY"]);
        await RejectBorrowedResource(RadUnitEffectTestFixture.Place(initial, "ENEMY", "P2", "BASE"), "HOST", id, ["ENEMY"]);
        var game = new Driver(initial);
        var expected = ArmamentMightTests.Might(initial);
        await game.Act("P1", new ActivateAbilityCommand("HOST", id, ["ENEMY"]));
        var captured = Assert.Single(game.State.StackItems);
        Assert.Contains("invalid captured borrowed skill", OfficialInsightAndSpellLockTests.Errors(game.State with {
            StackItems = [captured with { BorrowedExhaust = captured.BorrowedExhaust! with { BorrowerWasOnBattlefield = false } }] }));
        Assert.Equal(0, game.State.CardObjects["ENEMY"].Damage);
        if (response.StartsWith("boon", StringComparison.Ordinal))
        {
            await game.Act("P1", new PlayCardCommand("BOON", "UNL-031/219", ["HOST"]));
            await game.Top();
            expected++;
        }
        if (response is "boon-return" or "target-return")
        {
            if (response == "target-return")
            {
                await game.Act("P1", new PassPriorityCommand());
                await game.Act("P2", new PlayCardCommand("TARGET-RETURN", "OGN·104/298", ["ENEMY"]));
            }
            else await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["HOST"]));
            await game.Top();
        }
        await game.Top();
        if (response is "target-return" or "boon-return")
        {
            Assert.Equal(0, game.State.CardObjects["ENEMY"].Damage);
            Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "DAMAGE_APPLIED");
        }
        else
        {
            Assert.Equal(expected, game.Events().Last(e => e.Event.Kind == "DAMAGE_APPLIED").Event.Payload["damage"]);
            if (expected >= initial.CardObjects["ENEMY"].Power) Assert.Contains("ENEMY", game.State.PlayerZones["P2"].Graveyard);
            else Assert.Equal(expected, game.State.CardObjects["ENEMY"].Damage);
        }
        if (response == "boon-return") Assert.Contains("HOST", game.State.PlayerZones["P1"].Hand);
        await game.Replay();
    }
}
