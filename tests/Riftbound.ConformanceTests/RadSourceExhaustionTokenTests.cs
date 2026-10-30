using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class RadSourceExhaustionTokenTests
{
    private const string Card = "RAD·138/167";
    public static IEnumerable<object[]> Printings() => new object[][] {
        [Card, 39645L], ["RAD·138a/167", 39646L], ["RAD·SP5/005", 39724L] };
    private static CardObjectState[] Minions(MatchState s) => s.CardObjects.Values
        .Where(c => c.TokenFactoryCardNo is not null && c.Tags.Contains(CardObjectTags.MinionTokenFamily)).ToArray();
    private static MatchState Board(string no = Card, bool copied = false)
    {
        var s = ArmamentMightTests.Position(unit: no, power: 1, gear: copied ? "SFD·059/221" : "SFD·022/221", attached: copied);
        return s with { RoomId = "RAD-SOURCE-EXHAUSTION-TOKENS", CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
            ["HOST"] = OfficialGarenConquestBattlefieldCountTests.Printed("HOST", no, "P1") } };
    }
    private static int Might(MatchState s, string id = "HOST") => CurrentPowerTargetTests.Snapshot(s)
        .GetProperty("players").GetProperty("P1").GetProperty("objects").GetProperty(id).GetProperty("effectivePower").GetInt32();
    private static Task Move(Driver game, params string[] ids) => game.Act("P1",
        new MoveUnitCommand(ids[0], "BASE", "BATTLEFIELD:F", SourceObjectIds: ids));

    [Theory, MemberData(nameof(Printings))]
    public async Task EachPrintingPaysItsYellowCostAndDefaultExhaustedEntryEarnsOnlyOnePlayTrigger(string no, long id)
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, c => c.CardNo == no);
        Assert.Equal(id, card.Id); Assert.Equal("星火相随", card.SubTitle); Assert.Equal(5, card.Energy); Assert.Equal(1, card.Power);
        Assert.Equal(1, card.ReturnEnergy); Assert.Equal([RuneTrait.Yellow], card.CardColorList);
        var s = VenUnitEntryBasicTests.Position(no);
        s = s with { RunePools = new Dictionary<string, RunePool>(s.RunePools) {
            ["P1"] = new(5, 0, new Dictionary<string, int> { [RuneTrait.Yellow] = 1 }) } };
        var game = new Driver(s); await game.Act("P1", new PlayCardCommand("UNIT", no, [], Destination: "BASE"));
        Assert.Equal(0, game.State.RunePools["P1"].Mana); Assert.Equal(0, game.State.RunePools["P1"].TotalPower);
        Assert.True(game.State.CardObjects["UNIT"].IsExhausted); Assert.Empty(Minions(game.State));
        Assert.Equal("PLAY", Assert.Single(game.State.StackItems).SourceUnitTokens!.Timing); Assert.Equal(1, Might(game.State, "UNIT"));
        await game.Top(); var token = Assert.Single(Minions(game.State));
        Assert.Equal(1, token.Power); Assert.True(token.IsExhausted); Assert.Contains(token.ObjectId, game.State.PlayerZones["P1"].Base);
        Assert.Equal(2, Might(game.State, "UNIT")); Assert.Empty(game.State.StackItems);
    }

    [Theory, MemberData(nameof(Printings))]
    public async Task MissingYellowPowerRejectsTheWholePlayWithoutStateOrProjectionChanges(string no, long _)
    {
        var s = VenUnitEntryBasicTests.Position(no);
        s = s with { RunePools = new Dictionary<string, RunePool>(s.RunePools) { ["P1"] = new(5, 0) } };
        await Reject(s, new PlayCardCommand("UNIT", no, [], Destination: "BASE"));
    }

    [Theory, MemberData(nameof(Printings))]
    public async Task StandardMovementEarnsOneRespondableTokenAtTheBase(string no, long _)
    {
        var game = new Driver(Board(no)); await Move(game, "HOST");
        Assert.Equal("F", game.State.ObjectLocations["HOST"].BattlefieldObjectId); Assert.True(game.State.CardObjects["HOST"].IsExhausted);
        Assert.Empty(Minions(game.State)); Assert.Equal("EXHAUST", Assert.Single(game.State.StackItems).SourceUnitTokens!.Timing);
        await game.Top(); var token = Assert.Single(Minions(game.State));
        Assert.Contains(token.ObjectId, game.State.PlayerZones["P1"].Base); Assert.True(token.IsExhausted); Assert.Equal(2, Might(game.State));
    }

    [Fact]
    public async Task TheOldPlayedOrScoredTokenTextDoesNotTriggerFromExhaustion()
    {
        var game = new Driver(Board("VEN·109")); await Move(game, "HOST");
        Assert.Empty(game.State.StackItems); Assert.Empty(game.State.TriggerQueue);
        Assert.DoesNotContain(game.State.CardObjects.Values, c => c.TokenFactoryCardNo == "VEN·T06");
    }

    [Theory, MemberData(nameof(Printings))]
    public async Task ScoringDoesNotBecomeAPlayOrExhaustionEvent(string no, long _)
    {
        var s = OfficialHoldSequenceTests.AddUnit(OfficialHoldSequenceTests.State("OGN·278/298"), "HOST", no, "F", "P1");
        s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
            ["HOST"] = OfficialGarenConquestBattlefieldCountTests.Printed("HOST", no, "P1") } };
        var game = new Driver(s); await game.Act("P1", new PassPriorityCommand());
        Assert.Equal(1, game.State.PlayerScores["P1"]); Assert.Empty(game.State.StackItems); Assert.Empty(Minions(game.State));
    }

    [Fact]
    public async Task AGroupMoveCapturesEachSourceOnceAndIgnoresOrdinaryAllies()
    {
        var s = HandDiscardObserverTests.Put(Board(), "ALLY", "OGN·219/298", "P1", "BASE");
        var game = new Driver(s); await Move(game, "HOST", "ALLY");
        Assert.Equal("EXHAUST", Assert.Single(game.State.StackItems).SourceUnitTokens!.Timing);
        Assert.Equal(2, Might(game.State)); await game.Top(); Assert.Single(Minions(game.State)); Assert.Equal(3, Might(game.State));
    }

    [Theory]
    [InlineData("exhausted")] [InlineData("duplicate")] [InlineData("enemy")]
    public async Task IllegalMovementCannotExhaustAnyUnitOrCreateAnEarnedSkill(string failure)
    {
        var s = HandDiscardObserverTests.Put(Board(), "ALLY", "OGN·219/298", "P1", "BASE");
        if (failure == "exhausted") s = SetExhausted(s, "HOST", true);
        if (failure == "enemy") s = LocalDestructionRecallTests.At(s, "ALLY", "P2", "BASE");
        await Reject(s, new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F",
            SourceObjectIds: failure == "duplicate" ? ["HOST", "HOST"] : ["HOST", "ALLY"]));
    }

    [Fact]
    public async Task AnAdditionalExhaustionCostQueuesTheTokenAboveTheSpellItPaidFor()
    {
        var s = AttackDamageDistributionTests.Spell(Board(), "DRAW", "OGN·048/298", "P1");
        var game = new Driver(s);
        await game.Act("P1", new PlayCardCommand("DRAW", "OGN·048/298", [], OptionalCosts: ["EXHAUST_FRIENDLY_UNIT:HOST"]));
        Assert.Equal(2, game.State.StackItems.Count); Assert.Equal("EXHAUST", game.State.StackItems[^1].SourceUnitTokens!.Timing);
        Assert.Empty(Minions(game.State)); await game.Top(); Assert.Single(Minions(game.State));
        Assert.Single(game.State.StackItems, i => i.SourceObjectId == "DRAW"); await game.Top();
    }

    [Fact]
    public async Task ExhaustionIsCapturedBeforeALaterDamageInstructionDestroysTheSource()
    {
        var s = LocalDestructionRecallTests.At(Board(), "HOST", "P1", "BATTLEFIELD", "F");
        s = AttackDamageDistributionTests.Spell(s, "BURST", "OGN·123/298", "P1");
        var game = new Driver(s); await game.Act("P1", new PlayCardCommand("BURST", "OGN·123/298", [])); await game.Top();
        Assert.Contains("HOST", game.State.PlayerZones["P1"].Graveyard);
        Assert.Equal("EXHAUST", Assert.Single(game.State.StackItems).SourceUnitTokens!.Timing);
        await game.Top(); Assert.Single(Minions(game.State));
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind.StartsWith("INTERNAL_"));
    }

    [Fact]
    public async Task ExhaustingAnAlreadyExhaustedSourceDoesNotEarnAnotherSkill()
    {
        var s = AttackDamageDistributionTests.Spell(SetExhausted(Board(), "HOST", true), "BURST", "OGN·123/298", "P1");
        var game = new Driver(s); await game.Act("P1", new PlayCardCommand("BURST", "OGN·123/298", [])); await game.Top();
        Assert.Empty(game.State.StackItems); Assert.Empty(Minions(game.State));
    }

    [Fact]
    public async Task ReturningTheSourceToHandKeepsTheEarnedSkillAndHidesThePrivateSource()
    {
        var game = new Driver(AttackDamageDistributionTests.Spell(Board(), "RETURN", "OGN·104/298", "P1"));
        await Move(game, "HOST"); await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["HOST"])); await game.Top();
        Assert.Contains("HOST", game.State.PlayerZones["P1"].Hand);
        Assert.DoesNotContain("\"HOST\"", JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(game.State)["P2"].Stack));
        await game.Top(); Assert.Single(Minions(game.State));
    }

    [Fact]
    public async Task AttachedCopiedTextHasAnIndependentExhaustionTrigger()
    {
        var game = new Driver(Board(copied: true)); await Move(game, "HOST");
        var triggers = game.State.TriggerQueue.Where(t => t.SourceUnitTokens is not null).Select(t => t.TriggerId).ToArray();
        Assert.Equal(2, triggers.Length); await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: triggers));
        await game.Top(); await game.Top(); Assert.Equal(2, Minions(game.State).Length);
        Assert.Equal(5, Might(game.State)); // Each of the two text instances counts both exhausted minions.
    }

    [Fact]
    public async Task ARealCopiedTokenKeepsItsEarnedSkillAfterItCeasesToExist()
    {
        var s = OfficialCopyIdentityTests.AddSpell(Board(), "MIRROR", "UNL-200/219");
        s = (await OfficialCopyIdentityTests.Copy(s, "HOST", "MIRROR")).State;
        const string token = "MIRROR-TOKEN-001";
        s = LocalDestructionRecallTests.At(s, token, "P1", "BATTLEFIELD", "F");
        s = AttackDamageDistributionTests.Spell(s, "RETURN", "OGN·104/298", "P1");
        s = AttackDamageDistributionTests.Spell(s, "BURST", "OGN·123/298", "P1");
        var game = new Driver(s);
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["HOST"])); await game.Top();
        await game.Act("P1", new PlayCardCommand("BURST", "OGN·123/298", [])); await game.Top();
        Assert.False(game.State.CardObjects.ContainsKey(token));
        Assert.Equal(token, Assert.Single(game.State.StackItems).SourceObjectId);
        await game.Top(); Assert.Single(Minions(game.State));
    }

    [Theory]
    [InlineData("P1", "BASE", true, false, 2)]
    [InlineData("P1", "BATTLEFIELD", true, false, 2)]
    [InlineData("P1", "BASE", false, false, 1)]
    [InlineData("P2", "BASE", true, false, 1)]
    [InlineData("P1", "GRAVEYARD", true, false, 1)]
    [InlineData("P1", "HAND", true, false, 1)]
    [InlineData("P1", "BASE", true, true, 1)]
    public void MightCountsOnlyOtherFaceUpExhaustedFriendlyFieldUnits(string player, string zone, bool exhausted, bool hidden, int expected)
    {
        var s = HandDiscardObserverTests.Put(Board(), "ALLY", "OGN·219/298", player, "BASE");
        s = LocalDestructionRecallTests.At(s, "ALLY", player, zone, zone == "BATTLEFIELD" ? "F" : null);
        s = SetExhausted(SetExhausted(s, "HOST", true), "ALLY", exhausted);
        s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) { ["ALLY"] = s.CardObjects["ALLY"] with { IsFaceDown = hidden },
            ["GEAR"] = s.CardObjects["GEAR"] with { IsExhausted = true } } };
        Assert.Equal(expected, Might(s)); // Exhausted source and equipment never count.
        if (!hidden && zone is "BASE" or "BATTLEFIELD") ArmamentMightTests.PublicRecovery(s);
        s = SetExhausted(s, "ALLY", false); Assert.Equal(1, Might(s));
        s = SetExhausted(SetExhausted(s, "HOST", false), "ALLY", exhausted);
        Assert.Equal(expected, Might(s)); // The aura also applies while its source is active.
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task TheSharedOptionalTokenReplacementCanBeAcceptedOrDeclined(bool accept)
    {
        var s = HandDiscardObserverTests.Put(Board(), "ZILEAN", "UNL-086/219", "P1", "BASE");
        s = LocalDestructionRecallTests.At(s, "ZILEAN", "P1", "BATTLEFIELD", "F");
        var game = new Driver(s); await Move(game, "HOST"); await game.Top();
        Assert.Empty(Minions(game.State)); var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        Assert.Equal("TOKEN_ENTRY_REPLACEMENT", choice.ChoiceWindow);
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, accept ? ["ZILEAN"] : []));
        Assert.Equal(accept ? 2 : 1, Minions(game.State).Length);
        Assert.Equal(accept, game.State.CardObjects["ZILEAN"].UntilEndOfTurnEffects.Contains("TOKEN_ENTRY_REPLACEMENT_USED"));
    }

    [Theory]
    [InlineData("event")] [InlineData("card")]
    public async Task RecoveryRejectsForgedExhaustionQueueProvenance(string mutation)
    {
        var game = new Driver(Board(copied: true)); await Move(game, "HOST");
        Assert.Equal(2, game.State.TriggerQueue.Count);
        var first = game.State.TriggerQueue[0];
        var forged = mutation == "event" ? first with { TriggeredByEventKind = "CARD_DRAWN" }
            : first with { SourceCardNo = "VEN·109" };
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(game.State with {
            TriggerQueue = [forged, ..game.State.TriggerQueue.Skip(1)] }), e => e.Contains("source unit token"));
    }

    [Theory]
    [InlineData("generation")] [InlineData("face")] [InlineData("tick")] [InlineData("controller")]
    [InlineData("timing")] [InlineData("battlefield")] [InlineData("factory")]
    public async Task RecoveryRejectsForgedCapturedSourceIdentity(string mutation)
    {
        var game = new Driver(Board()); await Move(game, "HOST");
        var item = Assert.Single(game.State.StackItems); var c = item.SourceUnitTokens!;
        var changed = mutation switch {
            "generation" => c with { Source = c.Source with { Generation = c.Source.Generation + 1 } },
            "face" => c with { FaceRevision = c.FaceRevision + 1 },
            "tick" => c with { CapturedTick = game.State.Tick + 1 },
            "controller" => c with { ControllerId = "P2" },
            "timing" => c with { Timing = "SCORE" },
            "battlefield" => c with { BattlefieldId = "F" },
            _ => c with { TokenFactoryCardNo = "FORGED_TOKEN" }
        };
        Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(game.State with { StackItems = [item with { SourceUnitTokens = changed }] }));
    }

    private static MatchState SetExhausted(MatchState s, string id, bool value) => s with {
        CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) { [id] = s.CardObjects[id] with { IsExhausted = value } } };
    private static async Task Reject(MatchState s, GameCommand command)
    {
        var hash = MatchStateHasher.Hash(s); var views = JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(s));
        var prompts = JsonSerializer.Serialize(ResolutionResult.BuildPrompts(s));
        var r = await new CoreRuleEngine().ResolveAsync(s, new("invalid-source-exhaustion", "P1", command.CmdType), command, default);
        Assert.False(r.Accepted); Assert.Empty(r.Events); Assert.Equal(hash, MatchStateHasher.Hash(r.State));
        Assert.Equal(views, JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(r.State)));
        Assert.Equal(prompts, JsonSerializer.Serialize(ResolutionResult.BuildPrompts(r.State)));
    }
}
