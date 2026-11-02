using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class RadFaceDownPlayHeroTests
{
    private const string Normal = "RAD·090/167";
    private const string Alternate = "RAD·090a/167";
    public static IEnumerable<object[]> Prints() => new[] { Normal, Alternate }.Select(no => new object[] { no });

    [Theory]
    [InlineData(Normal, 39590)] [InlineData(Alternate, 39591)]
    public async Task EachHeroPrintingPaysItsOrangeCostAndEntersExhaustedWithoutTriggeringItself(string no, long id)
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, c => c.CardNo == no);
        Assert.Equal(id, card.Id); Assert.Equal("噬魂蚀骨", card.SubTitle);
        Assert.Equal(5, card.Energy); Assert.Equal(1, card.ReturnEnergy); Assert.Equal(5, card.Power);
        var effect = Assert.IsType<StandbyPlayedSelfEffect>(StandbyParser.SelfEffectWhenControllerPlaysFromFaceDown(card.CardEffect));
        Assert.True(effect.ReadiesSelf); Assert.Equal(1, effect.Might);
        Assert.True(CardBehaviorRegistry.TryGetByCardNo(no, out var behavior));
        Assert.False(behavior.CanPlayDuringPriority); Assert.False(behavior.CanPlayDuringSpellDuel);
        var s = VenUnitEntryBasicTests.Position(no);
        s = s with {
            PlayerZones = new Dictionary<string, PlayerZones>(s.PlayerZones) { ["P1"] = s.PlayerZones["P1"] with { Hand = [], ChampionZone = ["UNIT"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(s.ObjectLocations) { ["UNIT"] = new("P1", "CHAMPION") },
            RunePools = new Dictionary<string, RunePool>(s.RunePools) { ["P1"] = new(5, 0, new Dictionary<string, int> { [RuneTrait.Orange] = 1 }) } };
        var game = new Driver(s); await game.Act("P1", new PlayCardCommand("UNIT", no, [], Destination: "BASE"));
        Assert.Equal(0, game.State.RunePools["P1"].Mana); Assert.Equal(0, game.State.RunePools["P1"].TotalPower);
        Assert.Equal(5, game.State.CardObjects["UNIT"].Power); Assert.True(game.State.CardObjects["UNIT"].IsExhausted);
        Assert.Empty(game.State.StackItems); Assert.Empty(game.State.TriggerQueue); await game.Replay();
    }

    [Theory, MemberData(nameof(Prints))]
    public async Task AmbushUsesAnOccupiedFriendlyBattlefieldAndTheOrdinaryPaidResponsePath(string no)
    {
        var game = new Driver(OfficialCopyIdentityTests.AddSpell(AmbushPermissionTests.Position(no), "SPELL", "OGN·048/298"));
        await game.Act("P1", new PlayCardCommand("SPELL", "OGN·048/298", []));
        var s = game.State;
        var command = new PlayCardCommand("AMBUSH", no, [], Destination: "BATTLEFIELD:F");
        var quote = new CoreRuleEngine().PreviewPlayCard(s, "P1", PlayCostPreviewTests.Request(s, command));
        Assert.True(quote.CanPay, quote.Message); Assert.Equal(5, quote.Cost!.Mana);
        Assert.Equal(1, quote.Cost.PowerByTrait[RuneTrait.Orange]);
        await game.Act("P1", command);
        Assert.Equal(s.RunePools["P1"].Mana - 5, game.State.RunePools["P1"].Mana);
        Assert.Contains("AMBUSH", game.State.PlayerZones["P1"].Battlefields);
        Assert.Equal("F", game.State.ObjectLocations["AMBUSH"].BattlefieldObjectId);
        Assert.True(game.State.CardObjects["AMBUSH"].IsExhausted); Assert.Equal(5, game.State.CardObjects["AMBUSH"].Power);
        Assert.DoesNotContain(game.State.StackItems, i => i.StandbyObserver is not null); await game.Top(); await game.Replay();
    }

    public static IEnumerable<object[]> InvalidPlays() => Prints().SelectMany(row =>
        new[] { "empty", "base", "wrong-power", "no-mana", "priority" }.Select(branch => new object[] { row[0], branch }));

    [Theory, MemberData(nameof(InvalidPlays))]
    public async Task AmbushPreservesTimingLocationAndColoredPaymentRequirements(string no, string branch)
    {
        var s = await AmbushPermissionTests.Response(AmbushPermissionTests.Position(no));
        var command = new PlayCardCommand("AMBUSH", no, [], Destination: branch == "base" ? "BASE" : "BATTLEFIELD:F");
        if (branch == "empty") s = LocalDestructionRecallTests.At(s, "HOST", "P1", "BASE");
        if (branch == "wrong-power") s = s with { RunePools = new Dictionary<string, RunePool>(s.RunePools) {
            ["P1"] = new(5, 0, new Dictionary<string, int> { [RuneTrait.Blue] = 1 }) } };
        if (branch == "no-mana") s = s with { RunePools = new Dictionary<string, RunePool>(s.RunePools) { ["P1"] = new(4, 10) } };
        if (branch == "priority") s = s with { PriorityPlayerId = "P2", ActivePlayerId = "P2" };
        var hash = MatchStateHasher.Hash(s); var views = JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(s));
        var prompts = JsonSerializer.Serialize(ResolutionResult.BuildPrompts(s));
        Assert.False(new CoreRuleEngine().PreviewPlayCard(s, "P1", PlayCostPreviewTests.Request(s, command)).CanPay);
        var rejected = await new CoreRuleEngine().ResolveAsync(s, new("rad-evelynn-invalid", "P1", command.CmdType), command, default);
        Assert.False(rejected.Accepted); Assert.Empty(rejected.Events); Assert.Equal(hash, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(views, JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(rejected.State)));
        Assert.Equal(prompts, JsonSerializer.Serialize(ResolutionResult.BuildPrompts(rejected.State)));
    }

    private static MatchState Board(string no, bool exhausted = true, bool copied = false)
    {
        var s = StandbySourceRulesTests.Position();
        var unit = OfficialGarenConquestBattlefieldCountTests.Printed("HOST", no, "P1") with { IsExhausted = exhausted };
        var cards = new Dictionary<string, CardObjectState>(s.CardObjects) { ["HOST"] = unit };
        if (copied) cards["GEAR"] = ArmamentMightTests.Position(gear: "SFD·059/221", unit: no, attached: true).CardObjects["GEAR"];
        return s with { CardObjects = cards };
    }

    private static async Task<FaceDownPlayTestDriver> Ready(string no, bool exhausted = true, bool copied = false,
        bool withReturn = false, bool withCounter = false)
    {
        var s = Board(no, exhausted, copied);
        if (withReturn) s = OfficialCopyIdentityTests.AddSpell(s, "RETURN", "OGN·104/298");
        if (withCounter)
        {
            s = AttackDamageDistributionTests.Spell(s, "COUNTER", "SFD·045/221", "P2");
            s = RadGearTestPosition.Put(s, "COUNTER-GREEN", "UNL-R02", "P2", "BASE");
        }
        return await FaceDownPlayTestDriver.Ready(s);
    }

    private static async Task PlayShadow(FaceDownPlayTestDriver game, bool order = true)
    {
        await game.Act("P1", new PlayCardCommand("SHADOW", "OGN·057/298", ["HOST"]));
        if (order && game.State.TriggerQueue.Count > 0) await game.Act("P1", new OrderTriggersCommand(
            OrderedTriggerIds: game.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
    }

    public static IEnumerable<object[]> Readiness() => Prints().SelectMany(row => new[] { false, true }.Select(exhausted => new object[] { row[0], exhausted }));
    [Theory, MemberData(nameof(Readiness))]
    public async Task ARealFaceDownPlayQueuesARespondableReadyThenMightSkill(string no, bool exhausted)
    {
        var game = await Ready(no, exhausted); await PlayShadow(game);
        var observer = Assert.Single(game.State.StackItems, i => i.StandbyObserver is not null);
        Assert.Equal("HOST", observer.SourceObjectId); Assert.Empty(observer.TargetObjectIds);
        Assert.Equal(exhausted, game.State.CardObjects["HOST"].IsExhausted); Assert.Equal(5, game.State.CardObjects["HOST"].Power);
        await game.Top(); Assert.False(game.State.CardObjects["HOST"].IsExhausted); Assert.Equal(6, game.State.CardObjects["HOST"].Power);
        Assert.Single(game.State.CardObjects["HOST"].UntilEndOfTurnPowerModifiers);
        Assert.DoesNotContain(CardObjectTags.Boon, game.State.CardObjects["HOST"].Tags);
        Assert.Equal(exhausted ? 1 : 0, game.Events().Count(e => e.Event.Kind == "UNIT_READIED"));
        await game.Top(); await game.Replay();
    }

    [Theory, MemberData(nameof(Prints))]
    public async Task HidingAndOrdinaryHandPlayDoNotCreateTheFaceDownObserver(string no)
    {
        var s = Board(no);
        var hidden = await OfficialGraveyardRecastTests.Act(s, "P1", new HideCardCommand("SHADOW", "OGN·057/298", "BATTLEFIELD:F", ["STANDBY_A"]));
        Assert.Empty(hidden.State.StackItems); Assert.Empty(hidden.State.TriggerQueue);
        Assert.Equal(5, hidden.State.CardObjects["HOST"].Power); Assert.True(hidden.State.CardObjects["HOST"].IsExhausted);
        s = OfficialCopyIdentityTests.AddSpell(s with { RunePools = new Dictionary<string, RunePool>(s.RunePools) { ["P1"] = new(20, 20) } }, "ORDINARY", "OGN·048/298");
        var game = new Driver(s); await game.Act("P1", new PlayCardCommand("ORDINARY", "OGN·048/298", []));
        Assert.DoesNotContain(game.State.StackItems, i => i.StandbyObserver is not null);
        await game.Top(); Assert.Equal(5, game.State.CardObjects["HOST"].Power); Assert.True(game.State.CardObjects["HOST"].IsExhausted); await game.Replay();
    }

    [Theory, MemberData(nameof(Prints))]
    public async Task PrintedAndCopiedTextEarnSeparateBonusesAndOnlyOneActualReadyTransition(string no)
    {
        var game = await Ready(no, copied: true); await PlayShadow(game, order: false);
        Assert.Equal(2, game.State.TriggerQueue.Count);
        var wrongEvent = game.State with { TriggerQueue = game.State.TriggerQueue.Select((trigger, index) =>
            index == 0 ? trigger with { TriggeredByEventKind = "UNIT_DESTROYED" } : trigger).ToArray() };
        Assert.Contains("invalid queued standby play observer", OfficialInsightAndSpellLockTests.Errors(wrongEvent));
        await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds:
            game.State.TriggerQueue.Select(trigger => trigger.TriggerId).ToArray()));
        Assert.Equal(2, game.State.StackItems.Count(i => i.StandbyObserver is not null));
        await game.Top(); Assert.Equal(6, game.State.CardObjects["HOST"].Power);
        await game.Top(); Assert.Equal(7, game.State.CardObjects["HOST"].Power);
        Assert.False(game.State.CardObjects["HOST"].IsExhausted);
        Assert.Equal(2, game.State.CardObjects["HOST"].UntilEndOfTurnPowerModifiers.Count);
        Assert.Single(game.Events(), e => e.Event.Kind == "UNIT_READIED"); await game.Top(); await game.Replay();
    }

    [Theory, MemberData(nameof(Prints))]
    public async Task ReturningTheSourceBeforeResolutionCannotBuffItsNewIncarnation(string no)
    {
        var game = await Ready(no, withReturn: true); await PlayShadow(game);
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["HOST"])); await game.Top();
        Assert.Contains("HOST", game.State.PlayerZones["P1"].Hand);
        var snapshots = ResolutionResult.BuildSnapshots(game.State);
        var prompts = ResolutionResult.BuildPrompts(game.State);
        var views = snapshots.ToDictionary(p => p.Key, p => new RecoveredPlayerView(p.Key,
            game.State.Tick, 0, p.Value, game.State.Tick, 0, prompts[p.Key]));
        var spectator = MatchReplayRedactor.BuildSpectatorFrame(game.State.RoomId, game.State.Tick, 0, [], game.State);
        Assert.Equal("HOST", Assert.IsType<Dictionary<string, object?>>(Assert.Single(
            snapshots["P1"].Stack, row => row is Dictionary<string, object?> view && Equals(view["cardNo"], no)))["sourceObjectId"]);
        foreach (var snapshot in new[] { snapshots["P2"], spectator.SpectatorSnapshot })
        {
            var row = Assert.IsType<Dictionary<string, object?>>(Assert.Single(
                snapshot.Stack, row => row is Dictionary<string, object?> view && Equals(view["cardNo"], no)));
            Assert.Equal("HIDDEN", row["sourceObjectId"]);
            Assert.Equal("HIDDEN", row["sourceVisibility"]);
            Assert.Equal("STANDBY_PLAYED_SELF_MIGHT", row["effectKind"]);
            Assert.DoesNotContain("\"HOST\"", JsonSerializer.Serialize(snapshot.Stack));
            row["sourceObjectId"] = "HOST";
            Assert.NotEmpty(MatchRecoveryValidator.ValidateComponentStateAndViews(game.State, views, spectator));
            row["sourceObjectId"] = "HIDDEN";
        }
        Assert.Empty(MatchRecoveryValidator.ValidateComponentStateAndViews(game.State, views, spectator));
        await game.Top(); Assert.Equal(5, game.State.CardObjects["HOST"].Power);
        Assert.Empty(game.State.CardObjects["HOST"].UntilEndOfTurnPowerModifiers);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "UNIT_READIED"); await game.Top(); await game.Replay();
    }

    [Theory, MemberData(nameof(Prints))]
    public async Task ACounterRequiringFriendlyTargetsCannotChooseTheUntargetedObserver(string no)
    {
        var game = await Ready(no, withCounter: true); await PlayShadow(game);
        var observer = Assert.Single(game.State.StackItems, i => i.StandbyObserver is not null);
        await game.Act("P1", new PassPriorityCommand());
        await game.Act("P2", new RecycleRuneCommand("COUNTER-GREEN"));
        await RadGearTestPosition.Reject(game.State, "P2",
            new PlayCardCommand("COUNTER", "SFD·045/221", [observer.StackItemId]));
        Assert.Contains(game.State.StackItems, i => i.StandbyObserver is not null);
        await game.Top(); Assert.False(game.State.CardObjects["HOST"].IsExhausted);
        Assert.Equal(6, game.State.CardObjects["HOST"].Power);
        await game.Top(); await game.Replay();
    }
}
