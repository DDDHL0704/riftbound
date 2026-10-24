using System.Reflection;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class BattleWinnerPhaseScenarioTests
{
    [Theory]
    [InlineData("plain")] [InlineData("guardians")] [InlineData("final-conquest")]
    [InlineData("result-burnout")] [InlineData("defender")] [InlineData("skipped-damage")] [InlineData("control-observer")]
    public void FiniteWinnerQaHasNoPreplayedCombatantOrEarnedBattleResult(string mode)
    {
        var state = Build(mode); Assert.Equal(424242, state.Seed); Assert.Equal(5, state.TurnNumber);
        Assert.Equal(RuleInputIdentity.Current, state.RuleInputs); Assert.Null(state.PendingBattleResult); Assert.False(state.BattleState.IsActive);
        foreach (var id in new[] { "HOST", "ATTACKER", "READY", "ANGEL", "ANGEL-2" }) {
            var owner = id is "ATTACKER" or "READY" ? "P2" : "P1"; var card = state.CardObjects[id];
            Assert.Contains(id, state.PlayerZones[owner].Hand); Assert.Equal(new ObjectLocationState(owner, "HAND"), state.ObjectLocations[id]);
            Assert.Equal(0, card.ObjectGeneration); Assert.Equal(0, card.Damage); Assert.Null(card.AttachedToObjectId);
            Assert.False(card.IsAttacking); Assert.False(card.IsDefending); Assert.False(card.IsEmpowered);
        }
        Assert.Equal(mode == "final-conquest" ? "P2" : null, state.CardObjects["OTHER"].ControllerId);
        if (mode == "final-conquest") {
            Assert.Contains("OTHER-KEEPER", state.PlayerZones["P2"].Battlefields);
            Assert.Equal(new ObjectLocationState("P2", "BATTLEFIELD", "OTHER"), state.ObjectLocations["OTHER-KEEPER"]);
            Assert.Equal(6, state.PlayerScores["P2"]); Assert.Empty(state.UntilEndOfTurnEffects);
        }
        Assert.Equal(mode == "result-burnout" ? 1 : 7, state.PlayerZones["P2"].MainDeck.Count);
        Assert.Equal(mode == "result-burnout" ? 7 : 0, state.PlayerScores["P1"]);
        Assert.Empty(OfficialInsightAndSpellLockTests.Errors(state));
    }

    [Fact]
    public void FiniteWinnerQaRejectsUnlistedStateImports()
    {
        var error = Assert.Throws<InvalidOperationException>(() => Build("arbitrary-import"));
        Assert.Contains("Unknown finite", error.Message);
    }

    // Test-only finite midgame fixture. Every combatant and response is earned
    // by the normal command driver; no runtime state-import entry point is added.
    internal static MatchState Build(string mode)
    {
        var scenario = "battle-winner-phase-" + mode;
        if (scenario is not ("battle-winner-phase-plain" or "battle-winner-phase-guardians"
            or "battle-winner-phase-final-conquest" or "battle-winner-phase-result-burnout"
            or "battle-winner-phase-defender" or "battle-winner-phase-skipped-damage" or "battle-winner-phase-control-observer"))
            throw new InvalidOperationException("Unknown finite battle-winner-phase QA position.");
        var final = scenario == "battle-winner-phase-final-conquest";
        var burnout = scenario == "battle-winner-phase-result-burnout";
        var defender = scenario == "battle-winner-phase-defender";
        var skipped = scenario == "battle-winner-phase-skipped-damage";
        var control = scenario == "battle-winner-phase-control-observer";
        var cards = new Dictionary<string, CardObjectState>(StringComparer.Ordinal);
        var locations = new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal);
        void Put(string id, string no, string owner, string zone, string? field = null) {
            cards[id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, no, owner);
            locations[id] = new(owner, zone, field);
        }
        Put("HOME", control ? "OGN·298/298" : "OGN·278/298", "P1", "BATTLEFIELD", "HOME"); cards["HOME"] = cards["HOME"] with { ControllerId = "P1" };
        Put("OTHER", "OGN·295/298", final ? "P2" : "P1", "BATTLEFIELD", "OTHER");
        cards["OTHER"] = cards["OTHER"] with { ControllerId = final ? "P2" : null };
        if (final) {
            Put("OTHER-KEEPER", "OGN·219/298", "P2", "BATTLEFIELD", "OTHER");
            cards["OTHER-KEEPER"] = cards["OTHER-KEEPER"] with { ControllerId = "P2" };
        }
        foreach (var id in new[] { "ANGEL", "ANGEL-2" }) Put(id, "SFD·051/221", "P1", "HAND");
        Put("HOST", defender ? "OGN·142/298" : "OGN·210/298", "P1", "HAND");
        Put("ATTACKER", defender ? "OGN·210/298" : "OGN·142/298", "P2", "HAND"); Put("READY", "OGN·146/298", "P2", "HAND");
        if (skipped) { Put("RAY", "OGN·009/298", "P2", "HAND"); Put("P2-RED", "UNL-R01", "P2", "BASE"); }
        if (burnout || control) { Put("LEGEND", "SFD·185/221", "P2", "LEGEND"); cards["LEGEND"] = cards["LEGEND"] with { ControllerId = "P2" }; }
        var funding = Enumerable.Range(1, 12).Select(i => "P2-FUND-" + i).ToArray();
        foreach (var id in funding) Put(id, "UNL-R06", "P2", "BASE");
        PlayerZones Zones(IReadOnlyList<string> mainDeck, IReadOnlyList<string> runeDeck,
            IReadOnlyList<string> hand, IReadOnlyList<string> baseZone, IReadOnlyList<string> legendZone,
            IReadOnlyList<string> battlefields) => PlayerZones.Empty with {
                MainDeck = mainDeck, RuneDeck = runeDeck, Hand = hand, Base = baseZone,
                LegendZone = legendZone, Battlefields = battlefields };
        var zones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal);
        foreach (var player in new[] { "P1", "P2" }) {
            var key = player == "P1" ? "P1" : "P2";
            var deck = player == "P2" && burnout ? new[] { "P2-DRAW-1" }
                : Enumerable.Range(1, 6).Select(i => key + "-DRAW-" + i).Append("PRIVATE-" + key).ToArray();
            foreach (var id in deck) Put(id, "OGN·219/298", player, "MAIN_DECK");
            var runes = new[] { key + "-RUNE-1", "PRIVATE-" + key + "-RUNE-TAIL" };
            foreach (var id in runes) Put(id, "UNL-R03", player, "RUNE_DECK");
            zones[player] = Zones(mainDeck: deck, runeDeck: runes,
                hand: player == "P1" ? ["HOST", "ANGEL", "ANGEL-2"] : skipped ? ["ATTACKER", "READY", "RAY"] : ["ATTACKER", "READY"],
                baseZone: player == "P1" ? [] : skipped ? [..funding, "P2-RED"] : funding, legendZone: player == "P2" && (burnout || control) ? ["LEGEND"] : [],
                battlefields: player == "P1" ? final ? ["HOME"] : ["HOME", "OTHER"] : final ? ["OTHER", "OTHER-KEEPER"] : []);
        }
        var pool = new RunePool(40, 0, new Dictionary<string, int> {
            [RuneTrait.Green] = 10, [RuneTrait.Purple] = 10 });
        return new MatchState("C10-WINNER-" + mode, 0, 5, "P1",
            new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" },
            status: MatchStatuses.InProgress, readyPlayerIds: ["P1", "P2"], turnPlayerId: "P1",
            phase: MatchPhases.Main, timingState: TimingStates.NeutralOpen,
            runePools: new Dictionary<string, RunePool> { ["P1"] = pool, ["P2"] = RunePool.Empty },
            playerZones: zones, cardObjects: cards, objectLocations: locations,
            playerScores: new Dictionary<string, int> { ["P1"] = burnout ? 7 : 0, ["P2"] = final ? 6 : 0 },
            seed: 424242) { CardPlays = new(5, [], []) };
    }
}
