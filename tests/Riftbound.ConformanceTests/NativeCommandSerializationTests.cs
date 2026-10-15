using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.GodotClient;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class NativeCommandSerializationTests
{
    [Theory]
    [InlineData(1)] [InlineData(2)]
    public void EveryTypedRepeatDeclarationSurvivesTheNativeWireAndStrictServerMapper(int echoes)
    {
        var declarations = Enumerable.Range(0, echoes).Select(i => new SpellRepeatChoice("mode-" + i,
            ["Target-" + i], ["BASE:P1"], "BATTLEFIELD:Field-A")).ToArray();
        var payload = new Dictionary<string, object?> {
            ["cmdType"] = CommandTypes.PlayCard, ["sourceObjectId"] = "Source-C", ["cardNo"] = "UNL-063/219",
            ["targetObjectIds"] = new[] { "First-Target" }, ["optionalCosts"] = new[] { "ECHO" },
            ["repeatChoices"] = declarations, ["promptId"] = "Case-Sensitive-Prompt", ["snapshotTick"] = 17L };
        // The old default serializer emits PascalCase records. The server must
        // continue rejecting that malformed command rather than accepting both.
        Assert.IsType<UnsupportedCommand>(GameCommandJsonMapper.Map(JsonSerializer.SerializeToElement(payload)));
        var wire = ClientCommandSerializer.Serialize(payload);
        var command = Assert.IsType<PlayCardCommand>(GameCommandJsonMapper.Map(wire));
        Assert.Equal("Source-C", command.SourceObjectId); Assert.Equal(new[] { "First-Target" }, command.TargetObjectIds);
        Assert.Equal("Case-Sensitive-Prompt", wire.GetProperty("promptId").GetString()); Assert.Equal(17, wire.GetProperty("snapshotTick").GetInt64());
        Assert.Equal(echoes, command.RepeatChoices!.Count);
        for (var i = 0; i < echoes; i++)
        {
            Assert.Equal(declarations[i].Mode, command.RepeatChoices[i].Mode);
            Assert.Equal(declarations[i].TargetObjectIds, command.RepeatChoices[i].TargetObjectIds);
            Assert.Equal(declarations[i].MovementDestinations, command.RepeatChoices[i].MovementDestinations);
            Assert.Equal(declarations[i].Destination, command.RepeatChoices[i].Destination);
            Assert.False(wire.GetProperty("repeatChoices")[i].TryGetProperty("TargetObjectIds", out _));
        }
    }

    [Fact]
    public void TypedDamageAssignmentsKeepTheirObjectIdentitiesAndNumbersAcrossTheSameWire()
    {
        var payload = new Dictionary<string, object?> { ["cmdType"] = CommandTypes.AssignCombatDamage,
            ["battleId"] = "Battle-X", ["battlefieldId"] = "Field-Y",
            ["assignments"] = new[] { new CombatDamageAssignmentDto("Unit-A", "Unit-B", 3) } };
        var wire = ClientCommandSerializer.Serialize(payload);
        var command = Assert.IsType<AssignCombatDamageCommand>(GameCommandJsonMapper.Map(wire));
        Assert.Equal("Battle-X", command.BattleId); Assert.Equal("Field-Y", command.BattlefieldId);
        Assert.Equal(new CombatDamageAssignmentDto("Unit-A", "Unit-B", 3), Assert.Single(command.Assignments!));
    }
}
