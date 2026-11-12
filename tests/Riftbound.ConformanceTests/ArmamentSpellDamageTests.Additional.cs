using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed partial class ArmamentSpellDamageTests
{
    private static MatchState DeclaredCape(int copies = 1)
    {
        var state = RadUnitEffectTestFixture.Place(Position(), "GEAR", "P1", "BASE");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["GEAR"] = state.CardObjects["GEAR"] with { AttachedToObjectId = "HOST" } } };
        if (copies > 1)
        {
            state = RadGearTestPosition.Put(state, "CAP2", Card, "P1", "BASE");
            state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["CAP2"] = state.CardObjects["CAP2"] with { AttachedToObjectId = "HOST" } } };
        }
        return state;
    }

    [Fact]
    public void OnlyTheExactOfficialAttachedDamageLineProvidesTheNumericBonus()
    {
        Assert.Equal(3, OfficialArmamentCatalog.Find(Card)!.SpellSkillDamageBonus);
        Assert.Equal(0, OfficialArmamentCatalog.Find("SFD·192/221")!.SpellSkillDamageBonus);
        Assert.Equal(0, OfficialArmamentCatalog.Find("SFD·016/221")!.SpellSkillDamageBonus);
    }

    [Fact]
    public async Task TwoSeparateAttachedCapesAddOnceEachToTheSameRealSpellDamageAction()
    {
        var game = new Driver(DeclaredCape(copies: 2));
        await game.Act("P1", new PlayCardCommand("RAY", Ray, ["ENEMY"])); await game.Top();
        Assert.Equal(9, Assert.Single(game.Events(), entry => entry.Event.Kind == "DAMAGE_APPLIED").Event.Payload["damage"]);
        Assert.Equal(10, ArmamentMightTests.Might(game.State));
        await game.Replay();
    }

    [Fact]
    public async Task AnActualActivatedUnitSkillUsesTheSameCurrentAttachedBonus()
    {
        var state = DeclaredCape();
        state = LocalDestructionRecallTests.At(state, "HOST", "P1", "BATTLEFIELD", "OTHER");
        state = LocalDestructionRecallTests.At(state, "GEAR", "P1", "BATTLEFIELD", "OTHER");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["HOST"] = OfficialGarenConquestBattlefieldCountTests.Printed("HOST", "UNL-026/219", "P1") } };
        var game = new Driver(state);
        await game.Act("P1", new ActivateAbilityCommand("HOST", P4ActivatedAbilityCatalog.XerathDamageAbilityId, ["ENEMY"]));
        await game.Top();
        Assert.Equal(6, Assert.Single(game.Events(), entry => entry.Event.Kind == "DAMAGE_APPLIED").Event.Payload["damage"]);
        await game.Replay();
    }

    [Fact]
    public async Task AlphaStrikesChosenUnitDamageDoesNotAcquireASpellOrSkillBonus()
    {
        var state = RadGearTestPosition.Put(AlphaStrikeReflexiveExperienceTests.Position(power: 3), "CAPE", Card, "P1", "BASE");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["CAPE"] = state.CardObjects["CAPE"] with { AttachedToObjectId = "ALLY" } } };
        var game = new Driver(state);
        await game.Act("P1", new PlayCardCommand("SPELL", AlphaStrikeReflexiveExperienceTests.Alpha, ["SOURCE", "E0", "E1"]));
        await game.Top(); await AlphaStrikeReflexiveExperienceTests.Allocate(game);
        Assert.Equal(3, game.Events().Where(entry => entry.Event.Kind == "DAMAGE_APPLIED")
            .Sum(entry => Convert.ToInt32(entry.Event.Payload["damage"])));
        await AlphaStrikeReflexiveExperienceTests.Order(game);
        while (game.State.StackItems.Count > 0) await game.Top();
        await game.Replay();
    }

    [Fact]
    public async Task AnOriginalNullPayloadRejectsCapturedBonusRecoveryWithoutChangingTheAcceptedJournal()
    {
        var game = await ConfirmDistributedCape();
        var state = game.State;
        var commands = game.Commands(); var events = game.Events();
        var commandJson = JsonSerializer.Serialize(commands); var eventJson = JsonSerializer.Serialize(events);
        var before = MatchStateHasher.Hash(state); var sequence = commands.Last().CompletedEventSequence;
        var snapshots = ResolutionResult.BuildSnapshots(state); var prompts = ResolutionResult.BuildPrompts(state);
        var views = state.Seats.Keys.ToDictionary(player => player, player => new RecoveredPlayerView(
            player, state.Tick, sequence, snapshots[player], state.Tick, sequence, prompts[player]));
        var spectator = MatchReplayRedactor.BuildSpectatorFrame(state.RoomId, state.Tick, sequence, [], state);
        foreach (var original in events.DistinctBy(entry => entry.Event.Kind))
        {
            var malformed = events.Select(entry => entry.Sequence == original.Sequence
                ? entry with { Event = entry.Event with { Payload = null! } } : entry).ToArray();
            var errors = MatchRecoveryValidator.Validate(state.RoomId, sequence, commands, malformed, views, state, state.Tick, spectator);
            Assert.Contains(errors, error => error.Contains("payload is required", StringComparison.Ordinal));
            var frame = new MatchRecoveryFrame(state.RoomId, state.Tick, sequence, commands, malformed, views, errors,
                AuthoritativeState: state, SpectatorReplayFrame: spectator) { OpeningSeed = state.Seed };
            Assert.Throws<MatchSessionException>(() => MatchSession.Restore(frame, new CoreRuleEngine(), game));
        }
        Assert.Equal(commandJson, JsonSerializer.Serialize(game.Commands()));
        Assert.Equal(eventJson, JsonSerializer.Serialize(game.Events()));
        Assert.Equal(before, MatchStateHasher.Hash(game.State));
        await game.ColdCheckpoint();
    }

    [Theory]
    [InlineData(7, true)] [InlineData(10, false)]
    public async Task ADeclaredCombatUsesTheMightBonusAndNeverAddsAnotherThreeSpellDamage(int total, bool accepted)
    {
        // Direct combat boundary on an explicitly declared fixture, not a
        // claim to have reached this battle through the original action log.
        var state = RadGearTestPosition.Put(OfficialPooledCombatDamageTests.Position(), "CAPE", Card, "P1", "BATTLEFIELD", "BF");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["CAPE"] = state.CardObjects["CAPE"] with { AttachedToObjectId = "A1" } } };
        Assert.Equal(7L, ResolutionResult.BuildPrompts(state)["P1"].View!.Metadata!["totalAssignableDamage"]);
        var hash = MatchStateHasher.Hash(state);
        var command = new AssignCombatDamageCommand("battle:BF", "BF", [new("A1", "D1", total - 2), new("A2", "D1", 1), new("A2", "D2", 1)]);
        var result = await new CoreRuleEngine().ResolveAsync(state, new("cap-combat", "P1", command.CmdType), command, default);
        Assert.Equal(accepted, result.Accepted);
        if (!accepted) { Assert.Empty(result.Events); Assert.Equal(hash, MatchStateHasher.Hash(result.State)); }
    }

    private static async Task<Driver> ConfirmDistributedCape(string? response = null, bool copiedEquipment = false)
    {
        var state = AttackDamageDistributionTests.Position();
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["GEAR"] = OfficialGarenConquestBattlefieldCountTests.Printed("GEAR", Card, "P1") with { AttachedToObjectId = "HOST" } } };
        if (copiedEquipment)
        {
            // The opening explicitly declares an equipment Image. Subsequent
            // response, token departure and recovery use actual commands; this
            // fixture does not claim to test the earlier Image creation action.
            var image = CopyCharacteristics.CreateImage("GEAR", "P1", state.CardObjects["GEAR"], false);
            state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["GEAR"] = image with { AttachedToObjectId = "HOST", CopyFaceTransitions = [new(0, 0, P6TokenFactoryCatalog.ImageTokenCardNo, Card)] },
                ["ENEMY3"] = OfficialGarenConquestBattlefieldCountTests.Printed("ENEMY3", "RAD·063/167", "P2") } };
        }
        state = RadGearTestPosition.Put(state, "SPARE-CAPE", Card, "P1", "BASE");
        if (response is not null) state = AttackDamageDistributionTests.Spell(state, "CAP-RESPONSE", response, copiedEquipment ? "P2" : "P1");
        var game = new Driver(state);
        await game.Act("P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F"));
        for (var index = 0; index < 4 && game.State.FocusPlayerId is { } focus; index++) await game.Act(focus, new PassFocusCommand());
        if (game.PromptFor("P1").EnabledActions().Contains(CommandTypes.DeclareBattle))
            await game.Act("P1", new DeclareBattleCommand("F", ["HOST"], ["ENEMY1", "ENEMY2", "ENEMY3"], ["COMBAT_ASSIGNMENT"]));
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ENEMY1"]));
        Assert.Equal(8, Assert.Single(game.State.StackItems).DamageAmount);
        Assert.Single(game.Events(), entry => entry.Event.Payload.ContainsKey(CoreRuleEngine.ArmamentDamageCaptureHashKey));
        Assert.Empty(ArmamentGrantedCombatTriggerTests.Errors(game, game.State));
        return game;
    }

    [Theory]
    [InlineData("other-equipment")] [InlineData("missing-equipment")] [InlineData("bonus-erase")]
    [InlineData("bonus-double")] [InlineData("erase-skill")] [InlineData("duplicate-skill")] [InlineData("empty-card")]
    public async Task OriginalFramesRejectCoordinatedBonusForgeriesWithoutRewritingAcceptedCommandsOrEvents(string mutation)
    {
        var game = await ConfirmDistributedCape();
        var item = Assert.Single(game.State.StackItems); var context = item.DamageDistribution!;
        var bonus = Assert.Single(context.InitialBonuses);
        var originalCommands = JsonSerializer.Serialize(game.Commands());
        var originalEvents = JsonSerializer.Serialize(game.Events());
        var bonuses = mutation switch {
            "other-equipment" => new[] { bonus with { Source = new("SPARE-CAPE", 0) } },
            "missing-equipment" => [bonus with { Source = new("NEVER-ON-FIELD", 0) }],
            "empty-card" => [bonus with { CardNo = null! }],
            "bonus-erase" => [], "bonus-double" => [bonus, bonus], _ => new[] { bonus }
        };
        var amount = 5 + bonuses.Sum(value => value.Amount);
        var changed = item with { DamageAmount = amount, DamageDistribution = context with { InitialDamage = amount, InitialBonuses = bonuses } };
        var forged = game.State with { StackItems = mutation switch {
            "erase-skill" => [], "duplicate-skill" => [item, item], _ => [changed] } };
        Assert.Contains(ArmamentGrantedCombatTriggerTests.Errors(game, forged),
            error => error.Contains("armament damage bonus", StringComparison.Ordinal));
        Assert.Equal(originalCommands, JsonSerializer.Serialize(game.Commands()));
        Assert.Equal(originalEvents, JsonSerializer.Serialize(game.Events()));
        await game.Top();
        await game.Act("P1", AttackDamageDistributionTests.Allocation(game.State, ("ENEMY1", 8)));
        await game.Replay();
    }

    [Fact]
    public async Task ACompletedOriginalBonusCaptureCannotBeRevivedInTheStack()
    {
        var game = await ConfirmDistributedCape(); var original = Assert.Single(game.State.StackItems);
        await game.Top(); await game.Act("P1", AttackDamageDistributionTests.Allocation(game.State, ("ENEMY1", 8)));
        Assert.Contains(ArmamentGrantedCombatTriggerTests.Errors(game, game.State with { StackItems = [original] }),
            error => error.Contains("armament damage bonus", StringComparison.Ordinal));
        await game.Replay();
    }

    [Fact]
    public async Task AnActualResponseMovesTheSkillSourceWithoutChangingItsImmutableBonusCapture()
    {
        var game = await ConfirmDistributedCape("OGS·011/024");
        await game.Act("P1", new PlayCardCommand("CAP-RESPONSE", "OGS·011/024", ["HOST"])); await game.Top();
        Assert.Equal("BASE", game.State.ObjectLocations["HOST"].Zone);
        Assert.Equal("BASE", Assert.Single(game.State.StackItems).DamageDistribution!.SourceLocation.Zone);
        Assert.Empty(ArmamentGrantedCombatTriggerTests.Errors(game, game.State));
        await game.Top();
        Assert.DoesNotContain(game.Events(), entry => entry.Event.Kind == "DAMAGE_APPLIED");
        await game.Replay();
    }

    [Fact]
    public async Task ADeclaredEquipmentImageCanCeaseDuringAnActualResponseWhileItsOriginalBonusRemainsHistorical()
    {
        var game = await ConfirmDistributedCape("SFD·135/221", copiedEquipment: true);
        if (game.State.PriorityPlayerId != "P2") await game.Act(game.State.PriorityPlayerId!, new PassPriorityCommand());
        // The defending Kai'Sa is in this real showdown and gives the Swift
        // equipment-return spell Reaction, without an unrelated attack skill.
        Assert.True(game.State.SpellDuelState.IsActive);
        await game.Act("P2", new PlayCardCommand("CAP-RESPONSE", "SFD·135/221", ["GEAR"])); await game.Top();
        Assert.False(game.State.CardObjects.ContainsKey("GEAR"));
        var captured = Assert.Single(game.State.StackItems).DamageDistribution!;
        Assert.Equal(3, Assert.Single(captured.InitialBonuses).Amount);
        Assert.Empty(ArmamentGrantedCombatTriggerTests.Errors(game, game.State));
        await game.Top();
        Assert.Equal(5, Assert.Single(game.State.StackItems).DamageDistribution!.ResolutionDamage);
        await game.Act("P1", AttackDamageDistributionTests.Allocation(game.State, ("ENEMY1", 5)));
        Assert.Equal(5, Assert.Single(game.Events(), entry => entry.Event.Kind == "DAMAGE_APPLIED").Event.Payload["damage"]);
        await game.Replay();
    }
}
