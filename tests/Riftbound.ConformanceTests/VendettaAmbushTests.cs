using Riftbound.CardCatalog;
using Riftbound.Engine;
using Riftbound.Contracts;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class VendettaAmbushTests
{
    [Theory]
    [InlineData("VEN·017")] [InlineData("VEN·123")] [InlineData("VEN·179")] [InlineData("VEN·183")] [InlineData("VEN·186")]
    public async Task ReleasedExactPrintingHasItsCatalogIdentityAndExecutableUnitDefinition(string cardNo)
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync();
        var card = Assert.Single(catalog.Cards, c => c.CardNo == cardNo);
        Assert.True(CardBehaviorRegistry.TryGetByCardNo(cardNo, out var behavior));
        Assert.True(behavior.PlaysSourceToBaseAsUnit); Assert.Equal(card.Energy, behavior.ManaCost); Assert.Equal(card.Power, behavior.SourceUnitPower);
    }

    internal static MatchState Position(string cardNo, int damage = 2)
    {
        var state = MightyExperienceFrameTests.HandUnit(ArmamentMightTests.Position(unit: "OGN·001/298", power: 5), "UNIT", cardNo);
        return state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["HOST"] = state.CardObjects["HOST"] with { Damage = damage } },
            RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(20, 20), ["P2"] = new(20, 20) } };
    }

    [Theory]
    [InlineData("VEN·017", 0)] [InlineData("VEN·186", 0)]
    [InlineData("VEN·017", 1)] [InlineData("VEN·186", 1)]
    [InlineData("VEN·017", 2)] [InlineData("VEN·186", 2)]
    [InlineData("VEN·017", 3)] [InlineData("VEN·186", 3)]
    public async Task MarkedDamageIsARespondablePlayAbilityAndReadsTheCurrentTarget(string cardNo, int marked)
    {
        var initial = Position(cardNo, marked);
        var played = await Act(initial, "P1", new PlayCardCommand("UNIT", cardNo, ["HOST"]));
        Assert.Contains("UNIT", played.State.PlayerZones["P1"].Base);
        Assert.Equal(marked, played.State.CardObjects["HOST"].Damage);
        Assert.True(Assert.Single(played.State.StackItems).SourceConfirmed);
        Restore(played.State); ArmamentMightTests.PublicRecovery(played.State);
        var resolved = await Top(played.State);
        if (marked == 0) Assert.DoesNotContain(resolved.Events, e => e.Kind == "DAMAGE_APPLIED");
        else Assert.Equal(marked, Assert.Single(resolved.Events, e => e.Kind == "DAMAGE_APPLIED").Payload["damage"]);
        if (marked == 3) Assert.Contains("HOST", resolved.State.PlayerZones["P1"].Graveyard);
        else Assert.Equal(marked * 2, resolved.State.CardObjects["HOST"].Damage);
        Restore(resolved.State); ArmamentMightTests.PublicRecovery(resolved.State);
    }

    [Fact]
    public async Task NewDianaWordingParsesAndEachCompletedSpellGetsAnIndependentSkill()
    {
        var state = Position("VEN·183", 0);
        var played = await Act(state, "P1", new PlayCardCommand("UNIT", "VEN·183", []));
        Assert.Empty(played.State.StackItems); Assert.Equal(3, played.State.CardObjects["UNIT"].Power);
        state = OfficialCopyIdentityTests.AddSpell(played.State, "SPELL", "OGN·048/298");
        var spell = await Act(state, "P1", new PlayCardCommand("SPELL", "OGN·048/298", []));
        Assert.Single(spell.State.StackItems); Assert.Equal(3, spell.State.CardObjects["UNIT"].Power);
        var completed = await Top(spell.State); var skill = Assert.Single(completed.State.StackItems);
        Assert.Equal(TriggerKinds.UnitSpellPlayedPowerModifier, skill.SpellContext!.Kind);
        Assert.Equal(3, completed.State.CardObjects["UNIT"].Power); Restore(completed.State);
        var resolved = await Top(completed.State); Assert.Equal(5, resolved.State.CardObjects["UNIT"].Power);
        Restore(resolved.State); ArmamentMightTests.PublicRecovery(resolved.State);
    }

    [Fact]
    public async Task CatalogStatisticsAreEvidenceOfRegistrationOnly()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(); var stats = FunctionalUnitBuilder.Summarize(FunctionalUnitBuilder.Build(catalog.Cards));
        Console.WriteLine("VEN_CATALOG_STATISTICS " + System.Text.Json.JsonSerializer.Serialize(stats));
        Assert.Equal(1015, catalog.Cards.Count);
    }

    [Theory]
    [InlineData("VEN·017")] [InlineData("VEN·186")]
    public async Task ARealDamageResponseChangesTheAmountReadAtAbilityResolution(string cardNo)
    {
        var state = AttackDamageDistributionTests.Spell(Position(cardNo, 1), "DAMAGE", "OGN·133/298", "P2");
        state = LocalDestructionRecallTests.At(state, "HOST", "P1", "BATTLEFIELD", "F");
        var played = await Act(state, "P1", new PlayCardCommand("UNIT", cardNo, ["HOST"]));
        var passed = await Act(played.State, "P1", new PassPriorityCommand());
        var response = await Act(passed.State, "P2", new PlayCardCommand("DAMAGE", "OGN·133/298", []));
        var damaged = await Top(response.State); Assert.Equal(2, damaged.State.CardObjects["HOST"].Damage);
        Restore(damaged.State); ArmamentMightTests.PublicRecovery(damaged.State);
        var resolved = await Top(damaged.State); Assert.Equal(2, Assert.Single(resolved.Events, e => e.Kind == "DAMAGE_APPLIED").Payload["damage"]);
        Assert.Equal(4, resolved.State.CardObjects["HOST"].Damage); Restore(resolved.State); ArmamentMightTests.PublicRecovery(resolved.State);
    }

    [Theory]
    [InlineData("VEN·017")] [InlineData("VEN·186")]
    public async Task MarkedDamageAbilityReceivesTheBattlefieldsSpellAndSkillBonus(string cardNo)
    {
        var state = LocalDestructionRecallTests.At(Position(cardNo), "HOST", "P1", "BATTLEFIELD", "F");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["F"] = state.CardObjects["F"] with { CardNo = "OGN·296/298" } } };
        var played = await Act(state, "P1", new PlayCardCommand("UNIT", cardNo, ["HOST"]));
        var resolved = await Top(played.State);
        Assert.Equal(3, Assert.Single(resolved.Events, e => e.Kind == "DAMAGE_APPLIED").Payload["damage"]);
        Assert.Contains("HOST", resolved.State.PlayerZones["P1"].Graveyard);
        Restore(resolved.State); ArmamentMightTests.PublicRecovery(resolved.State);
    }

    [Theory]
    [InlineData("VEN·017", "HOST")] [InlineData("VEN·186", "HOST")]
    [InlineData("VEN·017", "UNIT")] [InlineData("VEN·186", "UNIT")]
    public async Task ARealReturnResponsePreservesTheSkillButDoesNotDamageANewTargetIncarnation(string cardNo, string returned)
    {
        var state = OfficialCopyIdentityTests.AddSpell(Position(cardNo), "RETURN", "OGN·104/298");
        var played = await Act(state, "P1", new PlayCardCommand("UNIT", cardNo, ["HOST"]));
        var response = await Act(played.State, "P1", new PlayCardCommand("RETURN", "OGN·104/298", [returned]));
        var departed = await Top(response.State); Assert.Contains(returned, departed.State.PlayerZones["P1"].Hand);
        Restore(departed.State); ArmamentMightTests.PublicRecovery(departed.State);
        var resolved = await Top(departed.State);
        if (returned == "HOST") Assert.DoesNotContain(resolved.Events, e => e.Kind == "DAMAGE_APPLIED");
        else Assert.Equal(4, resolved.State.CardObjects["HOST"].Damage);
        Restore(resolved.State); ArmamentMightTests.PublicRecovery(resolved.State);
    }

    [Theory]
    [InlineData("VEN·123")] [InlineData("VEN·179")] [InlineData("VEN·183")]
    public async Task CurrentNonTargetedPrintingsUseTheSharedAmbushEntryAndPrintedCosts(string cardNo)
    {
        var state = await AmbushPermissionTests.Response(AmbushPermissionTests.Position(cardNo));
        var quote = new CoreRuleEngine().PreviewPlayCard(state, "P1", new("quote", "prompt", state.Tick,
            new("AMBUSH", cardNo, [], Destination: "BATTLEFIELD:F")));
        Assert.True(quote.CanPay, quote.Message);
        Assert.Equal(cardNo == "VEN·123" ? 3 : cardNo == "VEN·179" ? 5 : 4, quote.Cost!.Mana);
        if (cardNo == "VEN·123") Assert.Empty(quote.Cost.PowerByTrait);
        else Assert.Equal(1, quote.Cost.PowerByTrait[cardNo == "VEN·179" ? RuneTrait.Orange : RuneTrait.Purple]);
        var done = await Act(state, "P1", new PlayCardCommand("AMBUSH", cardNo, [], Destination: "BATTLEFIELD:F"));
        Assert.Contains("AMBUSH", done.State.PlayerZones["P1"].Battlefields);
        Assert.Equal(1, done.State.CardObjects["AMBUSH"].ObjectGeneration);
        Restore(done.State); ArmamentMightTests.PublicRecovery(done.State);
    }

    [Fact]
    public async Task VendettaRengarRetainsTheEnemyOccupiedLocationPermission()
    {
        var state = await AmbushPermissionTests.Response(AmbushPermissionTests.Position("VEN·179", friendly: false));
        var done = await Act(state, "P1", new PlayCardCommand("AMBUSH", "VEN·179", [], Destination: "BATTLEFIELD:F"));
        Assert.Contains("AMBUSH", done.State.PlayerZones["P1"].Battlefields); Assert.Contains("FOE", done.State.PlayerZones["P2"].Battlefields);
        Restore(done.State); ArmamentMightTests.PublicRecovery(done.State);
    }

    [Theory]
    [InlineData("VEN·017")] [InlineData("VEN·186")]
    public async Task ADeclaredZeroDamageInstructionReceivesTheBattlefieldBonus(string cardNo)
    {
        var state = LocalDestructionRecallTests.At(Position(cardNo, 0), "HOST", "P1", "BATTLEFIELD", "F");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["F"] = state.CardObjects["F"] with { CardNo = "OGN·296/298" } } };
        var played = await Act(state, "P1", new PlayCardCommand("UNIT", cardNo, ["HOST"]));
        var resolved = await Top(played.State);
        Assert.Equal(1, Assert.Single(resolved.Events, e => e.Kind == "DAMAGE_APPLIED").Payload["damage"]);
        Assert.Equal(1, resolved.State.CardObjects["HOST"].Damage); Restore(resolved.State); ArmamentMightTests.PublicRecovery(resolved.State);
    }

    [Fact]
    public async Task AReturnInstructionDoesNotBecomeDamageBecauseOfTheBattlefield()
    {
        var state = LocalDestructionRecallTests.At(Position("VEN·017"), "HOST", "P1", "BATTLEFIELD", "F");
        state = OfficialCopyIdentityTests.AddSpell(state, "RETURN", "OGN·104/298");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["F"] = state.CardObjects["F"] with { CardNo = "OGN·296/298" } } };
        var played = await Act(state, "P1", new PlayCardCommand("RETURN", "OGN·104/298", ["HOST"]));
        var resolved = await Top(played.State); Assert.DoesNotContain(resolved.Events, e => e.Kind == "DAMAGE_APPLIED");
        Assert.Contains("HOST", resolved.State.PlayerZones["P1"].Hand); Restore(resolved.State); ArmamentMightTests.PublicRecovery(resolved.State);
    }
}
