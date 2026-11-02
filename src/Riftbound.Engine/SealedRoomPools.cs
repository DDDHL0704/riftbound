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
    public static string? OptionError(MatchSessionOptions options)
    {
        if (options.DeckFormat != OfficialDeckFormat.Sealed)
            return options.SealedSetCode is not null || options.SealedBestOf != 0
                ? "非现开房间不能设置现开系列或多局配置。" : null;
        if (options.AllowLegacyReadyWithoutDeck || !options.HideSubmittedDeckIdentities)
            return "现开房间必须校验卡组并隐藏提交内容。";
        if (options.SealedBestOf is not (1 or 3)) return "现开比赛的 bestOf 必须为 1 或 3。";
        if (!SealedSetCatalog.TryGet(options.SealedSetCode, out var set) || !set.Enabled
            || options.SealedSetCode != set.SetCode)
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
        var seed = PoolSeed(state.Seed, options.SealedSetCode!, state.Seats[playerId]);
        return state with { PlayerSealedPools = pools.Add(playerId,
            SealedPackGenerator.Generate(catalog, options.SealedSetCode!, seed)) };
    }

    private static string PoolSeed(long openingSeed, string setCode, string seat)
    {
        var key = new byte[sizeof(long)];
        BinaryPrimitives.WriteInt64LittleEndian(key, openingSeed);
        return Convert.ToHexStringLower(HMACSHA256.HashData(key,
            Encoding.UTF8.GetBytes($"sealed-pool-v1\0{setCode}\0{seat}")));
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
            pool.Packs.Select(pack => new SealedPackDto(pack.Cards.Select(card =>
                new SealedPackCardDto(card.CardNo, card.Slot, card.Foil)).ToArray())).ToArray(),
            pool.Pool.OrderBy(entry => entry.Key, StringComparer.Ordinal)
                .Select(entry => new SealedPoolCardDto(entry.Key, entry.Value)).ToArray());
    }

    internal static string? ViewError(MatchState state, SnapshotDto snapshot, string? viewerPlayerId)
    {
        var sealedRoom = state.SessionOptions?.DeckFormat == OfficialDeckFormat.Sealed;
        var expectedPool = viewerPlayerId is null ? null : ViewFor(state, viewerPlayerId);
        return snapshot.Format != (sealedRoom ? MatchFormats.Sealed : MatchFormats.Constructed)
            || snapshot.SetCode != state.SessionOptions?.SealedSetCode
            || snapshot.BestOf != (sealedRoom ? state.SessionOptions!.SealedBestOf : 1)
            || (snapshot.SealedPool is not null || expectedPool is not null)
                && MatchStateHasher.HashValue(snapshot.SealedPool) != MatchStateHasher.HashValue(expectedPool)
            ? "snapshot room format or private sealed pool disagrees with its viewer" : null;
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
        SealedSetCatalog.TryGet(options.SealedSetCode, out var set);
        var slots = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["common"] = set.Booster.CommonSlots, ["uncommon"] = set.Booster.UncommonSlots,
            ["rare_or_better"] = set.Booster.RareSlots, ["foil"] = set.Booster.FoilSlots,
            ["rune_or_token"] = set.Booster.RuneOrTokenSlots
        };
        foreach (var (playerId, pool) in pools)
        {
            if (pool is null || pool.SetCode != options.SealedSetCode
                || pool.Seed != PoolSeed(state.Seed, options.SealedSetCode!, state.Seats[playerId])
                || pool.ConfigurationVersion != SealedSetCatalog.Version
                || pool.ConfigurationSha256 != SealedSetCatalog.Sha256
                || pool.GeneratorVersion != SealedPackGenerator.Version
                || pool.Packs is null || pool.Packs.Count != SealedPackGenerator.PacksPerPlayer
                || pool.Pool is null || pool.Pool.Any(entry => entry.Value <= 0))
                return ["sealed pool metadata is invalid"];
            if (!pool.Packs.Select(pack => pack?.PackNumber ?? 0).SequenceEqual(Enumerable.Range(1, SealedPackGenerator.PacksPerPlayer)))
                return ["sealed pack order is invalid"];
            foreach (var pack in pool.Packs)
            {
                if (pack.Cards is null || pack.Cards.Count != slots.Values.Sum() || pack.PoolCardNos is null
                    || pack.Cards.Any(card => card is null || string.IsNullOrWhiteSpace(card.CardNo))
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
