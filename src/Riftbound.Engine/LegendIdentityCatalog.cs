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

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> SourceCardNosByIdentityId =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
        {
            [AhriLegendIdentityId] = ["OGN·255/298", "OGN·303/298", "OGN·303*/298"],
            [LucianLegendIdentityId] = ["SFD·183/221", "SFD·241/221"],
            [MasterYiLevelLegendIdentityId] = ["UNL-191/219", "UNL-231/219", "UNL-231*/219"],
            [DravenLegendIdentityId] = ["SFD·185/221", "SFD·242/221"],
            [GarenIntroLegendIdentityId] = ["OGS·023/024"],
            [LuxIntroLegendIdentityId] = ["OGS·021/024"],
            [AnnieLegendIdentityId] = ["OGS·017/024"],
            [JinxLegendIdentityId] = ["FND-251/298", "OGN·251/298", "OGN·301/298", "OGN·301*/298"],
            [RumbleLegendIdentityId] = ["SFD·181/221", "SFD·240/221"],
            [PowerfulUnitRuneLegendIdentityId] = ["FND-249/298", "OGN·249/298", "OGN·300/298", "OGN·300*/298", "SFD·205/221", "SFD·251/221"],
            [SettLegendIdentityId] = ["OGN·269/298", "OGN·310/298", "OGN·310*/298"],
            [ViLegendIdentityId] = ["UNL-187/219", "UNL-229/219", "UNL-229*/219"],
            [VexLegendIdentityId] = ["UNL-193/219", "UNL-232/219", "UNL-232*/219"],
            [RenataLegendIdentityId] = ["SFD·201/221", "SFD·249/221"],
            [ReksaiLegendIdentityId] = ["SFD·187/221", "SFD·243/221"],
            [IvernLegendIdentityId] = ["UNL-195/219", "UNL-233/219", "UNL-233*/219"],
            [LeblancLegendIdentityId] = ["UNL-199/219", "UNL-235/219", "UNL-235*/219"],
            [RengarLegendIdentityId] = ["UNL-183/219", "UNL-227/219", "UNL-227*/219"],
            [LeonaLegendIdentityId] = ["OGN·261/298", "OGN·306/298", "OGN·306*/298"],
            [SivirLegendIdentityId] = ["SFD·203/221", "SFD·250/221"],
            [JhinLegendIdentityId] = ["UNL-181/219", "UNL-226/219", "UNL-226*/219"]
        };

    public static IReadOnlyList<string> SourceCardNosForIdentity(string? identityId)
    {
        return !string.IsNullOrWhiteSpace(identityId)
            && SourceCardNosByIdentityId.TryGetValue(identityId.Trim(), out var sourceCardNos)
                ? sourceCardNos
                : [];
    }

    public static string PrimarySourceCardNoForIdentity(string? identityId)
    {
        return SourceCardNosForIdentity(identityId).FirstOrDefault() ?? string.Empty;
    }

    public static bool IsSourceCardNoForIdentity(
        string? identityId,
        string? cardNo)
    {
        return !string.IsNullOrWhiteSpace(cardNo)
            && SourceCardNosForIdentity(identityId).Contains(cardNo.Trim(), StringComparer.Ordinal);
    }
}
