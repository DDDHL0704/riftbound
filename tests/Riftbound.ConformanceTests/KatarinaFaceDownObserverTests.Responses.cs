using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed partial class KatarinaFaceDownObserverTests
{
    [Fact]
    public async Task ARealAbilityCounterCanRemoveOnlyTheIndependentDamageSkill()
    {
        var initial = RadGearTestPosition.Put(Position("OGN·083/298"), "COUNTER", "SFD·045/221", "P2", "HAND");
        initial = RadGearTestPosition.Put(initial, "P2-R5", "UNL-R02", "P2", "BASE");
        var game = new Driver(initial); await HiddenNextTurn(game, "OGN·083/298");
        await FromFaceDown(game, "OGN·083/298"); await Target(game); await Priority(game, "P2");
        var skill = Assert.Single(game.State.StackItems, item => item.EffectKind == CoreRuleEngine.StandbyPlayedEnemyDamageEffect).StackItemId;
        await game.Act("P2", new TapRuneCommand("P2-R4"));
        await game.Act("P2", new RecycleRuneCommand("P2-R5"));
        await game.Act("P2", new PlayCardCommand("COUNTER", "SFD·045/221", [skill])); await game.Top();
        Assert.DoesNotContain(game.State.StackItems, item => item.EffectKind == CoreRuleEngine.StandbyPlayedEnemyDamageEffect);
        Assert.DoesNotContain(game.State.StackItems, item => item.SourceObjectId == Shadow);
        Assert.Contains(Shadow, game.State.PlayerZones["P1"].Graveyard);
        while (game.State.StackItems.Count > 0) await game.Top();
        Assert.Equal(0, game.State.CardObjects["ENEMY"].Damage); await game.ColdCheckpoint();
    }

    [Fact]
    public async Task CounteringTheActuallyPlayedFaceDownSpellPreventsItsDamageTrigger()
    {
        var initial = RadGearTestPosition.Put(Position("OGN·083/298"), "COUNTER", "OGN·045/298", "P2", "HAND");
        initial = RadGearTestPosition.Put(initial, "P2-R5", "UNL-R02", "P2", "BASE");
        var game = new Driver(initial); await HiddenNextTurn(game, "OGN·083/298");
        await FromFaceDown(game, "OGN·083/298", resolveSpell: false); await Priority(game, "P2");
        var original = Assert.Single(game.State.StackItems, item => item.SourceObjectId == Shadow).StackItemId;
        await game.Act("P2", new RecycleRuneCommand("P2-R5"));
        await game.Act("P2", new PlayCardCommand("COUNTER", "OGN·045/298", [original])); await game.Top();
        Assert.Contains(Shadow, game.State.PlayerZones["P1"].Graveyard);
        Assert.DoesNotContain(game.State.StackItems, item => item.EffectKind == CoreRuleEngine.StandbyPlayedEnemyDamageEffect);
        Assert.Empty(game.State.TriggerQueue); Assert.Equal(0, game.State.CardObjects["ENEMY"].Damage);
        while (game.State.StackItems.Count > 0) await game.Top(); await game.ColdCheckpoint();
    }

    [Theory, InlineData(false), InlineData(true)]
    public async Task EnemyWardUsesTheSharedTargetPaymentAndMayBeDeclined(bool pay)
    {
        var initial = RadGearTestPosition.Put(Position("OGN·083/298"), "WARDED", "OGN·013/298", "P2", "HAND");
        var game = new Driver(initial); await HiddenNextTurn(game, "OGN·083/298", ["WARDED"]);
        await FromFaceDown(game, "OGN·083/298"); await Target(game, "WARDED");
        var payment = Assert.IsType<PendingPaymentState>(game.State.PendingPayment);
        Assert.Equal(CoreRuleEngine.TriggerTargetCostWindow, payment.PaymentWindow);
        Assert.Equal(0, payment.ManaCost); Assert.Equal(1, payment.PowerCost);
        Assert.DoesNotContain(game.Events(), entry => entry.Event.Kind == "DAMAGE_APPLIED");
        if (pay) await game.Act("P1", new RecycleRuneCommand("P1-R0"));
        await game.Act("P1", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, [pay ? "PAY" : "DECLINE"]));
        while (game.State.StackItems.Count > 0) await game.Top();
        Assert.Equal(pay, game.Events().Any(entry => entry.Event.Kind == "DAMAGE_APPLIED"
            && Equals(entry.Event.Payload.GetValueOrDefault("targetObjectId"), "WARDED")));
        await game.ColdCheckpoint();
    }

    [Fact]
    public async Task AConfirmedDamageSkillSurvivesItsSourcesActualReturnToHand()
    {
        var game = new Driver(Position("OGN·083/298")); await HiddenNextTurn(game, "OGN·083/298");
        await FromFaceDown(game, "OGN·083/298"); await Target(game); await Priority(game, "P1");
        await Resources(game, "P1");
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["HOST"])); await game.Top();
        Assert.Contains("HOST", game.State.PlayerZones["P1"].Hand);
        await game.Top(); Assert.Equal(2, game.State.CardObjects["ENEMY"].Damage);
        while (game.State.StackItems.Count > 0) await game.Top();
        await game.ColdCheckpoint();
    }

    [Fact]
    public async Task TheConfirmedEnemyGenerationCannotRetargetAfterAnActualReturnToHand()
    {
        var game = new Driver(Position("OGN·083/298")); await HiddenNextTurn(game, "OGN·083/298");
        await FromFaceDown(game, "OGN·083/298"); await Target(game); await Priority(game, "P2");
        await game.Act("P2", new PlayCardCommand("ENEMY-RETURN", "OGN·104/298", ["ENEMY"])); await game.Top();
        Assert.Contains("ENEMY", game.State.PlayerZones["P2"].Hand);
        await game.Top(); Assert.Equal(0, game.State.CardObjects["ENEMY"].Damage);
        Assert.DoesNotContain(game.Events(), entry => entry.Event.Kind == "DAMAGE_APPLIED");
        while (game.State.StackItems.Count > 0) await game.Top();
        await game.ColdCheckpoint();
    }

    [Fact]
    public async Task ActualSongCopiesEachPrintedObserverWithoutRequiringTheEquipmentToStayAttached()
    {
        var initial = RadGearTestPosition.Put(Position("OGN·083/298"), "GEAR", "SFD·059/221", "P1", "BASE");
        var game = new Driver(initial);
        await game.Act("P1", new AssembleEquipmentCommand("GEAR", "HOST", ["ASSEMBLE_GREEN"]));
        while (game.State.StackItems.Count > 0) await game.Top();
        await HiddenNextTurn(game, "OGN·083/298");
        await FromFaceDown(game, "OGN·083/298"); await Target(game); await Target(game);
        Assert.Equal(2, game.State.StackItems.Count(item => item.EffectKind == CoreRuleEngine.StandbyPlayedEnemyDamageEffect));
        await Priority(game, "P1"); await Resources(game, "P1");
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["HOST"])); await game.Top();
        Assert.Contains("HOST", game.State.PlayerZones["P1"].Hand);
        Assert.Contains("GEAR", game.State.PlayerZones["P1"].Base);
        Assert.Null(game.State.CardObjects["GEAR"].AttachedToObjectId);
        while (game.State.StackItems.Count > 0) await game.Top();
        Assert.Contains("ENEMY", game.State.PlayerZones["P2"].Graveyard);
        Assert.Equal(2, game.Events().Count(entry => entry.Event.Kind == "DAMAGE_APPLIED"
            && Equals(entry.Event.Payload.GetValueOrDefault("targetObjectId"), "ENEMY")));
        await game.ColdCheckpoint();
    }

    [Theory]
    [InlineData("action-hash")] [InlineData("actor")] [InlineData("source-generation")]
    [InlineData("face-revision")] [InlineData("text-instance")] [InlineData("captured-tick")]
    public async Task ConsistentViewsCannotForgeTheOriginallyCapturedHideOrSource(string part)
    {
        var game = new Driver(Position()); await Hide(game);
        Assert.Empty(ValidateOriginalHistory(game, game.State));
        var item = Assert.Single(game.State.StackItems); var context = item.StandbyObserver!; var birth = context.Unit!;
        var forged = part switch {
            "action-hash" => context with { Unit = birth with { ActionHash = new string('a', 64) } },
            "actor" => context with { Unit = birth with { ControllerId = "P2" } },
            "source-generation" => context with { Source = context.Source with { Generation = context.Source.Generation + 1 } },
            "face-revision" => context with { Unit = birth with { SourceFaceRevision = birth.SourceFaceRevision + 1 } },
            "text-instance" => context with { Unit = birth with { RuleTextInstanceId = "PRINTED:FORGED:0" } },
            _ => context with { Unit = birth with { CapturedTick = birth.CapturedTick + 1 } }
        };
        var changed = game.State with { StackItems = [item with { StandbyObserver = forged }] };
        Assert.NotEmpty(ValidateOriginalHistory(game, changed)); await game.Top(); await game.ColdCheckpoint();
    }

    [Fact]
    public async Task ConsistentViewsCannotSubstituteAnotherLegalEnemyForTheConfirmedTarget()
    {
        var initial = RadGearTestPosition.Put(Position("OGN·083/298"), "OTHER-ENEMY", "OGN·219/298", "P2", "BASE");
        var game = new Driver(initial); await HiddenNextTurn(game, "OGN·083/298");
        await FromFaceDown(game, "OGN·083/298"); await Target(game);
        Assert.Empty(ValidateOriginalHistory(game, game.State));
        var item = Assert.Single(game.State.StackItems, item => item.EffectKind == CoreRuleEngine.StandbyPlayedEnemyDamageEffect);
        var forged = item with { TargetObjectIds = ["OTHER-ENEMY"], TargetGenerations = new Dictionary<string, long> {
            ["OTHER-ENEMY"] = game.State.CardObjects["OTHER-ENEMY"].ObjectGeneration } };
        var changed = game.State with { StackItems = game.State.StackItems.Select(old => old.StackItemId == item.StackItemId ? forged : old).ToArray() };
        Assert.NotEmpty(ValidateOriginalHistory(game, changed));
        while (game.State.StackItems.Count > 0) await game.Top(); await game.ColdCheckpoint();
    }

    private static IReadOnlyList<string> ValidateOriginalHistory(Driver game, MatchState state)
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        state = JsonSerializer.Deserialize<MatchState>(JsonSerializer.Serialize(state, options), options)!;
        var commands = JsonSerializer.Deserialize<RecoveredCommand[]>(JsonSerializer.Serialize(game.Commands(), options), options)!;
        var events = JsonSerializer.Deserialize<RecoveredEvent[]>(JsonSerializer.Serialize(game.Events(), options), options)!;
        var sequence = events.Length == 0 ? 0 : events[^1].Sequence;
        var snapshots = ResolutionResult.BuildSnapshots(state); var prompts = ResolutionResult.BuildPrompts(state);
        var views = state.Seats.Keys.ToDictionary(player => player,
            player => new RecoveredPlayerView(player, state.Tick, sequence, snapshots[player], state.Tick, sequence, prompts[player]));
        var spectator = MatchReplayRedactor.BuildSpectatorFrame(state.RoomId, state.Tick, sequence, [], state);
        return MatchRecoveryValidator.Validate(state.RoomId, sequence, commands, events, views, state, state.Tick, spectator);
    }

    [Fact]
    public async Task TheOriginalWardSelectionCannotBeChangedWhilePaymentIsStillPending()
    {
        var initial = RadGearTestPosition.Put(Position("OGN·083/298"), "WARD-ONE", "OGN·013/298", "P2", "HAND");
        initial = RadGearTestPosition.Put(initial, "WARD-TWO", "OGN·013/298", "P2", "HAND");
        var game = new Driver(initial); await HiddenNextTurn(game, "OGN·083/298", ["WARD-ONE", "WARD-TWO"]);
        await FromFaceDown(game, "OGN·083/298"); await Target(game, "WARD-ONE");
        Assert.Empty(ValidateOriginalHistory(game, game.State));
        var payment = Assert.IsType<PendingPaymentState>(game.State.PendingPayment);
        var item = Assert.Single(game.State.StackItems, item => item.EffectKind == CoreRuleEngine.StandbyPlayedEnemyDamageEffect);
        var forged = item with { TargetObjectIds = ["WARD-TWO"], TargetGenerations = new Dictionary<string, long> {
            ["WARD-TWO"] = game.State.CardObjects["WARD-TWO"].ObjectGeneration } };
        var changed = game.State with { StackItems = game.State.StackItems.Select(old => old.StackItemId == item.StackItemId ? forged : old).ToArray() };
        Assert.NotEmpty(ValidateOriginalHistory(game, changed));
        await game.Act("P1", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, ["DECLINE"]));
        while (game.State.StackItems.Count > 0) await game.Top(); await game.ColdCheckpoint();
    }
}
