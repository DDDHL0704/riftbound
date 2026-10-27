using System.Reflection;
using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed partial class Ven035DelayedEmpowerConsumerTests
{
    [Theory]
    [InlineData("P1")] [InlineData("P2")]
    public async Task TargetLegalityDirectCoreRejectsAnUnempoweredRemovalTarget(string owner)
    {
        var initial = Position(owner); var game = new Driver(initial); ResolutionResult? result = null;
        try
        {
            var unit = await PrepareTarget(game, owner, false); await game.Act("P1", new RecycleRuneCommand("STATUS-RUNE-1"));
            var before = MatchStateHasher.Hash(game.State);
            result = await new CoreRuleEngine().ResolveAsync(game.State, new("ven035-target-core", "P1", CommandTypes.PlayCard),
                new PlayCardCommand("SPELL", No, [unit], Mode: Remove), default);
            Assert.False(result.Accepted); Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode); Assert.Empty(result.Events);
            Assert.Equal(before, MatchStateHasher.Hash(result.State));
        }
        finally { await SaveTargetLegality("core-" + owner, initial, game, result); }
    }

    [Theory]
    [InlineData("P1")] [InlineData("P2")]
    public async Task TargetLegalityPreviewRejectsAnUnempoweredRemovalTarget(string owner)
    {
        var initial = Position(owner); var game = new Driver(initial); PlayCostQuoteDto? quote = null;
        try
        {
            var unit = await PrepareTarget(game, owner, false); await game.Act("P1", new RecycleRuneCommand("STATUS-RUNE-1"));
            var before = MatchStateHasher.Hash(game.State);
            quote = TargetQuote(game, new PlayCardCommand("SPELL", No, [unit], Mode: Remove));
            Assert.False(quote.IsValid); Assert.False(quote.CanPay); Assert.Equal(ErrorCodes.InvalidTarget, quote.ErrorCode);
            Assert.Equal(before, MatchStateHasher.Hash(game.State));
        }
        finally { await SaveTargetLegality("quote-" + owner, initial, game, quote); }
    }

    [Theory]
    [InlineData("P1")] [InlineData("P2")]
    public async Task TargetLegalitySessionRejectsAnUnempoweredRemovalTargetWithCurrentPrompt(string owner)
    {
        var initial = Position(owner); var game = new Driver(initial);
        try
        {
            var unit = await PrepareTarget(game, owner, false); await game.Act("P1", new RecycleRuneCommand("STATUS-RUNE-1"));
            var command = new PlayCardCommand("SPELL", No, [unit], Mode: Remove);
            var raw = JsonSerializer.SerializeToNode(command, command.GetType(), Json)!.AsObject();
            raw["promptId"] = game.PromptFor("P1").PromptId; raw["snapshotTick"] = game.State.Tick;
            await game.ActRetained(new("P1", "ven035-target-session", JsonSerializer.SerializeToElement(raw, Json)), false);
            Assert.Equal(ErrorCodes.InvalidTarget, game.Commands()[^1].ErrorCode);
            Assert.Equal(CommandTypes.PlayCard, game.Commands()[^1].CommandType);
        }
        finally { await SaveTargetLegality("session-" + owner, initial, game, game.LastRetainedResult); }
    }

    [Theory]
    [InlineData(false, false, "P1")] [InlineData(false, true, "P1")] [InlineData(true, true, "P1")]
    [InlineData(false, false, "P2")] [InlineData(false, true, "P2")] [InlineData(true, true, "P2")]
    public async Task TargetLegalityValidModeQuotesAndPaysExactlyThreeManaAndOneGreen(bool remove, bool empowered, string owner)
    {
        var initial = Position(owner); var game = new Driver(initial); PlayCostQuoteDto? quote = null;
        try
        {
            var unit = await PrepareTarget(game, owner, empowered); await game.Act("P1", new RecycleRuneCommand("STATUS-RUNE-1"));
            var before = MatchStateHasher.Hash(game.State); var pool = game.State.RunePools["P1"]; var events = game.Events().Length;
            var command = new PlayCardCommand("SPELL", No, [unit], Mode: remove ? Remove : Empower);
            quote = TargetQuote(game, command);
            Assert.True(quote.IsValid, quote.Message); Assert.True(quote.CanPay, quote.Message);
            Assert.Equal(3, quote.Cost!.Mana); Assert.Equal(0, quote.Cost.GenericPower);
            Assert.Equal(new Dictionary<string, int> { [RuneTrait.Green] = 1 }, quote.Cost.PowerByTrait);
            Assert.Equal(before, MatchStateHasher.Hash(game.State)); await game.Act("P1", command);
            Assert.Equal(pool.Mana - 3, game.State.RunePools["P1"].Mana); Assert.Equal(pool.TotalPower - 1, game.State.RunePools["P1"].TotalPower);
            Assert.Equal(0, game.State.RunePools["P1"].PowerByTrait.GetValueOrDefault(RuneTrait.Green));
            var item = Assert.Single(game.State.StackItems); Assert.Equal([unit], item.TargetObjectIds);
            Assert.Equal(remove ? "TEMPORARY_REMOVE_EMPOWER_UNIT" : "TEMPORARY_EMPOWER_UNIT", item.EffectKind);
            Assert.Single(game.Events().Skip(events), e => e.Event.Kind == "COST_PAID");
            Assert.Equal(empowered, game.State.CardObjects[unit].IsEmpowered);
        }
        finally { await SaveTargetLegality($"legal-{remove}-{empowered}-{owner}", initial, game, quote); }
    }

    [Theory]
    [InlineData(false, "P1")] [InlineData(true, "P1")] [InlineData(false, "P2")] [InlineData(true, "P2")]
    public async Task TargetLegalityPromptFiltersEachModeAgainstTheActualEmpowerState(bool empowered, string owner)
    {
        var initial = Position(owner); var game = new Driver(initial); ActionPromptDto? prompt = null;
        try
        {
            var unit = await PrepareTarget(game, owner, empowered); await game.Act("P1", new RecycleRuneCommand("STATUS-RUNE-1"));
            var before = MatchStateHasher.Hash(game.State); prompt = game.PromptFor("P1");
            var play = Assert.Single(prompt.Candidates!, c => c.Action == CommandTypes.PlayCard);
            var rows = Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(play.Metadata!["sourceRequirements"])
                .Where(r => Equals(r["sourceObjectId"], "SPELL")).ToArray();
            Assert.Contains(unit, TargetIds(Assert.Single(rows, r => Equals(r["mode"], Empower))));
            if (empowered) Assert.Contains(unit, TargetIds(Assert.Single(rows, r => Equals(r["mode"], Remove))));
            else Assert.DoesNotContain(rows, r => Equals(r["mode"], Remove));
            Assert.Equal(before, MatchStateHasher.Hash(game.State));
        }
        finally { await SaveTargetLegality($"prompt-{empowered}-{owner}", initial, game, prompt); }
    }

    [Theory]
    [InlineData(false, "P1")] [InlineData(true, "P1")] [InlineData(false, "P2")] [InlineData(true, "P2")]
    public async Task TargetLegalityResolutionFilterPreservesOnlyCurrentlyEmpoweredTargetSlots(bool empowered, string owner)
    {
        var initial = Position(owner); var game = new Driver(initial); object? observation = null;
        try
        {
            var unit = await PrepareTarget(game, owner, empowered); var before = MatchStateHasher.Hash(game.State);
            Assert.True(CardBehaviorRegistry.TryGetByCardNoAndMode(No, Remove, out var behavior));
            // Pure resolution-filter probe over real status commands; never appended to accepted history.
            var input = new StackItemState("ven035-target-filter", "P1", "SPELL", behavior.EffectKind, No, [unit],
                targetGenerations: new Dictionary<string, long> { [unit] = game.State.CardObjects[unit].ObjectGeneration });
            var method = typeof(CoreRuleEngine).GetMethod("MaskTargetsNoLongerLegal", BindingFlags.NonPublic | BindingFlags.Static)!;
            var masked = Assert.IsType<StackItemState>(method.Invoke(null, [game.State, input, behavior]));
            observation = new { input, masked };
            Assert.Equal([empowered ? unit : string.Empty], masked.TargetObjectIds);
            Assert.Equal(before, MatchStateHasher.Hash(game.State));
        }
        finally { await SaveTargetLegality($"filter-{empowered}-{owner}", initial, game, observation); }
    }

    private static PlayCostQuoteDto TargetQuote(Driver game, PlayCardCommand command)
        => new CoreRuleEngine().PreviewPlayCard(game.State, "P1", new("ven035-target-quote", game.PromptFor("P1").PromptId!, game.State.Tick, command));

    private static string[] TargetIds(IReadOnlyDictionary<string, object?> row)
        => Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(
            Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(row["targetChoicesByIndex"])["0"]).Select(c => c.Id).ToArray();

    private static async Task SaveTargetLegality(string label, MatchState initial, Driver game, object? observation)
    {
        if (Environment.GetEnvironmentVariable("RIFTBOUND_VEN035_EVIDENCE") is not { Length: > 0 } output) return;
        Directory.CreateDirectory(output);
        await File.WriteAllTextAsync(Path.Combine(output, "target-legality-" + label + ".json"), JsonSerializer.Serialize(new {
            scope = "VEN035 target legality only. Status comes from real empowerment commands. Quote/Core probes and synthetic resolution-filter input are explicitly separate from session history. A failing session assertion may leave driverState before the observed response; observation retains that full response. Effects, delayed reversals, complete card, L2 and L3 remain OPEN.",
            initial, driverState = game.State, commands = game.Commands(), events = game.Events(), observation }, Json));
    }
}
