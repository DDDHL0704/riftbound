using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class Cn16RuleInputSelectionTests
{
    private const string PriorAuthority = "b3c0ed37468d6b2567c475e602c9d7c89a48a671454a0d1e29180374f0aec9a3";
    private const string PriorCn16Catalog = "eef90d99a6c88fbbf1763e94b733656254ffcd2a266000545595607a9b8ab76d";
    private const string PriorBoneErratumCatalog = "d5dbe0401fc52312c0f9033702b8090ff4063499db878706317dc259d051ac29";
    private const string PriorVoidErratumCatalog = "a940e5f8bcd5e1ead68a85c7f983e530333d6179a4b8429ed1f8fe2ee20b8397";
    private const string PriorVenFlowCatalog = "3fcf1613507883ce401c5258c7171fa0eb089d09d519af47ae0e50ecca073d83";
    private const string Prior1074Catalog = "8895025aa5cfd18025936f4c87b9a8d16d49341ff4d40f0c7b0fa1bca3a2cd66";
    private const string Prior1076Catalog = "5823aaf65ba61aa54706647fb89a4c653fc7a4ecd81cd09c921b3fedebaa1604";
    private const string PriorSp6OnlyCatalog = "eedd99268d0e6f5faa7ffbfeed570ef29a409ca34980e0627400b296c59d6988";
    private const string PriorPaymentPreparationCatalog = "7b565edb12e8f9d0859dd0b6dbdf2ff789c5f2fcc777eeae6c3376bbb3bb2f5a";
    private const string PriorCombatPreparationCatalog = "9124a2230da873a127240687f4a5c8c27b978ceb6e81fe447e59e6b321de64b9";
    private const string PriorGuardianCatalog = "db1bd394788127ba21b219ec475800fcb996f8098305081bcbe96019aaab0ede";
    private const string PriorVen050PreparationCatalog = "bac9b77371291d710a0b9aa9b210ba9c551c5a4e1a7bf7ce54951ea929dfe3e7";

    private const string PriorVen050GuardianCatalog = "5a92a98ebbadf009f87d6b232bec316a7b7453c3bf6f50bf659c0a483462efb3";

    private const string PriorVen070Catalog = "883984762a440fca9f803ac247b1c89245df175f09140061dd6023f20c41d88f";
    private const string PriorVen007Catalog = "a76a818ff531b3b47db146bb4a92742b95d079a43af356bb06753b7bb93c6771";
    private const string PriorVen059Catalog = "f705075d83f9af3758ad1698cc19c70db8a39e0ae012438704782bdf92096e0e";
    private const string PriorVen055Catalog = "774f3dde0c296496b30c354c5334bb66ee53d6a280e8fdc319967ed85c934a42";
    private const string PriorVen110Catalog = "353df2f0aa104ad593b90add08ca8f1e7187ccd68e4c43abe285d27f2c88462f";
    private const string PriorVen130Catalog = "534f281ee07516c8431e6121e7f4ad5fab30faec5eaa5cea01c54a9118c2f6b4";
    private const string PriorVen057Catalog = "f11fa20ce78c5ca1c9376870ec0de0da6b5b5a16b8f0fc751c7aaa6af1c28b45";
    private const string PriorNo35OnlyCatalog = "4aafafd814d176c6281e84f706dc068678b1de0e3bdf415b3f40ed1008dd577e";
    private const string PriorZhonyaOnlyCatalog = "af4d7aa516d1c93c11ddac2bbcf931a8f344ee6069251dd016a5b18623db9cd6";
    private const string PriorBeforeVen128Catalog = "6747a80cd62ee7cabe3f64c75d0396ec6a181f75d676ea66984789cb0f3b7e35";
    private const string PriorVen128OnlyCatalog = "3c5eb7a6d83bdbc4731e22161d59b749677875690366013afc8a1b96377cb4b4";
    private const string PriorBeforeVen078Catalog = "44151ac318c8961d6bfbbc0b60e295fb149d619420dafd33835f99bfe319050f";
    private const string PriorBeforeVen124Catalog = "06d1691637cd0750546481265b86b363c78d0f4bbd5c16770331415d0bc66cd2";


    [Fact]
    public async Task ActualPackagedInputsSelectTheCurrentCnCoreWithoutCertifyingAllItsSemantics()
    {
        var path = Path.Combine(Path.GetDirectoryName(OfficialCardCatalog.DefaultCatalogPath())!, "rule-input-scope.json");
        using var document = JsonDocument.Parse(File.ReadAllText(path)); var scope = document.RootElement;
        Assert.Equal("CN", scope.GetProperty("region").GetString());
        Assert.Equal("2026-10-16", scope.GetProperty("applicabilityDate").GetString());
        Assert.Equal("DOC-01", scope.GetProperty("primaryCoreId").GetString());
        Assert.False(scope.GetProperty("semanticCoverageCertified").GetBoolean());
        var sources = scope.GetProperty("adoptedSources").EnumerateArray().ToArray();
        Assert.Equal(26, sources.Length);
        var core = Assert.Single(sources, item => item.GetProperty("role").GetString() == "PRIMARY_CORE");
        Assert.Equal("DOC-01", core.GetProperty("id").GetString());
        Assert.Equal("d6fd7140f106f9b5b3dc0d5817fd040110fe870a6da9ee335ba7f4f70f892d11", core.GetProperty("sha256").GetString());
        Assert.Equal("2026-10-16", core.GetProperty("effectiveDate").GetString());
        Assert.DoesNotContain(sources, item => item.GetProperty("id").GetString() is "DOC-04" or "DOC-05");
        Assert.Equal("DOC-01", scope.GetProperty("supersededSourceIds").GetProperty("DOC-04").GetString());
        Assert.Equal("DOC-02", scope.GetProperty("supersededSourceIds").GetProperty("DOC-05").GetString());
        Assert.Equal("REGIONAL_ERRATA_RAD_PREVIEW_NO_RUNTIME_CARDS",
            Assert.Single(sources, item => item.GetProperty("id").GetString() == "DOC-03").GetProperty("role").GetString());
        Assert.Equal(1504, scope.GetProperty("cardQa").GetProperty("entries").GetInt32());
        Assert.Equal("0f5f933ced955f89b9046eb66bf07f62b4d827464b23669b8af6a009b2fc784c", scope.GetProperty("cardQa").GetProperty("sha256").GetString());
        Assert.NotEqual(PriorAuthority, RuleInputIdentity.Current.AuthoritySha256);
        // Retain both exact source additions and the Guardian Angel profile;
        // apply the exact CN31291 effective erratum. The VEN110 region conflict stays OPEN.
        // Registration and input identity do not certify all card semantics.
        Assert.Equal("SFD·051/221", Assert.Single(OfficialArmamentCatalog.All, a => a.ReplacesHostDestruction).CardNo);
        var guardian = Assert.IsType<OfficialArmamentDefinition>(OfficialArmamentCatalog.Find("SFD·051/221"));
        Assert.Equal(1, guardian.BaseMightBonus);
        Assert.False(guardian.CopiesHostPrintedRulesText);
        Assert.False(guardian.CopiesChosenFriendlyUnit);
        Assert.Equal("9801f8ca6b23f266587860a2b9db46c27529756f6a1f93b4e2200a2774c68795", RuleInputIdentity.Current.CatalogSha256);
        var catalog = await OfficialCardCatalog.LoadDefaultAsync();
        Assert.Equal(1089, catalog.Cards.Count);
        foreach (var cardNo in new[] { "VEN·008", "VEN·010", "VEN·012", "VEN·013", "VEN·026", "VEN·043", "VEN·048", "VEN·086", "VEN·093", "VEN·103", "VEN·118", "VEN·122", "VEN·159", "VEN·181", "VEN·050", "VEN·070", "VEN·007", "VEN·059", "VEN·055", "VEN·110", "VEN·130", "VEN·057", "VEN·035", "VEN·128", "VEN·078", "VEN·124" })
            Assert.Single(catalog.Cards, card => card.CardNo == cardNo);
    }

    [Fact]
    public async Task AWellFormedActualPriorAuthorityCannotRunUnderTheCurrentImplementation()
    {
        var current = RuleInputIdentity.Current;
        var prior = new RuleInputIdentity(1, "CN", "zh-CN", PriorAuthority, current.CatalogSha256, current.ImplementationSha256);
        var state = MatchReplayInitialStateBuilder.FromSeats("prior-cn14-under-cn16", new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" })
            with { RuleInputs = prior };
        var result = await new CoreRuleEngine().ResolveAsync(state, new("prior-authority", "P1", "PASS"), new PassCommand(), default);
        Assert.False(result.Accepted); Assert.Equal(ErrorCodes.RecoveryInconsistent, result.ErrorCode);
        Assert.Same(state, result.State); Assert.Empty(result.Events);
        Assert.Throws<MatchSessionException>(() => new MatchSession(state, new CoreRuleEngine(), NoopMatchJournal.Instance));
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(state, [], state, new CoreRuleEngine(), default);
        Assert.False(replay.IsMatch); Assert.NotEmpty(replay.Errors);
        Assert.Equal(replay.ExpectedStateHash, replay.ReplayedStateHash);
    }

    [Fact]
    public async Task AWellFormedActualPriorCatalogCannotRunUnderTheCurrentImplementation()
    {
        var current = RuleInputIdentity.Current;
        foreach (var catalog in new[] {
            "9706138cf5ec1795ad41dc689632603bb0530807f81420362fc3fe770d2874e9", PriorCn16Catalog, PriorBoneErratumCatalog, PriorVoidErratumCatalog,
            PriorVenFlowCatalog, Prior1074Catalog, Prior1076Catalog, PriorSp6OnlyCatalog,
            PriorPaymentPreparationCatalog, PriorCombatPreparationCatalog, PriorGuardianCatalog, PriorVen050PreparationCatalog, PriorVen050GuardianCatalog, PriorVen070Catalog, PriorVen007Catalog, PriorVen059Catalog, PriorVen055Catalog, PriorVen110Catalog, PriorVen130Catalog, PriorVen057Catalog, PriorNo35OnlyCatalog, PriorZhonyaOnlyCatalog, PriorBeforeVen128Catalog, PriorVen128OnlyCatalog, PriorBeforeVen078Catalog,
            "6b15cda26c2f69587c9c14331a5fa30d48fd607188b237be47bb3351db734524",
            "88711bc685dbfe2202b6aff49731b3269043dfcc22ab56d6d793e3f5c9af73dc", PriorBeforeVen124Catalog })
        {
            var prior = new RuleInputIdentity(1, "CN", "zh-CN", current.AuthoritySha256,
                catalog, current.ImplementationSha256);
            var state = MatchReplayInitialStateBuilder.FromSeats("prior-1033-under-1034", new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" })
                with { RuleInputs = prior };
            var result = await new CoreRuleEngine().ResolveAsync(state, new("prior-catalog", "P1", "PASS"), new PassCommand(), default);
            Assert.False(result.Accepted); Assert.Equal(ErrorCodes.RecoveryInconsistent, result.ErrorCode);
            Assert.Same(state, result.State); Assert.Empty(result.Events);
            Assert.Throws<MatchSessionException>(() => new MatchSession(state, new CoreRuleEngine(), NoopMatchJournal.Instance));
            var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(state, [], state, new CoreRuleEngine(), default);
            Assert.False(replay.IsMatch); Assert.NotEmpty(replay.Errors);
            Assert.Equal(replay.ExpectedStateHash, replay.ReplayedStateHash);
        }
    }
}
