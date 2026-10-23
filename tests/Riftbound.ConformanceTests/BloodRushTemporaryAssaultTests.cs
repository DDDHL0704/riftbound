using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class BloodRushTemporaryAssaultTests
{
    internal const string No = "SFD·003/221";
    internal const string EffectiveText = "{{迅捷}}（可在你的回合或法术对决中打出。）\n{{回响1}}（你可以选择支付此额外费用，以重复此法术效果。）\n让一名单位本回合内获得{{强攻2}}。（如果它是进攻方，则{{S}}+2。）";

    [Fact]
    public async Task ExactCurrentErrataGivesSwiftOneManaEchoAndATemporaryAssaultGrant()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(); var card = Assert.Single(catalog.Cards, c => c.CardNo == No);
        Assert.Equal(33075, card.Id); Assert.Equal(1, card.Energy); Assert.Null(card.ReturnEnergy); Assert.Null(card.Power);
        Assert.Equal(EffectiveText, card.CardEffect);
        var printed = Assert.Single((await OfficialCardCatalogLoader.LoadFromFileAsync(OfficialCardCatalog.DefaultCatalogPath(), default)).Cards, c => c.CardNo == No);
        Assert.Equal("{{迅捷}}（可在你的回合或法术对决中打出。）\n{{回响1}}（你可以选择支付此额外费用，以重复此法术效果。）\n让一名单位获得{{强攻2}}。（如果它是进攻方，则{{S}}+2。）", printed.CardEffect);
        Assert.True(CardBehaviorRegistry.TryGetByCardNo(No, out var behavior));
        Assert.Equal("COMBAT_KEYWORD:强攻2", behavior.StatusEffectId); Assert.Equal(0, behavior.PowerModifierAmount);
        Assert.True(behavior.CanPlayDuringSpellDuel); Assert.False(behavior.CanPlayDuringPriority);
        Assert.Equal(1, EchoCostRules.PrintedFor(No)!.Mana);
    }

    [Theory]
    [InlineData(false, false, 1)] [InlineData(false, true, 1)]
    [InlineData(false, false, 2)] [InlineData(false, true, 2)]
    [InlineData(true, false, 1)] [InlineData(true, true, 1)]
    [InlineData(true, false, 2)] [InlineData(true, true, 2)]
    public async Task EachActualEchoEffectAndEachPhysicalCastGrantIndependentAssaultBeforeOrDuringTheDuel(bool duringDuel, bool echo, int copies)
    {
        var initial = Position(copies); var driver = new Driver(initial);
        await driver.Act("P1", new PlayCardCommand("ALLY", "OGN·210/298", [], Destination: "BASE"));
        Assert.Contains(CardCombatKeywordNames.Assault, driver.State.CardObjects["ALLY"].Tags);
        if (!duringDuel) await Grants();
        await driver.Act("P1", new PlayCardCommand("READY", "UNL-009/219", ["ALLY"])); await driver.Top();
        await driver.Act("P1", new MoveUnitCommand("ALLY", "BASE", "BATTLEFIELD:OTHER", []));
        if (duringDuel) await Grants();
        await driver.Act(driver.State.FocusPlayerId!, new PassFocusCommand()); await driver.Act(driver.State.FocusPlayerId!, new PassFocusCommand());
        await driver.Act("P1", new DeclareBattleCommand("OTHER", ["ALLY"], ["ENEMY", "ENEMY2"], ["COMBAT_ASSIGNMENT"]));
        Assert.True(driver.State.BattleState.IsActive); Assert.True(driver.State.CardObjects["ALLY"].IsAttacking);
        var grants = copies * (echo ? 2 : 1); var expected = 3 + 2 * grants; var actual = Might(driver.State);
        await driver.Replay(); Export($"battle-{duringDuel}-{echo}-{copies}", initial, driver, expected, actual);
        Assert.Equal(expected, actual); Assert.Equal(2, driver.State.CardObjects["ALLY"].Power);
        Assert.Equal(grants, driver.State.CardObjects["ALLY"].UntilEndOfTurnEffects.Count(e => e.StartsWith("COMBAT_KEYWORD:强攻2:", StringComparison.Ordinal)));
        Assert.Empty(driver.State.CardObjects["ALLY"].UntilEndOfTurnPowerModifiers);
        Assert.Equal(copies, driver.State.PlayerZones["P1"].Graveyard.Count(id => id is "SPELL" or "SECOND"));
        var prompt = Assert.Single(ResolutionResult.BuildPrompts(driver.State)["P1"].Candidates!, c => c.Action == CommandTypes.AssignCombatDamage);
        Assert.Equal(expected, Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(prompt.Metadata!["damagePool"])["ALLY"]);

        async Task Grants()
        {
            foreach (var id in copies == 1 ? new[] { "SPELL" } : ["SPELL", "SECOND"]) {
                if (duringDuel && driver.State.FocusPlayerId is { } focus && focus != "P1") await driver.Act(focus, new PassFocusCommand());
                var command = new PlayCardCommand(id, No, ["ALLY"], OptionalCosts: echo ? ["ECHO"] : []);
                var quote = new CoreRuleEngine().PreviewPlayCard(driver.State, "P1", PlayCostPreviewTests.Request(driver.State, command));
                Assert.True(quote.CanPay, quote.Message); Assert.Equal(echo ? 2 : 1, quote.Cost!.Mana);
                var mana = driver.State.RunePools["P1"].Mana; var power = driver.State.RunePools["P1"].TotalPower;
                await driver.Act("P1", command); Assert.Single(driver.State.StackItems);
                Assert.Equal(mana - (echo ? 2 : 1), driver.State.RunePools["P1"].Mana); Assert.Equal(power, driver.State.RunePools["P1"].TotalPower);
                await driver.Top();
            }
        }
    }

    internal static MatchState Position(int copies = 1)
    {
        var initial = VenFlowCombatKeywordTests.Position(false); var cards = initial.CardObjects.ToDictionary(); var locations = initial.ObjectLocations.ToDictionary();
        cards["SPELL"] = OfficialGarenConquestBattlefieldCountTests.Printed("SPELL", No, "P1");
        cards["ALLY"] = OfficialGarenConquestBattlefieldCountTests.Printed("ALLY", "OGN·210/298", "P1");
        cards["ENEMY2"] = OfficialGarenConquestBattlefieldCountTests.Printed("ENEMY2", "OGN·219/298", "P2");
        locations["ALLY"] = new("P1", "HAND"); locations["ENEMY2"] = new("P2", "BATTLEFIELD", "OTHER");
        initial = initial with { RoomId = "BLOOD-RUSH-TEMPORARY-ASSAULT", CardObjects = cards, ObjectLocations = locations,
            PlayerZones = initial.PlayerZones.ToDictionary(p => p.Key, p => p.Key == "P1"
                ? p.Value with { Base = p.Value.Base.Where(id => id != "ALLY").ToArray(), Hand = [..p.Value.Hand, "ALLY"] }
                : p.Value with { Battlefields = [..p.Value.Battlefields, "ENEMY2"] }) };
        initial = OptionalEntryTargetConfirmationTests.AddSpell(initial, "READY", "UNL-009/219");
        return copies == 2 ? OptionalEntryTargetConfirmationTests.AddSpell(initial, "SECOND", No) : initial;
    }

    internal static int Might(MatchState state) => CurrentPowerTargetTests.Snapshot(state)
        .GetProperty("players").GetProperty("P1").GetProperty("objects").GetProperty("ALLY").GetProperty("effectivePower").GetInt32();
    internal static void Export(string name, MatchState initial, Driver driver, int? expected = null, int? actual = null)
    {
        if (Environment.GetEnvironmentVariable("RIFTBOUND_BLOOD_RUSH_EVIDENCE") is not { Length: > 0 } root) return;
        Directory.CreateDirectory(root); File.WriteAllText(Path.Combine(root, name + ".json"), JsonSerializer.Serialize(new {
            initial, final = driver.State, commands = driver.Commands(), events = driver.Events(), expected, actual
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));
    }
}
