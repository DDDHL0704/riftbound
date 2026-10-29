using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class MordekaiserSacrificeRecallTests
{
    private const string No = "RAD·020/167";
    private const string Kind = ReplacementKinds.SourceUnitDestroyedDestroyFriendlyRecallExhaustedOncePerTurn;
    private const string Kill = "OGN·229/298";

    private static MatchState Position(string no = No, bool copied = false)
    {
        var state = VenUnitEntryBasicTests.Position(no);
        state = state with { PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P1"] = state.PlayerZones["P1"] with { Hand = [], Base = [..state.PlayerZones["P1"].Base, "UNIT"] } },
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["UNIT"] = state.CardObjects["UNIT"] with { Damage = 2 } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["UNIT"] = new("P1", "BASE") },
            RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(100, 30) } };
        state = HandDiscardObserverTests.Put(state, "ALLY2", "OGN·219/298", "P1", "BASE");
        foreach (var id in new[] { "KILL1", "KILL2", "KILL3" })
            state = AttackDamageDistributionTests.Spell(state, id, Kill, "P1");
        if (copied) state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["GEAR"] = OfficialGarenConquestBattlefieldCountTests.Printed("GEAR", "SFD·059/221", "P1") with {
                AttachedToObjectId = "UNIT", AttachmentRevision = 1,
                AttachedRuleText = new(new("UNIT", 0), no, 1, state.Tick, 0) } } };
        return state;
    }

    private static async Task Destroy(Driver game, string spell = "KILL1", string target = "UNIT")
    {
        await game.Act("P1", new PlayCardCommand(spell, Kill, [target]));
        await game.Top();
    }

    private static async Task Select(Driver game, string sacrifice = "ALLY", bool copied = false)
    {
        var request = Assert.IsType<RuleChoiceContinuation>(game.State.PendingRuleChoice).Request;
        var id = "SACRIFICE_RECALL:UNIT:" + sacrifice + (copied ? ":ATTACHED:GEAR:0:1" : "");
        Assert.Contains(request.Options, o => o.Id == id);
        await game.Act(request.PlayerId, new PayCostCommand(request.Id, "RULE_REPLACEMENT", [id]));
    }

    private static async Task Reject(MatchState state, string player, GameCommand command)
    {
        var hash = MatchStateHasher.Hash(state);
        var views = JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(state));
        var prompts = JsonSerializer.Serialize(ResolutionResult.BuildPrompts(state));
        var result = await new CoreRuleEngine().ResolveAsync(state, new("invalid-sacrifice", player, command.CmdType), command, default);
        Assert.False(result.Accepted); Assert.Empty(result.Events);
        Assert.Equal(hash, MatchStateHasher.Hash(result.State));
        Assert.Equal(views, JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(result.State)));
        Assert.Equal(prompts, JsonSerializer.Serialize(ResolutionResult.BuildPrompts(result.State)));
    }

    [Theory]
    [InlineData("RAD·020/167", 39510)] [InlineData("RAD·020a/167", 39511)]
    public async Task BothOfficialPrintingsKeepCostsKeywordsAndTheLimitedOptionalReplacement(string no, long id)
    {
        var catalog = await OfficialCardCatalog.LoadRuntimeAsync(); var card = Assert.Single(catalog.Cards, c => c.CardNo == no);
        Assert.Equal(id, card.Id); Assert.Equal("莫德凯撒", card.Hero); Assert.Equal("冥途二渡", card.SubTitle);
        Assert.Equal(7, card.Energy); Assert.Equal(1, card.ReturnEnergy); Assert.Equal(5, card.Power);
        Assert.Contains("每回合限一次，如果我将被摧毁，则你可以选择改为摧毁一名友方单位，以此移除我所受伤害、让我变为休眠状态、并将我召回。", card.CardEffect);
        Assert.Equal("source-unit-destroyed", Assert.Single(RuleTextParser.Parse(card).Replacements, r => r.Kind == Kind).AppliesTo);
        Assert.True(CardBehaviorRegistry.TryGetByCardNo(no, out var behavior));
        Assert.Equal(7, behavior.ManaCost); Assert.Equal(5, behavior.SourceUnitPower);
        Assert.Contains("急速", behavior.SourceUnitTags); Assert.Contains("强攻3", behavior.SourceUnitTags);
        Assert.Equal(1, PrintedPowerCostRules.ForCard(no).Amount);
        var implemented = OfficialRuleDomainBehaviorCatalog.MergeWithNonPlayCardDomains(catalog.Cards,
            CardBehaviorRegistry.GetAll().Select(b => new ImplementedCardBehavior(b.CardNo, b.EffectKind, b.DisplayName,
                CardBehaviorRegistry.TriggerEffectKinds(b))).ToArray());
        Assert.Equal(BehaviorImplementationStatuses.Implemented, Assert.Single(BehaviorSpecCatalogBuilder.Build(catalog.Cards,
            FunctionalUnitBuilder.Build(catalog.Cards), implemented), s => s.CardNo == no).Status);
    }

    [Theory]
    [InlineData("RAD·020/167", false)] [InlineData("RAD·020/167", true)]
    [InlineData("RAD·020a/167", false)] [InlineData("RAD·020a/167", true)]
    public async Task RealHeroPlayPaysPrintedPowerAndTheChosenHasteCost(string no, bool haste)
    {
        var game = new Driver(VenUnitEntryBasicTests.Position(no));
        await game.Act("P1", new PlayCardCommand("UNIT", no, [], OptionalCosts: haste ? ["HASTE_READY"] : [], Destination: "BASE"));
        Assert.Equal(haste ? 12 : 13, game.State.RunePools["P1"].Mana);
        Assert.Equal(haste ? 18 : 19, game.State.RunePools["P1"].TotalPower);
        Assert.Equal(!haste, game.State.CardObjects["UNIT"].IsExhausted);
        Assert.Equal(5, game.State.CardObjects["UNIT"].Power); Assert.Contains("强攻3", game.State.CardObjects["UNIT"].Tags);
        Assert.Empty(game.State.StackItems); Assert.Null(game.State.PendingRuleChoice); await game.Replay();
    }

    [Theory]
    [InlineData("RAD·020/167", "mana")] [InlineData("RAD·020/167", "power")] [InlineData("RAD·020/167", "haste")]
    [InlineData("RAD·020a/167", "mana")] [InlineData("RAD·020a/167", "power")] [InlineData("RAD·020a/167", "haste")]
    public async Task UnfundedPrintedOrHasteCostsCannotPlayTheHero(string no, string branch)
    {
        var state = VenUnitEntryBasicTests.Position(no);
        state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(branch == "mana" ? 6 : 7,
            branch == "power" ? 0 : 1) } };
        await Reject(state, "P1", new PlayCardCommand("UNIT", no, [], OptionalCosts: branch == "haste" ? ["HASTE_READY"] : [], Destination: "BASE"));
    }

    [Theory]
    [InlineData("RAD·020/167", false)] [InlineData("RAD·020/167", true)]
    [InlineData("RAD·020a/167", false)] [InlineData("RAD·020a/167", true)]
    public async Task TheControllerSacrificesFirstThenRecallsTheSameIncarnationWithoutItsDamage(string no, bool battlefield)
    {
        var state = Position(no); if (battlefield) state = LocalDestructionRecallTests.At(state, "UNIT", "P1", "BATTLEFIELD", "BF");
        var game = new Driver(state); await Destroy(game);
        Assert.Equal("P1", game.State.PendingRuleChoice!.Request.PlayerId);
        Assert.Equal(2, game.State.CardObjects["UNIT"].Damage);
        Assert.Contains("ALLY", game.State.PlayerZones["P1"].Base);
        var count = game.Events().Length; await Select(game);
        Assert.Contains("ALLY", game.State.PlayerZones["P1"].Graveyard); Assert.Contains("UNIT", game.State.PlayerZones["P1"].Base);
        var hero = game.State.CardObjects["UNIT"];
        Assert.Equal(0, hero.ObjectGeneration); Assert.Equal(0, hero.Damage); Assert.True(hero.IsExhausted);
        Assert.Single(hero.UntilEndOfTurnEffects, e => e.StartsWith("REPLACEMENT_ONCE:", StringComparison.Ordinal));
        var events = game.Events().Skip(count).Select(e => e.Event).ToArray();
        Assert.Single(events, e => e.Kind == "UNIT_DESTROYED" && Equals(e.Payload.GetValueOrDefault("targetObjectId"), "ALLY"));
        Assert.DoesNotContain(events, e => e.Kind == "UNIT_DESTROYED" && Equals(e.Payload.GetValueOrDefault("targetObjectId"), "UNIT"));
        Assert.True(Array.FindIndex(events, e => e.Kind == "UNIT_DESTROYED") < Array.FindIndex(events, e => e.Kind == "UNIT_RECALLED_TO_BASE"));
        Assert.Equal(Kind, Assert.Single(events, e => e.Kind == "UNIT_RECALLED_TO_BASE").Payload["replacementEffectId"]);
        Assert.DoesNotContain(events, e => e.Kind == "UNIT_MOVED"); await game.Replay();
    }

    [Theory]
    [InlineData("RAD·020/167")] [InlineData("RAD·020a/167")]
    public async Task DecliningLeavesTheOptionalCostUnpaidAndTheOriginalDestructionHappens(string no)
    {
        var game = new Driver(Position(no)); await Destroy(game); var request = game.State.PendingRuleChoice!.Request;
        await game.Act("P1", new PayCostCommand(request.Id, "RULE_REPLACEMENT", ["DECLINE"]));
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Graveyard); Assert.Contains("ALLY", game.State.PlayerZones["P1"].Base);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "UNIT_RECALLED_TO_BASE"); await game.Replay();
    }

    [Theory]
    [InlineData("enemy")] [InlineData("equipment")] [InlineData("graveyard")] [InlineData("hand")]
    [InlineData("unknown")] [InlineData("opponent")] [InlineData("wrong-window")] [InlineData("extra")]
    public async Task InvalidReplacementChoicesCannotSpendTheAbilityOrAdvanceTheOriginalDestruction(string branch)
    {
        var game = new Driver(Position()); await Destroy(game); var request = game.State.PendingRuleChoice!.Request;
        var id = "SACRIFICE_RECALL:UNIT:" + (branch switch { "enemy" => "ENEMY", "equipment" => "GEAR", "graveyard" => "GRAVE",
            "hand" => "KILL2", "unknown" => "UNKNOWN", _ => "ALLY" });
        await Reject(game.State, branch == "opponent" ? "P2" : "P1", new PayCostCommand(request.Id,
            branch == "wrong-window" ? "TRIGGER_TARGET_COST" : "RULE_REPLACEMENT", branch == "extra" ? [id, "DECLINE"] : [id]));
        await Select(game); Assert.Contains("UNIT", game.State.PlayerZones["P1"].Base); await game.Replay();
    }

    [Fact]
    public async Task ASpentInstanceCannotReplaceASecondDestructionInTheSameTurn()
    {
        var game = new Driver(Position()); await Destroy(game); await Select(game);
        await Destroy(game, "KILL2"); Assert.Null(game.State.PendingRuleChoice);
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Graveyard); Assert.Contains("ALLY2", game.State.PlayerZones["P1"].Base);
        await game.Replay();
    }

    [Fact]
    public async Task ANewTurnMakesTheSameInstanceAvailableAgain()
    {
        var game = new Driver(Position()); await Destroy(game); await Select(game);
        await game.Act("P1", new EndTurnCommand()); await game.Act("P2", new EndTurnCommand());
        Assert.DoesNotContain(game.State.CardObjects["UNIT"].UntilEndOfTurnEffects, e => e.StartsWith("REPLACEMENT_ONCE:", StringComparison.Ordinal));
        await Destroy(game, "KILL2"); await Select(game, "ALLY2");
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Base); Assert.Contains("ALLY2", game.State.PlayerZones["P1"].Graveyard);
        await game.Replay();
    }

    [Fact]
    public async Task ReplacingTheSacrificeStillPaysTheCostAndSpendsTheOncePerTurnUse()
    {
        var state = Position(); state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["ALLY"] = state.CardObjects["ALLY"] with { UntilEndOfTurnEffects = ["RECALL_TO_BASE_EXHAUSTED_IF_DESTROYED_THIS_TURN"] } } };
        var game = new Driver(state); await Destroy(game); await Select(game);
        Assert.Contains("ALLY", game.State.PlayerZones["P1"].Base); Assert.Contains("UNIT", game.State.PlayerZones["P1"].Base);
        Assert.Equal(2, game.Events().Count(e => e.Event.Kind == "UNIT_RECALLED_TO_BASE"));
        await Destroy(game, "KILL2"); Assert.Contains("UNIT", game.State.PlayerZones["P1"].Graveyard); await game.Replay();
    }

    [Fact]
    public async Task ChoosingTheSourceItselfDoesNotLoopOrRecallItsNewGraveyardIncarnation()
    {
        var game = new Driver(Position()); await Destroy(game); await Select(game, "UNIT");
        Assert.Null(game.State.PendingRuleChoice); Assert.Contains("UNIT", game.State.PlayerZones["P1"].Graveyard);
        Assert.Single(game.Events(), e => e.Event.Kind == "UNIT_DESTROYED");
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "UNIT_RECALLED_TO_BASE"); await game.Replay();
    }

    [Fact]
    public async Task PrintedAndCopiedTextSpendIndependentUsesAndKeepTheAttachedEquipmentOnRecall()
    {
        var state = Position(copied: true);
        state = LocalDestructionRecallTests.At(LocalDestructionRecallTests.At(state, "UNIT", "P1", "BATTLEFIELD", "BF"), "GEAR", "P1", "BATTLEFIELD", "BF");
        var game = new Driver(state); await Destroy(game); await Select(game);
        Assert.Contains("GEAR", game.State.PlayerZones["P1"].Base); Assert.Equal("UNIT", game.State.CardObjects["GEAR"].AttachedToObjectId);
        await Destroy(game, "KILL2"); await Select(game, "ALLY2", copied: true);
        Assert.Equal(2, game.State.CardObjects["UNIT"].UntilEndOfTurnEffects.Count(e => e.StartsWith("REPLACEMENT_ONCE:", StringComparison.Ordinal)));
        await Destroy(game, "KILL3"); Assert.Contains("UNIT", game.State.PlayerZones["P1"].Graveyard); await game.Replay();
    }

    [Fact]
    public async Task MutuallySacrificingHeroesFinishTheNestedReplacementChainOncePerInstance()
    {
        var state = Position(); state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["ALLY"] = OfficialGarenConquestBattlefieldCountTests.Printed("ALLY", No, "P1") } };
        var game = new Driver(state); await Destroy(game); await Select(game);
        var request = Assert.IsType<RuleChoiceContinuation>(game.State.PendingRuleChoice).Request;
        await game.Act("P1", new PayCostCommand(request.Id, "RULE_REPLACEMENT", ["SACRIFICE_RECALL:ALLY:UNIT"]));
        Assert.Null(game.State.PendingRuleChoice); Assert.Contains("UNIT", game.State.PlayerZones["P1"].Graveyard);
        Assert.Contains("ALLY", game.State.PlayerZones["P1"].Base); Assert.True(game.State.CardObjects["ALLY"].IsExhausted);
        Assert.Single(game.Events(), e => e.Event.Kind == "UNIT_DESTROYED"); await game.Replay();
    }

    [Fact]
    public async Task StolenUnitsUseTheirControllerForTheChoiceAndTheirOwnerForTheGraveyard()
    {
        var state = Position(); state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["UNIT"] = state.CardObjects["UNIT"] with { OwnerId = "P2" },
            ["ALLY"] = state.CardObjects["ALLY"] with { OwnerId = "P2" } } };
        var game = new Driver(state); await Destroy(game); await Select(game);
        Assert.Contains("ALLY", game.State.PlayerZones["P2"].Graveyard); Assert.Contains("UNIT", game.State.PlayerZones["P1"].Base);
        Assert.Equal("P2", game.State.CardObjects["UNIT"].OwnerId); Assert.Equal("P1", game.State.CardObjects["UNIT"].ControllerId);
        await game.Replay();
    }

    [Fact]
    public async Task RecoveryRejectsAlteredReplacementOptionsOrAnswerHistory()
    {
        var game = new Driver(Position()); await Destroy(game); var pending = game.State.PendingRuleChoice!;
        Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(game.State with { PendingRuleChoice = pending with {
            Request = pending.Request with { Options = [new("DECLINE", "forged")] } } }));
        Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(game.State with { PendingRuleChoice = pending with {
            Answers = [new("forged", "SACRIFICE_RECALL:UNIT:ALLY")] } }));
        await Select(game); await game.Replay();
    }
}
