using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class CopiedLastBreathInsightTests
{
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public async Task EachActuallyAttachedVisionaryTextQueuesItsOwnLastBreathInsight(int songs)
    {
        // CN719.1 and808.2. Controlled inputs do not establish an ordinary
        // legal opening or native product acceptance.
        var initial = Position(); var driver = new StandbyDefendObserverTests.Driver(initial);
        await Attach(driver, songs); await Destroy(driver);
        Assert.Contains("HOST", driver.State.PlayerZones["P1"].Graveyard);
        var contexts = Contexts(driver.State);
        Assert.Equal(1 + songs, contexts.Length);
        Assert.Single(contexts, c => c.Origin is null);
        Assert.Equal(songs, contexts.Count(c => c.Origin is not null));
        Assert.All(contexts, c => { Assert.Equal(2, c.Count); Assert.Equal("P1", c.ControllerId); });
        foreach (var c in contexts.Where(c => c.Origin is not null))
        {
            Assert.Equal("HOST", c.Origin!.Unit.ObjectId);
            Assert.Equal("UNL-062/219", c.Origin.CardNo);
            Assert.True(c.SourceGeneration < driver.State.CardObjects["HOST"].ObjectGeneration);
        }
        await Resolve(driver, 1 + songs, 0);
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData(0, false)] [InlineData(1, false)] [InlineData(2, false)]
    [InlineData(0, true)] [InlineData(1, true)] [InlineData(2, true)]
    public async Task ActualOnBattlefieldDamageCapturesCopiesButRecallReplacementDoesNot(int songs, bool recalled)
    {
        var initial = Position();
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["FIELD"] = new("FIELD", cardNo: "OGN·294/298", ownerId: "P1", controllerId: "P1", tags: [P6TokenFactoryCatalog.BattlefieldCardTag]),
            ["HOST"] = initial.CardObjects["HOST"] with { UntilEndOfTurnEffects = recalled ? ["RECALL_TO_BASE_EXHAUSTED_IF_DESTROYED_THIS_TURN"] : [] } } };
        initial = LocalDestructionRecallTests.At(initial, "FIELD", "P1", "BATTLEFIELD", "FIELD");
        initial = LocalDestructionRecallTests.At(initial, "HOST", "P1", "BATTLEFIELD", "FIELD");
        for (var i = 1; i <= 3; i++) initial = OfficialCopyIdentityTests.AddSpell(initial, "BOLT" + i, "OGS·003/024");
        var driver = new StandbyDefendObserverTests.Driver(initial); await Attach(driver, songs);
        // The current spell deals2 only to a battlefield unit; barracks adds1
        // Might. Use actual spells until destruction or its real replacement.
        for (var i = 1; i <= 3 && driver.State.PlayerZones["P1"].Battlefields.Contains("HOST"); i++)
        {
            await driver.Act("P1", new PlayCardCommand("BOLT" + i, "OGS·003/024", ["HOST"]));
            await driver.Top();
        }
        Assert.DoesNotContain("HOST", driver.State.PlayerZones["P1"].Battlefields);
        Assert.Equal(recalled ? 0 : 1 + songs, Contexts(driver.State).Length);
        if (recalled) Assert.Contains("HOST", driver.State.PlayerZones["P1"].Base);
        else { Assert.Contains("HOST", driver.State.PlayerZones["P1"].Graveyard); await Resolve(driver, 1 + songs, 1); }
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData(false, 0)] [InlineData(false, 1)] [InlineData(false, 2)]
    [InlineData(true, 0)] [InlineData(true, 1)] [InlineData(true, 2)]
    public async Task CapturedControllerAndEachRecycleSubsetUseTheRemainingPrivateDeck(bool foreignOwner, int recycle)
    {
        var initial = Position();
        if (foreignOwner) initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["HOST"] = initial.CardObjects["HOST"] with { OwnerId = "P2" } } };
        var driver = new StandbyDefendObserverTests.Driver(initial); await Attach(driver, 2); await Destroy(driver);
        Assert.Contains("HOST", driver.State.PlayerZones[foreignOwner ? "P2" : "P1"].Graveyard);
        Assert.All(Contexts(driver.State), c => Assert.Equal("P1", c.ControllerId));
        await Resolve(driver, 3, recycle);
        Assert.DoesNotContain(driver.Events(), e => e.Event.Kind == "CARD_DRAWN" || e.Event.Kind.Contains("BURN", StringComparison.Ordinal));
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)]
    public async Task EachCopiedInsightDoesAsMuchAsPossibleOnAnEmptyOrShortDeck(int remaining)
    {
        var initial = Position(); var removed = initial.PlayerZones["P1"].MainDeck.Skip(remaining).ToHashSet();
        initial = initial with { PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
            ["P1"] = initial.PlayerZones["P1"] with { MainDeck = initial.PlayerZones["P1"].MainDeck.Take(remaining).ToArray() } },
            CardObjects = initial.CardObjects.Where(p => !removed.Contains(p.Key)).ToDictionary(p => p.Key, p => p.Value),
            ObjectLocations = initial.ObjectLocations.Where(p => !removed.Contains(p.Key)).ToDictionary(p => p.Key, p => p.Value) };
        var driver = new StandbyDefendObserverTests.Driver(initial); await Attach(driver, 1); await Destroy(driver);
        Assert.Equal(2, Contexts(driver.State).Length); await Resolve(driver, 2, 0);
        Assert.DoesNotContain(driver.Events(), e => e.Event.Kind == "CARD_DRAWN" || e.Event.Kind.Contains("BURN", StringComparison.Ordinal));
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Fact]
    public async Task PrivateChoicesRejectForeignStaleOutsideAndDuplicateOrderWithoutMutation()
    {
        var initial = Position(); var driver = new StandbyDefendObserverTests.Driver(initial);
        await Attach(driver, 1); await Destroy(driver); await Order(driver);
        string? first = null;
        for (var i = 0; i < 2; i++)
        {
            await driver.Top(); var choice = Assert.IsType<PendingCardChoiceState>(driver.State.PendingCardChoice);
            if (i == 0)
            {
                first = choice.ChoiceId;
                await Reject(driver.State, "P2", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, []));
                await Reject(driver.State, "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["OUTSIDE"]));
            }
            else { Assert.NotEqual(first, choice.ChoiceId); await Reject(driver.State, "P1", new ChooseCardsCommand(first!, choice.ChoiceWindow, [])); }
            await driver.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, []));
            var order = Assert.IsType<PendingCardChoiceState>(driver.State.PendingCardChoice);
            Assert.Equal("INSIGHT_ORDER", order.ChoiceWindow);
            await Reject(driver.State, "P1", new ChooseCardsCommand(order.ChoiceId, order.ChoiceWindow, [order.LegalObjectIds[0], order.LegalObjectIds[0]]));
            await driver.Act("P1", new ChooseCardsCommand(order.ChoiceId, order.ChoiceWindow, order.LegalObjectIds.Reverse().ToArray()));
        }
        Assert.Empty(driver.State.StackItems); Assert.Null(driver.State.PendingCardChoice);
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Fact]
    public async Task ActualReattachmentAfterDeathKeepsTheOriginalCapturedEquipmentRevision()
    {
        var initial = OfficialCopyIdentityTests.AddSpell(Position(), "REATTACH", "SFD·011/221");
        var driver = new StandbyDefendObserverTests.Driver(initial); await Attach(driver, 1); await Destroy(driver);
        var captured = Assert.Single(Contexts(driver.State), c => c.Origin is not null).Origin!;
        await Order(driver);
        await driver.Act("P1", new PlayCardCommand("REATTACH", "SFD·011/221", ["MALZ", "GEAR"])); await driver.Top();
        Assert.Equal("MALZ", driver.State.CardObjects["GEAR"].AttachedToObjectId);
        Assert.True(driver.State.CardObjects["GEAR"].AttachmentRevision > captured.Revision);
        Assert.Equal(captured, Assert.Single(Contexts(driver.State), c => c.Origin is not null).Origin);
        await Resolve(driver, 2, 0); await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task BorrowedAndRetainedHostFacesKeepTheirOwnEquipmentOriginsAtDeath(bool detachBorrowedBeforeDeath)
    {
        var initial = AddMalz(AttachedRuleTextTests.AddSong(ShadySpectaclesTests.Position("UNL-062/219"), "SONG"));
        initial = OfficialCopyIdentityTests.AddSpell(initial, "DETACH", "SFD·011/221");
        var driver = new StandbyDefendObserverTests.Driver(initial); await Borrow(driver, "HOST");
        await driver.Act("P1", new AssembleEquipmentCommand("SONG", "HOST", ["ASSEMBLE_GREEN"])); await driver.Top();
        if (detachBorrowedBeforeDeath)
        {
            await driver.Act("P1", new PlayCardCommand("DETACH", "SFD·011/221", ["HOST", "GEAR"])); await driver.Top();
            Assert.Equal("OGN·096/298", driver.State.CardObjects["HOST"].CardNo);
        }
        await Destroy(driver); var contexts = Contexts(driver.State);
        Assert.Equal(detachBorrowedBeforeDeath ? 1 : 2, contexts.Length);
        Assert.All(contexts, c => Assert.NotNull(c.Origin));
        Assert.Contains(contexts, c => c.Origin!.Equipment.ObjectId == "SONG");
        if (!detachBorrowedBeforeDeath) Assert.Contains(contexts, c => c.Origin!.Equipment.ObjectId == "GEAR");
        await Resolve(driver, contexts.Length, 0, expectNativeDraw: detachBorrowedBeforeDeath);
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ActuallyCreatedBorrowedSandSoldierKeepsCapturedInsightAfterItCeases(bool song)
    {
        var initial = AddMalz(ShadySpectaclesTests.Position("UNL-062/219"));
        initial = OfficialCopyIdentityTests.AddSpell(initial, "SANDCRAFT", "SFD·031/221");
        if (song) initial = AttachedRuleTextTests.AddSong(initial, "SONG");
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await driver.Act("P1", new PlayCardCommand("SANDCRAFT", "SFD·031/221", [])); await driver.Top();
        var token = Assert.Single(driver.State.CardObjects.Values, c => c.TokenFactoryCardNo == P6TokenFactoryCatalog.SandSoldierTokenCardNo).ObjectId;
        await Borrow(driver, token);
        if (song) { await driver.Act("P1", new AssembleEquipmentCommand("SONG", token, ["ASSEMBLE_GREEN"])); await driver.Top(); }
        await Destroy(driver, token); Assert.False(driver.State.CardObjects.ContainsKey(token));
        var contexts = Contexts(driver.State); Assert.Equal(song ? 2 : 1, contexts.Length);
        Assert.All(contexts, c => { Assert.NotNull(c.Origin); Assert.Equal(token, c.Origin!.Unit.ObjectId); });
        foreach (var factory in new string?[] { null, "NOT-A-TOKEN-FACTORY" })
        {
            var bad = driver.State with {
                TriggerQueue = driver.State.TriggerQueue.Select(t => t.InsightContext is { } c
                    ? t with { InsightContext = c with { SourceTokenFactoryCardNo = factory } } : t).ToArray(),
                StackItems = driver.State.StackItems.Select(t => t.InsightContext is { } c
                    ? t with { InsightContext = c with { SourceTokenFactoryCardNo = factory } } : t).ToArray() };
            Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(bad));
        }
        await Resolve(driver, contexts.Length, 0); await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    public static IEnumerable<object[]> PublicForgeryCases()
        => from viewer in new[] { "P1", "P2", "spectator" }
           from ordered in new[] { false, true }
           from mutation in new[] { "REMOVE", "COUNT", "SOURCE", "TOKEN", "MALFORMED", "UNKNOWN_FIELD" }
           select new object[] { viewer, ordered, mutation };

    [Theory]
    [MemberData(nameof(PublicForgeryCases))]
    public async Task EveryPublicQueueAndStackViewRejectsChangedRemovedOrMalformedCapturedInsight(string viewer, bool ordered, string mutation)
    {
        var driver = new StandbyDefendObserverTests.Driver(Position()); await Attach(driver, 1); await Destroy(driver);
        if (ordered) await Order(driver);
        var state = driver.State; ArmamentMightTests.PublicRecovery(state);
        var snapshots = ResolutionResult.BuildSnapshots(state); var prompts = ResolutionResult.BuildPrompts(state);
        var views = state.Seats.Keys.ToDictionary(id => id, id => new RecoveredPlayerView(id, state.Tick, 0,
            snapshots[id], state.Tick, 0, prompts[id]));
        var spectator = MatchReplayRedactor.BuildSpectatorFrame(state.RoomId, state.Tick, 0, [], state);
        var target = viewer == "spectator" ? spectator.SpectatorSnapshot : snapshots[viewer];
        IEnumerable<object?> items = ordered ? target.Stack : Assert.IsAssignableFrom<IEnumerable<Dictionary<string, object?>>>(target.Timing["triggerQueue"]);
        var item = items.Cast<Dictionary<string, object?>>().First(i => i.GetValueOrDefault("lastBreathInsight") is InsightTriggerContext { Origin: not null });
        var context = Assert.IsType<InsightTriggerContext>(item["lastBreathInsight"]);
        switch (mutation)
        {
            case "REMOVE": item.Remove("lastBreathInsight"); break;
            case "COUNT": item["lastBreathInsight"] = context with { Count = 99 }; break;
            case "SOURCE": item["lastBreathInsight"] = context with { Origin = context.Origin! with { Unit = new("MALZ", 0) } }; break;
            case "TOKEN": item["lastBreathInsight"] = context with { SourceTokenFactoryCardNo = P6TokenFactoryCatalog.SandSoldierTokenCardNo }; break;
            case "MALFORMED": item["lastBreathInsight"] = "FORGED"; break;
            default:
                var raw = JsonSerializer.SerializeToNode(context)!.AsObject(); raw["unexpected"] = "FORGED";
                item["lastBreathInsight"] = JsonSerializer.SerializeToElement(raw); break;
        }
        Assert.Contains(MatchRecoveryValidator.Validate(state.RoomId, 0, [], [], views, state, state.Tick, spectator),
            e => e.Contains("captured last-breath Insight", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("CARD")] [InlineData("SOURCE")] [InlineData("SOURCE_GENERATION")]
    [InlineData("ORIGIN_GENERATION")] [InlineData("EQUIPMENT")] [InlineData("EQUIPMENT_GENERATION")]
    [InlineData("REVISION")] [InlineData("CAPTURE_FUTURE")] [InlineData("CAPTURE_NEGATIVE")]
    [InlineData("FACE_NEGATIVE")] [InlineData("EQUIPMENT_SELF")]
    [InlineData("TOKEN_FACTORY")] [InlineData("UNKNOWN_TOKEN_FACTORY")]
    public async Task StructuralRecoveryRejectsMalformedCopiedLastBreathBindings(string mutation)
    {
        var driver = new StandbyDefendObserverTests.Driver(Position()); await Attach(driver, 1); await Destroy(driver);
        var trigger = Assert.Single(driver.State.TriggerQueue, t => t.InsightContext?.Origin is not null);
        var c = trigger.InsightContext!; var o = c.Origin!;
        var bad = mutation switch {
            "CARD" => c with { Origin = o with { CardNo = "OGN·096/298" } },
            "SOURCE" => c with { Origin = o with { Unit = o.Unit with { ObjectId = "MALZ" } } },
            "SOURCE_GENERATION" => c with { SourceGeneration = 99 },
            "ORIGIN_GENERATION" => c with { Origin = o with { Unit = o.Unit with { Generation = 99 } } },
            "EQUIPMENT" => c with { Origin = o with { Equipment = o.Equipment with { ObjectId = "UNKNOWN" } } },
            "EQUIPMENT_GENERATION" => c with { Origin = o with { Equipment = o.Equipment with { Generation = 99 } } },
            "REVISION" => c with { Origin = o with { Revision = 99 } },
            "CAPTURE_FUTURE" => c with { Origin = o with { CapturedTick = driver.State.Tick + 1 } },
            "CAPTURE_NEGATIVE" => c with { Origin = o with { CapturedTick = -1 } },
            "FACE_NEGATIVE" => c with { Origin = o with { FaceRevision = -1 } },
            "TOKEN_FACTORY" => c with { SourceTokenFactoryCardNo = P6TokenFactoryCatalog.SandSoldierTokenCardNo },
            "UNKNOWN_TOKEN_FACTORY" => c with { SourceTokenFactoryCardNo = "NOT-A-TOKEN-FACTORY" },
            _ => c with { Origin = o with { Equipment = o.Unit } } };
        var state = driver.State with { TriggerQueue = driver.State.TriggerQueue.Select(t => t.TriggerId == trigger.TriggerId
            ? t with { InsightContext = bad } : t).ToArray() };
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(state), e => e.Contains("Insight", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ActualSimultaneousDestructionRetainsEachHostsIndependentCopiedLastBreaths()
    {
        // UNL180 is a controlled destruction action, outside the green-blue
        // ordinary opening. All fixture setup precedes the accepted history.
        var initial = AttachedRuleTextTests.AddSong(AttachedRuleTextTests.Position("UNL-062/219", 0), "SONG2");
        initial = LocalDestructionRecallTests.At(
            OfficialHoldSequenceTests.AddUnit(initial, "HOST2", "UNL-062/219", "F", "P1"), "HOST2", "P1", "BASE");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["HOST2"] = initial.CardObjects["HOST2"] with { Power = 4 } } };
        initial = OfficialCopyIdentityTests.AddSpell(initial, "RUIN", "UNL-180/219");
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await driver.Act("P1", new AssembleEquipmentCommand("GEAR", "HOST", ["ASSEMBLE_GREEN"])); await driver.Top();
        await driver.Act("P1", new AssembleEquipmentCommand("SONG2", "HOST2", ["ASSEMBLE_GREEN"])); await driver.Top();
        await driver.Act("P1", new PlayCardCommand("RUIN", "UNL-180/219", [])); var destroyed = await driver.Top();
        Assert.Equal(2, destroyed.Events.Count(e => e.Kind == "UNIT_DESTROYED"));
        Assert.Equal(4, Contexts(driver.State).Length);
        Assert.Equal(4, driver.State.TriggerQueue.Select(t => t.TriggerId).Distinct().Count());
        foreach (var (host, gear) in new[] { ("HOST", "GEAR"), ("HOST2", "SONG2") })
        {
            Assert.Contains(host, driver.State.PlayerZones["P1"].Graveyard);
            var earned = driver.State.TriggerQueue.Where(t => t.SourceObjectId == host).ToArray(); Assert.Equal(2, earned.Length);
            Assert.Single(earned, t => t.InsightContext?.Origin is null);
            var copied = Assert.Single(earned, t => t.InsightContext?.Origin is not null).InsightContext!.Origin!;
            Assert.Equal(host, copied.Unit.ObjectId); Assert.Equal(gear, copied.Equipment.ObjectId);
            Assert.All(earned, t => { Assert.Equal("P1", t.ControllerId); Assert.Equal(2, t.InsightContext!.Count); });
        }
        await Resolve(driver, 4, 0); await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    internal static MatchState Position() => AddMalz(AttachedRuleTextTests.AddSong(AttachedRuleTextTests.Position("UNL-062/219", 0), "SONG2"));
    private static MatchState AddMalz(MatchState state) => LocalDestructionRecallTests.At(
        OfficialHoldSequenceTests.AddUnit(state, "MALZ", "OGN·113/298", "F", "P1"), "MALZ", "P1", "BASE");
    private static async Task Attach(StandbyDefendObserverTests.Driver driver, int songs)
    {
        for (var i = 0; i < songs; i++) { await driver.Act("P1", new AssembleEquipmentCommand(i == 0 ? "GEAR" : "SONG2", "HOST", ["ASSEMBLE_GREEN"])); await driver.Top(); }
    }
    private static Task<ResolutionResult> Destroy(StandbyDefendObserverTests.Driver driver, string target = "HOST")
        => driver.Act("P1", new ActivateAbilityCommand("MALZ", P4ActivatedAbilityCatalog.MalzaharResourceAbilityId, [target]));
    private static async Task Borrow(StandbyDefendObserverTests.Driver driver, string host)
    {
        await driver.Act("P1", new AssembleEquipmentCommand("GEAR", host, ["ASSEMBLE_YELLOW"])); await driver.Top();
        var pending = Assert.IsType<RuleChoiceContinuation>(driver.State.PendingRuleChoice);
        await driver.Act("P1", new PayCostCommand(pending.Request.Id, "RULE_REPLACEMENT", ["COPY:COPY-SOURCE"]));
        Assert.Equal("UNL-062/219", driver.State.CardObjects[host].CardNo);
    }
    private static InsightTriggerContext[] Contexts(MatchState state) => state.TriggerQueue.Select(t => t.InsightContext)
        .Concat(state.StackItems.Select(t => t.InsightContext)).Where(c => c?.Kind == "LAST_BREATH").Select(c => c!).ToArray();
    private static async Task Order(StandbyDefendObserverTests.Driver driver)
    {
        if (driver.State.TriggerQueue.Count > 0)
            await driver.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: driver.State.TriggerQueue.Select(t => t.TriggerId).Reverse().ToArray()));
    }
    private static async Task Resolve(StandbyDefendObserverTests.Driver driver, int count, int recycle, bool expectNativeDraw = false)
    {
        await Order(driver); var choices = new HashSet<string>();
        for (var i = 0; i < count; i++)
        {
            var deck = driver.State.PlayerZones["P1"].MainDeck.ToArray(); var top = deck.Take(2).ToArray();
            await driver.Top();
            if (top.Length == 0) { Assert.Null(driver.State.PendingCardChoice); continue; }
            var choice = Assert.IsType<PendingCardChoiceState>(driver.State.PendingCardChoice);
            Assert.Equal("P1", choice.PlayerId); Assert.Equal(top, choice.ContextObjectIds); Assert.Equal(top.Length, choice.MaxCount);
            Assert.True(choices.Add(choice.ChoiceId));
            foreach (var id in top) Assert.DoesNotContain(JsonSerializer.Serialize(id), JsonSerializer.Serialize(ResolutionResult.BuildPrompts(driver.State)["P2"]));
            var selected = top.Take(Math.Min(recycle, top.Length)).ToArray(); var kept = top.Except(selected).Reverse().ToArray();
            await driver.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, selected));
            if (kept.Length > 1)
            {
                var order = Assert.IsType<PendingCardChoiceState>(driver.State.PendingCardChoice); Assert.Equal("INSIGHT_ORDER", order.ChoiceWindow);
                await driver.Act("P1", new ChooseCardsCommand(order.ChoiceId, order.ChoiceWindow, kept));
            }
            var actual = driver.State.PlayerZones["P1"].MainDeck;
            Assert.Equal(kept.Concat(deck.Skip(top.Length)), actual.Take(actual.Count - selected.Length));
            Assert.Equal(selected.Order(), actual.Skip(actual.Count - selected.Length).Order());
        }
        if (expectNativeDraw)
        {
            var native = Assert.Single(driver.State.StackItems);
            Assert.Equal("WATCHFUL_SENTINEL_LAST_BREATH_DRAW_1", native.EffectKind);
            Assert.Equal("OGN·096/298", native.CardNo); Assert.Null(native.InsightContext);
            var before = driver.State.PlayerZones["P1"]; var drawn = before.MainDeck[0];
            await driver.Top();
            Assert.Equal(before.Hand.Append(drawn), driver.State.PlayerZones["P1"].Hand);
            Assert.Equal(before.MainDeck.Skip(1), driver.State.PlayerZones["P1"].MainDeck);
        }
        Assert.Empty(driver.State.StackItems); Assert.Empty(driver.State.TriggerQueue); Assert.Null(driver.State.PendingCardChoice);
    }
    private static async Task Reject(MatchState state, string player, GameCommand command)
    {
        var hash = MatchStateHasher.Hash(state); var result = await new CoreRuleEngine().ResolveAsync(state, new("bad-choice", player, command.CmdType), command, default);
        Assert.False(result.Accepted); Assert.Equal(hash, MatchStateHasher.Hash(result.State)); Assert.Empty(result.Events);
    }
}
