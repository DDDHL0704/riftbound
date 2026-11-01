using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class SelfAbilitySourceContinuityTests
{
    private static MatchState Position(bool recycle, bool token)
    {
        var state = ArmamentMightTests.Position(unit: recycle ? "VEN·167" : "VEN·092", power: recycle ? 3 : 4);
        var cards = state.CardObjects.ToDictionary();
        if (token)
            cards["HOST"] = cards["HOST"] with {
                TokenFactoryCardNo = P6TokenFactoryCatalog.ImageTokenCardNo,
                CopyFaceTransitions = [new(0, cards["HOST"].ObjectGeneration,
                    P6TokenFactoryCatalog.ImageTokenCardNo, cards["HOST"].CardNo!)] };
        cards["COST"] = new("COST", cardNo: "OGN·048/298", ownerId: "P1", controllerId: "P1", tags: [CardObjectTags.SpellCard]);
        state = state with { CardObjects = cards,
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["COST"] = new("P1", "GRAVEYARD") },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P1"] = state.PlayerZones["P1"] with { Graveyard = [..state.PlayerZones["P1"].Graveyard, "COST"] } } };
        return AttackDamageDistributionTests.Spell(state, "RETURN", "OGN·104/298", "P1");
    }

    private static ActivateAbilityCommand Activate(bool recycle)
        => new("HOST", recycle ? "RECYCLE-MIGHT:0" : "SELF-MIGHT:0", [],
            recycle ? ["RECYCLE_GRAVEYARD_CARD:COST"] : []);

    private static void AssertInvalid(MatchState state, StackItemState item)
        => Assert.Contains(OfficialInsightAndSpellLockTests.Errors(state with { StackItems = [item] }),
            error => error.Contains(item.SelfMightActivation is not null
                ? "invalid captured self Might activation" : "invalid captured recycle-cost ability", StringComparison.Ordinal));

    [Theory]
    [InlineData(false, false)] [InlineData(false, true)]
    [InlineData(true, false)] [InlineData(true, true)]
    public async Task ActualReturnResponseCompletesThePaidSkillWithoutBuffingTheDepartedSource(bool recycle, bool token)
    {
        var initial = Position(recycle, token);
        var game = new Driver(initial);
        await game.Act("P1", Activate(recycle));
        if (recycle)
            Assert.DoesNotContain("COST", game.State.PlayerZones["P1"].Graveyard);
        else
            Assert.Equal(initial.RunePools["P1"].Mana - 1, game.State.RunePools["P1"].Mana);
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["HOST"]));
        await game.Top();
        var item = Assert.Single(game.State.StackItems);
        if (token)
        {
            Assert.False(game.State.CardObjects.ContainsKey("HOST"));
            Assert.Contains(game.Events(), entry => entry.Event.Kind == "TOKEN_CEASED_TO_EXIST"
                && Equals(entry.Event.Payload.GetValueOrDefault("targetObjectId"), "HOST"));
            var ceased = Assert.IsType<CeasedSelfAbilitySource>(
                item.SelfMightActivation?.CeasedSource ?? item.RecycleMightActivation?.CeasedSource);
            foreach (var forged in new[] {
                ceased with { Source = ceased.Source with { Generation = ceased.Source.Generation + 1 } },
                ceased with { CardNo = "OGN·001/298" },
                ceased with { FaceRevision = ceased.FaceRevision + 1 },
                ceased with { AcquiredTick = ceased.AcquiredTick + 1 },
                ceased with { TokenFactoryCardNo = P6TokenFactoryCatalog.BombTokenCardNo },
                ceased with { DepartedTick = ceased.AcquiredTick },
                ceased with { DepartedTick = game.State.Tick + 1 } })
                AssertInvalid(game.State, recycle
                    ? item with { RecycleMightActivation = item.RecycleMightActivation! with { CeasedSource = forged } }
                    : item with { SelfMightActivation = item.SelfMightActivation! with { CeasedSource = forged } });
            AssertInvalid(game.State, recycle
                ? item with { RecycleMightActivation = item.RecycleMightActivation! with { CeasedSource = null } }
                : item with { SelfMightActivation = item.SelfMightActivation! with { CeasedSource = null } });
        }
        else
        {
            Assert.Contains("HOST", game.State.PlayerZones["P1"].Hand);
        }
        var spectator = MatchReplayRedactor.BuildSpectatorFrame(game.State.RoomId, game.State.Tick, 0, [], game.State);
        foreach (var snapshot in new[] { ResolutionResult.BuildSnapshots(game.State)["P2"], spectator.SpectatorSnapshot })
        {
            var shown = Assert.IsType<Dictionary<string, object?>>(Assert.Single(snapshot.Stack));
            Assert.Equal(token ? "HOST" : "HIDDEN", shown["sourceObjectId"]);
        }
        var manaAfterResponse = game.State.RunePools["P1"].Mana;
        var powerAfterResponse = token ? (int?)null : game.State.CardObjects["HOST"].Power;
        var firstEvent = game.Events().Length;
        await game.Top();
        Assert.Empty(game.State.StackItems);
        Assert.Equal(manaAfterResponse, game.State.RunePools["P1"].Mana);
        Assert.DoesNotContain(game.Events().Skip(firstEvent), entry => entry.Event.Kind == "POWER_MODIFIED_UNTIL_END_OF_TURN");
        if (recycle) Assert.Contains("COST", game.State.PlayerZones["P1"].MainDeck);
        if (!token) Assert.Equal(powerAfterResponse, game.State.CardObjects["HOST"].Power);
        await game.Replay();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task RecoveryRejectsAMissingTokenSourceWithoutACapturedDeparture(bool recycle)
    {
        var game = new Driver(Position(recycle, token: true));
        await game.Act("P1", Activate(recycle));
        var cards = game.State.CardObjects.Where(entry => entry.Key != "HOST").ToDictionary();
        var forged = game.State with { CardObjects = cards,
            ObjectLocations = game.State.ObjectLocations.Where(entry => entry.Key != "HOST").ToDictionary(),
            PlayerZones = new Dictionary<string, PlayerZones>(game.State.PlayerZones) {
                ["P1"] = game.State.PlayerZones["P1"] with {
                    Base = game.State.PlayerZones["P1"].Base.Where(id => id != "HOST").ToArray() } } };
        AssertInvalid(forged, Assert.Single(game.State.StackItems));
    }
}
