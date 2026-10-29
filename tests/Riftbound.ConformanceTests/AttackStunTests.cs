using System.Reflection;
using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;

namespace Riftbound.ConformanceTests;

public sealed class AttackStunTests
{
    [Theory]
    [InlineData("OGN·238/298")] [InlineData("OGN·238a/298")] [InlineData("VEN·184")]
    [InlineData("UNL-176/219")] [InlineData("UNL-176a/219")]
    public async Task PrintedAttackMustChooseAnEnemyHereAndWaitForResponses(string no)
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, c => c.CardNo == no);
        var spec = Assert.Single(RuleTextParser.Parse(card).Triggers, t => t.Kind == TriggerKinds.UnitAttackStunEnemyHere);
        Assert.Equal(TriggerTimings.UnitAttack, spec.Timing); Assert.Equal(TriggerTargetScopes.EnemyUnitHere, spec.TargetScope);
        var attacked = await AttackDamageDistributionTests.Attack(AttackDamageDistributionTests.Position(no));
        var choice = Assert.IsType<PendingCardChoiceState>(attacked.State.PendingCardChoice);
        Assert.Equal(1, choice.RequiredCount); Assert.Equal(1, choice.MaxCount);
        Assert.Equal(new[] { "ENEMY1", "ENEMY2", "ENEMY3" }, choice.LegalObjectIds);
        foreach (var (player, ids) in new[] { ("P1", Array.Empty<string>()), ("P1", new[] { "HOST" }),
                ("P1", new[] { "ENEMY1", "ENEMY2" }), ("P2", new[] { "ENEMY1" }) }) {
            var command = new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ids);
            var rejected = await new CoreRuleEngine().ResolveAsync(attacked.State, new("illegal-stun", player, command.CmdType), command, default);
            Assert.False(rejected.Accepted); Assert.Empty(rejected.Events); Assert.Equal(MatchStateHasher.Hash(attacked.State), MatchStateHasher.Hash(rejected.State));
        }
        var confirmed = await AttackDamageDistributionTests.Confirm(attacked.State, "ENEMY1");
        Assert.Null(confirmed.State.PendingPayment); Assert.DoesNotContain("STUNNED", confirmed.State.CardObjects["ENEMY1"].UntilEndOfTurnEffects);
        var done = await AttackDamageDistributionTests.Top(confirmed.State);
        Assert.Contains("STUNNED", done.State.CardObjects["ENEMY1"].UntilEndOfTurnEffects);
        Assert.DoesNotContain("STUNNED", done.State.CardObjects["ENEMY2"].UntilEndOfTurnEffects);
        Restore(done.State); ArmamentMightTests.PublicRecovery(done.State);
    }

    [Theory]
    [InlineData("OGN·238/298")] [InlineData("VEN·184")] [InlineData("UNL-176/219")]
    public async Task WardCanBePaidOrDeclinedBeforeTheMandatorySkillResolves(string no)
    {
        foreach (var answer in new[] { "PAY", "DECLINE" }) {
            var state = AttackDamageDistributionTests.Position(no);
            state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["ENEMY1"] = state.CardObjects["ENEMY1"] with { Tags = [CardObjectTags.UnitCard, "法盾2"] } } };
            var attacked = await AttackDamageDistributionTests.Attack(state); var choice = attacked.State.PendingCardChoice!;
            var selected = await Act(attacked.State, "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ENEMY1"]));
            var payment = Assert.IsType<PendingPaymentState>(selected.State.PendingPayment);
            Assert.Equal(0, payment.ManaCost); Assert.Equal(2, payment.PowerCost);
            var paid = await Act(selected.State, "P1", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, [answer]));
            var done = answer == "PAY" ? await AttackDamageDistributionTests.Top(paid.State) : paid;
            Assert.Equal(answer == "PAY", done.State.CardObjects["ENEMY1"].UntilEndOfTurnEffects.Contains("STUNNED"));
            Restore(done.State); ArmamentMightTests.PublicRecovery(done.State);
        }
    }

    [Theory]
    [InlineData("OGN·238/298")] [InlineData("VEN·184")] [InlineData("UNL-176/219")]
    public async Task ReturningTheAttackerInResponseMakesTheOriginalTargetNoLongerHere(string no)
    {
        var initial = AttackDamageDistributionTests.Spell(AttackDamageDistributionTests.Position(no), "RETURN", "OGN·169/298", "P2");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["HOST"] = initial.CardObjects["HOST"] with { Power = 2 } } };
        var attacked = await AttackDamageDistributionTests.Attack(initial);
        var confirmed = await AttackDamageDistributionTests.Confirm(attacked.State, "ENEMY1");
        var passed = await Act(confirmed.State, "P1", new PassPriorityCommand());
        var response = await Act(passed.State, "P2", new PlayCardCommand("RETURN", "OGN·169/298", ["HOST"]));
        var returned = await AttackDamageDistributionTests.Top(response.State);
        Assert.Contains("HOST", returned.State.PlayerZones["P1"].Hand);
        var done = await AttackDamageDistributionTests.Top(returned.State);
        Assert.DoesNotContain("STUNNED", done.State.CardObjects["ENEMY1"].UntilEndOfTurnEffects);
        Assert.DoesNotContain("\"HOST\"", JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(done.State)["P2"]));
        Restore(done.State); ArmamentMightTests.PublicRecovery(done.State);
    }

    [Theory]
    [InlineData("OGN·238/298")] [InlineData("VEN·184")] [InlineData("UNL-176/219")]
    public async Task CopiedPrintedTextCreatesTwoIndependentMandatoryAttackTriggers(string no)
    {
        var attacked = await AttackDamageDistributionTests.Attack(AttackDamageDistributionTests.Position(no, copied: true));
        Assert.Equal(2, attacked.State.TriggerQueue.Count);
        var ids = attacked.State.TriggerQueue.Select(trigger => trigger.TriggerId).ToArray();
        Assert.Equal(2, ids.Distinct().Count());
        var state = (await Act(attacked.State, "P1", new OrderTriggersCommand(OrderedTriggerIds: ids))).State;
        foreach (var target in new[] { "ENEMY1", "ENEMY2" }) {
            var choice = Assert.IsType<PendingCardChoiceState>(state.PendingCardChoice); Assert.Equal(1, choice.RequiredCount);
            state = (await Act(state, "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [target]))).State;
        }
        Assert.Equal(2, state.StackItems.Count); Assert.Single(state.StackItems, i => i.FieldContext!.Origin is not null);
        state = (await AttackDamageDistributionTests.Top(state)).State;
        state = (await AttackDamageDistributionTests.Top(state)).State;
        Assert.All(new[] { "ENEMY1", "ENEMY2" }, id => Assert.Contains("STUNNED", state.CardObjects[id].UntilEndOfTurnEffects));
        Restore(state); ArmamentMightTests.PublicRecovery(state);
    }

    [Fact]
    public async Task NewLeonaPrintingRequiresYellowPowerAndIsLegalInAChinaDeck()
    {
        const string no = "VEN·184"; const OfficialDeckFormat format = OfficialDeckFormat.ChinaStandard20260724;
        var card = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, c => c.CardNo == no);
        Assert.Equal(37887, card.Id); Assert.Equal("炽诚者", card.SubTitle); Assert.Equal(4, card.Energy); Assert.Equal(1, card.ReturnEnergy); Assert.Equal(4, card.Power);
        var state = VenUnitEntryBasicTests.Position(no);
        state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) {
            ["P1"] = new(4, 0, new Dictionary<string, int> { [RuneTrait.Yellow] = 1 }) } };
        var command = new PlayCardCommand("UNIT", no, [], Destination: "BASE");
        var bad = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) {
            ["P1"] = new(4, 0, new Dictionary<string, int> { [RuneTrait.Red] = 1 }) } };
        var rejected = await new CoreRuleEngine().ResolveAsync(bad, new("bad-power", "P1", command.CmdType), command, default);
        Assert.False(rejected.Accepted); Assert.Empty(rejected.Events); Assert.Equal(MatchStateHasher.Hash(bad), MatchStateHasher.Hash(rejected.State));
        var game = new Driver(state); await game.Act("P1", command);
        Assert.Contains("坚守", game.State.CardObjects["UNIT"].Tags); Assert.Equal(0, game.State.RunePools["P1"].Mana); await game.Replay();
        var catalog = await OfficialCardCatalog.LoadRuntimeAsync();
        var allowed = catalog with { Cards = catalog.Cards.Where(c => ChinaStandardBanList.IsAllowed(c, format)).ToArray() };
        string Rune(string color) => allowed.Cards.Where(c => c.CardCategory == "rune" && c.CardColorList.Contains(color))
            .OrderBy(c => c.CardNo, StringComparer.Ordinal).First().CardNo;
        var builder = typeof(PreconstructedDeckCatalog).GetMethod("BuildLowCurveDeck", BindingFlags.NonPublic | BindingFlags.Static)!;
        var deck = (OfficialDecklist)builder.Invoke(null, [allowed, allowed.Cards.ToDictionary(c => c.CardNo, StringComparer.Ordinal),
            "OGN·261/298", no, Array.Empty<string>(), new[] { "OGN·277/298", "OGN·278/298", "OGN·294/298" },
            Enumerable.Repeat(Rune("green"), 6).Concat(Enumerable.Repeat(Rune("yellow"), 6)).ToArray()])!;
        var validation = OfficialDeckValidator.Validate(deck, catalog, format);
        Assert.True(validation.IsValid, string.Join("; ", validation.Errors));
        var initial = MatchReplayInitialStateBuilder.FromSeats("leona-print-deck", new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" });
        var session = new MatchSession(initial, new CoreRuleEngine(), NoopMatchJournal.Instance, NoopMatchPlayerStore.Instance,
            new MatchSessionOptions(false, format));
        var submit = new SubmitDeckCommand(deck.LegendCardNo, deck.ChampionCardNo, deck.MainDeck, deck.RuneDeck, deck.Battlefields);
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web); var raw = JsonSerializer.SerializeToElement(submit, options);
        foreach (var player in new[] { "P1", "P2" }) Assert.True((await session.SubmitDeckAsync(player, "deck-" + player, submit, raw, default)).Accepted);
        var ready = JsonSerializer.SerializeToElement(new ReadyCommand(), options);
        Assert.True((await session.ReadyAsync("P1", "ready-P1", ready, default)).Accepted);
        var opened = await session.ReadyAsync("P2", "ready-P2", ready, default);
        Assert.True(opened.Accepted, opened.ErrorMessage); Assert.Equal(MatchPhases.Mulligan, opened.State.Phase);
    }
}
