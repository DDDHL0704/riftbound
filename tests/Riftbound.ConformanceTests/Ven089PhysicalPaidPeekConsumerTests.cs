using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;
using Common = Riftbound.ConformanceTests.Ven001PhysicalThresholdConsumerTests;

namespace Riftbound.ConformanceTests;

public sealed class Ven089PhysicalPaidPeekConsumerTests
{
    internal const string No = "VEN·089";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    [Theory]
    [InlineData("VEN·124", true)] [InlineData("VEN·124", false)]
    [InlineData("VEN·137", true)] [InlineData("VEN·137", false)]
    public async Task SevenActualManaAndOrangePowerPrivatelySelectThenPlayBeforeOptionalIndependentEmpower(string selectedNo, bool create)
    {
        var initial = Position(selectedNo); var game = new Driver(initial);
        try
        {
            await Open(game); var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
            Assert.Equal(initial.PlayerZones["P1"].MainDeck.Take(5), choice.ContextObjectIds); Assert.Equal(0, choice.RequiredCount);
            Assert.Equal(new[] { "PEEK-0", "PEEK-2", "PEEK-3" }, choice.LegalObjectIds);
            AssertPrivateFive(game);
            if (selectedNo == "VEN·124" && create)
            {
                await Common.Reject(game, "P2", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["PEEK-0"]), "ven089-other-player-private-choice");
                await Common.Reject(game, "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["P1-DRAW-1"]), "ven089-outside-five");
                await Common.Reject(game, "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["PEEK-1"]), "ven089-spell-not-permanent");
            }
            await Select(game, "PEEK-0", create);
            Assert.Equal(0, game.State.RunePools["P1"].Mana); Assert.Equal(0, game.State.RunePools["P1"].TotalPower);
            var before = game.Events().Length; await game.Act("P1", new PlayCardCommand("PEEK-0", selectedNo, []));
            AssertPlayed(game, selectedNo, create);
            var payment = Assert.Single(game.Events().Skip(before), e => e.Event.Kind == "COST_PAID");
            Assert.Equal(0, payment.Event.Payload["totalManaCost"]); Assert.Equal(0, payment.Event.Payload["totalPowerCost"]);
            Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "UNIT_DESTROYED"); Assert.Contains("ALLY", game.State.PlayerZones["P1"].Base);
            if (create) await game.Top();
            Assert.Equal(create, game.State.CardObjects["PEEK-0"].IsEmpowered);
            Assert.Equal(selectedNo == "VEN·124" ? create ? 5 : 3 : 0, Might(game));
            Assert.Equal(create ? 1 : 0, game.Events().Count(e => e.Event.Kind == "OBJECT_EMPOWERED"));
            Assert.Null(game.State.CardObjects["PEEK-0"].AttachedToObjectId);
            Assert.Equal(12, OwnedRunes(game.State)); Assert.Equal(7, Common.Runes(game.State).Length); Assert.Equal(5, game.State.PlayerZones["P1"].RuneDeck.Count);
            Assert.Empty(game.State.StackItems); await game.Replay();
        }
        finally { await Save("actual-selected-" + selectedNo.Replace('·', '-') + "-" + create, initial, game); }
    }

