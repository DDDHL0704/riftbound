using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed partial class Ven078EmpoweredRuneLastBreathConsumerTests
{
    private const string No = "VEN·078";
    private const string Text = "{{强化1AA}}（支付{{1}}和{{A}}{{A}}：强化我。仅在未强化时可用。）\n{{已强化>}} 我获得{{S}}+2。\n{{已强化>}}{{>绝念>}} 召出两枚休眠的符文。（当我在已强化状态下被摧毁时，发动此效果。）";
    private const string Destroy = "OGN·229/298";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    [Fact]
    public async Task ExactCn37772UsesThePrintedUnitProfileAndGenericEmpowerRatherThanAStartingStatusOrBonus()
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadDefaultAsync()).Cards, c => c.CardNo == No);
        Assert.Equal(37772, card.Id); Assert.Equal("枯爪巴凯", card.CardName); Assert.Equal(Text, card.CardEffect);
        Assert.Equal(4, card.Energy); Assert.Null(card.ReturnEnergy); Assert.Equal(4, card.Power);
        Assert.Equal([RuneTrait.Orange], card.CardColorList); Assert.Equal(["unit"], card.CardCategoryList);
        Assert.Equal("恕瑞玛", card.Region); Assert.Empty(card.Tag);
        Assert.True(CardBehaviorRegistry.TryGetByCardNo(No, out var behavior));
        Assert.True(behavior.PlaysSourceToBaseAsUnit); Assert.Equal(4, behavior.ManaCost); Assert.Equal(4, behavior.SourceUnitPower);
        Assert.Equal(0, PrintedPowerCostRules.ForCard(No).Amount);
        var cost = Assert.Single(Assert.Single(EmpowerParser.Parse(card.CardEffect)).Alternatives);
        Assert.Equal(1, cost.Mana); Assert.Equal(2, cost.Power); Assert.Empty(cost.TypedPower);
        Assert.True(cost.SupportedResources); Assert.False(cost.Exhaust); Assert.Empty(cost.AdditionalCost);
        var self = EmpowerParser.Self(card.CardEffect); Assert.Equal(2, self.Might); Assert.Empty(self.Keywords);
    }

    [Fact]
    public async Task TheActualCatalogTextRetainsItsOwnTwoRuneDependentDeathClause()
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadDefaultAsync()).Cards, c => c.CardNo == No);
        var trigger = Assert.Single(TriggerParser.Parse(card.CardEffect), t => t.Kind == TriggerKinds.UnitLastBreathCallRuneOne);
        Assert.Equal(TriggerTimings.UnitDestroyed, trigger.Timing); Assert.Equal(TriggerTargetScopes.SourceUnit, trigger.TargetScope);
        Assert.True(trigger.RequiresSourceEmpowered); Assert.Equal(2, trigger.RuneCallCount);
        Assert.Null(trigger.DrawCount); Assert.False(trigger.Optional ?? false);
    }

    [Theory]
    [InlineData("P1", false)] [InlineData("P1", true)]
    [InlineData("P2", false)] [InlineData("P2", true)]
    public async Task RealPlayAndMixedTraitGenericEmpowerDetermineWhetherActualDeathCallsTwoDormantRunes(string owner, bool empower)
    {
        var initial = Position(owner); var game = new Driver(initial); var opponent = Other(owner);
        try
        {
            await Tap(game, owner); Assert.Equal(6, game.State.RunePools[owner].Mana); Assert.Equal(0, game.State.RunePools[owner].TotalPower);
            await game.Act(owner, new PlayCardCommand("UNIT", No, []));
            Assert.Equal(2, game.State.RunePools[owner].Mana); Assert.Equal(0, game.State.RunePools[owner].TotalPower);
            Assert.Contains("UNIT", game.State.PlayerZones[owner].Base); Assert.False(game.State.CardObjects["UNIT"].IsEmpowered);
            Assert.Equal(4, game.State.CardObjects["UNIT"].Power); Assert.Equal(4, Might(game.State, owner));
            var generation = game.State.CardObjects["UNIT"].ObjectGeneration;
            if (empower)
            {
                await Empower(game, owner); Assert.Equal(1, game.State.RunePools[owner].Mana);
                Assert.Equal(0, game.State.RunePools[owner].TotalPower); Assert.False(game.State.CardObjects["UNIT"].IsEmpowered);
                Assert.Equal(1, Assert.Single(game.State.StackItems).EmpowerActivation!.PaidMana);
                Assert.Equal(generation, Assert.Single(game.State.StackItems).EmpowerActivation!.Source.Generation);
                Assert.Equal(4, Might(game.State, owner)); Cold(initial, game); await game.Top();
                Assert.True(game.State.CardObjects["UNIT"].IsEmpowered); Assert.Equal(4, game.State.CardObjects["UNIT"].Power);
                Assert.Equal(6, Might(game.State, owner)); Assert.Empty(game.State.CardObjects["UNIT"].UntilEndOfTurnPowerModifiers);
                Cold(initial, game);
            }
            await game.Act(owner, new EndTurnCommand()); Assert.Equal(opponent, game.State.TurnPlayerId);
            Assert.Equal(empower, game.State.CardObjects["UNIT"].IsEmpowered); Assert.Equal(empower ? 6 : 4, Might(game.State, owner));
            await Kill(game, opponent); Assert.Contains("UNIT", game.State.PlayerZones[owner].Graveyard);
            Assert.DoesNotContain("UNIT", game.State.PlayerZones[owner].Base); Assert.Equal("GRAVEYARD", game.State.ObjectLocations["UNIT"].Zone);
            Assert.Equal(generation + 1, game.State.CardObjects["UNIT"].ObjectGeneration); Assert.False(game.State.CardObjects["UNIT"].IsEmpowered);
            Assert.Equal(4, game.State.CardObjects["UNIT"].Power);
            var before = game.State.PlayerZones[owner].RuneDeck.ToArray(); Assert.True(before.Length >= 2);
            var skills = game.State.StackItems.Where(s => s.EffectKind == TriggerKinds.UnitLastBreathCallRuneOne).ToArray();
            Assert.Equal(empower ? 1 : 0, skills.Length); Assert.Empty(game.State.TriggerQueue); Cold(initial, game);
            if (empower)
            {
                var skill = Assert.Single(skills); Assert.Equal("UNIT", skill.SourceObjectId); Assert.Equal(owner, skill.ControllerId);
                Assert.Empty(skill.TargetObjectIds); await game.Top();
                Assert.Equal(before.Skip(2), game.State.PlayerZones[owner].RuneDeck); CalledRunes(game.State, owner, before.Take(2));
                var called = Assert.Single(game.Events(), e => e.Event.Kind == "RUNES_CALLED" && Equals(e.Event.Payload.GetValueOrDefault("reason"), TriggerKinds.UnitLastBreathCallRuneOne));
                Assert.Equal(2, called.Event.Payload["count"]); Assert.Equal(owner, called.Event.Payload["playerId"]);
            }
            else Assert.Equal(before, game.State.PlayerZones[owner].RuneDeck);
            Assert.Empty(game.State.StackItems); Assert.Empty(game.State.TriggerQueue); Cold(initial, game);
            await game.Act(opponent, new EndTurnCommand()); await game.Act(owner, new EndTurnCommand());
            Assert.False(game.State.CardObjects["UNIT"].IsEmpowered); Assert.Empty(game.State.StackItems); Cold(initial, game); await game.Replay();
        }
        finally { await Export("native-death-" + owner + "-" + empower, initial, game); }
    }

    private static MatchState Position(string owner, int ownRunes = 6)
    {
        var state = VenUnitEntryBasicTests.Position(No); var opponent = Other(owner);
        var cards = state.CardObjects.ToDictionary(); var locations = state.ObjectLocations.ToDictionary(); var zones = state.PlayerZones.ToDictionary();
        cards["UNIT"] = OfficialGarenConquestBattlefieldCountTests.Printed("UNIT", No, owner); locations["UNIT"] = new(owner, "HAND");
        foreach (var player in new[] { "P1", "P2" })
        {
            var runes = Enumerable.Range(1, player == owner ? ownRunes : 4).Select(i => player + "-RUNE-" + i).ToArray();
            foreach (var id in runes)
            {
                var no = player == owner && id.EndsWith("-1", StringComparison.Ordinal) ? "UNL-R02" : "UNL-R06";
                cards[id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, no, player); locations[id] = new(player, "BASE");
            }
            var deck = Enumerable.Range(1, 8).Select(i => player + "-DORMANT-" + i).ToArray();
            foreach (var id in deck) { cards[id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, "UNL-R04", player); locations[id] = new(player, "RUNE_DECK"); }
            zones[player] = zones[player] with { Hand = player == owner ? ["UNIT"] : ["KILL"], Base = [..zones[player].Base, ..runes], RuneDeck = deck };
        }
        cards["KILL"] = OfficialGarenConquestBattlefieldCountTests.Printed("KILL", Destroy, opponent); locations["KILL"] = new(opponent, "HAND");
        return state with { RoomId = "VEN078-EXPLICIT-MIDGAME-" + owner, TurnPlayerId = owner, ActivePlayerId = owner,
            CardObjects = cards, ObjectLocations = locations, PlayerZones = zones,
            RunePools = new Dictionary<string, RunePool> { ["P1"] = new(0, 0), ["P2"] = new(0, 0) } };
    }
    private static string Other(string player) => player == "P1" ? "P2" : "P1";
    private static async Task Tap(Driver game, string player)
    {
        var runes = game.State.PlayerZones[player].Base.Where(id => game.State.CardObjects[id].Tags.Contains(CardObjectTags.RuneCard)
            && !game.State.CardObjects[id].IsExhausted).ToArray();
        foreach (var batch in runes.Chunk(12)) await game.Act(player, new TapRuneCommand(batch[0], batch));
    }
    private static async Task Empower(Driver game, string owner)
    {
        await game.Act(owner, new RecycleRuneCommand(owner + "-RUNE-1")); await game.Act(owner, new RecycleRuneCommand(owner + "-RUNE-2"));
        Assert.Equal(1, game.State.RunePools[owner].PowerByTrait[RuneTrait.Green]); Assert.Equal(1, game.State.RunePools[owner].PowerByTrait[RuneTrait.Yellow]);
        await game.Act(owner, new ActivateAbilityCommand("UNIT", "EMPOWER:0:0", []));
    }
    private static async Task Kill(Driver game, string opponent)
    {
        // Use four known field runes; ordinary turn channeling may have added
        // other physical runes that this destruction payment does not spend.
        var runes = Enumerable.Range(1, 4).Select(i => opponent + "-RUNE-" + i).ToArray();
        await game.Act(opponent, new TapRuneCommand(runes[0], runes));
        await game.Act(opponent, new RecycleRuneCommand(opponent + "-RUNE-1"));
        await game.Act(opponent, new RecycleRuneCommand(opponent + "-RUNE-2"));
        Assert.Equal(4, game.State.RunePools[opponent].Mana); Assert.Equal(2, game.State.RunePools[opponent].PowerByTrait[RuneTrait.Yellow]);
        await game.Act(opponent, new PlayCardCommand("KILL", Destroy, ["UNIT"]));
        Assert.Equal(0, game.State.RunePools[opponent].Mana); Assert.Equal(0, game.State.RunePools[opponent].TotalPower); await game.Top();
    }
    private static int Might(MatchState state, string owner) => JsonSerializer.SerializeToElement(ResolutionResult.BuildSnapshots(state)[owner], Json)
        .GetProperty("players").GetProperty(owner).GetProperty("objects").GetProperty("UNIT").GetProperty("effectivePower").GetInt32();
    private static void CalledRunes(MatchState state, string player, IEnumerable<string> ids)
    {
        foreach (var id in ids)
        {
            Assert.Contains(id, state.PlayerZones[player].Base); Assert.Equal("BASE", state.ObjectLocations[id].Zone);
            Assert.True(state.CardObjects[id].IsExhausted); Assert.Equal(player, state.CardObjects[id].OwnerId);
            Assert.Equal(player, state.CardObjects[id].ControllerId); Assert.Contains(CardObjectTags.RuneCard, state.CardObjects[id].Tags);
        }
    }
    private static void Cold(MatchState initial, Driver game)
    {
        var state = game.State; var sequence = game.Commands().Last().CompletedEventSequence;
        var snapshots = ResolutionResult.BuildSnapshots(state); var prompts = ResolutionResult.BuildPrompts(state);
        var views = state.Seats.Keys.ToDictionary(p => p, p => new RecoveredPlayerView(p, state.Tick, sequence, snapshots[p], state.Tick, sequence, prompts[p]));
        var spectator = MatchReplayRedactor.BuildSpectatorFrame(state.RoomId, state.Tick, sequence, [], state);
        var errors = MatchRecoveryValidator.Validate(state.RoomId, sequence, game.Commands(), game.Events(), views, state, state.Tick, spectator); Assert.Empty(errors);
        var frame = new MatchRecoveryFrame(state.RoomId, state.Tick, sequence, game.Commands(), game.Events(), views, errors, state, initial, spectator) { OpeningSeed = initial.Seed };
        var decoded = JsonSerializer.Deserialize<MatchRecoveryFrame>(JsonSerializer.Serialize(frame, Json), Json)!;
        Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(decoded.AuthoritativeState!));
        var restored = MatchSession.Restore(decoded, new CoreRuleEngine(), NoopMatchJournal.Instance);
        foreach (var actor in state.Seats.Keys)
        {
            Assert.Equal(MatchStateHasher.HashValue(snapshots[actor]), MatchStateHasher.HashValue(restored.SnapshotFor(actor)));
            Assert.Equal(MatchStateHasher.HashValue(prompts[actor]), MatchStateHasher.HashValue(restored.PromptFor(actor)));
        }
    }
    private static async Task Export(string label, MatchState initial, Driver game)
    {
        if (Environment.GetEnvironmentVariable("RIFTBOUND_VEN078_EVIDENCE") is not { Length: > 0 } output) return;
        Directory.CreateDirectory(output);
        await File.WriteAllTextAsync(Path.Combine(output, label + ".json"), JsonSerializer.Serialize(new {
            scope = "Explicit turn-five fixture; normal unit play, physical mixed-trait generic Empower, self Might and conditional two-dormant-rune death. Not a canonical opening, L2, L3 or whole-card certification.",
            initial, final = game.State, commands = game.Commands(), events = game.Events(), ruleInputs = RuleInputIdentity.Current }, Json));
    }
}
