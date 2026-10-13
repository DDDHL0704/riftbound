using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class StandbyReactionSfdTeemoTests
{
    private const string BattlefieldObjectId = "F";
    internal const string EnemyTargetObjectId = "P2-SFD-TEEMO-ENEMY";
    private const string TeemoObjectId = "SHADOW";

    public static TheoryData<string, string> SfdTeemoCards()
    {
        return new TheoryData<string, string>
        {
            { "SFD·230/221", "SFD_230_TEEMO_STANDBY_DEFEND_REVEAL_PLAY_UNIT" },
            { "SFD·230*/221", "SFD_230_PROMO_TEEMO_STANDBY_DEFEND_REVEAL_PLAY_UNIT" }
        };
    }

    [Theory]
    [MemberData(nameof(SfdTeemoCards))]
    public async Task PromptMetadataExposesSfdTeemoStandbyReactionTargets(string cardNo, string _)
    {
        var initial = await BuildClosedPriorityState(cardNo);
        var prompt = ResolutionResult.BuildPrompts(initial)["P1"];
        Assert.Contains(CommandTypes.RevealCard, prompt.EnabledActions());
        var candidate = Assert.Single(prompt.Candidates!, c => c.Action == CommandTypes.RevealCard);
        Assert.True(candidate.Enabled);
        Assert.Equal([TeemoObjectId], candidate.Sources!.Select(s => s.Id));
        Assert.Empty(candidate.Targets ?? []);
        var metadata = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(candidate.Metadata);
        var requirement = Assert.Single(Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(metadata["sourceRequirements"]));
        Assert.Equal(TeemoObjectId, requirement["sourceObjectId"]);
        Assert.Equal(cardNo, requirement["cardNo"]);
        Assert.Equal("STANDBY_REACTION", requirement["mode"]);
        Assert.Equal(0, requirement["minTargetCount"]);
        Assert.Equal(0, requirement["maxTargetCount"]);
        Assert.Equal(CardTargetScopes.EnemyUnitAtSourceBattlefield, requirement["targetScope"]);
        var choices = Assert.IsAssignableFrom<IReadOnlyDictionary<string, IReadOnlyList<ActionPromptChoiceDto>>>(requirement["targetChoicesByIndex"]);
        Assert.Empty(choices);
        var played = await OfficialInsightAndSpellLockTests.Act(initial, "P1", new RevealCardCommand(
            TeemoObjectId, cardNo, [], "STANDBY_REACTION", ["STANDBY_REVEAL_0"], "STACK"));
        var pending = Assert.IsType<PendingCardChoiceState>(played.State.PendingCardChoice);
        Assert.Equal([EnemyTargetObjectId], pending.LegalObjectIds);
        Assert.Equal("TRIGGER_CONFIRMATION", pending.ChoiceWindow);
        Assert.Equal(1, pending.RequiredCount);
        Assert.Equal(1, pending.MaxCount);
        Assert.Equal(BattlefieldObjectId, played.State.ObjectLocations[TeemoObjectId].BattlefieldObjectId);
        var declined = await new CoreRuleEngine().ResolveAsync(played.State,
            new PlayerIntent("teemo-mandatory-target-decline", "P1", CommandTypes.ChooseCards),
            new ChooseCardsCommand(pending.ChoiceId, pending.ChoiceWindow, []), default);
        Assert.False(declined.Accepted);
        Assert.Empty(declined.Events);
        Assert.Equal(MatchStateHasher.Hash(played.State), MatchStateHasher.Hash(declined.State));
        OfficialGraveyardRecastTests.Restore(played.State);
    }

    [Theory]
    [MemberData(nameof(SfdTeemoCards))]
    public async Task BattlefieldStandbyReactionSfdTeemoDamagesAndRecyclesWithSharedResolver(string cardNo, string effectKind)
    {
        var initial = await BuildClosedPriorityState(cardNo);
        var bottom = Assert.Single(initial.StackItems);
        var played = await OfficialInsightAndSpellLockTests.Act(initial, "P1", new RevealCardCommand(
            TeemoObjectId, cardNo, [EnemyTargetObjectId], "STANDBY_REACTION", ["STANDBY_REVEAL_0"], "STACK"));
        Assert.Equal(2, played.State.StackItems.Count);
        Assert.Equal(bottom, played.State.StackItems[0]);
        var ability = Assert.Single(played.State.StackItems, s => s.SourceObjectId == TeemoObjectId);
        Assert.True(ability.SourceConfirmed);
        Assert.Equal(effectKind, ability.EffectKind);
        Assert.Equal([EnemyTargetObjectId], ability.TargetObjectIds);
        Assert.Equal("BATTLEFIELD:" + BattlefieldObjectId, ability.Destination);
        Assert.Equal(BattlefieldObjectId, ability.PlayedFromStandby!.Battlefield.ObjectId);
        Assert.Equal(BattlefieldObjectId, played.State.ObjectLocations[TeemoObjectId].BattlefieldObjectId);
        Assert.Equal(0, played.State.CardObjects[EnemyTargetObjectId].Damage);
        OfficialGraveyardRecastTests.Restore(played.State);
        var done = await OfficialGraveyardRecastTests.Top(played.State);
        Assert.Equal(bottom.StackItemId, Assert.Single(done.State.StackItems).StackItemId);
        Assert.Contains(TeemoObjectId, done.State.PlayerZones["P1"].Battlefields);
        Assert.Equal(new ObjectLocationState("P1", "BATTLEFIELD", BattlefieldObjectId), done.State.ObjectLocations[TeemoObjectId]);
        Assert.False(done.State.CardObjects[TeemoObjectId].IsFaceDown);
        Assert.Equal(2, done.State.CardObjects[EnemyTargetObjectId].Damage);
        Assert.Equal("P1-MAIN-BOTTOM", done.State.PlayerZones["P1"].MainDeck[0]);
        Assert.Equal(TopFiveMainDeckCards().Order(), done.State.PlayerZones["P1"].MainDeck.Skip(1).Order());
        Assert.Contains(done.Events, e => e.Kind == "MAIN_DECK_CARDS_REVEALED" && Equals(e.Payload["sourceObjectId"], TeemoObjectId) && Equals(e.Payload["count"], 5));
        Assert.Contains(done.Events, e => e.Kind == "DAMAGE_APPLIED" && Equals(e.Payload["sourceObjectId"], TeemoObjectId) && Equals(e.Payload["targetObjectId"], EnemyTargetObjectId) && Equals(e.Payload["damage"], 2));
        Assert.Contains(done.Events, e => e.Kind == "CARDS_RECYCLED" && Equals(e.Payload["sourceObjectId"], TeemoObjectId) && Equals(e.Payload["count"], 5));
        OfficialGraveyardRecastTests.Restore(done.State);
        ArmamentMightTests.PublicRecovery(done.State);
    }

    internal static async Task<MatchState> BuildClosedPriorityState(string cardNo, bool twoContests = false, string enemyCard = "UNL-057/219")
    {
        var state = StandbySourceRulesTests.Position(cardNo);
        if (twoContests)
        {
            Assert.True(CardBehaviorRegistry.TryGetByCardNo(P4ActivatedAbilityCatalog.ShadowCardNo, out var shadow));
            state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects)
            {
                ["HOST"] = state.CardObjects["HOST"] with { CardNo = shadow.CardNo, Power = shadow.SourceUnitPower, ManaCost = shadow.ManaCost, Tags = [CardObjectTags.UnitCard] }
            }};
            state = AttackDamageDistributionTests.Spell(state, "STANDBY-SEQUENTIAL-MOVE", "UNL-202/219", "P2");
            state = OfficialHoldSequenceTests.AddUnit(state, "STANDBY-NEXT-OPPONENT", "OGN·001/298", "F", "P1");
            state = LocalDestructionRecallTests.At(state, "STANDBY-NEXT-OPPONENT", "P1", "BASE");
            foreach (var (id, player, card, trait) in new[]
            {
                ("STANDBY-P1-RESOURCE-A", "P1", "UNL-R03", "blue"),
                ("STANDBY-P1-RESOURCE-B", "P1", "UNL-R03", "blue"),
                ("STANDBY-P2-ORANGE", "P2", "UNL-R05", "orange")
            })
            {
                state = state with
                {
                    CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects)
                    {
                        [id] = new(id, cardNo: card, ownerId: player, controllerId: player, tags: [CardObjectTags.RuneCard, "COLOR:" + trait])
                    },
                    PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones)
                    {
                        [player] = state.PlayerZones[player] with { Base = state.PlayerZones[player].Base.Append(id).ToArray() }
                    },
                    ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { [id] = new(player, "BASE") }
                };
            }
        }
        state = OfficialHoldSequenceTests.AddUnit(state, EnemyTargetObjectId, enemyCard, "F", "P2");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects)
        {
            [EnemyTargetObjectId] = state.CardObjects[EnemyTargetObjectId] with { Power = 7 }
        }};
        state = LocalDestructionRecallTests.At(state, EnemyTargetObjectId, "P2", "BASE");
        state = AttackDamageDistributionTests.Spell(state, "SFD-TEEMO-REPLY-SECOND", "OGN·048/298", "P2");
        const string extraRune = "SFD-TEEMO-P2-RUNE-EXTRA";
        state = state with
        {
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects)
            {
                [extraRune] = new(extraRune, cardNo: "UNL-R03", ownerId: "P2", controllerId: "P2", tags: [CardObjectTags.RuneCard, "COLOR:blue"])
            },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones)
            {
                ["P2"] = state.PlayerZones["P2"] with { Base = state.PlayerZones["P2"].Base.Append(extraRune).ToArray() }
            },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { [extraRune] = new("P2", "BASE") }
        };
        state = StandbyTestDriver.WithControlledBattlefield(state, "P2");
        state = OfficialHoldSequenceTests.AddUnit(state, "P2-SFD-TEEMO-OTHER-ENEMY", "UNL-092/219", StandbyTestDriver.BattlefieldId, "P2");
        var cards = state.CardObjects.ToDictionary(p => p.Key, p => p.Value);
        var locations = state.ObjectLocations.ToDictionary(p => p.Key, p => p.Value);
        foreach (var id in state.PlayerZones["P1"].MainDeck) { cards.Remove(id); locations.Remove(id); }
        var deck = TopFiveMainDeckCards().Append("P1-MAIN-BOTTOM").ToArray();
        var cardNos = new[] { "OGN·121/298", "SFD·125/221", "OGN·199/298", "OGN·009/298", "SFD·125/221", "SFD·125/221" };
        for (var i = 0; i < deck.Length; i++)
        {
            // Deliberately omit keyword tags: counting must read the actual printed keyword (811.5).
            cards[deck[i]] = new(deck[i], cardNo: cardNos[i], ownerId: "P1", controllerId: "P1");
            locations[deck[i]] = new("P1", "MAIN_DECK");
        }
        state = state with { CardObjects = cards, ObjectLocations = locations,
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones)
            {
                ["P1"] = state.PlayerZones["P1"] with { MainDeck = deck }
            }};
        state = await StandbySourceRulesTests.ReadyShadow(cardNo, state);
        state = (await OfficialInsightAndSpellLockTests.Act(state, "P1", new PassPriorityCommand())).State;
        Assert.Empty(state.StackItems);
        state = (await OfficialInsightAndSpellLockTests.Act(state, "P2", new TapRuneCommand(extraRune))).State;
        if (twoContests)
        {
            state = (await OfficialInsightAndSpellLockTests.Act(state, "P2", new RecycleRuneCommand("STANDBY-P2-ORANGE"))).State;
            var move = new PlayCardCommand("STANDBY-SEQUENTIAL-MOVE", "UNL-202/219", [EnemyTargetObjectId, "STANDBY-NEXT-OPPONENT"],
                MovementDestinations: ["BATTLEFIELD:F", "BATTLEFIELD:" + StandbyTestDriver.BattlefieldId]);
            state = (await OfficialInsightAndSpellLockTests.Act(state, "P2", move)).State;
            state = (await OfficialGraveyardRecastTests.Top(state)).State;
            Assert.Equal("P2", state.FocusPlayerId);
            Assert.Equal("F", state.SpellDuelState.BattlefieldObjectId);
            state = (await OfficialInsightAndSpellLockTests.Act(state, "P2", new PassFocusCommand())).State;
            Assert.Equal("P1", state.FocusPlayerId);
            OfficialGraveyardRecastTests.Restore(state);
            return state;
        }
        state = (await OfficialInsightAndSpellLockTests.Act(state, "P2", new MoveUnitCommand(EnemyTargetObjectId, "BASE", "BATTLEFIELD:F", []))).State;
        Assert.Equal("P2", state.FocusPlayerId);
        state = (await OfficialInsightAndSpellLockTests.Act(state, "P2", new PlayCardCommand("SFD-TEEMO-REPLY-SECOND", "OGN·048/298", []))).State;
        state = (await OfficialInsightAndSpellLockTests.Act(state, "P2", new PassPriorityCommand())).State;
        Assert.Equal("P1", state.PriorityPlayerId);
        OfficialGraveyardRecastTests.Restore(state);
        return state;
    }

    private static string[] TopFiveMainDeckCards() =>
        ["P1-MAIN-STANDBY-001", "P1-MAIN-NON-STANDBY-001", "P1-MAIN-STANDBY-002", "P1-MAIN-NON-STANDBY-002", "P1-MAIN-NON-STANDBY-003"];
}
