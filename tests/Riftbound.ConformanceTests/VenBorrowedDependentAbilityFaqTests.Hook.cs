using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed partial class VenBorrowedDependentAbilityFaqTests
{
    private static MatchState BorrowedHookPosition(bool token = false)
    {
        var state = BorrowedBombPosition("OGN·242/298");
        var deck = state.PlayerZones["P1"].MainDeck.ToHashSet();
        var cards = state.CardObjects.Where(p => !deck.Contains(p.Key)).ToDictionary();
        var locations = state.ObjectLocations.Where(p => !deck.Contains(p.Key)).ToDictionary();
        foreach (var (id, no) in new[] { ("HOOK-UNIT", "OGN·096/298"), ("HOOK-GEAR", "SFD·042/221") })
        {
            cards[id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, no, "P1");
            locations[id] = new("P1", "MAIN_DECK");
        }
        if (token) cards["HOST"] = cards["HOST"] with {
            TokenFactoryCardNo = P6TokenFactoryCatalog.ImageTokenCardNo,
            CopyFaceTransitions = [new(0, cards["HOST"].ObjectGeneration,
                P6TokenFactoryCatalog.ImageTokenCardNo, cards["HOST"].CardNo!)] };
        return state with { CardObjects = cards, ObjectLocations = locations,
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P1"] = state.PlayerZones["P1"] with { MainDeck = ["HOOK-UNIT", "HOOK-GEAR"] } },
            RunePools = new Dictionary<string, RunePool>(state.RunePools) {
                ["P1"] = new(20, 0, new Dictionary<string, int> { [RuneTrait.Yellow] = 2 }) } };
    }

    private static ActivateAbilityCommand BorrowedHook(MatchState state, string target = "ALLY")
        => new("HOST", TemplateId(state, "查看牌堆顶部五张牌"), [target]);

    private static async Task SelectBorrowedHook(Driver game, bool decline = false)
    {
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        Assert.Equal("DECK_PLAY_SELECTION", choice.ChoiceWindow);
        Assert.Equal(["HOOK-UNIT"], choice.LegalObjectIds);
        Assert.Equal(0, choice.RequiredCount);
        await RadGearTestPosition.Reject(game.State, "P2", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["HOOK-UNIT"]));
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, decline ? [] : ["HOOK-UNIT"]));
    }

    // VEN FAQ p.15 / 727.1.b.1: inherit the full skill. Its friendly
    // unit can be the borrower, and its source need not be base equipment.
    [Theory]
    [InlineData("battlefield")]
    [InlineData("self")]
    [InlineData("self-token")]
    [InlineData("source-return")]
    [InlineData("source-token-return")]
    public async Task BorrowedHookUsesSharedPrivateFreePlayAfterSourceOrTargetDeparture(string mode)
    {
        var initial = BorrowedHookPosition(mode.Contains("token", StringComparison.Ordinal));
        if (mode == "battlefield") initial = RadUnitEffectTestFixture.Place(initial, "HOST", "P1", "BATTLEFIELD", "BF");
        var target = mode.StartsWith("self", StringComparison.Ordinal) ? "HOST" : "ALLY";
        var command = BorrowedHook(initial, target);
        foreach (var illegal in new[] { "ENEMY", "GEAR", "UNIT", "HOOK-UNIT" })
            await RadGearTestPosition.Reject(initial, "P1", command with { TargetObjectIds = [illegal] });
        await RadGearTestPosition.Reject(initial, "P2", command);
        await RadGearTestPosition.Reject(initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) {
            ["P1"] = new(20, 0, new Dictionary<string, int> { [RuneTrait.Blue] = 2 }) } }, "P1", command);
        var game = new Driver(initial);
        await game.Act("P1", command);
        Assert.True(game.State.CardObjects["HOST"].IsExhausted);
        Assert.Equal(19, game.State.RunePools["P1"].Mana);
        Assert.Equal(1, game.State.RunePools["P1"].PowerByTrait[RuneTrait.Yellow]);
        if (mode.Contains("return", StringComparison.Ordinal))
        {
            await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["HOST"]));
            await game.Top();
        }
        await game.Top();
        if (mode == "self-token") Assert.False(game.State.CardObjects.ContainsKey("HOST"));
        else Assert.Contains(target, game.State.PlayerZones["P1"].Graveyard);
        var parent = Assert.Single(game.State.StackItems);
        Assert.True(parent.DestroyDeckPlay!.Prepared);
        Assert.NotNull(parent.DestroyDeckPlay.KilledMight);
        if (!game.State.CardObjects.ContainsKey("HOST"))
        {
            Assert.NotNull(parent.BorrowedExhaust!.Borrower.CeasedSource);
            var missingReceipt = parent with { BorrowedExhaust = parent.BorrowedExhaust with {
                Borrower = parent.BorrowedExhaust.Borrower with { CeasedSource = null } } };
            Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(game.State with { StackItems = [missingReceipt] }));
        }
        if (mode == "source-return")
            Assert.Equal("HIDDEN", Assert.Single(game.Events(), e => e.Event.Kind == "UNIT_DESTROYED"
                && Equals(e.Event.Payload.GetValueOrDefault("targetObjectId"), "ALLY")).Event.Payload["sourceObjectId"]);
        foreach (var viewer in new[] { "P2", ProjectionViewers.Spectator })
        {
            var projected = viewer == "P2" ? ResolutionResult.BuildSnapshots(game.State)[viewer]
                : ResolutionResult.BuildSpectatorSnapshot(game.State);
            var text = JsonSerializer.Serialize(projected);
            Assert.DoesNotContain("HOOK-UNIT", text);
            Assert.DoesNotContain("HOOK-GEAR", text);
        }
        await SelectBorrowedHook(game);
        Assert.False(game.State.PendingEffectPlay!.IgnoreAllCosts);
        var paid = MatchStateHasher.HashValue(game.State.RunePools);
        await game.Act("P1", new PlayCardCommand("HOOK-UNIT", "OGN·096/298", [], Destination: "BASE"));
        Assert.Equal(paid, MatchStateHasher.HashValue(game.State.RunePools));
        Assert.Contains("HOOK-UNIT", game.State.PlayerZones["P1"].Base);
        Assert.Contains("HOOK-GEAR", game.State.PlayerZones["P1"].MainDeck);
        Assert.Null(game.State.PendingEffectPlay);
        Assert.Empty(game.State.StackItems);
        await game.Replay();
    }

    [Fact]
    public async Task BorrowedHookCanDeclineAndRecycleEveryPrivatelyLookedCard()
    {
        var game = new Driver(BorrowedHookPosition());
        await game.Act("P1", BorrowedHook(game.State));
        await game.Top();
        await SelectBorrowedHook(game, decline: true);
        Assert.Equal(2, game.State.PlayerZones["P1"].MainDeck.Count);
        Assert.Empty(game.State.PlayerZones["P1"].Banished);
        Assert.Null(game.State.PendingEffectPlay);
        Assert.Empty(game.State.StackItems);
        await game.Replay();
    }

    [Fact]
    public async Task BorrowedHookStillLooksAndRecyclesWhenAResponseReturnsTheTarget()
    {
        var game = new Driver(BorrowedHookPosition());
        await game.Act("P1", BorrowedHook(game.State));
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["ALLY"]));
        await game.Top();
        await game.Top();
        Assert.Contains("ALLY", game.State.PlayerZones["P1"].Hand);
        Assert.Null(game.State.PendingCardChoice);
        Assert.Null(game.State.PendingEffectPlay);
        Assert.Equal(2, Assert.Single(game.Events(), e => e.Event.Kind == "CARDS_RECYCLED").Event.Payload["count"]);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "UNIT_PLAYED_TO_BASE");
        await game.Replay();
    }

    [Fact]
    public async Task BorrowedHookReplacementSuppliesNoDestroyedUnitMight()
    {
        var initial = HandDiscardObserverTests.Put(BorrowedHookPosition(), "WEAPON", "OGN·023/298", "P1", "BASE");
        initial = HandDiscardObserverTests.Put(initial, "RECALL-COST", "OGN·048/298", "P1", "HAND");
        initial = initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) {
            ["P1"] = new(20, 0, new Dictionary<string, int> { [RuneTrait.Yellow] = 1, [RuneTrait.Red] = 1 }) } };
        var game = new Driver(initial);
        await game.Act("P1", new ActivateAbilityCommand("WEAPON", PaidRecallAbility, ["ALLY"], ["DISCARD_HAND_CARD:RECALL-COST"]));
        await game.Top();
        await game.Act("P1", BorrowedHook(game.State));
        await game.Top();
        await PaySmuggledRecall(game);
        Assert.Contains("ALLY", game.State.PlayerZones["P1"].Base);
        Assert.Null(game.State.PendingCardChoice);
        Assert.Null(game.State.PendingEffectPlay);
        Assert.Equal(2, Assert.Single(game.Events(), e => e.Event.Kind == "CARDS_RECYCLED").Event.Payload["count"]);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "UNIT_DESTROYED");
        await game.Replay();
    }

    [Fact]
    public async Task BorrowedHookRecoveryRequiresBothBorrowedReceiptsAndTheOriginalKillPermission()
    {
        var game = new Driver(BorrowedHookPosition());
        await game.Act("P1", BorrowedHook(game.State));
        await game.Top();
        await SelectBorrowedHook(game);
        var pending = game.State.PendingEffectPlay!;
        var parent = pending.Parent;
        var killed = parent.DestroyDeckPlay!;
        foreach (var forged in new[] {
            parent with { BorrowedExhaust = null },
            parent with { BorrowedExhaust = parent.BorrowedExhaust! with { AbilityIndex = 999 } },
            parent with { DestroyDeckPlay = null },
            parent with { DestroyDeckPlay = killed with { KilledMight = killed.KilledMight + 10 } },
            parent with { DestroyDeckPlay = killed with { AbilityId = PaidRecallAbility } },
            parent with { DestroyDeckPlay = killed with { Source = killed.Target } } })
            Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(game.State with {
                PendingEffectPlay = pending with { Parent = forged } }));
        await game.Act("P1", new PlayCardCommand("HOOK-UNIT", "OGN·096/298", []));
        await game.Replay();
    }
}
