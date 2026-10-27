using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;
using Profile = Riftbound.ConformanceTests.LegendResourceBridgeVerifierTests.BridgeProfile;

namespace Riftbound.ConformanceTests;

// CN430.2-3: the legal resource child resolves without advancing the parent
// payment/choice. Its use restriction survives every kind of pending window.
public sealed class PendingLegendResourceTests
{
    public static IEnumerable<object[]> DariusWindows()
        => from row in LegendResourceBridgeVerifierTests.SuccessProfiles()
           where ((Profile)row[0]).AbilityId == LegendActionAbilityCatalog.DariusLegendAbilityId
           from window in new[] { "tempered", "effect-play", "instruction" }
           select new object[] { row[0], window };

    [Theory]
    [MemberData(nameof(LegendResourceBridgeVerifierTests.PowerSuccessProfiles), MemberType = typeof(LegendResourceBridgeVerifierTests))]
    public async Task ARealTemperedPaymentCanAcquireLegendPowerAndOnlyEquipmentPowerPays(Profile profile)
    {
        var initial = WithLegend(TemperedPlayTriggerAuditTests.PositionFor(), profile, new(3, 0));
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["HOST"] = Printed("HOST", "SFD·008/221"),
            ["GEAR"] = Printed("GEAR", "SFD·022/221") with { Tags = [CardObjectTags.EquipmentCard, CardEquipmentKeywordNames.Weapon] } } };
        var game = new Driver(initial); var probe = new Dictionary<string, object?>();
        try
        {
            await game.Act("P1", new PlayCardCommand("HOST", "SFD·008/221", []));
            var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
            await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["GEAR"]));
            await game.Top();
            var pending = Assert.IsType<PendingPaymentState>(game.State.PendingPayment);
            Assert.Equal("TEMPERED_EQUIPMENT_PAYMENT", pending.PaymentWindow);
            Assert.Equal(1, pending.PowerCostByTrait[RuneTrait.Red]);
            Assert.Null(game.State.CardObjects["GEAR"].AttachedToObjectId);
            await Bank(game, profile, probe);
            Assert.Equal(pending, game.State.PendingPayment);
            var pays = profile.AbilityId == LegendActionAbilityCatalog.OrnnLegendAbilityId;
            var pay = new PayCostCommand(pending.PaymentId, pending.PaymentWindow, ["PAY"]);
            if (pays) await game.Act("P1", pay);
            else
            {
                await Retained(game, "P1", pay, false);
                await game.Act("P1", new PayCostCommand(pending.PaymentId, pending.PaymentWindow, ["UNABLE_TO_PAY"]));
            }
            Assert.Null(game.State.PendingPayment); Assert.Empty(game.State.StackItems);
            Assert.Equal(pays ? "HOST" : null, game.State.CardObjects["GEAR"].AttachedToObjectId);
            Assert.Equal(pays ? RunePool.Empty : Granted(profile), game.State.RunePools["P1"]);
            if (pays)
            {
                var paid = Assert.Single(game.Events(), e => e.Event.Kind == "COST_PAID"
                    && Equals(e.Event.Payload.GetValueOrDefault("paymentWindow"), "TEMPERED_EQUIPMENT_PAYMENT")).Event;
                Assert.Equal("EQUIPMENT", paid.Payload["powerPurpose"]);
                Assert.Equal(1, paid.Payload["totalPowerCost"]);
            }
            await game.Replay();
        }
        finally { await Export("tempered", profile, initial, game, probe); }
    }

    [Theory]
    [MemberData(nameof(LegendResourceBridgeVerifierTests.PowerSuccessProfiles), MemberType = typeof(LegendResourceBridgeVerifierTests))]
    public async Task ARealBattlefieldPaymentCanBankRestrictedPowerButStillNeedsFourOrdinaryPower(Profile profile)
    {
        var initial = WithLegend(OfficialHoldSequenceTests.State("SFD·214/221"), profile, RunePool.Empty);
        var runes = new[] { "R1", "R2", "R3", "R4" };
        var cards = new Dictionary<string, CardObjectState>(initial.CardObjects);
        var locations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations);
        foreach (var id in runes) { cards[id] = Printed(id, "UNL-R01"); locations[id] = new("P1", "BASE"); }
        initial = initial with { CardObjects = cards, ObjectLocations = locations,
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
                ["P1"] = initial.PlayerZones["P1"] with { Base = runes, RuneDeck = [] } } };
        var game = new Driver(initial); var probe = new Dictionary<string, object?>();
        try
        {
            await game.Act("P1", new PassPriorityCommand());
            var pending = Assert.IsType<PendingPaymentState>(game.State.PendingPayment);
            Assert.Equal("TRIGGER_COST_CONFIRMATION", pending.PaymentWindow); Assert.Equal(4, pending.PowerCost);
            await Bank(game, profile, probe);
            var pay = new PayCostCommand(pending.PaymentId, pending.PaymentWindow, ["PAY"]);
            foreach (var id in runes.Take(3)) await game.Act("P1", new RecycleRuneCommand(id));
            Assert.Equal(3, game.State.RunePools["P1"].TotalPower);
            Assert.Equal(4, game.State.RunePools["P1"].TotalStoredPower);
            await Retained(game, "P1", pay, false);
            Assert.Equal(1, game.State.PlayerScores["P1"]);
            await game.Act("P1", new RecycleRuneCommand("R4"));
            await game.Act("P1", pay);
            Assert.Null(game.State.PendingPayment); Assert.Equal(Granted(profile), game.State.RunePools["P1"]);
            Assert.Equal(1, game.State.PlayerScores["P1"]);
            var paid = Assert.Single(game.Events(), e => e.Event.Kind == "COST_PAID").Event;
            Assert.Equal(4, paid.Payload["totalPowerCost"]);
            Assert.DoesNotContain("powerPurpose", paid.Payload.Keys);
            await game.Top(); Assert.Equal(2, game.State.PlayerScores["P1"]);
            Assert.Single(game.Events(), e => e.Event.Kind == "POWER_GAINED"
                && Equals(e.Event.Payload.GetValueOrDefault("sourceObjectId"), "LEGEND"));
            await game.Replay();
        }
        finally { await Export("battlefield", profile, initial, game, probe); }
    }

    [Theory]
    [MemberData(nameof(LegendResourceBridgeVerifierTests.PowerSuccessProfiles), MemberType = typeof(LegendResourceBridgeVerifierTests))]
    public async Task ARealPermanentTargetConfirmationKeepsItsChoiceWhileGainingResources(Profile profile)
    {
        var initial = WithLegend(PermanentPlayTargetTests.Position("OGN·063/298"), profile, new(2, 1));
        var game = new Driver(initial); var probe = new Dictionary<string, object?>();
        try
        {
            await game.Act("P1", new PlayCardCommand("UNIT", "OGN·063/298", []));
            var pending = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
            Assert.Equal("TRIGGER_CONFIRMATION", pending.ChoiceWindow);
            Assert.Contains("HOST", pending.LegalObjectIds);
            await Bank(game, profile, probe);
            Assert.Equal(pending, game.State.PendingCardChoice);
            await game.Act("P1", new ChooseCardsCommand(pending.ChoiceId, pending.ChoiceWindow, ["HOST"]));
            await game.Top(); Assert.Null(game.State.PendingCardChoice);
            Assert.Equal(Granted(profile), game.State.RunePools["P1"]);
            Assert.Contains(game.Events(), e => e.Event.Kind == "BOON_GRANTED"
                && Equals(e.Event.Payload.GetValueOrDefault("targetObjectId"), "HOST"));
            await game.Replay();
        }
        finally { await Export("target", profile, initial, game, probe); }
    }

    [Theory]
    [MemberData(nameof(LegendResourceBridgeVerifierTests.PowerSuccessProfiles), MemberType = typeof(LegendResourceBridgeVerifierTests))]
    public async Task ARealFizzRecastCanAcquireLegendPowerAndOnlySpellPowerPaysTheRetainedFee(Profile profile)
    {
        var initial = WithLegend(OfficialGraveyardRecastTests.Fizz("UNL-010/219"), profile, new(3, 1));
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["F"] = Printed("F", "SFD·140/221"), ["C"] = Printed("C", "UNL-010/219") } };
        var game = new Driver(initial); var probe = new Dictionary<string, object?>();
        try
        {
            await game.Act("P1", new PlayCardCommand("F", "SFD·140/221", [])); await game.Top();
            var pending = Assert.IsType<PendingEffectPlayState>(game.State.PendingEffectPlay);
            Assert.Contains("C", pending.Sources.Keys); Assert.True(pending.IgnoreBaseMana); Assert.False(pending.IgnoreBasePower);
            await Bank(game, profile, probe);
            Assert.Equal(pending, game.State.PendingEffectPlay);
            var pays = profile.AbilityId == LegendActionAbilityCatalog.KaisaLegendAbilityId;
            var play = new PlayCardCommand("C", "UNL-010/219", ["F"]);
            if (pays)
            {
                await game.Act("P1", play);
                Assert.Null(game.State.PendingEffectPlay); Assert.Equal(RunePool.Empty, game.State.RunePools["P1"]);
                var paid = game.Events().Last(e => e.Event.Kind == "COST_PAID").Event;
                Assert.Equal("PLAY_SPELL", paid.Payload["powerPurpose"]);
                Assert.Equal(0, paid.Payload["totalManaCost"]); Assert.Equal(1, paid.Payload["totalPowerCost"]);
                // The current Fizz errata's recycle timing belongs to a separate
                // consumer audit; this slice stops after the actual play fee.
            }
            else
            {
                await Retained(game, "P1", play, false);
                await game.Act("P1", new ChooseCardsCommand(pending.ChoiceId, "EFFECT_PLAY", []));
                Assert.Null(game.State.PendingEffectPlay); Assert.Equal(Granted(profile), game.State.RunePools["P1"]);
            }
            await game.Replay();
        }
        finally { await Export("effect-play", profile, initial, game, probe); }
    }

    [Theory]
    [MemberData(nameof(DariusWindows))]
    public async Task ActualEncourageEnablesDariusReactionResourceInsidePaymentAndResolution(Profile profile, string window)
    {
        var state = window == "tempered" ? TemperedPlayTriggerAuditTests.PositionFor()
            : window == "effect-play" ? OfficialGraveyardRecastTests.Fizz("UNL-010/219")
            : InstructionPowerPaymentTimingTests.Position();
        var initial = WithLegend(state, profile, window == "tempered" ? new(3, 0)
            : window == "effect-play" ? new(3, 1) : new(1, 0));
        if (window == "tempered") initial = initial with {
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["HOST"] = Printed("HOST", "SFD·008/221"),
                ["GEAR"] = Printed("GEAR", "SFD·022/221") with { Tags = [CardObjectTags.EquipmentCard, CardEquipmentKeywordNames.Weapon] } } };
        if (window == "effect-play") initial = initial with {
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["F"] = Printed("F", "SFD·140/221"), ["C"] = Printed("C", "UNL-010/219") } };
        var game = new Driver(initial); var probe = new Dictionary<string, object?>();
        try
        {
            if (window == "tempered")
            {
                await game.Act("P1", new PlayCardCommand("HOST", "SFD·008/221", []));
                var c = game.State.PendingCardChoice!;
                await game.Act("P1", new ChooseCardsCommand(c.ChoiceId, c.ChoiceWindow, ["GEAR"]));
            }
            else await game.Act("P1", new PlayCardCommand(window == "effect-play" ? "F" : "CARD",
                window == "effect-play" ? "SFD·140/221" : "OGN·268/298", window == "effect-play" ? [] : ["FIELD"]));
            await game.Top();
            Assert.Equal(1, game.State.PlayerCardsPlayedThisTurn["P1"]); Assert.Equal(RunePool.Empty, game.State.RunePools["P1"]);
            var before = game.State;
            var option = $"LEGEND_RESOURCE:LEGEND:{profile.AbilityId}";
            GameCommand command = window == "instruction"
                ? new PayCostCommand(before.PendingRuleChoice!.Request.Id, "RULE_REPLACEMENT", [option])
                : new LegendActCommand("LEGEND", profile.AbilityId, [], []);
            var core = await new CoreRuleEngine().ResolveAsync(before, new("darius-pending-core", "P1", command.CmdType), command, default);
            var offered = window == "instruction" ? before.PendingRuleChoice!.Request.Options.Any(o => o.Id == option)
                : (ResolutionResult.BuildPrompts(before)["P1"].Candidates ?? []).Any(c => c.Action == CommandTypes.LegendAct
                    && c.Enabled && (c.Sources ?? []).Any(s => s.Id == "LEGEND"));
            probe["beforeBank"] = before; probe["core"] = core; probe["offered"] = offered;
            await Retained(game, "P2", command, false);
            await Retained(game, "P1", command, true);
            Assert.True(core.Accepted, core.ErrorMessage); Assert.True(offered);
            Assert.Equal(new RunePool(1, 0), game.State.RunePools["P1"]);
            Assert.True(game.State.CardObjects["LEGEND"].IsExhausted);
            Assert.Equal(before.PriorityPlayerId, game.State.PriorityPlayerId); Assert.Equal(before.FocusPlayerId, game.State.FocusPlayerId);
            if (window == "instruction") command = new PayCostCommand(game.State.PendingRuleChoice!.Request.Id, "RULE_REPLACEMENT", [option]);
            await Retained(game, "P1", command, false);
            await game.Act("P1", new SurrenderCommand());
            Assert.Equal(MatchStatuses.Finished, game.State.Status); Assert.Equal(new RunePool(1, 0), game.State.RunePools["P1"]);
            Assert.Single(game.Events(), e => e.Event.Kind == "MANA_GAINED"
                && Equals(e.Event.Payload.GetValueOrDefault("sourceObjectId"), "LEGEND"));
            await game.Replay();
        }
        finally { await Export("darius-" + window, profile, initial, game, probe); }
    }

    [Fact]
    public async Task ARealPaymentDoesNotAdmitViktorsNonresourceLegendAction()
    {
        var profile = new Profile("Viktor", "OGN·265/298", "LEGEND", LegendActionAbilityCatalog.ViktorLegendAbilityId, "none", "payment-negative");
        var initial = WithLegend(TemperedPlayTriggerAuditTests.PositionFor(), profile, new(7, 0));
        var game = new Driver(initial); var probe = new Dictionary<string, object?>();
        try
        {
            await game.Act("P1", new PlayCardCommand("HOST", "SFD·008/221", []));
            var c = game.State.PendingCardChoice!;
            await game.Act("P1", new ChooseCardsCommand(c.ChoiceId, c.ChoiceWindow, ["GEAR"])); await game.Top();
            Assert.NotNull(game.State.PendingPayment);
            var command = new LegendActCommand("LEGEND", profile.AbilityId, [], ["SPEND_MANA:1"]);
            // The same source, resources and command are legal in an explicitly
            // open-main diagnostic control; the real pending window stays closed.
            var control = game.State with { PendingPayment = null, StackItems = [], PriorityPlayerId = null,
                FocusPlayerId = null, ActivePlayerId = "P1", Phase = MatchPhases.Main, TimingState = TimingStates.NeutralOpen };
            var allowed = await new CoreRuleEngine().ResolveAsync(control, new("viktor-open-control", "P1", command.CmdType), command, default);
            probe["diagnosticOpenMainControl"] = allowed;
            Assert.True(allowed.Accepted, allowed.ErrorMessage);
            Assert.DoesNotContain(ResolutionResult.BuildPrompts(game.State)["P1"].Candidates ?? [],
                candidate => candidate.Action == CommandTypes.LegendAct && candidate.Enabled);
            await Retained(game, "P1", command, false);
            Assert.False(game.State.CardObjects["LEGEND"].IsExhausted); await game.Replay();
        }
        finally { await Export("nonresource", profile, initial, game, probe); }
    }

    [Fact]
    public async Task ActualIgnoreAllCostsPlayKeepsItsResourceActionExclusion()
    {
        var profile = new Profile("Ornn", "SFD·189/221", "LEGEND", LegendActionAbilityCatalog.OrnnLegendAbilityId, "power", "ignore-all-negative");
        var initial = WithLegend(HookDeckPlayTests.Position("SFD·148/221"), profile, new(1, 1));
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["GEAR"] = Printed("GEAR", "OGN·242/298"), ["HOST"] = Printed("HOST", "SFD·148/221"),
            ["HOOK_TOP"] = Printed("HOOK_TOP", "OGN·096/298"), ["HOOK_OTHER"] = Printed("HOOK_OTHER", "SFD·042/221") } };
        var game = new Driver(initial); var probe = new Dictionary<string, object?>();
        try
        {
            var ability = Assert.Single(P4ActivatedAbilityCatalog.GetAll(), a => a.SourceCardNo == "OGN·242/298"
                && a.Kind == ActivatedAbilityKinds.DestroyFriendlyUnitLookTopPlayPowerPlusOneRecycleRest);
            await game.Act("P1", new ActivateAbilityCommand("GEAR", ability.AbilityId, ["HOST"])); await game.Top();
            var c = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
            await game.Act("P1", new ChooseCardsCommand(c.ChoiceId, c.ChoiceWindow, ["HOOK_TOP"]));
            Assert.True(game.State.PendingEffectPlay!.IgnoreAllCosts);
            Assert.DoesNotContain(CommandTypes.LegendAct, ResolutionResult.BuildPrompts(game.State)["P1"].Actions);
            await Retained(game, "P1", new LegendActCommand("LEGEND", profile.AbilityId, [], []), false);
            Assert.False(game.State.CardObjects["LEGEND"].IsExhausted); Assert.Equal(RunePool.Empty, game.State.RunePools["P1"]);
            await game.Act("P1", new PlayCardCommand("HOOK_TOP", "OGN·096/298", []));
            Assert.Null(game.State.PendingEffectPlay); Assert.Contains("HOOK_TOP", game.State.PlayerZones["P1"].Base);
            Assert.Equal(RunePool.Empty, game.State.RunePools["P1"]); await game.Replay();
        }
        finally { await Export("ignore-all", profile, initial, game, probe); }
    }

    private static MatchState WithLegend(MatchState state, Profile profile, RunePool pool)
        => OfficialHoldSequenceTests.AddLegend(state with { Tick = 0,
            RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = pool } }, profile.CardNo);

    private static CardObjectState Printed(string id, string no)
        => OfficialGarenConquestBattlefieldCountTests.Printed(id, no, "P1");

    private static RunePool Granted(Profile profile)
        => new(0, 0, spellOnlyPower: profile.AbilityId == LegendActionAbilityCatalog.KaisaLegendAbilityId ? 1 : 0,
            equipmentOnlyPower: profile.AbilityId == LegendActionAbilityCatalog.OrnnLegendAbilityId ? 1 : 0);

    private static async Task Bank(Driver game, Profile profile, Dictionary<string, object?> probe)
    {
        var command = new LegendActCommand("LEGEND", profile.AbilityId, [], []);
        await Retained(game, "P2", command, false);
        var before = game.State;
        var core = await new CoreRuleEngine().ResolveAsync(before, new("pending-legend-core", "P1", command.CmdType), command, default);
        var prompt = ResolutionResult.BuildPrompts(before)["P1"];
        var offered = (prompt.Candidates ?? []).Any(c => c.Action == CommandTypes.LegendAct && c.Enabled
            && (c.Sources ?? []).Any(s => s.Id == "LEGEND"));
        probe["beforeBank"] = before; probe["core"] = core; probe["offered"] = offered;
        await Retained(game, "P1", command, true);
        Assert.True(core.Accepted, core.ErrorMessage); Assert.True(offered);
        Assert.Equal(Granted(profile), game.State.RunePools["P1"]);
        Assert.Equal(before.PriorityPlayerId, game.State.PriorityPlayerId);
        Assert.Equal(before.FocusPlayerId, game.State.FocusPlayerId);
        Assert.True(game.State.CardObjects["LEGEND"].IsExhausted);
        Assert.DoesNotContain(ResolutionResult.BuildPrompts(game.State)["P1"].Candidates ?? [],
            c => c.Action == CommandTypes.LegendAct && c.Enabled && (c.Sources ?? []).Any(s => s.Id == "LEGEND"));
        await Retained(game, "P1", command, false);
    }

    private static Task Retained(Driver game, string player, GameCommand command, bool accepted)
    {
        var raw = JsonSerializer.SerializeToNode(command, command.GetType(), Web)!.AsObject();
        raw["promptId"] = ResolutionResult.BuildPrompts(game.State)[player].PromptId; raw["snapshotTick"] = game.State.Tick;
        return game.ActRetained(new(player, "pending-legend-" + game.Commands().Length, JsonSerializer.SerializeToElement(raw, Web)), accepted);
    }

    private static async Task Export(string kind, Profile profile, MatchState initial, Driver game, object probe)
    {
        if (Environment.GetEnvironmentVariable("RIFTBOUND_PENDING_LEGEND_EVIDENCE") is not { Length: > 0 } output) return;
        Directory.CreateDirectory(output);
        await File.WriteAllTextAsync(Path.Combine(output, kind + "-" + profile.CardNo.Replace('/', '_') + ".json"), JsonSerializer.Serialize(new {
            scope = "Exact CN reaction resource printings plus Viktor/ignore-all-cost negatives; actual parent play/trigger and resource/payment commands. Explicit turn-three/five positions, original journal, per-command privacy/recovery/dedup/replay; no full opening, L2/L3 or whole-card certification. Fizz ID37991/33230 and recycle errata stay separate from this fee slice.",
            initial, state = game.State, probe, commands = game.Commands(), events = game.Events()
        }, new JsonSerializerOptions(Web) { WriteIndented = true }));
    }

    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);
}
