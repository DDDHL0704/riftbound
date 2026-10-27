using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class Ven124CopiedFriendlyDestructionConsumerTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ActualCopiedFacePaysFriendlyDestructionOnceAndRetainsEarnedEmpowerAfterDetach(bool detach)
    {
        var initial = OfficialCopyIdentityTests.AddSpell(ShadySpectaclesTests.Position("VEN·124"), "DETACH", "SFD·011/221");
        var game = new Driver(initial);
        try
        {
            var basis = Might(game.State); var generation = game.State.CardObjects["HOST"].ObjectGeneration;
            await game.Act("P1", new AssembleEquipmentCommand("GEAR", "HOST", ["ASSEMBLE_YELLOW"]));
            await game.Top(); var pendingCopy = game.State.PendingRuleChoice!;
            Assert.Equal("ATTACHMENT_COPY_SELECTION", pendingCopy.Request.Kind);
            await game.Act("P1", new PayCostCommand(pendingCopy.Request.Id, "RULE_REPLACEMENT", ["COPY:COPY-SOURCE"]));
            Assert.Equal("VEN·124", game.State.CardObjects["HOST"].CardNo);
            Assert.False(game.State.CardObjects["HOST"].IsEmpowered); Assert.Equal(3, Might(game.State));
            var candidate = Assert.Single(ResolutionResult.BuildPrompts(game.State)["P1"].Candidates!, c => c.Action == CommandTypes.ActivateAbility);
            var row = Assert.Single(Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(candidate.Metadata!["sourceRequirements"]),
                r => (string?)r["sourceObjectId"] == "HOST" && (string?)r["abilityId"] == "EMPOWER:0:0");
            var instance = Assert.IsType<string>(row["abilityInstanceId"]);
            Assert.Equal(1, row["requiredAdditionalCostChoiceCount"]);
            var cost = Assert.Single(Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(row["additionalCostChoices"]),
                c => c.ObjectIds is not null && c.ObjectIds.SequenceEqual(["COPY-SOURCE"]));
            var activate = new ActivateAbilityCommand("HOST", "EMPOWER:0:0", [], OptionalCosts: [cost.Id]);
            var raw = JsonSerializer.SerializeToNode(activate, Json)!.AsObject();
            raw["promptId"] = game.PromptFor("P1").PromptId; raw["snapshotTick"] = game.State.Tick;
            await game.ActRetained(new("P1", "ven124-missing-copy-instance", JsonSerializer.SerializeToElement(raw, Json)), false);
            Assert.Contains("COPY-SOURCE", game.State.PlayerZones["P1"].Base);
            var costGeneration = game.State.CardObjects["COPY-SOURCE"].ObjectGeneration;
            var mana = game.State.RunePools["P1"].Mana; var power = game.State.RunePools["P1"].TotalPower;
            await game.Act("P1", activate with { AbilityInstanceId = instance });
            Assert.DoesNotContain("COPY-SOURCE", game.State.PlayerZones["P1"].Base);
            Assert.Contains("COPY-SOURCE", game.State.PlayerZones["P1"].Graveyard);
            Assert.Equal(costGeneration + 1, game.State.CardObjects["COPY-SOURCE"].ObjectGeneration);
            Assert.Equal(mana, game.State.RunePools["P1"].Mana); Assert.Equal(power, game.State.RunePools["P1"].TotalPower);
            Assert.False(game.State.CardObjects["HOST"].IsEmpowered); Assert.Equal(3, Might(game.State));
            var earned = Assert.Single(game.State.StackItems).EmpowerActivation!;
            Assert.Equal("HOST", earned.Source.ObjectId); Assert.Equal(generation, earned.Source.Generation);
            Assert.Equal("GEAR", earned.Origin!.Equipment.ObjectId); Assert.Equal(0, earned.PaidMana);
            if (detach)
            {
                await game.Act("P1", new PlayCardCommand("DETACH", "SFD·011/221", ["HOST", "GEAR"]));
                await game.Top(); Assert.Null(game.State.CardObjects["HOST"].AttachmentUnitCopies);
                Assert.False(game.State.CardObjects["HOST"].IsEmpowered); Assert.Equal(basis, Might(game.State));
                Assert.Equal(earned, Assert.Single(game.State.StackItems).EmpowerActivation);
            }
            await game.Top(); Assert.True(game.State.CardObjects["HOST"].IsEmpowered);
            Assert.Equal(detach ? basis : 5, Might(game.State));
            Assert.Equal(generation, game.State.CardObjects["HOST"].ObjectGeneration);
            Assert.Equal(1, game.State.PlayerZones["P1"].Graveyard.Count(id => id == "COPY-SOURCE"));
            await game.Replay();
        }
        finally
        {
            if (Environment.GetEnvironmentVariable("RIFTBOUND_VEN124_EVIDENCE") is { Length: > 0 } folder)
            {
                Directory.CreateDirectory(folder);
                await File.WriteAllTextAsync(Path.Combine(folder, "copied-friendly-cost-detach-" + detach + ".json"), JsonSerializer.Serialize(new {
                    scope = "Existing ShadySpectacles midgame fixture, actual assembly/copy choice and explicit ability origin; ordinary friendly cost unit pre-exists, no source/profile/empowered-state injection. Original per-command privacy/recovery/dedup and original action replay guards remain. No ordinary opening, SQL/network, applicability, L2/L3 or whole-card claim.",
                    initial, final = game.State, commands = game.Commands(), events = game.Events(), ruleInputs = RuleInputIdentity.Current }, Json));
            }
        }
    }

    private static int Might(MatchState state) => JsonSerializer.SerializeToElement(ResolutionResult.BuildSnapshots(state)["P1"], Json)
        .GetProperty("players").GetProperty("P1").GetProperty("objects").GetProperty("HOST").GetProperty("effectivePower").GetInt32();
}
