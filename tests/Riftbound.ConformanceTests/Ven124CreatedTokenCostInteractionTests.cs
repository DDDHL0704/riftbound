using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class Ven124CreatedTokenCostInteractionTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    [Theory]
    [InlineData("OGN·096/298", 0, 1, 0)]
    [InlineData("OGN·096/298", 2, 1, 0)]
    [InlineData("OGN·075/298", 2, 1, 0)]
    [InlineData("OGN·178/298", 2, 2, 2)]
    public async Task AnActuallyCreatedCopiedMinionCanCeaseAsTheCostWhileEveryEarnedTextFinishesBeforeEmpower(
        string copiedNo, int copies, int drawCount, int discardCount)
    {
        var initial = Position(copiedNo, copies); var game = new Driver(initial);
        try
        {
            Assert.DoesNotContain(game.State.CardObjects.Values, c => c.Tags.Contains(CardObjectTags.MinionTokenFamily));
            var funding = Enumerable.Range(0, 12).Select(i => "TOKEN-COST-RUNE-" + i).ToArray();
            var recover = game.RecoverAfterEveryCommand;
            game.RecoverAfterEveryCommand = false;
            try
            {
                // The paid carrier and its own empowerment only establish the
                // source used by the later token-cost choice. Keep duplicate,
                // privacy and command acceptance checks on each setup command,
                // then validate the complete prefix once before the dependent
                // token and earned-text assertions.
                await game.Act("P1", new TapRuneCommand(funding[0], funding));
                Assert.Equal(12, game.State.RunePools["P1"].Mana); Assert.Equal(0, game.State.RunePools["P1"].TotalPower);
                await game.Act("P1", new PlayCardCommand("CARRIER", "VEN·128", []));
                Assert.Equal(10, game.State.RunePools["P1"].Mana);
                await game.Act("P1", new RecycleRuneCommand("TOKEN-COST-RUNE-0"));
                await game.Act("P1", new ActivateAbilityCommand("CARRIER", "EMPOWER:0:0", [])); await game.Top();
                await game.ColdCheckpoint();
            }
            finally { game.RecoverAfterEveryCommand = recover; }
            Assert.True(game.State.CardObjects["CARRIER"].IsEmpowered); Assert.Equal(9, game.State.RunePools["P1"].Mana);
            await game.Act("P1", new PlayCardCommand("UNIT", "VEN·124", []));
            await game.Act("P1", new ActivateAbilityCommand("UNIT", "EMPOWER:0:0", [], OptionalCosts: [CostChoice(game.State, "UNIT", "CARRIER")]));
            Assert.Contains("CARRIER", game.State.PlayerZones["P1"].Graveyard);
            Assert.False(game.State.CardObjects["UNIT"].IsEmpowered); await game.Top();
            var minions = game.State.PlayerZones["P1"].Base.Where(id => game.State.CardObjects[id].Tags.Contains(CardObjectTags.MinionTokenFamily)).Order().ToArray();
            Assert.Equal(2, minions.Length);
            Assert.All(minions, id => { Assert.Equal(1, game.State.CardObjects[id].Power); Assert.False(game.State.CardObjects[id].IsEmpowered); });
            await game.Top(); Assert.True(game.State.CardObjects["UNIT"].IsEmpowered);
            var tokenId = minions[0]; var otherToken = minions[1]; var generation = game.State.CardObjects[tokenId].ObjectGeneration;
            var factory = game.State.CardObjects[tokenId].TokenFactoryCardNo;
            Assert.True(P6TokenFactoryCatalog.TryGetByCardNo(factory!, out var definition)); Assert.Equal("随从", definition.TokenFamilyName);
            await game.Act("P1", new RecycleRuneCommand("TOKEN-COST-RUNE-1"));
            await game.Act("P1", new AssembleEquipmentCommand("GEAR", tokenId, ["ASSEMBLE_YELLOW"])); await game.Top();
            var selection = Assert.IsType<RuleChoiceContinuation>(game.State.PendingRuleChoice);
            Assert.Equal("ATTACHMENT_COPY_SELECTION", selection.Request.Kind);
            await game.Act("P1", new PayCostCommand(selection.Request.Id, "RULE_REPLACEMENT", ["COPY:ALLY"]));
            Assert.Equal(copiedNo, game.State.CardObjects[tokenId].CardNo); Assert.Equal(factory, game.State.CardObjects[tokenId].TokenFactoryCardNo);
            Assert.Equal(generation, game.State.CardObjects[tokenId].ObjectGeneration); Assert.False(game.State.CardObjects[tokenId].IsEmpowered);
            for (var i = 0; i < copies; i++)
            {
                await game.Act("P1", new RecycleRuneCommand("TOKEN-COST-RUNE-" + (i + 2)));
                await game.Act("P1", new AssembleEquipmentCommand("SONG-" + i, tokenId, ["ASSEMBLE_GREEN"])); await game.Top();
                Assert.Equal(copiedNo, game.State.CardObjects["SONG-" + i].AttachedRuleText!.CardNo);
                Assert.Equal(generation, game.State.CardObjects["SONG-" + i].AttachedRuleText!.Unit.Generation);
            }
            var returnedRunes = Enumerable.Range(0, 2 + copies).Select(i => "TOKEN-COST-RUNE-" + i).ToArray();
            Assert.Equal(returnedRunes, game.State.PlayerZones["P1"].RuneDeck);
            await game.Act("P1", new PlayCardCommand("SECOND", "VEN·124", []));
            Assert.Equal(2 - copies, game.State.RunePools["P1"].Mana); Assert.Equal(0, game.State.RunePools["P1"].TotalPower);
            var before = game.Events().Length; var handAtCost = game.State.PlayerZones["P1"].Hand.ToArray(); var deckAtCost = game.State.PlayerZones["P1"].MainDeck.ToArray();
            await game.Act("P1", new ActivateAbilityCommand("SECOND", "EMPOWER:0:0", [], OptionalCosts: [CostChoice(game.State, "SECOND", tokenId)]));
            Assert.False(game.State.CardObjects.ContainsKey(tokenId)); Assert.False(game.State.ObjectLocations.ContainsKey(tokenId));
            Assert.DoesNotContain(tokenId, game.State.PlayerZones["P1"].Base); Assert.DoesNotContain(tokenId, game.State.PlayerZones["P1"].Graveyard);
            Assert.Contains(otherToken, game.State.PlayerZones["P1"].Base); Assert.Equal(1, game.State.CardObjects[otherToken].Power);
            Assert.Null(game.State.CardObjects["GEAR"].AttachedToObjectId);
            for (var i = 0; i < copies; i++) Assert.Null(game.State.CardObjects["SONG-" + i].AttachedToObjectId);
            Assert.Single(game.Events().Skip(before), e => e.Event.Kind == "TOKEN_CEASED_TO_EXIST");
            Assert.DoesNotContain(game.Events().Skip(before), e => e.Event.Kind is "CARD_DRAWN" or "CARD_DISCARDED" or "RUNES_CALLED");
            Assert.Equal(handAtCost, game.State.PlayerZones["P1"].Hand); Assert.Equal(deckAtCost, game.State.PlayerZones["P1"].MainDeck);
            Assert.Equal(returnedRunes, game.State.PlayerZones["P1"].RuneDeck);
            var parent = Assert.Single(game.State.StackItems, s => s.EmpowerActivation is not null); var paid = parent.EmpowerActivation!;
            Assert.Equal(new ObjectBinding(tokenId, generation), paid.DestructionCost!.Unit); Assert.Equal(factory, paid.DestructionCost.TokenFactoryCardNo);
            Assert.Equal(paid.AcquiredTick, paid.DestructionCost.TokenCeasedAtTick); Assert.False(game.State.CardObjects["SECOND"].IsEmpowered);
            var queued = game.State.TriggerQueue.Where(t => t.SourceObjectId == tokenId).ToArray();
            Assert.Equal(copies + 1, queued.Length + game.State.StackItems.Count(s => s.SourceObjectId == tokenId));
            if (queued.Length > 0) await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: queued.Select(t => t.TriggerId).ToArray()));
            Assert.Equal(copies + 2, game.State.StackItems.Count); Assert.Equal(parent, game.State.StackItems[0]);
            var instances = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i <= copies; i++)
            {
                var top = game.State.StackItems[^1]; Assert.Equal(tokenId, top.SourceObjectId); Assert.Equal("P1", top.ControllerId);
                var source = top.LastBreathDraw?.Source ?? top.LastBreathRuneCall!.Source;
                Assert.Equal(new ObjectBinding(tokenId, generation), source);
                Assert.Equal(factory, top.LastBreathDraw?.SourceTokenFactoryCardNo ?? top.LastBreathRuneCall!.SourceTokenFactoryCardNo);
                Assert.True(instances.Add(top.LastBreathDraw?.InstanceId ?? top.LastBreathRuneCall!.InstanceId)); AssertPrivateBirths(game.State);
                var hand = game.State.PlayerZones["P1"].Hand.ToArray(); var deck = game.State.PlayerZones["P1"].MainDeck.ToArray(); before = game.Events().Length;
                var expectedRunesBefore = copiedNo == "OGN·075/298" ? returnedRunes.Skip(i * 2).ToArray() : returnedRunes;
                Assert.Equal(expectedRunesBefore, game.State.PlayerZones["P1"].RuneDeck);
                await game.Act(game.State.PriorityPlayerId!, new PassPriorityCommand());
                Assert.Equal(hand, game.State.PlayerZones["P1"].Hand); Assert.Equal(deck, game.State.PlayerZones["P1"].MainDeck);
                Assert.Equal(expectedRunesBefore, game.State.PlayerZones["P1"].RuneDeck);
                Assert.False(game.State.CardObjects["SECOND"].IsEmpowered);
                await game.Act(game.State.PriorityPlayerId!, new PassPriorityCommand());
                if (discardCount > 0 && hand.Length >= discardCount)
                {
                    var pending = Assert.IsType<PendingHandChoiceState>(game.State.PendingHandChoice);
                    Assert.Equal(new ObjectBinding(tokenId, generation), pending.LastBreathDraw!.Source);
                    Assert.Equal(factory, pending.LastBreathDraw.SourceTokenFactoryCardNo); Assert.Equal(hand, pending.LegalObjectIds); AssertPrivateBirths(game.State);
                    await game.Act("P1", new ChooseHandCardsCommand(pending.ChoiceId, pending.ChoiceWindow, hand.Take(discardCount).ToArray()));
                }
                else Assert.Null(game.State.PendingHandChoice);
                var discarded = Math.Min(discardCount, hand.Length); var recent = game.Events().Skip(before).Select(e => e.Event).ToArray();
                Assert.Equal(hand.Skip(discarded).Concat(deck.Take(drawCount)), game.State.PlayerZones["P1"].Hand);
                Assert.Equal(deck.Skip(drawCount), game.State.PlayerZones["P1"].MainDeck);
                Assert.Equal(drawCount, Convert.ToInt32(Assert.Single(recent, e => e.Kind == "CARD_DRAWN").Payload["count"]));
                Assert.Equal(discarded, recent.Count(e => e.Kind == "CARD_DISCARDED"));
                if (discarded > 0) Assert.True(Array.FindLastIndex(recent, e => e.Kind == "CARD_DISCARDED") < Array.FindIndex(recent, e => e.Kind == "CARD_DRAWN"));
                if (copiedNo == "OGN·075/298")
                {
                    var call = Assert.Single(recent, e => e.Kind == "RUNES_CALLED"); var expectedCalled = i < 2 ? 2 : 0;
                    Assert.Equal(expectedCalled, Convert.ToInt32(call.Payload["count"]));
                    Assert.True(Array.IndexOf(recent, call) < Array.FindIndex(recent, e => e.Kind == "CARD_DRAWN"));
                    foreach (var id in returnedRunes.Skip(i * 2).Take(expectedCalled))
                    { Assert.Contains(id, game.State.PlayerZones["P1"].Base); Assert.True(game.State.CardObjects[id].IsExhausted); }
                    Assert.Equal(returnedRunes.Skip((i + 1) * 2), game.State.PlayerZones["P1"].RuneDeck);
                    if (i >= 1) Assert.Empty(game.State.PlayerZones["P1"].RuneDeck);
                }
                else
                {
                    Assert.Equal(returnedRunes, game.State.PlayerZones["P1"].RuneDeck);
                    Assert.DoesNotContain(recent, e => e.Kind == "RUNES_CALLED");
                }
                Assert.False(game.State.CardObjects.ContainsKey(tokenId)); Assert.Equal(0, game.State.RunePools["P1"].TotalPower);
                Assert.Equal(12, game.State.PlayerZones["P1"].Base.Count(id => game.State.CardObjects[id].Tags.Contains(CardObjectTags.RuneCard))
                    + game.State.PlayerZones["P1"].RuneDeck.Count);
                Assert.False(game.State.CardObjects["SECOND"].IsEmpowered); Assert.Equal(parent, game.State.StackItems[0]);
                Assert.Equal(copies - i + 1, game.State.StackItems.Count); Assert.Equal(2 - copies, game.State.RunePools["P1"].Mana);
            }
            Assert.Empty(game.State.TriggerQueue); Assert.Single(game.State.StackItems); await game.Top();
            Assert.True(game.State.CardObjects["SECOND"].IsEmpowered); Assert.Equal(3, game.State.CardObjects["SECOND"].Power);
            var view = JsonSerializer.SerializeToElement(ResolutionResult.BuildSnapshots(game.State)["P1"], Json);
            Assert.Equal(5, view.GetProperty("players").GetProperty("P1").GetProperty("objects").GetProperty("SECOND").GetProperty("effectivePower").GetInt32());
            Assert.False(game.State.CardObjects.ContainsKey(tokenId)); Assert.Contains(otherToken, game.State.PlayerZones["P1"].Base);
            Assert.Equal(2 - copies, game.State.RunePools["P1"].Mana); Assert.Equal(0, game.State.RunePools["P1"].TotalPower);
            var draws = game.Events().Where(e => e.Event.Kind == "CARD_DRAWN").ToArray(); Assert.Equal(copies + 1, draws.Length);
            Assert.Equal((copies + 1) * drawCount, draws.Sum(e => Convert.ToInt32(e.Event.Payload["count"])));
            Assert.Empty(game.State.StackItems); Assert.Null(game.State.PendingHandChoice); await game.Replay();
        }
        finally { await Save(copiedNo, copies, initial, game); }
    }

    internal static string CostChoice(MatchState state, string source, string victim)
    {
        var candidate = Assert.Single(ResolutionResult.BuildPrompts(state)["P1"].Candidates!, c => c.Action == CommandTypes.ActivateAbility);
        var row = Assert.Single(Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(candidate.Metadata!["sourceRequirements"]),
            r => (string?)r["sourceObjectId"] == source && (string?)r["abilityId"] == "EMPOWER:0:0");
        return Assert.Single(Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(row["additionalCostChoices"]),
            c => c.ObjectIds is not null && c.ObjectIds.SequenceEqual([victim])).Id;
    }
    internal static MatchState Position(string copiedNo, int copies)
    {
        var state = VenUnitEntryBasicTests.Position("VEN·124"); var cards = state.CardObjects.ToDictionary(); var locations = state.ObjectLocations.ToDictionary(); var zones = state.PlayerZones.ToDictionary();
        cards["ALLY"] = OfficialGarenConquestBattlefieldCountTests.Printed("ALLY", copiedNo, "P1");
        cards["GEAR"] = OfficialGarenConquestBattlefieldCountTests.Printed("GEAR", "VEN·137", "P1");
        void Add(string id, string no, string zone)
        {
            cards[id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, no, "P1"); locations[id] = new("P1", zone);
            zones["P1"] = zone switch { "BASE" => zones["P1"] with { Base = [..zones["P1"].Base, id] },
                "MAIN_DECK" => zones["P1"] with { MainDeck = [..zones["P1"].MainDeck, id] }, _ => zones["P1"] with { Hand = [..zones["P1"].Hand, id] } };
        }
        Add("CARRIER", "VEN·128", "HAND"); Add("SECOND", "VEN·124", "HAND");
        for (var i = 0; i < copies; i++) Add("SONG-" + i, "SFD·059/221", "BASE");
        for (var i = 0; i < 12; i++) Add("TOKEN-COST-RUNE-" + i, i < 2 ? "UNL-R06" : "UNL-R02", "BASE");
        for (var i = 4; i <= 12; i++) Add("P1-DRAW-" + i, "OGN·219/298", "MAIN_DECK");
        return state with { RoomId = "VEN124-ACTUAL-CREATED-COPIED-TOKEN-" + copiedNo, CardObjects = cards, ObjectLocations = locations, PlayerZones = zones,
            RunePools = new Dictionary<string, RunePool> { ["P1"] = RunePool.Empty, ["P2"] = RunePool.Empty } };
    }
    private static void AssertPrivateBirths(MatchState state)
    {
        foreach (var view in ResolutionResult.BuildSnapshots(state).Values.Cast<object>().Append(ResolutionResult.BuildSpectatorSnapshot(state)))
        {
            var raw = JsonSerializer.Serialize(view, Json);
            Assert.DoesNotContain("\"lastBreathDraw\"", raw, StringComparison.Ordinal); Assert.DoesNotContain("\"lastBreathRuneCall\"", raw, StringComparison.Ordinal);
            Assert.DoesNotContain("\"empowerActivation\"", raw, StringComparison.Ordinal);
        }
    }
    private static async Task Save(string no, int copies, MatchState initial, Driver game)
    {
        if (Environment.GetEnvironmentVariable("RIFTBOUND_VEN124_TOKEN_EVIDENCE") is not { Length: > 0 } folder) return;
        Directory.CreateDirectory(folder); await File.WriteAllTextAsync(Path.Combine(folder, no.Replace('/', '-') + "-copies-" + copies + ".json"), JsonSerializer.Serialize(new {
            scope = "Explicit midgame copy-source/gear material. Zero pools/exactly12 physical own runes, actual normal No128 play, native typed Empower, first No124 cost earns actual minions, actual paid Shady copy and0/2 paid Song texts. No124 second required cost makes real copied minion cease, independently captured skills finish before parent Empower. Actual native/Shady payments return two yellow runes, with two Song payments returning two more green runes; non-call skills preserve their exact order and Fae independently calls2/2/0 dormant before each draw. No injected token/copy/empower/death/skill/source/result. Per-command hidden/dedup checks, one carrier setup cold checkpoint, then original per-command recovery and original action replay. No canonical opening, SQL/network, latest complete L2/L3, applicability or whole-card claim.",
            initial, final = game.State, commands = game.Commands(), events = game.Events(), ruleInputs = RuleInputIdentity.Current }, Json));
    }
}
