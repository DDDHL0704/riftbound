using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

// CN377.2.a,401.2,818.1,821.1.c.2.a.
public sealed class EquipmentAbilityCompletionObserverTests
{
    private const string Progress = "SFD·075/221";
    private const string Blades = "RAD·071/167";

    private static MatchState Position()
        => RadGearTestPosition.Put(RadGearTestPosition.Create(), "PROGRESS", Progress, "P1", "BASE");

    [Theory]
    [InlineData("UNL·T05")] [InlineData("SFD·T03")]
    public async Task AResourceEquipmentAbilityFinishesBeforeTheIndependentMightSkill(string printing)
    {
        var state = RadGearTestPosition.Put(Position(), "GOLD", "SFD·033/221", "P1", "BASE");
        Assert.True(P6TokenFactoryCatalog.TryGetByCardNo(printing, out var factory));
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["GOLD"] = factory.CreateObject("GOLD", "P1", "P1") } };
        var game = new Driver(state);
        var ability = P4ActivatedAbilityCatalog.GetAll().Single(a => a.SourceCardNo == printing && a.IsResourceSkill);
        var power = game.State.RunePools["P1"].TotalPower;
        var might = game.State.CardObjects["PROGRESS"].Power;
        await game.Act("P1", new ActivateAbilityCommand("GOLD", ability.AbilityId, []));
        Assert.DoesNotContain("GOLD", game.State.CardObjects.Keys);
        Assert.Equal(power + 1, game.State.RunePools["P1"].TotalPower);
        Assert.Equal(might, game.State.CardObjects["PROGRESS"].Power);
        Assert.Equal("PROGRESS", Assert.Single(game.State.StackItems).SourceObjectId);
        await game.Top();
        Assert.Equal(might + 1, game.State.CardObjects["PROGRESS"].Power);
        await game.Act("P1", new EndTurnCommand());
        Assert.Equal(might, game.State.CardObjects["PROGRESS"].Power);
        await game.Replay();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task OnlyAnEquipmentAbilityThatActuallyResolvesEarnsTheMightSkill(bool countered)
    {
        var state = RadGearTestPosition.Put(Position(), "HOST", "OGN·017/298", "P1", "BASE");
        state = RadUnitEffectTestFixture.Place(state, "ENEMY", "P2", "BATTLEFIELD", "OTHER");
        state = RadGearTestPosition.Put(state, "COUNTER", "SFD·045/221", "P2", "HAND");
        var abilityId = PrintedExhaustAbilityTests.Ability(state);
        var game = new Driver(state);
        var might = game.State.CardObjects["PROGRESS"].Power;
        await game.Act("P1", new ActivateAbilityCommand("HOST", abilityId, ["ENEMY"]));
        Assert.True(game.State.CardObjects["HOST"].IsExhausted);
        Assert.Equal(might, game.State.CardObjects["PROGRESS"].Power);
        var skill = Assert.Single(game.State.StackItems);
        if (countered)
        {
            await game.Act("P1", new PassPriorityCommand());
            await game.Act("P2", new PlayCardCommand("COUNTER", "SFD·045/221", [skill.StackItemId]));
            await game.Top();
            Assert.Empty(game.State.StackItems);
            Assert.Equal(0, game.State.CardObjects["ENEMY"].Damage);
            Assert.Equal(might, game.State.CardObjects["PROGRESS"].Power);
        }
        else
        {
            await game.Top();
            // Void Gate adds one to the Ballista's two printed damage.
            Assert.Equal(3, game.State.CardObjects["ENEMY"].Damage);
            Assert.Equal(might, game.State.CardObjects["PROGRESS"].Power);
            Assert.Equal("PROGRESS", Assert.Single(game.State.StackItems).SourceObjectId);
            await game.Top();
            Assert.Equal(might + 1, game.State.CardObjects["PROGRESS"].Power);
        }
        await game.Replay();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task AssemblyTriggersAtResolutionAndItsObserverDoesNotBuffANewIncarnation(bool returnObserver)
    {
        var state = RadGearTestPosition.Put(Position(), "GEAR", Blades, "P1", "BASE");
        state = RadGearTestPosition.Put(state, "RETURN", "OGN·104/298", "P1", "HAND");
        var game = new Driver(state);
        var might = game.State.CardObjects["PROGRESS"].Power;
        await game.Act("P1", new AssembleEquipmentCommand("GEAR", "ALLY", ["ASSEMBLE_1_MANA"]));
        Assert.Single(game.State.StackItems);
        Assert.Null(game.State.CardObjects["GEAR"].AttachedToObjectId);
        Assert.Equal(might, game.State.CardObjects["PROGRESS"].Power);
        await game.Top();
        Assert.Equal("ALLY", game.State.CardObjects["GEAR"].AttachedToObjectId);
        Assert.Equal("PROGRESS", Assert.Single(game.State.StackItems).SourceObjectId);
        if (returnObserver)
        {
            await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["PROGRESS"]));
            await game.Top();
            Assert.Contains("PROGRESS", game.State.PlayerZones["P1"].Hand);
        }
        await game.Top();
        Assert.Equal(might + (returnObserver ? 0 : 1), game.State.CardObjects["PROGRESS"].Power);
        await game.Replay();
    }

    [Fact]
    public async Task TemperedPaysAnAssemblyCostWithoutUsingTheEquipmentActivatedAbility()
    {
        var state = RadGearTestPosition.Put(TemperedPlayTriggerAuditTests.PositionFor(gear: Blades),
            "PROGRESS", Progress, "P1", "BASE");
        var game = new Driver(state);
        var might = game.State.CardObjects["PROGRESS"].Power;
        await game.Act("P1", new PlayCardCommand("HOST", "SFD·008/221", []));
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["GEAR"]));
        await game.Top();
        Assert.Equal("HOST", game.State.CardObjects["GEAR"].AttachedToObjectId);
        Assert.Empty(game.State.StackItems);
        Assert.Equal(might, game.State.CardObjects["PROGRESS"].Power);
        await game.Replay();
    }
    [Theory]
    [InlineData("erase")] [InlineData("change-hash")] [InlineData("duplicate")]
    public async Task RecoveryRejectsChangedOrMissingOriginalEquipmentCompletionSkills(string change)
    {
        var state = RadGearTestPosition.Put(Position(), "GEAR", Blades, "P1", "BASE");
        var game = new Driver(state);
        await game.Act("P1", new AssembleEquipmentCommand("GEAR", "ALLY", ["ASSEMBLE_1_MANA"]));
        await game.Top();
        var captured = Assert.Single(game.State.StackItems);
        Assert.Equal("PROGRESS", captured.SourceObjectId);
        Assert.NotNull(captured.UnitEvent!.EquipmentSkillTriggerHash);
        var forged = change switch {
            "erase" => game.State with { StackItems = [] },
            "change-hash" => game.State with { StackItems = [captured with {
                UnitEvent = captured.UnitEvent with { EquipmentSkillTriggerHash = new string('a', 64) } }] },
            _ => game.State with { StackItems = [captured, captured with {
                StackItemId = captured.StackItemId + "-forged" }] }
        };
        Assert.Contains(ArmamentGrantedCombatTriggerTests.Errors(game, forged),
            error => error.Contains("equipment skill", StringComparison.Ordinal));
        await game.Top();
        Assert.Empty(ArmamentGrantedCombatTriggerTests.Errors(game, game.State));
        var reused = game.State with { StackItems = [captured] };
        Assert.Contains(ArmamentGrantedCombatTriggerTests.Errors(game, reused),
            error => error.Contains("equipment skill", StringComparison.Ordinal));
        await game.Replay();
    }
    [Fact]
    public async Task OnlyTheEquipmentAbilityControllersObserversEarnTheMightSkill()
    {
        var state = RadGearTestPosition.Put(Position(), "GOLD", "SFD·033/221", "P2", "BASE");
        Assert.True(P6TokenFactoryCatalog.TryGetByCardNo("SFD·T03", out var factory));
        state = state with { TurnPlayerId = "P2", ActivePlayerId = "P2",
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["GOLD"] = factory.CreateObject("GOLD", "P2", "P2") } };
        var game = new Driver(state);
        var ability = P4ActivatedAbilityCatalog.GetAll().Single(a => a.SourceCardNo == "SFD·T03" && a.IsResourceSkill);
        var might = game.State.CardObjects["PROGRESS"].Power;
        await game.Act("P2", new ActivateAbilityCommand("GOLD", ability.AbilityId, []));
        Assert.Empty(game.State.StackItems);
        Assert.Empty(game.State.TriggerQueue);
        Assert.Equal(might, game.State.CardObjects["PROGRESS"].Power);
        await game.Replay();
    }
}
