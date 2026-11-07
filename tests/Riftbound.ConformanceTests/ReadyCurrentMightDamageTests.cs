using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class ReadyCurrentMightDamageTests
{
    [Theory]
    [InlineData(false, false, false)] [InlineData(true, false, false)] [InlineData(true, true, false)]
    [InlineData(true, false, true)] [InlineData(true, true, true)]
    public async Task LastBreathReadiesThenUsesCurrentMightAndTheSpellRemainsTheDamageSource(bool sword, bool runeImmunity, bool battlefieldBonus)
    {
        var state = RadGearTestPosition.Create("OGN·278/298");
        state = RadGearTestPosition.Put(state, "OTHER", battlefieldBonus ? "OGN·296/298" : "OGN·278/298", "P2", "BATTLEFIELD", "OTHER");
        state = RadGearTestPosition.Put(state, "TARGET", "VEN·025", "P2", "BATTLEFIELD", "OTHER");
        state = RadGearTestPosition.Put(state, "CAST", "OGN·260/298", "P1", "HAND");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["ALLY"] = state.CardObjects["ALLY"] with { IsExhausted = true } } };
        if (sword) state = RadGearTestPosition.Put(state, "SWORD", "SFD·022/221", "P1", "BASE");
        if (runeImmunity)
            for (var i = 0; i < 7; i++) state = RadGearTestPosition.Put(state, "RUNE" + i, "VEN·R01", "P2", "BASE");
        var expectedMight = state.CardObjects["ALLY"].Power + (sword ? 2 : 0);
        var game = new Driver(state);
        if (sword)
        {
            await game.Act("P1", new AssembleEquipmentCommand("SWORD", "ALLY", ["ASSEMBLE_RED"]));
            await game.Top();
            Assert.Equal("ALLY", game.State.CardObjects["SWORD"].AttachedToObjectId);
        }
        await game.Act("P1", new PlayCardCommand("CAST", "OGN·260/298", ["ALLY", "TARGET"]));
        Assert.True(game.State.CardObjects["ALLY"].IsExhausted);
        var expectedDamage = expectedMight + (battlefieldBonus ? 1 : 0);
        var before = game.Events().Length; await game.Top();
        Assert.False(game.State.CardObjects["ALLY"].IsExhausted);
        var damage = Assert.Single(game.Events().Skip(before), e => e.Event.Kind == "DAMAGE_APPLIED").Event.Payload;
        Assert.Equal("CAST", damage["sourceObjectId"]);
        Assert.False(damage.ContainsKey("damageSourceObjectId"));
        Assert.Equal(runeImmunity ? 0 : expectedDamage, Convert.ToInt32(damage["damage"]));
        if (runeImmunity)
        {
            Assert.Equal(expectedDamage, Convert.ToInt32(damage["preventedDamage"]));
            Assert.Equal("RUNE_ENEMY_SPELL_SKILL_DAMAGE_IMMUNITY", damage["preventionEffectId"]);
            Assert.Equal(0, game.State.CardObjects["TARGET"].Damage);
        }
        await game.Replay();
    }
}
