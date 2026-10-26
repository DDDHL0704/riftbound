using Riftbound.Contracts;
using Riftbound.Engine;
using System.Text.Json;
using Xunit;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;

namespace Riftbound.ConformanceTests;

public sealed class MaskedAttendantTests
{
    internal const string Card = "UNL-081/219";
    internal static MatchState Position()
    {
        var state = OfficialCopyIdentityTests.AddSpell(OfficialCopyIdentityTests.Position(), "MASK", Card);
        return state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["MASK"] = new("MASK", cardNo: Card, power: 1, manaCost: 2, ownerId: "P1", controllerId: "P1", tags: [CardObjectTags.UnitCard]) } };
    }
    internal static CardObjectState[] Images(MatchState state) => state.CardObjects.Values
        .Where(c => c.TokenFactoryCardNo == P6TokenFactoryCatalog.ImageTokenCardNo).OrderBy(c => c.ObjectId).ToArray();
    internal static async Task<ResolutionResult> Played() => await Act(Position(), "P1", new PlayCardCommand("MASK", Card, []));

    [Fact]
    public async Task RuntimeCardTextUsesTheApplicableAprilErratum()
    {
        var catalog = await Riftbound.CardCatalog.OfficialCardCatalog.LoadDefaultAsync();
        var card = Assert.Single(catalog.Cards, c => c.Id == 34615 && c.CardNo == Card);
        Assert.Contains("然后进行一次：它们变为我的复制体。", card.CardEffect);
        Assert.Contains("下个开始阶段", card.CardEffect);
    }

    [Fact]
    public async Task NormalPlaySkillCreatesTwoImagesWhichImmediatelyCopyItsPrintedCharacteristics()
    {
        var played = await Played();
        Assert.Empty(Images(played.State)); CheckedRestore(played.State);
        var entered = await Top(played.State);
        Assert.Equal(2, Images(entered.State).Length);
        Assert.All(Images(entered.State), image => {
            Assert.Equal(P6TokenFactoryCatalog.ImageTokenCardNo, image.CardNo); Assert.Equal(0, image.Power);
            Assert.True(image.IsExhausted); Assert.DoesNotContain(CardObjectTags.Ephemeral, image.Tags);
            Assert.DoesNotContain("待命", image.Tags);
        });
        Assert.Equal(2, entered.Events.Count(e => e.Kind == "UNIT_TOKEN_CREATED"));
        var reflexive = Assert.Single(entered.State.StackItems).ReflexiveCopy!;
        Assert.True(reflexive.CopySourceIsSelf); Assert.Equal("MASK", reflexive.CopySource!.ObjectId);
        Assert.Empty(entered.State.StackItems.Single().TargetObjectIds); CheckedRestore(entered.State);
        // April CN erratum: copying follows a separate response window. The
        // original copy-characteristic assertions are retained at that point.
        var first = await Act(entered.State, entered.State.PriorityPlayerId!, new PassPriorityCommand());
        Assert.All(Images(first.State), image => Assert.Equal(0, image.Power));
        var done = await Act(first.State, first.State.PriorityPlayerId!, new PassPriorityCommand());
        var copies = Images(done.State);
        Assert.Equal(2,copies.Length);
        foreach(var copy in copies)
        {
            Assert.Equal("UNL-081/219",copy.CardNo);Assert.Equal(1,copy.Power);Assert.Equal(2,copy.ManaCost);
            Assert.Equal("P1",copy.ControllerId);Assert.True(copy.IsExhausted);
            Assert.Contains(copy.ObjectId,done.State.PlayerZones["P1"].Base);
            Assert.Contains(CardObjectTags.Ephemeral,copy.Tags);Assert.Contains("待命",copy.Tags);
        }
        Assert.Empty(done.State.StackItems);Assert.Empty(done.State.TriggerQueue);
        CheckedRestore(done.State);
    }

    [Theory]
    [InlineData("before-entry")]
    [InlineData("before-copy")]
    public async Task ActualResponseReturningSourceToHandCannotUseItsPreviousObject(string stage)
    {
        var result = await Played();
        if (stage == "before-copy") result = await Top(result.State);
        var s = AttackDamageDistributionTests.Spell(result.State, "BOUNCE", "OGN·104/298", "P1");
        result = await Act(s, "P1", new PlayCardCommand("BOUNCE", "OGN·104/298", ["MASK"]));
        result = await Top(result.State); Assert.Contains("MASK", result.State.PlayerZones["P1"].Hand); CheckedRestore(result.State);
        result = await Top(result.State);
        Assert.Equal(stage == "before-entry" ? 0 : 2, Images(result.State).Length);
        Assert.All(Images(result.State), image => {
            Assert.Equal(P6TokenFactoryCatalog.ImageTokenCardNo, image.CardNo); Assert.Equal(0, image.Power);
            Assert.DoesNotContain(CardObjectTags.Ephemeral, image.Tags);
        });
        CheckedRestore(result.State);
    }

    [Theory]
    [InlineData("source-before-entry")]
    [InlineData("source-before-copy")]
    [InlineData("recipient-before-copy")]
    public async Task ANewGenerationCannotReceiveThePreviousObjectsInstruction(string branch)
    {
        var result = await Played();
        if (branch != "source-before-entry") result = await Top(result.State);
        var id = branch == "recipient-before-copy" ? Images(result.State)[0].ObjectId : "MASK";
        var s = result.State with { CardObjects = new Dictionary<string, CardObjectState>(result.State.CardObjects) {
            [id] = result.State.CardObjects[id] with { ObjectGeneration = 2 } } };
        result = await Top(s);
        if (branch == "source-before-entry") Assert.Empty(Images(result.State));
        else if (branch == "source-before-copy") Assert.All(Images(result.State), image => {
            Assert.Equal(0, image.Power); Assert.DoesNotContain(CardObjectTags.Ephemeral, image.Tags); });
        else { Assert.Equal(0, result.State.CardObjects[id].Power); Assert.Single(Images(result.State), c => c.CardNo == Card); }
        CheckedRestore(result.State);
    }

    [Fact]
    public async Task CopiesUsePrintedFaceAndPreserveImagesExistingModifications()
    {
        var result = await Top((await Played()).State); var s = result.State; var id = Images(s)[0].ObjectId;
        s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
            ["MASK"] = s.CardObjects["MASK"] with { Power = 13, Damage = 1, UntilEndOfTurnPowerModifier = 4,
                GrantedTags = ["游走"], Tags = new[] { CardObjectTags.UnitCard, "游走", "增益" }.Order(StringComparer.Ordinal).ToArray() },
            [id] = s.CardObjects[id] with { Power = 2, UntilEndOfTurnPowerModifier = 2, IsExhausted = false,
                GrantedTags = ["后排"], Tags = [CardObjectTags.UnitCard, "后排"] } } };
        result = await Top(s); var image = result.State.CardObjects[id];
        Assert.Equal(3, image.Power); Assert.Equal(2, image.UntilEndOfTurnPowerModifier); Assert.False(image.IsExhausted);
        Assert.Contains("后排", image.Tags); Assert.DoesNotContain("游走", image.Tags); Assert.DoesNotContain("增益", image.Tags);
        Assert.Equal(0, image.Damage); Assert.Equal(2, image.ManaCost); Assert.Contains(CardObjectTags.Ephemeral, image.Tags);
        Assert.Equal(1, Images(result.State).Single(c => c.ObjectId != id).Power); CheckedRestore(result.State);
    }

    [Fact]
    public async Task SelfReferenceIsNotSubjectToTargetProtectionOrWardPayment()
    {
        var result = await Top((await Played()).State); var s = result.State;
        s = LocalDestructionRecallTests.At(s, "MASK", "P2", "BASE");
        s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
            ["MASK"] = s.CardObjects["MASK"] with { CardNo = "SFD·105/221", Power = 5,
                Tags = [CardObjectTags.UnitCard], CopyFaceTransitions = [new(s.Tick, s.CardObjects["MASK"].ObjectGeneration, Card, "SFD·105/221")] } },
            RunePools = new Dictionary<string, RunePool> { ["P1"] = new(0,0), ["P2"] = new(0,0) } };
        var rejectionState = OfficialCopyIdentityTests.AddSpell(s,"ILLEGAL","OGN·229/298");
        var illegal = await new CoreRuleEngine().ResolveAsync(rejectionState,new("protected","P1",CommandTypes.PlayCard),
            new PlayCardCommand("ILLEGAL","OGN·229/298",["MASK"]),default);
        Assert.False(illegal.Accepted); Assert.Equal(MatchStateHasher.Hash(rejectionState),MatchStateHasher.Hash(illegal.State));
        result = await Top(s); Assert.All(Images(result.State), image => {
            Assert.Equal("SFD·105/221", image.CardNo); Assert.DoesNotContain(CardObjectTags.Ephemeral,image.Tags);
        });
        Assert.Null(result.State.PendingPayment); CheckedRestore(result.State);
    }

    [Fact]
    public async Task OptionalReplacementAddsAnImageBeforeTheIndependentCopyTrigger()
    {
        var s = OfficialTokenReplacementTests.Position(card: Card);
        var result = await Act(s, "P1", new PlayCardCommand("C", Card, []));
        result = await Top(result.State); Assert.Empty(Images(result.State)); CheckedRestore(result.State);
        result = await OfficialTokenReplacementTests.Choose(result.State, "Z1");
        Assert.Equal(3, Images(result.State).Length); Assert.All(Images(result.State), image => Assert.Equal(0, image.Power));
        Assert.Equal(3, Assert.Single(result.State.StackItems).ReflexiveCopy!.Recipients.Count); CheckedRestore(result.State);
        result = await Top(result.State); Assert.All(Images(result.State), image => Assert.Equal(Card, image.CardNo)); CheckedRestore(result.State);
    }

    [Fact]
    public async Task IndependentRecoveryChecksRejectMissingAndForgedSourceBindings()
    {
        var s = (await Played()).State; CheckedRestore(s); var item = Assert.Single(s.StackItems);
        foreach (var binding in new ObjectBinding?[] { null, new("TARGET",0), new("MASK",-1), new("MASK",99) })
            Assert.Contains(OfficialInsightAndSpellLockTests.Errors(s with { StackItems = [item with { SelfCopySource = binding }] }),
                error => error.Contains("self copy token"));
        s = (await Top(s)).State; CheckedRestore(s); item = Assert.Single(s.StackItems); var copy = item.ReflexiveCopy!;
        foreach (var forged in new[] { copy with { CopySourceIsSelf = false }, copy with { CopySource = new("TARGET",0) },
            copy with { Recipients = [new("MASK",0)] } })
            Assert.Contains(OfficialInsightAndSpellLockTests.Errors(s with { StackItems = [item with { ReflexiveCopy = forged }] }),
                error => error.Contains("reflexive copy"));
    }

    [Fact]
    public async Task AnImageReturnedDuringTheCopyWindowCannotBeRecreated()
    {
        var entered = await Top((await Played()).State); var id = Images(entered.State)[0].ObjectId;
        var state = OfficialCopyIdentityTests.AddSpell(entered.State,"REMOVE","OGN·104/298");
        var result = await Top((await Act(state,"P1",new PlayCardCommand("REMOVE","OGN·104/298",[id]))).State);
        Assert.False(result.State.CardObjects.ContainsKey(id)); CheckedRestore(result.State);
        result = await Top(result.State);
        Assert.False(result.State.CardObjects.ContainsKey(id)); Assert.Single(Images(result.State), image=>image.CardNo==Card);
        CheckedRestore(result.State);
    }

    [Fact]
    public async Task OriginalAndCopiesHaveIndependentEphemeralBeginningTriggers()
    {
        var result = await Top((await Top((await Played()).State)).State);
        var images = Images(result.State).Select(image=>image.ObjectId).ToArray();
        result = await EphemeralStartTests.Start(EphemeralStartTests.Beginning(result.State));
        Assert.Equal(3,result.State.StackItems.Count+result.State.TriggerQueue.Count);
        Assert.Contains("MASK",result.State.PlayerZones["P1"].Base); Assert.All(images,id=>Assert.True(result.State.CardObjects.ContainsKey(id)));
        result = await TurnSequenceTestDriver.Complete(result);
        Assert.Contains("MASK",result.State.PlayerZones["P1"].Graveyard); Assert.All(images,id=>Assert.False(result.State.CardObjects.ContainsKey(id)));
        CheckedRestore(result.State);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task PublicCopyEventKeepsItsPayloadHashAcrossTheSqlJsonConvention(bool self)
    {
        var result = self ? await Top((await Played()).State)
            : await Top((await Act(OfficialCopyIdentityTests.Position(),"P1",new PlayCardCommand("C","UNL-200/219",["TARGET"]))).State);
        var ev = Assert.Single(result.Events,e=>e.Kind=="REFLEXIVE_COPY_CREATED");
        var payload = Assert.IsType<JsonElement>(ev.Payload["reflexiveCopyContext"]);
        Assert.Equal(self ? Card : "UNL-200/219",payload.GetProperty("cardNo").GetString());
        Assert.Equal(self ? "MASK" : "TARGET",payload.GetProperty("copySource").GetProperty("objectId").GetString());
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var stored = JsonSerializer.Deserialize<GameEvent>(JsonSerializer.Serialize(ev,json),json)!;
        Assert.Equal(MatchStateHasher.HashValue(ev.Payload),MatchStateHasher.HashValue(stored.Payload));
    }

    [Theory]
    [InlineData("field")]
    [InlineData("base")]
    [InlineData("other-field")]
    [InlineData("prevented")]
    public async Task StandbyPlayReadsTheSourcesCurrentLocationWhenCreatingImages(string branch)
    {
        var s = await StandbySourceRulesTests.ReadyShadow(Card);
        var result = await Act(s, "P1", new PlayCardCommand("SHADOW", Card, [])); CheckedRestore(result.State);
        s = result.State;
        if (branch == "base") s = LocalDestructionRecallTests.At(s, "SHADOW", "P1", "BASE");
        if (branch == "other-field") {
            s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
                ["F2"] = new("F2", cardNo: "OGN·296/298", ownerId: "P1", controllerId: "P1", tags: [P6TokenFactoryCatalog.BattlefieldCardTag]) },
                PlayerZones = new Dictionary<string,PlayerZones>(s.PlayerZones) { ["P1"] = s.PlayerZones["P1"] with { Battlefields = s.PlayerZones["P1"].Battlefields.Append("F2").ToArray() } },
                ObjectLocations = new Dictionary<string,ObjectLocationState>(s.ObjectLocations) { ["F2"] = new("P1","BATTLEFIELD","F2") } };
            s = LocalDestructionRecallTests.At(s, "SHADOW", "P1", "BATTLEFIELD", "F2");
        }
        if (branch == "prevented") s = s with { CardObjects = new Dictionary<string,CardObjectState>(s.CardObjects) {
            ["F"] = s.CardObjects["F"] with { CardNo = "SFD·216/221" } } };
        result = await Top(s);
        if (branch == "prevented") { Assert.Empty(Images(result.State)); Assert.DoesNotContain(result.Events, e => e.Kind == "REFLEXIVE_COPY_CREATED"); }
        else {
            Assert.Equal(2, Images(result.State).Length);
            Assert.All(Images(result.State), image => {
                Assert.True(image.IsExhausted); Assert.Equal(0,image.Power);
                Assert.Equal(branch == "base" ? "BASE" : "BATTLEFIELD", result.State.ObjectLocations[image.ObjectId].Zone);
                Assert.Equal(branch == "base" ? null : branch == "other-field" ? "F2" : "F", result.State.ObjectLocations[image.ObjectId].BattlefieldObjectId);
            });
            CheckedRestore(result.State); result = await Top(result.State);
            Assert.All(Images(result.State), image => Assert.Equal(Card,image.CardNo));
        }
        CheckedRestore(result.State);
    }

    [Fact]
    public async Task ColdRecoveryAtBothResponseWindowsKeepsCompleteActionAndEventReplay()
    {
        var initial = Position(); var journal = new Journal(); var engine = new CoreRuleEngine();
        var session = new MatchSession(initial, engine, journal); var result = await Submit("P1", new PlayCardCommand("MASK", Card, []));
        for (var stage = 0; stage < 2; stage++) {
            var restored = JsonSerializer.Deserialize<MatchState>(JsonSerializer.Serialize(result.State))!; CheckedRestore(restored);
            var frame = new MatchRecoveryFrame(initial.RoomId, restored.Tick, journal.Entries[^1].CompletedEventSequence,
                Commands(), Events(), new Dictionary<string,RecoveredPlayerView>(), [], restored, initial);
            session = MatchSession.Restore(frame, engine, journal);
            for (var pass = 0; pass < 2; pass++) result = await Submit(result.State.PriorityPlayerId!, new PassPriorityCommand());
        }
        Assert.Equal(2, Images(result.State).Length); Assert.All(Images(result.State), image => Assert.Equal(Card, image.CardNo));
        Assert.Empty(result.State.StackItems); Assert.Empty(result.State.TriggerQueue);
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, Commands(), result.State, engine, default, Events());
        Assert.True(replay.IsMatch, string.Join("; ", replay.Errors));
        async Task<ResolutionResult> Submit(string player, GameCommand command) {
            var accepted = await session.SubmitAsync(player, "masked-" + journal.Entries.Count, command,
                JsonSerializer.SerializeToElement(command, command.GetType(), new JsonSerializerOptions(JsonSerializerDefaults.Web)), default);
            Assert.True(accepted.Accepted, accepted.ErrorMessage); CheckedRestore(accepted.State); return accepted;
        }
        RecoveredCommand[] Commands() => journal.Entries.Select(e => new RecoveredCommand(e.PlayerId, e.ClientIntentId, e.CommandType, e.RawCommand,
            e.StartedTick, e.CompletedTick, e.StartedEventSequence, e.CompletedEventSequence, e.Accepted, e.ErrorMessage) { TypedCommand = e.TypedCommand }).ToArray();
        RecoveredEvent[] Events() => journal.Entries.SelectMany(e => e.Events.Select((ev,i) => new RecoveredEvent(e.StartedEventSequence+i+1,e.CompletedTick,i,ev))).ToArray();
    }

    private static void CheckedRestore(MatchState state) {
        var errors = OfficialInsightAndSpellLockTests.Errors(state);
        Assert.True(errors.Count == 0, string.Join("; ", errors));
        OfficialGraveyardRecastTests.Restore(state);
    }

    private sealed class Journal : IMatchJournal {
        public List<MatchJournalEntry> Entries { get; } = [];
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken cancellationToken) { Entries.Add(entry); return ValueTask.CompletedTask; }
    }
}
