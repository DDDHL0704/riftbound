using System.Reflection;
using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;

namespace Riftbound.ConformanceTests;

public sealed class RadCombatKeywordDoubleTests
{
    private const string Hero = "RAD·045/167";
    [Theory]
    [InlineData("当我进攻或防守时，让此处你控制的一名单位在本回合内{{坚守}}数值翻倍。", true)]
    [InlineData("当我进攻或防守时, 让此处你控制的一名单位在本回合内{{坚守}}数值翻倍.", true)]
    [InlineData("当我进攻或防守时，让此处你控制的一名单位在本回合内{{坚守}}数值翻倍", true)]
    [InlineData("当我进攻时，让此处你控制的一名单位在本回合内{{坚守}}数值翻倍。", false)]
    [InlineData("当我进攻或防守时，让此处的一名敌方单位在本回合内{{坚守}}数值翻倍。", false)]
    [InlineData("当我进攻或防守时，让此处你控制的所有单位在本回合内{{坚守}}数值翻倍。", false)]
    public void OnlyTheOfficialFriendlyKeywordDoublingIsParsed(string text, bool expected)
        => Assert.Equal(expected, TriggerParser.Parse(text).Any(t => t.Kind == TriggerKinds.UnitAttackOrDefendDoubleFriendlyKeywordHere));

