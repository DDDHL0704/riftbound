using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.OfficialInsightAndSpellLockTests;

namespace Riftbound.ConformanceTests;

public sealed class CurrentControllerDamageBonusTests
{
    private const string Attacker = "P1-TEEMO-DEFEND-ATTACKER";
    private const string Field = "P1-TEEMO-DEFEND-BATTLEFIELD";

    public static IEnumerable<object[]> CountedBranches() => CurrentTeemoCountedDamageTests.Printings()
        .SelectMany(p => new[] { 0, 1, 2 }.SelectMany(n => new[] { false, true }
            .Select(local => new object[] { p[1], n, local })));

    [Theory]
    [MemberData(nameof(CountedBranches))]
    public async Task CountedDefendAddsControllerAndLocalBonusesOnceAndPreservesNoActionAtZero(string printing, int count, bool local)
    {
        var state = CountedPosition(printing, count, local);
        state = Annie(state, "P2");
        var originalDeck = state.PlayerZones["P2"].MainDeck;
        var result = await Defend(state);
        var reveal = Assert.Single(result.Events, e => e.Kind == "MAIN_DECK_CARDS_REVEALED");
        Assert.Equal(count, reveal.Payload["damageAmount"]);
        var damage = result.Events.Where(e => e.Kind == "DAMAGE_APPLIED").ToArray();
        if (count == 0) Assert.Empty(damage);
        else Assert.Equal(count + 1 + (local ? 1 : 0), Assert.Single(damage).Payload["damage"]);
        Assert.Equal(count == 0 ? 0 : count + 1 + (local ? 1 : 0), result.State.CardObjects[Attacker].Damage);
        Assert.Equal(originalDeck.Take(5).Order(), result.State.PlayerZones["P2"].MainDeck.Skip(1).Order());
        Assert.Equal(state.RunePools, result.State.RunePools);
        Restore(result.State);
    }

    [Theory]
    [InlineData("BASE", "P2", 1, 2)]
    [InlineData("BATTLEFIELD", "P2", 1, 2)]
    [InlineData("HAND", "P2", 1, 1)]
    [InlineData("GRAVEYARD", "P2", 1, 1)]
    [InlineData("BASE", "P1", 1, 1)]
    [InlineData("BASE", "P2", 0, 1)]
    [InlineData("BASE", "P2", 2, 3)]
    public async Task CountedSkillUsesItsControllersCurrentFieldSources(string zone, string controller, int copies, int expected)
    {
        var state = CountedPosition("OGN·121/298", 1, false);
        for (var n = 0; n < copies; n++) state = Annie(state, controller, zone, "ANNIE" + n, Field);
        var result = await Defend(state);
        Assert.Equal(expected, Assert.Single(result.Events, e => e.Kind == "DAMAGE_APPLIED").Payload["damage"]);
        Assert.Equal(expected, result.State.CardObjects[Attacker].Damage);
        Restore(result.State);
    }

    public static IEnumerable<object[]> SpellBranches() => new[] { 0, 1, 2 }
        .SelectMany(copies => new[] { false, true }.Select(local => new object[] { copies, local }));

    [Theory]
    [MemberData(nameof(SpellBranches))]
    public async Task OfficialStarfallExampleAddsControllerBonusPerTargetAndLocalBonusOnlyAtItsTarget(int copies, bool local)
    {
        var state = SpellPosition("OGN·105/298", local);
        state = Place(state, "SECOND", "P2", "BASE");
        for (var n = 0; n < copies; n++) state = Annie(state, "P1", id: "ANNIE" + n);
        var done = await Cast(state, "OGN·105/298", "UNIT", "SECOND");
        Assert.Equal(6 + copies + (local ? 1 : 0), done.State.CardObjects["UNIT"].Damage);
        Assert.Equal(6 + copies, done.State.CardObjects["SECOND"].Damage);
        Assert.Equal(2, done.Events.Count(e => e.Kind == "DAMAGE_APPLIED"));
        Restore(done.State);
    }

