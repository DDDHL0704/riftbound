using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class ExperienceHereRoamTests
{
    private const string Card = "UNL-126/219";
    private static P4ActivatedAbilityDefinition Definition() => Assert.Single(P4ActivatedAbilityCatalog.GetAll(),
        ability => ability.SourceCardNo == Card && ability.Kind == ActivatedAbilityKinds.GrantFriendlyHereRoam);
    private static ActivateAbilityCommand Command(string? instance = null)
        => new("HOST", Definition().AbilityId, []) { AbilityInstanceId = instance };
    private static bool Roam(MatchState state, string id) => state.CardObjects[id].UntilEndOfTurnEffects.Contains("ROAM");

    private static MatchState Position(bool field = false, int experience = 3)
    {
        var state = PrintedExhaustAbilityTests.Position(Card);
        state = state with {
            PlayerExperience = new Dictionary<string, int>(state.PlayerExperience) { ["P1"] = experience },
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["HOST"] = state.CardObjects["HOST"] with { IsExhausted = true },
                ["ALLY"] = state.CardObjects["ALLY"] with { UntilEndOfTurnEffects = ["COMBAT_KEYWORD:法盾2:1"] },
                ["FAR"] = OfficialGarenConquestBattlefieldCountTests.Printed("FAR", "OGN·036/298", "P1") } };
        state = LocalDestructionRecallTests.At(state, "FAR", "P1", "BASE");
        if (field) {
            state = LocalDestructionRecallTests.At(state, "HOST", "P1", "BATTLEFIELD", "F");
            state = LocalDestructionRecallTests.At(state, "ALLY", "P1", "BATTLEFIELD", "F");
        }
        return state;
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ThreeExperienceGrantsRoamToCurrentFriendlyUnitsHereWithoutExhaustingOrTargeting(bool field)
    {
        var initial = Position(field); var pool = initial.RunePools["P1"];
        var game = new Driver(initial); await game.Act("P1", Command());
        Assert.Equal(0, game.State.PlayerExperience["P1"]); Assert.Equal(pool, game.State.RunePools["P1"]);
        Assert.True(game.State.CardObjects["HOST"].IsExhausted);
        Assert.False(Roam(game.State, "HOST")); Assert.False(Roam(game.State, "ALLY"));
        var skill = Assert.Single(game.State.StackItems);
        Assert.Empty(skill.TargetObjectIds); Assert.Null(skill.TargetGenerations);
        Assert.Null(game.State.PendingPayment);
        await game.Top();
        Assert.True(Roam(game.State, "HOST")); Assert.True(Roam(game.State, "ALLY"));
        Assert.Equal(!field, Roam(game.State, "FAR"));
        Assert.False(Roam(game.State, "GEAR")); Assert.False(Roam(game.State, "ENEMY"));
        Assert.Equal(pool, game.State.RunePools["P1"]);
        Assert.DoesNotContain(game.Events(), entry => entry.Event.Kind == "UNIT_EXHAUSTED"
            && Equals(entry.Event.Payload.GetValueOrDefault("sourceObjectId"), "HOST"));
        await game.Replay();
    }

    [Fact]
    public async Task AnActuallyPlayedDormantUnitCanPayThePrintedExperienceCost()
    {
        var state = Position();
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["HOST"] = state.CardObjects["HOST"] with { IsExhausted = false } } };
        state = CurrentControllerDamageBonusTests.Place(state, "HOST", "P1", "HAND");
        var game = new Driver(state); await game.Act("P1", new PlayCardCommand("HOST", Card, []));
        Assert.Equal(14, game.State.RunePools["P1"].Mana);
        Assert.True(game.State.CardObjects["HOST"].IsExhausted);
        await game.Act("P1", Command()); await game.Top();
        Assert.Equal(0, game.State.PlayerExperience["P1"]); Assert.True(Roam(game.State, "HOST"));
        await game.Replay();
    }

    [Fact]
    public void ThePromptExposesThreeExperienceWithNoTargetsAndNoExhaustCost()
    {
        var ability = Definition(); Assert.Equal(3, ability.ExperienceCost);
        Assert.False(ability.ExhaustsSourceAsCost); Assert.Equal(0, ability.RequiredTargetCount);
        var rows = (ResolutionResult.BuildPrompts(Position())["P1"].Candidates ?? [])
            .Where(candidate => candidate.Action == CommandTypes.ActivateAbility && candidate.Enabled)
            .SelectMany(candidate => candidate.Metadata?.GetValueOrDefault("sourceRequirements")
                is IEnumerable<IReadOnlyDictionary<string, object?>> requirements ? requirements : []);
        var row = Assert.Single(rows, row => Equals(row.GetValueOrDefault("sourceObjectId"), "HOST")
            && Equals(row.GetValueOrDefault("abilityId"), ability.AbilityId));
        Assert.Equal(3, row["experienceCost"]);
    }

    [Theory]
    [InlineData("experience")] [InlineData("target")]
    [InlineData("hand")] [InlineData("opponent")]
    public async Task InsufficientExperienceExtraTargetsAndIllegalSourcesRejectAtomically(string branch)
    {
        var state = Position(experience: branch == "experience" ? 2 : 3);
        if (branch == "hand") state = CurrentControllerDamageBonusTests.Place(state, "HOST", "P1", "HAND");
        var command = branch == "target" ? Command() with { TargetObjectIds = ["ALLY"] } : Command();
        var before = MatchStateHasher.Hash(state);
        var result = await new CoreRuleEngine().ResolveAsync(state,
            new("invalid-here-roam", branch == "opponent" ? "P2" : "P1", command.CmdType), command, default);
        Assert.False(result.Accepted); Assert.Empty(result.Events); Assert.Equal(before, MatchStateHasher.Hash(result.State));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ResponsesUseTheSourcesCurrentHereAndCannotReadTheDepartedGeneration(bool returnToHand)
    {
        var state = AttackDamageDistributionTests.Spell(Position(field: true), "RESPONSE",
            returnToHand ? "OGN·104/298" : "OGS·011/024", "P1");
        var game = new Driver(state); await game.Act("P1", Command());
        await game.Act("P1", new PlayCardCommand("RESPONSE", returnToHand ? "OGN·104/298" : "OGS·011/024", ["HOST"]));
        await game.Top(); await game.Top();
        Assert.Equal(0, game.State.PlayerExperience["P1"]);
        Assert.False(Roam(game.State, "ALLY"));
        Assert.Equal(!returnToHand, Roam(game.State, "FAR"));
        Assert.Equal(!returnToHand, Roam(game.State, "HOST"));
        Assert.Contains("HOST", returnToHand ? game.State.PlayerZones["P1"].Hand : game.State.PlayerZones["P1"].Base);
        await game.Replay();
    }

    [Fact]
    public async Task TurnCleanupRemovesTheGrantAndPreservesAnIndependentPrintedRoamKeyword()
    {
        var initial = CurrentControllerDamageBonusTests.Place(Position(), "FAR", "P1", "HAND");
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("FAR", "OGN·036/298", []));
        Assert.Contains("游走", game.State.CardObjects["FAR"].Tags);
        await game.Act("P1", Command()); await game.Top();
        Assert.True(Roam(game.State, "FAR"));
        await game.Act("P1", new EndTurnCommand());
        Assert.All(new[] { "HOST", "ALLY", "FAR" }, id => Assert.False(Roam(game.State, id)));
        Assert.Contains("游走", game.State.CardObjects["FAR"].Tags);
        await game.Replay();
    }

    [Fact]
    public async Task ARealSongCopyPaysExperienceAndKeepsItsQueuedSkillAfterDetachment()
    {
        var state = Position(experience: 6);
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["GEAR"] = OfficialGarenConquestBattlefieldCountTests.Printed("GEAR", "SFD·059/221", "P1") } };
        state = AttackDamageDistributionTests.Spell(state, "DETACH", "SFD·011/221", "P1");
        var game = new Driver(state);
        await game.Act("P1", new AssembleEquipmentCommand("GEAR", "HOST", ["ASSEMBLE_GREEN"])); await game.Top();
        var rows = (game.PromptFor("P1").Candidates ?? []).Where(candidate => candidate.Action == CommandTypes.ActivateAbility)
            .SelectMany(candidate => candidate.Metadata?.GetValueOrDefault("sourceRequirements")
                is IEnumerable<IReadOnlyDictionary<string, object?>> requirements ? requirements : []);
        var copied = Assert.Single(rows, row => Equals(row.GetValueOrDefault("sourceObjectId"), "HOST")
            && Equals(row.GetValueOrDefault("abilityId"), Definition().AbilityId) && row.GetValueOrDefault("abilityInstanceId") is not null);
        await game.Act("P1", Command(Assert.IsType<string>(copied["abilityInstanceId"])));
        Assert.Equal(3, game.State.PlayerExperience["P1"]);
        Assert.NotNull(Assert.Single(game.State.StackItems).SimpleActivation!.Origin);
        await game.Act("P1", new PlayCardCommand("DETACH", "SFD·011/221", ["HOST", "GEAR"])); await game.Top();
        Assert.Null(game.State.CardObjects["GEAR"].AttachedToObjectId);
        await game.Top(); Assert.True(Roam(game.State, "HOST")); Assert.True(Roam(game.State, "ALLY"));
        await game.Replay();
    }
}
