using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed partial class VexStunMovementTests
{
    [Theory, InlineData(false), InlineData(true)]
    public async Task SourceMovementUsesTheExistingImposedPaymentContinuationAndCanBeDeclined(bool pay)
    {
        var initial = Position();
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["TAX"] = new("TAX", cardNo: "RAD·062/167", ownerId: "P2", controllerId: "P2",
                tags: [CardObjectTags.EquipmentCard, CardEquipmentKeywordNames.Deploy]) } };
        initial = RadUnitEffectTestFixture.Place(initial, "TAX", "P2", "BATTLEFIELD", "OTHER");
        var game = new Driver(initial); await StunEnemy(game); await Decide(game);
        var before = game.State.RunePools["P1"];
        await game.Top();
        var request = game.State.PendingRuleChoice!.Request;
        Assert.Equal(CoreRuleEngine.ImposedMovementPaymentKind, request.Kind);
        Assert.Equal(1, request.ManaCost); Assert.Equal(1, request.PowerCost);
        Assert.Equal("BASE", game.State.ObjectLocations["HOST"].Zone);
        await game.Act(request.PlayerId, new PayCostCommand(request.Id, "RULE_REPLACEMENT", [pay ? "PAY" : "DECLINE"]));
        Assert.Null(game.State.PendingRuleChoice);
        Assert.Equal(pay ? "OTHER" : null, game.State.ObjectLocations["HOST"].BattlefieldObjectId);
        Assert.Equal(before.Mana - (pay ? 1 : 0), game.State.RunePools["P1"].Mana);
        Assert.Equal(before.TotalPower - (pay ? 1 : 0), game.State.RunePools["P1"].TotalPower);
        Assert.Contains("STUNNED", game.State.CardObjects["ENEMY"].UntilEndOfTurnEffects);
        await game.ColdCheckpoint();
    }

    [Theory]
    [InlineData("battlefield")] [InlineData("source-generation")] [InlineData("stunned-generation")]
    [InlineData("actor")] [InlineData("event-group")] [InlineData("card-no")]
    public async Task ACoherentProjectionCannotReplaceTheCapturedStunWithAnotherValidFieldOrIdentity(string part)
    {
        var game = new Driver(Position()); await StunEnemy(game); await Decide(game);
        Assert.Empty(ValidateOriginalHistory(game, game.State));
        var item = Assert.Single(game.State.StackItems); var context = item.FieldContext!; var birth = context.StunObserver!;
        var forged = part switch {
            "battlefield" => context with { BattlefieldId = "BF", StunObserver = birth with {
                Battlefield = new("BF", game.State.CardObjects["BF"].ObjectGeneration) } },
            "source-generation" => context with { SourceGeneration = context.SourceGeneration + 1,
                StunObserver = birth with { Source = birth.Source with { Generation = birth.Source.Generation + 1 } } },
            "stunned-generation" => context with { StunObserver = birth with {
                Stunned = birth.Stunned with { Generation = birth.Stunned.Generation + 1 } } },
            "actor" => context with { StunObserver = birth with { Actor = "P2" } },
            "event-group" => context with { StunObserver = birth with { EventGroup = "FORGED-ORIGINAL-STUN" } },
            _ => context with { CardNo = "UNL-055a/219" }
        };
        var state = game.State with { StackItems = [item with { FieldContext = forged, CardNo = forged.CardNo }] };
        Assert.NotEmpty(ValidateOriginalHistory(game, state));
        await game.ColdCheckpoint();
    }

    private static IReadOnlyList<string> ValidateOriginalHistory(Driver game, MatchState state)
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        state = JsonSerializer.Deserialize<MatchState>(JsonSerializer.Serialize(state, options), options)!;
        var commands = JsonSerializer.Deserialize<RecoveredCommand[]>(JsonSerializer.Serialize(game.Commands(), options), options)!;
        var events = JsonSerializer.Deserialize<RecoveredEvent[]>(JsonSerializer.Serialize(game.Events(), options), options)!;
        var sequence = events.Length == 0 ? 0 : events[^1].Sequence;
        var snapshots = ResolutionResult.BuildSnapshots(state); var prompts = ResolutionResult.BuildPrompts(state);
        var views = state.Seats.Keys.ToDictionary(player => player,
            player => new RecoveredPlayerView(player, state.Tick, sequence, snapshots[player], state.Tick, sequence, prompts[player]));
        var spectator = MatchReplayRedactor.BuildSpectatorFrame(state.RoomId, state.Tick, sequence, [], state);
        return MatchRecoveryValidator.Validate(state.RoomId, sequence, commands, events, views, state, state.Tick, spectator);
    }
}
