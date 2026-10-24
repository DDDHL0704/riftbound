using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class StunBinaryOperationTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task AlreadyStunnedTargetsKeepSelectionPaymentAndEchoExecutionsWithoutAnotherStun(
        bool initiallyStunned, bool echo)
    {
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-kerplunk-echo-stun-attacking-unit.fixture.json");
        var originalBytes = await File.ReadAllBytesAsync(path);
        var fixture = await ConformanceFixture.LoadAsync(path, default);
        var initial = Assert.IsType<ConformanceInitialState>(fixture.InitialState);
        var objects = initial.CardObjects!.ToDictionary();
        objects["P2-UNIT-001"] = objects["P2-UNIT-001"] with
        {
            UntilEndOfTurnEffects = initiallyStunned ? ["STUNNED"] : []
        };
        var commands = fixture.Commands.ToArray();
        var raw = JsonNode.Parse(commands[0].Cmd.GetRawText())!.AsObject();
        raw["optionalCosts"] = JsonSerializer.SerializeToNode(echo ? new[] { "ECHO" } : [], json);
        commands[0] = commands[0] with { Cmd = JsonSerializer.SerializeToElement(raw, json) };
        fixture = fixture with
        {
            FixtureId = $"binary-stun-{initiallyStunned}-{echo}",
            InitialState = initial with { CardObjects = objects },
            Commands = commands
        };
        var result = await ConformanceFixtureRunner.RunAsync(fixture, new CoreRuleEngine(), default);
        if (Environment.GetEnvironmentVariable("RIFTBOUND_BINARY_STUN_EVIDENCE") is { Length: > 0 } folder)
        {
            Directory.CreateDirectory(folder);
            var modules = new[] { typeof(CoreRuleEngine).Assembly, typeof(OfficialCardCatalog).Assembly, typeof(GameCommand).Assembly }
                .Select(assembly => new { name = assembly.GetName().Name, path = assembly.Location,
                    mvid = assembly.ManifestModule.ModuleVersionId,
                    sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(assembly.Location))).ToLowerInvariant() });
            var input = new { fixture.SchemaVersion, fixture.RoomId, fixture.Players, fixture.InitialState, fixture.Commands, fixture.Seed };
            await File.WriteAllTextAsync(Path.Combine(folder, $"binary-{initiallyStunned}-{echo}.json"),
                JsonSerializer.Serialize(new { input, result, ruleInputs = RuleInputIdentity.Current, modules }, json));
        }
        Assert.Equal(3, result.FinalTick);
        Assert.Single(result.Events, item => item.Kind == "CARD_PLAYED");
        var payment = Assert.Single(result.Events, item => item.Kind == "COST_PAID");
        Assert.Equal(echo ? 4 : 2, payment.Payload["mana"]);
        Assert.Equal(echo ? 0 : 2, result.FinalState.RunePools["P1"].Mana);
        Assert.Single(result.Events, item => item.Kind == "STACK_ITEM_RESOLVED");
        if (echo) Assert.Equal(2, result.Events.Count(item => item.Kind == "SPELL_EXECUTION_COMPLETED"));
        var stuns = result.Events.Where(item => item.Kind == "STATUS_EFFECT_APPLIED").ToArray();
        Assert.Equal(initiallyStunned ? 0 : 1, stuns.Length);
        Assert.All(stuns, item =>
        {
            Assert.Equal("P2-UNIT-001", item.Payload["targetObjectId"]);
            Assert.Equal("STUNNED", item.Payload["effectId"]);
        });
        Assert.Equal(["STUNNED"], result.FinalState.CardObjects["P2-UNIT-001"].UntilEndOfTurnEffects);
        Assert.Contains("P1-SPELL-KERPLUNK", result.FinalState.PlayerZones["P1"].Graveyard);
        Assert.Empty(result.FinalState.StackItems);
        Assert.Equal(originalBytes, await File.ReadAllBytesAsync(path));
    }
}
