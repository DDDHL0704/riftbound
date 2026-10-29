using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;

namespace Riftbound.ConformanceTests;

public sealed class AttachmentModeTests
{
    internal static MatchState Position(string cardNo = "SFD·049/221")
    {
        var s = AttachedRuleTextTests.Position(cardNo, 0);
        var cards = new Dictionary<string, CardObjectState>(s.CardObjects);
        var locations = new Dictionary<string, ObjectLocationState>(s.ObjectLocations);
        foreach (var id in new[] { "MR1", "MR2", "RD1", "RD2" }) {
            cards[id] = new(id, cardNo: "OGN·007/298", ownerId: "P1", controllerId: "P1", isExhausted: true,
                tags: [CardObjectTags.RuneCard, "COLOR:red"]);
            locations[id] = new("P1", id.StartsWith("RD") ? "RUNE_DECK" : "BASE");
        }
        return s with { CardObjects = cards, ObjectLocations = locations,
            PlayerZones = new Dictionary<string, PlayerZones>(s.PlayerZones) {
                ["P1"] = s.PlayerZones["P1"] with { Base = [..s.PlayerZones["P1"].Base, "MR1", "MR2"], RuneDeck = ["RD1", "RD2", ..s.PlayerZones["P1"].RuneDeck] } } };
    }

