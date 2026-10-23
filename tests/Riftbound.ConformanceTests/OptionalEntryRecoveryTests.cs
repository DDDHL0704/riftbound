using System.Text.Json;
using System.Text.Json.Nodes;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class OptionalEntryRecoveryTests
{
    [Theory]
    [InlineData("SFD·013/221", "OGN·007/298")]
    [InlineData("SFD·067/221", "OGN·089/298")]
    [InlineData("VEN·120", "OGN·214/298")]
    public async Task ARealRecyclePaymentRetainsOnlyTheExactBehaviorCostInThePendingSkill(string no, string runeNo)
    {
        var initial = OptionalEntryTargetConfirmationTests.Position(no);
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["PAYRUNE"] = OfficialGarenConquestBattlefieldCountTests.Printed("PAYRUNE", runeNo, "P1") },
            PlayerZones = initial.PlayerZones.ToDictionary(p => p.Key, p => p.Key == "P1" ? p.Value with { Base = [..p.Value.Base, "PAYRUNE"] } : p.Value),
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) { ["PAYRUNE"] = new("P1", "BASE") },
            RunePools = new Dictionary<string, RunePool>(initial.RunePools) { ["P1"] = new(20, 0) } };
        Assert.Contains(CardObjectTags.RuneCard, initial.CardObjects["PAYRUNE"].Tags);
        var driver = new Driver(initial); var costs = OptionalEntryTargetConfirmationTests.Costs(no);
        await driver.Act("P1", new PlayCardCommand("UNIT", no, [], OptionalCosts: ["RECYCLE_RUNE:PAYRUNE", ..costs], Destination: "BASE"));
        Assert.Contains("PAYRUNE", driver.State.PlayerZones["P1"].RuneDeck);
        Assert.Equal(0, driver.State.RunePools["P1"].TotalPower);
        Assert.Equal(costs, Assert.Single(driver.State.StackItems).OptionalCosts);
        var choice = driver.State.PendingCardChoice!; Assert.Equal(1, choice.RequiredCount);
        await driver.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ENEMY"])); await driver.Top(); await driver.Replay();
    }

    [Theory]
    [InlineData("SFD·013/221")]
    [InlineData("SFD·067/221")]
    [InlineData("VEN·120")]
    public async Task ARecoveredPendingTargetChoiceCannotLoseItsPrintedCostConditionOrMandatoryCardinality(string no)
    {
        var initial = OptionalEntryTargetConfirmationTests.Position(no); var driver = new Driver(initial);
        await driver.Act("P1", new PlayCardCommand("UNIT", no, [], OptionalCosts: OptionalEntryTargetConfirmationTests.Costs(no), Destination: "BASE"));
        var original = driver.State; var item = Assert.Single(original.StackItems); var choice = original.PendingCardChoice!;
        var sequence = driver.Events().Length;
        foreach (var branch in new[] { "unpaid", "wrong-cost", "extra-cost", "optional-target", "battlefield-target" }) {
            var forged = original with {
                StackItems = [item with { OptionalCosts = branch == "unpaid" ? [] : branch == "wrong-cost" ? ["SPEND_POWER:green:9"]
                    : branch == "extra-cost" ? [..item.OptionalCosts, "SPEND_POWER:green:999"] : item.OptionalCosts }],
                PendingCardChoice = branch == "optional-target" ? choice with { RequiredCount = 0 }
                    : branch == "battlefield-target" ? choice with { LegalObjectIds = [..choice.LegalObjectIds, "BF"], ContextObjectIds = [..choice.ContextObjectIds, "BF"] } : choice };
            var snapshots = ResolutionResult.BuildSnapshots(forged); var prompts = ResolutionResult.BuildPrompts(forged);
            var views = original.Seats.Keys.ToDictionary(p => p, p => new RecoveredPlayerView(p, forged.Tick, sequence, snapshots[p], forged.Tick, sequence, prompts[p]));
            var spectator = MatchReplayRedactor.BuildSpectatorFrame(original.RoomId, forged.Tick, sequence, [], forged);
            var errors = MatchRecoveryValidator.Validate(original.RoomId, sequence, driver.Commands(), driver.Events(), views, forged, forged.Tick, spectator);
            Assert.True(errors.Count > 0, branch + " must be rejected by the pending-target structural validator");
            var frame = new MatchRecoveryFrame(original.RoomId, forged.Tick, sequence, driver.Commands(), driver.Events(), views, errors, forged, initial, spectator) { OpeningSeed = initial.Seed };
            Assert.Equal(ErrorCodes.RecoveryInconsistent, Assert.Throws<MatchSessionException>(() => MatchSession.Restore(frame, new CoreRuleEngine(), driver)).Code);
        }
        await driver.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ENEMY"])); await driver.Top(); await driver.Replay();
    }

    [Theory]
    [InlineData("SFD·013/221")]
    [InlineData("SFD·067/221")]
    [InlineData("VEN·120")]
    public async Task RemovingTheOriginalCostChoiceFromRawCommandsCannotReplayThePaidAbilityOrItsOriginalEvents(string no)
    {
        var initial = OptionalEntryTargetConfirmationTests.Position(no); var driver = new Driver(initial);
        await driver.Act("P1", new PlayCardCommand("UNIT", no, [], OptionalCosts: OptionalEntryTargetConfirmationTests.Costs(no), Destination: "BASE"));
        var choice = driver.State.PendingCardChoice!;
        await driver.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ENEMY"])); await driver.Top();
        var commands = driver.Commands(); var raw = Assert.IsType<JsonElement>(commands[0].RawCommand);
        var wire = JsonNode.Parse(raw.GetRawText())!.AsObject();
        wire["optionalCosts"] = new JsonArray();
        commands[0] = commands[0] with { RawCommand = JsonSerializer.SerializeToElement(wire) };
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, commands, driver.State, new CoreRuleEngine(), default, driver.Events());
        Assert.False(replay.IsMatch); await driver.Replay();
    }
}
