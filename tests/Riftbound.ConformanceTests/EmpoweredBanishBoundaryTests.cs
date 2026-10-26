using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed partial class EmpoweredBanishBoundaryTests
{
    private const string Mel = "VEN·110";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    [Theory]
    [InlineData("PAY")] [InlineData("DECLINE")] [InlineData("INSUFFICIENT")]
    public async Task WardIsPaidAfterSelectionOrTheIndependentSkillIsDeclined(string branch)
    {
        var initial = Position("OGN·013/298");
        if (branch == "INSUFFICIENT") initial = initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) { ["P1"] = new(20, 0) } };
        await Run("ward-" + branch, initial, async game => {
            await Empower(game); await Choose(game, "TARGET");
            var payment = Assert.IsType<PendingPaymentState>(game.State.PendingPayment);
            Assert.Equal("TRIGGER_TARGET_COST", payment.PaymentWindow); Assert.Equal(1, payment.PowerCost);
            Assert.Null(game.State.PriorityPlayerId); Assert.True(game.State.CardObjects["UNIT"].IsEmpowered);
            Assert.Contains("FODDER", game.State.PlayerZones["P1"].Graveyard);
            var before = game.State.RunePools["P1"].TotalPower;
            if (branch == "INSUFFICIENT") await Reject(game, "P1", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, ["PAY"]));
            await game.Act("P1", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, [branch == "PAY" ? "PAY" : "DECLINE"]));
            Assert.Equal(before - (branch == "PAY" ? 1 : 0), game.State.RunePools["P1"].TotalPower);
            if (branch == "PAY") { Assert.NotNull(game.State.PriorityPlayerId); await game.Top(); Assert.Contains("TARGET", game.State.PlayerZones["P2"].Banished); }
            else { Assert.Empty(game.State.StackItems); Assert.DoesNotContain("TARGET", game.State.PlayerZones["P2"].Banished); }
        });
    }

    [Theory]
    [InlineData("friendly")] [InlineData("base")] [InlineData("large")] [InlineData("missing")]
    [InlineData("duplicate")] [InlineData("extra")] [InlineData("wrong-player")] [InlineData("wrong-window")]
    public async Task IllegalTargetConfirmationIsAtomicAndLeavesTheOriginalChoiceUsable(string branch)
    {
        await Run("invalid-target-" + branch, Position(), async game => {
            await Empower(game); var choice = game.State.PendingCardChoice!;
            var selected = branch switch { "friendly" => new[] { "ALLY" }, "base" => ["BASE-TARGET"], "large" => ["ENEMY"],
                "missing" => [], "duplicate" => ["TARGET", "TARGET"], "extra" => ["TARGET", "BASE-TARGET"], _ => ["TARGET"] };
            await Reject(game, branch == "wrong-player" ? "P2" : "P1", new ChooseCardsCommand(choice.ChoiceId,
                branch == "wrong-window" ? "MAIN_DECK" : choice.ChoiceWindow, selected));
            Assert.Equal(choice.ChoiceId, game.State.PendingCardChoice!.ChoiceId);
            await Choose(game, "TARGET"); await game.Top(); Assert.Contains("TARGET", game.State.PlayerZones["P2"].Banished);
        });
    }

    [Theory]
    [InlineData("might")] [InlineData("return")]
    public async Task ResponsesRecheckCurrentMightAndTheBoundTargetIncarnation(string branch)
    {
        var initial = Put(Position(), "RESPONSE", branch == "might" ? "UNL-031/219" : "OGN·104/298", "P2", "HAND");
        await Run("response-" + branch, initial, async game => {
            await Empower(game); await Choose(game, "TARGET"); var generation = game.State.CardObjects["TARGET"].ObjectGeneration;
            if (game.State.PriorityPlayerId != "P2") await game.Act(game.State.PriorityPlayerId!, new PassPriorityCommand());
            await game.Act("P2", new PlayCardCommand("RESPONSE", branch == "might" ? "UNL-031/219" : "OGN·104/298", ["TARGET"]));
            await game.Top(); Assert.Single(game.State.StackItems);
            if (branch == "might") Assert.Equal(4, Might(game.State, "P2", "TARGET"));
            else { Assert.Contains("TARGET", game.State.PlayerZones["P2"].Hand); Assert.Equal(generation + 1, game.State.CardObjects["TARGET"].ObjectGeneration); }
            await game.Top(); Assert.DoesNotContain("TARGET", game.State.PlayerZones["P2"].Banished);
            Assert.Empty(game.State.StackItems); Assert.True(game.State.CardObjects["UNIT"].IsEmpowered);
        });
    }

    [Fact]
    public async Task BattlefieldAuraUsesCurrentMightInsteadOfThePrintedNumber()
    {
        var initial = Put(Position(), "ELIGIBLE", "OGN·096/298", "P2", "BATTLEFIELD", "OTHER");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["OTHER"] = OfficialGarenConquestBattlefieldCountTests.Printed("OTHER", "OGN·294/298", "P2") with { ControllerId = "P2" } } };
        await Run("current-aura-might", initial, async game => {
            Assert.Equal(3, game.State.CardObjects["TARGET"].Power); Assert.Equal(4, Might(game.State, "P2", "TARGET"));
            Assert.Equal(2, Might(game.State, "P2", "ELIGIBLE")); await Empower(game);
            Assert.Equal(new[] { "ELIGIBLE" }, game.State.PendingCardChoice!.LegalObjectIds);
            await Choose(game, "ELIGIBLE"); await game.Top(); Assert.Contains("ELIGIBLE", game.State.PlayerZones["P2"].Banished);
            Assert.Contains("TARGET", game.State.PlayerZones["P2"].Battlefields);
        });
    }

    [Fact]
    public async Task AnActualNegativeMightSpellCanMakeALargerPrintedUnitEligible()
    {
        var initial = Put(Position(), "REDUCE", "SFD·066/221", "P1", "HAND");
        initial = LocalDestructionRecallTests.At(initial, "ENEMY", "P2", "BATTLEFIELD", "OTHER");
        await Run("reduced-current-might", initial, async game => {
            Assert.Equal(4, Might(game.State, "P2", "ENEMY"));
            await game.Act("P1", new PlayCardCommand("REDUCE", "SFD·066/221", ["ENEMY"])); await game.Top();
            Assert.Equal(2, Might(game.State, "P2", "ENEMY")); await Empower(game);
            Assert.Contains("ENEMY", game.State.PendingCardChoice!.LegalObjectIds);
            await Choose(game, "ENEMY"); await game.Top(); Assert.Contains("ENEMY", game.State.PlayerZones["P2"].Banished);
        });
    }

    [Theory]
    [InlineData(1)] [InlineData(2)]
    public async Task EveryActuallyAttachedTextInstanceEarnsItsOwnOrderedTargetedSkill(int copies)
    {
        var initial = CopyPosition(copies);
        await Run("native-and-copies-" + copies, initial, async game => {
            await CaptureCopies(game, copies); var queue = game.State.TriggerQueue;
            Assert.Equal(copies + 1, queue.Count); Assert.Single(queue, q => q.FieldContext!.Origin is null);
            Assert.Equal(copies, queue.Count(q => q.FieldContext!.Origin is not null));
            Assert.Equal(copies + 1, queue.Select(q => q.TriggerId).Distinct().Count());
            Assert.All(queue, q => { Assert.Equal(game.State.Tick, q.EnqueuedAtTick); Assert.Equal(game.State.Tick, q.FieldContext!.EmpoweredTransition!.CapturedTick); });
            await ConfirmAllCopies(game, copies);
            for (var i = 0; i <= copies; i++) await game.Top();
            Assert.All(Enumerable.Range(0, copies + 1), i => Assert.Contains("COPY-TARGET-" + i, game.State.PlayerZones["P2"].Banished));
            Assert.Equal(copies + 1, game.Events().Count(e => e.Event.Kind == "UNIT_BANISHED"));
            Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "UNIT_DESTROYED");
        });
    }

    [Fact]
    public async Task EarnedCopiedTextSurvivesActualEquipmentDetachment()
    {
        var initial = Put(CopyPosition(1), "DETACH", "SFD·011/221", "P1", "HAND");
        await Run("earned-copy-detached", initial, async game => {
            await CaptureCopies(game, 1); await ConfirmAllCopies(game, 1);
            await game.Act("P1", new PlayCardCommand("DETACH", "SFD·011/221", ["HOST", "GEAR"])); await game.Top();
            Assert.Null(game.State.CardObjects["GEAR"].AttachedToObjectId);
            Assert.Contains(game.State.StackItems, i => i.FieldContext?.Origin is not null);
            await game.Top(); await game.Top();
            Assert.All(new[] { "COPY-TARGET-0", "COPY-TARGET-1" }, id => Assert.Contains(id, game.State.PlayerZones["P2"].Banished));
        });
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task SurrenderFreezesAnUnconfirmedOrWardPendingSkillAndRejectsLateInput(bool ward)
    {
        await Run("terminal-" + ward, Position(ward ? "OGN·013/298" : "OGN·175/298"), async game => {
            await Empower(game); if (ward) await Choose(game, "TARGET");
            var pending = MatchStateHasher.HashValue(new { game.State.PendingCardChoice, game.State.PendingPayment, game.State.StackItems, game.State.TriggerQueue });
            var choice = game.State.PendingCardChoice; var payment = game.State.PendingPayment;
            await game.Act("P1", new SurrenderCommand()); Assert.Equal(MatchStatuses.Finished, game.State.Status);
            Assert.Equal(pending, MatchStateHasher.HashValue(new { game.State.PendingCardChoice, game.State.PendingPayment, game.State.StackItems, game.State.TriggerQueue }));
            Assert.DoesNotContain("TARGET", game.State.PlayerZones["P2"].Banished);
            await Reject(game, "P1", ward ? new PayCostCommand(payment!.PaymentId, payment.PaymentWindow, ["PAY"])
                : new ChooseCardsCommand(choice!.ChoiceId, choice.ChoiceWindow, ["TARGET"]));
        });
    }

    [Theory]
    [InlineData("null")] [InlineData("source")] [InlineData("generation")] [InlineData("controller")]
    [InlineData("negative-tick")] [InlineData("future-tick")] [InlineData("future-face")] [InlineData("null-card")]
    [InlineData("wrong-kind")] [InlineData("token")] [InlineData("ceased")]
    public async Task StructuralRecoveryRejectsMalformedEmpowerBirthFacts(string mutation)
    {
        await Run("malformed-birth-" + mutation, Position(), async game => {
            await Empower(game); var item = Assert.Single(game.State.StackItems); var c = item.FieldContext!; var birth = c.EmpoweredTransition!;
            c = mutation switch {
                "null" => c with { EmpoweredTransition = null },
                "source" => c with { EmpoweredTransition = birth with { Source = new("ENEMY", birth.Source.Generation) } },
                "generation" => c with { EmpoweredTransition = birth with { Source = birth.Source with { Generation = 99 } } },
                "controller" => c with { EmpoweredTransition = birth with { ControllerId = "P2" } },
                "negative-tick" => c with { EmpoweredTransition = birth with { CapturedTick = -1 } },
                "future-tick" => c with { EmpoweredTransition = birth with { CapturedTick = game.State.Tick + 1 } },
                "future-face" => c with { EmpoweredTransition = birth with { FaceRevision = 99 } },
                "null-card" => c with { CardNo = null! }, "wrong-kind" => c with { Kind = "SECOND_DRAW" },
                "token" => c with { SourceTokenFactoryCardNo = P6TokenFactoryCatalog.SandSoldierTokenCardNo },
                _ => c with { SourceCeasedToExist = true, EmpoweredTransition = birth with { CeasedSource = new(game.State.Tick, Mel) } } };
            var malformed = game.State with { StackItems = [item with { FieldContext = c }] };
            Assert.Contains(OfficialInsightAndSpellLockTests.Errors(malformed), error => error.Contains("field trigger", StringComparison.Ordinal));
        });
    }

    private static MatchState Position(string targetNo = "OGN·175/298")
    {
        var initial = Put(VenUnitEntryBasicTests.Position(Mel), "FODDER", "VEN·049", "P1", "HAND");
        initial = Put(initial, "TARGET", targetNo, "P2", "BATTLEFIELD", "OTHER");
        return Put(initial, "BASE-TARGET", "OGN·175/298", "P2", "BASE");
    }
    private static MatchState Put(MatchState state, string id, string no, string player, string zone, string? field = null)
    {
        var zones = state.PlayerZones[player];
        var printed = OfficialGarenConquestBattlefieldCountTests.Printed(id, no, player);
        if (zone is "BASE" or "BATTLEFIELD" && CardBehaviorRegistry.TryGetByCardNo(no, out var behavior) && behavior.PlaysSourceToBaseAsUnit)
            printed = printed with { Tags = printed.Tags.Concat(behavior.SourceUnitTags.Split('|', StringSplitOptions.RemoveEmptyEntries)).Distinct().Order(StringComparer.Ordinal).ToArray() };
        return state with {
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) { [id] = printed },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { [id] = new(player, zone, field) },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) { [player] = zone switch {
                "HAND" => zones with { Hand = [..zones.Hand, id] }, "BASE" => zones with { Base = [..zones.Base, id] },
                "BATTLEFIELD" => zones with { Battlefields = [..zones.Battlefields, id] }, _ => throw new InvalidOperationException("Unsupported fixture zone.") } } };
    }
    private static async Task Empower(Driver game)
    {
        await game.Act("P1", new PlayCardCommand("UNIT", Mel, []));
        await game.Act("P1", new ActivateAbilityCommand("UNIT", "EMPOWER:0:0", [], OptionalCosts: ["DISCARD_HAND_CARD:FODDER"]));
        await game.Top(); Assert.NotNull(game.State.PendingCardChoice);
    }
    private static Task Choose(Driver game, string target)
        => game.Act(game.State.PendingCardChoice!.PlayerId, new ChooseCardsCommand(game.State.PendingCardChoice.ChoiceId, game.State.PendingCardChoice.ChoiceWindow, [target]));
    private static MatchState CopyPosition(int copies)
    {
        var initial = Put(EmpowerTests.Position(Mel), "FODDER", "VEN·049", "P1", "HAND");
        for (var i = 2; i <= copies; i++) initial = AttachedRuleTextTests.AddSong(initial, "SONG" + i);
        for (var i = 0; i <= copies; i++) initial = Put(initial, "COPY-TARGET-" + i, "OGN·096/298", "P2", "BATTLEFIELD", "OTHER");
        return initial;
    }
    private static async Task CaptureCopies(Driver game, int copies)
    {
        await game.Act("P1", new PlayCardCommand("HOST", Mel, []));
        for (var i = 1; i <= copies; i++) { await game.Act("P1", new AssembleEquipmentCommand(i == 1 ? "GEAR" : "SONG" + i, "HOST", ["ASSEMBLE_GREEN"])); await game.Top(); }
        await game.Act("P1", new ActivateAbilityCommand("HOST", "EMPOWER:0:0", [], OptionalCosts: ["DISCARD_HAND_CARD:FODDER"])); await game.Top();
    }
    private static async Task ConfirmAllCopies(Driver game, int copies)
    {
        await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: game.State.TriggerQueue.Select(q => q.TriggerId).ToArray()));
        for (var i = 0; i <= copies; i++) await Choose(game, "COPY-TARGET-" + i);
    }
    private static int Might(MatchState state, string player, string id)
        => JsonSerializer.SerializeToElement(ResolutionResult.BuildSnapshots(state)[player], Json).GetProperty("players").GetProperty(player)
            .GetProperty("objects").GetProperty(id).GetProperty("effectivePower").GetInt32();
    private static async Task Reject(Driver game, string player, GameCommand command)
    {
        var raw = JsonSerializer.SerializeToNode(command, command.GetType(), Json)!.AsObject();
        raw["promptId"] = ResolutionResult.BuildPrompts(game.State)[player].PromptId; raw["snapshotTick"] = game.State.Tick;
        var retained = new ConformanceCommand(player, "empowered-rejected-" + game.Commands().Length, JsonSerializer.SerializeToElement(raw, Json));
        if (game.State.Status != MatchStatuses.Finished) { await game.ActRetained(retained, false); return; }
        var before = MatchStateHasher.Hash(game.State); var commandCount = game.Commands().Length; var eventCount = game.Events().Length;
        var snapshots = MatchStateHasher.HashValue(ResolutionResult.BuildSnapshots(game.State)); var prompts = MatchStateHasher.HashValue(ResolutionResult.BuildPrompts(game.State));
        var error = await Assert.ThrowsAsync<MatchSessionException>(() => game.ActRetained(retained, false));
        Assert.Equal(ErrorCodes.MatchFinished, error.Code); Assert.Equal(before, MatchStateHasher.Hash(game.State));
        Assert.Equal(commandCount, game.Commands().Length); Assert.Equal(eventCount, game.Events().Length);
        Assert.Equal(snapshots, MatchStateHasher.HashValue(ResolutionResult.BuildSnapshots(game.State)));
        Assert.Equal(prompts, MatchStateHasher.HashValue(ResolutionResult.BuildPrompts(game.State)));
    }
    private static async Task Run(string label, MatchState initial, Func<Driver, Task> body)
    {
        var game = new Driver(initial);
        try { await body(game); await game.Replay(); }
        finally {
            if (Environment.GetEnvironmentVariable("RIFTBOUND_VEN110_EVIDENCE") is { Length: > 0 } output) {
                Directory.CreateDirectory(output);
                await File.WriteAllTextAsync(Path.Combine(output, "boundary-" + label + ".json"), JsonSerializer.Serialize(new {
                    scope = "bounded empowerment banish semantics; explicit midgame; original accepted commands and events; no whole-card or backend certification",
                    initial, final = game.State, commands = game.Commands(), events = game.Events() }, Json));
            }
        }
    }
}
