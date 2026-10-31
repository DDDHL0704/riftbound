using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class MandatoryRecastMixedSourcesTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    [Theory]
    [InlineData("SFD·026/221", "decline")]
    [InlineData("SFD·026a/221", "decline")]
    [InlineData("SFD·026/221", "pay")]
    [InlineData("SFD·026a/221", "pay")]
    [InlineData("SFD·026/221", "free")]
    [InlineData("SFD·026a/221", "free")]
    [InlineData("SFD·026/221", "free-only")]
    [InlineData("SFD·026a/221", "free-only")]
    [InlineData("SFD·026/221", "paid-only")]
    [InlineData("SFD·026a/221", "paid-only")]
    public async Task RefusingAChosenNonzeroPaymentDoesNotRequireAllOtherCandidatesToHaveAResourceFee(string card, string route)
    {
        // Declared midgame component, not an empty-room/deck-origin witness.
        // All conquest/recycle steps below are actual engine commands.
        var initial = Position(card, route);
        var engine = new CoreRuleEngine(); var state = initial;
        var trace = new List<object>();
        PlayCostQuoteDto? freeQuote = null, paidQuote = null;
        ResolutionResult? coreDecline = null, nativeDecline = null;
        ActionPromptDto? pendingPrompt = null;
        try
        {
            await Act("P1", new MoveUnitCommand("UNIT", "BASE", "BATTLEFIELD:BF", []));
            await Act("P1", new PassFocusCommand());
            await Act("P2", new PassFocusCommand());
            Assert.Equal(1, state.PlayerScores["P1"]);
            while (state.StackItems.Count > 0 && state.PendingCardChoice is null)
                await Act(state.PriorityPlayerId!, new PassPriorityCommand());
            var recycle = Assert.IsType<PendingCardChoiceState>(state.PendingCardChoice);
            Assert.Equal("RECYCLE_FOR_EFFECT_PLAY", recycle.ChoiceWindow);
            await Act("P1", new ChooseCardsCommand(recycle.ChoiceId, recycle.ChoiceWindow, ["B"]));
            var pending = Assert.IsType<PendingEffectPlayState>(state.PendingEffectPlay);
            Assert.False(pending.Optional); Assert.False(pending.IgnoreBaseMana); Assert.False(pending.IgnoreBasePower);
            Assert.Equal(4, pending.ManaReduction);
            Assert.Contains("B", state.PlayerZones["P1"].MainDeck);
            Assert.DoesNotContain("B", state.PlayerZones["P1"].Base);
            var free = new PlayCardCommand("FREE", card, []);
            var paid = new PlayCardCommand("MECH", "SFD·075/221", []);
            if (route != "paid-only")
            {
                freeQuote = engine.PreviewPlayCard(state, "P1", PlayCostPreviewTests.Request(state, free));
                Assert.True(freeQuote.CanPay, freeQuote.Message);
                Assert.Equal(0, freeQuote.Cost!.Mana);
                Assert.Equal(0, freeQuote.Cost.GenericPower + freeQuote.Cost.PowerByTrait.Values.Sum());
            }
            if (route != "free-only")
            {
                paidQuote = engine.PreviewPlayCard(state, "P1", PlayCostPreviewTests.Request(state, paid));
                Assert.True(paidQuote.CanPay, paidQuote.Message);
                Assert.Equal(0, paidQuote.Cost!.Mana);
                Assert.Equal(1, paidQuote.Cost.PowerByTrait["blue"]);
            }
            pendingPrompt = ResolutionResult.BuildPrompts(state)["P1"];
            var before = MatchStateHasher.Hash(state);
            var pool = MatchStateHasher.HashValue(state.RunePools["P1"]);
            if (route is "decline" or "paid-only" or "free-only")
            {
                var command = new ChooseCardsCommand(pending.ChoiceId, "EFFECT_PLAY", []);
                coreDecline = await engine.ResolveAsync(state, new("mixed-core", "P1", command.CmdType), command, default);
                var session = new MatchSession(state, engine, NoopMatchJournal.Instance);
                var raw = JsonSerializer.SerializeToNode(command, Json)!.AsObject();
                var wire = JsonSerializer.SerializeToElement(raw, Json);
                nativeDecline = await session.SubmitAsync("P1", "mixed-native", GameCommandJsonMapper.Map(wire), wire, default);
                var duplicate = await session.SubmitAsync("P1", "mixed-native", GameCommandJsonMapper.Map(wire), wire, default);
                Assert.Equal(nativeDecline.Accepted, duplicate.Accepted);
                Assert.Equal(MatchStateHasher.Hash(nativeDecline.State), MatchStateHasher.Hash(duplicate.State));
                FullGameEndToEndTests.AssertNoHiddenZoneLeak(nativeDecline);
                if (route == "free-only")
                {
                    Assert.False(coreDecline.Accepted); Assert.False(nativeDecline.Accepted);
                    Assert.Equal(ErrorCodes.InvalidTarget, nativeDecline.ErrorCode);
                    Assert.Equal(before, MatchStateHasher.Hash(coreDecline.State));
                    Assert.Equal(before, MatchStateHasher.Hash(nativeDecline.State));
                    Assert.Empty(coreDecline.Events); Assert.Empty(nativeDecline.Events);
                    Assert.DoesNotContain(pendingPrompt.Candidates!, c => c.Action == CommandTypes.ChooseCards && c.Enabled);
                    await Act("P1", free);
                }
                else
                {
                    // Both real results are captured before asserting either.
                    Assert.True(coreDecline.Accepted, coreDecline.ErrorMessage);
                    Assert.True(nativeDecline.Accepted, nativeDecline.ErrorMessage);
                    Assert.Contains(pendingPrompt.Candidates!, c => c.Action == CommandTypes.ChooseCards && c.Enabled);
                    Assert.Equal(pool, MatchStateHasher.HashValue(nativeDecline.State.RunePools["P1"]));
                    Assert.Null(nativeDecline.State.PendingEffectPlay);
                    Assert.Contains("MECH", nativeDecline.State.PlayerZones["P1"].Graveyard);
                    if (route == "decline") Assert.Contains("FREE", nativeDecline.State.PlayerZones["P1"].Graveyard);
                    state = nativeDecline.State;
                }
            }
            else await Act("P1", route == "pay" ? paid : free);
            Assert.Null(state.PendingEffectPlay);
            Assert.Contains("B", state.PlayerZones["P1"].MainDeck);
            Assert.Equal(route == "pay" ? 0 : 1, state.RunePools["P1"].TotalPower);
            if (route is "free" or "free-only") Assert.Contains("FREE", state.PlayerZones["P1"].Base);
            if (route == "pay") Assert.Contains("MECH", state.PlayerZones["P1"].Base);
            Assert.Equal(1, state.PlayerScores["P1"]); Assert.Equal(MatchStatuses.InProgress, state.Status);
            Assert.Null(state.WinnerPlayerId);
        }
        finally
        {
            if (Environment.GetEnvironmentVariable("RIFTBOUND_MIXED_RECAST_EVIDENCE") is { Length: > 0 } output)
            {
                Directory.CreateDirectory(output);
                await File.WriteAllTextAsync(Path.Combine(output, (card.Contains("026a") ? "alt" : "base") + "-" + route + ".json"),
                    JsonSerializer.Serialize(new { scope = "Declared midgame component. Actual conquest, priority and nonrefundable recycle; mixed default-zero and funded nonzero routes. Native decline is a real mapped prompt/tick session command, with duplicate/privacy checks. No empty-room, full-origin, SQL/network/restart or whole-card certification.", initial, card, route, trace, freeQuote, paidQuote, pendingPrompt, coreDecline, nativeDecline, final = state }, Json));
            }
        }

        async Task Act(string player, GameCommand command)
        {
            var result = await engine.ResolveAsync(state, new("mixed-" + trace.Count, player, command.CmdType), command, default);
            trace.Add(new { player, command, result });
            Assert.True(result.Accepted, result.ErrorMessage);
            FullGameEndToEndTests.AssertNoHiddenZoneLeak(result);
            state = result.State;
        }
    }

    private static MatchState Position(string card, string route)
    {
        var state = ConquestLifecycleRegressionTests.State("OGN·296/298", 0);
        var cards = new Dictionary<string, CardObjectState> {
            ["BF"] = state.CardObjects["BF"],
            ["UNIT"] = Rumble("UNIT"), ["B"] = Rumble("B") };
        var graveyard = new List<string>();
        if (route != "paid-only") { cards["FREE"] = Rumble("FREE"); graveyard.Add("FREE"); }
        if (route != "free-only")
        {
            cards["MECH"] = new("MECH", cardNo: "SFD·075/221", ownerId: "P1", controllerId: "P1", power: 3,
                manaCost: 4, tags: [CardObjectTags.UnitCard, "机械", "COLOR:blue"]);
            graveyard.Add("MECH");
        }
        var locations = cards.Keys.ToDictionary(id => id, id => id == "BF" ? new ObjectLocationState("P2", "BATTLEFIELD", "BF")
            : new ObjectLocationState("P1", graveyard.Contains(id) ? "GRAVEYARD" : "BASE"));
        return state with { CardObjects = cards, ObjectLocations = locations,
            PlayerZones = new Dictionary<string, PlayerZones> { ["P1"] = PlayerZones.Empty with { Base = ["UNIT", "B"], Graveyard = graveyard },
                ["P2"] = PlayerZones.Empty with { Battlefields = ["BF"] } },
            RunePools = new Dictionary<string, RunePool> { ["P1"] = new(0, 1), ["P2"] = RunePool.Empty } };

        CardObjectState Rumble(string id) => new(id, cardNo: card, ownerId: "P1", controllerId: "P1", power: 4,
            manaCost: 4, tags: [CardObjectTags.UnitCard, "机械", "约德尔人", "COLOR:red"]);
    }
}