    internal static async Task<ResolutionResult> Choose(MatchState state, params string[] ids)
    {
        var choice = state.PendingCardChoice!;
        var result = await Act(state, choice.PlayerId, new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ids));
        Assert.Equal(state.Tick + 1, result.State.Tick);
        Restore(result.State); ArmamentMightTests.PublicRecovery(result.State); return result;
    }
    [Fact]
    public async Task OfficialAttachmentTextParsesThreeModeEffectsAndPerInstanceTurnRestriction()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync();
        var card = catalog.Cards.Single(c => c.CardNo == "SFD·049/221");
        var trigger = Assert.Single(TriggerParser.Parse(card.CardEffect), t => t.Kind == "UNIT_ARMAMENT_ATTACHED_CHOOSE_UNUSED_MODE");
        var json = JsonSerializer.SerializeToElement(trigger, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Equal(3, json.GetProperty("modes").GetArrayLength());
        Assert.True(json.GetProperty("distinctModesPerTurn").GetBoolean());
    }

    [Fact]
    public async Task FirstSongAttachmentOffersOnlyOneNativeModeConfirmationBeforeResponses()
    {
        var result = await AttachedRuleTextTests.Attach(Position());
        var choice = Assert.IsType<PendingCardChoiceState>(result.State.PendingCardChoice);
        Assert.Equal("ATTACHMENT_MODE_CONFIRMATION", choice.ChoiceWindow);
        Assert.Equal(1, choice.RequiredCount); Assert.Equal(1, choice.MaxCount);
        Assert.Equal(new[] { "READY_RUNES", "CHANNEL_RUNE", "GRANT_BOON" }, choice.LegalObjectIds);
        Assert.Single(result.State.StackItems);
        Assert.Null(result.State.PriorityPlayerId);
        Assert.NotNull(result.State.CardObjects["GEAR"].AttachedRuleText);
        OfficialGraveyardRecastTests.Restore(result.State); ArmamentMightTests.PublicRecovery(result.State);
    }

    public static IEnumerable<object[]> PrintingModes() => new[] { "SFD·049/221", "RAD·179*/167", "RAD·179/167" }
        .SelectMany(no => new[] { "READY_RUNES", "CHANNEL_RUNE", "GRANT_BOON" }.Select(mode => new object[] { no, mode }));

    [Theory]
    [MemberData(nameof(PrintingModes))]
    public async Task ConfirmedModeRecordsUseAndWaitsForResponsesBeforeItsEffect(string cardNo, string mode)
    {
        var result = await AttachedRuleTextTests.Attach(Position(cardNo));
        result = await Choose(result.State, mode);
        if (mode == "READY_RUNES") result = await Choose(result.State, "MR1", "MR2");
        if (mode == "GRANT_BOON") result = await Choose(result.State, "HOST");
        Assert.Null(result.State.PendingCardChoice); Assert.NotNull(result.State.PriorityPlayerId);
        Assert.Equal(mode, Assert.Single(result.State.RuleTextModeUses!.Uses).Mode);
        Assert.True(result.State.CardObjects["MR1"].IsExhausted); Assert.Contains("RD1", result.State.PlayerZones["P1"].RuneDeck);
        Assert.DoesNotContain(CardObjectTags.Boon, result.State.CardObjects["HOST"].Tags);
        result = await Top(result.State);
        if (mode == "READY_RUNES") { Assert.False(result.State.CardObjects["MR1"].IsExhausted); Assert.False(result.State.CardObjects["MR2"].IsExhausted); }
        if (mode == "CHANNEL_RUNE") { Assert.Contains("RD1", result.State.PlayerZones["P1"].Base); Assert.True(result.State.CardObjects["RD1"].IsExhausted); }
        if (mode == "GRANT_BOON") { Assert.Contains(CardObjectTags.Boon, result.State.CardObjects["HOST"].Tags); Assert.Equal(5, result.State.CardObjects["HOST"].Power); }
        Assert.Empty(result.State.StackItems); Restore(result.State); ArmamentMightTests.PublicRecovery(result.State);
    }

    [Fact]
    public async Task ExistingCopiedAndNativeInstancesCanChooseTheSameModeIndependently()
    {
        var result = await AttachedRuleTextTests.Attach(Position());
        result = await Top((await Choose(result.State, "CHANNEL_RUNE")).State);
        var s = CopiedAttachmentObserverTests.AddSword(result.State);
        result = await EquipTestDriver.FinishAsync(await Act(s, "P1", new AssembleEquipmentCommand("SWORD", "HOST", ["ASSEMBLE_RED"])));
        Assert.Equal(2, result.State.TriggerQueue.Count);
        result = await Act(result.State, "P1", new OrderTriggersCommand(OrderedTriggerIds: result.State.TriggerQueue.Select(i => i.TriggerId).ToArray()));
        for (var index = 0; index < 2; index++) {
            Assert.Contains("READY_RUNES", result.State.PendingCardChoice!.LegalObjectIds);
            result = await Choose(result.State, "READY_RUNES"); result = await Choose(result.State, "MR1", "MR2");
        }
        Assert.Equal(3, result.State.RuleTextModeUses!.Uses.Count);
        Assert.Single(result.State.RuleTextModeUses.Uses, use => use.Origin is not null);
        Assert.True(result.State.CardObjects["MR1"].IsExhausted);
        result = await Top(result.State); Assert.Single(result.State.StackItems);
        result = await Top(result.State); Assert.Empty(result.State.StackItems);
        Restore(result.State); ArmamentMightTests.PublicRecovery(result.State);
    }

    [Theory]
    [InlineData("turn")] [InlineData("mode")] [InlineData("source")] [InlineData("duplicate")]
    public async Task RecoveryRejectsInvalidInstanceModeHistory(string change)
    {
        var result = await AttachedRuleTextTests.Attach(Position());
        result = await Choose(result.State, "CHANNEL_RUNE");
        var ledger = result.State.RuleTextModeUses!; var use = ledger.Uses[0];
        var invalid = change switch {
            "turn" => ledger with { TurnNumber = ledger.TurnNumber + 1 },
            "mode" => ledger with { Uses = [use with { Mode = "FAKE" }] },
            "source" => ledger with { Uses = [use with { Source = use.Source with { Generation = 99 } }] },
            _ => ledger with { Uses = [use, use] }
        };
        Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(result.State with { RuleTextModeUses = invalid }));
        Restore(result.State);
    }

    internal static async Task<ResolutionResult> OpenLaterAttachment(MatchState state, string id = "SWORD")
    {
        state = AttachedRuleTextTests.AddSong(state, id);
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            [id] = state.CardObjects[id] with { CardNo = "SFD·022/221" } } };
        var result = await EquipTestDriver.FinishAsync(await Act(state, "P1", new AssembleEquipmentCommand(id, "HOST", ["ASSEMBLE_RED"])));
        if (result.State.TriggerQueue.Count > 0)
            result = await Act(result.State, "P1", new OrderTriggersCommand(OrderedTriggerIds: result.State.TriggerQueue
                .OrderBy(i => i.AttachmentObserver!.CopiedInstance ? 0 : 1).Select(i => i.TriggerId).ToArray()));
        return result;
    }

    [Fact]
    public async Task UsedNativeModeIsAbsentWhileExistingCopiedInstanceKeepsItsOwnChoices()
    {
        var result = await AttachedRuleTextTests.Attach(Position());
        result = await Top((await Choose(result.State, "CHANNEL_RUNE")).State);
        result = await OpenLaterAttachment(result.State);
        Assert.False(result.State.StackItems.Single(i => i.StackItemId == result.State.PendingCardChoice!.ResolvingStackItemId).AttachmentObserver!.CopiedInstance);
        Assert.DoesNotContain("CHANNEL_RUNE", result.State.PendingCardChoice!.LegalObjectIds);
        var rejected = await new CoreRuleEngine().ResolveAsync(result.State, new("used-mode", "P1", CommandTypes.ChooseCards), new ChooseCardsCommand(result.State.PendingCardChoice.ChoiceId,
            result.State.PendingCardChoice.ChoiceWindow, ["CHANNEL_RUNE"]), default);
        Assert.False(rejected.Accepted); Assert.Equal(result.State.Tick, rejected.State.Tick);
        result = await Choose(result.State, "GRANT_BOON"); result = await Choose(result.State, "HOST");
        Assert.True(result.State.StackItems.Single(i => i.StackItemId == result.State.PendingCardChoice!.ResolvingStackItemId).AttachmentObserver!.CopiedInstance);
        Assert.Contains("CHANNEL_RUNE", result.State.PendingCardChoice!.LegalObjectIds);
        result = await Choose(result.State, "CHANNEL_RUNE");
        result = await Top(result.State); result = await Top(result.State);
        Assert.Equal(3, result.State.RuleTextModeUses!.Uses.Count); Restore(result.State);
    }

    [Fact]
    public async Task DetachAndReattachRefreshesCopiedInstanceWithoutResettingPrintedModeHistory()
    {
        var result = await AttachedRuleTextTests.Attach(Position());
        result = await Top((await Choose(result.State, "CHANNEL_RUNE")).State);
        result = await OpenLaterAttachment(result.State);
        result = await Choose(result.State, "GRANT_BOON"); result = await Choose(result.State, "HOST");
        result = await Choose(result.State, "CHANNEL_RUNE");
        var revision = result.State.CardObjects["GEAR"].AttachmentRevision;
        var s = AttachedRuleTextTests.WithSpell(result.State, "P1", "SFD·011/221");
        result = await Top((await Act(s, "P1", new PlayCardCommand("SPELL", "SFD·011/221", ["HOST", "GEAR"]))).State);
        Assert.Null(result.State.CardObjects["GEAR"].AttachedRuleText);
        Assert.Equal(3, result.State.RuleTextModeUses!.Uses.Count);
        result = await Top(result.State); result = await Top(result.State);
        result = await AttachedRuleTextTests.Attach(result.State);
        Assert.Equal(revision + 1, result.State.CardObjects["GEAR"].AttachmentRevision);
        Assert.Equal(new[] { "READY_RUNES" }, result.State.PendingCardChoice!.LegalObjectIds);
        result = await Choose(result.State, "READY_RUNES"); result = await Choose(result.State, "MR1", "MR2");
        result = await Top(result.State);
        result = await OpenLaterAttachment(result.State, "SWORD2");
        Assert.Single(result.State.StackItems);
        Assert.True(result.State.StackItems[0].AttachmentObserver!.CopiedInstance);
        Assert.Equal(new[] { "READY_RUNES", "CHANNEL_RUNE", "GRANT_BOON" }, result.State.PendingCardChoice!.LegalObjectIds);
        result = await Choose(result.State, "CHANNEL_RUNE");
        Assert.Equal(revision + 1, result.State.RuleTextModeUses!.Uses.Last().Origin!.Revision);
        result = await Top(result.State); Assert.Equal(5, result.State.RuleTextModeUses!.Uses.Count);
        Restore(result.State); ArmamentMightTests.PublicRecovery(result.State);
    }

    [Theory]
    [InlineData("legal")] [InlineData("window")] [InlineData("count")] [InlineData("context")]
    public async Task RecoveryRejectsForgedModeChoices(string change)
    {
        var result = await AttachedRuleTextTests.Attach(Position()); var choice = result.State.PendingCardChoice!;
        var invalid = change switch {
            "legal" => choice with { LegalObjectIds = ["FAKE"] },
            "window" => choice with { ChoiceWindow = "ATTACHMENT_TARGET_CONFIRMATION" },
            "count" => choice with { RequiredCount = 0 },
            _ => choice with { ContextObjectIds = ["GEAR"] }
        };
        Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(result.State with { PendingCardChoice = invalid })); Restore(result.State);
    }

    [Theory]
    [InlineData("generation")] [InlineData("targets")] [InlineData("kind")] [InlineData("receipt")]
    public async Task RecoveryRejectsForgedConfirmedModeTargets(string change)
    {
        var result = await AttachedRuleTextTests.Attach(Position());
        result = await Choose(result.State, "READY_RUNES"); result = await Choose(result.State, "MR1", "MR2");
        var item = result.State.StackItems.Single(); var context = item.AttachmentObserver!;
        var invalid = change switch {
            "generation" => item with { AttachmentObserver = context with { References = [new("MR1", 99), context.References![1]] } },
            "targets" => item with { TargetObjectIds = ["MR2", "MR1"] },
            "kind" => item with { TargetObjectIds = ["HOST", "MR2"], TargetGenerations = new Dictionary<string, long> { ["HOST"] = result.State.CardObjects["HOST"].ObjectGeneration, ["MR2"] = result.State.CardObjects["MR2"].ObjectGeneration },
                AttachmentObserver = context with { References = [new("HOST", result.State.CardObjects["HOST"].ObjectGeneration), context.References![1]] } },
            _ => item
        };
        var forged = result.State with { StackItems = [invalid] };
        if (change == "receipt") forged = forged with { RuleTextModeUses = null };
        Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(forged)); Restore(result.State);
    }

    [Theory]
    [InlineData("SFD·049/221")] [InlineData("RAD·179*/167")] [InlineData("RAD·179/167")]
    public async Task ModeWithInsufficientRequiredRuneTargetsCannotBeConfirmed(string cardNo)
    {
        var result = await AttachedRuleTextTests.Attach(AttachedRuleTextTests.Position(cardNo, 0));
        Assert.Equal(new[] { "CHANNEL_RUNE", "GRANT_BOON" }, result.State.PendingCardChoice!.LegalObjectIds);
        result = await Choose(result.State, "CHANNEL_RUNE");
        Assert.Empty(result.State.StackItems[0].TargetObjectIds);
        result = await Top(result.State); Assert.Single(result.State.RuleTextModeUses!.Uses);
        Restore(result.State); ArmamentMightTests.PublicRecovery(result.State);
    }

    [Fact]
    public async Task ContinuousSourceControllerChangeDoesNotResetItsPrintedModeUse()
    {
        var result = await AttachedRuleTextTests.Attach(Position());
        result = await Choose(result.State, "CHANNEL_RUNE");
        var s = LocalDestructionRecallTests.At(LocalDestructionRecallTests.At(result.State, "HOST", "P2", "BASE"), "GEAR", "P2", "BASE");
        s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
            ["P2-SWORD"] = new("P2-SWORD", cardNo: "SFD·022/221", ownerId: "P2", controllerId: "P2", tags: [CardObjectTags.EquipmentCard, CardEquipmentKeywordNames.Weapon]) },
            PlayerZones = new Dictionary<string, PlayerZones>(s.PlayerZones) { ["P2"] = s.PlayerZones["P2"] with { Hand = ["P2-SWORD"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(s.ObjectLocations) { ["P2-SWORD"] = new("P2", "HAND") } };
        Restore(s); ArmamentMightTests.PublicRecovery(s);
        result = await Act(s, "P1", new PassPriorityCommand());
        result = await Top((await Act(result.State, "P2", new PlayCardCommand("P2-SWORD", "SFD·022/221", ["HOST"]))).State);
        Assert.Equal(2, result.State.TriggerQueue.Count);
        result = await Act(result.State, "P2", new OrderTriggersCommand(OrderedTriggerIds: result.State.TriggerQueue
            .OrderBy(i => i.AttachmentObserver!.CopiedInstance ? 0 : 1).Select(i => i.TriggerId).ToArray()));
        Assert.Equal("P2", result.State.PendingCardChoice!.PlayerId);
        Assert.False(result.State.StackItems.Single(i => i.StackItemId == result.State.PendingCardChoice.ResolvingStackItemId).AttachmentObserver!.CopiedInstance);
        Assert.DoesNotContain("CHANNEL_RUNE", result.State.PendingCardChoice.LegalObjectIds);
        Assert.Equal(result.State.CardObjects["HOST"].ObjectGeneration, Assert.Single(result.State.RuleTextModeUses!.Uses).Source.Generation);
        Restore(result.State); ArmamentMightTests.PublicRecovery(result.State);
    }
}
