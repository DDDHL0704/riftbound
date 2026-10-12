using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;

namespace Riftbound.ConformanceTests;

public sealed class PowerfulKeywordLayerTests
{
    internal static MatchState Position(int power = 4) => ArmamentMightTests.Position(unit: "OGN·232/298", power: power);

    internal static async Task<MatchState> Equipped(int power = 4)
    {
        var result = await Act(Position(power), "P1", new AssembleEquipmentCommand("GEAR", "HOST", ["ASSEMBLE_RED"]));
        return (await EquipTestDriver.FinishAsync(result)).State;
    }

    [Fact]
    public async Task ACurrentMightReductionDisablesFiorasPrintedPowerfulKeywords()
    {
        var initial = LocalDestructionRecallTests.At(LeonaStunnedMightTests.Board(stunned: false), "HOST", "P1", "BASE");
        initial = initial with { ActivePlayerId = "P2", TurnPlayerId = "P2",
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["ENEMY1"] = initial.CardObjects["ENEMY1"] with { CardNo = "OGN·232/298", Power = 4, Tags = [CardObjectTags.UnitCard] } },
            RunePools = new Dictionary<string, RunePool>(initial.RunePools) { ["P2"] = new(20, 20) } };
        initial = AttackDamageDistributionTests.Spell(initial, "BOON", "OGN·053/298", "P2");
        initial = AttackDamageDistributionTests.Spell(initial, "STUN", "OGN·262/298", "P1");
        Restore(initial);
        var boon = await Top((await Act(initial, "P2", new PlayCardCommand("BOON", "OGN·053/298", ["ENEMY1"]))).State);
        Assert.Equal(5, boon.State.CardObjects["ENEMY1"].Power);
        Assert.Contains(CardObjectTags.Spellshield, boon.State.CardObjects["ENEMY1"].Tags);
        var turn = await Act(boon.State, "P2", new EndTurnCommand());
        // Fund the next fixture phase independently of the automatic rune draw.
        var funded = turn.State with { RunePools = new Dictionary<string, RunePool>(turn.State.RunePools) { ["P1"] = new(20, 20) } };
        var moved = await Act(funded, "P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F", []));
        var played = await Act(moved.State, "P1", new PlayCardCommand("STUN", "OGN·262/298", ["ENEMY1"]));
        var done = await Top(played.State); Assert.Equal(1, LeonaStunnedMightTests.Might(done.State));
        Assert.DoesNotContain(CardObjectTags.Spellshield, done.State.CardObjects["ENEMY1"].Tags);
        Assert.DoesNotContain(CardCombatKeywordNames.Roam, done.State.CardObjects["ENEMY1"].Tags);
        Assert.DoesNotContain(CardCombatKeywordNames.Steadfast, done.State.CardObjects["ENEMY1"].Tags);
        Assert.Contains(CardObjectTags.Boon, done.State.CardObjects["ENEMY1"].Tags);
        Restore(done.State); ArmamentMightTests.PublicRecovery(done.State);
    }

    [Theory]
    [InlineData(2, false)] [InlineData(3, true)] [InlineData(4, true)]
    public async Task EquipmentMakesCurrentMightEnableThePrintedKeywords(int power, bool enabled)
    {
        var state = await Equipped(power);
        Assert.Equal(power + 2, ArmamentMightTests.Might(state)); Assert.Equal(power, state.CardObjects["HOST"].Power);
        Assert.Equal(enabled, state.CardObjects["HOST"].Tags.Contains(CardObjectTags.Spellshield));
        Assert.Equal(enabled, state.CardObjects["HOST"].Tags.Contains(CardCombatKeywordNames.Roam));
        Assert.Equal(enabled ? 3 : 0, state.CardObjects["HOST"].RuleDerivedTags?.Count ?? 0);
        Restore(state); ArmamentMightTests.PublicRecovery(state);
    }

    [Fact]
    public async Task ReturningEquipmentRemovesTheNowInactiveDerivedKeywords()
    {
        var state = await Equipped();
        state = AttackDamageDistributionTests.Spell(state, "RECALL", "SFD·135/221", "P1");
        var result = await Top((await Act(state, "P1", new PlayCardCommand("RECALL", "SFD·135/221", ["GEAR"]))).State);
        Assert.Equal(4, ArmamentMightTests.Might(result.State)); Assert.Null(result.State.CardObjects["HOST"].RuleDerivedTags);
        Assert.DoesNotContain(CardObjectTags.Spellshield, result.State.CardObjects["HOST"].Tags);
        Assert.DoesNotContain(CardCombatKeywordNames.Roam, result.State.CardObjects["HOST"].Tags);
        Assert.DoesNotContain(CardCombatKeywordNames.Steadfast, result.State.CardObjects["HOST"].Tags);
        Restore(result.State); ArmamentMightTests.PublicRecovery(result.State);
    }

    [Fact]
    public async Task ACopyOfPrintedConditionalTextAddsItsOwnCountedKeywords()
    {
        var state = AttachedRuleTextTests.AddSong(await Equipped(), "SONG");
        state = (await AttachedRuleTextTests.Attach(state, "SONG")).State;
        Assert.Equal(6, ArmamentMightTests.Might(state));
        Assert.Contains("坚守2", state.CardObjects["HOST"].RuleDerivedTags!); Assert.Contains("法盾2", state.CardObjects["HOST"].RuleDerivedTags!);
        Assert.Contains(CardCombatKeywordNames.Roam, state.CardObjects["HOST"].Tags);
        Restore(state); ArmamentMightTests.PublicRecovery(state);
        state = AttackDamageDistributionTests.Spell(state, "RECALL", "SFD·135/221", "P1");
        var done = await Top((await Act(state, "P1", new PlayCardCommand("RECALL", "SFD·135/221", ["SONG"]))).State);
        Assert.Contains(CardCombatKeywordNames.Steadfast, done.State.CardObjects["HOST"].RuleDerivedTags!);
        Assert.Contains(CardObjectTags.Spellshield, done.State.CardObjects["HOST"].RuleDerivedTags!);
        Assert.DoesNotContain("法盾2", done.State.CardObjects["HOST"].Tags); Restore(done.State); ArmamentMightTests.PublicRecovery(done.State);
    }

    [Fact]
    public async Task IndependentGrantedKeywordsSurviveThePowerfulConditionEnding()
    {
        var state = await Equipped();
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["HOST"] = state.CardObjects["HOST"] with {
                GrantedTags = [CardObjectTags.Spellshield, CardCombatKeywordNames.Roam],
                Tags = state.CardObjects["HOST"].Tags.Append(CardCombatKeywordNames.Roam).Distinct().Order(StringComparer.Ordinal).ToArray() } } };
        state = AttackDamageDistributionTests.Spell(state, "RECALL", "SFD·135/221", "P1");
        var done = await Top((await Act(state, "P1", new PlayCardCommand("RECALL", "SFD·135/221", ["GEAR"]))).State);
        Assert.Null(done.State.CardObjects["HOST"].RuleDerivedTags); Assert.Contains(CardObjectTags.Spellshield, done.State.CardObjects["HOST"].Tags);
        Assert.Contains(CardCombatKeywordNames.Roam, done.State.CardObjects["HOST"].Tags);
        Assert.DoesNotContain(CardCombatKeywordNames.Steadfast, done.State.CardObjects["HOST"].Tags);
        Restore(done.State); ArmamentMightTests.PublicRecovery(done.State);
    }

    [Fact]
    public async Task ConditionalSteadfastCannotKeepItsOwnPowerfulConditionAlive()
    {
        var initial = LeonaStunnedMightTests.Board(stunned: false);
        initial = LocalDestructionRecallTests.At(initial, "HOST", "P1", "BASE");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["ENEMY1"] = initial.CardObjects["ENEMY1"] with { CardNo = "OGN·232/298", Power = 4,
                Tags = new[] { CardObjectTags.UnitCard, CardObjectTags.Spellshield, CardCombatKeywordNames.Roam, CardCombatKeywordNames.Steadfast }.Order(StringComparer.Ordinal).ToArray(),
                RuleDerivedTags = [CardCombatKeywordNames.Steadfast, CardObjectTags.Spellshield, CardCombatKeywordNames.Roam] },
            ["GEAR"] = initial.CardObjects["GEAR"] with { OwnerId = "P2", ControllerId = "P2", AttachedToObjectId = "ENEMY1" } } };
        initial = LocalDestructionRecallTests.At(initial, "GEAR", "P2", "BATTLEFIELD", "F");
        initial = AttackDamageDistributionTests.Spell(initial, "RECALL", "SFD·135/221", "P1"); Restore(initial);
        var moved = await Act(initial, "P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F", []));
        Assert.True(moved.State.CardObjects["ENEMY1"].IsDefending); Assert.Equal(7, LeonaStunnedMightTests.Might(moved.State));
        var done = await Top((await Act(moved.State, "P1", new PlayCardCommand("RECALL", "SFD·135/221", ["GEAR"]))).State);
        Assert.Equal(4, LeonaStunnedMightTests.Might(done.State)); Assert.Null(done.State.CardObjects["ENEMY1"].RuleDerivedTags);
        Assert.DoesNotContain(CardCombatKeywordNames.Steadfast, done.State.CardObjects["ENEMY1"].Tags);
        Restore(done.State); ArmamentMightTests.PublicRecovery(done.State);
    }

    [Fact]
    public async Task IndependentDeflectAndConditionalDeflectBothContributeToActualCost()
    {
        var state = await Equipped();
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["HOST"] = state.CardObjects["HOST"] with { GrantedTags = ["法盾2"], Tags = state.CardObjects["HOST"].Tags.Append("法盾2").Order(StringComparer.Ordinal).ToArray() } },
            RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P2"] = new(20, 20, new Dictionary<string, int> { [RuneTrait.Purple] = 1 }) } };
        state = AttackDamageDistributionTests.Spell(state, "KNOCK", "OGN·095/298", "P2");
        state = AttackDamageDistributionTests.Spell(state, "BOON", "OGN·053/298", "P1");
        state = (await Act(state, "P1", new PlayCardCommand("BOON", "OGN·053/298", ["HOST"]))).State;
        state = (await Act(state, "P1", new PassPriorityCommand())).State;
        var command = new PlayCardCommand("KNOCK", "OGN·095/298", ["HOST"]);
        var preview = new CoreRuleEngine().PreviewPlayCard(state, "P2", PlayCostPreviewTests.Request(state, command));
        Assert.True(preview.CanPay, preview.Message);
        Assert.Equal(3, preview.Cost!.Adjustments.Single(a => a.Label == "法盾费用").Power);
        Restore(state); ArmamentMightTests.PublicRecovery(state);
    }

    [Theory]
    [InlineData("missing-derived")] [InlineData("foreign-derived")] [InlineData("missing-tag")]
    public async Task RecoveryRejectsForgedDerivedKeywordState(string change)
    {
        var state = await Equipped(); var card = state.CardObjects["HOST"];
        card = change switch {
            "missing-derived" => card with { RuleDerivedTags = null },
            "foreign-derived" => card with { RuleDerivedTags = ["FALSE"] },
            "missing-tag" => card with { Tags = card.Tags.Where(t => t != CardObjectTags.Spellshield).ToArray() },
            _ => card
        };
        var forged = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) { ["HOST"] = card } };
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(forged), e => e.Contains("rule-derived keywords", StringComparison.Ordinal));
    }
}
