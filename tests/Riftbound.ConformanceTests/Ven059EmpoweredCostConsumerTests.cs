using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class Ven059EmpoweredCostConsumerTests
{
    private const string No = "VEN·059";
    private const string Text = "{{迅捷}}（可在你的回合或法术对决中打出。）\n如果你控制着{{已强化}}的物体，则此牌的费用减少{{2}}。\n对战场上的一名单位造成4点伤害，";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    [Fact]
    public async Task ExactNewPrintingPreservesAllPriorFunctionalGroupsAndMembers()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync();
        var controlCards = catalog.Cards.Where(c => c.CardNo is not ("VEN·128" or "VEN·078")).ToArray();
        var priorCards = controlCards.Where(c => c.CardNo != No).ToArray();
        Assert.Equal(1085, priorCards.Length); Assert.Equal(1086, controlCards.Length);
        var prior = FunctionalUnitBuilder.Build(priorCards); var current = FunctionalUnitBuilder.Build(controlCards);
        Assert.Equal(866, prior.Count); Assert.Equal(867, current.Count);
        foreach (var unit in prior) Assert.Equal(JsonSerializer.Serialize(unit), JsonSerializer.Serialize(Assert.Single(current, c => c.Id == unit.Id)));
        Assert.Equal(No, Assert.Single(Assert.Single(current, c => c.Cards.Any(card => card.CardNo == No)).Cards).CardNo);
        var cumulativePriorCards = catalog.Cards.Where(c => c.CardNo != No).ToArray();
        Assert.Equal(1087, cumulativePriorCards.Length); Assert.Equal(1088, catalog.Cards.Count);
        var cumulativePrior = FunctionalUnitBuilder.Build(cumulativePriorCards); var cumulative = FunctionalUnitBuilder.Build(catalog.Cards);
        Assert.Equal(868, cumulativePrior.Count); Assert.Equal(869, cumulative.Count);
        foreach (var unit in cumulativePrior) Assert.Equal(JsonSerializer.Serialize(unit), JsonSerializer.Serialize(Assert.Single(cumulative, c => c.Id == unit.Id)));
        foreach (var unit in current) Assert.Equal(JsonSerializer.Serialize(unit), JsonSerializer.Serialize(Assert.Single(cumulative, c => c.Id == unit.Id)));
    }

    [Fact]
    public async Task ExactCnPrintingKeepsBluePowerSwiftFourDamageAndConditionalManaReduction()
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadDefaultAsync()).Cards, c => c.CardNo == No);
        Assert.Equal(37750, card.Id); Assert.Equal("电能震荡", card.CardName); Assert.Equal(Text, card.CardEffect);
        Assert.Equal(3, card.Energy); Assert.Equal(1, card.ReturnEnergy); Assert.Null(card.Power);
        Assert.Equal([RuneTrait.Blue], card.CardColorList);
        Assert.True(CardBehaviorRegistry.TryGetByCardNo(No, out var behavior));
        Assert.Equal(3, behavior.ManaCost); Assert.Equal(4, behavior.DamageAmount); Assert.Equal(1, behavior.RequiredTargetCount);
        Assert.Equal(CardTargetScopes.BattlefieldUnit, behavior.TargetScope); Assert.True(behavior.CanPlayDuringSpellDuel);
        Assert.False(behavior.CanPlayDuringPriority); Assert.Equal(2, behavior.CostReductionMana);
        Assert.Equal("CONTROLLER_CONTROLS_EMPOWERED_OBJECT", behavior.CostReductionConditionKind);
        Assert.Equal(1, PrintedPowerCostRules.ForCard(No).Amount);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ActualEmpowerChangesOnlyTheManaPaidAndDamageWaitsForSpellResolution(bool empower)
    {
        var initial = Position(); var game = new Driver(initial);
        try
        {
            await game.Act("P1", new PlayCardCommand("UNIT", "VEN·032", []));
            if (empower) { await game.Act("P1", new ActivateAbilityCommand("UNIT", "EMPOWER:0:0", [])); await game.Top(); }
            Assert.Equal(empower, game.State.CardObjects["UNIT"].IsEmpowered);
            var before = game.State.RunePools["P1"];
            await game.Act("P1", Play());
            Assert.Equal(before.Mana - (empower ? 1 : 3), game.State.RunePools["P1"].Mana);
            Assert.Equal(before.PowerByTrait[RuneTrait.Blue] - 1, game.State.RunePools["P1"].PowerByTrait[RuneTrait.Blue]);
            Assert.Equal(0, game.State.CardObjects["ENEMY"].Damage);
            var item = Assert.Single(game.State.StackItems); Assert.Equal("SPELL", item.SourceObjectId);
            Assert.Equal(new[] { "ENEMY" }, item.TargetObjectIds);
            JsonRestore(initial, game); await game.Top();
            Assert.Contains("ENEMY", game.State.PlayerZones["P2"].Graveyard);
            Assert.Contains("SPELL", game.State.PlayerZones["P1"].Graveyard); Assert.Empty(game.State.StackItems);
            await game.Replay(); JsonRestore(initial, game);
        }
        finally { await Save("actual-empower-" + empower, initial, game); }
    }

    [Fact]
    public async Task OneManaAndOneBlueAreEnoughAfterActualEmpower()
    {
        var initial = Position() with { RunePools = new Dictionary<string, RunePool> {
            ["P1"] = new(16, 0, new Dictionary<string, int> { [RuneTrait.Blue] = 1 }), ["P2"] = new(20, 20) } };
        var game = new Driver(initial);
        try
        {
            await game.Act("P1", new PlayCardCommand("UNIT", "VEN·032", []));
            await game.Act("P1", new ActivateAbilityCommand("UNIT", "EMPOWER:0:0", [])); await game.Top();
            Assert.True(game.State.CardObjects["UNIT"].IsEmpowered); Assert.Equal(1, game.State.RunePools["P1"].Mana);
            await game.Act("P1", Play()); Assert.Equal(0, game.State.RunePools["P1"].Mana); Assert.Equal(0, game.State.RunePools["P1"].TotalPower);
            await game.Top(); Assert.Contains("ENEMY", game.State.PlayerZones["P2"].Graveyard); await game.Replay(); JsonRestore(initial, game);
        }
        finally { await Save("exact-discounted-payment", initial, game); }
    }

    [Theory]
    [InlineData("ALLY", "P1")] [InlineData("ENEMY", "P2")]
    public async Task FourDamageCanChooseEitherPlayersBattlefieldUnit(string target, string owner)
    {
        var initial = Position(); var game = new Driver(initial);
        try
        {
            await game.Act("P1", Play(target)); Assert.Equal(37, game.State.RunePools["P1"].Mana);
            Assert.Equal(0, game.State.CardObjects[target].Damage); await game.Top();
            Assert.Contains(target, game.State.PlayerZones[owner].Graveyard);
            Assert.Contains("SPELL", game.State.PlayerZones["P1"].Graveyard); await game.Replay(); JsonRestore(initial, game);
        }
        finally { await Save("friendly-or-enemy-" + target, initial, game); }
    }

    [Theory]
    [InlineData("base")] [InlineData("equipment")] [InlineData("graveyard")]
    [InlineData("missing")] [InlineData("two")]
    [InlineData("wrong-trait")] [InlineData("insufficient-mana")]
    public async Task InvalidTargetOrPaymentKeepsTheHandResourcesAndAllViewsUnchanged(string branch)
    {
        var initial = Position();
        if (branch == "base") initial = Relocate(initial, "ENEMY", "P2", "BASE");
        if (branch == "wrong-trait") initial = initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) {
            ["P1"] = new(40, 0, new Dictionary<string, int> { [RuneTrait.Red] = 4 }) } };
        if (branch == "insufficient-mana") initial = initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) {
            ["P1"] = new(2, 0, new Dictionary<string, int> { [RuneTrait.Blue] = 4 }) } };
        var game = new Driver(initial);
        try
        {
            var command = Play(branch == "equipment" ? "GEAR" : branch == "graveyard" ? "GRAVE" : "ENEMY") with {
                TargetObjectIds = branch == "missing" ? [] : branch == "two" ? ["ALLY", "ENEMY"] :
                    [branch == "equipment" ? "GEAR" : branch == "graveyard" ? "GRAVE" : "ENEMY"] };
            await Reject(game.State, "P1", command);
            Assert.Contains("SPELL", game.State.PlayerZones["P1"].Hand); Assert.Empty(game.State.StackItems);
            await game.Replay(); JsonRestore(initial, game);
        }
        finally { await Save("rejected-" + branch, initial, game); }
    }

    [Fact]
    public async Task ACounterKeepsTheSpellPaidButPreventsItsFourDamage()
    {
        var initial = Add(Position(), "COUNTER", "OGN·045/298", "P2"); var game = new Driver(initial);
        try
        {
            await game.Act("P1", Play()); var id = Assert.Single(game.State.StackItems).StackItemId;
            await game.Act("P1", new PassPriorityCommand());
            await game.Act("P2", new PlayCardCommand("COUNTER", "OGN·045/298", [id])); await game.Top();
            Assert.Empty(game.State.StackItems); Assert.Equal(37, game.State.RunePools["P1"].Mana);
            Assert.Equal(3, game.State.RunePools["P1"].PowerByTrait[RuneTrait.Blue]);
            Assert.Equal(0, game.State.CardObjects["ENEMY"].Damage); Assert.DoesNotContain("ENEMY", game.State.PlayerZones["P2"].Graveyard);
            Assert.Contains("SPELL", game.State.PlayerZones["P1"].Graveyard); await game.Replay(); JsonRestore(initial, game);
        }
        finally { await Save("counter-retains-payment", initial, game); }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ActualReturnBeforePaymentRemovesTheDiscountAndReturnAfterPaymentCannotRefundIt(bool afterPayment)
    {
        var initial = Add(Position(), "RETURN", "OGN·104/298"); var game = new Driver(initial);
        try
        {
            await game.Act("P1", new PlayCardCommand("UNIT", "VEN·032", []));
            await game.Act("P1", new ActivateAbilityCommand("UNIT", "EMPOWER:0:0", [])); await game.Top();
            Assert.True(game.State.CardObjects["UNIT"].IsEmpowered);
            if (afterPayment) await game.Act("P1", Play());
            await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["UNIT"])); await game.Top();
            Assert.Contains("UNIT", game.State.PlayerZones["P1"].Hand); Assert.False(game.State.CardObjects["UNIT"].IsEmpowered);
            if (!afterPayment) await game.Act("P1", Play());
            Assert.Equal(afterPayment ? 23 : 21, game.State.RunePools["P1"].Mana);
            Assert.Equal(3, game.State.RunePools["P1"].PowerByTrait[RuneTrait.Blue]);
            Assert.Equal("SPELL", Assert.Single(game.State.StackItems).SourceObjectId);
            JsonRestore(initial, game); await game.Top();
            Assert.Contains("ENEMY", game.State.PlayerZones["P2"].Graveyard);
            Assert.Equal(afterPayment ? 23 : 21, game.State.RunePools["P1"].Mana);
            await game.Replay(); JsonRestore(initial, game);
        }
        finally { await Save("actual-return-payment-boundary-" + afterPayment, initial, game); }
    }

    [Fact]
    public async Task TwoActuallyEmpoweredObjectsReduceTheSpellOnlyOnce()
    {
        var initial = Add(Position(), "SECOND", "VEN·032"); var game = new Driver(initial);
        try
        {
            foreach (var id in new[] { "UNIT", "SECOND" })
            {
                await game.Act("P1", new PlayCardCommand(id, "VEN·032", []));
                await game.Act("P1", new ActivateAbilityCommand(id, "EMPOWER:0:0", [])); await game.Top();
                Assert.True(game.State.CardObjects[id].IsEmpowered);
            }
            Assert.Equal(10, game.State.RunePools["P1"].Mana);
            await game.Act("P1", Play()); Assert.Equal(9, game.State.RunePools["P1"].Mana);
            Assert.Equal(3, game.State.RunePools["P1"].PowerByTrait[RuneTrait.Blue]);
            await game.Top(); Assert.Contains("ENEMY", game.State.PlayerZones["P2"].Graveyard); await game.Replay(); JsonRestore(initial, game);
        }
        finally { await Save("two-empowered-objects-once", initial, game); }
    }

    [Fact]
    public async Task SwiftCanBePlayedByTheNonTurnPlayerInAnActualOpenSpellDuel()
    {
        var initial = Add(Relocate(Position(), "UNIT", "P1", "BASE"), "P2-SPELL", No, "P2"); var game = new Driver(initial);
        try
        {
            await game.Act("P1", new MoveUnitCommand("UNIT", "BASE", "BATTLEFIELD:OTHER"));
            Assert.Equal(TimingStates.SpellDuelOpen, game.State.TimingState); Assert.Equal("P1", game.State.TurnPlayerId);
            if (game.State.FocusPlayerId != "P2") await game.Act(game.State.FocusPlayerId!, new PassFocusCommand());
            Assert.Equal("P2", game.State.FocusPlayerId);
            await game.Act("P2", new PlayCardCommand("P2-SPELL", No, ["ALLY"]));
            Assert.Equal(17, game.State.RunePools["P2"].Mana); Assert.Equal(19, game.State.RunePools["P2"].TotalPower);
            Assert.Equal(TimingStates.SpellDuelClosed, game.State.TimingState); Assert.Equal(0, game.State.CardObjects["ALLY"].Damage);
            JsonRestore(initial, game); await game.Top();
            Assert.Contains("ALLY", game.State.PlayerZones["P1"].Graveyard);
            Assert.Contains("P2-SPELL", game.State.PlayerZones["P2"].Graveyard); await game.Replay(); JsonRestore(initial, game);
        }
        finally { await Save("actual-non-turn-duel-swift", initial, game); }
    }

    [Fact]
    public async Task SwiftCannotBeUsedAsAReactionInsideAnActualClosedChain()
    {
        var initial = Add(Position(), "REACTION", "OGN·048/298"); var game = new Driver(initial);
        try
        {
            await game.Act("P1", new PlayCardCommand("REACTION", "OGN·048/298", []));
            Assert.Equal(TimingStates.NeutralClosed, game.State.TimingState);
            await Reject(game.State, "P1", Play()); Assert.Contains("SPELL", game.State.PlayerZones["P1"].Hand);
            await game.Top(); await game.Replay(); JsonRestore(initial, game);
        }
        finally { await Save("actual-closed-chain-rejects-swift", initial, game); }
    }

    [Theory]
    [InlineData("P1")] [InlineData("P2")]
    public async Task CanonicalOpeningPaysOneManaAndBlueAfterActualEmpowerAndRejectsACoherentManaRefund(string actor)
    {
        var game = await VenEmpowerCanonicalOpeningTests.Open(actor, "lillia-lowcurve", "UNL-R03",
            ["VEN·032", "VEN·032", "VEN·032", No, No, No]);
        try
        {
            await game.WaitFor(actor, 8, "VEN·032", No); await game.Tap(actor);
            var unit = game.Hand(actor, "VEN·032").First();
            await game.Act(actor, new PlayCardCommand(unit, "VEN·032", []));
            await game.Act(actor, new EndTurnCommand()); await game.Act(game.State.TurnPlayerId!, new EndTurnCommand());
            Assert.Equal(actor, game.State.TurnPlayerId); Assert.False(game.State.CardObjects[unit].IsExhausted);
            var field = game.State.CardObjects.Values.First(c => c.Tags.Contains("CARD_TYPE:BATTLEFIELD", StringComparer.Ordinal)).ObjectId;
            await game.Act(actor, new MoveUnitCommand(unit, "BASE", "BATTLEFIELD:" + field));
            Assert.Equal(TimingStates.SpellDuelOpen, game.State.TimingState);
            await game.Act(game.State.FocusPlayerId!, new PassFocusCommand()); await game.Act(game.State.FocusPlayerId!, new PassFocusCommand());
            var runes = game.Runes(actor); Assert.InRange(runes.Length, 9, 12);
            var empowerMana = Math.Max(0, 12 - (runes.Length - 1));
            foreach (var rune in runes.Take(empowerMana + 1)) await game.Act(actor, new TapRuneCommand(rune));
            await game.Act(actor, new RecycleRuneCommand(runes[0]));
            Assert.Equal(runes.Length - 1, game.Runes(actor).Length);
            Assert.Equal(empowerMana + 1, game.State.RunePools[actor].Mana); Assert.Equal(1, game.State.RunePools[actor].PowerByTrait[RuneTrait.Blue]);
            await game.Act(actor, new ActivateAbilityCommand(unit, "EMPOWER:0:0", []));
            Assert.Equal(empowerMana, Assert.Single(game.State.StackItems).EmpowerActivation!.PaidMana);
            await game.Top(); Assert.True(game.State.CardObjects[unit].IsEmpowered);
            Assert.Equal(1, game.State.RunePools[actor].Mana); await game.ColdCheckpoint();
            var spell = game.Hand(actor, No).First();
            await game.Act(actor, new PlayCardCommand(spell, No, [unit]));
            Assert.Equal(0, game.State.RunePools[actor].Mana); Assert.Equal(0, game.State.RunePools[actor].TotalPower);
            Assert.Equal(0, game.State.CardObjects[unit].Damage); Assert.Equal(spell, Assert.Single(game.State.StackItems).SourceObjectId);
            await game.ColdCheckpoint();
            var refunded = game.State with { RunePools = new Dictionary<string, RunePool>(game.State.RunePools) {
                [actor] = game.State.RunePools[actor] with { Mana = 2 } } };
            await game.RejectCoordinatedForgery("invented-empowered-cost-mana-refund", refunded);
            await game.Top(); Assert.Equal(4, game.State.CardObjects[unit].Damage);
            Assert.Contains(spell, game.State.PlayerZones[actor].Graveyard); Assert.Equal(0, game.State.RunePools[actor].Mana);
            await game.ColdCheckpoint();
        }
        finally { await game.Save("ven059-" + actor); }
    }

    private static PlayCardCommand Play(string target = "ENEMY") => new("SPELL", No, [target]);
    private static MatchState Position()
    {
        var state = Add(VenUnitEntryBasicTests.Position("VEN·032"), "SPELL", No);
        state = Relocate(state, "ALLY", "P1", "BATTLEFIELD", "BF");
        state = Relocate(state, "ENEMY", "P2", "BATTLEFIELD", "OTHER");
        return state with { RunePools = new Dictionary<string, RunePool> {
            ["P1"] = new(40, 0, new Dictionary<string, int> { [RuneTrait.Blue] = 4 }), ["P2"] = new(20, 20) } };
    }
    private static MatchState Add(MatchState state, string id, string no, string owner = "P1") => state with {
        CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) { [id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, no, owner) },
        ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { [id] = new(owner, "HAND") },
        PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) { [owner] = state.PlayerZones[owner] with { Hand = [..state.PlayerZones[owner].Hand, id] } } };
    private static MatchState Relocate(MatchState state, string id, string owner, string zone, string? field = null)
    {
        var old = state.PlayerZones[owner];
        var zones = old with { Base = old.Base.Where(x => x != id).ToArray(), Battlefields = old.Battlefields.Where(x => x != id).ToArray(),
            Hand = old.Hand.Where(x => x != id).ToArray() };
        zones = zone == "BASE" ? zones with { Base = [..zones.Base, id] } : zones with { Battlefields = [..zones.Battlefields, id] };
        return state with { ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { [id] = new(owner, zone, field) },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) { [owner] = zones } };
    }
    private static async Task Reject(MatchState state, string player, GameCommand command)
    {
        var hash = MatchStateHasher.Hash(state); var snapshots = JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(state));
        var prompts = JsonSerializer.Serialize(ResolutionResult.BuildPrompts(state));
        var result = await new CoreRuleEngine().ResolveAsync(state, new("illegal-ven059", player, command.CmdType), command, default);
        Assert.False(result.Accepted); Assert.Equal(hash, MatchStateHasher.Hash(result.State)); Assert.Empty(result.Events);
        Assert.Equal(snapshots, JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(result.State)));
        Assert.Equal(prompts, JsonSerializer.Serialize(ResolutionResult.BuildPrompts(result.State)));
    }
    private static void JsonRestore(MatchState initial, Driver game)
    {
        var state = game.State; var sequence = game.Commands().LastOrDefault()?.CompletedEventSequence ?? 0;
        var snapshots = ResolutionResult.BuildSnapshots(state); var prompts = ResolutionResult.BuildPrompts(state);
        var views = state.Seats.Keys.ToDictionary(p => p, p => new RecoveredPlayerView(p, state.Tick, sequence, snapshots[p], state.Tick, sequence, prompts[p]));
        var spectator = MatchReplayRedactor.BuildSpectatorFrame(state.RoomId, state.Tick, sequence, [], state);
        var errors = MatchRecoveryValidator.Validate(state.RoomId, sequence, game.Commands(), game.Events(), views, state, state.Tick, spectator); Assert.Empty(errors);
        var frame = new MatchRecoveryFrame(state.RoomId, state.Tick, sequence, game.Commands(), game.Events(), views, errors, state, initial, spectator) { OpeningSeed = initial.Seed };
        var decoded = JsonSerializer.Deserialize<MatchRecoveryFrame>(JsonSerializer.Serialize(frame, Json), Json)!;
        Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(decoded.AuthoritativeState!));
        var restored = MatchSession.Restore(decoded, new CoreRuleEngine(), NoopMatchJournal.Instance);
        foreach (var player in state.Seats.Keys)
        {
            Assert.Equal(MatchStateHasher.HashValue(snapshots[player]), MatchStateHasher.HashValue(restored.SnapshotFor(player)));
            Assert.Equal(MatchStateHasher.HashValue(prompts[player]), MatchStateHasher.HashValue(restored.PromptFor(player)));
        }
    }
    private static async Task Save(string name, MatchState initial, Driver game)
    {
        if (Environment.GetEnvironmentVariable("RIFTBOUND_VEN059_EVIDENCE") is not { Length: > 0 } output) return;
        Directory.CreateDirectory(output);
        await File.WriteAllTextAsync(Path.Combine(output, name + ".json"), JsonSerializer.Serialize(new {
            kind = "EXPLICIT_MIDGAME_RULE_CONSUMER_NOT_CANONICAL_OPENING", initial, final = game.State,
            commands = game.Commands(), events = game.Events(), actualFinalHash = MatchStateHasher.Hash(game.State) }, Json));
    }
}
