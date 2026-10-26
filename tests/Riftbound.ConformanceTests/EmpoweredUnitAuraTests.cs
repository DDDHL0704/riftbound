using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class EmpoweredUnitAuraTests
{
    private const string No = "VEN·130";
    private const string Aura = "EMPOWERED_FRIENDLY_UNIT_POWER";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    [Fact]
    public void ParsingTheAuraDoesNotTurnItIntoASelfOnlyBonusOrDependOnACardNumber()
    {
        var text = "{{已强化>}} 你的{{已强化}}单位获得{{S}}+4（包括我在内）。";
        Assert.Equal(4, Assert.Single(EmpowerParser.FriendlyEmpoweredUnitAuras(text)).Might);
        Assert.Equal(0, EmpowerParser.Self(text).Might);
        Assert.Empty(EmpowerParser.FriendlyEmpoweredUnitAuras("你的单位获得{{S}}+4。"));
        Assert.Empty(EmpowerParser.FriendlyEmpoweredUnitAuras("{{已强化>}} 我获得{{S}}+4。"));
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public async Task EachActuallyAttachedTextContributesWhileItsEmpoweredHostRemainsActive(int copies)
    {
        await Run("instances-" + copies, copies, async game => {
            await Enter(game, copies); Assert.Equal(4, Might(game.State, "P1", "ALLY"));
            await Activate(game); Assert.Equal(4 + 2 * (copies + 1), Might(game.State, "P1", "ALLY"));
            Assert.Equal(5 + copies + 2 * (copies + 1), Might(game.State, "P1", "UNIT"));
            Assert.Equal(4, Might(game.State, "P2", "ENEMY")); Assert.Equal(5, game.State.CardObjects["UNIT"].Power);
            var effects = game.State.ContinuousEffects.Where(e => e.EffectKind == Aura && e.TargetObjectId == "ALLY").ToArray();
            Assert.Equal(copies + 1, effects.Length); Assert.Equal(copies + 1, effects.Select(e => e.EffectId).Distinct().Count());
            Assert.Single(effects, e => e.SourceDependencyObjectIds!.Count == 1);
            Assert.Equal(copies, effects.Count(e => e.SourceDependencyObjectIds!.Count == 2));
            if (copies > 0) {
                await game.Act("P1", new PlayCardCommand("DETACH", "SFD·011/221", ["UNIT", "GEAR"])); await game.Top();
                Assert.Null(game.State.CardObjects["GEAR"].AttachedToObjectId);
                Assert.Equal(4 + 2 * copies, Might(game.State, "P1", "ALLY"));
            }
            await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["UNIT"])); await game.Top();
            Assert.Contains("UNIT", game.State.PlayerZones["P1"].Hand); Assert.False(game.State.CardObjects["UNIT"].IsEmpowered);
            Assert.Equal(4, Might(game.State, "P1", "ALLY")); Assert.DoesNotContain(game.State.ContinuousEffects, e => e.EffectKind == Aura);
        });
    }

    [Fact]
    public async Task ExplicitControlContinuationRecomputesFriendlinessWithoutRewritingTheAcceptedHistory()
    {
        await Run("current-controller-component", 0, async game => {
            await Enter(game, 0); await Activate(game); var original = MatchStateHasher.Hash(game.State);
            // This is an explicit component continuation, not an edited journal or a claim of a played control spell.
            var controlled = LocalDestructionRecallTests.At(game.State, "UNIT", "P2", "BASE");
            controlled = OfficialInsightAndSpellLockTests.Restore(controlled); ArmamentMightTests.PublicRecovery(controlled);
            Assert.Equal(4, Might(controlled, "P1", "ALLY")); Assert.Equal(6, Might(controlled, "P2", "ENEMY"));
            Assert.Equal(original, MatchStateHasher.Hash(game.State));
        });
    }

    public static IEnumerable<object[]> ProjectionMutations()
        => from viewer in new[] { "P1", "P2", "spectator" }
           from mutation in new[] { "delta", "before", "after", "source", "card", "condition", "lifecycle", "dependencies", "missing", "extra" }
           select new object[] { viewer, mutation };

    [Theory]
    [MemberData(nameof(ProjectionMutations))]
    public async Task EveryPublicViewBindsTheExactCurrentAuraSetAndContribution(string viewer, string mutation)
    {
        await Run("projection-" + viewer + "-" + mutation, 0, async game => {
            await Enter(game, 0); await Activate(game); var state = game.State;
            var snapshots = ResolutionResult.BuildSnapshots(state); var prompts = ResolutionResult.BuildPrompts(state);
            var views = state.Seats.Keys.ToDictionary(id => id, id => new RecoveredPlayerView(id, state.Tick, 0, snapshots[id], state.Tick, 0, prompts[id]));
            var spectator = MatchReplayRedactor.BuildSpectatorFrame(state.RoomId, state.Tick, 0, [], state);
            Assert.Empty(MatchRecoveryValidator.ValidateComponentStateAndViews(state, views, spectator));
            var snapshot = viewer == "spectator" ? spectator.SpectatorSnapshot : snapshots[viewer];
            var rows = Assert.IsAssignableFrom<IEnumerable<Dictionary<string, object?>>>(snapshot.Timing["continuousEffects"]).ToList();
            var row = Assert.Single(rows, r => Equals(r.GetValueOrDefault("effectKind"), Aura) && Equals(r.GetValueOrDefault("targetObjectId"), "ALLY"));
            if (mutation == "missing") rows.Remove(row);
            else if (mutation == "extra") rows.Add(new(row) { ["effectId"] = "FORGED-AURA" });
            else {
                var key = mutation switch { "delta" => "powerDelta", "before" => "basePower", "after" => "effectivePower",
                    "source" => "sourceObjectId", "card" => "sourceCardNo", "dependencies" => "sourceDependencyObjectIds", _ => mutation };
                row[key] = mutation is "delta" or "before" or "after" ? 999 : mutation == "dependencies" ? Array.Empty<string>() : "FORGED";
            }
            snapshot.Timing["continuousEffects"] = rows.ToArray();
            Assert.NotEmpty(MatchRecoveryValidator.ValidateComponentStateAndViews(state, views, spectator));
        });
    }

    private static MatchState Position(int copies)
    {
        var initial = VenUnitEntryBasicTests.Position(No);
        initial = Put(initial, "ALLY", "OGN·219/298", "P1", "BASE"); initial = Put(initial, "ENEMY", "OGN·219/298", "P2", "BASE");
        initial = Put(initial, "RETURN", "OGN·104/298", "P1", "HAND"); initial = Put(initial, "DETACH", "SFD·011/221", "P1", "HAND");
        if (copies > 0) initial = Put(initial, "GEAR", "SFD·059/221", "P1", "BASE");
        if (copies > 1) initial = Put(initial, "GEAR2", "SFD·059/221", "P1", "BASE");
        return initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) {
            ["P1"] = new(20, 0, new Dictionary<string, int> { [RuneTrait.Yellow] = 2, [RuneTrait.Green] = copies }) },
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["ALLY"] = initial.CardObjects["ALLY"] with { IsEmpowered = true }, ["ENEMY"] = initial.CardObjects["ENEMY"] with { IsEmpowered = true } } };
    }
    private static MatchState Put(MatchState state, string id, string no, string player, string zone)
    {
        var zones = state.PlayerZones.ToDictionary(p => p.Key, p => p.Value with {
            Base = p.Value.Base.Where(x => x != id).ToArray(), Hand = p.Value.Hand.Where(x => x != id).ToArray() });
        zones[player] = zone == "BASE" ? zones[player] with { Base = [..zones[player].Base, id] } : zones[player] with { Hand = [..zones[player].Hand, id] };
        return state with { PlayerZones = zones,
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) { [id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, no, player) },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { [id] = new(player, zone) } };
    }
    private static async Task Enter(Driver game, int copies)
    {
        await game.Act("P1", new PlayCardCommand("UNIT", No, []));
        for (var i = 0; i < copies; i++) { await game.Act("P1", new AssembleEquipmentCommand(i == 0 ? "GEAR" : "GEAR2", "UNIT", ["ASSEMBLE_GREEN"])); await game.Top(); }
    }
    private static async Task Activate(Driver game)
    { await game.Act("P1", new ActivateAbilityCommand("UNIT", "EMPOWER:0:0", [])); await game.Top(); }
    private static int Might(MatchState state, string player, string id) => JsonSerializer.SerializeToElement(ResolutionResult.BuildSnapshots(state)[player], Json)
        .GetProperty("players").GetProperty(player).GetProperty("objects").GetProperty(id).GetProperty("effectivePower").GetInt32();
    private static async Task Run(string label, int copies, Func<Driver, Task> body)
    {
        var initial = Position(copies); var game = new Driver(initial);
        try { await body(game); await game.Replay(); }
        finally {
            if (Environment.GetEnvironmentVariable("RIFTBOUND_VEN130_EVIDENCE") is { Length: > 0 } output) {
                Directory.CreateDirectory(output); await File.WriteAllTextAsync(Path.Combine(output, "shared-" + label + ".json"),
                    JsonSerializer.Serialize(new { scope = "bounded aura semantics and component views; explicit midgame; no whole-card or global backend certification",
                        initial, final = game.State, commands = game.Commands(), events = game.Events() }, Json));
            }
        }
    }
}
