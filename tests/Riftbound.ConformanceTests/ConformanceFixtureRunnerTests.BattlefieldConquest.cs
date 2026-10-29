using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Engine;
using Riftbound.Contracts;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed partial class ConformanceFixtureRunnerTests
{

    [Fact]
    public async Task P79BattlefieldHeldDrawsCardFromBattlefieldObjectDoesNotTriggerOnDefensiveVictory()
    {
        var state = BattlefieldHeldDrawState();

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-battlefield-held-draw", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "P2-BATTLEFIELD-DREAM-TREE",
                ["P1-BATTLEFIELD-HELD-ATTACKER"],
                ["P2-BATTLEFIELD-HELD-DEFENDER"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.DoesNotContain(result.Events, e => e.Kind == "BATTLEFIELD_HELD");
        Assert.DoesNotContain(result.State.TriggerQueue, t => t.HeldContext is not null);
        Assert.DoesNotContain(result.State.StackItems, t => t.HeldContext is not null);
        Assert.Empty(result.State.DelayedResourceGains);
    }

    [Fact]
    public async Task P79DunehornBeastDrawsTwoWhenHoldingBattlefieldDoesNotTriggerOnDefensiveVictory()
    {
        var state = DunehornBeastHeldDrawState();

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-dunehorn-beast-held-draw", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "P2-DUNEHORN-BATTLEFIELD",
                ["P1-DUNEHORN-HELD-ATTACKER"],
                ["P2-DUNEHORN-BEAST"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.DoesNotContain(result.Events, e => e.Kind == "BATTLEFIELD_HELD");
        Assert.DoesNotContain(result.State.TriggerQueue, t => t.HeldContext is not null);
        Assert.DoesNotContain(result.State.StackItems, t => t.HeldContext is not null);
        Assert.Empty(result.State.DelayedResourceGains);
    }

    [Fact]
    public async Task P79BattlefieldHeldDrawSkipsOpponentOwnedBattlefield()
    {
        var state = BattlefieldHeldDrawState();
        var cardObjects = state.CardObjects.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        cardObjects["P2-BATTLEFIELD-DREAM-TREE"] = cardObjects["P2-BATTLEFIELD-DREAM-TREE"] with
        {
            OwnerId = "P1",
            ControllerId = ""
        };
        state = state with { CardObjects = cardObjects };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-battlefield-held-draw-dirty-source", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "P2-BATTLEFIELD-DREAM-TREE",
                ["P1-BATTLEFIELD-HELD-ATTACKER"],
                ["P2-BATTLEFIELD-HELD-DEFENDER"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Empty(result.State.PlayerZones["P2"].Hand);
        Assert.Equal(["P2-BATTLEFIELD-DRAW-001"], result.State.PlayerZones["P2"].MainDeck);
        Assert.DoesNotContain(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "BATTLEFIELD_HELD_DRAW_ONE", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "CARD_DRAWN", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["playerId"] as string, "P2", StringComparison.Ordinal));
    }

    [Fact]
    public async Task P79BattlefieldHeldGrantsBoonToSurvivingDefenderDoesNotTriggerOnDefensiveVictory()
    {
        var state = BattlefieldHeldBoonState();

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-battlefield-held-boon", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "P2-BATTLEFIELD-NAVORI-ARENA",
                ["P1-BATTLEFIELD-BOON-ATTACKER"],
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
    public async Task P79BattlefieldHeldMovesSurvivingDefenderToBaseDoesNotTriggerOnDefensiveVictory()
    {
        var state = BattlefieldHeldMoveToBaseState();

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-battlefield-held-move-to-base", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "P2-BATTLEFIELD-REHEARSAL-HALL",
                ["P1-BATTLEFIELD-REHEARSAL-ATTACKER"],
                ["P2-BATTLEFIELD-REHEARSAL-DEFENDER"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.DoesNotContain(result.Events, e => e.Kind == "BATTLEFIELD_HELD");
        Assert.DoesNotContain(result.State.TriggerQueue, t => t.HeldContext is not null);
        Assert.DoesNotContain(result.State.StackItems, t => t.HeldContext is not null);
        Assert.Empty(result.State.DelayedResourceGains);
    }

    [Fact]
    public async Task P79BattlefieldDefendMovesChosenSurvivingDefenderToBase()
    {
        var state = BattlefieldDefendMoveToBaseState();

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-battlefield-defend-move-to-base", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "P2-BATTLEFIELD-PLUNDER-ALLEY",
                ["P1-BATTLEFIELD-PLUNDER-ATTACKER"],
                ["P2-BATTLEFIELD-PLUNDER-DEFENDER"],
                ["COMBAT_ASSIGNMENT"],
                ["P2-BATTLEFIELD-PLUNDER-DEFENDER"]),
            CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Contains(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "BATTLEFIELD_DEFENSE_MOVE_FRIENDLY_UNIT_TO_BASE", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, "P2-BATTLEFIELD-PLUNDER-DEFENDER", StringComparison.Ordinal));
        Assert.Contains(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_MOVED_TO_BASE", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, "P2-BATTLEFIELD-PLUNDER-DEFENDER", StringComparison.Ordinal));
        Assert.Equal(["P2-BATTLEFIELD-PLUNDER-DEFENDER"], result.State.PlayerZones["P2"].Base);
        Assert.Equal(["P2-BATTLEFIELD-PLUNDER-ALLEY"], result.State.PlayerZones["P2"].Battlefields);
        Assert.False(result.State.CardObjects["P2-BATTLEFIELD-PLUNDER-DEFENDER"].IsDefending);
        Assert.Equal(["P1-BATTLEFIELD-PLUNDER-ATTACKER"], result.State.PlayerZones["P1"].Graveyard);
    }

    [Fact]
    public async Task P79BattlefieldConquerOverkillCreatesWarhawk()
    {
        var state = BattlefieldConquerWarhawkState();

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-battlefield-conquer-warhawk", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "P2-BATTLEFIELD-HUNTING-GROUNDS",
                ["P1-BATTLEFIELD-HUNTING-ATTACKER"],
                ["P2-BATTLEFIELD-HUNTING-DEFENDER"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted);
        var triggerEvent = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "BATTLEFIELD_CONQUERED_OVERKILL_CREATE_WARHAWK", StringComparison.Ordinal));
        Assert.Equal(4, triggerEvent.Payload["assignedOverkillDamageToEnemyUnits"]);
        var tokenEvent = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_TOKEN_CREATED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["tokenCardNo"] as string, "UNL·T02", StringComparison.Ordinal));
        var tokenObjectId = Assert.IsType<string>(tokenEvent.Payload["tokenObjectId"]);
        Assert.Contains(tokenObjectId, result.State.PlayerZones["P1"].Battlefields);
        var token = result.State.CardObjects[tokenObjectId];
        Assert.Equal(1, token.Power);
        Assert.Contains(CardObjectTags.UnitCard, token.Tags);
        Assert.Contains(CardObjectTags.Spellshield, token.Tags);
    }

    [Fact]
    public async Task P79BattlefieldConquerRecyclesRune()
    {
        var state = BattlefieldConquerRecycleRuneState();

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-battlefield-conquer-recycle-rune", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "P1-BATTLEFIELD-THUNDER-RUNE",
                ["P1-BATTLEFIELD-RECYCLE-ATTACKER"],
                ["P2-BATTLEFIELD-RECYCLE-DEFENDER"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Empty(result.State.PlayerZones["P1"].Base);
        Assert.Equal(["P1-MAIN-001"], result.State.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-BATTLEFIELD-RECYCLE-RUNE-001"], result.State.PlayerZones["P1"].RuneDeck);
        Assert.Equal(["P2-BATTLEFIELD-RECYCLE-DEFENDER"], result.State.PlayerZones["P2"].Graveyard);
        var triggerEvent = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "BATTLEFIELD_CONQUERED_RECYCLE_RUNE", StringComparison.Ordinal));
        Assert.Equal("P1-BATTLEFIELD-THUNDER-RUNE", triggerEvent.Payload["battlefieldObjectId"]);
        Assert.Equal("P1-BATTLEFIELD-RECYCLE-RUNE-001", triggerEvent.Payload["targetObjectId"]);
        var recycleEvent = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "CARDS_RECYCLED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, "P1-BATTLEFIELD-THUNDER-RUNE", StringComparison.Ordinal));
        Assert.Equal(["P1-BATTLEFIELD-RECYCLE-RUNE-001"], Assert.IsAssignableFrom<IReadOnlyList<string>>(recycleEvent.Payload["cardIds"]));
        Assert.Equal("BASE", recycleEvent.Payload["sourceZone"]);
        Assert.Equal("RUNE_DECK", recycleEvent.Payload["destinationZone"]);
    }

    [Fact]
    public async Task P79BattlefieldConquerRecycleRuneSkipsOpponentControlledBaseRune()
    {
        var baseState = BattlefieldConquerRecycleRuneState();
        var playerZones = baseState.PlayerZones.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        playerZones["P1"] = playerZones["P1"] with
        {
            Base = ["P1-BATTLEFIELD-RECYCLE-DIRTY-P2-RUNE", "P1-BATTLEFIELD-RECYCLE-RUNE-001"]
        };
        var cardObjects = baseState.CardObjects.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        cardObjects["P1-BATTLEFIELD-RECYCLE-DIRTY-P2-RUNE"] = new(
            "P1-BATTLEFIELD-RECYCLE-DIRTY-P2-RUNE",
            tags: [CardObjectTags.RuneCard],
            ownerId: "P2");
        var state = baseState with
        {
            PlayerZones = playerZones,
            CardObjects = cardObjects
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-battlefield-conquer-recycle-dirty-rune", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "P1-BATTLEFIELD-THUNDER-RUNE",
                ["P1-BATTLEFIELD-RECYCLE-ATTACKER"],
                ["P2-BATTLEFIELD-RECYCLE-DEFENDER"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Equal(["P1-BATTLEFIELD-RECYCLE-DIRTY-P2-RUNE"], result.State.PlayerZones["P1"].Base);
        Assert.Equal(["P1-MAIN-001"], result.State.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-BATTLEFIELD-RECYCLE-RUNE-001"], result.State.PlayerZones["P1"].RuneDeck);
        var recycleEvent = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "CARDS_RECYCLED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, "P1-BATTLEFIELD-THUNDER-RUNE", StringComparison.Ordinal));
        Assert.Equal(["P1-BATTLEFIELD-RECYCLE-RUNE-001"], Assert.IsAssignableFrom<IReadOnlyList<string>>(recycleEvent.Payload["cardIds"]));
    }

    [Fact]
    public async Task P79BattlefieldDefendRevealSpellDrawsTopSpell()
    {
        var state = BattlefieldDefendRevealSpellState(topCardIsSpell: true);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-battlefield-defend-reveal-spell", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "P2-BATTLEFIELD-RAVENBLOOM",
                ["P1-BATTLEFIELD-REVEAL-ATTACKER"],
                ["P2-BATTLEFIELD-REVEAL-DEFENDER"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Equal(["P2-BATTLEFIELD-REVEAL-SPELL"], result.State.PlayerZones["P2"].Hand);
        Assert.Equal(["P2-MAIN-001"], result.State.PlayerZones["P2"].MainDeck);
        var triggerEvent = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "BATTLEFIELD_DEFENSE_REVEAL_TOP_DRAW_SPELL_OR_RECYCLE", StringComparison.Ordinal));
        Assert.Equal("P2-BATTLEFIELD-RAVENBLOOM", triggerEvent.Payload["battlefieldObjectId"]);
        Assert.Equal("P2-BATTLEFIELD-REVEAL-SPELL", triggerEvent.Payload["revealedObjectId"]);
        Assert.Equal(true, triggerEvent.Payload["revealedIsSpell"]);
        Assert.Contains(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "CARDS_REVEALED", StringComparison.Ordinal)
            && Assert.IsAssignableFrom<IReadOnlyList<string>>(gameEvent.Payload["cardIds"]).Contains("P2-BATTLEFIELD-REVEAL-SPELL", StringComparer.Ordinal));
        Assert.Contains(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "CARD_ADDED_TO_HAND", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, "P2-BATTLEFIELD-RAVENBLOOM", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "CARD_DRAWN", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, "P2-BATTLEFIELD-RAVENBLOOM", StringComparison.Ordinal));
    }

    [Fact]
    public async Task P79BattlefieldDefendRevealSpellRecyclesTopNonSpell()
    {
        var state = BattlefieldDefendRevealSpellState(topCardIsSpell: false);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-battlefield-defend-reveal-nonspell", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "P2-BATTLEFIELD-RAVENBLOOM",
                ["P1-BATTLEFIELD-REVEAL-ATTACKER"],
                ["P2-BATTLEFIELD-REVEAL-DEFENDER"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Empty(result.State.PlayerZones["P2"].Hand);
        Assert.Equal(["P2-MAIN-001", "P2-BATTLEFIELD-REVEAL-UNIT"], result.State.PlayerZones["P2"].MainDeck);
        var triggerEvent = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "BATTLEFIELD_DEFENSE_REVEAL_TOP_DRAW_SPELL_OR_RECYCLE", StringComparison.Ordinal));
        Assert.Equal("P2-BATTLEFIELD-REVEAL-UNIT", triggerEvent.Payload["revealedObjectId"]);
        Assert.Equal(false, triggerEvent.Payload["revealedIsSpell"]);
        var recycleEvent = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "CARDS_RECYCLED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, "P2-BATTLEFIELD-RAVENBLOOM", StringComparison.Ordinal));
        Assert.Equal(["P2-BATTLEFIELD-REVEAL-UNIT"], Assert.IsAssignableFrom<IReadOnlyList<string>>(recycleEvent.Payload["cardIds"]));
    }

    [Fact]
    public async Task P79BattlefieldDefendRevealSpellSkipsOpponentControlledTopCard()
    {
        var baseState = BattlefieldDefendRevealSpellState(topCardIsSpell: true);
        var playerZones = baseState.PlayerZones.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        playerZones["P2"] = playerZones["P2"] with
        {
            MainDeck = ["P2-DIRTY-REVEAL-P1-OWNED", "P2-BATTLEFIELD-REVEAL-SPELL", "P2-MAIN-001"]
        };
        var cardObjects = baseState.CardObjects.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        cardObjects["P2-DIRTY-REVEAL-P1-OWNED"] = new(
            "P2-DIRTY-REVEAL-P1-OWNED",
            tags: [CardObjectTags.SpellCard],
            ownerId: "P1");
        var state = baseState with
        {
            PlayerZones = playerZones,
            CardObjects = cardObjects
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-battlefield-defend-reveal-dirty-top", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "P2-BATTLEFIELD-RAVENBLOOM",
                ["P1-BATTLEFIELD-REVEAL-ATTACKER"],
                ["P2-BATTLEFIELD-REVEAL-DEFENDER"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Empty(result.State.PlayerZones["P2"].Hand);
        Assert.Equal(
            ["P2-DIRTY-REVEAL-P1-OWNED", "P2-BATTLEFIELD-REVEAL-SPELL", "P2-MAIN-001"],
            result.State.PlayerZones["P2"].MainDeck);
        Assert.DoesNotContain(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "BATTLEFIELD_DEFENSE_REVEAL_TOP_DRAW_SPELL_OR_RECYCLE", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "CARDS_REVEALED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, "P2-BATTLEFIELD-RAVENBLOOM", StringComparison.Ordinal));
        Assert.Equal("MAIN_DECK", result.State.ObjectLocations["P2-DIRTY-REVEAL-P1-OWNED"].Zone);
    }

    [Fact]
    public async Task P79BattlefieldDefendRevealSpellSkipsAttackerControlledBattlefield()
    {
        var baseState = BattlefieldDefendRevealSpellState(topCardIsSpell: true);
        var cardObjects = baseState.CardObjects.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        cardObjects["P2-BATTLEFIELD-RAVENBLOOM"] = cardObjects["P2-BATTLEFIELD-RAVENBLOOM"] with
        {
            ControllerId = "P1"
        };
        var state = baseState with
        {
            CardObjects = cardObjects
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-battlefield-defend-reveal-dirty-source", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "P2-BATTLEFIELD-RAVENBLOOM",
                ["P1-BATTLEFIELD-REVEAL-ATTACKER"],
                ["P2-BATTLEFIELD-REVEAL-DEFENDER"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Empty(result.State.PlayerZones["P2"].Hand);
        Assert.Equal(["P2-BATTLEFIELD-REVEAL-SPELL", "P2-MAIN-001"], result.State.PlayerZones["P2"].MainDeck);
        Assert.DoesNotContain(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "BATTLEFIELD_DEFENSE_REVEAL_TOP_DRAW_SPELL_OR_RECYCLE", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "CARD_DRAWN", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, "P2-BATTLEFIELD-RAVENBLOOM", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "CARD_ADDED_TO_HAND", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, "P2-BATTLEFIELD-RAVENBLOOM", StringComparison.Ordinal));
    }

    [Fact]
    public async Task P79BattlefieldEphemeralDefenderGainsSteadfast()
    {
        var state = BattlefieldEphemeralSteadfastState();

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-battlefield-ephemeral-steadfast", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "P2-BATTLEFIELD-BLACK-FLAME",
                ["P1-BATTLEFIELD-EPHEMERAL-ATTACKER"],
                ["P2-BATTLEFIELD-EPHEMERAL-DEFENDER"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted);
        var defenderDamageEvent = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "DAMAGE_APPLIED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["combatRole"] as string, "DEFENDER", StringComparison.Ordinal));
        Assert.Equal("P2-BATTLEFIELD-EPHEMERAL-DEFENDER", defenderDamageEvent.Payload["sourceObjectId"]);
        Assert.Equal("P1-BATTLEFIELD-EPHEMERAL-ATTACKER", defenderDamageEvent.Payload["targetObjectId"]);
        Assert.Equal("坚守", defenderDamageEvent.Payload["keyword"]);
        Assert.Equal(2, defenderDamageEvent.Payload["basePower"]);
        Assert.Equal(1, defenderDamageEvent.Payload["keywordBonus"]);
        Assert.Equal(3, defenderDamageEvent.Payload["combatPower"]);
        Assert.Equal(3, defenderDamageEvent.Payload["damage"]);
        Assert.Equal(0, result.State.CardObjects["P1-BATTLEFIELD-EPHEMERAL-ATTACKER"].Damage);
    }

    [Fact]
    public async Task P79BattlefieldDefenderSteadfastTwoChoosesDefender()
    {
        var state = BattlefieldDefenderSteadfastState();
        var result = await OfficialInsightAndSpellLockTests.Act(state, "P1",
            new MoveUnitCommand("P1-BATTLEFIELD-FORTIFIED-ATTACKER", "BASE", "BATTLEFIELD:P2-BATTLEFIELD-FORTIFIED-POSITION", []));
        result = await StandbyDefendTeemoTests.ConfirmAndResolveDefend(result, "P2-BATTLEFIELD-FORTIFIED-DEFENDER");
        var defendingView = JsonSerializer.SerializeToElement(result.Snapshots["P2"], new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Equal(4, defendingView.GetProperty("players").GetProperty("P2").GetProperty("objects")
            .GetProperty("P2-BATTLEFIELD-FORTIFIED-DEFENDER").GetProperty("effectivePower").GetInt32());
        var events = result.Events.ToList();
        for (var pass = 0; result.State.FocusPlayerId is not null; pass++) {
            Assert.InRange(pass, 0, 3);
            result = await OfficialInsightAndSpellLockTests.Act(result.State, result.State.FocusPlayerId, new PassFocusCommand());
            events.AddRange(result.Events);
        }
        result = await OfficialInsightAndSpellLockTests.Act(result.State, "P1", new DeclareBattleCommand(
            "P2-BATTLEFIELD-FORTIFIED-POSITION", ["P1-BATTLEFIELD-FORTIFIED-ATTACKER"],
            ["P2-BATTLEFIELD-FORTIFIED-DEFENDER"], ["COMBAT_ASSIGNMENT"]));
        events.AddRange(result.Events); result = result with { Events = events };

        Assert.True(result.Accepted);
        var triggerEvent = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "BATTLEFIELD_DEFENSE_GRANT_STEADFAST_TWO", StringComparison.Ordinal));
        Assert.Equal("P2-BATTLEFIELD-FORTIFIED-POSITION", triggerEvent.Payload["battlefieldObjectId"]);
        Assert.Equal("P2-BATTLEFIELD-FORTIFIED-DEFENDER", triggerEvent.Payload["targetObjectId"]);
        Assert.Equal(2, triggerEvent.Payload["keywordBonus"]);

        var defenderDamageEvent = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "DAMAGE_APPLIED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["combatRole"] as string, "DEFENDER", StringComparison.Ordinal));
        Assert.Equal("P2-BATTLEFIELD-FORTIFIED-DEFENDER", defenderDamageEvent.Payload["sourceObjectId"]);
        Assert.Equal("P1-BATTLEFIELD-FORTIFIED-ATTACKER", defenderDamageEvent.Payload["targetObjectId"]);
        Assert.Equal("坚守", defenderDamageEvent.Payload["keyword"]);
        Assert.Equal(2, defenderDamageEvent.Payload["basePower"]);
        Assert.Equal(2, defenderDamageEvent.Payload["keywordBonus"]);
        Assert.Equal(4, defenderDamageEvent.Payload["combatPower"]);
        Assert.Equal(4, defenderDamageEvent.Payload["damage"]);
        var attackerDamageEvent = Assert.Single(result.Events, e => e.Kind == "DAMAGE_APPLIED"
            && Equals(e.Payload.GetValueOrDefault("combatRole"), "ATTACKER"));
        Assert.Equal("P1-BATTLEFIELD-FORTIFIED-ATTACKER", attackerDamageEvent.Payload["sourceObjectId"]);
        Assert.Equal("P2-BATTLEFIELD-FORTIFIED-DEFENDER", attackerDamageEvent.Payload["targetObjectId"]);
        Assert.Equal(3, attackerDamageEvent.Payload["combatPower"]);
        Assert.Equal(3, attackerDamageEvent.Payload["damage"]);
        Assert.Equal(["P1-BATTLEFIELD-FORTIFIED-ATTACKER"], result.State.PlayerZones["P1"].Graveyard);
        // Defending Might is 2 + Steadfast2 = 4, so the attacker's original 3 damage is nonlethal.
        Assert.Empty(result.State.PlayerZones["P2"].Graveyard);
        Assert.Contains("P2-BATTLEFIELD-FORTIFIED-DEFENDER", result.State.PlayerZones["P2"].Battlefields);
        Assert.Equal(new ObjectLocationState("P2", "BATTLEFIELD", "P2-BATTLEFIELD-FORTIFIED-POSITION"),
            result.State.ObjectLocations["P2-BATTLEFIELD-FORTIFIED-DEFENDER"]);
        Assert.Equal(2, result.State.CardObjects["P2-BATTLEFIELD-FORTIFIED-DEFENDER"].Power);
        Assert.Equal(0, result.State.CardObjects["P2-BATTLEFIELD-FORTIFIED-DEFENDER"].Damage);
        Assert.DoesNotContain(result.State.CardObjects["P2-BATTLEFIELD-FORTIFIED-DEFENDER"].UntilEndOfTurnEffects,
            e => e.StartsWith("COMBAT_KEYWORD:坚守2:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task P79BattlefieldIsolatedDefenderLosesSteadfastTwo()
    {
        var state = BattlefieldIsolatedDefenderState();

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-battlefield-isolated-defender", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "P2-BATTLEFIELD-FORBIDDEN-WASTELAND",
                ["P1-BATTLEFIELD-ISOLATED-ATTACKER"],
                ["P2-BATTLEFIELD-ISOLATED-DEFENDER"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted);
        var defenderDamageEvent = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "DAMAGE_APPLIED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["combatRole"] as string, "DEFENDER", StringComparison.Ordinal));
        Assert.Equal("P2-BATTLEFIELD-ISOLATED-DEFENDER", defenderDamageEvent.Payload["sourceObjectId"]);
        Assert.Equal("P1-BATTLEFIELD-ISOLATED-ATTACKER", defenderDamageEvent.Payload["targetObjectId"]);
        Assert.Equal("坚守", defenderDamageEvent.Payload["keyword"]);
        Assert.Equal(4, defenderDamageEvent.Payload["basePower"]);
        Assert.Equal(-2, defenderDamageEvent.Payload["keywordBonus"]);
        Assert.Equal(2, defenderDamageEvent.Payload["combatPower"]);
        Assert.Equal(2, defenderDamageEvent.Payload["damage"]);
        Assert.Equal(0, result.State.CardObjects["P1-BATTLEFIELD-ISOLATED-ATTACKER"].Damage);
        Assert.Equal(["P2-BATTLEFIELD-ISOLATED-DEFENDER"], result.State.PlayerZones["P2"].Graveyard);
    }

    [Fact]
    public async Task P79BattlefieldConquerReadyLegendPaysOne()
    {
        var state = BattlefieldConquerReadyLegendState();
        var engine = new CoreRuleEngine();

        var result = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-battlefield-ready-legend", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "P1-BATTLEFIELD-LEGEND-HALL",
                ["P1-BATTLEFIELD-READY-ATTACKER"],
                ["P2-BATTLEFIELD-READY-DEFENDER"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.True(result.State.CardObjects["P1-LEGEND-READY-TARGET"].IsExhausted);
        Assert.Equal(1, result.State.RunePools["P1"].Mana);
        var payment = result.State.PendingPayment;
        Assert.NotNull(payment);
        Assert.Equal("TRIGGER_PAYMENT", payment.PaymentWindow);
        Assert.Equal(["SPEND_MANA:1", "DECLINE"], payment.LegalPaymentChoiceIds);
        Assert.Contains(result.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLEFIELD_CONQUERED", StringComparison.Ordinal));
        Assert.Contains(result.Events, gameEvent => string.Equals(gameEvent.Kind, "PAYMENT_WINDOW_OPENED", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "BATTLEFIELD_CONQUERED_PAY_1_READY_LEGEND", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "LEGEND_READIED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, "P1-LEGEND-READY-TARGET", StringComparison.Ordinal));

        var paid = await engine.ResolveAsync(
            result.State,
            new PlayerIntent("intent-p7-9-battlefield-ready-legend-pay", "P1", "PAY_COST"),
            new PayCostCommand(payment.PaymentId, payment.PaymentWindow, ["SPEND_MANA:1"]),
            CancellationToken.None);

        Assert.True(paid.Accepted);
        Assert.Null(paid.State.PendingPayment);
        Assert.False(paid.State.CardObjects["P1-LEGEND-READY-TARGET"].IsExhausted);
        Assert.Equal(0, paid.State.RunePools["P1"].Mana);
        Assert.Contains(paid.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "BATTLEFIELD_CONQUERED_PAY_1_READY_LEGEND", StringComparison.Ordinal));
        Assert.Contains(paid.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "LEGEND_READIED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, "P1-LEGEND-READY-TARGET", StringComparison.Ordinal));
    }

    [Fact]
    public async Task P79BattlefieldConquerReadyLegendSkipsOpponentOwnedLegend()
    {
        var baseState = BattlefieldConquerReadyLegendState();
        var cardObjects = baseState.CardObjects.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        cardObjects["P1-LEGEND-READY-TARGET"] = cardObjects["P1-LEGEND-READY-TARGET"] with
        {
            OwnerId = "P2",
            ControllerId = ""
        };
        var state = baseState with { CardObjects = cardObjects };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-battlefield-ready-legend-dirty", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "P1-BATTLEFIELD-LEGEND-HALL",
                ["P1-BATTLEFIELD-READY-ATTACKER"],
                ["P2-BATTLEFIELD-READY-DEFENDER"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.True(result.State.CardObjects["P1-LEGEND-READY-TARGET"].IsExhausted);
        Assert.Equal(1, result.State.RunePools["P1"].Mana);
        Assert.DoesNotContain(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "BATTLEFIELD_CONQUERED_PAY_1_READY_LEGEND", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["reason"] as string, "BATTLEFIELD_CONQUERED_PAY_1_READY_LEGEND", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Events, gameEvent => string.Equals(gameEvent.Kind, "LEGEND_READIED", StringComparison.Ordinal));
    }

    [Fact]
    public async Task P79BattlefieldConquerReadyRunesAtEndSchedulesAndReadiesRunes()
    {
        var state = BattlefieldConquerReadyRunesEndState();
        var engine = new CoreRuleEngine();

        var battleResult = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-battlefield-ready-runes", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "P1-BATTLEFIELD-MOUNT-TARGON",
                ["P1-BATTLEFIELD-RUNE-ATTACKER"],
                ["P2-BATTLEFIELD-RUNE-DEFENDER"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(battleResult.Accepted);
        Assert.True(battleResult.State.CardObjects["P1-BATTLEFIELD-READY-RUNE-001"].IsExhausted);
        Assert.True(battleResult.State.CardObjects["P1-BATTLEFIELD-READY-RUNE-002"].IsExhausted);
        Assert.DoesNotContain(battleResult.State.UntilEndOfTurnEffects,id=>id.StartsWith("DELAYED_END_READY:"));
        Assert.Contains(battleResult.State.StackItems,item=>item.EndAbility is not null);
        battleResult=await OfficialInsightAndSpellLockTests.ResolveTop(battleResult.State);
        Assert.Single(battleResult.State.UntilEndOfTurnEffects,id=>id.StartsWith("DELAYED_END_READY:"));
        Assert.DoesNotContain(battleResult.Events,e=>e.Kind=="END_RUNE_TARGETS_CONFIRMED");

        var endResult = await engine.ResolveAsync(
            battleResult.State,
            new PlayerIntent("intent-p7-9-battlefield-ready-runes-end", "P1", "END_TURN"),
            new EndTurnCommand(),
            CancellationToken.None);

        Assert.True(endResult.Accepted);
        var choice=Assert.IsType<PendingCardChoiceState>(endResult.State.PendingCardChoice);
        Assert.Equal(["P1-BATTLEFIELD-READY-RUNE-001","P1-BATTLEFIELD-READY-RUNE-002"],choice.LegalObjectIds);
        endResult=await FinishEndRuneChoice(endResult,choice.LegalObjectIds);
        Assert.False(endResult.State.CardObjects["P1-BATTLEFIELD-READY-RUNE-001"].IsExhausted);
        Assert.False(endResult.State.CardObjects["P1-BATTLEFIELD-READY-RUNE-002"].IsExhausted);
        Assert.DoesNotContain(endResult.State.UntilEndOfTurnEffects,id=>id.StartsWith("DELAYED_END_READY:"));
        Assert.Equal(2,endResult.Events.Count(e=>e.Kind=="RUNE_READIED"&&e.Payload["playerId"] as string=="P1"));
    }

    [Fact]
    public async Task P79BattlefieldConquerReadyRunesAtEndSkipsOpponentOwnedRune()
    {
        var baseState = BattlefieldConquerReadyRunesEndState();
        var playerZones = baseState.PlayerZones.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        playerZones["P1"] = playerZones["P1"] with
        {
            Base =
            [
                "P1-BATTLEFIELD-DIRTY-P2-RUNE",
                "P1-BATTLEFIELD-READY-RUNE-001",
                "P1-BATTLEFIELD-READY-RUNE-002"
            ]
        };
        var cardObjects = baseState.CardObjects.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        cardObjects["P1-BATTLEFIELD-DIRTY-P2-RUNE"] = new(
            "P1-BATTLEFIELD-DIRTY-P2-RUNE",
            isExhausted: true,
            tags: [CardObjectTags.RuneCard],
            ownerId: "P2",
            controllerId: "");
        var state = baseState with
        {
            PlayerZones = playerZones,
            CardObjects = cardObjects
        };
        var engine = new CoreRuleEngine();

        var battleResult = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-battlefield-ready-runes-dirty", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "P1-BATTLEFIELD-MOUNT-TARGON",
                ["P1-BATTLEFIELD-RUNE-ATTACKER"],
                ["P2-BATTLEFIELD-RUNE-DEFENDER"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(battleResult.Accepted);
        battleResult=await OfficialInsightAndSpellLockTests.ResolveTop(battleResult.State);
        Assert.Single(battleResult.State.UntilEndOfTurnEffects,id=>id.StartsWith("DELAYED_END_READY:"));

        var endResult = await engine.ResolveAsync(
            battleResult.State,
            new PlayerIntent("intent-p7-9-battlefield-ready-runes-dirty-end", "P1", "END_TURN"),
            new EndTurnCommand(),
            CancellationToken.None);

        Assert.True(endResult.Accepted);
        var choice=Assert.IsType<PendingCardChoiceState>(endResult.State.PendingCardChoice);
        Assert.DoesNotContain("P1-BATTLEFIELD-DIRTY-P2-RUNE",choice.LegalObjectIds);
        endResult=await FinishEndRuneChoice(endResult,choice.LegalObjectIds);
        Assert.True(endResult.State.CardObjects["P1-BATTLEFIELD-DIRTY-P2-RUNE"].IsExhausted);
        Assert.False(endResult.State.CardObjects["P1-BATTLEFIELD-READY-RUNE-001"].IsExhausted);
        Assert.False(endResult.State.CardObjects["P1-BATTLEFIELD-READY-RUNE-002"].IsExhausted);
    }

    [Fact]
    public async Task P79BattlefieldConquerDrawsForOtherControlledBattlefields()
    {
        var state = BattlefieldConquerDrawOtherState();

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-battlefield-draw-other", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "P1-BATTLEFIELD-THRONE-OF-POWER",
                ["P1-BATTLEFIELD-DRAW-OTHER-ATTACKER"],
                ["P2-BATTLEFIELD-DRAW-OTHER-DEFENDER"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted);
        var triggerEvent = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "BATTLEFIELD_CONQUERED_DRAW_FOR_OTHER_BATTLEFIELDS", StringComparison.Ordinal));
        Assert.Equal(2, triggerEvent.Payload["drawCount"]);
        Assert.Equal(
            ["P1-BATTLEFIELD-OTHER-001", "P1-BATTLEFIELD-OTHER-002"],
            Assert.IsAssignableFrom<IReadOnlyList<string>>(triggerEvent.Payload["otherBattlefieldObjectIds"]));
        Assert.Equal(
            ["P1-BATTLEFIELD-DRAW-OTHER-001", "P1-BATTLEFIELD-DRAW-OTHER-002"],
            result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-BATTLEFIELD-DRAW-OTHER-003"], result.State.PlayerZones["P1"].MainDeck);
        Assert.Contains(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "CARD_DRAWN", StringComparison.Ordinal)
            && Equals(gameEvent.Payload["count"], 2));
    }

    [Fact]
    public async Task P79BattlefieldConquerDrawsForOtherBattlefieldsSkipsOpponentOwnedBattlefield()
    {
        var baseState = BattlefieldConquerDrawOtherState();
        var playerZones = baseState.PlayerZones.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        playerZones["P1"] = playerZones["P1"] with
        {
            Battlefields =
            [
                "P1-BATTLEFIELD-THRONE-OF-POWER",
                "P1-BATTLEFIELD-DIRTY-P2-OTHER",
                "P1-BATTLEFIELD-OTHER-001",
                "P1-BATTLEFIELD-OTHER-002",
                "P1-BATTLEFIELD-DRAW-OTHER-ATTACKER"
            ]
        };
        var cardObjects = baseState.CardObjects.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        cardObjects["P1-BATTLEFIELD-DIRTY-P2-OTHER"] = new(
            "P1-BATTLEFIELD-DIRTY-P2-OTHER",
            cardNo: "OGN·280/298",
            tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
            ownerId: "P2",
            controllerId: "");
        var state = baseState with
        {
            PlayerZones = playerZones,
            CardObjects = cardObjects
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-battlefield-draw-other-dirty", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "P1-BATTLEFIELD-THRONE-OF-POWER",
                ["P1-BATTLEFIELD-DRAW-OTHER-ATTACKER"],
                ["P2-BATTLEFIELD-DRAW-OTHER-DEFENDER"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted);
        var triggerEvent = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "BATTLEFIELD_CONQUERED_DRAW_FOR_OTHER_BATTLEFIELDS", StringComparison.Ordinal));
        Assert.Equal(2, triggerEvent.Payload["drawCount"]);
        var otherBattlefieldObjectIds = Assert.IsAssignableFrom<IReadOnlyList<string>>(triggerEvent.Payload["otherBattlefieldObjectIds"]);
        Assert.Equal(["P1-BATTLEFIELD-OTHER-001", "P1-BATTLEFIELD-OTHER-002"], otherBattlefieldObjectIds);
        Assert.DoesNotContain("P1-BATTLEFIELD-DIRTY-P2-OTHER", otherBattlefieldObjectIds);
        Assert.Equal(
            ["P1-BATTLEFIELD-DRAW-OTHER-001", "P1-BATTLEFIELD-DRAW-OTHER-002"],
            result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-BATTLEFIELD-DRAW-OTHER-003"], result.State.PlayerZones["P1"].MainDeck);
    }

    [Fact]
    public async Task P79BattlefieldConquerPowerfulUnitPaysOneToDraw()
    {
        var state = BattlefieldConquerPowerfulDrawState();
        var engine = new CoreRuleEngine();

        var result = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-battlefield-powerful-draw", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "P1-BATTLEFIELD-SUNKEN-TEMPLE",
                ["P1-BATTLEFIELD-POWERFUL-ATTACKER"],
                ["P2-BATTLEFIELD-POWERFUL-DEFENDER"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Equal(1, result.State.RunePools["P1"].Mana);
        var payment = result.State.PendingPayment;
        Assert.NotNull(payment);
        Assert.Equal("TRIGGER_COST_CONFIRMATION", payment.PaymentWindow);
        Assert.Equal(["PAY", "DECLINE"], payment.LegalPaymentChoiceIds);
        Assert.Contains(result.Events, gameEvent => string.Equals(gameEvent.Kind, "PAYMENT_WINDOW_OPENED", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "BATTLEFIELD_CONQUERED_POWERFUL_PAY_1_DRAW", StringComparison.Ordinal));
        Assert.Empty(result.State.PlayerZones["P1"].Hand);

        var paid = await engine.ResolveAsync(
            result.State,
            new PlayerIntent("intent-p7-9-battlefield-powerful-draw-pay", "P1", "PAY_COST"),
            new PayCostCommand(payment.PaymentId, payment.PaymentWindow, ["PAY"]),
            CancellationToken.None);

        Assert.True(paid.Accepted);
        Assert.Empty(paid.State.PlayerZones["P1"].Hand);
        Assert.Single(paid.State.StackItems);
        var response = await OfficialGraveyardRecastTests.Top(paid.State);
        paid = response with { Events = paid.Events.Concat(response.Events).ToArray() };
        Assert.Null(paid.State.PendingPayment);
        Assert.Equal(0, paid.State.RunePools["P1"].Mana);
        var triggerEvent = Assert.Single(paid.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "BATTLEFIELD_CONQUERED_POWERFUL_PAY_1_DRAW", StringComparison.Ordinal));
        Assert.Equal("P1-BATTLEFIELD-POWERFUL-ATTACKER", triggerEvent.Payload["powerfulObjectId"]);
        Assert.Equal(1, triggerEvent.Payload["drawCount"]);
        Assert.Equal(["P1-BATTLEFIELD-POWERFUL-DRAW-001"], paid.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-BATTLEFIELD-POWERFUL-DRAW-002"], paid.State.PlayerZones["P1"].MainDeck);
        Assert.Contains(paid.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["reason"] as string, "BATTLEFIELD_CONQUERED_POWERFUL_PAY_1_DRAW", StringComparison.Ordinal));
    }

    [Fact]
    public async Task P79BattlefieldConquerPowerfulDrawCountsAnySurvivingPowerfulAttacker()
    {
        var state = BattlefieldConquerPowerfulDrawMultipleAttackersState();
        var engine = new CoreRuleEngine();

        var result = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-battlefield-powerful-draw-multiple", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "P1-BATTLEFIELD-SUNKEN-TEMPLE",
                ["P1-BATTLEFIELD-WEAK-ATTACKER", "P1-BATTLEFIELD-SECOND-POWERFUL-ATTACKER"],
                ["P2-BATTLEFIELD-POWERFUL-DEFENDER"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted);
        result = await CombatTestDriver.FinishAsync(result, engine);
        var payment = result.State.PendingPayment;
        Assert.NotNull(payment);
        Assert.Equal("TRIGGER_COST_CONFIRMATION", payment.PaymentWindow);
        Assert.Equal(["PAY", "DECLINE"], payment.LegalPaymentChoiceIds);
        Assert.Contains(result.Events, gameEvent => string.Equals(gameEvent.Kind, "PAYMENT_WINDOW_OPENED", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "BATTLEFIELD_CONQUERED_POWERFUL_PAY_1_DRAW", StringComparison.Ordinal));

        var paid = await engine.ResolveAsync(
            result.State,
            new PlayerIntent("intent-p7-9-battlefield-powerful-draw-multiple-pay", "P1", "PAY_COST"),
            new PayCostCommand(payment.PaymentId, payment.PaymentWindow, ["PAY"]),
            CancellationToken.None);

        Assert.True(paid.Accepted);
        Assert.Empty(paid.State.PlayerZones["P1"].Hand);
        Assert.Single(paid.State.StackItems);
        var response = await OfficialGraveyardRecastTests.Top(paid.State);
        paid = response with { Events = paid.Events.Concat(response.Events).ToArray() };
        Assert.Null(paid.State.PendingPayment);
        Assert.Equal(0, paid.State.RunePools["P1"].Mana);
        var triggerEvent = Assert.Single(paid.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "BATTLEFIELD_CONQUERED_POWERFUL_PAY_1_DRAW", StringComparison.Ordinal));
        Assert.Equal("P1-BATTLEFIELD-SECOND-POWERFUL-ATTACKER", triggerEvent.Payload["powerfulObjectId"]);
        Assert.Equal(1, triggerEvent.Payload["drawCount"]);
        Assert.Equal(["P1-BATTLEFIELD-POWERFUL-DRAW-001"], paid.State.PlayerZones["P1"].Hand);
    }

    [Fact]
    public async Task P79BattlefieldConquerGoldOpensPaymentThenPaysOneToCreateDormantGold()
    {
        var state = BattlefieldConquerGoldState();
        var engine = new CoreRuleEngine();

        var result = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-battlefield-gold", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "P1-BATTLEFIELD-TREASURE-PILE",
                ["P1-BATTLEFIELD-GOLD-ATTACKER"],
                ["P2-BATTLEFIELD-GOLD-DEFENDER"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Equal(1, result.State.RunePools["P1"].Mana);
        var payment = result.State.PendingPayment;
        Assert.NotNull(payment);
        Assert.Equal("TRIGGER_PAYMENT", payment.PaymentWindow);
        Assert.Equal(["SPEND_MANA:1", "DECLINE"], payment.LegalPaymentChoiceIds);
        Assert.Contains(result.Events, gameEvent => string.Equals(gameEvent.Kind, "PAYMENT_WINDOW_OPENED", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "BATTLEFIELD_CONQUERED_PAY_1_CREATE_GOLD", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "EQUIPMENT_TOKEN_CREATED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["abilityId"] as string, "BATTLEFIELD_CONQUERED_PAY_1_CREATE_GOLD", StringComparison.Ordinal));

        var paid = await engine.ResolveAsync(
            result.State,
            new PlayerIntent("intent-p7-9-battlefield-gold-pay", "P1", "PAY_COST"),
            new PayCostCommand(payment.PaymentId, payment.PaymentWindow, ["SPEND_MANA:1"]),
            CancellationToken.None);

        Assert.True(paid.Accepted);
        Assert.Null(paid.State.PendingPayment);
        Assert.Equal(0, paid.State.RunePools["P1"].Mana);
        Assert.Contains(paid.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "BATTLEFIELD_CONQUERED_PAY_1_CREATE_GOLD", StringComparison.Ordinal));
        var tokenEvent = Assert.Single(paid.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "EQUIPMENT_TOKEN_CREATED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["abilityId"] as string, "BATTLEFIELD_CONQUERED_PAY_1_CREATE_GOLD", StringComparison.Ordinal));
        var tokenObjectId = Assert.IsType<string>(tokenEvent.Payload["tokenObjectId"]);
        Assert.Contains(tokenObjectId, paid.State.PlayerZones["P1"].Base);
        Assert.True(paid.State.CardObjects[tokenObjectId].IsExhausted);
        Assert.Contains("金币", paid.State.CardObjects[tokenObjectId].Tags);
    }

    [Fact]
    public async Task P79BattlefieldConquerSandSoldierPaysOneReturnsUnitAndCreatesToken()
    {
        var state = BattlefieldConquerSandSoldierState();
        var engine = new CoreRuleEngine();

        var result = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-battlefield-sand-soldier", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "P1-BATTLEFIELD-EMPEROR-SHRINE",
                ["P1-BATTLEFIELD-SAND-ATTACKER"],
                ["P2-BATTLEFIELD-SAND-DEFENDER"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Equal(1, result.State.RunePools["P1"].Mana);
        var payment = result.State.PendingPayment;
        Assert.NotNull(payment);
        Assert.Equal("TRIGGER_PAYMENT", payment.PaymentWindow);
        Assert.Equal(["SPEND_MANA:1", "DECLINE"], payment.LegalPaymentChoiceIds);
        Assert.Contains("P1-BATTLEFIELD-SAND-ATTACKER", result.State.PlayerZones["P1"].Battlefields);
        Assert.DoesNotContain("P1-BATTLEFIELD-SAND-ATTACKER", result.State.PlayerZones["P1"].Hand);
        Assert.Contains("P1-BATTLEFIELD-SAND-ATTACKER", result.State.CardObjects.Keys);
        Assert.Contains(result.Events, gameEvent => string.Equals(gameEvent.Kind, "PAYMENT_WINDOW_OPENED", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "BATTLEFIELD_CONQUERED_PAY_1_RETURN_UNIT_CREATE_SAND_SOLDIER", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_TOKEN_CREATED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["abilityId"] as string, "BATTLEFIELD_CONQUERED_PAY_1_RETURN_UNIT_CREATE_SAND_SOLDIER", StringComparison.Ordinal));

        var paid = await engine.ResolveAsync(
            result.State,
            new PlayerIntent("intent-p7-9-battlefield-sand-soldier-pay", "P1", "PAY_COST"),
            new PayCostCommand(payment.PaymentId, payment.PaymentWindow, ["SPEND_MANA:1"]),
            CancellationToken.None);

        Assert.True(paid.Accepted);
        Assert.Null(paid.State.PendingPayment);
        Assert.Equal(0, paid.State.RunePools["P1"].Mana);
        Assert.Contains("P1-BATTLEFIELD-SAND-ATTACKER", paid.State.PlayerZones["P1"].Hand);
        Assert.DoesNotContain("P1-BATTLEFIELD-SAND-ATTACKER", paid.State.PlayerZones["P1"].Battlefields);
        CardZoneTestAssertions.RetainedOutsidePlay(paid.State, "P1-BATTLEFIELD-SAND-ATTACKER", "HAND");
        var triggerEvent = Assert.Single(paid.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "BATTLEFIELD_CONQUERED_PAY_1_RETURN_UNIT_CREATE_SAND_SOLDIER", StringComparison.Ordinal));
        Assert.Equal("P1-BATTLEFIELD-SAND-ATTACKER", triggerEvent.Payload["returnedObjectId"]);
        var tokenEvent = Assert.Single(paid.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_TOKEN_CREATED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["abilityId"] as string, "BATTLEFIELD_CONQUERED_PAY_1_RETURN_UNIT_CREATE_SAND_SOLDIER", StringComparison.Ordinal));
        var tokenObjectId = Assert.IsType<string>(tokenEvent.Payload["tokenObjectId"]);
        Assert.Contains(tokenObjectId, paid.State.PlayerZones["P1"].Battlefields);
        Assert.Equal("SFD·T02", paid.State.CardObjects[tokenObjectId].CardNo);
        Assert.Equal(2, paid.State.CardObjects[tokenObjectId].Power);
        Assert.Contains(CardObjectTags.SandSoldier, paid.State.CardObjects[tokenObjectId].Tags);
    }

    [Fact]
    public async Task P79BattlefieldConquerSandSoldierSkipsWhenManaUnavailable()
    {
        var state = BattlefieldConquerSandSoldierState(mana: 0);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-battlefield-sand-soldier-no-mana", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "P1-BATTLEFIELD-EMPEROR-SHRINE",
                ["P1-BATTLEFIELD-SAND-ATTACKER"],
                ["P2-BATTLEFIELD-SAND-DEFENDER"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Equal(0, result.State.RunePools["P1"].Mana);
        Assert.Contains("P1-BATTLEFIELD-SAND-ATTACKER", result.State.PlayerZones["P1"].Battlefields);
        Assert.DoesNotContain("P1-BATTLEFIELD-SAND-ATTACKER", result.State.PlayerZones["P1"].Hand);
        Assert.DoesNotContain(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "BATTLEFIELD_CONQUERED_PAY_1_RETURN_UNIT_CREATE_SAND_SOLDIER", StringComparison.Ordinal));
        Assert.DoesNotContain(result.State.CardObjects.Values, cardObject => string.Equals(cardObject.CardNo, "SFD·T02", StringComparison.Ordinal));
    }

    [Fact]
    public async Task P79BattlefieldHeldCreatesMinionInBaseDoesNotTriggerOnDefensiveVictory()
    {
        var state = BattlefieldHeldMinionState();

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-battlefield-held-minion", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "P2-BATTLEFIELD-UNITY-SANCTUM",
                ["P1-BATTLEFIELD-MINION-ATTACKER"],
                ["P2-BATTLEFIELD-MINION-DEFENDER"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.DoesNotContain(result.Events, e => e.Kind == "BATTLEFIELD_HELD");
        Assert.DoesNotContain(result.State.TriggerQueue, t => t.HeldContext is not null);
        Assert.DoesNotContain(result.State.StackItems, t => t.HeldContext is not null);
        Assert.Empty(result.State.DelayedResourceGains);
    }

    [Fact]
    public async Task P79BattlefieldHeldCallsRunesForEachPlayerDoesNotTriggerOnDefensiveVictory()
    {
        var state = BattlefieldHeldRunesState();

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-battlefield-held-runes", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "P2-BATTLEFIELD-CONFETTI-TREE",
                ["P1-BATTLEFIELD-RUNES-ATTACKER"],
                ["P2-BATTLEFIELD-RUNES-DEFENDER"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.DoesNotContain(result.Events, e => e.Kind == "BATTLEFIELD_HELD");
        Assert.DoesNotContain(result.State.TriggerQueue, t => t.HeldContext is not null);
        Assert.DoesNotContain(result.State.StackItems, t => t.HeldContext is not null);
        Assert.Empty(result.State.DelayedResourceGains);
    }

    [Fact]
    public async Task P79BattlefieldHeldCallsRuneForHolderDoesNotTriggerOnDefensiveVictory()
    {
        var state = BattlefieldHeldRuneState();

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-battlefield-held-rune", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "P2-BATTLEFIELD-STAR-PEAK",
                ["P1-BATTLEFIELD-SINGLE-RUNE-ATTACKER"],
                ["P2-BATTLEFIELD-SINGLE-RUNE-DEFENDER"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.DoesNotContain(result.Events, e => e.Kind == "BATTLEFIELD_HELD");
        Assert.DoesNotContain(result.State.TriggerQueue, t => t.HeldContext is not null);
        Assert.DoesNotContain(result.State.StackItems, t => t.HeldContext is not null);
        Assert.Empty(result.State.DelayedResourceGains);
    }

    [Fact]
    public async Task P79BattlefieldStaticPowerAddsOneToBattleParticipants()
    {
        var state = BattlefieldStaticPowerState();
        var staticAuraEffects = state.ContinuousEffects
            .Where(effect => string.Equals(effect.Layer, ContinuousEffectLayers.StaticAura, StringComparison.Ordinal)
                && string.Equals(effect.SourceObjectId, "P1-BATTLEFIELD-POWER-PLUS", StringComparison.Ordinal))
            .OrderBy(effect => effect.TargetObjectId, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(2, staticAuraEffects.Length);
        Assert.All(
            staticAuraEffects,
            effect =>
            {
                Assert.Equal("BATTLEFIELD", effect.Scope);
                Assert.Equal(1, effect.PowerDelta);
                Assert.Equal("BATTLEFIELD_ALL_UNITS_POWER_PLUS_ONE", effect.EffectKind);
                Assert.Equal("OGN·294/298", effect.SourceCardNo);
                Assert.Equal("SOURCE_BATTLEFIELD_ALL_UNITS_POWER_PLUS_ONE_AND_PARTICIPANT_UNIT_AT_BATTLEFIELD", effect.Condition);
                Assert.Equal("DERIVED_FROM_CURRENT_BATTLEFIELD_OBJECT_LOCATIONS", effect.Lifecycle);
                Assert.Equal(
                    ["P1-BATTLEFIELD-STATIC-ATTACKER", "P2-BATTLEFIELD-STATIC-DEFENDER"],
                    effect.ParticipantObjectIds);
            });
        Assert.Equal(
            ["P1-BATTLEFIELD-STATIC-ATTACKER", "P2-BATTLEFIELD-STATIC-DEFENDER"],
            staticAuraEffects.Select(effect => Assert.IsType<string>(effect.TargetObjectId)).ToArray());
        AssertBattlefieldStaticAuraSnapshot(
            ResolutionResult.BuildSnapshots(state)["P1"],
            "P1-BATTLEFIELD-POWER-PLUS",
            "P1-BATTLEFIELD-STATIC-ATTACKER",
            "P2-BATTLEFIELD-STATIC-DEFENDER");

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-battlefield-static-power", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "P1-BATTLEFIELD-POWER-PLUS",
                ["P1-BATTLEFIELD-STATIC-ATTACKER"],
                ["P2-BATTLEFIELD-STATIC-DEFENDER"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted);
        var attackerDamageEvent = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "DAMAGE_APPLIED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["combatRole"] as string, "ATTACKER", StringComparison.Ordinal));
        Assert.Equal("P1-BATTLEFIELD-STATIC-ATTACKER", attackerDamageEvent.Payload["sourceObjectId"]);
        Assert.Equal("P2-BATTLEFIELD-STATIC-DEFENDER", attackerDamageEvent.Payload["targetObjectId"]);
        Assert.Equal(2, attackerDamageEvent.Payload["basePower"]);
        Assert.Equal(1, attackerDamageEvent.Payload["staticPowerBonus"]);
        Assert.Equal(3, attackerDamageEvent.Payload["combatPower"]);
        Assert.Equal(3, attackerDamageEvent.Payload["damage"]);

        var defenderDamageEvent = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "DAMAGE_APPLIED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["combatRole"] as string, "DEFENDER", StringComparison.Ordinal));
        Assert.Equal("P2-BATTLEFIELD-STATIC-DEFENDER", defenderDamageEvent.Payload["sourceObjectId"]);
        Assert.Equal("P1-BATTLEFIELD-STATIC-ATTACKER", defenderDamageEvent.Payload["targetObjectId"]);
        Assert.Equal(3, defenderDamageEvent.Payload["basePower"]);
        Assert.Equal(1, defenderDamageEvent.Payload["staticPowerBonus"]);
        Assert.Equal(4, defenderDamageEvent.Payload["combatPower"]);
        Assert.Equal(4, defenderDamageEvent.Payload["damage"]);
        Assert.Contains("P1-BATTLEFIELD-POWER-PLUS", result.State.PlayerZones["P1"].Battlefields);
        Assert.Equal(["P1-BATTLEFIELD-STATIC-ATTACKER"], result.State.PlayerZones["P1"].Graveyard);
        Assert.Equal(["P2-BATTLEFIELD-STATIC-DEFENDER"], result.State.PlayerZones["P2"].Graveyard);
        Assert.DoesNotContain(
            result.State.ContinuousEffects,
            effect => string.Equals(effect.Layer, ContinuousEffectLayers.StaticAura, StringComparison.Ordinal)
                && string.Equals(effect.SourceObjectId, "P1-BATTLEFIELD-POWER-PLUS", StringComparison.Ordinal));
    }

    [Fact]
    public async Task P79BattlefieldAllUnitsStaticPowerDoesNotProjectFromFaceDownBattlefieldSource()
    {
        var hiddenSourceState = BattlefieldStaticPowerState(battlefieldSourceFaceDown: true);

        Assert.DoesNotContain(
            hiddenSourceState.ContinuousEffects,
            effect => string.Equals(effect.Layer, ContinuousEffectLayers.StaticAura, StringComparison.Ordinal)
                && string.Equals(effect.SourceObjectId, "P1-BATTLEFIELD-POWER-PLUS", StringComparison.Ordinal));

        var result = await new CoreRuleEngine().ResolveAsync(
            hiddenSourceState,
            new PlayerIntent("intent-p7-9-battlefield-all-units-static-power-face-down-source", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "P1-BATTLEFIELD-POWER-PLUS",
                ["P1-BATTLEFIELD-STATIC-ATTACKER"],
                ["P2-BATTLEFIELD-STATIC-DEFENDER"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        var attackerDamageEvent = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "DAMAGE_APPLIED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["combatRole"] as string, "ATTACKER", StringComparison.Ordinal));
        Assert.Equal("P1-BATTLEFIELD-STATIC-ATTACKER", attackerDamageEvent.Payload["sourceObjectId"]);
        Assert.Equal(2, attackerDamageEvent.Payload["basePower"]);
        Assert.False(attackerDamageEvent.Payload.ContainsKey("staticPowerBonus"));
        Assert.Equal(2, attackerDamageEvent.Payload["combatPower"]);

        var defenderDamageEvent = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "DAMAGE_APPLIED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["combatRole"] as string, "DEFENDER", StringComparison.Ordinal));
        Assert.Equal("P2-BATTLEFIELD-STATIC-DEFENDER", defenderDamageEvent.Payload["sourceObjectId"]);
        Assert.Equal(3, defenderDamageEvent.Payload["basePower"]);
        Assert.False(defenderDamageEvent.Payload.ContainsKey("staticPowerBonus"));
        Assert.Equal(3, defenderDamageEvent.Payload["combatPower"]);
    }

    [Fact]
    public void P79BattlefieldAllUnitsStaticPowerDoesNotProjectToFaceDownTarget()
    {
        var hiddenTargetState = BattlefieldStaticPowerState(defenderFaceDown: true);

        Assert.DoesNotContain(
            hiddenTargetState.ContinuousEffects,
            effect => string.Equals(effect.Layer, ContinuousEffectLayers.StaticAura, StringComparison.Ordinal)
                && string.Equals(effect.SourceObjectId, "P1-BATTLEFIELD-POWER-PLUS", StringComparison.Ordinal)
                && string.Equals(effect.TargetObjectId, "P2-BATTLEFIELD-STATIC-DEFENDER", StringComparison.Ordinal));
    }

    [Fact]
    public async Task P79SameBattlefieldOtherFriendlyStaticPowerAddsOneToOnlyOtherFriendlyUnits()
    {
        var state = SameBattlefieldOtherFriendlyStaticPowerState();
        var staticAura = Assert.Single(state.ContinuousEffects, effect =>
            string.Equals(effect.Layer, ContinuousEffectLayers.StaticAura, StringComparison.Ordinal)
            && string.Equals(effect.SourceObjectId, "P1-GAREN-SOURCE", StringComparison.Ordinal));
        Assert.Equal("BATTLEFIELD", staticAura.Scope);
        Assert.Equal("P1-GAREN-ALLY", staticAura.TargetObjectId);
        Assert.Equal(1, staticAura.PowerDelta);
        Assert.Equal("SAME_BATTLEFIELD_OTHER_FRIENDLY_UNITS_POWER_PLUS_ONE", staticAura.EffectKind);
        Assert.Equal("OGS·013/024", staticAura.SourceCardNo);
        Assert.Equal("CoreRuleEngine.ResolveSameBattlefieldOtherFriendlyUnitsPowerBonus", staticAura.SourcePath);
        Assert.Equal("SOURCE_AND_OTHER_FRIENDLY_PUBLIC_UNITS_AT_SAME_BATTLEFIELD", staticAura.Condition);
        Assert.Equal("DERIVED_FROM_CURRENT_SAME_BATTLEFIELD_FRIENDLY_UNIT_LOCATIONS", staticAura.Lifecycle);
        Assert.Equal(["P1-GAREN-ALLY"], staticAura.ParticipantObjectIds);
        Assert.DoesNotContain(
            state.ContinuousEffects,
            effect => string.Equals(effect.SourceObjectId, "P1-GAREN-SOURCE", StringComparison.Ordinal)
                && string.Equals(effect.TargetObjectId, "P1-GAREN-SOURCE", StringComparison.Ordinal));
        Assert.DoesNotContain(
            state.ContinuousEffects,
            effect => string.Equals(effect.SourceObjectId, "P1-GAREN-SOURCE", StringComparison.Ordinal)
                && string.Equals(effect.TargetObjectId, "P2-GAREN-DEFENDER", StringComparison.Ordinal));
        Assert.DoesNotContain(
            state.ContinuousEffects,
            effect => string.Equals(effect.SourceObjectId, "P1-GAREN-SOURCE", StringComparison.Ordinal)
                && string.Equals(effect.TargetObjectId, "P1-GAREN-OTHER-BATTLEFIELD-ALLY", StringComparison.Ordinal));

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-same-battlefield-other-friendly-static-power", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "P1-GAREN-BATTLEFIELD",
                ["P1-GAREN-ALLY"],
                ["P2-GAREN-DEFENDER"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        result = await CombatTestDriver.FinishAsync(result);
        var attackerDamageEvent = CombatTestDriver.DamageFrom(result, "P1-GAREN-ALLY");
        Assert.Equal(2, attackerDamageEvent.Payload["basePower"]);
        Assert.Equal(1, attackerDamageEvent.Payload["staticPowerBonus"]);
        Assert.Equal(3, attackerDamageEvent.Payload["combatPower"]);
        Assert.Equal(3, attackerDamageEvent.Payload["damage"]);

        var defenderDamageEvent = CombatTestDriver.DamageFrom(result, "P2-GAREN-DEFENDER");
        Assert.Equal(5, defenderDamageEvent.Payload["basePower"]);
        Assert.False(defenderDamageEvent.Payload.ContainsKey("staticPowerBonus"));
        Assert.Equal(5, defenderDamageEvent.Payload["combatPower"]);
        Assert.Equal(5, defenderDamageEvent.Payload["damage"]);
    }

    [Fact]
    public async Task P79SameBattlefieldOtherFriendlyStaticPowerDoesNotProjectFromStandbySource()
    {
        var hiddenSourceState = SameBattlefieldOtherFriendlyStaticPowerState(sourceStandby: true);

        Assert.DoesNotContain(
            hiddenSourceState.ContinuousEffects,
            effect => string.Equals(effect.Layer, ContinuousEffectLayers.StaticAura, StringComparison.Ordinal)
                && string.Equals(effect.SourceObjectId, "P1-GAREN-SOURCE", StringComparison.Ordinal));

        var result = await new CoreRuleEngine().ResolveAsync(
            hiddenSourceState,
            new PlayerIntent("intent-p7-9-same-battlefield-other-friendly-static-power-standby-source", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "P1-GAREN-BATTLEFIELD",
                ["P1-GAREN-ALLY"],
                ["P2-GAREN-DEFENDER"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        result = await CombatTestDriver.FinishAsync(result);
        var attackerDamageEvent = CombatTestDriver.DamageFrom(result, "P1-GAREN-ALLY");
        Assert.Equal(2, attackerDamageEvent.Payload["basePower"]);
        Assert.False(attackerDamageEvent.Payload.ContainsKey("staticPowerBonus"));
        Assert.Equal(2, attackerDamageEvent.Payload["combatPower"]);
    }

    [Fact]
    public void P79SameBattlefieldOtherFriendlyStaticPowerDoesNotProjectToStandbyTarget()
    {
        var hiddenTargetState = SameBattlefieldOtherFriendlyStaticPowerState(allyStandby: true);

        Assert.DoesNotContain(
            hiddenTargetState.ContinuousEffects,
            effect => string.Equals(effect.Layer, ContinuousEffectLayers.StaticAura, StringComparison.Ordinal)
                && string.Equals(effect.SourceObjectId, "P1-GAREN-SOURCE", StringComparison.Ordinal)
                && string.Equals(effect.TargetObjectId, "P1-GAREN-ALLY", StringComparison.Ordinal));
    }

    [Fact]
    public async Task P79SameBattlefieldOtherFriendlyStaticKeywordGrantsAssaultToOnlyOtherFriendlyAttackers()
    {
        var state = PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields =
                    [
                        "P1-FARRON-BATTLEFIELD",
                        "P1-FARRON-SOURCE",
                        "P1-FARRON-ALLY",
                        "P1-FARRON-OTHER-BATTLEFIELD",
                        "P1-FARRON-OTHER-BATTLEFIELD-ALLY"
                    ]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-FARRON-DEFENDER"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-FARRON-BATTLEFIELD"] = new(
                    "P1-FARRON-BATTLEFIELD",
                    cardNo: "UNL-208/219",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-FARRON-SOURCE"] = new(
                    "P1-FARRON-SOURCE",
                    cardNo: "OGN·015/298",
                    power: 5,
                    tags: [CardObjectTags.UnitCard, "崔法利"],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-FARRON-ALLY"] = new(
                    "P1-FARRON-ALLY",
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-FARRON-OTHER-BATTLEFIELD"] = new(
                    "P1-FARRON-OTHER-BATTLEFIELD",
                    cardNo: "OGN·275/298",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-FARRON-OTHER-BATTLEFIELD-ALLY"] = new(
                    "P1-FARRON-OTHER-BATTLEFIELD-ALLY",
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-FARRON-DEFENDER"] = new(
                    "P2-FARRON-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 5,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2")
            },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                ["P1-FARRON-BATTLEFIELD"] = new("P1", "BATTLEFIELD", "P1-FARRON-BATTLEFIELD"),
                ["P1-FARRON-SOURCE"] = new("P1", "BATTLEFIELD", "P1-FARRON-BATTLEFIELD"),
                ["P1-FARRON-ALLY"] = new("P1", "BATTLEFIELD", "P1-FARRON-BATTLEFIELD"),
                ["P2-FARRON-DEFENDER"] = new("P2", "BATTLEFIELD", "P1-FARRON-BATTLEFIELD"),
                ["P1-FARRON-OTHER-BATTLEFIELD"] = new("P1", "BATTLEFIELD", "P1-FARRON-OTHER-BATTLEFIELD"),
                ["P1-FARRON-OTHER-BATTLEFIELD-ALLY"] = new("P1", "BATTLEFIELD", "P1-FARRON-OTHER-BATTLEFIELD")
            },
            UntilEndOfTurnEffects = [BattlefieldTaskMarkers.SpellDuelCompleted("P1-FARRON-BATTLEFIELD")]
        };

        var ruleTextAura = Assert.Single(state.ContinuousEffects, effect =>
            string.Equals(effect.Layer, ContinuousEffectLayers.RuleText, StringComparison.Ordinal)
            && string.Equals(effect.SourceObjectId, "P1-FARRON-SOURCE", StringComparison.Ordinal));
        Assert.Equal("OBJECT", ruleTextAura.Scope);
        Assert.Equal("WHILE_SOURCE_AND_TARGET_AT_SAME_BATTLEFIELD", ruleTextAura.Duration);
        Assert.Equal("P1-FARRON-ALLY", ruleTextAura.TargetObjectId);
        Assert.Equal(0, ruleTextAura.PowerDelta);
        Assert.Equal(0, ruleTextAura.BasePower);
        Assert.Equal(0, ruleTextAura.EffectivePower);
        Assert.Empty(ruleTextAura.EffectKind);
        Assert.Null(ruleTextAura.SourceCardNo);
        Assert.Empty(ruleTextAura.SourcePath);
        Assert.Null(ruleTextAura.ParticipantObjectIds);
        Assert.Null(ruleTextAura.SourceOrder);
        Assert.DoesNotContain(
            state.ContinuousEffects,
            effect => string.Equals(effect.SourceObjectId, "P1-FARRON-SOURCE", StringComparison.Ordinal)
                && string.Equals(effect.TargetObjectId, "P1-FARRON-SOURCE", StringComparison.Ordinal));
        Assert.DoesNotContain(
            state.ContinuousEffects,
            effect => string.Equals(effect.SourceObjectId, "P1-FARRON-SOURCE", StringComparison.Ordinal)
                && string.Equals(effect.TargetObjectId, "P2-FARRON-DEFENDER", StringComparison.Ordinal));
        Assert.DoesNotContain(
            state.ContinuousEffects,
            effect => string.Equals(effect.SourceObjectId, "P1-FARRON-SOURCE", StringComparison.Ordinal)
                && string.Equals(effect.TargetObjectId, "P1-FARRON-OTHER-BATTLEFIELD-ALLY", StringComparison.Ordinal));

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-same-battlefield-static-keyword", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "P1-FARRON-BATTLEFIELD",
                ["P1-FARRON-ALLY"],
                ["P2-FARRON-DEFENDER"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        result = await CombatTestDriver.FinishAsync(result);
        var attackerDamageEvent = CombatTestDriver.DamageFrom(result, "P1-FARRON-ALLY");
        Assert.Equal(2, attackerDamageEvent.Payload["basePower"]);
        Assert.Equal(1, attackerDamageEvent.Payload["keywordBonus"]);
        Assert.False(attackerDamageEvent.Payload.ContainsKey("staticPowerBonus"));
        Assert.Equal(3, attackerDamageEvent.Payload["combatPower"]);
        Assert.Equal(3, attackerDamageEvent.Payload["damage"]);

        var defenderDamageEvent = CombatTestDriver.DamageFrom(result, "P2-FARRON-DEFENDER");
        Assert.Equal(5, defenderDamageEvent.Payload["basePower"]);
        Assert.Equal(0, defenderDamageEvent.Payload["keywordBonus"]);
        Assert.Equal(5, defenderDamageEvent.Payload["combatPower"]);
        Assert.Equal(5, defenderDamageEvent.Payload["damage"]);
    }

    [Fact]
    public async Task P79SameBattlefieldStaticKeywordGrantUsesCurrentControllerForFriendlyScope()
    {
        var state = PunishmentState(mana: 0) with
        {
            ActivePlayerId = "P2",
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields =
                    [
                        "P1-FARRON-CONTROL-BATTLEFIELD",
                        "P1-FARRON-CONTROLLED-SOURCE",
                        "P1-FARRON-CONTROL-DEFENDER"
                    ]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-FARRON-CONTROL-ATTACKER"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-FARRON-CONTROL-BATTLEFIELD"] = new(
                    "P1-FARRON-CONTROL-BATTLEFIELD",
                    cardNo: "UNL-208/219",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-FARRON-CONTROLLED-SOURCE"] = new(
                    "P1-FARRON-CONTROLLED-SOURCE",
                    cardNo: "OGN·015/298",
                    power: 5,
                    tags: [CardObjectTags.UnitCard, "崔法利"],
                    ownerId: "P1",
                    controllerId: "P2"),
                ["P1-FARRON-CONTROL-DEFENDER"] = new(
                    "P1-FARRON-CONTROL-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 5,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-FARRON-CONTROL-ATTACKER"] = new(
                    "P2-FARRON-CONTROL-ATTACKER",
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2")
            },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                ["P1-FARRON-CONTROL-BATTLEFIELD"] = new("P1", "BATTLEFIELD", "P1-FARRON-CONTROL-BATTLEFIELD"),
                ["P1-FARRON-CONTROLLED-SOURCE"] = new("P1", "BATTLEFIELD", "P1-FARRON-CONTROL-BATTLEFIELD"),
                ["P1-FARRON-CONTROL-DEFENDER"] = new("P1", "BATTLEFIELD", "P1-FARRON-CONTROL-BATTLEFIELD"),
                ["P2-FARRON-CONTROL-ATTACKER"] = new("P2", "BATTLEFIELD", "P1-FARRON-CONTROL-BATTLEFIELD")
            },
            UntilEndOfTurnEffects = [BattlefieldTaskMarkers.SpellDuelCompleted("P1-FARRON-CONTROL-BATTLEFIELD")]
        };

        var ruleTextAura = Assert.Single(state.ContinuousEffects, effect =>
            string.Equals(effect.Layer, ContinuousEffectLayers.RuleText, StringComparison.Ordinal)
            && string.Equals(effect.SourceObjectId, "P1-FARRON-CONTROLLED-SOURCE", StringComparison.Ordinal));
        Assert.Equal("P2-FARRON-CONTROL-ATTACKER", ruleTextAura.TargetObjectId);
        Assert.DoesNotContain(
            state.ContinuousEffects,
            effect => string.Equals(effect.SourceObjectId, "P1-FARRON-CONTROLLED-SOURCE", StringComparison.Ordinal)
                && string.Equals(effect.TargetObjectId, "P1-FARRON-CONTROL-DEFENDER", StringComparison.Ordinal));

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-same-battlefield-static-keyword-control-scope", "P2", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "P1-FARRON-CONTROL-BATTLEFIELD",
                ["P2-FARRON-CONTROL-ATTACKER"],
                ["P1-FARRON-CONTROL-DEFENDER"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        var attackerDamageEvent = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "DAMAGE_APPLIED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["combatRole"] as string, "ATTACKER", StringComparison.Ordinal));
        Assert.Equal("P2-FARRON-CONTROL-ATTACKER", attackerDamageEvent.Payload["sourceObjectId"]);
        Assert.Equal(2, attackerDamageEvent.Payload["basePower"]);
        Assert.Equal(1, attackerDamageEvent.Payload["keywordBonus"]);
        Assert.Equal(CardCombatKeywordNames.Assault, attackerDamageEvent.Payload["keyword"]);
        Assert.Equal(3, attackerDamageEvent.Payload["combatPower"]);

        var defenderDamageEvent = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "DAMAGE_APPLIED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["combatRole"] as string, "DEFENDER", StringComparison.Ordinal));
        Assert.Equal("P1-FARRON-CONTROL-DEFENDER", defenderDamageEvent.Payload["sourceObjectId"]);
        Assert.Equal(0, defenderDamageEvent.Payload["keywordBonus"]);
        Assert.Equal(5, defenderDamageEvent.Payload["combatPower"]);
    }

    [Fact]
    public async Task P79SameBattlefieldOtherFriendlyStaticKeywordGrantsSteadfastToOnlyOtherFriendlyDefenders()
    {
        var state = PunishmentState(mana: 0) with
        {
            ActivePlayerId = "P2",
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields =
                    [
                        "P1-TARIC-BATTLEFIELD",
                        "P1-TARIC-SOURCE",
                        "P1-TARIC-ALLY",
                        "P1-TARIC-OTHER-BATTLEFIELD",
                        "P1-TARIC-OTHER-BATTLEFIELD-ALLY"
                    ]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-TARIC-ATTACKER"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-TARIC-BATTLEFIELD"] = new(
                    "P1-TARIC-BATTLEFIELD",
                    cardNo: "UNL-208/219",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-TARIC-SOURCE"] = new(
                    "P1-TARIC-SOURCE",
                    cardNo: "OGN·074/298",
                    power: 4,
                    tags: [CardObjectTags.UnitCard, CardCombatKeywordNames.Steadfast, CardCombatKeywordNames.Bulwark],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-TARIC-ALLY"] = new(
                    "P1-TARIC-ALLY",
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-TARIC-OTHER-BATTLEFIELD"] = new(
                    "P1-TARIC-OTHER-BATTLEFIELD",
                    cardNo: "OGN·275/298",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-TARIC-OTHER-BATTLEFIELD-ALLY"] = new(
                    "P1-TARIC-OTHER-BATTLEFIELD-ALLY",
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-TARIC-ATTACKER"] = new(
                    "P2-TARIC-ATTACKER",
                    cardNo: "SFD·125/221",
                    power: 5,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2")
            },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                ["P1-TARIC-BATTLEFIELD"] = new("P1", "BATTLEFIELD", "P1-TARIC-BATTLEFIELD"),
                ["P1-TARIC-SOURCE"] = new("P1", "BATTLEFIELD", "P1-TARIC-BATTLEFIELD"),
                ["P1-TARIC-ALLY"] = new("P1", "BATTLEFIELD", "P1-TARIC-BATTLEFIELD"),
                ["P2-TARIC-ATTACKER"] = new("P2", "BATTLEFIELD", "P1-TARIC-BATTLEFIELD"),
                ["P1-TARIC-OTHER-BATTLEFIELD"] = new("P1", "BATTLEFIELD", "P1-TARIC-OTHER-BATTLEFIELD"),
                ["P1-TARIC-OTHER-BATTLEFIELD-ALLY"] = new("P1", "BATTLEFIELD", "P1-TARIC-OTHER-BATTLEFIELD")
            },
            UntilEndOfTurnEffects = [BattlefieldTaskMarkers.SpellDuelCompleted("P1-TARIC-BATTLEFIELD")]
        };

        var ruleTextAura = Assert.Single(state.ContinuousEffects, effect =>
            string.Equals(effect.Layer, ContinuousEffectLayers.RuleText, StringComparison.Ordinal)
            && string.Equals(effect.SourceObjectId, "P1-TARIC-SOURCE", StringComparison.Ordinal));
        Assert.Equal("OBJECT", ruleTextAura.Scope);
        Assert.Equal("WHILE_SOURCE_AND_TARGET_AT_SAME_BATTLEFIELD", ruleTextAura.Duration);
        Assert.Equal("P1-TARIC-ALLY", ruleTextAura.TargetObjectId);
        Assert.Equal(0, ruleTextAura.PowerDelta);
        Assert.Equal(0, ruleTextAura.BasePower);
        Assert.Equal(0, ruleTextAura.EffectivePower);
        Assert.Empty(ruleTextAura.EffectKind);
        Assert.Null(ruleTextAura.SourceCardNo);
        Assert.Empty(ruleTextAura.SourcePath);
        Assert.Null(ruleTextAura.ParticipantObjectIds);
        Assert.Null(ruleTextAura.SourceOrder);
        Assert.DoesNotContain(
            state.ContinuousEffects,
            effect => string.Equals(effect.SourceObjectId, "P1-TARIC-SOURCE", StringComparison.Ordinal)
                && string.Equals(effect.TargetObjectId, "P1-TARIC-SOURCE", StringComparison.Ordinal));
        Assert.DoesNotContain(
            state.ContinuousEffects,
            effect => string.Equals(effect.SourceObjectId, "P1-TARIC-SOURCE", StringComparison.Ordinal)
                && string.Equals(effect.TargetObjectId, "P2-TARIC-ATTACKER", StringComparison.Ordinal));
        Assert.DoesNotContain(
            state.ContinuousEffects,
            effect => string.Equals(effect.SourceObjectId, "P1-TARIC-SOURCE", StringComparison.Ordinal)
                && string.Equals(effect.TargetObjectId, "P1-TARIC-OTHER-BATTLEFIELD-ALLY", StringComparison.Ordinal));

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-same-battlefield-static-steadfast-keyword", "P2", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "P1-TARIC-BATTLEFIELD",
                ["P2-TARIC-ATTACKER"],
                ["P1-TARIC-ALLY"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        result = await CombatTestDriver.FinishAsync(result);
        var attackerDamageEvent = CombatTestDriver.DamageFrom(result, "P2-TARIC-ATTACKER");
        Assert.Equal(5, attackerDamageEvent.Payload["basePower"]);
        Assert.Equal(0, attackerDamageEvent.Payload["keywordBonus"]);
        Assert.False(attackerDamageEvent.Payload.ContainsKey("staticPowerBonus"));
        Assert.Equal(5, attackerDamageEvent.Payload["combatPower"]);
        Assert.Equal(5, attackerDamageEvent.Payload["damage"]);

        var defenderDamageEvent = CombatTestDriver.DamageFrom(result, "P1-TARIC-ALLY");
        Assert.Equal("P2-TARIC-ATTACKER", defenderDamageEvent.Payload["targetObjectId"]);
        Assert.Equal(2, defenderDamageEvent.Payload["basePower"]);
        Assert.Equal(1, defenderDamageEvent.Payload["keywordBonus"]);
        Assert.Equal(CardCombatKeywordNames.Steadfast, defenderDamageEvent.Payload["keyword"]);
        Assert.Equal(3, defenderDamageEvent.Payload["combatPower"]);
        Assert.Equal(3, defenderDamageEvent.Payload["damage"]);
    }

    [Fact]
    public async Task P79SameBattlefieldStaticKeywordGrantExpiresWhenSourceLeavesBattlefield()
    {
        var activeAuraState = TaricSameBattlefieldKeywordGrantLifecycleState(sourceOnBattlefield: true);
        Assert.Single(activeAuraState.ContinuousEffects, effect =>
            string.Equals(effect.Layer, ContinuousEffectLayers.RuleText, StringComparison.Ordinal)
            && string.Equals(effect.SourceObjectId, "P1-TARIC-LIFECYCLE-SOURCE", StringComparison.Ordinal)
            && string.Equals(effect.TargetObjectId, "P1-TARIC-LIFECYCLE-ALLY", StringComparison.Ordinal));

        var activeResult = await ResolveTaricLifecycleBattleAsync(
            activeAuraState,
            "intent-p7-9-taric-lifecycle-active",
            "P1-TARIC-LIFECYCLE-BATTLEFIELD");
        activeResult = await CombatTestDriver.FinishAsync(activeResult);
        var activeDefenderDamage = CombatTestDriver.DamageFrom(activeResult, "P1-TARIC-LIFECYCLE-ALLY");
        Assert.Equal(1, activeDefenderDamage.Payload["keywordBonus"]);
        Assert.Equal(CardCombatKeywordNames.Steadfast, activeDefenderDamage.Payload["keyword"]);
        Assert.Equal(3, activeDefenderDamage.Payload["combatPower"]);

        var expiredAuraState = TaricSameBattlefieldKeywordGrantLifecycleState(sourceOnBattlefield: false);
        Assert.DoesNotContain(
            expiredAuraState.ContinuousEffects,
            effect => string.Equals(effect.Layer, ContinuousEffectLayers.RuleText, StringComparison.Ordinal)
                && string.Equals(effect.SourceObjectId, "P1-TARIC-LIFECYCLE-SOURCE", StringComparison.Ordinal));

        var expiredResult = await ResolveTaricLifecycleBattleAsync(
            expiredAuraState,
            "intent-p7-9-taric-lifecycle-expired",
            "P1-TARIC-LIFECYCLE-BATTLEFIELD");
        expiredResult = await CombatTestDriver.FinishAsync(expiredResult);
        var expiredDefenderDamage = CombatTestDriver.DamageFrom(expiredResult, "P1-TARIC-LIFECYCLE-ALLY");
        Assert.Equal(0, expiredDefenderDamage.Payload["keywordBonus"]);
        Assert.Equal(2, expiredDefenderDamage.Payload["combatPower"]);
    }

    [Fact]
    public async Task P79SameBattlefieldStaticKeywordGrantExpiresWhenTargetMovesToAnotherBattlefield()
    {
        var activeAuraState = TaricSameBattlefieldKeywordGrantLifecycleState(
            sourceOnBattlefield: true,
            targetAtSameBattlefield: true);
        Assert.Single(activeAuraState.ContinuousEffects, effect =>
            string.Equals(effect.Layer, ContinuousEffectLayers.RuleText, StringComparison.Ordinal)
            && string.Equals(effect.SourceObjectId, "P1-TARIC-LIFECYCLE-SOURCE", StringComparison.Ordinal)
            && string.Equals(effect.TargetObjectId, "P1-TARIC-LIFECYCLE-ALLY", StringComparison.Ordinal));

        var activeResult = await ResolveTaricLifecycleBattleAsync(
            activeAuraState,
            "intent-p7-9-taric-target-lifecycle-active",
            "P1-TARIC-LIFECYCLE-BATTLEFIELD");
        activeResult = await CombatTestDriver.FinishAsync(activeResult);
        var activeDefenderDamage = CombatTestDriver.DamageFrom(activeResult, "P1-TARIC-LIFECYCLE-ALLY");
        Assert.Equal(1, activeDefenderDamage.Payload["keywordBonus"]);
        Assert.Equal(3, activeDefenderDamage.Payload["combatPower"]);

        var movedTargetState = TaricSameBattlefieldKeywordGrantLifecycleState(
            sourceOnBattlefield: true,
            targetAtSameBattlefield: false);
        Assert.DoesNotContain(
            movedTargetState.ContinuousEffects,
            effect => string.Equals(effect.Layer, ContinuousEffectLayers.RuleText, StringComparison.Ordinal)
                && string.Equals(effect.SourceObjectId, "P1-TARIC-LIFECYCLE-SOURCE", StringComparison.Ordinal)
                && string.Equals(effect.TargetObjectId, "P1-TARIC-LIFECYCLE-ALLY", StringComparison.Ordinal));

        var movedResult = await ResolveTaricLifecycleBattleAsync(
            movedTargetState,
            "intent-p7-9-taric-target-lifecycle-moved",
            "P1-TARIC-LIFECYCLE-OTHER-BATTLEFIELD");
        movedResult = await CombatTestDriver.FinishAsync(movedResult);
        var movedDefenderDamage = CombatTestDriver.DamageFrom(movedResult, "P1-TARIC-LIFECYCLE-ALLY");
        Assert.Equal(0, movedDefenderDamage.Payload["keywordBonus"]);
        Assert.Equal(2, movedDefenderDamage.Payload["combatPower"]);
    }

    [Fact]
    public async Task P79SameBattlefieldStaticKeywordGrantDoesNotProjectFromFaceDownSource()
    {
        var hiddenSourceState = TaricSameBattlefieldKeywordGrantLifecycleState(
            sourceOnBattlefield: true,
            targetAtSameBattlefield: true,
            sourceFaceDown: true);

        Assert.DoesNotContain(
            hiddenSourceState.ContinuousEffects,
            effect => string.Equals(effect.Layer, ContinuousEffectLayers.RuleText, StringComparison.Ordinal)
                && string.Equals(effect.SourceObjectId, "P1-TARIC-LIFECYCLE-SOURCE", StringComparison.Ordinal));

        var result = await ResolveTaricLifecycleBattleAsync(
            hiddenSourceState,
            "intent-p7-9-taric-face-down-source",
            "P1-TARIC-LIFECYCLE-BATTLEFIELD");
        var defenderDamage = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "DAMAGE_APPLIED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["combatRole"] as string, "DEFENDER", StringComparison.Ordinal));
        Assert.Equal("P1-TARIC-LIFECYCLE-ALLY", defenderDamage.Payload["sourceObjectId"]);
        Assert.Equal(0, defenderDamage.Payload["keywordBonus"]);
        Assert.Equal(2, defenderDamage.Payload["combatPower"]);
    }

    [Fact]
    public async Task P79SameBattlefieldStaticKeywordGrantDoesNotProjectFromStandbySource()
    {
        var standbySourceState = TaricSameBattlefieldKeywordGrantLifecycleState(
            sourceOnBattlefield: true,
            targetAtSameBattlefield: true,
            sourceStandby: true);

        Assert.DoesNotContain(
            standbySourceState.ContinuousEffects,
            effect => string.Equals(effect.Layer, ContinuousEffectLayers.RuleText, StringComparison.Ordinal)
                && string.Equals(effect.SourceObjectId, "P1-TARIC-LIFECYCLE-SOURCE", StringComparison.Ordinal));

        var result = await ResolveTaricLifecycleBattleAsync(
            standbySourceState,
            "intent-p7-9-taric-standby-source",
            "P1-TARIC-LIFECYCLE-BATTLEFIELD");
        result = await CombatTestDriver.FinishAsync(result);
        var defenderDamage = CombatTestDriver.DamageFrom(result, "P1-TARIC-LIFECYCLE-ALLY");
        Assert.Equal(0, defenderDamage.Payload["keywordBonus"]);
        Assert.Equal(2, defenderDamage.Payload["combatPower"]);
    }

    [Fact]
    public void P79SameBattlefieldStaticKeywordGrantDoesNotProjectToFaceDownTarget()
    {
        var hiddenTargetState = TaricSameBattlefieldKeywordGrantLifecycleState(
            sourceOnBattlefield: true,
            targetAtSameBattlefield: true,
            targetFaceDown: true);

        Assert.DoesNotContain(
            hiddenTargetState.ContinuousEffects,
            effect => string.Equals(effect.Layer, ContinuousEffectLayers.RuleText, StringComparison.Ordinal)
                && string.Equals(effect.SourceObjectId, "P1-TARIC-LIFECYCLE-SOURCE", StringComparison.Ordinal));
    }

    private static async ValueTask<ResolutionResult> ResolveTaricLifecycleBattleAsync(
        MatchState state,
        string intentId,
        string battlefieldObjectId)
    {
        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent(intentId, "P2", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                battlefieldObjectId,
                ["P2-TARIC-LIFECYCLE-ATTACKER"],
                ["P1-TARIC-LIFECYCLE-ALLY"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        return result;
    }

    private static MatchState TaricSameBattlefieldKeywordGrantLifecycleState(
        bool sourceOnBattlefield,
        bool targetAtSameBattlefield = true,
        bool sourceFaceDown = false,
        bool targetFaceDown = false,
        bool sourceStandby = false)
    {
        var targetBattlefieldObjectId = targetAtSameBattlefield
            ? "P1-TARIC-LIFECYCLE-BATTLEFIELD"
            : "P1-TARIC-LIFECYCLE-OTHER-BATTLEFIELD";
        var p1Battlefields = new List<string>
        {
            "P1-TARIC-LIFECYCLE-BATTLEFIELD",
            "P1-TARIC-LIFECYCLE-OTHER-BATTLEFIELD",
            "P1-TARIC-LIFECYCLE-ALLY"
        };
        if (sourceOnBattlefield)
        {
            p1Battlefields.Insert(1, "P1-TARIC-LIFECYCLE-SOURCE");
        }

        var p1Graveyard = sourceOnBattlefield
            ? []
            : new[] { "P1-TARIC-LIFECYCLE-SOURCE" };
        var sourceTags = new List<string>
        {
            CardObjectTags.UnitCard,
            CardCombatKeywordNames.Steadfast,
            CardCombatKeywordNames.Bulwark
        };
        if (sourceStandby)
        {
            sourceTags.Add(CardObjectTags.Standby);
        }

        return PunishmentState(mana: 0) with
        {
            ActivePlayerId = "P2",
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = p1Battlefields,
                    Graveyard = p1Graveyard
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-TARIC-LIFECYCLE-ATTACKER"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-TARIC-LIFECYCLE-BATTLEFIELD"] = new(
                    "P1-TARIC-LIFECYCLE-BATTLEFIELD",
                    cardNo: "UNL-208/219",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-TARIC-LIFECYCLE-OTHER-BATTLEFIELD"] = new(
                    "P1-TARIC-LIFECYCLE-OTHER-BATTLEFIELD",
                    cardNo: "OGN·275/298",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-TARIC-LIFECYCLE-SOURCE"] = new(
                    "P1-TARIC-LIFECYCLE-SOURCE",
                    cardNo: "OGN·074/298",
                    isFaceDown: sourceFaceDown || sourceStandby,
                    power: 4,
                    tags: sourceTags.ToArray(),
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-TARIC-LIFECYCLE-ALLY"] = new(
                    "P1-TARIC-LIFECYCLE-ALLY",
                    cardNo: "SFD·125/221",
                    isFaceDown: targetFaceDown,
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-TARIC-LIFECYCLE-ATTACKER"] = new(
                    "P2-TARIC-LIFECYCLE-ATTACKER",
                    cardNo: "SFD·125/221",
                    power: 5,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2")
            },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                ["P1-TARIC-LIFECYCLE-BATTLEFIELD"] = new("P1", "BATTLEFIELD", "P1-TARIC-LIFECYCLE-BATTLEFIELD"),
                ["P1-TARIC-LIFECYCLE-OTHER-BATTLEFIELD"] = new("P1", "BATTLEFIELD", "P1-TARIC-LIFECYCLE-OTHER-BATTLEFIELD"),
                ["P1-TARIC-LIFECYCLE-SOURCE"] = sourceOnBattlefield
                    ? new ObjectLocationState("P1", "BATTLEFIELD", "P1-TARIC-LIFECYCLE-BATTLEFIELD")
                    : new ObjectLocationState("P1", "GRAVEYARD"),
                ["P1-TARIC-LIFECYCLE-ALLY"] = new("P1", "BATTLEFIELD", targetBattlefieldObjectId),
                ["P2-TARIC-LIFECYCLE-ATTACKER"] = new("P2", "BATTLEFIELD", targetBattlefieldObjectId)
            },
            UntilEndOfTurnEffects = [BattlefieldTaskMarkers.SpellDuelCompleted(targetBattlefieldObjectId)]
        };
    }

    [Fact]
    public async Task P79SameBattlefieldOtherFriendlyFilteredStaticPowerAddsTwoToBoonUnits()
    {
        var state = SameBattlefieldOtherFriendlyFilteredStaticPowerState();
        var staticAura = Assert.Single(state.ContinuousEffects, effect =>
            string.Equals(effect.Layer, ContinuousEffectLayers.StaticAura, StringComparison.Ordinal)
            && string.Equals(effect.SourceObjectId, "P1-LEE-SIN-SOURCE", StringComparison.Ordinal));
        Assert.Equal("BATTLEFIELD", staticAura.Scope);
        Assert.Equal("P1-LEE-SIN-BOON-ALLY", staticAura.TargetObjectId);
        Assert.Equal(2, staticAura.PowerDelta);
        Assert.Equal("SAME_BATTLEFIELD_OTHER_FRIENDLY_FILTERED_UNITS_POWER", staticAura.EffectKind);
        Assert.Equal("OGN·151/298", staticAura.SourceCardNo);
        Assert.Equal("CoreRuleEngine.ResolveSameBattlefieldOtherFriendlyFilteredUnitsPowerBonus", staticAura.SourcePath);
        Assert.Equal("SOURCE_AND_OTHER_FRIENDLY_FILTERED_PUBLIC_UNITS_AT_SAME_BATTLEFIELD", staticAura.Condition);
        Assert.Equal("DERIVED_FROM_CURRENT_SAME_BATTLEFIELD_FILTERED_FRIENDLY_UNIT_LOCATIONS", staticAura.Lifecycle);
        Assert.Equal(["P1-LEE-SIN-BOON-ALLY"], staticAura.ParticipantObjectIds);
        Assert.DoesNotContain(
            state.ContinuousEffects,
            effect => string.Equals(effect.SourceObjectId, "P1-LEE-SIN-SOURCE", StringComparison.Ordinal)
                && string.Equals(effect.TargetObjectId, "P1-LEE-SIN-SOURCE", StringComparison.Ordinal));
        Assert.DoesNotContain(
            state.ContinuousEffects,
            effect => string.Equals(effect.SourceObjectId, "P1-LEE-SIN-SOURCE", StringComparison.Ordinal)
                && string.Equals(effect.TargetObjectId, "P1-LEE-SIN-NONBOON-ALLY", StringComparison.Ordinal));
        Assert.DoesNotContain(
            state.ContinuousEffects,
            effect => string.Equals(effect.SourceObjectId, "P1-LEE-SIN-SOURCE", StringComparison.Ordinal)
                && string.Equals(effect.TargetObjectId, "P1-LEE-SIN-OTHER-BATTLEFIELD-BOON-ALLY", StringComparison.Ordinal));
        Assert.DoesNotContain(
            state.ContinuousEffects,
            effect => string.Equals(effect.SourceObjectId, "P1-LEE-SIN-SOURCE", StringComparison.Ordinal)
                && string.Equals(effect.TargetObjectId, "P2-LEE-SIN-BOON-DEFENDER", StringComparison.Ordinal));

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-same-battlefield-filtered-static-power", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "P1-LEE-SIN-BATTLEFIELD",
                ["P1-LEE-SIN-BOON-ALLY"],
                ["P2-LEE-SIN-DEFENDER"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        result = await CombatTestDriver.FinishAsync(result);
        var attackerDamageEvent = CombatTestDriver.DamageFrom(result, "P1-LEE-SIN-BOON-ALLY");
        Assert.Equal(2, attackerDamageEvent.Payload["basePower"]);
        Assert.Equal(2, attackerDamageEvent.Payload["staticPowerBonus"]);
        Assert.Equal(4, attackerDamageEvent.Payload["combatPower"]);
        Assert.Equal(4, attackerDamageEvent.Payload["damage"]);

        var defenderDamageEvent = CombatTestDriver.DamageFrom(result, "P2-LEE-SIN-DEFENDER");
        Assert.Equal(5, defenderDamageEvent.Payload["basePower"]);
        Assert.False(defenderDamageEvent.Payload.ContainsKey("staticPowerBonus"));
        Assert.Equal(5, defenderDamageEvent.Payload["combatPower"]);
        Assert.Equal(5, defenderDamageEvent.Payload["damage"]);
    }

    [Fact]
    public async Task P79BattlefieldFilteredStaticPowerAddsOneToBrushUnitTypes()
    {
        var state = BattlefieldFilteredStaticPowerState();
        var staticAuras = state.ContinuousEffects
            .Where(effect => string.Equals(effect.Layer, ContinuousEffectLayers.StaticAura, StringComparison.Ordinal)
                && string.Equals(effect.SourceObjectId, "P1-BRUSH-BATTLEFIELD", StringComparison.Ordinal))
            .OrderBy(effect => effect.TargetObjectId, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(3, staticAuras.Length);
        Assert.Equal(
            ["P1-BRUSH-BIRD-ATTACKER", "P1-BRUSH-IVERN-ALLY", "P2-BRUSH-CAT-DEFENDER"],
            staticAuras.Select(effect => Assert.IsType<string>(effect.TargetObjectId)).ToArray());
        Assert.All(
            staticAuras,
            effect =>
            {
                Assert.Equal("BATTLEFIELD", effect.Scope);
                Assert.Equal(1, effect.PowerDelta);
                Assert.Equal("BATTLEFIELD_FILTERED_UNITS_POWER", effect.EffectKind);
                Assert.Equal("UNL·T03", effect.SourceCardNo);
                Assert.Equal("CoreRuleEngine.ResolveBattlefieldFilteredUnitsPowerBonus", effect.SourcePath);
                Assert.Equal("SOURCE_BATTLEFIELD_FILTERED_UNITS_POWER_AND_PARTICIPANT_UNIT_AT_BATTLEFIELD", effect.Condition);
                Assert.Equal("DERIVED_FROM_CURRENT_BATTLEFIELD_FILTERED_OBJECT_LOCATIONS", effect.Lifecycle);
                Assert.Equal(
                    ["P1-BRUSH-BIRD-ATTACKER", "P1-BRUSH-IVERN-ALLY", "P2-BRUSH-CAT-DEFENDER"],
                    effect.ParticipantObjectIds);
            });
        Assert.DoesNotContain(
            state.ContinuousEffects,
            effect => string.Equals(effect.SourceObjectId, "P1-BRUSH-BATTLEFIELD", StringComparison.Ordinal)
                && string.Equals(effect.TargetObjectId, "P1-BRUSH-NONMATCHING-ALLY", StringComparison.Ordinal));
        Assert.DoesNotContain(
            state.ContinuousEffects,
            effect => string.Equals(effect.SourceObjectId, "P1-BRUSH-BATTLEFIELD", StringComparison.Ordinal)
                && string.Equals(effect.TargetObjectId, "P1-BRUSH-OTHER-BATTLEFIELD-PORO", StringComparison.Ordinal));

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-battlefield-filtered-static-power", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "P1-BRUSH-BATTLEFIELD",
                ["P1-BRUSH-BIRD-ATTACKER"],
                ["P2-BRUSH-CAT-DEFENDER"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        result = await CombatTestDriver.FinishAsync(result);
        var attackerDamageEvent = CombatTestDriver.DamageFrom(result, "P1-BRUSH-BIRD-ATTACKER");
        Assert.Equal(2, attackerDamageEvent.Payload["basePower"]);
        Assert.Equal(1, attackerDamageEvent.Payload["staticPowerBonus"]);
        Assert.Equal(3, attackerDamageEvent.Payload["combatPower"]);
        Assert.Equal(3, attackerDamageEvent.Payload["damage"]);

        var defenderDamageEvent = CombatTestDriver.DamageFrom(result, "P2-BRUSH-CAT-DEFENDER");
        Assert.Equal(3, defenderDamageEvent.Payload["basePower"]);
        Assert.Equal(1, defenderDamageEvent.Payload["staticPowerBonus"]);
        Assert.Equal(4, defenderDamageEvent.Payload["combatPower"]);
        Assert.Equal(4, defenderDamageEvent.Payload["damage"]);
    }

    [Fact]
    public async Task P79BattlefieldFilteredStaticPowerDoesNotProjectFromFaceDownBattlefieldSource()
    {
        var hiddenSourceState = BattlefieldFilteredStaticPowerState(battlefieldSourceFaceDown: true);

        Assert.DoesNotContain(
            hiddenSourceState.ContinuousEffects,
            effect => string.Equals(effect.Layer, ContinuousEffectLayers.StaticAura, StringComparison.Ordinal)
                && string.Equals(effect.SourceObjectId, "P1-BRUSH-BATTLEFIELD", StringComparison.Ordinal));

        var result = await new CoreRuleEngine().ResolveAsync(
            hiddenSourceState,
            new PlayerIntent("intent-p7-9-battlefield-filtered-static-power-face-down-source", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "P1-BRUSH-BATTLEFIELD",
                ["P1-BRUSH-BIRD-ATTACKER"],
                ["P2-BRUSH-CAT-DEFENDER"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        result = await CombatTestDriver.FinishAsync(result);
        var attackerDamageEvent = CombatTestDriver.DamageFrom(result, "P1-BRUSH-BIRD-ATTACKER");
        Assert.Equal(2, attackerDamageEvent.Payload["basePower"]);
        Assert.False(attackerDamageEvent.Payload.ContainsKey("staticPowerBonus"));
        Assert.Equal(2, attackerDamageEvent.Payload["combatPower"]);

        var defenderDamageEvent = CombatTestDriver.DamageFrom(result, "P2-BRUSH-CAT-DEFENDER");
        Assert.Equal(3, defenderDamageEvent.Payload["basePower"]);
        Assert.False(defenderDamageEvent.Payload.ContainsKey("staticPowerBonus"));
        Assert.Equal(3, defenderDamageEvent.Payload["combatPower"]);
    }

    [Fact]
    public void P79BattlefieldFilteredStaticPowerDoesNotProjectToFaceDownTarget()
    {
        var hiddenTargetState = BattlefieldFilteredStaticPowerState(catDefenderFaceDown: true);

        Assert.DoesNotContain(
            hiddenTargetState.ContinuousEffects,
            effect => string.Equals(effect.Layer, ContinuousEffectLayers.StaticAura, StringComparison.Ordinal)
                && string.Equals(effect.SourceObjectId, "P1-BRUSH-BATTLEFIELD", StringComparison.Ordinal)
                && string.Equals(effect.TargetObjectId, "P2-BRUSH-CAT-DEFENDER", StringComparison.Ordinal));
    }

    [Fact]
    public async Task P79BattlefieldFilteredStaticKeywordGrantsSteadfastToEphemeralDefenders()
    {
        var state = BattlefieldFilteredStaticKeywordState();
        var ruleTextAuras = state.ContinuousEffects
            .Where(effect => string.Equals(effect.Layer, ContinuousEffectLayers.RuleText, StringComparison.Ordinal)
                && string.Equals(effect.SourceObjectId, "P1-BLACKFLAME-BATTLEFIELD", StringComparison.Ordinal))
            .OrderBy(effect => effect.TargetObjectId, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(2, ruleTextAuras.Length);
        Assert.Equal(
            ["P1-BLACKFLAME-EPHEMERAL-ALLY", "P2-BLACKFLAME-EPHEMERAL-DEFENDER"],
            ruleTextAuras.Select(effect => Assert.IsType<string>(effect.TargetObjectId)).ToArray());
        Assert.All(
            ruleTextAuras,
            effect =>
            {
                Assert.Equal("OBJECT", effect.Scope);
                Assert.Equal("WHILE_SOURCE_BATTLEFIELD_AND_PARTICIPANT_AT_BATTLEFIELD", effect.Duration);
                Assert.Equal(0, effect.PowerDelta);
                Assert.Equal(0, effect.BasePower);
                Assert.Equal(0, effect.EffectivePower);
                Assert.Empty(effect.EffectKind);
                Assert.Null(effect.SourceCardNo);
                Assert.Empty(effect.SourcePath);
                Assert.Null(effect.ParticipantObjectIds);
                Assert.Null(effect.SourceDependencyObjectIds);
                Assert.Null(effect.TargetDependencyObjectIds);
                Assert.Null(effect.ParticipantDependencyObjectIds);
                Assert.Null(effect.SourceOrder);
            });
        Assert.DoesNotContain(
            state.ContinuousEffects,
            effect => string.Equals(effect.SourceObjectId, "P1-BLACKFLAME-BATTLEFIELD", StringComparison.Ordinal)
                && string.Equals(effect.TargetObjectId, "P1-BLACKFLAME-NONMATCHING-ALLY", StringComparison.Ordinal));
        Assert.DoesNotContain(
            state.ContinuousEffects,
            effect => string.Equals(effect.SourceObjectId, "P1-BLACKFLAME-BATTLEFIELD", StringComparison.Ordinal)
                && string.Equals(effect.TargetObjectId, "P1-BLACKFLAME-STANDBY-ALLY", StringComparison.Ordinal));
        Assert.DoesNotContain(
            state.ContinuousEffects,
            effect => string.Equals(effect.SourceObjectId, "P1-BLACKFLAME-BATTLEFIELD", StringComparison.Ordinal)
                && string.Equals(effect.TargetObjectId, "P1-BLACKFLAME-OTHER-BATTLEFIELD-EPHEMERAL", StringComparison.Ordinal));

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-battlefield-filtered-static-keyword", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "P1-BLACKFLAME-BATTLEFIELD",
                ["P1-BLACKFLAME-ATTACKER"],
                ["P2-BLACKFLAME-EPHEMERAL-DEFENDER"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        result = await CombatTestDriver.FinishAsync(result);
        var attackerDamageEvent = CombatTestDriver.DamageFrom(result, "P1-BLACKFLAME-ATTACKER");
        Assert.Equal(2, attackerDamageEvent.Payload["basePower"]);
        Assert.Equal(0, attackerDamageEvent.Payload["keywordBonus"]);
        Assert.Equal(2, attackerDamageEvent.Payload["combatPower"]);
        Assert.Equal(2, attackerDamageEvent.Payload["damage"]);

        var defenderDamageEvent = CombatTestDriver.DamageFrom(result, "P2-BLACKFLAME-EPHEMERAL-DEFENDER");
        Assert.Equal(3, defenderDamageEvent.Payload["basePower"]);
        Assert.Equal(1, defenderDamageEvent.Payload["keywordBonus"]);
        Assert.Equal(4, defenderDamageEvent.Payload["combatPower"]);
        Assert.Equal(4, defenderDamageEvent.Payload["damage"]);
    }

    [Fact]
    public async Task P79BattlefieldFilteredStaticKeywordGrantDoesNotProjectFromFaceDownBattlefieldSource()
    {
        var hiddenSourceState = BattlefieldFilteredStaticKeywordState(battlefieldSourceFaceDown: true);

        Assert.DoesNotContain(
            hiddenSourceState.ContinuousEffects,
            effect => string.Equals(effect.Layer, ContinuousEffectLayers.RuleText, StringComparison.Ordinal)
                && string.Equals(effect.SourceObjectId, "P1-BLACKFLAME-BATTLEFIELD", StringComparison.Ordinal));

        var result = await new CoreRuleEngine().ResolveAsync(
            hiddenSourceState,
            new PlayerIntent("intent-p7-9-battlefield-filtered-static-keyword-face-down-source", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "P1-BLACKFLAME-BATTLEFIELD",
                ["P1-BLACKFLAME-ATTACKER"],
                ["P2-BLACKFLAME-EPHEMERAL-DEFENDER"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        result = await CombatTestDriver.FinishAsync(result);
        var defenderDamageEvent = CombatTestDriver.DamageFrom(result, "P2-BLACKFLAME-EPHEMERAL-DEFENDER");
        Assert.Equal(3, defenderDamageEvent.Payload["basePower"]);
        Assert.Equal(0, defenderDamageEvent.Payload["keywordBonus"]);
        Assert.Equal(3, defenderDamageEvent.Payload["combatPower"]);
    }

    [Fact]
    public void P79BattlefieldFilteredStaticKeywordGrantDoesNotProjectToFaceDownTarget()
    {
        var hiddenTargetState = BattlefieldFilteredStaticKeywordState(ephemeralDefenderFaceDown: true);

        Assert.DoesNotContain(
            hiddenTargetState.ContinuousEffects,
            effect => string.Equals(effect.Layer, ContinuousEffectLayers.RuleText, StringComparison.Ordinal)
                && string.Equals(effect.SourceObjectId, "P1-BLACKFLAME-BATTLEFIELD", StringComparison.Ordinal)
                && string.Equals(effect.TargetObjectId, "P2-BLACKFLAME-EPHEMERAL-DEFENDER", StringComparison.Ordinal));
    }

    [Fact]
    public async Task P79OtherFriendlyStaticPowerAddsTwoAcrossPublicField()
    {
        var state = OtherFriendlyStaticPowerState();
        var staticAuras = state.ContinuousEffects
            .Where(effect => string.Equals(effect.Layer, ContinuousEffectLayers.StaticAura, StringComparison.Ordinal)
                && string.Equals(effect.SourceObjectId, "P1-BARON-SOURCE", StringComparison.Ordinal))
            .OrderBy(effect => effect.TargetObjectId, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(2, staticAuras.Length);
        Assert.Equal(
            ["P1-BARON-ALLY", "P1-BARON-BASE-ALLY"],
            staticAuras.Select(effect => Assert.IsType<string>(effect.TargetObjectId)).ToArray());
        Assert.All(
            staticAuras,
            effect =>
            {
                Assert.Equal("OBJECT", effect.Scope);
                Assert.Equal(2, effect.PowerDelta);
                Assert.Equal("OTHER_FRIENDLY_UNITS_POWER", effect.EffectKind);
                Assert.Equal("UNL-147/219", effect.SourceCardNo);
                Assert.Equal("CoreRuleEngine.ResolveOtherFriendlyUnitsPowerBonus", effect.SourcePath);
                Assert.Equal("SOURCE_PUBLIC_FIELD_UNIT_AND_OTHER_FRIENDLY_PUBLIC_UNITS", effect.Condition);
                Assert.Equal("DERIVED_FROM_CURRENT_PUBLIC_FIELD_FRIENDLY_UNIT_LOCATIONS", effect.Lifecycle);
                Assert.Equal(["P1-BARON-ALLY", "P1-BARON-BASE-ALLY"], effect.ParticipantObjectIds);
            });
        Assert.DoesNotContain(
            state.ContinuousEffects,
            effect => string.Equals(effect.SourceObjectId, "P1-BARON-SOURCE", StringComparison.Ordinal)
                && string.Equals(effect.TargetObjectId, "P1-BARON-SOURCE", StringComparison.Ordinal));
        Assert.DoesNotContain(
            state.ContinuousEffects,
            effect => string.Equals(effect.SourceObjectId, "P1-BARON-SOURCE", StringComparison.Ordinal)
                && string.Equals(effect.TargetObjectId, "P2-BARON-DEFENDER", StringComparison.Ordinal));

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-other-friendly-static-power", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "P1-BARON-BATTLEFIELD",
                ["P1-BARON-ALLY"],
                ["P2-BARON-DEFENDER"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        var attackerDamageEvent = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "DAMAGE_APPLIED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["combatRole"] as string, "ATTACKER", StringComparison.Ordinal));
        Assert.Equal("P1-BARON-ALLY", attackerDamageEvent.Payload["sourceObjectId"]);
        Assert.Equal(2, attackerDamageEvent.Payload["basePower"]);
        Assert.Equal(2, attackerDamageEvent.Payload["staticPowerBonus"]);
        Assert.Equal(4, attackerDamageEvent.Payload["combatPower"]);
        Assert.Equal(4, attackerDamageEvent.Payload["damage"]);

        var defenderDamageEvent = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "DAMAGE_APPLIED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["combatRole"] as string, "DEFENDER", StringComparison.Ordinal));
        Assert.Equal("P2-BARON-DEFENDER", defenderDamageEvent.Payload["sourceObjectId"]);
        Assert.Equal(5, defenderDamageEvent.Payload["basePower"]);
        Assert.False(defenderDamageEvent.Payload.ContainsKey("staticPowerBonus"));
        Assert.Equal(5, defenderDamageEvent.Payload["combatPower"]);
        Assert.Equal(5, defenderDamageEvent.Payload["damage"]);
    }

    [Fact]
    public async Task P79OtherFriendlyStaticPowerDoesNotProjectFromStandbySource()
    {
        var hiddenSourceState = OtherFriendlyStaticPowerState(sourceStandby: true);

        Assert.DoesNotContain(
            hiddenSourceState.ContinuousEffects,
            effect => string.Equals(effect.Layer, ContinuousEffectLayers.StaticAura, StringComparison.Ordinal)
                && string.Equals(effect.SourceObjectId, "P1-BARON-SOURCE", StringComparison.Ordinal));

        var result = await new CoreRuleEngine().ResolveAsync(
            hiddenSourceState,
            new PlayerIntent("intent-p7-9-other-friendly-static-power-standby-source", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "P1-BARON-BATTLEFIELD",
                ["P1-BARON-ALLY"],
                ["P2-BARON-DEFENDER"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        var attackerDamageEvent = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "DAMAGE_APPLIED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["combatRole"] as string, "ATTACKER", StringComparison.Ordinal));
        Assert.Equal("P1-BARON-ALLY", attackerDamageEvent.Payload["sourceObjectId"]);
        Assert.Equal(2, attackerDamageEvent.Payload["basePower"]);
        Assert.False(attackerDamageEvent.Payload.ContainsKey("staticPowerBonus"));
        Assert.Equal(2, attackerDamageEvent.Payload["combatPower"]);
    }

    [Fact]
    public void P79OtherFriendlyStaticPowerDoesNotProjectToStandbyTarget()
    {
        var hiddenTargetState = OtherFriendlyStaticPowerState(battlefieldAllyStandby: true);

        Assert.DoesNotContain(
            hiddenTargetState.ContinuousEffects,
            effect => string.Equals(effect.Layer, ContinuousEffectLayers.StaticAura, StringComparison.Ordinal)
                && string.Equals(effect.SourceObjectId, "P1-BARON-SOURCE", StringComparison.Ordinal)
                && string.Equals(effect.TargetObjectId, "P1-BARON-ALLY", StringComparison.Ordinal));
    }

    [Fact]
    public async Task P79FriendlyFilteredStaticPowerAddsOneToMatchingFriendlyUnits()
    {
        var state = FriendlyFilteredStaticPowerState();
        foreach (var snapshot in ResolutionResult.BuildSnapshots(state).Values)
        {
            var player = Assert.IsType<Dictionary<string, object?>>(snapshot.Players["P1"]);
            var objects = Assert.IsType<Dictionary<string, object?>>(player["objects"]);
            var ally = Assert.IsType<Dictionary<string, object?>>(objects["P1-RUMBLE-MECH-ALLY"]);
            Assert.Equal(3, ally["effectivePower"]); // Printed 2 plus Rumble's friendly-mech aura.
        }

        var rumbleAuras = state.ContinuousEffects
            .Where(effect => string.Equals(effect.Layer, ContinuousEffectLayers.StaticAura, StringComparison.Ordinal)
                && string.Equals(effect.SourceObjectId, "P1-RUMBLE-SOURCE", StringComparison.Ordinal))
            .OrderBy(effect => effect.TargetObjectId, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(2, rumbleAuras.Length);
        Assert.Equal(
            ["P1-RUMBLE-MECH-ALLY", "P1-RUMBLE-SOURCE"],
            rumbleAuras.Select(effect => Assert.IsType<string>(effect.TargetObjectId)).ToArray());
        Assert.All(
            rumbleAuras,
            effect =>
            {
                Assert.Equal("OBJECT", effect.Scope);
                Assert.Equal(1, effect.PowerDelta);
                Assert.Equal("FRIENDLY_FILTERED_UNITS_POWER", effect.EffectKind);
                Assert.Equal("SFD·089/221", effect.SourceCardNo);
                Assert.Equal("CoreRuleEngine.ResolveFriendlyFilteredUnitsPowerBonus", effect.SourcePath);
                Assert.Equal("SOURCE_PUBLIC_FIELD_UNIT_AND_FRIENDLY_FILTERED_PUBLIC_UNITS", effect.Condition);
                Assert.Equal("DERIVED_FROM_CURRENT_PUBLIC_FIELD_FILTERED_UNIT_LOCATIONS", effect.Lifecycle);
                Assert.Equal(["P1-RUMBLE-MECH-ALLY", "P1-RUMBLE-SOURCE"], effect.ParticipantObjectIds);
            });

        var soulShepherdAura = Assert.Single(state.ContinuousEffects, effect =>
            string.Equals(effect.Layer, ContinuousEffectLayers.StaticAura, StringComparison.Ordinal)
            && string.Equals(effect.SourceObjectId, "P1-SOUL-SHEPHERD-SOURCE", StringComparison.Ordinal));
        Assert.Equal("OBJECT", soulShepherdAura.Scope);
        Assert.Equal("P1-SOUL-SHEPHERD-TOKEN", soulShepherdAura.TargetObjectId);
        Assert.Equal(1, soulShepherdAura.PowerDelta);
        Assert.Equal("FRIENDLY_FILTERED_UNITS_POWER", soulShepherdAura.EffectKind);
        Assert.Equal("UNL-077/219", soulShepherdAura.SourceCardNo);
        Assert.Equal("CoreRuleEngine.ResolveFriendlyFilteredUnitsPowerBonus", soulShepherdAura.SourcePath);
        Assert.Equal("SOURCE_PUBLIC_FIELD_UNIT_AND_FRIENDLY_FILTERED_PUBLIC_UNITS", soulShepherdAura.Condition);
        Assert.Equal("DERIVED_FROM_CURRENT_PUBLIC_FIELD_FILTERED_UNIT_LOCATIONS", soulShepherdAura.Lifecycle);
        Assert.Equal(["P1-SOUL-SHEPHERD-TOKEN"], soulShepherdAura.ParticipantObjectIds);
        Assert.DoesNotContain(
            state.ContinuousEffects,
            effect => string.Equals(effect.EffectKind, "FRIENDLY_FILTERED_UNITS_POWER", StringComparison.Ordinal)
                && string.Equals(effect.TargetObjectId, "P1-RUMBLE-NONMECH-BASE", StringComparison.Ordinal));
        Assert.DoesNotContain(
            state.ContinuousEffects,
            effect => string.Equals(effect.EffectKind, "FRIENDLY_FILTERED_UNITS_POWER", StringComparison.Ordinal)
                && string.Equals(effect.TargetObjectId, "P2-RUMBLE-MECH-DEFENDER", StringComparison.Ordinal));

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-friendly-filtered-static-power", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "P1-RUMBLE-BATTLEFIELD",
                ["P1-RUMBLE-MECH-ALLY"],
                ["P2-RUMBLE-DEFENDER"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        result = await CombatTestDriver.FinishAsync(result);
        var attackerDamageEvent = CombatTestDriver.DamageFrom(result, "P1-RUMBLE-MECH-ALLY");
        Assert.Equal("P1-RUMBLE-MECH-ALLY", attackerDamageEvent.Payload["sourceObjectId"]);
        Assert.Equal(2, attackerDamageEvent.Payload["basePower"]);
        Assert.Equal(1, attackerDamageEvent.Payload["staticPowerBonus"]);
        Assert.Equal(3, attackerDamageEvent.Payload["combatPower"]);
        Assert.Equal(3, attackerDamageEvent.Payload["damage"]);

        var defenderDamageEvent = CombatTestDriver.DamageFrom(result, "P2-RUMBLE-DEFENDER");
        Assert.Equal("P2-RUMBLE-DEFENDER", defenderDamageEvent.Payload["sourceObjectId"]);
        Assert.Equal(5, defenderDamageEvent.Payload["basePower"]);
        Assert.False(defenderDamageEvent.Payload.ContainsKey("staticPowerBonus"));
        Assert.Equal(5, defenderDamageEvent.Payload["combatPower"]);
        Assert.Equal(5, defenderDamageEvent.Payload["damage"]);
    }

    [Fact]
    public async Task P79FriendlyFilteredStaticKeywordGrantsKeywordsToMatchingFriendlyUnits()
    {
        var state = FriendlyFilteredStaticKeywordState();
        var rumbleHeroAuras = state.ContinuousEffects
            .Where(effect => string.Equals(effect.Layer, ContinuousEffectLayers.RuleText, StringComparison.Ordinal)
                && string.Equals(effect.SourceObjectId, "P1-RUMBLE-HERO-SOURCE", StringComparison.Ordinal))
            .OrderBy(effect => effect.TargetObjectId, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(2, rumbleHeroAuras.Length);
        Assert.Equal(
            ["P1-RUMBLE-HERO-SOURCE", "P1-RUMBLE-MECH-ALLY"],
            rumbleHeroAuras.Select(effect => Assert.IsType<string>(effect.TargetObjectId)).ToArray());
        Assert.All(
            rumbleHeroAuras,
            effect =>
            {
                Assert.Equal("OBJECT", effect.Scope);
                Assert.Equal("WHILE_SOURCE_AND_TARGET_ON_PUBLIC_FIELD", effect.Duration);
                Assert.Equal(0, effect.PowerDelta);
                Assert.Equal(0, effect.BasePower);
                Assert.Equal(0, effect.EffectivePower);
                Assert.Empty(effect.EffectKind);
                Assert.Null(effect.SourceCardNo);
                Assert.Empty(effect.SourcePath);
                Assert.Null(effect.ParticipantObjectIds);
                Assert.Null(effect.SourceDependencyObjectIds);
                Assert.Null(effect.TargetDependencyObjectIds);
                Assert.Null(effect.ParticipantDependencyObjectIds);
                Assert.Null(effect.SourceOrder);
            });

        var rumbleLegendAuras = state.ContinuousEffects
            .Where(effect => string.Equals(effect.Layer, ContinuousEffectLayers.RuleText, StringComparison.Ordinal)
                && string.Equals(effect.SourceObjectId, "P1-RUMBLE-LEGEND", StringComparison.Ordinal))
            .OrderBy(effect => effect.TargetObjectId, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(2, rumbleLegendAuras.Length);
        Assert.Equal(
            ["P1-RUMBLE-HERO-SOURCE", "P1-RUMBLE-MECH-ALLY"],
            rumbleLegendAuras.Select(effect => Assert.IsType<string>(effect.TargetObjectId)).ToArray());
        Assert.All(
            rumbleLegendAuras,
            effect =>
            {
                Assert.Equal("OBJECT", effect.Scope);
                Assert.Equal("WHILE_SOURCE_AND_TARGET_ON_PUBLIC_FIELD", effect.Duration);
                Assert.Equal(0, effect.PowerDelta);
                Assert.Equal(0, effect.BasePower);
                Assert.Equal(0, effect.EffectivePower);
                Assert.Empty(effect.EffectKind);
                Assert.Null(effect.SourceCardNo);
                Assert.Empty(effect.SourcePath);
                Assert.Null(effect.ParticipantObjectIds);
                Assert.Null(effect.SourceDependencyObjectIds);
                Assert.Null(effect.TargetDependencyObjectIds);
                Assert.Null(effect.ParticipantDependencyObjectIds);
                Assert.Null(effect.SourceOrder);
            });

        var lilliaAura = Assert.Single(state.ContinuousEffects, effect =>
            string.Equals(effect.Layer, ContinuousEffectLayers.RuleText, StringComparison.Ordinal)
            && string.Equals(effect.SourceObjectId, "P1-LILLIA-SOURCE", StringComparison.Ordinal));
        Assert.Equal("OBJECT", lilliaAura.Scope);
        Assert.Equal("WHILE_SOURCE_AND_TARGET_ON_PUBLIC_FIELD", lilliaAura.Duration);
        Assert.Equal("P1-LILLIA-TOKEN", lilliaAura.TargetObjectId);
        Assert.Equal(0, lilliaAura.PowerDelta);
        Assert.Equal(0, lilliaAura.BasePower);
        Assert.Equal(0, lilliaAura.EffectivePower);
        Assert.Empty(lilliaAura.EffectKind);
        Assert.Null(lilliaAura.SourceCardNo);
        Assert.Empty(lilliaAura.SourcePath);
        Assert.Null(lilliaAura.ParticipantObjectIds);
        Assert.DoesNotContain(
            state.ContinuousEffects,
            effect => string.Equals(effect.Layer, ContinuousEffectLayers.RuleText, StringComparison.Ordinal)
                && string.Equals(effect.SourceObjectId, "P1-RUMBLE-HERO-SOURCE", StringComparison.Ordinal)
                && string.Equals(effect.TargetObjectId, "P1-RUMBLE-NONMECH-BASE", StringComparison.Ordinal));
        Assert.DoesNotContain(
            state.ContinuousEffects,
            effect => string.Equals(effect.Layer, ContinuousEffectLayers.RuleText, StringComparison.Ordinal)
                && string.Equals(effect.SourceObjectId, "P1-RUMBLE-HERO-SOURCE", StringComparison.Ordinal)
                && string.Equals(effect.TargetObjectId, "P2-RUMBLE-MECH-DEFENDER", StringComparison.Ordinal));
        Assert.DoesNotContain(
            state.ContinuousEffects,
            effect => string.Equals(effect.Layer, ContinuousEffectLayers.RuleText, StringComparison.Ordinal)
                && string.Equals(effect.SourceObjectId, "P1-LILLIA-SOURCE", StringComparison.Ordinal)
                && string.Equals(effect.TargetObjectId, "P2-LILLIA-TOKEN", StringComparison.Ordinal));

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-friendly-filtered-static-keyword", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "P1-RUMBLE-BATTLEFIELD",
                ["P1-RUMBLE-MECH-ALLY"],
                ["P2-RUMBLE-DEFENDER"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        result = await CombatTestDriver.FinishAsync(result);
        var attackerDamageEvent = CombatTestDriver.DamageFrom(result, "P1-RUMBLE-MECH-ALLY");
        Assert.Equal("P1-RUMBLE-MECH-ALLY", attackerDamageEvent.Payload["sourceObjectId"]);
        Assert.Equal(2, attackerDamageEvent.Payload["basePower"]);
        Assert.Equal(1, attackerDamageEvent.Payload["keywordBonus"]);
        Assert.False(attackerDamageEvent.Payload.ContainsKey("staticPowerBonus"));
        Assert.Equal(3, attackerDamageEvent.Payload["combatPower"]);
        Assert.Equal(3, attackerDamageEvent.Payload["damage"]);

        var defenderDamageEvent = CombatTestDriver.DamageFrom(result, "P2-RUMBLE-DEFENDER");
        Assert.Equal("P2-RUMBLE-DEFENDER", defenderDamageEvent.Payload["sourceObjectId"]);
        Assert.Equal(5, defenderDamageEvent.Payload["basePower"]);
        Assert.Equal(0, defenderDamageEvent.Payload["keywordBonus"]);
        Assert.Equal(5, defenderDamageEvent.Payload["combatPower"]);
        Assert.Equal(5, defenderDamageEvent.Payload["damage"]);
    }

    [Fact]
    public async Task P79FriendlyFilteredStaticKeywordGrantDoesNotProjectFromFaceDownSource()
    {
        var hiddenSourceState = FriendlyFilteredStaticKeywordState(
            rumbleHeroSourceFaceDown: true,
            includeRumbleLegend: false);

        Assert.DoesNotContain(
            hiddenSourceState.ContinuousEffects,
            effect => string.Equals(effect.Layer, ContinuousEffectLayers.RuleText, StringComparison.Ordinal)
                && string.Equals(effect.SourceObjectId, "P1-RUMBLE-HERO-SOURCE", StringComparison.Ordinal));

        var result = await new CoreRuleEngine().ResolveAsync(
            hiddenSourceState,
            new PlayerIntent("intent-p7-9-friendly-filtered-static-keyword-face-down-source", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "P1-RUMBLE-BATTLEFIELD",
                ["P1-RUMBLE-MECH-ALLY"],
                ["P2-RUMBLE-DEFENDER"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        result = await CombatTestDriver.FinishAsync(result);
        var attackerDamageEvent = CombatTestDriver.DamageFrom(result, "P1-RUMBLE-MECH-ALLY");
        Assert.Equal("P1-RUMBLE-MECH-ALLY", attackerDamageEvent.Payload["sourceObjectId"]);
        Assert.Equal(2, attackerDamageEvent.Payload["basePower"]);
        Assert.Equal(0, attackerDamageEvent.Payload["keywordBonus"]);
        Assert.Equal(2, attackerDamageEvent.Payload["combatPower"]);
    }

    [Fact]
    public void P79FriendlyFilteredStaticKeywordGrantDoesNotProjectToFaceDownTarget()
    {
        var hiddenTargetState = FriendlyFilteredStaticKeywordState(rumbleMechanicalAllyFaceDown: true);

        Assert.DoesNotContain(
            hiddenTargetState.ContinuousEffects,
            effect => string.Equals(effect.Layer, ContinuousEffectLayers.RuleText, StringComparison.Ordinal)
                && string.Equals(effect.TargetObjectId, "P1-RUMBLE-MECH-ALLY", StringComparison.Ordinal));
    }
}
