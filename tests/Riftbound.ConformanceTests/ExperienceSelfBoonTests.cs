using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class ExperienceSelfBoonTests
{
    private static P4ActivatedAbilityDefinition Definition(string cardNo)
        => Assert.Single(P4ActivatedAbilityCatalog.GetAll(), ability => ability.SourceCardNo == cardNo
            && ability.Kind == ActivatedAbilityKinds.GrantSelfBoon);

    private static MatchState Position(string cardNo, int experience = 4)
    {
        var state = PrintedExhaustAbilityTests.Position(cardNo);
        return state with {
            PlayerExperience = new Dictionary<string, int>(state.PlayerExperience) { ["P1"] = experience },
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["HOST"] = state.CardObjects["HOST"] with { IsExhausted = true } } };
    }

    private static MatchState InHand(MatchState state)
    {
        var zones = state.PlayerZones["P1"];
        return state with {
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P1"] = zones with { Base = zones.Base.Where(id => id != "HOST").ToArray(),
                    Hand = [..zones.Hand, "HOST"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) {
                ["HOST"] = new("P1", "HAND") },
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["HOST"] = state.CardObjects["HOST"] with { IsExhausted = false } } };
    }

    [Theory]
    [InlineData("UNL-102/219")]
    [InlineData("UNL-162/219")]
    public async Task AnExhaustedSourcePaysTwoExperienceBeforeEachBoonAndOrdinaryBoonsDoNotStack(string cardNo)
    {
        var ability = Definition(cardNo);
        Assert.Equal(2, ability.ExperienceCost);
        Assert.False(ability.ExhaustsSourceAsCost);
        var game = new Driver(InHand(Position(cardNo)));
        await game.Act("P1", new PlayCardCommand("HOST", cardNo, []));
        Assert.Contains("HOST", game.State.PlayerZones["P1"].Base);
        Assert.True(game.State.CardObjects["HOST"].IsExhausted);
        var power = game.State.CardObjects["HOST"].Power;
        for (var i = 0; i < 2; i++)
        {
            await game.Act("P1", new ActivateAbilityCommand("HOST", ability.AbilityId, []));
            Assert.Equal(2 - i * 2, game.State.PlayerExperience["P1"]);
            Assert.Equal(i == 0 ? 0 : 1, game.State.CardObjects["HOST"].BoonCount);
            Assert.True(game.State.CardObjects["HOST"].IsExhausted);
            await game.Top();
            Assert.Equal(1, game.State.CardObjects["HOST"].BoonCount);
            Assert.Equal(power + 1, game.State.CardObjects["HOST"].Power);
        }
        Assert.DoesNotContain(game.Events(), entry => entry.Event.Kind == "UNIT_EXHAUSTED"
            && Equals(entry.Event.Payload.GetValueOrDefault("sourceObjectId"), "HOST"));
        await game.Replay();
    }

    [Theory]
    [InlineData("UNL-102/219")]
    [InlineData("UNL-162/219")]
    public async Task ThePromptShowsTwoExperienceAndOnlyOffersPayableActivations(string cardNo)
    {
        var ability = Definition(cardNo);
        var state = Position(cardNo, 2);
        var candidates = ResolutionResult.BuildPrompts(state)["P1"].Candidates ?? [];
        var requirement = Assert.Single(candidates.Where(candidate => candidate.Action == CommandTypes.ActivateAbility && candidate.Enabled)
            .SelectMany(candidate => candidate.Metadata?.GetValueOrDefault("sourceRequirements")
                is IEnumerable<IReadOnlyDictionary<string, object?>> rows ? rows : []),
            row => Equals(row.GetValueOrDefault("sourceObjectId"), "HOST")
                && Equals(row.GetValueOrDefault("abilityId"), ability.AbilityId));
        Assert.Equal(2, requirement["experienceCost"]);
        foreach (var experience in new[] { 0, 1 })
        {
            var shortState = Position(cardNo, experience);
            Assert.DoesNotContain(ResolutionResult.BuildPrompts(shortState)["P1"].Candidates ?? [],
                candidate => candidate.Action == CommandTypes.ActivateAbility && candidate.Enabled
                    && (candidate.Sources ?? []).Any(source => source.Id == "HOST"));
            await Reject(shortState, new ActivateAbilityCommand("HOST", ability.AbilityId, []));
        }
    }

    [Theory]
    [InlineData("UNL-102/219")]
    [InlineData("UNL-162/219")]
    public async Task InsufficientExperienceAndUnexpectedTargetsCannotChangeState(string cardNo)
    {
        var id = Definition(cardNo).AbilityId;
        await Reject(Position(cardNo, 1), new ActivateAbilityCommand("HOST", id, []));
        await Reject(Position(cardNo), new ActivateAbilityCommand("HOST", id, ["ALLY"]));
        await Reject(InHand(Position(cardNo)), new ActivateAbilityCommand("HOST", id, []));
    }

    [Theory]
    [InlineData("UNL-102/219")]
    [InlineData("UNL-162/219")]
    public async Task AClosedResponseWindowDoesNotPermitOrdinaryExperienceActivation(string cardNo)
    {
        var game = new Driver(AttackDamageDistributionTests.Spell(Position(cardNo), "OLDER", "OGN·048/298", "P1"));
        await game.Act("P1", new PlayCardCommand("OLDER", "OGN·048/298", []));
        await Reject(game.State, new ActivateAbilityCommand("HOST", Definition(cardNo).AbilityId, []));
        await game.Replay();
    }

    [Theory]
    [InlineData("UNL-102/219")]
    [InlineData("UNL-162/219")]
    public async Task ReturningTheSourceInResponseKeepsTheExperienceSpentAndPreventsAnOffFieldBoon(string cardNo)
    {
        var game = new Driver(AttackDamageDistributionTests.Spell(Position(cardNo), "LEAVE", "OGN·104/298", "P1"));
        await game.Act("P1", new ActivateAbilityCommand("HOST", Definition(cardNo).AbilityId, []));
        await game.Act("P1", new PlayCardCommand("LEAVE", "OGN·104/298", ["HOST"]));
        await game.Top();
        Assert.Contains("HOST", game.State.PlayerZones["P1"].Hand);
        await game.Top();
        Assert.Equal(2, game.State.PlayerExperience["P1"]);
        Assert.Equal(0, game.State.CardObjects["HOST"].BoonCount);
        Assert.DoesNotContain(CardObjectTags.Boon, game.State.CardObjects["HOST"].Tags);
        await game.Replay();
    }

    private static async Task Reject(MatchState initial, ActivateAbilityCommand command)
    {
        var hash = MatchStateHasher.Hash(initial);
        var result = await new CoreRuleEngine().ResolveAsync(initial, new("bad-experience-skill", "P1", command.CmdType), command, default);
        Assert.False(result.Accepted);
        Assert.Empty(result.Events);
        Assert.Equal(hash, MatchStateHasher.Hash(result.State));
    }
}
