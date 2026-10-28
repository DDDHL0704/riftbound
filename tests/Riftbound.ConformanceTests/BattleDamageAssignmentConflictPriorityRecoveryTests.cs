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
        var initial = MixedDefenderPosition();
        var driver = new StandbyDefendObserverTests.Driver(initial);
        var opened = await driver.Act("P1", new DeclareBattleCommand(BattlefieldObjectId,
            [AttackerObjectId], [BulwarkDefenderObjectId, BackRowDefenderObjectId], ["COMBAT_ASSIGNMENT"]));
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
