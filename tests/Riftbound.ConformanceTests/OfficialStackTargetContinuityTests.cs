using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

// China core 124 and 359.3.e.2-8: current eligibility and physical-card continuity
// are distinct. An invalid target must not cancel independent instructions.
public sealed class OfficialStackTargetContinuityTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task VoidSeekerRechecksBattlefieldScopeButStillDraws(bool returnsToBattlefield)
    {
        var engine = new CoreRuleEngine();
        var current = await Play(engine, Position("OGN·024/298", "OGS·011/024"), "P1", "SPELL", "OGN·024/298", ["TARGET"]);
        current = await Pass(engine, current);
        current = await Play(engine, current.State, "P2", "RESPONSE", "OGS·011/024", ["TARGET"]);
        current = await ResolveTop(engine, current);
        Assert.Contains("TARGET", current.State.PlayerZones["P2"].Base);
        // Isolate the return leg of 359.3.e.3 from a particular movement effect.
        if (returnsToBattlefield) current = AtBattlefield(current, "TARGET");
        current = await ResolveTop(engine, current);
        Assert.Equal(returnsToBattlefield ? 4 : 0, current.State.CardObjects["TARGET"].Damage);
        Assert.Contains("DRAW", current.State.PlayerZones["P1"].Hand);
        Assert.Contains("SPELL", current.State.PlayerZones["P1"].Graveyard);
    }

    [Fact]
    public async Task GustDoesNotReturnTargetThatGrewAboveItsLimit()
    {
        var engine = new CoreRuleEngine();
        var current = await Play(engine, Position("OGN·169/298", "OGN·207/298", power: 3), "P1", "SPELL", "OGN·169/298", ["TARGET"]);
        current = await Pass(engine, current);
        current = await Play(engine, current.State, "P2", "RESPONSE", "OGN·207/298", ["TARGET"]);
        current = await ResolveTop(engine, current);
        Assert.Equal(6, current.State.CardObjects["TARGET"].Power);
        current = await ResolveTop(engine, current);
        Assert.Contains("TARGET", current.State.PlayerZones["P2"].Battlefields);
        Assert.DoesNotContain("TARGET", current.State.PlayerZones["P2"].Hand);
    }

    [Fact]
    public async Task PartlyInvalidMultiTargetSpellStillAffectsItsOtherTarget()
    {
        var engine = new CoreRuleEngine();
        var current = await Play(engine, Position("OGN·105/298", "OGN·104/298"), "P1", "SPELL", "OGN·105/298", ["TARGET", "OTHER"]);
        current = await Pass(engine, current);
        current = await Play(engine, current.State, "P2", "RESPONSE", "OGN·104/298", ["TARGET"]);
        current = await ResolveTop(engine, current);
        current = await ResolveTop(engine, current);
        Assert.Contains("TARGET", current.State.PlayerZones["P2"].Hand);
        Assert.Equal(0, current.State.CardObjects["TARGET"].Damage);
        Assert.Equal(6, current.State.CardObjects["OTHER"].Damage);
    }

    [Fact]
    public async Task OldSpellCannotFollowReturnedCardBackIntoPlayEvenAfterSaveRestore()
    {
        var engine = new CoreRuleEngine();
        var current = await Play(engine, Position("OGN·024/298", "OGN·104/298"), "P1", "SPELL", "OGN·024/298", ["TARGET"]);
        current = await Pass(engine, current);
        current = await Play(engine, current.State, "P2", "RESPONSE", "OGN·104/298", ["TARGET"]);
        current = await ResolveTop(engine, current);
        Assert.Contains("TARGET", current.State.PlayerZones["P2"].Hand);
        // Reentry is a fixture transition: the actual return effect above must
        // already break the old binding regardless of how this card reenters.
        current = AtBattlefield(current, "TARGET");
        var restored = JsonSerializer.Deserialize<MatchState>(JsonSerializer.Serialize(current.State))!;
        Assert.Equal(MatchStateHasher.Hash(current.State), MatchStateHasher.Hash(restored));
        current = await ResolveTop(engine, current with { State = restored });
        Assert.Contains("TARGET", current.State.PlayerZones["P2"].Battlefields);
        Assert.Equal(0, current.State.CardObjects["TARGET"].Damage);
        Assert.Contains("DRAW", current.State.PlayerZones["P1"].Hand);
        Assert.Equal(MatchStateHasher.HashValue(ResolutionResult.BuildSnapshots(current.State)), MatchStateHasher.HashValue(current.Snapshots));
        var rejected = await engine.ResolveAsync(current.State, new("check-projection", "P1", CommandTypes.PlayCard),
            new PlayCardCommand("MISSING", "SFD·125/221", []), default);
        Assert.False(rejected.Accepted);
        Assert.Equal(MatchStateHasher.HashValue(rejected.Prompts), MatchStateHasher.HashValue(current.Prompts));
    }

    private static ResolutionResult AtBattlefield(ResolutionResult current, string target)
    {
        var zones = current.State.PlayerZones.ToDictionary(e => e.Key, e => e.Value);
        zones["P2"] = zones["P2"] with
        {
            Base = zones["P2"].Base.Where(id => id != target).ToArray(),
            Hand = zones["P2"].Hand.Where(id => id != target).ToArray(),
            Battlefields = zones["P2"].Battlefields.Append(target).ToArray()
        };
        var locations = current.State.ObjectLocations.ToDictionary(e => e.Key, e => e.Value);
        locations[target] = new("P2", "BATTLEFIELD");
        return current with { State = current.State with { PlayerZones = zones, ObjectLocations = locations } };
    }

    private static MatchState Position(string spell, string response, int power = 10)
        => new("TARGET-CONTINUITY", 1, 3, "P1", new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" },
            status: MatchStatuses.InProgress, phase: MatchPhases.Main, timingState: TimingStates.NeutralOpen,
            runePools: new Dictionary<string, RunePool> { ["P1"] = new(30, 30), ["P2"] = new(30, 30) },
            playerZones: new Dictionary<string, PlayerZones>
            {
                ["P1"] = PlayerZones.Empty with { Hand = ["SPELL"], MainDeck = ["DRAW"] },
                ["P2"] = PlayerZones.Empty with { Hand = ["RESPONSE"], Battlefields = ["TARGET", "OTHER"] }
            }, cardObjects: new Dictionary<string, CardObjectState>
            {
                ["SPELL"] = new("SPELL", cardNo: spell, ownerId: "P1", controllerId: "P1", tags: [CardObjectTags.SpellCard]),
                ["RESPONSE"] = new("RESPONSE", cardNo: response, ownerId: "P2", controllerId: "P2", tags: [CardObjectTags.SpellCard]),
                ["TARGET"] = new("TARGET", cardNo: "SFD·125/221", power: power, ownerId: "P2", controllerId: "P2", tags: [CardObjectTags.UnitCard]),
                ["OTHER"] = new("OTHER", cardNo: "SFD·125/221", power: 10, ownerId: "P2", controllerId: "P2", tags: [CardObjectTags.UnitCard]),
                ["DRAW"] = new("DRAW", cardNo: "SFD·125/221", power: 4, ownerId: "P1", controllerId: "P1", tags: [CardObjectTags.UnitCard])
            });

    private static async Task<ResolutionResult> Play(CoreRuleEngine engine, MatchState state, string player, string source, string cardNo, string[] targets)
    {
        var result = await engine.ResolveAsync(state, new($"play-{state.Tick}-{source}", player, CommandTypes.PlayCard),
            new PlayCardCommand(source, cardNo, targets), default);
        Assert.True(result.Accepted, result.ErrorMessage);
        return result;
    }

    private static async Task<ResolutionResult> Pass(CoreRuleEngine engine, ResolutionResult current)
    {
        var result = await engine.ResolveAsync(current.State,
            new($"pass-{current.State.Tick}", current.State.PriorityPlayerId!, CommandTypes.PassPriority), new PassPriorityCommand(), default);
        Assert.True(result.Accepted, result.ErrorMessage);
        return result;
    }

    private static async Task<ResolutionResult> ResolveTop(CoreRuleEngine engine, ResolutionResult current)
    {
        var count = current.State.StackItems.Count;
        for (var step = 0; current.State.StackItems.Count == count && step < 4; step++) current = await Pass(engine, current);
        Assert.Equal(count - 1, current.State.StackItems.Count);
        return current;
    }
}
