using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class ExhaustedCopiedSourceContinuityTests
{
    private const string Oracle = "RAD·053/167";
    private const string CopiedOracle = "COPY-TOKEN-001";

    [Fact]
    public async Task RecoveryRejectsAnExhaustedInsightWhoseCopiedSourceNeverExisted()
    {
        var game = await EarnCopiedInsight();
        var item = Assert.Single(game.State.StackItems);
        Assert.Empty(MatchRecoveryValidator.ValidateComponentStateAndViews(game.State));
        var forged = item with { SourceObjectId = "NEVER-ENTERED-TOKEN" };
        Assert.NotEmpty(MatchRecoveryValidator.ValidateComponentStateAndViews(game.State with { StackItems = [forged] }));
        await game.ColdCheckpoint();
    }

    [Theory]
    [InlineData("stays")]
    [InlineData("return")]
    [InlineData("destroy")]
    public async Task ARealCopiedOraclesEarnedInsightSurvivesDepartureAndRestoresItsPrivateChoice(string departure)
    {
        var game = await EarnCopiedInsight();
        var captured = Assert.Single(game.State.StackItems);
        var originalDeck = game.State.PlayerZones["P1"].MainDeck.ToArray();
        if (departure != "stays")
        {
            var spell = departure == "return" ? "OGN·104/298" : "OGN·229/298";
            await game.Act("P1", new PlayCardCommand(departure == "return" ? "RETURN" : "DESTROY", spell, [CopiedOracle]));
            await game.Top();
            Assert.False(game.State.CardObjects.ContainsKey(CopiedOracle));
            Assert.False(game.State.ObjectLocations.ContainsKey(CopiedOracle));
            Assert.Equal(captured.StackItemId, Assert.Single(game.State.StackItems).StackItemId);
            Assert.Single(game.Events(), recovered => recovered.Event.Kind == "TOKEN_CEASED_TO_EXIST"
                && Equals(recovered.Event.Payload.GetValueOrDefault("targetObjectId"), CopiedOracle));
            AssertCeasedSource(game.State);
        }
        await game.ColdCheckpoint();
        await game.Top();
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        Assert.Equal("INSIGHT", choice.ChoiceWindow);
        Assert.Equal(new[] { originalDeck[0] }, choice.LegalObjectIds);
        Assert.DoesNotContain(originalDeck[0], JsonSerializer.Serialize(ResolutionResult.BuildPrompts(game.State)["P2"]));
        await game.ColdCheckpoint();

        var command = new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, []);
        var before = MatchStateHasher.Hash(game.State);
        var rejected = await new CoreRuleEngine().ResolveAsync(game.State,
            new("foreign-exhausted-insight", "P2", command.CmdType), command, default);
        Assert.False(rejected.Accepted);
        Assert.Empty(rejected.Events);
        Assert.Equal(before, MatchStateHasher.Hash(rejected.State));
        await game.Act("P1", command);
        Assert.Null(game.State.PendingCardChoice);
        Assert.Empty(game.State.StackItems);
        Assert.Equal(originalDeck, game.State.PlayerZones["P1"].MainDeck);
        Assert.Single(game.Events(), recovered => recovered.Event.Kind == "INSIGHT_REQUESTED");
        await game.ColdCheckpoint();
    }

    [Fact]
    public async Task ExhaustingAndDestroyingTheCopyInOneInstructionKeepsItsNewInsight()
    {
        var game = await EarnCopiedInsight();
        await game.Top();
        var first = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        await game.Act("P1", new ChooseCardsCommand(first.ChoiceId, first.ChoiceWindow, []));
        await CloseFocus(game);
        await game.Act("P1", new PlayCardCommand("READY", "UNL-009/219", [CopiedOracle]));
        await game.Top();
        Assert.False(game.State.CardObjects[CopiedOracle].IsExhausted);
        await CloseFocus(game);
        var requested = game.Events().Count(e => e.Event.Kind == "INSIGHT_REQUESTED");
        await game.Act("P1", new PlayCardCommand("OVERLOAD", "OGN·123/298", []));
        await game.Top();
        Assert.False(game.State.CardObjects.ContainsKey(CopiedOracle));
        var context = Assert.IsType<InsightTriggerContext>(Assert.Single(game.State.StackItems).InsightContext);
        Assert.Equal(context.SourceCapturedTick, AssertCeasedSource(game.State).DepartedTick);
        await game.ColdCheckpoint();
        await game.Top();
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        await game.ColdCheckpoint();
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, []));
        Assert.Equal(requested + 1, game.Events().Count(e => e.Event.Kind == "INSIGHT_REQUESTED"));
        Assert.Empty(game.State.StackItems);
        await game.ColdCheckpoint();
    }

    private static CeasedSelfAbilitySource AssertCeasedSource(MatchState state)
    {
        var item = Assert.Single(state.StackItems);
        var context = Assert.IsType<InsightTriggerContext>(item.InsightContext);
        var receipt = Assert.IsType<CeasedSelfAbilitySource>(context.CeasedSource);
        Assert.Equal(CopiedOracle, receipt.Source.ObjectId);
        Assert.Equal(Oracle, receipt.CardNo);
        Assert.Empty(MatchRecoveryValidator.ValidateComponentStateAndViews(state));
        foreach (var invalid in new CeasedSelfAbilitySource?[] {
            null,
            receipt with { Source = receipt.Source with { ObjectId = "NEVER-ENTERED-TOKEN" } },
            receipt with { FaceRevision = receipt.FaceRevision + 1 },
            receipt with { AcquiredTick = receipt.AcquiredTick - 1 },
            receipt with { DepartedTick = state.Tick + 1 }
        })
            Assert.NotEmpty(MatchRecoveryValidator.ValidateComponentStateAndViews(state with {
                StackItems = [item with { InsightContext = context with { CeasedSource = invalid } }]
            }));
        return receipt;
    }

    private static async Task CloseFocus(Driver game)
    {
        for (var passes = 0; game.State.StackItems.Count == 0 && game.State.FocusPlayerId is { } player; passes++)
        {
            Assert.True(passes < 4);
            await game.Act(player, new PassFocusCommand());
        }
    }

    private static async Task<Driver> EarnCopiedInsight()
    {
        var initial = VenUnitEntryBasicTests.Position(Oracle);
        initial = initial with {
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["UNIT"] = initial.CardObjects["UNIT"] with { IsExhausted = true }
            },
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
                ["P1"] = initial.PlayerZones["P1"] with { Hand = [], Base = [..initial.PlayerZones["P1"].Base, "UNIT"] }
            },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) { ["UNIT"] = new("P1", "BASE") }
        };
        initial = OfficialCopyIdentityTests.AddSpell(initial, "COPY", "UNL-200/219");
        initial = OfficialCopyIdentityTests.AddSpell(initial, "RETURN", "OGN·104/298");
        initial = OfficialCopyIdentityTests.AddSpell(initial, "DESTROY", "OGN·229/298");
        initial = OfficialCopyIdentityTests.AddSpell(initial, "READY", "UNL-009/219");
        initial = OfficialCopyIdentityTests.AddSpell(initial, "OVERLOAD", "OGN·123/298");
        var game = new Driver(initial) { RecoverAfterEveryCommand = false };
        await game.Act("P1", new PlayCardCommand("COPY", "UNL-200/219", ["UNIT"]));
        await game.Top();
        await game.Top(); // The copied unit has its own pending play.
        await CloseFocus(game);
        var token = game.State.CardObjects[CopiedOracle];
        Assert.Equal(Oracle, token.CardNo);
        Assert.Equal(P6TokenFactoryCatalog.ImageTokenCardNo, token.TokenFactoryCardNo);
        Assert.False(token.IsExhausted);
        await game.Act("P1", new MoveUnitCommand(CopiedOracle, "BASE", "BATTLEFIELD:BF"));
        Assert.True(game.State.CardObjects[CopiedOracle].IsExhausted);
        var insight = Assert.IsType<InsightTriggerContext>(Assert.Single(game.State.StackItems).InsightContext);
        Assert.Equal("EXHAUSTED", insight.Kind);
        Assert.Equal(Oracle, insight.CardNo);
        Assert.Equal(P6TokenFactoryCatalog.ImageTokenCardNo, insight.SourceTokenFactoryCardNo);
        return game;
    }
}
