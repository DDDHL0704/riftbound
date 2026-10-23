using System.Text.Json;
using System.Text.Json.Nodes;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed partial class EmpoweredChosenActionReplacementTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public static IEnumerable<object[]> BasicActions()
        => new[] { "VEN·086", "VEN·181" }.SelectMany(no => new[] { "STUN", "NEGATIVE_MIGHT", "RETURN_TO_HAND" }
            .SelectMany(kind => new[] { "P1", "P2" }.Select(actor => new object[] { no, kind, actor })));

    [Theory]
    [MemberData(nameof(BasicActions))]
    public async Task ActuallyEmpoweredExactFaceReplacesFriendlyAndEnemyChosenActionsWithItsOwnPositiveRule(
        string no, string kind, string actor)
    {
        var spell = kind switch { "STUN" => "OGN·050/298", "NEGATIVE_MIGHT" => "OGN·095/298", _ => "OGN·172/298" };
        var run = new Driver(Position(no, actor, spell), $"basic-{no}-{kind}-{actor}");
        await PlayAndEmpower(run, no);
        if (actor == "P2") await OpenEnemyFocus(run);
        var before = run.Entries.Count;
        await run.Act(actor, new PlayCardCommand("SPELL", spell, ["UNIT"])); await run.Top();
        var unit = run.State.CardObjects["UNIT"];
        Assert.Equal(9, unit.Power); Assert.Equal(3, unit.UntilEndOfTurnPowerModifier);
        Assert.True(unit.IsEmpowered); Assert.Equal(1, unit.ObjectGeneration);
        Assert.Equal("BATTLEFIELD", run.State.ObjectLocations["UNIT"].Zone);
        Assert.DoesNotContain("STUNNED", unit.UntilEndOfTurnEffects);
        var modifier = Assert.Single(unit.UntilEndOfTurnPowerModifiers);
        Assert.Equal(3, modifier.PowerDelta); Assert.Equal(3, modifier.RequestedPowerDelta);
        Assert.Equal(0, modifier.MinimumPower); Assert.Null(modifier.SnapshotMight); Assert.Null(modifier.ChosenNegativeMight);
        Assert.Equal("UNIT", modifier.SourceObjectId); Assert.Equal(no, modifier.SourceCardNo);
        var context = Assert.IsType<ChosenActionReplacementContext>(modifier.ChosenActionReplacement);
        Assert.Equal(kind, context.OriginalKind); Assert.Equal(actor, context.EffectControllerId);
        Assert.Equal("SPELL", context.SourceObjectId); Assert.Equal(spell, context.SourceCardNo);
        Assert.Equal(new ObjectBinding("UNIT", 1), context.Target);
        var step = Assert.Single(context.Steps);
        Assert.Equal("TARGET_ACTION_TO_MIGHT", step.Kind); Assert.Equal(3, step.AfterDelta);
        Assert.Equal("P1", step.Rule.ControllerId); Assert.Equal(new ObjectBinding("UNIT", 1), step.Rule.Source);
        Assert.Equal(no, step.Rule.CardNo); Assert.True(step.Rule.WasEmpowered);
        var events = run.Entries.Skip(before).SelectMany(e => e.Events).ToArray();
        var positive = Assert.Single(events, e => e.Kind == "POWER_MODIFIED_UNTIL_END_OF_TURN");
        Assert.Equal("UNIT", positive.Payload["sourceObjectId"]); Assert.Equal("P1", positive.Payload["controllerId"]);
        Assert.Equal(3, Convert.ToInt32(positive.Payload["appliedPowerDelta"]));
        Assert.DoesNotContain(events, e => e.Kind == "STATUS_EFFECT_APPLIED" || e.Kind == "UNIT_RETURNED_TO_HAND");
        if (kind == "NEGATIVE_MIGHT") Assert.Contains(actor + "-DRAW-1", run.State.PlayerZones[actor].Hand);
        await run.Cold();
    }

    private static async Task PlayAndEmpower(Driver run, string no)
    {
        await run.Act("P1", new PlayCardCommand("UNIT", no, [], Destination: "BATTLEFIELD:BF"));
        Assert.Equal(6, run.State.CardObjects["UNIT"].Power); Assert.False(run.State.CardObjects["UNIT"].IsEmpowered);
        Assert.Equal(64, run.State.RunePools["P1"].Mana);
        await run.Act("P1", new ActivateAbilityCommand("UNIT", "EMPOWER:0:0", []));
        Assert.Equal(8, run.State.RunePools["P1"].PowerByTrait[RuneTrait.Orange]);
        await run.Top(); Assert.True(run.State.CardObjects["UNIT"].IsEmpowered);
    }

    [Theory]
    [InlineData("VEN·086", false)] [InlineData("VEN·086", true)]
    [InlineData("VEN·181", false)] [InlineData("VEN·181", true)]
    public async Task SwapLocksBothOriginalDifferencesBeforeReplacingOnlyItsNegativeHalf(string no, bool reverse)
    {
        var initial = AddCard(Position(no, "P1", "SFD·145/221"), "PAIR", "OGN·210/298", "P1", "BATTLEFIELD", "BF");
        var run = new Driver(initial, $"swap-{no}-{reverse}"); await PlayAndEmpower(run, no);
        Assert.Equal(2, run.State.CardObjects["PAIR"].Power);
        await run.Act("P1", new PlayCardCommand("SPELL", "SFD·145/221", reverse ? ["UNIT", "PAIR"] : ["PAIR", "UNIT"])); await run.Top();
        Assert.Equal(9, run.State.CardObjects["UNIT"].Power); Assert.Equal(6, run.State.CardObjects["PAIR"].Power);
        Assert.NotNull(Assert.Single(run.State.CardObjects["UNIT"].UntilEndOfTurnPowerModifiers).ChosenActionReplacement);
        Assert.Null(Assert.Single(run.State.CardObjects["PAIR"].UntilEndOfTurnPowerModifiers).ChosenActionReplacement);
        await run.Cold();
    }

    [Theory]
    [InlineData("VEN·086", "STUN")] [InlineData("VEN·181", "STUN")]
    [InlineData("VEN·086", "NEGATIVE_MIGHT")] [InlineData("VEN·181", "NEGATIVE_MIGHT")]
    [InlineData("VEN·086", "RETURN_TO_HAND")] [InlineData("VEN·181", "RETURN_TO_HAND")]
    public async Task ActuallyPlayedButUnempoweredFaceReceivesTheOriginalAction(string no, string kind)
    {
        var spell = kind switch { "STUN" => "OGN·050/298", "NEGATIVE_MIGHT" => "OGN·095/298", _ => "OGN·172/298" };
        var run = new Driver(Position(no, "P1", spell), $"unempowered-{no}-{kind}");
        await run.Act("P1", new PlayCardCommand("UNIT", no, [], Destination: "BATTLEFIELD:BF"));
        await run.Act("P1", new PlayCardCommand("SPELL", spell, ["UNIT"])); await run.Top();
        var unit = run.State.CardObjects["UNIT"]; Assert.False(unit.IsEmpowered);
        Assert.All(unit.UntilEndOfTurnPowerModifiers, m => Assert.Null(m.ChosenActionReplacement));
        if (kind == "STUN") { Assert.Equal(6, unit.Power); Assert.Contains("STUNNED", unit.UntilEndOfTurnEffects); }
        if (kind == "NEGATIVE_MIGHT") { Assert.Equal(5, unit.Power); Assert.Contains("P1-DRAW-1", run.State.PlayerZones["P1"].Hand); }
        if (kind == "RETURN_TO_HAND") { Assert.Contains("UNIT", run.State.PlayerZones["P1"].Hand); Assert.Equal(2, unit.ObjectGeneration); }
        await run.Cold();
    }

    [Theory]
    [InlineData("VEN·086")] [InlineData("VEN·181")]
    public async Task RealStunBeforeEmpowerMakesASecondStunAnUnreplaceableNoOp(string no)
    {
        var initial = AddCard(Position(no, "P1", "OGN·050/298"), "FIRST", "OGN·050/298", "P1", "HAND");
        var run = new Driver(initial, "already-stunned-" + no);
        await run.Act("P1", new PlayCardCommand("UNIT", no, [], Destination: "BATTLEFIELD:BF"));
        await run.Act("P1", new PlayCardCommand("FIRST", "OGN·050/298", ["UNIT"])); await run.Top();
        await run.Act("P1", new ActivateAbilityCommand("UNIT", "EMPOWER:0:0", [])); await run.Top();
        var count = run.Entries.Count;
        await run.Act("P1", new PlayCardCommand("SPELL", "OGN·050/298", ["UNIT"])); await run.Top();
        var unit = run.State.CardObjects["UNIT"];
        Assert.True(unit.IsEmpowered); Assert.Equal(6, unit.Power); Assert.Empty(unit.UntilEndOfTurnPowerModifiers);
        Assert.Single(unit.UntilEndOfTurnEffects, e => e == "STUNNED");
        Assert.DoesNotContain(run.Entries.Skip(count).SelectMany(e => e.Events), e => e.Kind == "STATUS_EFFECT_APPLIED"
            || e.Kind == "POWER_MODIFIED_UNTIL_END_OF_TURN");
        await run.Cold();
    }

    [Theory]
    [InlineData("VEN·086")] [InlineData("VEN·181")]
    public async Task RealReductionsBeforeEmpowerReachMinimumOneWithoutInventingANegativeEvent(string no)
    {
        var initial = AddCard(AddCard(Position(no, "P1", "OGN·095/298"), "SMOKE", "OGN·093/298", "P1", "HAND"),
            "STEPDOWN", "OGN·095/298", "P1", "HAND");
        var run = new Driver(initial, "minimum-one-" + no);
        await run.Act("P1", new PlayCardCommand("UNIT", no, [], Destination: "BATTLEFIELD:BF"));
        await run.Act("P1", new PlayCardCommand("SMOKE", "OGN·093/298", ["UNIT"])); await run.Top();
        await run.Act("P1", new PlayCardCommand("STEPDOWN", "OGN·095/298", ["UNIT"])); await run.Top();
        Assert.Equal(1, run.State.CardObjects["UNIT"].Power);
        await run.Act("P1", new ActivateAbilityCommand("UNIT", "EMPOWER:0:0", [])); await run.Top();
        var before = run.State.CardObjects["UNIT"].UntilEndOfTurnPowerModifiers.Count;
        await run.Act("P1", new PlayCardCommand("SPELL", "OGN·095/298", ["UNIT"])); await run.Top();
        Assert.Equal(1, run.State.CardObjects["UNIT"].Power);
        Assert.Equal(before, run.State.CardObjects["UNIT"].UntilEndOfTurnPowerModifiers.Count);
        Assert.All(run.State.CardObjects["UNIT"].UntilEndOfTurnPowerModifiers, m => Assert.Null(m.ChosenActionReplacement));
        Assert.Contains("P1-DRAW-2", run.State.PlayerZones["P1"].Hand); await run.Cold();
    }

    [Theory]
    [InlineData("VEN·086")] [InlineData("VEN·181")]
    public async Task EnemyStunReplacementCrossesMightyForTheTargetControllerWithoutPublishingPrivateWitness(string no)
    {
        var initial = AddCard(Position(no, "P2", "OGN·050/298"), "SMOKE", "OGN·093/298", "P1", "HAND");
        initial = AddCard(AddCard(initial, "OBSERVE-P1", "SFD·180/221", "P1", "BASE"),
            "OBSERVE-P2", "SFD·180/221", "P2", "BASE");
        var run = new Driver(initial, "mighty-private-witness-" + no);
        await run.Act("P1", new PlayCardCommand("UNIT", no, [], Destination: "BATTLEFIELD:BF"));
        await run.Act("P1", new PlayCardCommand("SMOKE", "OGN·093/298", ["UNIT"])); await run.Top();
        Assert.Equal(2, run.State.CardObjects["UNIT"].Power);
        await run.Act("P1", new ActivateAbilityCommand("UNIT", "EMPOWER:0:0", [])); await run.Top();
        await OpenEnemyFocus(run);
        await run.Act("P2", new PlayCardCommand("SPELL", "OGN·050/298", ["UNIT"]));
        await run.Act(run.State.PriorityPlayerId!, new PassPriorityCommand());
        await run.Act(run.State.PriorityPlayerId!, new PassPriorityCommand(), cold: false);
        Assert.Equal(5, run.State.CardObjects["UNIT"].Power);
        var contexts = run.State.TriggerQueue.Select(q => q.MightySkill).Concat(run.State.StackItems.Select(q => q.MightySkill))
            .Where(c => c is not null).Select(c => c!).ToArray();
        var context = Assert.Single(contexts);
        Assert.Equal("P1", context.ControllerId); Assert.Equal("OBSERVE-P1", context.Source.ObjectId);
        Assert.Equal("UNIT", context.Unit.ObjectId); Assert.Equal(2, context.BeforeMight); Assert.Equal(5, context.AfterMight);
        var unit = run.State.CardObjects["UNIT"];
        var privateModifier = Assert.Single(unit.UntilEndOfTurnPowerModifiers, m => m.ChosenActionReplacement is not null);
        var witness = Assert.IsType<ChosenActionReplacementContext>(privateModifier.ChosenActionReplacement);
        Assert.Equal("P2", witness.EffectControllerId); Assert.Equal("SPELL", witness.SourceObjectId);
        Assert.Equal("UNIT", Assert.Single(witness.Steps).Rule.Source.ObjectId);
        var publicUnit = context.After.CardObjects["UNIT"];
        Assert.NotSame(unit, publicUnit);
        Assert.NotSame(unit.UntilEndOfTurnPowerModifiers, publicUnit.UntilEndOfTurnPowerModifiers);
        Assert.Equal(unit.UntilEndOfTurnPowerModifiers.Count, publicUnit.UntilEndOfTurnPowerModifiers.Count);
        for (var i = 0; i < unit.UntilEndOfTurnPowerModifiers.Count; i++) {
            Assert.NotSame(unit.UntilEndOfTurnPowerModifiers[i], publicUnit.UntilEndOfTurnPowerModifiers[i]);
            Assert.Equal(unit.UntilEndOfTurnPowerModifiers[i] with { ChosenActionReplacement = null },
                publicUnit.UntilEndOfTurnPowerModifiers[i]);
        }
        Assert.Equal(JsonSerializer.Serialize(unit with { UntilEndOfTurnPowerModifiers = publicUnit.UntilEndOfTurnPowerModifiers }, Json),
            JsonSerializer.Serialize(publicUnit, Json));
        Assert.DoesNotContain("chosenActionReplacement", JsonSerializer.Serialize(context, Json));
        Assert.DoesNotContain("chosenActionReplacement", JsonSerializer.Serialize(run.Entries.SelectMany(e => e.Events), Json));
        await run.Cold();
    }

    private static MatchState AddCard(MatchState initial, string id, string no, string player, string zone, string? field = null)
    {
        var zones = initial.PlayerZones[player];
        zones = zone switch { "HAND" => zones with { Hand = [..zones.Hand, id] },
            "BASE" => zones with { Base = [..zones.Base, id] }, "BATTLEFIELD" => zones with { Battlefields = [..zones.Battlefields, id] },
            "RUNE_DECK" => zones with { RuneDeck = [..zones.RuneDeck, id] },
            "LEGEND" => zones with { LegendZone = [..zones.LegendZone, id] }, _ => throw new InvalidOperationException("Unsupported explicit QA zone.") };
        return initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                [id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, no, player) },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) { [id] = new(player, zone, field) },
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) { [player] = zones } };
    }

    [Theory]
    [InlineData("VEN·086")] [InlineData("VEN·181")]
    public async Task ReplacedReturnKeepsReconsidersIndependentRuneInstructionButDoesNotTriggerGhostBay(string no)
    {
        var initial = Position(no, "P1", "OGN·104/298");
        var cards = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["BF"] = OfficialGarenConquestBattlefieldCountTests.Printed("BF", "UNL-214/219", "P1") with { ControllerId = "P1" },
            ["RUNE1"] = OfficialGarenConquestBattlefieldCountTests.Printed("RUNE1", "UNL-R03", "P1"),
            ["RUNE2"] = OfficialGarenConquestBattlefieldCountTests.Printed("RUNE2", "UNL-R03", "P1") };
        initial = initial with { CardObjects = cards, PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
                ["P1"] = initial.PlayerZones["P1"] with { RuneDeck = ["RUNE1", "RUNE2"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) {
                ["RUNE1"] = new("P1", "RUNE_DECK"), ["RUNE2"] = new("P1", "RUNE_DECK") } };
        var run = new Driver(initial, "reconsider-independent-rune-" + no);
        await PlayAndEmpower(run, no);
        var before = run.Entries.Count; var mana = run.State.RunePools["P1"].Mana;
        await run.Act("P1", new PlayCardCommand("SPELL", "OGN·104/298", ["UNIT"])); await run.Top();
        Assert.Equal(9, run.State.CardObjects["UNIT"].Power);
        Assert.Equal(1, run.State.CardObjects["UNIT"].ObjectGeneration);
        Assert.Contains("UNIT", run.State.PlayerZones["P1"].Battlefields);
        Assert.Equal(new[] { "RUNE2" }, run.State.PlayerZones["P1"].RuneDeck);
        Assert.Contains("RUNE1", run.State.PlayerZones["P1"].Base); Assert.True(run.State.CardObjects["RUNE1"].IsExhausted);
        Assert.Equal(mana - 1, run.State.RunePools["P1"].Mana);
        var events = run.Entries.Skip(before).SelectMany(e => e.Events).ToArray();
        Assert.DoesNotContain(events, e => e.Kind == "UNIT_RETURNED_TO_HAND" || e.Kind == "BATTLEFIELD_TRIGGER_RESOLVED");
        var call = Assert.Single(events, e => e.Kind == "RUNES_CALLED");
        Assert.Equal(1, Convert.ToInt32(call.Payload["count"])); Assert.Equal("SPELL", call.Payload["sourceObjectId"]);
        await run.Cold();
    }

    private static async Task OpenEnemyFocus(Driver run)
    {
        await run.Act("P1", new MoveUnitCommand("ALLY", "BASE", "BATTLEFIELD:OTHER", []));
        await run.Act("P1", new PassFocusCommand()); Assert.Equal("P2", run.State.FocusPlayerId);
    }

    [Theory]
    [InlineData("VEN·086")] [InlineData("VEN·181")]
    public async Task RealControlChangeKeepsTheOriginalOwnerForReconsidersIndependentRuneInstruction(string no)
    {
        var initial = AddCard(Position(no, "P2", "OGN·104/298"), "STEAL", "OGN·203/298", "P2", "HAND");
        var runes = Enumerable.Range(1, 12).Select(i => "P2-FUND-" + i).ToArray();
        foreach (var id in runes) initial = AddCard(initial, id, "UNL-R05", "P2", "BASE");
        initial = AddCard(AddCard(initial, "OWNER-RUNE-1", "UNL-R03", "P1", "RUNE_DECK"),
            "OWNER-RUNE-2", "UNL-R03", "P1", "RUNE_DECK");
        var run = new Driver(initial, "reconsider-real-borrowed-owner-" + no);
        await PlayAndEmpower(run, no); await run.Act("P1", new EndTurnCommand());
        await run.Act("P2", new TapRuneCommand(runes[0], runes));
        await run.Act("P2", new RecycleRuneCommand(runes[0], runes.Take(3).ToArray()));
        await run.Act("P2", new PlayCardCommand("STEAL", "OGN·203/298", ["UNIT"])); await run.Top();
        var before = run.State.CardObjects["UNIT"];
        Assert.Equal("P1", before.OwnerId); Assert.Equal("P2", before.ControllerId); Assert.True(before.IsEmpowered);
        Assert.Contains("UNIT", run.State.PlayerZones["P2"].Base); Assert.Equal(6, before.Power);
        var count = run.Entries.Count;
        await run.Act("P2", new PlayCardCommand("SPELL", "OGN·104/298", ["UNIT"])); await run.Top();
        var unit = run.State.CardObjects["UNIT"];
        Assert.Equal(9, unit.Power); Assert.Equal(before.ObjectGeneration, unit.ObjectGeneration);
        Assert.Equal("P1", unit.OwnerId); Assert.Equal("P2", unit.ControllerId);
        Assert.Contains("UNIT", run.State.PlayerZones["P2"].Base);
        Assert.Equal(new[] { "OWNER-RUNE-2" }, run.State.PlayerZones["P1"].RuneDeck);
        Assert.Contains("OWNER-RUNE-1", run.State.PlayerZones["P1"].Base);
        Assert.True(run.State.CardObjects["OWNER-RUNE-1"].IsExhausted);
        var events = run.Entries.Skip(count).SelectMany(e => e.Events).ToArray();
        var called = Assert.Single(events, e => e.Kind == "RUNES_CALLED");
        Assert.Equal("P1", called.Payload["playerId"]); Assert.Equal("SPELL", called.Payload["sourceObjectId"]);
        Assert.Equal(1, Convert.ToInt32(called.Payload["count"]));
        Assert.DoesNotContain(events, e => e.Kind == "UNIT_RETURNED_TO_HAND");
        var witness = Assert.Single(unit.UntilEndOfTurnPowerModifiers).ChosenActionReplacement!;
        Assert.Equal("P2", witness.EffectControllerId); Assert.Equal("P2", Assert.Single(witness.Steps).Rule.ControllerId);
        await run.Cold();
    }

    [Theory]
    [InlineData("VEN·086", true)] [InlineData("VEN·086", false)]
    [InlineData("VEN·181", true)] [InlineData("VEN·181", false)]
    public async Task AffectedControllerChoosesCasterOrTargetFirstAndOnlyOnePositiveActionCommits(string no, bool casterFirst)
    {
        var initial = AddCard(Position(no, "P2", "OGN·095/298"), "MEL", "VEN·069", "P2", "HAND");
        var runes = Enumerable.Range(1, 8).Select(i => "P2-MANA-" + i).ToArray();
        foreach (var id in runes) initial = AddCard(initial, id, "UNL-R03", "P2", "BASE");
        var run = new Driver(initial, $"caster-target-order-{no}-{casterFirst}");
        await PlayAndEmpower(run, no); await run.Act("P1", new EndTurnCommand());
        await run.Act("P2", new TapRuneCommand(runes[0], runes));
        await run.Act("P2", new RecycleRuneCommand(runes[0]));
        await run.Act("P2", new PlayCardCommand("MEL", "VEN·069", [], Destination: "BASE"));
        while (run.State.StackItems.Count > 0) await run.Top();
        await run.Act("P2", new ActivateAbilityCommand("MEL", "EMPOWER:0:0", [])); await run.Top();
        Assert.True(run.State.CardObjects["MEL"].IsEmpowered);
        await run.Act("P2", new PlayCardCommand("SPELL", "OGN·095/298", ["UNIT"])); await run.Top();
        var pending = Assert.IsType<RuleChoiceContinuation>(run.State.PendingRuleChoice);
        Assert.Equal("P1", pending.Request.PlayerId); Assert.Equal("TARGETED_ACTION_REPLACEMENT", pending.Request.Kind);
        Assert.Equal(2, pending.Request.Options.Count); Assert.Equal(0, pending.Request.ManaCost); Assert.Equal(0, pending.Request.PowerCost);
        var selected = Assert.Single(pending.Request.Options, o => o.ObjectIds!.Contains(casterFirst ? "MEL" : "UNIT"));
        var choice = new PayCostCommand(pending.Request.Id, "RULE_REPLACEMENT", [selected.Id]);
        await run.Reject("P2", choice); await run.Act("P1", choice);
        Assert.Null(run.State.PendingRuleChoice); Assert.Equal(9, run.State.CardObjects["UNIT"].Power);
        var modifier = Assert.Single(run.State.CardObjects["UNIT"].UntilEndOfTurnPowerModifiers);
        var context = Assert.IsType<ChosenActionReplacementContext>(modifier.ChosenActionReplacement);
        Assert.Equal("P2", context.EffectControllerId); Assert.Equal(3, modifier.PowerDelta); Assert.Null(modifier.ChosenNegativeMight);
        Assert.Equal(casterFirst ? 2 : 1, context.Steps.Count);
        if (casterFirst) { Assert.Equal("MEL", context.Steps[0].Rule.Source.ObjectId); Assert.Equal(-2, context.Steps[0].AfterDelta); }
        Assert.Equal("UNIT", context.Steps[^1].Rule.Source.ObjectId); Assert.Equal("P1", context.Steps[^1].Rule.ControllerId);
        Assert.Equal(0, run.State.RunePools["P2"].Mana);
        await run.Cold();
    }

    [Theory]
    [InlineData("VEN·086", true)] [InlineData("VEN·086", false)]
    [InlineData("VEN·181", true)] [InlineData("VEN·181", false)]
    public async Task IndependentCopiedTargetRuleInstancesAreAlternativesForOneEventRatherThanTwoBonuses(string no, bool printed)
    {
        var initial = AddCard(Position(no, "P1", "OGN·095/298"), "GEAR", "SFD·059/221", "P1", "HAND");
        var run = new Driver(initial, $"copied-target-order-{no}-{printed}"); await PlayAndEmpower(run, no);
        await run.Act("P1", new PlayCardCommand("GEAR", "SFD·059/221", [], Destination: "BASE"));
        await run.Act("P1", new AssembleEquipmentCommand("GEAR", "UNIT", ["ASSEMBLE_GREEN"])); await run.Top();
        Assert.Equal("UNIT", run.State.CardObjects["GEAR"].AttachedToObjectId);
        await run.Act("P1", new PlayCardCommand("SPELL", "OGN·095/298", ["UNIT"])); await run.Top();
        var request = run.State.PendingRuleChoice!.Request;
        Assert.Equal("P1", request.PlayerId); Assert.Equal(2, request.Options.Count);
        var selected = Assert.Single(request.Options, o => o.Id.Contains(printed ? "PRINTED:" : "ATTACHED:", StringComparison.Ordinal));
        await run.Act("P1", new PayCostCommand(request.Id, "RULE_REPLACEMENT", [selected.Id]));
        Assert.Equal(9, run.State.CardObjects["UNIT"].Power);
        var context = Assert.IsType<ChosenActionReplacementContext>(Assert.Single(run.State.CardObjects["UNIT"].UntilEndOfTurnPowerModifiers).ChosenActionReplacement);
        var step = Assert.Single(context.Steps); Assert.Equal(printed, step.Rule.Origin is null);
        Assert.Equal(3, step.AfterDelta); Assert.Equal("UNIT", step.Rule.Source.ObjectId);
        if (!printed) { Assert.Equal("GEAR", step.Rule.Origin!.Equipment.ObjectId); Assert.Equal(1, step.Rule.Origin.Revision); }
        await run.Cold();
    }

    [Theory]
    [InlineData("VEN·086")] [InlineData("VEN·181")]
    public async Task EchoKeepsTheFirstReplacementInTheSecondChoicePreviewAndColdRecoversBothDecisions(string no)
    {
        var initial = AddCard(AddCard(Position(no, "P2", "SFD·040/221"), "GEAR", "SFD·059/221", "P1", "HAND"),
            "READY", "OGN·146/298", "P1", "HAND");
        var run = new Driver(initial, "echo-two-choice-" + no);
        await run.Act("P1", new PlayCardCommand("UNIT", no, [], Destination: "BASE"));
        await run.Act("P1", new ActivateAbilityCommand("UNIT", "EMPOWER:0:0", [])); await run.Top();
        await run.Act("P1", new PlayCardCommand("GEAR", "SFD·059/221", [], Destination: "BASE"));
        await run.Act("P1", new AssembleEquipmentCommand("GEAR", "UNIT", ["ASSEMBLE_GREEN"])); await run.Top();
        await run.Act("P1", new PlayCardCommand("READY", "OGN·146/298", ["UNIT"])); await run.Top();
        await run.Act("P1", new MoveUnitCommand("UNIT", "BASE", "BATTLEFIELD:OTHER", []));
        Assert.True(run.State.CardObjects["UNIT"].IsAttacking);
        await run.Act("P1", new PassFocusCommand());
        var before = run.Entries.Count;
        await run.Act("P2", new PlayCardCommand("SPELL", "SFD·040/221", ["UNIT"], OptionalCosts: ["ECHO"],
            RepeatChoices: [new("", ["UNIT"])])); await run.Top();
        var first = Assert.IsType<RuleChoiceContinuation>(run.State.PendingRuleChoice);
        Assert.Equal("P1", first.Request.PlayerId); Assert.Equal(2, first.Request.Options.Count);
        Assert.Empty(first.Answers); Assert.Equal(6, first.Request.Preview!.CardObjects["UNIT"].Power);
        var printed = Assert.Single(first.Request.Options, o => o.Id.Contains("PRINTED:", StringComparison.Ordinal));
        await run.Act("P1", new PayCostCommand(first.Request.Id, "RULE_REPLACEMENT", [printed.Id]));
        var second = Assert.IsType<RuleChoiceContinuation>(run.State.PendingRuleChoice);
        Assert.Single(second.Answers); Assert.Equal("P1", second.Request.PlayerId); Assert.Equal(2, second.Request.Options.Count);
        Assert.Equal(6, run.State.CardObjects["UNIT"].Power); Assert.Empty(run.State.CardObjects["UNIT"].UntilEndOfTurnPowerModifiers);
        var preview = second.Request.Preview!.CardObjects["UNIT"];
        Assert.Equal(9, preview.Power);
        var priorWitness = Assert.IsType<ChosenActionReplacementContext>(Assert.Single(preview.UntilEndOfTurnPowerModifiers).ChosenActionReplacement);
        Assert.Equal(second.Origin.Tick + 1, priorWitness.AppliedTick); Assert.Equal(0, priorWitness.Occurrence);
        await run.Cold();
        var copied = Assert.Single(second.Request.Options, o => o.Id.Contains("ATTACHED:", StringComparison.Ordinal));
        await run.Act("P1", new PayCostCommand(second.Request.Id, "RULE_REPLACEMENT", [copied.Id]));
        Assert.Null(run.State.PendingRuleChoice); Assert.Equal(12, run.State.CardObjects["UNIT"].Power);
        Assert.DoesNotContain("STUNNED", run.State.CardObjects["UNIT"].UntilEndOfTurnEffects);
        var modifiers = run.State.CardObjects["UNIT"].UntilEndOfTurnPowerModifiers;
        Assert.Equal(2, modifiers.Count); Assert.All(modifiers, m => Assert.Equal(3, m.PowerDelta));
        Assert.All(modifiers, m => Assert.Equal(second.Origin.Tick + 1, m.ChosenActionReplacement!.AppliedTick));
        Assert.Equal(new[] { 0, 1 }, modifiers.Select(m => m.ChosenActionReplacement!.Occurrence));
        Assert.Null(modifiers[0].ChosenActionReplacement!.Steps[^1].Rule.Origin);
        Assert.Equal("GEAR", modifiers[1].ChosenActionReplacement!.Steps[^1].Rule.Origin!.Equipment.ObjectId);
        var changes = run.Entries.Skip(before).SelectMany(e => e.Events).Where(e => e.Kind == "POWER_MODIFIED_UNTIL_END_OF_TURN").ToArray();
        Assert.Equal(2, changes.Length); Assert.All(changes, e => Assert.Equal(3, Convert.ToInt32(e.Payload["appliedPowerDelta"])));
        await run.Cold();
    }

    private static MatchState Position(string no, string actor, string spell)
    {
        var cards = new Dictionary<string, CardObjectState>(); var locations = new Dictionary<string, ObjectLocationState>();
        void Put(string id, string number, string owner, string zone, string? field = null)
        { cards[id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, number, owner); locations[id] = new(owner, zone, field); }
        Put("UNIT", no, "P1", "HAND"); Put("SPELL", spell, actor, "HAND");
        Put("ALLY", "OGN·219/298", "P1", "BASE"); Put("ENEMY", "OGN·219/298", "P2", "BATTLEFIELD", "OTHER");
        Put("BF", "OGN·278/298", "P1", "BATTLEFIELD", "BF"); cards["BF"] = cards["BF"] with { ControllerId = "P1" };
        Put("OTHER", "OGN·278/298", "P2", "BATTLEFIELD", "OTHER"); cards["OTHER"] = cards["OTHER"] with { ControllerId = "P2" };
        var zones = new Dictionary<string, PlayerZones>(); var pools = new Dictionary<string, RunePool>();
        foreach (var player in new[] { "P1", "P2" })
        {
            var deck = Enumerable.Range(1, 4).Select(i => player + "-DRAW-" + i).Append("NEVER-" + player).ToArray();
            foreach (var id in deck) Put(id, "OGN·219/298", player, "MAIN_DECK");
            zones[player] = PlayerZones.Empty with { MainDeck = deck,
                Hand = player == "P1" ? actor == "P1" ? ["UNIT", "SPELL"] : ["UNIT"] : actor == "P2" ? ["SPELL"] : [],
                Base = player == "P1" ? ["ALLY"] : [], Battlefields = player == "P1" ? ["BF"] : ["OTHER", "ENEMY"] };
            pools[player] = new(70, 0, new Dictionary<string, int> {
                [RuneTrait.Red] = 10, [RuneTrait.Blue] = 10, [RuneTrait.Green] = 10,
                [RuneTrait.Yellow] = 10, [RuneTrait.Orange] = 10, [RuneTrait.Purple] = 10 });
        }
        return new MatchState("C7-CHOSEN-ACTION", 0, 5, "P1", new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" },
            status: MatchStatuses.InProgress, readyPlayerIds: ["P1", "P2"], turnPlayerId: "P1", phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen, playerZones: zones, cardObjects: cards, objectLocations: locations,
            runePools: pools, seed: 424242) with { CardPlays = new(5, [], []) };
    }

    private sealed class Driver : IMatchJournal
    {
        internal readonly MatchState Initial;
        internal List<MatchJournalEntry> Entries { get; } = [];
        internal MatchState State { get; private set; }
        internal MatchSession Session { get; private set; }
        private readonly string? output;
        internal Driver(MatchState initial, string label)
        {
            Initial = initial; State = initial; Session = new(initial, new CoreRuleEngine(), this);
            if (Environment.GetEnvironmentVariable("RIFTBOUND_C7_EVIDENCE_DIR") is { Length: > 0 } root)
            {
                output = Path.Combine(root, label.Replace('/', '_'));
                Assert.False(Directory.Exists(output)); Directory.CreateDirectory(output); Save("initial.json", initial);
            }
        }
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken token)
        { Entries.Add(entry); Save("journal-" + Entries.Count + ".json", entry); return ValueTask.CompletedTask; }
        internal JsonElement Raw(string actor, GameCommand command)
        {
            var raw = JsonSerializer.SerializeToNode(command, command.GetType(), Json)!.AsObject();
            raw["promptId"] = Session.PromptFor(actor).PromptId; raw["snapshotTick"] = State.Tick;
            return JsonSerializer.SerializeToElement(raw);
        }
        internal async Task Act(string actor, GameCommand command, bool cold = true)
        {
            var raw = Raw(actor, command); var id = "chosen-action-" + Entries.Count;
            command = GameCommandJsonMapper.Map(raw);
            var result = await Session.SubmitAsync(actor, id, command, raw, default);
            Save("result-" + Entries.Count + ".json", result);
            Assert.True(result.Accepted, result.ErrorMessage); State = result.State;
            FullGameEndToEndTests.AssertNoHiddenZoneLeak(result);
            var count = Entries.Count;
            var duplicate = await Session.SubmitAsync(actor, id, command, raw, default);
            Assert.True(duplicate.Accepted); Assert.Equal(count, Entries.Count);
            Assert.Equal(MatchStateHasher.Hash(State), MatchStateHasher.Hash(duplicate.State));
            if (cold) await Cold();
        }
        internal async Task Top() { await Act(State.PriorityPlayerId!, new PassPriorityCommand()); await Act(State.PriorityPlayerId!, new PassPriorityCommand()); }
        internal async Task<ResolutionResult> Reject(string actor, GameCommand command, bool cold = true)
        {
            var hash = MatchStateHasher.Hash(State); var snapshots = JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(State), Json);
            var prompts = JsonSerializer.Serialize(ResolutionResult.BuildPrompts(State), Json);
            var raw = Raw(actor, command); var id = "chosen-rejection-" + Entries.Count;
            var result = await Session.SubmitAsync(actor, id, GameCommandJsonMapper.Map(raw), raw, default);
            Assert.False(result.Accepted); Assert.Empty(result.Events); Assert.Equal(hash, MatchStateHasher.Hash(result.State));
            Assert.Equal(snapshots, JsonSerializer.Serialize(result.Snapshots, Json)); Assert.Equal(prompts, JsonSerializer.Serialize(result.Prompts, Json));
            var count = Entries.Count;
            var duplicate = await Session.SubmitAsync(actor, id, GameCommandJsonMapper.Map(raw), raw, default);
            Assert.False(duplicate.Accepted); Assert.Equal(result.ErrorCode, duplicate.ErrorCode); Assert.Equal(count, Entries.Count);
            Save("rejection-" + Entries.Count + ".json", result);
            if (cold) await Cold();
            return result;
        }
        internal async Task Cold()
        {
            var last = Entries[^1]; var sequence = last.CompletedEventSequence;
            var commands = Entries.Select(e => new RecoveredCommand(e.PlayerId, e.ClientIntentId, e.CommandType, e.RawCommand,
                e.StartedTick, e.CompletedTick, e.StartedEventSequence, e.CompletedEventSequence, e.Accepted, e.ErrorMessage) { RuleInputs = e.RuleInputs }).ToArray();
            var events = Entries.SelectMany(e => e.Events.Select((e0, i) => new RecoveredEvent(e.StartedEventSequence + i + 1, e.CompletedTick, i, e0))).ToArray();
            var snapshots = ResolutionResult.BuildSnapshots(State); var prompts = ResolutionResult.BuildPrompts(State);
            var views = State.Seats.Keys.ToDictionary(p => p, p => new RecoveredPlayerView(p, State.Tick, sequence,
                snapshots[p], State.Tick, sequence, prompts[p]));
            var spectator = MatchReplayRedactor.BuildSpectatorFrame(State.RoomId, State.Tick, sequence, [], State);
            var frame = new MatchRecoveryFrame(State.RoomId, State.Tick, sequence, commands, events, views, [], State, Initial, spectator) { OpeningSeed = Initial.Seed };
            var encoded = JsonSerializer.Serialize(frame, Json);
            frame = JsonSerializer.Deserialize<MatchRecoveryFrame>(encoded, Json)!;
            Assert.Empty(MatchRecoveryValidator.Validate(frame.RoomId, sequence, frame.Commands, frame.Events,
                frame.PlayerViews, frame.AuthoritativeState, frame.CurrentTick, frame.SpectatorReplayFrame));
            var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(Initial, frame.Commands, frame.AuthoritativeState!, new CoreRuleEngine(), default, frame.Events);
            Assert.True(replay.IsMatch, string.Join("; ", replay.Errors));
            Assert.Equal(MatchStateHasher.Hash(State), MatchStateHasher.Hash(frame.AuthoritativeState!));
            Session = MatchSession.Restore(frame, new CoreRuleEngine(), this); State = frame.AuthoritativeState!;
            foreach (var actor in State.Seats.Keys)
            {
                Assert.True(JsonNode.DeepEquals(JsonSerializer.SerializeToNode(snapshots[actor], Json), JsonSerializer.SerializeToNode(Session.SnapshotFor(actor), Json)));
                Assert.True(JsonNode.DeepEquals(JsonSerializer.SerializeToNode(prompts[actor], Json), JsonSerializer.SerializeToNode(Session.PromptFor(actor), Json)));
                var publicView = JsonSerializer.Serialize(new { snapshot = Session.SnapshotFor(actor), prompt = Session.PromptFor(actor) }, Json);
                Assert.DoesNotContain("NEVER-P1", publicView); Assert.DoesNotContain("NEVER-P2", publicView);
                Assert.DoesNotContain("chosenActionReplacement", publicView);
            }
            Assert.DoesNotContain("chosenActionReplacement", JsonSerializer.Serialize(spectator, Json));
            Save("checkpoint-" + Entries.Count + ".json", frame);
        }
        internal void Save(string name, object value)
        { if (output is not null) File.WriteAllText(Path.Combine(output, name), JsonSerializer.Serialize(value, value.GetType(), Json)); }
    }
}
