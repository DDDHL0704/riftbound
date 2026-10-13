using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class HeldMovementLegalityTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ProhibitedMovementIsStillAConfirmableHeldTargetButIsIgnoredAtExecution(bool temporary)
    {
        var initial = Position();
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["OTHER"] = initial.CardObjects["OTHER"] with { CardNo = temporary ? "OGN·294/298" : "OGN·295/298" },
            ["ENEMY"] = initial.CardObjects["ENEMY"] with { UntilEndOfTurnEffects = temporary ? [MovementRestrictionRules.ForPlayer("P1")] : [] } } };
        var driver = Driver(initial, temporary ? "temporary-prohibition" : "vilemaw-prohibition");
        await driver.Act("P1", new PassPriorityCommand());
        var choice = Assert.IsType<PendingCardChoiceState>(driver.State.PendingCardChoice);
        Assert.Contains("ENEMY", choice.LegalObjectIds);
        await driver.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ENEMY"]));
        Assert.NotNull(driver.State.PriorityPlayerId);
        var result = await driver.Top();
        Assert.Equal("OTHER", driver.State.ObjectLocations["ENEMY"].BattlefieldObjectId);
        Assert.Equal(0, driver.State.CardObjects["ENEMY"].MovementCountThisTurn);
        Assert.DoesNotContain(result.Events, e => e.Kind == "UNIT_MOVED_TO_BASE");
        await Replay(initial, driver);
    }

    [Theory]
    [InlineData("P1")]
    [InlineData("P2")]
    public async Task ARestrictionForTheOtherActorDoesNotPreventTheHeldMove(string restrictedPlayer)
    {
        var initial = Position();
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["ENEMY"] = initial.CardObjects["ENEMY"] with { UntilEndOfTurnEffects = [MovementRestrictionRules.ForPlayer(restrictedPlayer)] } } };
        var driver = Driver(initial, "actor-" + restrictedPlayer);
        await driver.Act("P1", new PassPriorityCommand());
        var choice = driver.State.PendingCardChoice!;
        await driver.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ENEMY"]));
        await driver.Top();
        Assert.Equal(restrictedPlayer == "P1" ? "BATTLEFIELD" : "BASE", driver.State.ObjectLocations["ENEMY"].Zone);
        Assert.Equal(restrictedPlayer == "P1" ? 0 : 1, driver.State.CardObjects["ENEMY"].MovementCountThisTurn);
        await Replay(initial, driver);
    }

    [Fact]
    public async Task SuccessfulHeldMoveKeepsEquipmentDamageAndControllerWhileCapturingTheRealMovement()
    {
        var initial = Position();
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["ENEMY"] = initial.CardObjects["ENEMY"] with { Damage = 1, IsExhausted = true },
            ["GEAR"] = new("GEAR", cardNo: "OGN·023/298", ownerId: "P2", controllerId: "P2",
                tags: [CardObjectTags.EquipmentCard], attachedToObjectId: "ENEMY") },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) { ["GEAR"] = new("P2", "BATTLEFIELD", "OTHER") },
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
                ["P2"] = initial.PlayerZones["P2"] with { Battlefields = [..initial.PlayerZones["P2"].Battlefields, "GEAR"] } } };
        var driver = Driver(initial, "equipment-damage");
        await driver.Act("P1", new PassPriorityCommand());
        var choice = driver.State.PendingCardChoice!;
        await driver.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ENEMY"]));
        var result = await driver.Top();
        Assert.Equal("BASE", driver.State.ObjectLocations["ENEMY"].Zone);
        Assert.Equal("BASE", driver.State.ObjectLocations["GEAR"].Zone);
        Assert.Equal("P2", driver.State.ObjectLocations["GEAR"].PlayerId);
        Assert.Equal("ENEMY", driver.State.CardObjects["GEAR"].AttachedToObjectId);
        Assert.Equal(1, driver.State.CardObjects["ENEMY"].Damage);
        Assert.True(driver.State.CardObjects["ENEMY"].IsExhausted);
        Assert.Equal(0, driver.State.CardObjects["ENEMY"].ObjectGeneration);
        Assert.Equal(1, driver.State.CardObjects["ENEMY"].MovementCountThisTurn);
        var movement = Assert.Single(result.Events, e => e.Kind == "UNIT_MOVED_TO_BASE");
        Assert.Equal("P1", movement.Payload["movingPlayerId"]);
        Assert.Equal("F", movement.Payload["sourceObjectId"]);
        Assert.Equal("OTHER", movement.Payload["originBattlefieldObjectId"]);
        await Replay(initial, driver);
    }

    [Fact]
    public async Task ADeclinedProhibitedTargetEndsConfirmationWithoutMovingAnything()
    {
        var initial = Position();
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["OTHER"] = initial.CardObjects["OTHER"] with { CardNo = "OGN·295/298" } } };
        var driver = Driver(initial, "decline-prohibited");
        await driver.Act("P1", new PassPriorityCommand());
        var choice = driver.State.PendingCardChoice!;
        await driver.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, []));
        Assert.Null(driver.State.PendingCardChoice);
        Assert.DoesNotContain(driver.State.StackItems, i => i.EffectKind == "HOLD_MOVE_BASE");
        Assert.Equal("OTHER", driver.State.ObjectLocations["ENEMY"].BattlefieldObjectId);
        Assert.Equal(0, driver.State.CardObjects["ENEMY"].MovementCountThisTurn);
        await Replay(initial, driver);
    }

    internal static MatchState Position() => OfficialHeldConfirmationTests.Position("move");
    internal static StandbyDefendObserverTests.Driver Driver(MatchState state, string branch)
    {
        var root = Environment.GetEnvironmentVariable("RIFTBOUND_HELD_MOVEMENT_EVIDENCE");
        return new(state, string.IsNullOrWhiteSpace(root) ? null : Path.Combine(root, branch));
    }
    internal static async Task Replay(MatchState initial, StandbyDefendObserverTests.Driver driver)
    {
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, driver.Commands(), driver.State,
            new CoreRuleEngine(), default, driver.Events());
        Assert.True(replay.IsMatch, string.Join("; ", replay.Errors));
        // Initial TurnStart fixture advancement is automatic server work and has
        // no offered client button. Every subsequent command is a native intent.
        if (driver.NativeCommands > 0) Assert.Equal(driver.Commands().Length - 1, driver.NativeCommands);
        driver.Export("steps.json", driver.Steps.Skip(1).ToArray());
        driver.Export("replay.json", new { nativeCommands = driver.NativeCommands,
            accepted = driver.Commands().Length, coldRestores = driver.Commands().Length,
            fixtureSetupCommands = 1, fullJournalReplay = replay });
    }
}