    [Theory]
    [MemberData(nameof(SpellBranches))]
    public async Task SelectedBattlefieldDoesNotDoubleTheSharedControllerOrLocalBonus(int copies, bool local)
    {
        var state = SpellPosition("OGS·002/024", local);
        for (var n = 0; n < copies; n++) state = Annie(state, "P1", id: "ANNIE" + n);
        var done = await Cast(state, "OGS·002/024", "HILL");
        Assert.All(new[] { "UNIT", "SECOND" }, id => Assert.Equal(3 + copies + (local ? 1 : 0), done.State.CardObjects[id].Damage));
        Assert.Equal(2, done.Events.Count(e => e.Kind == "DAMAGE_APPLIED"));
        Restore(done.State);
    }

    [Theory]
    [MemberData(nameof(SpellBranches))]
    public async Task ActivatedXerathSkillAddsCurrentControllerAndTargetLocalBonuses(int copies, bool local)
    {
        var state = SpellPosition("OGN·105/298", local);
        state = Place(Place(state, "UNIT", "P2", "BATTLEFIELD", "OTHER"), "SECOND", "P2", "BASE");
        state = Annie(state, "P1", "BATTLEFIELD", "XERATH");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["HILL"] = state.CardObjects["HILL"] with { CardNo = "OGN·278/298", ControllerId = "P1" },
            ["OTHER"] = state.CardObjects["OTHER"] with { CardNo = local ? "OGN·296/298" : "OGN·278/298" },
            ["XERATH"] = new("XERATH", cardNo: "UNL-026/219", power: 5, ownerId: "P1", controllerId: "P1", tags: [CardObjectTags.UnitCard]) } };
        for (var n = 0; n < copies; n++) state = Annie(state, "P1", id: "ANNIE" + n);
        var activated = await Act(state, "P1", new ActivateAbilityCommand("XERATH", P4ActivatedAbilityCatalog.XerathDamageAbilityId, ["UNIT"]));
        Assert.True(activated.State.CardObjects["XERATH"].IsExhausted);
        var done = await Top(activated.State);
        Assert.Equal(3 + copies + (local ? 1 : 0), Assert.Single(done.Events, e => e.Kind == "DAMAGE_APPLIED").Payload["damage"]);
        Assert.Equal(3 + copies + (local ? 1 : 0), done.State.CardObjects["UNIT"].Damage);
        Restore(done.State);
    }

    [Fact]
    public async Task ActualResponseSacrificeRemovesBonusBeforeTheOriginalSpellResolves()
    {
        var state = Annie(SpellPosition("OGN·105/298", false), "P1");
        state = Annie(state, "P1", id: "FRIEND");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["FRIEND"] = state.CardObjects["FRIEND"] with { CardNo = "OGN·001/298" },
            ["DRAW"] = new("DRAW", cardNo: "SFD·125/221", ownerId: "P1", controllerId: "P1", tags: [CardObjectTags.UnitCard]) },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) { ["P1"] = state.PlayerZones["P1"] with { MainDeck = ["DRAW"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["DRAW"] = new("P1", "MAIN_DECK") } };
        state = AttackDamageDistributionTests.Spell(state, "SOUL", "SFD·163/221", "P1");
        var pending = await Act(state, "P1", new PlayCardCommand("SPELL", "OGN·105/298", ["UNIT", "SECOND"]));
        var response = await Act(pending.State, "P1", new PlayCardCommand("SOUL", "SFD·163/221", ["ANNIE", "FRIEND"]));
        var sacrificed = await Top(response.State);
        Assert.Contains("ANNIE", sacrificed.State.PlayerZones["P1"].Graveyard);
        Assert.Contains("DRAW", sacrificed.State.PlayerZones["P1"].Hand);
        Assert.DoesNotContain(sacrificed.Events, e => e.Kind == "BURNOUT_APPLIED");
        Assert.DoesNotContain(sacrificed.Events, e => e.Kind == "DAMAGE_APPLIED");
        var done = await Top(sacrificed.State);
        Assert.All(new[] { "UNIT", "SECOND" }, id => Assert.Equal(6, done.State.CardObjects[id].Damage));
        Restore(done.State);
    }

    internal static MatchState CountedPosition(string printing, int count, bool local)
    {
        var state = CurrentTeemoCountedDamageTests.Position(printing, count);
        return state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            [Field] = state.CardObjects[Field] with { CardNo = local ? "OGN·296/298" : "OGN·278/298" } } };
    }

    internal static MatchState Annie(MatchState state, string controller, string zone = "BASE", string id = "ANNIE", string field = "HILL")
    {
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            [id] = new(id, cardNo: "OGS·001/024", power: 4, ownerId: controller, controllerId: controller, tags: [CardObjectTags.UnitCard]) } };
        return Place(state, id, controller, zone, field);
    }

    internal static MatchState Place(MatchState state, string id, string controller, string zone, string field = "HILL")
    {
        var zones = state.PlayerZones.ToDictionary(p => p.Key, p => p.Value with {
            Base = p.Value.Base.Where(x => x != id).ToArray(), Battlefields = p.Value.Battlefields.Where(x => x != id).ToArray(),
            Hand = p.Value.Hand.Where(x => x != id).ToArray(), Graveyard = p.Value.Graveyard.Where(x => x != id).ToArray() });
        zones[controller] = zone switch {
            "BASE" => zones[controller] with { Base = [..zones[controller].Base, id] },
            "BATTLEFIELD" => zones[controller] with { Battlefields = [..zones[controller].Battlefields, id] },
            "HAND" => zones[controller] with { Hand = [..zones[controller].Hand, id] },
            _ => zones[controller] with { Graveyard = [..zones[controller].Graveyard, id] } };
        return state with { PlayerZones = zones,
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { [id] = new(controller, zone, zone == "BATTLEFIELD" ? field : null) } };
    }

    internal static MatchState SpellPosition(string spell, bool local)
    {
        var state = BattlefieldLocalPermissionTests.Position(local ? "OGN·296/298" : "OGN·278/298", "P2", "HILL") with {
            ActivePlayerId = "P1", TurnPlayerId = "P1" };
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["UNIT"] = state.CardObjects["UNIT"] with { Power = 20 },
            ["SECOND"] = new("SECOND", cardNo: "SFD·069/221", power: 20, ownerId: "P2", controllerId: "P2", tags: [CardObjectTags.UnitCard]) } };
        state = Place(state, "SECOND", "P2", "BATTLEFIELD");
        return AttackDamageDistributionTests.Spell(state, "SPELL", spell, "P1");
    }

    internal static async Task<ResolutionResult> Defend(MatchState state)
    {
        var moved = await Act(state, "P1", new MoveUnitCommand(Attacker, "BASE", "BATTLEFIELD:" + Field, []));
        var choice = Assert.IsType<PendingCardChoiceState>(moved.State.PendingCardChoice);
        Assert.Equal("P2", choice.PlayerId);
        Assert.Equal(new[] { Attacker }, choice.LegalObjectIds);
        Assert.DoesNotContain(moved.Events, e => e.Kind is "MAIN_DECK_CARDS_REVEALED" or "DAMAGE_APPLIED");
        return await Top((await Act(moved.State, "P2", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [Attacker]))).State);
    }

    internal static async Task<ResolutionResult> Cast(MatchState state, string spell, params string[] targets)
        => await Top((await Act(state, "P1", new PlayCardCommand("SPELL", spell, targets))).State);

    internal static async Task<ResolutionResult> Top(MatchState state)
    {
        var first = await Act(state, state.PriorityPlayerId!, new PassPriorityCommand());
        return await Act(first.State, first.State.PriorityPlayerId!, new PassPriorityCommand());
    }
}
