using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.DestructionGraveyardFamilyTests;

namespace Riftbound.ConformanceTests;

public sealed class DestructionGraveyardRecoveryTests
{
    [Theory]
    [InlineData("SOURCE")]
    [InlineData("SOURCE_GENERATION")]
    [InlineData("CONTROLLER")]
    [InlineData("NULL_CONTROLLER")]
    [InlineData("PRINTING")]
    [InlineData("TURN")]
    [InlineData("FUTURE")]
    [InlineData("ZERO_TICK")]
    [InlineData("UNKNOWN_TARGET")]
    [InlineData("FUTURE_TARGET_GENERATION")]
    [InlineData("NEGATIVE_ORDINAL")]
    [InlineData("NO_CONTEXT")]
    public async Task DelayedStackRejectsForgedSourceRolePrintingTimeTargetAndMissingContext(string attack)
    {
        var run = await Delayed(); var state = run.State; var item = Assert.Single(state.StackItems); var c = item.DamageDestruction!;
        Assert.Empty(OfficialInsightAndSpellLockTests.Errors(state));
        var changed = attack switch {
            "SOURCE" => c with { Source = new("UNKNOWN", 0) },
            "SOURCE_GENERATION" => c with { Source = c.Source with { Generation = state.CardObjects[c.Source.ObjectId].ObjectGeneration + 1 } },
            "CONTROLLER" => c with { ControllerId = "P2" },
            "NULL_CONTROLLER" => c with { ControllerId = null! },
            "PRINTING" => c with { CardNo = Phoenix },
            "TURN" => c with { TurnNumber = state.TurnNumber - 1 },
            "FUTURE" => c with { CreatedAtTick = state.Tick + 1 },
            "ZERO_TICK" => c with { CreatedAtTick = 0 },
            "UNKNOWN_TARGET" => c with { DamagedUnit = new("UNKNOWN", 0) },
            "FUTURE_TARGET_GENERATION" => c with { DamagedUnit = new("TARGET", state.CardObjects["TARGET"].ObjectGeneration + 1) },
            "NEGATIVE_ORDINAL" => c with { DamageOrdinal = -1 },
            _ => null
        };
        Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(state with { StackItems = [item with { DamageDestruction = changed }] }));
    }

    [Theory]
    [InlineData("DUPLICATE")]
    [InlineData("UNKNOWN_MEMBER")]
    [InlineData("FALSE_GLOBAL_TARGET")]
    [InlineData("NULL_SOURCE")]
    [InlineData("BAD_BASE64")]
    public async Task DelayedInstructionMarkerRejectsDuplicatesUnknownSchemaAndForgedGlobalTarget(string attack)
    {
        var run = new StandbyDefendObserverTests.Driver(Position());
        await run.Act("P1", new PlayCardCommand("DECREE", Decree, [])); await run.Top();
        var state = run.State; var marker = Assert.Single(state.UntilEndOfTurnEffects, e => e.StartsWith("DAMAGE_DESTRUCTION|", StringComparison.Ordinal));
        var raw = JsonNode.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(marker.Split('|')[1])))!.AsObject();
        if (attack == "UNKNOWN_MEMBER") raw["untrustedRule"] = true;
        if (attack == "FALSE_GLOBAL_TARGET") raw["unit"] = new JsonObject { ["objectId"] = "TARGET", ["generation"] = 0 };
        if (attack == "NULL_SOURCE") raw["source"] = null;
        var forged = attack == "BAD_BASE64" ? "DAMAGE_DESTRUCTION|!" : "DAMAGE_DESTRUCTION|" + Convert.ToBase64String(Encoding.UTF8.GetBytes(raw.ToJsonString()));
        var effects = attack == "DUPLICATE" ? state.UntilEndOfTurnEffects.Append(marker).ToArray()
            : state.UntilEndOfTurnEffects.Select(e => e == marker ? forged : e).ToArray();
        Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(state with { UntilEndOfTurnEffects = effects }));
    }

    [Theory]
    [InlineData("NOT_SPELL")]
    [InlineData("UNIT_SOURCE_PRINTING")]
    [InlineData("UNKNOWN_SPELL")]
    [InlineData("SPELL_CONTROLLER")]
    [InlineData("RESPONSIBLE")]
    [InlineData("SOURCE_GENERATION")]
    [InlineData("VICTIM_GENERATION")]
    [InlineData("NO_CONTEXT")]
    [InlineData("NULL_CONTROLLER")]
    public async Task PhoenixContextRejectsForgedAttributionVictimSourceAndRole(string attack)
    {
        var run = await PhoenixCost(); var state = run.State; var item = Assert.Single(state.StackItems); var c = item.GraveyardPlay!;
        var changed = attack switch {
            "NOT_SPELL" => c with { Attribution = c.Attribution with { FromSpell = false } },
            "UNIT_SOURCE_PRINTING" => c with { Attribution = c.Attribution with { CardNo = Phoenix } },
            "UNKNOWN_SPELL" => c with { Attribution = c.Attribution with { Source = new("UNKNOWN", 0) } },
            "SPELL_CONTROLLER" => c with { Attribution = c.Attribution with { ControllerId = "P2" } },
            "RESPONSIBLE" => c with { Attribution = c.Attribution with { ResponsiblePlayerId = "P2" } },
            "SOURCE_GENERATION" => c with { Source = c.Source with { Generation = c.Source.Generation + 1 } },
            "VICTIM_GENERATION" => c with { Destroyed = c.Destroyed with { Generation = state.CardObjects["PHOENIX"].ObjectGeneration } },
            "NULL_CONTROLLER" => c with { ControllerId = null! },
            _ => null
        };
        Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(state with { StackItems = [item with { GraveyardPlay = changed }] }));
    }

    [Theory]
    [InlineData("MANA")]
    [InlineData("POWER")]
    [InlineData("TRAIT")]
    [InlineData("ACTOR")]
    [InlineData("STACK")]
    [InlineData("CHOICES")]
    public async Task LeadingPaymentCannotBeForgedToWaiveManaRedPowerOrConfirmation(string attack)
    {
        var run = await PhoenixCost(); var state = run.State; var cost = state.PendingPayment!;
        var forged = attack switch {
            "MANA" => cost with { ManaCost = 0 }, "POWER" => cost with { PowerCost = 0 },
            "TRAIT" => cost with { PowerCostByTrait = new Dictionary<string, int> { ["blue"] = 1 } },
            "ACTOR" => cost with { PlayerId = "P2" }, "STACK" => cost with { ResolvingStackItemId = "another-stack" },
            _ => cost with { LegalPaymentChoiceIds = ["PAY"] }
        };
        Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(state with { PendingPayment = forged }));
    }

    [Fact]
    public async Task InsufficientRedAndWrongOrStaleChoiceCannotMutateOriginalPendingPayment()
    {
        var run = await PhoenixCost(); var state = run.State; var p = state.PendingPayment!;
        var poor = state with { RunePools = state.RunePools.ToDictionary(x => x.Key, x => x.Key == "P1"
            ? new RunePool(10, 0, new Dictionary<string, int> { ["blue"] = 10 }) : x.Value) };
        foreach (var (actor, cmd, basis) in new[] {
            ("P2", new PayCostCommand(p.PaymentId, p.PaymentWindow, ["PAY"]), state),
            ("P1", new PayCostCommand("stale", p.PaymentWindow, ["PAY"]), state),
            ("P1", new PayCostCommand(p.PaymentId, p.PaymentWindow, ["PAY", "DECLINE"]), state),
            ("P1", new PayCostCommand(p.PaymentId, p.PaymentWindow, ["PAY"]), poor) })
        {
            var result = await new CoreRuleEngine().ResolveAsync(basis, new("invalid-phoenix-cost", actor, cmd.CmdType), cmd, default);
            Assert.False(result.Accepted); Assert.Empty(result.Events); Assert.Equal(MatchStateHasher.Hash(basis), MatchStateHasher.Hash(result.State));
        }
        await PayPhoenix(run, false); Assert.Contains("PHOENIX", run.State.PlayerZones["P1"].Graveyard);
    }

    private static async Task<StandbyDefendObserverTests.Driver> Delayed()
    {
        var run = new StandbyDefendObserverTests.Driver(Position());
        await run.Act("P1", new PlayCardCommand("DECREE", Decree, [])); await run.Top();
        await run.Act("P1", new PlayCardCommand("RAY", Ray, ["TARGET"])); await run.Top(); return run;
    }
    private static async Task<StandbyDefendObserverTests.Driver> PhoenixCost()
    {
        var run = new StandbyDefendObserverTests.Driver(Position("BATTLEFIELD"));
        await run.Act("P1", new PlayCardCommand("RAY", Ray, ["PHOENIX"])); await run.Top(); return run;
    }
}
