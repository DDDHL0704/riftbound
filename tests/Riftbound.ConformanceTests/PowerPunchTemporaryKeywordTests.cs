using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class PowerPunchTemporaryKeywordTests
{
    private const string No = "UNL-010/219";
    private const string Grant = "COMBAT_KEYWORD:强攻2:";

    [Fact]
    public async Task TheExactCurrentPrintingKeepsSwiftTypedPaymentAndBothTemporaryKeywords()
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadDefaultAsync()).Cards, c => c.CardNo == No);
        Assert.Equal(34533, card.Id); Assert.Equal(1, card.Energy); Assert.Equal(1, card.ReturnEnergy);
        Assert.Equal("{{迅捷}}（可在你的回合或法术对决中打出。）\n让一名单位本回合内获得{{强攻2}}和{{游走}}。（如果它是进攻方，则{{S}}+2。它可以向其它战场进行移动。）", card.CardEffect);
        Assert.True(CardBehaviorRegistry.TryGetByCardNo(No, out var b));
        Assert.Equal("COMBAT_KEYWORD:强攻2", b.StatusEffectId); Assert.Equal("ROAM", b.SecondaryStatusEffectId);
        Assert.Equal(0, b.PowerModifierAmount); Assert.True(b.CanPlayDuringSpellDuel); Assert.False(b.CanPlayDuringPriority);
        Assert.Equal(1, PrintedPowerCostRules.ForCard(No).Amount);
    }

    [Theory]
    [InlineData(false, 1, false)] [InlineData(false, 2, false)]
    [InlineData(false, 1, true)] [InlineData(false, 2, true)]
    [InlineData(true, 1, false)] [InlineData(true, 2, false)]
    [InlineData(true, 1, true)] [InlineData(true, 2, true)]
    public async Task RealCastsBeforeOrDuringTheDuelKeepPrintedAssaultEachGrantAndAnIndependentAura(bool during, int copies, bool aura)
    {
        var initial = BattlePosition(copies, aura); var driver = new Driver(initial);
        await driver.Act("P1", new PlayCardCommand("ALLY", "OGN·210/298", [], Destination: "BASE"));
        Assert.Contains(CardCombatKeywordNames.Assault, driver.State.CardObjects["ALLY"].Tags);
        if (!during) await Casts();
        await driver.Act("P1", new PlayCardCommand("READY", "UNL-009/219", ["ALLY"])); await driver.Top();
        await driver.Act("P1", new MoveUnitCommand("ALLY", "BASE", "BATTLEFIELD:OTHER", [], aura ? ["ALLY", "FARRON"] : ["ALLY"]));
        if (during) await Casts();
        await driver.Act(driver.State.FocusPlayerId!, new PassFocusCommand()); await driver.Act(driver.State.FocusPlayerId!, new PassFocusCommand());
        await driver.Act("P1", new DeclareBattleCommand("OTHER", aura ? ["ALLY", "FARRON"] : ["ALLY"], ["ENEMY", "ENEMY2"], ["COMBAT_ASSIGNMENT"]));
        var expected = 3 + 2 * copies + (aura ? 1 : 0); var actual = Might(driver.State, "P1", "ALLY");
        await driver.Replay(); Export($"battle-frontier-{during}-{copies}-{aura}", initial, driver, new { expected, actual });
        Assert.Equal(expected, actual); Assert.Equal(2, driver.State.CardObjects["ALLY"].Power);
        Assert.Equal(copies, driver.State.CardObjects["ALLY"].UntilEndOfTurnEffects.Count(e => e.StartsWith(Grant, StringComparison.Ordinal)));
        Assert.Contains("ROAM", driver.State.CardObjects["ALLY"].UntilEndOfTurnEffects);
        Assert.Empty(driver.State.CardObjects["ALLY"].UntilEndOfTurnPowerModifiers);
        var prompt = Assert.Single(ResolutionResult.BuildPrompts(driver.State)["P1"].Candidates!, c => c.Action == CommandTypes.AssignCombatDamage);
        Assert.Equal(expected, Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(prompt.Metadata!["damagePool"])["ALLY"]);
        var assignments = 0;
        while (driver.State.BattleState.IsActive && assignments < 4) {
            var next = ResolutionResult.BuildPrompts(driver.State).Values.First(p => p.Candidates?.Any(c => c.Action == CommandTypes.AssignCombatDamage && c.Enabled) == true);
            await driver.Act(next.PlayerId, new AssignCombatDamageCommand(driver.State.BattleState.BattleId!, "OTHER", CombatTestDriver.Assignments(next))); assignments++;
        }
        Assert.Equal(2, assignments); Assert.False(driver.State.BattleState.IsActive);
        var damage = driver.Events().Where(e => e.Event.Kind == "DAMAGE_APPLIED" && Equals(e.Event.Payload.GetValueOrDefault("sourceObjectId"), "ALLY")).ToArray();
        Assert.NotEmpty(damage); Assert.Equal(expected, damage.Sum(e => Convert.ToInt32(e.Event.Payload["damage"])));
        Assert.All(damage, e => Assert.Equal(expected, Convert.ToInt32(e.Event.Payload["combatPower"])));
        Assert.Equal(2, driver.State.CardObjects["ALLY"].Power); Assert.False(driver.State.CardObjects["ALLY"].IsAttacking);
        await driver.Act("P1", new EndTurnCommand()); Assert.Empty(driver.State.CardObjects["ALLY"].UntilEndOfTurnEffects);
        await driver.Replay(); Export($"battle-complete-{during}-{copies}-{aura}", initial, driver, new { expectedDamage = expected });

        async Task Casts()
        {
            foreach (var id in copies == 1 ? new[] { "SPELL" } : ["SPELL", "SECOND"]) {
                if (during && driver.State.FocusPlayerId is { } focus && focus != "P1") await driver.Act(focus, new PassFocusCommand());
                var command = new PlayCardCommand(id, No, ["ALLY"]);
                var quote = new CoreRuleEngine().PreviewPlayCard(driver.State, "P1", PlayCostPreviewTests.Request(driver.State, command));
                Assert.True(quote.CanPay, quote.Message); Assert.Equal(1, quote.Cost!.Mana); Assert.Equal(1, quote.Cost.PowerByTrait[RuneTrait.Red]);
                var mana = driver.State.RunePools["P1"].Mana; var red = driver.State.RunePools["P1"].PowerByTrait[RuneTrait.Red];
                await driver.Act("P1", command); Assert.Equal(mana - 1, driver.State.RunePools["P1"].Mana);
                Assert.Equal(red - 1, driver.State.RunePools["P1"].PowerByTrait[RuneTrait.Red]); await driver.Top();
            }
        }
    }

    [Theory]
    [InlineData("ALLY", false)] [InlineData("ALLY", true)] [InlineData("ENEMY", false)] [InlineData("ENEMY", true)]
    public async Task EitherPlayersUnitGetsBothKeywordsWithoutReadyingOrChangingNeutralMightAndBothExpire(string target, bool exhausted)
    {
        var initial = Neutral(); initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            [target] = initial.CardObjects[target] with { IsExhausted = exhausted } } };
        var driver = new Driver(initial); await driver.Act("P1", new PlayCardCommand("SPELL", No, [target])); await driver.Top();
        var unit = driver.State.CardObjects[target]; var player = target == "ALLY" ? "P1" : "P2";
        Assert.Equal(exhausted, unit.IsExhausted); Assert.Equal(4, unit.Power); Assert.Equal(4, Might(driver.State, player, target));
        Assert.Single(unit.UntilEndOfTurnEffects, e => e.StartsWith(Grant, StringComparison.Ordinal)); Assert.Contains("ROAM", unit.UntilEndOfTurnEffects);
        Assert.Empty(unit.UntilEndOfTurnPowerModifiers); Assert.Equal(19, driver.State.RunePools["P1"].Mana); Assert.Equal(9, driver.State.RunePools["P1"].TotalPower);
        await driver.Act("P1", new EndTurnCommand()); Assert.Empty(driver.State.CardObjects[target].UntilEndOfTurnEffects);
        Assert.Equal(4, driver.State.CardObjects[target].Power); await driver.Replay(); Export($"neutral-expiry-{target}-{exhausted}", initial, driver, new { expectedMight = 4 });
    }

    [Fact]
    public async Task TheActualRoamMoveWorksThisTurnAndCannotBeReusedAfterTheGrantExpires()
    {
        var initial = BattlePosition(1, false);
        foreach (var id in new[] { "ENEMY", "ENEMY2" }) initial = LocalDestructionRecallTests.At(initial, id, "P2", "BASE");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["OTHER"] = initial.CardObjects["OTHER"] with { ControllerId = null } } };
        var driver = new Driver(initial); await driver.Act("P1", new PlayCardCommand("ALLY", "OGN·210/298", [], Destination: "BATTLEFIELD:BF"));
        await driver.Act("P1", new PlayCardCommand("SPELL", No, ["ALLY"])); await driver.Top();
        await driver.Act("P1", new PlayCardCommand("READY", "UNL-009/219", ["ALLY"])); await driver.Top();
        await driver.Act("P1", new MoveUnitCommand("ALLY", "BATTLEFIELD:BF", "BATTLEFIELD:OTHER", ["ROAM"]));
        Assert.Equal("OTHER", driver.State.ObjectLocations["ALLY"].BattlefieldObjectId);
        Assert.Equal(TimingStates.SpellDuelOpen, driver.State.TimingState);
        await driver.Act(driver.State.FocusPlayerId!, new PassFocusCommand()); await driver.Act(driver.State.FocusPlayerId!, new PassFocusCommand());
        Assert.False(driver.State.BattleState.IsActive);
        await driver.Act("P1", new EndTurnCommand()); Assert.Empty(driver.State.CardObjects["ALLY"].UntilEndOfTurnEffects);
        await driver.Act("P2", new EndTurnCommand()); Assert.False(driver.State.CardObjects["ALLY"].IsExhausted);
        await OptionalEntryTargetBoundaryTests.Reject(driver.State, "P1", new MoveUnitCommand("ALLY", "BATTLEFIELD:OTHER", "BATTLEFIELD:BF", ["ROAM"]));
        await driver.Replay(); Export("real-roam-then-expiry", initial, driver, new { expectedCrossBattlefieldMoves = 1, expiredRoamRejected = true });
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ReturningBeforeOrAfterResolutionSeparatesBothGrantsFromTheNewIncarnation(bool before)
    {
        var initial = OptionalEntryTargetConfirmationTests.AddSpell(Neutral(), "RETURN", "OGN·104/298"); var driver = new Driver(initial);
        var generation = initial.CardObjects["ALLY"].ObjectGeneration;
        await driver.Act("P1", new PlayCardCommand("SPELL", No, ["ALLY"]));
        if (!before) { await driver.Top(); Assert.Contains("ROAM", driver.State.CardObjects["ALLY"].UntilEndOfTurnEffects); }
        await driver.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["ALLY"])); await driver.Top(); if (before) await driver.Top();
        Assert.Contains("ALLY", driver.State.PlayerZones["P1"].Hand); Assert.True(driver.State.CardObjects["ALLY"].ObjectGeneration > generation);
        Assert.Empty(driver.State.CardObjects["ALLY"].UntilEndOfTurnEffects); Assert.Equal(4, driver.State.CardObjects["ALLY"].Power);
        await driver.Replay(); Export("generation-return-" + before, initial, driver, new { noLeakedGrant = true });
    }

    [Fact]
    public async Task ARealWholeSpellCounterPreventsBothKeywordsWhileKeepingManaAndRedPayment()
    {
        var initial = AttackDamageDistributionTests.Spell(Neutral(), "COUNTER", "OGN·045/298", "P2"); var driver = new Driver(initial);
        await driver.Act("P1", new PlayCardCommand("SPELL", No, ["ALLY"])); var item = Assert.Single(driver.State.StackItems);
        await driver.Act("P1", new PassPriorityCommand()); await driver.Act("P2", new PlayCardCommand("COUNTER", "OGN·045/298", [item.StackItemId])); await driver.Top();
        Assert.Empty(driver.State.StackItems); Assert.Empty(driver.State.CardObjects["ALLY"].UntilEndOfTurnEffects);
        Assert.Equal(19, driver.State.RunePools["P1"].Mana); Assert.Equal(9, driver.State.RunePools["P1"].TotalPower);
        Assert.Equal(4, driver.State.CardObjects["ALLY"].Power); await driver.Replay(); Export("counter-both-grants", initial, driver, new { grants = 0 });
    }

    [Theory]
    [InlineData("ALLY", 4)] [InlineData("ENEMY", 6)]
    public async Task TheOpponentsRealMovementDuelAllowsSwiftAndOnlyItsAttackerUsesTheBonus(string target, int expected)
    {
        var initial = Neutral() with { TurnPlayerId = "P2", ActivePlayerId = "P2" };
        initial = LocalDestructionRecallTests.At(initial, "ALLY", "P1", "BATTLEFIELD", "BF");
        initial = LocalDestructionRecallTests.At(initial, "ENEMY", "P2", "BASE");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) { ["ENEMY"] = initial.CardObjects["ENEMY"] with { IsExhausted = false } } };
        var driver = new Driver(initial); await driver.Act("P2", new MoveUnitCommand("ENEMY", "BASE", "BATTLEFIELD:BF", []));
        await driver.Act("P2", new PassFocusCommand()); await driver.Act("P1", new PlayCardCommand("SPELL", No, [target])); await driver.Top();
        Export("offturn-frontier-" + target, initial, driver, new { expected, actual = Might(driver.State, target == "ALLY" ? "P1" : "P2", target) });
        Assert.Equal(4, driver.State.CardObjects[target].Power); Assert.Equal(expected, Might(driver.State, target == "ALLY" ? "P1" : "P2", target));
        Assert.Single(driver.State.CardObjects[target].UntilEndOfTurnEffects, e => e.StartsWith(Grant, StringComparison.Ordinal));
        await driver.Replay();
    }

    [Theory]
    [InlineData("mana")] [InlineData("red")] [InlineData("wrong-turn")] [InlineData("priority")]
    [InlineData("equipment")] [InlineData("battlefield")] [InlineData("graveyard")] [InlineData("deck")]
    [InlineData("missing")] [InlineData("extra")] [InlineData("source-graveyard")] [InlineData("duel-focus")]
    public async Task IllegalPaymentTimingSourcesAndTargetsCannotPartlyApplyEitherGrant(string branch)
    {
        var initial = Neutral(); string[] targets = ["ALLY"];
        if (branch is "mana" or "red") initial = initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) {
            ["P1"] = new(branch == "mana" ? 0 : 20, 0, new Dictionary<string, int> { [branch == "mana" ? RuneTrait.Red : RuneTrait.Blue] = 10 }) } };
        if (branch == "wrong-turn") initial = initial with { TurnPlayerId = "P2", ActivePlayerId = "P2" };
        if (branch == "duel-focus") initial = initial with { TimingState = TimingStates.SpellDuelOpen, FocusPlayerId = "P2", ActivePlayerId = "P2" };
        if (branch == "priority") {
            var driver = new Driver(OptionalEntryTargetConfirmationTests.AddSpell(initial, "PENDING", "OGN·048/298"));
            await driver.Act("P1", new PlayCardCommand("PENDING", "OGN·048/298", [])); initial = driver.State;
        }
        if (branch == "source-graveyard") initial = initial with {
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) { ["SPELL"] = new("P1", "GRAVEYARD") },
            PlayerZones = initial.PlayerZones.ToDictionary(p => p.Key, p => p.Key == "P1" ? p.Value with { Hand = [], Graveyard = [..p.Value.Graveyard, "SPELL"] } : p.Value) };
        if (branch == "equipment") targets = ["GEAR"]; if (branch == "battlefield") targets = ["BF"];
        if (branch == "graveyard") targets = ["GRAVE"]; if (branch == "deck") targets = ["P1-DRAW-1"];
        if (branch == "missing") targets = []; if (branch == "extra") targets = ["ALLY", "ENEMY"];
        await OptionalEntryTargetBoundaryTests.Reject(initial, "P1", new PlayCardCommand("SPELL", No, targets));
    }

    private static MatchState Neutral()
    {
        var s = VenFlowCombatKeywordTests.Position(false); return s with { RoomId = "POWER-PUNCH-TEMPORARY-KEYWORDS",
            CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) { ["SPELL"] = OfficialGarenConquestBattlefieldCountTests.Printed("SPELL", No, "P1") } };
    }
    private static MatchState BattlePosition(int copies, bool aura)
    {
        var s = CleaveTemporaryAssaultTests.Position(copies, aura); var cards = s.CardObjects.ToDictionary();
        foreach (var id in copies == 1 ? new[] { "SPELL" } : ["SPELL", "SECOND"]) cards[id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, No, "P1");
        foreach (var id in new[] { "ENEMY", "ENEMY2" }) cards[id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, "OGN·271/298", "P2");
        return s with { RoomId = "POWER-PUNCH-ACTUAL-ASSAULT-BATTLE", CardObjects = cards };
    }
    private static int Might(MatchState state, string player, string id) => CleaveTemporaryAssaultTests.Might(state, player, id);
    private static void Export(string name, MatchState initial, Driver driver, object expected)
    {
        if (Environment.GetEnvironmentVariable("RIFTBOUND_POWER_PUNCH_EVIDENCE") is not { Length: > 0 } root) return;
        Directory.CreateDirectory(root); File.WriteAllText(Path.Combine(root, name + ".json"), JsonSerializer.Serialize(new {
            initial, final = driver.State, commands = driver.Commands(), events = driver.Events(), expected
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));
    }
}
