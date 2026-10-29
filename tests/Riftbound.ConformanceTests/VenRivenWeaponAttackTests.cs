using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class VenRivenWeaponAttackTests
{
    private static MatchState Position(string no, int weapons, bool bonus = false)
    {
        var state = AttackDamageDistributionTests.Position(no);
        var cards = state.CardObjects.ToDictionary(); var zones = state.PlayerZones.ToDictionary();
        var locations = state.ObjectLocations.ToDictionary();
        cards["HOST"] = OfficialGarenConquestBattlefieldCountTests.Printed("HOST", no, "P1");
        cards["ENEMY1"] = cards["ENEMY1"] with { Power = 20 };
        for (var i = 1; i <= weapons; i++)
        {
            var id = "WEAPON-" + i;
            cards[id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, "SFD·186/221", "P1") with { AttachedToObjectId = "HOST" };
            locations[id] = new("P1", "BASE");
            zones["P1"] = zones["P1"] with { Base = [..zones["P1"].Base, id] };
        }
        state = state with { RoomId = "VEN-RIVEN-WEAPON-" + no, CardObjects = cards, PlayerZones = zones, ObjectLocations = locations };
        if (!bonus) return state;
        state = LocalDestructionRecallTests.At(OfficialHoldSequenceTests.AddUnit(state, "ANNIE", "OGS·001/024", "F", "P1"), "ANNIE", "P1", "BASE");
        return state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["F"] = state.CardObjects["F"] with { CardNo = "OGN·296/298" } } };
    }

    private static async Task AttackAndChoose(Driver game)
    {
        await game.Act("P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F", []));
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        Assert.Contains("ENEMY1", choice.LegalObjectIds);
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ENEMY1"]));
    }

    [Theory]
    [InlineData("VEN·041", 37730, 0)] [InlineData("VEN·041", 37730, 1)] [InlineData("VEN·041", 37730, 3)]
    [InlineData("VEN·171", 37874, 0)] [InlineData("VEN·171", 37874, 1)] [InlineData("VEN·171", 37874, 3)]
    public async Task AttackDealsOneDamageEventScaledByAttachedWeapons(string no, long id, int weapons)
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, c => c.CardNo == no);
        Assert.Equal(id, card.Id); Assert.Equal("重铸而归", card.SubTitle); Assert.Equal(3, card.Energy); Assert.Equal(3, card.Power);
        var spec = Assert.Single(TriggerParser.Parse(card.CardEffect), t => t.Kind == TriggerKinds.UnitAttackDamagePerAttachedWeapon);
        Assert.Equal(2, spec.DamageAmount);
        var game = new Driver(Position(no, weapons)); await AttackAndChoose(game);
        Assert.Equal(0, game.State.CardObjects["ENEMY1"].Damage); await game.Top();
        Assert.Equal(weapons * 2, game.State.CardObjects["ENEMY1"].Damage);
        var damage = game.Events().Where(e => e.Event.Kind == "DAMAGE_APPLIED" && Equals(e.Event.Payload.GetValueOrDefault("targetObjectId"), "ENEMY1")).ToArray();
        Assert.Equal(weapons == 0 ? 0 : 1, damage.Length);
        if (weapons != 0) Assert.Equal(weapons * 2, damage[0].Event.Payload["damage"]);
        await game.Replay();
    }

    [Theory]
    [InlineData("VEN·041", 0, 0)] [InlineData("VEN·041", 3, 8)]
    [InlineData("VEN·171", 0, 0)] [InlineData("VEN·171", 3, 8)]
    public async Task ExtraDamageAppliesOnceOnlyWhenWeaponDamageOccurs(string no, int weapons, int expected)
    {
        var game = new Driver(Position(no, weapons, bonus: true)); await AttackAndChoose(game); await game.Top();
        Assert.Equal(expected, game.State.CardObjects["ENEMY1"].Damage);
        var damage = game.Events().Where(e => e.Event.Kind == "DAMAGE_APPLIED" && Equals(e.Event.Payload.GetValueOrDefault("targetObjectId"), "ENEMY1")).ToArray();
        Assert.Equal(expected == 0 ? 0 : 1, damage.Length);
        if (expected != 0) Assert.Equal(expected, damage[0].Event.Payload["damage"]);
        await game.Replay();
    }

    [Theory]
    [InlineData("VEN·041")] [InlineData("VEN·171")]
    public async Task HeroPlayOffersTemperedAndAttachesTheChosenWeapon(string no)
    {
        var state = TemperedPlayTriggerAuditTests.PositionFor(no, "SFD·186/221");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["HOST"] = OfficialGarenConquestBattlefieldCountTests.Printed("HOST", no, "P1") } };
        var game = new Driver(state); await game.Act("P1", new PlayCardCommand("HOST", no, [], Destination: "BASE"));
        Assert.Contains("HOST", game.State.PlayerZones["P1"].Base); Assert.Null(game.State.CardObjects["GEAR"].AttachedToObjectId);
        Assert.Equal(4, game.State.RunePools["P1"].PowerByTrait.GetValueOrDefault(RuneTrait.Green));
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        Assert.Equal("TEMPERED_CONFIRMATION", choice.ChoiceWindow); Assert.Contains("GEAR", choice.LegalObjectIds);
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["GEAR"])); await game.Top();
        Assert.Equal("HOST", game.State.CardObjects["GEAR"].AttachedToObjectId); await game.Replay();
    }

    [Theory]
    [InlineData("VEN·041")] [InlineData("VEN·171")]
    public async Task PrintedPlayRequiresGreenPowerAndDoesNotPartiallyPay(string no)
    {
        var state = TemperedPlayTriggerAuditTests.PositionFor(no);
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["HOST"] = OfficialGarenConquestBattlefieldCountTests.Printed("HOST", no, "P1") },
            RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(20, 0, new Dictionary<string, int> { [RuneTrait.Red] = 3 }) } };
        var command = new PlayCardCommand("HOST", no, [], Destination: "BASE");
        var rejected = await new CoreRuleEngine().ResolveAsync(state, new("wrong-color", "P1", command.CmdType), command, default);
        Assert.False(rejected.Accepted); Assert.Empty(rejected.Events); Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(rejected.State));
    }

    [Theory]
    [InlineData("VEN·041", true)] [InlineData("VEN·041", false)]
    [InlineData("VEN·171", true)] [InlineData("VEN·171", false)]
    public async Task AttachmentChangesInResponseDetermineDamageAtResolution(string no, bool detach)
    {
        var state = AttackDamageDistributionTests.Spell(Position(no, detach ? 1 : 0), "ADJUST", "SFD·011/221", "P1");
        var game = new Driver(state); await AttackAndChoose(game);
        var gear = detach ? "WEAPON-1" : "GEAR";
        await game.Act("P1", new PlayCardCommand("ADJUST", "SFD·011/221", ["HOST", gear])); await game.Top();
        Assert.Equal(!detach, game.State.CardObjects[gear].AttachedToObjectId == "HOST");
        await game.Top(); Assert.Equal(detach ? 0 : 2, game.State.CardObjects["ENEMY1"].Damage); await game.Replay();
    }

    [Theory]
    [InlineData("VEN·041")] [InlineData("VEN·171")]
    public async Task InvalidFriendlyTargetDoesNotChangeTheConfirmationState(string no)
    {
        var game = new Driver(Position(no, 1));
        await game.Act("P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F", []));
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        var command = new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["HOST"]);
        var before = MatchStateHasher.Hash(game.State);
        var rejected = await new CoreRuleEngine().ResolveAsync(game.State, new("bad-target", "P1", command.CmdType), command, default);
        Assert.False(rejected.Accepted); Assert.Empty(rejected.Events); Assert.Equal(before, MatchStateHasher.Hash(rejected.State));
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ENEMY1"]));
        await game.Top(); await game.Replay();
    }

    [Theory]
    [InlineData("VEN·041", true)] [InlineData("VEN·041", false)]
    [InlineData("VEN·171", true)] [InlineData("VEN·171", false)]
    public async Task WardIsPaidAfterChoosingTheTargetOrTheSkillIsDeclined(string no, bool pay)
    {
        var state = Position(no, 1);
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["ENEMY1"] = state.CardObjects["ENEMY1"] with { Tags = [..state.CardObjects["ENEMY1"].Tags, "法盾2"] } } };
        var game = new Driver(state); var before = game.State.RunePools["P1"].TotalPower;
        await AttackAndChoose(game);
        var payment = Assert.IsType<PendingPaymentState>(game.State.PendingPayment);
        Assert.Equal(2, payment.PowerCost); Assert.Equal(0, game.State.CardObjects["ENEMY1"].Damage);
        await game.Act("P1", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, [pay ? "PAY" : "DECLINE"]));
        if (pay) await game.Top();
        Assert.Equal(pay ? before - 2 : before, game.State.RunePools["P1"].TotalPower);
        Assert.Equal(pay ? 2 : 0, game.State.CardObjects["ENEMY1"].Damage); await game.Replay();
    }

    [Fact]
    public async Task ReturningSourceToHandMakesHereInvalidWithoutExposingItsNewIdentity()
    {
        var state = AttackDamageDistributionTests.Spell(Position("VEN·041", 1), "RETURN", "OGN·104/298", "P1");
        var game = new Driver(state); await AttackAndChoose(game);
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["HOST"])); await game.Top();
        var opponent = ResolutionResult.BuildSnapshots(game.State)["P2"];
        var spectator = MatchReplayRedactor.BuildSpectatorFrame(game.State.RoomId, game.State.Tick, 0, [], game.State);
        Assert.DoesNotContain("\"HOST\"", JsonSerializer.Serialize(opponent.Stack));
        Assert.DoesNotContain("\"HOST\"", JsonSerializer.Serialize(spectator.SpectatorSnapshot.Stack));
        await game.Top(); Assert.Equal(0, game.State.CardObjects["ENEMY1"].Damage);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "TRIGGER_RESOLVED" && Equals(e.Event.Payload.GetValueOrDefault("sourceObjectId"), "HOST"));
        await game.Replay();
    }

    [Theory]
    [InlineData("VEN·041")] [InlineData("VEN·171")]
    public async Task CopiedRuleTextCreatesTwoIndependentDamageEvents(string no)
    {
        var initial = Position(no, 1);
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["GEAR"] = initial.CardObjects["GEAR"] with { CardNo = "SFD·059/221", AttachedToObjectId = "HOST", AttachmentRevision = 1,
                AttachedRuleText = new(new("HOST", initial.CardObjects["HOST"].ObjectGeneration), no, 1, initial.Tick, 0) } } };
        var game = new Driver(initial);
        await game.Act("P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F", []));
        var ids = game.State.TriggerQueue.Select(trigger => trigger.TriggerId).ToArray();
        Assert.Equal(2, ids.Length);
        await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: ids));
        for (var i = 0; i < 2; i++) {
            var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
            await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ENEMY1"]));
        }
        Assert.Single(game.State.StackItems, item => item.FieldContext?.Origin is not null);
        await game.Top();
        Assert.Equal(4, game.State.CardObjects["ENEMY1"].Damage);
        await game.Top();
        Assert.Equal(8, game.State.CardObjects["ENEMY1"].Damage);
        Assert.Equal(2, game.Events().Count(e => e.Event.Kind == "DAMAGE_APPLIED" && Equals(e.Event.Payload.GetValueOrDefault("targetObjectId"), "ENEMY1")));
        await game.Replay();
    }

    [Theory]
    [InlineData("VEN·041")] [InlineData("VEN·171")]
    public async Task AnAttackWithoutALegalEnemyDiscardsTheSkillAndResumesTheDuel(string no)
    {
        var initial = Position(no, 1);
        foreach (var id in new[] { "ENEMY1", "ENEMY2", "ENEMY3" })
            initial = LocalDestructionRecallTests.At(initial, id, "P2", "BASE");
        var game = new Driver(initial);
        await game.Act("P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F", []));
        Assert.Null(game.State.PendingCardChoice);
        Assert.Empty(game.State.StackItems);
        Assert.NotNull(game.State.FocusPlayerId);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "DAMAGE_APPLIED");
        await game.Replay();
    }

    [Theory]
    [InlineData("source-generation")] [InlineData("location")] [InlineData("damage")] [InlineData("repeat")]
    public async Task RecoveryRejectsForgedCapturedWeaponSkills(string change)
    {
        var game = new Driver(Position("VEN·041", 1));
        await AttackAndChoose(game);
        var item = Assert.Single(game.State.StackItems);
        var context = item.FieldContext!;
        var forged = change switch {
            "source-generation" => item with { FieldContext = context with { SourceGeneration = context.SourceGeneration + 1 } },
            "location" => item with { FieldContext = context with { SourceLocation = null } },
            "damage" => item with { DamageAmount = 99 },
            _ => item with { EffectRepeatCount = 2 } };
        Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(game.State with { StackItems = [forged] }));
    }
}
