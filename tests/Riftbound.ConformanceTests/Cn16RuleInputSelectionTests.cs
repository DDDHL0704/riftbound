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

    [Fact]
    public void ActualPackagedInputsSelectTheCurrentCnCoreWithoutCertifyingAllItsSemantics()
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
        // Exact UNL-139/219 current official erratum adoption; authority and scope remain unchanged.
        Assert.Equal("d5dbe0401fc52312c0f9033702b8090ff4063499db878706317dc259d051ac29", RuleInputIdentity.Current.CatalogSha256);
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
            "9706138cf5ec1795ad41dc689632603bb0530807f81420362fc3fe770d2874e9", PriorCn16Catalog })
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
