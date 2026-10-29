using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class RenektonMightEmpowerTests
{
    private static ActivateAbilityCommand Activate() => new("HOST", "SELF-MIGHT:0", []);
    private static MatchState Position(string no = "VEN·092") => EmpowerTests.Position(no);

    [Theory]
    [InlineData("VEN·092", 37788)] [InlineData("VEN·092a", 37789)] [InlineData("VEN·177", 37880)]
    public async Task ExactPrintingsRetainManaCostMightThresholdAndConditionalKeywords(string no, long id)
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, c => c.CardNo == no);
        Assert.Equal(id, card.Id); Assert.Equal("蛮荒巨兽", card.SubTitle);
        Assert.Equal(5, card.Energy); Assert.Null(card.ReturnEnergy); Assert.Equal(4, card.Power);
        var ability = Assert.Single(ActivatedAbilityParser.Parse(card.CardEffect), a => a.Kind == ActivatedAbilityKinds.ManaGrantSelfMight);
        Assert.Equal(1, ability.ManaCost); Assert.Equal(1, ability.PowerModifierAmount); Assert.Equal(0, ability.RequiredTargetCount);
        var trigger = Assert.Single(RuleTextParser.Parse(card).Triggers, t => t.Kind == TriggerKinds.SourceMightReachedEmpower);
        Assert.Equal(10, trigger.RequiredPowerThreshold); Assert.Equal(TriggerTargetScopes.SourceUnit, trigger.TargetScope);
    }

    [Fact]
    public void AnAdditionalInstructionWithinAnActivatedEffectCannotBeSilentlyDropped()
    {
        Assert.DoesNotContain(ActivatedAbilityParser.Parse("支付{{1}}：给予我在本回合内{{S}}+1。抽一张牌。"),
            ability => ability.Kind == ActivatedAbilityKinds.ManaGrantSelfMight);
    }

    [Theory]
    [InlineData("VEN·092")] [InlineData("VEN·092a")] [InlineData("VEN·177")]
    public async Task SixPaidActivationsReachTenThenASeparateRespondableSkillEmpowersTheSameUnit(string no)
    {
        var game = await AtThreshold(Position(no), no);
        Assert.False(game.State.CardObjects["HOST"].IsEmpowered);
        var trigger = Assert.Single(game.State.StackItems).MightySkill!;
        Assert.Equal(9, trigger.BeforeMight); Assert.Equal(10, trigger.AfterMight); Assert.True(trigger.Confirmed);
        Assert.Null(game.State.PendingPayment); Assert.Null(game.State.PendingCardChoice);
        await game.Top(); Assert.True(game.State.CardObjects["HOST"].IsEmpowered);
        Assert.Contains("法盾", game.State.CardObjects["HOST"].Tags); Assert.Contains("游走", game.State.CardObjects["HOST"].Tags);
        Assert.Single(game.Events(), e => e.Event.Kind == "OBJECT_EMPOWERED");
        await game.Act("P1", Activate()); await game.Top();
        Assert.Equal(11, ArmamentMightTests.Might(game.State)); Assert.Empty(game.State.StackItems); Assert.Empty(game.State.TriggerQueue);
        Assert.Single(game.Events(), e => e.Event.Kind == "OBJECT_EMPOWERED");
        await game.Act("P1", new EndTurnCommand());
        Assert.Equal(4, ArmamentMightTests.Might(game.State)); Assert.True(game.State.CardObjects["HOST"].IsEmpowered);
        await game.Replay();
    }

    private static async Task<Driver> AtThreshold(MatchState initial, string no = "VEN·092")
    {
        var game = new Driver(initial); await game.Act("P1", new PlayCardCommand("HOST", no, []));
        Assert.Equal(15, game.State.RunePools["P1"].Mana);
        Assert.Equal(4, ArmamentMightTests.Might(game.State));
        for (var i = 1; i <= 6; i++) {
            await game.Act("P1", Activate());
            Assert.Equal(15 - i, game.State.RunePools["P1"].Mana);
            Assert.Equal(3 + i, ArmamentMightTests.Might(game.State));
            Assert.Empty(Assert.Single(game.State.StackItems).TargetObjectIds);
            await game.Top(); Assert.Equal(4 + i, ArmamentMightTests.Might(game.State));
            Assert.False(game.State.CardObjects["HOST"].IsEmpowered);
            if (i < 6) { Assert.Empty(game.State.StackItems); Assert.Empty(game.State.TriggerQueue); }
        }
        return game;
    }

    public static IEnumerable<object[]> Illegal() => new[] { "VEN·092", "VEN·092a", "VEN·177" }.SelectMany(no =>
        new[] { "mana", "target", "cost", "opponent", "hidden", "hand", "instance", "spell-only" }
            .Select(branch => new object[] { no, branch }));
    [Theory]
    [MemberData(nameof(Illegal))]
    public async Task IllegalActivationPreservesResourcesStateAndEvents(string no, string branch)
    {
        var state = await EmpowerTests.Enter(no); var command = Activate();
        if (branch is "mana" or "spell-only") state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) {
            ["P1"] = new(0, 0, spellOnlyMana: branch == "spell-only" ? 10 : 0) } };
        if (branch == "target") command = command with { TargetObjectIds = ["HOST"] };
        if (branch == "cost") command = command with { OptionalCosts = ["UNSUPPORTED_COST"] };
        if (branch == "instance") command = command with { AbilityInstanceId = "NOT_AN_INSTANCE" };
        if (branch == "hidden") state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["HOST"] = state.CardObjects["HOST"] with { IsFaceDown = true } } };
        if (branch == "hand") state = state with {
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P1"] = state.PlayerZones["P1"] with {
                    Base = state.PlayerZones["P1"].Base.Where(id => id != "HOST").ToArray(),
                    Battlefields = state.PlayerZones["P1"].Battlefields.Where(id => id != "HOST").ToArray(),
                    Hand = [..state.PlayerZones["P1"].Hand, "HOST"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["HOST"] = new("P1", "HAND") } };
        var before = MatchStateHasher.Hash(state);
        var result = await new CoreRuleEngine().ResolveAsync(state, new("invalid-self-might", branch == "opponent" ? "P2" : "P1", command.CmdType), command, default);
        Assert.False(result.Accepted); Assert.Empty(result.Events); Assert.Equal(before, MatchStateHasher.Hash(result.State));
    }

    [Fact]
    public async Task AnExhaustedUnitCanSpendUnitAbilityManaWithoutSpendingSpellOnlyMana()
    {
        var initial = ArmamentMightTests.Position(unit: "VEN·092", power: 4);
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["HOST"] = initial.CardObjects["HOST"] with { IsExhausted = true } },
            RunePools = new Dictionary<string, RunePool>(initial.RunePools) { ["P1"] = new(0, 0, spellOnlyMana: 3, unitPlayOrUnitAbilityOnlyMana: 1) } };
        var game = new Driver(initial);
        Assert.Contains(ResolutionResult.BuildPrompts(initial)["P1"].Candidates ?? [], c => c.Action == CommandTypes.ActivateAbility
            && c.Sources!.Any(s => s.Id == "HOST"));
        await game.Act("P1", Activate()); Assert.Equal(0, game.State.RunePools["P1"].UnitPlayOrUnitAbilityOnlyMana);
        Assert.Equal(3, game.State.RunePools["P1"].SpellOnlyMana); await game.Top();
        Assert.Equal(5, ArmamentMightTests.Might(game.State)); Assert.True(game.State.CardObjects["HOST"].IsExhausted);
        await game.Replay();
    }

    [Theory]
    [InlineData("HOST", true)] [InlineData("SECOND_UNIT", false)]
    public async Task ASpellCanJumpAcrossTheThresholdAndOnlyTheSourceUnitObservesItsOwnMight(string target, bool expected)
    {
        var initial = ArmamentMightTests.Position(unit: "VEN·092", power: 4);
        initial = OfficialHoldSequenceTests.AddUnit(initial, "SECOND_UNIT", "OGN·219/298", "F", "P1");
        initial = AttackDamageDistributionTests.Spell(initial, "BUFF", "OGN·154/298", "P1");
        var game = new Driver(initial); await game.Act("P1", new PlayCardCommand("BUFF", "OGN·154/298", [target])); await game.Top();
        if (expected) { Assert.Equal(11, ArmamentMightTests.Might(game.State)); Assert.NotNull(Assert.Single(game.State.StackItems).MightySkill); await game.Top(); }
        else { Assert.Equal(4, ArmamentMightTests.Might(game.State)); Assert.Empty(game.State.StackItems); Assert.Empty(game.State.TriggerQueue); }
        Assert.Equal(expected, game.State.CardObjects["HOST"].IsEmpowered); await game.Replay();
    }

    [Fact]
    public async Task AResponseDroppingMightBelowTenDoesNotUndoTheAlreadyCapturedTrigger()
    {
        var initial = AttackDamageDistributionTests.Spell(Position(), "SMOKE", "OGN·093/298", "P2");
        initial = initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) { ["P2"] = new(20, 20) } };
        var game = await AtThreshold(initial);
        await game.Act("P1", new PassPriorityCommand());
        await game.Act("P2", new PlayCardCommand("SMOKE", "OGN·093/298", ["HOST"])); await game.Top();
        Assert.Equal(6, ArmamentMightTests.Might(game.State)); Assert.False(game.State.CardObjects["HOST"].IsEmpowered);
        Assert.Equal(10, Assert.Single(game.State.StackItems).MightySkill!.AfterMight);
        await game.Top(); Assert.True(game.State.CardObjects["HOST"].IsEmpowered); Assert.Equal(6, ArmamentMightTests.Might(game.State));
        await game.Replay();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task AReturnResponseDoesNotBuffOrEmpowerTheNewHandIncarnation(bool threshold)
    {
        var initial = AttackDamageDistributionTests.Spell(Position(), "RETURN", "OGN·104/298", "P1");
        var game = threshold ? await AtThreshold(initial) : new Driver(initial);
        if (!threshold) { await game.Act("P1", new PlayCardCommand("HOST", "VEN·092", [])); await game.Act("P1", Activate()); }
        var generation = game.State.CardObjects["HOST"].ObjectGeneration;
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["HOST"])); await game.Top();
        Assert.Contains("HOST", game.State.PlayerZones["P1"].Hand);
        Assert.True(game.State.CardObjects["HOST"].ObjectGeneration > generation);
        var stack = JsonSerializer.SerializeToElement(ResolutionResult.BuildSnapshots(game.State)["P2"].Stack);
        Assert.Equal("HIDDEN", stack[0].GetProperty("sourceObjectId").GetString());
        await game.Top(); Assert.False(game.State.CardObjects["HOST"].IsEmpowered); Assert.Equal(4, game.State.CardObjects["HOST"].Power);
        await game.Replay();
    }

    [Fact]
    public async Task CopiedPrintedTextUsesItsOwnAbilityInstanceAndTheSameCost()
    {
        var game = new Driver(ArmamentMightTests.Position("SFD·059/221", "VEN·177", 4, attached: true));
        await game.Act("P1", Activate() with { AbilityInstanceId = "ATTACHED:GEAR:0:1:SELF-MIGHT:0" });
        Assert.NotNull(Assert.Single(game.State.StackItems).SelfMightActivation!.Origin);
        Assert.Equal(19, game.State.RunePools["P1"].Mana); await game.Top(); Assert.Equal(5, ArmamentMightTests.Might(game.State));
        await game.Replay();
    }

    [Theory]
    [InlineData("before")] [InlineData("after")] [InlineData("confirmation")] [InlineData("unit")]
    public async Task RecoveryRejectsForgedThresholdFramesOrSourceIdentity(string mutation)
    {
        var game = await AtThreshold(Position()); var item = Assert.Single(game.State.StackItems); var context = item.MightySkill!;
        var changed = context with { BeforeMight = mutation == "before" ? 10 : context.BeforeMight,
            AfterMight = mutation == "after" ? 9 : context.AfterMight, Confirmed = mutation != "confirmation",
            Unit = mutation == "unit" ? new("GEAR", 0) : context.Unit };
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(game.State with { StackItems = [item with { MightySkill = changed }] }),
            e => e.Contains("Mighty transition"));
    }

    [Theory]
    [InlineData("index")] [InlineData("source")] [InlineData("instance")]
    public async Task RecoveryRejectsForgedCapturedPaidAbility(string mutation)
    {
        var game = new Driver(ArmamentMightTests.Position(unit: "VEN·092", power: 4)); await game.Act("P1", Activate());
        var item = Assert.Single(game.State.StackItems); var context = item.SelfMightActivation!;
        var changed = context with { AbilityIndex = mutation == "index" ? 99 : context.AbilityIndex,
            Source = mutation == "source" ? context.Source with { Generation = context.Source.Generation + 1 } : context.Source,
            InstanceId = mutation == "instance" ? "FORGED" : context.InstanceId };
        Assert.Contains("invalid captured self Might activation", OfficialInsightAndSpellLockTests.Errors(
            game.State with { StackItems = [item with { SelfMightActivation = changed }] }));
    }
}
