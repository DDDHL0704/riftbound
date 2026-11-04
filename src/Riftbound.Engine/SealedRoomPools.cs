using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using Riftbound.CardCatalog;
using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial record MatchState
{
    // Private authoritative data, never serialized as part of an event or prompt.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, SealedPoolGeneration>? PlayerSealedPools { get; init; }
}

public static class SealedRoomPools
{
    private const string MixedGeneratorVersion = SealedPackGenerator.Version + "+mixed-v1";

    public static string? OptionError(MatchSessionOptions options)
    {
        if (options.DeckFormat != OfficialDeckFormat.Sealed)
            return options.SealedSetCode is not null || options.SealedPacks is not null || options.SealedBestOf != 0
                ? "非现开房间不能设置现开系列或多局配置。" : null;
        if (options.AllowLegacyReadyWithoutDeck || !options.HideSubmittedDeckIdentities)
            return "现开房间必须校验卡组并隐藏提交内容。";
        if (options.SealedBestOf is not (1 or 3)) return "现开比赛的 bestOf 必须为 1 或 3。";
        if (options.SealedPacks is { } choices)
        {
            if (options.SealedSetCode is not null) return "自定义卡包组合不能同时指定单一系列 setCode。";
            if (choices.Count == 0 || choices.Any(choice => choice is null))
                return "自定义卡包组合不能为空，且每项都必须完整。";
            var seen = new HashSet<string>(StringComparer.Ordinal);
            long total = 0;
            foreach (var choice in choices)
            {
                if (choice.Count < 1) return "每个系列的卡包数量必须至少为 1。";
                if (!SealedSetCatalog.TryGet(choice.SetCode, out var set) || !set.Enabled
                    || choice.SetCode != set.SetCode)
                    return $"系列 {choice.SetCode} 尚未开放现开赛。";
                if (!seen.Add(set.SetCode)) return "自定义卡包组合中的系列不能重复。";
                total += choice.Count;
            }
            if (total is < 6 or > 12) return "自定义卡包组合的总包数必须为 6 至 12 包。";
            return null;
        }
        if (!SealedSetCatalog.TryGet(options.SealedSetCode, out var singleSet) || !singleSet.Enabled
            || options.SealedSetCode != singleSet.SetCode)
            return "该系列尚未开放现开赛。";
        return null;
    }

    public static void RequireOptions(MatchSessionOptions options)
    {
        if (OptionError(options) is { } error) throw new ArgumentException(error, nameof(options));
    }

    internal static MatchState AddPlayer(MatchState state, string playerId, OfficialCardCatalog catalog)
    {
        var options = state.SessionOptions!;
        RequireOptions(options);
        var pools = (state.PlayerSealedPools ?? ImmutableDictionary<string, SealedPoolGeneration>.Empty)
            .ToImmutableSortedDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        if (pools.ContainsKey(playerId)) throw new InvalidOperationException("Player already has a sealed pool.");
        var seed = options.SealedPacks is null
            ? PoolSeed(state.Seed, options.SealedSetCode!, state.Seats[playerId])
            : MixedPoolSeed(state.Seed, options.SealedPacks, state.Seats[playerId]);
        var generated = options.SealedPacks is null
            ? SealedPackGenerator.Generate(catalog, options.SealedSetCode!, seed)
            : GenerateMixed(catalog, options.SealedPacks, seed);
        return state with { PlayerSealedPools = pools.Add(playerId,
            generated) };
    }

    private static string PoolSeed(long openingSeed, string setCode, string seat)
    {
        var key = new byte[sizeof(long)];
        BinaryPrimitives.WriteInt64LittleEndian(key, openingSeed);
        return Convert.ToHexStringLower(HMACSHA256.HashData(key,
            Encoding.UTF8.GetBytes($"sealed-pool-v1\0{setCode}\0{seat}")));
    }

    private static string MixedPoolSeed(long openingSeed, IReadOnlyList<SealedPackChoiceDto> packs, string seat)
    {
        var key = new byte[sizeof(long)];
        BinaryPrimitives.WriteInt64LittleEndian(key, openingSeed);
        var composition = string.Join("\0", packs.Select(pack => $"{pack.SetCode}:{pack.Count}"));
        return Convert.ToHexStringLower(HMACSHA256.HashData(key,
            Encoding.UTF8.GetBytes($"sealed-pool-mixed-v1\0{composition}\0{seat}")));
    }

