using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class AvaStandbyAttackPlayTests
{
    private const string Ava = "OGN·107/298", Cub = "OGN·135/298", Draw = "OGN·083/298", Blade = "SFD·139/221";

    [Theory]
    [InlineData(Cub)]
    [InlineData(Draw)]
    [InlineData(Blade)]
    public async Task ActualAttackPaysBlueBeforeResponsesAndPlaysOnlyTheChosenHandCardForFree(string card)
    {
        var game = new Driver(Position(card));
        await Attack(game);
        Assert.Null(game.State.PendingEffectPlay);
        Assert.Contains("STANDBY", game.State.PlayerZones["P1"].Hand);
        var payment = Assert.IsType<PendingPaymentState>(game.State.PendingPayment);
        Assert.Equal(1, payment.PowerCostByTrait[RuneTrait.Blue]);
        await Pay(game);
        Assert.Equal(0, game.State.RunePools["P1"].TotalPower);
        Assert.NotNull(game.State.PriorityPlayerId);
        Assert.Null(game.State.PendingEffectPlay);
        Assert.Empty(Assert.Single(game.State.StackItems).TargetObjectIds);
        await game.Top();
        var pending = Assert.IsType<PendingEffectPlayState>(game.State.PendingEffectPlay);
        Assert.Equal(new[] { "STANDBY" }, pending.Sources.Keys);
        Assert.True(pending.IgnoreAllCosts);
        Assert.False(pending.Optional);
        await Reject(game.State, "P2", new PlayCardCommand("STANDBY", card, []));
        await Reject(game.State, "P1", new ChooseCardsCommand(pending.ChoiceId, "EFFECT_PLAY", []));
        await Reject(game.State, "P1", new PlayCardCommand("STANDBY", card, [], Destination: card == Cub ? "BASE" : "BATTLEFIELD:F"));
        var play = new PlayCardCommand("STANDBY", card, [], Destination: card == Cub ? "BATTLEFIELD:F" : "BASE");
        var quote = new CoreRuleEngine().PreviewPlayCard(game.State, "P1", PlayCostPreviewTests.Request(game.State, play));
        Assert.True(quote.CanPay, quote.Message);
        Assert.Equal(0, quote.Cost!.Mana);
        Assert.Equal(0, quote.Cost.GenericPower + quote.Cost.PowerByTrait.Values.Sum());
        await game.Act("P1", play);
        Assert.Null(game.State.PendingEffectPlay);
        Assert.Equal(0, game.State.RunePools["P1"].Mana);
        Assert.Equal(0, game.State.RunePools["P1"].TotalPower);
        if (card == Draw)
        {
            Assert.Single(game.State.StackItems);
            await game.Top();
            Assert.Contains("STANDBY", game.State.PlayerZones["P1"].Graveyard);
            var drawn = Assert.Single(game.Events(), e => e.Event.Kind == "CARD_DRAWN");
            Assert.Equal(2, Convert.ToInt32(drawn.Event.Payload["count"]));
            Assert.Equal(new[] { "ATK_TOP", "ATK_OTHER" }, game.State.PlayerZones["P1"].Hand);
        }
        else
        {
            Assert.Equal(card == Cub ? "BATTLEFIELD" : "BASE", game.State.ObjectLocations["STANDBY"].Zone);
            Assert.False(game.State.CardObjects["STANDBY"].IsFaceDown);
            Assert.Null(game.State.CardObjects["STANDBY"].StandbyPlacement);
            Assert.Null(game.State.CardObjects["STANDBY"].AttachedToObjectId);
            if (card == Cub) Assert.Equal("F", game.State.ObjectLocations["STANDBY"].BattlefieldObjectId);
        }
        await game.Replay();
    }

    [Fact]
    public async Task DecliningTheAttackTriggerKeepsBlueAndDoesNotExposeTheHand()
    {
        var game = new Driver(Position());
        await Attack(game);
        await Pay(game, false);
        Assert.Equal(1, game.State.RunePools["P1"].TotalPower);
        Assert.Empty(game.State.StackItems);
        Assert.Null(game.State.PendingEffectPlay);
        Assert.Contains("STANDBY", game.State.PlayerZones["P1"].Hand);
        await game.Replay();
    }

    [Theory]
    [InlineData("red")]
    [InlineData("none")]
    public async Task MissingOrWrongColorPowerCannotPayTheBlueConfirmation(string color)
    {
        var game = new Driver(Position());
        await Attack(game);
        var payment = game.State.PendingPayment!;
        var state = game.State with { RunePools = new Dictionary<string, RunePool>(game.State.RunePools) {
            ["P1"] = color == "none" ? RunePool.Empty : new(0, 0, new Dictionary<string, int> { [color] = 1 }) } };
        await Reject(state, "P1", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, ["PAY"]));
        await Pay(game, false);
    }

    [Theory]
    [InlineData("empty")]
    [InlineData("non-keyword")]
    [InlineData("champion")]
    [InlineData("graveyard")]
    public async Task AConfirmedAbilityWithNoEligibleHandCardFinishesWithoutAnInventedChoice(string source)
    {
        var initial = Position(source == "non-keyword" ? "OGN·096/298" : Cub);
        if (source != "non-keyword")
        {
            var destination = source == "champion" ? "CHAMPION" : source == "graveyard" ? "GRAVEYARD" : "BANISHED";
            var z = initial.PlayerZones["P1"];
            initial = initial with { PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
                ["P1"] = z with { Hand = [], ChampionZone = destination == "CHAMPION" ? [..z.ChampionZone, "STANDBY"] : z.ChampionZone,
                    Graveyard = destination == "GRAVEYARD" ? [..z.Graveyard, "STANDBY"] : z.Graveyard,
                    Banished = destination == "BANISHED" ? [..z.Banished, "STANDBY"] : z.Banished } },
                ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) { ["STANDBY"] = new("P1", destination) } };
        }
        var game = new Driver(initial);
        await Attack(game); await Pay(game); await game.Top();
        Assert.Null(game.State.PendingEffectPlay);
        Assert.Empty(game.State.StackItems);
        Assert.Equal(0, game.State.RunePools["P1"].TotalPower);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "CARD_PLAYED" && Equals(e.Event.Payload.GetValueOrDefault("sourceObjectId"), "STANDBY"));
        await game.Replay();
    }

    [Fact]
    public async Task ARealResponseMovingAvaToBaseMakesTheUnitEnterHerCurrentLocation()
    {
        var initial = HandDiscardObserverTests.Put(Position(), "FLASH", "OGS·011/024", "P1", "HAND");
        initial = initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) { ["P1"] = new(20, 20,
            new Dictionary<string, int> { [RuneTrait.Blue] = 1 }) } };
        var game = new Driver(initial);
        await Attack(game); await Pay(game);
        await game.Act("P1", new PlayCardCommand("FLASH", "OGS·011/024", ["HOST"])); await game.Top();
        Assert.Equal("BASE", game.State.ObjectLocations["HOST"].Zone);
        await game.Top();
        await Reject(game.State, "P1", new PlayCardCommand("STANDBY", Cub, [], Destination: "BATTLEFIELD:F"));
        await game.Act("P1", new PlayCardCommand("STANDBY", Cub, []));
        Assert.Contains("STANDBY", game.State.PlayerZones["P1"].Base);
        await game.Replay();
    }

    [Theory]
    [InlineData(Cub)]
    [InlineData(Draw)]
    public async Task ReturningAvaInResponsePreservesTheAbilityButCannotReuseHerOldHere(string card)
    {
        var initial = HandDiscardObserverTests.Put(Position(card), "RETURN", "OGN·104/298", "P1", "HAND");
        initial = initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) { ["P1"] = new(20, 20,
            new Dictionary<string, int> { [RuneTrait.Blue] = 1 }) } };
        var game = new Driver(initial);
        await Attack(game); await Pay(game);
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["HOST"])); await game.Top();
        Assert.Contains("HOST", game.State.PlayerZones["P1"].Hand);
        await game.Top();
        if (card == Cub)
        {
            await Reject(game.State, "P1", new PlayCardCommand("STANDBY", card, [], Destination: "BATTLEFIELD:F"));
            await Reject(game.State, "P1", new PlayCardCommand("STANDBY", card, [], Destination: "BASE"));
            var p = game.State.PendingEffectPlay!;
            await game.Act("P1", new ChooseCardsCommand(p.ChoiceId, "EFFECT_PLAY", []));
            Assert.Contains("STANDBY", game.State.PlayerZones["P1"].Hand);
        }
        else
        {
            await game.Act("P1", new PlayCardCommand("STANDBY", card, [])); await game.Top();
            Assert.Contains("STANDBY", game.State.PlayerZones["P1"].Graveyard);
        }
        Assert.Null(game.State.PendingEffectPlay);
        await game.Replay();
    }

    [Fact]
    public async Task MixedHandCandidatesCannotLetTheSpellDestinationBypassTheUnitRequirement()
    {
        var initial = HandDiscardObserverTests.Put(Position(), "DRAW", Draw, "P1", "HAND");
        initial = HandDiscardObserverTests.Put(initial, "ORDINARY", "OGN·096/298", "P1", "HAND");
        var game = new Driver(initial);
        await Attack(game); await Pay(game); await game.Top();
        Assert.Equal(new[] { "DRAW", "STANDBY" }, game.State.PendingEffectPlay!.Sources.Keys.Order());
        await Reject(game.State, "P1", new PlayCardCommand("STANDBY", Cub, []));
        await Reject(game.State, "P1", new PlayCardCommand("ORDINARY", "OGN·096/298", [], Destination: "BATTLEFIELD:F"));
        await game.Act("P1", new PlayCardCommand("STANDBY", Cub, [], Destination: "BATTLEFIELD:F"));
        Assert.Contains("DRAW", game.State.PlayerZones["P1"].Hand);
        await game.Replay();
    }

    [Fact]
    public async Task RecoveryRejectsForgedSourceCostsCandidatesAndDestinationPolicy()
    {
        var game = new Driver(Position());
        await Attack(game); await Pay(game); await game.Top();
        var p = game.State.PendingEffectPlay!;
        var parent = p.Parent;
        var mutations = new[] {
            p with { IgnoreAllCosts = false }, p with { IgnoreBasePower = false }, p with { ManaReduction = 1 },
            p with { Optional = true }, p with { SourceZone = "GRAVEYARD" }, p with { DestinationPolicy = "ANY" },
            p with { PlayerId = "P2" }, p with { Sources = new Dictionary<string, long>() },
            p with { Sources = new Dictionary<string, long> { ["STANDBY"] = p.Sources["STANDBY"] + 1 } },
            p with { Parent = parent with { TriggerCost = null } },
            p with { Parent = parent with { UnitEvent = parent.UnitEvent! with { SourceGeneration = parent.UnitEvent.SourceGeneration + 1 } } },
            p with { Parent = parent with { EffectPlayCompleted = true } }
        };
        foreach (var bad in mutations)
            Assert.Contains(OfficialInsightAndSpellLockTests.Errors(game.State with { PendingEffectPlay = bad }),
                error => error.Contains("standby hand effect-play", StringComparison.Ordinal));
        await game.Act("P1", new PlayCardCommand("STANDBY", Cub, [], Destination: "BATTLEFIELD:F"));
        await game.Replay();
    }

    [Fact]
    public async Task ActuallyCopiedAttackTextStillPlaysAtTheHostAfterTheCopyEquipmentIsRemoved()
    {
        var initial = Position();
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["HOST"] = initial.CardObjects["HOST"] with { CardNo = "OGN·096/298", Power = 1 },
            ["GEAR"] = initial.CardObjects["GEAR"] with { CardNo = "VEN·137" } },
            RunePools = new Dictionary<string, RunePool>(initial.RunePools) { ["P1"] = new(20, 20,
                new Dictionary<string, int> { [RuneTrait.Blue] = 1 }) } };
        initial = OfficialHoldSequenceTests.AddUnit(initial, "COPY-SOURCE", Ava, "F", "P1");
        initial = LocalDestructionRecallTests.At(initial, "COPY-SOURCE", "P1", "BASE");
        initial = HandDiscardObserverTests.Put(initial, "DETACH", "SFD·011/221", "P1", "HAND");
        var game = new Driver(initial);
        await game.Act("P1", new AssembleEquipmentCommand("GEAR", "HOST", ["ASSEMBLE_YELLOW"])); await game.Top();
        var choice = game.State.PendingRuleChoice!;
        await game.Act("P1", new PayCostCommand(choice.Request.Id, "RULE_REPLACEMENT", ["COPY:COPY-SOURCE"]));
        Assert.Equal(Ava, game.State.CardObjects["HOST"].CardNo);
        await Attack(game); await Pay(game);
        Assert.NotNull(Assert.Single(game.State.StackItems).UnitEvent!.Origin);
        await game.Act("P1", new PlayCardCommand("DETACH", "SFD·011/221", ["HOST", "GEAR"])); await game.Top();
        Assert.Equal("OGN·096/298", game.State.CardObjects["HOST"].CardNo);
        await game.Top();
        await game.Act("P1", new PlayCardCommand("STANDBY", Cub, [], Destination: "BATTLEFIELD:F"));
        Assert.Equal("F", game.State.ObjectLocations["STANDBY"].BattlefieldObjectId);
        await game.Replay();
    }

    [Fact]
    public async Task AFreePlayCannotBypassTheBattlefieldsPlayProhibition()
    {
        var initial = Position();
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["F"] = initial.CardObjects["F"] with { CardNo = "SFD·216/221" } } };
        var game = new Driver(initial);
        await Attack(game); await Pay(game); await game.Top();
        await Reject(game.State, "P1", new PlayCardCommand("STANDBY", Cub, [], Destination: "BATTLEFIELD:F"));
        var p = game.State.PendingEffectPlay!;
        await game.Act("P1", new ChooseCardsCommand(p.ChoiceId, "EFFECT_PLAY", []));
        Assert.Contains("STANDBY", game.State.PlayerZones["P1"].Hand);
        Assert.Null(game.State.PendingEffectPlay);
        await game.Replay();
    }

    [Fact]
    public async Task AnUnjournalledConfirmationCannotInventAFreePlayPermission()
    {
        var initial = Position();
        var game = new Driver(initial);
        await Attack(game);
        var payment = game.State.PendingPayment!;
        var command = new PayCostCommand(payment.PaymentId, payment.PaymentWindow, ["PAY"]);
        var simulated = await new CoreRuleEngine().ResolveAsync(game.State, new("ava-unjournalled", "P1", command.CmdType), command, default);
        Assert.True(simulated.Accepted, simulated.ErrorMessage);
        var forged = simulated.State with { Tick = game.State.Tick };
        var sequence = game.Events().Last().Sequence;
        var snapshots = ResolutionResult.BuildSnapshots(forged);
        var prompts = ResolutionResult.BuildPrompts(forged);
        var views = forged.Seats.Keys.ToDictionary(p => p, p => new RecoveredPlayerView(p, forged.Tick, sequence,
            snapshots[p], forged.Tick, sequence, prompts[p]));
        var spectator = MatchReplayRedactor.BuildSpectatorFrame(forged.RoomId, forged.Tick, sequence, [], forged);
        var errors = MatchRecoveryValidator.Validate(forged.RoomId, sequence, game.Commands(), game.Events(), views, forged, forged.Tick, spectator);
        Assert.Contains(errors, e => e.Contains("standby hand effect-play receipt", StringComparison.Ordinal)
            && e.Contains("accepted payment history", StringComparison.Ordinal));
        var frame = new MatchRecoveryFrame(forged.RoomId, forged.Tick, sequence, game.Commands(), game.Events(), views,
            errors, forged, initial, spectator) { OpeningSeed = initial.Seed };
        Assert.Throws<MatchSessionException>(() => MatchSession.Restore(frame, new CoreRuleEngine(), NoopMatchJournal.Instance));
        await Pay(game); await game.Top();
        await game.Act("P1", new PlayCardCommand("STANDBY", Cub, [], Destination: "BATTLEFIELD:F"));
        await game.Replay();
    }

    private static MatchState Position(string card = Cub)
    {
        var s = HandDiscardObserverTests.Put(AttackDeckPlayTests.Position(Ava), "STANDBY", card, "P1", "HAND");
        return s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) { ["HOST"] = s.CardObjects["HOST"] with { Power = 4 } },
            RunePools = new Dictionary<string, RunePool>(s.RunePools) { ["P1"] = new(0, 0, new Dictionary<string, int> { [RuneTrait.Blue] = 1 }) } };
    }

    private static async Task Attack(Driver game)
    {
        await game.Act("P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F", []));
        for (var i = 0; i < 4 && game.State.FocusPlayerId is not null && game.State.StackItems.Count == 0; i++)
            await game.Act(game.State.FocusPlayerId!, new PassFocusCommand());
        if (game.PromptFor("P1").EnabledActions().Contains(CommandTypes.DeclareBattle))
            await game.Act("P1", new DeclareBattleCommand("F", ["HOST"], ["ENEMY1", "ENEMY2", "ENEMY3"], ["COMBAT_ASSIGNMENT"]));
        Assert.True(game.State.CardObjects["HOST"].IsAttacking);
        Assert.NotNull(game.State.PendingPayment);
    }

    private static Task Pay(Driver game, bool pay = true)
    {
        var p = game.State.PendingPayment!;
        return game.Act("P1", new PayCostCommand(p.PaymentId, p.PaymentWindow, [pay ? "PAY" : "DECLINE"]));
    }

    private static async Task Reject(MatchState state, string player, GameCommand command)
    {
        var hash = MatchStateHasher.Hash(state);
        var result = await new CoreRuleEngine().ResolveAsync(state, new("ava-reject", player, command.CmdType), command, default);
        Assert.False(result.Accepted);
        Assert.Equal(hash, MatchStateHasher.Hash(result.State));
        Assert.Empty(result.Events);
        Assert.Equal(JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(state)), JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(result.State)));
    }
}
