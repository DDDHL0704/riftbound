using System.Reflection;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Riftbound.CardCatalog;
using Riftbound.Contracts;

namespace Riftbound.Engine;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RuleInputIdentity(
    [property: JsonRequired] int SchemaVersion,
    [property: JsonRequired] string Region,
    [property: JsonRequired] string Language,
    [property: JsonRequired] string AuthoritySha256,
    [property: JsonRequired] string CatalogSha256,
    [property: JsonRequired] string ImplementationSha256)
{
    [JsonRequired]
    public string IdentitySha256 { get; init; } = MatchStateHasher.HashValue(new {
        SchemaVersion,Region,Language,AuthoritySha256,CatalogSha256,ImplementationSha256 });

    private static readonly Lazy<RuleInputIdentity> Snapshot = new(Capture);
    public static RuleInputIdentity Current => Snapshot.Value;
    public static bool IsCurrent(RuleInputIdentity? value) => value is not null && value == Current;
    public static bool IsCurrentState(MatchState state) => IsCurrent(state.RuleInputs)
        && (state.PendingRuleChoice is not { } pending
            || pending.Origin is { } origin && IsCurrent(origin.RuleInputs)
                && (pending.Request.Preview is null || IsCurrent(pending.Request.Preview.RuleInputs)));

    public static void RequireCurrentState(MatchState state)
    {
        if (!IsCurrentState(state))
            throw new MatchSessionException(ErrorCodes.RecoveryInconsistent,
                "match rule input identity is missing or differs from the running rule/catalog/implementation inputs");
    }

    private static RuleInputIdentity Capture()
    {
        var path=Path.Combine(Path.GetDirectoryName(OfficialCardCatalog.DefaultCatalogPath())!,"rule-input-scope.json");
        using var source=JsonDocument.Parse(File.ReadAllText(path));
        var region=source.RootElement.GetProperty("region").GetString()!;
        var language=source.RootElement.GetProperty("language").GetString()!;
        if(source.RootElement.GetProperty("schemaVersion").GetInt32()!=1 || region!="CN" || language!="zh-CN")
            throw new InvalidDataException("Unsupported active rule input schema/region/language.");
        var applicability=DateOnly.ParseExact(source.RootElement.GetProperty("applicabilityDate").GetString()!,
            "yyyy-MM-dd",CultureInfo.InvariantCulture);
        var authorities=source.RootElement.GetProperty("adoptedSources").EnumerateArray().ToArray();
        var primaryId=source.RootElement.GetProperty("primaryCoreId").GetString();
        if (!authorities.Any(a=>a.GetProperty("id").GetString()==primaryId
            && a.GetProperty("role").GetString()=="PRIMARY_CORE"))
            throw new InvalidDataException("Active rule inputs require their declared primary regional core source.");
        foreach(var authoritySource in authorities)
        {
            ValidateSourceHash(authoritySource.GetProperty("sha256").GetString());
            if(authoritySource.GetProperty("effectiveDate") is { ValueKind: JsonValueKind.String } effective
                && DateOnly.ParseExact(effective.GetString()!,"yyyy-MM-dd",CultureInfo.InvariantCulture)>applicability)
                throw new InvalidDataException("Future-effective sources cannot enter the active rule input identity.");
        }
        ValidateSourceHash(source.RootElement.GetProperty("cardQa").GetProperty("sha256").GetString());
        var authority=MatchStateHasher.HashValue(source.RootElement);
        var catalog=OfficialCardCatalog.LoadRuntimeAsync().GetAwaiter().GetResult();
        var cards=catalog.Cards.OrderBy(c=>c.CardNo,StringComparer.Ordinal).Select(c=>new {
            c.Id,c.CardNo,c.CardName,c.SubTitle,c.CardCategory,c.CardCategoryName,c.CardCategoryList,c.CardCategoryNameList,
            c.CardColorList,c.Hero,c.Region,c.Tag,c.CardEffect,c.Energy,c.ReturnEnergy,c.Power,c.ExtendType,c.ExtendTypeName,
            c.CardGroupLimit,c.Status,c.ListSort,c.ProductCodeList,c.ProductNameList,c.Rarity,c.RarityName,c.ExtendRarity,c.ExtendRarityName });
        var armaments=OfficialArmamentCatalog.All.Select(a=>new {
            a.CardNo,a.SourceCardNo,a.BaseMightBonus,a.EffectText,a.CopiesHostPrintedRulesText,a.CopiesChosenFriendlyUnit,a.ReplacesHostDestruction,a.GrantedKeywords });
        var content=MatchStateHasher.HashValue(new {Cards=cards,Armaments=armaments});
        // MVID identifies the assemblies already executing in this process,
        // including compilation options. Audit files and filesystem timestamps
        // do not participate. Rebuilt different implementations require new games.
        Assembly[] assemblies=[typeof(CoreRuleEngine).Assembly,typeof(OfficialCardCatalog).Assembly,typeof(GameCommand).Assembly];
        var implementation=MatchStateHasher.HashValue(assemblies.OrderBy(a=>a.GetName().Name,StringComparer.Ordinal)
            .Select(a=>new {Name=a.GetName().Name,Module=a.ManifestModule.ModuleVersionId.ToString("N")}));
        return new(1,region,language,authority,content,implementation);
    }

    private static void ValidateSourceHash(string? sha)
    {
        if(sha is null || sha.Length!=64 || !sha.All(Uri.IsHexDigit))
            throw new InvalidDataException("Adopted authority source requires a SHA-256 content identity.");
    }
}
