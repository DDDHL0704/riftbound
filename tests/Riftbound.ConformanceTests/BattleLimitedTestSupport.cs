using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

internal static class BattleLimitedTestSupport
{
    internal static void Runtime(Xunit.Abstractions.ITestOutputHelper output)
    {
        var engine = typeof(CoreRuleEngine).Assembly;
        output.WriteLine($"Engine={engine.Location}; SHA256={Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(engine.Location))).ToLowerInvariant()}; MVID={engine.ManifestModule.ModuleVersionId}; RIF={RuleInputIdentity.Current.IdentitySha256}");
    }

    // Explicit midgame fixture: combatants and equipment have not been played.
    // The command driver earns every role, grant, damage mark and battle result.
    internal static MatchState Position(string mode)
    {
        Assert.Contains(mode, new[] { "plain", "final-conquest", "result-burnout" });
        var final = mode == "final-conquest";
        var burnout = mode == "result-burnout";
        var cards = new Dictionary<string, CardObjectState>(StringComparer.Ordinal);
        var locations = new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal);
        void Put(string id, string no, string owner, string zone, string? field = null)
        {
            cards[id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, no, owner);
            locations[id] = new(owner, zone, field);
        }
        Put("HOME", "OGN·278/298", "P1", "BATTLEFIELD", "HOME");
        cards["HOME"] = cards["HOME"] with { ControllerId = "P1" };
        Put("OTHER", "OGN·295/298", final ? "P2" : "P1", "BATTLEFIELD", "OTHER");
        cards["OTHER"] = cards["OTHER"] with { ControllerId = final ? "P2" : null };
        if (final) Put("OTHER-KEEPER", "OGN·219/298", "P2", "BATTLEFIELD", "OTHER");
        foreach (var id in new[] { "ANGEL", "ANGEL-2" }) Put(id, "SFD·051/221", "P1", "HAND");
        Put("HOST", "OGN·210/298", "P1", "HAND");
        Put("ATTACKER", "OGN·142/298", "P2", "HAND");
        Put("READY", "OGN·146/298", "P2", "HAND");
        if (burnout) Put("LEGEND", "SFD·185/221", "P2", "LEGEND");
        var funding = Enumerable.Range(1, 12).Select(i => "P2-FUND-" + i).ToArray();
        foreach (var id in funding) Put(id, "UNL-R06", "P2", "BASE");
        var zones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal);
        foreach (var player in new[] { "P1", "P2" })
        {
            var deck = player == "P2" && burnout ? ["P2-DRAW-1"]
                : Enumerable.Range(1, 6).Select(i => player + "-DRAW-" + i).Append("PRIVATE-" + player).ToArray();
            foreach (var id in deck) Put(id, "OGN·219/298", player, "MAIN_DECK");
            var runes = new[] { player + "-RUNE-1", "PRIVATE-" + player + "-RUNE-TAIL" };
            foreach (var id in runes) Put(id, "UNL-R03", player, "RUNE_DECK");
            zones[player] = PlayerZones.Empty with
            {
                MainDeck = deck, RuneDeck = runes,
                Hand = player == "P1" ? ["HOST", "ANGEL", "ANGEL-2"] : ["ATTACKER", "READY"],
                Base = player == "P1" ? [] : funding,
                LegendZone = player == "P2" && burnout ? ["LEGEND"] : [],
                Battlefields = player == "P1" ? final ? ["HOME"] : ["HOME", "OTHER"]
                    : final ? ["OTHER", "OTHER-KEEPER"] : []
            };
        }
        return new("C11-BATTLE-DURATION-" + mode, 0, 5, "P1",
            new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" },
            status: MatchStatuses.InProgress, readyPlayerIds: ["P1", "P2"], turnPlayerId: "P1",
            phase: MatchPhases.Main, timingState: TimingStates.NeutralOpen,
            runePools: new Dictionary<string, RunePool>
            {
                ["P1"] = new(40, 0, new Dictionary<string, int> { [RuneTrait.Green] = 10, [RuneTrait.Purple] = 10 }),
                ["P2"] = RunePool.Empty
            },
            playerZones: zones, cardObjects: cards, objectLocations: locations,
            playerScores: new Dictionary<string, int> { ["P1"] = burnout ? 7 : 0, ["P2"] = final ? 6 : 0 },
            seed: 424242) { CardPlays = new(5, [], []) };
    }

    internal static async Task PassFocus(Driver run)
    {
        for (var i = 0; run.State.FocusPlayerId is not null; i++)
        {
            Assert.InRange(i, 0, 5);
            await run.Act(run.State.FocusPlayerId, new PassFocusCommand());
        }
    }

    internal static Task Declare(Driver run)
        => run.Act("P2", new DeclareBattleCommand("HOME", ["ATTACKER"], ["HOST"], ["COMBAT_ASSIGNMENT"]));
}
