using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class Ven110EmpoweredBanishConsumerTests
{
    private const string No = "VEN·110";
    private const string Text = "{{强化}} — 弃置一张法术牌（支付此费用：强化我。仅在未强化时可用。）\n当我变为{{已强化}}时，放逐战场上一名不高于3{{S}}的敌方单位。";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    [Fact]
    public async Task ExactPrintingKeepsTheSpellDiscardCostSeparateFromItsEmpoweredBanishTrigger()
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadDefaultAsync()).Cards, c => c.CardNo == No);
        Assert.Equal(37807, card.Id); Assert.Equal("梅尔", card.CardName); Assert.Equal("镜转乾坤", card.SubTitle);
        Assert.Equal(Text, card.CardEffect); Assert.Equal(5, card.Energy); Assert.Equal(4, card.Power); Assert.Null(card.ReturnEnergy);
        Assert.Equal([RuneTrait.Purple], card.CardColorList); Assert.Equal(["hero_unit"], card.CardCategoryList);
        // The raw API region says Shurima while the printed face says Noxus.
        // Preserve the exact API source; region-dependent semantics are not certified here.
        var cost = Assert.Single(Assert.Single(EmpowerParser.Parse(card.CardEffect)).Alternatives);
        Assert.Equal("弃置一张法术牌", cost.AdditionalCost); Assert.False(cost.SupportedResources);
        Assert.Equal(0, cost.Mana); Assert.Equal(0, cost.Power); Assert.Empty(cost.TypedPower);
        Assert.True(CardBehaviorRegistry.TryGetByCardNo(No, out var behavior));
        Assert.True(behavior.PlaysSourceToBaseAsUnit); Assert.Equal(5, behavior.ManaCost); Assert.Equal(4, behavior.SourceUnitPower);
        Assert.Equal(0, PrintedPowerCostRules.ForCard(No).Amount);
    }

    [Theory]
    [InlineData("VEN·049")] [InlineData("OGN·093/298")]
    public async Task ARealSpellIsDiscardedBeforeEmpowerResolvesWithoutPrechoosingTheFutureBanishTarget(string spell)
    {
        var initial = Position(spell); var game = new Driver(initial);
        try
        {
            await game.Act("P1", new PlayCardCommand("UNIT", No, [])); Assert.Equal(15, game.State.RunePools["P1"].Mana);
            var generation = game.State.CardObjects["FODDER"].ObjectGeneration;
            await game.Act("P1", Activate());
            Assert.Contains("FODDER", game.State.PlayerZones["P1"].Graveyard); Assert.DoesNotContain("FODDER", game.State.PlayerZones["P1"].Hand);
            Assert.Equal(15, game.State.RunePools["P1"].Mana); Assert.Equal(20, game.State.RunePools["P1"].TotalPower);
            Assert.False(game.State.CardObjects["UNIT"].IsEmpowered); Assert.Null(game.State.PendingCardChoice);
            var item = Assert.Single(game.State.StackItems); Assert.Empty(item.TargetObjectIds);
            Assert.Equal(new ObjectBinding("FODDER", generation), item.EmpowerActivation!.DiscardedHandCard);
            await game.Replay(); Restore(initial, game);
        }
        finally { await Save("real-spell-payment-" + spell.Replace('/', '-'), initial, game); }
    }

    [Theory]
    [InlineData("missing")] [InlineData("unit")] [InlineData("equipment")] [InlineData("duplicate")] [InlineData("target")]
    public async Task ActualMissingOrNonspellCostsCannotPartlyDiscardOrActivate(string branch)
    {
        var initial = Add(Add(Position(), "WRONG-UNIT", "OGN·096/298"), "WRONG-GEAR", "SFD·022/221"); var game = new Driver(initial);
        try
        {
            await game.Act("P1", new PlayCardCommand("UNIT", No, []));
            var command = Activate() with { OptionalCosts = branch switch {
                "missing" => [], "unit" => ["DISCARD_HAND_CARD:WRONG-UNIT"], "equipment" => ["DISCARD_HAND_CARD:WRONG-GEAR"],
                "duplicate" => ["DISCARD_HAND_CARD:FODDER", "DISCARD_HAND_CARD:FODDER"], _ => ["DISCARD_HAND_CARD:FODDER"] },
                TargetObjectIds = branch == "target" ? ["ENEMY"] : [] };
            var hash = MatchStateHasher.Hash(game.State); var views = JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(game.State));
            var prompts = JsonSerializer.Serialize(ResolutionResult.BuildPrompts(game.State));
            var result = await new CoreRuleEngine().ResolveAsync(game.State, new("invalid-ven110", "P1", command.CmdType), command, default);
            Assert.False(result.Accepted); Assert.Empty(result.Events); Assert.Equal(hash, MatchStateHasher.Hash(result.State));
            Assert.Equal(views, JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(result.State)));
            Assert.Equal(prompts, JsonSerializer.Serialize(ResolutionResult.BuildPrompts(result.State)));
            Assert.Contains("FODDER", game.State.PlayerZones["P1"].Hand); Assert.False(game.State.CardObjects["UNIT"].IsEmpowered);
            Assert.Empty(game.State.StackItems); await game.Replay(); Restore(initial, game);
        }
        finally { await Save("invalid-spell-cost-" + branch, initial, game); }
    }

    [Fact]
    public async Task NoEnemyBattlefieldTargetDoesNotPreventPayingTheCostAndBecomingEmpowered()
    {
        var initial = Position(); var game = new Driver(initial);
        try
        {
            await game.Act("P1", new PlayCardCommand("UNIT", No, [])); await game.Act("P1", Activate()); await game.Top();
            Assert.True(game.State.CardObjects["UNIT"].IsEmpowered); Assert.Contains("FODDER", game.State.PlayerZones["P1"].Graveyard);
            Assert.Equal(4, game.State.CardObjects["UNIT"].Power); Assert.Equal(15, game.State.RunePools["P1"].Mana);
            Assert.Null(game.State.PendingCardChoice); Assert.Empty(game.State.StackItems); await game.Replay(); Restore(initial, game);
        }
        finally { await Save("no-legal-banish-target", initial, game); }
    }

    [Theory]
    [InlineData("SMALL")] [InlineData("LAST-BREATH")]
    public async Task BecomingEmpoweredOffersOnlySmallEnemyBattlefieldUnitsAndBanishesAfterBothPlayersCanRespond(string target)
    {
        var initial = Add(Add(Add(Add(Position(), "SMALL", "OGN·175/298", "P2", "BATTLEFIELD", "BF"),
            "LAST-BREATH", "OGN·096/298", "P2", "BATTLEFIELD", "OTHER"), "BASE-SMALL", "OGN·175/298", "P2", "BASE"),
            "FRIENDLY-SMALL", "OGN·175/298", "P1", "BATTLEFIELD", "BF");
        initial = LocalDestructionRecallTests.At(initial, "ENEMY", "P2", "BATTLEFIELD", "OTHER"); var game = new Driver(initial);
        try
        {
            await game.Act("P1", new PlayCardCommand("UNIT", No, [])); await game.Act("P1", Activate());
            Assert.False(game.State.CardObjects["UNIT"].IsEmpowered); Assert.Null(game.State.PendingCardChoice);
            await game.Top(); Assert.True(game.State.CardObjects["UNIT"].IsEmpowered);
            Assert.Contains("FODDER", game.State.PlayerZones["P1"].Graveyard); Assert.Equal(15, game.State.RunePools["P1"].Mana);
            var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
            Assert.Equal("TRIGGER_CONFIRMATION", choice.ChoiceWindow); Assert.Equal("UNIT", choice.SourceObjectId);
            Assert.Equal(new[] { "LAST-BREATH", "SMALL" }, choice.LegalObjectIds.Order().ToArray());
            Assert.DoesNotContain(target, game.State.PlayerZones["P2"].Banished); Restore(initial, game);
            await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [target]));
            Assert.Single(game.State.StackItems); var firstPriority = game.State.PriorityPlayerId!;
            Assert.Contains("PASS_PRIORITY", ResolutionResult.BuildPrompts(game.State)[firstPriority].Actions);
            Assert.DoesNotContain(target, game.State.PlayerZones["P2"].Banished); Restore(initial, game);
            await game.Act(firstPriority, new PassPriorityCommand()); Assert.NotEqual(firstPriority, game.State.PriorityPlayerId);
            Assert.DoesNotContain(target, game.State.PlayerZones["P2"].Banished);
            await game.Top(); Assert.Contains(target, game.State.PlayerZones["P2"].Banished);
            Assert.DoesNotContain(target, game.State.PlayerZones["P2"].Graveyard); Assert.Equal("BANISHED", game.State.ObjectLocations[target].Zone);
            Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "UNIT_DESTROYED");
            Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "CARD_DRAWN");
            Assert.True(game.State.CardObjects["UNIT"].IsEmpowered); await game.Replay(); Restore(initial, game);
        }
        finally { await Save("empowered-banish-" + target, initial, game); }
    }

    private static ActivateAbilityCommand Activate() => new("UNIT", "EMPOWER:0:0", [], OptionalCosts: ["DISCARD_HAND_CARD:FODDER"]);
    private static MatchState Position(string spell = "VEN·049") => Add(VenUnitEntryBasicTests.Position(No), "FODDER", spell);
    private static MatchState Add(MatchState state, string id, string no, string owner = "P1", string zone = "HAND", string? field = null) => state with {
        CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) { [id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, no, owner) },
        ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { [id] = new(owner, zone, field) },
        PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) { [owner] = zone switch {
            "BASE" => state.PlayerZones[owner] with { Base = [..state.PlayerZones[owner].Base, id] },
            "BATTLEFIELD" => state.PlayerZones[owner] with { Battlefields = [..state.PlayerZones[owner].Battlefields, id] },
            _ => state.PlayerZones[owner] with { Hand = [..state.PlayerZones[owner].Hand, id] } } } };
    private static void Restore(MatchState initial, Driver game)
    {
        var state = game.State; var sequence = game.Commands().Last().CompletedEventSequence;
        var snapshots = ResolutionResult.BuildSnapshots(state); var prompts = ResolutionResult.BuildPrompts(state);
        var views = state.Seats.Keys.ToDictionary(p => p, p => new RecoveredPlayerView(p, state.Tick, sequence, snapshots[p], state.Tick, sequence, prompts[p]));
        var spectator = MatchReplayRedactor.BuildSpectatorFrame(state.RoomId, state.Tick, sequence, [], state);
        var errors = MatchRecoveryValidator.Validate(state.RoomId, sequence, game.Commands(), game.Events(), views, state, state.Tick, spectator); Assert.Empty(errors);
        var frame = new MatchRecoveryFrame(state.RoomId, state.Tick, sequence, game.Commands(), game.Events(), views, errors, state, initial, spectator) { OpeningSeed = initial.Seed };
        var restored = MatchSession.Restore(JsonSerializer.Deserialize<MatchRecoveryFrame>(JsonSerializer.Serialize(frame, Json), Json)!, new CoreRuleEngine(), NoopMatchJournal.Instance);
        foreach (var player in state.Seats.Keys)
        {
            Assert.Equal(MatchStateHasher.HashValue(snapshots[player]), MatchStateHasher.HashValue(restored.SnapshotFor(player)));
            Assert.Equal(MatchStateHasher.HashValue(prompts[player]), MatchStateHasher.HashValue(restored.PromptFor(player)));
        }
    }
    private static async Task Save(string label, MatchState initial, Driver game)
    {
        if (Environment.GetEnvironmentVariable("RIFTBOUND_VEN110_EVIDENCE") is not { Length: > 0 } output) return;
        Directory.CreateDirectory(output);
        await File.WriteAllTextAsync(Path.Combine(output, label + ".json"), JsonSerializer.Serialize(new {
            scope = "VEN110 explicit midgame actual play/spell-discard/Empower consumer; source region conflict, L2/L3 and whole-card certification remain OPEN",
            initial, final = game.State, commands = game.Commands(), events = game.Events() }, Json));
    }
}
