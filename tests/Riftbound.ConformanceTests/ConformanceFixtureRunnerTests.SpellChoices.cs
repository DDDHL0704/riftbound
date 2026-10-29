using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Engine;
using Riftbound.Contracts;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed partial class ConformanceFixtureRunnerTests
{

    [Fact]
    public async Task CoreRuleEngineMirrorImageDoesNotTriggerCopiedCardPlayEffects()
    {
        var state = PunishmentState(mana: 3) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-MIRROR-IMAGE"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-MIRROR-VANGUARD"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-SPELL-MIRROR-IMAGE"] = new(
                    "P1-SPELL-MIRROR-IMAGE",
                    cardNo: "UNL-200/219",
                    tags: [CardObjectTags.SpellCard],
                    manaCost: 3,
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-MIRROR-VANGUARD"] = new(
                    "P2-MIRROR-VANGUARD",
                    cardNo: "OGN·218/298",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2")
            }
        };
        state = PrintedCostFixture.Add(state, "P1", "UNL-200/219");
        var engine = new CoreRuleEngine();
        var playResult = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-mirror-image-copy-on-play-source", "P1", "PLAY_CARD"),
            new PlayCardCommand("P1-SPELL-MIRROR-IMAGE", "UNL-200/219", ["P2-MIRROR-VANGUARD"]),
            CancellationToken.None);
        var p1Pass = await engine.ResolveAsync(
            playResult.State,
            new PlayerIntent("intent-mirror-image-copy-on-play-source-p1-pass", "P1", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-mirror-image-copy-on-play-source-p2-pass", "P2", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(playResult.Accepted, playResult.ErrorMessage);
        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);
        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);
        Assert.Single(p2Pass.Events, gameEvent => string.Equals(gameEvent.Kind, "UNIT_TOKEN_CREATED", StringComparison.Ordinal));
        var copied = await OfficialGraveyardRecastTests.Top(p2Pass.State);
        Assert.Equal("OGN·218/298", copied.State.CardObjects["P1-SPELL-MIRROR-IMAGE-TOKEN-001"].CardNo);
        Assert.Single(copied.State.PlayerZones["P1"].Base);
        Assert.DoesNotContain(copied.Events, e => e.Kind is "UNIT_TOKEN_CREATED" or "CARD_DRAWN" or "SCORE_GAINED");
        Assert.DoesNotContain(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "CARD_DRAWN", StringComparison.Ordinal)
            || string.Equals(gameEvent.Kind, "SCORE_GAINED", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CoreRuleEnginePlaysSkullcrackStunsFriendlyAndEnemyBattlefieldUnits()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-skullcrack-stun-friendly-and-enemy-battlefield-units.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Contains("STUNNED", result.FinalState.CardObjects["P1-BATTLEFIELD-UNIT-001"].UntilEndOfTurnEffects);
        Assert.Contains("STUNNED", result.FinalState.CardObjects["P2-BATTLEFIELD-UNIT-001"].UntilEndOfTurnEffects);
        Assert.Equal(
            2,
            result.EventKinds.Count(kind => string.Equals(kind, "STATUS_EFFECT_APPLIED", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task CoreRuleEnginePlaysHeroicChargePowerPlusAndStunsEnemy()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-heroic-charge-power-plus-stun.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(4, result.FinalState.CardObjects["P1-HEROIC-FRIENDLY-001"].Power);
        Assert.Equal(1, result.FinalState.CardObjects["P1-HEROIC-FRIENDLY-001"].UntilEndOfTurnPowerModifier);
        Assert.Contains("STUNNED", result.FinalState.CardObjects["P2-HEROIC-ENEMY-001"].UntilEndOfTurnEffects);
        Assert.DoesNotContain("STUNNED", result.FinalState.CardObjects["P1-HEROIC-FRIENDLY-001"].UntilEndOfTurnEffects);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysKnockdownFriendlyPowerDamage()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-knockdown-friendly-power-damage.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(5, result.FinalState.CardObjects["P2-KNOCKDOWN-ENEMY-001"].Damage);
        Assert.Equal(["P1-KNOCKDOWN-FRIENDLY-001", "P1-KNOCKDOWN-WEAPON-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(
            1,
            result.EventKinds.Count(kind => string.Equals(kind, "DAMAGE_APPLIED", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task CoreRuleEngineRejectsKnockdownWhenSecondTargetIsFriendlyUnit()
    {
        var state = PunishmentState(mana: 3) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-KNOCKDOWN"],
                    Base = ["P1-KNOCKDOWN-FRIENDLY-001", "P1-KNOCKDOWN-FRIENDLY-002"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-KNOCKDOWN-FRIENDLY-001"] = new(
                    "P1-KNOCKDOWN-FRIENDLY-001",
                    power: 4,
                    tags: [CardObjectTags.UnitCard]),
                ["P1-KNOCKDOWN-FRIENDLY-002"] = new(
                    "P1-KNOCKDOWN-FRIENDLY-002",
                    power: 3,
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-knockdown-friendly-second-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-KNOCKDOWN",
                "SFD·107/221",
                ["P1-KNOCKDOWN-FRIENDLY-001", "P1-KNOCKDOWN-FRIENDLY-002"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(3, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-KNOCKDOWN"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-KNOCKDOWN-FRIENDLY-001", "P1-KNOCKDOWN-FRIENDLY-002"], result.State.PlayerZones["P1"].Base);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysAlphaStrikeSingleEnemyPowerDamage()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-alpha-strike-single-enemy-power-damage.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(5, result.FinalState.CardObjects["P2-ALPHA-ENEMY-001"].Damage);
        Assert.Equal(["P1-ALPHA-FRIENDLY-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(
            1,
            result.EventKinds.Count(kind => string.Equals(kind, "DAMAGE_APPLIED", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task CoreRuleEngineRejectsAlphaStrikeWhenEnemyTargetIsNotBattlefieldUnit()
    {
        var state = PunishmentState(mana: 3) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-ALPHA-STRIKE"],
                    Base = ["P1-ALPHA-FRIENDLY-001"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Base = ["P2-ALPHA-ENEMY-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-ALPHA-FRIENDLY-001"] = new(
                    "P1-ALPHA-FRIENDLY-001",
                    power: 5,
                    tags: [CardObjectTags.UnitCard]),
                ["P2-ALPHA-ENEMY-001"] = new(
                    "P2-ALPHA-ENEMY-001",
                    power: 6,
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-alpha-strike-base-enemy-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-ALPHA-STRIKE",
                "UNL-192/219",
                ["P1-ALPHA-FRIENDLY-001", "P2-ALPHA-ENEMY-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(3, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-ALPHA-STRIKE"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-ALPHA-FRIENDLY-001"], result.State.PlayerZones["P1"].Base);
        Assert.Equal(["P2-ALPHA-ENEMY-001"], result.State.PlayerZones["P2"].Base);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysDangerTemperatureBuffsOnlyFriendlyMechanicalUnits()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-danger-temperature-mechanical-power-plus-1.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(3, result.FinalState.CardObjects["P1-MECHANICAL-BASE"].Power);
        Assert.Equal(5, result.FinalState.CardObjects["P1-MECHANICAL-BATTLEFIELD"].Power);
        Assert.Equal(3, result.FinalState.CardObjects["P1-NON-MECHANICAL-BASE"].Power);
        Assert.Equal(4, result.FinalState.CardObjects["P2-MECHANICAL-BASE"].Power);
        Assert.Equal(
            2,
            result.EventKinds.Count(kind => string.Equals(kind, "POWER_MODIFIED_UNTIL_END_OF_TURN", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task CoreRuleEnginePlaysSiphonEnergyBattlefieldPowerSplit()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-siphon-energy-battlefield-power-split.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(3, result.FinalState.CardObjects["P1-BATTLEFIELD-UNIT-001"].Power);
        Assert.Equal(6, result.FinalState.CardObjects["P1-BATTLEFIELD-UNIT-002"].Power);
        Assert.Equal(2, result.FinalState.CardObjects["P2-BATTLEFIELD-UNIT-001"].Power);
        Assert.Equal(1, result.FinalState.CardObjects["P2-BATTLEFIELD-UNIT-002"].Power);
        Assert.Equal(4, result.FinalState.CardObjects["P1-BASE-UNIT-001"].Power);
        Assert.Equal(3, result.FinalState.CardObjects["P2-BASE-UNIT-001"].Power);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysMoonRiseEnemyBattlefieldPowerMinus2()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-moonrise-enemy-battlefield-power-minus-2.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(2, result.FinalState.CardObjects["P2-BATTLEFIELD-UNIT-001"].Power);
        Assert.Equal(-1, result.FinalState.CardObjects["P2-BATTLEFIELD-UNIT-002"].Power);
        Assert.Equal(3, result.FinalState.CardObjects["P1-BATTLEFIELD-UNIT-001"].Power);
        Assert.Equal(4, result.FinalState.CardObjects["P1-BASE-UNIT-001"].Power);
        Assert.Equal(5, result.FinalState.CardObjects["P2-BASE-UNIT-001"].Power);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysWellTrainedPowerDrawThroughStack()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-well-trained-power-draw-stack.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(5, result.FinalState.CardObjects["P2-UNIT-001"].Power);
        Assert.Equal(2, result.FinalState.CardObjects["P2-UNIT-001"].UntilEndOfTurnPowerModifier);
        Assert.Equal(["P1-DRAW-001"], result.FinalState.PlayerZones["P1"].Hand);
    }

    [Fact]
    public async Task CoreRuleEngineReducesSpellCostForBattlefieldEagerApprentice()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-eager-apprentice-spell-cost-reduction.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        var costPaid = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Equal(1, costPaid.Payload["mana"]);
        Assert.Equal(1, costPaid.Payload["battlefieldSpellCostReductionMana"]);
    }

    [Fact]
    public async Task CoreRuleEngineReadiesArenaServiceCrewWhenEquipmentPlayed()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-arena-service-crew-equipment-ready.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.False(result.FinalState.CardObjects["P1-UNIT-ARENA-SERVICE-CREW"].IsExhausted);
        var triggerEvent = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "ARENA_SERVICE_CREW_EQUIPMENT_READY", StringComparison.Ordinal));
        Assert.Equal("P1-UNIT-ARENA-SERVICE-CREW", triggerEvent.Payload["triggerSourceObjectId"]);
        var readyEvent = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_READIED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["reason"] as string, "ARENA_SERVICE_CREW_EQUIPMENT_READY", StringComparison.Ordinal));
        Assert.Equal("P1-UNIT-ARENA-SERVICE-CREW", readyEvent.Payload["targetObjectId"]);
    }

    [Fact]
    public async Task CoreRuleEngineExpiresWellTrainedPowerAtEndTurn()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-well-trained-power-expires-end-turn.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(3, result.FinalState.CardObjects["P2-UNIT-001"].Power);
        Assert.Equal(0, result.FinalState.CardObjects["P2-UNIT-001"].UntilEndOfTurnPowerModifier);
        Assert.Contains("POWER_MODIFIER_EXPIRED", result.EventKinds);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysPracticalExperiencePowerBoost()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-practical-experience-power-plus-1.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(3, result.FinalState.CardObjects["P2-BATTLEFIELD-UNIT-001"].Power);
        Assert.Equal(1, result.FinalState.CardObjects["P2-BATTLEFIELD-UNIT-001"].UntilEndOfTurnPowerModifier);
    }

    [Fact]
    public async Task CoreRuleEngineTriggersRavenbloomStudentWhenSpellPlayed()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-ravenbloom-student-spell-power-plus-1.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(3, result.FinalState.CardObjects["P1-UNIT-RAVENBLOOM-STUDENT"].Power);
        Assert.Equal(1, result.FinalState.CardObjects["P1-UNIT-RAVENBLOOM-STUDENT"].UntilEndOfTurnPowerModifier);
        Assert.Contains(result.Events, evt =>
            string.Equals(evt.Kind, "TRIGGER_RESOLVED", StringComparison.Ordinal)
            && evt.Payload.TryGetValue("trigger", out var trigger)
            && string.Equals(trigger as string, TriggerKinds.UnitSpellPlayedPowerModifier, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("UNL-149/219")]
    [InlineData("UNL-149a/219")]
    public async Task CoreRuleEngineTriggersDianaUnitSpellPlayedPowerModifierWhenSpellPlayed(string dianaCardNo)
    {
        var state = DianaSpellTriggerState(dianaCardNo);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent($"intent-diana-{dianaCardNo}-spell-trigger", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-PRACTICAL-EXPERIENCE",
                "UNL-031/219",
                ["P2-BATTLEFIELD-UNIT-001"]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        result = await SpellResolutionTestDriver.Finish(result);
        var diana = result.State.CardObjects["P1-UNIT-DIANA"];
        Assert.Equal(5, diana.Power);
        Assert.Equal(2, diana.UntilEndOfTurnPowerModifier);
        Assert.Contains(result.Events, evt =>
            string.Equals(evt.Kind, "TRIGGER_RESOLVED", StringComparison.Ordinal)
            && evt.Payload.TryGetValue("trigger", out var trigger)
            && string.Equals(trigger as string, TriggerKinds.UnitSpellPlayedPowerModifier, StringComparison.Ordinal)
            && evt.Payload.TryGetValue("triggerSourceObjectId", out var triggerSourceObjectId)
            && string.Equals(triggerSourceObjectId as string, "P1-UNIT-DIANA", StringComparison.Ordinal)
            && evt.Payload.TryGetValue("powerDelta", out var powerDelta)
            && Equals(powerDelta, 2));
        Assert.Contains(result.Events, evt =>
            string.Equals(evt.Kind, "POWER_MODIFIED_UNTIL_END_OF_TURN", StringComparison.Ordinal)
            && string.Equals(evt.Payload["targetObjectId"] as string, "P1-UNIT-DIANA", StringComparison.Ordinal)
            && Equals(evt.Payload["powerDelta"], 2)
            && Equals(evt.Payload["resultingPower"], 5));
    }

    [Fact]
    public async Task CoreRuleEngineSkipsRavenbloomStudentSpellTriggerWhenSourceIsStandby()
    {
        var state = WithFaceDownStandby(RavenbloomStudentSpellTriggerState(), "P1-UNIT-RAVENBLOOM-STUDENT");

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-ravenbloom-standby-spell-trigger", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-PRACTICAL-EXPERIENCE",
                "UNL-031/219",
                ["P2-BATTLEFIELD-UNIT-001"]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        var ravenbloom = result.State.CardObjects["P1-UNIT-RAVENBLOOM-STUDENT"];
        Assert.Equal(2, ravenbloom.Power);
        Assert.Equal(0, ravenbloom.UntilEndOfTurnPowerModifier);
        Assert.DoesNotContain(result.Events, evt =>
            string.Equals(evt.Kind, "TRIGGER_RESOLVED", StringComparison.Ordinal)
            && evt.Payload.TryGetValue("trigger", out var trigger)
            && string.Equals(trigger as string, TriggerKinds.UnitSpellPlayedPowerModifier, StringComparison.Ordinal));
        Assert.DoesNotContain(result.Events, evt =>
            string.Equals(evt.Kind, "POWER_MODIFIED_UNTIL_END_OF_TURN", StringComparison.Ordinal)
            && string.Equals(evt.Payload["targetObjectId"] as string, "P1-UNIT-RAVENBLOOM-STUDENT", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CoreRuleEnginePlaysDuelingStanceFriendlyPowerBoost()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-dueling-stance-friendly-power-plus-1.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(3, result.FinalState.CardObjects["P1-UNIT-DUELING-001"].Power);
        Assert.Equal(1, result.FinalState.CardObjects["P1-UNIT-DUELING-001"].UntilEndOfTurnPowerModifier);
    }

    [Fact]
    public async Task CoreRuleEngineRepeatsSavageStrengthPowerWithEcho()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-savage-strength-echo-power-stack.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(7, result.FinalState.CardObjects["P2-UNIT-001"].Power);
        Assert.Equal(4, result.FinalState.CardObjects["P2-UNIT-001"].UntilEndOfTurnPowerModifier);
        Assert.Equal(
            2,
            result.EventKinds.Count(kind => string.Equals(kind, "POWER_MODIFIED_UNTIL_END_OF_TURN", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task CoreRuleEngineRepeatsFreezePowerPenaltyWithEcho()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-freeze-echo-power-minus-2.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(2, result.FinalState.CardObjects["P2-BATTLEFIELD-UNIT-001"].Power);
        Assert.Equal(-4, result.FinalState.CardObjects["P2-BATTLEFIELD-UNIT-001"].UntilEndOfTurnPowerModifier);
        Assert.Equal(
            2,
            result.EventKinds.Count(kind => string.Equals(kind, "POWER_MODIFIED_UNTIL_END_OF_TURN", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task CoreRuleEnginePlaysDistanceBreakDanceSplitPowerModifiers()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-distance-break-dance-split-power-modifiers.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(4, result.FinalState.CardObjects["P1-BASE-UNIT-001"].Power);
        Assert.Equal(2, result.FinalState.CardObjects["P1-BASE-UNIT-001"].UntilEndOfTurnPowerModifier);
        Assert.Equal(2, result.FinalState.CardObjects["P2-BATTLEFIELD-UNIT-001"].Power);
        Assert.Equal(-2, result.FinalState.CardObjects["P2-BATTLEFIELD-UNIT-001"].UntilEndOfTurnPowerModifier);
        Assert.Equal(
            2,
            result.EventKinds.Count(kind => string.Equals(kind, "POWER_MODIFIED_UNTIL_END_OF_TURN", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task CoreRuleEnginePlaysShootFirstPowerBoost()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-shoot-first-power-plus-5-stack.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(7, result.FinalState.CardObjects["P2-UNIT-001"].Power);
        Assert.Equal(5, result.FinalState.CardObjects["P2-UNIT-001"].UntilEndOfTurnPowerModifier);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysGloryCallPowerBoost()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-glory-call-power-plus-3.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(5, result.FinalState.CardObjects["P2-BATTLEFIELD-UNIT-001"].Power);
        Assert.Equal(3, result.FinalState.CardObjects["P2-BATTLEFIELD-UNIT-001"].UntilEndOfTurnPowerModifier);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysSecretArtMercyGrantBoon()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-secret-art-mercy-grant-boon.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(3, result.FinalState.CardObjects["P1-BOON-UNIT-001"].Power);
        Assert.Equal(4, SecretArtMercyCurrentTests.Power(result.FinalState, "P1-BOON-UNIT-001"));
        Assert.Equal([CardObjectTags.UnitCard, CardObjectTags.Boon], result.FinalState.CardObjects["P1-BOON-UNIT-001"].Tags);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysStunningDisplayBoonMoveBaseUnit()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-stunning-display-boon-move-base-unit.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Empty(result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(["P1-BASE-BOON-UNIT-001"], result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(3, result.FinalState.CardObjects["P1-BASE-BOON-UNIT-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard, CardObjectTags.Boon], result.FinalState.CardObjects["P1-BASE-BOON-UNIT-001"].Tags);
    }

    [Fact]
    public async Task CoreRuleEngineStunningDisplayResolutionSkipsOpponentControlledFriendlyBaseTarget()
    {
        var state = PunishmentState(mana: 0) with
        {
            TimingState = TimingStates.NeutralClosed,
            ActivePlayerId = "P1",
            PriorityPlayerId = "P1",
            StackItems =
            [
                new StackItemState(
                    "STACK-STUNNING-DISPLAY-DIRTY-BASE",
                    "P1",
                    "P1-SPELL-STUNNING-DISPLAY",
                    "STUNNING_DISPLAY_GRANT_BOON_MOVE_BASE_UNIT_TO_BATTLEFIELD",
                    "OGN·270/298",
                    ["P1-DIRTY-P2-CONTROLLED-BASE-UNIT"])
            ],
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-DIRTY-P2-CONTROLLED-BASE-UNIT"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-DIRTY-P2-CONTROLLED-BASE-UNIT"] = new(
                    "P1-DIRTY-P2-CONTROLLED-BASE-UNIT",
                    cardNo: "SFD·125/221",
                    manaCost: 3,
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2")
            }
        };
        var engine = new CoreRuleEngine();

        var p1Pass = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-stunning-display-dirty-resolution-p1-pass", "P1", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-stunning-display-dirty-resolution-p2-pass", "P2", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p1Pass.Accepted);
        Assert.True(p2Pass.Accepted);
        Assert.Equal(["P1-DIRTY-P2-CONTROLLED-BASE-UNIT"], p2Pass.State.PlayerZones["P1"].Base);
        Assert.Empty(p2Pass.State.PlayerZones["P1"].Battlefields);
        Assert.Equal(["P1-SPELL-STUNNING-DISPLAY"], p2Pass.State.PlayerZones["P1"].Graveyard);
        Assert.Equal(2, p2Pass.State.CardObjects["P1-DIRTY-P2-CONTROLLED-BASE-UNIT"].Power);
        Assert.Equal([CardObjectTags.UnitCard], p2Pass.State.CardObjects["P1-DIRTY-P2-CONTROLLED-BASE-UNIT"].Tags);
        Assert.DoesNotContain(p2Pass.Events, gameEvent => string.Equals(gameEvent.Kind, "BOON_GRANTED", StringComparison.Ordinal));
        Assert.DoesNotContain(p2Pass.Events, gameEvent => string.Equals(gameEvent.Kind, "OBJECT_TAG_ADDED", StringComparison.Ordinal));
        Assert.DoesNotContain(p2Pass.Events, gameEvent => string.Equals(gameEvent.Kind, "UNIT_MOVED_TO_BATTLEFIELD", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CoreRuleEnginePlaysOpenActionGrantAllBoons()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-open-action-grant-all-boons.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(2, result.FinalState.CardObjects["P1-OPEN-ACTION-BASE-001"].Power);
        Assert.Equal(3, result.FinalState.CardObjects["P1-OPEN-ACTION-BATTLEFIELD-001"].Power);
        Assert.Equal(2, result.FinalState.CardObjects["P2-UNIT-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard, CardObjectTags.Boon], result.FinalState.CardObjects["P1-OPEN-ACTION-BASE-001"].Tags);
        Assert.Equal([CardObjectTags.UnitCard, CardObjectTags.Boon], result.FinalState.CardObjects["P1-OPEN-ACTION-BATTLEFIELD-001"].Tags);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysDecisiveStrikeAllFriendlyPowerBoost()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-decisive-strike-all-friendly-power-plus-2.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(4, result.FinalState.CardObjects["P1-BASE-UNIT-001"].Power);
        Assert.Equal(5, result.FinalState.CardObjects["P1-BATTLEFIELD-UNIT-001"].Power);
        Assert.Equal(4, result.FinalState.CardObjects["P2-BATTLEFIELD-UNIT-001"].Power);
        Assert.Equal(
            2,
            result.EventKinds.Count(kind => string.Equals(kind, "POWER_MODIFIED_UNTIL_END_OF_TURN", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task CoreRuleEnginePlaysTremendousStrengthPowerBoost()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-tremendous-strength-power-plus-7.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(9, result.FinalState.CardObjects["P2-BATTLEFIELD-UNIT-001"].Power);
        Assert.Equal(7, result.FinalState.CardObjects["P2-BATTLEFIELD-UNIT-001"].UntilEndOfTurnPowerModifier);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysMoonfallPowerPenalty()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-moonfall-power-minus-10.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(2, result.FinalState.CardObjects["P2-BATTLEFIELD-UNIT-001"].Power);
        Assert.Equal(-10, result.FinalState.CardObjects["P2-BATTLEFIELD-UNIT-001"].UntilEndOfTurnPowerModifier);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysGrandStrategyAllFriendlyPowerBoost()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-grand-strategy-all-friendly-power-plus-5.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(7, result.FinalState.CardObjects["P1-BASE-UNIT-001"].Power);
        Assert.Equal(8, result.FinalState.CardObjects["P1-BATTLEFIELD-UNIT-001"].Power);
        Assert.Equal(4, result.FinalState.CardObjects["P2-BATTLEFIELD-UNIT-001"].Power);
        Assert.Equal(
            2,
            result.EventKinds.Count(kind => string.Equals(kind, "POWER_MODIFIED_UNTIL_END_OF_TURN", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task CoreRuleEnginePlaysBackToBackTwoFriendlyPowerBoosts()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-back-to-back-two-friendly-power-plus-2.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(4, result.FinalState.CardObjects["P1-BASE-UNIT-001"].Power);
        Assert.Equal(6, result.FinalState.CardObjects["P1-BATTLEFIELD-UNIT-001"].Power);
        Assert.Equal(5, result.FinalState.CardObjects["P2-BATTLEFIELD-UNIT-001"].Power);
        Assert.Equal(
            2,
            result.EventKinds.Count(kind => string.Equals(kind, "POWER_MODIFIED_UNTIL_END_OF_TURN", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task CoreRuleEnginePlaysPowerBindEchoTwoFriendlyPowerBoosts()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-power-bind-echo-two-friendly-power-plus-1.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(4, result.FinalState.CardObjects["P1-BASE-UNIT-001"].Power);
        Assert.Equal(6, result.FinalState.CardObjects["P1-BATTLEFIELD-UNIT-001"].Power);
        Assert.Equal(5, result.FinalState.CardObjects["P2-BATTLEFIELD-UNIT-001"].Power);
        Assert.Equal(
            4,
            result.EventKinds.Count(kind => string.Equals(kind, "POWER_MODIFIED_UNTIL_END_OF_TURN", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task PowerModifierAppliedOrderFollowsPowerBindEchoAppendSequence()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-power-bind-echo-two-friendly-power-plus-1.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        var baseUnit = result.FinalState.CardObjects["P1-BASE-UNIT-001"];
        Assert.Equal(2, baseUnit.UntilEndOfTurnPowerModifiers.Count);
        Assert.Equal(
            [1, 2],
            baseUnit.UntilEndOfTurnPowerModifiers
                .Select(modifier => modifier.AppliedOrder.GetValueOrDefault())
                .ToArray());

        var baseUnitEffects = result.FinalState.ContinuousEffects
            .Where(effect => string.Equals(effect.Layer, ContinuousEffectLayers.PowerModifier, StringComparison.Ordinal)
                && string.Equals(effect.TargetObjectId, "P1-BASE-UNIT-001", StringComparison.Ordinal))
            .ToArray();
        Assert.Equal(2, baseUnitEffects.Length);
        Assert.Equal(
            [1, 2],
            baseUnitEffects
                .Select(effect => effect.AppliedOrder.GetValueOrDefault())
                .ToArray());

        var snapshot = ResolutionResult.BuildSnapshots(result.FinalState)["P1"];
        var continuousEffects = Assert.IsAssignableFrom<IReadOnlyList<Dictionary<string, object?>>>(
            snapshot.Timing["continuousEffects"]);
        var baseUnitEffectViews = continuousEffects
            .Where(effect => string.Equals(effect["layer"] as string, ContinuousEffectLayers.PowerModifier, StringComparison.Ordinal)
                && string.Equals(effect["targetObjectId"] as string, "P1-BASE-UNIT-001", StringComparison.Ordinal))
            .ToArray();
        Assert.Equal(2, baseUnitEffectViews.Length);
        Assert.Equal(
            [1, 2],
            baseUnitEffectViews
                .Select(effect => Assert.IsType<int>(effect["appliedOrder"]))
                .ToArray());
    }

    [Fact]
    public async Task PowerModifierMinimumPowerAppliedOrderSkipsZeroFloorSequence()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-minimum-power-ordering-sequence.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));

        var zeroAppliedEvent = Assert.Single(
            result.Events,
            gameEvent => string.Equals(gameEvent.Kind, "POWER_MODIFIED_UNTIL_END_OF_TURN", StringComparison.Ordinal)
                && string.Equals(gameEvent.Payload["sourceObjectId"] as string, "P1-SPELL-EXTORTION", StringComparison.Ordinal));
        Assert.Equal(0, Assert.IsType<int>(zeroAppliedEvent.Payload["appliedPowerDelta"]));
        Assert.Equal(1, Assert.IsType<int>(zeroAppliedEvent.Payload["minimumPower"]));
        Assert.Equal(1, Assert.IsType<int>(zeroAppliedEvent.Payload["resultingPower"]));

        var targetState = result.FinalState.CardObjects["P1-TARGET-UNIT"];
        Assert.Equal(2, targetState.Power);
        Assert.Equal(-1, targetState.UntilEndOfTurnPowerModifier);
        Assert.Equal(2, targetState.UntilEndOfTurnPowerModifiers.Count);
        Assert.Equal(
            ["SMOKE_BOMB_POWER_MINUS_4_MIN_1", "POWER_BIND_TWO_FRIENDLY_POWER_PLUS_1"],
            targetState.UntilEndOfTurnPowerModifiers.Select(modifier => modifier.EffectKind).ToArray());
        Assert.Equal([-2, 1], targetState.UntilEndOfTurnPowerModifiers.Select(modifier => modifier.PowerDelta).ToArray());
        Assert.Equal([-4, 1], targetState.UntilEndOfTurnPowerModifiers.Select(modifier => modifier.RequestedPowerDelta).ToArray());
        Assert.Equal([1, 0], targetState.UntilEndOfTurnPowerModifiers.Select(modifier => modifier.MinimumPower).ToArray());
        Assert.Equal([1, 2], targetState.UntilEndOfTurnPowerModifiers.Select(modifier => modifier.ResultingPower).ToArray());
        Assert.Equal([3, 3], targetState.UntilEndOfTurnPowerModifiers.Select(modifier => modifier.BasePower).ToArray());
        Assert.Equal([1, 2], targetState.UntilEndOfTurnPowerModifiers.Select(modifier => modifier.EffectivePower).ToArray());
        Assert.Equal(
            [1, 2],
            targetState.UntilEndOfTurnPowerModifiers
                .Select(modifier => modifier.AppliedOrder.GetValueOrDefault())
                .ToArray());
        Assert.DoesNotContain(
            targetState.UntilEndOfTurnPowerModifiers,
            modifier => string.Equals(modifier.EffectKind, "EXTORTION_POWER_MINUS_1_MIN_1_DRAW_1", StringComparison.Ordinal));

        var targetEffects = result.FinalState.ContinuousEffects
            .Where(effect => string.Equals(effect.Layer, ContinuousEffectLayers.PowerModifier, StringComparison.Ordinal)
                && string.Equals(effect.TargetObjectId, "P1-TARGET-UNIT", StringComparison.Ordinal))
            .ToArray();
        Assert.Equal(2, targetEffects.Length);
        Assert.Equal(
            ["SMOKE_BOMB_POWER_MINUS_4_MIN_1", "POWER_BIND_TWO_FRIENDLY_POWER_PLUS_1"],
            targetEffects.Select(effect => effect.EffectKind).ToArray());
        Assert.Equal([-2, 1], targetEffects.Select(effect => effect.PowerDelta).ToArray());
        Assert.Equal([-4, 1], targetEffects.Select(effect => effect.RequestedPowerDelta.GetValueOrDefault()).ToArray());
        Assert.Equal([-2, 1], targetEffects.Select(effect => effect.AppliedPowerDelta.GetValueOrDefault()).ToArray());
        Assert.Equal([1, 0], targetEffects.Select(effect => effect.MinimumPower.GetValueOrDefault()).ToArray());
        Assert.Equal([1, 2], targetEffects.Select(effect => effect.ResultingPower.GetValueOrDefault()).ToArray());
        Assert.Equal([3, 3], targetEffects.Select(effect => effect.BasePower).ToArray());
        Assert.Equal([1, 2], targetEffects.Select(effect => effect.EffectivePower).ToArray());
        Assert.Equal([1, 2], targetEffects.Select(effect => effect.AppliedOrder.GetValueOrDefault()).ToArray());

        var snapshot = ResolutionResult.BuildSnapshots(result.FinalState)["P1"];
        var continuousEffects = Assert.IsAssignableFrom<IReadOnlyList<Dictionary<string, object?>>>(
            snapshot.Timing["continuousEffects"]);
        var targetEffectViews = continuousEffects
            .Where(effect => string.Equals(effect["layer"] as string, ContinuousEffectLayers.PowerModifier, StringComparison.Ordinal)
                && string.Equals(effect["targetObjectId"] as string, "P1-TARGET-UNIT", StringComparison.Ordinal))
            .ToArray();
        Assert.Equal(2, targetEffectViews.Length);
        Assert.Equal(
            ["SMOKE_BOMB_POWER_MINUS_4_MIN_1", "POWER_BIND_TWO_FRIENDLY_POWER_PLUS_1"],
            targetEffectViews.Select(effect => Assert.IsType<string>(effect["effectKind"])).ToArray());
        Assert.Equal([-2, 1], targetEffectViews.Select(effect => Assert.IsType<int>(effect["powerDelta"])).ToArray());
        Assert.Equal([-4, 1], targetEffectViews.Select(effect => Assert.IsType<int>(effect["requestedPowerDelta"])).ToArray());
        Assert.Equal([-2, 1], targetEffectViews.Select(effect => Assert.IsType<int>(effect["appliedPowerDelta"])).ToArray());
        Assert.Equal([1, 0], targetEffectViews.Select(effect => Assert.IsType<int>(effect["minimumPower"])).ToArray());
        Assert.Equal([1, 2], targetEffectViews.Select(effect => Assert.IsType<int>(effect["resultingPower"])).ToArray());
        Assert.Equal([3, 3], targetEffectViews.Select(effect => Assert.IsType<int>(effect["basePower"])).ToArray());
        Assert.Equal([1, 2], targetEffectViews.Select(effect => Assert.IsType<int>(effect["effectivePower"])).ToArray());
        Assert.Equal([1, 2], targetEffectViews.Select(effect => Assert.IsType<int>(effect["appliedOrder"])).ToArray());
    }

    [Fact]
    public async Task CoreRuleEnginePlaysSmokeBombPowerFloorThroughStack()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-smoke-bomb-power-floor-stack.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(1, result.FinalState.CardObjects["P2-UNIT-001"].Power);
        Assert.Equal(-2, result.FinalState.CardObjects["P2-UNIT-001"].UntilEndOfTurnPowerModifier);
    }

    [Fact]
    public async Task CoreRuleEngineExpiresSmokeBombPowerFloorAtEndTurn()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-smoke-bomb-power-floor-expires-end-turn.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(3, result.FinalState.CardObjects["P2-UNIT-001"].Power);
        Assert.Equal(0, result.FinalState.CardObjects["P2-UNIT-001"].UntilEndOfTurnPowerModifier);
        Assert.Empty(result.FinalState.CardObjects["P2-UNIT-001"].UntilEndOfTurnPowerModifiers);
        Assert.DoesNotContain(
            result.FinalState.ContinuousEffects,
            effect => string.Equals(effect.Layer, ContinuousEffectLayers.PowerModifier, StringComparison.Ordinal)
                && string.Equals(effect.TargetObjectId, "P2-UNIT-001", StringComparison.Ordinal));
        var snapshot = ResolutionResult.BuildSnapshots(result.FinalState)["P1"];
        var continuousEffects = Assert.IsAssignableFrom<IReadOnlyList<Dictionary<string, object?>>>(
            snapshot.Timing["continuousEffects"]);
        Assert.DoesNotContain(
            continuousEffects,
            effect => string.Equals(effect["layer"] as string, ContinuousEffectLayers.PowerModifier, StringComparison.Ordinal)
                && string.Equals(effect["targetObjectId"] as string, "P2-UNIT-001", StringComparison.Ordinal));
        Assert.Contains("POWER_MODIFIER_EXPIRED", result.EventKinds);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysExtortionPowerFloorDrawThroughStack()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-extortion-power-floor-draw-stack.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(1, result.FinalState.CardObjects["P2-UNIT-001"].Power);
        Assert.Equal(0, result.FinalState.CardObjects["P2-UNIT-001"].UntilEndOfTurnPowerModifier);
        Assert.Empty(result.FinalState.CardObjects["P2-UNIT-001"].UntilEndOfTurnPowerModifiers);
        Assert.DoesNotContain(
            result.FinalState.ContinuousEffects,
            effect => string.Equals(effect.Layer, ContinuousEffectLayers.PowerModifier, StringComparison.Ordinal)
                && string.Equals(effect.TargetObjectId, "P2-UNIT-001", StringComparison.Ordinal));
        Assert.Equal(["P1-DRAW-001"], result.FinalState.PlayerZones["P1"].Hand);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysEclipsePowerPenalty()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-eclipse-power-minus-4.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(1, result.FinalState.CardObjects["P2-UNIT-ECLIPSE-001"].Power);
        Assert.Equal(-4, result.FinalState.CardObjects["P2-UNIT-ECLIPSE-001"].UntilEndOfTurnPowerModifier);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysEclipsePowerPenaltyAndInsightRecycle()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-eclipse-power-minus-4-insight-recycle.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(1, result.FinalState.CardObjects["P2-UNIT-ECLIPSE-INSIGHT-001"].Power);
        Assert.Equal(-4, result.FinalState.CardObjects["P2-UNIT-ECLIPSE-INSIGHT-001"].UntilEndOfTurnPowerModifier);
        Assert.Equal(
            ["P1-MAIN-KEEPER", "P1-ECLIPSE-INSIGHT-RECYCLE-001"],
            result.FinalState.PlayerZones["P1"].MainDeck);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsEclipseInsightTargetOutsideTopCard()
    {
        var state = PunishmentState(mana: 3) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    MainDeck = ["P1-TOP-001", "P1-NOT-VIEWED-001"],
                    Hand = ["P1-SPELL-ECLIPSE"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-UNIT-ECLIPSE-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-UNIT-ECLIPSE-001"] = new("P2-UNIT-ECLIPSE-001", power: 5)
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-eclipse-insight-outside-top-card", "P1", "PLAY_CARD"),
            new PlayCardCommand("P1-SPELL-ECLIPSE", "UNL-063/219", ["P2-UNIT-ECLIPSE-001", "P1-NOT-VIEWED-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(["P1-SPELL-ECLIPSE"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-TOP-001", "P1-NOT-VIEWED-001"], result.State.PlayerZones["P1"].MainDeck);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEngineDestroysBaseUnitWithVengeance()
    {
        var engine = new CoreRuleEngine();
        var state = PunishmentState(mana: 4) with
        {
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(4, 0),
                ["P2"] = RunePool.Empty
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-VENGEANCE"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Base = ["P2-BASE-UNIT-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-BASE-UNIT-001"] = new("P2-BASE-UNIT-001")
            }
        };
        state = PrintedCostFixture.Add(state, "P1", "OGN·229/298");

        var play = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-vengeance-base-play", "P1", "PLAY_CARD"),
            new PlayCardCommand("P1-SPELL-VENGEANCE", "OGN·229/298", ["P2-BASE-UNIT-001"]),
            CancellationToken.None);
        var p1Pass = await engine.ResolveAsync(
            play.State,
            new PlayerIntent("intent-vengeance-base-p1-pass", "P1", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-vengeance-base-p2-pass", "P2", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(play.Accepted);
        Assert.True(p1Pass.Accepted);
        Assert.True(p2Pass.Accepted);
        Assert.Equal(new[] { "UNIT_DESTROYED" }, p2Pass.Events.TakeLast(1).Select(gameEvent => gameEvent.Kind));
        Assert.Empty(p2Pass.State.PlayerZones["P2"].Base);
        Assert.Equal(new[] { "P2-BASE-UNIT-001" }, p2Pass.State.PlayerZones["P2"].Graveyard);
        Assert.DoesNotContain("P2-BASE-UNIT-001", p2Pass.State.CardObjects.Keys);
    }

    [Fact]
    public async Task CoreRuleEngineVengeanceResolutionSkipsOpponentControlledEnemyZoneTarget()
    {
        var state = PunishmentState(mana: 0) with
        {
            TimingState = TimingStates.NeutralClosed,
            ActivePlayerId = "P1",
            PriorityPlayerId = "P1",
            StackItems =
            [
                new StackItemState(
                    "STACK-VENGEANCE-DIRTY-DESTROY",
                    "P1",
                    "P1-SPELL-VENGEANCE",
                    "VENGEANCE_DESTROY_UNIT",
                    "OGN·229/298",
                    ["P2-DIRTY-P1-CONTROLLED-BASE-UNIT"])
            ],
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty,
                ["P2"] = PlayerZones.Empty with
                {
                    Base = ["P2-DIRTY-P1-CONTROLLED-BASE-UNIT"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-DIRTY-P1-CONTROLLED-BASE-UNIT"] = new(
                    "P2-DIRTY-P1-CONTROLLED-BASE-UNIT",
                    cardNo: "SFD·125/221",
                    manaCost: 3,
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1")
            }
        };
        var engine = new CoreRuleEngine();

        var p1Pass = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-vengeance-dirty-resolution-p1-pass", "P1", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-vengeance-dirty-resolution-p2-pass", "P2", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p1Pass.Accepted);
        Assert.True(p2Pass.Accepted);
        Assert.Equal(["P2-DIRTY-P1-CONTROLLED-BASE-UNIT"], p2Pass.State.PlayerZones["P2"].Base);
        Assert.Empty(p2Pass.State.PlayerZones["P2"].Graveyard);
        Assert.Equal(["P1-SPELL-VENGEANCE"], p2Pass.State.PlayerZones["P1"].Graveyard);
        Assert.Contains("P2-DIRTY-P1-CONTROLLED-BASE-UNIT", p2Pass.State.CardObjects.Keys);
        Assert.DoesNotContain(p2Pass.Events, gameEvent => string.Equals(gameEvent.Kind, "UNIT_DESTROYED", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CoreRuleEnginePlaysStellarConvergenceAgainstTwoTargetsThroughStack()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-stellar-convergence-two-target-damage-stack.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
    }

    [Fact]
    public async Task CoreRuleEnginePlaysRocketBarrageBaseUnitModeThroughStack()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-rocket-barrage-base-unit-mode-stack.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
    }

    [Fact]
    public async Task CoreRuleEnginePlaysRocketBarrageDestroyEquipmentModeThroughStack()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-rocket-barrage-destroy-equipment-mode.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.DoesNotContain("P2-BASE-EQUIPMENT-ROCKET-BARRAGE-001", result.FinalState.CardObjects.Keys);
        Assert.Equal(["P2-BASE-EQUIPMENT-ROCKET-BARRAGE-001"], result.FinalState.PlayerZones["P2"].Graveyard);
        Assert.Empty(result.FinalState.DestroyedUnitOwnerIdsThisTurn);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysEmergencyRecallReturnEquipment()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-emergency-recall-return-equipment.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.DoesNotContain("P2-BASE-EQUIPMENT-EMERGENCY-RECALL-001", result.FinalState.CardObjects.Keys);
        Assert.Equal(["P2-BASE-EQUIPMENT-EMERGENCY-RECALL-001"], result.FinalState.PlayerZones["P2"].Hand);
        Assert.Equal(["P1-SPELL-EMERGENCY-RECALL"], result.FinalState.PlayerZones["P1"].Graveyard);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsEmergencyRecallAgainstUnit()
    {
        var state = PunishmentState(mana: 1) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-EMERGENCY-RECALL"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Base = ["P2-BASE-EMERGENCY-RECALL-UNIT-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-BASE-EMERGENCY-RECALL-UNIT-001"] = new("P2-BASE-EMERGENCY-RECALL-UNIT-001", power: 2, tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-emergency-recall-unit-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-EMERGENCY-RECALL",
                "SFD·135/221",
                ["P2-BASE-EMERGENCY-RECALL-UNIT-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(1, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-EMERGENCY-RECALL"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P2-BASE-EMERGENCY-RECALL-UNIT-001"], result.State.PlayerZones["P2"].Base);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4EmergencyRecallUnitTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-emergency-recall-unit-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(1, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-EMERGENCY-RECALL"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P2-BASE-EMERGENCY-RECALL-UNIT-001"], result.FinalState.PlayerZones["P2"].Base);
        Assert.Equal(0, result.FinalState.CardObjects["P2-BASE-EMERGENCY-RECALL-UNIT-001"].Damage);
        Assert.Equal(2, result.FinalState.CardObjects["P2-BASE-EMERGENCY-RECALL-UNIT-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P2-BASE-EMERGENCY-RECALL-UNIT-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysThermogenicBeamDestroyAllEquipment()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-thermogenic-beam-destroy-all-equipment.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.DoesNotContain("P1-BASE-EQUIPMENT-THERMOGENIC-BEAM-001", result.FinalState.CardObjects.Keys);
        Assert.DoesNotContain("P2-BASE-EQUIPMENT-THERMOGENIC-BEAM-001", result.FinalState.CardObjects.Keys);
        Assert.Contains("P1-BATTLEFIELD-UNIT-THERMOGENIC-BEAM-001", result.FinalState.CardObjects.Keys);
        Assert.Contains("P2-BATTLEFIELD-UNIT-THERMOGENIC-BEAM-001", result.FinalState.CardObjects.Keys);
        Assert.Empty(result.FinalState.DestroyedUnitOwnerIdsThisTurn);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysPerfectFinaleDrawModeThroughStack()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-perfect-finale-draw-mode.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-DRAW-001"], result.FinalState.PlayerZones["P1"].Hand);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysPerfectFinaleBattlefieldDamageModeThroughStack()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-perfect-finale-battlefield-damage-mode.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(2, result.FinalState.CardObjects["P2-BATTLEFIELD-UNIT-001"].Damage);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysPerfectFinaleBaseDamageModeThroughStack()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-perfect-finale-base-damage-mode.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(3, result.FinalState.CardObjects["P2-BASE-UNIT-001"].Damage);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysPerfectFinaleBattlefieldPowerModeThroughStack()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-perfect-finale-battlefield-power-mode.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(1, result.FinalState.CardObjects["P2-BATTLEFIELD-UNIT-001"].Power);
        Assert.Equal(-4, result.FinalState.CardObjects["P2-BATTLEFIELD-UNIT-001"].UntilEndOfTurnPowerModifier);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysVoidSeekerThroughStack()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-void-seeker-damage-draw-stack.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
    }

    [Fact]
    public async Task CoreRuleEngineBurnsOutDuringVoidSeekerDraw()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-void-seeker-draw-burnout-stack.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
    }

    [Fact]
    public async Task CoreRuleEnginePlaysRunePrisonThroughStack()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-rune-prison-stun-stack.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
    }

    [Fact]
    public async Task CoreRuleEnginePlaysKerplunkAgainstAttackingUnit()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-kerplunk-stun-attacking-unit.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Contains("STUNNED", result.FinalState.CardObjects["P2-UNIT-001"].UntilEndOfTurnEffects);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysKerplunkEchoAgainstAttackingUnit()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-kerplunk-echo-stun-attacking-unit.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(
            1,
            result.EventKinds.Count(kind => string.Equals(kind, "STATUS_EFFECT_APPLIED", StringComparison.Ordinal)));
        Assert.Contains("STUNNED", result.FinalState.CardObjects["P2-UNIT-001"].UntilEndOfTurnEffects);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysExistentialDreadEchoStunThenReturn()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-existential-dread-echo-stun-then-return.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.DoesNotContain("P2-UNIT-001", result.FinalState.CardObjects.Keys);
        Assert.Equal(["P2-UNIT-001"], result.FinalState.PlayerZones["P2"].Hand);
        Assert.Equal(
            ["STATUS_EFFECT_APPLIED", "UNIT_RETURNED_TO_HAND"],
            result.EventKinds.Where(kind =>
                string.Equals(kind, "STATUS_EFFECT_APPLIED", StringComparison.Ordinal)
                || string.Equals(kind, "UNIT_RETURNED_TO_HAND", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task CoreRuleEnginePlaysZenithBladeAgainstEnemyBattlefieldUnit()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-zenith-blade-stun-enemy-battlefield-unit.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Contains("STUNNED", result.FinalState.CardObjects["P2-UNIT-ZENITH-001"].UntilEndOfTurnEffects);
        Assert.Equal(["P1-UNIT-ZENITH-ALLY-001"], result.FinalState.PlayerZones["P1"].Base);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysHighwayRobberyEnemyUnitDamageBranch()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-highway-robbery-enemy-unit-damage.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(6, result.FinalState.CardObjects["P2-BASE-UNIT-HIGHWAY-ROBBERY-001"].Damage);
        Assert.Equal(["P2-BASE-UNIT-HIGHWAY-ROBBERY-001"], result.FinalState.PlayerZones["P2"].Base);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysDeadlyFlourishEnemyUnitDamage()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-deadly-flourish-enemy-unit-damage.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(3, result.FinalState.CardObjects["P2-UNIT-DEADLY-FLOURISH-001"].Damage);
        Assert.Equal(["P1-SPELL-DEADLY-FLOURISH"], result.FinalState.PlayerZones["P1"].Graveyard);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysPainfulPayoffDamageAndGold()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-painful-payoff-damage-create-gold.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(3, result.FinalState.CardObjects["P2-BATTLEFIELD-PAINFUL-PAYOFF-001"].Damage);
        Assert.Equal(["P1-SPELL-PAINFUL-PAYOFF-TOKEN-001"], result.FinalState.PlayerZones["P1"].Base);
        var painfulPayoffGold = result.FinalState.CardObjects["P1-SPELL-PAINFUL-PAYOFF-TOKEN-001"];
        Assert.True(painfulPayoffGold.IsExhausted);
        Assert.Equal([CardObjectTags.EquipmentCard, "反应", "金币"], painfulPayoffGold.Tags);
        Assert.Equal("SFD·T03", painfulPayoffGold.CardNo);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysJungleAmbushCreatesGold()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-jungle-ambush-create-gold.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-SPELL-JUNGLE-AMBUSH-TOKEN-001"], result.FinalState.PlayerZones["P1"].Base);
        var jungleAmbushGold = result.FinalState.CardObjects["P1-SPELL-JUNGLE-AMBUSH-TOKEN-001"];
        Assert.True(jungleAmbushGold.IsExhausted);
        Assert.Equal([CardObjectTags.EquipmentCard, "反应", "金币"], jungleAmbushGold.Tags);
        Assert.Equal("SFD·T03", jungleAmbushGold.CardNo);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsPainfulPayoffAgainstBaseUnit()
    {
        var state = PunishmentState(mana: 3) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-PAINFUL-PAYOFF"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Base = ["P2-BASE-PAINFUL-PAYOFF-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-BASE-PAINFUL-PAYOFF-001"] = new("P2-BASE-PAINFUL-PAYOFF-001", power: 5)
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-painful-payoff-base-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-PAINFUL-PAYOFF",
                "SFD·070/221",
                ["P2-BASE-PAINFUL-PAYOFF-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(3, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-PAINFUL-PAYOFF"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P2-BASE-PAINFUL-PAYOFF-001"], result.State.PlayerZones["P2"].Base);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysBloodMoneyDestroyEnemySmallUnitAndGold()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-blood-money-destroy-enemy-small-unit-create-gold.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.DoesNotContain("P2-BATTLEFIELD-BLOOD-MONEY-ENEMY-001", result.FinalState.CardObjects.Keys);
        Assert.Equal(["P1-SPELL-BLOOD-MONEY-ENEMY-TOKEN-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.True(result.FinalState.CardObjects["P1-SPELL-BLOOD-MONEY-ENEMY-TOKEN-001"].IsExhausted);
        Assert.Equal(["P2"], result.FinalState.DestroyedUnitOwnerIdsThisTurn);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysBloodMoneyDestroyFriendlySmallUnitAndTwoGold()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-blood-money-destroy-friendly-small-unit-create-two-gold.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.DoesNotContain("P1-BATTLEFIELD-BLOOD-MONEY-FRIENDLY-001", result.FinalState.CardObjects.Keys);
        Assert.Equal(
            ["P1-SPELL-BLOOD-MONEY-FRIENDLY-TOKEN-001", "P1-SPELL-BLOOD-MONEY-FRIENDLY-TOKEN-002"],
            result.FinalState.PlayerZones["P1"].Base);
        Assert.True(result.FinalState.CardObjects["P1-SPELL-BLOOD-MONEY-FRIENDLY-TOKEN-001"].IsExhausted);
        Assert.True(result.FinalState.CardObjects["P1-SPELL-BLOOD-MONEY-FRIENDLY-TOKEN-002"].IsExhausted);
        Assert.Equal(["P1"], result.FinalState.DestroyedUnitOwnerIdsThisTurn);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsBloodMoneyAgainstLargeBattlefieldUnit()
    {
        var state = PunishmentState(mana: 2) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-BLOOD-MONEY"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-BATTLEFIELD-BLOOD-MONEY-LARGE-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-BATTLEFIELD-BLOOD-MONEY-LARGE-001"] = new("P2-BATTLEFIELD-BLOOD-MONEY-LARGE-001", power: 3, tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-blood-money-large-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-BLOOD-MONEY",
                "SFD·162/221",
                ["P2-BATTLEFIELD-BLOOD-MONEY-LARGE-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(2, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-BLOOD-MONEY"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P2-BATTLEFIELD-BLOOD-MONEY-LARGE-001"], result.State.PlayerZones["P2"].Battlefields);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4BloodMoneyTargetPowerTooHighRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-blood-money-target-power-too-high-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(2, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-BLOOD-MONEY"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P2-BATTLEFIELD-BLOOD-MONEY-LARGE-001"], result.FinalState.PlayerZones["P2"].Battlefields);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysHighwayRobberyTargetControllerDrawChoice()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-highway-robbery-target-controller-draw-choice.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(
            ["P1-HIGHWAY-ROBBERY-DRAW-001", "P1-HIGHWAY-ROBBERY-DRAW-002"],
            result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(0, result.FinalState.CardObjects["P2-BASE-UNIT-HIGHWAY-ROBBERY-DRAW-CHOICE-001"].Damage);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysLastBreathReadyThenPowerDamage()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-last-breath-ready-damage-enemy-battlefield.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.False(result.FinalState.CardObjects["P1-LAST-BREATH-FRIENDLY-001"].IsExhausted);
        Assert.Equal(4, result.FinalState.CardObjects["P2-LAST-BREATH-ENEMY-001"].Damage);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysConvergentMutationMatchFriendlyPower()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-convergent-mutation-match-friendly-power.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(5, result.FinalState.CardObjects["P1-CONVERGENT-LOW-001"].Power);
        Assert.Equal(3, result.FinalState.CardObjects["P1-CONVERGENT-LOW-001"].UntilEndOfTurnPowerModifier);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysRunePrisonAgainstBaseUnitThroughStack()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-rune-prison-base-unit-stun-stack.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
    }

    [Fact]
    public async Task CoreRuleEngineExpiresRunePrisonStunAtEndTurn()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-rune-prison-stun-expires-end-turn.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
    }

    [Fact]
    public async Task CoreRuleEngineRejectsPunishmentWhenManaIsInsufficient()
    {
        var state = PunishmentState(mana: 1);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-punishment-no-mana", "P1", "PLAY_CARD"),
            new PlayCardCommand("P1-SPELL-PUNISHMENT", "UNL-007/219", ["P2-UNIT-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InsufficientCost, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(1, 0), result.State.RunePools["P1"]);
        Assert.Equal(new[] { "P1-SPELL-PUNISHMENT" }, result.State.PlayerZones["P1"].Hand);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsPunishmentWhenTargetIsInvalid()
    {
        var state = PunishmentState(mana: 2);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-punishment-bad-target", "P1", "PLAY_CARD"),
            new PlayCardCommand("P1-SPELL-PUNISHMENT", "UNL-007/219", ["P2-HAND-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEngineUsesBaseThunderingDropDamageAgainstNonAttackingUnit()
    {
        var engine = new CoreRuleEngine();
        var state = PunishmentState(mana: 3) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-THUNDERING-DROP"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-UNIT-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-UNIT-001"] = new("P2-UNIT-001", power: 5)
            }
        };

        var play = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-thundering-drop-base-play", "P1", "PLAY_CARD"),
            new PlayCardCommand("P1-SPELL-THUNDERING-DROP", "SFD·017/221", ["P2-UNIT-001"]),
            CancellationToken.None);
        var p1Pass = await engine.ResolveAsync(
            play.State,
            new PlayerIntent("intent-thundering-drop-base-p1-pass", "P1", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-thundering-drop-base-p2-pass", "P2", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(play.Accepted);
        Assert.True(p1Pass.Accepted);
        Assert.True(p2Pass.Accepted);
        Assert.Equal(2, p2Pass.State.CardObjects["P2-UNIT-001"].Damage);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsKerplunkWhenTargetIsNotAttacking()
    {
        var state = PunishmentState(mana: 2) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-KERPLUNK"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-UNIT-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-UNIT-001"] = new("P2-UNIT-001", power: 3)
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-kerplunk-not-attacking-target", "P1", "PLAY_CARD"),
            new PlayCardCommand("P1-SPELL-KERPLUNK", "SFD·040/221", ["P2-UNIT-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4KerplunkNonAttackingTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-kerplunk-non-attacking-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(2, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-KERPLUNK"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P2-UNIT-001"], result.FinalState.PlayerZones["P2"].Battlefields);
        Assert.DoesNotContain("STUNNED", result.FinalState.CardObjects["P2-UNIT-001"].UntilEndOfTurnEffects);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsZenithBladeAgainstFriendlyOrBaseUnit()
    {
        var state = PunishmentState(mana: 3) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-ZENITH-BLADE"],
                    Battlefields = ["P1-FRIENDLY-UNIT-001"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Base = ["P2-BASE-UNIT-001"],
                    Battlefields = ["P2-BATTLEFIELD-UNIT-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-FRIENDLY-UNIT-001"] = new("P1-FRIENDLY-UNIT-001"),
                ["P2-BASE-UNIT-001"] = new("P2-BASE-UNIT-001"),
                ["P2-BATTLEFIELD-UNIT-001"] = new("P2-BATTLEFIELD-UNIT-001")
            }
        };

        foreach (var invalidTargetObjectId in new[] { "P1-FRIENDLY-UNIT-001", "P2-BASE-UNIT-001" })
        {
            var result = await new CoreRuleEngine().ResolveAsync(
                state,
                new PlayerIntent($"intent-zenith-blade-invalid-{invalidTargetObjectId}", "P1", "PLAY_CARD"),
                new PlayCardCommand(
                    "P1-SPELL-ZENITH-BLADE",
                    "OGN·262/298",
                    [invalidTargetObjectId]),
                CancellationToken.None);

            Assert.False(result.Accepted);
            Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
            Assert.Empty(result.Events);
            Assert.Equal(0, result.State.Tick);
            Assert.Empty(result.State.StackItems);
        }
    }

    [Fact]
    public async Task P4ZenithBladeBaseUnitTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-zenith-blade-base-unit-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(3, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-ZENITH-BLADE"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P2-BASE-UNIT-001"], result.FinalState.PlayerZones["P2"].Base);
        Assert.DoesNotContain("STUNNED", result.FinalState.CardObjects["P2-BASE-UNIT-001"].UntilEndOfTurnEffects);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4ZenithBladeFriendlyTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-zenith-blade-friendly-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(3, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-ZENITH-BLADE"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-FRIENDLY-UNIT-001"], result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.DoesNotContain("STUNNED", result.FinalState.CardObjects["P1-FRIENDLY-UNIT-001"].UntilEndOfTurnEffects);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsHighwayRobberyAgainstFriendlyOrOffFieldUnit()
    {
        var state = PunishmentState(mana: 2) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-HIGHWAY-ROBBERY"],
                    Base = ["P1-FRIENDLY-BASE-UNIT-001"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Base = ["P2-BASE-UNIT-001"],
                    Graveyard = ["P2-GRAVEYARD-UNIT-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-FRIENDLY-BASE-UNIT-001"] = new("P1-FRIENDLY-BASE-UNIT-001"),
                ["P2-BASE-UNIT-001"] = new("P2-BASE-UNIT-001"),
                ["P2-GRAVEYARD-UNIT-001"] = new("P2-GRAVEYARD-UNIT-001")
            }
        };

        foreach (var invalidTargetObjectId in new[] { "P1-FRIENDLY-BASE-UNIT-001", "P2-GRAVEYARD-UNIT-001" })
        {
            var result = await new CoreRuleEngine().ResolveAsync(
                state,
                new PlayerIntent($"intent-highway-robbery-invalid-{invalidTargetObjectId}", "P1", "PLAY_CARD"),
                new PlayCardCommand(
                    "P1-SPELL-HIGHWAY-ROBBERY",
                    "OGN·033/298",
                    [invalidTargetObjectId]),
                CancellationToken.None);

            Assert.False(result.Accepted);
            Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
            Assert.Empty(result.Events);
            Assert.Equal(0, result.State.Tick);
            Assert.Empty(result.State.StackItems);
        }
    }

    [Fact]
    public async Task CoreRuleEngineRejectsHighwayRobberyWhenEnemyTargetIsNotControlledByOpponentZonePlayer()
    {
        var state = PunishmentState(mana: 2) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-HIGHWAY-ROBBERY"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Base = ["P2-DIRTY-P1-OWNED-UNIT"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-SPELL-HIGHWAY-ROBBERY"] = new(
                    "P1-SPELL-HIGHWAY-ROBBERY",
                    cardNo: "OGN·033/298",
                    tags: [CardObjectTags.SpellCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-DIRTY-P1-OWNED-UNIT"] = new(
                    "P2-DIRTY-P1-OWNED-UNIT",
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1")
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-highway-robbery-dirty-opponent-zone-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-HIGHWAY-ROBBERY",
                "OGN·033/298",
                ["P2-DIRTY-P1-OWNED-UNIT"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(2, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-HIGHWAY-ROBBERY"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P2-DIRTY-P1-OWNED-UNIT"], result.State.PlayerZones["P2"].Base);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4HighwayRobberyOffFieldTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-highway-robbery-off-field-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(2, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-HIGHWAY-ROBBERY"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P2-GRAVEYARD-UNIT-001"], result.FinalState.PlayerZones["P2"].Graveyard);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsDeadlyFlourishAgainstFriendlyUnit()
    {
        var state = PunishmentState(mana: 4) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-DEADLY-FLOURISH"],
                    Base = ["P1-FRIENDLY-DEADLY-FLOURISH-001"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-FRIENDLY-DEADLY-FLOURISH-001"] = new("P1-FRIENDLY-DEADLY-FLOURISH-001", power: 5)
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-deadly-flourish-friendly-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-DEADLY-FLOURISH",
                "UNL-073/219",
                ["P1-FRIENDLY-DEADLY-FLOURISH-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(4, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-DEADLY-FLOURISH"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-FRIENDLY-DEADLY-FLOURISH-001"], result.State.PlayerZones["P1"].Base);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4DeadlyFlourishFriendlyTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-deadly-flourish-friendly-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(4, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-DEADLY-FLOURISH"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-FRIENDLY-DEADLY-FLOURISH-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsLastBreathAgainstWrongTargetOrderOrEnemyBaseUnit()
    {
        var state = PunishmentState(mana: 3) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-LAST-BREATH"],
                    Base = ["P1-FRIENDLY-LAST-BREATH-001"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Base = ["P2-BASE-LAST-BREATH-001"],
                    Battlefields = ["P2-BATTLEFIELD-LAST-BREATH-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-FRIENDLY-LAST-BREATH-001"] = new("P1-FRIENDLY-LAST-BREATH-001", power: 4, isExhausted: true),
                ["P2-BASE-LAST-BREATH-001"] = new("P2-BASE-LAST-BREATH-001", power: 5),
                ["P2-BATTLEFIELD-LAST-BREATH-001"] = new("P2-BATTLEFIELD-LAST-BREATH-001", power: 5)
            }
        };

        foreach (var invalidTargetObjectIds in new[]
        {
            new[] { "P2-BATTLEFIELD-LAST-BREATH-001", "P1-FRIENDLY-LAST-BREATH-001" },
            new[] { "P1-FRIENDLY-LAST-BREATH-001", "P2-BASE-LAST-BREATH-001" }
        })
        {
            var result = await new CoreRuleEngine().ResolveAsync(
                state,
                new PlayerIntent($"intent-last-breath-invalid-{string.Join("-", invalidTargetObjectIds)}", "P1", "PLAY_CARD"),
                new PlayCardCommand(
                    "P1-SPELL-LAST-BREATH",
                    "OGN·260/298",
                    invalidTargetObjectIds),
                CancellationToken.None);

            Assert.False(result.Accepted);
            Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
            Assert.Empty(result.Events);
            Assert.Equal(0, result.State.Tick);
            Assert.Empty(result.State.StackItems);
        }
    }

    [Fact]
    public async Task P4LastBreathEnemyBaseTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-last-breath-enemy-base-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(3, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-LAST-BREATH"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-FRIENDLY-LAST-BREATH-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.True(result.FinalState.CardObjects["P1-FRIENDLY-LAST-BREATH-001"].IsExhausted);
        Assert.Equal(0, result.FinalState.CardObjects["P2-BASE-LAST-BREATH-001"].Damage);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4LastBreathTargetOrderRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-last-breath-target-order-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(3, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-LAST-BREATH"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-FRIENDLY-LAST-BREATH-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.True(result.FinalState.CardObjects["P1-FRIENDLY-LAST-BREATH-001"].IsExhausted);
        Assert.Equal(0, result.FinalState.CardObjects["P2-BATTLEFIELD-LAST-BREATH-001"].Damage);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsConvergentMutationAgainstEnemyOrDuplicateTarget()
    {
        var state = PunishmentState(mana: 2) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-CONVERGENT-MUTATION"],
                    Base = ["P1-CONVERGENT-FRIENDLY-001"],
                    Battlefields = ["P1-CONVERGENT-HIGH-001"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-CONVERGENT-ENEMY-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-CONVERGENT-FRIENDLY-001"] = new("P1-CONVERGENT-FRIENDLY-001", power: 2),
                ["P1-CONVERGENT-HIGH-001"] = new("P1-CONVERGENT-HIGH-001", power: 5),
                ["P2-CONVERGENT-ENEMY-001"] = new("P2-CONVERGENT-ENEMY-001", power: 5)
            }
        };

        foreach (var invalidTargetObjectIds in new[]
        {
            new[] { "P1-CONVERGENT-FRIENDLY-001", "P2-CONVERGENT-ENEMY-001" },
            new[] { "P1-CONVERGENT-FRIENDLY-001", "P1-CONVERGENT-FRIENDLY-001" }
        })
        {
            var result = await new CoreRuleEngine().ResolveAsync(
                state,
                new PlayerIntent($"intent-convergent-mutation-invalid-{string.Join("-", invalidTargetObjectIds)}", "P1", "PLAY_CARD"),
                new PlayCardCommand(
                    "P1-SPELL-CONVERGENT-MUTATION",
                    "OGN·108/298",
                    invalidTargetObjectIds),
                CancellationToken.None);

            Assert.False(result.Accepted);
            Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
            Assert.Empty(result.Events);
            Assert.Equal(0, result.State.Tick);
            Assert.Empty(result.State.StackItems);
        }
    }

    [Fact]
    public async Task P4ConvergentMutationEnemyTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-convergent-mutation-enemy-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(2, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-CONVERGENT-MUTATION"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-CONVERGENT-FRIENDLY-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(["P1-CONVERGENT-HIGH-001"], result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(2, result.FinalState.CardObjects["P1-CONVERGENT-FRIENDLY-001"].Power);
        Assert.Equal(0, result.FinalState.CardObjects["P1-CONVERGENT-FRIENDLY-001"].UntilEndOfTurnPowerModifier);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4ConvergentMutationDuplicateTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-convergent-mutation-duplicate-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(2, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-CONVERGENT-MUTATION"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-CONVERGENT-FRIENDLY-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(["P1-CONVERGENT-HIGH-001"], result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(2, result.FinalState.CardObjects["P1-CONVERGENT-FRIENDLY-001"].Power);
        Assert.Equal(0, result.FinalState.CardObjects["P1-CONVERGENT-FRIENDLY-001"].UntilEndOfTurnPowerModifier);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsExistentialDreadWhenTargetIsFriendlyAttackingUnit()
    {
        var state = PunishmentState(mana: 1) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-EXISTENTIAL-DREAD"],
                    Battlefields = ["P1-BATTLEFIELD-UNIT-001"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-UNIT-001"] = new("P1-BATTLEFIELD-UNIT-001", isAttacking: true)
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-existential-dread-friendly-attacking-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-EXISTENTIAL-DREAD",
                "UNL-134/219",
                ["P1-BATTLEFIELD-UNIT-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4ExistentialDreadFriendlyAttackingTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-existential-dread-friendly-attacking-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(1, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-EXISTENTIAL-DREAD"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-BATTLEFIELD-UNIT-001"], result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.True(result.FinalState.CardObjects["P1-BATTLEFIELD-UNIT-001"].IsAttacking);
        Assert.Empty(result.FinalState.CardObjects["P1-BATTLEFIELD-UNIT-001"].UntilEndOfTurnEffects);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsThunderingSkyWithoutEnoughReducedCost()
    {
        var state = PunishmentState(mana: 5) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-THUNDERING-SKY"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-UNIT-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-UNIT-001"] = new("P2-UNIT-001", power: 6)
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-thundering-sky-not-enough-reduced-cost", "P1", "PLAY_CARD"),
            new PlayCardCommand("P1-SPELL-THUNDERING-SKY", "OGN·014/298", ["P2-UNIT-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InsufficientCost, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4ThunderingSkyInsufficientReducedCostRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-thundering-sky-insufficient-reduced-cost-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(5, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-THUNDERING-SKY"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(["P2-UNIT-001"], result.FinalState.PlayerZones["P2"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P2-UNIT-001"].Damage);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsMindAndBalanceWithoutOpponentScoreReduction()
    {
        var state = PunishmentState(mana: 1) with
        {
            PlayerScores = new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 4
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    MainDeck = ["P1-DRAW-001"],
                    RuneDeck = ["P1-RUNE-001"],
                    Hand = ["P1-SPELL-MIND-AND-BALANCE"]
                },
                ["P2"] = PlayerZones.Empty
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-mind-and-balance-not-enough-reduced-cost", "P1", "PLAY_CARD"),
            new PlayCardCommand("P1-SPELL-MIND-AND-BALANCE", "OGN·047/298", []),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InsufficientCost, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(1, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-MIND-AND-BALANCE"], result.State.PlayerZones["P1"].Hand);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4MindAndBalanceInsufficientUnreducedCostRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-mind-and-balance-insufficient-unreduced-cost-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(1, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-DRAW-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-RUNE-001"], result.FinalState.PlayerZones["P1"].RuneDeck);
        Assert.Equal(["P1-SPELL-MIND-AND-BALANCE"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Graveyard);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsHuntTheWeakWhenTargetPowerIsTooHigh()
    {
        var state = PunishmentState(mana: 2) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-HUNT-THE-WEAK"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-UNIT-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-UNIT-001"] = new("P2-UNIT-001", power: 4)
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-hunt-the-weak-large-target", "P1", "PLAY_CARD"),
            new PlayCardCommand("P1-SPELL-HUNT-THE-WEAK", "UNL-159/219", ["P2-UNIT-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4HuntTheWeakTargetPowerTooHighFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-hunt-the-weak-target-power-too-high-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(2, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-HUNT-THE-WEAK"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P2-UNIT-001"], result.FinalState.PlayerZones["P2"].Battlefields);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsSpiritFireWhenTotalTargetPowerIsTooHigh()
    {
        var state = PunishmentState(mana: 3) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-SPIRIT-FIRE"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-SPIRIT-FIRE-UNIT-001", "P2-SPIRIT-FIRE-UNIT-002"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-SPIRIT-FIRE-UNIT-001"] = new("P2-SPIRIT-FIRE-UNIT-001", power: 2),
                ["P2-SPIRIT-FIRE-UNIT-002"] = new("P2-SPIRIT-FIRE-UNIT-002", power: 3)
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-spirit-fire-total-power-too-high", "P1", "PLAY_CARD"),
            new PlayCardCommand("P1-SPELL-SPIRIT-FIRE", "OGN·256/298", ["P2-SPIRIT-FIRE-UNIT-001", "P2-SPIRIT-FIRE-UNIT-002"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(["P1-SPELL-SPIRIT-FIRE"], result.State.PlayerZones["P1"].Hand);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4SpiritFireTotalPowerTooHighFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-spirit-fire-total-power-too-high-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(3, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-SPIRIT-FIRE"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(
            ["P2-SPIRIT-FIRE-UNIT-001", "P2-SPIRIT-FIRE-UNIT-002"],
            result.FinalState.PlayerZones["P2"].Battlefields);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsPlayfulTentaclesWhenTotalTargetPowerIsTooHigh()
    {
        var state = PunishmentState(mana: 4) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-PLAYFUL-TENTACLES"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-PLAYFUL-TENTACLES-UNIT-001", "P2-PLAYFUL-TENTACLES-UNIT-002"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-PLAYFUL-TENTACLES-UNIT-001"] = new("P2-PLAYFUL-TENTACLES-UNIT-001", power: 4, tags: [CardObjectTags.UnitCard]),
                ["P2-PLAYFUL-TENTACLES-UNIT-002"] = new("P2-PLAYFUL-TENTACLES-UNIT-002", power: 5, tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-playful-tentacles-total-power-too-high", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-PLAYFUL-TENTACLES",
                "UNL-054/219",
                ["P2-PLAYFUL-TENTACLES-UNIT-001", "P2-PLAYFUL-TENTACLES-UNIT-002"], Destination: "BASE:P2"),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(["P1-SPELL-PLAYFUL-TENTACLES"], result.State.PlayerZones["P1"].Hand);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4PlayfulTentaclesTotalPowerTooHighFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-playful-tentacles-total-power-too-high-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(4, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-PLAYFUL-TENTACLES"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(
            ["BF", "P2-PLAYFUL-TENTACLES-UNIT-001", "P2-PLAYFUL-TENTACLES-UNIT-002"],
            result.FinalState.PlayerZones["P2"].Battlefields);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsBaitAgainstFriendlyOrRepeatedTarget()
    {
        var state = PunishmentState(mana: 2) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-BAIT"],
                    Base = ["P1-BAIT-FRIENDLY-001"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Base = ["P2-BAIT-DESTINATION-001"],
                    Battlefields = ["P2-BAIT-MOVED-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BAIT-FRIENDLY-001"] = new("P1-BAIT-FRIENDLY-001", power: 2),
                ["P2-BAIT-DESTINATION-001"] = new("P2-BAIT-DESTINATION-001", power: 3),
                ["P2-BAIT-MOVED-001"] = new("P2-BAIT-MOVED-001", power: 4)
            }
        };

        IReadOnlyList<string>[] invalidTargets =
        [
            ["P2-BAIT-MOVED-001", "P1-BAIT-FRIENDLY-001"],
            ["P1-BAIT-FRIENDLY-001", "P2-BAIT-DESTINATION-001"],
            ["P2-BAIT-MOVED-001", "P2-BAIT-MOVED-001"]
        ];

        foreach (var targetObjectIds in invalidTargets)
        {
            var result = await new CoreRuleEngine().ResolveAsync(
                state,
                new PlayerIntent($"intent-bait-invalid-{string.Join("-", targetObjectIds)}", "P1", "PLAY_CARD"),
                new PlayCardCommand(
                    "P1-SPELL-BAIT",
                    "SFD·129/221",
                    targetObjectIds),
                CancellationToken.None);

            Assert.False(result.Accepted);
            Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
            Assert.Empty(result.Events);
            Assert.Equal(0, result.State.Tick);
            Assert.Equal(["P1-SPELL-BAIT"], result.State.PlayerZones["P1"].Hand);
            Assert.Empty(result.State.StackItems);
        }
    }

    [Fact]
    public async Task CoreRuleEngineRejectsDragonsRageAgainstFriendlyOrRepeatedTarget()
    {
        var state=ReflexiveMovementDuelTests.Position();
        IReadOnlyList<string>[] invalidTargets=[["A"],["D","A"],["A","E"],["D","D"]];
        foreach(var ids in invalidTargets)
        {
            var command=ReflexiveMovementDuelTests.Command("BATTLEFIELD:BF2") with {TargetObjectIds=ids};
            var result=await new CoreRuleEngine().ResolveAsync(state,new("invalid-rage","P1",CommandTypes.PlayCard),command,default);
            Assert.False(result.Accepted);Assert.Equal(ErrorCodes.InvalidTarget,result.ErrorCode);Assert.Empty(result.Events);
            Assert.Equal(state.Tick,result.State.Tick);Assert.Equal(state.PlayerZones["P1"].Hand,result.State.PlayerZones["P1"].Hand);
            Assert.Empty(result.State.StackItems);Assert.Equal(MatchStateHasher.Hash(state),MatchStateHasher.Hash(result.State));
        }
    }

    [Fact]
    public async Task CoreRuleEngineRejectsHousecleaningAgainstWrongTargetOrder()
    {
        var state = PunishmentState(mana: 2) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-HOUSECLEANING"],
                    Base = ["P1-HOUSECLEANING-FRIENDLY-001"],
                    Battlefields = ["P1-HOUSECLEANING-FRIENDLY-002"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-HOUSECLEANING-ENEMY-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-HOUSECLEANING-FRIENDLY-001"] = new("P1-HOUSECLEANING-FRIENDLY-001", power: 2),
                ["P1-HOUSECLEANING-FRIENDLY-002"] = new("P1-HOUSECLEANING-FRIENDLY-002", power: 3),
                ["P2-HOUSECLEANING-ENEMY-001"] = new("P2-HOUSECLEANING-ENEMY-001", power: 4)
            }
        };

        foreach (var invalidTargetObjectIds in new[]
        {
            new[] { "P2-HOUSECLEANING-ENEMY-001", "P1-HOUSECLEANING-FRIENDLY-001" },
            new[] { "P1-HOUSECLEANING-FRIENDLY-001", "P1-HOUSECLEANING-FRIENDLY-002" }
        })
        {
            var result = await new CoreRuleEngine().ResolveAsync(
                state,
                new PlayerIntent($"intent-housecleaning-invalid-{string.Join("-", invalidTargetObjectIds)}", "P1", "PLAY_CARD"),
                new PlayCardCommand(
                    "P1-SPELL-HOUSECLEANING",
                    "OGN·209/298",
                    invalidTargetObjectIds),
                CancellationToken.None);

            Assert.False(result.Accepted);
            Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
            Assert.Empty(result.Events);
            Assert.Equal(0, result.State.Tick);
            Assert.Empty(result.State.StackItems);
        }
    }

    [Fact]
    public async Task CoreRuleEngineRejectsKingsEdictAgainstFriendlyUnit()
    {
        var state = PunishmentState(mana: 6) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-KINGS-EDICT"],
                    Base = ["P1-FRIENDLY-UNIT-001"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-ENEMY-UNIT-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-FRIENDLY-UNIT-001"] = new("P1-FRIENDLY-UNIT-001"),
                ["P2-ENEMY-UNIT-001"] = new("P2-ENEMY-UNIT-001")
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-kings-edict-friendly-target", "P1", "PLAY_CARD"),
            new PlayCardCommand("P1-SPELL-KINGS-EDICT", "OGN·237/298", ["P1-FRIENDLY-UNIT-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsHurricaneSweepDuplicateChosenUnit()
    {
        var state = PunishmentState(mana: 4) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-HURRICANE-SWEEP"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-HURRICANE-ENEMY-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-HURRICANE-ENEMY-001"] = new("P2-HURRICANE-ENEMY-001", power: 4)
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-hurricane-sweep-duplicate-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-HURRICANE-SWEEP",
                "OGN·187/298",
                ["P2-HURRICANE-ENEMY-001", "P2-HURRICANE-ENEMY-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsCustodianJudgmentWithoutOwnerDeckChoiceMode()
    {
        var state = PunishmentState(mana: 2) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-CUSTODIAN-JUDGMENT"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-CUSTODIAN-TARGET-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-CUSTODIAN-TARGET-001"] = new("P2-CUSTODIAN-TARGET-001", power: 3)
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-custodian-judgment-missing-mode", "P1", "PLAY_CARD"),
            new PlayCardCommand("P1-SPELL-CUSTODIAN-JUDGMENT", "UNL-204/219", ["P2-CUSTODIAN-TARGET-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.UnsupportedCardBehavior, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsCustodianJudgmentAgainstFriendlyOrBaseUnit()
    {
        var state = PunishmentState(mana: 2) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-CUSTODIAN-JUDGMENT"],
                    Battlefields = ["P1-CUSTODIAN-FRIENDLY-001"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Base = ["P2-CUSTODIAN-BASE-001"],
                    Battlefields = ["P2-CUSTODIAN-TARGET-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-CUSTODIAN-FRIENDLY-001"] = new("P1-CUSTODIAN-FRIENDLY-001"),
                ["P2-CUSTODIAN-BASE-001"] = new("P2-CUSTODIAN-BASE-001"),
                ["P2-CUSTODIAN-TARGET-001"] = new("P2-CUSTODIAN-TARGET-001")
            }
        };

        foreach (var invalidTargetObjectId in new[] { "P1-CUSTODIAN-FRIENDLY-001", "P2-CUSTODIAN-BASE-001" })
        {
            var result = await new CoreRuleEngine().ResolveAsync(
                state,
                new PlayerIntent($"intent-custodian-judgment-invalid-{invalidTargetObjectId}", "P1", "PLAY_CARD"),
                new PlayCardCommand(
                    "P1-SPELL-CUSTODIAN-JUDGMENT",
                    "UNL-204/219",
                    [invalidTargetObjectId],
                    "OWNER_MAIN_DECK_TOP"),
                CancellationToken.None);

            Assert.False(result.Accepted);
            Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
            Assert.Empty(result.Events);
            Assert.Equal(0, result.State.Tick);
            Assert.Empty(result.State.StackItems);
        }
    }

    [Fact]
    public async Task CoreRuleEngineRejectsGustWhenTargetPowerIsTooHigh()
    {
        var state = PunishmentState(mana: 1) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-GUST"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-UNIT-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-UNIT-001"] = new("P2-UNIT-001", power: 4)
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-gust-large-target", "P1", "PLAY_CARD"),
            new PlayCardCommand("P1-SPELL-GUST", "OGN·169/298", ["P2-UNIT-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4GustTargetPowerTooHighFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-gust-target-power-too-high-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(1, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-GUST"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P2-UNIT-001"], result.FinalState.PlayerZones["P2"].Battlefields);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsBattlefieldOnlySpellWhenTargetIsBaseUnit()
    {
        var state = PunishmentState(mana: 1) with
        {
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(1, 0),
                ["P2"] = RunePool.Empty
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-HEXTECH-RAY"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Base = ["P2-BASE-UNIT-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-BASE-UNIT-001"] = new("P2-BASE-UNIT-001")
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-hextech-ray-base-target", "P1", "PLAY_CARD"),
            new PlayCardCommand("P1-SPELL-HEXTECH-RAY", "OGN·009/298", ["P2-BASE-UNIT-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsFriendlyOnlySpellWhenTargetIsEnemyUnit()
    {
        var state = PunishmentState(mana: 3) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-BACK-TO-BACK"],
                    Base = ["P1-BASE-UNIT-001"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-BATTLEFIELD-UNIT-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BASE-UNIT-001"] = new("P1-BASE-UNIT-001"),
                ["P2-BATTLEFIELD-UNIT-001"] = new("P2-BATTLEFIELD-UNIT-001")
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-back-to-back-enemy-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-BACK-TO-BACK",
                "OGN·206/298",
                ["P1-BASE-UNIT-001", "P2-BATTLEFIELD-UNIT-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsRuthlessPursuitAgainstEnemyOrNonUnit()
    {
        var state = PunishmentState(mana: 2) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-RUTHLESS-PURSUIT"],
                    Base = ["P1-RUTHLESS-PURSUIT-EQUIPMENT-001"],
                    Battlefields = ["P1-RUTHLESS-PURSUIT-UNIT-001"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-RUTHLESS-PURSUIT-ENEMY-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-RUTHLESS-PURSUIT-UNIT-001"] = new(
                    "P1-RUTHLESS-PURSUIT-UNIT-001",
                    tags: [CardObjectTags.UnitCard]),
                ["P1-RUTHLESS-PURSUIT-EQUIPMENT-001"] = new(
                    "P1-RUTHLESS-PURSUIT-EQUIPMENT-001",
                    tags: [CardObjectTags.EquipmentCard]),
                ["P2-RUTHLESS-PURSUIT-ENEMY-001"] = new(
                    "P2-RUTHLESS-PURSUIT-ENEMY-001",
                    tags: [CardObjectTags.UnitCard])
            }
        };

        IReadOnlyList<string>[] invalidTargets =
        [
            ["P2-RUTHLESS-PURSUIT-ENEMY-001"],
            ["P1-RUTHLESS-PURSUIT-EQUIPMENT-001"]
        ];

        foreach (var targetObjectIds in invalidTargets)
        {
            var result = await new CoreRuleEngine().ResolveAsync(
                state,
                new PlayerIntent($"intent-ruthless-pursuit-invalid-{string.Join("-", targetObjectIds)}", "P1", "PLAY_CARD"),
                new PlayCardCommand(
                    "P1-SPELL-RUTHLESS-PURSUIT",
                    "SFD·184/221",
                    targetObjectIds),
                CancellationToken.None);

            Assert.False(result.Accepted);
            Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
            Assert.Empty(result.Events);
            Assert.Equal(0, result.State.Tick);
            Assert.Equal(new RunePool(2, 0), result.State.RunePools["P1"]);
            Assert.Equal(["P1-SPELL-RUTHLESS-PURSUIT"], result.State.PlayerZones["P1"].Hand);
            Assert.Empty(result.State.StackItems);
        }
    }

    [Fact]
    public async Task CoreRuleEngineRejectsFlashWhenTargetIsEnemyUnit()
    {
        var state = PunishmentState(mana: 2) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-FLASH"],
                    Battlefields = ["P1-BATTLEFIELD-UNIT-001"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-BATTLEFIELD-UNIT-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-UNIT-001"] = new("P1-BATTLEFIELD-UNIT-001"),
                ["P2-BATTLEFIELD-UNIT-001"] = new("P2-BATTLEFIELD-UNIT-001")
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-flash-enemy-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-FLASH",
                "OGS·011/024",
                ["P2-BATTLEFIELD-UNIT-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsHappenstanceWhenTargetsAreReversed()
    {
        var state = PunishmentState(mana: 3) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-HAPPENSTANCE"],
                    Base = ["P1-BASE-UNIT-001"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-BATTLEFIELD-UNIT-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BASE-UNIT-001"] = new("P1-BASE-UNIT-001"),
                ["P2-BATTLEFIELD-UNIT-001"] = new("P2-BATTLEFIELD-UNIT-001")
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-happenstance-reversed-targets", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-HAPPENSTANCE",
                "UNL-128/219",
                ["P2-BATTLEFIELD-UNIT-001", "P1-BASE-UNIT-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsReconsiderWhenTargetIsEnemyUnit()
    {
        var state = PunishmentState(mana: 1) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-RECONSIDER"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-BATTLEFIELD-UNIT-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-BATTLEFIELD-UNIT-001"] = new("P2-BATTLEFIELD-UNIT-001")
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-reconsider-enemy-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-RECONSIDER",
                "OGN·104/298",
                ["P2-BATTLEFIELD-UNIT-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsFlashWhenTargetIsFriendlyBaseUnit()
    {
        var state = PunishmentState(mana: 2) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-FLASH"],
                    Base = ["P1-BASE-UNIT-001"],
                    Battlefields = ["P1-BATTLEFIELD-UNIT-001"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BASE-UNIT-001"] = new("P1-BASE-UNIT-001"),
                ["P1-BATTLEFIELD-UNIT-001"] = new("P1-BATTLEFIELD-UNIT-001")
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-flash-base-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-FLASH",
                "OGS·011/024",
                ["P1-BASE-UNIT-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsShieldWallAgainstEnemyBaseOrRepeatedTarget()
    {
        var state = PunishmentState(mana: 2) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-SHIELD-WALL"],
                    Base = ["P1-BASE-UNIT-001"],
                    Battlefields = ["P1-BATTLEFIELD-UNIT-001"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-BATTLEFIELD-UNIT-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BASE-UNIT-001"] = new("P1-BASE-UNIT-001"),
                ["P1-BATTLEFIELD-UNIT-001"] = new("P1-BATTLEFIELD-UNIT-001"),
                ["P2-BATTLEFIELD-UNIT-001"] = new("P2-BATTLEFIELD-UNIT-001")
            }
        };

        IReadOnlyList<string>[] invalidTargets =
        [
            ["P2-BATTLEFIELD-UNIT-001"],
            ["P1-BASE-UNIT-001"],
            ["P1-BATTLEFIELD-UNIT-001", "P1-BATTLEFIELD-UNIT-001"]
        ];

        foreach (var targetObjectIds in invalidTargets)
        {
            var result = await new CoreRuleEngine().ResolveAsync(
                state,
                new PlayerIntent("intent-shield-wall-invalid-target", "P1", "PLAY_CARD"),
                new PlayCardCommand(
                    "P1-SPELL-SHIELD-WALL",
                    "SFD·043/221",
                    targetObjectIds),
                CancellationToken.None);

            Assert.False(result.Accepted);
            Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
            Assert.Empty(result.Events);
            Assert.Equal(0, result.State.Tick);
            Assert.Empty(result.State.StackItems);
        }
    }

    [Fact]
    public async Task CoreRuleEngineRejectsSkullcrackAgainstWrongOrderOrBaseUnits()
    {
        var state = PunishmentState(mana: 2) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-SKULLCRACK"],
                    Base = ["P1-BASE-UNIT-001"],
                    Battlefields = ["P1-BATTLEFIELD-UNIT-001"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Base = ["P2-BASE-UNIT-001"],
                    Battlefields = ["P2-BATTLEFIELD-UNIT-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BASE-UNIT-001"] = new("P1-BASE-UNIT-001"),
                ["P1-BATTLEFIELD-UNIT-001"] = new("P1-BATTLEFIELD-UNIT-001"),
                ["P2-BASE-UNIT-001"] = new("P2-BASE-UNIT-001"),
                ["P2-BATTLEFIELD-UNIT-001"] = new("P2-BATTLEFIELD-UNIT-001")
            }
        };

        IReadOnlyList<string>[] invalidTargets =
        [
            ["P2-BATTLEFIELD-UNIT-001", "P1-BATTLEFIELD-UNIT-001"],
            ["P1-BASE-UNIT-001", "P2-BATTLEFIELD-UNIT-001"],
            ["P1-BATTLEFIELD-UNIT-001", "P2-BASE-UNIT-001"],
            ["P1-BATTLEFIELD-UNIT-001", "P1-BATTLEFIELD-UNIT-001"]
        ];

        foreach (var targetObjectIds in invalidTargets)
        {
            var result = await new CoreRuleEngine().ResolveAsync(
                state,
                new PlayerIntent("intent-skullcrack-invalid-target", "P1", "PLAY_CARD"),
                new PlayCardCommand(
                    "P1-SPELL-SKULLCRACK",
                    "OGN·220/298",
                    targetObjectIds),
                CancellationToken.None);

            Assert.False(result.Accepted);
            Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
            Assert.Empty(result.Events);
            Assert.Equal(0, result.State.Tick);
            Assert.Empty(result.State.StackItems);
        }
    }

    [Fact]
    public async Task CoreRuleEngineRejectsHeroicChargeAgainstWrongOrderOrBaseUnits()
    {
        var state = PunishmentState(mana: 3) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-HEROIC-CHARGE"],
                    Base = ["P1-BASE-UNIT-001"],
                    Battlefields = ["P1-BATTLEFIELD-UNIT-001"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Base = ["P2-BASE-UNIT-001"],
                    Battlefields = ["P2-BATTLEFIELD-UNIT-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BASE-UNIT-001"] = new("P1-BASE-UNIT-001"),
                ["P1-BATTLEFIELD-UNIT-001"] = new("P1-BATTLEFIELD-UNIT-001"),
                ["P2-BASE-UNIT-001"] = new("P2-BASE-UNIT-001"),
                ["P2-BATTLEFIELD-UNIT-001"] = new("P2-BATTLEFIELD-UNIT-001")
            }
        };

        IReadOnlyList<string>[] invalidTargets =
        [
            ["P2-BATTLEFIELD-UNIT-001", "P1-BATTLEFIELD-UNIT-001"],
            ["P1-BASE-UNIT-001", "P2-BATTLEFIELD-UNIT-001"],
            ["P1-BATTLEFIELD-UNIT-001", "P2-BASE-UNIT-001"],
            ["P1-BATTLEFIELD-UNIT-001", "P1-BATTLEFIELD-UNIT-001"]
        ];

        foreach (var targetObjectIds in invalidTargets)
        {
            var result = await new CoreRuleEngine().ResolveAsync(
                state,
                new PlayerIntent("intent-heroic-charge-invalid-target", "P1", "PLAY_CARD"),
                new PlayCardCommand(
                    "P1-SPELL-HEROIC-CHARGE",
                    "UNL-155/219",
                    targetObjectIds),
                CancellationToken.None);

            Assert.False(result.Accepted);
            Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
            Assert.Empty(result.Events);
            Assert.Equal(0, result.State.Tick);
            Assert.Empty(result.State.StackItems);
        }
    }

    [Fact]
    public async Task CoreRuleEngineRejectsStandDefiantAgainstNonFriendlyBattlefieldTarget()
    {
        var state = PunishmentState(mana: 2) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-STAND-DEFIANT"],
                    Base = ["P1-BASE-STAND-001"],
                    Battlefields = ["P1-BATTLEFIELD-STAND-001"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Base = ["P2-BASE-STAND-001"],
                    Battlefields = ["P2-BATTLEFIELD-STAND-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BASE-STAND-001"] = new("P1-BASE-STAND-001"),
                ["P1-BATTLEFIELD-STAND-001"] = new("P1-BATTLEFIELD-STAND-001"),
                ["P2-BASE-STAND-001"] = new("P2-BASE-STAND-001"),
                ["P2-BATTLEFIELD-STAND-001"] = new("P2-BATTLEFIELD-STAND-001")
            }
        };

        IReadOnlyList<string>[] invalidTargets =
        [
            ["P1-BASE-STAND-001"],
            ["P2-BATTLEFIELD-STAND-001"],
            ["P2-BASE-STAND-001"]
        ];

        foreach (var targetObjectIds in invalidTargets)
        {
            var result = await new CoreRuleEngine().ResolveAsync(
                state,
                new PlayerIntent("intent-stand-defiant-invalid-target", "P1", "PLAY_CARD"),
                new PlayCardCommand(
                    "P1-SPELL-STAND-DEFIANT",
                    "SFD·001/221",
                    targetObjectIds),
                CancellationToken.None);

            Assert.False(result.Accepted);
            Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
            Assert.Empty(result.Events);
            Assert.Equal(0, result.State.Tick);
            Assert.Empty(result.State.StackItems);
        }
    }

    [Fact]
    public async Task CoreRuleEngineRejectsIsolateAgainstNonEnemyBattlefieldTarget()
    {
        var state = PunishmentState(mana: 2) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-ISOLATE"],
                    Base = ["P1-BASE-UNIT-001"],
                    Battlefields = ["P1-BATTLEFIELD-UNIT-001"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Base = ["P2-BASE-UNIT-001"],
                    Battlefields = ["P2-BATTLEFIELD-UNIT-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BASE-UNIT-001"] = new("P1-BASE-UNIT-001"),
                ["P1-BATTLEFIELD-UNIT-001"] = new("P1-BATTLEFIELD-UNIT-001"),
                ["P2-BASE-UNIT-001"] = new("P2-BASE-UNIT-001"),
                ["P2-BATTLEFIELD-UNIT-001"] = new("P2-BATTLEFIELD-UNIT-001")
            }
        };

        IReadOnlyList<string>[] invalidTargets =
        [
            ["P1-BATTLEFIELD-UNIT-001"],
            ["P1-BASE-UNIT-001"],
            ["P2-BASE-UNIT-001"]
        ];

        foreach (var targetObjectIds in invalidTargets)
        {
            var result = await new CoreRuleEngine().ResolveAsync(
                state,
                new PlayerIntent("intent-isolate-invalid-target", "P1", "PLAY_CARD"),
                new PlayCardCommand(
                    "P1-SPELL-ISOLATE",
                    "UNL-124/219",
                    targetObjectIds),
                CancellationToken.None);

            Assert.False(result.Accepted);
            Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
            Assert.Empty(result.Events);
            Assert.Equal(0, result.State.Tick);
            Assert.Empty(result.State.StackItems);
        }
    }

    [Fact]
    public async Task CoreRuleEngineAllowsUpToTwoTargetSpellWithOneTarget()
    {
        var state = PunishmentState(mana: 6) with
        {
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(6, 0),
                ["P2"] = RunePool.Empty
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-STELLAR-CONVERGENCE"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-UNIT-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-UNIT-001"] = new("P2-UNIT-001")
            }
        };
        state = PrintedCostFixture.Add(state, "P1", "OGN·105/298");

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-stellar-convergence-one-target", "P1", "PLAY_CARD"),
            new PlayCardCommand("P1-SPELL-STELLAR-CONVERGENCE", "OGN·105/298", ["P2-UNIT-001"]),
            CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Equal(1, result.State.Tick);
        Assert.Single(result.State.StackItems);
        Assert.Equal(new[] { "P2-UNIT-001" }, result.State.StackItems[0].TargetObjectIds);
    }

    [Fact]
    public async Task CoreRuleEngineAllowsPiercingLightWithOneBattlefieldTarget()
    {
        var state = PunishmentState(mana: 2) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-PIERCING-LIGHT"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-UNIT-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-UNIT-001"] = new("P2-UNIT-001", power: 5)
            }
        };
        state = PrintedCostFixture.Add(state, "P1", "SFD·023/221");

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-piercing-light-one-target", "P1", "PLAY_CARD"),
            new PlayCardCommand("P1-SPELL-PIERCING-LIGHT", "SFD·023/221", ["P2-UNIT-001"]),
            CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Single(result.State.StackItems);
        Assert.Equal(new[] { "P2-UNIT-001" }, result.State.StackItems[0].TargetObjectIds);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsPiercingLightRepeatedTarget()
    {
        var state = PunishmentState(mana: 2) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-PIERCING-LIGHT"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-UNIT-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-UNIT-001"] = new("P2-UNIT-001", power: 5)
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-piercing-light-repeated-target", "P1", "PLAY_CARD"),
            new PlayCardCommand("P1-SPELL-PIERCING-LIGHT", "SFD·023/221", ["P2-UNIT-001", "P2-UNIT-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4PiercingLightRepeatedTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-piercing-light-repeated-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(2, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-PIERCING-LIGHT"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P2-UNIT-001"], result.FinalState.PlayerZones["P2"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P2-UNIT-001"].Damage);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsBellowsBreathRepeatedOrFourthTarget()
    {
        var state = PunishmentState(mana: 1) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-BELLOWS-BREATH"],
                    Battlefields = ["P1-UNIT-001", "P1-UNIT-002"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-UNIT-001", "P2-UNIT-002"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-UNIT-001"] = new("P1-UNIT-001"),
                ["P1-UNIT-002"] = new("P1-UNIT-002"),
                ["P2-UNIT-001"] = new("P2-UNIT-001"),
                ["P2-UNIT-002"] = new("P2-UNIT-002")
            }
        };

        IReadOnlyList<string>[] invalidTargets =
        [
            ["P1-UNIT-001", "P1-UNIT-001"],
            ["P1-UNIT-001", "P1-UNIT-002", "P2-UNIT-001", "P2-UNIT-002"]
        ];

        foreach (var targetObjectIds in invalidTargets)
        {
            var result = await new CoreRuleEngine().ResolveAsync(
                state,
                new PlayerIntent("intent-bellows-breath-invalid-targets", "P1", "PLAY_CARD"),
                new PlayCardCommand(
                    "P1-SPELL-BELLOWS-BREATH",
                    "SFD·080/221",
                    targetObjectIds),
                CancellationToken.None);

            Assert.False(result.Accepted);
            Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
            Assert.Empty(result.Events);
            Assert.Equal(0, result.State.Tick);
            Assert.Empty(result.State.StackItems);
        }
    }

    [Fact]
    public async Task P4BellowsBreathFourthTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-bellows-breath-fourth-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(1, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-BELLOWS-BREATH"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-UNIT-001", "P1-UNIT-002"], result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(["P2-UNIT-001", "P2-UNIT-002"], result.FinalState.PlayerZones["P2"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-UNIT-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-UNIT-002"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P2-UNIT-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P2-UNIT-002"].Damage);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4BellowsBreathRepeatedTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-bellows-breath-repeated-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(1, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-BELLOWS-BREATH"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-UNIT-001", "P1-UNIT-002"], result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(["P2-UNIT-001", "P2-UNIT-002"], result.FinalState.PlayerZones["P2"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-UNIT-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-UNIT-002"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P2-UNIT-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P2-UNIT-002"].Damage);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsFirestormUnitTarget()
    {
        var state = PunishmentState(mana: 6) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-FIRESTORM"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-UNIT-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-UNIT-001"] = new("P2-UNIT-001")
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-firestorm-unit-target", "P1", "PLAY_CARD"),
            new PlayCardCommand("P1-SPELL-FIRESTORM", "OGS·002/024", ["P2-UNIT-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4FirestormExplicitUnitTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-firestorm-explicit-unit-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(6, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-FIRESTORM"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P2-UNIT-001"], result.FinalState.PlayerZones["P2"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P2-UNIT-001"].Damage);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsCrescentStrikeAgainstFriendlyOrBaseUnit()
    {
        var state = PunishmentState(mana: 3) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-CRESCENT-STRIKE"],
                    Battlefields = ["P1-FRIENDLY-UNIT-001"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Base = ["P2-BASE-UNIT-001"],
                    Battlefields = ["P2-BATTLEFIELD-UNIT-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-FRIENDLY-UNIT-001"] = new("P1-FRIENDLY-UNIT-001", power: 4, cardNo: "OGN·219/298", tags: [CardObjectTags.UnitCard], ownerId: "P1", controllerId: "P1"),
                ["P2-BASE-UNIT-001"] = new("P2-BASE-UNIT-001", power: 4, cardNo: "OGN·219/298", tags: [CardObjectTags.UnitCard], ownerId: "P2", controllerId: "P2"),
                ["P2-BATTLEFIELD-UNIT-001"] = new("P2-BATTLEFIELD-UNIT-001", power: 5, cardNo: "OGN·049/298", tags: [CardObjectTags.UnitCard], ownerId: "P2", controllerId: "P2")
            }
        };
        state = SelectedBattlefieldFixture.WithExplicitFields(state);

        IReadOnlyList<string>[] invalidTargets =
        [
            [SelectedBattlefieldFixture.FriendlyField, "P1-FRIENDLY-UNIT-001"],
            [SelectedBattlefieldFixture.Field, "P2-BASE-UNIT-001"]
        ];

        foreach (var targetObjectIds in invalidTargets)
        {
            var result = await new CoreRuleEngine().ResolveAsync(
                state,
                new PlayerIntent("intent-crescent-strike-invalid-targets", "P1", "PLAY_CARD"),
                new PlayCardCommand("P1-SPELL-CRESCENT-STRIKE", "UNL-072/219", targetObjectIds),
                CancellationToken.None);

            Assert.False(result.Accepted);
            Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
            Assert.Empty(result.Events);
            Assert.Equal(0, result.State.Tick);
            Assert.Empty(result.State.StackItems);
        }
    }

    [Fact]
    public async Task P4CrescentStrikeFriendlyTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-crescent-strike-friendly-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(3, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-CRESCENT-STRIKE"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-FRIENDLY-UNIT-001", SelectedBattlefieldFixture.FriendlyField], result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(["P2-BATTLEFIELD-UNIT-001", SelectedBattlefieldFixture.Field], result.FinalState.PlayerZones["P2"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-FRIENDLY-UNIT-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P2-BATTLEFIELD-UNIT-001"].Damage);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4CrescentStrikeBaseTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-crescent-strike-base-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(3, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-CRESCENT-STRIKE"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-FRIENDLY-UNIT-001", SelectedBattlefieldFixture.FriendlyField], result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(["P2-BASE-UNIT-001"], result.FinalState.PlayerZones["P2"].Base);
        Assert.Equal(["P2-BATTLEFIELD-UNIT-001", SelectedBattlefieldFixture.Field], result.FinalState.PlayerZones["P2"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-FRIENDLY-UNIT-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P2-BASE-UNIT-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P2-BATTLEFIELD-UNIT-001"].Damage);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsSwitcherooAgainstDuplicateOrBaseTarget()
    {
        var state = PunishmentState(mana: 2) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-SWITCHEROO"],
                    Base = ["P1-BASE-UNIT-001"],
                    Battlefields = ["P1-BATTLEFIELD-UNIT-001"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-BATTLEFIELD-UNIT-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BASE-UNIT-001"] = new("P1-BASE-UNIT-001"),
                ["P1-BATTLEFIELD-UNIT-001"] = new("P1-BATTLEFIELD-UNIT-001"),
                ["P2-BATTLEFIELD-UNIT-001"] = new("P2-BATTLEFIELD-UNIT-001")
            }
        };

        IReadOnlyList<string>[] invalidTargets =
        [
            ["P1-BATTLEFIELD-UNIT-001", "P1-BATTLEFIELD-UNIT-001"],
            ["P1-BASE-UNIT-001", "P2-BATTLEFIELD-UNIT-001"]
        ];

        foreach (var targetObjectIds in invalidTargets)
        {
            var result = await new CoreRuleEngine().ResolveAsync(
                state,
                new PlayerIntent("intent-switcheroo-invalid-targets", "P1", "PLAY_CARD"),
                new PlayCardCommand(
                    "P1-SPELL-SWITCHEROO",
                    "SFD·145/221",
                    targetObjectIds),
                CancellationToken.None);

            Assert.False(result.Accepted);
            Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
            Assert.Empty(result.Events);
            Assert.Equal(0, result.State.Tick);
            Assert.Empty(result.State.StackItems);
        }
    }

    [Fact]
    public async Task P4SwitcherooDuplicateTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-switcheroo-duplicate-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(2, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-SWITCHEROO"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-BATTLEFIELD-UNIT-001"], result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(["P2-BATTLEFIELD-UNIT-001"], result.FinalState.PlayerZones["P2"].Battlefields);
        Assert.Equal(2, result.FinalState.CardObjects["P1-BATTLEFIELD-UNIT-001"].Power);
        Assert.Equal(5, result.FinalState.CardObjects["P2-BATTLEFIELD-UNIT-001"].Power);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4SwitcherooBaseTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-switcheroo-base-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(2, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-SWITCHEROO"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-BASE-UNIT-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(["P1-BATTLEFIELD-UNIT-001"], result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(["P2-BATTLEFIELD-UNIT-001"], result.FinalState.PlayerZones["P2"].Battlefields);
        Assert.Equal(4, result.FinalState.CardObjects["P1-BASE-UNIT-001"].Power);
        Assert.Equal(2, result.FinalState.CardObjects["P1-BATTLEFIELD-UNIT-001"].Power);
        Assert.Equal(5, result.FinalState.CardObjects["P2-BATTLEFIELD-UNIT-001"].Power);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsModalSpellWhenModeIsMissing()
    {
        var state = PunishmentState(mana: 4) with
        {
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(4, 0),
                ["P2"] = RunePool.Empty
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-ROCKET-BARRAGE"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Base = ["P2-BASE-UNIT-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-BASE-UNIT-001"] = new("P2-BASE-UNIT-001")
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-rocket-barrage-missing-mode", "P1", "PLAY_CARD"),
            new PlayCardCommand("P1-SPELL-ROCKET-BARRAGE", "SFD·077/221", ["P2-BASE-UNIT-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.UnsupportedCardBehavior, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4RocketBarrageMissingModeRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-rocket-barrage-missing-mode-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(4, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-ROCKET-BARRAGE"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P2-BASE-UNIT-001"], result.FinalState.PlayerZones["P2"].Base);
        Assert.Equal(0, result.FinalState.CardObjects["P2-BASE-UNIT-001"].Damage);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsRocketBarrageDestroyEquipmentModeAgainstUnit()
    {
        var state = PunishmentState(mana: 4) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-ROCKET-BARRAGE"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Base = ["P2-BASE-ROCKET-BARRAGE-UNIT-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-BASE-ROCKET-BARRAGE-UNIT-001"] = new(
                    "P2-BASE-ROCKET-BARRAGE-UNIT-001",
                    power: 2,
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-rocket-barrage-destroy-equipment-unit-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-ROCKET-BARRAGE",
                "SFD·077/221",
                ["P2-BASE-ROCKET-BARRAGE-UNIT-001"],
                Mode: "DESTROY_EQUIPMENT"),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(4, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-ROCKET-BARRAGE"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P2-BASE-ROCKET-BARRAGE-UNIT-001"], result.State.PlayerZones["P2"].Base);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4RocketBarrageDestroyEquipmentUnitTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-rocket-barrage-destroy-equipment-unit-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(4, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-ROCKET-BARRAGE"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P2-BASE-ROCKET-BARRAGE-UNIT-001"], result.FinalState.PlayerZones["P2"].Base);
        Assert.Equal(0, result.FinalState.CardObjects["P2-BASE-ROCKET-BARRAGE-UNIT-001"].Damage);
        Assert.Equal(2, result.FinalState.CardObjects["P2-BASE-ROCKET-BARRAGE-UNIT-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P2-BASE-ROCKET-BARRAGE-UNIT-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsEchoWhenManaIsInsufficient()
    {
        var state = CenterStageState(mana: 3);
        var initialStateHash = MatchStateHasher.Hash(state);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-center-stage-echo-no-mana", "P1", "PLAY_CARD"),
            new PlayCardCommand("P1-SPELL-CENTER-STAGE", "UNL-061/219", [], OptionalCosts: ["ECHO"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InsufficientCost, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(initialStateHash, MatchStateHasher.Hash(result.State));
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(3, 0), result.State.RunePools["P1"]);
        Assert.Equal(new[] { "P1-SPELL-CENTER-STAGE" }, result.State.PlayerZones["P1"].Hand);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4CenterStageEchoInsufficientManaRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-center-stage-echo-insufficient-mana-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(3, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-DRAW-001", "P1-DRAW-002"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-SPELL-CENTER-STAGE"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Graveyard);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsEchoOnNonEchoSpell()
    {
        var state = PunishmentState(mana: 4);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-punishment-illegal-echo", "P1", "PLAY_CARD"),
            new PlayCardCommand("P1-SPELL-PUNISHMENT", "UNL-007/219", ["P2-UNIT-001"], OptionalCosts: ["ECHO"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.UnsupportedCardBehavior, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(4, 0), result.State.RunePools["P1"]);
        Assert.Equal(new[] { "P1-SPELL-PUNISHMENT" }, result.State.PlayerZones["P1"].Hand);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4PunishmentIllegalEchoRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-punishment-illegal-echo-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(4, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-PUNISHMENT"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P2-UNIT-001"], result.FinalState.PlayerZones["P2"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P2-UNIT-001"].Damage);
        Assert.Empty(result.FinalState.StackItems);
    }
}
