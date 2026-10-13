using Riftbound.Contracts;
using Riftbound.Engine;
using Riftbound.CardCatalog;
using System.Text.Json;
using Xunit;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;

namespace Riftbound.ConformanceTests;

public sealed class AhriCombatMightTests
{
    internal static MatchState Board(string printing = "OGN·119/298", bool defending = false, bool copied = false)
    {
        var s = AttackDamageDistributionTests.Position(defending ? "OGN·001/298" : printing, copied);
        var id = defending ? "ENEMY1" : "HOST";
        return s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
            [id] = s.CardObjects[id] with { CardNo = printing, Power = 3 } } };
    }

    [Theory]
    [InlineData("OGN·119/298", false)] [InlineData("OGN·119a/298", false)]
    [InlineData("SFD·227/221", false)] [InlineData("SFD·227*/221", false)] [InlineData("VEN·SP3", false)]
    [InlineData("OGN·119/298", true)] [InlineData("OGN·119a/298", true)]
    [InlineData("SFD·227/221", true)] [InlineData("SFD·227*/221", true)] [InlineData("VEN·SP3", true)]
    public async Task EachCurrentPrintingConfirmsOneLocalEnemyWhenItsBattleRoleIsEstablished(string printing, bool defending)
    {
        var initial = Board(printing, defending); Restore(initial);
        var moved = await Act(initial, "P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F", []));
        var player = defending ? "P2" : "P1"; var source = defending ? "ENEMY1" : "HOST";
        Assert.True(defending ? moved.State.CardObjects[source].IsDefending : moved.State.CardObjects[source].IsAttacking);
        var choice = Assert.IsType<PendingCardChoiceState>(moved.State.PendingCardChoice);
        Assert.Equal("TRIGGER_CONFIRMATION", choice.ChoiceWindow); Assert.Equal(player, choice.PlayerId);
        Assert.Equal(1, choice.RequiredCount); Assert.Equal(1, choice.MaxCount);
        Assert.Equal(source, choice.SourceObjectId);
        Assert.Contains(defending ? "HOST" : "ENEMY1", choice.LegalObjectIds);
        Assert.DoesNotContain(source, choice.LegalObjectIds);
        Assert.DoesNotContain(CommandTypes.PassFocus, moved.Prompts[player].EnabledActions());
        Assert.All(moved.State.CardObjects.Values, c => Assert.Empty(c.UntilEndOfTurnPowerModifiers));
        Restore(moved.State); Assert.Empty(MinimumMightAuditTests.PublicErrors(moved.State));
    }

    internal static async Task<MatchState> Pending(MatchState initial, params string[] targets)
    {
        var moved = await Act(initial, "P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F", []));
        var s = moved.State;
        if (s.TriggerQueue.Count > 0) s = (await Act(s, "P1", new OrderTriggersCommand(OrderedTriggerIds: s.TriggerQueue.Select(t => t.TriggerId).ToArray()))).State;
        for (var i = 0; s.PendingCardChoice is { ChoiceWindow: "TRIGGER_CONFIRMATION" } choice; i++) {
            Assert.True(i < 4); var target = targets.Length > i ? targets[i] : targets[0];
            s = (await Act(s, choice.PlayerId, new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [target]))).State;
        }
        return s;
    }

    [Theory]
    [InlineData("OGN·119/298")] [InlineData("OGN·119a/298")]
    [InlineData("SFD·227/221")] [InlineData("SFD·227*/221")] [InlineData("VEN·SP3")]
    public async Task SelectedEnemyUsesCurrentMightAndAnIndependentTurnModifier(string printing)
    {
        var s = Board(printing);
        s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
            ["ENEMY1"] = s.CardObjects["ENEMY1"] with { Power = 2 } } };
        s = LocalDestructionRecallTests.At(DeathObserverAuditTests.Source(s, "OGS·013/024", "GAREN"), "GAREN", "P2", "BATTLEFIELD", "F");
        var pending = await Pending(s, "ENEMY1"); Restore(pending);
        Assert.Equal(2, pending.CardObjects["ENEMY1"].Power);
        var done = await Top(pending);
        Assert.Equal(0, done.State.CardObjects["ENEMY1"].Power);
        Assert.Equal(3, Assert.Single(done.State.CardObjects["ENEMY1"].UntilEndOfTurnPowerModifiers).SnapshotMight);
        Assert.Equal(1, CurrentPowerTargetTests.Snapshot(done.State).GetProperty("players").GetProperty("P2")
            .GetProperty("objects").GetProperty("ENEMY1").GetProperty("effectivePower").GetInt32());
        Assert.Equal(s.CardObjects["ENEMY2"].Power, done.State.CardObjects["ENEMY2"].Power);
        Assert.Equal(s.CardObjects["ENEMY3"].Power, done.State.CardObjects["ENEMY3"].Power);
        Assert.Equal("P1", done.State.FocusPlayerId); Restore(done.State); Assert.Empty(MinimumMightAuditTests.PublicErrors(done.State));
    }

    [Theory]
    [InlineData(0)] [InlineData(-2)]
    public async Task LowerMightIsNeitherRaisedNorDestroyedWithoutDamage(int power)
    {
        var s = Board(); s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
            ["ENEMY1"] = s.CardObjects["ENEMY1"] with { Power = power, Damage = 0 } } };
        var done = await Top(await Pending(s, "ENEMY1"));
        Assert.Equal(power, done.State.CardObjects["ENEMY1"].Power); Assert.Empty(done.State.CardObjects["ENEMY1"].UntilEndOfTurnPowerModifiers);
        Assert.Contains("ENEMY1", done.State.PlayerZones["P2"].Battlefields); Restore(done.State);
    }

    [Fact]
    public async Task CopiedPrintedTextCreatesTwoSeparatelyConfirmedModifiers()
    {
        var s = Board(copied: true); s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
            ["ENEMY1"] = s.CardObjects["ENEMY1"] with { Power = 6 } } };
        var pending = await Pending(s, "ENEMY1", "ENEMY1");
        Assert.Equal(2, pending.StackItems.Count); Assert.Single(pending.StackItems, i => i.FieldContext!.Origin is not null);
        Restore(pending); var first = await Top(pending); Assert.Equal(4, first.State.CardObjects["ENEMY1"].Power);
        var done = await Top(first.State); Assert.Equal(2, done.State.CardObjects["ENEMY1"].Power);
        Assert.Equal(2, done.State.CardObjects["ENEMY1"].UntilEndOfTurnPowerModifiers.Count); Restore(done.State);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task AReactionCanRemoveTheSourceOrInvalidateTheChosenTarget(bool targetLeaves)
    {
        var s = EquipChainTests.Response(Board(), "OGN·169/298", "P2");
        s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
            ["ENEMY1"] = s.CardObjects["ENEMY1"] with { Power = targetLeaves ? 3 : 4 } } };
        var pending = await Pending(s, "ENEMY1"); var passed = await Act(pending, "P1", new PassPriorityCommand());
        var response = await Act(passed.State, "P2", new PlayCardCommand("RESPONSE", "OGN·169/298", [targetLeaves ? "ENEMY1" : "HOST"]));
        var returned = await Top(response.State); Restore(returned.State);
        Assert.Contains(targetLeaves ? "ENEMY1" : "HOST", returned.State.PlayerZones[targetLeaves ? "P2" : "P1"].Hand);
        var done = await Top(returned.State);
        // OGN-001 is printed at 5 Might; entering hand starts a fresh object.
        Assert.Equal(targetLeaves ? 5 : 2, done.State.CardObjects["ENEMY1"].Power);
        Assert.Equal(targetLeaves, done.State.CardObjects["ENEMY1"].UntilEndOfTurnPowerModifiers.Count == 0);
        Restore(done.State); var errors = MinimumMightAuditTests.PublicErrors(done.State);
        Assert.True(errors.Count == 0, string.Join("; ", errors));
    }

    [Fact]
    public async Task ACounterCancelsTheModifierWithoutDestroyingItsSource()
    {
        var s = EquipChainTests.Response(Board(), "SFD·045/221", "P2");
        var pending = await Pending(s, "ENEMY1"); var original = pending.CardObjects["ENEMY1"].Power;
        var passed = await Act(pending, "P1", new PassPriorityCommand());
        var response = await Act(passed.State, "P2", new PlayCardCommand("RESPONSE", "SFD·045/221", [pending.StackItems.Single().StackItemId]));
        var done = await Top(response.State);
        Assert.Empty(done.State.StackItems); Assert.Equal(original, done.State.CardObjects["ENEMY1"].Power);
        Assert.Contains("HOST", done.State.PlayerZones["P1"].Battlefields); Assert.Contains(done.Events, e => e.Kind == "STACK_ITEM_COUNTERED");
        Restore(done.State);
    }

    [Fact]
    public async Task HereFollowsAStillExistingSourceMovedToItsBase()
    {
        var initial = AttackDamageDistributionTests.Spell(Board(), "FLASH", "OGS·011/024", "P1");
        Restore(initial); var pending = await Pending(initial, "ENEMY1");
        var response = await Act(pending, "P1", new PlayCardCommand("FLASH", "OGS·011/024", ["HOST"]));
        var moved = await Top(response.State);
        Assert.Equal("BASE", moved.State.ObjectLocations["HOST"].Zone);
        Assert.Equal(0, moved.State.CardObjects["HOST"].ObjectGeneration);
        Assert.Equal("BASE", Assert.Single(moved.State.StackItems).FieldContext!.SourceLocation!.Zone); Restore(moved.State);
        var done = await Top(moved.State);
        Assert.Equal(initial.CardObjects["ENEMY1"].Power, done.State.CardObjects["ENEMY1"].Power);
        Assert.Empty(done.State.CardObjects["ENEMY1"].UntilEndOfTurnPowerModifiers);
        Restore(done.State); Assert.Empty(MinimumMightAuditTests.PublicErrors(done.State));
    }

    [Theory]
    [InlineData("PAY")] [InlineData("DECLINE")]
    public async Task DeflectMustBePaidAfterChoosingAndBeforeResponses(string paymentChoice)
    {
        var initial = Board(); initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["ENEMY1"] = initial.CardObjects["ENEMY1"] with { Tags = [CardObjectTags.UnitCard, "法盾2"], Power = 4 } } };
        var moved = await Act(initial, "P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F", []));
        var choice = moved.State.PendingCardChoice!;
        var selected = await Act(moved.State, "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ENEMY1"]));
        var payment = selected.State.PendingPayment!; Assert.NotNull(payment); Assert.Equal(2, payment.PowerCost);
        Assert.Null(selected.State.PriorityPlayerId); Assert.Null(selected.State.PendingCardChoice); Restore(selected.State);
        var paid = await Act(selected.State, "P1", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, [paymentChoice]));
        Assert.Equal(initial.RunePools["P1"].TotalPower - (paymentChoice == "PAY" ? 2 : 0), paid.State.RunePools["P1"].TotalPower);
        if (paymentChoice == "PAY") {
            Assert.NotNull(paid.State.PriorityPlayerId); Assert.Single(paid.Events, e => e.Kind == "COST_PAID");
            var done = await Top(paid.State); Assert.Equal(2, done.State.CardObjects["ENEMY1"].Power); Restore(done.State);
        } else {
            Assert.Empty(paid.State.StackItems); Assert.Empty(paid.State.CardObjects["ENEMY1"].UntilEndOfTurnPowerModifiers);
            Assert.DoesNotContain(paid.Events, e => e.Kind == "COST_PAID"); Restore(paid.State);
        }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task CopiedTriggersKeepTheirCapturedTextAfterTheEquipmentLeaves(bool gearLeaves)
    {
        var initial = Board(copied: true);
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["ENEMY1"] = initial.CardObjects["ENEMY1"] with { Power = 6 } } };
        var pending = await Pending(initial, "ENEMY1", "ENEMY1");
        if (gearLeaves) pending = pending with {
            CardObjects = new Dictionary<string, CardObjectState>(pending.CardObjects) {
                ["GEAR"] = pending.CardObjects["GEAR"] with { AttachedToObjectId = null, AttachedRuleText = null, ObjectGeneration = 1, AttachmentRevision = 0 } },
            PlayerZones = new Dictionary<string, PlayerZones>(pending.PlayerZones) { ["P1"] = pending.PlayerZones["P1"] with {
                Base = pending.PlayerZones["P1"].Base.Where(id => id != "GEAR").ToArray(),
                Battlefields = pending.PlayerZones["P1"].Battlefields.Where(id => id != "GEAR").ToArray(), Graveyard = ["GEAR"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(pending.ObjectLocations) { ["GEAR"] = new("P1", "GRAVEYARD") } };
        Restore(pending); var first = await Top(pending); var done = await Top(first.State);
        Assert.Equal(2, done.State.CardObjects["ENEMY1"].Power); Restore(done.State);
    }

    [Theory]
    [InlineData("invented-source")] [InlineData("visible-other-source")] [InlineData("source-card")]
    public async Task AHistoricalModifierCannotAcquireForgedPublicProvenance(string change)
    {
        var initial = EquipChainTests.Response(Board(), "OGN·169/298", "P2");
        var pending = await Pending(initial, "ENEMY1");
        var passed = await Act(pending, "P1", new PassPriorityCommand());
        var response = await Act(passed.State, "P2", new PlayCardCommand("RESPONSE", "OGN·169/298", ["HOST"]));
        var returned = await Top(response.State); var done = await Top(returned.State); var state = done.State;
        Assert.Empty(MinimumMightAuditTests.PublicErrors(state));
        var snapshot = ResolutionResult.BuildSnapshots(state)["P2"];
        var serialized = JsonSerializer.SerializeToElement(snapshot, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.False(serialized.GetProperty("players").GetProperty("P1").GetProperty("objects").TryGetProperty("HOST", out _));
        var timing = new Dictionary<string, object?>(snapshot.Timing);
        var effects = Assert.IsAssignableFrom<IEnumerable<object?>>(timing["continuousEffects"]).ToArray();
        var index = Array.FindIndex(effects, e => e is Dictionary<string, object?> d && Equals(d.GetValueOrDefault("sourceObjectId"), "HOST"));
        Assert.True(index >= 0); var effect = new Dictionary<string, object?>(Assert.IsType<Dictionary<string, object?>>(effects[index]));
        if (change == "source-card") effect["sourceCardNo"] = "OGN·001/298";
        else effect["sourceObjectId"] = change == "visible-other-source" ? "RESPONSE" : "INVENTED-SOURCE";
        effects[index] = effect; timing["continuousEffects"] = effects;
        var views = new Dictionary<string, RecoveredPlayerView> { ["P2"] = new("P2", state.Tick, 0, snapshot with { Timing = timing }, null, null, null) };
        var errors = MatchRecoveryValidator.Validate(state.RoomId, 0, [], [], views, state, state.Tick);
        Assert.Contains(errors, e => e.Contains("source provenance", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("future-source")] [InlineData("different-card")] [InlineData("wrong-here")]
    [InlineData("non-field")] [InlineData("missing-here")]
    public async Task RecoveryRejectsForgedCombatTriggerProvenance(string change)
    {
        var s = await Pending(Board(), "ENEMY1"); var item = Assert.Single(s.StackItems); var context = item.FieldContext!;
        context = change switch {
            "future-source" => context with { SourceGeneration = 100 },
            "different-card" => context with { CardNo = "SFD·227/221" },
            "wrong-here" => context with { SourceLocation = new("P1", "BASE") },
            "non-field" => context with { SourceLocation = new("P1", "HAND") },
            "missing-here" => context with { SourceLocation = null },
            _ => context
        };
        var forged = s with { StackItems = [item with { FieldContext = context, CardNo = context.CardNo }] };
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(forged), e => e.Contains("field trigger", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PromotionKeepsEveryHistoricalRuntimeRecordAndItsExactSource()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Riftbound.slnx"))) root = root.Parent;
        Assert.NotNull(root);
        var historical = await OfficialCardCatalogLoader.LoadFromFileAsync(Path.Combine(root.FullName, "data/official/upstream/2026-10-11/active-catalog.zh-CN.json"));
        var current = await OfficialCardCatalog.LoadDefaultAsync();
        Assert.Equal(1009, historical.Cards.Count); Assert.Equal(1024, current.Cards.Count);
        foreach (var card in historical.Cards)
            Assert.Equal(JsonSerializer.Serialize(card), JsonSerializer.Serialize(current.Cards.Single(c => c.CardNo == card.CardNo)));
        Assert.Equal(15, current.Cards.Count(c => !historical.Cards.Any(old => old.CardNo == c.CardNo)));
        var added = Assert.Single(current.Cards, c => c.CardNo == "VEN·SP3");
        Assert.Equal("VEN·SP3", added.CardNo); Assert.Equal(37918, added.Id); Assert.Equal(3, added.Power);
        Assert.Equal(3, added.Energy); Assert.Equal(1, added.ReturnEnergy);
        Assert.DoesNotContain(current.Cards, c => c.CardNo == "VEN-SP3/006");
    }
}
