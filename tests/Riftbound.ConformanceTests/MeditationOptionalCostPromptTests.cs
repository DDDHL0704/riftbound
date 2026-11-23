using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;
using static Riftbound.ConformanceTests.RadGearTestPosition;

namespace Riftbound.ConformanceTests;

public sealed class MeditationOptionalCostPromptTests
{
    private const string Meditation = "OGN·048/298";
    private const string Exhaust = "EXHAUST_FRIENDLY_UNIT:";

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void PromptListsEachReadyFriendlyUnitByName(int readyCount)
    {
        var state = Position();
        state = Put(state, "FIELD-ALLY", "OGN·175/298", "P1", "BATTLEFIELD", "BF");
        state = Put(state, "P1-FACE-DOWN", "OGN·219/298", "P1", "BASE");
        state = Put(state, "P2-PRIVATE-HAND", "OGN·175/298", "P2", "HAND");
        state = state with
        {
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects)
            {
                ["ALLY"] = state.CardObjects["ALLY"] with { IsExhausted = readyCount == 0 },
                ["FIELD-ALLY"] = state.CardObjects["FIELD-ALLY"] with { IsExhausted = readyCount < 2 },
                ["P1-FACE-DOWN"] = state.CardObjects["P1-FACE-DOWN"] with { IsFaceDown = true }
            }
        };

        var prompts = ResolutionResult.BuildPrompts(state);
        var play = PlayCandidate(prompts["P1"]);
        var requirement = Requirement(play);
        var choices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(requirement["optionalCostChoices"])
            .Where(choice => choice.Id.StartsWith(Exhaust, StringComparison.Ordinal)).ToArray();
        string[] eligibleIds = ["ALLY", "FIELD-ALLY"];
        string[] names = ["先锋中士", "船坞潜伏者"];
        Assert.Equal(eligibleIds.Take(readyCount).Select(id => Exhaust + id), choices.Select(choice => choice.Id));
        for (var index = 0; index < choices.Length; index++)
        {
            Assert.Contains(names[index], choices[index].Label);
            Assert.Equal(new[] { eligibleIds[index] }, choices[index].ObjectIds);
        }
        Assert.Equal(choices.Select(choice => choice.Id), (play.OptionalCosts ?? [])
            .Where(choice => choice.Id.StartsWith(Exhaust, StringComparison.Ordinal)).Select(choice => choice.Id));
        Assert.False(requirement.ContainsKey("requiredAdditionalCostChoiceCount"));

