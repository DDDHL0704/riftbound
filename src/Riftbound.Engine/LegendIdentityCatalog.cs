namespace Riftbound.Engine;

public static class LegendIdentityCatalog
{
    public const string AhriLegendIdentityId = "LEGEND_IDENTITY_AHRI";
    public const string LucianLegendIdentityId = "LEGEND_IDENTITY_LUCIAN";
    public const string MasterYiLevelLegendIdentityId = "LEGEND_IDENTITY_MASTER_YI_LEVEL";
    public const string DravenLegendIdentityId = "LEGEND_IDENTITY_DRAVEN";
    public const string GarenIntroLegendIdentityId = "LEGEND_IDENTITY_GAREN_INTRO";
    public const string LuxIntroLegendIdentityId = "LEGEND_IDENTITY_LUX_INTRO";
    public const string AnnieLegendIdentityId = "LEGEND_IDENTITY_ANNIE";
    public const string JinxLegendIdentityId = "LEGEND_IDENTITY_JINX";
    public const string RumbleLegendIdentityId = "LEGEND_IDENTITY_RUMBLE";
    public const string PowerfulUnitRuneLegendIdentityId = "LEGEND_IDENTITY_POWERFUL_UNIT_RUNE";
    public const string SettLegendIdentityId = "LEGEND_IDENTITY_SETT";
    public const string ViLegendIdentityId = "LEGEND_IDENTITY_VI";
    public const string VexLegendIdentityId = "LEGEND_IDENTITY_VEX";
    public const string RenataLegendIdentityId = "LEGEND_IDENTITY_RENATA";
    public const string ReksaiLegendIdentityId = "LEGEND_IDENTITY_REKSAI";
    public const string IvernLegendIdentityId = "LEGEND_IDENTITY_IVERN";
    public const string LeblancLegendIdentityId = "LEGEND_IDENTITY_LEBLANC";
    public const string RengarLegendIdentityId = "LEGEND_IDENTITY_RENGAR";
    public const string LeonaLegendIdentityId = "LEGEND_IDENTITY_LEONA";
    public const string SivirLegendIdentityId = "LEGEND_IDENTITY_SIVIR";
    public const string JhinLegendIdentityId = "LEGEND_IDENTITY_JHIN";

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> RepresentativeSourceCardNosByIdentityId =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
        {
            [AhriLegendIdentityId] = ["OGN·255/298"],
            [LucianLegendIdentityId] = ["SFD·183/221"],
            [MasterYiLevelLegendIdentityId] = ["UNL-191/219"],
            [DravenLegendIdentityId] = ["SFD·185/221"],
            [GarenIntroLegendIdentityId] = ["OGS·023/024"],
            [LuxIntroLegendIdentityId] = ["OGS·021/024"],
            [AnnieLegendIdentityId] = ["OGS·017/024"],
            [JinxLegendIdentityId] = ["FND-251/298"],
            [RumbleLegendIdentityId] = ["SFD·181/221"],
            [PowerfulUnitRuneLegendIdentityId] = ["FND-249/298", "SFD·205/221"],
            [SettLegendIdentityId] = ["OGN·269/298"],
            [ViLegendIdentityId] = ["UNL-187/219"],
            [VexLegendIdentityId] = ["UNL-193/219"],
            [RenataLegendIdentityId] = ["SFD·201/221"],
            [ReksaiLegendIdentityId] = ["SFD·187/221"],
            [IvernLegendIdentityId] = ["UNL-195/219"],
            [LeblancLegendIdentityId] = ["UNL-199/219"],
            [RengarLegendIdentityId] = ["UNL-183/219"],
            [LeonaLegendIdentityId] = ["OGN·261/298"],
            [SivirLegendIdentityId] = ["SFD·203/221"],
            [JhinLegendIdentityId] = ["UNL-181/219"]
        };

    private static readonly Lazy<IReadOnlyDictionary<string, IReadOnlyList<string>>> SourceCardNosByRepresentativeCardNo =
        new(BuildSourceCardNosByRepresentativeCardNo, LazyThreadSafetyMode.ExecutionAndPublication);

    public static IReadOnlyList<string> SourceCardNosForIdentity(string? identityId)
    {
        if (string.IsNullOrWhiteSpace(identityId)
            || !RepresentativeSourceCardNosByIdentityId.TryGetValue(identityId.Trim(), out var representativeCardNos))
        {
            return [];
        }

        return representativeCardNos
            .SelectMany(SourceCardNosForRepresentative)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    public static string PrimarySourceCardNoForIdentity(string? identityId)
    {
        return !string.IsNullOrWhiteSpace(identityId)
            && RepresentativeSourceCardNosByIdentityId.TryGetValue(identityId.Trim(), out var representativeCardNos)
                ? representativeCardNos.FirstOrDefault() ?? string.Empty
                : string.Empty;
    }

    public static bool IsSourceCardNoForIdentity(
        string? identityId,
        string? cardNo)
    {
        return !string.IsNullOrWhiteSpace(cardNo)
            && SourceCardNosForIdentity(identityId).Contains(cardNo.Trim(), StringComparer.Ordinal);
    }

    private static IReadOnlyList<string> SourceCardNosForRepresentative(string representativeCardNo)
    {
        var normalized = OfficialCardSourceIdentityGroups.NormalizeCardNo(representativeCardNo);
        return SourceCardNosByRepresentativeCardNo.Value.TryGetValue(normalized, out var sourceCardNos)
            ? sourceCardNos
            : [normalized];
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> BuildSourceCardNosByRepresentativeCardNo()
    {
        return OfficialCardSourceIdentityGroups.BuildByRepresentativeCardNo(
            RepresentativeSourceCardNosByIdentityId.Values.SelectMany(cardNos => cardNos));
    }
}
