using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;
using static Riftbound.ConformanceTests.RadGearTestPosition;

namespace Riftbound.ConformanceTests;

// Explicit established public positions; destruction, confirmation, responses,
// target choices and effect plays use accepted commands and original replay.
public sealed class LastBreathLocalRepetitionTests
{
    [Theory]
    [InlineData(1)] [InlineData(2)]
    public async Task EachAreaSkillDamagesOnlyTheCapturedBattlefield(int amplifiers)
    {
        var game = await Destroy("OGN·190/298", amplifiers);
        var births = Births(game.State);
        Assert.Equal(amplifiers + 1, births.Length);
        Assert.Equal(amplifiers + 1, births.Select(c => c.Repetition!.Occurrence).Distinct().Count());
        Assert.All(births, c => Assert.Equal("BF", c.BattlefieldObjectId));
        await Order(game);
        for (var i = 0; i <= amplifiers; i++)
        {
            var prefix = game.Events().Length;
            await game.Top();
            Assert.Equal(4 * (i + 1), game.State.CardObjects["ALLY"].Damage);
            Assert.Equal(4 * (i + 1), game.State.CardObjects["VICTIM"].Damage);
            Assert.Equal(0, game.State.CardObjects["ENEMY"].Damage);
            Assert.Single(game.Events().Skip(prefix), e => e.Event.Kind == "TRIGGER_RESOLVED");
        }
        Assert.Empty(game.State.StackItems); await game.Replay();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task EachOptionalDestructionChoosesIndependentlyAndRechecksItsTarget(bool sameTarget)
    {
        var game = await Destroy("RAD·126/167", 1);
        Assert.Equal(2, Births(game.State).Length); await Order(game);
        foreach (var target in new[] { "ALLY", sameTarget ? "ALLY" : "VICTIM" })
        {
            var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
            Assert.Contains(target, choice.LegalObjectIds);
            await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [target]));
        }
        Assert.Equal(2, game.State.StackItems.Count);
        await game.Top(); await game.Top();
        Assert.Contains("ALLY", game.State.PlayerZones["P1"].Graveyard);
        Assert.Equal(!sameTarget, game.State.PlayerZones["P1"].Graveyard.Contains("VICTIM"));
        Assert.Single(game.Events(), e => e.Event.Kind == "UNIT_DESTROYED" && Equals(e.Event.Payload.GetValueOrDefault("targetObjectId"), "ALLY"));
        Assert.Empty(game.State.StackItems); await game.Replay();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task EachDeckTopSkillBanishesItsOwnCardAndOffersItsOwnOptionalPlay(bool play)
    {
        var game = await Destroy("RAD·067/167", 1);
        var top = game.State.PlayerZones["P1"].MainDeck.Take(2).ToArray();
        Assert.Equal(2, Births(game.State).Length); await Order(game);
        var occurrences = new HashSet<int>();
        for (var i = 0; i < 2; i++)
        {
            await game.Top();
            var pending = Assert.IsType<PendingEffectPlayState>(game.State.PendingEffectPlay);
            Assert.Equal(new[] { top[i] }, pending.Sources.Keys);
            Assert.True(occurrences.Add(pending.Parent.LastBreathBattlefieldDamage!.Repetition!.Occurrence));
            var mana = game.State.RunePools["P1"].Mana;
            if (play) await game.Act("P1", new PlayCardCommand(top[i], "OGN·219/298", [], Destination: "BASE"));
            else await game.Act("P1", new ChooseCardsCommand(pending.ChoiceId, "EFFECT_PLAY", []));
            Assert.Null(game.State.PendingEffectPlay);
            Assert.Equal(mana, game.State.RunePools["P1"].Mana);
            Assert.Equal(!play, game.State.PlayerZones["P1"].Banished.Contains(top[i]));
        }
        Assert.Equal(2, game.Events().Count(e => e.Event.Kind == "CARD_BANISHED"));
        Assert.Empty(game.State.StackItems); await game.Replay();
    }

    [Fact]
    public async Task CapturedLocalSkillsRejectDuplicateOccurrencesAndCoordinatedLocationForgery()
    {
        var game = await Destroy("OGN·190/298", 1);
        var item = game.State.TriggerQueue[0]; var birth = item.LastBreathBattlefieldDamage!;
        var duplicate = game.State with { TriggerQueue = [..game.State.TriggerQueue, item] };
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(duplicate), e => e.Contains("duplicate captured last-breath battlefield damage", StringComparison.Ordinal));
        var forged = game.State with { TriggerQueue = game.State.TriggerQueue.Select(t => t.TriggerId == item.TriggerId
            ? t with { TriggerId = t.TriggerId.Replace("BF", "OTHER", StringComparison.Ordinal),
                LastBreathBattlefieldDamage = birth with { BattlefieldObjectId = "OTHER" } } : t).ToArray() };
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(forged), e => e.Contains("captured last-breath battlefield damage", StringComparison.Ordinal));
        var outOfRange = game.State with { TriggerQueue = game.State.TriggerQueue.Select(t => t.TriggerId == item.TriggerId
            ? t with { LastBreathBattlefieldDamage = birth with { Repetition = birth.Repetition! with { Occurrence = 2 } } } : t).ToArray() };
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(outOfRange), e => e.Contains("captured last-breath battlefield damage", StringComparison.Ordinal));
        await Order(game); await game.Top(); await game.Top(); await game.Replay();
    }

    private static async Task<Driver> Destroy(string no, int amplifiers)
    {
        var initial = Put(Put(Create(), "UNIT", no, "P1", "BATTLEFIELD", "BF"), "KILL", "OGN·229/298", "P1", "HAND");
        initial = RadUnitEffectTestFixture.Place(initial, "ALLY", "P1", "BATTLEFIELD", "BF");
        initial = Put(initial, "VICTIM", "OGN·219/298", "P1", "BATTLEFIELD", "BF");
        initial = initial with { CardObjects = initial.CardObjects.ToDictionary(p => p.Key,
            p => p.Key is "ALLY" or "VICTIM" ? p.Value with { Power = 20 } : p.Value) };
        for (var i = 0; i < amplifiers; i++)
            initial = LastBreathRepetitionTests.AddAmplifier(initial, "AMP-" + i, "P1", "BF");
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("KILL", "OGN·229/298", ["UNIT"])); await game.Top();
        return game;
    }
    private static LastBreathBattlefieldDamageContext[] Births(MatchState state) => state.TriggerQueue.Select(t => t.LastBreathBattlefieldDamage)
        .Concat(state.StackItems.Select(t => t.LastBreathBattlefieldDamage)).OfType<LastBreathBattlefieldDamageContext>().ToArray();
    private static Task Order(Driver game) => game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: game.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
}