        var chooser = JsonSerializer.Serialize(play);
        Assert.DoesNotContain("P2-PRIVATE-HAND", chooser);
        Assert.DoesNotContain("P2-D0", chooser);
        Assert.DoesNotContain("P1-FACE-DOWN", chooser);
        var opponent = JsonSerializer.Serialize(prompts["P2"]);
        Assert.DoesNotContain("MEDITATION-SOURCE", opponent);
        Assert.DoesNotContain(Exhaust + "ALLY", opponent);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task PreviewAndPlayAgreeAndRecoverAcrossTheResponseWindow(bool payExhaust, bool tapRunes)
    {
        var state = Position();
        if (tapRunes)
        {
            state = Put(state, "RUNE-ONE", "UNL-R02", "P1", "BASE");
            state = Put(state, "RUNE-TWO", "UNL-R02", "P1", "BASE");
            state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = RunePool.Empty } };
        }
        var game = new Driver(state) { RecoverAfterEveryCommand = false };
        var before = MatchStateHasher.Hash(state);
        var requirement = Requirement(PlayCandidate(game.PromptFor("P1")));
        var choices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(requirement["optionalCostChoices"]);
        var exhaust = Assert.Single(choices, choice => choice.Id.StartsWith(Exhaust, StringComparison.Ordinal));
        var costs = new List<string>();
        if (payExhaust) costs.Add(exhaust.Id);
        if (tapRunes)
        {
            Assert.Equal(2, requirement["minimumManaCost"]);
            Assert.Equal(0, requirement["availableMana"]);
            var resources = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(requirement["paymentResourceChoices"]);
            var taps = resources.Where(choice => choice.Id.StartsWith("TAP_RUNE:", StringComparison.Ordinal)).Select(choice => choice.Id).ToArray();
            Assert.Equal(new[] { "TAP_RUNE:RUNE-ONE", "TAP_RUNE:RUNE-TWO" }, taps);
            costs.AddRange(taps);
        }
        var command = Play(costs.ToArray());
        var quote = Quote(state, command);
        Assert.True(quote.CanPay, quote.Message);
        Assert.Equal(2, quote.Cost!.Mana);
        Assert.Equal(0, quote.Cost.GenericPower + quote.Cost.PowerByTrait.Values.Sum());
        Assert.Equal(before, MatchStateHasher.Hash(state));

        await game.Act("P1", command);
        Assert.Equal(payExhaust, game.State.CardObjects["ALLY"].IsExhausted);
        Assert.Empty(game.State.PlayerZones["P1"].Hand);
        Assert.Equal(quote.Cost.RemainingMana, game.State.RunePools["P1"].Mana);
        if (tapRunes)
        {
            Assert.True(game.State.CardObjects["RUNE-ONE"].IsExhausted);
            Assert.True(game.State.CardObjects["RUNE-TWO"].IsExhausted);
        }
        await game.ColdCheckpoint();
        await game.Top();
        Assert.Equal(state.PlayerZones["P1"].MainDeck.Take(payExhaust ? 2 : 1), game.State.PlayerZones["P1"].Hand);
        Assert.Contains("MEDITATION-SOURCE", game.State.PlayerZones["P1"].Graveyard);
        await game.ColdCheckpoint();
    }

    [Theory]
    [InlineData("ALLY")]
    [InlineData("ENEMY")]
    public async Task InvalidExhaustIsRejectedByPreviewAndPlayWithoutChangingState(string unit)
    {
        var state = Position();
        state = state with
        {
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects)
            {
                ["ALLY"] = state.CardObjects["ALLY"] with { IsExhausted = true }
            }
        };
        var before = MatchStateHasher.Hash(state);
        var snapshots = JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(state));
        var prompts = JsonSerializer.Serialize(ResolutionResult.BuildPrompts(state));
        var command = Play([Exhaust + unit]);
        var quote = Quote(state, command);
        Assert.False(quote.CanPay);
        Assert.Equal(ErrorCodes.InvalidTarget, quote.ErrorCode);
        var result = await new CoreRuleEngine().ResolveAsync(state,
            new("invalid-meditation-payment", "P1", CommandTypes.PlayCard), command, default);
        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(before, MatchStateHasher.Hash(result.State));
        Assert.Equal(snapshots, JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(result.State)));
        Assert.Equal(prompts, JsonSerializer.Serialize(ResolutionResult.BuildPrompts(result.State)));
    }

    private static MatchState Position() => Put(Create(), "MEDITATION-SOURCE", Meditation, "P1", "HAND");

    private static PlayCardCommand Play(string[] costs) => new("MEDITATION-SOURCE", Meditation, [], OptionalCosts: costs);

    private static PlayCostQuoteDto Quote(MatchState state, PlayCardCommand command)
        => new CoreRuleEngine().PreviewPlayCard(state, "P1", PlayCostPreviewTests.Request(state, command));

    private static ActionPromptCandidateDto PlayCandidate(ActionPromptDto prompt)
        => Assert.Single(prompt.Candidates ?? [], candidate => candidate.Action == CommandTypes.PlayCard);

    private static IReadOnlyDictionary<string, object?> Requirement(ActionPromptCandidateDto play)
    {
        var rows = Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(play.Metadata!["sourceRequirements"]);
        return Assert.Single(rows, row => Equals(row["sourceObjectId"], "MEDITATION-SOURCE"));
    }
}
