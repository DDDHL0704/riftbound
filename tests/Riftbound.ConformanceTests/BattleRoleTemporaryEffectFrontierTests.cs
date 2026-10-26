using System.Security.Cryptography;
using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

// Canonical cards played from hand into a genuine one-on-one battle duel.
// CN 383.4.e-f / 466.2.c admit these skills when roles are first acquired.
public sealed partial class BattleRoleTemporaryEffectFrontierTests
{
    [Theory]
    [InlineData("OGN·279/298", "OGN·210/298", "HOME")]
    [InlineData("OGN·278/298", "SFD·110/221", "ATTACKER")]
    [InlineData("OGN·278/298", "SFD·110a/221", "ATTACKER")]
    [InlineData("OGN·278/298", "OGN·210/298", "")]
    public async Task ActualFirstRoleAcquisitionQueuesThePrintedBattleLimitedSkillBeforeCombat(
        string fieldNo, string attackerNo, string expectedSource)
    {
        var initial = BattleLimitedTestSupport.Position("plain");
        var cards = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["HOME"] = OfficialGarenConquestBattlefieldCountTests.Printed("HOME", fieldNo, "P1") with { ControllerId = "P1" },
            ["ATTACKER"] = OfficialGarenConquestBattlefieldCountTests.Printed("ATTACKER", attackerNo, "P2") };
        if (attackerNo.StartsWith("SFD·110", StringComparison.Ordinal)) {
            cards["P2-ORANGE"] = OfficialGarenConquestBattlefieldCountTests.Printed("P2-ORANGE", "UNL-R04", "P2");
            initial = initial with {
                PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
                    ["P2"] = initial.PlayerZones["P2"] with { Base = [..initial.PlayerZones["P2"].Base, "P2-ORANGE"] } },
                ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) { ["P2-ORANGE"] = new("P2", "BASE") } };
        }
        initial = initial with { CardObjects = cards };
        var run = new Driver(initial);
        try {
            await run.Act("P1", new PlayCardCommand("HOST", "OGN·210/298", [], Destination: "BATTLEFIELD:HOME"));
            await run.Act("P1", new EndTurnCommand());
            var funding = Enumerable.Range(1, 12).Select(i => "P2-FUND-" + i).ToArray();
            await run.Act("P2", new TapRuneCommand(funding[0], funding));
            if (cards.ContainsKey("P2-ORANGE")) await run.Act("P2", new RecycleRuneCommand("P2-ORANGE"));
            await run.Act("P2", new PlayCardCommand("ATTACKER", attackerNo, [], Destination: "BASE"));
            await run.Act("P2", new PlayCardCommand("READY", "OGN·146/298", ["ATTACKER"])); await run.Top();
            await run.Act("P2", new MoveUnitCommand("ATTACKER", "BASE", "BATTLEFIELD:HOME", []));
            Assert.True(run.State.CardObjects["ATTACKER"].IsAttacking); Assert.True(run.State.CardObjects["HOST"].IsDefending);
            Assert.Equal("P1", run.State.CardObjects["HOME"].ControllerId);
            Assert.DoesNotContain(run.Commands(), c => c.CommandType is CommandTypes.DeclareBattle or CommandTypes.AssignCombatDamage);
            var sources = run.State.TriggerQueue.Select(t => t.SourceObjectId).Concat(run.State.StackItems.Select(s => s.SourceObjectId)).ToArray();
            if (expectedSource.Length == 0) Assert.Empty(sources);
            else Assert.Contains(expectedSource, sources);
            await run.Replay();
        }
        finally {
            if (Environment.GetEnvironmentVariable("RIFTBOUND_C11_BATTLE_END_EVIDENCE") is { Length: > 0 } output) {
                Directory.CreateDirectory(output); var assembly = typeof(CoreRuleEngine).Assembly;
                File.WriteAllText(Path.Combine(output, fieldNo.Replace('/', '_') + "-" + attackerNo.Replace('/', '_') + ".json"),
                    JsonSerializer.Serialize(new { initial, final = run.State, commands = run.Commands(), events = run.Events(), expectedSource,
                        runtime = new { path = assembly.Location, mvid = assembly.ManifestModule.ModuleVersionId,
                            sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(assembly.Location))).ToLowerInvariant(), ruleInputs = RuleInputIdentity.Current },
                        scope = "Actual canonical first-role trigger admission frontier only. No implementation fix, full-card, SQL, API, battle-end duration or later response semantics claim." },
                        new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));
            }
        }
    }
}
