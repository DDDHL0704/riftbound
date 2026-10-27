using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

// Explicit midgame wallets before an actual cast, not ordinary opening evidence.
public sealed class InstructionPowerAllocationTests
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);
    private const string Allocate = "SPEND_POWER:ALLOCATE";
    private const string Confirm = "CONFIRM_INSTRUCTION_POWER_ALLOCATION";
    private const string Cancel = "CANCEL_INSTRUCTION_POWER_ALLOCATION";

    [Theory]
    [InlineData(0, 0, 2)] [InlineData(0, 1, 1)] [InlineData(0, 2, 0)]
    [InlineData(1, 0, 1)] [InlineData(1, 1, 0)]
    public async Task EveryLegalTwoPowerVectorCanBeChosenAndPaidExactlyOnce(int wild, int blue, int red)
    {
        var run = await Open($"vector-{wild}-{blue}-{red}", Mixed());
        await ToConfirmation(run, 2, wild, blue, red);
        await run.Choice(Confirm);
        Assert.Equal(new RunePool(0, 1 - wild, Colors(2 - blue, 2 - red)), run.State.RunePools["P1"]);
        AssertPayment(run, 2, wild, Colors(blue, red), Allocate);
        await run.Finish();
    }

    [Theory]
    [InlineData("SPEND_POWER:0", 0, 1, 2, 2)]
    [InlineData("SPEND_POWER:1", 1, 0, 2, 2)]
    [InlineData("SPEND_POWER:2", 2, 0, 1, 2)]
    [InlineData("SPEND_POWER:3", 3, 0, 0, 2)]
    [InlineData("SPEND_POWER:4", 4, 0, 0, 1)]
    [InlineData("SPEND_POWER:5", 5, 0, 0, 0)]
    [InlineData("SPEND_POWER:blue:1", 1, 1, 1, 2)]
    [InlineData("SPEND_POWER:blue:2", 2, 1, 0, 2)]
    [InlineData("SPEND_POWER:red:1", 1, 1, 2, 1)]
    [InlineData("SPEND_POWER:red:2", 2, 1, 2, 0)]
    public async Task AllTenOriginalChoicesRetainTheirOriginalMeaning(string choice, int amount, int wild, int blue, int red)
    {
        var run = await Open("original-" + choice.Replace(':', '-'), Mixed());
        await run.Cold();
        await run.Choice(choice);
        Assert.Null(run.State.PendingRuleChoice);
        Assert.Equal(new RunePool(0, wild, Colors(blue, red)), run.State.RunePools["P1"]);
        AssertPayment(run, amount, choice.Count(c => c == ':') == 1 ? amount : 0,
            choice.StartsWith("SPEND_POWER:blue:", StringComparison.Ordinal) ? Colors(amount, 0)
                : choice.StartsWith("SPEND_POWER:red:", StringComparison.Ordinal) ? Colors(0, amount) : Colors(0, 0), choice);
        await run.Finish();
    }

    [Theory]
    [InlineData("zero")] [InlineData("all")] [InlineData("maximum")]
    public async Task ManualZeroFullAndMaximumWalletUseBoundedChoices(string boundary)
    {
        var maximum = boundary == "maximum";
        var run = await Open(boundary, maximum ? new(1, int.MaxValue - 2, Colors(1, 1)) : Mixed());
        var amount = boundary == "zero" ? 0 : maximum ? 2 : 5;
        var wild = boundary == "all" ? 1 : 0;
        var blue = boundary == "zero" ? 0 : maximum ? 1 : 2;
        var red = blue;
        var steps = await ToConfirmation(run, amount, wild, blue, red);
        Assert.InRange(steps, 0, 249);
        if (maximum) Assert.InRange(steps, 31, 35);
        await run.Choice(Confirm);
        Assert.Equal(new RunePool(0, maximum ? int.MaxValue - 2 : 1 - wild,
            Colors((maximum ? 1 : 2) - blue, (maximum ? 1 : 2) - red)), run.State.RunePools["P1"]);
        AssertPayment(run, amount, wild, Colors(blue, red), Allocate);
        await run.Finish();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task SingleBucketRetainsItsExistingCompletePaymentSurface(bool typed)
    {
        var run = await Open("single-" + typed, typed ? new(1, 0, Colors(2, 0)) : new(1, 2));
        Assert.DoesNotContain(run.State.PendingRuleChoice!.Request.Options, option => option.Id == Allocate);
        await run.Choice("SPEND_POWER:2");
        Assert.Equal(RunePool.Empty, run.State.RunePools["P1"]);
        AssertPayment(run, 2, 2, Colors(0, 0), "SPEND_POWER:2");
        await run.Finish();
    }

    [Theory]
    [InlineData("total")] [InlineData("trait")] [InlineData("confirmation")]
    public async Task CancellingAtEachStagePreservesEarlierRealRuneGainBeforeOriginalPayment(string stage)
    {
        var run = await Open("cancel-" + stage, new(1, 1, Colors(1, 0)), rune: true);
        await run.Choice("RECYCLE_RUNE:RUNE");
        var origin = MatchStateHasher.Hash(run.State.PendingRuleChoice!.Origin);
        var checkpoint = MatchStateHasher.HashValue(run.State.RunePools);
        var working = MatchStateHasher.Hash(run.State.PendingRuleChoice.Request.Preview!);
        Assert.DoesNotContain("RUNE", run.State.PlayerZones["P1"].Base);
        Assert.Contains("RUNE", run.State.PlayerZones["P1"].RuneDeck);
        Assert.Equal(new RunePool(0, 1, Colors(1, 1)), run.State.RunePools["P1"]);
        Assert.Single(run.Events(), entry => entry.Event.Kind == "RUNE_RECYCLED");
        Assert.Contains("RUNE", run.State.PendingRuleChoice.Request.Preview!.PlayerZones["P1"].RuneDeck);
        await run.Choice(Allocate);
        await run.Cold();
        if (stage == "trait")
            while (AllocationPurpose(run) == "INSTRUCTION_POWER_ALLOCATION:TOTAL")
            {
                await run.Choice(QuantityChoice(run, 2));
                await run.Cold();
            }
        if (stage == "confirmation") await ToConfirmation(run, 2, 0, 1, 1, enter: false);
        Assert.Contains(run.State.PendingRuleChoice!.Request.Options, option => option.Id == Cancel);
        var before = run.Entries.Count;
        var tick = run.State.Tick;
        await run.Choice(Cancel);
        run.Save("cancel-observation", new { stage, run.State, commands = run.Commands(), events = run.Events() });
        Assert.Equal(before + 1, run.Entries.Count);
        Assert.Equal(tick + 1, run.State.Tick);
        Assert.Single(run.Entries[^1].Events, ev => ev.Kind == "RULE_CHOICE_REQUESTED");
        Assert.Equal(origin, MatchStateHasher.Hash(run.State.PendingRuleChoice!.Origin));
        Assert.Equal(checkpoint, MatchStateHasher.HashValue(run.State.RunePools));
        Assert.Equal(working, MatchStateHasher.Hash(run.State.PendingRuleChoice.Request.Preview!));
        Assert.Contains(run.State.PendingRuleChoice.Request.Options, option => option.Id == Allocate);
        Assert.Single(run.Events(), entry => entry.Event.Kind == "RUNE_RECYCLED");
        Assert.DoesNotContain(run.Events(), entry => entry.Event.Kind == "DAMAGE_APPLIED");
        await run.Cold();
        var paymentStart = run.Entries.Count;
        await run.Choice("SPEND_POWER:2");
        Assert.DoesNotContain(run.Entries.Skip(paymentStart).SelectMany(entry => entry.Events), ev => ev.Kind == "RUNE_RECYCLED");
        Assert.Equal(new RunePool(0, 0, Colors(0, 1)), run.State.RunePools["P1"]);
        Assert.DoesNotContain("RUNE", run.State.PlayerZones["P1"].Base);
        Assert.Contains("RUNE", run.State.PlayerZones["P1"].RuneDeck);
        Assert.Single(run.Events(), entry => entry.Event.Kind == "RUNE_RECYCLED");
        AssertPayment(run, 2, 2, Colors(0, 0), "SPEND_POWER:2");
        await run.Finish();
    }

    [Theory]
    [InlineData("entry-forged")] [InlineData("cancel-before-manual")] [InlineData("wrong-player")]
    [InlineData("stale-request")] [InlineData("stale-prompt")] [InlineData("multiple")]
    [InlineData("negative-range")] [InlineData("over-range")] [InlineData("foreign-purpose")]
    [InlineData("early-confirmation")]
    public async Task InvalidSelectionsKeepStateAndExistingRejectionAuditCachePolicy(string invalid)
    {
        var run = await Open("invalid-" + invalid, Mixed());
        var oldRequest = run.State.PendingRuleChoice!.Request.Id;
        var oldWire = run.Wire(new PayCostCommand(oldRequest, "RULE_REPLACEMENT", ["SPEND_POWER:0"]));
        var entry = invalid is "entry-forged" or "cancel-before-manual";
        if (!entry) await run.Choice(Allocate);
        var request = run.State.PendingRuleChoice!.Request;
        var currentChoice = request.Options[0].Id;
        var choice = invalid switch {
            "entry-forged" => Allocate + ":FORGED",
            "cancel-before-manual" => Cancel,
            "negative-range" => ReplaceLastPart(currentChoice, "-1"),
            "over-range" => ReplaceLastPart(currentChoice, "2147483647"),
            "foreign-purpose" => currentChoice.Replace("%3ATOTAL", "%3AFOREIGN", StringComparison.Ordinal),
            "early-confirmation" => Confirm,
            _ => currentChoice
        };
        if (invalid == "foreign-purpose") Assert.NotEqual(currentChoice, choice);
        var command = new PayCostCommand(invalid == "stale-request" ? oldRequest : request.Id,
            "RULE_REPLACEMENT", invalid == "multiple" ? [currentChoice, Cancel] : [choice]);
        var actor = invalid == "wrong-player" ? "P2" : "P1";
        var before = run.Entries.Count;
        var state = MatchStateHasher.Hash(run.State);
        var snapshots = ViewHash(ResolutionResult.BuildSnapshots(run.State));
        var prompts = ViewHash(ResolutionResult.BuildPrompts(run.State));
        var rejected = invalid == "stale-prompt" ? await run.Repeat("P1", "stale-prompt-original", oldWire) : await run.Submit(command, actor);
        var input = run.LastInput!;
        run.Save("first-rejection", new { invalid, input, rejected });
        Assert.False(rejected.Accepted);
        Assert.Equal(invalid == "stale-prompt" ? ErrorCodes.PromptExpired : ErrorCodes.InvalidTarget, rejected.ErrorCode);
        Assert.Empty(rejected.Events);
        Assert.Equal(state, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(snapshots, ViewHash(rejected.Snapshots)); Assert.Equal(prompts, ViewHash(rejected.Prompts));
        Assert.Equal(before + 1, run.Entries.Count);
        Assert.False(run.Entries[^1].Accepted);
        Assert.Equal(run.Entries[^1].StartedTick, run.Entries[^1].CompletedTick);
        Assert.Equal(run.Entries[^1].StartedEventSequence, run.Entries[^1].CompletedEventSequence);
        var retry = await run.Repeat(input.Actor, input.Intent, input.Raw);
        Assert.False(retry.Accepted); Assert.Equal(rejected.ErrorCode, retry.ErrorCode); Assert.Equal(before + 1, run.Entries.Count);
        var changed = JsonSerializer.SerializeToNode(input.Raw, Web)!.AsObject();
        changed["paymentChoiceIds"] = JsonSerializer.SerializeToNode(new[] { currentChoice, "FORGED_CHANGED_RAW" }, Web);
        var conflict = await run.Repeat(input.Actor, input.Intent, JsonSerializer.SerializeToElement(changed));
        Assert.Equal(ErrorCodes.ClientIntentConflict, conflict.ErrorCode); Assert.Equal(before + 1, run.Entries.Count);
        await run.Cold();
        var cold = await run.Repeat(input.Actor, input.Intent, input.Raw);
        run.Save("cold-rejection-cache", new { originalCode = rejected.ErrorCode, actualColdCode = cold.ErrorCode, cold });
        Assert.Equal(rejected.ErrorCode, cold.ErrorCode);
        Assert.False(cold.Accepted); Assert.Empty(cold.Events); Assert.Equal(rejected.ErrorMessage, cold.ErrorMessage);
        Assert.Equal(before + 1, run.Entries.Count); Assert.Equal(state, MatchStateHasher.Hash(run.State));
        await ToConfirmation(run, 2, 0, 1, 1, enter: entry);
        var finalWire = run.Wire(new PayCostCommand(run.State.PendingRuleChoice!.Request.Id, "RULE_REPLACEMENT", [Confirm]));
        await run.Repeat("P1", "final-confirmation", finalWire);
        Assert.Equal(new RunePool(0, 1, Colors(1, 1)), run.State.RunePools["P1"]);
        await run.Cold();
        var finalCount = run.Entries.Count;
        var duplicate = await run.Repeat("P1", "final-confirmation", finalWire);
        Assert.True(duplicate.Accepted); Assert.Empty(duplicate.Events); Assert.Equal(finalCount, run.Entries.Count);
        Assert.Equal(new RunePool(0, 1, Colors(1, 1)), run.State.RunePools["P1"]);
        AssertPayment(run, 2, 0, Colors(1, 1), Allocate);
        await run.Finish();
    }

    [Fact]
    public async Task EmptyPoolRejectsPositiveOrManualPaymentAndStillAllowsZero()
    {
        var run = await Open("empty", new(1, 0));
        Assert.DoesNotContain(run.State.PendingRuleChoice!.Request.Options, option => option.Id == Allocate);
        foreach (var choice in new[] { "SPEND_POWER:1", Allocate })
        {
            var hash = MatchStateHasher.Hash(run.State);
            var rejected = await run.Submit(new PayCostCommand(run.State.PendingRuleChoice.Request.Id, "RULE_REPLACEMENT", [choice]));
            run.Save("empty-rejection", rejected);
            Assert.False(rejected.Accepted); Assert.Equal(ErrorCodes.InvalidTarget, rejected.ErrorCode); Assert.Empty(rejected.Events);
            Assert.Equal(hash, MatchStateHasher.Hash(run.State));
        }
        await run.Cold();
        await run.Choice("SPEND_POWER:0");
        Assert.Equal(RunePool.Empty, run.State.RunePools["P1"]);
        AssertPayment(run, 0, 0, Colors(0, 0), "SPEND_POWER:0");
        await run.Finish();
    }

    [Fact]
    public async Task EachEchoAllocatesAgainstItsOwnRemainingWorkingPool()
    {
        var run = await Open("echo", new(2, 1, Colors(2, 2)), echo: true);
        var checkpoint = MatchStateHasher.HashValue(run.State.RunePools);
        await ToConfirmation(run, 2, 0, 1, 1);
        await run.Choice(Confirm);
        Assert.Equal(new RunePool(0, 1, Colors(1, 1)), run.State.PendingRuleChoice!.Request.Preview!.RunePools["P1"]);
        Assert.Equal(checkpoint, MatchStateHasher.HashValue(run.State.RunePools));
        Assert.Equal(2, run.State.PendingRuleChoice.Request.Preview.CardObjects["TARGET"].Damage);
        Assert.DoesNotContain(run.Events(), entry => entry.Event.Kind == "COST_PAID"
            && Equals(entry.Event.Payload.GetValueOrDefault("paymentWindow"), "INSTRUCTION_POWER_PAYMENT"));
        await run.Cold();
        await ToConfirmation(run, 2, 0, 1, 1);
        await run.Choice(Confirm);
        Assert.Equal(new RunePool(0, 1), run.State.RunePools["P1"]);
        var receipts = run.Events().Where(entry => entry.Event.Kind == "COST_PAID"
            && Equals(entry.Event.Payload.GetValueOrDefault("paymentWindow"), "INSTRUCTION_POWER_PAYMENT")).ToArray();
        Assert.Equal(2, receipts.Length);
        Assert.All(receipts, entry => {
            var payload = JsonSerializer.SerializeToElement(entry.Event.Payload, Web);
            Assert.Equal(2, payload.GetProperty("power").GetInt32()); Assert.Equal(0, payload.GetProperty("genericPower").GetInt32());
            Assert.Equal(JsonSerializer.Serialize(Colors(1, 1), Web), payload.GetProperty("powerByTrait").GetRawText());
        });
        Assert.Equal(2, run.Events().Count(entry => entry.Event.Kind == "SPELL_EXECUTION_COMPLETED"));
        Assert.Equal(1, run.State.PlayerCardsPlayedThisTurn["P1"]);
        await run.Finish();
    }

    [Fact]
    public async Task InstructionAllocationDoesNotSpendTheParentsRemainingSpellOnlyMana()
    {
        var run = await Open("restricted", new(1, 1, Colors(2, 2), spellOnlyMana: 3));
        Assert.Equal(1, run.State.RunePools["P1"].Mana); Assert.Equal(2, run.State.RunePools["P1"].SpellOnlyMana);
        await ToConfirmation(run, 2, 0, 1, 1);
        await run.Choice(Confirm);
        Assert.Equal(new RunePool(1, 1, Colors(1, 1), spellOnlyMana: 2), run.State.RunePools["P1"]);
        AssertPayment(run, 2, 0, Colors(1, 1), Allocate);
        await run.Finish();
    }

    [Fact]
    public async Task AllSixTraitsAndWildcardCanParticipateInOneExactAllocation()
    {
        var available = new[] { RuneTrait.Red, RuneTrait.Green, RuneTrait.Blue, RuneTrait.Yellow, RuneTrait.Orange, RuneTrait.Purple }
            .Order(StringComparer.Ordinal).ToDictionary(trait => trait, _ => 2, StringComparer.Ordinal);
        var selected = available.ToDictionary(pair => pair.Key, _ => 1, StringComparer.Ordinal);
        var run = await Open("all-traits", new(1, 1, available));
        await ToConfirmation(run, 7, 1, 1, 1, allTyped: selected);
        await run.Choice(Confirm);
        Assert.Equal(new RunePool(0, 0, selected), run.State.RunePools["P1"]);
        AssertPayment(run, 7, 1, selected, Allocate);
        await run.Finish();
    }

    [Theory]
    [InlineData("option")] [InlineData("transcript")] [InlineData("coherent-wallet")]
    public async Task RecoveryIndependentlyRejectsChoiceForgeryAndCoordinatedWalletForgery(string tamper)
    {
        var run = await Open("tamper-" + tamper, Mixed());
        await ToConfirmation(run, 2, 0, 1, 1);
        if (tamper == "coherent-wallet") await run.Choice(Confirm);
        var commands = ViewHash(run.Commands()); var events = ViewHash(run.Events());
        var original = MatchStateHasher.Hash(run.State);
        MatchState forged;
        if (tamper == "coherent-wallet")
            forged = run.State with { RunePools = new Dictionary<string, RunePool>(run.State.RunePools) {
                ["P1"] = new(0, 0, Colors(2, 1)) } };
        else
        {
            var pending = run.State.PendingRuleChoice!;
            forged = run.State with { PendingRuleChoice = tamper == "option"
                ? pending with { Request = pending.Request with { Options = [..pending.Request.Options, new("FORGED_ALLOCATION", "伪造分配")] } }
                : pending with { Answers = [pending.Answers[0] with { RequestHash = "FORGED_REQUEST_HASH" }, ..pending.Answers.Skip(1)] } };
        }
        var frame = run.Frame(forged);
        run.Save("unvalidated-forged-frame", frame);
        var loaded = ValidateFrame(JsonSerializer.Deserialize<MatchRecoveryFrame>(JsonSerializer.Serialize(frame, Web), Web)!);
        var errors = loaded.ValidationErrors.ToArray();
        if (tamper == "coherent-wallet")
        {
            var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(run.Initial, loaded.Commands,
                loaded.AuthoritativeState!, new CoreRuleEngine(), default, loaded.Events);
            run.Save("coherent-wallet-rejection", new { frame = loaded, structuralErrors = errors, replay,
                originalCommandsHash = commands, originalEventsHash = events });
            Assert.Empty(errors);
            Assert.False(replay.IsMatch);
            Assert.Contains(replay.Errors, error => error.Contains("hash", StringComparison.OrdinalIgnoreCase));
        }
        else
        {
            Exception? observed = null;
            try { _ = MatchSession.Restore(loaded, new CoreRuleEngine(), NoopMatchJournal.Instance); }
            catch (Exception exception) { observed = exception; }
            run.Save("structural-choice-rejection", new { frame = loaded, structuralErrors = errors,
                actualType = observed?.GetType().FullName, actualCode = (observed as MatchSessionException)?.Code,
                actualError = observed?.ToString(), originalCommandsHash = commands, originalEventsHash = events });
            Assert.Contains(errors, error => error.Contains("replacement choice", StringComparison.OrdinalIgnoreCase));
            Assert.Equal(ErrorCodes.RecoveryInconsistent, Assert.IsType<MatchSessionException>(observed).Code);
        }
        Assert.Equal(original, MatchStateHasher.Hash(run.State));
        Assert.Equal(commands, ViewHash(run.Commands())); Assert.Equal(events, ViewHash(run.Events()));
        Assert.Equal(commands, ViewHash(loaded.Commands)); Assert.Equal(events, ViewHash(loaded.Events));
        Assert.Equal(run.Initial.RuleInputs, loaded.ReplayInitialState!.RuleInputs);
        if (tamper != "coherent-wallet") await run.Choice(Confirm);
        AssertPayment(run, 2, 0, Colors(1, 1), Allocate);
        await run.Finish();
    }

    private static RunePool Mixed() => new(1, 1, Colors(2, 2));
    private static Dictionary<string, int> Colors(int blue, int red)
        => new Dictionary<string, int> { ["blue"] = blue, ["red"] = red }
            .Where(pair => pair.Value > 0).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);

    private static async Task<Run> Open(string name, RunePool pool, bool echo = false, bool rune = false)
    {
        var card = OfficialGarenConquestBattlefieldCountTests.Printed("CARD", "OGN·268/298", "P1");
        var field = OfficialGarenConquestBattlefieldCountTests.Printed("FIELD", "OGN·280/298", "P2") with { ControllerId = "P2" };
        var target = OfficialGarenConquestBattlefieldCountTests.Printed("TARGET", "OGN·142/298", "P2");
        Assert.True(CardBehaviorRegistry.TryGetByCardNo(target.CardNo!, out var targetBehavior));
        target = target with { Tags = target.Tags.Concat(targetBehavior.SourceUnitTags.Split('|', StringSplitOptions.RemoveEmptyEntries))
            .Distinct(StringComparer.Ordinal).ToArray() };
        var initial = new MatchState("INSTRUCTION-ALLOCATION", 0, 3, "P1",
            new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" },
            status: MatchStatuses.InProgress, phase: MatchPhases.Main, timingState: TimingStates.NeutralOpen,
            runePools: new Dictionary<string, RunePool> { ["P1"] = pool, ["P2"] = RunePool.Empty },
            playerZones: new Dictionary<string, PlayerZones> { ["P1"] = PlayerZones.Empty with { Hand = ["CARD"] },
                ["P2"] = PlayerZones.Empty with { Battlefields = ["FIELD", "TARGET"] } },
            cardObjects: new Dictionary<string, CardObjectState> { ["CARD"] = card, ["FIELD"] = field, ["TARGET"] = target },
            objectLocations: new Dictionary<string, ObjectLocationState> { ["CARD"] = new("P1", "HAND"),
                ["FIELD"] = new("P2", "BATTLEFIELD", "FIELD"), ["TARGET"] = new("P2", "BATTLEFIELD", "FIELD") });
        if (echo) initial = initial with { UntilEndOfTurnEffects = EchoCostRules.AddBaseCostGrant([], "P1") };
        if (rune) initial = initial with {
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) { ["P1"] = initial.PlayerZones["P1"] with { Base = ["RUNE"] } },
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["RUNE"] = new("RUNE", cardNo: "OGN·007/298", ownerId: "P1", controllerId: "P1", tags: [CardObjectTags.RuneCard, "COLOR:red"]) },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) { ["RUNE"] = new("P1", "BASE") } };
        Assert.True(pool.HasSupportedAmounts);
        var run = new Run(name, initial);
        await run.Accept(new PlayCardCommand("CARD", "OGN·268/298", ["FIELD"], OptionalCosts: echo ? ["ECHO"] : null));
        for (var index = 0; index < 4 && run.State.PendingRuleChoice is null; index++)
            await run.Accept(new PassPriorityCommand(), run.State.PriorityPlayerId!);
        Assert.Equal("INSTRUCTION_POWER_PAYMENT", run.State.PendingRuleChoice!.Request.Kind);
        return run;
    }

    private static string? AllocationPurpose(Run run)
        => run.State.PendingRuleChoice!.Request.Options.FirstOrDefault(option => option.Id.StartsWith("QUANTITY:", StringComparison.Ordinal))
            is { } option ? Uri.UnescapeDataString(option.Id.Split(':')[4]) : null;

    private static string QuantityChoice(Run run, int value)
        => Assert.Single(run.State.PendingRuleChoice!.Request.Options, option => {
            if (!option.Id.StartsWith("QUANTITY:", StringComparison.Ordinal)) return false;
            var parts = option.Id.Split(':'); return int.Parse(parts[^2]) <= value && value <= int.Parse(parts[^1]);
        }).Id;

    private static string ReplaceLastPart(string id, string value) => id[..(id.LastIndexOf(':') + 1)] + value;
    private static string ViewHash(object value) => MatchStateHasher.HashValue(JsonSerializer.SerializeToElement(value, value.GetType(), Web));
    private static MatchRecoveryFrame ValidateFrame(MatchRecoveryFrame frame)
        => frame with { ValidationErrors = MatchRecoveryValidator.Validate(frame.RoomId, frame.LastEventSequence,
            frame.Commands, frame.Events, frame.PlayerViews, frame.AuthoritativeState, frame.CurrentTick, frame.SpectatorReplayFrame) };

    private static async Task<int> ToConfirmation(Run run, int amount, int wild, int blue, int red, bool enter = true,
        IReadOnlyDictionary<string, int>? allTyped = null)
    {
        var pending = run.State.PendingRuleChoice!;
        var origin = MatchStateHasher.Hash(pending.Origin);
        var checkpoint = MatchStateHasher.HashValue(run.State.RunePools);
        var preview = MatchStateHasher.Hash(pending.Request.Preview!);
        var first = run.Entries.Count;
        if (enter) await run.Choice(Allocate);
        await run.Cold();
        var steps = 0;
        while (run.State.PendingRuleChoice!.Request.Options.Any(option => option.Id.StartsWith("QUANTITY:", StringComparison.Ordinal)))
        {
            var request = run.State.PendingRuleChoice.Request;
            var purpose = Uri.UnescapeDataString(request.Options[0].Id.Split(':')[4]);
            var value = purpose switch {
                "INSTRUCTION_POWER_ALLOCATION:TOTAL" => amount,
                "INSTRUCTION_POWER_ALLOCATION:ANY" => wild,
                "INSTRUCTION_POWER_ALLOCATION:TRAIT:blue" => blue,
                "INSTRUCTION_POWER_ALLOCATION:TRAIT:red" => red,
                _ => purpose.StartsWith("INSTRUCTION_POWER_ALLOCATION:TRAIT:", StringComparison.Ordinal)
                    ? (allTyped ?? throw new InvalidOperationException("Unexpected allocation purpose: " + purpose))
                        .GetValueOrDefault(purpose["INSTRUCTION_POWER_ALLOCATION:TRAIT:".Length..])
                    : throw new InvalidOperationException("Unexpected allocation purpose: " + purpose)
            };
            var selected = Assert.Single(request.Options, option => {
                if (!option.Id.StartsWith("QUANTITY:", StringComparison.Ordinal)) return false;
                var parts = option.Id.Split(':');
                return int.Parse(parts[^2]) <= value && value <= int.Parse(parts[^1]);
            });
            await run.Choice(selected.Id);
            Assert.InRange(++steps, 1, 249);
            Assert.Equal(origin, MatchStateHasher.Hash(run.State.PendingRuleChoice!.Origin));
            Assert.Equal(checkpoint, MatchStateHasher.HashValue(run.State.RunePools));
            Assert.Equal(preview, MatchStateHasher.Hash(run.State.PendingRuleChoice.Request.Preview!));
            await run.Cold();
        }
        Assert.Contains(run.State.PendingRuleChoice!.Request.Options, option => option.Id == Confirm);
        Assert.Equal(amount, run.State.PendingRuleChoice.Request.PowerCost);
        Assert.All(run.Entries.Skip(first).SelectMany(entry => entry.Events), ev => Assert.Equal("RULE_CHOICE_REQUESTED", ev.Kind));
        return steps;
    }

    private static void AssertPayment(Run run, int amount, int generic, IReadOnlyDictionary<string, int> typed, string choice)
    {
        var ev = Assert.Single(run.Events(), entry => entry.Event.Kind == "COST_PAID"
            && Equals(entry.Event.Payload.GetValueOrDefault("paymentWindow"), "INSTRUCTION_POWER_PAYMENT")).Event;
        run.Save("payment-observation", new { ev, run.State, commands = run.Commands(), events = run.Events() });
        var payload = JsonSerializer.SerializeToElement(ev.Payload, Web);
        Assert.Equal(amount, payload.GetProperty("power").GetInt32());
        Assert.Equal(amount, payload.GetProperty("totalPowerCost").GetInt32());
        Assert.Equal(0, payload.GetProperty("mana").GetInt32());
        Assert.Equal(generic, payload.GetProperty("genericPower").GetInt32());
        Assert.Equal(typed.OrderBy(pair => pair.Key, StringComparer.Ordinal),
            payload.GetProperty("powerByTrait").Deserialize<Dictionary<string, int>>(Web)!.OrderBy(pair => pair.Key, StringComparer.Ordinal));
        Assert.Equal(new[] { choice }, payload.GetProperty("paymentChoiceIds").Deserialize<string[]>(Web));
        Assert.Equal("CARD", payload.GetProperty("sourceObjectId").GetString());
        var castSource = run.Entries.First(entry => entry.Accepted && entry.CommandType == CommandTypes.PlayCard)
            .AuthoritativeState.CardObjects["CARD"];
        Assert.Equal(castSource.ObjectGeneration, payload.GetProperty("sourceGeneration").GetInt64());
        Assert.False(string.IsNullOrEmpty(payload.GetProperty("stackItemId").GetString()));
        Assert.True(CardBehaviorRegistry.TryGetByCardNo("OGN·268/298", out var behavior));
        Assert.Equal(behavior.EffectKind, payload.GetProperty("reason").GetString());
        Assert.Equal(amount, run.Events().Where(entry => entry.Event.Kind == "DAMAGE_APPLIED"
            && Equals(entry.Event.Payload.GetValueOrDefault("targetObjectId"), "TARGET"))
            .Sum(entry => Convert.ToInt32(entry.Event.Payload["damage"])));
    }

    private sealed class Run(string name, MatchState initial) : IMatchJournal
    {
        private MatchSession session = null!;
        private int intents;
        private int artifacts;
        public MatchState Initial { get; } = initial;
        public MatchState State { get; private set; } = initial;
        public List<MatchJournalEntry> Entries { get; } = [];
        public Input? LastInput { get; private set; }
        public sealed record Input(string Actor, string Intent, JsonElement Raw);
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken token) { Entries.Add(entry); return ValueTask.CompletedTask; }
        public RecoveredCommand[] Commands() => Entries.Select(entry => new RecoveredCommand(entry.PlayerId, entry.ClientIntentId,
            entry.CommandType, entry.RawCommand, entry.StartedTick, entry.CompletedTick, entry.StartedEventSequence,
            entry.CompletedEventSequence, entry.Accepted, entry.ErrorMessage) { ErrorCode = entry.ErrorCode, RuleInputs = entry.RuleInputs }).ToArray();
        public RecoveredEvent[] Events() => Entries.SelectMany(entry => entry.Events.Select((ev, index) =>
            new RecoveredEvent(entry.StartedEventSequence + index + 1, entry.CompletedTick, index, ev))).ToArray();
        public JsonElement Wire(GameCommand command, string actor = "P1")
        {
            session ??= new(State, new CoreRuleEngine(), this);
            var raw = JsonSerializer.SerializeToNode(command, command.GetType(), Web)!.AsObject();
            raw["promptId"] = session.PromptFor(actor).PromptId; raw["snapshotTick"] = State.Tick;
            return JsonSerializer.SerializeToElement(raw);
        }
        public Task<ResolutionResult> Submit(GameCommand command, string? actor = null)
            => Repeat(actor ?? "P1", "instruction-allocation-" + intents++, Wire(command, actor ?? "P1"));
        public async Task<ResolutionResult> Repeat(string actor, string intent, JsonElement wire)
        {
            session ??= new(State, new CoreRuleEngine(), this);
            LastInput = new(actor, intent, wire);
            var result = await session.SubmitAsync(actor, intent, GameCommandJsonMapper.Map(wire), wire, default);
            if (result.Accepted) State = result.State;
            FullGameEndToEndTests.AssertNoHiddenZoneLeak(result);
            return result;
        }
        public async Task Accept(GameCommand command, string? actor = null)
        { var result = await Submit(command, actor); Assert.True(result.Accepted, result.ErrorMessage); }
        public Task Choice(string choice) => Accept(new PayCostCommand(State.PendingRuleChoice!.Request.Id, "RULE_REPLACEMENT", [choice]));
        public Task Cold()
        {
            var snapshots = ResolutionResult.BuildSnapshots(State); var prompts = ResolutionResult.BuildPrompts(State);
            var frame = Frame();
            var loaded = ValidateFrame(JsonSerializer.Deserialize<MatchRecoveryFrame>(JsonSerializer.Serialize(frame, Web), Web)!);
            Save("cold", loaded);
            Assert.True(loaded.IsConsistent, string.Join(";", loaded.ValidationErrors));
            session = MatchSession.Restore(loaded, new CoreRuleEngine(), this);
            foreach (var player in State.Seats.Keys)
            {
                Assert.Equal(ProtocolHash(snapshots[player]), ProtocolHash(session.SnapshotFor(player)));
                Assert.Equal(ProtocolHash(prompts[player]), ProtocolHash(session.PromptFor(player)));
            }
            State = loaded.AuthoritativeState!;
            return Task.CompletedTask;
        }
        public MatchRecoveryFrame Frame(MatchState? authority = null)
        {
            var state = authority ?? State;
            var snapshots = ResolutionResult.BuildSnapshots(state); var prompts = ResolutionResult.BuildPrompts(state);
            var sequence = Entries[^1].CompletedEventSequence;
            var views = state.Seats.Keys.ToDictionary(player => player, player => new RecoveredPlayerView(player,
                state.Tick, sequence, snapshots[player], state.Tick, sequence, prompts[player]));
            return new(state.RoomId, state.Tick, sequence, Commands(), Events(), views, [], state, Initial,
                MatchReplayRedactor.BuildSpectatorFrame(state.RoomId, state.Tick, sequence, [], state));
        }
        public async Task Finish()
        {
            Assert.Null(State.PendingRuleChoice);
            await Cold();
            var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(Initial, Commands(), State, new CoreRuleEngine(), default, Events());
            Save("final", new { Initial, State, commands = Commands(), events = Events(), replay });
            Assert.True(replay.IsMatch, string.Join(";", replay.Errors));
            Assert.Empty(OfficialInsightAndSpellLockTests.Errors(State));
        }
        public void Save(string stage, object value)
        {
            if (Environment.GetEnvironmentVariable("RIFTBOUND_POWER_ALLOCATION_EVIDENCE") is not { Length: > 0 } output) return;
            var directory = Path.Combine(output, name); Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, artifacts++ + "-" + stage + ".json"), JsonSerializer.Serialize(value, value.GetType(), Web));
        }
        private static string ProtocolHash(object value)
            => MatchStateHasher.HashValue(JsonSerializer.SerializeToElement(value, value.GetType(), Web));
    }
}