    private static SealedPoolGeneration GenerateMixed(OfficialCardCatalog catalog,
        IReadOnlyList<SealedPackChoiceDto> choices, string poolSeed)
    {
        var packs = new List<SealedPack>();
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var choice in choices)
        {
            for (var index = 0; index < choice.Count; index++)
            {
                var packNumber = packs.Count + 1;
                var packSeed = Convert.ToHexStringLower(HMACSHA256.HashData(Convert.FromHexString(poolSeed),
                    Encoding.UTF8.GetBytes($"sealed-pack-v1\0{packNumber}\0{choice.SetCode}")));
                var generated = SealedPackGenerator.Generate(catalog, choice.SetCode, packSeed, 1).Packs[0];
                var pack = generated with { PackNumber = packNumber, SetCode = choice.SetCode };
                packs.Add(pack);
                foreach (var cardNo in pack.PoolCardNos)
                    counts[cardNo] = counts.GetValueOrDefault(cardNo) + 1;
            }
        }
        return new(string.Empty, poolSeed, SealedSetCatalog.Version, SealedSetCatalog.Sha256,
            MixedGeneratorVersion, packs.ToImmutableArray(),
            counts.ToImmutableSortedDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal));
    }

    public static SealedPoolDto? ViewFor(MatchState state, string viewerPlayerId)
    {
        if (state.SessionOptions?.DeckFormat != OfficialDeckFormat.Sealed
            || !state.Seats.ContainsKey(viewerPlayerId)
            || state.PlayerSealedPools is not { } pools
            || !pools.TryGetValue(viewerPlayerId, out var pool)
            || pool is null || pool.Packs is null || pool.Pool is null
            || pool.Packs.Any(pack => pack?.Cards is null || pack.Cards.Any(card => card is null)))
            return null;
        return new(pool.SetCode,
            pool.Packs.Select(pack => new SealedPackDto(pack.SetCode, pack.Cards.Select(card =>
                new SealedPackCardDto(card.CardNo, card.Slot, card.Foil)).ToArray())).ToArray(),
            pool.Pool.OrderBy(entry => entry.Key, StringComparer.Ordinal)
                .Select(entry => new SealedPoolCardDto(entry.Key, entry.Value)).ToArray());
    }

    // CN tournament 403.6 / 602.4.a.7: only the number of unallocated pool
    // cards is public. The selected champion is already in the main deck.
    public static IReadOnlyDictionary<string, int>? SideboardCountsFor(MatchState state)
    {
        if (state.SessionOptions?.DeckFormat != OfficialDeckFormat.Sealed || state.PlayerSealedPools is not { } pools)
            return null;
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var player in ResolutionResult.SnapshotSeats(state).Select(entry => entry.Key))
        {
            if (!state.PlayerDecklists.TryGetValue(player, out var deck)
                && state.LastCompletedSeriesGame?.State.PlayerDecklists.TryGetValue(player, out deck) != true)
                continue;
            if (deck is null || !pools.TryGetValue(player, out var pool)) continue;
            var configured = deck.MainDeck.Concat(deck.RuneDeck).Concat(deck.Battlefields)
                .Concat(string.IsNullOrEmpty(deck.LegendCardNo) ? [] : new[] { deck.LegendCardNo })
                .GroupBy(cardNo => cardNo, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
            // Unlimited external runes/blank battlefields and the display-only
            // rune/token slot do not add cards to the authoritative usable pool.
            counts[player] = pool.Pool.Sum(entry => Math.Max(0, entry.Value - configured.GetValueOrDefault(entry.Key)));
        }
        return counts.Count == 0 ? null : counts;
    }

    internal static string? ViewError(MatchState state, SnapshotDto snapshot, string? viewerPlayerId)
    {
        var sealedRoom = state.SessionOptions?.DeckFormat == OfficialDeckFormat.Sealed;
        var expectedPool = viewerPlayerId is null ? null : ViewFor(state, viewerPlayerId);
        return snapshot.Format != (sealedRoom ? MatchFormats.Sealed : MatchFormats.Constructed)
            || snapshot.SetCode != state.SessionOptions?.SealedSetCode
            || MatchStateHasher.HashValue(snapshot.SealedPacks) != MatchStateHasher.HashValue(state.SessionOptions?.SealedPacks)
            || snapshot.BestOf != (sealedRoom ? state.SessionOptions!.SealedBestOf : 1)
            || MatchStateHasher.HashValue(snapshot.Series) != MatchStateHasher.HashValue(RoomMatchSeries.ViewFor(state, viewerPlayerId))
            || MatchStateHasher.HashValue(snapshot.SideboardCounts) != MatchStateHasher.HashValue(SideboardCountsFor(state))
            || (snapshot.SealedPool is not null || expectedPool is not null)
                && MatchStateHasher.HashValue(snapshot.SealedPool) != MatchStateHasher.HashValue(expectedPool)
            ? "snapshot room format, sideboard counts or private sealed pool disagrees with its viewer" : null;
    }

    // Structural validation is also used by synchronous checkpoint restore.
    // Action-log audit independently regenerates each pool from its room seat.
    public static IReadOnlyList<string> Validate(MatchState state)
    {
        var options = state.SessionOptions ?? MatchSessionOptions.Default;
        if (OptionError(options) is { } error) return [error];
        if (options.DeckFormat != OfficialDeckFormat.Sealed)
            return state.PlayerSealedPools is null ? [] : ["non-sealed room contains sealed pools"];
        var pools = state.PlayerSealedPools;
        if (state.Seats.Count == 0)
            return pools is null ? [] : ["empty sealed room contains player pools"];
        if (pools is null || !pools.Keys.Order(StringComparer.Ordinal).SequenceEqual(state.Seats.Keys.Order(StringComparer.Ordinal)))
            return ["sealed pools do not match room seats"];
        var packSetCodes = (options.SealedPacks ?? [new SealedPackChoiceDto(options.SealedSetCode!, SealedPackGenerator.PacksPerPlayer)])
            .SelectMany(choice => Enumerable.Repeat(choice.SetCode, choice.Count)).ToArray();
        foreach (var (playerId, pool) in pools)
        {
            var expectedSeed = options.SealedPacks is null
                ? PoolSeed(state.Seed, options.SealedSetCode!, state.Seats[playerId])
                : MixedPoolSeed(state.Seed, options.SealedPacks, state.Seats[playerId]);
            if (pool is null || pool.SetCode != (options.SealedSetCode ?? string.Empty)
                || pool.Seed != expectedSeed
                || pool.ConfigurationVersion != SealedSetCatalog.Version
                || pool.ConfigurationSha256 != SealedSetCatalog.Sha256
                || pool.GeneratorVersion != (options.SealedPacks is null
                    ? SealedPackGenerator.Version : MixedGeneratorVersion)
                || pool.Packs is null || pool.Packs.Count != packSetCodes.Length
                || pool.Pool is null || pool.Pool.Any(entry => entry.Value <= 0))
                return ["sealed pool metadata is invalid"];
            if (!pool.Packs.Select(pack => pack?.PackNumber ?? 0).SequenceEqual(Enumerable.Range(1, packSetCodes.Length)))
                return ["sealed pack order is invalid"];
            for (var index = 0; index < pool.Packs.Count; index++)
            {
                var pack = pool.Packs[index];
                if (pack is null || !SealedSetCatalog.TryGet(packSetCodes[index], out var set))
                    return ["sealed pack series is invalid"];
                var slots = new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    ["common"] = set.Booster.CommonSlots, ["uncommon"] = set.Booster.UncommonSlots,
                    ["rare_or_better"] = set.Booster.RareSlots, ["foil"] = set.Booster.FoilSlots,
                    ["rune_or_token"] = set.Booster.RuneOrTokenSlots
                };
                if (pack.Cards is null || pack.Cards.Count != slots.Values.Sum() || pack.PoolCardNos is null
                    || pack.SetCode != set.SetCode
                    || pack.Cards.Any(card => card is null || string.IsNullOrWhiteSpace(card.CardNo))
                    || pack.Cards.Any(card => !card.CardNo.StartsWith(set.SetCode + "·", StringComparison.Ordinal))
                    || !pack.Cards.Where(card => card.Slot != "rune_or_token").Select(card => card.CardNo).SequenceEqual(pack.PoolCardNos)
                    || pack.PoolCardNos.Count != slots.Values.Sum() - set.Booster.RuneOrTokenSlots
                    || slots.Any(slot => pack.Cards.Count(card => card.Slot == slot.Key) != slot.Value)
                    || pack.Cards.Any(card => card.Foil != (card.Slot is "rare_or_better" or "foil" or "rune_or_token")))
                    return ["sealed pack cards are invalid"];
            }
            var counts = pool.Packs.SelectMany(pack => pack.PoolCardNos).GroupBy(no => no, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
            if (!counts.OrderBy(entry => entry.Key, StringComparer.Ordinal).SequenceEqual(pool.Pool.OrderBy(entry => entry.Key, StringComparer.Ordinal)))
                return ["sealed pool counts disagree with packs"];
        }
        return [];
    }
}
