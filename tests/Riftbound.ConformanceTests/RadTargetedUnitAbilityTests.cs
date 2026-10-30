using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class RadTargetedUnitAbilityTests
{
    private const string Bird = "RAD·027/167";
    private const string Lurker = "RAD·104/167";

    [Theory]
    [InlineData(Bird, 39520L, 3, 0, 3, "鸟类", ActivatedAbilityKinds.GrantUnitBoonOrTemporaryPower)]
    [InlineData(Lurker, 39606L, 5, 1, 5, "猫科", ActivatedAbilityKinds.StunEnemyUnit)]
    public async Task OfficialPrintingsAndTheirTypedAbilitiesKeepEveryCostAndCharacteristic(
        string no, long id, int mana, int powerCost, int might, string tag, string kind)
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, c => c.CardNo == no);
        Assert.Equal(id, card.Id); Assert.Equal(mana, card.Energy);
        Assert.Equal(powerCost, card.ReturnEnergy ?? 0); Assert.Equal(might, card.Power); Assert.Equal(tag, card.Tag);
        Assert.Equal(no == Bird
            ? "{{T}}：给予一名单位{{增益}}。如果该单位已拥有增益，则改为给予其在本回合内{{S}}+2。（给予一名单位增益，即让未拥有增益的单位获得一个{{S}}+1增益。）"
            : "{{T}}：{{眩晕}}一名敌方单位。（使其在本回合内无法造成战斗伤害。）", card.CardEffect);
        var spec = Assert.Single(RuleTextParser.Parse(card).ActivatedAbilities);
        Assert.Equal(kind, spec.Kind); Assert.True(spec.ExhaustsSourceAsCost); Assert.Equal(1, spec.RequiredTargetCount);
        var ability = Ability(no);
        Assert.Equal(kind, ability.Kind); Assert.Equal(0, ability.ManaCost); Assert.Equal(0, ability.PowerCost);
        Assert.True(ability.AppliesSpellshieldTargetTax); Assert.False(ability.ReactionSpeed);
    }

    [Theory]
    [InlineData(Bird, "BASE")]
    [InlineData(Bird, "BATTLEFIELD:BF")]
    [InlineData(Lurker, "BASE")]
    [InlineData(Lurker, "BATTLEFIELD:BF")]
    public async Task NormalPlayPaysPrintedCostsAndCannotUseTheExhaustAbilityWhileSleeping(string no, string destination)
    {
        var game = new Driver(VenUnitEntryBasicTests.Position(no));
        await game.Act("P1", new PlayCardCommand("UNIT", no, [], Destination: destination));
        Assert.Equal(no == Bird ? 17 : 15, game.State.RunePools["P1"].Mana);
        Assert.Equal(no == Bird ? 20 : 19, game.State.RunePools["P1"].TotalPower);
        Assert.Equal(no == Bird ? 3 : 5, game.State.CardObjects["UNIT"].Power);
        Assert.True(game.State.CardObjects["UNIT"].IsExhausted);
        Assert.Contains(no == Bird ? "鸟类" : "猫科", game.State.CardObjects["UNIT"].Tags);
        await Reject(game.State, Activate(no));
        await game.Replay();
    }

    [Theory]
    [InlineData(Bird, "BASE", "ALLY")]
    [InlineData(Bird, "BATTLEFIELD", "ENEMY")]
    [InlineData(Lurker, "BASE", "ENEMY")]
    [InlineData(Lurker, "BATTLEFIELD", "ENEMY")]
    public async Task TheSourceExhaustsAsACostAndTheTargetChangesOnlyAfterTheResponseWindow(
        string no, string sourceZone, string target)
    {
        var game = new Driver(Position(no, sourceZone));
        var power = game.State.CardObjects[target].Power;
        await game.Act("P1", Activate(no, target));
        Assert.True(game.State.CardObjects["UNIT"].IsExhausted);
        Assert.Equal(power, game.State.CardObjects[target].Power);
        Assert.DoesNotContain("STUNNED", game.State.CardObjects[target].UntilEndOfTurnEffects);
        Assert.Single(game.State.StackItems);
        await game.Top();
        if (no == Bird) {
            Assert.Equal(power + 1, game.State.CardObjects[target].Power);
            Assert.Contains(CardObjectTags.Boon, game.State.CardObjects[target].Tags);
        } else Assert.Contains("STUNNED", game.State.CardObjects[target].UntilEndOfTurnEffects);
        Assert.Empty(game.State.StackItems);
        await game.Replay();
    }

    [Fact]
    public async Task AnExistingBoonGetsOnlyTemporaryPowerAndKeepsItsOriginalBoonAfterTheTurn()
    {
        var state = WithBoon(Position(Bird), "ALLY");
        var power = state.CardObjects["ALLY"].Power;
        var game = new Driver(state);
        await game.Act("P1", Activate(Bird, "ALLY")); await game.Top();
        Assert.Equal(power + 2, game.State.CardObjects["ALLY"].Power);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "BOON_GRANTED");
        await game.Act("P1", new EndTurnCommand());
        Assert.Equal(power, game.State.CardObjects["ALLY"].Power);
        Assert.Contains(CardObjectTags.Boon, game.State.CardObjects["ALLY"].Tags);
        await game.Replay();
    }

    [Fact]
    public async Task AFreshBoonFromOneActivationChangesTheNextActivationIntoTemporaryPower()
    {
        var state = Position(Bird);
        state = HandDiscardObserverTests.Put(state, "BIRD2", Bird, "P1", "HAND");
        state = RadUnitEffectTestFixture.Place(state, "BIRD2", "P1", "BASE");
        var game = new Driver(state);
        await game.Act("P1", Activate(Bird, "ALLY"));
        var before = game.State.CardObjects["ALLY"].Power;
        await game.Top();
        await game.Act("P1", Activate(Bird, "ALLY") with { SourceObjectId = "BIRD2" }); await game.Top();
        Assert.Equal(before + 3, game.State.CardObjects["ALLY"].Power);
        Assert.Single(game.Events(), e => e.Event.Kind == "BOON_GRANTED");
        await game.Replay();
    }

    [Fact]
    public async Task AnAlreadyStunnedEnemyRemainsLegalWithoutASecondStunEvent()
    {
        var state = Position(Lurker);
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["ENEMY"] = state.CardObjects["ENEMY"] with { UntilEndOfTurnEffects = ["STUNNED"] } } };
        var game = new Driver(state);
        await game.Act("P1", Activate(Lurker)); await game.Top();
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "STATUS_EFFECT_APPLIED");
        Assert.True(game.State.CardObjects["UNIT"].IsExhausted);
        await game.Replay();
    }

    [Theory]
    [InlineData(Bird)] [InlineData(Lurker)]
    public async Task AReturnedTargetCannotBeReplacedByAnotherObjectOrRetargeted(string no)
    {
        var state = HandDiscardObserverTests.Put(Position(no), "RETURN", "OGN·104/298", "P2", "HAND");
        var game = new Driver(state);
        await game.Act("P1", Activate(no));
        await game.Act("P1", new PassPriorityCommand());
        await game.Act("P2", new PlayCardCommand("RETURN", "OGN·104/298", ["ENEMY"]));
        await game.Top();
        Assert.Contains("ENEMY", game.State.PlayerZones["P2"].Hand);
        await game.Top();
        Assert.DoesNotContain(CardObjectTags.Boon, game.State.CardObjects["ENEMY"].Tags);
        Assert.DoesNotContain("STUNNED", game.State.CardObjects["ENEMY"].UntilEndOfTurnEffects);
        Assert.Equal(4, game.State.CardObjects["ALLY"].Power);
        await game.Replay();
    }

    public static IEnumerable<object[]> InvalidActivations() => new[] { Bird, Lurker }.SelectMany(no =>
        new[] { "sleeping", "hidden", "hand", "enemy-source", "wrong-turn", "missing", "extra", "gear", "grave", "hidden-target", "cost", "instance" }
            .Select(reason => new object[] { no, reason }));

    [Theory]
    [MemberData(nameof(InvalidActivations))]
    public async Task IllegalActivationsLeaveStateResourcesAndViewsUnchanged(string no, string reason)
    {
        var state = Position(no);
        var source = state.CardObjects["UNIT"];
        if (reason is "sleeping" or "hidden" or "enemy-source")
            state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["UNIT"] = source with { IsExhausted = reason == "sleeping", IsFaceDown = reason == "hidden",
                    ControllerId = reason == "enemy-source" ? "P2" : "P1" } } };
        if (reason == "hand") state = RadUnitEffectTestFixture.Place(state, "UNIT", "P1", "HAND");
        if (reason == "wrong-turn") state = state with { ActivePlayerId = "P2", TurnPlayerId = "P2" };
        if (reason == "hidden-target") state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["ENEMY"] = state.CardObjects["ENEMY"] with { IsFaceDown = true } } };
        var command = Activate(no) with {
            TargetObjectIds = reason switch { "missing" => [], "extra" => ["ALLY", "ENEMY"], "gear" => ["GEAR"], "grave" => ["GRAVE"], _ => ["ENEMY"] },
            OptionalCosts = reason == "cost" ? ["FAKE_COST"] : [],
            AbilityInstanceId = reason == "instance" ? "FORGED" : null };
        await Reject(state, command);
    }

    [Fact]
    public async Task AStunCannotTargetAFriendAndWardMustBePaidBeforeTheSourceExhausts()
    {
        await Reject(Position(Lurker), Activate(Lurker, "ALLY"));
        var state = Position(Lurker);
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["ENEMY"] = state.CardObjects["ENEMY"] with { Tags = [..state.CardObjects["ENEMY"].Tags, "法盾"] } },
            RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(20, 0) } };
        await Reject(state, Activate(Lurker));
        state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(20, 1) } };
        var game = new Driver(state);
        await game.Act("P1", Activate(Lurker)); Assert.Equal(0, game.State.RunePools["P1"].TotalPower);
        await game.Top(); Assert.Contains("STUNNED", game.State.CardObjects["ENEMY"].UntilEndOfTurnEffects);
        await game.Replay();
    }

    private static P4ActivatedAbilityDefinition Ability(string no) => Assert.Single(P4ActivatedAbilityCatalog.GetAll(),
        a => a.SourceCardNo == no && P4ActivatedAbilityCatalog.IsOrdinaryTargetedExhaustAbility(a));
    private static ActivateAbilityCommand Activate(string no, string target = "ENEMY") => new("UNIT", Ability(no).AbilityId, [target], []);
    private static MatchState Position(string no, string zone = "BASE")
    {
        var state = RadUnitEffectTestFixture.Place(VenUnitEntryBasicTests.Position(no), "UNIT", "P1", zone, zone == "BATTLEFIELD" ? "BF" : null);
        return state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["UNIT"] = state.CardObjects["UNIT"] with { IsExhausted = false } } };
    }
    private static MatchState WithBoon(MatchState state, string id) => state with {
        CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            [id] = state.CardObjects[id] with { Power = state.CardObjects[id].Power + 1,
                Tags = [..state.CardObjects[id].Tags, CardObjectTags.Boon] } } };
    private static async Task Reject(MatchState state, ActivateAbilityCommand command)
    {
        var hash = MatchStateHasher.Hash(state);
        var snapshots = JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(state));
        var prompts = JsonSerializer.Serialize(ResolutionResult.BuildPrompts(state));
        var result = await new CoreRuleEngine().ResolveAsync(state, new("illegal-rad-unit-ability", "P1", command.CmdType), command, default);
        Assert.False(result.Accepted); Assert.Empty(result.Events);
        Assert.Equal(hash, MatchStateHasher.Hash(result.State));
        Assert.Equal(snapshots, JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(result.State)));
        Assert.Equal(prompts, JsonSerializer.Serialize(ResolutionResult.BuildPrompts(result.State)));
    }
}
