using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.Ven103AnyGraveyardReturnConsumerTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class VenFlowCombatKeywordTests
{
    private const string No = "VEN·012";
    private const string Grant = "COMBAT_KEYWORD:强攻3:1";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    [Fact]
    public async Task TheExactCurrentFaceKeepsItsPrintedCostReadyEffectAssaultAndAlternativeFlowCost()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(); var card = Assert.Single(catalog.Cards, c => c.CardNo == No);
        Assert.Equal(37697, card.Id); Assert.Equal(3, card.Energy); Assert.Equal(1, card.ReturnEnergy); Assert.Null(card.Power);
        Assert.Equal("让一名单位变为活跃状态，并给予其在本回合内{{强攻3}}。（如果其为进攻方，则{{S}}+3。）\n{{流转3红色}}（你可以选择支付此牌的流转费用，以此将其从你的废牌堆中打出。然后将其放逐。）", card.CardEffect);
        Assert.True(CardBehaviorRegistry.TryGetByCardNo(No, out var behavior));
        Assert.True(behavior.ReadiesTarget); Assert.Equal("COMBAT_KEYWORD:强攻3", behavior.StatusEffectId);
        Assert.Equal(0, behavior.PowerModifierAmount); Assert.False(behavior.CanPlayDuringPriority); Assert.False(behavior.CanPlayDuringSpellDuel);
        Assert.Equal(3, behavior.FlowCost!.Mana); Assert.Equal(1, behavior.FlowCost.Power); Assert.Equal([RuneTrait.Red], behavior.FlowCost.Traits);
        Assert.Equal(1, PrintedPowerCostRules.ForCard(No).Amount);
    }

    public static IEnumerable<object[]> OrdinaryAndFlowTargets() => new[] { false, true }.SelectMany(flow => new[] { "ALLY", "ENEMY" }
        .SelectMany(target => new[] { false, true }.Select(exhausted => new object[] { flow, target, exhausted })));

    [Theory]
    [MemberData(nameof(OrdinaryAndFlowTargets))]
    public async Task BothPermittedZonesReadyEitherPlayersUnitAndGrantAKeywordWithoutChangingItsRawPower(bool flow, string target, bool exhausted)
    {
        var initial = Position(flow); initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            [target] = initial.CardObjects[target] with { IsExhausted = exhausted } } };
        var driver = new Driver(initial); var command = new PlayCardCommand("SPELL", No, [target]);
        var quote = new CoreRuleEngine().PreviewPlayCard(initial, "P1", PlayCostPreviewTests.Request(initial, command));
        Assert.True(quote.CanPay, quote.Message); Assert.Equal(3, quote.Cost!.Mana); Assert.Equal(1, quote.Cost.PowerByTrait[RuneTrait.Red]);
        await driver.Act("P1", command);
        Assert.Equal(exhausted, driver.State.CardObjects[target].IsExhausted);
        Assert.Empty(driver.State.CardObjects[target].UntilEndOfTurnEffects); Assert.Equal(17, driver.State.RunePools["P1"].Mana);
        Assert.Equal(9, driver.State.RunePools["P1"].TotalPower);
        var item = Assert.Single(driver.State.StackItems); Assert.Equal(flow, item.FlowPlay is not null);
        await driver.Top();
        Assert.False(driver.State.CardObjects[target].IsExhausted); Assert.Equal(4, driver.State.CardObjects[target].Power);
        Assert.Contains(Grant, driver.State.CardObjects[target].UntilEndOfTurnEffects);
        Assert.Empty(driver.State.CardObjects[target].UntilEndOfTurnPowerModifiers);
        Assert.DoesNotContain(CardCombatKeywordNames.Assault, driver.State.CardObjects[target].Tags);
        Assert.Single(flow ? driver.State.PlayerZones["P1"].Banished : driver.State.PlayerZones["P1"].Graveyard, id => id == "SPELL");
        Assert.DoesNotContain("SPELL", driver.State.PlayerZones["P1"].Hand);
        var ready = Assert.Single(driver.Events(), e => e.Event.Kind == "UNIT_READIED" && Equals(e.Event.Payload.GetValueOrDefault("targetObjectId"), target));
        Assert.Equal(exhausted, Assert.IsType<bool>(ready.Event.Payload["wasExhausted"]));
        Assert.False(Assert.IsType<bool>(ready.Event.Payload["isExhausted"]));
        await driver.Act("P1", new EndTurnCommand());
        Assert.DoesNotContain(Grant, driver.State.CardObjects[target].UntilEndOfTurnEffects); Assert.Equal(4, driver.State.CardObjects[target].Power);
        await Replay(driver, initial); Export($"ready-{flow}-{target}-{exhausted}", initial, driver);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task CounteringTheSpellPreventsBothEffectsAndStillHonorsThePhysicalFlowExit(bool flow)
    {
        var initial = AttackDamageDistributionTests.Spell(Position(flow), "COUNTER", "OGN·045/298", "P2");
        var driver = new Driver(initial); await driver.Act("P1", new PlayCardCommand("SPELL", No, ["ALLY"]));
        var spell = Assert.Single(driver.State.StackItems).StackItemId;
        await driver.Act("P1", new PassPriorityCommand());
        await driver.Act("P2", new PlayCardCommand("COUNTER", "OGN·045/298", [spell])); await driver.Top();
        Assert.True(driver.State.CardObjects["ALLY"].IsExhausted); Assert.Empty(driver.State.CardObjects["ALLY"].UntilEndOfTurnEffects);
        Assert.Empty(driver.State.StackItems); Assert.Contains("SPELL", flow ? driver.State.PlayerZones["P1"].Banished : driver.State.PlayerZones["P1"].Graveyard);
        Assert.Equal(17, driver.State.RunePools["P1"].Mana); Assert.Equal(9, driver.State.RunePools["P1"].TotalPower);
        await Replay(driver, initial);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ATargetThatLeavesBeforeResolutionGetsNeitherAReadyEffectNorAKeywordOnItsNewIncarnation(bool flow)
    {
        var initial = AddSpell(Position(flow), "RETURN", "OGN·104/298");
        var driver = new Driver(initial); await driver.Act("P1", new PlayCardCommand("SPELL", No, ["ALLY"]));
        var generation = driver.State.CardObjects["ALLY"].ObjectGeneration;
        await driver.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["ALLY"])); await driver.Top();
        Assert.Contains("ALLY", driver.State.PlayerZones["P1"].Hand);
        Assert.True(driver.State.CardObjects["ALLY"].ObjectGeneration > generation);
        await driver.Top(); Assert.Empty(driver.State.CardObjects["ALLY"].UntilEndOfTurnEffects);
        Assert.Contains("SPELL", flow ? driver.State.PlayerZones["P1"].Banished : driver.State.PlayerZones["P1"].Graveyard);
        await Replay(driver, initial);
    }

    public static IEnumerable<object[]> InvalidPlays() => new[] { false, true }.SelectMany(flow => new[] {
        "mana", "trait", "equipment", "battlefield-target", "graveyard-target", "hand-target", "deck-target", "missing-target", "two-targets", "wrong-turn", "response", "duel" }
        .Select(branch => new object[] { flow, branch }));

    [Theory]
    [MemberData(nameof(InvalidPlays))]
    public async Task FlowDoesNotChangeTimingAndInvalidCostsOrTargetsCannotMoveTheSourceOrGrantEffects(bool flow, string branch)
    {
        var state = Position(flow); string[] targets = ["ALLY"];
        if (branch == "mana") state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(2, 0, new Dictionary<string, int> { [RuneTrait.Red] = 10 }) } };
        if (branch == "trait") state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(20, 0, new Dictionary<string, int> { [RuneTrait.Blue] = 10 }) } };
        if (branch == "equipment") targets = ["GEAR"];
        if (branch == "graveyard-target") targets = ["GRAVE"];
        if (branch == "battlefield-target") targets = ["BF"];
        if (branch == "deck-target") targets = ["P1-DRAW-1"];
        if (branch == "hand-target") {
            state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["HAND-UNIT"] = OfficialGarenConquestBattlefieldCountTests.Printed("HAND-UNIT", "OGN·219/298", "P1") },
                PlayerZones = state.PlayerZones.ToDictionary(p => p.Key, p => p.Key == "P1" ? p.Value with { Hand = [..p.Value.Hand, "HAND-UNIT"] } : p.Value),
                ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["HAND-UNIT"] = new("P1", "HAND") } };
            targets = ["HAND-UNIT"];
        }
        if (branch == "missing-target") targets = [];
        if (branch == "two-targets") targets = ["ALLY", "ENEMY"];
        if (branch == "wrong-turn") state = state with { TurnPlayerId = "P2", ActivePlayerId = "P2" };
        if (branch == "duel") state = state with { TimingState = TimingStates.SpellDuelOpen, FocusPlayerId = "P1" };
        if (branch == "response") {
            state = AddSpell(state, "RESPONSE", "OGN·048/298");
            var opened = await new CoreRuleEngine().ResolveAsync(state, new("open-flow-keyword-response", "P1", CommandTypes.PlayCard), new PlayCardCommand("RESPONSE", "OGN·048/298", []), default);
            Assert.True(opened.Accepted, opened.ErrorMessage); state = opened.State;
        }
        await Reject(state, "P1", new PlayCardCommand("SPELL", No, targets));
    }

    [Theory]
    [InlineData(false, false)] [InlineData(false, true)] [InlineData(true, false)] [InlineData(true, true)]
    public async Task OrdinaryAndFlowPlaysPayWardInAdditionToTheirOwnCostBeforeAnyEffect(bool flow, bool enemy)
    {
        var initial = Position(flow); var target = enemy ? "ENEMY" : "ALLY";
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            [target] = initial.CardObjects[target] with { Tags = initial.CardObjects[target].Tags.Append(CardObjectTags.Spellshield).Distinct(StringComparer.Ordinal).OrderBy(tag => tag, StringComparer.Ordinal).ToArray() } } };
        var driver = new Driver(initial); await driver.Act("P1", new PlayCardCommand("SPELL", No, [target]));
        Assert.Equal(enemy ? 8 : 9, driver.State.RunePools["P1"].TotalPower); Assert.True(driver.State.CardObjects[target].IsExhausted);
        await driver.Top(); Assert.False(driver.State.CardObjects[target].IsExhausted); Assert.Contains(Grant, driver.State.CardObjects[target].UntilEndOfTurnEffects);
        await Replay(driver, initial);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task InsufficientWardCostRejectsTheWholeSpellWithoutPayingOrLeavingItsOriginalZone(bool flow)
    {
        var initial = Position(flow);
        initial = initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) { ["P1"] = new(20, 0, new Dictionary<string, int> { [RuneTrait.Red] = 1 }) },
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["ENEMY"] = initial.CardObjects["ENEMY"] with { Tags = initial.CardObjects["ENEMY"].Tags.Append(CardObjectTags.Spellshield).Distinct(StringComparer.Ordinal).OrderBy(tag => tag, StringComparer.Ordinal).ToArray() } } };
        await Reject(initial, "P1", new PlayCardCommand("SPELL", No, ["ENEMY"]));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task AUnitThatCannotBecomeActiveStillReceivesTheSeparateAssaultGrant(bool flow)
    {
        var initial = Position(flow);
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["ALLY"] = OfficialGarenConquestBattlefieldCountTests.Printed("ALLY", "UNL-144/219", "P1") with { IsExhausted = true } } };
        var power = initial.CardObjects["ALLY"].Power; var driver = new Driver(initial);
        await driver.Act("P1", new PlayCardCommand("SPELL", No, ["ALLY"])); await driver.Top();
        Assert.True(driver.State.CardObjects["ALLY"].IsExhausted);
        Assert.Equal(power, driver.State.CardObjects["ALLY"].Power);
        Assert.Contains(Grant, driver.State.CardObjects["ALLY"].UntilEndOfTurnEffects);
        Assert.DoesNotContain(driver.Events(), e => e.Event.Kind == "UNIT_READIED" && Equals(e.Event.Payload.GetValueOrDefault("targetObjectId"), "ALLY"));
        await Replay(driver, initial);
    }

    internal static MatchState Position(bool flow)
    {
        var state = Ven103AnyGraveyardReturnConsumerTests.UnitEntryPosition(No);
        state = Ven103AnyGraveyardReturnConsumerTests.At(state, "ENEMY", "P2", "BATTLEFIELD", "OTHER");
        var cards = state.CardObjects.ToDictionary();
        cards.Remove("UNIT"); cards["SPELL"] = OfficialGarenConquestBattlefieldCountTests.Printed("SPELL", No, "P1");
        cards["ALLY"] = cards["ALLY"] with { IsExhausted = true }; cards["ENEMY"] = cards["ENEMY"] with { IsExhausted = true };
        var locations = state.ObjectLocations.ToDictionary(); locations.Remove("UNIT"); locations["SPELL"] = new("P1", flow ? "GRAVEYARD" : "HAND");
        return state with { RoomId = "VEN-FLOW-COMBAT-KEYWORD", CardObjects = cards, ObjectLocations = locations,
            PlayerZones = state.PlayerZones.ToDictionary(p => p.Key, p => p.Key == "P1" ? p.Value with { Hand = flow ? [] : ["SPELL"], Graveyard = flow ? [..p.Value.Graveyard, "SPELL"] : p.Value.Graveyard } : p.Value),
            RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(20, 0, new Dictionary<string, int> { [RuneTrait.Red] = 10 }) } };
    }

    private static void Export(string name, MatchState initial, Driver driver)
    {
        if (Environment.GetEnvironmentVariable("RIFTBOUND_FLOW_KEYWORD_EVIDENCE") is not { Length: > 0 } root) return;
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, name + ".json"), JsonSerializer.Serialize(new { initial, final = driver.State,
            commands = driver.Commands(), events = driver.Events(), runtime = RuntimeEvidence() }, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));
    }
    private static MatchState AddSpell(MatchState state, string id, string no)
        => OfficialCopyIdentityTests.AddSpell(state, id, no) with { RunePools = state.RunePools };

    private static async Task Reject(MatchState state, string player, GameCommand command,
        [System.Runtime.CompilerServices.CallerMemberName] string scenario = "")
    {
        var hash = MatchStateHasher.Hash(state); var views = JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(state));
        var prompts = JsonSerializer.Serialize(ResolutionResult.BuildPrompts(state));
        var result = await new CoreRuleEngine().ResolveAsync(state, new("illegal-optional-entry", player, command.CmdType), command, default);
        Assert.False(result.Accepted); Assert.Empty(result.Events); Assert.Equal(hash, MatchStateHasher.Hash(result.State));
        Assert.Equal(views, JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(result.State)));
        Assert.Equal(prompts, JsonSerializer.Serialize(ResolutionResult.BuildPrompts(result.State)));
        FullGameEndToEndTests.AssertNoHiddenZoneLeak(result);
        if (Environment.GetEnvironmentVariable("RIFTBOUND_FLOW_KEYWORD_EVIDENCE") is not { Length: > 0 } output) return;
        var directory = Path.Combine(output, "direct-rejections"); Directory.CreateDirectory(directory);
        var raw = JsonSerializer.SerializeToElement(command, command.GetType(), Json);
        var key = scenario + "-" + MatchStateHasher.HashValue(new { hash, raw })[..16];
        File.WriteAllText(Path.Combine(directory, key + ".json"), JsonSerializer.Serialize(new {
            initial = state, final = result.State, player, command = raw, result.Accepted, result.ErrorCode, result.ErrorMessage,
            events = result.Events, runtime = RuntimeEvidence() }, Json));
    }

    private static async Task Replay(Driver driver, MatchState initial,
        [System.Runtime.CompilerServices.CallerMemberName] string scenario = "")
    {
        await driver.Replay(); JsonRestore(initial, driver);
        if (Environment.GetEnvironmentVariable("RIFTBOUND_FLOW_KEYWORD_EVIDENCE") is not { Length: > 0 } output) return;
        var directory = Path.Combine(output, "all-final"); Directory.CreateDirectory(directory);
        var key = scenario + "-" + MatchStateHasher.HashValue(new {
            initialHash = MatchStateHasher.Hash(initial), command = driver.Commands()[0].RawCommand })[..16];
        File.WriteAllText(Path.Combine(directory, key + ".json"), JsonSerializer.Serialize(new {
            initial, final = driver.State, commands = driver.Commands(), events = driver.Events(), runtime = RuntimeEvidence() }, Json));
    }

    private static object RuntimeEvidence() => new {
        ruleInputs = RuleInputIdentity.Current, baseDirectory = AppContext.BaseDirectory,
        catalogPath = OfficialCardCatalog.DefaultCatalogPath(),
        modules = new[] { typeof(CoreRuleEngine).Assembly, typeof(OfficialCardCatalog).Assembly, typeof(GameCommand).Assembly }
            .Select(a => new { name = a.GetName().Name, path = a.Location, mvid = a.ManifestModule.ModuleVersionId,
                sha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(a.Location))).ToLowerInvariant() }) };
    private static void JsonRestore(MatchState initial, Driver run)
    {
        var state = run.State; var sequence = run.Commands()[^1].CompletedEventSequence;
        var snapshots = ResolutionResult.BuildSnapshots(state); var prompts = ResolutionResult.BuildPrompts(state);
        var views = state.Seats.Keys.ToDictionary(p => p, p => new RecoveredPlayerView(p, state.Tick, sequence, snapshots[p], state.Tick, sequence, prompts[p]));
        var frame = new MatchRecoveryFrame(state.RoomId, state.Tick, sequence, run.Commands(), run.Events(), views, [], state, initial,
            MatchReplayRedactor.BuildSpectatorFrame(state.RoomId, state.Tick, sequence, [], state)) { OpeningSeed = initial.Seed };
        var decoded = JsonSerializer.Deserialize<MatchRecoveryFrame>(JsonSerializer.Serialize(frame, Json), Json)!;
        Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(decoded.AuthoritativeState!));
        Assert.Empty(MatchRecoveryValidator.Validate(decoded.RoomId, decoded.LastEventSequence, decoded.Commands, decoded.Events,
            decoded.PlayerViews, decoded.AuthoritativeState, decoded.CurrentTick, decoded.SpectatorReplayFrame));
        var restored = MatchSession.Restore(decoded, new CoreRuleEngine(), run);
        foreach (var player in state.Seats.Keys) {
            Assert.Equal(MatchStateHasher.HashValue(snapshots[player]), MatchStateHasher.HashValue(restored.SnapshotFor(player)));
            Assert.Equal(MatchStateHasher.HashValue(prompts[player]), MatchStateHasher.HashValue(restored.PromptFor(player)));
        }
    }

}
