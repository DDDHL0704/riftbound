using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

// Official SFD·016 lower box; CN 718.3 / 719.1 (granted host text), 383 (roles),
// 403 (confirmation), 359.3.f.2 (current here) and 418 (skill damage).
public sealed class ArmamentGrantedCombatTriggerTests
{
    private const string Bow = "SFD·016/221";

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task NativeBowPlayAndAssemblyGrantTheHostOneIndependentAttackOrDefendDamageSkill(bool defend)
    {
        var game = new Driver(Position(defend)); await Attach(game);
        await Battle(game, defend);
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        Assert.Equal(1, choice.RequiredCount); Assert.Equal(1, choice.MaxCount);
        Assert.Contains("ENEMY1", choice.LegalObjectIds); Assert.DoesNotContain("HOST", choice.LegalObjectIds);
        Assert.Equal(0, game.State.CardObjects["ENEMY1"].Damage);
        await Choose(game, "ENEMY1");
        var skill = Assert.Single(game.State.StackItems);
        Assert.Equal("HOST", skill.SourceObjectId); Assert.Equal("P1", skill.ControllerId);
        Assert.True(skill.UnitEvent!.Origin!.ArmamentGranted);
        Assert.Equal("GEAR", skill.UnitEvent.Origin.Equipment.ObjectId);
        Assert.Equal(Bow, skill.CardNo); Assert.Equal(0, game.State.CardObjects["ENEMY1"].Damage);
        Assert.Empty(Errors(game, game.State));
        await game.Top();
        Assert.Equal(2, game.State.CardObjects["ENEMY1"].Damage);
        Assert.Equal(0, game.State.CardObjects["ENEMY2"].Damage);
        Assert.Single(game.Events(), e => e.Event.Kind == "DAMAGE_APPLIED");
        await game.Replay();
    }

    [Fact]
    public async Task AnUnattachedBowDoesNotGiveItsEquipmentTextToAnUnarmedUnit()
    {
        var game = new Driver(Position());
        await game.Act("P1", new PlayCardCommand("GEAR", Bow, [], Destination: "BASE"));
        await Battle(game, false);
        Assert.DoesNotContain(game.State.StackItems, i => i.UnitEvent?.Origin?.ArmamentGranted == true);
        Assert.DoesNotContain(game.State.TriggerQueue, i => i.UnitEvent?.Origin?.ArmamentGranted == true);
        Assert.Null(game.State.PendingCardChoice); Assert.Equal(0, game.State.CardObjects["ENEMY1"].Damage);
        await game.Replay();
    }

