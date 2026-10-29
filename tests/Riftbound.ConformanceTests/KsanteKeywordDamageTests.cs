using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class KsanteKeywordDamageTests
{
    private const string Spell = "RAD·148/167";
    private static PlayCardCommand Cast() => new("SPELL", Spell, ["HOST", "ENEMY"]);

    [Theory, InlineData(0), InlineData(1), InlineData(3)]
    public async Task PrintedSignaturePaysOrangeThenGrantsSteadfastBeforeReferencingItsValue(int before)
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, c => c.CardNo == Spell);
        Assert.Equal(39656, card.Id); Assert.Equal("陀斧打击", card.CardName); Assert.Equal("奎桑提", card.Hero);
        Assert.Equal(2, card.Energy); Assert.Equal(1, card.ReturnEnergy);
        Assert.Contains("给予一名友方单位在本回合内{{坚守2}}，然后对一名敌方单位造成等同于该友方单位{{坚守}}数值的伤害。", card.CardEffect);
        var game = new Driver(Position(before)); await game.Act("P1", Cast());
        Assert.Equal(0, game.State.RunePools["P1"].Mana); Assert.Equal(0, game.State.RunePools["P1"].TotalPower);
        Assert.Equal(0, game.State.CardObjects["ENEMY"].Damage);
        Assert.DoesNotContain("COMBAT_KEYWORD:坚守2:1", game.State.CardObjects["HOST"].UntilEndOfTurnEffects);
        await game.Top(); Assert.Equal(before + 2, game.State.CardObjects["ENEMY"].Damage);
        Assert.Contains("COMBAT_KEYWORD:坚守2:1", game.State.CardObjects["HOST"].UntilEndOfTurnEffects);
        Assert.Equal(3, game.State.CardObjects["HOST"].Power); Assert.Equal(3, ArmamentMightTests.Might(game.State));
        var kinds = game.Events().Select(e => e.Event.Kind).ToArray();
        Assert.True(Array.IndexOf(kinds, "STATUS_EFFECT_APPLIED") < Array.IndexOf(kinds, "DAMAGE_APPLIED"));
        Assert.Contains("SPELL", game.State.PlayerZones["P1"].Graveyard); await game.Replay();
    }

    [Theory, InlineData("mana"), InlineData("orange"), InlineData("reversed"), InlineData("equipment")]
    [InlineData("same-target"), InlineData("missing-target"), InlineData("opponent"), InlineData("priority")]
    public async Task InvalidPaymentTargetsOrReactionTimingCannotChangeState(string branch)
    {
        var s = Position();
        if (branch is "mana" or "orange") s = s with { RunePools = new Dictionary<string, RunePool>(s.RunePools) {
            ["P1"] = branch == "mana" ? new(1, 0, new Dictionary<string, int> { [RuneTrait.Orange] = 1 }) : new(2, 0) } };
        if (branch == "priority") s = s with { TimingState = TimingStates.NeutralClosed, PriorityPlayerId = "P1", FocusPlayerId = null };
        var targets = branch switch { "reversed" => new[] { "ENEMY", "HOST" }, "equipment" => ["GEAR", "ENEMY"],
            "same-target" => ["HOST", "HOST"], "missing-target" => ["HOST"], _ => ["HOST", "ENEMY"] };
        await Reject(s, branch == "opponent" ? "P2" : "P1", new PlayCardCommand("SPELL", Spell, targets));
    }

    [Theory, InlineData("friendly", false), InlineData("enemy", false)]
    [InlineData("friendly", true), InlineData("enemy", true)]
    public async Task RecallResponsesSuppressOnlyTheInstructionsDependingOnAnIllegalTarget(string branch, bool damageBonus)
    {
        var s = Position(1); var recalled = branch == "friendly" ? "HOST" : "ENEMY";
        if (damageBonus) s = DeathObserverAuditTests.Source(s, "OGS·001/024", "ANNIE", "P1");
        s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
            [recalled] = s.CardObjects[recalled] with { Power = 3 } } };
        var game = new Driver(EquipChainTests.Response(s, "OGN·169/298", "P2"));
        await game.Act("P1", Cast()); await game.Act("P1", new PassPriorityCommand());
        await game.Act("P2", new PlayCardCommand("RESPONSE", "OGN·169/298", [recalled])); await game.Top();
        await game.Top(); Assert.Equal(0, game.State.CardObjects["ENEMY"].Damage);
        if (branch == "friendly") Assert.DoesNotContain("COMBAT_KEYWORD:坚守2:1", game.State.CardObjects["HOST"].UntilEndOfTurnEffects);
        else Assert.Contains("COMBAT_KEYWORD:坚守2:1", game.State.CardObjects["HOST"].UntilEndOfTurnEffects);
        Assert.Equal(0, game.State.RunePools["P1"].Mana); Assert.Contains("SPELL", game.State.PlayerZones["P1"].Graveyard); await game.Replay();
    }

    [Fact]
    public async Task AValidKeywordReferenceReceivesDamageBonusesAfterTheGrant()
    {
        var game = new Driver(DeathObserverAuditTests.Source(Position(1), "OGS·001/024", "ANNIE", "P1"));
        await game.Act("P1", Cast()); await game.Top();
        Assert.Equal(3, KsanteKeywordValueTests.Keyword(game.State, "HOST", "坚守"));
        Assert.Equal(4, game.State.CardObjects["ENEMY"].Damage);
        Assert.Contains("COMBAT_KEYWORD:坚守2:1", game.State.CardObjects["HOST"].UntilEndOfTurnEffects);
        await game.Replay();
    }

    [Fact]
    public async Task ARealCounterPreventsBothKeywordAndDamageInstructionsWithoutRefundingTheCost()
    {
        var game = new Driver(EquipChainTests.Response(Position(), "SFD·045/221", "P2"));
        await game.Act("P1", Cast()); var skill = Assert.Single(game.State.StackItems);
        await game.Act("P1", new PassPriorityCommand());
        await game.Act("P2", new PlayCardCommand("RESPONSE", "SFD·045/221", [skill.StackItemId])); await game.Top();
        Assert.Empty(game.State.StackItems); Assert.Equal(0, game.State.CardObjects["ENEMY"].Damage);
        Assert.DoesNotContain("COMBAT_KEYWORD:坚守2:1", game.State.CardObjects["HOST"].UntilEndOfTurnEffects);
        Assert.Equal(0, game.State.RunePools["P1"].Mana); await game.Replay();
    }

    [Fact]
    public async Task IndependentSteadfastGrantsStackAndBothExpireAtTurnEnd()
    {
        var s = Position();
        s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
            ["HOST"] = s.CardObjects["HOST"] with { UntilEndOfTurnEffects = ["COMBAT_KEYWORD:坚守2:1"] } } };
        var game = new Driver(s); await game.Act("P1", Cast()); await game.Top();
        Assert.Equal(4, game.State.CardObjects["ENEMY"].Damage);
        Assert.Contains("COMBAT_KEYWORD:坚守2:2", game.State.CardObjects["HOST"].UntilEndOfTurnEffects);
        await game.Act("P1", new EndTurnCommand()); Assert.Empty(game.State.CardObjects["HOST"].UntilEndOfTurnEffects); await game.Replay();
    }

    [Fact]
    public async Task DamagePreventionLeavesTheEarlierSteadfastGrantIntact()
    {
        var game = new Driver(EquipChainTests.Response(Position(1), "OGN·145/298", "P2"));
        await game.Act("P1", Cast()); await game.Act("P1", new PassPriorityCommand());
        await game.Act("P2", new PlayCardCommand("RESPONSE", "OGN·145/298", [])); await game.Top(); await game.Top();
        Assert.Equal(0, game.State.CardObjects["ENEMY"].Damage);
        Assert.Contains("COMBAT_KEYWORD:坚守2:1", game.State.CardObjects["HOST"].UntilEndOfTurnEffects); await game.Replay();
    }

    [Fact]
    public async Task ASubsequentRealKeywordGrantIsAddedAfterTheFixedDoublingRatherThanMultipliedAgain()
    {
        var s = AttackDamageDistributionTests.Spell(KsanteKeywordValueTests.Board(), "SPELL", Spell, "P1");
        s = s with { RunePools = new Dictionary<string, RunePool>(s.RunePools) { ["P1"] = new(2, 0, new Dictionary<string, int> { [RuneTrait.Orange] = 1 }) } };
        var game = new Driver(s); await KsanteKeywordValueTests.Pending(game, "HOST"); await game.Top();
        Assert.Equal(2, KsanteKeywordValueTests.Keyword(game.State, "HOST", "坚守"));
        await game.Act("P1", new PlayCardCommand("SPELL", Spell, ["HOST", "ENEMY1"])); await game.Top();
        Assert.Equal(4, KsanteKeywordValueTests.Keyword(game.State, "HOST", "坚守"));
        Assert.Equal(4, game.State.CardObjects["ENEMY1"].Damage);
        Assert.Contains("COMBAT_KEYWORD:坚守1:1", game.State.CardObjects["HOST"].UntilEndOfTurnEffects);
        Assert.Contains("COMBAT_KEYWORD:坚守2:1", game.State.CardObjects["HOST"].UntilEndOfTurnEffects); await game.Replay();
    }

    private static MatchState Position(int keyword = 0)
    {
        var s = ArmamentMightTests.Position(power: 3);
        s = DeathObserverAuditTests.Source(s, "OGN·001/298", "ENEMY");
        s = LocalDestructionRecallTests.At(s, "ENEMY", "P2", "BASE");
        s = s with {
            CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
                ["HOST"] = s.CardObjects["HOST"] with { Tags = keyword == 0 ? [CardObjectTags.UnitCard] : [CardObjectTags.UnitCard, "坚守" + keyword] },
                ["ENEMY"] = s.CardObjects["ENEMY"] with { Power = 10 } },
            RunePools = new Dictionary<string, RunePool>(s.RunePools) { ["P1"] = new(2, 0, new Dictionary<string, int> { [RuneTrait.Orange] = 1 }), ["P2"] = new(20, 20) } };
        return AttackDamageDistributionTests.Spell(s, "SPELL", Spell, "P1");
    }
    private static async Task Reject(MatchState s, string player, GameCommand command)
    {
        var hash = MatchStateHasher.Hash(s); var result = await new CoreRuleEngine().ResolveAsync(s, new("ksante-damage-invalid", player, command.CmdType), command, default);
        Assert.False(result.Accepted); Assert.Empty(result.Events); Assert.Equal(hash, MatchStateHasher.Hash(result.State));
    }
}
