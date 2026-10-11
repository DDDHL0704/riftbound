using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.LocalDestructionRecallTests;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;

namespace Riftbound.ConformanceTests;

public sealed class AttachedRuleTextTests
{
    internal static MatchState Position(string unit = "OGN·109/298", int graveCount = 1)
    {
        var s = ArmamentMightTests.Position("SFD·059/221", unit,
            unit == "OGN·109/298" ? 6 : unit == "SFD·068/221" ? 3 : 4);
        Assert.True(CardBehaviorRegistry.TryGetByCardNo(unit, out var definition));
        var cards = s.CardObjects.ToDictionary(p => p.Key, p => p.Value);
        cards["HOST"] = cards["HOST"] with { Power = definition.SourceUnitPower, Tags = definition.SourceUnitTags.Split('|', StringSplitOptions.RemoveEmptyEntries)
            .Append(CardObjectTags.UnitCard).Distinct().Order(StringComparer.Ordinal).ToArray() };
        var locations = s.ObjectLocations.ToDictionary(p => p.Key, p => p.Value);
        var grave = Enumerable.Range(1, graveCount).Select(i => "G" + i).ToArray();
        foreach (var id in grave) {
            cards[id] = new(id, cardNo: "OGN·001/298", ownerId: "P1", controllerId: "P1", tags: [CardObjectTags.UnitCard]);
            locations[id] = new("P1", "GRAVEYARD");
        }
        return s with { CardObjects = cards, ObjectLocations = locations,
            PlayerZones = new Dictionary<string, PlayerZones>(s.PlayerZones) { ["P1"] = s.PlayerZones["P1"] with { Graveyard = grave } } };
    }