    [Theory, InlineData("RAD·045/167", 39540), InlineData("RAD·045a/167", 39541)]
    public async Task EveryHeroPrintingUsesPrintedPaymentAndKeepsItsChampionIdentity(string no, long id)
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, c => c.CardNo == no);
        Assert.Equal(id, card.Id); Assert.Equal("奎桑提", card.CardName); Assert.Equal("傲然无畏", card.SubTitle);
        Assert.Equal(4, card.Energy); Assert.Equal(1, card.ReturnEnergy); Assert.Equal(4, card.Power);
        Assert.Equal(new[] { "green" }, card.CardColorList);
        var spec = Assert.Single(TriggerParser.Parse(card.CardEffect), t => t.Kind == TriggerKinds.UnitAttackOrDefendDoubleFriendlyKeywordHere);
        Assert.Equal(TriggerTargetScopes.FriendlyUnitHere, spec.TargetScope); Assert.Equal("坚守", spec.GrantedKeyword);
        var s = VenUnitEntryBasicTests.Position(no);
        s = s with {
            PlayerZones = new Dictionary<string, PlayerZones>(s.PlayerZones) { ["P1"] = s.PlayerZones["P1"] with { Hand = [], ChampionZone = ["UNIT"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(s.ObjectLocations) { ["UNIT"] = new("P1", "CHAMPION") },
            RunePools = new Dictionary<string, RunePool>(s.RunePools) { ["P1"] = new(4, 0, new Dictionary<string, int> { [RuneTrait.Green] = 1 }) } };
        var game = new Driver(s); await game.Act("P1", new PlayCardCommand("UNIT", no, [], Destination: "BASE"));
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Base); Assert.Empty(game.State.PlayerZones["P1"].ChampionZone);
        Assert.Equal(0, game.State.RunePools["P1"].Mana); Assert.Equal(0, game.State.RunePools["P1"].TotalPower);
        Assert.True(game.State.CardObjects["UNIT"].IsExhausted); Assert.Equal(4, game.State.CardObjects["UNIT"].Power);
        Assert.Equal(1, Keyword(game.State, "UNIT", "坚守")); Assert.Empty(game.State.StackItems); await game.Replay();
    }

    [Theory, InlineData("RAD·045/167", false), InlineData("RAD·045a/167", false)]
    [InlineData("RAD·045/167", true), InlineData("RAD·045a/167", true)]
    public async Task AHeroWithoutItsPrintedGreenPowerCannotEnterOrSpendMana(string no, bool wrongColor)
    {
        var s = VenUnitEntryBasicTests.Position(no);
        s = s with { RunePools = new Dictionary<string, RunePool>(s.RunePools) {
            ["P1"] = new(4, 0, wrongColor ? new Dictionary<string, int> { [RuneTrait.Orange] = 1 } : null) } };
        await Reject(s, "P1", new PlayCardCommand("UNIT", no, [], Destination: "BASE"));
    }

    [Theory, InlineData("RAD·045/167", false), InlineData("RAD·045a/167", false)]
    [InlineData("RAD·045/167", true), InlineData("RAD·045a/167", true)]
    public async Task AttackingAndDefendingBothConfirmOneFriendlyHereAndDoubleOnlyAtResolution(string no, bool defending)
    {
        var source = defending ? "ENEMY1" : "HOST";
        var game = new Driver(Board(no, defending));
        await game.Act("P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F", []));
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        Assert.Contains(source, choice.LegalObjectIds); Assert.DoesNotContain(defending ? "HOST" : "ENEMY1", choice.LegalObjectIds);
        Assert.DoesNotContain("GEAR", choice.LegalObjectIds); Assert.Equal(1, Keyword(game.State, source, "坚守"));
        await game.Act(choice.PlayerId, new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [source]));
        Assert.Null(game.State.PendingPayment); Assert.Equal(1, Keyword(game.State, source, "坚守"));
        await game.Top(); Assert.Equal(2, Keyword(game.State, source, "坚守"));
        Assert.Equal(defending ? 6 : 4, EffectivePower(game.State, source, defending ? "P2" : "P1")); await game.Replay();
    }

    [Theory, InlineData("enemy"), InlineData("equipment"), InlineData("other-location"), InlineData("missing")]
    [InlineData("duplicate"), InlineData("wrong-player")]
    public async Task InvalidHeroChoicesLeaveTheOriginalChoiceAndAllStateUntouched(string branch)
    {
        var s = Board(); s = DeathObserverAuditTests.Source(s, "OGN·001/298", "REMOTE-UNIT", "P1");
        var game = new Driver(s); await game.Act("P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F", []));
        var choice = game.State.PendingCardChoice!;
        var targets = branch switch { "enemy" => new[] { "ENEMY1" }, "equipment" => ["GEAR"], "other-location" => ["REMOTE-UNIT"],
            "missing" => [], "duplicate" => ["HOST", "HOST"], _ => ["HOST"] };
        await Reject(game.State, branch == "wrong-player" ? "P2" : "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, targets));
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["HOST"]));
        await game.Top(); Assert.Equal(2, Keyword(game.State, "HOST", "坚守"));
    }

    [Fact]
    public async Task CopiedCombatTextCreatesIndependentSnapshotDoublingsRatherThanALiveMultiplier()
    {
        var game = new Driver(Board(copied: true)); await Pending(game, "HOST");
        Assert.Equal(2, game.State.StackItems.Count); Assert.Single(game.State.StackItems, i => i.FieldContext!.Origin is not null);
        // Each printed text instance contributes one Steadfast before either skill resolves.
        Assert.Equal(2, Keyword(game.State, "HOST", "坚守"));
        await game.Top(); Assert.Equal(4, Keyword(game.State, "HOST", "坚守"));
        await game.Top(); Assert.Equal(8, Keyword(game.State, "HOST", "坚守"));
        Assert.Contains("COMBAT_KEYWORD:坚守2:1", game.State.CardObjects["HOST"].UntilEndOfTurnEffects);
        Assert.Contains("COMBAT_KEYWORD:坚守4:1", game.State.CardObjects["HOST"].UntilEndOfTurnEffects); await game.Replay();
    }

    [Fact]
    public async Task ZeroSteadfastIsALegalTargetAndFriendlySpellshieldDoesNotCreateATax()
    {
        var s = DeathObserverAuditTests.Source(Board(), "OGN·001/298", "ALLY", "P1");
        s = LocalDestructionRecallTests.At(s, "ALLY", "P1", "BASE");
        s = WithCard(s, "ALLY", c => c with { Tags = [CardObjectTags.UnitCard, "法盾2"] });
        var game = new Driver(s); await Pending(game, "ALLY"); Assert.Null(game.State.PendingPayment);
        await game.Top(); Assert.Equal(0, Keyword(game.State, "ALLY", "坚守"));
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "STATUS_EFFECT_APPLIED"); await game.Replay();
    }

    [Theory, InlineData("source"), InlineData("target")]
    public async Task ARealRecallInvalidatesHereOrTheTargetAndNeverLeaksTheNewHandIdentity(string branch)
    {
        var s = WithCard(Board(), "HOST", c => c with { Power = 3 });
        s = DeathObserverAuditTests.Source(s, "OGN·001/298", "ALLY", "P1");
        s = LocalDestructionRecallTests.At(s, "ALLY", "P1", "BASE");
        s = WithCard(s, "ALLY", c => c with { Power = 3, Tags = [CardObjectTags.UnitCard, "坚守2"] });
        var game = new Driver(RadCombatHeroTests.Response(s, "OGN·169/298", "P2"));
        await Pending(game, "ALLY"); await game.Act("P1", new PassPriorityCommand());
        var recalled = branch == "source" ? "HOST" : "ALLY";
        await game.Act("P2", new PlayCardCommand("RESPONSE", "OGN·169/298", [recalled])); await game.Top();
        Assert.Contains(recalled, game.State.PlayerZones["P1"].Hand); await game.Top();
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "STATUS_EFFECT_APPLIED");
        Assert.DoesNotContain('"' + recalled + '"', JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(game.State)["P2"].Stack));
        await game.Replay();
    }

    [Theory, InlineData("card"), InlineData("generation"), InlineData("location"), InlineData("origin")]
    public async Task RecoveryRejectsForgedCombatKeywordOrigins(string branch)
    {
        var game = new Driver(Board()); await Pending(game, "HOST");
        var item = Assert.Single(game.State.StackItems); var context = item.FieldContext!;
        var forged = item with { FieldContext = branch switch {
            "card" => context with { CardNo = "OGN·001/298" }, "generation" => context with { SourceGeneration = 999 },
            "location" => context with { SourceLocation = new("P1", "BASE") },
            _ => context with { Origin = new(Hero, new("HOST", 0), new("INVENTED", 0), 1, 1, 0) } } };
        Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(game.State with { StackItems = [forged] }));
    }

    internal static MatchState Board(string no = Hero, bool defending = false, bool copied = false)
    {
        var s = AhriCombatMightTests.Board(no, defending, copied); var source = defending ? "ENEMY1" : "HOST";
        return WithCard(s, source, c => c with { Power = 4, Tags = [CardObjectTags.UnitCard, "坚守"] });
    }
    private static MatchState WithCard(MatchState s, string id, Func<CardObjectState, CardObjectState> change)
        => s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) { [id] = change(s.CardObjects[id]) } };
    internal static async Task Pending(Driver game, string target)
    {
        var movers = target != "HOST" && game.State.PlayerZones["P1"].Base.Contains(target, StringComparer.Ordinal)
            ? new[] { "HOST", target } : new[] { "HOST" };
        await game.Act("P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F", [], SourceObjectIds: movers));
        if (game.State.TriggerQueue.Count > 0) await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: game.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
        for (var n = 0; game.State.PendingCardChoice is { ChoiceWindow: "TRIGGER_CONFIRMATION" } choice; n++) {
            Assert.True(n < 4); await game.Act(choice.PlayerId, new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [target]));
        }
    }
    internal static int Keyword(MatchState s, string id, string keyword)
        => (int)typeof(CoreRuleEngine).GetMethod("ResolveCurrentCountedCombatKeyword", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, [s, s.CardObjects[id], keyword])!;
    private static int EffectivePower(MatchState s, string id, string player)
        => CurrentPowerTargetTests.Snapshot(s).GetProperty("players").GetProperty(player).GetProperty("objects").GetProperty(id).GetProperty("effectivePower").GetInt32();
    private static async Task Reject(MatchState s, string player, GameCommand command)
    {
        var hash = MatchStateHasher.Hash(s); var result = await new CoreRuleEngine().ResolveAsync(s, new("ksante-invalid", player, command.CmdType), command, default);
        Assert.False(result.Accepted); Assert.Empty(result.Events); Assert.Equal(hash, MatchStateHasher.Hash(result.State));
    }
}
