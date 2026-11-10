using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class RadChronobreakBoundaryTests
{
    private const string Card = "RAD·140/167";
    private const string Unit = "OGN·219/298";
    private static MatchState Position() => VenUnitEntryBasicTests.Position(Card);
    private static PlayCardCommand Cast(int amount) => new("UNIT", Card, [], OptionalCosts: ["ADDITIONAL_POWER:" + amount]);
    private static async Task Choose(Driver g, string id)
    {
        var p = g.State.PendingRuleChoice!;
        await g.Act(p.Request.PlayerId, new PayCostCommand(p.Request.Id, "RULE_REPLACEMENT", [id]));
    }

    [Fact]
    public async Task ARealResponseCanRemoveTheOnlyUnitWithoutRefundingTheDeclaredFee()
    {
        var initial = Position();
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["ALLY"] = OfficialGarenConquestBattlefieldCountTests.Printed("ALLY", "OGN·096/298", "P1") } };
        initial = AttackDamageDistributionTests.Spell(
            LocalDestructionRecallTests.At(initial, "ALLY", "P1", "BATTLEFIELD", "BF"), "RETURN", "OGN·169/298", "P2");
        var g = new Driver(initial);
        await g.Act("P1", Cast(1)); await g.Act("P1", new PassPriorityCommand());
        await g.Act("P2", new PlayCardCommand("RETURN", "OGN·169/298", ["ALLY"]));
        await g.Top(); await g.Top();
        Assert.Contains("ALLY", g.State.PlayerZones["P1"].Hand);
        Assert.Equal(19, g.State.RunePools["P1"].TotalPower);
        Assert.Null(g.State.PendingEffectPlay); Assert.Null(g.State.PendingRuleChoice);
        Assert.DoesNotContain(g.Events(), e => e.Event.Kind == "UNIT_BANISHED");
    }

    [Fact]
    public async Task ARealEquipmentCopyEndsBeforeThePhysicalUnitIsQueuedAndPlayedAgain()
    {
        var initial = AttackDamageDistributionTests.Spell(ShadySpectaclesTests.Position(Unit), "CHRONO", Card, "P1");
        var g = new Driver(initial);
        await g.Act("P1", new AssembleEquipmentCommand("GEAR", "HOST", ["ASSEMBLE_YELLOW"]));
        await g.Top(); await Choose(g, "COPY:COPY-SOURCE");
        Assert.Equal(Unit, g.State.CardObjects["HOST"].CardNo);
        Assert.NotNull(g.State.CardObjects["HOST"].AttachmentUnitCopies);
        await g.Act("P1", new PlayCardCommand("CHRONO", Card, [], OptionalCosts: ["ADDITIONAL_POWER:1"]));
        await g.Top(); await Choose(g, "HOST");
        var c = g.State.PendingEffectPlay!.Parent.DeckPlaySequence!;
        Assert.Equal(Unit, Assert.Single(c.OwnedReplay!.Units).CardNo);
        Assert.Equal("OGN·096/298", Assert.Single(c.Queue).CardNo);
        Assert.Null(g.State.CardObjects["GEAR"].AttachedToObjectId);
        Assert.Null(g.State.CardObjects["HOST"].AttachmentUnitCopies);
        await g.Act("P1", new PlayCardCommand("HOST", "OGN·096/298", []));
        Assert.Equal("OGN·096/298", g.State.CardObjects["HOST"].CardNo);
        Assert.Contains("HOST", g.State.PlayerZones["P1"].Base);
    }

    [Theory]
    [InlineData("SFD·016/221", "BASE")]
    [InlineData("RAD·002/167", "BATTLEFIELD:BF")]
    public async Task ARealAnimatedEquipmentIsReplayedAsItsPrintedEquipmentWithoutKeepingTheUnitTransformation(
        string equipmentNo, string destination)
    {
        var initial = Position();
        initial = initial with {
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["GEAR"] = OfficialGarenConquestBattlefieldCountTests.Printed("GEAR", equipmentNo, "P1"),
                ["LEGEND"] = OfficialGarenConquestBattlefieldCountTests.Printed("LEGEND", "RAD·145/167", "P1") },
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
                ["P1"] = initial.PlayerZones["P1"] with { LegendZone = ["LEGEND"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) {
                ["LEGEND"] = new("P1", "LEGEND") } };
        initial = RadUnitEffectTestFixture.Place(initial, "GEAR", "P1", "HAND");
        initial = AnimatedArmamentAttachmentTests.WithMainPhaseResources(initial);
        if (destination != "BASE") initial = LocalDestructionRecallTests.At(initial, "ALLY", "P1", "BATTLEFIELD", "BF");
        var g = new Driver(initial);
        await g.Act("P1", new PlayCardCommand("GEAR", equipmentNo, [], Destination: destination));
        await AnimatedArmamentAttachmentTests.TransformAtNextOwnMain(g, "GEAR");
        Assert.Contains(CardObjectTags.UnitCard, g.State.CardObjects["GEAR"].Tags);
        Assert.NotNull(g.State.CardObjects["GEAR"].LegendEquipmentUnit);
        await g.Act("P1", Cast(2)); await g.Top(); await Choose(g, "GEAR");
        Assert.Equal(equipmentNo, g.State.PendingEffectPlay!.Parent.DeckPlaySequence!.Queue[0].CardNo);
        Assert.DoesNotContain(CardObjectTags.UnitCard, g.State.CardObjects["GEAR"].Tags);
        Assert.Null(g.State.CardObjects["GEAR"].LegendEquipmentUnit);
        var resources = g.State.RunePools["P1"];
        var outside = new PlayCardCommand("GEAR", equipmentNo, [], Destination: "BATTLEFIELD:OTHER");
        Assert.False(new CoreRuleEngine().PreviewPlayCard(g.State, "P1", PlayCostPreviewTests.Request(g.State, outside)).CanPay);
        var rejected = await new CoreRuleEngine().ResolveAsync(g.State, new("outside-original", "P1", CommandTypes.PlayCard), outside, default);
        Assert.False(rejected.Accepted); Assert.Equal(MatchStateHasher.Hash(g.State), MatchStateHasher.Hash(rejected.State));
        var play = new PlayCardCommand("GEAR", equipmentNo, [], Destination: destination);
        var quote = new CoreRuleEngine().PreviewPlayCard(g.State, "P1", PlayCostPreviewTests.Request(g.State, play));
        Assert.True(quote.CanPay, quote.Message);
        await g.Act("P1", play);
        Assert.Equal(resources, g.State.RunePools["P1"]);
        Assert.Equal(destination == "BASE" ? "BASE" : "BATTLEFIELD", g.State.ObjectLocations["GEAR"].Zone);
        Assert.DoesNotContain(CardObjectTags.UnitCard, g.State.CardObjects["GEAR"].Tags);
        Assert.Null(g.State.CardObjects["GEAR"].LegendEquipmentUnit);
        await g.Act("P1", new PlayCardCommand("ALLY", Unit, []));
        Assert.Null(g.State.PendingEffectPlay);
    }

    [Theory]
    [InlineData("SFD·016/221", "BASE")]
    [InlineData("RAD·002/167", "BATTLEFIELD:BF")]
    public async Task BanishmentOfAnAnimatedEquipmentHostLeavesItsRealArmamentOnTheField(
        string equipmentNo, string destination)
    {
        const string armamentNo = "SFD·022/221";
        var initial = Position();
        initial = initial with {
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["GEAR"] = OfficialGarenConquestBattlefieldCountTests.Printed("GEAR", equipmentNo, "P1"),
                ["LEGEND"] = OfficialGarenConquestBattlefieldCountTests.Printed("LEGEND", "RAD·145/167", "P1") },
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
                ["P1"] = initial.PlayerZones["P1"] with { LegendZone = ["LEGEND"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) {
                ["LEGEND"] = new("P1", "LEGEND") } };
        initial = RadUnitEffectTestFixture.Place(initial, "GEAR", "P1", "HAND");
        initial = AnimatedArmamentAttachmentTests.WithMainPhaseResources(initial);
        initial = LastBreathDrawBoundaryTests.Add(initial, "ARMAMENT", armamentNo, "P1", "HAND");
        if (destination != "BASE") initial = LocalDestructionRecallTests.At(initial, "ALLY", "P1", "BATTLEFIELD", "BF");
        var g = new Driver(initial);
        await g.Act("P1", new PlayCardCommand("GEAR", equipmentNo, [], Destination: destination));
        await AnimatedArmamentAttachmentTests.TransformAtNextOwnMain(g, "GEAR");
        await g.Act("P1", new PlayCardCommand("ARMAMENT", armamentNo, ["GEAR"], Destination: "BASE"));
        await g.Top();
        Assert.Equal("GEAR", g.State.CardObjects["ARMAMENT"].AttachedToObjectId);
        Assert.Contains(CardObjectTags.UnitCard, g.State.CardObjects["GEAR"].Tags);
        var at = g.State.ObjectLocations["ARMAMENT"];
        Assert.Equal(destination == "BASE" ? new("P1", "BASE") : new ObjectLocationState("P1", "BATTLEFIELD", "BF"), at);
        var generation = g.State.CardObjects["ARMAMENT"].ObjectGeneration;
        await g.Act("P1", Cast(1)); await g.Top(); await Choose(g, "GEAR");
        Assert.Equal("GEAR", Assert.Single(g.State.PendingEffectPlay!.Parent.DeckPlaySequence!.Queue).Card.ObjectId);
        Assert.Null(g.State.CardObjects["ARMAMENT"].AttachedToObjectId);
        Assert.Equal(new("P1", "BASE"), g.State.ObjectLocations["ARMAMENT"]);
        Assert.Equal(generation, g.State.CardObjects["ARMAMENT"].ObjectGeneration);
        Assert.Equal(destination == "BASE" ? 0 : 1, g.Events().Count(e => e.Event.Kind == "EQUIPMENT_RECALLED_TO_BASE"
            && e.Event.Payload.GetValueOrDefault("equipmentObjectId") as string == "ARMAMENT"));
        Assert.DoesNotContain(CardObjectTags.UnitCard, g.State.CardObjects["GEAR"].Tags);
        var banished = Assert.Single(g.Events(), e => e.Event.Kind == "UNIT_BANISHED").Event;
        Assert.Equal("GEAR", banished.Payload["targetObjectId"]);
        var detached = Assert.IsType<string[]>(JsonSerializer.SerializeToElement(banished.Payload["detachedEquipmentObjectIds"])
            .Deserialize<string[]>());
        Assert.Equal(["ARMAMENT"], detached);
        var resources = g.State.RunePools["P1"];
        await g.Act("P1", new PlayCardCommand("GEAR", equipmentNo, [], Destination: destination));
        Assert.Equal(resources, g.State.RunePools["P1"]);
        Assert.Null(g.State.PendingEffectPlay); Assert.Empty(g.State.StackItems);
        Assert.Null(g.State.CardObjects["ARMAMENT"].AttachedToObjectId);
        Assert.Equal(new("P1", "BASE"), g.State.ObjectLocations["ARMAMENT"]);
        Assert.Equal(generation, g.State.CardObjects["ARMAMENT"].ObjectGeneration);
        Assert.Equal(equipmentNo, g.State.CardObjects["GEAR"].CardNo);
        Assert.Null(g.State.CardObjects["GEAR"].LegendEquipmentUnit);
        Assert.DoesNotContain(CardObjectTags.UnitCard, g.State.CardObjects["GEAR"].Tags);
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task AnAnimatedAgileArmamentCanReplayInEitherOrderAndConfirmsItsHostAfterEntry(bool equipmentFirst)
    {
        const string equipmentNo = "SFD·022/221";
        var initial = Position();
        initial = initial with {
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["GEAR"] = OfficialGarenConquestBattlefieldCountTests.Printed("GEAR", equipmentNo, "P1"),
                ["LEGEND"] = OfficialGarenConquestBattlefieldCountTests.Printed("LEGEND", "RAD·145/167", "P1") },
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
                ["P1"] = initial.PlayerZones["P1"] with { LegendZone = ["LEGEND"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) {
                ["LEGEND"] = new("P1", "LEGEND") } };
        initial = RadUnitEffectTestFixture.Place(initial, "GEAR", "P1", "HAND");
        initial = AnimatedArmamentAttachmentTests.WithMainPhaseResources(initial);
        var g = new Driver(initial);
        await g.Act("P1", new PlayCardCommand("GEAR", equipmentNo, ["ALLY"], Destination: "BASE"));
        await g.Top();
        Assert.Equal("ALLY", g.State.CardObjects["GEAR"].AttachedToObjectId);
        await AnimatedArmamentAttachmentTests.TransformAtNextOwnMain(g, "GEAR");
        Assert.Contains(CardObjectTags.UnitCard, g.State.CardObjects["GEAR"].Tags);
        Assert.Null(g.State.CardObjects["GEAR"].AttachedToObjectId);
        Assert.Equal("GEAR", Assert.Single(g.Events(), e => e.Event.Kind == "EQUIPMENT_DETACHED"
            && e.Event.Payload.GetValueOrDefault("reason") as string == "BECAME_UNIT").Event.Payload["equipmentObjectId"]);
        await g.Act("P1", Cast(2)); await g.Top(); await Choose(g, equipmentFirst ? "GEAR" : "ALLY");
        var resources = g.State.RunePools["P1"];
        if (!equipmentFirst) await g.Act("P1", new PlayCardCommand("ALLY", Unit, []));
        Assert.Equal("GEAR", Assert.Single(g.State.PendingEffectPlay!.Sources).Key);
        Assert.DoesNotContain(CardObjectTags.UnitCard, g.State.CardObjects["GEAR"].Tags);
        Assert.Null(g.State.CardObjects["GEAR"].LegendEquipmentUnit);
        var pending = g.State.PendingEffectPlay!;
        var finish = new ChooseCardsCommand(pending.ChoiceId, "EFFECT_PLAY", []);
        var prompts = ResolutionResult.BuildPrompts(g.State)["P1"];
        // CN355.5.b / 383.4.a.2 / 819.1.d: no host does not prevent
        // the mandatory equipment replay. Only its independent Agile skill
        // is discarded when it cannot confirm a legal attachment target.
        Assert.DoesNotContain(prompts.Candidates!, c => c.Action == CommandTypes.ChooseCards && c.Enabled);
        var play = new PlayCardCommand("GEAR", equipmentNo, [], Destination: "BASE");
        Assert.True(new CoreRuleEngine().PreviewPlayCard(g.State, "P1", PlayCostPreviewTests.Request(g.State, play)).CanPay);
        var before = MatchStateHasher.Hash(g.State);
        var rejected = await new CoreRuleEngine().ResolveAsync(g.State, new("cannot-skip-free-armament", "P1", finish.CmdType), finish, default);
        Assert.False(rejected.Accepted); Assert.Empty(rejected.Events);
        Assert.Equal(before, MatchStateHasher.Hash(rejected.State));
        await g.Act("P1", play);
        Assert.Contains("GEAR", g.State.PlayerZones["P1"].Base);
        Assert.Null(g.State.CardObjects["GEAR"].AttachedToObjectId);
        if (equipmentFirst)
        {
            Assert.Equal("ALLY", Assert.Single(g.State.PendingEffectPlay!.Sources).Key);
            Assert.Null(g.State.PendingCardChoice);
            Assert.Contains(g.Events(), e => e.Event.Kind == "TRIGGER_NOT_CONFIRMED");
            await g.Act("P1", new PlayCardCommand("ALLY", Unit, []));
            Assert.Null(g.State.CardObjects["GEAR"].AttachedToObjectId);
        }
        else
        {
            var host = Assert.IsType<PendingCardChoiceState>(g.State.PendingCardChoice);
            Assert.Equal("TRIGGER_CONFIRMATION", host.ChoiceWindow);
            await g.Act("P1", new ChooseCardsCommand(host.ChoiceId, host.ChoiceWindow, ["ALLY"]));
            Assert.Null(g.State.PendingEffectPlay);
            await g.Top();
            Assert.Equal("ALLY", g.State.CardObjects["GEAR"].AttachedToObjectId);
        }
        Assert.DoesNotContain(g.Events(), e => e.Event.Kind == "EFFECT_PLAY_UNABLE");
        Assert.Contains("ALLY", g.State.PlayerZones["P1"].Base);
        Assert.Equal(resources, g.State.RunePools["P1"]);
        Assert.Null(g.State.PendingEffectPlay); Assert.Empty(g.State.StackItems);
    }

    [Theory]
    [InlineData("BASE")] [InlineData("BATTLEFIELD:BF")]
    public async Task IgnoreAllCostsKeepsTheChosenPeakAndHasteBenefitsWithNoResourcesRemaining(string destination)
    {
        var initial = Position();
        initial = initial with {
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["ALLY"] = OfficialGarenConquestBattlefieldCountTests.Printed("ALLY", "OGN·001/298", "P1"),
                ["BF"] = OfficialGarenConquestBattlefieldCountTests.Printed("BF", "VEN·157", "P1") with { ControllerId = null } },
            RunePools = new Dictionary<string, RunePool>(initial.RunePools) { ["P1"] = new(2, 1) } };
        var g = new Driver(initial); await g.Act("P1", Cast(1)); await g.Top();
        Assert.Equal(RunePool.Empty, g.State.RunePools["P1"]);
        var play = new PlayCardCommand("ALLY", "OGN·001/298", [], Destination: destination,
            OptionalCosts: ["BATTLEFIELD_UNIT_PLAY:BF", HasteOptionalCostNames.HasteReady]);
        var quote = new CoreRuleEngine().PreviewPlayCard(g.State, "P1", PlayCostPreviewTests.Request(g.State, play));
        Assert.True(quote.CanPay, quote.Message); Assert.Equal(0, quote.Cost!.Mana);
        Assert.Equal(0, quote.Cost.GenericPower); Assert.Empty(quote.Cost.PowerByTrait);
        await g.Act("P1", play);
        Assert.Equal(RunePool.Empty, g.State.RunePools["P1"]); Assert.False(g.State.CardObjects["ALLY"].IsExhausted);
        Assert.Equal(destination == "BASE" ? "BASE" : "BATTLEFIELD", g.State.ObjectLocations["ALLY"].Zone);
    }

    [Fact]
    public async Task AnUnplayableRestrictedUnitStaysBanishedEvenThoughItsCostsAreIgnored()
    {
        var initial = Position();
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["ALLY"] = OfficialGarenConquestBattlefieldCountTests.Printed("ALLY", "SFD·015/221", "P1") } };
        initial = LocalDestructionRecallTests.At(initial, "ALLY", "P1", "BATTLEFIELD", "BF");
        var g = new Driver(initial); await g.Act("P1", Cast(1)); await g.Top();
        var p = g.State.PendingEffectPlay!;
        foreach (var destination in new[] { "BASE", "BATTLEFIELD:BF" })
        {
            var play = new PlayCardCommand("ALLY", "SFD·015/221", [], Destination: destination);
            Assert.False(new CoreRuleEngine().PreviewPlayCard(g.State, "P1", PlayCostPreviewTests.Request(g.State, play)).CanPay);
            var rejected = await new CoreRuleEngine().ResolveAsync(g.State, new("restricted-" + destination, "P1", CommandTypes.PlayCard), play, default);
            Assert.False(rejected.Accepted); Assert.Equal(MatchStateHasher.Hash(g.State), MatchStateHasher.Hash(rejected.State));
        }
        await g.Act("P1", new ChooseCardsCommand(p.ChoiceId, "EFFECT_PLAY", []));
        Assert.Contains("ALLY", g.State.PlayerZones["P1"].Banished); Assert.Null(g.State.PendingEffectPlay);
    }

    [Theory]
    [InlineData("P1", "ALLY")]
    [InlineData("P2", "ENEMY")]
    public async Task OneOwnersRestrictedCardCanStayBanishedWithoutBlockingTheOtherOwnersReplay(
        string restrictedOwner, string restrictedId)
    {
        const string restrictedCard = "SFD·015/221";
        var initial = LocalDestructionRecallTests.At(Position(), "ENEMY", "P1", "BASE");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            [restrictedId] = OfficialGarenConquestBattlefieldCountTests.Printed(restrictedId, restrictedCard, restrictedOwner)
                with { ControllerId = "P1" } } };
        var g = new Driver(initial); await g.Act("P1", Cast(2)); await g.Top();
        Assert.Equal(["ALLY", "ENEMY"], g.State.StackItems.Select(item => item.SourceObjectId));
        var p1Resources = g.State.RunePools["P1"]; var p2Resources = g.State.RunePools["P2"];
        foreach (var (owner, id) in new[] { ("P1", "ALLY"), ("P2", "ENEMY") })
        {
            var pending = Assert.IsType<PendingEffectPlayState>(g.State.PendingEffectPlay);
            Assert.Equal(owner, pending.PlayerId); Assert.Equal(id, Assert.Single(pending.Sources).Key);
            if (id == restrictedId)
            {
                var play = new PlayCardCommand(id, restrictedCard, []);
                Assert.False(new CoreRuleEngine().PreviewPlayCard(g.State, owner, PlayCostPreviewTests.Request(g.State, play)).CanPay);
                var generation = g.State.CardObjects[id].ObjectGeneration;
                await g.Act(owner, new ChooseCardsCommand(pending.ChoiceId, "EFFECT_PLAY", []));
                Assert.Contains(id, g.State.PlayerZones[owner].Banished);
                Assert.Equal(new ObjectLocationState(owner, "BANISHED"), g.State.ObjectLocations[id]);
                Assert.Equal(generation + 1, g.State.CardObjects[id].ObjectGeneration);
            }
            else
            {
                await g.Act(owner, new PlayCardCommand(id, Unit, []));
                Assert.Contains(id, g.State.PlayerZones[owner].Base);
                Assert.Equal(owner, g.State.CardObjects[id].ControllerId);
            }
            Assert.Equal(p1Resources, g.State.RunePools["P1"]);
            Assert.Equal(p2Resources, g.State.RunePools["P2"]);
        }
        Assert.Null(g.State.PendingEffectPlay); Assert.Empty(g.State.StackItems);
        Assert.Single(g.Events(), row => row.Event.Kind == "EFFECT_PLAY_UNABLE"
            && Equals(row.Event.Payload.GetValueOrDefault("targetObjectId"), restrictedId));
        Assert.DoesNotContain(g.Events(), row => row.Event.Kind == "CARD_PLAYED"
            && Equals(row.Event.Payload.GetValueOrDefault("sourceObjectId"), restrictedId));
        await g.Replay();
    }

    [Theory]
    [InlineData("units")] [InlineData("fact")] [InlineData("binding")]
    public async Task PersistedRecoveryRejectsMalformedReplayFactsWithoutThrowing(string mutation)
    {
        var g = new Driver(Position()); await g.Act("P1", Cast(1)); await g.Top();
        var p = g.State.PendingEffectPlay!; var c = p.Parent.DeckPlaySequence!; var r = c.OwnedReplay!;
        var bad = g.State with { PendingEffectPlay = p with { Parent = p.Parent with { DeckPlaySequence = c with {
            OwnedReplay = r with { Units = mutation == "units" ? null! : mutation == "fact" ? [null!] : [r.Units[0] with { Unit = null! }] } } } } };
        Assert.NotEmpty(RecoveryErrors(g, bad));
    }

    [Fact]
    public async Task AcceptedHistoryRejectsACoherentlyForgedFutureCardsOriginalBattlefield()
    {
        var initial = LastBreathDrawBoundaryTests.Add(Position(), "SECOND", Unit, "P1", "BASE");
        initial = LocalDestructionRecallTests.At(initial, "SECOND", "P1", "BATTLEFIELD", "BF");
        var g = new Driver(initial); await g.Act("P1", Cast(2)); await g.Top(); await Choose(g, "ALLY");
        var p = g.State.PendingEffectPlay!; var c = p.Parent.DeckPlaySequence!; var r = c.OwnedReplay!;
        var at = new ObjectLocationState("P1", "BATTLEFIELD", "OTHER");
        var field = new ObjectBinding("OTHER", g.State.CardObjects["OTHER"].ObjectGeneration);
        var frame = r.Frames[0] with { ObjectLocations = new Dictionary<string, ObjectLocationState>(r.Frames[0].ObjectLocations) { ["SECOND"] = at } };
        var changed = c with {
            Queue = c.Queue.Select(q => q.Card.ObjectId == "SECOND" ? q with { OriginalLocation = at, OriginalBattlefield = field } : q).ToArray(),
            OwnedReplay = r with { Frames = [frame], Units = r.Units.Select(f => f.Unit.ObjectId == "SECOND" ? f with { Location = at, Battlefield = field } : f).ToArray() } };
        var bad = g.State with { PendingEffectPlay = p with { Parent = p.Parent with { DeckPlaySequence = changed } } };
        Assert.Empty(OfficialInsightAndSpellLockTests.Errors(bad));
        Assert.Contains(RecoveryErrors(g, bad), e => e == "owned replay fact differs from accepted banishment");
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, g.Commands(), bad, new CoreRuleEngine(), default, g.Events());
        Assert.False(replay.IsMatch);
    }

    private static IReadOnlyList<string> RecoveryErrors(Driver g, MatchState state)
    {
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var events = JsonSerializer.Deserialize<RecoveredEvent[]>(JsonSerializer.Serialize(g.Events(), json), json)!;
        var sequence = events[^1].Sequence;
        var snapshots = ResolutionResult.BuildSnapshots(state); var prompts = ResolutionResult.BuildPrompts(state);
        var views = state.Seats.Keys.ToDictionary(p => p, p => new RecoveredPlayerView(p, state.Tick, sequence, snapshots[p], state.Tick, sequence, prompts[p]));
        var spectator = MatchReplayRedactor.BuildSpectatorFrame(state.RoomId, state.Tick, sequence, [], state);
        return MatchRecoveryValidator.Validate(state.RoomId, sequence, g.Commands(), events, views, state, state.Tick, spectator);
    }
}
