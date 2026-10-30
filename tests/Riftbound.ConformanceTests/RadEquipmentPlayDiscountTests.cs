using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class RadEquipmentPlayDiscountTests
{
    private const string Hero = "RAD·068/167", Gear = "SFD·059/221";
    private const string ChoicePrefix = "USE_PLAY_COST_REDUCTION:";
    public static IEnumerable<object[]> CardNumbers() => new[] { Hero, "RAD·068a/167" }.Select(no => new object[] { no });

    private static MatchState Position(string no = Hero, int mana = 20, int power = 20)
    {
        var s = ArmamentMightTests.Position(unit: no, power: 5);
        s = LocalDestructionRecallTests.At(s, "HOST", "P1", "BATTLEFIELD", "F");
        s = HandDiscardObserverTests.Put(s, "PLAY", Gear, "P1", "HAND");
        s = HandDiscardObserverTests.Put(s, "FOLLOW", Gear, "P1", "HAND");
        return s with { RunePools = new Dictionary<string, RunePool>(s.RunePools) { ["P1"] = new(mana, power) } };
    }

    private static string[] Choices(MatchState state, string source = "PLAY")
    {
        var prompt = JsonSerializer.SerializeToElement(ResolutionResult.BuildPrompts(state)["P1"], new JsonSerializerOptions(JsonSerializerDefaults.Web));
        foreach (var candidate in prompt.GetProperty("candidates").EnumerateArray().Where(c => c.GetProperty("action").GetString() == "PLAY_CARD"))
        foreach (var requirement in candidate.GetProperty("metadata").GetProperty("sourceRequirements").EnumerateArray())
            if (requirement.GetProperty("sourceObjectId").GetString() == source)
                return requirement.GetProperty("optionalCostChoices").EnumerateArray()
                    .Select(c => c.GetProperty("id").GetString()!).Where(id => id.StartsWith(ChoicePrefix, StringComparison.Ordinal)).ToArray();
        return [];
    }

    private static PlayCardCommand Play(params string[] costs) => new("PLAY", Gear, [], OptionalCosts: costs, Destination: "BASE");

    [Theory, MemberData(nameof(CardNumbers))]
    public async Task EveryPrintingKeepsItsOfficialCostMightAndOptionalSimultaneousReduction(string no)
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, c => c.CardNo == no);
        Assert.Equal(no == Hero ? 39566 : 39567, card.Id); Assert.Equal(5, card.Energy); Assert.Equal(5, card.Power);
        Assert.Equal("机械奇迹", card.SubTitle);
        var spec = Assert.IsType<OptionalPlayCostReductionSpec>(OptionalPlayCostReductionParser.Parse(card.CardEffect));
        Assert.Equal("EQUIPMENT", spec.Category); Assert.Equal(1, spec.Mana); Assert.Equal(1, spec.Power); Assert.Equal("BATTLEFIELD", spec.SourceZone);
        var game = new Driver(VenUnitEntryBasicTests.Position(no));
        await game.Act("P1", new PlayCardCommand("UNIT", no, [], Destination: "BASE"));
        Assert.Equal(15, game.State.RunePools["P1"].Mana); Assert.Equal(5, game.State.CardObjects["UNIT"].Power);
        Assert.Empty(game.State.StackItems); await game.Replay();
    }

    [Theory, MemberData(nameof(CardNumbers))]
    public async Task ExplicitChoicePaysOneLessManaAndPowerAndUsesTheAllowanceExactlyOnce(string no)
    {
        var initial = Position(no, 2, 0); var choice = Assert.Single(Choices(initial));
        var engine = new CoreRuleEngine(); var hash = MatchStateHasher.Hash(initial);
        var quote = engine.PreviewPlayCard(initial, "P1", PlayCostPreviewTests.Request(initial, Play(choice)));
        Assert.True(quote.CanPay, quote.Message); Assert.Equal(2, quote.Cost!.Mana); Assert.Equal(0, quote.Cost.GenericPower);
        Assert.Equal(hash, MatchStateHasher.Hash(initial));
        var game = new Driver(initial); await game.Act("P1", Play(choice));
        Assert.Equal(0, game.State.RunePools["P1"].Mana); Assert.Equal(0, game.State.RunePools["P1"].TotalPower);
        Assert.Contains("PLAY", game.State.PlayerZones["P1"].Base); Assert.Empty(Choices(game.State, "FOLLOW"));
        Assert.DoesNotContain("OPTIONAL_PLAY_COST_REDUCTION_USED", JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(game.State)));
        await Reject(game.State, new("FOLLOW", Gear, [], OptionalCosts: [choice], Destination: "BASE")); await game.Replay();
    }

    [Theory, MemberData(nameof(CardNumbers))]
    public async Task DecliningTheOptionalReductionPaysFullPriceAndLeavesItAvailable(string no)
    {
        var game = new Driver(Position(no)); var choice = Assert.Single(Choices(game.State));
        await game.Act("P1", Play()); Assert.Equal(17, game.State.RunePools["P1"].Mana); Assert.Equal(19, game.State.RunePools["P1"].TotalPower);
        Assert.Equal(choice, Assert.Single(Choices(game.State, "FOLLOW")));
        await game.Act("P1", new PlayCardCommand("FOLLOW", Gear, [], OptionalCosts: [choice], Destination: "BASE"));
        Assert.Equal(15, game.State.RunePools["P1"].Mana); Assert.Equal(19, game.State.RunePools["P1"].TotalPower); await game.Replay();
    }

    [Theory]
    [InlineData("base")] [InlineData("enemy")] [InlineData("hidden")] [InlineData("unit")] [InlineData("spell")]
    public async Task ThePermissionRequiresTheCurrentFriendlyBattlefieldSourceAndEquipment(string branch)
    {
        var state = Position(); var oldChoice = Assert.Single(Choices(state));
        if (branch == "base") state = LocalDestructionRecallTests.At(state, "HOST", "P1", "BASE");
        if (branch == "enemy") state = LocalDestructionRecallTests.At(state, "HOST", "P2", "BATTLEFIELD", "OTHER");
        if (branch == "hidden") state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["HOST"] = state.CardObjects["HOST"] with { IsFaceDown = true } } };
        if (branch is "unit" or "spell") {
            state = state with { PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P1"] = state.PlayerZones["P1"] with { Hand = state.PlayerZones["P1"].Hand.Where(id => id != "PLAY").ToArray() } } };
            state = HandDiscardObserverTests.Put(state, "PLAY", branch == "unit" ? "OGN·001/298" : "VEN·049", "P1", "HAND");
        }
        Assert.Empty(Choices(state));
        await Reject(state, branch is "unit" or "spell"
            ? new("PLAY", branch == "unit" ? "OGN·001/298" : "VEN·049", [], OptionalCosts: [oldChoice], Destination: branch == "unit" ? "BASE" : "") : Play(oldChoice));
    }

    [Theory]
    [InlineData("invented")] [InlineData("duplicate")] [InlineData("unpayable")]
    public async Task InvalidChoicesDoNotPayMoveCardsOrConsumeTheAllowance(string branch)
    {
        var state = Position(); var choice = Assert.Single(Choices(state));
        if (branch == "unpayable") state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(1, 0) } };
        await Reject(state, Play(branch == "invented" ? [ChoicePrefix + "forged"] : branch == "duplicate" ? [choice, choice] : [choice]));
        Assert.DoesNotContain(state.UntilEndOfTurnEffects, effect => effect.StartsWith("OPTIONAL_PLAY_COST_REDUCTION_USED|", StringComparison.Ordinal));
        if (branch != "unpayable") Assert.Equal(choice, Assert.Single(Choices(state)));
    }

    [Fact]
    public async Task AZeroPowerPrintedCostStillReceivesTheManaReductionAndCannotBecomeNegative()
    {
        var state = HandDiscardObserverTests.Put(Position(mana: 1, power: 0), "PLAY", "SFD·016/221", "P1", "HAND");
        // Put adds the requested card to hand; preserve one physical occurrence.
        state = state with { PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
            ["P1"] = state.PlayerZones["P1"] with { Hand = state.PlayerZones["P1"].Hand.Distinct().ToArray() } } };
        var choice = Assert.Single(Choices(state)); var game = new Driver(state);
        await game.Act("P1", new PlayCardCommand("PLAY", "SFD·016/221", [], OptionalCosts: [choice], Destination: "BASE"));
        Assert.Equal(0, game.State.RunePools["P1"].Mana); Assert.Equal(0, game.State.RunePools["P1"].TotalPower); await game.Replay();
    }

    [Fact]
    public async Task CopiedTextHasItsOwnAllowanceAndDoesNotChargeEitherInstanceTwice()
    {
        var game = new Driver(AttachedRuleTextTests.AddSong(Position(), "SONG"));
        await game.Act("P1", new AssembleEquipmentCommand("SONG", "HOST", ["ASSEMBLE_GREEN"])); await game.Top();
        var choices = Choices(game.State); Assert.Equal(2, choices.Length);
        await game.Act("P1", Play(choices[0]));
        Assert.Equal(choices[1], Assert.Single(Choices(game.State, "FOLLOW")));
        await game.Act("P1", new PlayCardCommand("FOLLOW", Gear, [], OptionalCosts: [choices[1]], Destination: "BASE"));
        Assert.Equal(2, game.State.UntilEndOfTurnEffects.Count(effect => effect.StartsWith("OPTIONAL_PLAY_COST_REDUCTION_USED|", StringComparison.Ordinal)));
        await game.Replay();
    }

    [Fact]
    public async Task IndependentSourcesCanReduceTheSamePlayAndCannotCreateNegativePowerCosts()
    {
        var state = OfficialHoldSequenceTests.AddUnit(Position(mana: 1, power: 0), "SECOND", "RAD·068a/167", "F", "P1");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["SECOND"] = OfficialGarenConquestBattlefieldCountTests.Printed("SECOND", "RAD·068a/167", "P1") } };
        var choices = Choices(state); Assert.Equal(2, choices.Length);
        var game = new Driver(state); await game.Act("P1", Play(choices));
        Assert.Equal(0, game.State.RunePools["P1"].Mana); Assert.Equal(0, game.State.RunePools["P1"].TotalPower);
        Assert.Equal(2, game.State.UntilEndOfTurnEffects.Count(effect => effect.StartsWith("OPTIONAL_PLAY_COST_REDUCTION_USED|", StringComparison.Ordinal)));
        Assert.Empty(Choices(game.State, "FOLLOW")); await game.Replay();
    }

    [Fact]
    public async Task ReturningTheSourceRevokesItsOldChoiceWithoutLeakingItsNewPrivateIdentity()
    {
        var initial = HandDiscardObserverTests.Put(Position(), "RETURN", "OGN·104/298", "P1", "HAND");
        var choice = Assert.Single(Choices(initial)); var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["HOST"])); await game.Top();
        Assert.Contains("HOST", game.State.PlayerZones["P1"].Hand); Assert.Empty(Choices(game.State));
        await Reject(game.State, Play(choice)); await game.Replay();
    }

    [Fact]
    public async Task AConsumedAllowanceRemainsValidAfterItsSourceReturnsToAPrivateZone()
    {
        var game = new Driver(HandDiscardObserverTests.Put(Position(), "RETURN", "OGN·104/298", "P1", "HAND"));
        await game.Act("P1", Play(Assert.Single(Choices(game.State))));
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["HOST"])); await game.Top();
        Assert.Contains("HOST", game.State.PlayerZones["P1"].Hand);
        Assert.DoesNotContain("OPTIONAL_PLAY_COST_REDUCTION_USED", JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(game.State)));
        await game.Replay();
    }

    [Theory]
    [InlineData("future")] [InlineData("turn")] [InlineData("generation")]
    [InlineData("face")] [InlineData("instance")] [InlineData("duplicate")]
    [InlineData("null-source")] [InlineData("null-card")] [InlineData("null-controller")]
    public async Task RecoveryRejectsForgedAllowanceRecords(string change)
    {
        var game = new Driver(Position()); await game.Act("P1", Play(Assert.Single(Choices(game.State))));
        const string prefix = "OPTIONAL_PLAY_COST_REDUCTION_USED|";
        var marker = Assert.Single(game.State.UntilEndOfTurnEffects, effect => effect.StartsWith(prefix, StringComparison.Ordinal));
        var use = JsonSerializer.Deserialize<OptionalPlayCostReductionUse>(Convert.FromBase64String(marker[prefix.Length..]))!;
        var forged = change switch {
            "future" => use with { CapturedTick = game.State.Tick + 1 },
            "turn" => use with { TurnNumber = use.TurnNumber + 1 },
            "generation" => use with { Source = use.Source with { Generation = use.Source.Generation + 1 } },
            "face" => use with { FaceRevision = use.FaceRevision + 1 },
            "instance" => use with { InstanceId = "invented" },
            "null-source" => use with { Source = null! },
            "null-card" => use with { CardNo = null! },
            "null-controller" => use with { ControllerId = null! },
            _ => use
        };
        var forgedMarker = prefix + Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(forged));
        var state = game.State with { UntilEndOfTurnEffects = game.State.UntilEndOfTurnEffects.Where(effect => effect != marker)
            .Concat(change == "duplicate" ? [marker, marker] : new[] { forgedMarker }).ToArray() };
        Assert.Contains("invalid optional play cost reduction use", OfficialInsightAndSpellLockTests.Errors(state));
    }

    [Fact]
    public async Task TheNextTurnRestoresTheAllowanceAndRejectsThePreviousTurnsChoice()
    {
        var initial = Position();
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["P2-DRAW"] = OfficialGarenConquestBattlefieldCountTests.Printed("P2-DRAW", "OGN·001/298", "P2") },
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) { ["P2"] = initial.PlayerZones["P2"] with { MainDeck = ["P2-DRAW"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) { ["P2-DRAW"] = new("P2", "MAIN_DECK") } };
        var game = new Driver(initial); var old = Assert.Single(Choices(initial)); await game.Act("P1", Play(old));
        await game.Act("P1", new EndTurnCommand()); await game.Act("P2", new EndTurnCommand());
        var current = Assert.Single(Choices(game.State, "FOLLOW")); Assert.NotEqual(old, current);
        await Reject(game.State, new("FOLLOW", Gear, [], OptionalCosts: [old], Destination: "BASE"));
        await game.Act("P1", new TapRuneCommand("R1")); await game.Act("P1", new TapRuneCommand("R2"));
        await game.Act("P1", new PlayCardCommand("FOLLOW", Gear, [], OptionalCosts: [current], Destination: "BASE"));
        await game.Replay();
    }

    private static async Task Reject(MatchState state, PlayCardCommand command)
    {
        var hash = MatchStateHasher.Hash(state); var views = JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(state));
        var result = await new CoreRuleEngine().ResolveAsync(state, new("invalid-optional-play-reduction", "P1", command.CmdType), command, default);
        Assert.False(result.Accepted); Assert.Empty(result.Events); Assert.Equal(hash, MatchStateHasher.Hash(result.State));
        Assert.Equal(views, JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(result.State)));
    }
}
