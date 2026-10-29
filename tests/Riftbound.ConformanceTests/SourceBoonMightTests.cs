using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class SourceBoonMightTests
{
    private const string Ability = "SOURCE-BOON-MIGHT:0";
    private static MatchState Position(string no = "OGN·164/298", bool copied = false)
    {
        var state = ArmamentMightTests.Position(unit: no, power: 5,
            gear: copied ? "SFD·059/221" : "SFD·022/221", attached: copied);
        return state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["HOST"] = state.CardObjects["HOST"] with { Tags = [..state.CardObjects["HOST"].Tags, CardObjectTags.Boon] } } };
    }
    private static ActivateAbilityCommand Activate() => new("HOST", Ability, []);

    private static IEnumerable<IReadOnlyDictionary<string, object?>> Rows(MatchState state)
        => (ResolutionResult.BuildPrompts(state)["P1"].Candidates ?? []).Where(c => c.Action == CommandTypes.ActivateAbility)
            .SelectMany(c => Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(c.Metadata!["sourceRequirements"]));

    [Theory]
    [InlineData("OGN·164/298")] [InlineData("OGN·164a/298")]
    [InlineData("SFD·232/221")] [InlineData("SFD·232*/221")] [InlineData("VEN·SP4")]
    public async Task PlayingTheBossPaysOrangePowerThenWaitsForResponsesBeforeGrantingTheActivatableBoon(string no)
    {
        var state = VenUnitEntryBasicTests.Position(no) with { RunePools = new Dictionary<string, RunePool> {
            ["P1"] = new(5, 0, new Dictionary<string, int> { [RuneTrait.Orange] = 1 }), ["P2"] = RunePool.Empty } };
        var game = new Driver(state); await game.Act("P1", new PlayCardCommand("UNIT", no, [], Destination: "BASE"));
        Assert.Equal(0, game.State.RunePools["P1"].Mana);
        Assert.Equal(0, game.State.RunePools["P1"].PowerByTrait.GetValueOrDefault(RuneTrait.Orange));
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Base);
        Assert.Equal(4, game.State.CardObjects["UNIT"].Power);
        Assert.DoesNotContain(CardObjectTags.Boon, game.State.CardObjects["UNIT"].Tags);
        Assert.True(Assert.Single(game.State.StackItems).SourceConfirmed);
        await game.Top(); Assert.Equal(5, game.State.CardObjects["UNIT"].Power);
        Assert.Contains(CardObjectTags.Boon, game.State.CardObjects["UNIT"].Tags);
        await game.Act("P1", new ActivateAbilityCommand("UNIT", Ability, []));
        Assert.Equal(4, game.State.CardObjects["UNIT"].Power);
        Assert.DoesNotContain(CardObjectTags.Boon, game.State.CardObjects["UNIT"].Tags);
        await game.Top(); Assert.Equal(8, game.State.CardObjects["UNIT"].Power);
        await game.Act("P1", new EndTurnCommand()); Assert.Equal(4, game.State.CardObjects["UNIT"].Power);
        await game.Replay();
    }

    [Theory]
    [InlineData("OGN·164/298")] [InlineData("OGN·164a/298")]
    [InlineData("SFD·232/221")] [InlineData("SFD·232*/221")] [InlineData("VEN·SP4")]
    public async Task MissingThePrintedOrangePowerRejectsTheWholePlay(string no)
    {
        var state = VenUnitEntryBasicTests.Position(no) with { RunePools = new Dictionary<string, RunePool> {
            ["P1"] = new(5, 0), ["P2"] = RunePool.Empty } };
        await Reject(state, "P1", new PlayCardCommand("UNIT", no, [], Destination: "BASE"));
    }

    [Theory]
    [InlineData("OGN·164/298")] [InlineData("OGN·164a/298")]
    [InlineData("SFD·232/221")] [InlineData("SFD·232*/221")] [InlineData("VEN·SP4")]
    public async Task EveryBossPrintingConsumesItsBoonBeforeResponsesAndGetsFourMightUntilTurnEnd(string no)
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, c => c.CardNo == no);
        Assert.Equal("铁拳", card.SubTitle);
        var ability = Assert.Single(ActivatedAbilityParser.Parse(card.CardEffect), a => a.Kind == ActivatedAbilityKinds.ConsumeSourceBoonGrantSelfMight);
        Assert.Equal(4, ability.PowerModifierAmount); Assert.Equal(0, ability.RequiredTargetCount);
        var state = Position(no); var game = new Driver(state);
        Assert.Single(Rows(state), r => Equals(r.GetValueOrDefault("abilityId"), Ability));
        await game.Act("P1", Activate());
        Assert.Equal(4, ArmamentMightTests.Might(game.State));
        Assert.DoesNotContain(CardObjectTags.Boon, game.State.CardObjects["HOST"].Tags);
        Assert.NotNull(Assert.Single(game.State.StackItems).SourceBoonMight);
        Assert.Equal(state.RunePools["P1"], game.State.RunePools["P1"]);
        Assert.DoesNotContain(Rows(game.State), r => Equals(r.GetValueOrDefault("abilityId"), Ability));
        await game.Act(game.State.PriorityPlayerId!, new PassPriorityCommand()); Assert.Equal(4, ArmamentMightTests.Might(game.State));
        await game.Act(game.State.PriorityPlayerId!, new PassPriorityCommand()); Assert.Equal(8, ArmamentMightTests.Might(game.State));
        Assert.DoesNotContain(CardObjectTags.Boon, game.State.CardObjects["HOST"].Tags);
        await Reject(game.State, "P1", Activate());
        await game.Act("P1", new EndTurnCommand()); Assert.Equal(4, ArmamentMightTests.Might(game.State)); await game.Replay();
    }

    [Theory]
    [InlineData("OGN·164/298")] [InlineData("OGN·164a/298")]
    [InlineData("SFD·232/221")] [InlineData("SFD·232*/221")] [InlineData("VEN·SP4")]
    public async Task ASourceWithoutABoonHasNoCandidateAndCannotActivate(string no)
    {
        var state = Position(no); state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["HOST"] = state.CardObjects["HOST"] with { Power = 4, Tags = state.CardObjects["HOST"].Tags.Where(t => t != CardObjectTags.Boon).ToArray() } } };
        Assert.DoesNotContain(Rows(state), r => Equals(r.GetValueOrDefault("abilityId"), Ability));
        await Reject(state, "P1", Activate());
    }

    [Theory]
    [InlineData("OGN·164/298")] [InlineData("OGN·164a/298")]
    [InlineData("SFD·232/221")] [InlineData("SFD·232*/221")] [InlineData("VEN·SP4")]
    public async Task ExhaustionDoesNotPreventPayingTheBoonCost(string no)
    {
        var state = Position(no); state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["HOST"] = state.CardObjects["HOST"] with { IsExhausted = true } } };
        var game = new Driver(state); await game.Act("P1", Activate()); await game.Top();
        Assert.True(game.State.CardObjects["HOST"].IsExhausted); Assert.Equal(8, ArmamentMightTests.Might(game.State)); await game.Replay();
    }

    [Theory]
    [InlineData(5, 4, true)] [InlineData(1, 0, false)]
    public async Task PayingTheBoonCleansUpLethalDamageBeforeResponsesButAllowsUndamagedZeroMight(int power, int damage, bool dies)
    {
        var state = Position(); state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["HOST"] = state.CardObjects["HOST"] with { Power = power, Damage = damage } } };
        var game = new Driver(state); await game.Act("P1", Activate());
        Assert.Equal(dies, game.State.PlayerZones["P1"].Graveyard.Contains("HOST"));
        Assert.Single(game.State.StackItems, item => item.SourceBoonMight is not null);
        Assert.DoesNotContain(CardObjectTags.Boon, game.State.CardObjects["HOST"].Tags);
        if (dies) Assert.Contains(game.Events(), e => e.Event.Kind == "UNIT_DESTROYED");
        else Assert.Equal(0, ArmamentMightTests.Might(game.State));
        await game.Top();
        if (dies) Assert.Contains("HOST", game.State.PlayerZones["P1"].Graveyard);
        else Assert.Equal(4, ArmamentMightTests.Might(game.State));
        await game.Replay();
    }

    [Theory]
    [InlineData("target")] [InlineData("optional-cost")] [InlineData("wrong-player")]
    [InlineData("wrong-instance")] [InlineData("closed")] [InlineData("off-field")]
    public async Task IllegalSelectionsAndTimingLeaveTheBoonUntouched(string scenario)
    {
        var state = Position(); var command = Activate();
        if (scenario == "target") command = command with { TargetObjectIds = ["HOST"] };
        if (scenario == "optional-cost") command = command with { OptionalCosts = ["CONSUME_BOON:HOST"] };
        if (scenario == "wrong-instance") command = command with { AbilityInstanceId = "ATTACHED:MISSING:0:1:" + Ability };
        if (scenario == "closed") state = state with { TimingState = TimingStates.NeutralClosed };
        if (scenario == "off-field") state = state with {
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P1"] = state.PlayerZones["P1"] with {
                    Base = state.PlayerZones["P1"].Base.Where(id => id != "HOST").ToArray(),
                    Battlefields = state.PlayerZones["P1"].Battlefields.Where(id => id != "HOST").ToArray(),
                    Hand = [..state.PlayerZones["P1"].Hand, "HOST"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["HOST"] = new("P1", "HAND") } };
        await Reject(state, scenario == "wrong-player" ? "P2" : "P1", command);
    }

    [Fact]
    public async Task CopiedTextPaysFromTheHostAndCanBeSelectedByItsInstance()
    {
        var game = new Driver(Position(copied: true));
        await game.Act("P1", Activate() with { AbilityInstanceId = "ATTACHED:GEAR:0:1:" + Ability });
        Assert.NotNull(Assert.Single(game.State.StackItems).SourceBoonMight!.Origin);
        Assert.DoesNotContain(CardObjectTags.Boon, game.State.CardObjects["HOST"].Tags);
        await game.Top(); Assert.Equal(8, ArmamentMightTests.Might(game.State)); await game.Replay();
    }

    [Fact]
    public async Task ReturningTheSourceSkipsItsMightEffectWithoutRefundingOrExposingTheCost()
    {
        var game = new Driver(AttackDamageDistributionTests.Spell(Position(), "RETURN", "OGN·104/298", "P1"));
        await game.Act("P1", Activate());
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["HOST"])); await game.Top();
        Assert.Contains("HOST", game.State.PlayerZones["P1"].Hand);
        Assert.DoesNotContain("HOST", JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(game.State)["P2"].Stack));
        var before = game.Events().Length; await game.Top();
        Assert.DoesNotContain("\"HOST\"", JsonSerializer.Serialize(game.Events().Skip(before)));
        Assert.DoesNotContain(CardObjectTags.Boon, game.State.CardObjects["HOST"].Tags);
        Assert.Equal(4, game.State.CardObjects["HOST"].Power); await game.Replay();
    }

    [Theory]
    [InlineData("source")] [InlineData("index")] [InlineData("card")] [InlineData("instance")] [InlineData("tick")]
    [InlineData("empty-source")] [InlineData("missing-card")] [InlineData("missing-equipment")]
    public async Task RecoveryRejectsForgedActivationBindings(string mutation)
    {
        var game = new Driver(Position()); await game.Act("P1", Activate());
        var item = Assert.Single(game.State.StackItems); var c = item.SourceBoonMight!;
        var changed = mutation switch {
            "source" => c with { Source = new(c.Source.ObjectId, c.Source.Generation + 1) },
            "empty-source" => c with { Source = new("", c.Source.Generation) },
            "missing-card" => c with { CardNo = null! },
            "missing-equipment" => c with { Origin = new(c.CardNo, c.Source, null!, 1, c.AcquiredTick, c.FaceRevision) },
            "index" => c with { AbilityIndex = 99 }, "card" => c with { CardNo = "OGN·001/298" },
            "instance" => c with { RuleTextInstanceId = "FORGED" }, _ => c with { AcquiredTick = game.State.Tick + 1 } };
        Assert.Contains("invalid source boon ability", OfficialInsightAndSpellLockTests.Errors(game.State with {
            StackItems = [item with { SourceBoonMight = changed }] }));
    }

    [Fact]
    public async Task NewOfficialWordingParsesTheSameActivationAsTheOlderPrintings()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Riftbound.slnx"))) root = root.Parent;
        Assert.NotNull(root);
        var catalog = await OfficialCardCatalogLoader.LoadFromFileAsync(Path.Combine(root.FullName, VendettaCardSources.SnapshotPath));
        var card = Assert.Single(catalog.Cards, c => c.CardNo == "VEN·SP4");
        var spec = Assert.Single(ActivatedAbilityParser.Parse(card.CardEffect), a => a.Kind == ActivatedAbilityKinds.ConsumeSourceBoonGrantSelfMight);
        Assert.Equal(4, spec.PowerModifierAmount); Assert.False(spec.ExhaustsSourceAsCost);
    }

    private static async Task Reject(MatchState state, string player, GameCommand command)
    {
        var rejected = await new CoreRuleEngine().ResolveAsync(state, new("illegal-boon-might", player, command.CmdType), command, default);
        Assert.False(rejected.Accepted); Assert.Empty(rejected.Events);
        Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(rejected.State));
    }
}
