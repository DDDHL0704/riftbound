using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class OptionalEntryTargetBoundaryTests
{
    internal static readonly string[] Cards = ["SFD·013/221", "SFD·067/221", "VEN·120"];

    [Theory]
    [InlineData("SFD·013/221")]
    [InlineData("SFD·067/221")]
    [InlineData("VEN·120")]
    public async Task ARealStandbyActionKeepsTheHiddenCardInItsOwnSubzoneAndOutsideUnitTargetChoices(string no)
    {
        var initial = OptionalEntryTargetConfirmationTests.AddSpell(OptionalEntryTargetConfirmationTests.Position(no), "SECRET", "OGN·097/298");
        // A visible friendly unit maintains control of the standby battlefield.
        initial = LocalDestructionRecallTests.At(initial, "ALLY", "P1", "BATTLEFIELD", "BF");
        var driver = new Driver(initial);
        await driver.Act("P1", new HideCardCommand("SECRET", "OGN·097/298", "BATTLEFIELD:BF", ["STANDBY_A"]));
        Assert.True(driver.State.CardObjects["SECRET"].IsFaceDown);
        // The engine encodes the standby subzone as a marker on a battlefield
        // location; its printed card face is not a chosen public unit target.
        Assert.Contains(CardObjectTags.Standby, driver.State.CardObjects["SECRET"].Tags);
        Assert.Equal("BATTLEFIELD", driver.State.ObjectLocations["SECRET"].Zone);
        Assert.Equal("BF", driver.State.ObjectLocations["SECRET"].BattlefieldObjectId);
        await driver.Act("P1", new PlayCardCommand("UNIT", no, [], OptionalCosts: OptionalEntryTargetConfirmationTests.Costs(no), Destination: "BASE"));
        var choice = driver.State.PendingCardChoice!;
        Assert.DoesNotContain("SECRET", choice.LegalObjectIds); Assert.Contains("ENEMY", choice.LegalObjectIds);
        await Reject(driver.State, "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["SECRET"]));
        await driver.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ENEMY"])); await driver.Top();
        if (Environment.GetEnvironmentVariable("RIFTBOUND_OPTIONAL_ENTRY_EVIDENCE") is { Length: > 0 } root) {
            Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root, "standby-" + no.Replace('·', '-').Replace('/', '-') + ".json"), JsonSerializer.Serialize(new {
                initial, final = driver.State, commands = driver.Commands(), events = driver.Events()
            }, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));
        }
        Assert.True(driver.State.CardObjects["SECRET"].IsFaceDown);
        Assert.Contains(CardObjectTags.Standby, driver.State.CardObjects["SECRET"].Tags);
        Assert.Equal("BATTLEFIELD", driver.State.ObjectLocations["SECRET"].Zone); await driver.Replay();
    }

    [Theory]
    [InlineData("SFD·013/221")]
    [InlineData("SFD·067/221")]
    [InlineData("VEN·120")]
    public async Task PostEntryChoicesUseThePrintedUnitDomainAndRejectInvalidIntentWithoutChangingState(string no)
    {
        var driver = new Driver(FieldPosition(no));
        await driver.Act("P1", new PlayCardCommand("UNIT", no, [], OptionalCosts: OptionalEntryTargetConfirmationTests.Costs(no), Destination: "BASE"));
        var choice = Assert.IsType<PendingCardChoiceState>(driver.State.PendingCardChoice);
        var expected = no switch {
            "SFD·013/221" => new[] { "ENEMY", "FRIEND" },
            "SFD·067/221" => ["ALLY", "ENEMY", "ENEMYBASE", "FRIEND", "UNIT"],
            _ => ["ENEMY"] };
        Assert.Equal(expected, choice.LegalObjectIds.Order().ToArray());
        Assert.Equal(1, choice.RequiredCount); Assert.Equal(1, choice.MaxCount);
        foreach (var id in new[] { "GEAR", "GRAVE", "BF", "OTHER", "SECRET", "P1-DRAW-1", "P2-DRAW-1", "MISSING" }
            .Concat(no == "VEN·120" ? ["FRIEND", "ALLY", "UNIT", "ENEMYBASE"] : no == "SFD·013/221" ? ["ALLY", "UNIT", "ENEMYBASE"] : []))
            await Reject(driver.State, "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [id]));
        await Reject(driver.State, "P2", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ENEMY"]));
        await Reject(driver.State, "P1", new ChooseCardsCommand("STALE", choice.ChoiceWindow, ["ENEMY"]));
        await Reject(driver.State, "P1", new ChooseCardsCommand(choice.ChoiceId, "MAIN_DECK", ["ENEMY"]));
        await Reject(driver.State, "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, []));
        await Reject(driver.State, "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ENEMY", "ENEMY"]));
        await driver.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ENEMY"]));
        await driver.Top(); await driver.Replay();
    }

    public static IEnumerable<object[]> LegalTargets() => Cards.SelectMany(no =>
        (no == "SFD·013/221" ? new[] { "ENEMY", "FRIEND" }
            : no == "SFD·067/221" ? ["ALLY", "ENEMY", "ENEMYBASE", "FRIEND", "UNIT"] : ["ENEMY"])
        .Select(id => new object[] { no, id }));

    [Theory]
    [MemberData(nameof(LegalTargets))]
    public async Task EveryPrintedTargetDomainResolvesOnlyTheChosenUnitAndTemporaryEffectsExpire(string no, string target)
    {
        var initial = FieldPosition(no); var driver = new Driver(initial);
        await driver.Act("P1", new PlayCardCommand("UNIT", no, [], OptionalCosts: OptionalEntryTargetConfirmationTests.Costs(no), Destination: "BASE"));
        var choice = driver.State.PendingCardChoice!;
        await driver.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [target]));
        var before = driver.State.CardObjects[target];
        await driver.Top();
        Assert.Equal(before.Damage + (no == "SFD·013/221" ? 2 : 0), driver.State.CardObjects[target].Damage);
        Assert.Equal(before.Power - (no == "SFD·067/221" ? 2 : 0), driver.State.CardObjects[target].Power);
        Assert.Equal(no == "VEN·120", driver.State.CardObjects[target].UntilEndOfTurnEffects.Contains("STUNNED"));
        foreach (var other in new[] { "ALLY", "ENEMY", "ENEMYBASE", "FRIEND", "UNIT" }.Where(id => id != target)) {
            Assert.Equal(initial.CardObjects[other].Power, driver.State.CardObjects[other].Power);
            Assert.Equal(0, driver.State.CardObjects[other].Damage);
            Assert.DoesNotContain("STUNNED", driver.State.CardObjects[other].UntilEndOfTurnEffects);
        }
        await driver.Act("P1", new EndTurnCommand());
        Assert.Equal(before.Power, driver.State.CardObjects[target].Power);
        Assert.Empty(driver.State.CardObjects[target].UntilEndOfTurnPowerModifiers);
        Assert.DoesNotContain("STUNNED", driver.State.CardObjects[target].UntilEndOfTurnEffects);
        await driver.Replay();
    }

    public static IEnumerable<object[]> IllegalPlays() => Cards.SelectMany(no => new[] {
        "mana", "trait", "malformed-cost", "extra-cost", "wrong-turn", "response", "graveyard", "owner", "enemy-field" }
        .Select(branch => new object[] { no, branch }));

    [Theory]
    [MemberData(nameof(IllegalPlays))]
    public async Task IllegalSourceTimingDestinationAndOptionalPaymentsHaveNoGameplaySideEffects(string no, string branch)
    {
        var state = OptionalEntryTargetConfirmationTests.Position(no);
        var costs = OptionalEntryTargetConfirmationTests.Costs(no); var destination = "BASE";
        if (branch == "mana") state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) {
            ["P1"] = state.RunePools["P1"] with { Mana = no == "SFD·013/221" ? 2 : no == "SFD·067/221" ? 2 : 3 } } };
        if (branch == "trait") state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) {
            ["P1"] = new(20, 0, new Dictionary<string, int> { [RuneTrait.Green] = 6 }) } };
        if (branch == "malformed-cost") costs = no == "SFD·013/221" ? ["SPEND_POWER:red:1"] : ["SPEND_POWER:red:99"];
        if (branch == "extra-cost") costs = [..costs, "SPEND_MANA:99"];
        if (branch == "wrong-turn") state = state with { TurnPlayerId = "P2", ActivePlayerId = "P2" };
        if (branch == "response") {
            state = OptionalEntryTargetConfirmationTests.AddSpell(state, "RESPONSE", "OGN·048/298");
            var result = await new CoreRuleEngine().ResolveAsync(state, new("open-optional-entry-response", "P1", CommandTypes.PlayCard),
                new PlayCardCommand("RESPONSE", "OGN·048/298", []), default);
            Assert.True(result.Accepted, result.ErrorMessage); state = result.State;
        }
        if (branch == "graveyard") state = state with {
            PlayerZones = state.PlayerZones.ToDictionary(p => p.Key, p => p.Key == "P1" ? p.Value with { Hand = [], Graveyard = [..p.Value.Graveyard, "UNIT"] } : p.Value),
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["UNIT"] = new("P1", "GRAVEYARD") } };
        if (branch == "owner") state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["UNIT"] = state.CardObjects["UNIT"] with { OwnerId = "P2", ControllerId = "P2" } } };
        if (branch == "enemy-field") destination = "BATTLEFIELD:OTHER";
        await Reject(state, "P1", new PlayCardCommand("UNIT", no, [], OptionalCosts: costs, Destination: destination));
    }

    internal static async Task Reject(MatchState state, string player, GameCommand command)
    {
        var hash = MatchStateHasher.Hash(state); var views = JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(state));
        var prompts = JsonSerializer.Serialize(ResolutionResult.BuildPrompts(state));
        var result = await new CoreRuleEngine().ResolveAsync(state, new("illegal-optional-entry", player, command.CmdType), command, default);
        Assert.False(result.Accepted); Assert.Empty(result.Events); Assert.Equal(hash, MatchStateHasher.Hash(result.State));
        Assert.Equal(views, JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(result.State)));
        Assert.Equal(prompts, JsonSerializer.Serialize(ResolutionResult.BuildPrompts(result.State)));
    }

    internal static MatchState FieldPosition(string no)
    {
        var state = OptionalEntryTargetConfirmationTests.Position(no); var cards = state.CardObjects.ToDictionary();
        var locations = state.ObjectLocations.ToDictionary(); var zones = state.PlayerZones.ToDictionary();
        cards["FRIEND"] = OfficialGarenConquestBattlefieldCountTests.Printed("FRIEND", "OGN·219/298", "P1");
        locations["FRIEND"] = new("P1", "BATTLEFIELD", "BF"); zones["P1"] = zones["P1"] with { Battlefields = [..zones["P1"].Battlefields, "FRIEND"] };
        cards["ENEMYBASE"] = OfficialGarenConquestBattlefieldCountTests.Printed("ENEMYBASE", "OGN·219/298", "P2");
        locations["ENEMYBASE"] = new("P2", "BASE"); zones["P2"] = zones["P2"] with { Base = ["ENEMYBASE"] };
        cards["SECRET"] = OfficialGarenConquestBattlefieldCountTests.Printed("SECRET", "OGN·219/298", "P2") with { IsFaceDown = true };
        locations["SECRET"] = new("P2", "BATTLEFIELD", "OTHER"); zones["P2"] = zones["P2"] with { Battlefields = [..zones["P2"].Battlefields, "SECRET"] };
        return state with { CardObjects = cards, ObjectLocations = locations, PlayerZones = zones };
    }
}
