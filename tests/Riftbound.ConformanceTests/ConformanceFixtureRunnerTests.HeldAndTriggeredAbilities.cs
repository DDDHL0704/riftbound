using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Engine;
using Riftbound.Contracts;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed partial class ConformanceFixtureRunnerTests
{

    [Fact]
    public async Task P79BattlefieldUnitExperienceAbilityRejectsWithoutMutationGarden()
    {
        var state = BattlefieldUnitExperienceAbilityState(includeMutationGarden: false);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-battlefield-unit-experience-ability-missing-garden", "P1", "ACTIVATE_ABILITY"),
            new ActivateAbilityCommand(
                "P1-BATTLEFIELD-EXPERIENCE-UNIT",
                "BATTLEFIELD_UNIT_EXHAUST_GAIN_EXPERIENCE",
                []),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.UnsupportedCardBehavior, result.ErrorCode);
        Assert.Equal("没有可用的战场授予经验技能。", result.ErrorMessage);
        Assert.DoesNotContain("Mutation Garden", result.ErrorMessage, StringComparison.Ordinal);
        Assert.DoesNotContain("ACTIVATE_ABILITY", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Equal(0, result.State.PlayerExperience["P1"]);
        Assert.False(result.State.CardObjects["P1-BATTLEFIELD-EXPERIENCE-UNIT"].IsExhausted);
        Assert.Empty(result.Events);
    }

    [Fact]
    public async Task P79BattlefieldUnitExperienceAbilityRejectsSourceWithoutCardNo()
    {
        var state = BattlefieldUnitExperienceAbilityState();
        var cardObjects = state.CardObjects.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        cardObjects["P1-BATTLEFIELD-EXPERIENCE-UNIT"] = cardObjects["P1-BATTLEFIELD-EXPERIENCE-UNIT"] with
        {
            CardNo = null
        };
        state = state with
        {
            CardObjects = cardObjects
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-battlefield-unit-experience-unknown-source", "P1", "ACTIVATE_ABILITY"),
            new ActivateAbilityCommand(
                "P1-BATTLEFIELD-EXPERIENCE-UNIT",
                "BATTLEFIELD_UNIT_EXHAUST_GAIN_EXPERIENCE",
                []),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.UnsupportedCardBehavior, result.ErrorCode);
        Assert.Equal("战场授予经验技能需要服务端已确认的单位牌信息。", result.ErrorMessage);
        Assert.DoesNotContain("Battlefield unit", result.ErrorMessage, StringComparison.Ordinal);
        Assert.DoesNotContain("ACTIVATE_ABILITY", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Equal(0, result.State.PlayerExperience["P1"]);
        Assert.False(result.State.CardObjects["P1-BATTLEFIELD-EXPERIENCE-UNIT"].IsExhausted);
        Assert.Empty(result.Events);
    }

    [Fact]
    public async Task P79BattlefieldUnitExperienceAbilityRejectsExhaustedSourceWithChineseError()
    {
        var state = BattlefieldUnitExperienceAbilityState();
        var cardObjects = state.CardObjects.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        cardObjects["P1-BATTLEFIELD-EXPERIENCE-UNIT"] = cardObjects["P1-BATTLEFIELD-EXPERIENCE-UNIT"] with
        {
            IsExhausted = true
        };
        state = state with
        {
            CardObjects = cardObjects
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-battlefield-unit-experience-exhausted-source", "P1", "ACTIVATE_ABILITY"),
            new ActivateAbilityCommand(
                "P1-BATTLEFIELD-EXPERIENCE-UNIT",
                "BATTLEFIELD_UNIT_EXHAUST_GAIN_EXPERIENCE",
                []),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Equal("战场授予经验技能需要选择未横置的单位。", result.ErrorMessage);
        Assert.DoesNotContain("Battlefield unit", result.ErrorMessage, StringComparison.Ordinal);
        Assert.DoesNotContain("ACTIVATE_ABILITY", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Equal(0, result.State.PlayerExperience["P1"]);
        Assert.True(result.State.CardObjects["P1-BATTLEFIELD-EXPERIENCE-UNIT"].IsExhausted);
        Assert.Empty(result.Events);
    }

    [Fact]
    public async Task P79BattlefieldReturnedUnitPaysOneAndCallsRune()
    {
        var state = BattlefieldReturnCallRuneState();
        var engine = new CoreRuleEngine();

        var play = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-battlefield-return-call-rune-play", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-RECONSIDER",
                "OGN·104/298",
                ["P1-BATTLEFIELD-RETURN-UNIT"]),
            CancellationToken.None);
        var p1Pass = await engine.ResolveAsync(
            play.State,
            new PlayerIntent("intent-p7-9-battlefield-return-call-rune-p1-pass", "P1", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-p7-9-battlefield-return-call-rune-p2-pass", "P2", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(play.Accepted);
        Assert.True(p1Pass.Accepted);
        Assert.True(p2Pass.Accepted);
        Assert.Equal(new RunePool(0, 0), p2Pass.State.RunePools["P1"]);
        Assert.Equal(["P1-BATTLEFIELD-RETURN-UNIT"], p2Pass.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-GHOST-BAY-RUNE-001", "P1-GHOST-BAY-RUNE-002"], p2Pass.State.PlayerZones["P1"].Base);
        Assert.Empty(p2Pass.State.PlayerZones["P1"].RuneDeck);
        var trigger = Assert.Single(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "BATTLEFIELD_UNIT_RETURNED_PAY_1_CALL_RUNE", StringComparison.Ordinal));
        Assert.Equal("P1-BATTLEFIELD-GHOST-BAY", trigger.Payload["battlefieldObjectId"]);
        Assert.Equal("P1-BATTLEFIELD-RETURN-UNIT", trigger.Payload["returnedObjectId"]);
        Assert.Contains(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["reason"] as string, "BATTLEFIELD_UNIT_RETURNED_PAY_1_CALL_RUNE", StringComparison.Ordinal));
        Assert.Equal(2, p2Pass.Events.Count(gameEvent => string.Equals(gameEvent.Kind, "RUNES_CALLED", StringComparison.Ordinal)));
        var ghostBayRuneEvent = Assert.Single(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "RUNES_CALLED", StringComparison.Ordinal)
            && gameEvent.Payload.TryGetValue("reason", out var reason)
            && string.Equals(reason as string, "BATTLEFIELD_UNIT_RETURNED_PAY_1_CALL_RUNE", StringComparison.Ordinal));
        Assert.Equal(["P1-GHOST-BAY-RUNE-002"], Assert.IsAssignableFrom<IReadOnlyList<string>>(ghostBayRuneEvent.Payload["runeObjectIds"]));
    }

    [Fact]
    public async Task P79BattlefieldReturnedUnitDoesNotPayWhenNoRuneCanBeCalled()
    {
        var baseState = BattlefieldReturnCallRuneState();
        var state = baseState with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(baseState.PlayerZones, StringComparer.Ordinal)
            {
                ["P1"] = baseState.PlayerZones["P1"] with
                {
                    RuneDeck = []
                }
            }
        };
        var engine = new CoreRuleEngine();

        var play = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-battlefield-return-call-rune-no-rune-play", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-RECONSIDER",
                "OGN·104/298",
                ["P1-BATTLEFIELD-RETURN-UNIT"]),
            CancellationToken.None);
        var p1Pass = await engine.ResolveAsync(
            play.State,
            new PlayerIntent("intent-p7-9-battlefield-return-call-rune-no-rune-p1-pass", "P1", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-p7-9-battlefield-return-call-rune-no-rune-p2-pass", "P2", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(play.Accepted);
        Assert.True(p1Pass.Accepted);
        Assert.True(p2Pass.Accepted);
        Assert.Equal(new RunePool(1, 0), p2Pass.State.RunePools["P1"]);
        Assert.Equal(["P1-BATTLEFIELD-RETURN-UNIT"], p2Pass.State.PlayerZones["P1"].Hand);
        Assert.Empty(p2Pass.State.PlayerZones["P1"].Base);
        Assert.DoesNotContain(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "BATTLEFIELD_UNIT_RETURNED_PAY_1_CALL_RUNE", StringComparison.Ordinal));
        Assert.DoesNotContain(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal)
            && gameEvent.Payload.TryGetValue("reason", out var reason)
            && string.Equals(reason as string, "BATTLEFIELD_UNIT_RETURNED_PAY_1_CALL_RUNE", StringComparison.Ordinal));
    }

    [Fact]
    public async Task P79BattlefieldReturnedUnitDoesNotPayWhenGhostBayIsOpponentOwned()
    {
        var baseState = BattlefieldReturnCallRuneState();
        var cardObjects = baseState.CardObjects.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        cardObjects["P1-BATTLEFIELD-GHOST-BAY"] = cardObjects["P1-BATTLEFIELD-GHOST-BAY"] with
        {
            OwnerId = "P2",
            ControllerId = ""
        };
        var state = baseState with
        {
            CardObjects = cardObjects
        };
        var engine = new CoreRuleEngine();

        var play = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-battlefield-return-call-rune-dirty-play", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-RECONSIDER",
                "OGN·104/298",
                ["P1-BATTLEFIELD-RETURN-UNIT"]),
            CancellationToken.None);
        var p1Pass = await engine.ResolveAsync(
            play.State,
            new PlayerIntent("intent-p7-9-battlefield-return-call-rune-dirty-p1-pass", "P1", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-p7-9-battlefield-return-call-rune-dirty-p2-pass", "P2", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(play.Accepted);
        Assert.True(p1Pass.Accepted);
        Assert.True(p2Pass.Accepted);
        Assert.Equal(new RunePool(1, 0), p2Pass.State.RunePools["P1"]);
        Assert.Equal(["P1-BATTLEFIELD-RETURN-UNIT"], p2Pass.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-GHOST-BAY-RUNE-001"], p2Pass.State.PlayerZones["P1"].Base);
        Assert.Equal(["P1-GHOST-BAY-RUNE-002"], p2Pass.State.PlayerZones["P1"].RuneDeck);
        Assert.DoesNotContain(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "BATTLEFIELD_UNIT_RETURNED_PAY_1_CALL_RUNE", StringComparison.Ordinal));
        Assert.DoesNotContain(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal)
            && gameEvent.Payload.TryGetValue("reason", out var reason)
            && string.Equals(reason as string, "BATTLEFIELD_UNIT_RETURNED_PAY_1_CALL_RUNE", StringComparison.Ordinal));
    }

    [Fact]
    public async Task P79BattlefieldTargetDamageBonusAddsOneToSpellDamage()
    {
        var state = BattlefieldTargetDamageBonusState();
        state = PrintedCostFixture.Add(state, "P1", "UNL-007/219");
        var engine = new CoreRuleEngine();

        var play = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-battlefield-target-damage-bonus-play", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-PUNISHMENT",
                "UNL-007/219",
                ["P2-BATTLEFIELD-TARGET"]),
            CancellationToken.None);
        var p1Pass = await engine.ResolveAsync(
            play.State,
            new PlayerIntent("intent-p7-9-battlefield-target-damage-bonus-p1-pass", "P1", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-p7-9-battlefield-target-damage-bonus-p2-pass", "P2", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(play.Accepted);
        Assert.True(p1Pass.Accepted);
        Assert.True(p2Pass.Accepted);
        Assert.Equal(4, p2Pass.State.CardObjects["P2-BATTLEFIELD-TARGET"].Damage);
        var damageEvent = Assert.Single(p2Pass.Events, gameEvent => string.Equals(gameEvent.Kind, "DAMAGE_APPLIED", StringComparison.Ordinal));
        Assert.Equal("P2-BATTLEFIELD-TARGET", damageEvent.Payload["targetObjectId"]);
        Assert.Equal(4, damageEvent.Payload["damage"]);
    }

    [Fact]
    public async Task P79BattlefieldTargetDamageBonusSkipsTargetsWithoutVoidGate()
    {
        var state = BattlefieldTargetDamageBonusState(includeVoidGate: false);
        state = PrintedCostFixture.Add(state, "P1", "UNL-007/219");
        var engine = new CoreRuleEngine();

        var play = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-battlefield-target-damage-bonus-none-play", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-PUNISHMENT",
                "UNL-007/219",
                ["P2-BATTLEFIELD-TARGET"]),
            CancellationToken.None);
        var p1Pass = await engine.ResolveAsync(
            play.State,
            new PlayerIntent("intent-p7-9-battlefield-target-damage-bonus-none-p1-pass", "P1", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-p7-9-battlefield-target-damage-bonus-none-p2-pass", "P2", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(play.Accepted);
        Assert.True(p1Pass.Accepted);
        Assert.True(p2Pass.Accepted);
        Assert.Equal(3, p2Pass.State.CardObjects["P2-BATTLEFIELD-TARGET"].Damage);
        var damageEvent = Assert.Single(p2Pass.Events, gameEvent => string.Equals(gameEvent.Kind, "DAMAGE_APPLIED", StringComparison.Ordinal));
        Assert.Equal(3, damageEvent.Payload["damage"]);
    }

    [Fact]
    public async Task P79BattlefieldHeldUnitCostIncreaseMarksHolderUntilEndOfTurnDoesNotTriggerOnDefensiveVictory()
    {
        var state = BattlefieldHeldUnitCostIncreaseBattleState();

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-battlefield-held-unit-cost-increase-battle", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "P2-BATTLEFIELD-HELIA-VAULT",
                ["P1-BATTLEFIELD-ATTACKER"],
                ["P2-BATTLEFIELD-DEFENDER"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.DoesNotContain(result.Events, e => e.Kind == "BATTLEFIELD_HELD");
        Assert.DoesNotContain(result.State.TriggerQueue, t => t.HeldContext is not null);
        Assert.DoesNotContain(result.State.StackItems, t => t.HeldContext is not null);
        Assert.Empty(result.State.DelayedResourceGains);
    }

    [Fact]
    public async Task P79BattlefieldHeldUnitCostIncreaseAddsOneToNonTokenUnitPlay()
    {
        var state = BattlefieldHeldUnitCostIncreasePlayState();

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-battlefield-held-unit-cost-increase-play", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-UNIT-CRAFTSMAN",
                "OGN·211/298",
                []),
            CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Equal(0, result.State.RunePools["P1"].Mana);
        var costEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Equal(4, costEvent.Payload["mana"]);
        Assert.Equal(3, costEvent.Payload["baseMana"]);
        Assert.Equal(1, costEvent.Payload["battlefieldHeldUnitCostIncreaseMana"]);
    }

    [Fact]
    public async Task P79BattlefieldPlayUnitBoonPaysOneAndGrantsBoon()
    {
        var state = BattlefieldPlayUnitBoonState(mana: 4);
        var engine = new CoreRuleEngine();

        var play = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-battlefield-play-unit-boon-play", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-UNIT-CRAFTSMAN",
                "OGN·211/298",
                [],
                Destination: "BATTLEFIELD:P1-BATTLEFIELD-IDOL-VALLEY"),
            CancellationToken.None);
        var p1Pass = await engine.ResolveAsync(
            play.State,
            new PlayerIntent("intent-p7-9-battlefield-play-unit-boon-p1-pass", "P1", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-p7-9-battlefield-play-unit-boon-p2-pass", "P2", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(play.Accepted);
        Assert.True(p1Pass.Accepted);
        Assert.True(p2Pass.Accepted);
        Assert.Equal(1, play.State.RunePools["P1"].Mana);
        Assert.Equal(0, p2Pass.State.RunePools["P1"].Mana);
        Assert.Contains("P1-UNIT-CRAFTSMAN", p2Pass.State.PlayerZones["P1"].Battlefields);
        var unit = p2Pass.State.CardObjects["P1-UNIT-CRAFTSMAN"];
        Assert.Equal(3, unit.Power);
        Assert.Contains(CardObjectTags.Boon, unit.Tags);
        Assert.Contains(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "BATTLEFIELD_PLAY_UNIT_PAY_1_GRANT_BOON", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, "P1-UNIT-CRAFTSMAN", StringComparison.Ordinal));
        Assert.Contains(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["reason"] as string, "BATTLEFIELD_PLAY_UNIT_PAY_1_GRANT_BOON", StringComparison.Ordinal)
            && Equals(gameEvent.Payload["mana"], 1));
        Assert.Contains(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BOON_GRANTED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, "P1-UNIT-CRAFTSMAN", StringComparison.Ordinal));
    }

    [Fact]
    public async Task P79BattlefieldPlayUnitBoonSkipsWhenManaUnavailable()
    {
        var state = BattlefieldPlayUnitBoonState(mana: 3);
        var engine = new CoreRuleEngine();

        var play = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-battlefield-play-unit-boon-no-mana-play", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-UNIT-CRAFTSMAN",
                "OGN·211/298",
                [],
                Destination: "BATTLEFIELD:P1-BATTLEFIELD-IDOL-VALLEY"),
            CancellationToken.None);
        var p1Pass = await engine.ResolveAsync(
            play.State,
            new PlayerIntent("intent-p7-9-battlefield-play-unit-boon-no-mana-p1-pass", "P1", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-p7-9-battlefield-play-unit-boon-no-mana-p2-pass", "P2", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(play.Accepted);
        Assert.True(p1Pass.Accepted);
        Assert.True(p2Pass.Accepted);
        Assert.Equal(0, p2Pass.State.RunePools["P1"].Mana);
        var unit = p2Pass.State.CardObjects["P1-UNIT-CRAFTSMAN"];
        Assert.Equal(2, unit.Power);
        Assert.DoesNotContain(CardObjectTags.Boon, unit.Tags);
        Assert.DoesNotContain(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "BATTLEFIELD_PLAY_UNIT_PAY_1_GRANT_BOON", StringComparison.Ordinal));
    }

    [Fact]
    public async Task P79BattlefieldPlayUnitBoonSkipsOpponentControlledBattlefieldSource()
    {
        var state = BattlefieldPlayUnitBoonState(mana: 4);
        var cardObjects = state.CardObjects.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        cardObjects["P1-BATTLEFIELD-IDOL-VALLEY"] = cardObjects["P1-BATTLEFIELD-IDOL-VALLEY"] with
        {
            ControllerId = "P2"
        };
        state = state with
        {
            CardObjects = cardObjects,
            PriorityPlayerId = null,
            StackItems = []
        };
        var engine = new CoreRuleEngine();

        var play = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-battlefield-play-unit-boon-dirty-source-play", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-UNIT-CRAFTSMAN",
                "OGN·211/298",
                [],
                Destination: "BASE"),
            CancellationToken.None);
        Assert.True(play.Accepted);
        Assert.Equal(1, play.State.RunePools["P1"].Mana);
        var unit = play.State.CardObjects["P1-UNIT-CRAFTSMAN"];
        Assert.Equal(2, unit.Power);
        Assert.DoesNotContain(CardObjectTags.Boon, unit.Tags);
        Assert.DoesNotContain(play.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "BATTLEFIELD_PLAY_UNIT_PAY_1_GRANT_BOON", StringComparison.Ordinal));
        Assert.DoesNotContain(play.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BOON_GRANTED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, "P1-UNIT-CRAFTSMAN", StringComparison.Ordinal));
    }

    [Fact]
    public async Task P79BattlefieldFirstUnitPlayedMovesOtherUnitToBase()
    {
        var state = BattlefieldFirstUnitMoveOtherState();
        var engine = new CoreRuleEngine();

        var play = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-battlefield-first-unit-move-other-play", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-UNIT-CRAFTSMAN",
                "OGN·211/298",
                [],
                Destination: "BATTLEFIELD:P1-BATTLEFIELD-METEOR-SPRING"),
            CancellationToken.None);
        var p1Pass = await engine.ResolveAsync(
            play.State,
            new PlayerIntent("intent-p7-9-battlefield-first-unit-move-other-p1-pass", "P1", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-p7-9-battlefield-first-unit-move-other-p2-pass", "P2", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(play.Accepted);
        Assert.True(p1Pass.Accepted);
        Assert.True(p2Pass.Accepted);
        Assert.Contains("P1-UNIT-CRAFTSMAN", p2Pass.State.PlayerZones["P1"].Battlefields);
        Assert.Contains("P1-BATTLEFIELD-ALLY", p2Pass.State.PlayerZones["P1"].Base);
        Assert.DoesNotContain("P1-BATTLEFIELD-ALLY", p2Pass.State.PlayerZones["P1"].Battlefields);
        Assert.Contains(
            "BATTLEFIELD_FIRST_UNIT_PLAYED_MOVE_OTHER_TO_BASE_USED:P1:P1-BATTLEFIELD-METEOR-SPRING",
            p2Pass.State.UntilEndOfTurnEffects);
        Assert.Contains(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "BATTLEFIELD_FIRST_UNIT_PLAYED_MOVE_OTHER_TO_BASE", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, "P1-BATTLEFIELD-ALLY", StringComparison.Ordinal));
        Assert.Contains(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_MOVED_TO_BASE", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["reason"] as string, "BATTLEFIELD_FIRST_UNIT_PLAYED_MOVE_OTHER_TO_BASE", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, "P1-BATTLEFIELD-ALLY", StringComparison.Ordinal));
    }

    [Fact]
    public async Task P79BattlefieldFirstUnitPlayedMoveOtherSkipsAfterFirstUse()
    {
        var state = BattlefieldFirstUnitMoveOtherState() with
        {
            UntilEndOfTurnEffects =
            [
                "BATTLEFIELD_FIRST_UNIT_PLAYED_MOVE_OTHER_TO_BASE_USED:P1:P1-BATTLEFIELD-METEOR-SPRING"
            ]
        };
        var engine = new CoreRuleEngine();

        var play = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-battlefield-first-unit-move-other-used-play", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-UNIT-CRAFTSMAN",
                "OGN·211/298",
                [],
                Destination: "BATTLEFIELD:P1-BATTLEFIELD-METEOR-SPRING"),
            CancellationToken.None);
        var p1Pass = await engine.ResolveAsync(
            play.State,
            new PlayerIntent("intent-p7-9-battlefield-first-unit-move-other-used-p1-pass", "P1", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-p7-9-battlefield-first-unit-move-other-used-p2-pass", "P2", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(play.Accepted);
        Assert.True(p1Pass.Accepted);
        Assert.True(p2Pass.Accepted);
        Assert.Contains("P1-BATTLEFIELD-ALLY", p2Pass.State.PlayerZones["P1"].Battlefields);
        Assert.DoesNotContain("P1-BATTLEFIELD-ALLY", p2Pass.State.PlayerZones["P1"].Base);
        Assert.DoesNotContain(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "BATTLEFIELD_FIRST_UNIT_PLAYED_MOVE_OTHER_TO_BASE", StringComparison.Ordinal));
    }

    [Fact]
    public async Task P79BattlefieldFirstUnitPlayedMoveOtherSkipsOpponentControlledBattlefieldSource()
    {
        var state = BattlefieldFirstUnitMoveOtherState();
        var cardObjects = state.CardObjects.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        cardObjects["P1-BATTLEFIELD-METEOR-SPRING"] = cardObjects["P1-BATTLEFIELD-METEOR-SPRING"] with
        {
            ControllerId = "P2"
        };
        state = state with
        {
            CardObjects = cardObjects,
            PriorityPlayerId = null,
            StackItems = []
        };
        var engine = new CoreRuleEngine();

        var play = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-battlefield-first-unit-move-other-dirty-source-play", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-UNIT-CRAFTSMAN",
                "OGN·211/298",
                [],
                Destination: "BASE"),
            CancellationToken.None);
        Assert.True(play.Accepted);
        Assert.Contains("P1-BATTLEFIELD-ALLY", play.State.PlayerZones["P1"].Battlefields);
        Assert.DoesNotContain("P1-BATTLEFIELD-ALLY", play.State.PlayerZones["P1"].Base);
        Assert.DoesNotContain(
            "BATTLEFIELD_FIRST_UNIT_PLAYED_MOVE_OTHER_TO_BASE_USED:P1:P1-BATTLEFIELD-METEOR-SPRING",
            play.State.UntilEndOfTurnEffects);
        Assert.DoesNotContain(play.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "BATTLEFIELD_FIRST_UNIT_PLAYED_MOVE_OTHER_TO_BASE", StringComparison.Ordinal));
        Assert.DoesNotContain(play.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_MOVED_TO_BASE", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, "P1-BATTLEFIELD-ALLY", StringComparison.Ordinal));
    }

    [Fact]
    public async Task P79BattlefieldStaticWinningScoreIncreaseDelaysBurnoutWin()
    {
        var state = BattlefieldWinningScoreState();

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-battlefield-winning-score", "P1", "END_TURN"),
            new EndTurnCommand(),
            CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Equal(8, result.State.PlayerScores["P1"]);
        Assert.Null(result.State.WinnerPlayerId);
        Assert.Equal(MatchStatuses.InProgress, result.State.Status);
        Assert.Contains(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BURNOUT_APPLIED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["scoredPlayerId"] as string, "P1", StringComparison.Ordinal)
            && Equals(gameEvent.Payload["scoredPlayerScore"], 8));
        Assert.DoesNotContain(result.Events, gameEvent => string.Equals(gameEvent.Kind, "MATCH_WON", StringComparison.Ordinal));
        var p2Snapshot = result.Snapshots["P2"];
        Assert.Equal(9, p2Snapshot.Timing["winningScore"]);
    }

    [Fact]
    public async Task P79BattlefieldStaticWinningScoreIncreaseAppliesRegardlessOfController()
    {
        var state = BattlefieldWinningScoreState();
        var dirtyObjects = state.CardObjects.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        dirtyObjects["P1-BATTLEFIELD-HIGHROAD"] = dirtyObjects["P1-BATTLEFIELD-HIGHROAD"] with
        {
            ControllerId = "P2"
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state with { CardObjects = dirtyObjects },
            new PlayerIntent("intent-p7-9-battlefield-winning-score-dirty-source", "P1", "END_TURN"),
            new EndTurnCommand(),
            CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Equal(8, result.State.PlayerScores["P1"]);
        Assert.Null(result.State.WinnerPlayerId);
        Assert.Equal(MatchStatuses.InProgress, result.State.Status);
        Assert.DoesNotContain(result.Events, e => e.Kind == "MATCH_WON");
        Assert.Equal(9, result.Snapshots["P2"].Timing["winningScore"]);
    }

    [Fact]
    public async Task P79BattlefieldStaticWinningScoreIncreaseStillWinsAtNine()
    {
        var state = BattlefieldWinningScoreState(score: 8);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-battlefield-winning-score-win", "P1", "END_TURN"),
            new EndTurnCommand(),
            CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Equal(9, result.State.PlayerScores["P1"]);
        Assert.Equal("P1", result.State.WinnerPlayerId);
        Assert.Equal(MatchStatuses.Finished, result.State.Status);
        var winEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "MATCH_WON", StringComparison.Ordinal));
        Assert.Equal("P1", winEvent.Payload["winnerPlayerId"]);
        Assert.Equal(9, winEvent.Payload["winningScore"]);
    }

    [Fact]
    public async Task P79BattlefieldStaticFirstTurnRuneCallsOneExtraRune()
    {
        var state = BattlefieldFirstTurnRuneState();

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-battlefield-first-turn-rune", "P1", "END_TURN"),
            new EndTurnCommand(),
            CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Equal("P2", result.State.TurnPlayerId);
        Assert.Empty(result.State.PlayerZones["P2"].RuneDeck);
        Assert.Equal(
            ["P2-RUNE-001", "P2-RUNE-002", "P2-RUNE-003", "P2-RUNE-004"],
            result.State.PlayerZones["P2"].Base);
        var runeEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "RUNES_CALLED", StringComparison.Ordinal));
        Assert.Equal("P2", runeEvent.Payload["playerId"]);
        Assert.Equal(4, runeEvent.Payload["count"]);
    }

    [Fact]
    public async Task P79BattlefieldStaticFirstTurnRuneIgnoresBattlefieldControlChange()
    {
        var state = BattlefieldFirstTurnRuneState();
        var dirtyObjects = state.CardObjects.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        dirtyObjects["P1-BATTLEFIELD-RUNE-OBELISK"] = dirtyObjects["P1-BATTLEFIELD-RUNE-OBELISK"] with
        {
            ControllerId = "P2"
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state with { CardObjects = dirtyObjects },
            new PlayerIntent("intent-p7-9-battlefield-first-turn-rune-control-change", "P1", "END_TURN"),
            new EndTurnCommand(),
            CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Equal("P2", result.State.TurnPlayerId);
        Assert.Empty(result.State.PlayerZones["P2"].RuneDeck);
        Assert.Equal(
            ["P2-RUNE-001", "P2-RUNE-002", "P2-RUNE-003", "P2-RUNE-004"],
            result.State.PlayerZones["P2"].Base);
        var runeEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "RUNES_CALLED", StringComparison.Ordinal));
        Assert.Equal("P2", runeEvent.Payload["playerId"]);
        Assert.Equal(4, runeEvent.Payload["count"]);
    }

    [Fact]
    public async Task P79BattlefieldStaticFirstTurnScoreGainsOneScore()
    {
        var state = BattlefieldFirstTurnScoreState();

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-battlefield-first-turn-score", "P1", "END_TURN"),
            new EndTurnCommand(),
            CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Equal("P2", result.State.TurnPlayerId);
        Assert.Equal(1, result.State.PlayerScores["P2"]);
        Assert.Null(result.State.WinnerPlayerId);
        Assert.Contains(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "BATTLEFIELD_FIRST_TURN_GAIN_SCORE", StringComparison.Ordinal));
        var scoreEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "SCORE_GAINED", StringComparison.Ordinal));
        Assert.Equal("P2", scoreEvent.Payload["playerId"]);
        Assert.Equal(1, scoreEvent.Payload["amount"]);
        Assert.Equal(1, scoreEvent.Payload["score"]);
        Assert.DoesNotContain(result.Events, gameEvent => string.Equals(gameEvent.Kind, "MATCH_WON", StringComparison.Ordinal));
    }

    [Fact]
    public async Task P79BattlefieldStaticFirstTurnScoreIgnoresBattlefieldControlChange()
    {
        var state = BattlefieldFirstTurnScoreState();
        var dirtyObjects = state.CardObjects.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        dirtyObjects["P1-BATTLEFIELD-GLORY-ARENA"] = dirtyObjects["P1-BATTLEFIELD-GLORY-ARENA"] with
        {
            ControllerId = "P2"
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state with { CardObjects = dirtyObjects },
            new PlayerIntent("intent-p7-9-battlefield-first-turn-score-control-change", "P1", "END_TURN"),
            new EndTurnCommand(),
            CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Equal("P2", result.State.TurnPlayerId);
        Assert.Equal(1, result.State.PlayerScores["P2"]);
        Assert.Contains(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "BATTLEFIELD_FIRST_TURN_GAIN_SCORE", StringComparison.Ordinal));
        var scoreEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "SCORE_GAINED", StringComparison.Ordinal));
        Assert.Equal("P2", scoreEvent.Payload["playerId"]);
        Assert.Equal(1, scoreEvent.Payload["amount"]);
    }

    [Fact]
    public async Task P79BattlefieldScoreDelayDoesNotPreventOtherBattlefieldFirstTurnScore()
    {
        var baseState = BattlefieldFirstTurnScoreState();
        var state = baseState with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(baseState.PlayerZones, StringComparer.Ordinal)
            {
                ["P1"] = baseState.PlayerZones["P1"] with
                {
                    Battlefields = ["P1-BATTLEFIELD-GLORY-ARENA", "P1-BATTLEFIELD-FORGOTTEN-MONUMENT"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(baseState.CardObjects, StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-FORGOTTEN-MONUMENT"] = new(
                    "P1-BATTLEFIELD-FORGOTTEN-MONUMENT",
                    cardNo: "SFD·209/221",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: "P1",
                    controllerId: "P1")
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-battlefield-score-delay-first-turn", "P1", "END_TURN"),
            new EndTurnCommand(),
            CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Equal("P2", result.State.TurnPlayerId);
        // Official SFD 209: "from here" never prevents Glory Arena's separate score.
        Assert.Equal(1, result.State.PlayerScores["P2"]);
        Assert.DoesNotContain(result.Events, e => e.Kind == "BATTLEFIELD_SCORE_PREVENTED");
        var scoreEvent = Assert.Single(result.Events, e => e.Kind == "SCORE_GAINED");
        Assert.Equal("BATTLEFIELD_FIRST_TURN_GAIN_SCORE", scoreEvent.Payload["reason"]);
        Assert.Equal("P2", scoreEvent.Payload["playerId"]);
        Assert.DoesNotContain(result.Events, e => e.Kind == "MATCH_WON");
    }

    [Fact]
    public async Task P79BattlefieldScoreDelayDoesNotAffectOtherBattlefieldsAfterControlChange()
    {
        var baseState = BattlefieldFirstTurnScoreState();
        var state = baseState with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(baseState.PlayerZones, StringComparer.Ordinal)
            {
                ["P1"] = baseState.PlayerZones["P1"] with
                {
                    Battlefields = ["P1-BATTLEFIELD-GLORY-ARENA", "P1-BATTLEFIELD-FORGOTTEN-MONUMENT"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(baseState.CardObjects, StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-FORGOTTEN-MONUMENT"] = new(
                    "P1-BATTLEFIELD-FORGOTTEN-MONUMENT",
                    cardNo: "SFD·209/221",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: "P1",
                    controllerId: "P2")
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-battlefield-score-delay-control-change", "P1", "END_TURN"),
            new EndTurnCommand(),
            CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Equal("P2", result.State.TurnPlayerId);
        // Official SFD 209: "from here" never prevents Glory Arena's separate score.
        Assert.Equal(1, result.State.PlayerScores["P2"]);
        var prevented = Assert.Single(result.Events, e => e.Kind == "BATTLEFIELD_SCORE_PREVENTED");
        Assert.Equal(new[] { "P1-BATTLEFIELD-FORGOTTEN-MONUMENT" }, Assert.IsType<string[]>(prevented.Payload["sourceObjectIds"]));
        var scoreEvent = Assert.Single(result.Events, e => e.Kind == "SCORE_GAINED");
        Assert.Equal("BATTLEFIELD_FIRST_TURN_GAIN_SCORE", scoreEvent.Payload["reason"]);
        Assert.Equal("P2", scoreEvent.Payload["playerId"]);
        Assert.DoesNotContain(result.Events, e => e.Kind == "MATCH_WON");
    }

    [Fact]
    public async Task P79BattlefieldTurnStartDamageAllBattlefieldUnitsBeforeScoring()
    {
        var state = BattlefieldTurnStartDamageState();

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-battlefield-turn-start-damage", "P1", "END_TURN"),
            new EndTurnCommand(),
            CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Single(result.State.StackItems);
        Assert.DoesNotContain(result.Events, e => e.Kind == "DAMAGE_APPLIED");
        result = await TurnSequenceTestDriver.Complete(result);
        Assert.Equal("P2", result.State.TurnPlayerId);
        var triggerEvent = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "BATTLEFIELD_TURN_START_DAMAGE_ALL_UNITS", StringComparison.Ordinal));
        Assert.Equal(["P1-BATTLEFIELD-FROST-HOLD"], Assert.IsAssignableFrom<IReadOnlyList<string>>(triggerEvent.Payload["sourceObjectIds"]));
        Assert.Equal(1, triggerEvent.Payload["damage"]);
        Assert.True((bool)triggerEvent.Payload["beforeScoring"]!);
        Assert.Equal(["P1-BATTLEFIELD-FROST-FALLING"], Assert.IsAssignableFrom<IReadOnlyList<string>>(triggerEvent.Payload["targetObjectIds"]));
        var damageEvents = result.Events
            .Where(gameEvent => string.Equals(gameEvent.Kind, "DAMAGE_APPLIED", StringComparison.Ordinal)
                && string.Equals(gameEvent.Payload["reason"] as string, "BATTLEFIELD_TURN_START_DAMAGE_ALL_UNITS", StringComparison.Ordinal))
            .OrderBy(gameEvent => gameEvent.Payload["targetObjectId"] as string, StringComparer.Ordinal)
            .ToArray();
        Assert.Single(damageEvents);
        Assert.Equal("P1-BATTLEFIELD-FROST-FALLING", damageEvents[0].Payload["targetObjectId"]);
        Assert.Contains(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_DESTROYED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, "P1-BATTLEFIELD-FROST-FALLING", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["reason"] as string, "LETHAL_DAMAGE", StringComparison.Ordinal));
        Assert.Equal(["P1-BATTLEFIELD-FROST-HOLD"], result.State.PlayerZones["P1"].Battlefields);
        Assert.Equal(["P1-BATTLEFIELD-FROST-FALLING"], result.State.PlayerZones["P1"].Graveyard);
        Assert.Equal("GRAVEYARD", result.State.ObjectLocations["P1-BATTLEFIELD-FROST-FALLING"].Zone);
        Assert.Contains("P2-BATTLEFIELD-FROST-SURVIVOR", result.State.PlayerZones["P2"].Battlefields);
        Assert.Equal(0, result.State.CardObjects["P2-BATTLEFIELD-FROST-SURVIVOR"].Damage);
        Assert.Contains("P1", result.State.DestroyedUnitOwnerIdsThisTurn);
        var indexedEvents = result.Events.Select((gameEvent, index) => (gameEvent, index)).ToArray();
        var triggerIndex = indexedEvents.Single(entry => ReferenceEquals(entry.gameEvent, triggerEvent)).index;
        var runeIndex = indexedEvents.First(entry => string.Equals(entry.gameEvent.Kind, "RUNES_CALLED", StringComparison.Ordinal)).index;
        Assert.True(triggerIndex >= 0);
        Assert.True(runeIndex > triggerIndex);
    }

    [Fact]
    public async Task P79BattlefieldTurnStartDamageSkipsOpponentControlledSource()
    {
        var state = BattlefieldTurnStartDamageState();
        var dirtyObjects = state.CardObjects.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        dirtyObjects["P1-BATTLEFIELD-FROST-HOLD"] = dirtyObjects["P1-BATTLEFIELD-FROST-HOLD"] with
        {
            ControllerId = "P2"
        };

        var dirtyState = state with { CardObjects = dirtyObjects };
        var result = await new CoreRuleEngine().ResolveAsync(
            dirtyState,
            new PlayerIntent("intent-p7-9-battlefield-turn-start-damage-dirty-source", "P1", "END_TURN"),
            new EndTurnCommand(),
            CancellationToken.None);

        // The historical dirty fixture puts only P1 units on a P2-controlled
        // field. With a real field identity this must finish its control task
        // before ending the turn; it cannot silently skip an official trigger.
        Assert.False(result.Accepted);
        Assert.Contains("任务队列", result.ErrorMessage);
        Assert.Equal(MatchStateHasher.Hash(dirtyState), MatchStateHasher.Hash(result.State));
        Assert.Equal("P1", result.State.TurnPlayerId);
        Assert.DoesNotContain(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "BATTLEFIELD_TURN_START_DAMAGE_ALL_UNITS", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "DAMAGE_APPLIED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["reason"] as string, "BATTLEFIELD_TURN_START_DAMAGE_ALL_UNITS", StringComparison.Ordinal));
        Assert.Contains("P1-BATTLEFIELD-FROST-FALLING", result.State.PlayerZones["P1"].Battlefields);
        Assert.Empty(result.State.PlayerZones["P1"].Graveyard);
        Assert.Equal(0, result.State.CardObjects["P1-BATTLEFIELD-FROST-FALLING"].Damage);
        Assert.Equal(0, result.State.CardObjects["P2-BATTLEFIELD-FROST-SURVIVOR"].Damage);

    }

    [Fact]
    public async Task P79BattlefieldTurnStartDestroyDrawsBeforeScoring()
    {
        var state = BattlefieldTurnStartDestroyDrawState();

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-battlefield-turn-start-destroy-draw", "P1", "END_TURN"),
            new EndTurnCommand(),
            CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.NotNull(result.State.PendingCardChoice);
        Assert.DoesNotContain(result.Events, e => e.Kind == "UNIT_DESTROYED" || e.Kind == "CARD_DRAWN");
        var openedEvents = result.Events;
        var choice = result.State.PendingCardChoice!;
        result = await new CoreRuleEngine().ResolveAsync(result.State,
            new("lab-pay", "P2", CommandTypes.ChooseCards),
            new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["P2-BATTLEFIELD-ROSE-SACRIFICE"]), default);
        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Empty(result.State.PlayerZones["P2"].Hand);
        result = await TurnSequenceTestDriver.Complete(result with { Events = openedEvents.Concat(result.Events).ToArray() });
        Assert.Equal("P2", result.State.TurnPlayerId);
        var triggerEvent = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "BATTLEFIELD_TURN_START_DESTROY_UNIT_DRAW", StringComparison.Ordinal));
        Assert.Equal(["P2-BATTLEFIELD-ROSE-LAB"], Assert.IsAssignableFrom<IReadOnlyList<string>>(triggerEvent.Payload["sourceObjectIds"]));
        Assert.Equal("P2-BATTLEFIELD-ROSE-SACRIFICE", triggerEvent.Payload["targetObjectId"]);
        Assert.Equal(1, triggerEvent.Payload["drawCount"]);
        Assert.True((bool)triggerEvent.Payload["beforeScoring"]!);
        Assert.Contains(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_DESTROYED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, "P2-BATTLEFIELD-ROSE-SACRIFICE", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["reason"] as string, "DESTROY_COST", StringComparison.Ordinal));
        Assert.Equal(2, result.Events.Count(gameEvent => string.Equals(gameEvent.Kind, "CARD_DRAWN", StringComparison.Ordinal)));
        Assert.Equal(["P2-BATTLEFIELD-AAA-OFFSITE", "P2-BATTLEFIELD-ROSE-LAB"], result.State.PlayerZones["P2"].Battlefields);
        Assert.Equal(["P2-BATTLEFIELD-ROSE-SACRIFICE"], result.State.PlayerZones["P2"].Graveyard);
        Assert.Equal(["P2-ROSE-DRAW-001", "P2-NORMAL-DRAW-001"], result.State.PlayerZones["P2"].Hand);
        Assert.Empty(result.State.PlayerZones["P2"].MainDeck);
        Assert.Contains("P2", result.State.DestroyedUnitOwnerIdsThisTurn);

        var indexedEvents = result.Events.Select((gameEvent, index) => (gameEvent, index)).ToArray();
        var triggerIndex = indexedEvents.Single(entry => ReferenceEquals(entry.gameEvent, triggerEvent)).index;
        var firstDrawIndex = indexedEvents.First(entry => string.Equals(entry.gameEvent.Kind, "CARD_DRAWN", StringComparison.Ordinal)).index;
        var runeIndex = indexedEvents.First(entry => string.Equals(entry.gameEvent.Kind, "RUNES_CALLED", StringComparison.Ordinal)).index;
        Assert.True(triggerIndex >= 0);
        Assert.True(firstDrawIndex > triggerIndex);
        Assert.True(runeIndex > firstDrawIndex);
    }

    [Fact]
    public async Task P79BattlefieldTurnStartDestroyDrawSkipsOpponentControlledSource()
    {
        var state = BattlefieldTurnStartDestroyDrawState();
        var dirtyObjects = state.CardObjects.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        dirtyObjects["P2-BATTLEFIELD-ROSE-LAB"] = dirtyObjects["P2-BATTLEFIELD-ROSE-LAB"] with
        {
            ControllerId = "P1"
        };

        var dirtyState = state with { CardObjects = dirtyObjects };
        var result = await new CoreRuleEngine().ResolveAsync(
            dirtyState,
            new PlayerIntent("intent-p7-9-battlefield-turn-start-destroy-draw-dirty-source", "P1", "END_TURN"),
            new EndTurnCommand(),
            CancellationToken.None);

        // A real field with only P2 units cannot remain controlled by P1.
        // Reject the dirty fixture until its control task completes.
        Assert.False(result.Accepted);
        Assert.Contains("任务队列", result.ErrorMessage);
        Assert.Equal(MatchStateHasher.Hash(dirtyState), MatchStateHasher.Hash(result.State));
        Assert.Equal("P1", result.State.TurnPlayerId);
        Assert.DoesNotContain(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "BATTLEFIELD_TURN_START_DESTROY_UNIT_DRAW", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_DESTROYED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["reason"] as string, "BATTLEFIELD_TURN_START_DESTROY_UNIT_DRAW", StringComparison.Ordinal));
        Assert.Equal(["P2-BATTLEFIELD-AAA-OFFSITE", "P2-BATTLEFIELD-ROSE-LAB", "P2-BATTLEFIELD-ROSE-SACRIFICE"], result.State.PlayerZones["P2"].Battlefields);
        Assert.Empty(result.State.PlayerZones["P2"].Graveyard);
        Assert.Empty(result.State.PlayerZones["P2"].Hand);
        Assert.Equal(["P2-ROSE-DRAW-001", "P2-NORMAL-DRAW-001"], result.State.PlayerZones["P2"].MainDeck);
        Assert.DoesNotContain(result.Events, gameEvent => string.Equals(gameEvent.Kind, "CARD_DRAWN", StringComparison.Ordinal));
    }

    [Fact]
    public async Task P79BattlefieldHeldPaysPowerToGainScoreDoesNotTriggerOnDefensiveVictory()
    {
        var state = BattlefieldHeldScoreState();

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-battlefield-held-score", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "P2-BATTLEFIELD-ENERGY-HUB",
                ["P1-BATTLEFIELD-ENERGY-ATTACKER"],
                ["P2-BATTLEFIELD-ENERGY-DEFENDER"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.DoesNotContain(result.Events, e => e.Kind == "BATTLEFIELD_HELD");
        Assert.DoesNotContain(result.State.TriggerQueue, t => t.HeldContext is not null);
        Assert.DoesNotContain(result.State.StackItems, t => t.HeldContext is not null);
        Assert.Empty(result.State.DelayedResourceGains);
    }

    [Fact]
    public async Task BrushReplacementCannotBeSelectedAtBattleDeclaration()
    {
        var state = BrushHeldScoreState();

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-brush-replacement-held-score", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "P2-BATTLEFIELD-BRUSH",
                ["P1-BATTLEFIELD-ENERGY-ATTACKER"],
                ["P2-BATTLEFIELD-ENERGY-DEFENDER"],
                ["COMBAT_ASSIGNMENT", "BRUSH_USE_REPLACED_BATTLEFIELD:P2-BATTLEFIELD-ENERGY-HUB"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(result.State));
        Assert.DoesNotContain(result.Events, e => e.Kind == "BATTLEFIELD_HELD");
        Assert.DoesNotContain(result.State.TriggerQueue, t => t.HeldContext is not null);
        Assert.DoesNotContain(result.State.StackItems, t => t.HeldContext is not null);
        Assert.Empty(result.State.DelayedResourceGains);
    }

    [Fact]
    public async Task BrushReplacementDoesNotAutoApplyWithoutChoice()
    {
        var state = BrushHeldScoreState();

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-brush-replacement-held-score-no-choice", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "P2-BATTLEFIELD-BRUSH",
                ["P1-BATTLEFIELD-ENERGY-ATTACKER"],
                ["P2-BATTLEFIELD-ENERGY-DEFENDER"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.DoesNotContain(result.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLEFIELD_REPLACEMENT_APPLIED", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "BATTLEFIELD_HELD_PAY_4_POWER_GAIN_SCORE", StringComparison.Ordinal));
        Assert.Equal(0, result.State.PlayerScores.TryGetValue("P2", out var score) ? score : 0);
        Assert.Equal(4, result.State.RunePools["P2"].Power);
    }

    [Theory]
    [InlineData("unknown-original")]
    [InlineData("missing-tag")]
    [InlineData("non-battlefield-original")]
    [InlineData("self-reference")]
    [InlineData("non-brush-battlefield")]
    public async Task BrushReplacementRejectsInvalidChoiceWithoutMutation(string caseName)
    {
        var baseState = BrushHeldScoreState();
        var state = caseName switch
        {
            "missing-tag" => baseState with
            {
                CardObjects = new Dictionary<string, CardObjectState>(baseState.CardObjects, StringComparer.Ordinal)
                {
                    ["P2-BATTLEFIELD-BRUSH"] = baseState.CardObjects["P2-BATTLEFIELD-BRUSH"] with
                    {
                        Tags = [P6TokenFactoryCatalog.BattlefieldCardTag, "草丛"]
                    }
                }
            },
            "non-battlefield-original" => baseState with
            {
                CardObjects = new Dictionary<string, CardObjectState>(baseState.CardObjects, StringComparer.Ordinal)
                {
                    ["P2-BATTLEFIELD-ENERGY-HUB"] = baseState.CardObjects["P2-BATTLEFIELD-ENERGY-HUB"] with
                    {
                        CardNo = "SFD·125/221",
                        Tags = [CardObjectTags.UnitCard]
                    }
                }
            },
            "self-reference" => baseState with
            {
                CardObjects = new Dictionary<string, CardObjectState>(baseState.CardObjects, StringComparer.Ordinal)
                {
                    ["P2-BATTLEFIELD-BRUSH"] = baseState.CardObjects["P2-BATTLEFIELD-BRUSH"] with
                    {
                        Tags =
                        [
                            P6TokenFactoryCatalog.BattlefieldCardTag,
                            "草丛",
                            "REPLACES_BATTLEFIELD:P2-BATTLEFIELD-BRUSH"
                        ]
                    }
                }
            },
            _ => baseState
        };
        var battlefieldId = string.Equals(caseName, "non-brush-battlefield", StringComparison.Ordinal)
            ? "P2-BATTLEFIELD-ENERGY-HUB"
            : "P2-BATTLEFIELD-BRUSH";
        var originalId = string.Equals(caseName, "unknown-original", StringComparison.Ordinal)
            ? "P2-BATTLEFIELD-UNKNOWN"
            : "P2-BATTLEFIELD-ENERGY-HUB";
        var initialHash = MatchStateHasher.Hash(state);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent($"intent-p7-9-brush-replacement-invalid-{caseName}", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                battlefieldId,
                ["P1-BATTLEFIELD-ENERGY-ATTACKER"],
                ["P2-BATTLEFIELD-ENERGY-DEFENDER"],
                ["COMBAT_ASSIGNMENT", $"BRUSH_USE_REPLACED_BATTLEFIELD:{originalId}"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Empty(result.Events);
        Assert.Equal(initialHash, MatchStateHasher.Hash(result.State));
    }

    [Fact]
    public void BrushReplacementPromptDoesNotOfferObsoleteDeclarationChoice()
    {
        var prompt = ResolutionResult.BuildPrompts(BrushHeldScoreState())["P1"];
        var declareBattleCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "DECLARE_BATTLE", StringComparison.Ordinal));
        var metadata = Assert.IsType<Dictionary<string, object?>>(declareBattleCandidate.Metadata);
        var sourceRequirement = Assert.Single(Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(
            metadata["sourceRequirements"]));
        var optionalCostChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(
                sourceRequirement["optionalCostChoices"])
            .ToArray();

        Assert.DoesNotContain(optionalCostChoices, choice =>
            string.Equals(choice.Id, "BRUSH_USE_REPLACED_BATTLEFIELD:P2-BATTLEFIELD-ENERGY-HUB", StringComparison.Ordinal));
    }

    [Fact]
    public async Task P79BattlefieldHeldPaysTypedPowerToGainScoreDoesNotTriggerOnDefensiveVictory()
    {
        var state = BattlefieldHeldScoreState() with
        {
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = RunePool.Empty,
                ["P2"] = new(0, 0, new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    ["red"] = 4
                })
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-battlefield-held-score-typed", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "P2-BATTLEFIELD-ENERGY-HUB",
                ["P1-BATTLEFIELD-ENERGY-ATTACKER"],
                ["P2-BATTLEFIELD-ENERGY-DEFENDER"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.DoesNotContain(result.Events, e => e.Kind == "BATTLEFIELD_HELD");
        Assert.DoesNotContain(result.State.TriggerQueue, t => t.HeldContext is not null);
        Assert.DoesNotContain(result.State.StackItems, t => t.HeldContext is not null);
        Assert.Empty(result.State.DelayedResourceGains);
    }

    [Fact]
    public async Task P79BattlefieldHeldPaysPowerToGainScoreWithRecycleRunePaymentResourceDoesNotTriggerOnDefensiveVictory()
    {
        const string runeObjectId = "P2-RUNE-RED-HELD-SCORE";
        var paymentResourceAction = $"RECYCLE_RUNE:{runeObjectId}";
        var baseState = BattlefieldHeldScoreState();
        var state = baseState with
        {
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = RunePool.Empty,
                ["P2"] = new(0, 3)
            },
            PlayerZones = new Dictionary<string, PlayerZones>(baseState.PlayerZones, StringComparer.Ordinal)
            {
                ["P2"] = baseState.PlayerZones["P2"] with
                {
                    Base = [runeObjectId],
                    RuneDeck = ["P2-RUNE-BOTTOM-HELD-SCORE"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(baseState.CardObjects, StringComparer.Ordinal)
            {
                [runeObjectId] = new(
                    runeObjectId,
                    isExhausted: true,
                    tags: [CardObjectTags.RuneCard, "COLOR:red"],
                    cardNo: "UNL-R01",
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P2-RUNE-BOTTOM-HELD-SCORE"] = new(
                    "P2-RUNE-BOTTOM-HELD-SCORE",
                    isExhausted: true,
                    tags: [CardObjectTags.RuneCard, "COLOR:blue"],
                    cardNo: "UNL-R02",
                    ownerId: "P2",
                    controllerId: "P2")
            },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(baseState.ObjectLocations, StringComparer.Ordinal)
            {
                [runeObjectId] = new("P2", "BASE"),
                ["P2-RUNE-BOTTOM-HELD-SCORE"] = new("P2", "RUNE_DECK")
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-battlefield-held-score-recycle", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "P2-BATTLEFIELD-ENERGY-HUB",
                ["P1-BATTLEFIELD-ENERGY-ATTACKER"],
                ["P2-BATTLEFIELD-ENERGY-DEFENDER"],
                ["COMBAT_ASSIGNMENT", paymentResourceAction]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.DoesNotContain(result.Events, e => e.Kind == "BATTLEFIELD_HELD");
        Assert.DoesNotContain(result.State.TriggerQueue, t => t.HeldContext is not null);
        Assert.DoesNotContain(result.State.StackItems, t => t.HeldContext is not null);
        Assert.Empty(result.State.DelayedResourceGains);
    }

    [Fact]
    public void BattlefieldHeldScorePromptCountsSigilPowerInOrdinaryRunePool()
    {
        var state = BattlefieldHeldScoreState() with
        {
            RunePools = new Dictionary<string, RunePool>
            {
                ["P1"] = RunePool.Empty,
                ["P2"] = new(0, 3, new Dictionary<string, int> { [RuneTrait.Red] = 1 })
            }
        };
        var initialHash = MatchStateHasher.Hash(state);
        var candidate = Assert.Single(ResolutionResult.BuildPrompts(state)["P1"].Candidates!,
            c => c.Action == CommandTypes.DeclareBattle);
        var metadata = Assert.IsType<Dictionary<string, object?>>(candidate.Metadata);
        var source = Assert.Single(Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(metadata["sourceRequirements"]));
        Assert.Equal(4, source["availablePower"]);
        Assert.Equal(4, source["availablePowerWithPaymentResources"]);
        Assert.DoesNotContain(Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(source["paymentResourceChoices"]),
            c => c.Id.StartsWith("TEMPORARY_RESOURCE", StringComparison.Ordinal));
        Assert.Equal(initialHash, MatchStateHasher.Hash(state));
    }

    [Theory]
    [InlineData("unnecessary")]
    [InlineData("wrong-player")]
    [InlineData("missing-card-no")]
    [InlineData("duplicate")]
    public async Task P79BattlefieldHeldRejectsInvalidRecycleRunePaymentResourceWithoutMutation(string caseName)
    {
        const string validRuneObjectId = "P2-RUNE-RED-HELD-SCORE-INVALID";
        const string wrongPlayerRuneObjectId = "P1-RUNE-RED-HELD-SCORE-INVALID";
        var runeObjectId = string.Equals(caseName, "wrong-player", StringComparison.Ordinal)
            ? wrongPlayerRuneObjectId
            : validRuneObjectId;
        var paymentResourceAction = $"RECYCLE_RUNE:{runeObjectId}";
        var baseState = BattlefieldHeldScoreState();
        var p2Pool = string.Equals(caseName, "unnecessary", StringComparison.Ordinal)
            ? new RunePool(0, 4)
            : new RunePool(0, 3);
        var optionalCosts = string.Equals(caseName, "duplicate", StringComparison.Ordinal)
            ? new[] { "COMBAT_ASSIGNMENT", paymentResourceAction, paymentResourceAction }
            : ["COMBAT_ASSIGNMENT", paymentResourceAction];
        var state = baseState with
        {
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = RunePool.Empty,
                ["P2"] = p2Pool
            },
            PlayerZones = new Dictionary<string, PlayerZones>(baseState.PlayerZones, StringComparer.Ordinal)
            {
                ["P1"] = baseState.PlayerZones["P1"] with
                {
                    Base = [wrongPlayerRuneObjectId]
                },
                ["P2"] = baseState.PlayerZones["P2"] with
                {
                    Base = [validRuneObjectId],
                    RuneDeck = ["P2-RUNE-BOTTOM-HELD-SCORE-INVALID"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(baseState.CardObjects, StringComparer.Ordinal)
            {
                [validRuneObjectId] = new(
                    validRuneObjectId,
                    isExhausted: true,
                    tags: [CardObjectTags.RuneCard, "COLOR:red"],
                    cardNo: string.Equals(caseName, "missing-card-no", StringComparison.Ordinal) ? null : "UNL-R01",
                    ownerId: "P2",
                    controllerId: "P2"),
                [wrongPlayerRuneObjectId] = new(
                    wrongPlayerRuneObjectId,
                    isExhausted: true,
                    tags: [CardObjectTags.RuneCard, "COLOR:red"],
                    cardNo: "UNL-R01",
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-RUNE-BOTTOM-HELD-SCORE-INVALID"] = new(
                    "P2-RUNE-BOTTOM-HELD-SCORE-INVALID",
                    isExhausted: true,
                    tags: [CardObjectTags.RuneCard, "COLOR:blue"],
                    cardNo: "UNL-R02",
                    ownerId: "P2",
                    controllerId: "P2")
            },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(baseState.ObjectLocations, StringComparer.Ordinal)
            {
                [validRuneObjectId] = new("P2", "BASE"),
                [wrongPlayerRuneObjectId] = new("P1", "BASE"),
                ["P2-RUNE-BOTTOM-HELD-SCORE-INVALID"] = new("P2", "RUNE_DECK")
            }
        };
        var initialHash = MatchStateHasher.Hash(state);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent($"intent-p7-9-battlefield-held-score-invalid-recycle-{caseName}", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "P2-BATTLEFIELD-ENERGY-HUB",
                ["P1-BATTLEFIELD-ENERGY-ATTACKER"],
                ["P2-BATTLEFIELD-ENERGY-DEFENDER"],
                optionalCosts),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Empty(result.Events);
        Assert.Equal(initialHash, MatchStateHasher.Hash(result.State));
        Assert.DoesNotContain(result.Events, gameEvent => string.Equals(gameEvent.Kind, "RUNE_RECYCLED", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
    }

    [Fact]
    public async Task P79BattlefieldHeldScoreCanOnlyScoreSameBattlefieldOncePerTurnDoesNotTriggerOnDefensiveVictory()
    {
        var state = BattlefieldHeldScoreState() with
        {
            UntilEndOfTurnEffects =
            [
                BattlefieldTaskMarkers.ScoreGainedThisTurn("P2-BATTLEFIELD-ENERGY-HUB", "P2")
            ]
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-battlefield-held-score-once", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "P2-BATTLEFIELD-ENERGY-HUB",
                ["P1-BATTLEFIELD-ENERGY-ATTACKER"],
                ["P2-BATTLEFIELD-ENERGY-DEFENDER"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.DoesNotContain(result.Events, e => e.Kind == "BATTLEFIELD_HELD");
        Assert.DoesNotContain(result.State.TriggerQueue, t => t.HeldContext is not null);
        Assert.DoesNotContain(result.State.StackItems, t => t.HeldContext is not null);
        Assert.Empty(result.State.DelayedResourceGains);
    }

    [Fact]
    public async Task P79BattlefieldScoreDelayDoesNotPreventOtherBattlefieldHeldScorePaymentDoesNotTriggerOnDefensiveVictory()
    {
        var baseState = BattlefieldHeldScoreState();
        var state = baseState with
        {
            TurnNumber = 1,
            PlayerZones = new Dictionary<string, PlayerZones>(baseState.PlayerZones, StringComparer.Ordinal)
            {
                ["P2"] = baseState.PlayerZones["P2"] with
                {
                    Battlefields =
                    [
                        "P2-BATTLEFIELD-ENERGY-HUB",
                        "P2-BATTLEFIELD-ENERGY-DEFENDER",
                        "P2-BATTLEFIELD-FORGOTTEN-MONUMENT"
                    ]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(baseState.CardObjects, StringComparer.Ordinal)
            {
                ["P2-BATTLEFIELD-FORGOTTEN-MONUMENT"] = new(
                    "P2-BATTLEFIELD-FORGOTTEN-MONUMENT",
                    cardNo: "SFD·209/221",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: "P2",
                    controllerId: "P2")
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-battlefield-score-delay-held", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "P2-BATTLEFIELD-ENERGY-HUB",
                ["P1-BATTLEFIELD-ENERGY-ATTACKER"],
                ["P2-BATTLEFIELD-ENERGY-DEFENDER"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.DoesNotContain(result.Events, e => e.Kind == "BATTLEFIELD_HELD");
        Assert.DoesNotContain(result.State.TriggerQueue, t => t.HeldContext is not null);
        Assert.DoesNotContain(result.State.StackItems, t => t.HeldContext is not null);
        Assert.Empty(result.State.DelayedResourceGains);
    }

    [Fact]
    public async Task P79BattlefieldHeldGrantsNextSpellEchoDoesNotTriggerOnDefensiveVictory()
    {
        var state = BattlefieldHeldNextSpellEchoState();

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-battlefield-held-next-spell-echo", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "P2-BATTLEFIELD-PILTOVER-ACADEMY",
                ["P1-BATTLEFIELD-PILTOVER-ATTACKER"],
                ["P2-BATTLEFIELD-PILTOVER-DEFENDER"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.DoesNotContain(result.Events, e => e.Kind == "BATTLEFIELD_HELD");
        Assert.DoesNotContain(result.State.TriggerQueue, t => t.HeldContext is not null);
        Assert.DoesNotContain(result.State.StackItems, t => t.HeldContext is not null);
        Assert.Empty(result.State.DelayedResourceGains);
    }

    [Fact]
    public async Task P79BattlefieldHeldActivateConquestEffectsCreatesGoldAndDrawsDoesNotTriggerOnDefensiveVictory()
    {
        var state = BattlefieldHeldActivateConquestState();

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-battlefield-held-activate-conquest", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "P2-BATTLEFIELD-RECKONER-ARENA",
                ["P1-BATTLEFIELD-RECKONER-ATTACKER"],
                ["P2-BATTLEFIELD-BAD-PORO"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.DoesNotContain(result.Events, e => e.Kind == "BATTLEFIELD_HELD");
        Assert.DoesNotContain(result.State.TriggerQueue, t => t.HeldContext is not null);
        Assert.DoesNotContain(result.State.StackItems, t => t.HeldContext is not null);
        Assert.Empty(result.State.DelayedResourceGains);
    }

    [Fact]
    public async Task P79BattlefieldHeldActivateConquestEffectsSkipsOpponentOwnedUnitsDoesNotTriggerOnDefensiveVictory()
    {
        var baseState = BattlefieldHeldActivateConquestState();
        var cardObjects = baseState.CardObjects.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        cardObjects["P2-BATTLEFIELD-KAISA"] = cardObjects["P2-BATTLEFIELD-KAISA"] with
        {
            OwnerId = "P1",
            ControllerId = ""
        };
        var state = baseState with
        {
            CardObjects = cardObjects
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-battlefield-held-activate-conquest-dirty-unit", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "P2-BATTLEFIELD-RECKONER-ARENA",
                ["P1-BATTLEFIELD-RECKONER-ATTACKER"],
                ["P2-BATTLEFIELD-BAD-PORO"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.DoesNotContain(result.Events, e => e.Kind == "BATTLEFIELD_HELD");
        Assert.DoesNotContain(result.State.TriggerQueue, t => t.HeldContext is not null);
        Assert.DoesNotContain(result.State.StackItems, t => t.HeldContext is not null);
        Assert.Empty(result.State.DelayedResourceGains);
    }

    [Fact]
    public async Task P79BattlefieldHeldActivateConquestEffectsSkipsUnitsAtOtherBattlefieldsDoesNotTriggerOnDefensiveVictory()
    {
        var baseState = BattlefieldHeldActivateConquestState();
        var objectLocations = baseState.ObjectLocations.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        objectLocations["P1-BATTLEFIELD-RECKONER-ATTACKER"] = new ObjectLocationState("P1", "BATTLEFIELD", "P2-BATTLEFIELD-RECKONER-ARENA");
        objectLocations["P2-BATTLEFIELD-RECKONER-ARENA"] = new ObjectLocationState("P2", "BATTLEFIELD");
        objectLocations["P2-BATTLEFIELD-BAD-PORO"] = new ObjectLocationState("P2", "BATTLEFIELD", "P2-BATTLEFIELD-RECKONER-ARENA");
        objectLocations["P2-BATTLEFIELD-KAISA"] = new ObjectLocationState("P2", "BATTLEFIELD", "P2-BATTLEFIELD-RECKONER-OFFSITE");
        var state = baseState with
        {
            ObjectLocations = objectLocations,
            UntilEndOfTurnEffects =
            [
                ..baseState.UntilEndOfTurnEffects,
                BattlefieldTaskMarkers.BattleSkipped("P2-BATTLEFIELD-RECKONER-ARENA")
            ]
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-battlefield-held-activate-conquest-offsite-unit", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "P2-BATTLEFIELD-RECKONER-ARENA",
                ["P1-BATTLEFIELD-RECKONER-ATTACKER"],
                ["P2-BATTLEFIELD-BAD-PORO"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.DoesNotContain(result.Events, e => e.Kind == "BATTLEFIELD_HELD");
        Assert.DoesNotContain(result.State.TriggerQueue, t => t.HeldContext is not null);
        Assert.DoesNotContain(result.State.StackItems, t => t.HeldContext is not null);
        Assert.Empty(result.State.DelayedResourceGains);
    }

    [Fact]
    public async Task P79BattlefieldHeldActivateConquestEffectsQiyanaDrawsWhenMainDeckAvailableDoesNotTriggerOnDefensiveVictory()
    {
        var state = BattlefieldHeldActivateQiyanaConquestState(hasMainDeck: true);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-battlefield-held-qiyana-draw", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "P2-BATTLEFIELD-RECKONER-ARENA",
                ["P1-BATTLEFIELD-RECKONER-ATTACKER"],
                ["P2-BATTLEFIELD-QIYANA"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.DoesNotContain(result.Events, e => e.Kind == "BATTLEFIELD_HELD");
        Assert.DoesNotContain(result.State.TriggerQueue, t => t.HeldContext is not null);
        Assert.DoesNotContain(result.State.StackItems, t => t.HeldContext is not null);
        Assert.Empty(result.State.DelayedResourceGains);
    }

    [Fact]
    public async Task P79BattlefieldHeldActivateConquestEffectsQiyanaCallsRuneWhenMainDeckEmptyDoesNotTriggerOnDefensiveVictory()
    {
        var state = BattlefieldHeldActivateQiyanaConquestState(hasMainDeck: false);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-battlefield-held-qiyana-call-rune", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "P2-BATTLEFIELD-RECKONER-ARENA",
                ["P1-BATTLEFIELD-RECKONER-ATTACKER"],
                ["P2-BATTLEFIELD-QIYANA"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.DoesNotContain(result.Events, e => e.Kind == "BATTLEFIELD_HELD");
        Assert.DoesNotContain(result.State.TriggerQueue, t => t.HeldContext is not null);
        Assert.DoesNotContain(result.State.StackItems, t => t.HeldContext is not null);
        Assert.Empty(result.State.DelayedResourceGains);
    }

    [Fact]
    public async Task P79BattlefieldHeldActivateConquestEffectsReadiesLucianAndGrantsSettBoonDoesNotTriggerOnDefensiveVictory()
    {
        var state = BattlefieldHeldActivateConquestReadinessState();

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-battlefield-held-activate-conquest-ready-boon", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "P2-BATTLEFIELD-RECKONER-ARENA",
                ["P1-BATTLEFIELD-RECKONER-ATTACKER"],
                ["P2-BATTLEFIELD-READY-DEFENDER"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.DoesNotContain(result.Events, e => e.Kind == "BATTLEFIELD_HELD");
        Assert.DoesNotContain(result.State.TriggerQueue, t => t.HeldContext is not null);
        Assert.DoesNotContain(result.State.StackItems, t => t.HeldContext is not null);
        Assert.Empty(result.State.DelayedResourceGains);
    }

    [Fact]
    public async Task P79BattlefieldHeldActivateConquestEffectsCrimsonSignetTreantGrantsFriendlyBoonDoesNotTriggerOnDefensiveVictory()
    {
        var state = BattlefieldHeldActivateConquestFriendlyBoonState();

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-battlefield-held-friendly-boon", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "P2-BATTLEFIELD-RECKONER-ARENA",
                ["P1-BATTLEFIELD-RECKONER-ATTACKER"],
                ["P2-BATTLEFIELD-BOON-DEFENDER"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.DoesNotContain(result.Events, e => e.Kind == "BATTLEFIELD_HELD");
        Assert.DoesNotContain(result.State.TriggerQueue, t => t.HeldContext is not null);
        Assert.DoesNotContain(result.State.StackItems, t => t.HeldContext is not null);
        Assert.Empty(result.State.DelayedResourceGains);
    }

    [Fact]
    public async Task P79BattlefieldHeldActivateConquestEffectsSkyvoiceWyrmlingGrantsFriendlyPowerDoesNotTriggerOnDefensiveVictory()
    {
        var state = BattlefieldHeldActivateConquestFriendlyPowerState();

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-battlefield-held-friendly-power", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "P2-BATTLEFIELD-RECKONER-ARENA",
                ["P1-BATTLEFIELD-RECKONER-ATTACKER"],
                ["P2-BATTLEFIELD-POWER-DEFENDER"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.DoesNotContain(result.Events, e => e.Kind == "BATTLEFIELD_HELD");
        Assert.DoesNotContain(result.State.TriggerQueue, t => t.HeldContext is not null);
        Assert.DoesNotContain(result.State.StackItems, t => t.HeldContext is not null);
        Assert.Empty(result.State.DelayedResourceGains);
    }

    [Fact]
    public async Task P79BattlefieldHeldActivateConquestEffectsAdaptiveRobotDestroysEquipmentAndGrantsSelfBoonDoesNotTriggerOnDefensiveVictory()
    {
        var state = BattlefieldHeldActivateConquestAdaptiveRobotState(hasEquipment: true);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-battlefield-held-adaptive-robot", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "P2-BATTLEFIELD-RECKONER-ARENA",
                ["P1-BATTLEFIELD-RECKONER-ATTACKER"],
                ["P2-BATTLEFIELD-ADAPTIVE-ROBOT"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.DoesNotContain(result.Events, e => e.Kind == "BATTLEFIELD_HELD");
        Assert.DoesNotContain(result.State.TriggerQueue, t => t.HeldContext is not null);
        Assert.DoesNotContain(result.State.StackItems, t => t.HeldContext is not null);
        Assert.Empty(result.State.DelayedResourceGains);
    }

    [Fact]
    public async Task P79BattlefieldHeldActivateConquestEffectsAdaptiveRobotSkipsBoonWhenNoEquipment()
    {
        var state = BattlefieldHeldActivateConquestAdaptiveRobotState(hasEquipment: false);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-battlefield-held-adaptive-robot-no-equipment", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "P2-BATTLEFIELD-RECKONER-ARENA",
                ["P1-BATTLEFIELD-RECKONER-ATTACKER"],
                ["P2-BATTLEFIELD-ADAPTIVE-ROBOT"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        var adaptiveRobot = result.State.CardObjects["P2-BATTLEFIELD-ADAPTIVE-ROBOT"];
        Assert.Equal(3, adaptiveRobot.Power);
        Assert.DoesNotContain(CardObjectTags.Boon, adaptiveRobot.Tags);
        Assert.DoesNotContain(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_CONQUEST_EFFECT_ACTIVATED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["effectId"] as string, "UNIT_CONQUEST_DESTROY_EQUIPMENT_GRANT_SELF_BOON", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Events, gameEvent => string.Equals(gameEvent.Kind, "BOON_GRANTED", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Events, gameEvent => string.Equals(gameEvent.Kind, "EQUIPMENT_DESTROYED", StringComparison.Ordinal));
    }

    [Fact]
    public async Task P79BattlefieldHeldNextSpellEchoRepeatsSpellAndConsumesMarker()
    {
        var state = PunishmentState(mana: 0) with
        {
            TurnNumber = 2,
            ActivePlayerId = "P2",
            TurnPlayerId = "P2",
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = RunePool.Empty,
                ["P2"] = new(4, 0, new Dictionary<string, int> { ["red"] = 2 })
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["P1-BATTLEFIELD-PUNISHMENT-TARGET"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Hand = ["P2-SPELL-PUNISHMENT"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-PUNISHMENT-TARGET"] = new(
                    "P1-BATTLEFIELD-PUNISHMENT-TARGET",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-SPELL-PUNISHMENT"] = new(
                    "P2-SPELL-PUNISHMENT",
                    cardNo: "UNL-007/219",
                    ownerId: "P2",
                    controllerId: "P2")
            },
            UntilEndOfTurnEffects = ["BATTLEFIELD_HELD_NEXT_SPELL_GAINS_ECHO:P2"]
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-battlefield-held-next-spell-echo-play", "P2", "PLAY_CARD"),
            new PlayCardCommand(
                "P2-SPELL-PUNISHMENT",
                "UNL-007/219",
                ["P1-BATTLEFIELD-PUNISHMENT-TARGET"],
                OptionalCosts: ["ECHO"]),
            CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Equal(new RunePool(0, 0), result.State.RunePools["P2"]);
        Assert.DoesNotContain("BATTLEFIELD_HELD_NEXT_SPELL_GAINS_ECHO:P2", result.State.UntilEndOfTurnEffects);
        var stackItem = Assert.Single(result.State.StackItems);
        Assert.Equal(2, stackItem.EffectRepeatCount);
        Assert.Equal(["ECHO"], stackItem.OptionalCosts);
        Assert.Contains(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal)
            && Equals(gameEvent.Payload["mana"], 4)
            && Equals(gameEvent.Payload["power"], 2)
            && Equals(gameEvent.Payload["baseMana"], 2));
        var triggerEvent = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "BATTLEFIELD_HELD_NEXT_SPELL_GAINS_ECHO", StringComparison.Ordinal));
        Assert.True(Assert.IsType<bool>(triggerEvent.Payload["echoPaid"]));
        Assert.Equal(2, triggerEvent.Payload["effectRepeatCount"]);
    }

    [Fact]
    public async Task P79BattlefieldBattleDestroyedUnitPaysThreeAndRecalls()
    {
        var state = BattlefieldBattleDestroyedRecallState(mana: 3);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-battlefield-battle-destroyed-recall", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "P2-BATTLEFIELD-BLOOD-ALTAR",
                ["P1-BATTLEFIELD-BLOOD-ATTACKER"],
                ["P2-BATTLEFIELD-BLOOD-DEFENDER"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.NotNull(result.State.PendingRuleChoice);
        result = await DestructionOrderTests.Select(result.State, "ALTAR:P2-BATTLEFIELD-BLOOD-DEFENDER:P2-BATTLEFIELD-BLOOD-ALTAR");
        Assert.True(result.Accepted);
        Assert.Equal(new RunePool(0, 0), result.State.RunePools["P2"]);
        Assert.DoesNotContain(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_DESTROYED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, "P2-BATTLEFIELD-BLOOD-DEFENDER", StringComparison.Ordinal));
        Assert.Contains(result.Events, gameEvent =>
            gameEvent.Kind == "UNIT_RECALLED_TO_BASE"
            && gameEvent.Payload.GetValueOrDefault("sourceObjectId") as string == "P2-BATTLEFIELD-BLOOD-ALTAR");
        Assert.Contains(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["reason"] as string, "BATTLEFIELD_DESTROYED_IN_BATTLE_PAY_3_RECALL", StringComparison.Ordinal)
            && Equals(gameEvent.Payload["mana"], 3));
        Assert.Contains(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_RECALLED_TO_BASE", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["replacementEffectId"] as string, "BATTLEFIELD_DESTROYED_IN_BATTLE_PAY_3_RECALL", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, "P2-BATTLEFIELD-BLOOD-DEFENDER", StringComparison.Ordinal));
        Assert.Contains("P2-BATTLEFIELD-BLOOD-DEFENDER", result.State.PlayerZones["P2"].Base);
        Assert.DoesNotContain("P2-BATTLEFIELD-BLOOD-DEFENDER", result.State.PlayerZones["P2"].Battlefields);
        Assert.Empty(result.State.PlayerZones["P2"].Graveyard);
        var savedDefender = result.State.CardObjects["P2-BATTLEFIELD-BLOOD-DEFENDER"];
        Assert.Equal(0, savedDefender.Damage);
        Assert.True(savedDefender.IsExhausted);
        Assert.False(savedDefender.IsDefending);
    }

    [Fact]
    public async Task P79BattlefieldBattleDestroyedUnitSkipsOpponentOwnedAltar()
    {
        var state = BattlefieldBattleDestroyedRecallState(mana: 3);
        var cardObjects = state.CardObjects.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        cardObjects["P2-BATTLEFIELD-BLOOD-ALTAR"] = cardObjects["P2-BATTLEFIELD-BLOOD-ALTAR"] with
        {
            OwnerId = "P1",
            ControllerId = ""
        };
        state = state with { CardObjects = cardObjects };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-battlefield-battle-destroyed-recall-dirty-source", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "P2-BATTLEFIELD-BLOOD-ALTAR",
                ["P1-BATTLEFIELD-BLOOD-ATTACKER"],
                ["P2-BATTLEFIELD-BLOOD-DEFENDER"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Equal(new RunePool(3, 0), result.State.RunePools["P2"]);
        Assert.DoesNotContain(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "BATTLEFIELD_DESTROYED_IN_BATTLE_PAY_3_RECALL", StringComparison.Ordinal));
        Assert.Contains(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_DESTROYED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, "P2-BATTLEFIELD-BLOOD-DEFENDER", StringComparison.Ordinal));
        Assert.Contains("P2-BATTLEFIELD-BLOOD-DEFENDER", result.State.PlayerZones["P2"].Graveyard);
        Assert.DoesNotContain("P2-BATTLEFIELD-BLOOD-DEFENDER", result.State.PlayerZones["P2"].Base);
    }

    [Fact]
    public async Task P79BattlefieldBattleDestroyedUnitFallsBackToDestroyWhenNoMana()
    {
        var state = BattlefieldBattleDestroyedRecallState(mana: 2);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-battlefield-battle-destroyed-recall-no-mana", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "P2-BATTLEFIELD-BLOOD-ALTAR",
                ["P1-BATTLEFIELD-BLOOD-ATTACKER"],
                ["P2-BATTLEFIELD-BLOOD-DEFENDER"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Equal(new RunePool(2, 0), result.State.RunePools["P2"]);
        Assert.DoesNotContain(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "BATTLEFIELD_DESTROYED_IN_BATTLE_PAY_3_RECALL", StringComparison.Ordinal));
        Assert.Contains(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_DESTROYED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, "P2-BATTLEFIELD-BLOOD-DEFENDER", StringComparison.Ordinal));
        Assert.Contains("P2-BATTLEFIELD-BLOOD-DEFENDER", result.State.PlayerZones["P2"].Graveyard);
    }

    [Fact]
    public async Task P79BattlefieldHeldReturnsHeroFromGraveyardToChampionZoneDoesNotTriggerOnDefensiveVictory()
    {
        var state = BattlefieldHeldReturnHeroState();

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-battlefield-held-return-hero", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "P2-BATTLEFIELD-HALLOWED-TOMB",
                ["P1-BATTLEFIELD-TOMB-ATTACKER"],
                ["P2-BATTLEFIELD-TOMB-DEFENDER"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.DoesNotContain(result.Events, e => e.Kind == "BATTLEFIELD_HELD");
        Assert.DoesNotContain(result.State.TriggerQueue, t => t.HeldContext is not null);
        Assert.DoesNotContain(result.State.StackItems, t => t.HeldContext is not null);
        Assert.Empty(result.State.DelayedResourceGains);
    }

    [Fact]
    public async Task P79BattlefieldHeldReturnHeroSkipsOpponentOwnedTomb()
    {
        var state = BattlefieldHeldReturnHeroState();
        var cardObjects = state.CardObjects.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        cardObjects["P2-BATTLEFIELD-HALLOWED-TOMB"] = cardObjects["P2-BATTLEFIELD-HALLOWED-TOMB"] with
        {
            OwnerId = "P1",
            ControllerId = ""
        };
        state = state with { CardObjects = cardObjects };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-battlefield-held-return-hero-dirty-tomb", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "P2-BATTLEFIELD-HALLOWED-TOMB",
                ["P1-BATTLEFIELD-TOMB-ATTACKER"],
                ["P2-BATTLEFIELD-TOMB-DEFENDER"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.DoesNotContain(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "BATTLEFIELD_HELD_RETURN_HERO_FROM_GRAVEYARD", StringComparison.Ordinal));
        Assert.Equal(["P2-HERO-TOMB-RETURN"], result.State.PlayerZones["P2"].Graveyard);
        Assert.Empty(result.State.PlayerZones["P2"].ChampionZone);
    }

    [Theory]
    [InlineData("OGN·293/298")]
    [InlineData("OGN·293a/298")]
    public async Task P79BattlefieldHeldSevenUnitsWinsGameDoesNotTriggerOnDefensiveVictory(string battlefieldCardNo)
    {
        var state = BattlefieldHeldSevenUnitsWinState(battlefieldCardNo);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-battlefield-held-seven-units-win", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "P2-BATTLEFIELD-GRAND-PLAZA",
                ["P1-BATTLEFIELD-GRAND-ATTACKER"],
                ["P2-BATTLEFIELD-GRAND-UNIT-001"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.DoesNotContain(result.Events, e => e.Kind == "BATTLEFIELD_HELD");
        Assert.DoesNotContain(result.State.TriggerQueue, t => t.HeldContext is not null);
        Assert.DoesNotContain(result.State.StackItems, t => t.HeldContext is not null);
        Assert.Empty(result.State.DelayedResourceGains);
    }

    [Fact]
    public async Task P79BattlefieldHeldSevenUnitsWinCountsOnlyUnitsAtThatBattlefield()
    {
        var state = BattlefieldHeldSevenUnitsWinSplitCountState();

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-battlefield-held-seven-units-win-split-count", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "P2-BATTLEFIELD-GRAND-PLAZA",
                ["P1-BATTLEFIELD-GRAND-ATTACKER"],
                ["P2-BATTLEFIELD-GRAND-UNIT-001"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Null(result.State.WinnerPlayerId);
        Assert.Equal(MatchStatuses.InProgress, result.State.Status);
        Assert.DoesNotContain(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "BATTLEFIELD_HELD_SEVEN_UNITS_WIN", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Events, gameEvent => string.Equals(gameEvent.Kind, "MATCH_WON", StringComparison.Ordinal));
    }

    [Fact]
    public async Task P79BattlefieldConquerRevealRecyclesTopTwo()
    {
        var state = BattlefieldConquerRevealRecycleState();

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-battlefield-conquer-reveal-recycle", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "P1-BATTLEFIELD-CANDLELIT-SANCTUM",
                ["P1-BATTLEFIELD-CANDLE-ATTACKER"],
                ["P2-BATTLEFIELD-CANDLE-DEFENDER"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Contains(result.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLEFIELD_CONQUERED", StringComparison.Ordinal));
        Assert.Contains(result.Events,e=>e.Kind=="TRIGGER_QUEUED" && Equals(e.Payload["effectKind"],"INSIGHT_TRIGGER"));
        Assert.DoesNotContain(result.Events,e=>e.Kind is "CARDS_REVEALED" or "CARDS_RECYCLED");
        var opened=await SpellResolutionTestDriver.Top(result);
        var choice=Assert.IsType<PendingCardChoiceState>(opened.State.PendingCardChoice);
        Assert.Equal(0,choice.RequiredCount);Assert.Equal(2,choice.MaxCount);
        Assert.Equal(["P1-BATTLEFIELD-CANDLE-001","P1-BATTLEFIELD-CANDLE-002"],choice.LegalObjectIds);
        Assert.DoesNotContain("P1-BATTLEFIELD-CANDLE-001",JsonSerializer.Serialize(opened.Prompts["P2"]));
        var chosen=await OfficialInsightAndSpellLockTests.Act(opened.State,"P1",new ChooseCardsCommand(choice.ChoiceId,choice.ChoiceWindow,choice.LegalObjectIds));
        result=chosen with {Events=opened.Events.Concat(chosen.Events).ToArray()};
        Assert.DoesNotContain(result.Events,e=>e.Kind=="CARDS_REVEALED");
        var recycleEvent=Assert.Single(result.Events,e=>e.Kind=="CARDS_RECYCLED");
        Assert.Equal(2,recycleEvent.Payload["count"]);Assert.False(recycleEvent.Payload.ContainsKey("cardIds"));
        Assert.Equal("P1-BATTLEFIELD-CANDLE-003", result.State.PlayerZones["P1"].MainDeck[0]);
        Assert.Equal(
            ["P1-BATTLEFIELD-CANDLE-001", "P1-BATTLEFIELD-CANDLE-002"],
            result.State.PlayerZones["P1"].MainDeck.Skip(1).Order(StringComparer.Ordinal).ToArray());
    }

    [Fact]
    public async Task P79LegendTriggerLuxDrawsWhenControllerPlaysHighCostSpell()
    {
        var state = PunishmentState(mana: 6) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-EVOLUTION-DAY"],
                    MainDeck = ["P1-LUX-DRAW-001"],
                    LegendZone = ["P1-LEGEND-LUX"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-LEGEND-LUX"] = new(
                    "P1-LEGEND-LUX",
                    cardNo: "OGS·021/024",
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-LUX-DRAW-001"] = new(
                    "P1-LUX-DRAW-001",
                    cardNo: "SFD·125/221",
                    ownerId: "P1",
                    controllerId: "P1",
                    power: 3,
                    tags: [CardObjectTags.UnitCard])
            }
        };
        var deck=Enumerable.Range(1,5).Select(i=>"LUX-DRAW-"+i).ToArray();
        var cards=new Dictionary<string,CardObjectState>(state.CardObjects);
        foreach(var id in deck)cards[id]=new(id,cardNo:"SFD·125/221",ownerId:"P1",controllerId:"P1");
        state=state with {CardObjects=cards,PlayerZones=new Dictionary<string,PlayerZones>(state.PlayerZones){["P1"]=state.PlayerZones["P1"] with {MainDeck=deck}}};
        state = PrintedCostFixture.Add(state, "P1", "OGN·114/298");

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-lux-high-cost-spell", "P1", "PLAY_CARD"),
            new PlayCardCommand("P1-SPELL-EVOLUTION-DAY", "OGN·114/298", []),
            CancellationToken.None);

        Assert.True(result.Accepted);
        result = await SpellResolutionTestDriver.Finish(result);
        Assert.Equal(0, result.State.RunePools["P1"].Mana);
        Assert.Empty(result.State.PlayerZones["P1"].MainDeck);
        Assert.Equal(deck, result.State.PlayerZones["P1"].Hand);
        Assert.Empty(result.State.StackItems);
        Assert.Contains(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "LEGEND_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, TriggerKinds.LegendHighCostSpellDrawOne, StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["playedCardNo"] as string, "OGN·114/298", StringComparison.Ordinal));
        Assert.Contains(result.Events, gameEvent => string.Equals(gameEvent.Kind, "CARD_DRAWN", StringComparison.Ordinal));
        Assert.Null(result.State.WinnerPlayerId);
    }

    [Fact]
    public async Task P79LegendStaticMasterYiLevelGrantsFriendlyUnitPowerAtSixExperience()
    {
        var state = PunishmentState(mana: 0) with
        {
            PlayerExperience = new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 6,
                ["P2"] = 0
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["P1-BATTLEFIELD-ATTACKER"],
                    LegendZone = ["P1-LEGEND-MASTER-YI-LEVEL"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-BATTLEFIELD-DEFENDER"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-ATTACKER"] = new(
                    "P1-BATTLEFIELD-ATTACKER",
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-LEGEND-MASTER-YI-LEVEL"] = new(
                    "P1-LEGEND-MASTER-YI-LEVEL",
                    cardNo: "UNL-191/219",
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-BATTLEFIELD-DEFENDER"] = new(
                    "P2-BATTLEFIELD-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2")
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-master-yi-level-static-battle", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "BATTLEFIELD:P1-MAIN",
                ["P1-BATTLEFIELD-ATTACKER"],
                ["P2-BATTLEFIELD-DEFENDER"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted);
        var attackerDamageEvent = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "DAMAGE_APPLIED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["combatRole"] as string, "ATTACKER", StringComparison.Ordinal));
        Assert.Equal("P1-BATTLEFIELD-ATTACKER", attackerDamageEvent.Payload["sourceObjectId"]);
        Assert.Equal(2, attackerDamageEvent.Payload["basePower"]);
        Assert.Equal(0, attackerDamageEvent.Payload["keywordBonus"]);
        Assert.Equal(1, attackerDamageEvent.Payload["staticPowerBonus"]);
        Assert.Equal(3, attackerDamageEvent.Payload["combatPower"]);
        Assert.Equal(3, attackerDamageEvent.Payload["damage"]);
    }

    [Fact]
    public async Task P79LegendStaticMasterYiLevelElevenReadiesUnitOnEntry()
    {
        var state = PunishmentState(mana: 4) with
        {
            PlayerExperience = new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 11,
                ["P2"] = 0
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-MIGHTY-FAERIE"],
                    LegendZone = ["P1-LEGEND-MASTER-YI-LEVEL"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-LEGEND-MASTER-YI-LEVEL"] = new(
                    "P1-LEGEND-MASTER-YI-LEVEL",
                    cardNo: "UNL-231/219",
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-MIGHTY-FAERIE"] = new(
                    "P1-MIGHTY-FAERIE",
                    isExhausted: true,
                    cardNo: "SFD·125/221",
                    ownerId: "P1",
                    controllerId: "P1")
            }
        };
        var engine = new CoreRuleEngine();

        var playResult = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-master-yi-level-play", "P1", "PLAY_CARD"),
            new PlayCardCommand("P1-MIGHTY-FAERIE", "SFD·125/221", []),
            CancellationToken.None);
        // CN 359.2: entry has already completed in PLAY_CARD.
        var p2PassResult = playResult;

        Assert.True(playResult.Accepted);
        Assert.True(p2PassResult.Accepted);
        Assert.Contains("P1-MIGHTY-FAERIE", p2PassResult.State.PlayerZones["P1"].Base);
        Assert.False(p2PassResult.State.CardObjects["P1-MIGHTY-FAERIE"].IsExhausted);
        Assert.Contains(p2PassResult.Events, gameEvent => string.Equals(gameEvent.Kind, "UNIT_PLAYED_TO_BASE", StringComparison.Ordinal));
    }

    [Fact]
    public async Task P79LegendTriggerAnnieReadiesTwoRunesAtTurnEnd()
    {
        var state = PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base =
                    [
                        "P1-RUNE-EXHAUSTED-001",
                        "P1-RUNE-EXHAUSTED-002",
                        "P1-RUNE-EXHAUSTED-003",
                        "P1-UNIT-EXHAUSTED"
                    ],
                    LegendZone = ["P1-LEGEND-ANNIE"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    RuneDeck = ["P2-RUNE-001", "P2-RUNE-002"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-LEGEND-ANNIE"] = new(
                    "P1-LEGEND-ANNIE",
                    cardNo: "OGS·017/024",
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-RUNE-EXHAUSTED-001"] = new(
                    "P1-RUNE-EXHAUSTED-001",
                    isExhausted: true,
                    tags: [CardObjectTags.RuneCard]),
                ["P1-RUNE-EXHAUSTED-002"] = new(
                    "P1-RUNE-EXHAUSTED-002",
                    isExhausted: true,
                    tags: [CardObjectTags.RuneCard]),
                ["P1-RUNE-EXHAUSTED-003"] = new(
                    "P1-RUNE-EXHAUSTED-003",
                    isExhausted: true,
                    tags: [CardObjectTags.RuneCard]),
                ["P1-UNIT-EXHAUSTED"] = new(
                    "P1-UNIT-EXHAUSTED",
                    isExhausted: true,
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-annie-end-turn", "P1", "END_TURN"),
            new EndTurnCommand(),
            CancellationToken.None);

        Assert.True(result.Accepted);
        result=await FinishEndRuneChoice(result,["P1-RUNE-EXHAUSTED-001","P1-RUNE-EXHAUSTED-002"]);
        Assert.False(result.State.CardObjects["P1-RUNE-EXHAUSTED-001"].IsExhausted);
        Assert.False(result.State.CardObjects["P1-RUNE-EXHAUSTED-002"].IsExhausted);
        Assert.True(result.State.CardObjects["P1-RUNE-EXHAUSTED-003"].IsExhausted);
        Assert.True(result.State.CardObjects["P1-UNIT-EXHAUSTED"].IsExhausted);
        Assert.Equal(2,result.Events.Count(e=>e.Kind=="RUNE_READIED"&&e.Payload["playerId"] as string=="P1"));
    }

    private static async Task<ResolutionResult> FinishEndRuneChoice(ResolutionResult result,IReadOnlyList<string> selected)
    {
        var c=Assert.IsType<PendingCardChoiceState>(result.State.PendingCardChoice);
        Assert.Equal("END_STEP_RUNE_TARGETS",c.ChoiceWindow);
        var chosen=await OfficialInsightAndSpellLockTests.Act(result.State,c.PlayerId,new ChooseCardsCommand(c.ChoiceId,c.ChoiceWindow,selected));
        var resolved=await OfficialInsightAndSpellLockTests.ResolveTop(chosen.State);
        return resolved with {Events=result.Events.Concat(chosen.Events).Concat(resolved.Events).ToArray()};
    }

    [Theory]
    [InlineData("OGN·249/298", "P1-LEGEND-VOLIBEAR")]
    [InlineData("SFD·205/221", "P1-LEGEND-FIORA")]
    public async Task P79LegendTriggerPowerfulUnitCallsRuneAndExhaustsLegend(
        string legendCardNo,
        string legendObjectId)
    {
        var state = PunishmentState(mana: 6) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-GAI-BEAST"],
                    RuneDeck = ["P1-RUNE-POWERFUL-001"],
                    LegendZone = [legendObjectId]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [legendObjectId] = new(
                    legendObjectId,
                    cardNo: legendCardNo,
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-GAI-BEAST"] = new(
                    "P1-GAI-BEAST",
                    cardNo: "SFD·175/221",
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-RUNE-POWERFUL-001"] = new(
                    "P1-RUNE-POWERFUL-001",
                    ownerId: "P1",
                    controllerId: "P1",
                    tags: [CardObjectTags.RuneCard])
            }
        };
        state = PrintedCostFixture.Add(state, "P1", "SFD·175/221");
        var engine = new CoreRuleEngine();

        var playResult = await engine.ResolveAsync(
            state,
            new PlayerIntent($"intent-p7-9-powerful-unit-play-{legendCardNo}", "P1", "PLAY_CARD"),
            new PlayCardCommand("P1-GAI-BEAST", "SFD·175/221", []),
            CancellationToken.None);
        var p1PassResult = await engine.ResolveAsync(
            playResult.State,
            new PlayerIntent($"intent-p7-9-powerful-unit-p1-pass-{legendCardNo}", "P1", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2PassResult = await engine.ResolveAsync(
            p1PassResult.State,
            new PlayerIntent($"intent-p7-9-powerful-unit-p2-pass-{legendCardNo}", "P2", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(playResult.Accepted);
        Assert.True(p1PassResult.Accepted);
        Assert.True(p2PassResult.Accepted);
        Assert.Contains("P1-GAI-BEAST", p2PassResult.State.PlayerZones["P1"].Base);
        if (legendCardNo == "SFD·205/221") {
            // Printed Mighty outside play is already Mighty; entering alone is
            // not the below-five crossing required by current CN rule 709.
            Assert.Contains("P1-RUNE-POWERFUL-001", p2PassResult.State.PlayerZones["P1"].RuneDeck);
            Assert.DoesNotContain("P1-RUNE-POWERFUL-001", p2PassResult.State.PlayerZones["P1"].Base);
            Assert.False(p2PassResult.State.CardObjects[legendObjectId].IsExhausted);
            Assert.DoesNotContain(p2PassResult.Events, e => e.Kind is "RUNES_CALLED" or "LEGEND_EXHAUSTED");
            Assert.Null(p2PassResult.State.PendingCardChoice); Assert.Empty(p2PassResult.State.StackItems); return;
        }
        Assert.Contains("P1-RUNE-POWERFUL-001", p2PassResult.State.PlayerZones["P1"].Base);
        Assert.Empty(p2PassResult.State.PlayerZones["P1"].RuneDeck);
        Assert.True(p2PassResult.State.CardObjects[legendObjectId].IsExhausted);
        Assert.True(p2PassResult.State.CardObjects["P1-RUNE-POWERFUL-001"].IsExhausted);
        Assert.Contains(p2PassResult.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "LEGEND_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "POWERFUL_UNIT_PLAYED_CALL_RUNE", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["legendCardNo"] as string, legendCardNo, StringComparison.Ordinal));
        Assert.Contains(p2PassResult.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "LEGEND_EXHAUSTED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["legendObjectId"] as string, legendObjectId, StringComparison.Ordinal));
        var runeCallEvent = Assert.Single(p2PassResult.Events, gameEvent => string.Equals(gameEvent.Kind, "RUNES_CALLED", StringComparison.Ordinal));
        Assert.Equal(1, runeCallEvent.Payload["count"]);
    }
}
