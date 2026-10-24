using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class Ven103AnyGraveyardReturnConsumerTests
{
    private const string No = "VEN·103";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)] [InlineData(6)]
    public async Task ARealCastReturnsZeroOwnEnemyOrMixedGraveyardUnitsToTheirOwners(int selection)
    {
        var initial = Position(); var driver = new Driver(initial);
        string[] targets = selection switch { 0 => [], 1 => ["OWN"], 2 => ["THEIRS"], 3 => ["THEIRS", "OWN"],
            4 => ["OWN", "OWN2"], 5 => ["THEIRS", "THEIRS2"], _ => ["OWN", "THEIRS"] };
        await driver.Act("P1", new PlayCardCommand("SPELL", No, targets));
        Assert.Equal(17, driver.State.RunePools["P1"].Mana); Assert.Equal(3, driver.State.RunePools["P1"].PowerByTrait[RuneTrait.Purple]);
        Assert.All(targets, id => Assert.Contains(id, driver.State.PlayerZones[initial.CardObjects[id].OwnerId!].Graveyard));
        await driver.Top();
        foreach (var id in targets) {
            var owner = initial.CardObjects[id].OwnerId!; Assert.Contains(id, driver.State.PlayerZones[owner].Hand);
            Assert.DoesNotContain(id, driver.State.PlayerZones[owner].Graveyard);
            Assert.Equal(new ObjectLocationState(owner, "HAND"), driver.State.ObjectLocations[id]);
            Assert.Equal(owner, driver.State.CardObjects[id].ControllerId);
            Assert.Equal(initial.CardObjects[id].ObjectGeneration + 1, driver.State.CardObjects[id].ObjectGeneration);
            var returned = Assert.Single(Returns(driver), e => Equals(e.Event.Payload["targetObjectId"], id));
            Assert.Equal(owner, returned.Event.Payload["playerId"]); Assert.Equal("GRAVEYARD", returned.Event.Payload["sourceZone"]);
            Assert.Equal("HAND", returned.Event.Payload["destinationZone"]);
            Assert.DoesNotContain(id, driver.State.PlayerZones[owner == "P1" ? "P2" : "P1"].Hand);
            HiddenFromOtherPlayerAndSpectator(driver.State, owner, id);
        }
        Assert.Equal(targets.Length, Returns(driver).Length); Assert.Contains("SPELL", driver.State.PlayerZones["P1"].Graveyard);
        await Export("owners-selection-" + selection, initial, driver);
    }

    [Fact]
    public async Task ExactCn103SourceHasNoReactionSwiftOrAdditionalInstruction()
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadDefaultAsync()).Cards, c => c.CardNo == No);
        Assert.Equal(37800, card.Id); Assert.Equal("往日阴影", card.CardName);
        Assert.Equal("让最多两名单位从任意废牌堆返回其所属的手牌。", card.CardEffect);
        Assert.Equal(3, card.Energy); Assert.Equal(1, card.ReturnEnergy); Assert.Equal([RuneTrait.Purple], card.CardColorList);
        Assert.True(CardBehaviorRegistry.TryGetByCardNo(No, out var behavior));
        Assert.Equal(3, behavior.ManaCost); Assert.Equal(2, behavior.RequiredTargetCount); Assert.Equal(0, behavior.MinTargetCount);
        Assert.Equal("ANY_GRAVEYARD_CARD", behavior.TargetScope); Assert.Equal(CardObjectTags.UnitCard, behavior.TargetRequiredTag);
        Assert.True(behavior.ReturnsGraveyardTargetToHand); Assert.False(behavior.CanPlayDuringPriority); Assert.False(behavior.CanPlayDuringSpellDuel);
        Assert.False(behavior.GrantsFreeStandbyHidePermission); Assert.Null(behavior.FlowCost);
    }

    [Fact]
    public async Task ThePromptOffersOnlyPublicGraveyardUnitsAndThePreviewPaysTheExactPrintedCost()
    {
        var initial = Position(); var prompt = ResolutionResult.BuildPrompts(initial)["P1"];
        var candidate = Assert.Single(prompt.Candidates!, c => c.Action == CommandTypes.PlayCard && c.Sources!.Any(s => s.Id == "SPELL"));
        Assert.True(candidate.Enabled, candidate.Reason);
        Assert.Equal(new[] { "GRAVE", "OWN", "OWN2", "THEIRS", "THEIRS2" }, candidate.Targets!.Select(t => t.Id).Order().ToArray());
        var command = new PlayCardCommand("SPELL", No, ["THEIRS", "OWN"]);
        var quote = new CoreRuleEngine().PreviewPlayCard(initial, "P1", PlayCostPreviewTests.Request(initial, command));
        Assert.True(quote.CanPay, quote.Message); Assert.Equal(3, quote.Cost!.Mana);
        var run = new Driver(initial); await run.Act("P1", command);
        var cost = Assert.Single(run.Events(), e => e.Event.Kind == "COST_PAID").Event.Payload;
        Assert.Equal(3, cost["mana"]); Assert.Equal(1, cost["power"]); Assert.Equal(0, cost["spellshieldTaxPower"]);
        JsonRestore(initial, run); await run.Top(); await Export("prompt-and-printed-cost", initial, run);
    }

    [Theory]
    [InlineData("field")] [InlineData("hand")] [InlineData("deck")] [InlineData("gear")] [InlineData("spell")]
    [InlineData("banished")] [InlineData("unknown")] [InlineData("duplicate")] [InlineData("three")]
    [InlineData("mana")] [InlineData("wrong-power")] [InlineData("power-zero")] [InlineData("extra-cost")] [InlineData("wrong-turn")]
    public async Task IllegalDeclarationsKeepResourcesZonesViewsAndPromptsExactlyUnchanged(string kind)
    {
        var initial = Position(); string[] targets = kind switch {
            "field" => ["ENEMY"], "hand" => ["UNIT"], "deck" => ["P1-DRAW-1"], "gear" => ["GRAVE-GEAR"],
            "spell" => ["GRAVE-SPELL"], "banished" => ["BANISHED"], "unknown" => ["UNKNOWN"],
            "duplicate" => ["OWN", "OWN"], "three" => ["OWN", "OWN2", "THEIRS"], _ => ["OWN"] };
        if (kind is "mana" or "wrong-power" or "power-zero")
            initial = initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) { ["P1"] =
                kind == "mana" ? new(2, 0, new Dictionary<string, int> { [RuneTrait.Purple] = 1 }) :
                kind == "wrong-power" ? new(3, 0, new Dictionary<string, int> { [RuneTrait.Red] = 1 }) : new(3, 0) } };
        if (kind == "wrong-turn") initial = initial with { ActivePlayerId = "P2", TurnPlayerId = "P2" };
        var run = new Driver(initial);
        await Reject(run, "P1", new PlayCardCommand("SPELL", No, targets, OptionalCosts: kind == "extra-cost" ? ["DISCARD_HAND_CARD:UNIT"] : []));
        Assert.False(run.Commands().Single().Accepted); Assert.False(string.IsNullOrWhiteSpace(run.Commands().Single().ErrorMessage));
        Assert.Empty(Returns(run)); await Export("illegal-" + kind, initial, run);
    }

    [Fact]
    public async Task AnOrdinarySpellCannotRespondDuringAnActualSpellDuel()
    {
        var initial = Position() with { ActivePlayerId = "P2", TurnPlayerId = "P2" };
        initial = At(initial, "ALLY", "P1", "BATTLEFIELD", "BF"); var run = new Driver(initial);
        await run.Act("P2", new MoveUnitCommand("ENEMY", "BASE", "BATTLEFIELD:BF", []));
        await run.Act("P2", new PassFocusCommand()); Assert.Equal(TimingStates.SpellDuelOpen, run.State.TimingState);
        await Reject(run, "P1", new PlayCardCommand("SPELL", No, ["OWN"]));
        Assert.Contains("SPELL", run.State.PlayerZones["P1"].Hand); Assert.Empty(Returns(run));
        await Export("ordinary-spell-in-real-movement-duel", initial, run);
    }

    [Fact]
    public async Task AnOrdinarySpellCannotRespondToARealReactionInAClosedNeutralChain()
    {
        var initial = Put(Position(), "READY", "UNL-009/219", "P1", "HAND"); var run = new Driver(initial);
        await run.Act("P1", new PlayCardCommand("READY", "UNL-009/219", ["ALLY"]));
        Assert.Equal(TimingStates.NeutralClosed, run.State.TimingState);
        await Reject(run, "P1", new PlayCardCommand("SPELL", No, ["OWN"])); Assert.Contains("SPELL", run.State.PlayerZones["P1"].Hand);
        await run.Top(); await run.Act("P1", new PlayCardCommand("SPELL", No, ["OWN"])); await run.Top();
        Assert.Contains("OWN", run.State.PlayerZones["P1"].Hand); await Export("ordinary-timing-after-real-reaction", initial, run);
    }

    [Theory]
    [InlineData(No, false)] [InlineData("OGN·264/298", false)] [InlineData(No, true)] [InlineData("OGN·264/298", true)]
    public async Task MalformedTargetPlaceholdersCannotCreateAnUnrecoverableAcceptedCommand(string sourceNo, bool stale)
    {
        var initial = Put(UnitEntryPosition("OGN·219/298"), "SPELL", sourceNo, "P1", "HAND") with { RoomId = "RAW-BLANK-TARGET-BOUNDARY" };
        var journal = new Driver(initial); var session = new MatchSession(initial, new CoreRuleEngine(), journal);
        var command = new PlayCardCommand("SPELL", sourceNo, ["", "  "]);
        var raw = JsonSerializer.SerializeToNode(command, Json)!.AsObject();
        raw["promptId"] = session.PromptFor("P1").PromptId + (stale ? "-stale" : ""); raw["snapshotTick"] = initial.Tick;
        var wire = JsonSerializer.SerializeToElement(raw, Json);
        var intentId = "ven103-blank-target-" + sourceNo;
        var result = await session.SubmitAsync("P1", intentId, GameCommandJsonMapper.Map(wire), wire, default);
        if (Environment.GetEnvironmentVariable("RIFTBOUND_VEN103_EVIDENCE") is { Length: > 0 } output) {
            Directory.CreateDirectory(output); File.WriteAllText(Path.Combine(output, (sourceNo == No ? "blank-n15-" : "blank-old-warfare-") + stale + ".json"),
                JsonSerializer.Serialize(new { initial, final = result.State, submitted = wire, commands = journal.Commands(), events = journal.Events(), accepted = result.Accepted, runtime = RuntimeEvidence() }, Json));
        }
        Assert.False(result.Accepted); Assert.Equal(ErrorCodes.InvalidPayload, result.ErrorCode);
        Assert.Empty(result.Events); Assert.Equal(MatchStateHasher.Hash(initial), MatchStateHasher.Hash(result.State));
        FullGameEndToEndTests.AssertNoHiddenZoneLeak(result); Assert.Empty(journal.Commands()); Assert.Empty(journal.Events());
        Assert.Equal(MatchStateHasher.HashValue(ResolutionResult.BuildSnapshots(initial)), MatchStateHasher.HashValue(result.Snapshots));
        Assert.Equal(MatchStateHasher.HashValue(ResolutionResult.BuildPrompts(initial)), MatchStateHasher.HashValue(result.Prompts));
        raw["targetObjectIds"] = JsonSerializer.SerializeToNode(Array.Empty<string>(), Json);
        raw["promptId"] = session.PromptFor("P1").PromptId; raw["snapshotTick"] = initial.Tick;
        var canonical = JsonSerializer.SerializeToElement(raw, Json); var validCommand = GameCommandJsonMapper.Map(canonical);
        var corrected = await session.SubmitAsync("P1", intentId, validCommand, canonical, default);
        Assert.True(corrected.Accepted, corrected.ErrorMessage); Assert.Single(journal.Commands()); Assert.True(journal.Commands()[0].Accepted);
        FullGameEndToEndTests.AssertNoHiddenZoneLeak(corrected); JsonRestore(initial, journal, corrected.State);
        var duplicate = await session.SubmitAsync("P1", intentId, validCommand, canonical, default);
        Assert.True(duplicate.Accepted); Assert.Single(journal.Commands()); Assert.Equal(MatchStateHasher.Hash(corrected.State), MatchStateHasher.Hash(duplicate.State));
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, journal.Commands(), corrected.State, new CoreRuleEngine(), default, journal.Events());
        Assert.True(replay.IsMatch, string.Join("; ", replay.Errors));
        if (Environment.GetEnvironmentVariable("RIFTBOUND_VEN103_EVIDENCE") is { Length: > 0 } saved) {
            Directory.CreateDirectory(saved); File.WriteAllText(Path.Combine(saved, (sourceNo == No ? "corrected-n15-blank-boundary-" : "corrected-old-warfare-blank-boundary-") + stale + ".json"),
                JsonSerializer.Serialize(new { initial, rejected = new { submitted = wire, result.Accepted, result.ErrorCode },
                    submitted = canonical, final = corrected.State, commands = journal.Commands(), events = journal.Events(), runtime = RuntimeEvidence() }, Json));
        }
    }

    [Fact]
    public async Task AnActualWholeCounterKeepsThePaidCostAndLeavesBothTargetsInGraveyards()
    {
        var initial = Put(Position(), "COUNTER", "OGN·045/298", "P2", "HAND"); var run = new Driver(initial);
        await run.Act("P1", new PlayCardCommand("SPELL", No, ["OWN", "THEIRS"])); var item = Assert.Single(run.State.StackItems);
        await run.Act("P1", new PassPriorityCommand()); await run.Act("P2", new PlayCardCommand("COUNTER", "OGN·045/298", [item.StackItemId])); await run.Top();
        Assert.Empty(run.State.StackItems); Assert.Empty(Returns(run)); Assert.Contains("OWN", run.State.PlayerZones["P1"].Graveyard);
        Assert.Contains("THEIRS", run.State.PlayerZones["P2"].Graveyard); Assert.Contains("SPELL", run.State.PlayerZones["P1"].Graveyard);
        Assert.Equal(17, run.State.RunePools["P1"].Mana); Assert.Equal(3, run.State.RunePools["P1"].PowerByTrait[RuneTrait.Purple]);
        await Export("actual-whole-counter", initial, run);
    }

    [Theory]
    [InlineData(false, 1)] [InlineData(false, 2)] [InlineData(true, 1)]
    public async Task ActualRecyclingMakesOnlyTheMissingTargetsFailAtResolution(bool mixed, int count)
    {
        var initial = Put(Position(), "RECYCLE", "UNL-103/219", "P2", "HAND"); var run = new Driver(initial);
        string[] selected = ["OWN", mixed ? "THEIRS" : "OWN2"]; await run.Act("P1", new PlayCardCommand("SPELL", No, selected));
        await run.Act("P1", new PassPriorityCommand()); string[] removed = count == 2 ? ["OWN", "OWN2"] : ["OWN"];
        await run.Act("P2", new PlayCardCommand("RECYCLE", "UNL-103/219", removed, "RECYCLE_OPPONENT_GRAVEYARD_UP_TO_3")); await run.Top();
        Assert.All(removed, id => Assert.Contains(id, run.State.PlayerZones["P1"].MainDeck)); JsonRestore(initial, run);
        await run.Top(); Assert.All(removed, id => Assert.DoesNotContain(id, run.State.PlayerZones["P1"].Hand));
        Assert.Equal(2 - count, Returns(run).Length);
        if (count == 1) Assert.Contains(selected[1], run.State.PlayerZones[mixed ? "P2" : "P1"].Hand);
        Assert.Contains("SPELL", run.State.PlayerZones["P1"].Graveyard); await Export($"actual-recycle-{mixed}-{count}", initial, run);
    }

    [Fact]
    public async Task AnActualRecycleDrawAndDiscardCannotRebindTheOldTargetAfterItReturnsToTheSameGraveyard()
    {
        var initial = Put(Put(Put(Put(Position(), "RECYCLE", "UNL-103/219", "P2", "HAND"), "REFILL", "UNL-103/219", "P2", "HAND"),
            "DRAW", "UNL-103/219", "P1", "HAND"), "MOON", "UNL-125/219", "P1", "HAND");
        initial = RemoveDeck(initial, "P1"); var run = new Driver(initial);
        await run.Act("P1", new PlayCardCommand("SPELL", No, ["OWN", "THEIRS"])); var binding = Assert.Single(run.State.StackItems).TargetGenerations!["OWN"];
        await run.Act("P1", new PassPriorityCommand());
        await run.Act("P2", new PlayCardCommand("RECYCLE", "UNL-103/219", ["OWN"], "RECYCLE_OPPONENT_GRAVEYARD_UP_TO_3")); await run.Top();
        await run.Act("P1", new PlayCardCommand("DRAW", "UNL-103/219", [], "DRAW_1")); await run.Top();
        Assert.Contains("OWN", run.State.PlayerZones["P1"].Hand);
        if (run.State.PriorityPlayerId != "P2") await run.Act(run.State.PriorityPlayerId!, new PassPriorityCommand());
        await run.Act("P2", new PlayCardCommand("REFILL", "UNL-103/219", ["GRAVE", "OWN2"], "RECYCLE_OPPONENT_GRAVEYARD_UP_TO_3")); await run.Top();
        Assert.Equal(2, run.State.PlayerZones["P1"].MainDeck.Count);
        await run.Act("P1", new PlayCardCommand("MOON", "UNL-125/219", ["OWN"])); await run.Top();
        Assert.Contains("OWN", run.State.PlayerZones["P1"].Graveyard); Assert.True(run.State.CardObjects["OWN"].ObjectGeneration > binding);
        JsonRestore(initial, run); await run.Top();
        Assert.Contains("OWN", run.State.PlayerZones["P1"].Graveyard); Assert.DoesNotContain("OWN", run.State.PlayerZones["P1"].Hand);
        Assert.Equal("THEIRS", Assert.Single(Returns(run)).Event.Payload["targetObjectId"]);
        await Export("actual-recycle-draw-discard-new-generation", initial, run);
    }

    [Fact]
    public async Task AnActualReversalChangesTheSpellControllerWithoutChangingEitherCardsOwnerDestination()
    {
        var initial = Put(Position(), "STEAL", "OGN·080/298", "P2", "HAND"); var run = new Driver(initial);
        await run.Act("P1", new PlayCardCommand("SPELL", No, ["OWN", "THEIRS"])); var item = Assert.Single(run.State.StackItems);
        await run.Act("P1", new PassPriorityCommand()); await run.Act("P2", new PlayCardCommand("STEAL", "OGN·080/298", [item.StackItemId])); await run.Top();
        var pending = run.State.PendingRuleChoice!; Assert.Equal("SPELL_CHOICE_REMAKE", pending.Request.Kind);
        await run.Act("P2", new PayCostCommand(pending.Request.Id, "RULE_REPLACEMENT", ["KEEP_ALL"]));
        Assert.Equal("P2", Assert.Single(run.State.StackItems).ControllerId); JsonRestore(initial, run); await run.Top();
        Assert.Contains("OWN", run.State.PlayerZones["P1"].Hand); Assert.Contains("THEIRS", run.State.PlayerZones["P2"].Hand);
        Assert.Contains("SPELL", run.State.PlayerZones["P1"].Graveyard); Assert.DoesNotContain("SPELL", run.State.PlayerZones["P2"].Graveyard);
        Assert.Equal("P1", Returns(run).Single(e => Equals(e.Event.Payload["targetObjectId"], "OWN")).Event.Payload["playerId"]);
        Assert.Equal("P2", Returns(run).Single(e => Equals(e.Event.Payload["targetObjectId"], "THEIRS")).Event.Payload["playerId"]);
        await Export("actual-reversal-owner-destinations", initial, run);
    }

    [Theory]
    [InlineData("targets")] [InlineData("recipient")]
    public async Task TheIndependentOriginalLogRejectsCoordinatedTargetOrRecipientForgery(string mutation)
    {
        var initial = Position(); var run = new Driver(initial); await run.Act("P1", new PlayCardCommand("SPELL", No, ["THEIRS"]));
        JsonRestore(initial, run); MatchState forged;
        if (mutation == "targets") {
            var item = Assert.Single(run.State.StackItems);
            forged = run.State with { StackItems = [item with { TargetObjectIds = ["OWN"], TargetGenerations = new Dictionary<string, long> { ["OWN"] = initial.CardObjects["OWN"].ObjectGeneration } }] };
        } else {
            await run.Top(); var card = run.State.CardObjects["THEIRS"];
            forged = run.State with {
                PlayerZones = run.State.PlayerZones.ToDictionary(p => p.Key, p => p.Key == "P1" ? p.Value with { Hand = [..p.Value.Hand, "THEIRS"] } :
                    p.Value with { Hand = p.Value.Hand.Where(id => id != "THEIRS").ToArray() }),
                ObjectLocations = new Dictionary<string, ObjectLocationState>(run.State.ObjectLocations) { ["THEIRS"] = new("P1", "HAND") },
                CardObjects = new Dictionary<string, CardObjectState>(run.State.CardObjects) { ["THEIRS"] = card with { OwnerId = "P1", ControllerId = "P1" } } };
        }
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, run.Commands(), forged, new CoreRuleEngine(), default, run.Events());
        Assert.False(replay.IsMatch); Assert.NotEmpty(replay.Errors); await Export("original-log-forged-" + mutation, initial, run);
    }

    [Fact]
    public async Task ActualReversalCanRemakeBothGraveyardTargetsWithoutChangingOwnersOrRepayingTheSpell()
    {
        var initial = Put(Position(), "STEAL", "OGN·080/298", "P2", "HAND"); var run = new Driver(initial);
        await run.Act("P1", new PlayCardCommand("SPELL", No, ["OWN", "THEIRS"])); var item = Assert.Single(run.State.StackItems);
        await run.Act("P1", new PassPriorityCommand()); await run.Act("P2", new PlayCardCommand("STEAL", "OGN·080/298", [item.StackItemId])); await run.Top();
        var paid = run.State.PendingRuleChoice!.Request.Preview!.RunePools;
        Assert.DoesNotContain(run.State.PendingRuleChoice.Request.Options, o => o.ObjectIds?.Any(id => id is "GRAVE-GEAR" or "GRAVE-SPELL" or "BANISHED" or "UNIT") == true);
        await Select("THEIRS2", 1, rejectOtherActor: true); JsonRestore(initial, run);
        await Select("OWN2", 2, rejectOtherActor: false); JsonRestore(initial, run);
        var request = run.State.PendingRuleChoice!.Request;
        await run.Act("P2", new PayCostCommand(request.Id, "RULE_REPLACEMENT", ["CONFIRM"]));
        var remade = Assert.Single(run.State.StackItems); Assert.Equal("P2", remade.ControllerId);
        Assert.Equal(new[] { "THEIRS2", "OWN2" }, remade.TargetObjectIds);
        Assert.Equal(new[] { "OWN2", "THEIRS2" }, remade.TargetGenerations!.Keys.Order().ToArray());
        Assert.Equal(paid["P1"], run.State.RunePools["P1"]); Assert.Equal(paid["P2"], run.State.RunePools["P2"]);
        JsonRestore(initial, run); await run.Top(); Assert.Contains("OWN", run.State.PlayerZones["P1"].Graveyard);
        Assert.Contains("THEIRS", run.State.PlayerZones["P2"].Graveyard); Assert.Contains("OWN2", run.State.PlayerZones["P1"].Hand);
        Assert.Contains("THEIRS2", run.State.PlayerZones["P2"].Hand);
        Assert.Equal("P1", Returns(run).Single(e => Equals(e.Event.Payload["targetObjectId"], "OWN2")).Event.Payload["playerId"]);
        Assert.Equal("P2", Returns(run).Single(e => Equals(e.Event.Payload["targetObjectId"], "THEIRS2")).Event.Payload["playerId"]);
        Assert.Single(run.Events(), e => e.Event.Kind == "SPELL_CHOICES_REMADE");
        Assert.Equal(2, run.Events().Count(e => e.Event.Kind == "COST_PAID"));
        Assert.Equal(paid["P1"], run.State.RunePools["P1"]); Assert.Equal(paid["P2"], run.State.RunePools["P2"]);
        Assert.Contains("SPELL", run.State.PlayerZones["P1"].Graveyard); HiddenFromOtherPlayerAndSpectator(run.State, "P1", "OWN2");
        HiddenFromOtherPlayerAndSpectator(run.State, "P2", "THEIRS2"); await Export("actual-reversal-remakes-both-graveyard-targets", initial, run);
        async Task Select(string id, int index, bool rejectOtherActor)
        {
            var pending = run.State.PendingRuleChoice!.Request;
            var option = Assert.Single(pending.Options, o => o.Label.Contains("重选目标 " + index + " 为", StringComparison.Ordinal) && o.ObjectIds?.Contains(id) == true);
            if (rejectOtherActor) await Reject(run, "P1", new PayCostCommand(pending.Id, "RULE_REPLACEMENT", [option.Id]));
            await run.Act("P2", new PayCostCommand(pending.Id, "RULE_REPLACEMENT", [option.Id]));
        }
    }

    [Fact]
    public async Task AReturnedUnitCanActuallyBePlayedAgainWithItsPrintedFaceAndFreshGeneration()
    {
        var initial = Position(); var run = new Driver(initial); await run.Act("P1", new PlayCardCommand("SPELL", No, ["OWN"])); await run.Top();
        var returned = run.State.CardObjects["OWN"]; Assert.Equal(1, returned.ObjectGeneration); Assert.Equal(4, returned.Power);
        await run.Act("P1", new PlayCardCommand("OWN", "VEN·093", [], Destination: "BASE"));
        if (run.State.StackItems.Count > 0) await run.Top();
        var played = run.State.CardObjects["OWN"]; Assert.Equal(2, played.ObjectGeneration); Assert.Equal("VEN·093", played.CardNo);
        Assert.Equal(4, played.Power); Assert.True(played.IsExhausted); Assert.Contains("OWN", run.State.PlayerZones["P1"].Base);
        await Export("actual-return-and-replay-unit", initial, run);
    }

    [Fact]
    public async Task AnActuallyEmpoweredAndDestroyedEnemyReturnsWithoutWardTaxAndCanBePlayedAsAFreshUnit()
    {
        var initial = Position() with { ActivePlayerId = "P2", TurnPlayerId = "P2" };
        initial = At(initial, "ENEMY", "P2", "BATTLEFIELD", "OTHER");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["ENEMY"] = OfficialGarenConquestBattlefieldCountTests.Printed("ENEMY", "VEN·122", "P2") } };
        initial = Put(initial, "STRIKE", "VEN·008", "P1", "HAND");
        for (var i = 0; i < 6; i++) initial = Put(initial, "DEATH-RUNE-" + i, i == 5 ? "UNL-R05" : "UNL-R01", "P1", "BASE");
        for (var i = 0; i < 3; i++) initial = Put(initial, "REPLAY-RUNE-" + i, "UNL-R06", "P2", "BASE");
        var run = new Driver(initial); await run.Act("P2", new ActivateAbilityCommand("ENEMY", "EMPOWER:0:0", [])); await run.Top();
        Assert.True(run.State.CardObjects["ENEMY"].IsEmpowered); await run.Act("P2", new EndTurnCommand());
        for (var i = 0; i < 6; i++) await run.Act("P1", new TapRuneCommand("DEATH-RUNE-" + i));
        await run.Act("P1", new PlayCardCommand("STRIKE", "VEN·008", ["ENEMY"],
            OptionalCosts: ["DISCARD_HAND_CARD:UNIT", "RECYCLE_RUNE:DEATH-RUNE-0", "RECYCLE_RUNE:DEATH-RUNE-1"]));
        Assert.Equal(2, run.Events().Last(e => e.Event.Kind == "COST_PAID").Event.Payload["spellshieldTaxPower"]); await run.Top();
        Assert.Contains("ENEMY", run.State.PlayerZones["P2"].Graveyard);
        var dead = run.State.CardObjects["ENEMY"]; Assert.Equal(1, dead.ObjectGeneration); Assert.Equal(0, dead.Damage);
        Assert.False(dead.IsEmpowered); Assert.Equal("P2", dead.ControllerId);
        var command = new PlayCardCommand("SPELL", No, ["ENEMY"], OptionalCosts: ["RECYCLE_RUNE:DEATH-RUNE-5"]);
        var quote = new CoreRuleEngine().PreviewPlayCard(run.State, "P1", PlayCostPreviewTests.Request(run.State, command));
        Assert.True(quote.CanPay, quote.Message); Assert.Equal(3, quote.Cost!.Mana); Assert.Equal(0, quote.Cost.GenericPower);
        await run.Act("P1", command); Assert.Equal(0, run.Events().Last(e => e.Event.Kind == "COST_PAID").Event.Payload["spellshieldTaxPower"]);
        JsonRestore(initial, run); await run.Top(); Assert.Contains("ENEMY", run.State.PlayerZones["P2"].Hand);
        Assert.Equal(2, run.State.CardObjects["ENEMY"].ObjectGeneration); HiddenFromOtherPlayerAndSpectator(run.State, "P2", "ENEMY");
        Assert.Equal("P2", Assert.Single(Returns(run)).Event.Payload["playerId"]);
        await run.Act("P1", new EndTurnCommand());
        for (var i = 0; i < 3; i++) await run.Act("P2", new TapRuneCommand("REPLAY-RUNE-" + i));
        await run.Act("P2", new PlayCardCommand("ENEMY", "VEN·122", [], Destination: "BASE"));
        if (run.State.StackItems.Count > 0) await run.Top();
        var replayed = run.State.CardObjects["ENEMY"]; Assert.Equal(3, replayed.ObjectGeneration); Assert.Equal(3, replayed.Power);
        Assert.Equal(0, replayed.Damage); Assert.False(replayed.IsEmpowered); Assert.True(replayed.IsExhausted);
        Assert.Contains("ENEMY", run.State.PlayerZones["P2"].Base); await Export("actual-empower-death-return-replay", initial, run);
    }

    [Fact]
    public async Task APrintedWardUnitInTheGraveyardDoesNotChargeAnOnFieldWardTax()
    {
        var initial = Put(Position(), "WARD-GRAVE", "VEN·043", "P2", "GRAVEYARD");
        var printed = Assert.Single((await OfficialCardCatalog.LoadDefaultAsync()).Cards, c => c.CardNo == "VEN·043");
        Assert.Contains("{{法盾}}", printed.CardEffect, StringComparison.Ordinal);
        var command = new PlayCardCommand("SPELL", No, ["WARD-GRAVE"]);
        var quote = new CoreRuleEngine().PreviewPlayCard(initial, "P1", PlayCostPreviewTests.Request(initial, command));
        Assert.True(quote.CanPay, quote.Message); Assert.Equal(0, quote.Cost!.GenericPower);
        var run = new Driver(initial); await run.Act("P1", command);
        Assert.Equal(0, Assert.Single(run.Events(), e => e.Event.Kind == "COST_PAID").Event.Payload["spellshieldTaxPower"]);
        Assert.Equal(3, run.State.RunePools["P1"].PowerByTrait[RuneTrait.Purple]);
        await run.Top(); Assert.Contains("WARD-GRAVE", run.State.PlayerZones["P2"].Hand);
        await Export("graveyard-ward-is-inactive", initial, run);
    }

    [Theory]
    [InlineData("missing-owner")] [InlineData("empty-owner")] [InlineData("foreign-owner")]
    [InlineData("wrong-graveyard")] [InlineData("wrong-controller")] [InlineData("missing-card")]
    public async Task TheNewScopeRejectsUnverifiableOwnershipInBothCoreAndPrompt(string kind)
    {
        var initial = Position(); var card = initial.CardObjects["THEIRS"]; var cards = initial.CardObjects.ToDictionary();
        if (kind == "missing-card") cards.Remove("THEIRS");
        else cards["THEIRS"] = kind switch {
            "missing-owner" => card with { OwnerId = null }, "empty-owner" => card with { OwnerId = "" },
            "foreign-owner" => card with { OwnerId = "OUTSIDE" }, "wrong-graveyard" => card with { OwnerId = "P1" },
            _ => card with { ControllerId = "P1" } };
        initial = initial with { CardObjects = cards };
        var candidate = Assert.Single(ResolutionResult.BuildPrompts(initial)["P1"].Candidates!,
            c => c.Action == CommandTypes.PlayCard && c.Sources!.Any(s => s.Id == "SPELL"));
        Assert.DoesNotContain(candidate.Targets!, t => t.Id == "THEIRS");
        var command = new PlayCardCommand("SPELL", No, ["THEIRS"]); var engine = new CoreRuleEngine();
        var quote = engine.PreviewPlayCard(initial, "P1", PlayCostPreviewTests.Request(initial, command)); Assert.False(quote.CanPay);
        var before = MatchStateHasher.Hash(initial);
        var rejected = await engine.ResolveAsync(initial, new("ven103-invalid-owner-" + kind, "P1", CommandTypes.PlayCard), command, default);
        Assert.False(rejected.Accepted); Assert.Empty(rejected.Events); Assert.Equal(before, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(MatchStateHasher.HashValue(ResolutionResult.BuildSnapshots(initial)), MatchStateHasher.HashValue(rejected.Snapshots));
        Assert.Equal(MatchStateHasher.HashValue(ResolutionResult.BuildPrompts(initial)), MatchStateHasher.HashValue(rejected.Prompts));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task TheOldFriendlyReviveScopeKeepsItsOwnLegalityAndRecipient(bool opponent)
    {
        var initial = Put(Position(), "REVIVE", "OGN·170/298", "P1", "HAND"); var run = new Driver(initial);
        var command = new PlayCardCommand("REVIVE", "OGN·170/298", [opponent ? "THEIRS" : "OWN"]);
        if (opponent) { await Reject(run, "P1", command); Assert.Contains("THEIRS", run.State.PlayerZones["P2"].Graveyard); }
        else {
            await run.Act("P1", command); await run.Top(); Assert.Contains("OWN", run.State.PlayerZones["P1"].Hand);
            var returned = Assert.Single(run.Events(), e => e.Event.Kind == "CARD_RETURNED_TO_HAND" && Equals(e.Event.Payload["sourceObjectId"], "REVIVE"));
            Assert.Equal("P1", returned.Event.Payload["playerId"]);
        }
        await Export("old-friendly-revive-" + opponent, initial, run);
    }

    private static MatchState Position()
    {
        var state = BasePosition() with { RoomId = "VEN103-ANY-GRAVEYARD" };
        var cards = state.CardObjects.ToDictionary(); var zones = state.PlayerZones.ToDictionary(); var locations = state.ObjectLocations.ToDictionary();
        void Put(string id, string no, string owner, bool hand = false) {
            cards[id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, no, owner); locations[id] = new(owner, hand ? "HAND" : "GRAVEYARD");
            zones[owner] = hand ? zones[owner] with { Hand = [..zones[owner].Hand, id] } : zones[owner] with { Graveyard = [..zones[owner].Graveyard, id] };
        }
        Put("SPELL", No, "P1", true); Put("OWN", "VEN·093", "P1"); Put("THEIRS", "VEN·122", "P2");
        Put("OWN2", "OGN·219/298", "P1"); Put("THEIRS2", "OGN·219/298", "P2");
        Put("GRAVE-GEAR", "SFD·022/221", "P1"); Put("GRAVE-SPELL", "UNL-009/219", "P2");
        cards["BANISHED"] = OfficialGarenConquestBattlefieldCountTests.Printed("BANISHED", "VEN·093", "P1");
        locations["BANISHED"] = new("P1", "BANISHED"); zones["P1"] = zones["P1"] with { Banished = [..zones["P1"].Banished, "BANISHED"] };
        return state with { CardObjects = cards, PlayerZones = zones, ObjectLocations = locations,
            RunePools = new Dictionary<string, RunePool> { ["P1"] = new(20, 0, new Dictionary<string, int> { [RuneTrait.Purple] = 4 }), ["P2"] = new(20, 20) } };
    }

    private static MatchState Put(MatchState state, string id, string no, string owner, string zone) => state with {
        CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) { [id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, no, owner) },
        ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { [id] = new(owner, zone) },
        PlayerZones = state.PlayerZones.ToDictionary(p => p.Key, p => p.Key != owner ? p.Value : zone switch {
            "HAND" => p.Value with { Hand = [..p.Value.Hand, id] }, "BASE" => p.Value with { Base = [..p.Value.Base, id] },
            "GRAVEYARD" => p.Value with { Graveyard = [..p.Value.Graveyard, id] }, _ => throw new InvalidOperationException(zone) }) };
    private static MatchState RemoveDeck(MatchState state, string owner)
    {
        var removed = state.PlayerZones[owner].MainDeck.ToHashSet(StringComparer.Ordinal);
        return state with { CardObjects = state.CardObjects.Where(p => !removed.Contains(p.Key)).ToDictionary(),
            ObjectLocations = state.ObjectLocations.Where(p => !removed.Contains(p.Key)).ToDictionary(),
            PlayerZones = state.PlayerZones.ToDictionary(p => p.Key, p => p.Key == owner ? p.Value with { MainDeck = [] } : p.Value) };
    }
    private static RecoveredEvent[] Returns(Driver run) => run.Events().Where(e => e.Event.Kind == "CARD_RETURNED_TO_HAND"
        && Equals(e.Event.Payload.GetValueOrDefault("sourceObjectId"), "SPELL")).ToArray();
    private static void HiddenFromOtherPlayerAndSpectator(MatchState state, string owner, string id)
    {
        foreach (var snapshot in new[] { ResolutionResult.BuildSnapshots(state)[owner == "P1" ? "P2" : "P1"], ResolutionResult.BuildSpectatorSnapshot(state) }) {
            var objects = JsonSerializer.SerializeToElement(snapshot, Json).GetProperty("players").GetProperty(owner).GetProperty("objects");
            Assert.False(objects.TryGetProperty(id, out _));
        }
        var ownObjects = JsonSerializer.SerializeToElement(ResolutionResult.BuildSnapshots(state)[owner], Json)
            .GetProperty("players").GetProperty(owner).GetProperty("objects");
        Assert.Equal(state.CardObjects[id].CardNo, ownObjects.GetProperty(id).GetProperty("cardNo").GetString());
    }
    private static async Task Reject(Driver run, string player, GameCommand command)
    {
        var raw = JsonSerializer.SerializeToNode(command, command.GetType(), Json)!.AsObject();
        raw["promptId"] = ResolutionResult.BuildPrompts(run.State)[player].PromptId; raw["snapshotTick"] = run.State.Tick;
        await run.ActRetained(new(player, "ven103-reject-" + run.Commands().Length, JsonSerializer.SerializeToElement(raw, Json)), false);
    }
    private static void JsonRestore(MatchState initial, Driver run, MatchState? observed = null)
    {
        var state = observed ?? run.State; var sequence = run.Commands().Last().CompletedEventSequence;
        var snapshots = ResolutionResult.BuildSnapshots(state); var prompts = ResolutionResult.BuildPrompts(state);
        var views = state.Seats.Keys.ToDictionary(p => p, p => new RecoveredPlayerView(p, state.Tick, sequence, snapshots[p], state.Tick, sequence, prompts[p]));
        var frame = new MatchRecoveryFrame(state.RoomId, state.Tick, sequence, run.Commands(), run.Events(), views, [], state, initial,
            MatchReplayRedactor.BuildSpectatorFrame(state.RoomId, state.Tick, sequence, [], state)) { OpeningSeed = initial.Seed };
        var decoded = JsonSerializer.Deserialize<MatchRecoveryFrame>(JsonSerializer.Serialize(frame, Json), Json)!;
        Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(decoded.AuthoritativeState!));
        Assert.Empty(MatchRecoveryValidator.Validate(decoded.RoomId, decoded.LastEventSequence, decoded.Commands, decoded.Events,
            decoded.PlayerViews, decoded.AuthoritativeState, decoded.CurrentTick, decoded.SpectatorReplayFrame));
        var restored = MatchSession.Restore(decoded, new CoreRuleEngine(), run);
        foreach (var player in state.Seats.Keys) {
            Assert.Equal(MatchStateHasher.HashValue(snapshots[player]), MatchStateHasher.HashValue(restored.SnapshotFor(player)));
            Assert.Equal(MatchStateHasher.HashValue(prompts[player]), MatchStateHasher.HashValue(restored.PromptFor(player)));
        }
    }
    private static async Task Export(string name, MatchState initial, Driver run)
    {
        await run.Replay(); JsonRestore(initial, run);
        if (Environment.GetEnvironmentVariable("RIFTBOUND_VEN103_EVIDENCE") is not { Length: > 0 } output) return;
        Directory.CreateDirectory(output);
        File.WriteAllText(Path.Combine(output, name + ".json"), JsonSerializer.Serialize(new { initial, final = run.State, commands = run.Commands(), events = run.Events(), runtime = RuntimeEvidence() }, Json));
    }
    private static MatchState At(MatchState state, string id, string player, string zone, string? field = null)
    {
        var moved = LocalDestructionRecallTests.At(state, id, player, zone, field);
        var cards = moved.CardObjects.ToDictionary();
        foreach (var battlefield in new[] { "BF", "OTHER" }) {
            var controllers = moved.ObjectLocations.Where(p => p.Value.Zone == "BATTLEFIELD" && p.Value.BattlefieldObjectId == battlefield
                && cards.TryGetValue(p.Key, out var card) && card.Tags.Contains(CardObjectTags.UnitCard))
                .Select(p => cards[p.Key].ControllerId ?? p.Value.PlayerId).Distinct(StringComparer.Ordinal).ToArray();
            cards[battlefield] = cards[battlefield] with { ControllerId = controllers.Length == 1 ? controllers[0] : null };
        }
        return moved with { CardObjects = cards };
    }

    private static MatchState BasePosition()
    {
        var state = UnitEntryPosition("VEN·093");
        return state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) {
            ["P1"] = new(40, 0, new Dictionary<string, int> { [RuneTrait.Orange] = 4, [RuneTrait.Red] = 4 }),
            ["P2"] = new(20, 20, new Dictionary<string, int> { [RuneTrait.Green] = 4 }) } };
    }
    private static MatchState UnitEntryPosition(string no)
    {
        var cards = new Dictionary<string, CardObjectState>(); var locations = new Dictionary<string, ObjectLocationState>();
        void Put(string id, string cardNo, string owner, string zone, string? field = null)
        { cards[id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, cardNo, owner); locations[id] = new(owner, zone, field); }
        Put("UNIT", no, "P1", "HAND"); Put("ALLY", "OGN·219/298", "P1", "BASE"); Put("ENEMY", "OGN·219/298", "P2", "BASE");
        Put("GEAR", "SFD·022/221", "P1", "BASE"); Put("GRAVE", "OGN·219/298", "P1", "GRAVEYARD");
        Put("BF", "OGN·278/298", "P1", "BATTLEFIELD", "BF"); Put("OTHER", "OGN·278/298", "P2", "BATTLEFIELD", "OTHER");
        cards["BF"] = cards["BF"] with { ControllerId = null }; cards["OTHER"] = cards["OTHER"] with { ControllerId = null };
        var zones = new Dictionary<string, PlayerZones>();
        foreach (var p in new[] { "P1", "P2" }) {
            var deck = Enumerable.Range(1, 3).Select(i => p + "-DRAW-" + i).ToArray(); foreach (var id in deck) Put(id, "OGN·219/298", p, "MAIN_DECK");
            zones[p] = PlayerZones.Empty with { Base = p == "P1" ? ["ALLY", "GEAR"] : ["ENEMY"], Battlefields = p == "P1" ? ["BF"] : ["OTHER"],
                Hand = p == "P1" ? ["UNIT"] : [], Graveyard = p == "P1" ? ["GRAVE"] : [], MainDeck = deck };
        }
        return new MatchState("VEN-UNIT-ENTRY", 0, 5, "P1", new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" },
            status: MatchStatuses.InProgress, readyPlayerIds: ["P1", "P2"], turnPlayerId: "P1", phase: MatchPhases.Main, timingState: TimingStates.NeutralOpen,
            playerZones: zones, cardObjects: cards, objectLocations: locations, runePools: new Dictionary<string, RunePool> { ["P1"] = new(20, 20), ["P2"] = new(20, 20) }, seed: 424242)
            with { CardPlays = new(5, [], []) };
    }
    private sealed class Driver : IMatchJournal
    {
        private readonly MatchState initial;
        private readonly List<MatchJournalEntry> entries = [];
        private readonly JsonSerializerOptions json = new(JsonSerializerDefaults.Web);
        private MatchSession session;
        internal MatchState State { get; private set; }
        internal Driver(MatchState initial) { this.initial = initial; State = initial; session = new(initial, new CoreRuleEngine(), this); }
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken token) { entries.Add(entry); return ValueTask.CompletedTask; }
        internal RecoveredCommand[] Commands() => entries.Select(e => new RecoveredCommand(e.PlayerId, e.ClientIntentId, e.CommandType, e.RawCommand,
            e.StartedTick, e.CompletedTick, e.StartedEventSequence, e.CompletedEventSequence, e.Accepted, e.ErrorMessage) { RuleInputs = e.RuleInputs }).ToArray();
        internal RecoveredEvent[] Events() => entries.SelectMany(e => e.Events.Select((v, i) => new RecoveredEvent(e.StartedEventSequence + i + 1, e.CompletedTick, i, v))).ToArray();
        internal async Task Act(string player, GameCommand command)
        {
            var raw = JsonSerializer.SerializeToNode(command, command.GetType(), json)!.AsObject();
            raw["promptId"] = session.PromptFor(player).PromptId; raw["snapshotTick"] = State.Tick;
            var wire = JsonSerializer.SerializeToElement(raw, json); command = GameCommandJsonMapper.Map(wire);
            var id = "ven103-" + entries.Count;
            var result = await session.SubmitAsync(player, id, command, wire, default);
            Assert.True(result.Accepted, result.ErrorMessage); State = result.State;
            FullGameEndToEndTests.AssertNoHiddenZoneLeak(result);
            var seq = entries[^1].CompletedEventSequence;
            var snapshots = ResolutionResult.BuildSnapshots(State); var prompts = ResolutionResult.BuildPrompts(State);
            var views = State.Seats.Keys.ToDictionary(p => p, p => new RecoveredPlayerView(p, State.Tick, seq, snapshots[p], State.Tick, seq, prompts[p]));
            var spectator = MatchReplayRedactor.BuildSpectatorFrame(State.RoomId, State.Tick, seq, [], State);
            var errors = MatchRecoveryValidator.Validate(State.RoomId, seq, Commands(), Events(), views, State, State.Tick, spectator);
            Assert.Empty(errors);
            var frame = new MatchRecoveryFrame(State.RoomId, State.Tick, seq, Commands(), Events(), views, errors, State, initial, spectator) { OpeningSeed = initial.Seed };
            session = MatchSession.Restore(frame, new CoreRuleEngine(), this);
            var count = entries.Count; var hash = MatchStateHasher.Hash(State);
            var duplicate = await session.SubmitAsync(player, id, command, wire, default);
            Assert.True(duplicate.Accepted);
            Assert.Equal(count, entries.Count); Assert.Equal(hash, MatchStateHasher.Hash(duplicate.State));
            Assert.Equal(hash, MatchStateHasher.Hash(State));
            await Replay();
        }
        internal async Task ActRetained(ConformanceCommand retained, bool expectedAccepted)
        {
            var before = MatchStateHasher.Hash(State); var snapshotsBefore = JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(State));
            var promptsBefore = JsonSerializer.Serialize(ResolutionResult.BuildPrompts(State));
            var raw = retained.Cmd.Clone(); var command = GameCommandJsonMapper.Map(raw);
            var result = await session.SubmitAsync(retained.PlayerId, retained.ClientIntentId, command, raw, default);
            Assert.Equal(expectedAccepted, result.Accepted); State = result.State;
            if (!expectedAccepted) {
                Assert.Equal(before, MatchStateHasher.Hash(State)); Assert.Empty(result.Events);
                Assert.Equal(snapshotsBefore, JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(State)));
                Assert.Equal(promptsBefore, JsonSerializer.Serialize(ResolutionResult.BuildPrompts(State)));
            }
            FullGameEndToEndTests.AssertNoHiddenZoneLeak(result);
            var sequence = entries[^1].CompletedEventSequence;
            var snapshots = ResolutionResult.BuildSnapshots(State); var prompts = ResolutionResult.BuildPrompts(State);
            var views = State.Seats.Keys.ToDictionary(p => p, p => new RecoveredPlayerView(p, State.Tick, sequence, snapshots[p], State.Tick, sequence, prompts[p]));
            var spectator = MatchReplayRedactor.BuildSpectatorFrame(State.RoomId, State.Tick, sequence, [], State);
            var errors = MatchRecoveryValidator.Validate(State.RoomId, sequence, Commands(), Events(), views, State, State.Tick, spectator);
            Assert.Empty(errors);
            var frame = new MatchRecoveryFrame(State.RoomId, State.Tick, sequence, Commands(), Events(), views, errors, State, initial, spectator) { OpeningSeed = initial.Seed };
            session = MatchSession.Restore(frame, new CoreRuleEngine(), this);
            if (expectedAccepted) {
                var count = entries.Count; var hash = MatchStateHasher.Hash(State);
                var duplicate = await session.SubmitAsync(retained.PlayerId, retained.ClientIntentId, command, raw, default);
                Assert.True(duplicate.Accepted); Assert.Equal(count, entries.Count); Assert.Equal(hash, MatchStateHasher.Hash(duplicate.State));
            }
            await Replay();
        }
        internal async Task Top() { await Act(State.PriorityPlayerId!, new PassPriorityCommand()); await Act(State.PriorityPlayerId!, new PassPriorityCommand()); }
        internal async Task Replay()
        {
            var result = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, Commands(), State, new CoreRuleEngine(), default, Events());
            Assert.True(result.IsMatch, string.Join("; ", result.Errors));
        }
    }
    private static object RuntimeEvidence() => new {
        ruleInputs = RuleInputIdentity.Current, baseDirectory = AppContext.BaseDirectory,
        catalogPath = OfficialCardCatalog.DefaultCatalogPath(),
        modules = new[] { typeof(CoreRuleEngine).Assembly, typeof(OfficialCardCatalog).Assembly, typeof(GameCommand).Assembly }
            .Select(a => new { name = a.GetName().Name, path = a.Location, mvid = a.ManifestModule.ModuleVersionId,
                sha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(a.Location))).ToLowerInvariant() }) };
}
