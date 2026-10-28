using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Riftbound.GodotClient;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed partial class BattleDamageAssignmentLifecycleTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task MixedPriorityPromptAndDamageEventsSurviveColdRestoreAndReplay(bool mixedFirst)
    {
        var initial = MixedDefenderPosition() with { Tick = 0 };
        var driver = new StandbyDefendObserverTests.Driver(initial);
        ResolutionResult opened;
        try
        {
            opened = await driver.Act("P1", new DeclareBattleCommand(BattlefieldObjectId,
                [AttackerObjectId], [BulwarkDefenderObjectId, BackRowDefenderObjectId], ["COMBAT_ASSIGNMENT"]));
        }
        catch
        {
            if (Environment.GetEnvironmentVariable("RIFTBOUND_CONTRADICTORY_PRIORITY_DIAGNOSTICS") is { } folder)
            {
                Directory.CreateDirectory(folder);
                var before = driver.State;
                var after = JsonSerializer.Deserialize<MatchState>(JsonSerializer.Serialize(before))!;
                File.WriteAllText(Path.Combine(folder, $"battle-open-roundtrip-{mixedFirst}.json"), JsonSerializer.Serialize(new {
                    before, after, beforeHash = MatchStateHasher.Hash(before), afterHash = MatchStateHasher.Hash(after),
                    errors = MatchRecoveryValidator.ValidateComponentStateAndViews(after) }, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
            }
            throw;
        }
        var candidate = Assert.Single(opened.Prompts["P1"].Candidates ?? [], c => c.Action == CommandTypes.AssignCombatDamage);
        var metadata = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(candidate.Metadata);
        var options = Assert.IsAssignableFrom<IReadOnlyDictionary<string, IReadOnlyList<int>>>(metadata["targetPriorityOptions"]);
        Assert.Equal([0, 2], options[BulwarkDefenderObjectId]);
        Assert.Equal([1], options[BackRowDefenderObjectId]);
        Assert.Contains("同时拥有壁垒和后排", Assert.IsType<string>(metadata["assignmentGuidance"]));
        var optionsJson = JsonSerializer.SerializeToElement(options);
        Assert.True(CombatDamagePriorityOptions.AllowsFirstOrLast(optionsJson, BulwarkDefenderObjectId));
        Assert.False(CombatDamagePriorityOptions.AllowsFirstOrLast(optionsJson, BackRowDefenderObjectId));

        CombatDamageAssignmentDto[] assignments = mixedFirst
            ? [new(AttackerObjectId, BulwarkDefenderObjectId, 2), new(AttackerObjectId, BackRowDefenderObjectId, 3)]
            : [new(AttackerObjectId, BackRowDefenderObjectId, 4), new(AttackerObjectId, BulwarkDefenderObjectId, 1)];
        await driver.Act("P1", new AssignCombatDamageCommand($"battle:{BattlefieldObjectId}", BattlefieldObjectId, assignments));
        var resolved = await driver.Act("P2", new AssignCombatDamageCommand($"battle:{BattlefieldObjectId}", BattlefieldObjectId,
            [new(BulwarkDefenderObjectId, AttackerObjectId, 2), new(BackRowDefenderObjectId, AttackerObjectId, 4)]));
        var damage = Assert.Single(resolved.Events, e => e.Kind == "DAMAGE_APPLIED"
            && Equals(e.Payload.GetValueOrDefault("targetObjectId"), BulwarkDefenderObjectId));
        Assert.Equal(mixedFirst ? "BULWARK_FIRST" : "BACK_ROW_LAST", damage.Payload["assignmentRole"]);
        Assert.Equal(mixedFirst ? 1 : 2, damage.Payload["assignmentIndex"]);
        await BanishEffectPlayTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task DefendingPlayerMixedPrioritySurvivesColdRestoreAndReplay(bool mixedFirst)
    {
        var initial = MixedAttackerPosition() with { Tick = 0 };
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await driver.Act("P1", new DeclareBattleCommand(BattlefieldObjectId, [AttackerObjectId, SecondAttackerObjectId],
            [BulwarkDefenderObjectId, BackRowDefenderObjectId], ["COMBAT_ASSIGNMENT"]));
        var first = await driver.Act("P1", new AssignCombatDamageCommand($"battle:{BattlefieldObjectId}", BattlefieldObjectId,
            [new(AttackerObjectId, BulwarkDefenderObjectId, 2), new(SecondAttackerObjectId, BulwarkDefenderObjectId, 2),
             new(SecondAttackerObjectId, BackRowDefenderObjectId, 2)]));
        var candidate = Assert.Single(first.Prompts["P2"].Candidates ?? [], c => c.Action == CommandTypes.AssignCombatDamage);
        var metadata = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(candidate.Metadata);
        var options = Assert.IsAssignableFrom<IReadOnlyDictionary<string, IReadOnlyList<int>>>(metadata["targetPriorityOptions"]);
        Assert.Equal([0, 2], options[AttackerObjectId]);
        Assert.Equal([1], options[SecondAttackerObjectId]);
        CombatDamageAssignmentDto[] assignments = mixedFirst
            ? [new(BulwarkDefenderObjectId, AttackerObjectId, 2), new(BulwarkDefenderObjectId, SecondAttackerObjectId, 2),
               new(BackRowDefenderObjectId, SecondAttackerObjectId, 1)]
            : [new(BulwarkDefenderObjectId, SecondAttackerObjectId, 4), new(BackRowDefenderObjectId, AttackerObjectId, 1)];
        var resolved = await driver.Act("P2", new AssignCombatDamageCommand($"battle:{BattlefieldObjectId}", BattlefieldObjectId, assignments));
        var damage = Assert.Single(resolved.Events, e => e.Kind == "DAMAGE_APPLIED"
            && Equals(e.Payload.GetValueOrDefault("targetObjectId"), AttackerObjectId));
        Assert.Equal(mixedFirst ? "BULWARK_FIRST" : "BACK_ROW_LAST", damage.Payload["assignmentRole"]);
        Assert.Equal(mixedFirst ? 1 : 2, damage.Payload["assignmentIndex"]);
        await BanishEffectPlayTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData("{\"U\":[0,2]}", true)]
    [InlineData("{\"U\":[2,0]}", true)]
    [InlineData("{\"U\":[0]}", false)]
    [InlineData("{\"U\":[0,2,2]}", false)]
    [InlineData("{\"U\":[0,\"2\"]}", false)]
    [InlineData("{}", false)]
    public void NativePromptReadsOnlyTheTwoSupportedPriorityAlternatives(string json, bool expected)
    {
        using var document = JsonDocument.Parse(json);
        Assert.Equal(expected, CombatDamagePriorityOptions.AllowsFirstOrLast(document.RootElement, "U"));
    }
}
