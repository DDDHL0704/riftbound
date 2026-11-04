using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Riftbound.CardCatalog;

// Only this data file enables a series. Incomplete runtime card sets stay disabled.
public static class SealedSetCatalog
{
    private static readonly Lazy<SealedSetConfiguration> Configuration = new(Load);
    public static int Version => Configuration.Value.Version;
    public static string Sha256 => Configuration.Value.Sha256;
    public static IReadOnlyList<SealedSetDefinition> All => Configuration.Value.Sets;
    public static IReadOnlyList<SealedSetDefinition> Available => All.Where(set => set.Enabled).ToArray();

    public static bool TryGet(string? setCode, out SealedSetDefinition definition)
    {
        definition = All.FirstOrDefault(set => string.Equals(
            set.SetCode, setCode?.Trim(), StringComparison.OrdinalIgnoreCase))!;
        return definition is not null;
    }

    public static string ConfigurationPath()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var path = Path.Combine(directory.FullName, "data", "official", "sealed-sets.json");
            if (File.Exists(path)) return path;
        }
        throw new FileNotFoundException("Sealed set configuration is missing.");
    }

    private static SealedSetConfiguration Load()
    {
        var bytes = File.ReadAllBytes(ConfigurationPath());
        var config = JsonSerializer.Deserialize<SealedSetConfiguration>(bytes,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidDataException("Sealed configuration is empty.");
        if (config.Version <= 0 || config.Sets.Count == 0
            || config.Sets.Select(set => set.SetCode).Distinct(StringComparer.Ordinal).Count() != config.Sets.Count)
            throw new InvalidDataException("Invalid sealed configuration version or set codes.");
        foreach (var set in config.Sets)
        {
            if (string.IsNullOrWhiteSpace(set.SetCode) || set.SetCode != set.SetCode.ToUpperInvariant()
                || string.IsNullOrWhiteSpace(set.Name) || string.IsNullOrWhiteSpace(set.BoosterProductCode)
                || set.Booster.CommonSlots < 0 || set.Booster.UncommonSlots < 0
                || set.Booster.RareSlots <= 0 || set.Booster.FoilSlots < 0 || set.Booster.RuneOrTokenSlots < 0
                || (set.VariantOnlyInSecondRareSlot && set.Booster.RareSlots < 2))
                throw new InvalidDataException($"Invalid sealed booster definition: {set.SetCode}.");
            var rates = set.Rates;
            foreach (var rate in new[] { rates.EpicPerPack, rates.EpicPerRareSlot, rates.AltArtPerPack, rates.OvernumberedPerPack,
                rates.SignedFraction, rates.SpecialAltArtPerPack, rates.UltimatePerPack,
                rates.FoilRare, rates.FoilEpic, rates.AltRunePerPack, rates.LegendPerPack, rates.LegendPerRareSlot })
            {
                if (rate is not null && (rate.Value is < 0 or > 1
                    || string.IsNullOrWhiteSpace(rate.SourceLevel) || string.IsNullOrWhiteSpace(rate.SourceUrl)))
                    throw new InvalidDataException($"Invalid sourced probability: {set.SetCode}.");
            }
            if (rates.FoilRare.Value + rates.FoilEpic.Value > 1
                || rates.AltArtPerPack.Value + rates.OvernumberedPerPack.Value
                    + rates.SpecialAltArtPerPack.Value + rates.UltimatePerPack.Value > 1)
                throw new InvalidDataException($"Sealed replacement probabilities exceed one: {set.SetCode}.");
            if (rates.SpecialAltArtPerPack.Value > 0 && set.SpecialAltArtCardNos.Count == 0
                || rates.UltimatePerPack.Value > 0 && set.UltimateCardNos.Count == 0)
                throw new InvalidDataException($"Sealed replacement targets are missing: {set.SetCode}.");
        }
        return config with { Sets = config.Sets.ToImmutableArray(), Sha256 = Convert.ToHexStringLower(SHA256.HashData(bytes)) };
    }
}

public sealed record SealedSourceValue(
    decimal Value, string SourceLevel, string SourceUrl, string? Note = null, string? BasisSourceLevel = null);
public sealed record SealedSetConfiguration(int Version, IReadOnlyList<SealedSetDefinition> Sets)
{
    public string Sha256 { get; init; } = string.Empty;
}
public sealed record SealedSetDefinition(
    string SetCode, string Name, bool Enabled, string BoosterProductCode, bool SelectBySetPrefix,
    SealedBoosterDefinition Booster, SealedPackRates Rates, bool VariantOnlyInSecondRareSlot,
    IReadOnlyList<string> SpecialAltArtCardNos, IReadOnlyList<string> UltimateCardNos,
    IReadOnlyList<string>? ExcludedCardNos = null);
public sealed record SealedBoosterDefinition(
    int CommonSlots, int UncommonSlots, int RareSlots, int FoilSlots, int RuneOrTokenSlots,
    string SourceLevel, string SourceUrl, SealedSourceValue NoSameRarityDuplicates);
