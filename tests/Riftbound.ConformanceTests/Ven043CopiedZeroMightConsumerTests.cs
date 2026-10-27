using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class Ven043CopiedZeroMightConsumerTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ActualCopyKeepsPrintedZeroThenEarnedEmpowerSurvivesDetachingItsCopiedText(bool detach)
    {
        var initial = OfficialCopyIdentityTests.AddSpell(ShadySpectaclesTests.Position("VEN·043"), "DETACH", "SFD·011/221");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["COPY-SOURCE"] = OfficialGarenConquestBattlefieldCountTests.Printed("COPY-SOURCE", "VEN·043", "P1") } };
        var game = new Driver(initial);
        try
        {
            var basis = Might(game.State); var generation = game.State.CardObjects["HOST"].ObjectGeneration;
            Assert.Equal(0, game.State.CardObjects["COPY-SOURCE"].Power);
            await game.Act("P1", new AssembleEquipmentCommand("GEAR", "HOST", ["ASSEMBLE_YELLOW"])); await game.Top();
            var copy = game.State.PendingRuleChoice!; Assert.Equal("ATTACHMENT_COPY_SELECTION", copy.Request.Kind);
            await game.Act("P1", new PayCostCommand(copy.Request.Id, "RULE_REPLACEMENT", ["COPY:COPY-SOURCE"]));
            Assert.Equal("VEN·043", game.State.CardObjects["HOST"].CardNo); Assert.Equal(0, game.State.CardObjects["HOST"].Power);
            Assert.Equal(0, Might(game.State)); Assert.Contains(CardObjectTags.Spellshield, game.State.CardObjects["HOST"].Tags);
            Assert.False(game.State.CardObjects["HOST"].IsEmpowered);
            var candidate = Assert.Single(ResolutionResult.BuildPrompts(game.State)["P1"].Candidates!, c => c.Action == CommandTypes.ActivateAbility);
            var row = Assert.Single(Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(candidate.Metadata!["sourceRequirements"]),
                r => (string?)r["sourceObjectId"] == "HOST" && (string?)r["abilityId"] == "EMPOWER:0:0");
            var instance = Assert.IsType<string>(row["abilityInstanceId"]); var command = new ActivateAbilityCommand("HOST", "EMPOWER:0:0", []);
            var raw = JsonSerializer.SerializeToNode(command, Json)!.AsObject();
            raw["promptId"] = game.PromptFor("P1").PromptId; raw["snapshotTick"] = game.State.Tick;
            await game.ActRetained(new("P1", "steelclaw-copy-instance-required", JsonSerializer.SerializeToElement(raw, Json)), false);
            var before = game.State.RunePools["P1"]; await game.Act("P1", command with { AbilityInstanceId = instance });
            Assert.Equal(before.Mana - 7, game.State.RunePools["P1"].Mana); Assert.Equal(before.TotalPower, game.State.RunePools["P1"].TotalPower);
            var earned = Assert.Single(game.State.StackItems).EmpowerActivation!;
            Assert.Equal(7, earned.PaidMana); Assert.Equal(generation, earned.Source.Generation); Assert.Equal("GEAR", earned.Origin!.Equipment.ObjectId);
            Assert.False(game.State.CardObjects["HOST"].IsEmpowered); Assert.Equal(0, Might(game.State));
            if (detach)
            {
                await game.Act("P1", new PlayCardCommand("DETACH", "SFD·011/221", ["HOST", "GEAR"])); await game.Top();
                Assert.Null(game.State.CardObjects["HOST"].AttachmentUnitCopies); Assert.Equal(basis, Might(game.State));
                Assert.False(game.State.CardObjects["HOST"].IsEmpowered); Assert.Equal(earned, Assert.Single(game.State.StackItems).EmpowerActivation);
            }
            await game.Top(); Assert.True(game.State.CardObjects["HOST"].IsEmpowered);
            Assert.Equal(detach ? basis : 7, Might(game.State)); Assert.Equal(detach ? basis : 0, game.State.CardObjects["HOST"].Power);
            Assert.Equal(!detach, game.State.CardObjects["HOST"].Tags.Contains(CardObjectTags.Spellshield));
            Assert.Equal(generation, game.State.CardObjects["HOST"].ObjectGeneration); await game.Replay();
        }
        finally
        {
            if (Environment.GetEnvironmentVariable("RIFTBOUND_VEN043_EVIDENCE") is { Length: > 0 } folder)
            {
                Directory.CreateDirectory(folder); await File.WriteAllTextAsync(Path.Combine(folder, "copied-zero-might-detach-" + detach + ".json"), JsonSerializer.Serialize(new {
                    scope = "Existing ShadySpectacles midgame fixture and its pre-existing resource pools, exact normally printed zero-Might copy source, real assembly/copy and explicit origin. Seven-mana cost and earned skill after detach are actual consumers. No forced empowered state, source/profile injection, canonical opening, SQL/network, applicability, full L2/L3 or whole-card claim.",
                    initial, final = game.State, commands = game.Commands(), events = game.Events(), ruleInputs = RuleInputIdentity.Current }, Json));
            }
        }
    }

    private static int Might(MatchState state) => JsonSerializer.SerializeToElement(ResolutionResult.BuildSnapshots(state)["P1"], Json)
        .GetProperty("players").GetProperty("P1").GetProperty("objects").GetProperty("HOST").GetProperty("effectivePower").GetInt32();
}
