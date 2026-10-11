using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.LocalDestructionRecallTests;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;

namespace Riftbound.ConformanceTests;

public sealed class CopiedActivatedAbilityTests
{
    internal static async Task<MatchState> Position()
    {
        var s = (await AttachedRuleTextTests.Attach(AttachedRuleTextTests.Position("SFD·050/221", 0))).State;
        s = OfficialHoldSequenceTests.AddUnit(s, "TARGET", "SFD·130/221", "F", "P1");
        return At(s, "TARGET", "P1", "BASE");
    }

    internal static IReadOnlyDictionary<string, object?>[] Requirements(MatchState s, string player = "P1")
    {
        var candidate = ResolutionResult.BuildPrompts(s)[player].Candidates?.FirstOrDefault(c => c.Action == CommandTypes.ActivateAbility && c.Enabled);
        if (candidate is null) return [];
        var rows = Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(candidate.Metadata!["sourceRequirements"]);
        return rows.Where(r => Equals(r.GetValueOrDefault("sourceObjectId"), "HOST")
            && Equals(r.GetValueOrDefault("abilityId"), P4ActivatedAbilityCatalog.AzirSwiftSwapAbilityId)).ToArray();
    }

    internal static ActivateAbilityCommand Native() => new("HOST", P4ActivatedAbilityCatalog.AzirSwiftSwapAbilityId, ["TARGET"], []);
    internal static ActivateAbilityCommand Copy(MatchState s) => Native() with {
        AbilityInstanceId = (string)Requirements(s).First(r => r.GetValueOrDefault("abilityInstanceId") is not null)["abilityInstanceId"]! };

    [Fact]
    public async Task ServerOffersNativeAndCopiedActivatedAbilityAsSeparateInstances()
    {
        var s = await Position(); var rows = Requirements(s);
        Assert.Equal(2, rows.Length);
        Assert.Single(rows, r => r.GetValueOrDefault("abilityInstanceId") is not null);
        Restore(s); ArmamentMightTests.PublicRecovery(s);
    }

    [Fact]
    public async Task UsingNativeOnceDoesNotConsumeTheCopiedInstance()
    {
        var s = await Position(); var activated = await Act(s, "P1", Native());
        var done = await Top(activated.State);
        var remaining = Assert.Single(Requirements(done.State));
        Assert.NotNull(remaining.GetValueOrDefault("abilityInstanceId"));
        var rejected = await new CoreRuleEngine().ResolveAsync(done.State, new("repeat-native", "P1", CommandTypes.ActivateAbility), Native(), default);
        Assert.False(rejected.Accepted); Assert.Equal(MatchStateHasher.Hash(done.State), MatchStateHasher.Hash(rejected.State));
        Restore(done.State); ArmamentMightTests.PublicRecovery(done.State);
    }

    [Fact]
    public async Task NativeAndCopiedActivationPaySeparatelyAndEachCanBeUsedOnlyOnce()
    {
        var s = await Position(); var power = s.RunePools["P1"].TotalPower;
        s = (await Top((await Act(s, "P1", Native())).State)).State;
        var copy = Copy(s); var activated = await Act(s, "P1", copy);
        var item = Assert.Single(activated.State.StackItems);
        Assert.True(item.AzirMovement!.CopiedInstance); Assert.NotNull(item.AzirMovement.Origin);
        Restore(activated.State); ArmamentMightTests.PublicRecovery(activated.State);
        s = (await Top(activated.State)).State;
        Assert.Equal(power - 2, s.RunePools["P1"].TotalPower); Assert.Empty(Requirements(s));
        foreach (var command in new[] { Native(), copy }) {
            var rejected = await new CoreRuleEngine().ResolveAsync(s, new("repeat", "P1", CommandTypes.ActivateAbility), command, default);
            Assert.False(rejected.Accepted); Assert.Equal(MatchStateHasher.Hash(s), MatchStateHasher.Hash(rejected.State));
        }
        Restore(s); ArmamentMightTests.PublicRecovery(s);
    }

    [Fact]
    public async Task ActualDetachAndReattachRefreshOnlyTheCopiedAbilityAndRejectTheOldInstanceId()
    {
        var s = await Position(); s = (await Top((await Act(s, "P1", Native())).State)).State;
        var old = Copy(s); s = (await Top((await Act(s, "P1", old)).State)).State;
        s = AttachedRuleTextTests.WithSpell(s, "P1", "SFD·011/221");
        s = (await Top((await Act(s, "P1", new PlayCardCommand("SPELL", "SFD·011/221", ["HOST", "GEAR"]))).State)).State;
        Assert.Empty(Requirements(s)); Restore(s); ArmamentMightTests.PublicRecovery(s);
        s = (await AttachedRuleTextTests.Attach(s)).State;
        var current = Copy(s); Assert.NotEqual(old.AbilityInstanceId, current.AbilityInstanceId);
        Assert.Single(Requirements(s));
        var rejected = await new CoreRuleEngine().ResolveAsync(s, new("stale-instance", "P1", CommandTypes.ActivateAbility), old, default);
        Assert.False(rejected.Accepted); Assert.Equal(MatchStateHasher.Hash(s), MatchStateHasher.Hash(rejected.State));
        s = (await Top((await Act(s, "P1", current)).State)).State;
        Assert.Empty(Requirements(s)); Restore(s); ArmamentMightTests.PublicRecovery(s);
    }

    [Fact]
    public async Task TwoArmamentsCreateIndependentCopiedInstances()
    {
        var s = await Position(); s = (await AttachedRuleTextTests.Attach(AttachedRuleTextTests.AddSong(s, "SONG2"), "SONG2")).State;
        Assert.Equal(3, Requirements(s).Length);
        var commands = Requirements(s).Select(r => Native() with { AbilityInstanceId = r.GetValueOrDefault("abilityInstanceId") as string }).ToArray();
        var power = s.RunePools["P1"].TotalPower;
        foreach (var command in commands) s = (await Top((await Act(s, "P1", command)).State)).State;
        Assert.Equal(power - 3, s.RunePools["P1"].TotalPower); Assert.Empty(Requirements(s));
        Restore(s); ArmamentMightTests.PublicRecovery(s);
    }

    [Theory]
    [InlineData("origin")] [InlineData("flag")] [InlineData("revision")] [InlineData("equipment-generation")]
    [InlineData("unit-generation")] [InlineData("card")]
    public async Task PrivateRecoveryRejectsForgedCopiedActivatedOrigins(string change)
    {
        var s = await Position(); var activated = await Act(s, "P1", Copy(s));
        var item = Assert.Single(activated.State.StackItems); var context = item.AzirMovement!; var origin = context.Origin!;
        var forged = change switch {
            "origin" => context with { Origin = null },
            "flag" => context with { CopiedInstance = false },
            "revision" => context with { Origin = origin with { Revision = 99 } },
            "equipment-generation" => context with { Origin = origin with { Equipment = origin.Equipment with { Generation = 99 } } },
            "unit-generation" => context with { Origin = origin with { Unit = origin.Unit with { Generation = 99 } } },
            _ => context with { Origin = origin with { CardNo = "OGN·109/298" } }
        };
        Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(activated.State with { StackItems = [item with { AzirMovement = forged }] }));
        Restore(activated.State); ArmamentMightTests.PublicRecovery(activated.State);
    }
}
