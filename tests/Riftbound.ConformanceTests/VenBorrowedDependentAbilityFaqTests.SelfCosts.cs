using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed partial class VenBorrowedDependentAbilityFaqTests
{
    [Theory]
    [InlineData("VEN·054", "EQUIPMENT", true)]
    [InlineData("UNL-203/219", "LEGEND", false)]
    [InlineData("UNL-237/219", "LEGEND", false)]
    [InlineData("UNL-237*/219", "LEGEND", false)]
    public async Task BorrowedDrawPaysTheBorrowersEmpowermentOrExperience(string printing, string category, bool empower)
    {
        var game = await WithTemplate(printing, category, empower: empower);
        var id = TemplateId(game.State, "抽 1 张牌");
        var before = game.State;
        await game.Act("P1", new ActivateAbilityCommand("HOST", id, []));
        Assert.Equal(before.PlayerZones["P1"].MainDeck, game.State.PlayerZones["P1"].MainDeck);
        Assert.False(game.State.CardObjects["HOST"].IsEmpowered);
        Assert.False(game.State.CardObjects["DONOR"].IsEmpowered);
        Assert.True(game.State.CardObjects["HOST"].IsExhausted);
        Assert.Equal(before.PlayerExperience["P1"] - (empower ? 0 : 3), game.State.PlayerExperience["P1"]);
        Assert.Equal(before.RunePools["P1"].Mana - (empower ? 1 : 0), game.State.RunePools["P1"].Mana);
        await game.Top();
        Assert.Equal(before.PlayerZones["P1"].MainDeck[0], game.State.PlayerZones["P1"].Hand[^1]);
        Assert.Equal(before.PlayerZones["P1"].MainDeck.Skip(1), game.State.PlayerZones["P1"].MainDeck);
        var bad = await new CoreRuleEngine().ResolveAsync(before,
            new("borrowed-draw-extra-target", "P1", CommandTypes.ActivateAbility),
            new ActivateAbilityCommand("HOST", id, ["ALLY"]), default);
        Assert.False(bad.Accepted);
        Assert.Equal(MatchStateHasher.Hash(before), MatchStateHasher.Hash(bad.State));
        var insufficient = empower ? before with { CardObjects = new Dictionary<string, CardObjectState>(before.CardObjects) {
                ["HOST"] = before.CardObjects["HOST"] with { IsEmpowered = false } } }
            : before with { PlayerExperience = new Dictionary<string, int>(before.PlayerExperience) { ["P1"] = 2 } };
        bad = await new CoreRuleEngine().ResolveAsync(insufficient,
            new("borrowed-draw-cost-invalid", "P1", CommandTypes.ActivateAbility), new ActivateAbilityCommand("HOST", id, []), default);
        Assert.False(bad.Accepted);
        Assert.Empty(bad.Events);
        Assert.Equal(MatchStateHasher.Hash(insufficient), MatchStateHasher.Hash(bad.State));
        await game.Replay();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BorrowedShellPaysSelfDestructionBeforeResolvingItsOriginalTarget(bool targetSelf)
    {
        var game = await WithTemplate("UNL-161/219", "EQUIPMENT");
        var id = TemplateId(game.State, "战力 +2");
        var target = targetSelf ? "HOST" : "ALLY";
        var generation = game.State.CardObjects[target].ObjectGeneration;
        var bad = await new CoreRuleEngine().ResolveAsync(game.State,
            new("borrowed-shell-invalid", "P1", CommandTypes.ActivateAbility), new ActivateAbilityCommand("HOST", id, ["GEAR"]), default);
        Assert.False(bad.Accepted);
        Assert.Equal(MatchStateHasher.Hash(game.State), MatchStateHasher.Hash(bad.State));
        await game.Act("P1", new ActivateAbilityCommand("HOST", id, [target]));
        Assert.Contains("HOST", game.State.PlayerZones["P1"].Graveyard);
        Assert.Contains("DONOR", game.State.PlayerZones["P1"].Base);
        Assert.Equal(generation, Assert.Single(game.State.StackItems).TargetGenerations![target]);
        await game.Top();
        Assert.Equal(targetSelf ? 0 : 2, game.State.CardObjects["ALLY"].UntilEndOfTurnPowerModifier);
        Assert.Equal(0, game.State.CardObjects["HOST"].UntilEndOfTurnPowerModifier);
        await game.Replay();
    }

    [Fact]
    public async Task BorrowedDestructionCostMayBeReplacedByHourglassAndStillDrawsOnce()
    {
        var game = await WithTemplate("SFD·046/221", "EQUIPMENT", pool:
            new(8, 4, new Dictionary<string, int> { ["green"] = 4 }), adjust: initial =>
                HandDiscardObserverTests.Put(initial, "HOURGLASS", "OGN·077/298", "P1", "BASE"));
        var id = TemplateId(game.State, "抽 1 张牌");
        var before = game.State;
        await game.Act("P1", new ActivateAbilityCommand("HOST", id, []));
        Assert.Contains("HOURGLASS", game.State.PlayerZones["P1"].Graveyard);
        Assert.Contains("HOST", game.State.PlayerZones["P1"].Base);
        Assert.True(game.State.CardObjects["HOST"].IsExhausted);
        Assert.Equal(before.RunePools["P1"].Mana - 1, game.State.RunePools["P1"].Mana);
        await game.Top();
        Assert.Equal(before.PlayerZones["P1"].Hand.Count + 1, game.State.PlayerZones["P1"].Hand.Count);
        Assert.Contains("HOST", game.State.PlayerZones["P1"].Base);
        await game.Replay();
    }

    [Theory]
    [InlineData("SFD·046/221")]
    [InlineData("UNL-161/219")]
    public async Task BorrowedSelfDestructionCapturesTheSameTickTokenDeparture(string donor)
    {
        var initial = HandDiscardObserverTests.Put(AttachedRuleTextTests.Position("OGN·111/298", 0),
            "DONOR", donor, "P1", "BASE");
        initial = OfficialHoldSequenceTests.AddUnit(initial, "ALLY", "OGN·001/298", "F", "P1");
        var source = initial.CardObjects["HOST"];
        initial = initial with {
            RunePools = new Dictionary<string, RunePool>(initial.RunePools) { ["P1"] = new(8, 4, new Dictionary<string, int> { ["green"] = 4 }) },
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["DONOR"] = initial.CardObjects["DONOR"] with { IsExhausted = true },
            ["HOST"] = source with { TokenFactoryCardNo = P6TokenFactoryCatalog.ImageTokenCardNo,
                CopyFaceTransitions = [new(0, source.ObjectGeneration, P6TokenFactoryCatalog.ImageTokenCardNo, source.CardNo!)] } } };
        var game = new VenBasicFlowSpellTests.Driver(initial);
        var id = TemplateId(initial, donor == "SFD·046/221" ? "抽 1 张牌" : "战力 +2");
        var targets = donor == "SFD·046/221" ? Array.Empty<string>() : ["ALLY"];
        await game.Act("P1", new ActivateAbilityCommand("HOST", id, targets));
        Assert.False(game.State.CardObjects.ContainsKey("HOST"));
        var item = Assert.Single(game.State.StackItems);
        var context = item.BorrowedExhaust!;
        var receipt = Assert.IsType<CeasedSelfAbilitySource>(context.Borrower.CeasedSource);
        Assert.Equal(context.AcquiredTick, receipt.DepartedTick);
        foreach (var forged in new CeasedSelfAbilitySource?[] { null,
            receipt with { AcquiredTick = receipt.AcquiredTick - 1 },
            receipt with { DepartedTick = receipt.AcquiredTick - 1 },
            receipt with { DepartedTick = game.State.Tick + 1 },
            receipt with { Source = receipt.Source with { Generation = receipt.Source.Generation + 1 } } })
            Assert.Contains("invalid captured borrowed skill", OfficialInsightAndSpellLockTests.Errors(game.State with {
                StackItems = [item with { BorrowedExhaust = context with { Borrower = context.Borrower with { CeasedSource = forged } } }] }));
        await game.Top();
        Assert.Empty(game.State.StackItems);
        await game.Replay();
    }

    [Fact]
    public async Task BorrowedPoroTreatsDestroyThisCardAsTheBorrowersCostAndThenDraws()
    {
        var game = await WithTemplate("SFD·046/221", "EQUIPMENT", pool:
            new(8, 4, new Dictionary<string, int> { ["green"] = 4 }));
        var id = TemplateId(game.State, "抽 1 张牌");
        var beforeHand = game.State.PlayerZones["P1"].Hand.Count;
        var beforeMana = game.State.RunePools["P1"];
        await game.Act("P1", new ActivateAbilityCommand("HOST", id, []));
        Assert.Contains("HOST", game.State.PlayerZones["P1"].Graveyard);
        Assert.Equal("GRAVEYARD", game.State.ObjectLocations["HOST"].Zone);
        Assert.Single(game.State.StackItems);
        Assert.Equal(beforeMana with { Mana = beforeMana.Mana - 1,
            PowerByTrait = new Dictionary<string, int> { ["green"] = 3 } }, game.State.RunePools["P1"]);
        await game.Top();
        Assert.Equal(beforeHand + 1, game.State.PlayerZones["P1"].Hand.Count);
        Assert.Empty(game.State.StackItems);
        await game.Replay();
    }

    [Theory]
    [InlineData("wrong-target")]
    [InlineData("no-green")]
    [InlineData("no-mana")]
    public async Task BorrowedPoroRejectsIllegalDestroyAndDrawCostsAtomically(string boundary)
    {
        var game = await WithTemplate("SFD·046/221", "EQUIPMENT", pool:
            new(8, 4, new Dictionary<string, int> { ["green"] = 4 }));
        var id = TemplateId(game.State, "抽 1 张牌");
        var state = game.State;
        if (boundary == "no-green")
            state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(8, 0, new Dictionary<string, int> { ["blue"] = 4 }) } };
        if (boundary == "no-mana")
            state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(0, 4, new Dictionary<string, int> { ["green"] = 4 }) } };
        var hash = MatchStateHasher.Hash(state);
        var result = await new CoreRuleEngine().ResolveAsync(state,
            new("borrowed-self-cost-invalid", "P1", CommandTypes.ActivateAbility),
            new ActivateAbilityCommand("HOST", id, boundary == "wrong-target" ? ["ALLY"] : []), default);
        Assert.False(result.Accepted);
        Assert.Empty(result.Events);
        Assert.Equal(hash, MatchStateHasher.Hash(result.State));
        Assert.DoesNotContain("HOST", result.State.PlayerZones["P1"].Graveyard);
        Assert.False(result.State.CardObjects["HOST"].IsExhausted);
    }
}
