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

    internal static async Task<ResolutionResult> Attack(MatchState initial)
    {
        Restore(initial); ArmamentMightTests.PublicRecovery(initial);
        var attack = await Act(initial, "P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F", []));
        Restore(attack.State); ArmamentMightTests.PublicRecovery(attack.State);
        return attack;
    }

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

    [Theory]
    [InlineData("PAY")]
    [InlineData("DECLINE")]
    public async Task WardMustBePaidBeforeResponsesAndDecliningDoesNotReduceMight(string decision)
    {
        var initial = await Position("RAD·054/167", 1);
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["ENEMY1"] = initial.CardObjects["ENEMY1"] with {
                Tags = initial.CardObjects["ENEMY1"].Tags.Append("法盾2").Order(StringComparer.Ordinal).ToArray() } } };
        Record("ward-" + decision, "initial", initial);
        var attack = await Attack(initial);
        var selected = await AttackDamageDistributionTests.Confirm(attack.State, "ENEMY1");
        var payment = Assert.IsType<PendingPaymentState>(selected.State.PendingPayment);
        Assert.Equal(2, payment.PowerCost); Assert.Null(selected.State.PriorityPlayerId); Assert.Equal(3, Might(selected.State));
        var paid = await Act(selected.State, "P1", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, [decision]));
        Record("ward-" + decision, "paid", paid.State, paid.Events);
        if (decision == "PAY")
        {
            Assert.Equal(initial.RunePools["P1"].Power - 2, paid.State.RunePools["P1"].Power);
            Assert.NotNull(paid.State.PriorityPlayerId); Assert.Single(paid.State.StackItems);
            paid = await AttackDamageDistributionTests.Top(paid.State);
            Assert.Equal(2, Might(paid.State));
        }
        else
        {
            Assert.Equal(initial.RunePools["P1"], paid.State.RunePools["P1"]);
            Assert.Empty(paid.State.StackItems); Assert.Equal(3, Might(paid.State));
        }
        Assert.Equal(0, paid.State.CardObjects["ENEMY1"].Damage);
        Record("ward-" + decision, "done", paid.State, paid.Events);
        Restore(paid.State); ArmamentMightTests.PublicRecovery(paid.State);
    }

    [Theory]
    [InlineData("HOST", "P1")]
    [InlineData("ENEMY1", "P2")]
    public async Task ActualFlashOfEitherSourceOrChosenTargetInvalidatesHereWithoutRetargeting(string movedId, string actor)
    {
        var initial = AttackDamageDistributionTests.Spell(await Position("RAD·050/167", 2), "FLASH", "OGS·011/024", actor);
        var confirmed = await AttackDamageDistributionTests.Confirm((await Attack(initial)).State, "ENEMY1");
        if (actor == "P2") confirmed = await Act(confirmed.State, "P1", new PassPriorityCommand());
        var moved = await AttackDamageDistributionTests.Top((await Act(confirmed.State, actor,
            new PlayCardCommand("FLASH", "OGS·011/024", [movedId]))).State);
        Assert.Equal("BASE", moved.State.ObjectLocations[movedId].Zone);
        Record("flash-" + movedId, "response", moved.State, moved.Events);
        var done = await AttackDamageDistributionTests.Top(moved.State);
        Assert.Equal(3, Might(done.State)); Assert.Equal(0, done.State.CardObjects["ENEMY1"].Damage);
        Assert.DoesNotContain(done.Events, e => e.Kind is "DAMAGE_APPLIED" or "POWER_MODIFIED_UNTIL_END_OF_TURN");
        Assert.Empty(done.State.StackItems);
        Record("flash-" + movedId, "done", done.State, done.Events);
        Restore(done.State); ArmamentMightTests.PublicRecovery(done.State);
    }

    [Fact]
    public async Task AppliedReductionSurvivesOrdinaryMovementAndExpiresAtActualTurnCleanup()
    {
        var initial = AttackDamageDistributionTests.Spell(await Position("RAD·054/167", 1), "FLASH", "OGS·011/024", "P2");
        var confirmed = await AttackDamageDistributionTests.Confirm((await Attack(initial)).State, "ENEMY1");
        var current = await AttackDamageDistributionTests.Top(confirmed.State);
        Assert.Equal(2, Might(current.State));
        if (current.State.FocusPlayerId != "P2") current = await Act(current.State, current.State.FocusPlayerId!, new PassFocusCommand());
        current = await AttackDamageDistributionTests.Top((await Act(current.State, "P2",
            new PlayCardCommand("FLASH", "OGS·011/024", ["ENEMY1"]))).State);
        Assert.Equal("BASE", current.State.ObjectLocations["ENEMY1"].Zone);
        Assert.Equal(2, Might(current.State));
        for (var i = 0; i < 6 && current.State.FocusPlayerId is not null; i++)
            current = await Act(current.State, current.State.FocusPlayerId, new PassFocusCommand());
        Assert.Contains(CommandTypes.EndTurn, current.Prompts["P1"].EnabledActions());
        current = await Act(current.State, "P1", new EndTurnCommand());
        Assert.Equal(3, Might(current.State));
        Assert.Empty(current.State.CardObjects["ENEMY1"].UntilEndOfTurnPowerModifiers);
        Assert.Equal(0, current.State.CardObjects["ENEMY1"].UntilEndOfTurnPowerModifier);
        Record("turn-cleanup", "done", current.State, current.Events);
        Restore(current.State); ArmamentMightTests.PublicRecovery(current.State);
    }

    [Theory]
    [InlineData("empty")]
    [InlineData("friendly")]
    [InlineData("elsewhere")]
    [InlineData("two")]
    [InlineData("actor")]
    public async Task InvalidTargetSelectionsCannotChangeThePendingSkill(string branch)
    {
        var attack = await Attack(await Position("RAD·054/167", 1));
        var choice = Assert.IsType<PendingCardChoiceState>(attack.State.PendingCardChoice);
        string[] selected = branch switch {
            "empty" => [], "friendly" => ["HOST"], "elsewhere" => ["ENEMY2"],
            "two" => ["ENEMY1", "ENEMY2"], _ => ["ENEMY1"] };
        var command = new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, selected);
        var result = await new CoreRuleEngine().ResolveAsync(attack.State,
            new("bad-disarm-target", branch == "actor" ? "P2" : "P1", command.CmdType), command, default);
        Assert.False(result.Accepted); Assert.Empty(result.Events);
        Assert.Equal(MatchStateHasher.Hash(attack.State), MatchStateHasher.Hash(result.State));
        Record("bad-target-" + branch, "rejected", result.State, result.Events, result.Accepted, result.ErrorMessage);
    }

    [Theory]
    [InlineData("amount")]
    [InlineData("zero")]
    [InlineData("negative")]
    [InlineData("duplicate")]
    [InlineData("foreign-source")]
    [InlineData("generation")]
    [InlineData("field-generation")]
    [InlineData("card")]
    [InlineData("aura")]
    [InlineData("missing")]
    [InlineData("other-kind")]
    [InlineData("location")]
    public async Task RecoveryAndConfirmationRejectForgedDisarmContributions(string branch)
    {
        var attack = await Attack(await Position("RAD·054/167", 1));
        var item = Assert.Single(attack.State.StackItems); var field = Assert.IsType<FieldTriggerContext>(item.FieldContext);
        var contribution = Assert.Single(field.Disarm!.Contributions);
        var changed = branch switch {
            "amount" => contribution with { Amount = 99 }, "zero" => contribution with { Amount = 0 },
            "negative" => contribution with { Amount = -1 },
            "foreign-source" => contribution with { Source = new("ENEMY1", 0) },
            "generation" => contribution with { Source = contribution.Source with { Generation = contribution.Source.Generation + 1 } },
            "card" => contribution with { CardNo = "OGN·001/298" },
            "aura" => contribution with { AuraKey = "FORGED" }, _ => contribution };
        field = field with { Disarm = new(branch == "duplicate" ? [changed, changed] : [changed]) };
        if (branch == "field-generation") field = field with { SourceGeneration = field.SourceGeneration + 1 };
        if (branch == "missing") field = field with { Disarm = null };
        if (branch == "other-kind") field = field with { Kind = "COMBAT_MIGHT" };
        if (branch == "location") field = field with { SourceLocation = new("P1", "BASE") };
        var forged = attack.State with { StackItems = [item with { FieldContext = field }] };
        Assert.NotEmpty(MatchRecoveryValidator.ValidateComponentStateAndViews(forged));
        Record("bad-context-" + branch, "forged", forged);
        var choice = attack.State.PendingCardChoice!;
        var command = new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ENEMY1"]);
        var rejected = await new CoreRuleEngine().ResolveAsync(forged, new("forged-disarm", "P1", command.CmdType), command, default);
        Assert.False(rejected.Accepted); Assert.Empty(rejected.Events);
        Assert.Equal(MatchStateHasher.Hash(forged), MatchStateHasher.Hash(rejected.State));
    }
}
