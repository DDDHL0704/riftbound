using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.LocalDestructionRecallTests;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;

namespace Riftbound.ConformanceTests;

public sealed class DisarmTriggerTests
{
    // These controlled field positions use the retained exact CN source prints.
    // They do not claim a registered card, accepted room birth, or legal deck.
    internal static async Task<MatchState> Position(string printing, int amount)
    {
        const string source = "data/official/upstream/2026-10-28/radiance-disarm/card-catalog.zh-CN.json";
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, source))) root = root.Parent;
        Assert.NotNull(root);
        var catalog = await OfficialCardCatalogLoader.LoadFromFileAsync(Path.Combine(root.FullName, source));
        var card = Assert.Single(catalog.Cards, c => c.CardNo == printing);
        Assert.Contains(amount == 1 ? "{{缴械}}" : "{{缴械2}}", card.CardEffect);
        var state = AttackDamageDistributionTests.Position();
        state = At(At(state, "ENEMY2", "P2", "BASE"), "ENEMY3", "P2", "BASE");
        return state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["HOST"] = new("HOST", cardNo: printing, power: card.Power!.Value,
                manaCost: card.Energy!.Value, ownerId: "P1", controllerId: "P1",
                tags: [CardObjectTags.UnitCard, "COLOR:blue", card.Tag, amount == 1 ? "缴械" : "缴械2"]),
            ["ENEMY1"] = new("ENEMY1", cardNo: "OGN·175/298", power: 3, manaCost: 3,
                ownerId: "P2", controllerId: "P2", tags: [CardObjectTags.UnitCard, "COLOR:purple", "海盗"])
        } };
    }

    internal static void Record(string row, string stage, MatchState state, IReadOnlyList<GameEvent>? events = null,
        bool? accepted = null, string? error = null)
    {
        var directory = Environment.GetEnvironmentVariable("RIFTBOUND_DISARM_EVIDENCE");
        if (directory is null) return;
        directory = Path.Combine(directory, row); Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, stage + ".json"), JsonSerializer.Serialize(new {
            state, events, accepted, error,
            componentErrors = MatchRecoveryValidator.ValidateComponentStateAndViews(state),
            snapshots = ResolutionResult.BuildSnapshots(state), prompts = ResolutionResult.BuildPrompts(state)
        }));
    }

    internal static int Might(MatchState state, string id = "ENEMY1")
        => CurrentPowerTargetTests.Snapshot(state).GetProperty("players").GetProperty("P2")
            .GetProperty("objects").GetProperty(id).GetProperty("effectivePower").GetInt32();

    [Theory]
    [InlineData("RAD·054/167", 1)]
    [InlineData("RAD·050/167", 2)]
    public async Task FirstAttackChoosesOneEnemyHereAndReducesMightWithoutDamage(string printing, int amount)
    {
        var row = amount.ToString(); var initial = await Position(printing, amount);
        Record(row, "initial", initial); Restore(initial); ArmamentMightTests.PublicRecovery(initial);
        var move = new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F", []);
        var attack = await new CoreRuleEngine().ResolveAsync(initial, new("disarm-attack", "P1", move.CmdType), move, default);
        Record(row, "attack", attack.State, attack.Events, attack.Accepted, attack.ErrorMessage);
        Assert.True(attack.Accepted, attack.ErrorMessage);
        Assert.True(attack.State.CardObjects["HOST"].IsAttacking);
        var choice = Assert.IsType<PendingCardChoiceState>(attack.State.PendingCardChoice);
        Assert.Equal("TRIGGER_CONFIRMATION", choice.ChoiceWindow); Assert.Equal("P1", choice.PlayerId);
        Assert.Equal("HOST", choice.SourceObjectId); Assert.Equal(1, choice.RequiredCount); Assert.Equal(1, choice.MaxCount);
        Assert.Equal(new[] { "ENEMY1" }, choice.LegalObjectIds);
        Assert.Equal(1, attack.State.StackItems.Count(i => i.SourceObjectId == "HOST")
            + attack.State.TriggerQueue.Count(t => t.SourceObjectId == "HOST"));
        var confirmed = await Act(attack.State, "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ENEMY1"]));
        Record(row, "confirmed", confirmed.State, confirmed.Events);
        Assert.Equal(3, Might(confirmed.State)); Assert.Equal(0, confirmed.State.CardObjects["ENEMY1"].Damage);
        var done = await AttackDamageDistributionTests.Top(confirmed.State);
        Record(row, "resolved", done.State, done.Events);
        Assert.Equal(3 - amount, Might(done.State)); Assert.Equal(0, done.State.CardObjects["ENEMY1"].Damage);
        Assert.DoesNotContain(done.Events, e => e.Kind == "DAMAGE_APPLIED");
        Restore(done.State); ArmamentMightTests.PublicRecovery(done.State);
    }
}
