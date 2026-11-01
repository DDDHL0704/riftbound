using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class SourceBoonMightSourceContinuityTests
{
    private const string Ability = "SOURCE-BOON-MIGHT:0";

    private static MatchState Position(bool token, bool attached, bool lethal = false)
    {
        var state = ArmamentMightTests.Position(unit: "OGN·164/298", power: 5,
            gear: attached ? "SFD·059/221" : "SFD·022/221", attached: attached);
        var source = state.CardObjects["HOST"] with {
            Tags = [..state.CardObjects["HOST"].Tags, CardObjectTags.Boon], Damage = lethal ? 4 : 0 };
        if (token) source = source with {
            TokenFactoryCardNo = P6TokenFactoryCatalog.ImageTokenCardNo,
            CopyFaceTransitions = [new(0, source.ObjectGeneration, P6TokenFactoryCatalog.ImageTokenCardNo, source.CardNo!)] };
        var cards = new Dictionary<string, CardObjectState>(state.CardObjects) { ["HOST"] = source };
        if (token && attached) cards["GEAR"] = cards["GEAR"] with {
            AttachedRuleText = cards["GEAR"].AttachedRuleText! with { FaceRevision = 1 } };
        state = state with { CardObjects = cards };
        return AttackDamageDistributionTests.Spell(state, "RETURN", "OGN·104/298", "P1");
    }

    private static ActivateAbilityCommand Activate(bool attached)
        => new("HOST", Ability, []) { AbilityInstanceId = attached ? "ATTACHED:GEAR:0:1:" + Ability : null };

    private static void Invalid(MatchState state, StackItemState item, SourceBoonMightContext context)
        => Assert.Contains("invalid source boon ability", OfficialInsightAndSpellLockTests.Errors(state with {
            StackItems = [item with { SourceBoonMight = context }] }));

    private static void InvalidReceipts(MatchState state, StackItemState item, bool lethal)
    {
        var context = item.SourceBoonMight!;
        var receipt = Assert.IsType<CeasedSourceBoonMight>(context.CeasedSource);
        foreach (var forged in new[] {
            receipt with { Source = receipt.Source with { Generation = receipt.Source.Generation + 1 } },
            receipt with { CardNo = "OGN·001/298" },
            receipt with { FaceRevision = receipt.FaceRevision + 1 },
            receipt with { AcquiredTick = receipt.AcquiredTick + 1 },
            receipt with { TokenFactoryCardNo = P6TokenFactoryCatalog.BombTokenCardNo },
            receipt with { DepartedTick = receipt.AcquiredTick - 1 },
            receipt with { DepartedTick = state.Tick + 1 } })
            Invalid(state, item, context with { CeasedSource = forged });
        Invalid(state, item, context with { CeasedSource = null });
        Invalid(state, item, context with { BoonCost = null });
        Invalid(state, item, context with { BoonCost = context.BoonCost! with { Source = new("FORGED-COST", 0) } });
        if (lethal)
        {
            Assert.Equal(context.AcquiredTick, receipt.DepartedTick);
            Invalid(state, item, context with { BoonCost = context.BoonCost! with { TokenCeasedAtTick = null } });
            Invalid(state, item, context with { BoonCost = context.BoonCost! with { TokenCeasedAtTick = context.AcquiredTick + 1 } });
        }
        else Invalid(state, item, context with { CeasedSource = receipt with { DepartedTick = receipt.AcquiredTick } });
    }

    [Theory]
    [InlineData(false, false)] [InlineData(false, true)]
    [InlineData(true, false)] [InlineData(true, true)]
    public async Task RealReturnResponsePreservesTheSkillAndThePaidBoon(bool token, bool attached)
    {
        var game = new Driver(Position(token, attached));
        await game.Act("P1", Activate(attached));
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["HOST"]));
        await game.Top();
        var item = Assert.Single(game.State.StackItems);
        if (token)
        {
            Assert.False(game.State.CardObjects.ContainsKey("HOST"));
            InvalidReceipts(game.State, item, lethal: false);
        }
        else
        {
            Assert.Contains("HOST", game.State.PlayerZones["P1"].Hand);
            Assert.DoesNotContain(CardObjectTags.Boon, game.State.CardObjects["HOST"].Tags);
        }
        var spectator = MatchReplayRedactor.BuildSpectatorFrame(game.State.RoomId, game.State.Tick, 0, [], game.State);
        foreach (var snapshot in new[] { ResolutionResult.BuildSnapshots(game.State)["P2"], spectator.SpectatorSnapshot })
        {
            var shown = Assert.IsType<Dictionary<string, object?>>(Assert.Single(snapshot.Stack));
            Assert.Equal(token ? "HOST" : "HIDDEN", shown["sourceObjectId"]);
        }
        var resources = MatchStateHasher.HashValue(game.State.RunePools);
        var source = token ? null : MatchStateHasher.HashValue(game.State.CardObjects["HOST"]);
        var firstEvent = game.Events().Length;
        await game.Top();
        Assert.Empty(game.State.StackItems);
        Assert.Equal(resources, MatchStateHasher.HashValue(game.State.RunePools));
        Assert.DoesNotContain(game.Events().Skip(firstEvent), entry => entry.Event.Kind == "POWER_MODIFIED_UNTIL_END_OF_TURN");
        if (!token) Assert.Equal(source, MatchStateHasher.HashValue(game.State.CardObjects["HOST"]));
        await game.Replay();
    }

    [Theory]
    [InlineData(false, false)] [InlineData(false, true)]
    [InlineData(true, false)] [InlineData(true, true)]
    public async Task TheSkillSurvivesLethalDamageWhenItsBoonIsPaid(bool token, bool attached)
    {
        var game = new Driver(Position(token, attached, lethal: true));
        await game.Act("P1", Activate(attached));
        var item = Assert.Single(game.State.StackItems);
        Assert.Contains(game.Events(), entry => entry.Event.Kind == "UNIT_DESTROYED");
        if (token)
        {
            Assert.False(game.State.CardObjects.ContainsKey("HOST"));
            InvalidReceipts(game.State, item, lethal: true);
        }
        else Assert.Contains("HOST", game.State.PlayerZones["P1"].Graveyard);
        var firstEvent = game.Events().Length;
        await game.Top();
        Assert.Empty(game.State.StackItems);
        Assert.DoesNotContain(game.Events().Skip(firstEvent), entry => entry.Event.Kind == "POWER_MODIFIED_UNTIL_END_OF_TURN");
        await game.Replay();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task RecoveryRejectsMissingSourcesWithoutADeparture(bool attached)
    {
        var game = new Driver(Position(token: true, attached: attached));
        await game.Act("P1", Activate(attached));
        var item = Assert.Single(game.State.StackItems);
        var forged = game.State with {
            CardObjects = game.State.CardObjects.Where(entry => entry.Key != "HOST").ToDictionary(),
            ObjectLocations = game.State.ObjectLocations.Where(entry => entry.Key != "HOST").ToDictionary(),
            PlayerZones = new Dictionary<string, PlayerZones>(game.State.PlayerZones) {
                ["P1"] = game.State.PlayerZones["P1"] with {
                    Base = game.State.PlayerZones["P1"].Base.Where(id => id != "HOST").ToArray() } } };
        Invalid(forged, item, item.SourceBoonMight!);
    }

    [Theory]
    [InlineData("revision")] [InlineData("late-origin")]
    public async Task RecoveryRejectsCopiedTextBindingsThatDidNotExistAtActivation(string mutation)
    {
        var game = new Driver(Position(token: false, attached: true));
        await game.Act("P1", Activate(attached: true));
        var item = Assert.Single(game.State.StackItems);
        var context = item.SourceBoonMight!;
        var forged = mutation == "revision"
            ? context with { FaceRevision = context.FaceRevision + 1 }
            : context with { Origin = context.Origin! with { CapturedTick = context.AcquiredTick + 1 } };
        Invalid(game.State with { Tick = game.State.Tick + 1 }, item, forged);
    }
}
