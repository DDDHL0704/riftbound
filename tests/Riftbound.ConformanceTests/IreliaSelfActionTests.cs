using System.Reflection;
using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class IreliaSelfActionTests
{
    private static readonly string[] Prints = ["SFD·057/221", "SFD·057a/221", "SFD·225/221", "SFD·225*/221", "VEN·174"];
    public static IEnumerable<object[]> Printings() => Prints.Select(no => new object[] { no });
    public static IEnumerable<object[]> ReadyCases() => Prints.SelectMany(no => new[] { true, false }.Select(ready => new object[] { no, ready }));

    private static MatchState Position(string no = "VEN·174", bool exhausted = true, bool copied = false)
    {
        var state = ArmamentMightTests.Position(gear: copied ? "SFD·059/221" : "SFD·022/221", unit: no, power: 4, attached: copied);
        return state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["HOST"] = state.CardObjects["HOST"] with { IsExhausted = exhausted, Tags = [CardObjectTags.UnitCard, "法盾"] } },
            RunePools = new Dictionary<string, RunePool> { ["P1"] = new(30, 30), ["P2"] = new(30, 30) } };
    }

    private static MatchState Spell(MatchState state, string no, string player = "P1", string id = "SPELL")
        => AttackDamageDistributionTests.Spell(state, id, no, player);

    private static bool Observer(StackItemState item) => item.FieldContext?.Kind == "CONTROLLER_TARGETED_OR_READIED_SELF_POWER";

    private static async Task Order(Driver game)
    {
        if (game.State.TriggerQueue.Count > 1)
            await game.Act(game.State.TriggerQueue[0].ControllerId,
                new OrderTriggersCommand(OrderedTriggerIds: game.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
    }

    [Theory]
    [MemberData(nameof(Printings))]
    public async Task EveryPrintingParsesTheActualClauseAndEntersWithWard(string no)
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, c => c.CardNo == no);
        Assert.Equal("虔心承志", card.SubTitle);
        var spec = Assert.Single(RuleTextParser.Parse(card).Triggers,
            t => t.Kind == TriggerKinds.ControllerTargetedOrReadiedSourcePower);
        Assert.Equal(1, spec.PowerDelta); Assert.Equal(TriggerTargetScopes.SourceUnit, spec.TargetScope);
        var game = new Driver(VenUnitEntryBasicTests.Position(no) with { RunePools = new Dictionary<string, RunePool> {
            ["P1"] = new(20, 0, new Dictionary<string, int> { [RuneTrait.Green] = 2 }), ["P2"] = new(20, 20) } });
        await game.Act("P1", new PlayCardCommand("UNIT", no, [], Destination: "BASE"));
        Assert.Equal(15, game.State.RunePools["P1"].Mana);
        Assert.Equal(2, game.State.RunePools["P1"].PowerByTrait[RuneTrait.Green]);
        Assert.Equal(4, game.State.CardObjects["UNIT"].Power); Assert.True(game.State.CardObjects["UNIT"].IsExhausted);
        Assert.Contains("法盾", game.State.CardObjects["UNIT"].Tags); Assert.Empty(game.State.StackItems);
        await game.Replay();
    }

    [Theory]
    [MemberData(nameof(ReadyCases))]
    public async Task FriendlyTargetingAndActualReadyEarnSeparateRespondableSkills(string no, bool exhausted)
    {
        var game = new Driver(Spell(Position(no, exhausted), "UNL-009/219"));
        await game.Act("P1", new PlayCardCommand("SPELL", "UNL-009/219", ["HOST"]));
        Assert.Equal(2, game.State.StackItems.Count); Assert.True(Observer(game.State.StackItems[^1]));
        Assert.Equal(4, game.State.CardObjects["HOST"].Power);
        await game.Top(); Assert.Equal(5, game.State.CardObjects["HOST"].Power);
        await game.Top(); Assert.False(game.State.CardObjects["HOST"].IsExhausted);
        if (exhausted) {
            Assert.True(Observer(Assert.Single(game.State.StackItems)));
            Assert.Equal(5, game.State.CardObjects["HOST"].Power);
            await game.Top();
        }
        Assert.Empty(game.State.StackItems); Assert.Equal(exhausted ? 6 : 5, game.State.CardObjects["HOST"].Power);
        Assert.Equal(exhausted ? 2 : 1, game.Events().Count(e => e.Event.Kind == "TRIGGER_RESOLVED"
            && Equals(e.Event.Payload.GetValueOrDefault("effectKind"), "CONTROLLER_TARGETED_OR_READIED_SELF_POWER")));
        await game.Act("P1", new EndTurnCommand()); Assert.Equal(4, game.State.CardObjects["HOST"].Power);
        await game.Replay();
    }

    [Theory]
    [MemberData(nameof(Printings))]
    public async Task OpposingTargetAndOpposingReadyDoNotTriggerTheControllerClause(string no)
    {
        var state = Spell(Position(no), "UNL-009/219", "P2") with { TurnPlayerId = "P2", ActivePlayerId = "P2" };
        var game = new Driver(state);
        await game.Act("P2", new PlayCardCommand("SPELL", "UNL-009/219", ["HOST"]));
        Assert.Single(game.State.StackItems); await game.Top();
        Assert.Empty(game.State.StackItems); Assert.False(game.State.CardObjects["HOST"].IsExhausted);
        Assert.Equal(4, game.State.CardObjects["HOST"].Power); await game.Replay();
    }

    [Theory]
    [MemberData(nameof(Printings))]
    public async Task UntargetedMassReadyOnlyEarnsTheReadySkill(string no)
    {
        var game = new Driver(Spell(Position(no), "SFD·204/221"));
        await game.Act("P1", new PlayCardCommand("SPELL", "SFD·204/221", []));
        Assert.Single(game.State.StackItems); await game.Top();
        Assert.True(Observer(Assert.Single(game.State.StackItems))); Assert.Equal(4, game.State.CardObjects["HOST"].Power);
        await game.Top(); Assert.Equal(5, game.State.CardObjects["HOST"].Power); await game.Replay();
    }

    [Theory]
    [MemberData(nameof(Printings))]
    public async Task IllegalPlayCannotCreateATargetingTriggerOrSpendResources(string no)
    {
        var state = Spell(Position(no), "SFD·034/221");
        foreach (var targets in new string[][] { ["GEAR"], ["HOST", "HOST"], ["MISSING"] }) {
            var command = new PlayCardCommand("SPELL", "SFD·034/221", targets);
            var rejected = await new CoreRuleEngine().ResolveAsync(state, new("bad-self-target", "P1", command.CmdType), command, default);
            Assert.False(rejected.Accepted); Assert.Empty(rejected.Events);
            Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(rejected.State));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TriggeredUnitPlaySkillConfirmsItsTargetBeforeEarningTheObserver(bool selectedInPlayCommand)
    {
        var state = OfficialCopyIdentityTests.AddSpell(Position(), "MATE", "OGN·132/298");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["MATE"] = new("MATE", cardNo: "OGN·132/298", manaCost: 3, power: 3, ownerId: "P1", controllerId: "P1",
                tags: [CardObjectTags.UnitCard]) } };
        var game = new Driver(state);
        await game.Act("P1", new PlayCardCommand("MATE", "OGN·132/298", selectedInPlayCommand ? ["HOST"] : [], Destination: "BASE"));
        if (!selectedInPlayCommand) {
            Assert.NotNull(game.State.PendingCardChoice); Assert.DoesNotContain(game.State.StackItems, Observer);
            var choice = game.State.PendingCardChoice!;
            await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["HOST"]));
        }
        Assert.Null(game.State.PendingCardChoice);
        Assert.True(Observer(game.State.StackItems[^1]));
        await game.Top(); await game.Top(); await game.Top();
        Assert.Equal(6, game.State.CardObjects["HOST"].Power); await game.Replay();
    }

    [Fact]
    public async Task FriendlyLegendReactionTargetsAndReadiesAsTwoIndependentActions()
    {
        var state = Spell(Position(), "SFD·034/221");
        state = state with {
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["IRELIA"] = new("IRELIA", cardNo: "SFD·195/221", ownerId: "P1", controllerId: "P1") },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P1"] = state.PlayerZones["P1"] with { LegendZone = ["IRELIA"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["IRELIA"] = new("P1", "LEGEND") } };
        var game = new Driver(state);
        await game.Act("P1", new PlayCardCommand("SPELL", "SFD·034/221", ["HOST"]));
        await game.Top();
        await game.Act("P1", new LegendActCommand("IRELIA", "LEGEND_REACTION_PAY_1_EXHAUST_READY_TARGETED_FRIENDLY_UNIT",
            ["HOST"], ["SPEND_MANA:1"]));
        Assert.False(game.State.CardObjects["HOST"].IsExhausted);
        Assert.Equal(5, game.State.CardObjects["HOST"].Power);
        Assert.Equal(2, game.State.TriggerQueue.Count);
        Assert.Equal(new[] { "READIED", "TARGETED" }, game.State.TriggerQueue
            .Select(t => t.FieldContext!.SelfAction!.Action).Order(StringComparer.Ordinal).ToArray());
        await Order(game); await game.Top(); await game.Top();
        Assert.Equal(7, game.State.CardObjects["HOST"].Power); await game.Replay();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DestructionCostReferenceNeverTargetsTheUnitEvenWhenDestructionIsReplaced(bool replaced)
    {
        var state = Position();
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["MALZ"] = new("MALZ", cardNo: "OGN·113/298", power: 3, ownerId: "P1", controllerId: "P1", tags: [CardObjectTags.UnitCard]),
                ["GEAR"] = state.CardObjects["GEAR"] with { CardNo = replaced ? "OGN·077/298" : "SFD·022/221" } },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P1"] = state.PlayerZones["P1"] with { Base = [..state.PlayerZones["P1"].Base, "MALZ"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["MALZ"] = new("P1", "BASE") } };
        var game = new Driver(state);
        await game.Act("P1", new ActivateAbilityCommand("MALZ", P4ActivatedAbilityCatalog.MalzaharResourceAbilityId, ["HOST"]));
        Assert.Empty(game.State.StackItems); Assert.Empty(game.State.TriggerQueue);
        Assert.Equal(4, game.State.CardObjects["HOST"].Power);
        Assert.Equal(replaced, game.State.PlayerZones["P1"].Base.Contains("HOST")); await game.Replay();
    }

    [Fact]
    public async Task AttachedTextCopiesObserveTheSameSelectionIndependently()
    {
        var game = new Driver(Spell(Position(copied: true), "SFD·034/221"));
        await game.Act("P1", new PlayCardCommand("SPELL", "SFD·034/221", ["HOST"]));
        Assert.Equal(2, game.State.TriggerQueue.Count); await Order(game);
        Assert.Equal(2, game.State.StackItems.Count(Observer));
        Assert.Contains(game.State.StackItems.Where(Observer), i => i.FieldContext!.Origin is not null);
        await game.Top(); await game.Top(); await game.Top();
        Assert.Equal(8, game.State.CardObjects["HOST"].Power); await game.Replay();
    }

    [Fact]
    public async Task EchoChoosesTwiceButReadiesAnExhaustedUnitOnlyOnce()
    {
        var game = new Driver(Spell(Position(), "UNL-009/219"));
        await game.Act("P1", new PlayCardCommand("SPELL", "UNL-009/219", ["HOST"], OptionalCosts: ["ECHO"],
            RepeatChoices: [new("", ["HOST"])]));
        Assert.Equal(2, game.State.TriggerQueue.Count); await Order(game);
        await game.Top(); await game.Top(); Assert.Equal(6, game.State.CardObjects["HOST"].Power);
        await game.Top(); Assert.True(Observer(Assert.Single(game.State.StackItems)));
        await game.Top(); Assert.Equal(7, game.State.CardObjects["HOST"].Power); await game.Replay();
    }

    [Fact]
    public async Task TurnWakeObserverResolvesBeforeChannelAndNormalDraw()
    {
        var state = Position() with { Phase = MatchPhases.TurnStart, TurnStartStep = null, TimingState = TimingStates.NeutralClosed };
        var game = new Driver(state); await game.Act("P1", new PassPriorityCommand());
        Assert.Equal("START", game.State.TurnStartStep); Assert.True(Observer(Assert.Single(game.State.StackItems)));
        Assert.Empty(game.State.PlayerZones["P1"].Hand); Assert.Equal(3, game.State.PlayerZones["P1"].RuneDeck.Count);
        await game.Top(); Assert.Equal(5, game.State.CardObjects["HOST"].Power);
        Assert.Equal(MatchPhases.Main, game.State.Phase); Assert.Single(game.State.PlayerZones["P1"].Hand);
        await game.Replay();
    }

    [Fact]
    public async Task ReturnedSourceDoesNotReceiveTheOldSkillOrExposeItsNewHandIdentity()
    {
        var state = Spell(Spell(Position(), "SFD·034/221"), "UNL-128/219", "P2", "RETURN");
        state = OfficialHoldSequenceTests.AddUnit(state, "BOUNCE-MATE", "OGN·001/298", "F", "P2");
        state = LocalDestructionRecallTests.At(state, "BOUNCE-MATE", "P2", "BASE");
        var game = new Driver(state);
        await game.Act("P1", new PlayCardCommand("SPELL", "SFD·034/221", ["HOST"]));
        await game.Act("P1", new PassPriorityCommand());
        await game.Act("P2", new PlayCardCommand("RETURN", "UNL-128/219", ["BOUNCE-MATE", "HOST"])); await game.Top();
        Assert.Contains("HOST", game.State.PlayerZones["P1"].Hand);
        Assert.DoesNotContain("HOST", JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(game.State)["P2"]));
        var count = game.Events().Length; await game.Top();
        Assert.DoesNotContain(game.Events().Skip(count), e => e.Event.Kind == "TRIGGER_RESOLVED"
            && Equals(e.Event.Payload.GetValueOrDefault("sourceObjectId"), "HOST"));
        await game.Top(); Assert.Equal(4, game.State.CardObjects["HOST"].Power); await game.Replay();
    }

    [Theory]
    [InlineData("actor")] [InlineData("instance")] [InlineData("tick")] [InlineData("face")] [InlineData("target")]
    public async Task RecoveryRejectsAlteredObserverBindings(string change)
    {
        var game = new Driver(Spell(Position(), "SFD·034/221"));
        await game.Act("P1", new PlayCardCommand("SPELL", "SFD·034/221", ["HOST"]));
        var item = game.State.StackItems.Single(Observer); var context = item.FieldContext!; var birth = context.SelfAction!;
        item = item with { TargetObjectIds = change == "target" ? ["HOST"] : [], FieldContext = context with {
            SelfAction = birth with { Actor = change == "actor" ? "P2" : birth.Actor,
                InstanceId = change == "instance" ? "FORGED" : birth.InstanceId,
                CapturedTick = change == "tick" ? game.State.Tick + 1 : birth.CapturedTick,
                FaceRevision = change == "face" ? 99 : birth.FaceRevision } } };
        var altered = game.State with { StackItems = game.State.StackItems.Select(i => Observer(i) ? item : i).ToArray() };
        Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(altered));
    }

    [Fact]
    public async Task ChinaStandardDeckAcceptsTheOfficialVenHeroPrinting()
    {
        const OfficialDeckFormat format = OfficialDeckFormat.ChinaStandard20260724;
        var catalog = await OfficialCardCatalog.LoadRuntimeAsync();
        var allowed = catalog with { Cards = catalog.Cards.Where(c => ChinaStandardBanList.IsAllowed(c, format)).ToArray() };
        string Rune(string color) => allowed.Cards.Where(c => c.CardCategory == "rune" && c.CardColorList.Contains(color))
            .OrderBy(c => c.CardNo, StringComparer.Ordinal).First().CardNo;
        var builder = typeof(PreconstructedDeckCatalog).GetMethod("BuildLowCurveDeck", BindingFlags.NonPublic | BindingFlags.Static)!;
        var deck = (OfficialDecklist)builder.Invoke(null, [allowed, allowed.Cards.ToDictionary(c => c.CardNo, StringComparer.Ordinal),
            "SFD·195/221", "VEN·174", Array.Empty<string>(), new[] { "OGN·277/298", "OGN·278/298", "OGN·294/298" },
            Enumerable.Repeat(Rune("green"), 6).Concat(Enumerable.Repeat(Rune("purple"), 6)).ToArray()])!;
        var validation = OfficialDeckValidator.Validate(deck, catalog, format);
        Assert.True(validation.IsValid, string.Join("; ", validation.Errors));
    }
}
