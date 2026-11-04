using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;
using static Riftbound.ConformanceTests.RadGearTestPosition;

namespace Riftbound.ConformanceTests;

public sealed class CursedSarcophagusLinkedExileTests
{
    private const string Card = "UNL-148/219";
    private const string Ability = "CURSED_SARCOPHAGUS_EXHAUST_PLAY_LINKED_UNIT";

    private static MatchState Position(int mana = 30)
    {
        var state = Put(Create(), "GEAR", Card, "P1", "HAND");
        state = Put(state, "OTHER-GEAR", Card, "P1", "BASE");
        state = Put(state, "UNLINKED", "OGN·219/298", "P1", "BANISHED");
        state = RadUnitEffectTestFixture.Place(state, "ALLY", "P1", "GRAVEYARD");
        return state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(mana, 30) } };
    }

    private static async Task<Driver> Captured(int mana = 30)
    {
        var game = new Driver(Position(mana));
        await game.Act("P1", new PlayCardCommand("GEAR", Card, []));
        await game.Top();
        var group = Assert.Single(game.State.LinkedExiles);
        Assert.Equal(game.State.CardObjects["GEAR"].ObjectGeneration, group.SourceGeneration);
        Assert.Equal(game.State.CardObjects["ALLY"].ObjectGeneration, group.Objects["ALLY"]);
        Assert.DoesNotContain("UNLINKED", group.Objects.Keys);
        return game;
    }

    private static async Task Activate(Driver game, string source = "GEAR")
    {
        await game.Act("P1", new ActivateAbilityCommand(source, Ability, []));
        Assert.True(game.State.CardObjects[source].IsExhausted);
        await game.Top();
    }

    [Fact]
    public async Task NativeActivationPaysNormalPrintedCostsAndOnlyPlaysItsOwnExiles()
    {
        var game = await Captured();
        await Reject(game.State, "P2", new ActivateAbilityCommand("GEAR", Ability, []));
        await Reject(game.State, "P1", new ActivateAbilityCommand("GEAR", Ability, ["ALLY"]));
        await Activate(game, "OTHER-GEAR");
        Assert.Null(game.State.PendingEffectPlay);
        await Activate(game);
        var pending = Assert.IsType<PendingEffectPlayState>(game.State.PendingEffectPlay);
        Assert.Equal(new[] { "ALLY" }, pending.Sources.Keys);
        Assert.False(pending.IgnoreAllCosts); Assert.False(pending.IgnoreBaseMana); Assert.False(pending.IgnoreBasePower);
        await Reject(game.State, "P1", new PlayCardCommand("UNLINKED", "OGN·219/298", []));
        await Reject(game.State, "P2", new PlayCardCommand("ALLY", "OGN·219/298", []));
        var command = new PlayCardCommand("ALLY", "OGN·219/298", []);
        var quote = new CoreRuleEngine().PreviewPlayCard(game.State, "P1", PlayCostPreviewTests.Request(game.State, command));
        Assert.True(quote.CanPay, quote.Message); Assert.True(quote.Cost!.Mana > 0);
        var pool = game.State.RunePools["P1"];
        await game.Act("P1", command);
        Assert.Equal(pool.Mana - quote.Cost.Mana, game.State.RunePools["P1"].Mana);
        Assert.Contains("ALLY", game.State.PlayerZones["P1"].Base);
        Assert.DoesNotContain("ALLY", game.State.PlayerZones["P1"].Banished);
        Assert.Contains("UNLINKED", game.State.PlayerZones["P1"].Banished);
        Assert.Null(game.State.PendingEffectPlay);
        await game.Replay();
    }

    [Fact]
    public async Task InsufficientPaymentDoesNotSpendOrMoveTheCardAndMayBeRefused()
    {
        var game = await Captured(4);
        await Activate(game);
        var pending = game.State.PendingEffectPlay!;
        await Reject(game.State, "P1", new PlayCardCommand("ALLY", "OGN·219/298", []));
        await game.Act("P1", new ChooseCardsCommand(pending.ChoiceId, "EFFECT_PLAY", []));
        Assert.Contains("ALLY", game.State.PlayerZones["P1"].Banished);
        Assert.Empty(game.State.StackItems);
        await game.Replay();
    }

    [Theory]
    [InlineData("waive-mana")] [InlineData("waive-power")] [InlineData("waive-all")]
    [InlineData("unlinked")] [InlineData("generation")] [InlineData("different-instance")]
    [InlineData("different-source")] [InlineData("optional")]
    public async Task RecoveryRejectsForgedCostsBindingsAndSourceInstances(string branch)
    {
        var game = await Captured(); await Activate(game);
        var pending = game.State.PendingEffectPlay!;
        var forged = branch switch {
            "waive-mana" => pending with { IgnoreBaseMana = true },
            "waive-power" => pending with { IgnoreBasePower = true },
            "waive-all" => pending with { IgnoreAllCosts = true },
            "unlinked" => pending with { Sources = new Dictionary<string, long> { ["UNLINKED"] = game.State.CardObjects["UNLINKED"].ObjectGeneration } },
            "generation" => pending with { Sources = new Dictionary<string, long> { ["ALLY"] = 999 } },
            "different-instance" => pending with { Parent = pending.Parent with { SimpleActivation = pending.Parent.SimpleActivation! with { RuleTextInstanceId = "PRINTED:OTHER-GEAR:0" } } },
            "different-source" => pending with { Parent = pending.Parent with { SourceObjectId = "OTHER-GEAR" } },
            _ => pending with { Optional = true }
        };
        Assert.Contains("invalid banished unit effect-play continuation", OfficialInsightAndSpellLockTests.Errors(
            game.State with { PendingEffectPlay = forged }));
        await game.Replay();
    }

    [Theory]
    [InlineData("delete")] [InlineData("rebind")] [InlineData("remove-history")]
    public async Task AcceptedHistoryAuthenticatesEveryExileGroup(string branch)
    {
        var game = await Captured();
        var group = Assert.Single(game.State.LinkedExiles);
        var state = branch == "delete" ? game.State with { LinkedExiles = [] }
            : branch == "rebind" ? game.State with { LinkedExiles = [group with { SourceObjectId = "OTHER-GEAR" }] } : game.State;
        var events = branch == "remove-history" ? game.Events().Where(e => e.Event.Kind != "LINKED_EXILE_CREATED").ToArray() : game.Events();
        var commands = game.Commands();
        var errors = MatchRecoveryValidator.Validate(state.RoomId, commands[^1].CompletedEventSequence, commands,
            events, new Dictionary<string, RecoveredPlayerView>(), state, state.Tick);
        Assert.Contains("linked exile ledger differs from accepted banishment history", errors);
        await game.Replay();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ARealReturnAndReentryCannotInheritTheOldExiles(bool duringPayment)
    {
        var initial = Put(Position(), "RETURN", "SFD·135/221", "P1", "HAND");
        initial = Put(initial, "SECOND", "OGN·219/298", "P1", "GRAVEYARD");
        if (duringPayment) initial = Put(initial, "ALLY", "SFD·044/221", "P1", "GRAVEYARD");
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("GEAR", Card, [])); await game.Top();
        var generation = game.State.CardObjects["GEAR"].ObjectGeneration;
        if (duringPayment)
        {
            await Activate(game);
            await game.Act("P1", new PlayCardCommand("ALLY", "SFD·044/221", [],
                OptionalCosts: ["RETURN_FRIENDLY_EQUIPMENT:GEAR"]));
        }
        else
        {
            await game.Act("P1", new PlayCardCommand("RETURN", "SFD·135/221", ["GEAR"]));
            await game.Top();
        }
        Assert.Contains("GEAR", game.State.PlayerZones["P1"].Hand);
        Assert.Equal(generation, Assert.Single(game.State.LinkedExiles).SourceGeneration);
        await game.Act("P1", new PlayCardCommand("GEAR", Card, [])); await game.Top();
        Assert.NotEqual(generation, game.State.CardObjects["GEAR"].ObjectGeneration);
        await Activate(game);
        Assert.Null(game.State.PendingEffectPlay);
        Assert.Contains("SECOND", game.State.PlayerZones["P1"].Banished);
        await game.Replay();
    }

    [Fact]
    public async Task TwoActualPlaysOfTheSamePrintingKeepIndependentExileCollections()
    {
        var initial = RadUnitEffectTestFixture.Place(Position(), "OTHER-GEAR", "P1", "HAND");
        initial = Put(initial, "FODDER", "OGN·219/298", "P1", "BASE");
        initial = Put(initial, "KILL", "OGN·229/298", "P1", "HAND");
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("GEAR", Card, [])); await game.Top();
        await game.Act("P1", new PlayCardCommand("KILL", "OGN·229/298", ["FODDER"])); await game.Top();
        await game.Act("P1", new PlayCardCommand("OTHER-GEAR", Card, [])); await game.Top();
        Assert.Equal(2, game.State.LinkedExiles.Count);
        await Activate(game, "OTHER-GEAR");
        Assert.Equal(new[] { "FODDER" }, game.State.PendingEffectPlay!.Sources.Keys);
        await Reject(game.State, "P1", new PlayCardCommand("ALLY", "OGN·219/298", []));
        await game.Act("P1", new PlayCardCommand("FODDER", "OGN·219/298", []));
        await Activate(game);
        Assert.Equal(new[] { "ALLY" }, game.State.PendingEffectPlay!.Sources.Keys);
        await game.Act("P1", new PlayCardCommand("ALLY", "OGN·219/298", []));
        await game.Replay();
    }

    [Fact]
    public async Task NormalAdditionalCostsMayReturnTheVerySourceOfTheCapturedActivation()
    {
        var initial = Put(Position(), "ALLY", "SFD·044/221", "P1", "GRAVEYARD");
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("GEAR", Card, [])); await game.Top();
        await Activate(game);
        await Reject(game.State, "P1", new PlayCardCommand("ALLY", "SFD·044/221", []));
        var mana = game.State.RunePools["P1"].Mana;
        await game.Act("P1", new PlayCardCommand("ALLY", "SFD·044/221", [], OptionalCosts: ["RETURN_FRIENDLY_EQUIPMENT:GEAR"]));
        Assert.Equal(mana - 3, game.State.RunePools["P1"].Mana);
        Assert.Contains("ALLY", game.State.PlayerZones["P1"].Base);
        Assert.Contains("GEAR", game.State.PlayerZones["P1"].Hand);
        Assert.Null(game.State.PendingEffectPlay);
        await game.Replay();
    }

    [Fact]
    public async Task SharedResourceActionsRemainAvailableWhilePayingForTheLinkedUnit()
    {
        var initial = Position(4);
        foreach (var id in new[] { "R1", "R2", "R3", "R4" }) initial = Put(initial, id, "OGN·007/298", "P1", "BASE");
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("GEAR", Card, [])); await game.Top();
        await Activate(game);
        foreach (var id in new[] { "R1", "R2", "R3", "R4" }) await game.Act("P1", new TapRuneCommand(id));
        Assert.Equal(4, game.State.RunePools["P1"].Mana);
        await game.Act("P1", new PlayCardCommand("ALLY", "OGN·219/298", []));
        Assert.Equal(0, game.State.RunePools["P1"].Mana);
        Assert.Contains("ALLY", game.State.PlayerZones["P1"].Base);
        await game.Replay();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PrintedColoredPowerIsStillRequiredByTheLinkedPlay(bool hasRed)
    {
        var initial = Put(Position(), "ALLY", "OGN·015/298", "P1", "GRAVEYARD");
        var traits = new Dictionary<string, int> { [RuneTrait.Purple] = 1 };
        if (hasRed) traits[RuneTrait.Red] = 1;
        initial = initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) { ["P1"] = new(30, 0, traits) } };
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("GEAR", Card, [])); await game.Top();
        await Activate(game);
        var command = new PlayCardCommand("ALLY", "OGN·015/298", []);
        if (hasRed)
        {
            await game.Act("P1", command);
            Assert.Equal(0, game.State.RunePools["P1"].TotalPower);
            Assert.Contains("ALLY", game.State.PlayerZones["P1"].Base);
        }
        else
        {
            await Reject(game.State, "P1", command);
            Assert.Contains("ALLY", game.State.PlayerZones["P1"].Banished);
        }
        await game.Replay();
    }

    [Fact]
    public async Task ALinkedExileCollectionSurvivesTheTurnHistoryBeingCleared()
    {
        var initial = Position();
        foreach (var id in new[] { "R1", "R2", "R3", "R4" }) initial = Put(initial, id, "OGN·007/298", "P1", "BASE");
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("GEAR", Card, [])); await game.Top();
        var group = Assert.Single(game.State.LinkedExiles);
        await game.Act("P1", new EndTurnCommand()); await game.Act("P2", new EndTurnCommand());
        Assert.Empty(game.State.CardPlays.Resolved);
        Assert.Equal(MatchStateHasher.HashValue(group), MatchStateHasher.HashValue(Assert.Single(game.State.LinkedExiles)));
        await Activate(game);
        foreach (var id in new[] { "R1", "R2", "R3", "R4" }) await game.Act("P1", new TapRuneCommand(id));
        await game.Act("P1", new PlayCardCommand("ALLY", "OGN·219/298", []));
        Assert.Contains("ALLY", game.State.PlayerZones["P1"].Base);
        await game.Replay();
    }
}
