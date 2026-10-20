using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;

namespace Riftbound.ConformanceTests;

public sealed class ReversalTargetChoiceTests
{
    internal const string Card = "OGN·080/298";

    internal static MatchState Position(string spell = "OGN·095/298")
        => AttackDamageDistributionTests.Spell(RebuttalRulesTests.Position(spell), "REVERSAL", Card, "P2");

    internal static async Task<MatchState> Open(MatchState? initial = null, string spell = "OGN·095/298",
        IReadOnlyList<string>? targets = null, string mode = "", IReadOnlyList<string>? costs = null,
        string destination = "", IReadOnlyList<string>? movementDestinations = null)
    {
        var played = await Act(initial ?? Position(spell), "P1", new PlayCardCommand("R-SPELL", spell,
            targets ?? ["R-TARGET"], mode, costs, destination, MovementDestinations: movementDestinations));
        var handed = await Act(played.State, "P1", new PassPriorityCommand());
        var control = await Act(handed.State, "P2", new PlayCardCommand("REVERSAL", Card,
            [Assert.Single(played.State.StackItems).StackItemId]));
        var opened = await Top(control.State);
        Assert.NotNull(opened.State.PendingRuleChoice);
        Assert.Equal("SPELL_CHOICE_REMAKE", opened.State.PendingRuleChoice.Request.Kind);
        Assert.Equal("P2", opened.State.PendingRuleChoice.Request.PlayerId);
        Restore(opened.State); ArmamentMightTests.PublicRecovery(opened.State);
        return opened.State;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ControlAllowsKeepingOrRemakingTargetsWithoutReplayingCosts(bool remake)
    {
        var s = await Open();
        var before = s.PendingRuleChoice!.Request.Preview!;
        var pool = before.RunePools["P2"];
        if (remake)
        {
            s = await RebuttalRulesTests.Choose(s, RebuttalRulesTests.Option(s, "重选目标 1 为", "（R-OTHER）"));
            s = await RebuttalRulesTests.Choose(s, "CONFIRM");
        }
        else s = await RebuttalRulesTests.Choose(s, "KEEP_ALL");
        var spell = Assert.Single(s.StackItems);
        Assert.Equal("P2", spell.ControllerId);
        Assert.Equal(new[] { remake ? "R-OTHER" : "R-TARGET" }, spell.TargetObjectIds);
        Assert.Equal(pool, s.RunePools["P2"]);
        Assert.Contains("REVERSAL", s.PlayerZones["P2"].Graveyard);
        Assert.DoesNotContain("R-SPELL", s.PlayerZones["P1"].Graveyard);
        var done = await Top(s);
        Assert.Equal(remake ? 8 : 9, done.State.CardObjects["R-OTHER"].Power);
        Assert.Equal(remake ? 9 : 8, done.State.CardObjects["R-TARGET"].Power);
        Assert.Contains("R-SPELL", done.State.PlayerZones["P1"].Graveyard);
        Assert.DoesNotContain("R-SPELL", done.State.PlayerZones["P2"].Graveyard);
        Restore(done.State); ArmamentMightTests.PublicRecovery(done.State);
    }

    [Fact]
    public async Task NewTargetsDoNotGrantANewMode()
    {
        var s = await Open(spell: "UNL-182/219", targets: [], mode: "DRAW_1");
        Assert.DoesNotContain(s.PendingRuleChoice!.Request.Options, o => o.Label.Contains("模式 "));
        s = await RebuttalRulesTests.Choose(s, "KEEP_ALL");
        Assert.Equal("PERFECT_FINALE_DRAW_1", Assert.Single(s.StackItems).EffectKind);
        Restore(s);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RemakingTargetsRetainsDestinationsAndRequiresEveryRetainedChoiceToBeLegal(bool remake)
    {
        var s = await Open(spell: "UNL-202/219", targets: ["R-OTHER", "R-TARGET"],
            movementDestinations: ["BATTLEFIELD:F", "BATTLEFIELD:F"]);
        Assert.DoesNotContain(s.PendingRuleChoice!.Request.Options, o => o.Label.Contains("目的地"));
        s = await RebuttalRulesTests.Choose(s, RebuttalRulesTests.Option(s, "重选目标 1 为", "（R-TARGET）"));
        Assert.DoesNotContain(s.PendingRuleChoice!.Request.Options, o => o.Id == "CONFIRM");
        if (remake)
        {
            s = await RebuttalRulesTests.Choose(s, RebuttalRulesTests.Option(s, "重选目标 2 为", "（R-OTHER）"));
            s = await RebuttalRulesTests.Choose(s, "CONFIRM");
        }
        else s = await RebuttalRulesTests.Choose(s, "KEEP_ALL");
        Assert.Equal(new[] { "BATTLEFIELD:F", "BATTLEFIELD:F" }, Assert.Single(s.StackItems).MovementDestinations);
        var done = await Top(s);
        Assert.Equal(remake ? "BATTLEFIELD" : "BASE", done.State.ObjectLocations["R-TARGET"].Zone);
        Assert.Equal(remake ? "BATTLEFIELD" : "BASE", done.State.ObjectLocations["R-OTHER"].Zone);
        Restore(done.State); ArmamentMightTests.PublicRecovery(done.State);
    }

    [Fact]
    public async Task EachDeclaredEchoExecutionRetainsItsCostsAndCanSelectItsOwnTarget()
    {
        var s = await Open(spell: "SFD·080/221", targets: ["R-TARGET"], costs: ["ECHO"]);
        var original = s.PendingRuleChoice!.Request.Preview!.StackItems.Single(i => i.SourceObjectId == "R-SPELL");
        s = await RebuttalRulesTests.Choose(s, RebuttalRulesTests.Option(s, "第 2 次", "重选目标 1 为", "（R-OTHER）"));
        s = await RebuttalRulesTests.Choose(s, "CONFIRM");
        var spell = Assert.Single(s.StackItems);
        Assert.Equal(original.OptionalCosts, spell.OptionalCosts);
        Assert.Equal(original.PlayCost, spell.PlayCost);
        Assert.Equal(original.EffectRepeatCount, spell.EffectRepeatCount);
        Assert.Equal(2, spell.RepeatExecutions!.Count);
        Assert.Equal(new[] { "R-TARGET" }, spell.RepeatExecutions[0].TargetObjectIds);
        Assert.Equal(new[] { "R-OTHER" }, spell.RepeatExecutions[1].TargetObjectIds);
        Restore(s); ArmamentMightTests.PublicRecovery(s);
        var done = await Top(s); Assert.Empty(done.State.StackItems); Restore(done.State);
    }

    [Theory]
    [InlineData("actor")]
    [InlineData("stale")]
    [InlineData("window")]
    [InlineData("option")]
    [InlineData("multiple")]
    [InlineData("priority")]
    public async Task OnlyTheCurrentControllerCanSubmitTheCurrentServerChoice(string mutation)
    {
        var s = await Open(); var pending = s.PendingRuleChoice!;
        GameCommand command = mutation == "priority" ? new PassPriorityCommand()
            : new PayCostCommand(mutation == "stale" ? pending.Request.Id + "-old" : pending.Request.Id,
                mutation == "window" ? "WRONG" : "RULE_REPLACEMENT",
                mutation == "multiple" ? ["KEEP_ALL", "CONFIRM"] : [mutation == "option" ? "FORGED" : "KEEP_ALL"]);
        var result = await new CoreRuleEngine().ResolveAsync(s,
            new("invalid-target-choice", mutation == "actor" ? "P1" : "P2", command.CmdType), command, default);
        Assert.False(result.Accepted); Assert.Empty(result.Events);
        Assert.Equal(MatchStateHasher.Hash(s), MatchStateHasher.Hash(result.State)); Restore(s);
    }

    [Fact]
    public async Task RetargetingAnEnemyWithWardDoesNotPlayTheSpellOrPayItsCostsAgain()
    {
        var initial = Position();
        initial = initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) { ["P2"] = new(4, 3) },
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["R-OTHER"] = initial.CardObjects["R-OTHER"] with { Tags = [CardObjectTags.UnitCard, CardObjectTags.Spellshield] } } };
        var s = await Open(initial);
        Assert.Equal(0, s.PendingRuleChoice!.Request.Preview!.RunePools["P2"].Mana);
        Assert.Equal(0, s.PendingRuleChoice.Request.Preview.RunePools["P2"].TotalPower);
        s = await RebuttalRulesTests.Choose(s, RebuttalRulesTests.Option(s, "重选目标 1 为", "（R-OTHER）"));
        s = await RebuttalRulesTests.Choose(s, "CONFIRM");
        Assert.Equal(0, s.RunePools["P2"].TotalPower);
        var done = await Top(s); Assert.Equal(8, done.State.CardObjects["R-OTHER"].Power); Restore(done.State);
    }

    [Fact]
    public async Task AConfirmedNewTargetUsesItsCurrentIncarnation()
    {
        var s = await Open();
        s = await RebuttalRulesTests.Choose(s, RebuttalRulesTests.Option(s, "重选目标 1 为", "（R-OTHER）"));
        s = await RebuttalRulesTests.Choose(s, "CONFIRM");
        Assert.Equal(s.CardObjects["R-OTHER"].ObjectGeneration, Assert.Single(s.StackItems).TargetGenerations!["R-OTHER"]);
        s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
            ["R-OTHER"] = s.CardObjects["R-OTHER"] with { ObjectGeneration = s.CardObjects["R-OTHER"].ObjectGeneration + 1 } } };
        var done = await Top(s); Assert.Equal(9, done.State.CardObjects["R-OTHER"].Power); Restore(done.State);
    }

    [Theory]
    [InlineData("actor")]
    [InlineData("option")]
    [InlineData("transcript")]
    [InlineData("preview")]
    public async Task IndependentRecoveryRejectsAlteredTargetChoiceEvidence(string mutation)
    {
        var s = await Open(); var pending = s.PendingRuleChoice!;
        var forged = mutation switch {
            "actor" => pending with { Request = pending.Request with { PlayerId = "P1" } },
            "option" => pending with { Request = pending.Request with { Options = [new("KEEP_ALL", "Forged")] } },
            "transcript" => pending with { Answers = [new("FORGED", "KEEP_ALL")] },
            _ => pending with { Request = pending.Request with { Preview = pending.Origin with { Tick = pending.Origin.Tick + 1 } } } };
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(s with { PendingRuleChoice = forged }),
            e => e.Contains("replacement choice", StringComparison.OrdinalIgnoreCase));
        Restore(s);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task ConfirmingEvenTheSameTargetTriggersTargetSelectionButKeepingChoicesDoesNot(bool soulMirror, bool remake)
    {
        // Controlled rule-754 fixture, not tournament admission or certification of this restricted printing.
        var initial = LocalDestructionRecallTests.At(Position(), "R-TARGET", "P2", "BATTLEFIELD", "F");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["F"] = initial.CardObjects["F"] with { CardNo = "OGN·292/298", ControllerId = "P2", Tags = [P6TokenFactoryCatalog.BattlefieldCardTag] } } };
        var s = soulMirror ? await RebuttalRulesTests.Choose(await RebuttalRulesTests.Open(initial), "PAY:ANY") : await Open(initial);
        s = await RebuttalRulesTests.Choose(s, remake ? "CONFIRM" : "KEEP_ALL");
        var trigger = s.StackItems.Where(i => i.SpellContext?.Kind == TriggerKinds.BattlefieldFriendlySpellDraw).ToArray();
        Assert.Equal(remake ? 1 : 0, trigger.Length);
        if (remake) Assert.Equal("P2", trigger.Single().ControllerId);
        Restore(s); ArmamentMightTests.PublicRecovery(s);
    }

    [Fact]
    public async Task NewTargetChoicesDoNotExposeOpponentHandOrDeckIdentities()
    {
        var initial = Position();
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["PRIVATE-HAND"] = new("PRIVATE-HAND", cardNo: "OGN·080/298", ownerId: "P1", controllerId: "P1"),
                ["PRIVATE-DECK"] = new("PRIVATE-DECK", cardNo: "OGN·080/298", ownerId: "P1", controllerId: "P1") },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) {
                ["PRIVATE-HAND"] = new("P1", "HAND"), ["PRIVATE-DECK"] = new("P1", "MAIN_DECK") },
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
                ["P1"] = initial.PlayerZones["P1"] with {
                    Hand = [..initial.PlayerZones["P1"].Hand, "PRIVATE-HAND"],
                    MainDeck = [..initial.PlayerZones["P1"].MainDeck, "PRIVATE-DECK"] } } };
        var s = await Open(initial);
        var snapshot = System.Text.Json.JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(s)["P2"]);
        var prompt = System.Text.Json.JsonSerializer.Serialize(ResolutionResult.BuildPrompts(s)["P2"]);
        foreach (var id in new[] { "PRIVATE-HAND", "PRIVATE-DECK" })
        {
            Assert.DoesNotContain(id, snapshot); Assert.DoesNotContain(id, prompt);
            Assert.DoesNotContain(s.PendingRuleChoice!.Request.Options, o => o.ObjectIds?.Contains(id) == true);
        }
        Restore(s); ArmamentMightTests.PublicRecovery(s);
    }
}
