using Riftbound.Contracts;
using Riftbound.Engine;
using System.Text.Json;
using Xunit;

namespace Riftbound.ConformanceTests;

internal static class StandbyTestDriver
{
    private sealed class RecordingMatchJournal : IMatchJournal
    {
        public List<MatchJournalEntry> Entries { get; } = [];
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken cancellationToken)
        {
            Entries.Add(entry);
            return ValueTask.CompletedTask;
        }
    }

    internal const string BattlefieldId = "STANDBY-TEST-FIELD";

    internal static async Task AssertRevealRejected(MatchState state, RevealCardCommand command, string code, string? reason = null)
    {
        var stateHash = MatchStateHasher.Hash(state);
        var snapshots = MatchStateHasher.HashValue(ResolutionResult.BuildSnapshots(state));
        var prompts = MatchStateHasher.HashValue(ResolutionResult.BuildPrompts(state));
        var result = await new CoreRuleEngine().ResolveAsync(state,
            new PlayerIntent("rejected-reveal-" + state.Tick, "P1", CommandTypes.RevealCard), command, default);
        Assert.False(result.Accepted);
        Assert.Equal(code, result.ErrorCode);
        if (reason is not null) Assert.Contains(reason, result.ErrorMessage);
        Assert.Empty(result.Events);
        Assert.Equal(stateHash, MatchStateHasher.Hash(result.State));
        Assert.Equal(snapshots, MatchStateHasher.HashValue(result.Snapshots));
        Assert.Equal(prompts, MatchStateHasher.HashValue(result.Prompts));
    }

    internal static async Task AssertRevealWithoutMarkerUsesActualPermission(string mode, bool marker = false)
    {
        var state = await StandbySourceRulesTests.ReadyShadow("OGN·197/298");
        var resources = state.RunePools["P1"];
        var result = await OfficialInsightAndSpellLockTests.Act(state, "P1",
            new RevealCardCommand("SHADOW", "OGN·197/298", [], mode, marker ? ["STANDBY_REVEAL_0"] : [], "BATTLEFIELD:F"));
        Assert.Equal(resources, result.State.RunePools["P1"]);
        Assert.False(result.State.CardObjects["SHADOW"].IsFaceDown);
        Assert.Equal("F", result.State.ObjectLocations["SHADOW"].BattlefieldObjectId);
        Assert.True(Assert.Single(result.State.StackItems, i => i.SourceObjectId == "SHADOW").SourceConfirmed);
        Assert.Single(result.Events, e => e.Kind == "TRIGGER_QUEUED");
        var done = await OfficialGraveyardRecastTests.Top(result.State);
        Assert.DoesNotContain(done.Events, e => e.Kind == "TRIGGER_QUEUED");
        Assert.Single(done.Events, e => e.Kind == "TRIGGER_RESOLVED");
        Assert.Equal(4, done.State.CardObjects["SHADOW"].Power);
        Assert.Equal(3, done.State.CardObjects["SHADOW"].UntilEndOfTurnPowerModifier);
        OfficialGraveyardRecastTests.Restore(done.State);
        ArmamentMightTests.PublicRecovery(done.State);
    }

    internal static async Task AssertUnitRevealAtOriginalBattlefield(string mode)
    {
        var state = await StandbySourceRulesTests.ReadyShadow("OGN·121/298");
        var resources = state.RunePools["P1"];
        Assert.Equal("F", state.CardObjects["SHADOW"].StandbyPlacement!.Battlefield.ObjectId);
        var result = await OfficialInsightAndSpellLockTests.Act(state, "P1",
            new RevealCardCommand("SHADOW", "OGN·121/298", [], mode, ["STANDBY_REVEAL_0"], "BATTLEFIELD:F"));
        Assert.Equal(state.Tick + 1, result.State.Tick);
        Assert.Equal(resources, result.State.RunePools["P1"]);
        Assert.False(result.State.CardObjects["SHADOW"].IsFaceDown);
        Assert.Equal(2, result.State.CardObjects["SHADOW"].Power);
        Assert.Equal(2, result.State.CardObjects["SHADOW"].ManaCost);
        Assert.Equal(2, result.State.CardObjects["SHADOW"].ObjectGeneration);
        Assert.Null(result.State.CardObjects["SHADOW"].StandbyPlacement);
        Assert.Equal("F", result.State.CardObjects["SHADOW"].LastStandbyPlay!.Battlefield.ObjectId);
        Assert.Equal(new ObjectLocationState("P1", "BATTLEFIELD", "F"), result.State.ObjectLocations["SHADOW"]);
        Assert.Contains("SHADOW", result.State.PlayerZones["P1"].Battlefields);
        Assert.DoesNotContain("SHADOW", result.State.PlayerZones["P1"].Base);
        Assert.DoesNotContain("SHADOW", result.State.PlayerZones["P1"].Hand);
        Assert.Equal("REPLY", Assert.Single(result.State.StackItems).SourceObjectId);
        Assert.Equal(["CARD_REVEALED", "CARD_PLAYED", "COST_PAID", "PERMANENT_CONFIRMED", "UNIT_PLAYED_TO_BATTLEFIELD", "TRIGGER_QUEUED", "TRIGGER_NOT_CONFIRMED"], result.Events.Select(e => e.Kind));
        Assert.Equal("P2", result.State.PriorityPlayerId);
        foreach (var snapshot in result.Snapshots.Values)
        {
            var player = Assert.IsType<Dictionary<string, object?>>(snapshot.Players["P1"]);
            var card = Assert.IsType<Dictionary<string, object?>>(Assert.IsType<Dictionary<string, object?>>(player["objects"])["SHADOW"]);
            Assert.False(Assert.IsType<bool>(card["isFaceDown"]));
            Assert.Equal("OGN·121/298", card["cardNo"]);
            Assert.Equal(2, card["power"]);
        }
        OfficialGraveyardRecastTests.Restore(result.State);
        ArmamentMightTests.PublicRecovery(result.State);
    }

    internal static async Task AssertCountedStandbyResponse(string mode, bool postEntrySelection, bool rejectOtherField)
    {
        const string cardNo = "OGN·121/298";
        var state = await StandbyReactionSfdTeemoTests.BuildClosedPriorityState(cardNo);
        var enemy = StandbyReactionSfdTeemoTests.EnemyTargetObjectId;
        Assert.True(state.SpellDuelState.IsActive);
        Assert.Equal("F", state.SpellDuelState.BattlefieldObjectId);
        if (rejectOtherField)
            await AssertRevealRejected(state, new RevealCardCommand("SHADOW", cardNo, ["P2-SFD-TEEMO-OTHER-ENEMY"],
                mode, ["STANDBY_REVEAL_0"], "BATTLEFIELD:F"), ErrorCodes.InvalidTarget);
        var beforeResources = state.RunePools["P1"];
        var result = await OfficialInsightAndSpellLockTests.Act(state, "P1", new RevealCardCommand("SHADOW", cardNo,
            postEntrySelection ? [] : [enemy], mode, ["STANDBY_REVEAL_0"], "BATTLEFIELD:F"));
        Assert.Equal(beforeResources, result.State.RunePools["P1"]);
        Assert.Equal("F", result.State.ObjectLocations["SHADOW"].BattlefieldObjectId);
        Assert.Equal(0, result.State.CardObjects[enemy].Damage);
        if (postEntrySelection)
        {
            var choice = Assert.IsType<PendingCardChoiceState>(result.State.PendingCardChoice);
            Assert.Equal(1, choice.RequiredCount);
            Assert.Equal(1, choice.MaxCount);
            Assert.Equal([enemy], choice.LegalObjectIds);
            OfficialGraveyardRecastTests.Restore(result.State);
            result = await OfficialInsightAndSpellLockTests.Act(result.State, "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [enemy]));
        }
        var ability = Assert.Single(result.State.StackItems, i => i.SourceObjectId == "SHADOW");
        Assert.True(ability.SourceConfirmed);
        Assert.Equal([enemy], ability.TargetObjectIds);
        Assert.Equal("F", ability.PlayedFromStandby!.Battlefield.ObjectId);
        Assert.Equal("BATTLEFIELD:F", ability.Destination);
        OfficialGraveyardRecastTests.Restore(result.State);
        var done = await OfficialGraveyardRecastTests.Top(result.State);
        Assert.Equal(2, done.State.CardObjects[enemy].Damage);
        Assert.Equal(0, done.State.CardObjects["P2-SFD-TEEMO-OTHER-ENEMY"].Damage);
        Assert.Equal("SFD-TEEMO-REPLY-SECOND", Assert.Single(done.State.StackItems).SourceObjectId);
        Assert.Equal("F", done.State.ObjectLocations["SHADOW"].BattlefieldObjectId);
        Assert.Contains("SHADOW", done.State.PlayerZones["P1"].Battlefields);
        Assert.DoesNotContain("SHADOW", done.State.PlayerZones["P1"].Base);
        Assert.Equal("P1-MAIN-BOTTOM", done.State.PlayerZones["P1"].MainDeck[0]);
        Assert.Equal(6, done.State.PlayerZones["P1"].MainDeck.Count);
        Assert.Contains(done.Events, e => e.Kind == "MAIN_DECK_CARDS_REVEALED" && Equals(e.Payload["count"], 5));
        Assert.Contains(done.Events, e => e.Kind == "CARDS_RECYCLED" && Equals(e.Payload["count"], 5));
        Assert.Contains(done.Events, e => e.Kind == "DAMAGE_APPLIED" && Equals(e.Payload["targetObjectId"], enemy) && Equals(e.Payload["damage"], 2));
        OfficialGraveyardRecastTests.Restore(done.State);
        ArmamentMightTests.PublicRecovery(done.State);
    }

    internal static async Task AssertCountedStandbyMetadata()
    {
        var state = await StandbyReactionSfdTeemoTests.BuildClosedPriorityState("OGN·121/298");
        var prompt = ResolutionResult.BuildPrompts(state)["P1"];
        Assert.Contains(CommandTypes.RevealCard, prompt.EnabledActions());
        var candidate = Assert.Single(prompt.Candidates!, c => c.Action == CommandTypes.RevealCard);
        Assert.Equal(["SHADOW"], candidate.Sources!.Select(s => s.Id));
        Assert.Empty(candidate.Targets ?? []);
        Assert.Equal(["BATTLEFIELD:F"], candidate.Destinations!.Select(d => d.Id));
        var metadata = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(candidate.Metadata);
        var requirement = Assert.Single(Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(metadata["sourceRequirements"]));
        Assert.Equal("SHADOW", requirement["sourceObjectId"]);
        Assert.Equal("OGN·121/298", requirement["cardNo"]);
        Assert.Equal("STANDBY_REACTION", requirement["mode"]);
        Assert.Equal(0, requirement["minTargetCount"]);
        Assert.Equal(0, requirement["maxTargetCount"]);
        Assert.Equal(CardTargetScopes.EnemyUnitAtSourceBattlefield, requirement["targetScope"]);
        Assert.Empty(Assert.IsAssignableFrom<IReadOnlyDictionary<string, IReadOnlyList<ActionPromptChoiceDto>>>(requirement["targetChoicesByIndex"]));
        var played = await OfficialInsightAndSpellLockTests.Act(state, "P1", new RevealCardCommand(
            "SHADOW", "OGN·121/298", [], "STANDBY_REACTION", ["STANDBY_REVEAL_0"], "BATTLEFIELD:F"));
        var pending = Assert.IsType<PendingCardChoiceState>(played.State.PendingCardChoice);
        Assert.Equal("TRIGGER_CONFIRMATION", pending.ChoiceWindow);
        Assert.Equal(1, pending.RequiredCount);
        Assert.Equal(1, pending.MaxCount);
        Assert.Equal([StandbyReactionSfdTeemoTests.EnemyTargetObjectId], pending.LegalObjectIds);
        Assert.DoesNotContain("P2-SFD-TEEMO-OTHER-ENEMY", pending.LegalObjectIds);
        OfficialGraveyardRecastTests.Restore(played.State);
        ArmamentMightTests.PublicRecovery(played.State);
    }

    internal static async Task AssertRevealSourceFiltering()
    {
        var initial = StandbySourceRulesTests.Position("OGN·197/298");
        var hidden = await OfficialInsightAndSpellLockTests.Act(initial, "P1",
            new HideCardCommand("SHADOW", "OGN·197/298", "BATTLEFIELD:F", ["STANDBY_A"]));
        Assert.DoesNotContain(CommandTypes.RevealCard, ResolutionResult.BuildPrompts(hidden.State)["P1"].EnabledActions());
        var state = await StandbySourceRulesTests.ReadyShadow("OGN·197/298");
        var candidate = Assert.Single(ResolutionResult.BuildPrompts(state)["P1"].Candidates!, c => c.Action == CommandTypes.RevealCard);
        Assert.True(candidate.Enabled);
        Assert.Equal(["SHADOW"], candidate.Sources!.Select(s => s.Id));
        Assert.Equal(["BATTLEFIELD:F"], candidate.Destinations!.Select(d => d.Id));
        Assert.Equal(["STANDBY_REACTION"], candidate.Modes!.Select(m => m.Id));
        Assert.Equal(["STANDBY_REVEAL_0"], candidate.OptionalCosts!.Select(c => c.Id));
        foreach (var branch in new[] { "face-up", "unknown-card", "plain-keyword-tag", "opponent", "no-history", "base", "generation" })
        {
            var card = state.CardObjects["SHADOW"];
            card = branch switch
            {
                "face-up" => card with { IsFaceDown = false },
                "unknown-card" => card with { CardNo = null },
                "plain-keyword-tag" => card with { CardNo = "OGN·001/298" },
                "opponent" => card with { ControllerId = "P2" },
                "no-history" => card with { StandbyPlacement = null },
                "generation" => card with { ObjectGeneration = card.ObjectGeneration + 1 },
                _ => card
            };
            var bad = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) { ["SHADOW"] = card } };
            if (branch == "base") bad = bad with { ObjectLocations = new Dictionary<string, ObjectLocationState>(bad.ObjectLocations)
            {
                ["SHADOW"] = new("P1", "BASE")
            }};
            var prompt = ResolutionResult.BuildPrompts(bad)["P1"];
            Assert.DoesNotContain(CommandTypes.RevealCard, prompt.EnabledActions());
            Assert.DoesNotContain(prompt.Candidates ?? [], c => c.Action == CommandTypes.RevealCard && c.Sources?.Any(s => s.Id == "SHADOW") == true);
        }
        var wrongPriority = state with { PriorityPlayerId = "P2" };
        Assert.DoesNotContain(CommandTypes.RevealCard, ResolutionResult.BuildPrompts(wrongPriority)["P1"].EnabledActions());
        OfficialGraveyardRecastTests.Restore(state);
    }

    internal static async Task AssertNaturalNestedBattleResponse()
    {
        var state = await StandbyReactionSfdTeemoTests.BuildClosedPriorityState("OGN·121/298", twoContests: true);
        var enemy = StandbyReactionSfdTeemoTests.EnemyTargetObjectId;
        var nextGeneration = state.CardObjects["STANDBY-NEXT-OPPONENT"].ObjectGeneration;
        var nextLocation = state.ObjectLocations["STANDBY-NEXT-OPPONENT"];
        void NextNotAdvanced(MatchState current)
        {
            Assert.Equal("F", current.SpellDuelState.BattlefieldObjectId);
            Assert.Equal(nextGeneration, current.CardObjects["STANDBY-NEXT-OPPONENT"].ObjectGeneration);
            Assert.Equal(nextLocation, current.ObjectLocations["STANDBY-NEXT-OPPONENT"]);
            Assert.Equal(0, current.CardObjects["STANDBY-NEXT-OPPONENT"].Damage);
            Assert.DoesNotContain("STUNNED", current.CardObjects["STANDBY-NEXT-OPPONENT"].UntilEndOfTurnEffects);
            Assert.Contains(current.BattlefieldTasks, t => t.BattlefieldObjectId == BattlefieldId && t.Status == "PENDING");
        }
        NextNotAdvanced(state);
        state = (await OfficialInsightAndSpellLockTests.Act(state, "P1", new TapRuneCommand("STANDBY-P1-RESOURCE-A"))).State;
        state = (await OfficialInsightAndSpellLockTests.Act(state, "P1", new RecycleRuneCommand("STANDBY-P1-RESOURCE-B"))).State;
        state = (await OfficialInsightAndSpellLockTests.Act(state, "P1", new ActivateAbilityCommand("HOST", P4ActivatedAbilityCatalog.ShadowStunAbilityId, [enemy], []))).State;
        var bottom = Assert.Single(state.StackItems);
        Assert.Equal(P4ActivatedAbilityCatalog.ShadowStunAbilityEffectKind, bottom.EffectKind);
        Assert.True(state.CardObjects["HOST"].IsExhausted);
        Assert.DoesNotContain("STUNNED", state.CardObjects[enemy].UntilEndOfTurnEffects);
        Assert.Equal("P1", state.PriorityPlayerId);
        NextNotAdvanced(state);
        var entered = await OfficialInsightAndSpellLockTests.Act(state, "P1", new RevealCardCommand("SHADOW", "OGN·121/298", [enemy],
            "STANDBY_REACTION", ["STANDBY_REVEAL_0"], "BATTLEFIELD:F"));
        Assert.Equal(2, entered.State.StackItems.Count);
        Assert.Equal(bottom.StackItemId, entered.State.StackItems[0].StackItemId);
        var top = entered.State.StackItems[1];
        Assert.Equal("SHADOW", top.SourceObjectId);
        Assert.True(top.SourceConfirmed);
        Assert.Equal("F", top.PlayedFromStandby!.Battlefield.ObjectId);
        Assert.Equal("F", entered.State.ObjectLocations["SHADOW"].BattlefieldObjectId);
        Assert.Contains(entered.Events, e => e.Kind == "PERMANENT_CONFIRMED");
        Assert.DoesNotContain(entered.Events, e => e.Kind is "SPELL_DUEL_CLOSED" or "BATTLE_DAMAGE_ASSIGNMENT_OPENED");
        NextNotAdvanced(entered.State);
        OfficialGraveyardRecastTests.Restore(entered.State);
        var first = await OfficialGraveyardRecastTests.Top(entered.State);
        Assert.Equal(bottom.StackItemId, Assert.Single(first.State.StackItems).StackItemId);
        Assert.Equal(2, first.State.CardObjects[enemy].Damage);
        Assert.DoesNotContain("STUNNED", first.State.CardObjects[enemy].UntilEndOfTurnEffects);
        NextNotAdvanced(first.State);
        OfficialGraveyardRecastTests.Restore(first.State);
        var second = await OfficialGraveyardRecastTests.Top(first.State);
        Assert.Empty(second.State.StackItems);
        Assert.Contains("STUNNED", second.State.CardObjects[enemy].UntilEndOfTurnEffects);
        Assert.Equal(TimingStates.SpellDuelOpen, second.State.TimingState);
        Assert.Equal("P2", second.State.FocusPlayerId);
        var kinds = second.Events.Select(e => e.Kind).ToList();
        Assert.True(kinds.IndexOf("STACK_ITEM_RESOLVED") < kinds.IndexOf("ABILITY_RESOLVED"));
        NextNotAdvanced(second.State);
        OfficialGraveyardRecastTests.Restore(second.State);
        ArmamentMightTests.PublicRecovery(second.State);
    }

    internal static async Task AssertDirectRevealReplayRejected(string card)
    {
        var state = await StandbySourceRulesTests.ReadyShadow(card);
        var command = new RevealCardCommand("SHADOW", card, [], "STANDBY_REACTION", ["STANDBY_REVEAL_0"], "STACK");
        var accepted = await OfficialInsightAndSpellLockTests.Act(state, "P1", command);
        Assert.False(accepted.State.CardObjects["SHADOW"].IsFaceDown);
        Assert.Equal(state.RunePools["P1"], accepted.State.RunePools["P1"]);
        Assert.Equal(2, accepted.State.CardObjects["SHADOW"].ObjectGeneration);
        OfficialGraveyardRecastTests.Restore(accepted.State);
        await AssertRevealRejected(accepted.State, command, ErrorCodes.InvalidTarget, "面朝下");
    }

    internal static async Task AssertSessionRevealReplay(string card, bool stale)
    {
        var state = await StandbySourceRulesTests.ReadyShadow(card);
        var journal = new RecordingMatchJournal();
        var session = new MatchSession(state, new CoreRuleEngine(), journal);
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");
        var prompt = session.PromptFor("P1");
        Assert.Contains(CommandTypes.RevealCard, prompt.EnabledActions());
        var command = new RevealCardCommand("SHADOW", card, [], "STANDBY_REACTION", ["STANDBY_REVEAL_0"], "STACK");
        var payload = new Dictionary<string, object?>
        {
            ["cmdType"] = CommandTypes.RevealCard, ["sourceObjectId"] = command.SourceObjectId,
            ["cardNo"] = card, ["targetObjectIds"] = Array.Empty<string>(), ["mode"] = command.Mode,
            ["optionalCosts"] = command.OptionalCosts, ["destination"] = command.Destination,
            ["promptId"] = prompt.PromptId, ["snapshotTick"] = prompt.SnapshotTick
        };
        var raw = JsonSerializer.SerializeToElement(payload);
        var accepted = await session.SubmitAsync("P1", "accepted-shadow", command, raw, default);
        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        Assert.Equal(state.RunePools["P1"], accepted.State.RunePools["P1"]);
        Assert.False(accepted.State.CardObjects["SHADOW"].IsFaceDown);
        Assert.Equal(2, accepted.State.CardObjects["SHADOW"].ObjectGeneration);
        Assert.Equal(state.Tick + 1, accepted.State.Tick);
        Assert.Contains(accepted.Events, e => e.Kind == "CARD_REVEALED");
        Assert.Contains(accepted.Events, e => e.Kind == "CARD_PLAYED");
        if (card == "OGN·135/298")
        {
            Assert.Contains("SHADOW", accepted.State.PlayerZones["P1"].Battlefields);
            Assert.Equal("F", accepted.State.ObjectLocations["SHADOW"].BattlefieldObjectId);
        }
        else
        {
            Assert.Equal("STACK", accepted.State.ObjectLocations["SHADOW"].Zone);
            Assert.NotNull(Assert.Single(accepted.State.StackItems, i => i.SourceObjectId == "SHADOW").PlayedFromStandby);
        }
        OfficialGraveyardRecastTests.Restore(accepted.State);
        ArmamentMightTests.PublicRecovery(accepted.State);
        var acceptedEntry = Assert.Single(journal.Entries);
        Assert.True(acceptedEntry.Accepted);
        Assert.Equal("accepted-shadow", acceptedEntry.ClientIntentId);
        Assert.Equal(MatchStateHasher.Hash(accepted.State), MatchStateHasher.Hash(acceptedEntry.AuthoritativeState));
        Assert.Equal(MatchStateHasher.HashValue(raw), MatchStateHasher.HashValue(acceptedEntry.RawCommand));
        var acceptedHash = MatchStateHasher.Hash(accepted.State);
        var snapshots = MatchStateHasher.HashValue(accepted.Snapshots);
        var prompts = MatchStateHasher.HashValue(accepted.Prompts);
        var journalHash = MatchStateHasher.HashValue(journal.Entries);
        var replay = await session.SubmitAsync("P1", "accepted-shadow", command, raw, default);
        Assert.True(replay.Accepted, replay.ErrorMessage);
        Assert.Equal(acceptedHash, MatchStateHasher.Hash(replay.State));
        Assert.Equal(snapshots, MatchStateHasher.HashValue(replay.Snapshots));
        Assert.Equal(prompts, MatchStateHasher.HashValue(replay.Prompts));
        Assert.Equal(MatchStateHasher.HashValue(accepted.Events), MatchStateHasher.HashValue(replay.Events));
        Assert.Equal(journalHash, MatchStateHasher.HashValue(journal.Entries));
        if (stale)
        {
            var rejected = await session.SubmitAsync("P1", "stale-shadow", command, raw, default);
            Assert.False(rejected.Accepted);
            Assert.Equal(ErrorCodes.PromptExpired, rejected.ErrorCode);
            Assert.Empty(rejected.Events);
            Assert.Equal(acceptedHash, MatchStateHasher.Hash(rejected.State));
            Assert.Equal(snapshots, MatchStateHasher.HashValue(rejected.Snapshots));
            Assert.Equal(prompts, MatchStateHasher.HashValue(rejected.Prompts));
            Assert.Equal(2, journal.Entries.Count);
            Assert.Single(journal.Entries, e => !e.Accepted);
            journalHash = MatchStateHasher.HashValue(journal.Entries);
            var staleReplay = await session.SubmitAsync("P1", "stale-shadow", command, raw, default);
            Assert.False(staleReplay.Accepted);
            Assert.Equal(rejected.ErrorCode, staleReplay.ErrorCode);
            Assert.Equal(rejected.ErrorMessage, staleReplay.ErrorMessage);
            Assert.Equal(acceptedHash, MatchStateHasher.Hash(staleReplay.State));
            Assert.Empty(staleReplay.Events);
            Assert.Equal(journalHash, MatchStateHasher.HashValue(journal.Entries));
        }
        payload["clientNote"] = "changed-payload";
        var conflict = await session.SubmitAsync("P1", stale ? "stale-shadow" : "accepted-shadow", command,
            JsonSerializer.SerializeToElement(payload), default);
        Assert.False(conflict.Accepted);
        Assert.Equal(ErrorCodes.ClientIntentConflict, conflict.ErrorCode);
        Assert.Empty(conflict.Events);
        Assert.Equal(acceptedHash, MatchStateHasher.Hash(conflict.State));
        Assert.Equal(snapshots, MatchStateHasher.HashValue(conflict.Snapshots));
        Assert.Equal(prompts, MatchStateHasher.HashValue(conflict.Prompts));
        Assert.Equal(journalHash, MatchStateHasher.HashValue(journal.Entries));
    }

    internal static MatchState WithUnitInHand(MatchState state, string id, string cardNo, string player)
    {
        Assert.True(CardBehaviorRegistry.TryGetByCardNo(cardNo, out var printed));
        Assert.True(printed.PlaysSourceToBaseAsUnit);
        Assert.DoesNotContain(id, state.CardObjects.Keys);
        return state with
        {
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects)
            {
                [id] = new(id, cardNo: cardNo, power: printed.SourceUnitPower, manaCost: printed.ManaCost,
                    tags: new[] { CardObjectTags.UnitCard }.Concat(printed.SourceUnitTags.Split('|', StringSplitOptions.RemoveEmptyEntries)).ToArray(),
                    ownerId: player, controllerId: player)
            },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones)
            {
                [player] = state.PlayerZones[player] with { Hand = state.PlayerZones[player].Hand.Append(id).ToArray() }
            },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { [id] = new(player, "HAND") }
        };
    }

    // Explicit legal initial battlefield; this grants no hidden-card history or play permission.
    internal static MatchState WithControlledBattlefield(MatchState state, string player = "P1")
    {
        var cards = new Dictionary<string, CardObjectState>(state.CardObjects, StringComparer.Ordinal)
        {
            [BattlefieldId] = new(BattlefieldId, cardNo: "OGN·275/298",
                tags: [P6TokenFactoryCatalog.BattlefieldCardTag], ownerId: player, controllerId: player)
        };
        var zones = new Dictionary<string, PlayerZones>(state.PlayerZones, StringComparer.Ordinal)
        {
            [player] = state.PlayerZones[player] with
            {
                Battlefields = state.PlayerZones[player].Battlefields.Append(BattlefieldId).Distinct(StringComparer.Ordinal).ToArray()
            }
        };
        var locations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations, StringComparer.Ordinal)
        {
            [BattlefieldId] = new(player, "BATTLEFIELD", BattlefieldId)
        };
        return state with { CardObjects = cards, PlayerZones = zones, ObjectLocations = locations };
    }

    internal static void AssertHiddenBattlefieldPrivacy(ResolutionResult result, string source, string owner = "P1", string viewer = "P2")
    {
        var privatePlayer = Assert.IsType<Dictionary<string, object?>>(result.Snapshots[owner].Players[owner]);
        var privateObjects = Assert.IsType<Dictionary<string, object?>>(privatePlayer["objects"]);
        var privateCard = Assert.IsType<Dictionary<string, object?>>(privateObjects[source]);
        Assert.True(Assert.IsType<bool>(privateCard["isFaceDown"]));
        Assert.Equal(result.State.CardObjects[source].CardNo, privateCard["cardNo"]);
        var publicPlayer = Assert.IsType<Dictionary<string, object?>>(result.Snapshots[viewer].Players[owner]);
        var publicObjects = Assert.IsType<Dictionary<string, object?>>(publicPlayer["objects"]);
        Assert.DoesNotContain(source, publicObjects.Keys);
        var field = Assert.Single(Assert.IsAssignableFrom<IEnumerable<object?>>(result.Snapshots[viewer].Lanes["battlefields"])
            .OfType<Dictionary<string, object?>>(), f => Equals(f.GetValueOrDefault("battlefieldObjectId"), result.State.ObjectLocations[source].BattlefieldObjectId));
        var slot = Assert.Single(Assert.IsAssignableFrom<IEnumerable<object?>>(field["standbySlots"])
            .OfType<Dictionary<string, object?>>(), s => Equals(s.GetValueOrDefault("controllerId"), owner));
        Assert.False(Assert.IsType<bool>(slot["visible"]));
        Assert.Equal("HIDDEN", slot["state"]);
        Assert.True(Assert.IsType<bool>(slot["isFaceDown"]));
        foreach (var key in new[] { "objectId", "cardNo", "power", "tags", "manaCost" })
            Assert.DoesNotContain(key, slot.Keys);
    }
}
