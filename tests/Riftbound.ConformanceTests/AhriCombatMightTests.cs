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
        Assert.Equal(targetLeaves ? 5 : 4, done.State.CardObjects["ENEMY1"].Power);
        Assert.Empty(done.State.CardObjects["ENEMY1"].UntilEndOfTurnPowerModifiers);
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
        var applied = await Top(pending);
        Assert.Single(applied.State.CardObjects["ENEMY1"].UntilEndOfTurnPowerModifiers);
        var offered = await Act(applied.State, applied.State.FocusPlayerId!, new PassFocusCommand());
        var passed = offered.State.FocusPlayerId == "P2" ? offered
            : await Act(offered.State, offered.State.FocusPlayerId!, new PassFocusCommand());
        var response = await Act(passed.State, "P2", new PlayCardCommand("RESPONSE", "OGN·169/298", ["HOST"]));
        var returned = await Top(response.State); var state = returned.State;
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
        Assert.Equal(1009, historical.Cards.Count); Assert.Equal(1083, current.Cards.Count);
        // Only these exact printings adopt the reviewed applicable errata.
        // Keep the archived source and every other historical field unchanged.
        var errata = new Dictionary<string,(string Before,string After)> {
            // DOC-12 p3 / CN33075: only this printing gains the explicit turn duration.
            ["SFD·003/221"] = ("{{迅捷}}（可在你的回合或法术对决中打出。）\n{{回响1}}（你可以选择支付此额外费用，以重复此法术效果。）\n让一名单位获得{{强攻2}}。（如果它是进攻方，则{{S}}+2。）",
                "{{迅捷}}（可在你的回合或法术对决中打出。）\n{{回响1}}（你可以选择支付此额外费用，以重复此法术效果。）\n让一名单位本回合内获得{{强攻2}}。（如果它是进攻方，则{{S}}+2。）"),
            // DOC-16-P012-B006/B007: exact current and old Void Gate text.
            ["OGN·296/298"] = ("以此处的单位作为目标的法术或技能，造成的伤害+1（每段伤害都+1）。",
                "法术和技能对此处的单位造成的伤害+1（每段伤害都+1）。"),
            // DOC-10-P002-B005 through B008 explicitly distinguish current and old text.
            ["UNL-139/219"] = ("{{待命}}（支付{{A}}正面朝下放置此牌，之后可支付{{0}}将其当作反应牌打出。）\n选择一处战场。让一名对手展示自己的手牌。你可以从中选择一名单位。让该对手将该单位打出到该战场，无视一切费用。当对手如此做时，{{眩晕}}该单位。（使其在本回合内无法造成战斗伤害。）",
                "{{待命}}（支付{{A}}正面朝下放置此牌，之后可支付{{0}}将其当作反应牌打出。）\n\n选择一处战场。让一名对手展示自己的手牌。你可以从中选择一名单位。该对手将该单位打出到该战场，无视一切费用。若其如此做，则进行一次：{{眩晕}}该单位。（使其在本回合内无法造成战斗伤害。）"),
            ["UNL-081/219"] = ("{{待命}}（支付{{A}}正面朝下放置此牌，之后可支付{{0}}将其当作反应牌打出。）\n{{瞬息}}（在我控制者的开始阶段开始时，结算得分之前将我摧毁。）\n当你打出我时，在此处打出两名“映像”。它们变为我的复制体。",
                "{{待命}}（支付{{A}}正面朝下放置此牌，之后可支付{{0}}将其当作反应牌打出。）\n{{瞬息}}（在我控制者的下个开始阶段开始时，结算得分之前将我摧毁。）\n当你打出我时，在此处打出两名“映像”。然后进行一次：它们变为我的复制体。"),
            ["OGN·025/298"] = ("{{迅捷}}（可在你的回合或法术对决中打出。）\n每名对手展示其主牌堆顶部的一张牌。你从中选择一张，并当作自己的牌打出，无视费用，然后回收其余的卡牌。",
                "{{迅捷}}（可在你的回合或法术对决中打出。）\n每名对手展示其主牌堆顶部的一张牌。你从中选择一张，将其放逐，然后当作自己的牌打出，无视费用。然后回收其余的卡牌。"),
            ["OGN·115/298"] = ("每名玩家可以查看主牌堆顶部的五张牌，从中选择一张，然后回收其余的卡牌。从下一名玩家开始，每名玩家打出这些卡牌，并无视其法力费用（仍需支付所有符能费用）。",
                "每名玩家查看其主牌堆顶部的五张牌，放逐其中一张，然后回收其余的卡牌。从下一名玩家开始，每名玩家打出这些被放逐的卡牌，无视其法力费用。（仍需支付所有符能费用。）"),
            ["OGN·062/298"] = ("查看主牌堆顶部的五张牌，你可以从中选择一名单位，将它的法力费用减少{{5}}后打出，然后回收其余卡牌。",
                "查看你主牌堆顶部的五张牌。你可以选择从中放逐一名单位，然后将其打出，其费用减少{{5}}。回收其余的卡牌。"),
            ["SFD·188/221"] = ("展示你主牌堆顶部的两张牌。你可以选择其中一张，将其费用减少{{2}}后打出。抽取其中未以此方式打出的卡牌。",
                "展示你主牌堆顶部的两张牌。你可以选择放逐其中一张，然后将其打出，其费用减少{{2}}。抽取其中未被放逐的卡牌。"),
            ["SFD·187/221"] = ("当你征服一处战场时，你可以选择让我变为休眠状态，以此展示你主牌堆顶部的两张牌。你可以选择打出其中一张牌，然后回收其余的卡牌。",
                "当你征服一处战场时，你可以选择让我变为休眠状态，以此展示你主牌堆顶部的两张牌。你可以选择放逐其中一张，然后将其打出。回收其余的卡牌。"),
            ["SFD·243/221"] = ("当你征服一处战场时，你可以选择让我变为休眠状态，以此展示你主牌堆顶部的两张牌。你可以选择打出其中一张牌，然后回收其余的卡牌。",
                "当你征服一处战场时，你可以选择让我变为休眠状态，以此展示你主牌堆顶部的两张牌。你可以选择放逐其中一张，然后将其打出。回收其余的卡牌。"),
            ["SFD·170/221"] = ("当我进攻时，你可以选择展示你主牌堆顶部的两张牌。你可以选择打出其中一张牌，然后回收其余的卡牌。如果以此方式打出的卡牌为单位，则可以选择将其打出到此处。",
                "当我进攻时，你可以选择展示你主牌堆顶部的两张牌。你可以选择放逐其中一张，然后将其打出。如果打出的卡牌为单位，则可以选择将其打出到此处。回收其余的卡牌。"),
            ["SFD·170a/221"] = ("当我进攻时，你可以选择展示你主牌堆顶部的两张牌。你可以选择打出其中一张牌，然后回收其余的卡牌。如果以此方式打出的卡牌为单位，则可以选择将其打出到此处。",
                "当我进攻时，你可以选择展示你主牌堆顶部的两张牌。你可以选择放逐其中一张，然后将其打出。如果打出的卡牌为单位，则可以选择将其打出到此处。回收其余的卡牌。") };
        errata["OGN·242/298"] = ("支付{{1}}和{{黄色}}，{{横置}}：摧毁一名友方单位，然后查看主牌堆顶部的五张牌。你可以选择从中打出一名战力比被摧毁单位最多高1点的单位卡牌，无视费用，然后回收其余的卡牌。",
            "支付{{1}}和{{黄色}}，{{横置}}：摧毁一名友方单位。查看你主牌堆顶部的五张牌。你可以选择从中放逐一名战力比被摧毁单位最多高 1 点的单位卡牌，然后将其打出，无视费用。然后回收其余的卡牌。");
        errata["OGN·160/298"] = ("在你的回合即将结束时，从主牌堆顶部开始逐一展示卡牌，直到翻出一名单位为止，然后将其打出，无视费用，并回收其余的卡牌。", "在你的回合结束时，从主牌堆顶部开始逐一展示卡牌，直到翻出一名单位为止，将其放逐，然后将其打出，无视费用，并回收其余的卡牌。");
        errata["OGN·073/298"] = ("在你的回合结束时，如果我位于战场上，则让四枚友方符文变为活跃状态。", "在你的回合结束时，如果我位于战场上，则让最多四枚友方符文变为活跃状态。");
        errata["OGS·017/024"] = ("在你回合结束时，让两枚符文变为活跃状态。", "在你回合结束时，让最多两枚符文变为活跃状态。");
        errata["OGN·289/298"] = ("当你征服此处时，选择两枚符文，并在本回合结束时，让它们变为活跃状态。", "当你征服此处时，在本回合结束时，让最多两枚符文变为活跃。");
        errata["OGN·194/298"] = ("{{游走}}（我可以向其他战场进行移动。）\n当你查看主牌堆顶部的卡牌（不是抽牌）并看到我时，可以选择支付{{A}}将我打出。", "{{游走}}（我可以向其他战场进行移动。）\n你查看或展示主牌堆顶部的卡牌并看到我时，可以选择将我放逐。如选择放逐，则你可以选择支付{{A}}将我打出。");
        // DOC-16-P005-B003 applies to these three current CN printings.
        foreach (var printing in new[] { "OGN·235/298", "SFD·237/221", "SFD·237*/221" })
            errata[printing] = ("{{预知}}（当你打出我时，查看主牌堆顶部的一张牌，你可以选择将其回收。）\n每当你回收任意数量的卡牌时，给予一名友方单位增益。（如果该单位未拥有增益，则获得一个{{S}}+1增益。符文不被视为卡牌。）",
                "{{预知}}（当你打出我时，查看主牌堆顶部的一张牌，你可以选择将其回收。）\n每当你将任意数量的卡牌回收到自己的主牌堆时，给予一名友方单位增益。（如果该单位未拥有增益，则获得一个{{S}}+1增益。符文不被视为卡牌。）");
        // DOC-16-P009-B004/B005: four exact Teemo printings keep the archived old text.
        foreach (var printing in new[] { "OGN·121/298", "OGN·121a/298", "SFD·230/221", "SFD·230*/221" })
            errata[printing] = ("{{待命}}（支付{{A}}正面朝下放置此牌，之后可支付{{0}}将其当作反应牌打出。）\n当我防守一处战场、或从正面朝下的{{待命}}状态中打出时，展示你主牌堆顶部的五张牌，当中每有一张带有{{待命}}技能的卡牌，就对此处的一名敌方单位造成1点伤害，然后将展示的牌回收。",
                "{{待命}}（支付{{A}}正面朝下放置此牌，之后可支付{{0}}将其当作反应牌打出。）\n当我防守时，选择一名此处的敌方单位，展示你主牌堆顶部的五张牌。当中每有一张带有{{待命}}技能的卡牌，便对该单位造成1点伤害，然后将展示的牌回收。");
        foreach (var card in historical.Cards)
        {
            var actual = current.Cards.Single(c => c.CardNo == card.CardNo);
            if (errata.TryGetValue(card.CardNo, out var change))
            {
                Assert.Equal(change.Before, card.CardEffect); Assert.Equal(change.After, actual.CardEffect);
                Assert.Equal(JsonSerializer.Serialize(card), JsonSerializer.Serialize(actual with { CardEffect = card.CardEffect }));
            }
            else Assert.Equal(JsonSerializer.Serialize(card), JsonSerializer.Serialize(actual));
        }
        Assert.Equal(74, current.Cards.Count(c => !historical.Cards.Any(old => old.CardNo == c.CardNo)));
        var added = Assert.Single(current.Cards, c => c.CardNo == "VEN·SP3");
        Assert.Equal("VEN·SP3", added.CardNo); Assert.Equal(37918, added.Id); Assert.Equal(3, added.Power);
        Assert.Equal(3, added.Energy); Assert.Equal(1, added.ReturnEnergy);
        Assert.DoesNotContain(current.Cards, c => c.CardNo == "VEN-SP3/006");
    }
}
