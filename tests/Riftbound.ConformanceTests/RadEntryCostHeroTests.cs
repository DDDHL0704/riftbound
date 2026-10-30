using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class RadEntryCostHeroTests
{
    private const string Normal = "RAD·061/167";
    private const string Alternate = "RAD·061a/167";
    public static IEnumerable<object[]> CardNumbers() => new[] { Normal, Alternate }.Select(no => new object[] { no });

    [Theory]
    [InlineData(Normal, 39557)] [InlineData(Alternate, 39558)]
    public async Task EveryOfficialPrintingPaysThreeManaAndReturnsTheChosenUnitBeforeEntering(string no, long id)
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, c => c.CardNo == no);
        Assert.Equal(id, card.Id); Assert.Equal("灵光一现", card.SubTitle);
        Assert.Equal(3, card.Energy); Assert.Null(card.ReturnEnergy); Assert.Equal(4, card.Power);
        Assert.StartsWith("让一名友方单位返回其所属的手牌，作为打出我的额外费用。", card.CardEffect);
        var game = new Driver(Position(no)); var play = Play(no);
        var quote = new CoreRuleEngine().PreviewPlayCard(game.State, "P1", PlayCostPreviewTests.Request(game.State, play));
        Assert.True(quote.CanPay, quote.Message); Assert.Equal(3, quote.Cost!.Mana); Assert.Equal(0, quote.Cost.PrintedPower);
        await game.Act("P1", play);
        Assert.Equal(RunePool.Empty, game.State.RunePools["P1"]);
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Base);
        Assert.Equal(4, game.State.CardObjects["UNIT"].Power); Assert.True(game.State.CardObjects["UNIT"].IsExhausted);
        Assert.Contains("游走", game.State.CardObjects["UNIT"].Tags);
        Assert.Equal(new[] { "ALLY" }, game.State.PlayerZones["P1"].Hand);
        CardZoneTestAssertions.RetainedOutsidePlay(game.State, "ALLY", "HAND");
        Assert.Equal(1, game.State.CardObjects["ALLY"].ObjectGeneration);
        var events = game.Events().Select(e => e.Event).ToArray();
        var returned = Assert.Single(events, e => e.Kind == "UNIT_RETURNED_TO_HAND");
        Assert.Equal("ADDITIONAL_COST", returned.Payload!["reason"]);
        Assert.Equal("ALLY", returned.Payload["targetObjectId"]);
        Assert.True(Array.IndexOf(events, returned) < Array.FindIndex(events, e => e.Kind == "UNIT_PLAYED_TO_BASE"));
        Assert.Empty(game.State.StackItems); Assert.Null(game.State.PendingPayment); await game.Replay();
    }

    public static IEnumerable<object[]> Entries() => CardNumbers().SelectMany(row =>
        new[] { "hand", "champion", "battlefield-cost" }.Select(source => new object[] { row[0], source }));

    [Theory, MemberData(nameof(Entries))]
    public async Task AnExhaustedWardUnitCanPayFromEitherFieldZoneWithoutBeingATarget(string no, string source)
    {
        var s = Position(no);
        s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
            ["ALLY"] = s.CardObjects["ALLY"] with { IsExhausted = true, Tags = [CardObjectTags.UnitCard, CardObjectTags.Spellshield] } } };
        if (source == "champion") s = s with {
            PlayerZones = new Dictionary<string, PlayerZones>(s.PlayerZones) { ["P1"] = s.PlayerZones["P1"] with { Hand = [], ChampionZone = ["UNIT"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(s.ObjectLocations) { ["UNIT"] = new("P1", "CHAMPION") } };
        if (source == "battlefield-cost") s = LocalDestructionRecallTests.At(s, "ALLY", "P1", "BATTLEFIELD", "BF");
        var game = new Driver(s); await game.Act("P1", Play(no) with { Destination = "BATTLEFIELD:BF" });
        Assert.Equal(RunePool.Empty, game.State.RunePools["P1"]);
        Assert.Contains("ALLY", game.State.PlayerZones["P1"].Hand);
        Assert.Equal("BF", game.State.ObjectLocations["UNIT"].BattlefieldObjectId);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "WARD_COST_REQUIRED");
        await game.Replay();
    }

    [Theory, MemberData(nameof(CardNumbers))]
    public async Task ReturningAnOpponentsUnitYouControlUsesItsOwnersHandAndDetachesEquipment(string no)
    {
        var s = Position(no);
        s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
            ["ALLY"] = s.CardObjects["ALLY"] with { OwnerId = "P2", ControllerId = "P1", Damage = 1 },
            ["GEAR"] = s.CardObjects["GEAR"] with { AttachedToObjectId = "ALLY" } } };
        var game = new Driver(s); await game.Act("P1", Play(no));
        Assert.Contains("ALLY", game.State.PlayerZones["P2"].Hand); Assert.Empty(game.State.PlayerZones["P1"].Hand);
        Assert.Equal("P2", game.State.CardObjects["ALLY"].ControllerId);
        CardZoneTestAssertions.RetainedOutsidePlay(game.State, "ALLY", "HAND");
        Assert.Null(game.State.CardObjects["GEAR"].AttachedToObjectId);
        Assert.Contains("GEAR", game.State.PlayerZones["P1"].Base);
        await game.Replay();
    }

    [Theory, MemberData(nameof(CardNumbers))]
    public async Task ARealUnitTokenCanPayTheReturnCostThenCeasesOutsideTheField(string no)
    {
        var s = AttackDamageDistributionTests.Spell(Position(no), "SANDCRAFT", "SFD·031/221", "P1");
        s = s with { RunePools = new Dictionary<string, RunePool>(s.RunePools) { ["P1"] = new(20, 20) } };
        var game = new Driver(s); await game.Act("P1", new PlayCardCommand("SANDCRAFT", "SFD·031/221", [])); await game.Top();
        var token = Assert.Single(game.State.CardObjects.Values, c => c.TokenFactoryCardNo == P6TokenFactoryCatalog.SandSoldierTokenCardNo).ObjectId;
        await game.Act("P1", Play(no, token));
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Base);
        Assert.False(game.State.CardObjects.ContainsKey(token)); Assert.False(game.State.ObjectLocations.ContainsKey(token));
        Assert.DoesNotContain(token, game.State.PlayerZones["P1"].Hand);
        Assert.Single(game.Events(), e => e.Event.Kind == "UNIT_RETURNED_TO_HAND" && Equals(e.Event.Payload!["targetObjectId"], token));
        await game.Replay();
    }

    [Theory, MemberData(nameof(CardNumbers))]
    public async Task ReturningTheOutsideHandHasteSourceStillRetainsTheIndependentlyPaidReadyEntry(string no)
    {
        var initial = GrantedOriginHasteTests.Position("SFD·029/221", no);
        initial = initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) {
            ["P1"] = new(4, 0, new Dictionary<string, int> { [RuneTrait.Blue] = 1 }) } };
        var game = await GrantedOriginHasteTests.Reveal(initial, "return-friendly-unit-" + no.Replace('/', '-'));
        var command = new PlayCardCommand("ATK_TOP", no, [],
            OptionalCosts: ["RETURN_FRIENDLY_UNIT:HASTE-GRANT", HasteOptionalCostNames.HasteReady]);
        var engine = new CoreRuleEngine();
        var missing = command with { OptionalCosts = [HasteOptionalCostNames.HasteReady] };
        Assert.False(engine.PreviewPlayCard(game.State, "P1", PlayCostPreviewTests.Request(game.State, missing)).CanPay);
        var quote = engine.PreviewPlayCard(game.State, "P1", PlayCostPreviewTests.Request(game.State, command));
        Assert.True(quote.CanPay, quote.Message); Assert.Equal(4, quote.Cost!.Mana); Assert.Equal(1, quote.Cost.PowerByTrait[RuneTrait.Blue]);
        await game.Act("P1", command);
        Assert.Equal(RunePool.Empty, game.State.RunePools["P1"]);
        Assert.Contains("HASTE-GRANT", game.State.PlayerZones["P1"].Hand);
        Assert.False(game.State.CardObjects["ATK_TOP"].IsExhausted);
        await RideTheWindCurrentMovementTests.Replay(initial, game);
    }

    public static IEnumerable<object[]> InvalidPlays() => CardNumbers().SelectMany(row => new[] {
        "missing", "two-units", "equipment", "enemy", "graveyard", "source", "hidden",
        "unknown", "wrong-prefix", "mana", "wrong-turn", "targets", "stale-controller", "enemy-field",
        "duplicate", "no-controller", "stale-location"
    }.Select(branch => new object[] { row[0], branch }));

    [Theory, MemberData(nameof(InvalidPlays))]
    public async Task IllegalAdditionalCostsTargetsPaymentAndTimingPreserveEveryStateAndView(string no, string branch)
    {
        var s = Position(no); var play = Play(no);
        play = branch switch {
            "missing" => play with { OptionalCosts = [] },
            "duplicate" => play with { OptionalCosts = ["RETURN_FRIENDLY_UNIT:ALLY", "RETURN_FRIENDLY_UNIT:ALLY"] },
            "two-units" => play with { OptionalCosts = ["RETURN_FRIENDLY_UNIT:ALLY", "RETURN_FRIENDLY_UNIT:ENEMY"] },
            "equipment" => Play(no, "GEAR"), "enemy" => Play(no, "ENEMY"), "graveyard" => Play(no, "GRAVE"),
            "source" => Play(no, "UNIT"), "unknown" => Play(no, "UNKNOWN"),
            "wrong-prefix" => play with { OptionalCosts = ["RETURN_FRIENDLY_EQUIPMENT:GEAR"] },
            "targets" => play with { TargetObjectIds = ["ALLY"] },
            "enemy-field" => play with { Destination = "BATTLEFIELD:OTHER" }, _ => play };
        if (branch == "hidden") s = LocalDestructionRecallTests.At(s, "ALLY", "P1", "BATTLEFIELD", "BF");
        if (branch is "hidden" or "stale-controller" or "no-controller") s = s with {
            CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) { ["ALLY"] = s.CardObjects["ALLY"] with {
                IsFaceDown = branch == "hidden", ControllerId = branch == "no-controller" ? null : branch == "stale-controller" ? "P2" : "P1" } } };
        if (branch == "stale-location") s = s with { ObjectLocations = new Dictionary<string, ObjectLocationState>(s.ObjectLocations) {
            ["ALLY"] = new("P1", "GRAVEYARD") } };
        if (branch == "mana") s = s with { RunePools = new Dictionary<string, RunePool>(s.RunePools) { ["P1"] = new(2, 20) } };
        if (branch == "wrong-turn") s = s with { TurnPlayerId = "P2", ActivePlayerId = "P2" };
        var before = MatchStateHasher.Hash(s);
        var views = JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(s)); var prompts = JsonSerializer.Serialize(ResolutionResult.BuildPrompts(s));
        Assert.False(new CoreRuleEngine().PreviewPlayCard(s, "P1", PlayCostPreviewTests.Request(s, play)).CanPay);
        var result = await new CoreRuleEngine().ResolveAsync(s, new("invalid-rad-entry-cost", "P1", play.CmdType), play, default);
        Assert.False(result.Accepted); Assert.Empty(result.Events); Assert.Equal(before, MatchStateHasher.Hash(result.State));
        Assert.Equal(views, JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(result.State)));
        Assert.Equal(prompts, JsonSerializer.Serialize(ResolutionResult.BuildPrompts(result.State)));
    }

    [Theory, MemberData(nameof(CardNumbers))]
    public void TheComposerOffersOnlyVisibleFriendlyUnitsAsCostsAndHidesAnUnavailablePlay(string no)
    {
        var s = Position(no);
        var command = new PlayCardCommand("UNIT", no, [], Destination: "BASE");
        Assert.False(new CoreRuleEngine().PreviewPlayCard(s, "P1", PlayCostPreviewTests.Request(s, command)).CanPay);
        var prompt = JsonSerializer.SerializeToElement(ResolutionResult.BuildPrompts(s)["P1"], new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var requirement = prompt.GetProperty("candidates").EnumerateArray().Single(c => c.GetProperty("action").GetString() == "PLAY_CARD")
            .GetProperty("metadata").GetProperty("sourceRequirements").EnumerateArray().Single(r => r.GetProperty("sourceObjectId").GetString() == "UNIT");
        Assert.True(requirement.GetProperty("composable").GetBoolean());
        var choice = Assert.Single(requirement.GetProperty("optionalCostChoices").EnumerateArray());
        Assert.Equal("RETURN_FRIENDLY_UNIT:ALLY", choice.GetProperty("id").GetString());
        Assert.Contains("返回友方单位", choice.GetProperty("label").GetString());
        s = s with {
            PlayerZones = new Dictionary<string, PlayerZones>(s.PlayerZones) { ["P1"] = s.PlayerZones["P1"] with {
                Base = ["GEAR"], Hand = ["UNIT", "ALLY"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(s.ObjectLocations) { ["ALLY"] = new("P1", "HAND") } };
        Assert.DoesNotContain(ResolutionResult.BuildPrompts(s)["P1"].Candidates!.Where(c => c.Action == CommandTypes.PlayCard)
            .SelectMany(c => c.Sources ?? []), source => source.Id == "UNIT");
    }

    [Theory, MemberData(nameof(CardNumbers))]
    public async Task ThePrintedRoamPermissionAllowsBattlefieldToBattlefieldMovement(string no)
    {
        var s = Position(no);
        s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
            ["OTHER"] = s.CardObjects["OTHER"] with { ControllerId = "P1" } },
            UntilEndOfTurnEffects = [BattlefieldTaskMarkers.SpellDuelCompleted("BF"), BattlefieldTaskMarkers.SpellDuelCompleted("OTHER")] };
        // Keep the entry and ready spell in one journal so recovery sees every command.
        s = OfficialCopyIdentityTests.AddSpell(s, "READY", "VEN·150");
        s = s with { RunePools = new Dictionary<string, RunePool>(s.RunePools) {
            ["P1"] = new(6, 0, new Dictionary<string, int> { [RuneTrait.Blue] = 1 }) } };
        var game = new Driver(s); await game.Act("P1", Play(no) with { Destination = "BATTLEFIELD:BF" });
        await game.Act("P1", new PlayCardCommand("READY", "VEN·150", ["UNIT"])); await game.Top();
        Assert.Equal(RunePool.Empty, game.State.RunePools["P1"]);
        await game.Act("P1", new MoveUnitCommand("UNIT", "BATTLEFIELD:BF", "BATTLEFIELD:OTHER", [], ["UNIT"]));
        Assert.Equal("OTHER", game.State.ObjectLocations["UNIT"].BattlefieldObjectId);
        Assert.True(game.State.CardObjects["UNIT"].IsExhausted); await game.Replay();
    }

    [Theory, MemberData(nameof(CardNumbers))]
    public Task EveryPrintingCanBeSubmittedInAChinaStandardDeck(string no)
        => new RadHeroReprintTests().EveryPrintingCanBeSubmittedInAChinaStandardDeck(no);

    private static MatchState Position(string no)
    {
        var s = VenUnitEntryBasicTests.Position(no);
        return s with { RunePools = new Dictionary<string, RunePool>(s.RunePools) { ["P1"] = new(3, 0) } };
    }

    private static PlayCardCommand Play(string no, string returned = "ALLY")
        => new("UNIT", no, [], OptionalCosts: ["RETURN_FRIENDLY_UNIT:" + returned], Destination: "BASE");
}
