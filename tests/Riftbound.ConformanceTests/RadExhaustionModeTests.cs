using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class RadExhaustionModeTests
{
    private const string Card = "RAD·043/167", Window = "EXHAUSTION_MODE_CONFIRMATION", TargetWindow = "EXHAUSTION_MODE_TARGET";
    private const string Kind = "SOURCE_EXHAUSTED_CHOOSE_UNUSED_MODE", Used = "EXHAUSTION_MODE_USED|";
    private const string Boon = "BOON_SELF", Stun = "BOON_STUN_ENEMY", Ready = "BOON_READY_SELF";
    public static IEnumerable<object[]> Printings() => new object[][] { [Card, 39537L], ["RAD·043a/167", 39538L] };
    public static IEnumerable<object[]> Modes() => Printings().SelectMany(p => new[] { Boon, Stun, Ready }.Select(mode => new object[] { p[0], mode }));

    private static MatchState Board(string no = Card, bool boon = false, bool copied = false)
    {
        var s = ArmamentMightTests.Position(unit: no, power: 4, gear: copied ? "SFD·059/221" : "SFD·022/221", attached: copied);
        s = s with { RoomId = "RAD-EXHAUSTION-MODES", CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
            ["HOST"] = OfficialGarenConquestBattlefieldCountTests.Printed("HOST", no, "P1") } };
        if (boon) s = WithBoon(s);
        s = HandDiscardObserverTests.Put(s, "ENEMY", "OGN·001/298", "P2", "BASE");
        s = HandDiscardObserverTests.Put(s, "ALLY", "OGN·001/298", "P1", "BASE");
        return s with { RunePools = new Dictionary<string, RunePool>(s.RunePools) { ["P2"] = new(20, 20) } };
    }
    private static MatchState WithBoon(MatchState s)
        => s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) { ["HOST"] = s.CardObjects["HOST"] with {
            Power = 5, Tags = [..s.CardObjects["HOST"].Tags, CardObjectTags.Boon] } } };
    private static async Task Exhaust(Driver game)
    {
        var inBase = game.State.PlayerZones["P1"].Base.Contains("HOST");
        await game.Act("P1", new MoveUnitCommand("HOST", inBase ? "BASE" : "BATTLEFIELD:F", inBase ? "BATTLEFIELD:F" : "BASE", []));
    }
    private static async Task Choose(Driver game, string value)
    {
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        await game.Act(choice.PlayerId, new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [value]));
    }
    private static async Task Pay(Driver game, string choice = "PAY")
    {
        var payment = Assert.IsType<PendingPaymentState>(game.State.PendingPayment);
        await game.Act(payment.PlayerId, new PayCostCommand(payment.PaymentId, payment.PaymentWindow, [choice]));
    }

    [Theory, MemberData(nameof(Printings))]
    public async Task BothOfficialPrintingsPayFourManaWithoutInventingAGreenPowerCost(string no, long id)
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, c => c.CardNo == no);
        Assert.Equal(id, card.Id); Assert.Equal("断剑之誓", card.SubTitle); Assert.Equal(4, card.Energy); Assert.Null(card.ReturnEnergy); Assert.Equal(4, card.Power);
        Assert.Single(TriggerParser.Parse(card.CardEffect), t => t.Kind == TriggerKinds.SourceExhaustedChooseUnusedMode);
        var initial = VenUnitEntryBasicTests.Position(no) with { RunePools = new Dictionary<string, RunePool> { ["P1"] = new(4, 0), ["P2"] = RunePool.Empty } };
        var game = new Driver(initial); await game.Act("P1", new PlayCardCommand("UNIT", no, [], Destination: "BASE"));
        Assert.Equal(0, game.State.RunePools["P1"].Mana); Assert.True(game.State.CardObjects["UNIT"].IsExhausted);
        Assert.Equal(4, game.State.CardObjects["UNIT"].Power); Assert.Null(game.State.PendingCardChoice); Assert.Empty(game.State.StackItems);
        await Reject(initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) { ["P1"] = new(3, 20) } }, new PlayCardCommand("UNIT", no, [], Destination: "BASE"));
    }

    [Theory, MemberData(nameof(Modes))]
    public async Task EachModeConfirmsItsCostBeforeResponsesAndUsesTheSharedEffect(string no, string mode)
    {
        var game = new Driver(Board(no, boon: mode != Boon)); await Exhaust(game);
        Assert.Equal(Window, game.State.PendingCardChoice!.ChoiceWindow);
        await Choose(game, mode);
        if (mode == Stun) { Assert.Equal(TargetWindow, game.State.PendingCardChoice!.ChoiceWindow); await Choose(game, "ENEMY"); }
        if (mode != Boon) {
            Assert.True(game.State.CardObjects["HOST"].Tags.Contains(CardObjectTags.Boon));
            await Pay(game); Assert.DoesNotContain(CardObjectTags.Boon, game.State.CardObjects["HOST"].Tags);
            Assert.Equal(4, game.State.CardObjects["HOST"].Power);
        }
        Assert.Single(game.State.UntilEndOfTurnEffects, e => e.StartsWith(Used));
        Assert.True(game.State.CardObjects["HOST"].IsExhausted); Assert.DoesNotContain("STUNNED", game.State.CardObjects["ENEMY"].UntilEndOfTurnEffects);
        await game.Top();
        Assert.Equal(mode == Boon ? 5 : 4, game.State.CardObjects["HOST"].Power);
        Assert.Equal(mode == Boon, game.State.CardObjects["HOST"].Tags.Contains(CardObjectTags.Boon));
        Assert.Equal(mode != Ready, game.State.CardObjects["HOST"].IsExhausted);
        Assert.Equal(mode == Stun, game.State.CardObjects["ENEMY"].UntilEndOfTurnEffects.Contains("STUNNED"));
        Assert.DoesNotContain(Used, JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(game.State)));
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind.StartsWith("INTERNAL_"));
    }

    [Fact]
    public async Task TheBoonModeRemainsLegalWhenTheSourceAlreadyHasABoon()
    {
        var game = new Driver(Board(boon: true)); await Exhaust(game); await Choose(game, Boon); await game.Top();
        Assert.Equal(5, game.State.CardObjects["HOST"].Power); Assert.Single(game.State.CardObjects["HOST"].Tags, t => t == CardObjectTags.Boon);
    }

    [Fact]
    public async Task MissingBoonOrEnemyTargetRemovesOnlyTheUnpayableModes()
    {
        var game = new Driver(Board()); await Exhaust(game);
        Assert.Equal([Boon], game.State.PendingCardChoice!.LegalObjectIds); await Reject(game.State, new ChooseCardsCommand(game.State.PendingCardChoice.ChoiceId, Window, [Ready]));
        var noEnemy = LocalDestructionRecallTests.At(Board(boon: true), "ENEMY", "P2", "HAND");
        var other = new Driver(noEnemy); await Exhaust(other);
        Assert.Equal([Boon, Ready], other.State.PendingCardChoice!.LegalObjectIds);
    }

    [Theory]
    [InlineData("ALLY")] [InlineData("HOST")] [InlineData("GEAR")] [InlineData("MISSING")]
    public async Task StunCannotTargetAlliesEquipmentOrInventedObjects(string target)
    {
        var game = new Driver(Board(boon: true)); await Exhaust(game); await Choose(game, Stun);
        var choice = game.State.PendingCardChoice!;
        await Reject(game.State, new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [target]));
        Assert.Contains(CardObjectTags.Boon, game.State.CardObjects["HOST"].Tags);
    }

    [Fact]
    public async Task ResponsesCannotSkipConfirmationAndDecliningDoesNotSpendTheBoonOrMode()
    {
        var state = AttackDamageDistributionTests.Spell(Board(boon: true), "RETURN", "OGN·104/298", "P1");
        var game = new Driver(state); await Exhaust(game);
        await Reject(game.State, new PlayCardCommand("RETURN", "OGN·104/298", ["HOST"]));
        var choice = game.State.PendingCardChoice!; await Reject(game.State, new ChooseCardsCommand(choice.ChoiceId, Window, []));
        await Choose(game, Ready); await Pay(game, "DECLINE");
        Assert.Contains(CardObjectTags.Boon, game.State.CardObjects["HOST"].Tags); Assert.Empty(game.State.StackItems);
        Assert.DoesNotContain(game.State.UntilEndOfTurnEffects, e => e.StartsWith(Used));
    }

    [Theory]
    [InlineData(0, false)] [InlineData(0, true)] [InlineData(1, false)]
    public async Task StunPaysTheEnemyWardUsingEligiblePowerBeforeConsumingTheBoon(int power, bool restricted)
    {
        var state = Board(boon: true); state = state with {
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) { ["ENEMY"] = state.CardObjects["ENEMY"] with { Tags = [..state.CardObjects["ENEMY"].Tags, CardObjectTags.Spellshield] } },
            RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = restricted ? new(0, 0, spellOnlyPower: 1, equipmentOnlyPower: 1) : new(0, power) } };
        var game = new Driver(state); await Exhaust(game); await Choose(game, Stun); await Choose(game, "ENEMY");
        var payment = game.State.PendingPayment!; Assert.Equal(1, payment.PowerCost);
        if (power == 0) {
            await Reject(game.State, new PayCostCommand(payment.PaymentId, payment.PaymentWindow, ["PAY"]));
            Assert.Contains(CardObjectTags.Boon, game.State.CardObjects["HOST"].Tags); await Pay(game, "DECLINE");
        } else {
            await Pay(game); Assert.Equal(0, game.State.RunePools["P1"].TotalPower);
            Assert.DoesNotContain(CardObjectTags.Boon, game.State.CardObjects["HOST"].Tags); await game.Top();
            Assert.Contains("STUNNED", game.State.CardObjects["ENEMY"].UntilEndOfTurnEffects);
        }
    }

    [Fact]
    public async Task ExhaustionPaidAsAnotherAbilityCostEarnsTheSameModeChoiceExactlyOnce()
    {
        var state = HandDiscardObserverTests.Put(Board(), "ACT", "RAD·114/167", "P1", "BASE");
        var game = new Driver(state); await game.Act("P1", new ActivateAbilityCommand("ACT", "EXHAUST-READY:0", ["HOST"], ["EXHAUST_FRIENDLY_UNIT:HOST"]));
        await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: game.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
        Assert.Equal(Window, game.State.PendingCardChoice!.ChoiceWindow);
        await Choose(game, Boon); while (game.State.StackItems.Count > 0) await game.Top();
        Assert.False(game.State.CardObjects["HOST"].IsExhausted); Assert.Equal(5, game.State.CardObjects["HOST"].Power);
        Assert.Single(game.State.UntilEndOfTurnEffects, e => e.StartsWith(Used));
    }

    [Fact]
    public async Task EachModeIsUsedOncePerTurnAndTheNextTurnRestoresTheChoices()
    {
        var state = AttackDamageDistributionTests.Spell(Board(boon: true), "READY1", "OGN·146/298", "P1");
        state = AttackDamageDistributionTests.Spell(state, "READY2", "OGN·146/298", "P1");
        var game = new Driver(state); await Exhaust(game); await Choose(game, Ready); await Pay(game); await game.Top();
        await Exhaust(game); Assert.DoesNotContain(Ready, game.State.PendingCardChoice!.LegalObjectIds);
        await Choose(game, Boon); await game.Top();
        await game.Act("P1", new PlayCardCommand("READY1", "OGN·146/298", ["HOST"])); await game.Top();
        await Exhaust(game); Assert.Equal([Stun], game.State.PendingCardChoice!.LegalObjectIds);
        await Choose(game, Stun); await Choose(game, "ENEMY"); await Pay(game); await game.Top();
        Assert.Equal(3, game.State.UntilEndOfTurnEffects.Count(e => e.StartsWith(Used)));
        // Use a second independent ready instruction to earn a fourth exhaustion.
        await game.Act("P1", new PlayCardCommand("READY2", "OGN·146/298", ["HOST"])); await game.Top();
        await Exhaust(game); Assert.Null(game.State.PendingCardChoice); Assert.Empty(game.State.StackItems);
        await game.Act("P1", new EndTurnCommand()); await game.Act("P2", new EndTurnCommand());
        Assert.DoesNotContain(game.State.UntilEndOfTurnEffects, e => e.StartsWith(Used));
        await Exhaust(game); Assert.Equal([Boon], game.State.PendingCardChoice!.LegalObjectIds);
    }

    [Fact]
    public async Task CopiedTextInstancesHaveIndependentModesAndGrantingTheSameBoonDoesNotStack()
    {
        var game = new Driver(Board(copied: true)); await Exhaust(game);
        Assert.Equal(2, game.State.TriggerQueue.Count); await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: game.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
        await Choose(game, Boon); await Choose(game, Boon); await game.Top(); await game.Top();
        Assert.Equal(5, game.State.CardObjects["HOST"].Power); Assert.Equal(2, game.State.UntilEndOfTurnEffects.Count(e => e.StartsWith(Used)));
    }

    [Fact]
    public async Task RemovingAConfirmedSkillDoesNotRefundItsModeOrConsumedBoon()
    {
        var game = new Driver(Board(boon: true)); await Exhaust(game); await Choose(game, Ready); await Pay(game);
        var removed = game.State with { StackItems = [], PriorityPlayerId = null, TimingState = TimingStates.NeutralOpen, ActivePlayerId = "P1" };
        ArmamentMightTests.PublicRecovery(removed);
        Assert.DoesNotContain(CardObjectTags.Boon, removed.CardObjects["HOST"].Tags);
        Assert.Single(removed.UntilEndOfTurnEffects, e => e.StartsWith(Used)); Assert.True(removed.CardObjects["HOST"].IsExhausted);
    }

    [Theory]
    [InlineData(Ready)] [InlineData(Stun)]
    public async Task ConsumingTheBoonRunsLethalDamageCleanupBeforeAnyResponse(string mode)
    {
        var state = Board(boon: true); state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["HOST"] = state.CardObjects["HOST"] with { Damage = 4 } } };
        var game = new Driver(state); await Exhaust(game); await Choose(game, mode);
        if (mode == Stun) await Choose(game, "ENEMY"); await Pay(game);
        Assert.Contains("HOST", game.State.PlayerZones["P1"].Graveyard); Assert.NotEmpty(game.State.StackItems);
        await game.Top(); Assert.Equal(mode == Stun, game.State.CardObjects["ENEMY"].UntilEndOfTurnEffects.Contains("STUNNED"));
    }

    [Theory]
    [InlineData(Boon)] [InlineData(Ready)] [InlineData(Stun)]
    public async Task SourceReturningToHandKeepsTheSkillWithoutExposingOrAffectingTheNewIncarnation(string mode)
    {
        var state = AttackDamageDistributionTests.Spell(Board(boon: mode != Boon), "RETURN", "OGN·104/298", "P1");
        var game = new Driver(state); await Exhaust(game); await Choose(game, mode);
        if (mode == Stun) await Choose(game, "ENEMY"); if (mode != Boon) await Pay(game);
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["HOST"])); await game.Top();
        Assert.Contains("HOST", game.State.PlayerZones["P1"].Hand);
        Assert.DoesNotContain("\"HOST\"", JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(game.State)["P2"].Stack));
        var source = game.State.CardObjects["HOST"]; var prior = game.Events().Length; await game.Top();
        Assert.Equal(source, game.State.CardObjects["HOST"]); Assert.DoesNotContain("\"HOST\"", JsonSerializer.Serialize(game.Events().Skip(prior)));
        Assert.Equal(mode == Stun, game.State.CardObjects["ENEMY"].UntilEndOfTurnEffects.Contains("STUNNED"));
    }

    [Fact]
    public async Task TheStunTargetBindingDoesNotFollowAUnitIntoHand()
    {
        var state = AttackDamageDistributionTests.Spell(Board(boon: true), "RETURN", "OGN·104/298", "P2");
        var game = new Driver(state); await Exhaust(game); await Choose(game, Stun); await Choose(game, "ENEMY"); await Pay(game);
        await game.Act("P1", new PassPriorityCommand()); await game.Act("P2", new PlayCardCommand("RETURN", "OGN·104/298", ["ENEMY"])); await game.Top();
        var enemy = game.State.CardObjects["ENEMY"]; await game.Top();
        Assert.Equal(enemy, game.State.CardObjects["ENEMY"]); Assert.Contains("ENEMY", game.State.PlayerZones["P2"].Hand);
    }

    [Fact]
    public async Task ARealCopiedTokenCanConsumeItsBoonAndKeepItsEarnedStunAfterCeasing()
    {
        var state = OfficialCopyIdentityTests.AddSpell(Board(), "MIRROR", "UNL-200/219");
        state = (await OfficialCopyIdentityTests.Copy(state, "HOST", "MIRROR")).State;
        const string token = "MIRROR-TOKEN-001";
        foreach (var id in new[] { "READY1", "READY2" }) state = AttackDamageDistributionTests.Spell(state, id, "OGN·146/298", "P1");
        state = AttackDamageDistributionTests.Spell(state, "RETURN", "OGN·104/298", "P1");
        var game = new Driver(state);
        await game.Act("P1", new PlayCardCommand("READY1", "OGN·146/298", [token])); await game.Top();
        await game.Act("P1", new MoveUnitCommand(token, "BASE", "BATTLEFIELD:F", []));
        await Choose(game, Boon); await game.Top(); Assert.Contains(CardObjectTags.Boon, game.State.CardObjects[token].Tags);
        await game.Act("P1", new PlayCardCommand("READY2", "OGN·146/298", [token])); await game.Top();
        await game.Act("P1", new MoveUnitCommand(token, "BATTLEFIELD:F", "BASE", []));
        await Choose(game, Stun); await Choose(game, "ENEMY"); await Pay(game);
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", [token])); await game.Top();
        Assert.False(game.State.CardObjects.ContainsKey(token)); Assert.True(Assert.Single(game.State.StackItems).FieldContext!.SourceCeasedToExist);
        await game.Top(); Assert.Contains("STUNNED", game.State.CardObjects["ENEMY"].UntilEndOfTurnEffects);
        Assert.Equal(2, game.State.UntilEndOfTurnEffects.Count(e => e.StartsWith(Used)));
    }

    [Fact]
    public async Task AStunStillEarnsTheSharedEnemyStunnedObservers()
    {
        var state = HandDiscardObserverTests.Put(Board(boon: true), "VANGUARD", "OGN·059/298", "P1", "BASE");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["VANGUARD"] = state.CardObjects["VANGUARD"] with { IsExhausted = true } } };
        var game = new Driver(state); await Exhaust(game); await Choose(game, Stun); await Choose(game, "ENEMY"); await Pay(game); await game.Top();
        Assert.Contains(game.State.StackItems, i => i.FieldContext?.Kind == "ENEMY_STUNNED_SOURCE_READY_POWER");
        await game.Top(); Assert.False(game.State.CardObjects["VANGUARD"].IsExhausted);
    }

    [Theory]
    [InlineData("mode")] [InlineData("confirmed")] [InlineData("unit")] [InlineData("instance")]
    [InlineData("receipt")] [InlineData("target")]
    public async Task RecoveryRejectsForgedModesCaptureFactsAndMissingConfirmationReceipts(string branch)
    {
        var game = new Driver(Board(boon: true)); await Exhaust(game); await Choose(game, Stun); await Choose(game, "ENEMY"); await Pay(game);
        var item = Assert.Single(game.State.StackItems); var context = item.FieldContext!; var birth = context.ExhaustionObserver!;
        var forged = item with { FieldContext = context with { ExhaustionObserver = branch switch {
            "mode" => birth with { Mode = Ready }, "confirmed" => birth with { Confirmed = false },
            "unit" => birth with { Units = [birth.Units[0] with { Unit = new("ALLY", 0) }] },
            "instance" => birth with { InstanceId = "FORGED" }, _ => birth } } };
        if (branch == "target") forged = forged with { TargetObjectIds = ["ALLY"], TargetGenerations = new Dictionary<string, long> { ["ALLY"] = 0 } };
        var s = game.State with { StackItems = [forged], UntilEndOfTurnEffects = branch == "receipt" ? [] : game.State.UntilEndOfTurnEffects };
        Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(s));
    }

    private static async Task Reject(MatchState state, GameCommand command)
    {
        var hash = MatchStateHasher.Hash(state); var snapshots = JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(state));
        var prompts = JsonSerializer.Serialize(ResolutionResult.BuildPrompts(state));
        var result = await new CoreRuleEngine().ResolveAsync(state, new("invalid-exhaustion-mode", "P1", command.CmdType), command, default);
        Assert.False(result.Accepted); Assert.Empty(result.Events); Assert.Equal(hash, MatchStateHasher.Hash(result.State));
        Assert.Equal(snapshots, JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(result.State))); Assert.Equal(prompts, JsonSerializer.Serialize(ResolutionResult.BuildPrompts(result.State)));
    }
}
