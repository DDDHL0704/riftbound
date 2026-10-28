using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class RadianceAuthoritySourcesTests
{
    private const string PriorAuthority = "70ccda5893a1b8b56cdceadee35e084b1e3f5b297ef64e1ce7d0cbeb141ce2d4";
    private const string PriorProfile = "cc07cf3eef6a9af403454642a0435d061544400b5e01d08d3fdc65869bdad180";
    private const string SnapshotSha = "f73cbe2efcca701cc48f59e790487640cb1b72d82b70f9a97bbd49a6cef0c7d9";

    [Theory]
    [InlineData("GLOBAL-SUPPLEMENT-13", "666ae2800998100b12d0224cc63a5f4d510f0811eae85fec38f26eb85b85bb8d", "f9c930e96a0f09df6000b43c049bd04e3c7fc84e5f7ec46d21d5c34f004f05c9", "2026-10-09T16:00:00.000Z", "2026-10-16")]
    [InlineData("GLOBAL-SUPPLEMENT-14", "3bbdd5e2e876f193ce4d02bb616a0d7bf9a66848c4403a2f6c2bfbcbe708c664", "6fe7069b673e55a77c865776962e0694d51bd7ad155f085fc6eb8bb0a9d904e9", "2026-10-09T16:00:00.000Z", null)]
    [InlineData("GLOBAL-SUPPLEMENT-15", "6efbdc0389654c3ea4d8db42506e2816cc99352b454fcbc3775e1c40eec6401a", "c7a11d4043f1f601d693afea75f24b5232bf7352047dc951fc68f056ae92a5c3", "2026-10-26T16:00:00.000Z", null)]
    public void GlobalSupplementsBindBothRetrievedBodiesAndRemainSubjectToTheCnSources(
        string id, string englishSha, string chineseSha, string published, string? effective)
    {
        using var profile = Profile(); using var snapshot = Snapshot();
        var selected = Source(profile.RootElement.GetProperty("adoptedSources"), id);
        var retained = Source(snapshot.RootElement.GetProperty("sources"), id);
        Assert.Equal(englishSha, selected.GetProperty("sha256").GetString());
        Assert.Equal(englishSha, Sha(retained.GetProperty("bodyHtml").GetString()!));
        Assert.Equal(chineseSha, selected.GetProperty("localizedCompanion").GetProperty("sha256").GetString());
        Assert.Equal(chineseSha, Sha(retained.GetProperty("localizedBodyHtml").GetString()!));
        Assert.Equal("GLOBAL", selected.GetProperty("region").GetString());
        Assert.Equal("en-US", selected.GetProperty("language").GetString());
        Assert.Equal("SUPPLEMENTAL_CN_PREVAILS", selected.GetProperty("role").GetString());
        Assert.Equal("OFFICIAL_GLOBAL_TRANSLATION_NOT_REGIONAL_OVERRIDE",
            selected.GetProperty("localizedCompanion").GetProperty("role").GetString());
        Assert.Equal(published, selected.GetProperty("publishedAt").GetString());
        Assert.Equal(effective, selected.GetProperty("effectiveDate").GetString());
    }

    [Theory]
    [InlineData("DOC-17", "82b17be790f5c60a231a88bc276f6fffe847b25c35a9eb88032cf1ae3153362c", "b3704f26fd9f68bc6d7a449dfc82c8bde78df7064085efb7be90284bd4416a13", "REGIONAL_JUDGE_FAQ_OFFICIAL_FAQ_PREVAILS", 17)]
    [InlineData("DOC-18", "ff7d4e8db970c343eca84238232dcf3b36440e9cb24b5d94ccaff97a3270ee36", "62e98697f6ab1d5ede91244f09868c99ba61673988e3ad8c70572fcc2537192c", "REGIONAL_OFFICIAL_FAQ_NEWER_CORE_PREVAILS", 9)]
    public void RegionalFaqVersionsRetainTheirExactPdfAndTextIdentitiesWithoutInventingEffectiveDates(
        string id, string pdfSha, string textSha, string role, int pages)
    {
        using var profile = Profile(); using var snapshot = Snapshot();
        var selected = Source(profile.RootElement.GetProperty("adoptedSources"), id);
        var retained = Source(snapshot.RootElement.GetProperty("sources"), id);
        Assert.Equal(pdfSha, selected.GetProperty("sha256").GetString());
        Assert.Equal(pdfSha, retained.GetProperty("sha256").GetString());
        Assert.Equal(textSha, Sha(retained.GetProperty("textSnapshot").GetString()!));
        Assert.Equal(pages, retained.GetProperty("pages").GetInt32());
        Assert.Equal("2026-10-27", selected.GetProperty("versionDate").GetString());
        Assert.Null(selected.GetProperty("effectiveDate").GetString());
        Assert.Equal("CN", selected.GetProperty("region").GetString());
        Assert.Equal("zh-CN", selected.GetProperty("language").GetString());
        Assert.Equal(role, selected.GetProperty("role").GetString());
        Assert.StartsWith("https://cdn.playloltcg.com/lol/2026/10/2026-10-27/", selected.GetProperty("url").GetString());
    }

    [Fact]
    public void AllPreviousSourceRecordsAndThePrimaryCoreRemainExactWithoutCertifyingTheirSemantics()
    {
        using var profile = Profile(); using var snapshot = Snapshot();
        var rawPrior = snapshot.RootElement.GetProperty("priorProfileRawUtf8").GetString()!;
        Assert.Equal(PriorProfile, Sha(rawPrior));
        using var prior = JsonDocument.Parse(rawPrior);
        var oldSources = prior.RootElement.GetProperty("adoptedSources").EnumerateArray().ToArray();
        var sources = profile.RootElement.GetProperty("adoptedSources").EnumerateArray().ToArray();
        Assert.Equal(26, oldSources.Length); Assert.Equal(31, sources.Length);
        for (var i = 0; i < oldSources.Length; i++)
            Assert.Equal(MatchStateHasher.HashValue(oldSources[i]), MatchStateHasher.HashValue(sources[i]));
        Assert.Equal(new[] { "DOC-17", "DOC-18", "GLOBAL-SUPPLEMENT-13", "GLOBAL-SUPPLEMENT-14", "GLOBAL-SUPPLEMENT-15" },
            sources.Skip(26).Select(s => s.GetProperty("id").GetString()));
        foreach (var field in new[] { "region", "language", "primaryCoreId", "cardQa", "excludedFutureSourceIds", "supersededSourceIds" })
            Assert.Equal(MatchStateHasher.HashValue(prior.RootElement.GetProperty(field)),
                MatchStateHasher.HashValue(profile.RootElement.GetProperty(field)));
        Assert.Equal("DOC-01", profile.RootElement.GetProperty("primaryCoreId").GetString());
        Assert.Equal("2026-10-28", profile.RootElement.GetProperty("applicabilityDate").GetString());
        Assert.Equal(SnapshotSha, profile.RootElement.GetProperty("supplementSnapshot").GetProperty("sha256").GetString());
        Assert.False(profile.RootElement.GetProperty("semanticCoverageCertified").GetBoolean());
        Assert.False(snapshot.RootElement.GetProperty("review").GetProperty("semanticCoverageCertified").GetBoolean());
        Assert.NotEqual(PriorAuthority, RuleInputIdentity.Current.AuthoritySha256);
    }

    [Theory]
    [InlineData("state")]
    [InlineData("origin")]
    [InlineData("preview")]
    public async Task TheActualPreviousAuthorityCannotResumeOrRestoreWithOtherwiseCurrentInputs(string boundary)
    {
        var current = RuleInputIdentity.Current;
        var previous = new RuleInputIdentity(1, "CN", "zh-CN", PriorAuthority, current.CatalogSha256, current.ImplementationSha256);
        var clean = MatchReplayInitialStateBuilder.FromSeats("radiance-source-version", new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" });
        var old = clean with { RuleInputs = previous };
        var state = boundary == "state" ? old : clean with { PendingRuleChoice = new(
            boundary == "origin" ? old : clean, new("original", "P1", CommandTypes.Pass),
            JsonSerializer.SerializeToElement(new PassCommand()), [],
            new("choice", "P1", "test", [new("yes", "Yes")], Preview: boundary == "preview" ? old : clean)) };
        var before = MatchStateHasher.Hash(state);
        var rejected = await new CoreRuleEngine().ResolveAsync(state, new("prior-source", "P1", CommandTypes.Surrender), new SurrenderCommand(), default);
        Assert.False(rejected.Accepted); Assert.Equal(ErrorCodes.RecoveryInconsistent, rejected.ErrorCode);
        Assert.Same(state, rejected.State); Assert.Empty(rejected.Events); Assert.Equal(before, MatchStateHasher.Hash(state));
        Assert.Throws<MatchSessionException>(() => new MatchSession(state, new CoreRuleEngine(), NoopMatchJournal.Instance));
        var frame = new MatchRecoveryFrame(state.RoomId, 0, 0, [], [], new Dictionary<string, RecoveredPlayerView>(), [], state, state);
        Assert.Throws<MatchSessionException>(() => MatchSession.Restore(frame, new CoreRuleEngine(), NoopMatchJournal.Instance));
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(state, [], state, new CoreRuleEngine(), default);
        Assert.False(replay.IsMatch); Assert.NotEmpty(replay.Errors);
        Assert.Equal(replay.ExpectedStateHash, replay.ReplayedStateHash);
    }

    private static string CatalogDirectory => Path.GetDirectoryName(OfficialCardCatalog.DefaultCatalogPath())!;
    private static JsonDocument Profile() => JsonDocument.Parse(File.ReadAllBytes(Path.Combine(CatalogDirectory, "rule-input-scope.json")));
    private static JsonDocument Snapshot()
    {
        var bytes = File.ReadAllBytes(Path.Combine(CatalogDirectory, "upstream", "2026-10-28", "radiance-rule-authorities", "source-snapshot.json"));
        Assert.Equal(SnapshotSha, Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
        return JsonDocument.Parse(bytes);
    }
    private static JsonElement Source(JsonElement sources, string id)
        => Assert.Single(sources.EnumerateArray(), s => s.GetProperty("id").GetString() == id);
    private static string Sha(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
}
