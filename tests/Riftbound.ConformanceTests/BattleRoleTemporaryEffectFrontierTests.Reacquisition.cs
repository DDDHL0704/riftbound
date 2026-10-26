using System.Security.Cryptography;
using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed partial class BattleRoleTemporaryEffectFrontierTests
{
    [Fact]
    public async Task ActualSameIncarnationLeavingAndRejoiningDoesNotEarnItsDefendSkillTwice()
    {
        var initial = BattleLimitedTestSupport.Position("plain");
        var cards = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["HOST"] = OfficialGarenConquestBattlefieldCountTests.Printed("HOST", "SFD·128/221", "P1"),
            ["ATTACKER"] = OfficialGarenConquestBattlefieldCountTests.Printed("ATTACKER", "OGN·210/298", "P2") };
        var locations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations);
        foreach (var (id, no) in Enumerable.Range(1, 4).Select(i => ("P1-FUND-" + i, "UNL-R06"))
            .Concat(new[] { ("P1-PURPLE-1", "UNL-R05"), ("P1-PURPLE-2", "UNL-R05") })) {
            cards[id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, no, "P1"); locations[id] = new("P1", "BASE");
        }
        foreach (var id in new[] { "WIND-1", "WIND-2" }) {
            cards[id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, "OGN·173/298", "P1"); locations[id] = new("P1", "HAND");
        }
        initial = initial with { CardObjects = cards, ObjectLocations = locations,
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
                ["P1"] = initial.PlayerZones["P1"] with { Hand = [..initial.PlayerZones["P1"].Hand, "WIND-1", "WIND-2"],
                    Base = [..initial.PlayerZones["P1"].Base, "P1-FUND-1", "P1-FUND-2", "P1-FUND-3", "P1-FUND-4", "P1-PURPLE-1", "P1-PURPLE-2"] } } };
        var run = new Driver(initial);
        try {
            await run.Act("P1", new PlayCardCommand("HOST", "SFD·128/221", [], Destination: "BATTLEFIELD:HOME"));
            await run.Act("P1", new EndTurnCommand());
            var funding = Enumerable.Range(1, 12).Select(i => "P2-FUND-" + i).ToArray();
            await run.Act("P2", new TapRuneCommand(funding[0], funding));
            await run.Act("P2", new PlayCardCommand("ATTACKER", "OGN·210/298", [], Destination: "BASE"));
            await run.Act("P2", new PlayCardCommand("READY", "OGN·146/298", ["ATTACKER"])); await run.Top();
            await run.Act("P2", new MoveUnitCommand("ATTACKER", "BASE", "BATTLEFIELD:HOME", []));
            var generation = run.State.CardObjects["HOST"].ObjectGeneration;
            var choice = Assert.IsType<PendingCardChoiceState>(run.State.PendingCardChoice);
            Assert.Equal("HOST", choice.SourceObjectId); Assert.Equal(0, choice.RequiredCount);
            await run.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, []));
            foreach (var (id, destination) in new[] { ("WIND-1", "BASE:P1"), ("WIND-2", "BATTLEFIELD:HOME") }) {
                if (run.State.FocusPlayerId != "P1") await run.Act(run.State.FocusPlayerId!, new PassFocusCommand());
                Assert.Equal("P1", run.State.FocusPlayerId);
                if (id == "WIND-1") {
                    var ownFunding = Enumerable.Range(1, 4).Select(i => "P1-FUND-" + i).ToArray();
                    await run.Act("P1", new TapRuneCommand(ownFunding[0], ownFunding));
                    await run.Act("P1", new RecycleRuneCommand("P1-PURPLE-1", ["P1-PURPLE-1", "P1-PURPLE-2"]));
                }
                await run.Act("P1", new PlayCardCommand(id, "OGN·173/298", ["HOST"], Destination: destination)); await run.Top();
                Assert.True(run.State.SpellDuelState.IsActive); Assert.Equal(generation, run.State.CardObjects["HOST"].ObjectGeneration);
                Assert.Contains("HOST", destination == "BASE:P1" ? run.State.PlayerZones["P1"].Base : run.State.PlayerZones["P1"].Battlefields);
            }
            Assert.True(run.State.CardObjects["HOST"].IsDefending);
            Assert.Equal(1, run.Events().Count(e => e.Event.Kind == "TRIGGER_QUEUED"
                && e.Event.Payload.GetValueOrDefault("sourceObjectId") as string == "HOST"));
            Assert.Null(run.State.PendingCardChoice); await run.Replay();
        }
        finally {
            if (Environment.GetEnvironmentVariable("RIFTBOUND_C11_BATTLE_END_EVIDENCE") is { Length: > 0 } output) {
                Directory.CreateDirectory(output); var a = typeof(CoreRuleEngine).Assembly;
                File.WriteAllText(Path.Combine(output, "same-generation-defend-reacquisition.json"), JsonSerializer.Serialize(new {
                    initial, final = run.State, commands = run.Commands(), events = run.Events(),
                    runtime = new { path = a.Location, mvid = a.ManifestModule.ModuleVersionId,
                        sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(a.Location))).ToLowerInvariant(), ruleInputs = RuleInputIdentity.Current },
                    scope = "Actual same-generation field/base/field movement during one ongoing duel; decline first optional defend skill, then require no second admission. Explicit finite QA, no production changes or SQL/API claim."
                }, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));
            }
        }
    }
}
