using System.Text.Json;
using System.Text.Json.Nodes;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.LocalDestructionRecallTests;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;

namespace Riftbound.ConformanceTests;

public sealed class AbilityCounterAuditTests
{
    internal static async Task<MatchState> PendingAzir(string counter = "SFD·045/221", bool copied = false)
    {
        var s = await AzirCounterPosition(counter, copied);
        var command = AzirMovementAuditTests.Command();
        if (copied) command = command with { AbilityInstanceId = CopiedActivatedAbilityTests.Copy(s).AbilityInstanceId };
        s = (await Act(s, "P1", command)).State;
        return (await Act(s, "P1", new PassPriorityCommand())).State;
    }

    private static async Task<MatchState> AzirCounterPosition(string counter, bool copied)
    {
        var s = copied ? await AzirMovementNativeTests.Initial("copied-attach") : AzirMovementAuditTests.AzirPosition();
        s = At(s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
            ["GEAR"] = s.CardObjects["GEAR"] with { OwnerId = "P2", ControllerId = "P2" }
        }}, "GEAR", "P2", "BATTLEFIELD", "OTHER");
        return EquipChainTests.Response(s, counter, "P2");
    }

    internal static async Task<MatchState> PendingEntry(string counter = "UNL-106/219")
    {
        var s = OfficialRevealedHandPlayTests.Position("OGN·097/298");
        s = OfficialHoldSequenceTests.AddUnit(s, "FRIEND", "OGN·001/298", "BF", "P1");
        s = EquipChainTests.Response(s, counter) with { ActivePlayerId = "P2", TurnPlayerId = "P2",
            RunePools = new Dictionary<string, RunePool> { ["P1"] = new(20, 20), ["P2"] = new(20, 20) } };
        s = (await Act(s, "P2", new PlayCardCommand("U", "OGN·097/298", ["FRIEND"]))).State;
        if (s.PriorityPlayerId != "P1") s = (await Act(s, s.PriorityPlayerId!, new PassPriorityCommand())).State;
        Assert.Equal("P1", s.PriorityPlayerId);
        Assert.True(Assert.Single(s.StackItems).SourceConfirmed);
        return s;
    }

    internal static async Task<MatchState> PendingAutomaticEntry(string counter = "UNL-106/219")
    {
        var s = EquipChainTests.Response(OfficialUnitEntryTriggerTests.Position(), counter);
        s = (await Act(s, "P1", new PlayCardCommand("C", "OGN·219/298", [], Destination: "BATTLEFIELD:BF"))).State;
        if (s.PriorityPlayerId != "P1") s = (await Act(s, s.PriorityPlayerId!, new PassPriorityCommand())).State;
        Assert.Equal("P1", s.PriorityPlayerId);
        Assert.NotNull(Assert.Single(s.StackItems).UnitEntryContext);
        return s;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActualCounterOnConfirmedEnemyArmamentReferenceLeavesAbilitySourceInPlay(bool copied)
    {
        var s = await PendingAzir(copied: copied); Restore(s); ArmamentMightTests.PublicRecovery(s);
        var item = Assert.Single(s.StackItems);
        Assert.Equal(new[] { "TARGET", "GEAR" }, item.TargetObjectIds);
        var command = new PlayCardCommand("RESPONSE", "SFD·045/221", [item.StackItemId]);
        var quote = new CoreRuleEngine().PreviewPlayCard(s, "P2", PlayCostPreviewTests.Request(s, command));
        Assert.True(quote.CanPay, quote.Message);
        var played = await Act(s, "P2", command); var done = await Top(played.State);
        Assert.Empty(done.State.StackItems); Assert.Null(done.State.PendingCardChoice);
        Assert.Equal("F", done.State.ObjectLocations["HOST"].BattlefieldObjectId);
        Assert.Equal("OTHER", done.State.ObjectLocations["TARGET"].BattlefieldObjectId);
        Assert.Equal("TARGET", done.State.CardObjects["GEAR"].AttachedToObjectId);
        Assert.DoesNotContain("HOST", done.State.PlayerZones["P1"].Graveyard);
        Assert.Contains("RESPONSE", done.State.PlayerZones["P2"].Graveyard);
        Assert.Single(done.Events, e => e.Kind == "STACK_ITEM_COUNTERED");
        Assert.DoesNotContain(done.Events, e => e.Kind is "UNIT_MOVED_TO_BATTLEFIELD" or "EQUIPMENT_ATTACHED" or "ABILITY_RESOLVED");
        Assert.Equal(s.RunePools["P1"], done.State.RunePools["P1"]);
        Assert.Equal(s.UntilEndOfTurnEffects.Append("PLAYED_SPELL_THIS_TURN:P2").Order(StringComparer.Ordinal), done.State.UntilEndOfTurnEffects);
        Restore(done.State); ArmamentMightTests.PublicRecovery(done.State);
    }

    [Fact]
    public async Task ActualRepulseCountersChosenPlayTriggerWithoutDestroyingItsSource()
    {
        var s = await PendingEntry(); Restore(s); ArmamentMightTests.PublicRecovery(s);
        var item = Assert.Single(s.StackItems);
        var command = new PlayCardCommand("RESPONSE", "UNL-106/219", ["FRIEND", item.StackItemId]);
        var quote = new CoreRuleEngine().PreviewPlayCard(s, "P1", PlayCostPreviewTests.Request(s, command));
        Assert.True(quote.CanPay, quote.Message);
        var done = await Top((await Act(s, "P1", command)).State);
        Assert.Empty(done.State.StackItems);
        Assert.Equal("BASE", done.State.ObjectLocations["U"].Zone);
        Assert.Equal(0, done.State.CardObjects["FRIEND"].UntilEndOfTurnPowerModifier);
        Assert.DoesNotContain("U", done.State.PlayerZones["P2"].Graveyard);
        Assert.Single(done.Events, e => e.Kind == "STACK_ITEM_COUNTERED");
        Restore(done.State); ArmamentMightTests.PublicRecovery(done.State);
    }

    [Theory]
    [InlineData("activated")]
    [InlineData("triggered")]
    public async Task SpellOnlyCounterPromptDoesNotOfferAnAbility(string kind)
    {
        var s = kind == "activated" ? await PendingAzir("OGN·064/298") : await PendingEntry("OGN·064/298");
        var player = s.PriorityPlayerId!; var id = s.StackItems.Single().StackItemId;
        var candidates = ResolutionResult.BuildPrompts(s)[player].Candidates!.Where(c => c.Action == CommandTypes.PlayCard);
        Assert.DoesNotContain(candidates.SelectMany(c => c.Targets ?? []), choice => choice.Id == id);
        var command = new PlayCardCommand("RESPONSE", "OGN·064/298", [id]);
        Assert.False(new CoreRuleEngine().PreviewPlayCard(s, player, PlayCostPreviewTests.Request(s, command)).CanPay);
        var rejected = await new CoreRuleEngine().ResolveAsync(s, new("spell-only-counter", player, command.CmdType), command, default);
        Assert.False(rejected.Accepted);
        Assert.Equal(MatchStateHasher.Hash(s), MatchStateHasher.Hash(rejected.State));
    }

    [Theory]
    [InlineData("UNL-106/219")]
    [InlineData("SFD·045/221")]
    public async Task AutomaticallyReferencedEnteringUnitIsNotAChosenCounterTarget(string counter)
    {
        var s = await PendingAutomaticEntry(counter);
        var item = Assert.Single(s.StackItems); Assert.Empty(item.TargetObjectIds);
        Assert.Equal("C", item.UnitEntryContext!.Entered.ObjectId);
        var command = new PlayCardCommand("RESPONSE", counter, counter == "UNL-106/219" ? ["C", item.StackItemId] : [item.StackItemId]);
        Assert.False(new CoreRuleEngine().PreviewPlayCard(s, "P1", PlayCostPreviewTests.Request(s, command)).CanPay);
        var rejected = await new CoreRuleEngine().ResolveAsync(s, new("automatic-reference-is-not-target", "P1", command.CmdType), command, default);
        Assert.False(rejected.Accepted); Assert.Equal(MatchStateHasher.Hash(s), MatchStateHasher.Hash(rejected.State));
        Restore(s); ArmamentMightTests.PublicRecovery(s);
    }

    [Fact]
    public async Task RepulseCannotSubstituteFriendlyGearForItsRequiredUnit()
    {
        var s = await PendingAzir("UNL-106/219");
        var command = new PlayCardCommand("RESPONSE", "UNL-106/219", ["GEAR", s.StackItems.Single().StackItemId]);
        var result = await new CoreRuleEngine().ResolveAsync(s, new("gear-is-not-a-unit", "P2", command.CmdType), command, default);
        Assert.False(result.Accepted); Assert.Equal(MatchStateHasher.Hash(s), MatchStateHasher.Hash(result.State));
    }

    [Theory]
    [InlineData("unknown-stack")]
    [InlineData("source-object")]
    public async Task SpectatorRecoveryRejectsForgedCounterReferences(string change)
    {
        var initial = await PendingAzir();
        var s = (await Act(initial, "P2", new PlayCardCommand("RESPONSE", "SFD·045/221", [initial.StackItems.Single().StackItemId]))).State;
        Restore(s); ArmamentMightTests.PublicRecovery(s);
        var frame = MatchReplayRedactor.BuildSpectatorFrame(s.RoomId, s.Tick, 0, [], s);
        var stack = frame.SpectatorSnapshot.Stack!.ToArray();
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var node = JsonNode.Parse(JsonSerializer.SerializeToElement(stack[^1], options).GetRawText())!;
        var reference = change == "unknown-stack" ? "COUNTER-FAKE-STACK" : "HOST";
        node["targetObjectIds"] = new JsonArray(JsonValue.Create(reference));
        stack[^1] = JsonSerializer.SerializeToElement(node, options);
        frame = frame with { SpectatorSnapshot = frame.SpectatorSnapshot with { Stack = stack } };
        var snapshots = ResolutionResult.BuildSnapshots(s); var prompts = ResolutionResult.BuildPrompts(s);
        var views = s.Seats.Keys.ToDictionary(id => id, id => new RecoveredPlayerView(id, s.Tick, 0, snapshots[id], s.Tick, 0, prompts[id]));
        var errors = MatchRecoveryValidator.Validate(s.RoomId, 0, [], [], views, s, s.Tick, frame);
        Assert.Contains("spectator replay frame snapshot stack target object ids disagree with authoritative state stack target object ids", errors);
        if (change == "unknown-stack") Assert.Contains(errors, error => error.Contains(reference) && error.Contains("missing"));
    }

    [Fact]
    public async Task LaurentCanChooseAFriendlyBaseUnitAndGainsTheCounteredSpellsPrintedManaCost()
    {
        var s = await PendingLaurent();
        var item = s.StackItems.Single();
        Assert.True(CardBehaviorRegistry.TryGetByCardNo(item.CardNo, out var spell));
        var command = new PlayCardCommand("SPELL", "SFD·206/221", ["HOST", item.StackItemId]);
        var quote = new CoreRuleEngine().PreviewPlayCard(s, "P1", PlayCostPreviewTests.Request(s, command));
        Assert.True(quote.CanPay, quote.Message);
        var done = await Top((await Act(s, "P1", command)).State);
        Assert.Equal("BASE", done.State.ObjectLocations["HOST"].Zone);
        Assert.Equal(spell.ManaCost, done.State.CardObjects["HOST"].UntilEndOfTurnPowerModifier);
        Assert.Contains("RESPONSE", done.State.PlayerZones["P2"].Graveyard);
        Assert.Contains("SPELL", done.State.PlayerZones["P1"].Graveyard);
        Restore(done.State); ArmamentMightTests.PublicRecovery(done.State);
    }

    internal static async Task<MatchState> PendingLaurent()
    {
        var s = EquipChainTests.Response(ArmamentMightTests.Position(), "UNL-061/219", "P2");
        s = AttachedRuleTextTests.WithSpell(s, "P1", "SFD·206/221") with { ActivePlayerId = "P2", TurnPlayerId = "P2" };
        s = (await Act(s, "P2", new PlayCardCommand("RESPONSE", "UNL-061/219", []))).State;
        return (await Act(s, "P2", new PassPriorityCommand())).State;
    }

    internal static async Task<MatchState> PendingSourceGone()
    {
        var s = await AzirCounterPosition("SFD·045/221", false);
        s = At(OfficialHoldSequenceTests.AddUnit(s, "DEF", "OGN·001/298", "OTHER", "P1"), "DEF", "P1", "BASE");
        s = AttachedRuleTextTests.WithSpell(s, "P1", "SFD·163/221");
        s = (await Act(s, "P1", AzirMovementAuditTests.Command())).State;
        s = (await Top((await Act(s, "P1", new PlayCardCommand("SPELL", "SFD·163/221", ["HOST", "DEF"]))).State)).State;
        if (s.PriorityPlayerId != "P2") s = (await Act(s, s.PriorityPlayerId!, new PassPriorityCommand())).State;
        Assert.Contains("HOST", s.PlayerZones["P1"].Graveyard);
        Assert.NotNull(Assert.Single(s.StackItems).AzirMovement);
        return s;
    }
}
