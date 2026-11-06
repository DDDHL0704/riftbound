using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class PrintedEnemyMovementTests
{
    private static MatchState Position(string no, string origin = "BATTLEFIELD", bool gear = false)
    {
        var state = ArmamentMightTests.Position();
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["TARGET"] = OfficialGarenConquestBattlefieldCountTests.Printed("TARGET", "OGN·219/298", "P2") with {
                    IsExhausted = true, Damage = 1 } },
            RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(20, 20), ["P2"] = new(20, 20) } };
        state = LocalDestructionRecallTests.At(state, "TARGET", "P2", origin, origin == "BATTLEFIELD" ? "OTHER" : null);
        if (gear)
        {
            state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["GEAR"] = state.CardObjects["GEAR"] with { OwnerId = "P2", ControllerId = "P2", AttachedToObjectId = "TARGET" } } };
            state = LocalDestructionRecallTests.At(state, "GEAR", "P2", origin, origin == "BATTLEFIELD" ? "OTHER" : null);
        }
        return AttackDamageDistributionTests.Spell(state, "SPELL", no, "P1");
    }

    [Theory]
    [InlineData("OGN·043/298", "BASE", "BATTLEFIELD:F")]
    [InlineData("OGN·043/298", "BASE", "BATTLEFIELD:OTHER")]
    [InlineData("OGN·043/298", "BATTLEFIELD", "BATTLEFIELD:F")]
    [InlineData("OGN·043/298", "BATTLEFIELD", "BASE:P2")]
    [InlineData("UNL-038/219", "BASE", "BATTLEFIELD:F")]
    [InlineData("UNL-038/219", "BASE", "BATTLEFIELD:OTHER")]
    [InlineData("UNL-038/219", "BATTLEFIELD", "BATTLEFIELD:F")]
    [InlineData("UNL-038/219", "BATTLEFIELD", "BASE:P2")]
    public async Task EnemyAtEitherFieldZoneCanMoveOnceToTheDeclaredLegalLocation(string no, string origin, string destination)
    {
        var initial = Position(no, origin, true); var target = initial.CardObjects["TARGET"];
        var game = new Driver(initial); var command = new PlayCardCommand("SPELL", no, ["TARGET"], Destination: destination);
        var candidate = Assert.Single(game.PromptFor("P1").Candidates!, c => c.Action == CommandTypes.PlayCard
            && (c.Sources ?? []).Any(source => source.Id == "SPELL"));
        Assert.Contains(candidate.Targets ?? [], choice => choice.Id == "TARGET");
        var quote = new CoreRuleEngine().PreviewPlayCard(initial, "P1", new("movement-quote", game.PromptFor("P1").PromptId!, initial.Tick, command));
        Assert.True(quote.IsValid, quote.Message); Assert.True(quote.CanPay, quote.Message);
        await game.Act("P1", command);
        Assert.Equal(destination, Assert.Single(game.State.StackItems).Destination);
        Assert.Equal(initial.ObjectLocations["TARGET"], game.State.ObjectLocations["TARGET"]);
        await game.Top();
        var location = game.State.ObjectLocations["TARGET"];
        Assert.Equal(destination.StartsWith("BASE:") ? "BASE" : "BATTLEFIELD", location.Zone);
        Assert.Equal(destination.StartsWith("BASE:") ? null : destination[12..], location.BattlefieldObjectId);
        Assert.Equal("P2", location.PlayerId); Assert.Equal("P2", game.State.CardObjects["TARGET"].ControllerId);
        Assert.Equal(location, game.State.ObjectLocations["GEAR"]); Assert.Equal("TARGET", game.State.CardObjects["GEAR"].AttachedToObjectId);
        Assert.Equal(target.ObjectGeneration, game.State.CardObjects["TARGET"].ObjectGeneration);
        Assert.Equal(target.Damage, game.State.CardObjects["TARGET"].Damage); Assert.True(game.State.CardObjects["TARGET"].IsExhausted);
        Assert.Equal(1, game.State.CardObjects["TARGET"].MovementCountThisTurn);
        Assert.Single(game.Events(), e => e.Event.Kind is "UNIT_MOVED_TO_BASE" or "UNIT_MOVED_TO_BATTLEFIELD");
        Assert.Equal(no == "OGN·043/298" ? 19 : 18, game.State.RunePools["P1"].Mana); await game.Replay();
    }

    [Theory]
    [InlineData("OGN·043/298")] [InlineData("UNL-038/219")]
    public async Task OmittedLocationDeclaresTheTargetsControllerBaseBeforeResponses(string no)
    {
        var game = new Driver(Position(no));
        await game.Act("P1", new PlayCardCommand("SPELL", no, ["TARGET"]));
        Assert.Equal("BASE:P2", Assert.Single(game.State.StackItems).Destination);
        await game.Top(); Assert.Contains("TARGET", game.State.PlayerZones["P2"].Base);
        Assert.Equal(1, game.State.CardObjects["TARGET"].MovementCountThisTurn); await game.Replay();
    }

    [Theory]
    [InlineData("OGN·043/298")] [InlineData("UNL-038/219")]
    public async Task EchoFreezesTheDefaultInitialLocationAndTheSeparateDeclaredRepeatLocation(string no)
    {
        var state = Position(no) with { UntilEndOfTurnEffects = [EchoCostRules.GrantPrefix + "P1"] };
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["SECOND"] = OfficialGarenConquestBattlefieldCountTests.Printed("SECOND", "OGN·219/298", "P2") } };
        state = LocalDestructionRecallTests.At(state, "SECOND", "P2", "BASE");
        var game = new Driver(state);
        await game.Act("P1", new PlayCardCommand("SPELL", no, ["TARGET"], OptionalCosts: ["ECHO"],
            RepeatChoices: [new("", ["SECOND"], Destination: "BATTLEFIELD:OTHER")]));
        var item = Assert.Single(game.State.StackItems);
        Assert.Equal(new[] { "BASE:P2", "BATTLEFIELD:OTHER" }, item.RepeatExecutions!.Select(e => e.Destination));
        foreach (var index in new[] { 0, 1 })
        {
            var executions = item.RepeatExecutions!.ToArray(); executions[index] = executions[index] with { Destination = string.Empty };
            Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(game.State with { StackItems = [item with { RepeatExecutions = executions }] }));
        }
        await game.Top();
        Assert.Contains("TARGET", game.State.PlayerZones["P2"].Base);
        Assert.Equal("OTHER", game.State.ObjectLocations["SECOND"].BattlefieldObjectId);
        Assert.Equal(1, game.State.CardObjects["TARGET"].MovementCountThisTurn);
        Assert.Equal(1, game.State.CardObjects["SECOND"].MovementCountThisTurn);
        Assert.Equal(2, game.Events().Count(e => e.Event.Kind is "UNIT_MOVED_TO_BASE" or "UNIT_MOVED_TO_BATTLEFIELD"));
        Assert.Equal(no == "OGN·043/298" ? 18 : 16, game.State.RunePools["P1"].Mana);
        Assert.DoesNotContain(game.State.UntilEndOfTurnEffects, e => EchoCostRules.IsGrant(e, "P1")); await game.Replay();
    }

    [Theory]
    [InlineData("OGN·043/298", "BATTLEFIELD:OTHER", "TARGET")]
    [InlineData("OGN·043/298", "BASE:P1", "TARGET")]
    [InlineData("OGN·043/298", "BATTLEFIELD:ABSENT", "TARGET")]
    [InlineData("OGN·043/298", "BATTLEFIELD:F", "HOST")]
    [InlineData("UNL-038/219", "BATTLEFIELD:OTHER", "TARGET")]
    [InlineData("UNL-038/219", "BASE:P1", "TARGET")]
    [InlineData("UNL-038/219", "BATTLEFIELD:ABSENT", "TARGET")]
    [InlineData("UNL-038/219", "BATTLEFIELD:F", "HOST")]
    public async Task InvalidLocationOrFriendlyTargetCannotPayOrChangeState(string no, string destination, string target)
    {
        var state = Position(no); var before = MatchStateHasher.Hash(state);
        var result = await new CoreRuleEngine().ResolveAsync(state, new("invalid-move", "P1", CommandTypes.PlayCard),
            new PlayCardCommand("SPELL", no, [target], Destination: destination), default);
        Assert.False(result.Accepted); Assert.Empty(result.Events); Assert.Equal(before, MatchStateHasher.Hash(result.State));
    }

    [Theory]
    [InlineData("OGN·043/298")] [InlineData("UNL-038/219")]
    public async Task ATargetReturnedToHandDuringResponsesIsNotMovedAsANewGeneration(string no)
    {
        var initial = AttackDamageDistributionTests.Spell(Position(no), "RETURN", "OGN·104/298", "P2");
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("SPELL", no, ["TARGET"], Destination: "BATTLEFIELD:F"));
        await game.Act("P1", new PassPriorityCommand());
        await game.Act("P2", new PlayCardCommand("RETURN", "OGN·104/298", ["TARGET"])); await game.Top();
        var hand = game.State.CardObjects["TARGET"]; Assert.Contains("TARGET", game.State.PlayerZones["P2"].Hand);
        await game.Top(); Assert.Equal(hand, game.State.CardObjects["TARGET"]);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind is "UNIT_MOVED_TO_BASE" or "UNIT_MOVED_TO_BATTLEFIELD"); await game.Replay();
    }
}
