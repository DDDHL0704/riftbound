using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

// Controlled positions. The starting hand size is fixed before the match session;
// conquest, both priority passes, the discard/draw, recovery and replay are real.
public sealed class ZaunEmptyAndSingleHandTests
{
    [Theory]
    [InlineData("P1", 0)] [InlineData("P1", 1)]
    [InlineData("P2", 0)] [InlineData("P2", 1)]
    public async Task ZeroOrOneLegalHandCardsDoNotOpenAnUnnecessaryDiscardChoice(string player, int handCount)
    {
        var initial = ThreeBattlefieldTimingProbe.Position(player, "OGN·298/298");
        var removed = initial.PlayerZones[player].Hand.Skip(handCount).ToHashSet(StringComparer.Ordinal);
        initial = initial with {
            CardObjects = initial.CardObjects.Where(p => !removed.Contains(p.Key)).ToDictionary(p => p.Key, p => p.Value),
            ObjectLocations = initial.ObjectLocations.Where(p => !removed.Contains(p.Key)).ToDictionary(p => p.Key, p => p.Value),
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
                [player] = initial.PlayerZones[player] with { Hand = initial.PlayerZones[player].Hand.Take(handCount).ToArray() } } };
        var evidence = Environment.GetEnvironmentVariable("RIFTBOUND_THREE_FIELD_EVIDENCE");
        var driver = new StandbyDefendObserverTests.Driver(initial,
            evidence is null ? null : Path.Combine(evidence, $"zaun-short-hand-{player}-{handCount}"));
        try
        {
            await driver.Act(player, new MoveUnitCommand("UNIT", "BASE", "BATTLEFIELD:BF", []));
            await driver.Act(player, new PassFocusCommand());
            var conquered = await driver.Act(ThreeBattlefieldTimingProbe.Other(player), new PassFocusCommand());
            Assert.Contains(conquered.Events, e => e.Kind == "BATTLEFIELD_CONQUERED");
            Assert.Null(driver.State.PendingHandChoice);
            Assert.DoesNotContain(conquered.Events, e => e.Kind is "CARD_DISCARDED" or "CARD_DRAWN");
            Assert.Single(driver.State.StackItems, i => i.EffectKind == TriggerKinds.BattlefieldConquerDiscardDraw);
            var resolved = await driver.Top();
            Assert.Null(driver.State.PendingHandChoice);
            Assert.Equal(handCount, resolved.Events.Count(e => e.Kind == "CARD_DISCARDED"));
            Assert.DoesNotContain(resolved.Events, e => e.Kind == "HAND_CHOICE_REQUESTED");
            Assert.Single(resolved.Events, e => e.Kind == "CARD_DRAWN" && Equals(e.Payload.GetValueOrDefault("count"), 1));
            Assert.Equal(new[] { "DRAW1" }, driver.State.PlayerZones[player].Hand);
            Assert.Equal(new[] { "DRAW2", "DRAW3" }, driver.State.PlayerZones[player].MainDeck);
            if (handCount == 1) Assert.Contains("H1", driver.State.PlayerZones[player].Graveyard);
            await RideTheWindCurrentMovementTests.Replay(initial, driver);
        }
        finally { driver.Export("summary.json", new { initial, state = driver.State, commands = driver.Commands(), events = driver.Events() }); }
    }
}