public sealed record SealedPackRates(
    SealedSourceValue EpicPerPack, SealedSourceValue EpicPerRareSlot, SealedSourceValue AltArtPerPack, SealedSourceValue OvernumberedPerPack,
    SealedSourceValue SignedFraction, SealedSourceValue SpecialAltArtPerPack, SealedSourceValue UltimatePerPack,
    SealedSourceValue FoilRare, SealedSourceValue FoilEpic, SealedSourceValue AltRunePerPack,
    SealedSourceValue? LegendPerPack, SealedSourceValue? LegendPerRareSlot);
public sealed record SealedPackCard(string CardNo, string Slot, bool Foil);
public sealed record SealedPack(int PackNumber, IReadOnlyList<SealedPackCard> Cards, IReadOnlyList<string> PoolCardNos, string SetCode);
public sealed record SealedPoolGeneration(
    string SetCode, string Seed, int ConfigurationVersion, string ConfigurationSha256, string GeneratorVersion,
    IReadOnlyList<SealedPack> Packs, IReadOnlyDictionary<string, int> Pool);

public static class SealedPackGenerator
{
    public const int PacksPerPlayer = 6;
    public const string Version = "sealed-sha256-v1";

    public static SealedPoolGeneration Generate(OfficialCardCatalog catalog, string setCode, string seed,
        int packCount = PacksPerPlayer)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (!SealedSetCatalog.TryGet(setCode, out var definition) || !definition.Enabled)
            throw new ArgumentException($"Sealed set {setCode} is not available.", nameof(setCode));
        return Generate(catalog, definition, seed, packCount);
    }

    // Explicit definitions also support deterministic collation verification before a set is enabled.
    public static SealedPoolGeneration Generate(OfficialCardCatalog catalog, SealedSetDefinition definition,
        string seed, int packCount = PacksPerPlayer)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (string.IsNullOrWhiteSpace(seed)) throw new ArgumentException("A private sealed seed is required.", nameof(seed));
        if (packCount is <= 0 or > 100) throw new ArgumentOutOfRangeException(nameof(packCount));
        var candidates = EligibleCards(catalog, definition);
        var baseCards = candidates.Where(card => card.ExtendRarity == "base_inscript").ToArray();
        var ordinary = baseCards.Where(card => !IsRuneOrToken(card)).ToArray();
        var units = FunctionalUnitBuilder.Build(candidates);
        var identity = units.SelectMany(unit => unit.Cards.Select(card => (card.CardNo, unit.Id)))
            .ToDictionary(entry => entry.CardNo, entry => entry.Id, StringComparer.Ordinal);
        var rates = definition.Rates;
        var random = new SeedRandom(seed);
        var packs = new List<SealedPack>();
        var pool = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var packNumber = 1; packNumber <= packCount; packNumber++)
        {
            var cards = new List<SealedPackCard>();
            var used = new HashSet<string>(StringComparer.Ordinal);
            OfficialCard Pick(IEnumerable<OfficialCard> source)
            {
                var eligible = source.Where(card => definition.Booster.NoSameRarityDuplicates.Value == 0
                    || !used.Contains(identity[card.CardNo])).ToArray();
                if (eligible.Length == 0)
                    throw new InvalidDataException($"Sealed {definition.SetCode} has an empty or exhausted slot.");
                var card = eligible[random.NextIndex(eligible.Length)];
                used.Add(identity[card.CardNo]);
                return card;
            }
            void Add(OfficialCard card, string slot, bool foil) => cards.Add(new(card.CardNo, slot, foil));
            for (var i = 0; i < definition.Booster.CommonSlots; i++)
                Add(Pick(ordinary.Where(card => card.Rarity == "rune_dust")), "common", false);
            for (var i = 0; i < definition.Booster.UncommonSlots; i++)
                Add(Pick(ordinary.Where(card => card.Rarity == "rune_glimmer")), "uncommon", false);
            for (var i = 0; i < definition.Booster.RareSlots; i++)
            {
                var epic = random.Chance(rates.EpicPerRareSlot.Value);
                var source = ordinary.Where(card => card.Rarity == (epic ? "rune_core" : "rune_shard"));
                if (!epic && rates.LegendPerRareSlot is { } legendRate)
                {
                    var legend = random.Chance(legendRate.Value);
                    source = source.Where(card => (card.CardCategoryName == "传奇") == legend);
                }
                Add(Pick(source), "rare_or_better", true);
            }
            // A mutually exclusive per-pack roll reserves at most one rare slot for a special printing.
            var roll = random.NextFraction();
            var ultimate = rates.UltimatePerPack.Value;
            var overnumbered = ultimate + rates.OvernumberedPerPack.Value;
            var specialAlt = overnumbered + rates.SpecialAltArtPerPack.Value;
            var alt = specialAlt + rates.AltArtPerPack.Value;
            IEnumerable<OfficialCard>? replacements = roll < ultimate
                ? candidates.Where(card => definition.UltimateCardNos.Contains(card.CardNo, StringComparer.Ordinal))
                : roll < overnumbered
                    ? candidates.Where(card => card.ExtendRarity == (random.Chance(rates.SignedFraction.Value)
                        ? "runemaster_mark" : "exalted_script") && !definition.UltimateCardNos.Contains(card.CardNo, StringComparer.Ordinal))
                    : roll < specialAlt
                        ? candidates.Where(card => definition.SpecialAltArtCardNos.Contains(card.CardNo, StringComparer.Ordinal))
                        : roll < alt
                            ? candidates.Where(card => card.ExtendRarity == "legacy_etching"
                                && !definition.SpecialAltArtCardNos.Contains(card.CardNo, StringComparer.Ordinal))
                            : null;
            if (replacements is not null)
            {
                var slotIndex = definition.Booster.CommonSlots + definition.Booster.UncommonSlots
                    + (definition.VariantOnlyInSecondRareSlot ? 1 : random.NextIndex(definition.Booster.RareSlots));
                used.Remove(identity[cards[slotIndex].CardNo]);
                var replacement = Pick(replacements.Where(card => !IsRuneOrToken(card)));
                cards[slotIndex] = new(replacement.CardNo, "rare_or_better", true);
            }
            for (var i = 0; i < definition.Booster.FoilSlots; i++)
            {
                var foilRoll = random.NextFraction();
                var foilRarity = foilRoll < rates.FoilEpic.Value ? "rune_core"
                    : foilRoll < rates.FoilEpic.Value + rates.FoilRare.Value ? "rune_shard" : null;
                Add(Pick(ordinary.Where(card => foilRarity is null
                    ? card.Rarity is "rune_dust" or "rune_glimmer" : card.Rarity == foilRarity)), "foil", true);
            }
            for (var i = 0; i < definition.Booster.RuneOrTokenSlots; i++)
            {
                var runeSource = random.Chance(rates.AltRunePerPack.Value)
                    ? candidates.Where(card => card.CardCategoryName == "符文" && card.ExtendRarity == "legacy_etching")
                    : baseCards.Where(IsRuneOrToken);
                Add(Pick(runeSource), "rune_or_token", true);
            }
            var poolCards = cards.Where(card => card.Slot != "rune_or_token").Select(card => card.CardNo).ToArray();
            foreach (var cardNo in poolCards) pool[cardNo] = pool.GetValueOrDefault(cardNo) + 1;
            packs.Add(new(packNumber, cards.ToImmutableArray(), poolCards.ToImmutableArray(), definition.SetCode));
        }
        return new(definition.SetCode, seed, SealedSetCatalog.Version, SealedSetCatalog.Sha256, Version,
            packs.ToImmutableArray(), pool.ToImmutableSortedDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal));
    }

    public static IReadOnlyList<OfficialCard> EligibleCards(OfficialCardCatalog catalog, SealedSetDefinition definition) =>
        catalog.Cards.Where(card => card.CardNo.StartsWith(definition.SetCode + "·", StringComparison.Ordinal)
                || definition.SetCode == "UNL" && card.CardNo.StartsWith("UNL-", StringComparison.Ordinal))
            .Where(card => definition.ExcludedCardNos?.Contains(card.CardNo, StringComparer.Ordinal) != true)
            .Where(card => definition.SelectBySetPrefix || card.ProductCodeList.Contains(definition.BoosterProductCode, StringComparer.Ordinal))
            .DistinctBy(card => card.CardNo, StringComparer.Ordinal).OrderBy(card => card.CardNo, StringComparer.Ordinal).ToArray();

    private static bool IsRuneOrToken(OfficialCard card) => card.CardCategoryNameList.Any(category =>
        category == "符文" || category.StartsWith("指示物", StringComparison.Ordinal));

    private sealed class SeedRandom(string seed)
    {
        private readonly byte[] seedBytes = Encoding.UTF8.GetBytes(seed);
        private ulong counter;
        public int NextIndex(int exclusiveMax)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(exclusiveMax);
            // Rejection sampling avoids modulo bias in card selection.
            var limit = ulong.MaxValue - ulong.MaxValue % (ulong)exclusiveMax;
            ulong value;
            do { value = NextUInt64(); } while (value >= limit);
            return (int)(value % (ulong)exclusiveMax);
        }
        public decimal NextFraction() => (decimal)NextUInt64() / ((decimal)ulong.MaxValue + 1);
        public bool Chance(decimal probability) => probability > 0 && (probability >= 1 || NextFraction() < probability);
        private ulong NextUInt64()
        {
            var input = new byte[seedBytes.Length + sizeof(ulong)];
            seedBytes.CopyTo(input, 0);
            BinaryPrimitives.WriteUInt64LittleEndian(input.AsSpan(seedBytes.Length), counter++);
            return BinaryPrimitives.ReadUInt64LittleEndian(SHA256.HashData(input));
        }
    }
}
