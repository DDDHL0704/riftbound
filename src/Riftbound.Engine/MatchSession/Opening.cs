using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Riftbound.CardCatalog;
using Riftbound.Contracts;

namespace Riftbound.Engine;
public sealed partial class MatchSession
{

    private static MatchState RestoreState(MatchRecoveryFrame recovery)
    {
        if (recovery.AuthoritativeState is not null)
        {
            return recovery.AuthoritativeState with
            {
                Tick = recovery.CurrentTick
            };
        }

        throw new MatchSessionException(ErrorCodes.RecoveryInconsistent,
            "match recovery requires authoritative state with recorded rule input identity");
    }

    private string NextOpenSeat()
    {
        return seats.ContainsValue("P1") ? "P2" : "P1";
    }

    private static IReadOnlyDictionary<string, RunePool> RunePoolsForSeats(
        IReadOnlyDictionary<string, RunePool> current,
        IEnumerable<string> playerIds)
    {
        return playerIds.ToDictionary(
            playerId => playerId,
            playerId => current.TryGetValue(playerId, out var runePool) ? runePool : RunePool.Empty,
            StringComparer.Ordinal);
    }

    private static IReadOnlyDictionary<string, PlayerZones> PlayerZonesForSeats(
        IReadOnlyDictionary<string, PlayerZones> current,
        IEnumerable<string> playerIds)
    {
        return playerIds.ToDictionary(
            playerId => playerId,
            playerId => current.TryGetValue(playerId, out var zones) ? zones : PlayerZones.Empty,
            StringComparer.Ordinal);
    }

    private static IReadOnlyDictionary<string, int> PlayerScoresForSeats(
        IReadOnlyDictionary<string, int> current,
        IEnumerable<string> playerIds)
    {
        return playerIds.ToDictionary(
            playerId => playerId,
            playerId => current.TryGetValue(playerId, out var score) ? score : 0,
            StringComparer.Ordinal);
    }

    private static IReadOnlyDictionary<string, int> PlayerExperienceForSeats(
        IReadOnlyDictionary<string, int> current,
        IEnumerable<string> playerIds)
    {
        return playerIds.ToDictionary(
            playerId => playerId,
            playerId => current.TryGetValue(playerId, out var experience) ? experience : 0,
            StringComparer.Ordinal);
    }

    private static IReadOnlyDictionary<string, int> PlayerCardsPlayedThisTurnForSeats(
        IReadOnlyDictionary<string, int> current,
        IEnumerable<string> playerIds)
    {
        return playerIds
            .Where(playerId => current.TryGetValue(playerId, out var count) && count > 0)
            .ToDictionary(
                playerId => playerId,
                playerId => current[playerId],
                StringComparer.Ordinal);
    }

    private static IReadOnlyDictionary<string, OfficialDecklist> PlayerDecklistsForSeats(
        IReadOnlyDictionary<string, OfficialDecklist> current,
        IEnumerable<string> playerIds)
    {
        var seatSet = playerIds.ToHashSet(StringComparer.Ordinal);
        return current
            .Where(entry => seatSet.Contains(entry.Key))
            .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
    }

    private PlayerSessionDto PlayerSessionFor(string playerId)
    {
        if (!reconnectTokens.TryGetValue(playerId, out var reconnectToken)
            || string.IsNullOrWhiteSpace(reconnectToken))
        {
            reconnectToken = NewReconnectToken();
            reconnectTokens[playerId] = reconnectToken;
        }

        return new PlayerSessionDto(playerId, seats[playerId], reconnectToken);
    }

    private async ValueTask<PlayerSessionDto> RotateReconnectTokenAsync(
        string playerId,
        CancellationToken cancellationToken)
    {
        PlayerSessionDto playerSession;
        lock (seatGate)
        {
            reconnectTokens[playerId] = NewReconnectToken();
            playerSession = PlayerSessionFor(playerId);
        }

        await PersistPlayerSessionAsync(playerSession, cancellationToken).ConfigureAwait(false);
        return playerSession;
    }

