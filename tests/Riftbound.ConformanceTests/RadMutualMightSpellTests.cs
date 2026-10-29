using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class RadMutualMightSpellTests
{
    private const string CardNo = "RAD·144/167";
    private const string Text = "给予一名敌方单位在本回合内{{S}}-2，并给予一名友方单位在本回合内{{S}}+2。让这两名单位相互以自身战力给对方造成伤害。";

    private static MatchState Position(int enemyPower = 4, int allyPower = 4)
    {
        var state = VenUnitEntryBasicTests.Position(CardNo);
        return state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["ENEMY"] = state.CardObjects["ENEMY"] with { Power = enemyPower },
            ["ALLY"] = state.CardObjects["ALLY"] with { Power = allyPower } } };
    }

    private static PlayCardCommand Play() => new("UNIT", CardNo, ["ENEMY", "ALLY"]);

    [Fact]
    public async Task OfficialSignatureSpellKeepsItsExactIdentityCostAndOrderedTargets()
    {
        var catalog = await OfficialCardCatalog.LoadRuntimeAsync();
        var card = Assert.Single(catalog.Cards, c => c.CardNo == CardNo);
        Assert.Equal(39652L, card.Id); Assert.Equal("轮回绝境", card.CardName);
        Assert.Equal("莫德凯撒", card.Hero); Assert.Equal("专属法术", card.CardCategoryName);
        Assert.Equal(Text, card.CardEffect); Assert.Equal(2, card.Energy); Assert.Equal(1, card.ReturnEnergy);
        Assert.Equal(new[] { "red", "yellow" }, card.CardColorList.Order());
        Assert.True(CardBehaviorRegistry.TryGetByCardNo(CardNo, out var behavior));
        Assert.Equal(2, behavior.ManaCost); Assert.Equal(2, behavior.RequiredTargetCount);
        Assert.Equal(CardTargetScopes.EnemyThenFriendlyUnits, behavior.TargetScope);
        Assert.True(behavior.DealsMutualTargetPowerDamage);
        Assert.Equal(-2, behavior.PowerModifierAmount); Assert.Equal(2, behavior.SecondaryPowerModifierAmount);
        Assert.False(behavior.CanPlayDuringPriority); Assert.False(behavior.CanPlayDuringSpellDuel);
        Assert.Equal(1, PrintedPowerCostRules.ForCard(CardNo).Amount);
        var plays = CardBehaviorRegistry.GetAll().Select(b => new ImplementedCardBehavior(
            b.CardNo, b.EffectKind, b.DisplayName, CardBehaviorRegistry.TriggerEffectKinds(b))).ToArray();
        var implemented = OfficialRuleDomainBehaviorCatalog.MergeWithNonPlayCardDomains(catalog.Cards, plays);
        var spec = Assert.Single(BehaviorSpecCatalogBuilder.Build(catalog.Cards, FunctionalUnitBuilder.Build(catalog.Cards), implemented), s => s.CardNo == CardNo);
        Assert.Equal(BehaviorImplementationStatuses.Implemented, spec.Status);
    }

    [Theory]
    [InlineData(false, "red")] [InlineData(false, "yellow")]
    [InlineData(true, "red")] [InlineData(true, "yellow")]
    public async Task BothModificationsPrecedeUnitSourcedDamageAndTheSurvivorsBonusExpires(bool battlefield, string trait)
    {
        var state = Position();
        if (battlefield) state = LocalDestructionRecallTests.At(
            LocalDestructionRecallTests.At(state, "ENEMY", "P2", "BATTLEFIELD", "OTHER"), "ALLY", "P1", "BATTLEFIELD", "BF");
        state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) {
            ["P1"] = new(2, 0, new Dictionary<string, int> { [trait] = 1 }) } };
        var game = new Driver(state);
        var play = Play();
        var quote = new CoreRuleEngine().PreviewPlayCard(state, "P1", PlayCostPreviewTests.Request(state, play));
        Assert.True(quote.CanPay, quote.Message); Assert.Equal(2, quote.Cost!.Mana);
        Assert.Equal(1, quote.Cost.PowerByTrait[trait]); Assert.Equal(0, quote.Cost.GenericPower);
        await game.Act("P1", play);
        Assert.Equal(0, game.State.RunePools["P1"].Mana); Assert.Equal(0, game.State.RunePools["P1"].PowerByTrait.GetValueOrDefault(trait));
        Assert.Equal(4, game.State.CardObjects["ALLY"].Power); Assert.Equal(4, game.State.CardObjects["ENEMY"].Power);
        await game.Top();
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Graveyard);
        Assert.Contains("ENEMY", game.State.PlayerZones["P2"].Graveyard);
        Assert.Equal(6, game.State.CardObjects["ALLY"].Power); Assert.Equal(2, game.State.CardObjects["ALLY"].Damage);
        var effects = game.Events().Select(e => e.Event).Where(e => e.Kind is "POWER_MODIFIED_UNTIL_END_OF_TURN" or "DAMAGE_APPLIED").ToArray();
        Assert.Equal(new[] { "POWER_MODIFIED_UNTIL_END_OF_TURN", "POWER_MODIFIED_UNTIL_END_OF_TURN", "DAMAGE_APPLIED", "DAMAGE_APPLIED" }, effects.Select(e => e.Kind));
        Assert.Equal("ENEMY", effects[0].Payload["targetObjectId"]); Assert.Equal(-2, effects[0].Payload["powerDelta"]);
        Assert.Equal("ALLY", effects[1].Payload["targetObjectId"]); Assert.Equal(2, effects[1].Payload["powerDelta"]);
        AssertDamage(effects[2], "ENEMY", "ALLY", 2); AssertDamage(effects[3], "ALLY", "ENEMY", 6);
        await game.Act("P1", new EndTurnCommand());
        Assert.Equal(4, game.State.CardObjects["ALLY"].Power); Assert.Equal(0, game.State.CardObjects["ALLY"].Damage);
        await game.Replay();
    }

    [Theory]
    [InlineData(1, 0, 1)] [InlineData(2, 0, 1)] [InlineData(8, 6, 2)] [InlineData(10, 8, 2)]
    public async Task ZeroMightAndMutualLethalDamageUseBothModifiedPowers(int enemyPower, int modifiedEnemyPower, int damageEvents)
    {
        var game = new Driver(Position(enemyPower)); await game.Act("P1", Play()); await game.Top();
        var damage = game.Events().Select(e => e.Event).Where(e => e.Kind == "DAMAGE_APPLIED").ToArray();
        Assert.Equal(damageEvents, damage.Length);
        if (modifiedEnemyPower > 0) AssertDamage(damage[0], "ENEMY", "ALLY", modifiedEnemyPower);
        AssertDamage(damage[^1], "ALLY", "ENEMY", 6);
        Assert.Equal(modifiedEnemyPower >= 6, game.State.PlayerZones["P1"].Graveyard.Contains("ALLY"));
        Assert.Equal(modifiedEnemyPower <= 6, game.State.PlayerZones["P2"].Graveyard.Contains("ENEMY"));
        await game.Replay();
    }

    private static void AssertDamage(GameEvent e, string source, string target, int amount)
    {
        Assert.Equal("UNIT", e.Payload["sourceObjectId"]);
        Assert.Equal(source, e.Payload["damageSourceObjectId"]);
        Assert.Equal(target, e.Payload["targetObjectId"]); Assert.Equal(amount, e.Payload["damage"]);
    }

    [Fact]
    public void PromptOffersEnemyThenFriendlyPublicUnitsInTheSameOrderAsAuthoritativeValidation()
    {
        var candidate = Assert.Single(new Driver(Position()).PromptFor("P1").Candidates!, c => c.Action == CommandTypes.PlayCard);
        var requirements = Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(candidate.Metadata!["sourceRequirements"]);
        var requirement = Assert.Single(requirements, r => Equals(r["sourceObjectId"], "UNIT"));
        var choices = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(requirement["targetChoicesByIndex"]);
        Assert.Equal(new[] { "ENEMY" }, Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(choices["0"]).Select(c => c.Id));
        Assert.Equal(new[] { "ALLY" }, Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(choices["1"]).Select(c => c.Id));
    }

    [Fact]
    public async Task SpellDamagePreventionDoesNotStopDamageDealtByTheTwoUnits()
    {
        var game = new Driver(Position() with { UntilEndOfTurnEffects = ["PREVENT_SPELL_AND_SKILL_DAMAGE_THIS_TURN"] });
        await game.Act("P1", Play()); await game.Top();
        var damage = game.Events().Select(e => e.Event).Where(e => e.Kind == "DAMAGE_APPLIED").ToArray();
        Assert.Equal(2, damage.Length);
        AssertDamage(damage[0], "ENEMY", "ALLY", 2); AssertDamage(damage[1], "ALLY", "ENEMY", 6);
        Assert.Contains("ENEMY", game.State.PlayerZones["P2"].Graveyard);
        await game.Replay();
    }

    [Fact]
    public async Task CounteringTheSpellLeavesBothUnitsUnmodifiedAfterPayingItsPrintedCost()
    {
        var state = AttackDamageDistributionTests.Spell(Position(), "COUNTER", "OGN·045/298", "P2");
        var game = new Driver(state); await game.Act("P1", Play());
        await game.Act("P1", new PassPriorityCommand());
        await game.Act("P2", new PlayCardCommand("COUNTER", "OGN·045/298", [Assert.Single(game.State.StackItems).StackItemId]));
        await game.Top();
        Assert.Empty(game.State.StackItems); Assert.Equal(18, game.State.RunePools["P1"].Mana);
        Assert.Equal(4, game.State.CardObjects["ALLY"].Power); Assert.Equal(4, game.State.CardObjects["ENEMY"].Power);
        Assert.Equal(0, game.State.CardObjects["ALLY"].Damage); Assert.Equal(0, game.State.CardObjects["ENEMY"].Damage);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind is "POWER_MODIFIED_UNTIL_END_OF_TURN" or "DAMAGE_APPLIED");
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Graveyard);
        await game.Replay();
    }

    [Theory]
    [InlineData("missing")] [InlineData("extra")] [InlineData("reversed")] [InlineData("same")]
    [InlineData("equipment")] [InlineData("graveyard")] [InlineData("face-down")]
    [InlineData("mana")] [InlineData("power")] [InlineData("wrong-trait")]
    [InlineData("wrong-turn")] [InlineData("response")]
    public async Task InvalidTargetsTimingAndPaymentLeaveAllStateAndViewsUnchanged(string branch)
    {
        var state = Position(); var command = Play();
        if (branch == "missing") command = command with { TargetObjectIds = ["ENEMY"] };
        if (branch == "extra") command = command with { TargetObjectIds = ["ENEMY", "ALLY", "ALLY"] };
        if (branch == "reversed") command = command with { TargetObjectIds = ["ALLY", "ENEMY"] };
        if (branch == "same") command = command with { TargetObjectIds = ["ENEMY", "ENEMY"] };
        if (branch == "equipment") command = command with { TargetObjectIds = ["ENEMY", "GEAR"] };
        if (branch == "graveyard") command = command with { TargetObjectIds = ["ENEMY", "GRAVE"] };
        if (branch == "face-down") state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["ENEMY"] = state.CardObjects["ENEMY"] with { IsFaceDown = true } } };
        if (branch is "mana" or "power" or "wrong-trait") state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) {
            ["P1"] = branch == "mana" ? new(1, 20) : branch == "power" ? new(20, 0) : new(20, 0, new Dictionary<string, int> { ["blue"] = 1 }) } };
        if (branch == "wrong-turn") state = state with { ActivePlayerId = "P2", TurnPlayerId = "P2" };
        if (branch == "response") {
            state = OfficialCopyIdentityTests.AddSpell(state, "RESPONSE", "OGN·048/298");
            state = (await OfficialGraveyardRecastTests.Act(state, "P1", new PlayCardCommand("RESPONSE", "OGN·048/298", []))).State;
        }
        var hash = MatchStateHasher.Hash(state);
        var snapshots = JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(state));
        var prompts = JsonSerializer.Serialize(ResolutionResult.BuildPrompts(state));
        var result = await new CoreRuleEngine().ResolveAsync(state, new("invalid-realm", "P1", command.CmdType), command, default);
        Assert.False(result.Accepted); Assert.Empty(result.Events); Assert.Equal(hash, MatchStateHasher.Hash(result.State));
        Assert.Equal(snapshots, JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(result.State)));
        Assert.Equal(prompts, JsonSerializer.Serialize(ResolutionResult.BuildPrompts(result.State)));
    }

    [Theory]
    [InlineData("ENEMY")] [InlineData("ALLY")]
    public async Task ReturningEitherTargetPreventsMutualDamageButPreservesTheOtherIndependentModifier(string returned)
    {
        var owner = returned == "ALLY" ? "P1" : "P2";
        var initial = AttackDamageDistributionTests.Spell(Position(), "RETURN", "OGN·104/298", owner);
        var game = new Driver(initial); await game.Act("P1", Play());
        if (owner == "P2") await game.Act("P1", new PassPriorityCommand());
        await game.Act(owner, new PlayCardCommand("RETURN", "OGN·104/298", [returned])); await game.Top();
        Assert.Contains(returned, game.State.PlayerZones[owner].Hand);
        await game.Top();
        var remaining = returned == "ALLY" ? "ENEMY" : "ALLY";
        Assert.Equal(returned == "ALLY" ? 2 : 6, game.State.CardObjects[remaining].Power);
        Assert.Equal(0, game.State.CardObjects[remaining].Damage);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "DAMAGE_APPLIED");
        var modified = Assert.Single(game.Events(), e => e.Event.Kind == "POWER_MODIFIED_UNTIL_END_OF_TURN");
        Assert.Equal(remaining, modified.Event.Payload["targetObjectId"]);
        await game.Act("P1", new EndTurnCommand()); Assert.Equal(4, game.State.CardObjects[remaining].Power);
        await game.Replay();
    }

    [Theory]
    [InlineData("ENEMY")] [InlineData("ALLY")]
    public async Task AnOldTargetBindingCannotAffectAReenteredCopyOfTheSamePhysicalCard(string reentered)
    {
        var state = (await OfficialGraveyardRecastTests.Act(Position(), "P1", Play())).State;
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            [reentered] = state.CardObjects[reentered] with { ObjectGeneration = state.CardObjects[reentered].ObjectGeneration + 2 } } };
        // Isolate CN 124 continuity from the effect responsible for leaving and reentering.
        state = JsonSerializer.Deserialize<MatchState>(JsonSerializer.Serialize(state))!;
        var result = await OfficialGraveyardRecastTests.Top(state);
        Assert.Equal(4, result.State.CardObjects[reentered].Power);
        Assert.Equal(reentered == "ALLY" ? 2 : 6, result.State.CardObjects[reentered == "ALLY" ? "ENEMY" : "ALLY"].Power);
        Assert.DoesNotContain(result.Events, e => e.Kind == "DAMAGE_APPLIED");
        ArmamentMightTests.PublicRecovery(result.State);
    }

    [Fact]
    public async Task SignatureRestrictionStillRejectsADeckLedByAnotherHero()
    {
        var catalog = await OfficialCardCatalog.LoadRuntimeAsync();
        var original = PreconstructedDeckCatalog.Build(catalog).First().Decklist;
        var deck = original with { MainDeck = [CardNo, ..original.MainDeck.Skip(1)] };
        var errors = OfficialDeckValidator.Validate(deck, catalog).Errors;
        Assert.Contains(errors, error => error.Contains(CardNo, StringComparison.Ordinal) && error.Contains("传奇英雄标签不匹配", StringComparison.Ordinal));
        var initial = MatchReplayInitialStateBuilder.FromSeats("rad-realm-deck-restriction",
            new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" });
        var session = new MatchSession(initial, new CoreRuleEngine(), NoopMatchJournal.Instance);
        var command = new SubmitDeckCommand(deck.LegendCardNo, deck.ChampionCardNo, deck.MainDeck, deck.RuneDeck, deck.Battlefields);
        var raw = JsonSerializer.SerializeToElement(command, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var result = await session.SubmitDeckAsync("P1", "invalid-realm-deck", command, raw, default);
        Assert.False(result.Accepted); Assert.Equal(MatchStateHasher.Hash(initial), MatchStateHasher.Hash(result.State));
    }
}
