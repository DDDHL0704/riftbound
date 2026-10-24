using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed partial class ImposedPowerAllocationTests
{
    private const string Cancel = "CANCEL_IMPOSED_MOVEMENT_POWER";

    [Theory]
    [InlineData(false, 0, 0, 2)] [InlineData(false, 0, 1, 1)] [InlineData(false, 0, 2, 0)]
    [InlineData(false, 1, 0, 1)] [InlineData(false, 1, 1, 0)]
    [InlineData(true, 0, 0, 2)] [InlineData(true, 0, 1, 1)] [InlineData(true, 0, 2, 0)]
    [InlineData(true, 1, 0, 1)] [InlineData(true, 1, 1, 0)]
    public async Task EveryMixedTwoPowerVectorPaysTheFixedTwoPatrolFeeExactlyOnce(bool effect, int wild, int blue, int red)
    {
        var run = await OpenWallet(effect, $"mixed-{effect}-{wild}-{blue}-{red}", new(0, 1, Colors(2, 2)), 2);
        await run.Cold("mixed-parent");
        await ToConfirmation(run, 2, wild, Colors(blue, red));
        await run.Cold("mixed-confirmation");
        await run.Choice(Confirm);
        Assert.Equal(new RunePool(0, 1 - wild, Colors(2 - blue, 2 - red)), run.State.RunePools["P1"]);
        AssertPayment(run, effect, 2, wild, Colors(blue, red));
        AssertMoved(run, effect, true);
        await run.Cold("mixed-final");
    }

    [Theory]
    [InlineData("any")] [InlineData("blue")] [InlineData("green")] [InlineData("orange")]
    [InlineData("purple")] [InlineData("red")] [InlineData("yellow")]
    public async Task EveryTraitAndWildcardCanPayWithoutConsumingAnyOtherBucket(string bucket)
    {
        var colors = new[] { "blue", "green", "orange", "purple", "red", "yellow" }.ToDictionary(trait => trait, _ => 1);
        var run = await OpenWallet(false, "all-traits-" + bucket, new(0, 1, colors));
        var paid = colors.Where(pair => pair.Key == bucket).ToDictionary(pair => pair.Key, pair => pair.Value);
        await ToConfirmation(run, 1, bucket == "any" ? 1 : 0, paid);
        await run.Choice(Confirm);
        Assert.Equal(bucket == "any" ? 0 : 1, run.State.RunePools["P1"].Power);
        foreach (var trait in colors.Keys) Assert.Equal(trait == bucket ? 0 : 1, run.State.RunePools["P1"].PowerByTrait.GetValueOrDefault(trait));
        AssertPayment(run, false, 1, bucket == "any" ? 1 : 0, paid);
        await run.Cold("all-traits-final");
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task SupportedMaximumWalletAndSixtyFiveRealPatrolsUseBoundedMenus(bool effect)
    {
        // Explicit supported midgame boundary, with 65 actual independent text instances.
        // The effect cast first spends one green power from its supported initial wallet.
        var any = int.MaxValue - 130 - (effect ? 1 : 0);
        var run = await OpenWallet(effect, "bounded-" + effect, new(0, any, Colors(65, 65)), 65);
        await run.Cold("bounded-parent");
        var decisions = await ToConfirmation(run, 65, 0, Colors(32, 33));
        Assert.InRange(decisions, 12, 14);
        await run.Cold("bounded-confirmation");
        await run.Choice(Confirm);
        Assert.Equal(new RunePool(0, any, Colors(33, 32)), run.State.RunePools["P1"]);
        AssertPayment(run, effect, 65, 0, Colors(32, 33));
        await run.Cold("bounded-final");
    }

    public static IEnumerable<object[]> CancellationPaths()
        => from effect in new[] { false, true }
           from stage in new[] { "wildcard", "trait", "confirmation" }
           from finish in new[] { "PAY", "manual", "DECLINE" }
           select new object[] { effect, stage, finish };

    [Theory]
    [MemberData(nameof(CancellationPaths))]
    public async Task CancellationPreservesEarlierInWindowRuneGainForAnyFinalDecision(bool effect, string stage, string finish)
    {
        var run = await OpenWallet(effect, $"cancel-{effect}-{stage}-{finish}", new(0, 1, Colors(1, 0)));
        await run.Choice("RECYCLE_RUNE:RED");
        var pending = run.State.PendingRuleChoice!;
        var origin = MatchStateHasher.Hash(pending.Origin);
        var authoritative = ViewHash(run.State.RunePools);
        var preview = MatchStateHasher.Hash(pending.Request.Preview!);
        Assert.Equal(new RunePool(0, 1, Colors(1, 1)), run.Pool());
        await run.Choice(Allocate);
        if (stage == "trait") await run.Choice(QuantityChoice(run, 0));
        if (stage == "confirmation") await ToConfirmation(run, 1, 0, Colors(0, 1), enter: false);
        Assert.Contains(run.State.PendingRuleChoice!.Request.Options, option => option.Id == Cancel);
        await run.Cold("before-cancel");
        var count = run.Entries.Count; var tick = run.State.Tick;
        await run.Choice(Cancel);
        Assert.Equal(count + 1, run.Entries.Count); Assert.Equal(tick + 1, run.State.Tick);
        Assert.Single(run.Entries[^1].Events, ev => ev.Kind == "RULE_CHOICE_REQUESTED");
        Assert.Equal(origin, MatchStateHasher.Hash(run.State.PendingRuleChoice!.Origin));
        Assert.Equal(authoritative, ViewHash(run.State.RunePools));
        Assert.Equal(preview, MatchStateHasher.Hash(run.State.PendingRuleChoice.Request.Preview!));
        Assert.Contains("RED", run.State.PlayerZones["P1"].Base);
        Assert.Contains("RED", run.State.PendingRuleChoice.Request.Preview!.PlayerZones["P1"].RuneDeck);
        Assert.DoesNotContain(run.Entries.SelectMany(entry => entry.Events), ev => ev.Kind == "RUNE_RECYCLED" || IsImposedCost(ev));
        await run.Cold("after-cancel");
        if (finish == "manual")
        {
            await ToConfirmation(run, 1, 0, Colors(0, 1));
            await run.Choice(Confirm);
        }
        else await run.Choice(finish);
        Assert.Single(run.Entries.SelectMany(entry => entry.Events), ev => ev.Kind == "RUNE_RECYCLED");
        Assert.Contains("RED", run.State.PlayerZones["P1"].RuneDeck);
        Assert.Equal(finish == "DECLINE" ? new RunePool(0, 1, Colors(1, 1))
            : finish == "manual" ? new RunePool(0, 1, Colors(1, 0)) : new RunePool(0, 0, Colors(1, 1)), run.State.RunePools["P1"]);
        if (finish == "DECLINE") Assert.DoesNotContain(run.Entries.SelectMany(entry => entry.Events), IsImposedCost);
        else AssertPayment(run, effect, 1, finish == "manual" ? 0 : 1, finish == "manual" ? Colors(0, 1) : Colors(0, 0));
        AssertMoved(run, effect, finish != "DECLINE");
        await run.Cold("cancel-final");
    }

    [Theory]
    [InlineData("entry-forged")] [InlineData("cancel-before-manual")] [InlineData("wrong-player")]
    [InlineData("stale-request")] [InlineData("stale-prompt")] [InlineData("multiple")]
    [InlineData("negative-range")] [InlineData("over-range")] [InlineData("foreign-purpose")]
    [InlineData("foreign-cost")] [InlineData("foreign-anchor")] [InlineData("foreign-generation")]
    [InlineData("early-confirmation")]
    public async Task InvalidOrStaleSelectionsKeepStateAndTheExistingAuditAndRetryPolicy(string invalid)
    {
        var run = await OpenWallet(false, "invalid-" + invalid, new(0, 1, Colors(2, 2)), 2);
        var old = run.State.PendingRuleChoice!.Request.Id;
        var oldWire = run.Wire(new PayCostCommand(old, "RULE_REPLACEMENT", ["PAY"]));
        var entry = invalid is "entry-forged" or "cancel-before-manual";
        if (!entry) await run.Choice(Allocate);
        var request = run.State.PendingRuleChoice!.Request;
        var current = request.Options[0].Id;
        var parts = current.Split(':');
        var choice = invalid switch {
            "entry-forged" => Allocate + ":FORGED", "cancel-before-manual" => Cancel,
            "negative-range" => string.Join(':', parts[..^1].Append("-1")),
            "over-range" => string.Join(':', parts[..^1].Append("2147483647")),
            "foreign-purpose" => current.Replace("%3AANY", "%3AFOREIGN", StringComparison.Ordinal),
            "foreign-cost" => current.Replace("%3A2%3A", "%3A3%3A", StringComparison.Ordinal),
            "foreign-anchor" => "QUANTITY:B:" + string.Join(':', parts[2..]),
            "foreign-generation" => string.Join(':', parts.Take(2).Append("999").Concat(parts[3..])),
            "early-confirmation" => Confirm, _ => current
        };
        if (invalid.StartsWith("foreign-", StringComparison.Ordinal)) Assert.NotEqual(current, choice);
        var command = new PayCostCommand(invalid == "stale-request" ? old : request.Id, "RULE_REPLACEMENT",
            invalid == "multiple" ? [current, Cancel] : [choice]);
        var actor = invalid == "wrong-player" ? "P2" : "P1";
        var count = run.Entries.Count; var state = MatchStateHasher.Hash(run.State);
        var snapshots = ViewHash(ResolutionResult.BuildSnapshots(run.State)); var prompts = ViewHash(ResolutionResult.BuildPrompts(run.State));
        var rejected = invalid == "stale-prompt" ? await run.Repeat("P1", "old-prompt", oldWire) : await run.Submit(command, actor);
        var input = run.LastInput!;
        run.Save("first-rejection", new { invalid, input, rejected });
        Assert.False(rejected.Accepted); Assert.Empty(rejected.Events);
        Assert.Equal(invalid == "stale-prompt" ? ErrorCodes.PromptExpired : ErrorCodes.InvalidTarget, rejected.ErrorCode);
        Assert.Equal(state, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(snapshots, ViewHash(rejected.Snapshots)); Assert.Equal(prompts, ViewHash(rejected.Prompts));
        Assert.Equal(count + 1, run.Entries.Count); Assert.False(run.Entries[^1].Accepted);
        Assert.Equal(run.Entries[^1].StartedTick, run.Entries[^1].CompletedTick);
        Assert.Equal(run.Entries[^1].StartedEventSequence, run.Entries[^1].CompletedEventSequence);
        var retry = await run.Repeat(input.Actor, input.Intent, input.Raw);
        Assert.False(retry.Accepted); Assert.Equal(rejected.ErrorCode, retry.ErrorCode); Assert.Equal(count + 1, run.Entries.Count);
        var changed = JsonSerializer.SerializeToNode(input.Raw, Web)!.AsObject();
        changed["paymentChoiceIds"] = JsonSerializer.SerializeToNode(new[] { current, "CHANGED_RAW" }, Web);
        var conflict = await run.Repeat(input.Actor, input.Intent, JsonSerializer.SerializeToElement(changed));
        Assert.Equal(ErrorCodes.ClientIntentConflict, conflict.ErrorCode); Assert.Equal(count + 1, run.Entries.Count);
        await run.Cold("rejected-checkpoint");
        var cold = await run.Repeat(input.Actor, input.Intent, input.Raw);
        run.Save("cold-rejection", new { originalCode = rejected.ErrorCode, cold });
        // The pre-existing cold rejection ErrorCode gap remains a separate issue.
        Assert.False(cold.Accepted); Assert.Empty(cold.Events); Assert.Equal(rejected.ErrorMessage, cold.ErrorMessage);
        Assert.Equal(count + 1, run.Entries.Count); Assert.Equal(state, MatchStateHasher.Hash(run.State));
        await ToConfirmation(run, 2, 0, Colors(1, 1), enter: entry);
        await run.Choice(Confirm);
        AssertPayment(run, false, 2, 0, Colors(1, 1));
        Assert.Equal(new RunePool(0, 1, Colors(1, 1)), run.State.RunePools["P1"]);
        await run.Cold("invalid-final");
    }

    [Theory]
    [InlineData("option")] [InlineData("answer")] [InlineData("coherent-wallet")]
    public async Task RecoveryChecksIndependentlyRejectForgedAllocationOrCoordinatedFinalWallet(string tamper)
    {
        var run = await OpenWallet(false, "tamper-" + tamper, new(0, 1, Colors(2, 2)), 2);
        await ToConfirmation(run, 2, 0, Colors(1, 1));
        if (tamper == "coherent-wallet") await run.Choice(Confirm);
        var original = MatchStateHasher.Hash(run.State); var frame = run.Frame();
        var commands = ViewHash(frame.Commands); var events = ViewHash(frame.Events);
        var pending = run.State.PendingRuleChoice;
        var forged = tamper == "coherent-wallet" ? run.State with {
            RunePools = new Dictionary<string, RunePool>(run.State.RunePools) { ["P1"] = new(0, 0, Colors(2, 1)) } }
            : run.State with { PendingRuleChoice = tamper == "option"
                ? pending! with { Request = pending.Request with { Options = [..pending.Request.Options, new("FORGED_ALLOCATION", "伪造分配")] } }
                : pending! with { Answers = [pending.Answers[0] with { RequestHash = "FORGED_REQUEST_HASH" }, ..pending.Answers.Skip(1)] } };
        var loaded = DecodeAndValidate(run.Frame(forged));
        run.Save("forged-frame", loaded);
        if (tamper == "coherent-wallet")
        {
            Assert.Empty(loaded.ValidationErrors);
            var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(run.Initial, loaded.Commands, forged, new CoreRuleEngine(), default, loaded.Events);
            run.Save("forged-wallet-replay", replay); Assert.False(replay.IsMatch);
            Assert.Contains(replay.Errors, error => error.Contains("hash", StringComparison.OrdinalIgnoreCase));
        }
        else
        {
            Assert.Contains(loaded.ValidationErrors, error => error.Contains("replacement choice", StringComparison.OrdinalIgnoreCase));
            var rejected = Assert.Throws<MatchSessionException>(() => MatchSession.Restore(loaded, new CoreRuleEngine(), run));
            Assert.Equal(ErrorCodes.RecoveryInconsistent, rejected.Code);
        }
        Assert.Equal(original, MatchStateHasher.Hash(run.State));
        Assert.Equal(commands, ViewHash(run.Frame().Commands)); Assert.Equal(events, ViewHash(run.Frame().Events));
        Assert.Equal(commands, ViewHash(loaded.Commands)); Assert.Equal(events, ViewHash(loaded.Events));
        Assert.Equal(run.Initial.RuleInputs, loaded.ReplayInitialState!.RuleInputs);
        if (tamper != "coherent-wallet") await run.Choice(Confirm);
        AssertPayment(run, false, 2, 0, Colors(1, 1));
        await run.Cold("tamper-original-final");
    }

    private static Dictionary<string, int> Colors(int blue, int red)
        => new Dictionary<string, int> { ["blue"] = blue, ["red"] = red }
            .Where(pair => pair.Value > 0).ToDictionary(pair => pair.Key, pair => pair.Value);

    private static async Task<Run> OpenWallet(bool effect, string name, RunePool wallet, int patrols = 1)
    {
        var initial = Position(name);
        var cards = new Dictionary<string, CardObjectState>(initial.CardObjects);
        var locations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations);
        var zones = new Dictionary<string, PlayerZones>(initial.PlayerZones);
        for (var i = 2; i <= patrols; i++)
        {
            var id = "PATROL" + i; cards[id] = Printed(id, "UNL-163/219", "P2"); locations[id] = new("P2", "BATTLEFIELD", "FIELD2");
            zones["P2"] = zones["P2"] with { Battlefields = [..zones["P2"].Battlefields, id] };
        }
        cards["OPPONENT_SECRET"] = Printed("OPPONENT_SECRET", "OGN·001/298", "P2");
        zones["P2"] = zones["P2"] with { Hand = ["OPPONENT_SECRET"] };
        locations["OPPONENT_SECRET"] = new("P2", "HAND");
        var power = new Dictionary<string, int>(wallet.PowerByTrait);
        if (effect) power["green"] = power.GetValueOrDefault("green") + 1;
        initial = initial with { CardObjects = cards, ObjectLocations = locations, PlayerZones = zones,
            RunePools = new Dictionary<string, RunePool>(initial.RunePools) { ["P1"] = new(wallet.Mana + (effect ? 4 : 0), wallet.Power, power) } };
        var run = new Run(initial);
        if (effect)
        {
            await run.Act("cast", "P1", new PlayCardCommand("SPELL", "UNL-054/219", ["D", "D2"], Destination: "BATTLEFIELD:FIELD2"));
            await run.Act("pass-1", run.State.PriorityPlayerId!, new PassPriorityCommand());
            await run.Act("pass-2", run.State.PriorityPlayerId!, new PassPriorityCommand());
        }
        else await run.Act("move", "P1", new MoveUnitCommand("A", "BASE", "BATTLEFIELD:FIELD2", SourceObjectIds: ["A", "B"]));
        Assert.Equal(wallet, run.Pool()); Assert.Equal("P1", run.State.PendingRuleChoice!.Request.PlayerId);
        return run;
    }

    private static async Task<int> ToConfirmation(Run run, int amount, int wildcard, IReadOnlyDictionary<string, int> typed, bool enter = true)
    {
        if (enter) await run.Choice(Allocate);
        var pool = ViewHash(run.Pool()); var authority = ViewHash(run.State.RunePools);
        var preview = MatchStateHasher.Hash(run.State.PendingRuleChoice!.Request.Preview!);
        var decisions = 0;
        while (run.State.PendingRuleChoice!.Request.Options.Any(option => option.Id.StartsWith("QUANTITY:", StringComparison.Ordinal)))
        {
            Assert.InRange(decisions++, 0, 216);
            var request = run.State.PendingRuleChoice.Request;
            Assert.Equal(3, request.Options.Count); Assert.Equal(amount, request.PowerCost);
            Assert.DoesNotContain(ResolutionResult.BuildPrompts(run.State)["P2"].Candidates!, candidate => candidate.Action == CommandTypes.PayCost);
            var quantities = request.Options.Where(option => option.Id.StartsWith("QUANTITY:", StringComparison.Ordinal)).ToArray();
            Assert.Equal(2, quantities.Length);
            foreach (var quantity in quantities)
            {
                var anchor = Uri.UnescapeDataString(quantity.Id.Split(':')[1]);
                Assert.Contains(anchor, new[] { "A", "D" }); Assert.Equal(new[] { anchor }, quantity.ObjectIds);
                Assert.DoesNotContain("SPELL", quantity.Id, StringComparison.Ordinal);
                Assert.DoesNotContain("OPPONENT_SECRET", quantity.Id, StringComparison.Ordinal);
            }
            var purpose = Uri.UnescapeDataString(quantities[0].Id.Split(':')[4]);
            var prefix = "IMPOSED_MOVEMENT_POWER_ALLOCATION:" + amount + ":";
            Assert.StartsWith(prefix, purpose);
            var bucket = purpose[prefix.Length..];
            var desired = bucket == "ANY" ? wildcard : typed.GetValueOrDefault(bucket[6..]);
            await run.Choice(QuantityChoice(run, desired));
            Assert.Equal(pool, ViewHash(run.Pool())); Assert.Equal(authority, ViewHash(run.State.RunePools));
            Assert.Equal(preview, MatchStateHasher.Hash(run.State.PendingRuleChoice!.Request.Preview!));
            Assert.DoesNotContain(run.Entries.SelectMany(entry => entry.Events), IsImposedCost);
        }
        Assert.Contains(run.State.PendingRuleChoice.Request.Options, option => option.Id == Confirm);
        Assert.Equal(2, run.State.PendingRuleChoice.Request.Options.Count);
        return decisions;
    }

    private static string QuantityChoice(Run run, int desired)
        => Assert.Single(run.State.PendingRuleChoice!.Request.Options, option => {
            if (!option.Id.StartsWith("QUANTITY:", StringComparison.Ordinal)) return false;
            var parts = option.Id.Split(':'); return int.Parse(parts[^2]) <= desired && desired <= int.Parse(parts[^1]);
        }).Id;

    private static void AssertPayment(Run run, bool effect, int amount, int wildcard, IReadOnlyDictionary<string, int> typed)
    {
        var cost = Assert.Single(run.Entries.SelectMany(entry => entry.Events), IsImposedCost);
        run.Save("payment", new { cost, run.State });
        var payload = JsonSerializer.SerializeToElement(cost.Payload, Web);
        Assert.Equal(amount, payload.GetProperty("totalPowerCost").GetInt32()); Assert.Equal(amount, payload.GetProperty("powerCost").GetInt32());
        Assert.Equal(wildcard, payload.GetProperty("genericPower").GetInt32()); Assert.Equal(0, payload.GetProperty("manaCost").GetInt32());
        Assert.Equal(typed.OrderBy(pair => pair.Key), payload.GetProperty("powerByTrait").Deserialize<Dictionary<string, int>>(Web)!.OrderBy(pair => pair.Key));
        Assert.Equal("P1", payload.GetProperty("playerId").GetString()); Assert.Equal(effect ? "SPELL" : "A", payload.GetProperty("sourceObjectId").GetString());
        Assert.Equal("FIELD2", payload.GetProperty("battlefieldObjectId").GetString());
        Assert.Equal(effect ? new[] { "D", "D2" } : ["A", "B"], payload.GetProperty("sourceObjectIds").Deserialize<string[]>(Web));
        Assert.Equal(RunePool.Empty, run.State.RunePools["P2"]);
    }

    private static void AssertMoved(Run run, bool effect, bool moved)
    {
        foreach (var id in effect ? new[] { "D", "D2" } : ["A", "B"])
        {
            Assert.Equal(moved ? "FIELD2" : effect ? "FIELD1" : null, run.State.ObjectLocations[id].BattlefieldObjectId);
            Assert.Equal(!effect && moved, run.State.CardObjects[id].IsExhausted);
        }
    }

    private static string ViewHash(object value) => MatchStateHasher.HashValue(JsonSerializer.SerializeToElement(value, value.GetType(), Web));

    private static MatchRecoveryFrame DecodeAndValidate(MatchRecoveryFrame frame)
    {
        var loaded = JsonSerializer.Deserialize<MatchRecoveryFrame>(JsonSerializer.Serialize(frame, Web), Web)!;
        var errors = MatchRecoveryValidator.Validate(loaded.RoomId, loaded.LastEventSequence, loaded.Commands, loaded.Events,
            loaded.PlayerViews, loaded.AuthoritativeState, loaded.CurrentTick, loaded.SpectatorReplayFrame);
        return loaded with { ValidationErrors = errors };
    }
}