    [Fact]
    public async Task NeitherSevenManaWithoutOrangeNorSixWithOrangePaysAndARejectedIntentCannotSpendTheLaterTap()
    {
        var initial = Position("VEN·124"); var game = new Driver(initial);
        try
        {
            foreach (var id in Common.Runes(game.State).Take(6)) await game.Act("P1", new TapRuneCommand(id));
            await game.Act("P1", new RecycleRuneCommand("PEEK-RUNE-0"));
            var command = new PlayCardCommand("UNIT", No, []); var raw = JsonSerializer.SerializeToNode(command, Json)!.AsObject();
            raw["promptId"] = game.PromptFor("P1").PromptId; raw["snapshotTick"] = game.State.Tick;
            var retained = new ConformanceCommand("P1", "ven089-six-plus-orange", JsonSerializer.SerializeToElement(raw, Json));
            await game.ActRetained(retained, false); Assert.Equal(ErrorCodes.InsufficientCost, game.LastRetainedResult!.ErrorCode);
            Assert.Equal(6, game.State.RunePools["P1"].Mana); Assert.Equal(1, game.State.RunePools["P1"].PowerByTrait[RuneTrait.Orange]);
            await game.Act("P1", new TapRuneCommand("PEEK-RUNE-6")); await game.ActRetained(retained, false);
            Assert.Equal(7, game.State.RunePools["P1"].Mana); Assert.Empty(game.State.StackItems);
            await game.Act("P1", command); Assert.Equal(0, game.State.RunePools["P1"].Mana); Assert.Equal(0, game.State.RunePools["P1"].TotalPower);
            await game.Top(); var choice = game.State.PendingCardChoice!;
            await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [])); await Decide(game, false);
            Assert.Empty(game.State.StackItems); await game.Replay();
        }
        finally { await Save("actual-six-and-orange-cached-intent", initial, game); }

        var onlyMana = new Driver(initial);
        try
        {
            foreach (var id in Common.Runes(onlyMana.State).Take(7)) await onlyMana.Act("P1", new TapRuneCommand(id));
            await Common.Reject(onlyMana, "P1", new PlayCardCommand("UNIT", No, []), "ven089-seven-without-orange");
            Assert.Equal(ErrorCodes.InsufficientCost, onlyMana.LastRetainedResult!.ErrorCode); Assert.Equal(7, onlyMana.State.RunePools["P1"].Mana);
            Assert.Equal(0, onlyMana.State.RunePools["P1"].TotalPower); Assert.Contains("UNIT", onlyMana.State.PlayerZones["P1"].Hand); await onlyMana.Replay();
        }
        finally { await Save("actual-seven-without-orange", initial, onlyMana); }
    }

    [Fact]
    public async Task TheFiveManaReductionKeepsSelectedOrangePowerAndOnlyItsRealPaymentPermitsPlay()
    {
        var initial = Position("VEN·070"); var game = new Driver(initial);
        try
        {
            await Open(game); await Select(game, "PEEK-0", true);
            var play = new PlayCardCommand("PEEK-0", "VEN·070", []);
            await Common.Reject(game, "P1", play, "ven089-selected-missing-orange");
            Assert.Equal(ErrorCodes.InsufficientCost, game.LastRetainedResult!.ErrorCode); Assert.Equal("STACK", game.State.ObjectLocations["PEEK-0"].Zone);
            Assert.False(game.State.CardObjects["PEEK-0"].IsEmpowered); Assert.Equal(0, game.State.RunePools["P1"].TotalPower);
            await game.Act("P1", new RecycleRuneCommand("PEEK-RUNE-1")); Assert.Equal(1, game.State.RunePools["P1"].PowerByTrait[RuneTrait.Orange]);
            var before = game.Events().Length; await game.Act("P1", play); AssertPlayed(game, "VEN·070", true);
            var payment = Assert.Single(game.Events().Skip(before), e => e.Event.Kind == "COST_PAID");
            Assert.Equal(0, payment.Event.Payload["totalManaCost"]); Assert.Equal(1, payment.Event.Payload["totalPowerCost"]);
            Assert.Equal(0, game.State.RunePools["P1"].Mana); Assert.Equal(0, game.State.RunePools["P1"].TotalPower); Assert.Equal(4, Might(game));
            await game.Top(); Assert.True(game.State.CardObjects["PEEK-0"].IsEmpowered); Assert.Equal(6, Might(game));
            Assert.Equal(12, OwnedRunes(game.State)); Assert.Equal(6, Common.Runes(game.State).Length); Assert.Equal(6, game.State.PlayerZones["P1"].RuneDeck.Count); await game.Replay();
        }
        finally { await Save("actual-selected-printed-orange", initial, game); }
    }

    [Fact]
    public async Task ASevenManaSelectedUnitStillNeedsTwoActualManaAfterTheFiveManaReduction()
    {
        var initial = Position("OGN·075/298", 12); var game = new Driver(initial);
        try
        {
            await Open(game); await Select(game, "PEEK-0", true);
            await game.Act("P1", new TapRuneCommand("PEEK-RUNE-7")); Assert.Equal(1, game.State.RunePools["P1"].Mana);
            var play = new PlayCardCommand("PEEK-0", "OGN·075/298", []);
            await Common.Reject(game, "P1", play, "ven089-selected-one-of-two-mana");
            Assert.Equal(ErrorCodes.InsufficientCost, game.LastRetainedResult!.ErrorCode); Assert.Equal(1, game.State.RunePools["P1"].Mana);
            await game.Act("P1", new TapRuneCommand("PEEK-RUNE-8")); var before = game.Events().Length;
            await game.Act("P1", play); AssertPlayed(game, "OGN·075/298", true);
            var payment = Assert.Single(game.Events().Skip(before), e => e.Event.Kind == "COST_PAID");
            Assert.Equal(2, payment.Event.Payload["totalManaCost"]); Assert.Equal(0, payment.Event.Payload["totalPowerCost"]);
            Assert.Equal(0, game.State.RunePools["P1"].Mana); Assert.True(game.State.CardObjects["PEEK-0"].IsExhausted);
            await game.Top(); Assert.True(game.State.CardObjects["PEEK-0"].IsEmpowered); Assert.Equal(6, Might(game));
            Assert.Equal(12, OwnedRunes(game.State)); Assert.DoesNotContain(game.Events(), e => e.Event.Kind is "RUNE_CALLED" or "CARD_DRAWN"); await game.Replay();
        }
        finally { await Save("actual-selected-seven-minus-five", initial, game); }
    }

    [Fact]
    public async Task AZeroManaSelectedUnitStillReturnsTheActualEquipmentAsItsMandatoryAdditionalCost()
    {
        var initial = Position("SFD·044/221"); var game = new Driver(initial);
        try
        {
            await Open(game); await Select(game, "PEEK-0", true); var play = new PlayCardCommand("PEEK-0", "SFD·044/221", []);
            await Common.Reject(game, "P1", play, "ven089-selected-missing-equipment-return");
            Assert.Contains("GEAR", game.State.PlayerZones["P1"].Base); Assert.False(game.State.CardObjects["PEEK-0"].IsEmpowered);
            var generation = game.State.CardObjects["GEAR"].ObjectGeneration; var before = game.Events().Length;
            await game.Act("P1", play with { OptionalCosts = ["RETURN_FRIENDLY_EQUIPMENT:GEAR"] }); AssertPlayed(game, "SFD·044/221", true);
            Assert.Contains("GEAR", game.State.PlayerZones["P1"].Hand); Assert.Equal(generation + 1, game.State.CardObjects["GEAR"].ObjectGeneration);
            var payment = Assert.Single(game.Events().Skip(before), e => e.Event.Kind == "COST_PAID"); Assert.Equal(0, payment.Event.Payload["totalManaCost"]);
            await game.Top(); Assert.True(game.State.CardObjects["PEEK-0"].IsEmpowered); Assert.Equal(4, Might(game));
            Assert.Equal(0, game.State.RunePools["P1"].Mana); Assert.Equal(0, game.State.RunePools["P1"].TotalPower); Assert.Equal(12, OwnedRunes(game.State)); await game.Replay();
        }
        finally { await Save("actual-selected-mandatory-equipment-return", initial, game); }
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task DecliningThePrivateSelectionRecyclesAllFiveAndNeverInventsAnEmpoweredObject(bool create)
    {
        var initial = Position("VEN·124"); var game = new Driver(initial);
        try
        {
            await Open(game); var choice = game.State.PendingCardChoice!; AssertPrivateFive(game);
            await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [])); await Decide(game, create);
            Assert.Null(game.State.PendingEffectPlay); Assert.Contains("UNIT", game.State.PlayerZones["P1"].Graveyard);
            Assert.Equal(initial.PlayerZones["P1"].MainDeck.Order(), game.State.PlayerZones["P1"].MainDeck.Order());
            if (create) { Assert.Null(Assert.Single(game.State.StackItems).DeckPlayReflexive!.Selected); await game.Top(); }
            Assert.Empty(game.State.StackItems); Assert.DoesNotContain(game.State.CardObjects.Values, c => c.IsEmpowered);
            Assert.DoesNotContain(game.Events(), e => e.Event.Kind is "OBJECT_EMPOWERED" or "CARD_DRAWN" or "BURNOUT_APPLIED" or "UNIT_PLAYED_TO_BASE");
            Assert.Equal(12, OwnedRunes(game.State)); await game.Replay();
        }
        finally { await Save("actual-decline-five-" + create, initial, game); }
    }

    internal static MatchState Position(string selectedNo, int fieldRunes = 8)
    {
        var state = VenUnitEntryBasicTests.Position(No); var cards = state.CardObjects.ToDictionary(); var locations = state.ObjectLocations.ToDictionary(); var zones = state.PlayerZones.ToDictionary();
        cards["GEAR"] = OfficialGarenConquestBattlefieldCountTests.Printed("GEAR", "VEN·137", "P1");
        var five = new[] { selectedNo, No, "VEN·124", "VEN·137", No };
        for (var i = 0; i < five.Length; i++)
        {
            var id = "PEEK-" + i; cards[id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, five[i], "P1"); locations[id] = new("P1", "MAIN_DECK");
        }
        zones["P1"] = zones["P1"] with { MainDeck = ["PEEK-0", "PEEK-1", "PEEK-2", "PEEK-3", "PEEK-4", ..zones["P1"].MainDeck] };
        for (var i = 0; i < 12; i++)
        {
            var id = "PEEK-RUNE-" + i; cards[id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, "UNL-R04", "P1"); locations[id] = new("P1", i < fieldRunes ? "BASE" : "RUNE_DECK");
            zones["P1"] = i < fieldRunes ? zones["P1"] with { Base = [..zones["P1"].Base, id] } : zones["P1"] with { RuneDeck = [..zones["P1"].RuneDeck, id] };
        }
        return state with { RoomId = "VEN089-ACTUAL-PAID-PRIVATE-FIVE", CardObjects = cards, ObjectLocations = locations, PlayerZones = zones,
            RunePools = new Dictionary<string, RunePool> { ["P1"] = RunePool.Empty, ["P2"] = RunePool.Empty } };
    }
    internal static async Task Open(Driver game)
    {
        Assert.Equal(12, OwnedRunes(game.State)); Assert.Equal(0, game.State.RunePools["P1"].Mana); Assert.Equal(0, game.State.RunePools["P1"].TotalPower);
        for (var i = 0; i < 7; i++) await game.Act("P1", new TapRuneCommand("PEEK-RUNE-" + i));
        await game.Act("P1", new RecycleRuneCommand("PEEK-RUNE-0")); Assert.Equal(7, game.State.RunePools["P1"].Mana); Assert.Equal(1, game.State.RunePools["P1"].PowerByTrait[RuneTrait.Orange]);
        var before = game.Events().Length; await game.Act("P1", new PlayCardCommand("UNIT", No, []));
        var payment = Assert.Single(game.Events().Skip(before), e => e.Event.Kind == "COST_PAID"); Assert.Equal(7, payment.Event.Payload["totalManaCost"]); Assert.Equal(1, payment.Event.Payload["totalPowerCost"]);
        Assert.Equal(0, game.State.RunePools["P1"].Mana); Assert.Equal(0, game.State.RunePools["P1"].TotalPower); await game.Top();
    }
    internal static async Task Select(Driver game, string selected, bool create)
    {
        var choice = game.State.PendingCardChoice!; var viewed = choice.ContextObjectIds!.ToArray();
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [selected]));
        var decision = Assert.IsType<RuleChoiceContinuation>(game.State.PendingRuleChoice); Assert.Equal("DECK_REFLEXIVE_CREATE", decision.Request.Kind);
        var preview = Assert.IsType<MatchState>(decision.Request.Preview); Assert.Equal("STACK", preview.ObjectLocations[selected].Zone); Assert.False(preview.CardObjects[selected].IsEmpowered);
        await Decide(game, create); Assert.Contains("UNIT", game.State.PlayerZones["P1"].Graveyard);
        Assert.Equal(5, game.State.PendingEffectPlay!.ManaReduction); Assert.Equal(selected, Assert.Single(game.State.PendingEffectPlay.Sources).Key);
        Assert.Equal(2, game.State.CardObjects[selected].ObjectGeneration); Assert.False(game.State.CardObjects[selected].IsEmpowered);
        Assert.All(viewed.Where(id => id != selected), id => Assert.Contains(id, game.State.PlayerZones["P1"].MainDeck));
        if (create) Assert.False(Assert.Single(game.State.StackItems, i => i.DeckPlayReflexive is not null).DeckPlayReflexive!.Confirmed);
    }
    internal static async Task Decide(Driver game, bool create)
    {
        var choice = game.State.PendingRuleChoice!; await game.Act("P1", new PayCostCommand(choice.Request.Id, "RULE_REPLACEMENT", [create ? "CREATE" : "DECLINE"]));
    }
    internal static void AssertPlayed(Driver game, string no, bool create)
    {
        Assert.Contains("PEEK-0", game.State.PlayerZones["P1"].Base); Assert.Equal(no, game.State.CardObjects["PEEK-0"].CardNo);
        Assert.Equal(3, game.State.CardObjects["PEEK-0"].ObjectGeneration); Assert.False(game.State.CardObjects["PEEK-0"].IsEmpowered); Assert.Null(game.State.PendingEffectPlay);
        if (create)
        {
            var ability = Assert.Single(game.State.StackItems).DeckPlayReflexive!; Assert.True(ability.Confirmed);
            Assert.Equal(new ObjectBinding("PEEK-0", 3), ability.Played); Assert.Empty(Assert.Single(game.State.StackItems).TargetObjectIds);
        }
        else Assert.Empty(game.State.StackItems);
    }
    private static void AssertPrivateFive(Driver game)
    {
        var own = JsonSerializer.Serialize(ResolutionResult.BuildPrompts(game.State)["P1"], Json);
        var other = JsonSerializer.Serialize(new { snapshot = ResolutionResult.BuildSnapshots(game.State)["P2"], prompt = ResolutionResult.BuildPrompts(game.State)["P2"] }, Json);
        foreach (var id in game.State.PendingCardChoice!.ContextObjectIds!) { Assert.Contains(id, own); Assert.DoesNotContain(id, other); }
    }
    internal static int OwnedRunes(MatchState state) => state.CardObjects.Values.Count(c => c.OwnerId == "P1" && c.Tags.Contains(CardObjectTags.RuneCard));
    internal static int Might(Driver game) => JsonSerializer.SerializeToElement(ResolutionResult.BuildSnapshots(game.State)["P1"], Json)
        .GetProperty("players").GetProperty("P1").GetProperty("objects").GetProperty("PEEK-0").GetProperty("effectivePower").GetInt32();
    internal static async Task Save(string label, MatchState initial, Driver game)
    {
        if (Environment.GetEnvironmentVariable("RIFTBOUND_VEN089_PHYSICAL_EVIDENCE") is not { Length: > 0 } folder) return;
        Directory.CreateDirectory(folder); await File.WriteAllTextAsync(Path.Combine(folder, label + ".json"), JsonSerializer.Serialize(new {
            scope = "Explicit turn-five material, normal printed No089, zero pools/exactly12 physical orange runes, actual7mana+orange, private five and selected unit/gear actually played before independent optional Empower. Actual remaining mana/printed power/mandatory return retained. No forced status/selection-result/source/profile, submitted opening, SQL/network, full applicability, latest complete L2/L3 or whole-card claim. Original per-command privacy/current recovery/dedup/original action-event replay retained.",
            initial, final = game.State, commands = game.Commands(), events = game.Events(), ruleInputs = RuleInputIdentity.Current }, Json));
    }
}
