using Riftbound.Contracts;
using Riftbound.CardCatalog;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class RadActivatedUnitTests
{
    private static MatchState Position(string sourceCardNo, string sourceAbility, bool enemyTarget = false,
        bool targetHasBoon = false, bool targetAlreadyStunned = false, bool targetHasSpellshield = false)
    {
        var targetPlayer = enemyTarget ? "P2" : "P1";
        var targetTags = new List<string> { CardObjectTags.UnitCard };
        if (targetHasBoon) targetTags.Add(CardObjectTags.Boon);
        if (targetHasSpellshield) targetTags.Add(CardObjectTags.Spellshield);
        var targetEffects = targetAlreadyStunned ? new[] { "STUNNED" } : Array.Empty<string>();
        var cards = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
        {
            ["BF"] = new("BF", cardNo: "OGN·278/298", ownerId: "P1", controllerId: "P1",
                tags: [P6TokenFactoryCatalog.BattlefieldCardTag]),
            ["SOURCE"] = new("SOURCE", manaCost: sourceCardNo == "RAD·027/167" ? 3 : 5,
                power: sourceCardNo == "RAD·027/167" ? 3 : 5, cardNo: sourceCardNo,
                ownerId: "P1", controllerId: "P1", tags: [CardObjectTags.UnitCard]),
            ["TARGET"] = new("TARGET", manaCost: 2, power: 4, cardNo: "TEST·UNIT",
                ownerId: targetPlayer, controllerId: targetPlayer, tags: targetTags,
                untilEndOfTurnEffects: targetEffects)
        };
        var locations = new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
        {
            ["BF"] = new("P1", "BATTLEFIELD", "BF"),
            ["SOURCE"] = new("P1", "BATTLEFIELD", "BF"),
            ["TARGET"] = new(targetPlayer, "BATTLEFIELD", "BF")
        };
        var zones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
        {
            ["P1"] = PlayerZones.Empty with { Battlefields = enemyTarget ? ["BF", "SOURCE"] : ["BF", "SOURCE", "TARGET"] },
            ["P2"] = PlayerZones.Empty with { Battlefields = enemyTarget ? ["BF", "TARGET"] : [] }
        };
        return new MatchState("RAD-ACTIVATED-UNITS", 0, 5, "P1",
            new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" },
            status: MatchStatuses.InProgress, readyPlayerIds: ["P1", "P2"], turnPlayerId: "P1",
            phase: MatchPhases.Main, timingState: TimingStates.NeutralOpen,
            playerZones: zones, cardObjects: cards, objectLocations: locations,
            runePools: new Dictionary<string, RunePool> { ["P1"] = new(0, 0), ["P2"] = new(20, 0) }, seed: 92);
    }

    [Fact]
    public void CatalogExposesBothActivatedUnitSkills()
    {
        Assert.True(P4ActivatedAbilityCatalog.TryGetByAbilityId(
            P4ActivatedAbilityCatalog.PrismwingFlockAbilityId, out var boonAbility));
        Assert.Equal("RAD·027/167", boonAbility.SourceCardNo);
        Assert.True(boonAbility.SimpleEffect!.GrantsBoonOrPower);
        Assert.Equal(2, boonAbility.SimpleEffect.ExistingBoonPowerBonus);
        Assert.True(boonAbility.AppliesSpellshieldTargetTax);

        Assert.True(P4ActivatedAbilityCatalog.TryGetByAbilityId(
            P4ActivatedAbilityCatalog.RelentlessStalkerAbilityId, out var stunAbility));
        Assert.Equal("RAD·104/167", stunAbility.SourceCardNo);
        Assert.True(stunAbility.SimpleEffect!.StunsTarget);
        Assert.True(stunAbility.AppliesSpellshieldTargetTax);
    }

    [Fact]
    public void RuntimeCatalogAdmitsTheThreeRemainingUnitPrintings()
    {
        var cards = RadiantCardSources.GetAdmittedCards();
        Assert.Contains(cards, card => card.Id == 39520L && card.CardNo == "RAD·027/167");
        Assert.Contains(cards, card => card.Id == 39606L && card.CardNo == "RAD·104/167");
        Assert.Contains(cards, card => card.Id == 39620L && card.CardNo == "RAD·115/167");
    }

    [Fact]
    public async Task PrismwingFlockGrantsBoonOrAddsTwoPowerToExistingBoon()
    {
        var fresh = new Driver(Position("RAD·027/167", P4ActivatedAbilityCatalog.PrismwingFlockAbilityId));
        await fresh.Act("P1", new ActivateAbilityCommand("SOURCE",
            P4ActivatedAbilityCatalog.PrismwingFlockAbilityId, ["TARGET"]));
        await fresh.Top();
        Assert.Equal(5, fresh.State.CardObjects["TARGET"].Power);
        Assert.Contains(CardObjectTags.Boon, fresh.State.CardObjects["TARGET"].Tags);

        var existing = new Driver(Position("RAD·027/167", P4ActivatedAbilityCatalog.PrismwingFlockAbilityId,
            targetHasBoon: true));
        await existing.Act("P1", new ActivateAbilityCommand("SOURCE",
            P4ActivatedAbilityCatalog.PrismwingFlockAbilityId, ["TARGET"]));
        await existing.Top();
        Assert.Equal(6, existing.State.CardObjects["TARGET"].Power);
        Assert.Equal(2, existing.State.CardObjects["TARGET"].UntilEndOfTurnPowerModifier);
        await existing.Replay();
    }

    [Fact]
    public async Task RelentlessStalkerStunsEnemyUnit()
    {
        var game = new Driver(Position("RAD·104/167", P4ActivatedAbilityCatalog.RelentlessStalkerAbilityId,
            enemyTarget: true));
        await game.Act("P1", new ActivateAbilityCommand("SOURCE",
            P4ActivatedAbilityCatalog.RelentlessStalkerAbilityId, ["TARGET"]));
        await game.Top();
        Assert.Contains("STUNNED", game.State.CardObjects["TARGET"].UntilEndOfTurnEffects);
        Assert.Contains(game.Events(), item => item.Event.Kind == "STATUS_EFFECT_APPLIED"
            && Equals(item.Event.Payload["targetObjectId"], "TARGET"));
        await game.Replay();
    }

    [Fact]
    public async Task RelentlessStalkerRejectsFriendlyTargetWithoutMutation()
    {
        var initial = Position("RAD·104/167", P4ActivatedAbilityCatalog.RelentlessStalkerAbilityId);
        var before = MatchStateHasher.Hash(initial);
        var result = await new CoreRuleEngine().ResolveAsync(initial,
            new PlayerIntent("invalid-stalker-target", "P1", CommandTypes.ActivateAbility),
            new ActivateAbilityCommand("SOURCE", P4ActivatedAbilityCatalog.RelentlessStalkerAbilityId, ["TARGET"]),
            default);
        Assert.False(result.Accepted);
        Assert.Equal(before, MatchStateHasher.Hash(result.State));
        Assert.Empty(result.Events);
    }

    [Fact]
    public async Task EnemySpellshieldTargetAddsOnePowerToTheActivatedAbilityCost()
    {
        var game = new Driver(Position("RAD·104/167", P4ActivatedAbilityCatalog.RelentlessStalkerAbilityId,
            enemyTarget: true, targetHasSpellshield: true) with
        {
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal) {
                ["P1"] = new(0, 1), ["P2"] = new(20, 0) }
        });
        await game.Act("P1", new ActivateAbilityCommand("SOURCE",
            P4ActivatedAbilityCatalog.RelentlessStalkerAbilityId, ["TARGET"]));
        await game.Top();
        Assert.Contains("STUNNED", game.State.CardObjects["TARGET"].UntilEndOfTurnEffects);
        Assert.Equal(0, game.State.RunePools["P1"].TotalPower);
    }

    [Fact]
    public async Task AlreadyStunnedEnemyRemainsAValidTargetButTheSkillHasNoEffect()
    {
        var game = new Driver(Position("RAD·104/167", P4ActivatedAbilityCatalog.RelentlessStalkerAbilityId,
            enemyTarget: true, targetAlreadyStunned: true));
        await game.Act("P1", new ActivateAbilityCommand("SOURCE",
            P4ActivatedAbilityCatalog.RelentlessStalkerAbilityId, ["TARGET"]));
        await game.Top();
        Assert.Equal(["STUNNED"], game.State.CardObjects["TARGET"].UntilEndOfTurnEffects);
        Assert.Contains(game.Events(), item => item.Event.Kind == "ABILITY_NO_EFFECT"
            && Equals(item.Event.Payload["reason"], "ALREADY_STUNNED"));
    }
}
