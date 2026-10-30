using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class RadMovementBaseMightTests
{
    private const string Lulu = "RAD·110/167";

    private static MatchState Board(bool copied = false, bool targets = true)
    {
        var state = ArmamentMightTests.Position(unit: Lulu, gear: copied ? "SFD·059/221" : "SFD·022/221", attached: copied);
        if (targets)
        {
            state = OfficialHoldSequenceTests.AddUnit(state, "ALLY", "OGN·001/298", "F", "P1");
            state = LocalDestructionRecallTests.At(state, "ALLY", "P1", "BASE");
            state = OfficialHoldSequenceTests.AddUnit(state, "REMOTE", "OGN·001/298", "OTHER", "P2");
            state = OfficialHoldSequenceTests.AddUnit(state, "ENEMYBASE", "OGN·001/298", "OTHER", "P2");
            state = LocalDestructionRecallTests.At(state, "ENEMYBASE", "P2", "BASE");
        }
        return state with { RoomId = "RAD-MOVEMENT-BASE-MIGHT", CardObjects = state.CardObjects.ToDictionary(p => p.Key,
            p => p.Key == "HOST" ? OfficialGarenConquestBattlefieldCountTests.Printed("HOST", Lulu, "P1")
                : p.Key is "ALLY" or "REMOTE" or "ENEMYBASE"
                    ? OfficialGarenConquestBattlefieldCountTests.Printed(p.Key, "OGN·001/298", p.Value.ControllerId!) : p.Value) };
    }

    private static Task Move(Driver game) => game.Act("P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F", []));
    private static Task Choose(Driver game, params string[] targets)
        => game.Act("P1", new ChooseCardsCommand(game.State.PendingCardChoice!.ChoiceId, "TRIGGER_CONFIRMATION", targets));
    private static JsonElement Unit(MatchState state, string id)
        => JsonSerializer.SerializeToElement(ResolutionResult.BuildSnapshots(state)["P1"], new JsonSerializerOptions(JsonSerializerDefaults.Web))
            .GetProperty("players").GetProperty(state.CardObjects[id].ControllerId!).GetProperty("objects").GetProperty(id);

    [Fact]
    public async Task OfficialPrintingPaysThreeManaAndKeepsItsPrintedMight()
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, c => c.CardNo == Lulu);
        Assert.Equal(39613, card.Id); Assert.Equal("古灵精怪", card.SubTitle);
        Assert.Equal(3, card.Energy); Assert.Equal(0, card.ReturnEnergy ?? 0); Assert.Equal(3, card.Power);
        var spec = Assert.Single(TriggerParser.Parse(card.CardEffect), t => t.Kind == TriggerKinds.UnitMovedSetOtherUnitBaseMight);
        Assert.Equal(4, spec.BasePowerUntilEndOfTurn); Assert.True(spec.Optional);
        Assert.Equal(TriggerTargetScopes.OtherUnitOnField, spec.TargetScope);
        var state = VenUnitEntryBasicTests.Position(Lulu);
        state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(3, 0) } };
        var game = new Driver(state);
        await game.Act("P1", new PlayCardCommand("UNIT", Lulu, [], Destination: "BASE"));
        Assert.Equal(0, game.State.RunePools["P1"].Mana); Assert.Equal(0, game.State.RunePools["P1"].TotalPower);
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Base); Assert.True(game.State.CardObjects["UNIT"].IsExhausted);
        Assert.Equal(3, game.State.CardObjects["UNIT"].Power); Assert.Empty(game.State.StackItems);
    }

    [Theory]
    [InlineData("mana")] [InlineData("turn")] [InlineData("targets")]
    public async Task IllegalPlaysLeaveAllStateAndPublicViewsUnchanged(string branch)
    {
        var state = VenUnitEntryBasicTests.Position(Lulu);
        if (branch == "mana") state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(2, 0) } };
        if (branch == "turn") state = state with { TurnPlayerId = "P2", ActivePlayerId = "P2" };
        await Reject(state, new PlayCardCommand("UNIT", Lulu, branch == "targets" ? ["ALLY"] : [], Destination: "BASE"));
    }

    [Theory]
    [InlineData("ALLY")] [InlineData("REMOTE")] [InlineData("ENEMYBASE")]
    public async Task AnotherUnitAtAnyFieldLocationCanBeChosenBeforeResponses(string target)
    {
        var game = new Driver(Board()); await Move(game);
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        Assert.Equal(new[] { "ALLY", "ENEMYBASE", "REMOTE" }, choice.LegalObjectIds.Order());
        Assert.Equal(0, choice.RequiredCount); Assert.Equal(1, choice.MaxCount);
        await Choose(game, target);
        Assert.DoesNotContain("BASE_MIGHT_THIS_TURN:4", game.State.CardObjects[target].UntilEndOfTurnEffects);
        Assert.NotNull(game.State.PriorityPlayerId); await game.Top();
        Assert.Equal(4, Unit(game.State, target).GetProperty("basePower").GetInt32());
        // 崔法利兵营仍授予 REMOTE +1；替换基础战力不移除战场修正。
        Assert.Equal(target == "REMOTE" ? 5 : 4, Unit(game.State, target).GetProperty("effectivePower").GetInt32());
        Assert.Contains("BASE_MIGHT_THIS_TURN:4", game.State.CardObjects[target].UntilEndOfTurnEffects);
    }

    [Theory]
    [InlineData("HOST")] [InlineData("GEAR")] [InlineData("F")] [InlineData("D0")]
    public async Task TheSourceNonUnitsAndHiddenCardsCannotBeTargets(string target)
    {
        var game = new Driver(Board()); await Move(game);
        Assert.DoesNotContain(target, game.State.PendingCardChoice!.LegalObjectIds);
        await Reject(game.State, new ChooseCardsCommand(game.State.PendingCardChoice.ChoiceId, "TRIGGER_CONFIRMATION", [target]));
        await Choose(game); Assert.Empty(game.State.StackItems);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "BASE_MIGHT_SET_UNTIL_END_OF_TURN");
    }

    [Fact]
    public async Task NoOtherUnitDiscardsTheOptionalSkillWithoutStoppingPlay()
    {
        var game = new Driver(Board(targets: false)); await Move(game);
        Assert.Null(game.State.PendingCardChoice); Assert.Empty(game.State.StackItems);
        Assert.Contains(game.Events(), e => e.Event.Kind == "TRIGGER_NOT_CONFIRMED");
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task EnemyWardIsPaidBeforeResponsesOrTheSkillIsDeclined(bool pay)
    {
        var state = Board();
        state = state with { CardObjects = state.CardObjects.ToDictionary(p => p.Key,
            p => p.Key == "REMOTE" ? p.Value with { Tags = [..p.Value.Tags, "法盾2"] } : p.Value) };
        var game = new Driver(state); await Move(game); await Choose(game, "REMOTE");
        var before = game.State.RunePools["P1"].TotalPower;
        var payment = Assert.IsType<PendingPaymentState>(game.State.PendingPayment); Assert.Equal(2, payment.PowerCost);
        await game.Act("P1", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, [pay ? "PAY" : "DECLINE"]));
        if (pay) await game.Top();
        Assert.Equal(before - (pay ? 2 : 0), game.State.RunePools["P1"].TotalPower);
        Assert.Equal(pay, game.State.CardObjects["REMOTE"].UntilEndOfTurnEffects.Contains("BASE_MIGHT_THIS_TURN:4"));
    }

    [Fact]
    public async Task AnEnemyProtectedFromSkillsDoesNotAppearAmongTheTargets()
    {
        var state = Board();
        state = state with { CardObjects = state.CardObjects.ToDictionary(p => p.Key, p => p.Key == "REMOTE"
            ? p.Value with { UntilEndOfTurnEffects = ["ENEMY_SPELL_SKILL_TARGET_PROTECTION_THIS_TURN"] } : p.Value) };
        var game = new Driver(state); await Move(game);
        Assert.DoesNotContain("REMOTE", game.State.PendingCardChoice!.LegalObjectIds);
        await Reject(game.State, new ChooseCardsCommand(game.State.PendingCardChoice.ChoiceId, "TRIGGER_CONFIRMATION", ["REMOTE"]));
        await Choose(game, "ALLY"); await game.Top();
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task BaseReplacementPreservesModifiersAndExpiresAtTurnEnd(bool buffFirst)
    {
        var state = AttackDamageDistributionTests.Spell(Board(), "BUFF", "VEN·031", "P1");
        var printed = state.CardObjects["ALLY"].Power;
        var game = new Driver(state);
        if (buffFirst) { await game.Act("P1", new PlayCardCommand("BUFF", "VEN·031", ["ALLY"])); await game.Top(); }
        await Move(game); await Choose(game, "ALLY"); await game.Top();
        if (!buffFirst) { await game.Act("P1", new PlayCardCommand("BUFF", "VEN·031", ["ALLY"])); await game.Top(); }
        Assert.Equal(4, Unit(game.State, "ALLY").GetProperty("basePower").GetInt32());
        Assert.Equal(5, Unit(game.State, "ALLY").GetProperty("effectivePower").GetInt32());
        for (var passes = 0; game.State.SpellDuelState.IsActive && passes < 4; passes++) {
            if (game.State.StackItems.Count > 0 && game.State.PriorityPlayerId is { } priority) await game.Act(priority, new PassPriorityCommand());
            else await game.Act(game.State.SpellDuelState.FocusPlayerId!, new PassFocusCommand());
        }
        Assert.False(game.State.SpellDuelState.IsActive);
        await game.Act("P1", new EndTurnCommand());
        Assert.Empty(game.State.CardObjects["ALLY"].UntilEndOfTurnEffects);
        Assert.Equal(printed, Unit(game.State, "ALLY").GetProperty("effectivePower").GetInt32());
    }

    [Fact]
    public async Task BaseReplacementRetainsBoonAndArmamentBonuses()
    {
        var state = Board(); var ally = state.CardObjects["ALLY"];
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["ALLY"] = ally with { Power = ally.Power + 1, Tags = [..ally.Tags, "增益"] },
            ["GEAR"] = state.CardObjects["GEAR"] with { AttachedToObjectId = "ALLY" } } };
        var game = new Driver(state); await Move(game); await Choose(game, "ALLY"); await game.Top();
        Assert.Equal(4, Unit(game.State, "ALLY").GetProperty("basePower").GetInt32());
        Assert.Equal(7, Unit(game.State, "ALLY").GetProperty("effectivePower").GetInt32());
        Assert.Contains("增益", game.State.CardObjects["ALLY"].Tags);
        Assert.Equal("ALLY", game.State.CardObjects["GEAR"].AttachedToObjectId);
    }

    [Fact]
    public async Task LoweringBaseMightCanCauseImmediateLethalCleanup()
    {
        var state = Board();
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["ALLY"] = OfficialGarenConquestBattlefieldCountTests.Printed("ALLY", "OGN·088/298", "P1") with { Damage = 4 } } };
        var game = new Driver(state); await Move(game); await Choose(game, "ALLY"); await game.Top();
        Assert.Contains("ALLY", game.State.PlayerZones["P1"].Graveyard);
        Assert.Empty(game.State.CardObjects["ALLY"].UntilEndOfTurnEffects);
    }

    [Fact]
    public async Task ReturningTheTargetMakesItsNewHandGenerationIneligible()
    {
        var game = new Driver(AttackDamageDistributionTests.Spell(Board(), "RETURN", "OGN·104/298", "P1"));
        await Move(game); await Choose(game, "ALLY");
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["ALLY"])); await game.Top(); await game.Top();
        Assert.Contains("ALLY", game.State.PlayerZones["P1"].Hand);
        Assert.Empty(game.State.CardObjects["ALLY"].UntilEndOfTurnEffects);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "BASE_MIGHT_SET_UNTIL_END_OF_TURN");
    }

    [Fact]
    public async Task SourceReturningToHandDoesNotCancelItsSkillOrExposeItsPrivateIdentity()
    {
        var game = new Driver(AttackDamageDistributionTests.Spell(Board(), "RETURN", "OGN·104/298", "P1"));
        await Move(game); await Choose(game, "ALLY");
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["HOST"])); await game.Top();
        Assert.DoesNotContain("\"HOST\"", JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(game.State)["P2"].Stack));
        var spectator = MatchReplayRedactor.BuildSpectatorFrame(game.State.RoomId, game.State.Tick, 0, [], game.State);
        Assert.DoesNotContain("\"HOST\"", JsonSerializer.Serialize(spectator.SpectatorSnapshot.Stack));
        await game.Top(); Assert.Equal(4, Unit(game.State, "ALLY").GetProperty("basePower").GetInt32());
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "BASE_MIGHT_SET_UNTIL_END_OF_TURN"
            && Equals(e.Event.Payload.GetValueOrDefault("sourceObjectId"), "HOST"));
    }

    [Fact]
    public async Task ATokenTargetThatActuallyLeavesTheFieldCeasesToExist()
    {
        var state = Board();
        var token = Assert.Single(P6TokenFactoryCatalog.GetAll(), t => t.CardNo == P6TokenFactoryCatalog.WarhawkTokenCardNo)
            .CreateObject("ALLY", "P1", "P1");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) { ["ALLY"] = token } };
        var game = new Driver(AttackDamageDistributionTests.Spell(state, "RETURN", "OGN·104/298", "P1"));
        await Move(game); await Choose(game, "ALLY");
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["ALLY"])); await game.Top();
        Assert.False(game.State.CardObjects.ContainsKey("ALLY")); await game.Top();
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "BASE_MIGHT_SET_UNTIL_END_OF_TURN");
    }

    [Fact]
    public async Task CopiedTextConfirmsTwoIndependentSkills()
    {
        var game = new Driver(Board(copied: true)); await Move(game);
        var triggers = game.State.TriggerQueue.Where(t => t.MovementObserver?.Kind == TriggerKinds.UnitMovedSetOtherUnitBaseMight)
            .Select(t => t.TriggerId).ToArray(); Assert.Equal(2, triggers.Length);
        await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: triggers));
        await Choose(game, "ALLY"); await Choose(game, "REMOTE"); await game.Top(); await game.Top();
        Assert.Equal(4, Unit(game.State, "ALLY").GetProperty("basePower").GetInt32());
        Assert.Equal(4, Unit(game.State, "REMOTE").GetProperty("basePower").GetInt32());
    }

    [Fact]
    public async Task RecoveryRejectsForgedMovementAndTargetGenerations()
    {
        var game = new Driver(Board()); await Move(game); await Choose(game, "ALLY");
        var item = Assert.Single(game.State.StackItems); var context = item.MovementObserver!;
        foreach (var forged in new[] {
            item with { MovementObserver = context with { SourceGeneration = context.SourceGeneration + 1 } },
            item with { MovementObserver = context with { TargetTokenFactoryCardNo = "INVENTED" } },
            item with { TargetObjectIds = ["HOST"], TargetGenerations = new Dictionary<string, long> { ["HOST"] = context.SourceGeneration } },
            item with { TargetGenerations = new Dictionary<string, long> { ["ALLY"] = -1 } },
            item with { TargetGenerations = new Dictionary<string, long> { ["ALLY"] = game.State.CardObjects["ALLY"].ObjectGeneration + 1 } } })
            Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(game.State with { StackItems = [forged] }));
        await game.Top();
    }

    [Fact]
    public async Task RecoveryRejectsABaseValueThatDiffersFromTheAcceptedGrant()
    {
        var game = new Driver(Board()); await Move(game); await Choose(game, "ALLY"); await game.Top();
        var state = game.State with { CardObjects = new Dictionary<string, CardObjectState>(game.State.CardObjects) {
            ["ALLY"] = game.State.CardObjects["ALLY"] with { UntilEndOfTurnEffects = ["BASE_MIGHT_THIS_TURN:9"] } } };
        var seq = game.Events()[^1].Sequence; var snapshots = ResolutionResult.BuildSnapshots(state); var prompts = ResolutionResult.BuildPrompts(state);
        var views = state.Seats.Keys.ToDictionary(p => p, p => new RecoveredPlayerView(p, state.Tick, seq, snapshots[p], state.Tick, seq, prompts[p]));
        var spectator = MatchReplayRedactor.BuildSpectatorFrame(state.RoomId, state.Tick, seq, [], state);
        var errors = MatchRecoveryValidator.Validate(state.RoomId, seq, game.Commands(), game.Events(), views, state, state.Tick, spectator);
        Assert.Contains(errors, e => e.Contains("base Might replacement differs from accepted history"));
    }

    private static async Task Reject(MatchState state, GameCommand command)
    {
        var before = MatchStateHasher.Hash(state); var views = JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(state));
        var prompts = JsonSerializer.Serialize(ResolutionResult.BuildPrompts(state));
        var result = await new CoreRuleEngine().ResolveAsync(state, new("invalid-lulu", "P1", command.CmdType), command, default);
        Assert.False(result.Accepted); Assert.Empty(result.Events); Assert.Equal(before, MatchStateHasher.Hash(result.State));
        Assert.Equal(views, JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(result.State)));
        Assert.Equal(prompts, JsonSerializer.Serialize(ResolutionResult.BuildPrompts(result.State)));
    }
}
