using System.Reflection;
using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class AttackMightDamageTests
{
    private static MatchState Position(string no = "VEN·180", int experience = 3, bool copied = false)
    {
        var state = AttackDamageDistributionTests.Position(no, copied);
        state = OfficialHoldSequenceTests.AddUnit(state, "DISTANT", "OGN·001/298", "F", "P2");
        state = LocalDestructionRecallTests.At(state, "DISTANT", "P2", "BASE");
        return state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["HOST"] = state.CardObjects["HOST"] with { Power = 5 } },
            PlayerExperience = new Dictionary<string, int> { ["P1"] = experience, ["P2"] = 0 } };
    }

    private static async Task Attack(Driver game)
    {
        await game.Act("P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F", []));
        for (var i = 0; i < 4 && game.State.FocusPlayerId is { } focus; i++)
            await game.Act(focus, new PassFocusCommand());
        if (ResolutionResult.BuildPrompts(game.State)["P1"].EnabledActions().Contains(CommandTypes.DeclareBattle))
            await game.Act("P1", new DeclareBattleCommand("F", ["HOST"], ["ENEMY1", "ENEMY2", "ENEMY3"], ["COMBAT_ASSIGNMENT"]));
    }

    private static async Task Choose(Driver game, params string[] targets)
    {
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        await game.Act(choice.PlayerId, new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, targets));
    }

    private static async Task Pay(Driver game, string answer = "PAY")
    {
        var payment = Assert.IsType<PendingPaymentState>(game.State.PendingPayment);
        await game.Act(payment.PlayerId, new PayCostCommand(payment.PaymentId, payment.PaymentWindow, [answer]));
    }

    private static async Task Reject(MatchState state, string player, GameCommand command)
    {
        var result = await new CoreRuleEngine().ResolveAsync(state, new("illegal-attack-damage", player, command.CmdType), command, default);
        Assert.False(result.Accepted); Assert.Empty(result.Events);
        Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(result.State));
    }

    [Theory]
    [InlineData("UNL-119/219", 34660)] [InlineData("UNL-119a/219", 34661)] [InlineData("VEN·180", 37883)]
    public async Task EachOfficialPrintingPaysItsPrintedCostsAndEntersWithHunt(string no, long id)
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, card => card.CardNo == no);
        Assert.Equal(id, card.Id); Assert.Equal("进化猎手", card.SubTitle);
        var spec = Assert.Single(RuleTextParser.Parse(card).Triggers, t => t.Kind == TriggerKinds.UnitAttackPayExperienceSourceMightDamage);
        Assert.Equal(3, spec.ExperienceCost); Assert.True(spec.Optional);
        var state = VenUnitEntryBasicTests.Position(no) with { RunePools = new Dictionary<string, RunePool> {
            ["P1"] = new(20, 0, new Dictionary<string, int> { [RuneTrait.Orange] = 2 }), ["P2"] = new(20, 20) } };
        var game = new Driver(state);
        await game.Act("P1", new PlayCardCommand("UNIT", no, [], Destination: "BASE"));
        Assert.Equal(15, game.State.RunePools["P1"].Mana);
        Assert.Equal(1, game.State.RunePools["P1"].PowerByTrait[RuneTrait.Orange]);
        Assert.True(game.State.CardObjects["UNIT"].IsExhausted); Assert.Equal(5, game.State.CardObjects["UNIT"].Power);
        Assert.Contains("狩猎", game.State.CardObjects["UNIT"].Tags); Assert.Empty(game.State.StackItems);
        await game.Replay();
    }

    [Theory]
    [InlineData("UNL-119/219")] [InlineData("UNL-119a/219")] [InlineData("VEN·180")]
    public async Task AttackChoosesThenPaysExperienceBeforeResponsesAndDealsFiveDamage(string no)
    {
        var game = new Driver(Position(no)); await Attack(game);
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        Assert.Equal(0, choice.RequiredCount); Assert.Equal(new[] { "ENEMY1", "ENEMY2", "ENEMY3" }, choice.LegalObjectIds);
        Assert.Equal(3, game.State.PlayerExperience["P1"]);
        await Choose(game, "ENEMY1");
        Assert.Equal(3, game.State.PendingPayment!.ExperienceCost); Assert.Equal(0, game.State.PendingPayment.PowerCost);
        await Pay(game); Assert.Equal(0, game.State.PlayerExperience["P1"]);
        Assert.Equal(0, game.State.CardObjects["ENEMY1"].Damage);
        await game.Top();
        var damage = Assert.Single(game.Events(), e => e.Event.Kind == "DAMAGE_APPLIED").Event;
        Assert.Equal(5, damage.Payload["damage"]); Assert.Contains("ENEMY1", game.State.PlayerZones["P2"].Graveyard);
        await game.Replay();
    }

    [Theory]
    [InlineData("UNL-119/219")] [InlineData("UNL-119a/219")] [InlineData("VEN·180")]
    public async Task FriendlyDistantEquipmentAndDuplicateTargetsAreRejectedWithoutSpendingExperience(string no)
    {
        var game = new Driver(Position(no)); await Attack(game);
        var choice = game.State.PendingCardChoice!;
        foreach (var targets in new string[][] { ["HOST"], ["GEAR"], ["DISTANT"], ["ENEMY1", "ENEMY2"], ["ENEMY1", "ENEMY1"] })
            await Reject(game.State, "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, targets));
        await Reject(game.State, "P2", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ENEMY1"]));
        Assert.Equal(3, game.State.PlayerExperience["P1"]);
        await Choose(game); Assert.Null(game.State.PendingPayment); Assert.Empty(game.State.StackItems);
        Assert.Equal(3, game.State.PlayerExperience["P1"]); await game.Replay();
    }

    [Theory]
    [InlineData("UNL-119/219")] [InlineData("UNL-119a/219")] [InlineData("VEN·180")]
    public async Task TwoExperienceCannotPayTheCostAndTheControllerMayStillDecline(string no)
    {
        var game = new Driver(Position(no, experience: 2)); await Attack(game); await Choose(game, "ENEMY1");
        var payment = game.State.PendingPayment!;
        await Reject(game.State, "P1", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, ["PAY"]));
        Assert.Equal(2, game.State.PlayerExperience["P1"]);
        await Pay(game, "DECLINE"); Assert.Equal(2, game.State.PlayerExperience["P1"]);
        Assert.Empty(game.State.StackItems); Assert.Equal(0, game.State.CardObjects["ENEMY1"].Damage); await game.Replay();
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task WardAndExperienceCommitTogetherOrNeitherIsSpent(bool pay)
    {
        var state = Position();
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["ENEMY1"] = state.CardObjects["ENEMY1"] with { Tags = [CardObjectTags.UnitCard, "法盾2"] } } };
        var game = new Driver(state); await Attack(game); await Choose(game, "ENEMY1");
        Assert.Equal(2, game.State.PendingPayment!.PowerCost); Assert.Equal(3, game.State.PendingPayment.ExperienceCost);
        var power = game.State.RunePools["P1"].TotalPower;
        await Pay(game, pay ? "PAY" : "DECLINE");
        Assert.Equal(pay ? 0 : 3, game.State.PlayerExperience["P1"]);
        Assert.Equal(power - (pay ? 2 : 0), game.State.RunePools["P1"].TotalPower);
        if (pay) await game.Top();
        Assert.Equal(pay, game.State.PlayerZones["P2"].Graveyard.Contains("ENEMY1")); await game.Replay();
    }

    [Fact]
    public async Task DamageReadsCurrentMightAfterAResponseRatherThanMightWhenAttacking()
    {
        var game = new Driver(AttackDamageDistributionTests.Spell(Position(), "BUFF", "SFD·034/221", "P1"));
        await Attack(game); await Choose(game, "ENEMY1"); await Pay(game);
        await game.Act("P1", new PlayCardCommand("BUFF", "SFD·034/221", ["HOST"])); await game.Top();
        Assert.Equal(7, game.State.CardObjects["HOST"].Power); await game.Top();
        Assert.Equal(7, Assert.Single(game.Events(), e => e.Event.Kind == "DAMAGE_APPLIED").Event.Payload["damage"]);
        await game.Replay();
    }

    [Theory]
    [InlineData("source-hand")] [InlineData("source-base")] [InlineData("target-base")]
    public async Task LeavingTheFieldOrNoLongerSharingALocationPreventsTheDamageWithoutRefundingExperience(string move)
    {
        var no = move == "source-hand" ? "OGN·104/298" : "OGS·011/024";
        var player = move == "target-base" ? "P2" : "P1";
        var game = new Driver(AttackDamageDistributionTests.Spell(Position(), "MOVE", no, player));
        await Attack(game); await Choose(game, "ENEMY1"); await Pay(game);
        if (player == "P2") await game.Act("P1", new PassPriorityCommand());
        await game.Act(player, new PlayCardCommand("MOVE", no, [move == "target-base" ? "ENEMY1" : "HOST"]));
        await game.Top(); await game.Top();
        Assert.Equal(0, game.State.PlayerExperience["P1"]);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "DAMAGE_APPLIED");
        if (move == "source-hand")
            Assert.DoesNotContain("HOST", JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(game.State)["P2"]));
        await game.Replay();
    }

    [Fact]
    public async Task AttachedCopyCreatesTwoIndependentTargetAndExperienceConfirmations()
    {
        var game = new Driver(Position(experience: 6, copied: true)); await Attack(game);
        Assert.Equal(2, game.State.TriggerQueue.Count);
        await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: game.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
        await Choose(game, "ENEMY1"); await Pay(game); Assert.Equal(3, game.State.PlayerExperience["P1"]);
        await Choose(game, "ENEMY2"); await Pay(game); Assert.Equal(0, game.State.PlayerExperience["P1"]);
        Assert.Equal(2, game.State.StackItems.Count); Assert.Contains(game.State.StackItems, i => i.FieldContext!.Origin is not null);
        await game.Top(); await game.Top();
        Assert.All(new[] { "ENEMY1", "ENEMY2" }, id => Assert.Contains(id, game.State.PlayerZones["P2"].Graveyard));
        await game.Replay();
    }

    [Theory]
    [InlineData("experience")] [InlineData("ward")] [InlineData("paid")] [InlineData("controller")]
    [InlineData("instance")] [InlineData("source-generation")]
    public async Task RecoveryRejectsTamperedTargetedPaymentAndSourceReceipts(string change)
    {
        var game = new Driver(Position()); await Attack(game); await Choose(game, "ENEMY1");
        var state = game.State; var item = Assert.Single(state.StackItems); var context = item.FieldContext!;
        if (change is "experience" or "ward")
            state = state with { PendingPayment = state.PendingPayment! with {
                ExperienceCost = change == "experience" ? 2 : 3, PowerCost = change == "ward" ? 1 : 0 } };
        else state = state with { StackItems = [item with { FieldContext = context with {
            SourceGeneration = context.SourceGeneration + (change == "source-generation" ? 1 : 0),
            AttackMightDamage = context.AttackMightDamage! with {
                CostPaid = change == "paid", ControllerId = change == "controller" ? "P2" : "P1",
                RuleTextInstanceId = change == "instance" ? "FORGED" : context.AttackMightDamage.RuleTextInstanceId } } }] };
        Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(state));
    }

    [Fact]
    public async Task ChinaStandardDeckAcceptsTheOfficialVenHeroPrinting()
    {
        const OfficialDeckFormat format = OfficialDeckFormat.ChinaStandard20260724;
        var catalog = await OfficialCardCatalog.LoadRuntimeAsync();
        var allowed = catalog with { Cards = catalog.Cards.Where(c => ChinaStandardBanList.IsAllowed(c, format)).ToArray() };
        string Rune(string color) => allowed.Cards.Where(c => c.CardCategory == "rune" && c.CardColorList.Contains(color))
            .OrderBy(c => c.CardNo, StringComparer.Ordinal).First().CardNo;
        var builder = typeof(PreconstructedDeckCatalog).GetMethod("BuildLowCurveDeck", BindingFlags.NonPublic | BindingFlags.Static)!;
        var deck = (OfficialDecklist)builder.Invoke(null, [allowed, allowed.Cards.ToDictionary(c => c.CardNo, StringComparer.Ordinal),
            "UNL-201/219", "VEN·180", Array.Empty<string>(),
            new[] { "OGN·277/298", "OGN·278/298", "OGN·294/298" },
            Enumerable.Repeat(Rune("orange"), 6).Concat(Enumerable.Repeat(Rune("purple"), 6)).ToArray()])!;
        var validation = OfficialDeckValidator.Validate(deck, catalog, format);
        Assert.True(validation.IsValid, string.Join("; ", validation.Errors));
    }
}
