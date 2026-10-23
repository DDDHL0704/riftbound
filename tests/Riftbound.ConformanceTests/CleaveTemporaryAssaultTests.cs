using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class CleaveTemporaryAssaultTests
{
    internal const string No = "OGN·004/298";

    [Fact]
    public async Task TheExactPrintingGrantsAnAttackerOnlyKeywordRatherThanChangingStoredPower()
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadDefaultAsync()).Cards, c => c.CardNo == No);
        Assert.Equal(31208, card.Id); Assert.Equal(1, card.Energy); Assert.Null(card.ReturnEnergy);
        Assert.Equal("{{迅捷}}（可在你的回合或法术对决中打出。）\n让一名单位本回合内获得{{强攻3}}。（如果它是进攻方，则{{S}}+3。）", card.CardEffect);
        Assert.True(CardBehaviorRegistry.TryGetByCardNo(No, out var behavior));
        Assert.Equal("COMBAT_KEYWORD:强攻3", behavior.StatusEffectId);
        Assert.Equal(0, behavior.PowerModifierAmount);
        Assert.True(behavior.CanPlayDuringSpellDuel); Assert.False(behavior.CanPlayDuringPriority);
    }

    [Theory]
    [InlineData(false, 1, false)] [InlineData(false, 2, false)]
    [InlineData(false, 1, true)] [InlineData(false, 2, true)]
    [InlineData(true, 1, false)] [InlineData(true, 2, false)]
    [InlineData(true, 1, true)] [InlineData(true, 2, true)]
    public async Task CastingBeforeOrDuringTheBattleDuelKeepsEachGrantAndIndependentAuraSeparate(bool duringDuel, int copies, bool aura)
    {
        var initial = Position(copies, aura); var driver = new Driver(initial);
        await driver.Act("P1", new PlayCardCommand("ALLY", "OGN·210/298", [], Destination: "BASE"));
        Assert.Equal(2, driver.State.CardObjects["ALLY"].Power);
        Assert.Contains(CardCombatKeywordNames.Assault, driver.State.CardObjects["ALLY"].Tags);
        if (!duringDuel) await Grants();
        await driver.Act("P1", new PlayCardCommand("READY", "UNL-009/219", ["ALLY"])); await driver.Top();
        await driver.Act("P1", new MoveUnitCommand("ALLY", "BASE", "BATTLEFIELD:OTHER", [], aura ? ["ALLY", "FARRON"] : ["ALLY"]));
        Assert.True(driver.State.CardObjects["ALLY"].IsAttacking);
        if (duringDuel) await Grants();
        await driver.Act(driver.State.FocusPlayerId!, new PassFocusCommand());
        await driver.Act(driver.State.FocusPlayerId!, new PassFocusCommand());
        await driver.Act("P1", new DeclareBattleCommand("OTHER", aura ? ["ALLY", "FARRON"] : ["ALLY"], ["ENEMY", "ENEMY2"], ["COMBAT_ASSIGNMENT"]));
        Assert.True(driver.State.BattleState.IsActive); Assert.True(driver.State.CardObjects["ALLY"].IsAttacking);
        var expected = 2 + 1 + copies * 3 + (aura ? 1 : 0); var actual = Might(driver.State, "P1", "ALLY");
        await driver.Replay(); Export($"battle-{duringDuel}-{copies}-{aura}", initial, driver, expected, actual);
        Assert.Equal(expected, actual); Assert.Equal(2, driver.State.CardObjects["ALLY"].Power);
        Assert.Equal(copies, driver.State.CardObjects["ALLY"].UntilEndOfTurnEffects.Count(e => e.StartsWith("COMBAT_KEYWORD:强攻3:", StringComparison.Ordinal)));
        Assert.DoesNotContain("OVERWHELM_3", driver.State.CardObjects["ALLY"].UntilEndOfTurnEffects);
        Assert.Empty(driver.State.CardObjects["ALLY"].UntilEndOfTurnPowerModifiers);
        var prompt = Assert.Single(ResolutionResult.BuildPrompts(driver.State)["P1"].Candidates!, c => c.Action == CommandTypes.AssignCombatDamage);
        var pool = Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(prompt.Metadata!["damagePool"]);
        Assert.Equal(expected, pool["ALLY"]);

        async Task Grants()
        {
            Assert.Equal(duringDuel, driver.State.CardObjects["ALLY"].IsAttacking);
            foreach (var id in copies == 1 ? new[] { "SPELL" } : ["SPELL", "SECOND"]) {
                if (duringDuel && driver.State.FocusPlayerId is { } focus && focus != "P1")
                    await driver.Act(focus, new PassFocusCommand());
                var mana = driver.State.RunePools["P1"].Mana; var power = driver.State.RunePools["P1"].TotalPower;
                await driver.Act("P1", new PlayCardCommand(id, No, ["ALLY"]));
                Assert.Equal(mana - 1, driver.State.RunePools["P1"].Mana); Assert.Equal(power, driver.State.RunePools["P1"].TotalPower);
                await driver.Top();
            }
        }
    }

    internal static MatchState Position(int copies = 1, bool aura = false)
    {
        var state = VenFlowCombatKeywordTests.Position(false);
        var cards = state.CardObjects.ToDictionary(); var zones = state.PlayerZones.ToDictionary(); var locations = state.ObjectLocations.ToDictionary();
        cards["SPELL"] = OfficialGarenConquestBattlefieldCountTests.Printed("SPELL", No, "P1");
        cards["ALLY"] = OfficialGarenConquestBattlefieldCountTests.Printed("ALLY", "OGN·210/298", "P1");
        cards["ENEMY2"] = OfficialGarenConquestBattlefieldCountTests.Printed("ENEMY2", "OGN·219/298", "P2");
        zones["P1"] = zones["P1"] with { Base = zones["P1"].Base.Where(id => id != "ALLY").ToArray(), Hand = [..zones["P1"].Hand, "ALLY"] };
        zones["P2"] = zones["P2"] with { Battlefields = [..zones["P2"].Battlefields, "ENEMY2"] };
        locations["ALLY"] = new("P1", "HAND");
        locations["ENEMY2"] = new("P2", "BATTLEFIELD", "OTHER");
        if (aura) {
            cards["FARRON"] = OfficialGarenConquestBattlefieldCountTests.Printed("FARRON", "OGN·015/298", "P1");
            zones["P1"] = zones["P1"] with { Base = [..zones["P1"].Base, "FARRON"] }; locations["FARRON"] = new("P1", "BASE");
        }
        state = state with { RoomId = "CLEAVE-TEMPORARY-ASSAULT", CardObjects = cards, PlayerZones = zones, ObjectLocations = locations };
        state = OptionalEntryTargetConfirmationTests.AddSpell(state, "READY", "UNL-009/219");
        return copies == 2 ? OptionalEntryTargetConfirmationTests.AddSpell(state, "SECOND", No) : state;
    }

    internal static int Might(MatchState state, string player, string id) => CurrentPowerTargetTests.Snapshot(state)
        .GetProperty("players").GetProperty(player).GetProperty("objects").GetProperty(id).GetProperty("effectivePower").GetInt32();

    internal static void Export(string name, MatchState initial, Driver driver, int? expected = null, int? actual = null)
    {
        if (Environment.GetEnvironmentVariable("RIFTBOUND_CLEAVE_EVIDENCE") is not { Length: > 0 } root) return;
        Directory.CreateDirectory(root); File.WriteAllText(Path.Combine(root, name + ".json"), JsonSerializer.Serialize(new {
            initial, final = driver.State, commands = driver.Commands(), events = driver.Events(), expected, actual
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));
    }
}
