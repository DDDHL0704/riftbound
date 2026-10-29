using System.Reflection;
using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class RadControlledBattlefieldEntryTests
{
    private const string Text = "你打出到你所控制战场的单位以活跃状态进场。";
    public static IEnumerable<object[]> Printings() => new object[][] {
        ["RAD·149/167", 39657L], ["RAD·173*/167", 39686L], ["RAD·173/167", 39687L]
    };
    public static IEnumerable<object[]> CardNumbers() => Printings().Select(row => new[] { row[0] });
    public static IEnumerable<object[]> Destinations() => CardNumbers().SelectMany(row =>
        new[] { "BASE", "BATTLEFIELD:BF", "BATTLEFIELD:OTHER" }.Select(destination => new[] { row[0], destination }));
    public static IEnumerable<object[]> InvalidPlays() => CardNumbers().SelectMany(row =>
        new[] { "enemy-field", "uncontrolled-field", "mana", "targets", "wrong-turn" }.Select(branch => new[] { row[0], branch }));
    public static IEnumerable<object[]> Sources() => CardNumbers().SelectMany(row =>
        new[] { "exhausted", "face-down", "other-controller", "outside-legend-zone" }.Select(branch => new[] { row[0], branch }));
    public static IEnumerable<object[]> ControlStates() => CardNumbers().SelectMany(row =>
        new[] { "P1", "P2" }.Select(controller => new[] { row[0], controller }));
    public static IEnumerable<object[]> TokenLocations() => CardNumbers().SelectMany(row =>
        new[] { "base", "controlled", "enemy", "uncontrolled" }.Select(location => new[] { row[0], location }));

    private static MatchState WithLegend(MatchState state, string no, string player = "P1")
        => state with {
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["RIVEN"] = OfficialGarenConquestBattlefieldCountTests.Printed("RIVEN", no, player) },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["RIVEN"] = new(player, "LEGEND") },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                [player] = state.PlayerZones[player] with { LegendZone = ["RIVEN"] } }
        };

    private static MatchState WithBattlefieldOccupant(MatchState state, string battlefield, string controller)
    {
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            [battlefield] = state.CardObjects[battlefield] with { ControllerId = controller },
            ["ANCHOR"] = OfficialGarenConquestBattlefieldCountTests.Printed("ANCHOR", "OGN·219/298", controller) } };
        return LocalDestructionRecallTests.At(state, "ANCHOR", controller, "BATTLEFIELD", battlefield);
    }

    [Theory]
    [MemberData(nameof(Printings))]
    public async Task EveryOfficialPrintingParsesTheControlledBattlefieldPlayRequirement(string no, long id)
    {
        var catalog = await OfficialCardCatalog.LoadRuntimeAsync();
        var card = Assert.Single(catalog.Cards, c => c.CardNo == no);
        Assert.Equal(id, card.Id); Assert.Equal("放逐之刃", card.CardName); Assert.Equal("锐雯", card.Hero);
        Assert.Equal("传奇", card.CardCategoryName); Assert.Equal(Text, card.CardEffect);
        Assert.Equal(new[] { "green", "purple" }, card.CardColorList.Order());
        var plays = CardBehaviorRegistry.GetAll().Select(b => new ImplementedCardBehavior(
            b.CardNo, b.EffectKind, b.DisplayName, CardBehaviorRegistry.TriggerEffectKinds(b))).ToArray();
        var implemented = OfficialRuleDomainBehaviorCatalog.MergeWithNonPlayCardDomains(catalog.Cards, plays);
        var spec = Assert.Single(BehaviorSpecCatalogBuilder.Build(catalog.Cards, FunctionalUnitBuilder.Build(catalog.Cards), implemented), s => s.CardNo == no);
        Assert.Equal(BehaviorImplementationStatuses.Implemented, spec.Status);
        var ability = Assert.Single(spec.StaticAbilities);
        Assert.Equal(StaticAbilityKinds.FriendlyUnitsEnterReady, ability.Kind);
        Assert.True(ability.RequiresControlledBattlefieldPlay);
        Assert.Equal(BehaviorImplementationStatuses.Implemented, ability.Status);
    }

    [Theory]
    [MemberData(nameof(Destinations))]
    public async Task PaidUnitEntryUsesCurrentControlIncludingAnOpponentsOwnedBattlefield(string no, string destination)
    {
        var initial = WithLegend(VenUnitEntryBasicTests.Position("OGN·219/298"), no);
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["OTHER"] = initial.CardObjects["OTHER"] with { ControllerId = "P1" } } };
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("UNIT", "OGN·219/298", [], Destination: destination));
        var ready = destination != "BASE";
        Assert.Equal(!ready, game.State.CardObjects["UNIT"].IsExhausted);
        Assert.Equal(20 - CardBehaviorRegistry.GetAll().Single(b => b.CardNo == "OGN·219/298").ManaCost, game.State.RunePools["P1"].Mana);
        Assert.DoesNotContain("UNIT", game.State.PlayerZones["P1"].Hand);
        Assert.Equal(ready ? destination[12..] : null, game.State.ObjectLocations["UNIT"].BattlefieldObjectId);
        var entry = Assert.Single(game.Events(), e => e.Event.Kind == (ready ? "UNIT_PLAYED_TO_BATTLEFIELD" : "UNIT_PLAYED_TO_BASE")).Event;
        Assert.Equal(ready, entry.Payload.ContainsKey("entryStaticAbilityKind"));
        if (ready) {
            Assert.Equal("RIVEN", entry.Payload["entryStaticAbilitySourceObjectId"]);
            Assert.Equal(no, entry.Payload["entryStaticAbilitySourceCardNo"]);
        }
        Assert.Empty(game.State.StackItems); await game.Replay();
    }

    [Theory]
    [MemberData(nameof(InvalidPlays))]
    public async Task TheLegendNeverBypassesDestinationTimingTargetsOrPaymentLegality(string no, string branch)
    {
        var state = WithLegend(VenUnitEntryBasicTests.Position("OGN·219/298"), no);
        if (branch == "uncontrolled-field") state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["BF"] = state.CardObjects["BF"] with { ControllerId = null } } };
        if (branch == "mana") state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(0, 20) } };
        if (branch == "wrong-turn") state = state with { ActivePlayerId = "P2", TurnPlayerId = "P2" };
        var command = new PlayCardCommand("UNIT", "OGN·219/298", branch == "targets" ? ["ALLY"] : [],
            Destination: branch == "enemy-field" ? "BATTLEFIELD:OTHER" : "BATTLEFIELD:BF");
        var hash = MatchStateHasher.Hash(state);
        var snapshots = JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(state));
        var prompts = JsonSerializer.Serialize(ResolutionResult.BuildPrompts(state));
        var result = await new CoreRuleEngine().ResolveAsync(state, new("invalid-riven-entry", "P1", command.CmdType), command, default);
        Assert.False(result.Accepted); Assert.Empty(result.Events); Assert.Equal(hash, MatchStateHasher.Hash(result.State));
        Assert.Equal(snapshots, JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(result.State)));
        Assert.Equal(prompts, JsonSerializer.Serialize(ResolutionResult.BuildPrompts(result.State)));
    }

    [Theory]
    [MemberData(nameof(Sources))]
    public async Task OnlyTheControllersPublicLegendAppliesAndExhaustionDoesNotDisableItsStaticAbility(string no, string branch)
    {
        var state = WithLegend(VenUnitEntryBasicTests.Position("OGN·219/298"), no);
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["RIVEN"] = state.CardObjects["RIVEN"] with {
                IsExhausted = branch == "exhausted", IsFaceDown = branch == "face-down",
                ControllerId = branch == "other-controller" ? "P2" : "P1" } } };
        if (branch == "outside-legend-zone") state = state with {
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P1"] = state.PlayerZones["P1"] with { LegendZone = [], Base = [..state.PlayerZones["P1"].Base, "RIVEN"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["RIVEN"] = new("P1", "BASE") } };
        var game = new Driver(state);
        await game.Act("P1", new PlayCardCommand("UNIT", "OGN·219/298", [], Destination: "BATTLEFIELD:BF"));
        Assert.Equal(branch != "exhausted", game.State.CardObjects["UNIT"].IsExhausted);
    }

    [Theory]
    [MemberData(nameof(CardNumbers))]
    public async Task OrdinaryMovementToAControlledBattlefieldStillExhaustsTheMovingUnit(string no)
    {
        var game = new Driver(WithLegend(VenUnitEntryBasicTests.Position("OGN·219/298"), no));
        Assert.False(game.State.CardObjects["ALLY"].IsExhausted);
        await game.Act("P1", new MoveUnitCommand("ALLY", "BASE", "BATTLEFIELD:BF", []));
        Assert.True(game.State.CardObjects["ALLY"].IsExhausted);
        Assert.Equal("BF", game.State.ObjectLocations["ALLY"].BattlefieldObjectId);
        Assert.DoesNotContain(game.Events(), e => e.Event.Payload.ContainsKey("entryStaticAbilitySourceObjectId"));
    }

    [Theory]
    [MemberData(nameof(ControlStates))]
    public async Task AnEffectReplayToItsOriginalBattlefieldRechecksCurrentControl(string no, string controller)
    {
        var state = WithLegend(VenUnitEntryBasicTests.Position("VEN·066"), no);
        state = WithBattlefieldOccupant(state, "BF", controller);
        state = LocalDestructionRecallTests.At(state, "ALLY", controller, "BATTLEFIELD", "BF");
        var game = new Driver(state);
        await game.Act("P1", new PlayCardCommand("UNIT", "VEN·066", ["ALLY"])); await game.Top();
        Assert.NotNull(game.State.PendingEffectPlay);
        await game.Act("P1", new PlayCardCommand("ALLY", "OGN·219/298", [], Destination: "BATTLEFIELD:BF"));
        Assert.Null(game.State.PendingEffectPlay);
        Assert.Equal(controller != "P1", game.State.CardObjects["ALLY"].IsExhausted);
        Assert.Equal(2, game.State.CardObjects["ALLY"].ObjectGeneration);
        Assert.Equal("BF", game.State.ObjectLocations["ALLY"].BattlefieldObjectId);
        await game.Replay();
    }

    [Theory]
    [MemberData(nameof(TokenLocations))]
    public async Task PlayedUnitTokensUseTheirActualDestinationAndCurrentController(string no, string location)
    {
        var inBase = location == "base";
        var state = WithLegend(MovementTokenTests.Position("UNL-082/219", inBase ? "BASE" : "BATTLEFIELD"), no, "P2");
        var moved = await MovementObserverAuditTests.Move(state);
        var responseState = location is "controlled" or "enemy"
            ? WithBattlefieldOccupant(moved.State, "BF", location == "controlled" ? "P2" : "P1") : moved.State;
        if (location == "uncontrolled") Assert.Null(responseState.CardObjects["BF"].ControllerId);
        var resolved = await OfficialGraveyardRecastTests.Top(responseState);
        var token = Assert.Single(MovementTokenTests.Tokens(resolved.State));
        Assert.Equal(location != "controlled", token.IsExhausted);
        Assert.Equal(inBase ? "BASE" : "BATTLEFIELD", resolved.State.ObjectLocations[token.ObjectId].Zone);
        Assert.Equal(inBase ? null : "BF", resolved.State.ObjectLocations[token.ObjectId].BattlefieldObjectId);
        OfficialGraveyardRecastTests.Restore(resolved.State);
        ArmamentMightTests.PublicRecovery(resolved.State);
    }

    [Theory]
    [MemberData(nameof(CardNumbers))]
    public async Task EachLegendPrintingCanBeSubmittedWithTheAlreadyImplementedRivenChampion(string no)
    {
        const OfficialDeckFormat format = OfficialDeckFormat.ChinaStandard20260724;
        var catalog = await OfficialCardCatalog.LoadRuntimeAsync();
        var allowed = catalog with { Cards = catalog.Cards.Where(c => ChinaStandardBanList.IsAllowed(c, format)).ToArray() };
        var legend = Assert.Single(allowed.Cards, c => c.CardNo == no);
        var runes = legend.CardColorList.SelectMany(color => Enumerable.Repeat(allowed.Cards
            .Where(c => c.CardCategory == "rune" && c.CardColorList.Contains(color)).OrderBy(c => c.CardNo, StringComparer.Ordinal).First().CardNo, 6)).ToArray();
        var builder = typeof(PreconstructedDeckCatalog).GetMethod("BuildLowCurveDeck", BindingFlags.NonPublic | BindingFlags.Static)!;
        var deck = (OfficialDecklist)builder.Invoke(null, [allowed, allowed.Cards.ToDictionary(c => c.CardNo, StringComparer.Ordinal),
            no, "VEN·041", Array.Empty<string>(), new[] { "OGN·277/298", "OGN·278/298", "OGN·294/298" }, runes])!;
        var validation = OfficialDeckValidator.Validate(deck, catalog, format);
        Assert.True(validation.IsValid, string.Join("; ", validation.Errors));
        var initial = MatchReplayInitialStateBuilder.FromSeats("rad-riven-deck-" + no,
            new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" });
        var session = new MatchSession(initial, new CoreRuleEngine(), NoopMatchJournal.Instance,
            NoopMatchPlayerStore.Instance, new MatchSessionOptions(false, format));
        var command = new SubmitDeckCommand(deck.LegendCardNo, deck.ChampionCardNo, deck.MainDeck, deck.RuneDeck, deck.Battlefields);
        var raw = JsonSerializer.SerializeToElement(command, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var submitted = await session.SubmitDeckAsync("P1", "deck", command, raw, default);
        Assert.True(submitted.Accepted, submitted.ErrorMessage);
        Assert.Equal(no, submitted.State.PlayerDecklists["P1"].LegendCardNo);
    }
}
