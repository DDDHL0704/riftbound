using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class FullGameEndToEndTests
{
    [Fact]
    public async Task OfficialLowCurveDecksSkipNoLegalBattleAndReachMatchResultThroughServerPrompts()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var deck = BuildLowCurveOfficialDeck(catalog);
        var session = new MatchSession("b0-full-game-official-low-curve-room", new CoreRuleEngine());
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        var p1Submit = await SubmitDeckAsync(session, "P1", deck, "b0-submit-p1");
        var p2Submit = await SubmitDeckAsync(session, "P2", deck, "b0-submit-p2");
        AssertAccepted(p1Submit);
        AssertAccepted(p2Submit);

        AssertAccepted(await session.ReadyAsync("P1", "b0-ready-p1", RawCommand(CommandTypes.Ready), CancellationToken.None));
        var ready = await session.ReadyAsync("P2", "b0-ready-p2", RawCommand(CommandTypes.Ready), CancellationToken.None);
        AssertAccepted(ready);
        AssertNoHiddenZoneLeak(ready);

        var activePlayerId = ready.State.ActivePlayerId;
        var secondPlayerId = ready.State.OpeningSecondActionPlayerId!;
        var activeMulligan = await session.SubmitAsync(
            activePlayerId,
            "b0-mulligan-active",
            new MulliganCommand([]),
            RawCommand(CommandTypes.Mulligan),
            CancellationToken.None);
        AssertAccepted(activeMulligan);
        AssertNoHiddenZoneLeak(activeMulligan);

        var secondMulligan = await session.SubmitAsync(
            secondPlayerId,
            "b0-mulligan-second",
            new MulliganCommand([]),
            RawCommand(CommandTypes.Mulligan),
            CancellationToken.None);
        AssertAccepted(secondMulligan);
        AssertNoHiddenZoneLeak(secondMulligan);
        Assert.Equal(MatchPhases.Main, secondMulligan.State.Phase);

        var result = secondMulligan;
        result = await PreparePlayerBoardAsync(session, result.State.ActivePlayerId, result, "first", playUnitToBattlefield: true);
        var nextPlayerId = OpponentOf(result.State, result.State.ActivePlayerId);
        result = await EndTurnAsync(session, result.State.ActivePlayerId, "b0-end-first-player");
        AssertNoHiddenZoneLeak(result);

        Assert.Equal(nextPlayerId, result.State.ActivePlayerId);
        result = await PreparePlayerBoardAsync(session, result.State.ActivePlayerId, result, "second", playUnitToBattlefield: false);
        result = await MoveBaseUnitToOpponentBattlefieldAsync(session, result.State.ActivePlayerId, result);
        result = await PassOpenSpellDuelAsync(session, result);
        Assert.Contains(result.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLE_SKIPPED", StringComparison.Ordinal));
        Assert.False(result.State.PendingTaskQueue.HasTasks);
        Assert.False(result.State.PendingTaskQueue.IsBlocking);
        Assert.Equal("IDLE", result.State.PendingTaskQueue.Phase);
        Assert.DoesNotContain(result.State.PendingTaskQueue.Tasks, task =>
            string.Equals(task.Kind, "START_BATTLE", StringComparison.Ordinal));
        Assert.DoesNotContain("DECLARE_BATTLE", result.Prompts["P1"].Actions);
        Assert.DoesNotContain("DECLARE_BATTLE", result.Prompts["P2"].Actions);
        Assert.True(result.Prompts[result.State.ActivePlayerId].Actionable);
        Assert.DoesNotContain("WAIT", result.Prompts[result.State.ActivePlayerId].Actions);
        AssertNoHiddenZoneLeak(result);

        var winner = OpponentOf(result.State, result.State.ActivePlayerId);
        var surrender = await session.SubmitAsync(
            result.State.ActivePlayerId,
            "b0-surrender-after-battle",
            new SurrenderCommand(),
            RawCommand(CommandTypes.Surrender),
            CancellationToken.None);
        AssertAccepted(surrender);
        Assert.Equal(MatchStatuses.Finished, surrender.State.Status);
        Assert.Equal(winner, surrender.State.WinnerPlayerId);
        Assert.Contains(surrender.Events, gameEvent => string.Equals(gameEvent.Kind, "MATCH_WON", StringComparison.Ordinal));
        AssertNoHiddenZoneLeak(surrender);
    }

    private static async ValueTask<ResolutionResult> PreparePlayerBoardAsync(
        MatchSession session,
        string playerId,
        ResolutionResult current,
        string label,
        bool playUnitToBattlefield)
    {
        var result = current;
        result = await TapAllAvailableRunesAsync(session, playerId, result, $"b0-{label}-tap");
        result = await TryPlayFirstUnitAsync(session, playerId, result, $"b0-{label}-play-unit", playUnitToBattlefield);
        return result;
    }

    private static async ValueTask<ResolutionResult> TapAllAvailableRunesAsync(
        MatchSession session,
        string playerId,
        ResolutionResult current,
        string intentPrefix)
    {
        var result = current;
        for (var index = 0; index < 10; index++)
        {
            var prompt = result.Prompts[playerId];
            var candidate = EnabledCandidate(prompt, CommandTypes.TapRune);
            var sourceObjectId = candidate?.Sources?.FirstOrDefault()?.Id;
            if (sourceObjectId is null)
            {
                return result;
            }

            result = await session.SubmitAsync(
                playerId,
                $"{intentPrefix}-{index}",
                new TapRuneCommand(sourceObjectId),
                RawCommand(CommandTypes.TapRune),
                CancellationToken.None);
            AssertAccepted(result);
            AssertNoHiddenZoneLeak(result);
        }

        throw new InvalidOperationException("B0 auto-driver exceeded tap-rune guard.");
    }

    private static async ValueTask<ResolutionResult> TryPlayFirstUnitAsync(
        MatchSession session,
        string playerId,
        ResolutionResult current,
        string intentPrefix,
        bool playUnitToBattlefield)
    {
        var playCandidate = EnabledCandidate(current.Prompts[playerId], CommandTypes.PlayCard);
        if (playCandidate?.Sources is not { Count: > 0 } sources)
        {
            throw new InvalidOperationException($"B0 auto-driver could not find an enabled PLAY_CARD source for {playerId}.");
        }

        var destination = playUnitToBattlefield
            ? BattlefieldDestinationFor(current.State, playerId)
            : playCandidate.Destinations?.FirstOrDefault(choice => string.Equals(choice.Id, "BASE", StringComparison.Ordinal))?.Id ?? "BASE";
        for (var index = 0; index < sources.Count; index++)
        {
            var sourceObjectId = sources[index].Id;
            if (!current.State.CardObjects.TryGetValue(sourceObjectId, out var cardObject)
                || string.IsNullOrWhiteSpace(cardObject.CardNo))
            {
                continue;
            }

            var attempted = await session.SubmitAsync(
                playerId,
                $"{intentPrefix}-attempt-{index}",
                new PlayCardCommand(sourceObjectId, cardObject.CardNo, [], Destination: destination),
                RawCommand(CommandTypes.PlayCard),
                CancellationToken.None);
            if (!attempted.Accepted)
            {
                continue;
            }

            AssertNoHiddenZoneLeak(attempted);
            return await ResolveStackPassPassAsync(session, attempted, $"{intentPrefix}-resolve-{index}");
        }

        throw new InvalidOperationException($"B0 auto-driver could not play any exposed PLAY_CARD source for {playerId}.");
    }

    private static async ValueTask<ResolutionResult> ResolveStackPassPassAsync(
        MatchSession session,
        ResolutionResult current,
        string intentPrefix)
    {
        var result = current;
        for (var index = 0; index < 20; index++)
        {
            if (result.State.StackItems.Count == 0 && string.IsNullOrWhiteSpace(result.State.PriorityPlayerId))
            {
                return result;
            }

            var priorityPlayerId = result.State.PriorityPlayerId;
            if (string.IsNullOrWhiteSpace(priorityPlayerId))
            {
                throw new InvalidOperationException("B0 auto-driver found stack items without a priority player.");
            }

            result = await session.SubmitAsync(
                priorityPlayerId,
                $"{intentPrefix}-pass-priority-{index}",
                new PassPriorityCommand(),
                RawCommand(CommandTypes.PassPriority),
                CancellationToken.None);
            AssertAccepted(result);
            AssertNoHiddenZoneLeak(result);
        }

        throw new InvalidOperationException("B0 auto-driver exceeded stack pass guard.");
    }

    private static string BattlefieldDestinationFor(MatchState state, string playerId)
    {
        var battlefieldObjectId = state.PlayerZones[playerId].Battlefields
            .FirstOrDefault(objectId => state.CardObjects.TryGetValue(objectId, out var cardObject)
                && cardObject.Tags.Contains("CARD_TYPE:BATTLEFIELD", StringComparer.Ordinal))
            ?? throw new InvalidOperationException($"B0 auto-driver could not find a battlefield card for {playerId}.");
        return $"BATTLEFIELD:{battlefieldObjectId}";
    }

    private static async ValueTask<ResolutionResult> EndTurnAsync(
        MatchSession session,
        string playerId,
        string intentId)
    {
        var result = await session.SubmitAsync(
            playerId,
            intentId,
            new EndTurnCommand(),
            RawCommand(CommandTypes.EndTurn),
            CancellationToken.None);
        AssertAccepted(result);
        return result;
    }

    private static async ValueTask<ResolutionResult> MoveBaseUnitToOpponentBattlefieldAsync(
        MatchSession session,
        string playerId,
        ResolutionResult current)
    {
        var zones = current.State.PlayerZones[playerId];
        var sourceObjectId = zones.Base.FirstOrDefault(objectId => IsReadyUnit(current.State, objectId))
            ?? throw new InvalidOperationException($"B0 auto-driver could not find a ready base unit for {playerId}.");
        var opponentId = OpponentOf(current.State, playerId);
        var opponentBattlefieldObjectId = current.State.PlayerZones[opponentId].Battlefields
            .FirstOrDefault(objectId => current.State.CardObjects.TryGetValue(objectId, out var cardObject)
                && cardObject.Tags.Contains("CARD_TYPE:BATTLEFIELD", StringComparer.Ordinal))
            ?? throw new InvalidOperationException($"B0 auto-driver could not find opponent battlefield for {playerId}.");

        var result = await session.SubmitAsync(
            playerId,
            "b0-move-unit-to-opponent-battlefield",
            new MoveUnitCommand(sourceObjectId, "BASE", $"BATTLEFIELD:{opponentBattlefieldObjectId}", []),
            RawCommand(CommandTypes.MoveUnit),
            CancellationToken.None);
        AssertAccepted(result);
        AssertNoHiddenZoneLeak(result);
        return result;
    }

    private static async ValueTask<ResolutionResult> PassOpenSpellDuelAsync(
        MatchSession session,
        ResolutionResult current)
    {
        var result = current;
        for (var index = 0; index < 20; index++)
        {
            if (!string.Equals(result.State.TimingState, TimingStates.SpellDuelOpen, StringComparison.Ordinal)
                && string.IsNullOrWhiteSpace(result.State.FocusPlayerId))
            {
                return result;
            }

            var focusPlayerId = result.State.FocusPlayerId;
            if (string.IsNullOrWhiteSpace(focusPlayerId))
            {
                throw new InvalidOperationException("B0 auto-driver found spell duel without a focus player.");
            }

            result = await session.SubmitAsync(
                focusPlayerId,
                $"b0-pass-focus-{index}",
                new PassFocusCommand(),
                RawCommand(CommandTypes.PassFocus),
                CancellationToken.None);
            AssertAccepted(result);
            AssertNoHiddenZoneLeak(result);
        }

        throw new InvalidOperationException("B0 auto-driver exceeded spell-duel pass guard.");
    }

    private static ActionPromptCandidateDto? EnabledCandidate(ActionPromptDto prompt, string action)
    {
        return prompt.Candidates?.FirstOrDefault(candidate =>
            candidate.Enabled && string.Equals(candidate.Action, action, StringComparison.Ordinal));
    }

    private static string? PlayerWithEnabledCandidate(ResolutionResult result, string action)
    {
        return result.Prompts
            .Where(entry => EnabledCandidate(entry.Value, action) is not null)
            .Select(entry => entry.Key)
            .FirstOrDefault();
    }

    private static bool IsReadyUnit(MatchState state, string objectId)
    {
        return state.CardObjects.TryGetValue(objectId, out var cardObject)
            && cardObject.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal)
            && !cardObject.IsExhausted
            && !cardObject.IsFaceDown;
    }

    private static string OpponentOf(MatchState state, string playerId)
    {
        return state.Seats.Keys.Single(seatPlayerId => !string.Equals(seatPlayerId, playerId, StringComparison.Ordinal));
    }

    private static async ValueTask<ResolutionResult> SubmitDeckAsync(
        MatchSession session,
        string playerId,
        OfficialDecklist deck,
        string intentId)
    {
        return await session.SubmitDeckAsync(
            playerId,
            intentId,
            new SubmitDeckCommand(
                deck.LegendCardNo,
                deck.ChampionCardNo,
                deck.MainDeck,
                deck.RuneDeck,
                deck.Battlefields),
            JsonSerializer.SerializeToElement(new
            {
                cmdType = CommandTypes.SubmitDeck,
                legendCardNo = deck.LegendCardNo,
                championCardNo = deck.ChampionCardNo,
                mainDeck = deck.MainDeck,
                runeDeck = deck.RuneDeck,
                battlefields = deck.Battlefields
            }),
            CancellationToken.None);
    }

    private static OfficialDecklist BuildLowCurveOfficialDeck(OfficialCardCatalog catalog)
    {
        const string legendCardNo = "UNL-181/219";
        const string championCardNo = "UNL-022/219";
        var legend = catalog.Cards.Single(card => string.Equals(card.CardNo, legendCardNo, StringComparison.Ordinal));
        var allowedColors = legend.CardColorList.ToHashSet(StringComparer.Ordinal);
        var cardsByNo = catalog.Cards
            .Where(card => !string.IsNullOrWhiteSpace(card.CardNo))
            .ToDictionary(card => card.CardNo, StringComparer.Ordinal);
        var implementedLowCurveUnits = CardBehaviorRegistry.GetAll()
            .Where(behavior => behavior.PlaysSourceToBaseAsUnit)
            .Where(behavior => behavior.RequiredTargetCount == 0 && behavior.MinTargetCount <= 0)
            .Where(behavior => string.IsNullOrWhiteSpace(behavior.Mode))
            .Where(behavior => behavior.ManaCost <= 2)
            .Select(behavior => behavior.CardNo)
            .Distinct(StringComparer.Ordinal)
            .Where(cardsByNo.ContainsKey)
            .Select(cardNo => cardsByNo[cardNo])
            .Where(card => IsMainDeckCandidate(card, allowedColors))
            .OrderBy(card => card.Energy ?? 0)
            .ThenBy(card => card.CardNo, StringComparer.Ordinal)
            .ToArray();

        var mainDeck = new List<string> { championCardNo };
        var nameCounts = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            [cardsByNo[championCardNo].CardName] = 1
        };
        foreach (var card in implementedLowCurveUnits)
        {
            while (mainDeck.Count < OfficialDeckValidator.MinimumMainDeckCount
                && (!nameCounts.TryGetValue(card.CardName, out var count)
                    || count < OfficialDeckValidator.DefaultMaxCopiesByName))
            {
                mainDeck.Add(card.CardNo);
                nameCounts[card.CardName] = nameCounts.TryGetValue(card.CardName, out var current) ? current + 1 : 1;
            }

            if (mainDeck.Count >= OfficialDeckValidator.MinimumMainDeckCount)
            {
                break;
            }
        }

        Assert.Equal(OfficialDeckValidator.MinimumMainDeckCount, mainDeck.Count);

        var runeDeck = catalog.Cards
            .Where(card => string.Equals(card.CardCategoryName, "符文", StringComparison.Ordinal))
            .Where(card => TraitsAllowed(card, allowedColors))
            .OrderBy(card => card.CardNo, StringComparer.Ordinal)
            .Select(card => card.CardNo)
            .Take(OfficialDeckValidator.RuneDeckCount)
            .ToArray();
        Assert.Equal(OfficialDeckValidator.RuneDeckCount, runeDeck.Length);

        var battlefields = catalog.Cards
            .Where(card => string.Equals(card.CardCategoryName, "战场", StringComparison.Ordinal))
            .GroupBy(card => card.CardName, StringComparer.Ordinal)
            .Select(group => group.OrderBy(card => card.CardNo, StringComparer.Ordinal).First())
            .OrderBy(card => card.CardNo, StringComparer.Ordinal)
            .Take(OfficialDeckValidator.BattlefieldCount)
            .Select(card => card.CardNo)
            .ToArray();
        Assert.Equal(OfficialDeckValidator.BattlefieldCount, battlefields.Length);

        var deck = new OfficialDecklist(legendCardNo, championCardNo, mainDeck, runeDeck, battlefields);
        var validation = OfficialDeckValidator.Validate(deck, catalog);
        Assert.True(validation.IsValid, string.Join("; ", validation.Errors));
        return deck;
    }

    private static bool IsMainDeckCandidate(OfficialCard card, HashSet<string> allowedColors)
    {
        return card.CardCategoryName is "单位" or "英雄单位"
            && !card.CardCategoryName.StartsWith("专属", StringComparison.Ordinal)
            && card.CardGroupLimit != 1
            && !card.CardEffect.Contains("{{唯我}}", StringComparison.Ordinal)
            && TraitsAllowed(card, allowedColors);
    }

    private static bool TraitsAllowed(OfficialCard card, HashSet<string> allowedColors)
    {
        return card.CardColorList.All(color => string.Equals(color, "colorless", StringComparison.Ordinal)
            || allowedColors.Contains(color));
    }

    private static JsonElement RawCommand(string cmdType)
    {
        return JsonSerializer.SerializeToElement(new { cmdType });
    }

    private static void AssertAccepted(ResolutionResult result)
    {
        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Null(result.ErrorCode);
    }

    private static void AssertNoHiddenZoneLeak(ResolutionResult result)
    {
        foreach (var viewerId in result.State.Seats.Keys)
        {
            var snapshotJson = JsonSerializer.Serialize(result.Snapshots[viewerId]);
            foreach (var (playerId, zones) in result.State.PlayerZones)
            {
                if (string.Equals(playerId, viewerId, StringComparison.Ordinal))
                {
                    continue;
                }

                foreach (var objectId in zones.Hand.Concat(zones.MainDeck).Concat(zones.RuneDeck))
                {
                    Assert.DoesNotContain(objectId, snapshotJson, StringComparison.Ordinal);
                }
            }
        }
    }
}
