using Xunit;
namespace Riftbound.ConformanceTests;

// Re-execute the original manual test vectors against current CN 355.5.b,
// 383.2.a.1 and 383.4.a.2. Keep the retained fixture bytes, all commands,
// object identities, seeds and final-state assertions unchanged. This is a
// current test expectation, never a migration of an accepted match journal.
internal static class OptionalEntryFixtureExpectations
{
    internal static ConformanceFixture ForCurrentRules(ConformanceFixture retained)
    {
        var paid = retained.FixtureId is "p2-preflight-play-blast-crew-apprentice-optional-damage"
            or "p2-preflight-play-frostcoat-cub-optional-power-minus-two";
        var unpaid = retained.FixtureId is "p2-preflight-play-blast-crew-apprentice-no-optional-damage"
            or "p2-preflight-play-frostcoat-cub-no-optional-power-minus-two";
        if (!paid && !unpaid) return retained;
        var original = retained.Expected;
        Assert.Equal(3, original.FinalTick);
        Assert.Equal(paid ? 9 : 8, original.EventKinds.Count);
        Assert.Equal("TRIGGER_QUEUED", original.EventKinds[4]);
        var expected = paid ? original with {
            EventKinds = original.EventKinds.Take(5).Concat(["TRIGGER_CONFIRMED"]).Concat(original.EventKinds.Skip(5)).ToArray(),
            Events = original.Events!.Take(5).Concat([new ConformanceExpectedEvent("TRIGGER_CONFIRMED")]).Concat(original.Events!.Skip(5)).ToArray()
        } : original with {
            FinalTick = 1, EventKinds = original.EventKinds.Take(4).ToArray(), Events = original.Events!.Take(4).ToArray()
        };
        return retained with { Expected = expected };
    }
}
