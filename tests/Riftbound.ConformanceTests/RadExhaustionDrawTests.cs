using System.Text;
using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class RadExhaustionDrawTests
{
    private const string Card = "RAD·065/167", Kind = "SIMULTANEOUS_FRIENDLY_EXHAUSTION_DRAW";
    private const string Window = "EXHAUSTION_DRAW_CONFIRMATION", Used = "EXHAUSTION_DRAW_USED|";
    public static IEnumerable<object[]> Printings() => new object[][] { [Card, 39562L], ["RAD·065a/167", 39563L] };

    private static MatchState Board(string no = Card, int units = 6, bool copied = false)
    {
        var state = ArmamentMightTests.Position(unit: no, gear: copied ? "SFD·059/221" : "SFD·022/221", power: 3, attached: copied);
        for (var i = 0; i < units; i++)
            state = HandDiscardObserverTests.Put(state, "U" + i, "OGN·001/298", "P1", "BASE");
        return state with { RoomId = "RAD-EXHAUSTION-DRAW", CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["HOST"] = OfficialGarenConquestBattlefieldCountTests.Printed("HOST", no, "P1") } };
    }

    private static Task Move(Driver game, params string[] ids)
        => game.Act("P1", new MoveUnitCommand(ids[0], "BASE", "BATTLEFIELD:F", SourceObjectIds: ids));
    private static Task Confirm(Driver game, bool accept)
        => game.Act("P1", new ChooseCardsCommand(game.State.PendingCardChoice!.ChoiceId, Window, accept ? ["DRAW"] : []));

    [Theory, MemberData(nameof(Printings))]
    public async Task EachOfficialPrintingPaysItsBlueCostAndEnteringExhaustedDoesNotTrigger(string no, long id)
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, c => c.CardNo == no);
        Assert.Equal(id, card.Id); Assert.Equal("声愈人心", card.SubTitle); Assert.Equal(4, card.Energy); Assert.Equal(3, card.Power);
        var spec = Assert.Single(TriggerParser.Parse(card.CardEffect), t => t.Kind == TriggerKinds.ControllerExhaustedUnitGroupDraw);
        Assert.Equal(3, spec.RequiredUnitCount); Assert.Equal(1, spec.DrawCount); Assert.True(spec.Optional); Assert.True(spec.OncePerTurn);
        var state = VenUnitEntryBasicTests.Position(no);
        state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) {
            ["P1"] = new(4, 0, new Dictionary<string, int> { [RuneTrait.Blue] = 1 }) } };
        var game = new Driver(state); await game.Act("P1", new PlayCardCommand("UNIT", no, [], Destination: "BASE"));
        Assert.Equal(0, game.State.RunePools["P1"].Mana); Assert.Equal(0, game.State.RunePools["P1"].TotalPower);
        Assert.True(game.State.CardObjects["UNIT"].IsExhausted); Assert.Equal(3, game.State.CardObjects["UNIT"].Power);
        Assert.Empty(game.State.StackItems); Assert.Null(game.State.PendingCardChoice); Assert.Empty(game.State.PlayerZones["P1"].Hand);
    }

    [Theory, MemberData(nameof(Printings))]
    public async Task EachPrintingRejectsUnpaidBluePowerWithoutChangingState(string no, long _)
    {
        var state = VenUnitEntryBasicTests.Position(no);
        state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(4, 0) } };
        await Reject(state, new PlayCardCommand("UNIT", no, [], Destination: "BASE"));
    }

    [Theory, MemberData(nameof(Printings))]
    public async Task ThreeFriendlyUnitsMovedTogetherOfferOneDrawOnlyAfterConfirmationAndResponses(string no, long _)
    {
        var game = new Driver(Board(no)); var before = game.State.PlayerZones["P1"].Hand.Count;
        await Move(game, "U0", "U1", "U2");
        Assert.All(new[] { "U0", "U1", "U2" }, id => Assert.True(game.State.CardObjects[id].IsExhausted));
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        Assert.Equal(Window, choice.ChoiceWindow); Assert.Equal(0, choice.RequiredCount); Assert.Equal(["DRAW"], choice.LegalObjectIds);
        Assert.Equal(before, game.State.PlayerZones["P1"].Hand.Count); Assert.DoesNotContain(game.State.UntilEndOfTurnEffects, e => e.StartsWith(Used));
        await Confirm(game, true);
        Assert.Equal(before, game.State.PlayerZones["P1"].Hand.Count); Assert.NotNull(game.State.PriorityPlayerId);
        Assert.Single(game.State.UntilEndOfTurnEffects, e => e.StartsWith(Used));
        await game.Top(); Assert.Equal(before + 1, game.State.PlayerZones["P1"].Hand.Count);
        await Move(game, "U3", "U4", "U5");
        Assert.Null(game.State.PendingCardChoice); Assert.Empty(game.State.StackItems); Assert.Equal(before + 1, game.State.PlayerZones["P1"].Hand.Count);
    }

    [Fact]
    public async Task DecliningPreservesTheAllowanceForALaterSimultaneousInstruction()
    {
        var game = new Driver(Board()); await Move(game, "U0", "U1", "U2"); await Confirm(game, false);
        Assert.DoesNotContain(game.State.UntilEndOfTurnEffects, e => e.StartsWith(Used)); Assert.Empty(game.State.StackItems);
        await Move(game, "U3", "U4", "U5"); await Confirm(game, true); await game.Top();
        Assert.Single(game.State.PlayerZones["P1"].Hand); Assert.Single(game.Events(), e => e.Event.Kind == "CARD_DRAWN");
    }

    [Fact]
    public async Task ANewTurnRestoresTheConfirmedTextInstancesAllowance()
    {
        var game = new Driver(Board());
        await Move(game, "U0", "U1", "U2"); await Confirm(game, true); await game.Top();
        Assert.Single(game.State.UntilEndOfTurnEffects, e => e.StartsWith(Used));
        var previousTurn = game.State.TurnNumber;
        await game.Act("P1", new EndTurnCommand()); await game.Act("P2", new EndTurnCommand());
        Assert.True(game.State.TurnNumber > previousTurn);
        Assert.DoesNotContain(game.State.UntilEndOfTurnEffects, e => e.StartsWith(Used));
        var before = game.State.PlayerZones["P1"].Hand.Count;
        await Move(game, "U3", "U4", "U5"); await Confirm(game, true); await game.Top();
        Assert.Equal(before + 1, game.State.PlayerZones["P1"].Hand.Count);
        Assert.Single(game.State.UntilEndOfTurnEffects, e => e.StartsWith(Used));
    }

    [Fact]
    public async Task ThreeSuccessiveSingleUnitMovesDoNotBecomeASimultaneousInstruction()
    {
        var game = new Driver(Board());
        foreach (var id in new[] { "U0", "U1", "U2" }) {
            await game.Act("P1", new MoveUnitCommand(id, "BASE", "BATTLEFIELD:F", []));
            Assert.Null(game.State.PendingCardChoice); Assert.Empty(game.State.StackItems);
        }
        Assert.Empty(game.State.PlayerZones["P1"].Hand);
        await Move(game, "U3", "U4", "U5"); Assert.Equal(Window, game.State.PendingCardChoice!.ChoiceWindow);
    }

    [Theory]
    [InlineData("duplicate")] [InlineData("exhausted")] [InlineData("enemy")]
    public async Task AnIllegalGroupCannotPartiallyExhaustUnitsOrEarnADraw(string branch)
    {
        var state = Board();
        if (branch == "exhausted") state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["U2"] = state.CardObjects["U2"] with { IsExhausted = true } } };
        if (branch == "enemy") state = HandDiscardObserverTests.Put(state, "U2", "OGN·001/298", "P2", "BASE");
        await Reject(state, new MoveUnitCommand("U0", "BASE", "BATTLEFIELD:F", SourceObjectIds: branch == "duplicate" ? ["U0", "U1", "U0"] : ["U0", "U1", "U2"]));
    }

    [Fact]
    public async Task RemovingAConfirmedSkillKeepsTheTurnAllowanceSpent()
    {
        var game = new Driver(Board()); await Move(game, "U0", "U1", "U2"); await Confirm(game, true);
        // The shared counter path removes a skill without refunding confirmation.
        // Test that postcondition directly: current official counters require a
        // friendly target and cannot target this targetless draw skill.
        var removed = game.State with { StackItems = [], PriorityPlayerId = null, ActivePlayerId = "P1" };
        ArmamentMightTests.PublicRecovery(removed);
        var result = await new CoreRuleEngine().ResolveAsync(removed, new("after-removed-draw", "P1", "MOVE_UNIT"),
            new MoveUnitCommand("U3", "BASE", "BATTLEFIELD:F", SourceObjectIds: ["U3", "U4", "U5"]), default);
        Assert.True(result.Accepted, result.ErrorMessage); Assert.Null(result.State.PendingCardChoice); Assert.Empty(result.State.StackItems);
        Assert.Single(result.State.UntilEndOfTurnEffects, e => e.StartsWith(Used));
    }

    [Fact]
    public async Task TwoCopiedTextInstancesConfirmAndUseTheirOwnTurnAllowances()
    {
        var game = new Driver(Board(copied: true)); await Move(game, "U0", "U1", "U2");
        var ids = game.State.TriggerQueue.Where(t => t.FieldContext?.Kind == Kind).Select(t => t.TriggerId).ToArray();
        Assert.Equal(2, ids.Length); await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: ids));
        await Confirm(game, false); await Confirm(game, true); await game.Top();
        Assert.Single(game.State.PlayerZones["P1"].Hand);
        await Move(game, "U3", "U4", "U5"); await Confirm(game, true); await game.Top();
        Assert.Equal(2, game.State.PlayerZones["P1"].Hand.Count); Assert.Equal(2, game.State.UntilEndOfTurnEffects.Count(e => e.StartsWith(Used)));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ReturningTheSourceToHandPreservesTheEarnedDrawAndHidesItsPrivateIdentity(bool attemptEarlyResponse)
    {
        var state = AttackDamageDistributionTests.Spell(Board(), "RETURN", "OGN·104/298", "P1");
        var game = new Driver(state); await Move(game, "U0", "U1", "U2");
        // Optional confirmation precedes the response window.
        if (attemptEarlyResponse) await Reject(game.State, new PlayCardCommand("RETURN", "OGN·104/298", ["HOST"]));
        await Confirm(game, true); await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["HOST"])); await game.Top();
        Assert.Contains("HOST", game.State.PlayerZones["P1"].Hand);
        Assert.DoesNotContain("\"HOST\"", JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(game.State)["P2"].Stack));
        Assert.DoesNotContain(Used, JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(game.State)));
        await game.Top(); Assert.Equal(2, game.State.PlayerZones["P1"].Hand.Count);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind.StartsWith("INTERNAL_"));
    }

    [Fact]
    public async Task ExhaustingAllFriendlyUnitsAlsoCapturesTheSourceBeforeLethalDamageRemovesIt()
    {
        var state = LocalDestructionRecallTests.At(Board(units: 2), "HOST", "P1", "BATTLEFIELD", "F");
        state = AttackDamageDistributionTests.Spell(state, "BURST", "OGN·123/298", "P1");
        var game = new Driver(state); await game.Act("P1", new PlayCardCommand("BURST", "OGN·123/298", [])); await game.Top();
        Assert.Contains("HOST", game.State.PlayerZones["P1"].Graveyard);
        await Confirm(game, true); await game.Top(); Assert.Single(game.State.PlayerZones["P1"].Hand);
    }

    [Fact]
    public async Task AlreadyExhaustedUnitsDoNotCountTowardTheThreshold()
    {
        var state = Board(units: 2);
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["U0"] = state.CardObjects["U0"] with { IsExhausted = true } } };
        state = AttackDamageDistributionTests.Spell(state, "BURST", "OGN·123/298", "P1");
        var game = new Driver(state); await game.Act("P1", new PlayCardCommand("BURST", "OGN·123/298", [])); await game.Top();
        Assert.Null(game.State.PendingCardChoice); Assert.Empty(game.State.StackItems); Assert.Empty(game.State.PlayerZones["P1"].Hand);
    }

    [Fact]
    public async Task ARealCopiedTokenMayConfirmItsDrawAfterTheSameInstructionMakesItCease()
    {
        var state = OfficialCopyIdentityTests.AddSpell(Board(units: 2), "MIRROR", "UNL-200/219");
        state = (await OfficialCopyIdentityTests.Copy(state, "HOST", "MIRROR")).State;
        const string token = "MIRROR-TOKEN-001";
        Assert.Equal(P6TokenFactoryCatalog.ImageTokenCardNo, state.CardObjects[token].TokenFactoryCardNo);
        state = LocalDestructionRecallTests.At(state, token, "P1", "BATTLEFIELD", "F");
        state = AttackDamageDistributionTests.Spell(state, "RETURN", "OGN·104/298", "P1");
        state = AttackDamageDistributionTests.Spell(state, "BURST", "OGN·123/298", "P1");
        var game = new Driver(state);
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["HOST"])); await game.Top();
        await game.Act("P1", new PlayCardCommand("BURST", "OGN·123/298", [])); await game.Top();
        Assert.False(game.State.CardObjects.ContainsKey(token));
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        Assert.Equal(Window, choice.ChoiceWindow); Assert.Equal(token, choice.SourceObjectId);
        Assert.True(Assert.Single(game.State.StackItems).FieldContext!.SourceCeasedToExist);
        var before = game.State.PlayerZones["P1"].Hand.Count;
        await Confirm(game, true); await game.Top();
        Assert.Equal(before + 1, game.State.PlayerZones["P1"].Hand.Count);
        Assert.Single(game.State.UntilEndOfTurnEffects, e => e.StartsWith(Used));
    }

    [Theory]
    [InlineData("units")] [InlineData("duplicate")] [InlineData("actor")] [InlineData("face")]
    [InlineData("source-generation")] [InlineData("source-cessation")] [InlineData("unit-token")]
    public async Task RecoveryRejectsForgedInstructionAndSourceBindings(string branch)
    {
        var game = new Driver(Board()); await Move(game, "U0", "U1", "U2");
        var item = Assert.Single(game.State.StackItems); var context = item.FieldContext!; var birth = context.ExhaustionObserver!;
        var forged = branch switch {
            "units" => context with { ExhaustionObserver = birth with { Units = birth.Units.Take(2).ToArray() } },
            "duplicate" => context with { ExhaustionObserver = birth with { Units = [birth.Units[0], birth.Units[0], birth.Units[0]] } },
            "actor" => context with { ExhaustionObserver = birth with { Actor = "P2" } },
            "face" => context with { ExhaustionObserver = birth with { FaceRevision = 99 } },
            "source-cessation" => context with { SourceCeasedToExist = true,
                ExhaustionObserver = birth with { CeasedSource = new(game.State.Tick, context.CardNo) } },
            "unit-token" => context with { ExhaustionObserver = birth with { Units = birth.Units.Select((unit, i) =>
                i == 0 ? unit with { TokenFactoryCardNo = "INVENTED_TOKEN" } : unit).ToArray() } },
            _ => context with { SourceGeneration = context.SourceGeneration + 1 }
        };
        Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(game.State with { StackItems = [item with { FieldContext = forged }] }));
        await Confirm(game, true);
        var confirmed = Assert.Single(game.State.StackItems);
        var changedInstruction = confirmed.FieldContext! with { ExhaustionObserver = confirmed.FieldContext!.ExhaustionObserver! with { CauseId = "FORGED_INSTRUCTION" } };
        Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(game.State with { StackItems = [confirmed with { FieldContext = changedInstruction }] }));
        await game.Top();
        var marker = Assert.Single(game.State.UntilEndOfTurnEffects, e => e.StartsWith(Used));
        var use = JsonSerializer.Deserialize<ExhaustionDrawUse>(Encoding.UTF8.GetString(Convert.FromBase64String(marker[Used.Length..])))!;
        var invalid = Used + Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(use with { SourceId = "INVENTED" })));
        Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(game.State with { UntilEndOfTurnEffects = [invalid] }));
    }

    private static async Task Reject(MatchState state, GameCommand command)
    {
        var hash = MatchStateHasher.Hash(state); var views = JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(state));
        var prompts = JsonSerializer.Serialize(ResolutionResult.BuildPrompts(state));
        var result = await new CoreRuleEngine().ResolveAsync(state, new("invalid-group-exhaustion", "P1", command.CmdType), command, default);
        Assert.False(result.Accepted); Assert.Empty(result.Events); Assert.Equal(hash, MatchStateHasher.Hash(result.State));
        Assert.Equal(views, JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(result.State)));
        Assert.Equal(prompts, JsonSerializer.Serialize(ResolutionResult.BuildPrompts(result.State)));
    }
}