    internal static MatchState AddSong(MatchState s, string id)
        => At(s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
            [id] = new(id, cardNo: "SFD·059/221", ownerId: "P1", controllerId: "P1",
                tags: [CardObjectTags.EquipmentCard, CardEquipmentKeywordNames.Weapon]) } }, id, "P1", "BASE");

    internal static async Task<ResolutionResult> Attach(MatchState s, string id = "GEAR", MatchSession? session = null)
    {
        var command = new AssembleEquipmentCommand(id, "HOST", ["ASSEMBLE_GREEN"]);
        var first = session is null ? await Act(s, "P1", command)
            : await session.SubmitAsync("P1", "attach-" + s.Tick, command,
                System.Text.Json.JsonSerializer.SerializeToElement(command, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)), default);
        return await EquipTestDriver.FinishAsync(first, session);
    }

    [Theory]
    [InlineData(1, 8)]
    [InlineData(3, 10)]
    public async Task SongsCopyOnlyPrintedMundoTextAndGrowLinearly(int songs, int expected)
    {
        var s = Position();
        for (var i = 0; i < songs; i++) {
            var id = i == 0 ? "GEAR" : "SONG" + i;
            if (i > 0) s = AddSong(s, id);
            s = (await Attach(s, id)).State;
        }
        Assert.Equal(expected, ArmamentMightTests.Might(s));
        Assert.Equal(6, s.CardObjects["HOST"].Power);
        Restore(s); ArmamentMightTests.PublicRecovery(s);
    }

    [Fact]
    public async Task CopiedMechanicalFanTextDoesNotDoubleAnAlreadyDoubledBonus()
    {
        var s = AddSong(Position("SFD·068/221", 0), "SHIELD");
        s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
            ["SHIELD"] = s.CardObjects["SHIELD"] with { CardNo = "SFD·033/221" } } };
        s = (await Attach(s, "SHIELD")).State;
        s = (await Attach(s)).State;
        Assert.Equal(5, ArmamentMightTests.Might(s)); Restore(s); ArmamentMightTests.PublicRecovery(s);
    }

    [Fact]
    public async Task TwoSongsGiveOrnnThreeIndependentWardInstances()
    {
        var s = (await Attach(Position("SFD·085/221", 0))).State;
        s = (await Attach(AddSong(s, "SONG2"), "SONG2")).State;
        s = (await Act(s, "P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F", []))).State;
        s = (await Act(s, "P1", new PassFocusCommand())).State;
        Assert.Equal("P2", s.FocusPlayerId);
        s = s with {
            RunePools = new Dictionary<string, RunePool>(s.RunePools) { ["P2"] = new(20, 20) },
            PlayerZones = new Dictionary<string, PlayerZones>(s.PlayerZones) { ["P2"] = s.PlayerZones["P2"] with { Hand = ["CARD"] } },
            CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
                ["CARD"] = new("CARD", cardNo: "SFD·011/221", ownerId: "P2", controllerId: "P2", tags: [CardObjectTags.SpellCard]) },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(s.ObjectLocations) { ["CARD"] = new("P2", "HAND") } };
        var command = new PlayCardCommand("CARD", "SFD·011/221", ["HOST", "GEAR"]);
        var quote = new CoreRuleEngine().PreviewPlayCard(s, "P2", PlayCostPreviewTests.Request(s, command));
        Assert.True(quote.Cost is not null, quote.Message);
        Assert.Equal(6, quote.Cost!.GenericPower);
        Assert.Equal(2, quote.Cost.Mana); Restore(s); ArmamentMightTests.PublicRecovery(s);
    }

    [Fact]
    public async Task CopiedMundoBeginningTextQueuesSeparateMandatoryTriggers()
    {
        var s = (await Attach(Position(graveCount: 7))).State;
        s = s with { Phase = MatchPhases.TurnStart, TimingState = TimingStates.NeutralClosed, TurnStartStep = null };
        var opened = await Act(s, "P1", new PassPriorityCommand());
        Assert.Equal(2, opened.State.TriggerQueue.Count + opened.State.StackItems.Count);
        Assert.All(opened.State.TriggerQueue, i => Assert.NotNull(i.BeginningRecycle));
        Assert.All(opened.State.StackItems, i => Assert.NotNull(i.BeginningRecycle));
        Restore(opened.State); ArmamentMightTests.PublicRecovery(opened.State);
    }

    [Fact]
    public async Task ActualDetachRemovesCopiedTextAndReattachmentCreatesOneFreshInstance()
    {
        var s = (await Attach(Position())).State;
        var first = Assert.IsType<AttachedRuleTextState>(s.CardObjects["GEAR"].AttachedRuleText);
        s = WithSpell(s, "P1", "SFD·011/221");
        var spell = await Act(s, "P1", new PlayCardCommand("SPELL", "SFD·011/221", ["HOST", "GEAR"]));
        var detached = await Top(spell.State); s = detached.State;
        Assert.Null(s.CardObjects["GEAR"].AttachedToObjectId); Assert.Null(s.CardObjects["GEAR"].AttachedRuleText);
        Assert.Equal(first.Revision, s.CardObjects["GEAR"].AttachmentRevision);
        Assert.Equal(2, s.PlayerZones["P1"].Graveyard.Count);
        Assert.Equal(8, ArmamentMightTests.Might(s)); Restore(s); ArmamentMightTests.PublicRecovery(s);
        s = (await Attach(s)).State;
        var second = Assert.IsType<AttachedRuleTextState>(s.CardObjects["GEAR"].AttachedRuleText);
        Assert.Equal(first.Revision + 1, second.Revision);
        Assert.Equal(10, ArmamentMightTests.Might(s));
        Assert.Single(s.ContinuousEffects, e => e.EffectKind == "ATTACHED_RULE_TEXT_POWER");
        Restore(s); ArmamentMightTests.PublicRecovery(s);
    }

    internal static MatchState WithSpell(MatchState s, string player, string no)
        => s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
            ["SPELL"] = new("SPELL", cardNo: no, ownerId: player, controllerId: player, tags: [CardObjectTags.SpellCard]) },
            PlayerZones = new Dictionary<string, PlayerZones>(s.PlayerZones) { [player] = s.PlayerZones[player] with { Hand = [..s.PlayerZones[player].Hand, "SPELL"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(s.ObjectLocations) { ["SPELL"] = new(player, "HAND") } };

    [Fact]
    public async Task EachCopiedBeginningResolvesAgainstTheRemainingGraveyardWithItsOwnResponseWindow()
    {
        var s = (await Attach(Position(graveCount: 7))).State;
        var cursor = s.RngCursor;
        s = s with { Phase = MatchPhases.TurnStart, TimingState = TimingStates.NeutralClosed, TurnStartStep = null };
        var opened = await Act(s, "P1", new PassPriorityCommand());
        Assert.Equal(2, opened.State.TriggerQueue.Count);
        var ordered = await Act(opened.State, "P1", new OrderTriggersCommand(opened.State.TriggerQueue.Select(i => i.TriggerId).ToArray()));
        Assert.Equal(2, ordered.State.StackItems.Count); Assert.Single(ordered.State.StackItems, i => i.BeginningRecycle!.Origin is not null);
        Restore(ordered.State); ArmamentMightTests.PublicRecovery(ordered.State);
        var first = await Top(ordered.State);
        var done = await MundoBeginningTests.Choice(first.State, "G1", "G2", "G3");
        Assert.Single(done.State.StackItems); Assert.Null(done.State.PendingCardChoice); Assert.NotNull(done.State.PriorityPlayerId);
        Assert.Equal(14, ArmamentMightTests.Might(done.State)); Restore(done.State); ArmamentMightTests.PublicRecovery(done.State);
        var second = await Top(done.State);
        done = await MundoBeginningTests.Choice(second.State, "G4", "G5", "G6");
        Assert.Empty(done.State.StackItems); Assert.Equal(MatchPhases.Main, done.State.Phase);
        Assert.Equal(["G7"], done.State.PlayerZones["P1"].Graveyard);
        Assert.Equal(cursor + 2, done.State.RngCursor); Assert.Equal(8, ArmamentMightTests.Might(done.State));
        Restore(done.State); ArmamentMightTests.PublicRecovery(done.State);
    }

    [Theory]
    [InlineData("missing")] [InlineData("face")] [InlineData("unit")] [InlineData("generation")]
    [InlineData("revision")] [InlineData("epoch")] [InlineData("gear")]
    public async Task CurrentRecoveryRejectsForgedAttachedPrintedText(string change)
    {
        var s = (await Attach(Position())).State;
        var gear = s.CardObjects["GEAR"]; var copy = gear.AttachedRuleText!;
        var forged = change switch {
            "missing" => gear with { AttachedRuleText = null },
            "face" => gear with { AttachedRuleText = copy with { CardNo = "OGN·001/298" } },
            "unit" => gear with { AttachedRuleText = copy with { Unit = copy.Unit with { ObjectId = "OTHER" } } },
            "generation" => gear with { AttachedRuleText = copy with { Unit = copy.Unit with { Generation = copy.Unit.Generation + 1 } } },
            "revision" => gear with { AttachedRuleText = copy with { Revision = copy.Revision + 1 } },
            "epoch" => gear with { AttachmentRevision = -1 },
            _ => gear with { CardNo = "SFD·022/221" }
        };
        Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) { ["GEAR"] = forged } }));
        Restore(s);
    }

    [Theory]
    [InlineData("powerDelta")] [InlineData("condition")] [InlineData("sourcePath")] [InlineData("sourceDependencyObjectIds")]
    public async Task AllPublicViewsRejectForgedCopiedRuleContributions(string key)
    {
        var s = (await Attach(Position())).State;
        var snapshots = ResolutionResult.BuildSnapshots(s); var prompts = ResolutionResult.BuildPrompts(s);
        var views = s.Seats.Keys.ToDictionary(id => id, id => new RecoveredPlayerView(id, s.Tick, 0, snapshots[id], s.Tick, 0, prompts[id]));
        var spectator = MatchReplayRedactor.BuildSpectatorFrame(s.RoomId, s.Tick, 0, [], s);
        foreach (var snapshot in snapshots.Values.Append(spectator.SpectatorSnapshot)) {
            var effects = Assert.IsAssignableFrom<IEnumerable<Dictionary<string, object?>>>(snapshot.Timing["continuousEffects"]);
            var effect = effects.Single(e => Equals(e.GetValueOrDefault("effectKind"), "ATTACHED_RULE_TEXT_POWER"));
            var old = effect[key]; effect[key] = key == "powerDelta" ? 99 : key == "sourceDependencyObjectIds" ? new[] { "HOST" } : "FORGED";
            Assert.NotEmpty(MatchRecoveryValidator.Validate(s.RoomId, 0, [], [], views, s, s.Tick, spectator)); effect[key] = old;
        }
        Restore(s); ArmamentMightTests.PublicRecovery(s);
    }
}