    [Theory]
    [InlineData(false, false)] [InlineData(false, true)]
    [InlineData(true, false)] [InlineData(true, true)]
    public async Task GrantedDamagePaysNativeSpellshieldAndAddsDamageBonusesOnlyToTheChosenEnemy(bool ward, bool bonus)
    {
        var state = Position();
        if (ward) {
            Assert.True(CardBehaviorRegistry.TryGetByCardNo("OGN·161/298", out var nativeWard));
            var printed = OfficialGarenConquestBattlefieldCountTests.Printed("ENEMY1", "OGN·161/298", "P2");
            state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["ENEMY1"] = printed with { Tags = printed.Tags.Concat(nativeWard.SourceUnitTags.Split('|')).Distinct().ToArray() } } };
        }
        if (bonus) state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["F"] = OfficialGarenConquestBattlefieldCountTests.Printed("F", "OGN·296/298", "P1") with { ControllerId = "P2" } } };
        var game = new Driver(state); await Attach(game); await Battle(game, false);
        var power = game.State.RunePools["P1"].TotalPower;
        await Choose(game, "ENEMY1");
        if (ward) {
            var payment = Assert.IsType<PendingPaymentState>(game.State.PendingPayment);
            Assert.Equal(1, payment.PowerCost); Assert.Equal(power, game.State.RunePools["P1"].TotalPower);
            await game.Act("P1", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, ["PAY"]));
        }
        Assert.Equal(power - (ward ? 1 : 0), game.State.RunePools["P1"].TotalPower);
        await game.Top();
        Assert.Equal(bonus ? 3 : 2, game.State.CardObjects["ENEMY1"].Damage);
        Assert.Equal(0, game.State.CardObjects["ENEMY2"].Damage);
        Assert.Single(game.Events(), e => e.Event.Kind == "DAMAGE_APPLIED"); await game.Replay();
    }

    [Fact]
    public async Task TwoBowsGrantTwoIndependentSkillsAndCopyingPrintedHostTextDoesNotAddAnother()
    {
        var state = RadGearTestPosition.Put(Position(), "BOW2", Bow, "P1", "HAND");
        state = AttachedRuleTextTests.AddSong(state, "SONG");
        var game = new Driver(state); await Attach(game); await Attach(game, "BOW2");
        await game.Act("P1", new AssembleEquipmentCommand("SONG", "HOST", ["ASSEMBLE_GREEN"])); await game.Top();
        await Battle(game, false);
        Assert.Equal(2, game.State.TriggerQueue.Count);
        await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: game.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
        await Choose(game, "ENEMY1"); await Choose(game, "ENEMY1");
        Assert.Equal(2, game.State.StackItems.Count);
        Assert.Equal(2, game.State.StackItems.Select(i => i.UnitEvent!.Origin!.Equipment.ObjectId).Distinct().Count());
        await game.Top(); Assert.Equal(2, game.State.CardObjects["ENEMY1"].Damage);
        await game.Top(); Assert.Equal(4, game.State.CardObjects["ENEMY1"].Damage);
        Assert.Equal(0, game.State.CardObjects["ENEMY2"].Damage); await game.Replay();
    }

    [Fact]
    public async Task ReturningTheHostDetachesTheGrantButKeepsTheEarnedSkillUntilItsCurrentHereBecomesUnavailable()
    {
        var game = new Driver(AttackDamageDistributionTests.Spell(Position(), "RETURN", "OGN·104/298", "P1"));
        await Attach(game); await Battle(game, false); await Choose(game, "ENEMY1");
        var origin = Assert.Single(game.State.StackItems).UnitEvent!.Origin;
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["HOST"])); await game.Top();
        Assert.Equal("HAND", game.State.ObjectLocations["HOST"].Zone);
        Assert.Null(game.State.CardObjects["GEAR"].AttachedToObjectId);
        Assert.Equal(origin, Assert.Single(game.State.StackItems).UnitEvent!.Origin);
        Assert.Empty(Errors(game, game.State));
        var view = JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(game.State)["P2"].Stack);
        Assert.DoesNotContain("HOST", view); Assert.DoesNotContain("armamentHostCardNo", view);
        Assert.DoesNotContain("armamentEquipmentFactory", view);
        await game.Top(); Assert.Equal(0, game.State.CardObjects["ENEMY1"].Damage); await game.Replay();
    }

    [Theory]
    [InlineData("gear")] [InlineData("revision")] [InlineData("host-generation")]
    [InlineData("capture")] [InlineData("face")] [InlineData("erase")]
    [InlineData("erase-and-rename")]
    public async Task RecoveryBindsTheGrantToTheOriginalAcceptedQueueAndRejectsForgedBindings(string change)
    {
        var game = new Driver(Position()); await Attach(game); await Battle(game, false); await Choose(game, "ENEMY1");
        Assert.Empty(Errors(game, game.State));
        var item = Assert.Single(game.State.StackItems); var context = item.UnitEvent!; var origin = context.Origin!;
        var forgedOrigin = change switch {
            "gear" => origin with { Equipment = new("OTHER", 0) },
            "revision" => origin with { Revision = origin.Revision + 1 },
            "host-generation" => origin with { Unit = origin.Unit with { Generation = origin.Unit.Generation + 1 } },
            "capture" => origin with { CapturedTick = origin.CapturedTick + 1 },
            "erase" or "erase-and-rename" => origin with { ArmamentGranted = false }, _ => origin };
        var forged = game.State with { StackItems = [item with {
            StackItemId = change == "erase-and-rename" ? "invented-unit-event" : item.StackItemId,
            UnitEvent = context with { Origin = forgedOrigin,
                FaceRevision = change == "face" ? context.FaceRevision + 1 : context.FaceRevision } }] };
        Assert.Contains(Errors(game, forged), e => e.Contains("original accepted grant event", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("empty")] [InlineData("friendly")] [InlineData("gear")] [InlineData("two")]
    public async Task TargetConfirmationRequiresExactlyOneEnemyHereAndRejectsWrongChoicesAtomically(string branch)
    {
        var game = new Driver(Position()); await Attach(game); await Battle(game, false);
        var choice = game.State.PendingCardChoice!;
        string[] targets = branch switch { "empty" => [], "friendly" => ["HOST"], "gear" => ["GEAR"], _ => ["ENEMY1", "ENEMY2"] };
        var hash = MatchStateHasher.Hash(game.State);
        var result = await new CoreRuleEngine().ResolveAsync(game.State, new("bad-armament-target", "P1", CommandTypes.ChooseCards),
            new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, targets), default);
        Assert.False(result.Accepted); Assert.Empty(result.Events); Assert.Equal(hash, MatchStateHasher.Hash(result.State));
    }

    private static MatchState Position(bool defend = false)
    {
        var state = ArmamentMightTests.Position(Bow, "OGN·219/298", 4);
        state = RadUnitEffectTestFixture.Place(state, "GEAR", "P1", "HAND");
        state = RadGearTestPosition.Put(state, "ENEMY1", "OGN·082/298", "P2", defend ? "BASE" : "BATTLEFIELD", defend ? null : "F");
        state = RadGearTestPosition.Put(state, "ENEMY2", "OGN·082/298", "P2", "BATTLEFIELD", defend ? "OTHER" : "F");
        if (defend) {
            state = RadUnitEffectTestFixture.Place(state, "HOST", "P1", "BATTLEFIELD", "F");
            for (var i = 0; i < 3; i++) state = RadGearTestPosition.Put(state, "P2-DECK-" + i, "OGN·219/298", "P2", "MAIN_DECK");
        }
        return state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["F"] = state.CardObjects["F"] with { ControllerId = defend ? "P1" : "P2" } } };
    }
    private static async Task Attach(Driver game, string id = "GEAR")
    {
        await game.Act("P1", new PlayCardCommand(id, Bow, [], Destination: "BASE"));
        await game.Act("P1", new AssembleEquipmentCommand(id, "HOST", ["ASSEMBLE_RED"])); await game.Top();
        Assert.Equal("HOST", game.State.CardObjects[id].AttachedToObjectId);
    }
    private static async Task Battle(Driver game, bool defend)
    {
        if (defend) await game.Act("P1", new EndTurnCommand());
        await game.Act(defend ? "P2" : "P1", new MoveUnitCommand(defend ? "ENEMY1" : "HOST", "BASE", "BATTLEFIELD:F", []));
        for (var i = 0; i < 4 && game.State.FocusPlayerId is { } focus; i++) await game.Act(focus, new PassFocusCommand());
    }
    private static Task Choose(Driver game, string id)
    {
        var choice = game.State.PendingCardChoice!;
        return game.Act(choice.PlayerId, new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [id]));
    }
    private static IReadOnlyList<string> Errors(Driver game, MatchState state)
    {
        var sequence = game.Commands().Last().CompletedEventSequence;
        var views = state.Seats.Keys.ToDictionary(p => p, p => new RecoveredPlayerView(p, state.Tick, sequence,
            ResolutionResult.BuildSnapshots(state)[p], state.Tick, sequence, ResolutionResult.BuildPrompts(state)[p]));
        return MatchRecoveryValidator.Validate(state.RoomId, sequence, game.Commands(), game.Events(), views, state,
            state.Tick, MatchReplayRedactor.BuildSpectatorFrame(state.RoomId, state.Tick, sequence, [], state));
    }
}