    private ValueTask PersistPlayerSessionAsync(
        PlayerSessionDto playerSession,
        CancellationToken cancellationToken)
    {
        return playerStore.SavePlayerSessionAsync(
            RoomId,
            playerSession.PlayerId,
            playerSession.Seat,
            ReconnectTokenHasher.Hash(playerSession.ReconnectToken),
            cancellationToken,
            openingSeed);
    }

    private static async ValueTask<OfficialCardCatalog> LoadOfficialCatalogAsync(CancellationToken cancellationToken)
    {
        if (officialCatalog is not null)
        {
            return officialCatalog;
        }

        await OfficialCatalogGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            officialCatalog ??= await OfficialCardCatalog.LoadRuntimeAsync(cancellationToken)
                .ConfigureAwait(false);
            return officialCatalog;
        }
        finally
        {
            OfficialCatalogGate.Release();
        }
    }

    private static bool HasOfficialDecklistsForAllSeats(MatchState state)
    {
        return state.Seats.Count == 2
            && state.Seats.Keys.All(playerId => state.PlayerDecklists.ContainsKey(playerId));
    }

    private static bool OfficialDecklistsEqual(OfficialDecklist left, OfficialDecklist right)
    {
        return string.Equals(left.LegendCardNo, right.LegendCardNo, StringComparison.Ordinal)
            && string.Equals(left.ChampionCardNo, right.ChampionCardNo, StringComparison.Ordinal)
            && left.MainDeck.SequenceEqual(right.MainDeck, StringComparer.Ordinal)
            && left.RuneDeck.SequenceEqual(right.RuneDeck, StringComparer.Ordinal)
            && left.Battlefields.SequenceEqual(right.Battlefields, StringComparer.Ordinal);
    }

    private static OfficialOpeningBuildResult BuildOfficialOpeningState(
        MatchState readyState,
        OfficialCardCatalog catalog)
    {
        var seed = readyState.Seed;
        var rngCursor = readyState.RngCursor;
        var seatOrder = readyState.Seats
            .OrderBy(entry => entry.Value, StringComparer.Ordinal)
            .Select(entry => entry.Key)
            .ToArray();
        var turnOrder = RandomizeStable(seatOrder, seed, rngCursor, "TURN_ORDER");
        if (seatOrder.Length > 1)
        {
            rngCursor++;
        }

        var activePlayerId = turnOrder[0];
        var secondActionPlayerId = turnOrder.Count > 1 ? turnOrder[1] : null;
        var playerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal);
        var cardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal);
        var objectLocations = new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal);
        var events = new List<GameEvent>
        {
            new(
                "OFFICIAL_OPENING_STARTED",
                "正式 1v1 开局流程开始",
                new Dictionary<string, object?>
                {
                    ["activePlayerId"] = activePlayerId,
                    ["secondActionPlayerId"] = secondActionPlayerId,
                    ["openingHandCount"] = OfficialDeckValidator.OpeningHandCount
                })
        };

        var cardsByNo = catalog.Cards.ToDictionary(card => card.CardNo, StringComparer.Ordinal);
        foreach (var playerId in seatOrder)
        {
            var decklist = readyState.PlayerDecklists[playerId];
            var playerOpening = BuildOfficialPlayerOpening(
                playerId,
                decklist,
                cardsByNo,
                seed,
                rngCursor);
            rngCursor = playerOpening.RngCursor;
            playerZones[playerId] = playerOpening.Zones;
            foreach (var (objectId, cardObject) in playerOpening.CardObjects)
            {
                cardObjects[objectId] = cardObject;
            }
            foreach (var (objectId, location) in playerOpening.ObjectLocations)
            {
                objectLocations[objectId] = location;
            }

            events.AddRange(playerOpening.Events);
        }

        var runePools = seatOrder.ToDictionary(playerId => playerId, _ => RunePool.Empty, StringComparer.Ordinal);
        var playerScores = seatOrder.ToDictionary(playerId => playerId, _ => 0, StringComparer.Ordinal);
        var playerExperience = seatOrder.ToDictionary(playerId => playerId, _ => 0, StringComparer.Ordinal);
        var nextState = readyState with
        {
            ActivePlayerId = activePlayerId,
            TurnPlayerId = activePlayerId,
            TurnNumber = 1,
            Status = MatchStatuses.InProgress,
            Phase = MatchPhases.Mulligan,
            TimingState = TimingStates.Mulligan,
            RunePools = runePools,
            PlayerZones = playerZones,
            ObjectLocations = objectLocations,
            PlayerScores = playerScores,
            PlayerExperience = playerExperience,
            PlayerCardsPlayedThisTurn = new Dictionary<string, int>(StringComparer.Ordinal),
            CardObjects = cardObjects,
            PriorityPlayerId = null,
            PassedPriorityPlayerIds = [],
            StackItems = [],
            FocusPlayerId = null,
            PassedFocusPlayerIds = [],
            WinnerPlayerId = null,
            DestroyedUnitOwnerIdsThisTurn = [],
            Seed = seed,
            RngCursor = rngCursor,
            UntilEndOfTurnEffects = [],
            ExtraTurnPlayerId = null,
            MulliganCompletedPlayerIds = [],
            OpeningSecondActionPlayerId = secondActionPlayerId
        };

        return new OfficialOpeningBuildResult(nextState, events);
    }

    private static OfficialPlayerOpeningBuildResult BuildOfficialPlayerOpening(
        string playerId,
        OfficialDecklist decklist,
        IReadOnlyDictionary<string, OfficialCard> cardsByNo,
        long seed,
        long rngCursor)
    {
        var cardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal);
        var objectLocations = new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal);
        var mainCardNos = decklist.MainDeck.ToList();
        var championIndex = mainCardNos.FindIndex(cardNo => string.Equals(cardNo, decklist.ChampionCardNo, StringComparison.Ordinal));
        if (championIndex >= 0)
        {
            mainCardNos.RemoveAt(championIndex);
        }

        var legendObjectId = OfficialObjectId(playerId, "LEGEND", decklist.LegendCardNo, 1);
        var championObjectId = OfficialObjectId(playerId, "CHAMPION", decklist.ChampionCardNo, 1);
        cardObjects[legendObjectId] = OfficialCardObject(legendObjectId, playerId, cardsByNo[decklist.LegendCardNo]);
        cardObjects[championObjectId] = OfficialCardObject(championObjectId, playerId, cardsByNo[decklist.ChampionCardNo]);
        objectLocations[legendObjectId] = new ObjectLocationState(playerId, "LEGEND");
        objectLocations[championObjectId] = new ObjectLocationState(playerId, "CHAMPION");

        var mainObjectIds = new List<string>();
        for (var index = 0; index < mainCardNos.Count; index++)
        {
            var cardNo = mainCardNos[index];
            var objectId = OfficialObjectId(playerId, "MAIN", cardNo, index + 1);
            mainObjectIds.Add(objectId);
            cardObjects[objectId] = OfficialCardObject(objectId, playerId, cardsByNo[cardNo]);
        }

        var shuffledMain = RandomizeStable(mainObjectIds, seed, rngCursor, $"{playerId}:MAIN_DECK");
        if (mainObjectIds.Count > 1)
        {
            rngCursor++;
        }

        var hand = shuffledMain.Take(OfficialDeckValidator.OpeningHandCount).ToArray();
        var remainingMain = shuffledMain.Skip(hand.Length).ToArray();

        var runeObjectIds = new List<string>();
        for (var index = 0; index < decklist.RuneDeck.Count; index++)
        {
            var cardNo = decklist.RuneDeck[index];
            var objectId = OfficialObjectId(playerId, "RUNE", cardNo, index + 1);
            runeObjectIds.Add(objectId);
            cardObjects[objectId] = OfficialCardObject(objectId, playerId, cardsByNo[cardNo]);
        }

        var shuffledRunes = RandomizeStable(runeObjectIds, seed, rngCursor, $"{playerId}:RUNE_DECK");
        if (runeObjectIds.Count > 1)
        {
            rngCursor++;
        }

        var battlefieldCardNos = RandomizeStable(decklist.Battlefields, seed, rngCursor, $"{playerId}:BATTLEFIELDS");
        if (decklist.Battlefields.Count > 1)
        {
            rngCursor++;
        }

        var selectedBattlefieldCardNo = battlefieldCardNos[0];
        var battlefieldObjectId = OfficialObjectId(playerId, "BATTLEFIELD", selectedBattlefieldCardNo, 1);
        cardObjects[battlefieldObjectId] = OfficialCardObject(
            battlefieldObjectId,
            playerId,
            cardsByNo[selectedBattlefieldCardNo]);
        objectLocations[battlefieldObjectId] = new ObjectLocationState(
            playerId,
            "BATTLEFIELD",
            battlefieldObjectId);
        foreach (var objectId in remainingMain)
        {
            objectLocations[objectId] = new ObjectLocationState(playerId, "MAIN_DECK");
        }
        foreach (var objectId in hand)
        {
            objectLocations[objectId] = new ObjectLocationState(playerId, "HAND");
        }
        foreach (var objectId in shuffledRunes)
        {
            objectLocations[objectId] = new ObjectLocationState(playerId, "RUNE_DECK");
        }

        var zones = new PlayerZones(
            remainingMain,
            shuffledRunes,
            hand,
            [],
            [battlefieldObjectId],
            [],
            [],
            [legendObjectId],
            [championObjectId]);

        var events = new List<GameEvent>
        {
            new(
                "OFFICIAL_BATTLEFIELD_SELECTED",
                $"{playerId} 随机选择 1 张战场进入本局",
                new Dictionary<string, object?>
                {
                    ["playerId"] = playerId,
                    ["battlefieldObjectId"] = battlefieldObjectId,
                    ["removedBattlefieldCount"] = Math.Max(0, decklist.Battlefields.Count - 1)
                }),
            new(
                "OPENING_HAND_DRAWN",
                $"{playerId} 抽取起手牌",
                new Dictionary<string, object?>
                {
                    ["playerId"] = playerId,
                    ["drawCount"] = hand.Length
                })
        };

        return new OfficialPlayerOpeningBuildResult(zones, cardObjects, objectLocations, events, rngCursor);
    }

    private static CardObjectState OfficialCardObject(string objectId, string playerId, OfficialCard card)
        => PrintedCardFactory.Create(objectId, playerId, card);

    private static string OfficialObjectId(string playerId, string zone, string cardNo, int index)
    {
        var normalized = new string(cardNo
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '-')
            .ToArray())
            .Trim('-');
        return $"{playerId}-{zone}-{normalized}-{index:00}";
    }

    private static IReadOnlyList<T> RandomizeStable<T>(
        IReadOnlyList<T> values,
        long seed,
        long rngCursor,
        string scope)
    {
        if (values.Count <= 1)
        {
            return values.ToArray();
        }

        var shuffled = values.ToArray();
        ulong sampleIndex = 0;
        ulong NextSample()
        {
            var input = FormattableString.Invariant($"opening-v1:{seed}:{rngCursor}:{scope}:{sampleIndex++}");
            return BinaryPrimitives.ReadUInt64BigEndian(SHA256.HashData(Encoding.UTF8.GetBytes(input)));
        }
        for (var index = shuffled.Length - 1; index > 0; index--)
        {
            var selected = NextOpeningIndex(index + 1, NextSample);
            (shuffled[index], shuffled[selected]) = (shuffled[selected], shuffled[index]);
        }
        return shuffled;
    }

    private static int NextOpeningIndex(int exclusiveMax, Func<ulong> nextSample)
    {
        // Rejection sampling avoids modulo bias. The opening stream excludes
        // card names/physical IDs and is stable across platforms.
        var bound = (ulong)exclusiveMax;
        var threshold = unchecked(0UL - bound) % bound;
        ulong sample;
        do { sample = nextSample(); } while (sample < threshold);
        return (int)(sample % bound);
    }

    private static long CreatePrivateOpeningSeed()
    {
        long seed;
        do { seed = BinaryPrimitives.ReadInt64BigEndian(RandomNumberGenerator.GetBytes(sizeof(long))); }
        while (seed == 0);
        return seed;
    }
}
