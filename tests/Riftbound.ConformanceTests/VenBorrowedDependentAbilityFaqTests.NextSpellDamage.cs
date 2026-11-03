using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed partial class VenBorrowedDependentAbilityFaqTests
{
    private const string SpellDamagePrefix = "ACTIVATED_NEXT_SPELL_DAMAGE:";
    private const string TomeAbility = "RAVENBLOOM_TOME_NEXT_SPELL_DAMAGE";

    private static MatchState NextSpellDamagePosition()
    {
        var state = BorrowedBombPosition("OGN·032/298");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["UNIT"] = state.CardObjects["UNIT"] with { IsExhausted = false },
            ["ALLY"] = state.CardObjects["ALLY"] with { Power = 30 },
            ["ENEMY"] = state.CardObjects["ENEMY"] with { Power = 30 } } };
        state = RadUnitEffectTestFixture.Place(state, "ALLY", "P2", "BATTLEFIELD", "OTHER");
        state = RadUnitEffectTestFixture.Place(state, "ENEMY", "P2", "BATTLEFIELD", "OTHER");
        foreach (var id in new[] { "FIRE", "SECOND" })
            state = AttackDamageDistributionTests.Spell(state, id, "SFD·080/221", "P1");
        return AttackDamageDistributionTests.Spell(state, "DRAW", "OGN·048/298", "P1");
    }

    private static string NextSpellDamageAbility(MatchState state, bool borrowed)
        => borrowed ? TemplateId(state, "下一个法术") : TomeAbility;

    private static ActivatedPlayGrant[] SpellDamageGrants(MatchState state)
        => state.UntilEndOfTurnEffects.Where(m => m.StartsWith(SpellDamagePrefix, StringComparison.Ordinal))
            .Select(m => JsonSerializer.Deserialize<ActivatedPlayGrant>(Convert.FromBase64String(m[SpellDamagePrefix.Length..]))!).ToArray();

    private static async Task CloseTomeSpellDuel(Driver game)
    {
        while (game.State.StackItems.Count == 0 && game.State.FocusPlayerId is { } player)
            await game.Act(player, new PassFocusCommand());
    }

    [Fact]
    public async Task NextSpellDamageUsesTheExactOfficialTurnLimitedErratum()
    {
        var card = Assert.Single((await Riftbound.CardCatalog.OfficialCardCatalog.LoadRuntimeAsync()).Cards,
            c => c.CardNo == "OGN·032/298");
        Assert.Equal(31240, card.Id);
        Assert.Equal("{{横置}}：本回合内你打出的下一个法术造成的伤害+1（每段伤害都+1）。", card.CardEffect);
    }

    // CN 392, 393, 715.1/2: each damage action of the confirmed next spell,
    // including each Echo, benefits. Responses before the grant do not consume it.
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, true, false)]
    [InlineData(true, false, true)]
    public async Task NextSpellDamageBindsOnlyAfterResolutionAndAppliesToEveryTargetAndEcho(bool borrowed, bool echo, bool early)
    {
        var initial = NextSpellDamagePosition();
        var source = borrowed ? "HOST" : "UNIT";
        var ability = NextSpellDamageAbility(initial, borrowed);
        await RadGearTestPosition.Reject(initial, "P2", new ActivateAbilityCommand(source, ability, []));
        await RadGearTestPosition.Reject(initial, "P1", new ActivateAbilityCommand(source, ability, ["ALLY"]));
        var game = new Driver(initial);
        await game.Act("P1", new ActivateAbilityCommand(source, ability, []));
        Assert.Empty(SpellDamageGrants(game.State));
        if (early)
        {
            await game.Act("P1", new PlayCardCommand("DRAW", "OGN·048/298", []));
            await game.Top();
            Assert.Equal(0, game.State.CardObjects["ENEMY"].Damage);
        }
        await game.Top();
        await CloseTomeSpellDuel(game);
        Assert.Null(Assert.Single(SpellDamageGrants(game.State)).SpellPlayId);
        await game.Act("P1", new PlayCardCommand("FIRE", "SFD·080/221", ["ALLY", "ENEMY"], OptionalCosts: echo ? ["ECHO"] : []));
        Assert.Equal(Assert.Single(game.State.StackItems).StackItemId, Assert.Single(SpellDamageGrants(game.State)).SpellPlayId);
        await game.Top();
        var damage = echo ? 4 : 2;
        Assert.Equal(damage, game.State.CardObjects["ALLY"].Damage);
        Assert.Equal(damage, game.State.CardObjects["ENEMY"].Damage);
        if (!early)
        {
            await CloseTomeSpellDuel(game);
            await game.Act("P1", new PlayCardCommand("SECOND", "SFD·080/221", ["ENEMY"]));
            await game.Top();
            Assert.Equal(damage + 1, game.State.CardObjects["ENEMY"].Damage);
        }
        await game.Replay();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NextSpellDamageIsConsumedByANonDamageSpellAndRejectsForgedBindings(bool borrowed)
    {
        var game = new Driver(NextSpellDamagePosition());
        await game.Act("P1", new ActivateAbilityCommand(borrowed ? "HOST" : "UNIT", NextSpellDamageAbility(game.State, borrowed), []));
        await game.Top();
        var marker = Assert.Single(game.State.UntilEndOfTurnEffects, m => m.StartsWith(SpellDamagePrefix, StringComparison.Ordinal));
        Assert.DoesNotContain(marker, JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(game.State)["P2"]));
        var grant = Assert.Single(SpellDamageGrants(game.State));
        foreach (var forged in new[] { grant with { SpellPlayId = "FORGED" }, grant with { GrantedTick = game.State.Tick + 1 },
            grant with { CardNo = "FORGED" }, grant with { Kind = "NEXT_UNIT_READY" }, grant with { Borrowed = null, Printed = null } })
        {
            var bad = SpellDamagePrefix + Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(forged));
            Assert.Contains("invalid activated next play grant", OfficialInsightAndSpellLockTests.Errors(game.State with {
                UntilEndOfTurnEffects = game.State.UntilEndOfTurnEffects.Select(m => m == marker ? bad : m).ToArray() }));
        }
        await RadGearTestPosition.Reject(game.State, "P1", new PlayCardCommand("FIRE", "SFD·080/221", ["MISSING"]));
        Assert.Null(Assert.Single(SpellDamageGrants(game.State)).SpellPlayId);
        await game.Act("P1", new PlayCardCommand("DRAW", "OGN·048/298", []));
        var used = Assert.Single(SpellDamageGrants(game.State));
        Assert.Equal(Assert.Single(game.State.StackItems).StackItemId, used.SpellPlayId);
        await game.Top();
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "DAMAGE_APPLIED");
        await CloseTomeSpellDuel(game);
        await game.Act("P1", new PlayCardCommand("FIRE", "SFD·080/221", ["ENEMY"]));
        await game.Top();
        Assert.Equal(1, game.State.CardObjects["ENEMY"].Damage);
        await game.Replay();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NextSpellDamageSurvivesBorrowerReturnOrCopyTokenCeasing(bool token)
    {
        var initial = NextSpellDamagePosition();
        if (token) initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["HOST"] = initial.CardObjects["HOST"] with { TokenFactoryCardNo = P6TokenFactoryCatalog.ImageTokenCardNo,
                CopyFaceTransitions = [new(0, initial.CardObjects["HOST"].ObjectGeneration,
                    P6TokenFactoryCatalog.ImageTokenCardNo, initial.CardObjects["HOST"].CardNo!)] } } };
        var game = new Driver(initial);
        await game.Act("P1", new ActivateAbilityCommand("HOST", NextSpellDamageAbility(game.State, true), []));
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["HOST"]));
        await game.Top();
        await game.Top();
        Assert.Equal(!token, game.State.CardObjects.ContainsKey("HOST"));
        Assert.Null(Assert.Single(SpellDamageGrants(game.State)).SpellPlayId);
        if (token) Assert.NotNull(Assert.Single(SpellDamageGrants(game.State)).Borrowed!.Borrower.CeasedSource);
        await game.Act("P1", new PlayCardCommand("FIRE", "SFD·080/221", ["ENEMY"]));
        await game.Top();
        Assert.Equal(2, game.State.CardObjects["ENEMY"].Damage);
        await game.Replay();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NextSpellDamageIncreasesDistributedPoolOnceAndPermitsTheExtraTarget(bool borrowed)
    {
        var initial = AttackDamageDistributionTests.Spell(NextSpellDamagePosition(), "SPLIT", "RAD·006/167", "P1");
        var targets = Enumerable.Range(1, 7).Select(i => "TARGET-" + i).ToArray();
        foreach (var id in targets)
            initial = RadUnitEffectTestFixture.Place(HandDiscardObserverTests.Put(initial, id, "OGN·208/298", "P2", "BASE"), id, "P2", "BATTLEFIELD", "OTHER");
        var game = new Driver(initial);
        await game.Act("P1", new ActivateAbilityCommand(borrowed ? "HOST" : "UNIT", NextSpellDamageAbility(game.State, borrowed), []));
        await game.Top();
        await RadGearTestPosition.Reject(game.State, "P1", new PlayCardCommand("SPLIT", "RAD·006/167", targets));
        await game.Act("P1", new PlayCardCommand("SPLIT", "RAD·006/167", targets[..6]));
        await game.Top();
        while (game.State.PendingRuleChoice is { Request: { Kind: "DISTRIBUTED_DAMAGE_AMOUNT" } request })
        {
            Assert.Single(request.Options);
            await game.Act(request.PlayerId, new PayCostCommand(request.Id, "RULE_REPLACEMENT", [request.Options[0].Id]));
        }
        Assert.Empty(game.State.StackItems);
        Assert.All(targets[..6], id => Assert.Equal(1, game.State.CardObjects[id].Damage));
        Assert.Equal(0, game.State.CardObjects[targets[6]].Damage);
        await game.Replay();
    }

    [Fact]
    public async Task NextSpellDamageStacksIndependentGrantsAndUnusedGrantsExpireAtTurnEnd()
    {
        var game = new Driver(NextSpellDamagePosition());
        await game.Act("P1", new ActivateAbilityCommand("HOST", NextSpellDamageAbility(game.State, true), []));
        await game.Top();
        await game.Act("P1", new ActivateAbilityCommand("UNIT", TomeAbility, []));
        await game.Top();
        Assert.Equal(2, SpellDamageGrants(game.State).Length);
        await game.Act("P1", new PlayCardCommand("FIRE", "SFD·080/221", ["ALLY", "ENEMY"]));
        await game.Top();
        Assert.Equal(3, game.State.CardObjects["ALLY"].Damage);
        Assert.Equal(3, game.State.CardObjects["ENEMY"].Damage);
        await CloseTomeSpellDuel(game);
        await game.Act("P1", new EndTurnCommand());
        Assert.Empty(SpellDamageGrants(game.State));
        await game.Replay();

        var unused = new Driver(NextSpellDamagePosition());
        await unused.Act("P1", new ActivateAbilityCommand("UNIT", TomeAbility, []));
        await unused.Top();
        await unused.Act("P1", new EndTurnCommand());
        Assert.Empty(SpellDamageGrants(unused.State));
        await unused.Replay();
    }

    [Fact]
    public async Task NextSpellDamageFollowsTheConfirmedSpellAfterARealControlChange()
    {
        var initial = AttackDamageDistributionTests.Spell(NextSpellDamagePosition(), "STEAL", "OGN·080/298", "P2");
        var game = new Driver(initial);
        await game.Act("P1", new ActivateAbilityCommand("UNIT", TomeAbility, []));
        await game.Top();
        await game.Act("P1", new PlayCardCommand("FIRE", "SFD·080/221", ["ALLY", "ENEMY"]));
        var spell = Assert.Single(game.State.StackItems).StackItemId;
        await game.Act("P1", new PassPriorityCommand());
        await game.Act("P2", new PlayCardCommand("STEAL", "OGN·080/298", [spell]));
        await game.Top();
        var choice = Assert.IsType<RuleChoiceContinuation>(game.State.PendingRuleChoice).Request;
        Assert.Equal("SPELL_CHOICE_REMAKE", choice.Kind);
        await game.Act("P2", new PayCostCommand(choice.Id, "RULE_REPLACEMENT", ["KEEP_ALL"]));
        Assert.Equal("P2", Assert.Single(game.State.StackItems).ControllerId);
        Assert.Equal(spell, Assert.Single(SpellDamageGrants(game.State)).SpellPlayId);
        await game.Top();
        Assert.Equal(2, game.State.CardObjects["ALLY"].Damage);
        Assert.Equal(2, game.State.CardObjects["ENEMY"].Damage);
        await CloseTomeSpellDuel(game);
        await game.Act("P1", new PlayCardCommand("SECOND", "SFD·080/221", ["ENEMY"]));
        await game.Top();
        Assert.Equal(3, game.State.CardObjects["ENEMY"].Damage);
        await game.Replay();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NextSpellDamageBindsAnActualDeckEffectPlay(bool borrowed)
    {
        var initial = HandDiscardObserverTests.Put(OfficialLegendDeckPlayTests.Position(top: ["SFD·080/221", "SFD·042/221"]),
            "TOME", "OGN·032/298", "P1", "BASE");
        if (borrowed) initial = HandDiscardObserverTests.Put(initial, "HOST", "OGN·111/298", "P1", "BASE");
        var game = new Driver(initial);
        await game.Act("P1", new ActivateAbilityCommand(borrowed ? "HOST" : "TOME", NextSpellDamageAbility(game.State, borrowed), []));
        await game.Top();
        await game.Act("P1", new MoveUnitCommand("UNIT", "BASE", "BATTLEFIELD:BF", []));
        await game.Act("P1", new PassFocusCommand());
        await game.Act("P2", new PassFocusCommand());
        var cost = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        await game.Act("P1", new ChooseCardsCommand(cost.ChoiceId, cost.ChoiceWindow, ["LEGEND"]));
        await game.Top();
        var selection = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        await game.Act("P1", new ChooseCardsCommand(selection.ChoiceId, selection.ChoiceWindow, ["DECK0"]));
        Assert.NotNull(game.State.PendingEffectPlay);
        Assert.Null(Assert.Single(SpellDamageGrants(game.State)).SpellPlayId);
        await game.Act("P1", new PlayCardCommand("DECK0", "SFD·080/221", ["UNIT"]));
        Assert.Equal(Assert.Single(game.State.StackItems).StackItemId, Assert.Single(SpellDamageGrants(game.State)).SpellPlayId);
        await game.Top();
        // Void Gate adds one local damage in addition to the Tome grant.
        Assert.Equal(3, game.State.CardObjects["UNIT"].Damage);
        Assert.Contains("DECK0", game.State.PlayerZones["P1"].Graveyard);
        await game.Replay();
    }

    [Theory]
    [InlineData(false, "UNL-072/219")]
    [InlineData(true, "UNL-072/219")]
    [InlineData(false, "OGN·250/298")]
    [InlineData(true, "OGN·250/298")]
    public async Task NextSpellDamageKeepsSpellIdentityForSecondaryAndUnitMightBattlefieldInstructions(bool borrowed, string no)
    {
        var initial = AttackDamageDistributionTests.Spell(NextSpellDamagePosition(), "AREA", no, "P1");
        initial = RadUnitEffectTestFixture.Place(HandDiscardObserverTests.Put(initial, "SECOND-ENEMY", "OGN·208/298", "P2", "BASE"),
            "SECOND-ENEMY", "P2", "BATTLEFIELD", "OTHER");
        initial = RadUnitEffectTestFixture.Place(initial, "ALLY", "P1", "BASE");
        var game = new Driver(initial);
        await game.Act("P1", new ActivateAbilityCommand(borrowed ? "HOST" : "UNIT", NextSpellDamageAbility(game.State, borrowed), []));
        await game.Top();
        var fromMight = no == "OGN·250/298";
        var amount = fromMight ? initial.CardObjects["HOST"].Power + 1 : 5;
        await game.Act("P1", new PlayCardCommand("AREA", no, fromMight ? ["HOST", "OTHER"] : ["OTHER", "ENEMY"]));
        await game.Top();
        Assert.Equal(amount, game.State.CardObjects["ENEMY"].Damage);
        Assert.Equal(fromMight ? amount : 2, game.State.CardObjects["SECOND-ENEMY"].Damage);
        Assert.Equal(0, game.State.CardObjects["ALLY"].Damage);
        await game.Replay();
    }

}
