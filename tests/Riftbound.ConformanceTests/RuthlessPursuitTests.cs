using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class RuthlessPursuitTests
{
    internal const string Card = "SFD·184/221";

    [Theory]
    [InlineData("BASE", "BATTLEFIELD:F")]
    [InlineData("BATTLEFIELD:F", "BASE:P1")]
    [InlineData("BATTLEFIELD:F", "BATTLEFIELD:OTHER")]
    public async Task ActualSpellMovesTheFriendlyUnitToItsDeclaredLocationAndGrantsConquestReturn(string origin, string destination)
    {
        var initial = Position(origin);
        var command = new PlayCardCommand("PURSUIT", Card, ["HOST"], Destination: destination);
        var prompt = ResolutionResult.BuildPrompts(initial)["P1"];
        var quote = new CoreRuleEngine().PreviewPlayCard(initial, "P1", new("quote", prompt.PromptId!, initial.Tick, command));
        Assert.True(quote.IsValid, quote.Message);
        var driver = Driver(initial, "move-" + origin.Replace(':', '-') + "-" + destination.Replace(':', '-'));
        await driver.Act("P1", command);
        await driver.Top();
        Assert.Equal(destination.StartsWith("BASE:") ? "BASE" : "BATTLEFIELD", driver.State.ObjectLocations["HOST"].Zone);
        Assert.Equal(destination.StartsWith("BASE:") ? null : destination[12..], driver.State.ObjectLocations["HOST"].BattlefieldObjectId);
        Assert.Contains("PURSUIT", driver.State.PlayerZones["P1"].Graveyard);
        Assert.Equal("GRAVEYARD", driver.State.ObjectLocations["PURSUIT"].Zone);
        Assert.True(driver.State.CardObjects["HOST"].IsExhausted);
        Assert.Equal(1, driver.State.CardObjects["HOST"].MovementCountThisTurn);
        Assert.Contains(driver.State.CardObjects["HOST"].UntilEndOfTurnEffects, effect => effect.StartsWith("GRANTED_CONQUEST_RETURN_BASE|", StringComparison.Ordinal));
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DeclaredEquipmentWaitsForThePostMovementOptionalDecision(bool attach)
    {
        var initial = Position();
        var driver = Driver(initial, attach ? "attach" : "decline-attach");
        await driver.Act("P1", new PlayCardCommand("PURSUIT", Card, ["HOST", "GEAR"], Destination: "BATTLEFIELD:F"));
        Assert.Equal(18, driver.State.RunePools["P1"].Mana);
        Assert.Equal(19, driver.State.RunePools["P1"].TotalPower);
        var moved = await driver.Top();
        Assert.Equal("F", driver.State.ObjectLocations["HOST"].BattlefieldObjectId);
        Assert.Null(driver.State.CardObjects["GEAR"].AttachedToObjectId);
        Assert.DoesNotContain(driver.State.CardObjects["HOST"].UntilEndOfTurnEffects, e => e.StartsWith("GRANTED_CONQUEST_RETURN_BASE|"));
        var choice = Assert.IsType<PendingCardChoiceState>(driver.State.PendingCardChoice);
        Assert.Equal("MOVEMENT_OPTIONAL_ATTACHMENT", choice.ChoiceWindow);
        Assert.Equal(0, choice.RequiredCount);
        Assert.Equal(["GEAR"], choice.LegalObjectIds);
        Assert.Null(driver.State.PriorityPlayerId);
        Assert.Equal("STACK", driver.State.ObjectLocations["PURSUIT"].Zone);
        Assert.Contains(moved.Events, e => e.Kind == "UNIT_MOVED_TO_BATTLEFIELD");
        await driver.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, attach ? ["GEAR"] : []));
        Assert.Null(driver.State.PendingCardChoice);
        Assert.Contains("PURSUIT", driver.State.PlayerZones["P1"].Graveyard);
        Assert.Equal(attach ? "HOST" : null, driver.State.CardObjects["GEAR"].AttachedToObjectId);
        Assert.Equal(attach ? "F" : null, driver.State.ObjectLocations["GEAR"].BattlefieldObjectId);
        Assert.Single(driver.State.CardObjects["HOST"].UntilEndOfTurnEffects, e => e.StartsWith("GRANTED_CONQUEST_RETURN_BASE|"));
        Assert.Equal(1, driver.State.CardObjects["HOST"].MovementCountThisTurn);
        Assert.Equal(18, driver.State.RunePools["P1"].Mana);
        Assert.Equal(19, driver.State.RunePools["P1"].TotalPower);
        await Replay(initial, driver);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AProhibitedMoveSkipsItsLinkedAttachmentAndGrant(bool temporary)
    {
        var initial = Position("BATTLEFIELD:F");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["F"] = initial.CardObjects["F"] with { CardNo = temporary ? initial.CardObjects["F"].CardNo : "OGN·295/298" },
            ["HOST"] = initial.CardObjects["HOST"] with { UntilEndOfTurnEffects = temporary ? [MovementRestrictionRules.ForPlayer("P1")] : [] } } };
        var driver = Driver(initial, temporary ? "temporary-prohibition" : "vilemaw-prohibition");
        await driver.Act("P1", new PlayCardCommand("PURSUIT", Card, ["HOST", "GEAR"], Destination: "BASE:P1"));
        var resolved = await driver.Top();
        Assert.Equal("F", driver.State.ObjectLocations["HOST"].BattlefieldObjectId);
        Assert.Null(driver.State.PendingCardChoice);
        Assert.DoesNotContain(resolved.Events, e => e.Kind is "UNIT_MOVED_TO_BASE" or "EQUIPMENT_ATTACHED" or "TEMPORARY_CONQUEST_ABILITY_GRANTED");
        Assert.Equal(0, driver.State.CardObjects["HOST"].MovementCountThisTurn);
        await Replay(initial, driver);
    }

    [Fact]
    public async Task TwoEchoExecutionsKeepDifferentDestinationsAndIndependentGrantedAbilities()
    {
        var initial = Position("BATTLEFIELD:F");
        initial = initial with { UntilEndOfTurnEffects = [EchoCostRules.GrantPrefix + "P1"] };
        var driver = Driver(initial, "echo-base-then-other");
        await driver.Act("P1", new PlayCardCommand("PURSUIT", Card, ["HOST"], Destination: "BASE:P1", OptionalCosts: ["ECHO"],
            RepeatChoices: [new("", ["HOST"], Destination: "BATTLEFIELD:OTHER")]));
        await driver.Top();
        var grants = driver.State.CardObjects["HOST"].UntilEndOfTurnEffects.Where(e => e.StartsWith("GRANTED_CONQUEST_RETURN_BASE|")).ToArray();
        Assert.Equal(2, grants.Length);
        Assert.Equal(2, grants.Distinct().Count());
        Assert.Equal(2, driver.State.CardObjects["HOST"].MovementCountThisTurn);
        Assert.Equal("OTHER", driver.State.ObjectLocations["HOST"].BattlefieldObjectId);
        await Replay(initial, driver);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RealUnopposedConquestOffersTheGrantedOptionalReturnAndWaitsForResponses(bool returnToBase)
    {
        var initial = Position();
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["HOST"] = initial.CardObjects["HOST"] with { IsExhausted = false } } };
        var driver = Driver(initial, returnToBase ? "conquest-return" : "conquest-decline");
        await driver.Act("P1", new PlayCardCommand("PURSUIT", Card, ["HOST"], Destination: "BATTLEFIELD:OTHER"));
        await driver.Top();
        for (var i = 0; i < 4 && driver.State.PendingCardChoice is null; i++)
            await driver.Act(driver.State.FocusPlayerId!, new PassFocusCommand());
        var choice = Assert.IsType<PendingCardChoiceState>(driver.State.PendingCardChoice);
        Assert.Equal("UNIT_GRANTED_CONQUEST_RETURN_BASE", choice.EffectKind);
        Assert.Equal(0, choice.RequiredCount);
        await driver.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, returnToBase ? ["HOST"] : []));
        Assert.Equal("OTHER", driver.State.ObjectLocations["HOST"].BattlefieldObjectId);
        if (returnToBase) await driver.Top();
        Assert.Equal(returnToBase ? "BASE" : "BATTLEFIELD", driver.State.ObjectLocations["HOST"].Zone);
        Assert.Equal(returnToBase ? 2 : 1, driver.State.CardObjects["HOST"].MovementCountThisTurn);
        await Replay(initial, driver);
    }

    [Theory]
    [InlineData("foreign")]
    [InlineData("same-host")]
    [InlineData("not-equipment")]
    [InlineData("non-weapon")]
    public async Task OptionalEquipmentChoicesUseActualControllerAndEquipmentType(string branch)
    {
        var initial = Position();
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["GEAR"] = initial.CardObjects["GEAR"] with {
                ControllerId = branch == "foreign" ? "P2" : "P1",
                AttachedToObjectId = branch == "same-host" ? "HOST" : null,
                CardNo = branch == "non-weapon" ? "OGN·077/298" : initial.CardObjects["GEAR"].CardNo,
                Tags = branch == "not-equipment" ? [CardObjectTags.UnitCard]
                    : branch == "non-weapon" ? [CardObjectTags.EquipmentCard] : initial.CardObjects["GEAR"].Tags } } };
        var command = new PlayCardCommand("PURSUIT", Card, ["HOST", "GEAR"], Destination: "BATTLEFIELD:F");
        var prompt = ResolutionResult.BuildPrompts(initial)["P1"];
        var engine = new CoreRuleEngine();
        var quote = engine.PreviewPlayCard(initial, "P1", new("quote", prompt.PromptId!, initial.Tick, command));
        if (branch == "same-host")
        {
            Assert.True(quote.IsValid, quote.Message);
            var driver = Driver(initial, "already-attached");
            await driver.Act("P1", command);
            await driver.Top();
            var choice = driver.State.PendingCardChoice!;
            var done = await driver.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["GEAR"]));
            Assert.DoesNotContain(done.Events, e => e.Kind == "EQUIPMENT_ATTACHED");
            Assert.Equal("F", driver.State.ObjectLocations["GEAR"].BattlefieldObjectId);
            Assert.Single(driver.State.CardObjects["HOST"].UntilEndOfTurnEffects, e => e.StartsWith("GRANTED_CONQUEST_RETURN_BASE|"));
            await Replay(initial, driver);
            return;
        }
        Assert.False(quote.IsValid);
        var rejected = await engine.ResolveAsync(initial, new("bad", "P1", command.CmdType), command, default);
        Assert.False(rejected.Accepted);
        Assert.Empty(rejected.Events);
        Assert.Equal(MatchStateHasher.Hash(initial), MatchStateHasher.Hash(rejected.State));
    }

    [Theory]
    [InlineData("turn")]
    [InlineData("generation")]
    [InlineData("duplicate")]
    [InlineData("source")]
    [InlineData("granting-card")]
    [InlineData("recipient")]
    [InlineData("stack-instance")]
    [InlineData("short-stack")]
    [InlineData("future-stack")]
    public async Task RecoveryRejectsIndependentlyForgedTemporaryAbilityProvenance(string mutation)
    {
        var driver = Driver(Position(), "tamper-" + mutation);
        await driver.Act("P1", new PlayCardCommand("PURSUIT", Card, ["HOST"], Destination: "BATTLEFIELD:F"));
        await driver.Top();
        var state = driver.State; var effects = state.CardObjects["HOST"].UntilEndOfTurnEffects;
        var grant = Assert.Single(effects, e => e.StartsWith("GRANTED_CONQUEST_RETURN_BASE|"));
        var fields = grant.Split('|');
        if (mutation == "turn") fields[1] = (state.TurnNumber + 1).ToString();
        if (mutation == "generation") fields[6] = "999999";
        if (mutation == "source") fields[2] = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("FORGED"));
        if (mutation == "granting-card") fields[4] = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("OGN·173/298"));
        if (mutation == "recipient") fields[8] = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("GEAR"));
        if (mutation == "stack-instance") fields[5] = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("FORGED"));
        if (mutation == "short-stack") fields[5] = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("STACK-PURSUIT"));
        if (mutation == "future-stack") fields[5] = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("STACK-999999-PURSUIT"));
        var altered = mutation == "duplicate" ? effects.Append(grant).ToArray()
            : effects.Select(e => e == grant ? string.Join('|', fields) : e).ToArray();
        var bad = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["HOST"] = state.CardObjects["HOST"] with { UntilEndOfTurnEffects = altered } } };
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(bad), e => e.Contains("temporary conquest grant"));
    }

    [Fact]
    public async Task ARealReturnResponseInvalidatesTheUnitAndBothLinkedFollowUps()
    {
        var initial = AttackDamageDistributionTests.Spell(Position(), "RETURN", "OGN·104/298", "P1");
        var driver = Driver(initial, "return-before-resolution");
        await driver.Act("P1", new PlayCardCommand("PURSUIT", Card, ["HOST", "GEAR"], Destination: "BATTLEFIELD:F"));
        await driver.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["HOST"]));
        await driver.Top();
        var result = await driver.Top();
        Assert.Equal("HAND", driver.State.ObjectLocations["HOST"].Zone);
        Assert.Null(driver.State.CardObjects["GEAR"].AttachedToObjectId);
        Assert.Null(driver.State.PendingCardChoice);
        Assert.DoesNotContain(result.Events, e => e.Kind is "UNIT_MOVED_TO_BATTLEFIELD" or "TEMPORARY_CONQUEST_ABILITY_GRANTED" or "EQUIPMENT_ATTACHED");
        Assert.Contains("PURSUIT", driver.State.PlayerZones["P1"].Graveyard);
        await Replay(initial, driver);
    }

    [Fact]
    public async Task EchoCanPauseAtTwoIndependentAttachmentDecisionsWithoutRepeatingEitherMovement()
    {
        var initial = Position("BATTLEFIELD:F");
        initial = initial with { UntilEndOfTurnEffects = [EchoCostRules.GrantPrefix + "P1"] };
        var driver = Driver(initial, "echo-two-attachments");
        await driver.Act("P1", new PlayCardCommand("PURSUIT", Card, ["HOST", "GEAR"], Destination: "BASE:P1", OptionalCosts: ["ECHO"],
            RepeatChoices: [new("", ["HOST", "GEAR"], Destination: "BATTLEFIELD:OTHER")]));
        await driver.Top();
        Assert.Equal("BASE", driver.State.ObjectLocations["HOST"].Zone);
        var first = driver.State.PendingCardChoice!;
        await driver.Act("P1", new ChooseCardsCommand(first.ChoiceId, first.ChoiceWindow, []));
        Assert.Equal("OTHER", driver.State.ObjectLocations["HOST"].BattlefieldObjectId);
        Assert.Equal(2, driver.State.CardObjects["HOST"].MovementCountThisTurn);
        Assert.Single(driver.State.CardObjects["HOST"].UntilEndOfTurnEffects, e => e.StartsWith("GRANTED_CONQUEST_RETURN_BASE|"));
        var second = driver.State.PendingCardChoice!;
        await driver.Act("P1", new ChooseCardsCommand(second.ChoiceId, second.ChoiceWindow, ["GEAR"]));
        Assert.Equal("HOST", driver.State.CardObjects["GEAR"].AttachedToObjectId);
        Assert.Equal("OTHER", driver.State.ObjectLocations["GEAR"].BattlefieldObjectId);
        Assert.Equal(2, driver.State.CardObjects["HOST"].UntilEndOfTurnEffects.Count(e => e.StartsWith("GRANTED_CONQUEST_RETURN_BASE|")));
        Assert.Equal(2, driver.State.CardObjects["HOST"].MovementCountThisTurn);
        Assert.Contains("PURSUIT", driver.State.PlayerZones["P1"].Graveyard);
        await Replay(initial, driver);
    }

    [Fact]
    public async Task TheGrantedAbilityExpiresAtTheActualTurnEnd()
    {
        var initial = Position();
        var driver = Driver(initial, "expires");
        await driver.Act("P1", new PlayCardCommand("PURSUIT", Card, ["HOST"], Destination: "BATTLEFIELD:F"));
        await driver.Top();
        Assert.Single(driver.State.CardObjects["HOST"].UntilEndOfTurnEffects, e => e.StartsWith("GRANTED_CONQUEST_RETURN_BASE|"));
        await driver.Act("P1", new EndTurnCommand());
        Assert.DoesNotContain(driver.State.CardObjects["HOST"].UntilEndOfTurnEffects, e => e.StartsWith("GRANTED_CONQUEST_RETURN_BASE|"));
        Assert.True(driver.State.TurnNumber > initial.TurnNumber);
        await Replay(initial, driver);
    }

    [Fact]
    public async Task AReturnResponseAfterConquestLeavesTheCapturedAbilityButInvalidatesItsOldUnit()
    {
        var initial = AttackDamageDistributionTests.Spell(Position(), "RETURN", "OGN·104/298", "P1");
        var driver = Driver(initial, "captured-return-response");
        await driver.Act("P1", new PlayCardCommand("PURSUIT", Card, ["HOST"], Destination: "BATTLEFIELD:OTHER"));
        await driver.Top();
        for (var i = 0; i < 4 && driver.State.PendingCardChoice is null; i++)
            await driver.Act(driver.State.FocusPlayerId!, new PassFocusCommand());
        var choice = driver.State.PendingCardChoice!;
        await driver.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["HOST"]));
        await driver.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["HOST"]));
        await driver.Top();
        Assert.Contains(driver.State.StackItems, i => i.EffectKind == "UNIT_GRANTED_CONQUEST_RETURN_BASE");
        Assert.Equal("HAND", driver.State.ObjectLocations["HOST"].Zone);
        Assert.DoesNotContain(driver.State.CardObjects["HOST"].UntilEndOfTurnEffects, e => e.StartsWith("GRANTED_CONQUEST_RETURN_BASE|"));
        var finished = await driver.Top();
        Assert.Equal("HAND", driver.State.ObjectLocations["HOST"].Zone);
        Assert.DoesNotContain(finished.Events, e => e.Kind == "UNIT_MOVED_TO_BASE");
        await Replay(initial, driver);
    }

    [Fact]
    public async Task ActualCombatVictoryCapturesAndResolvesTheGrantedConquestReturn()
    {
        var initial = Position();
        Assert.True(CardBehaviorRegistry.TryGetByCardNo("OGN·001/298", out var attacker));
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["HOST"] = initial.CardObjects["HOST"] with { CardNo = attacker.CardNo, Power = attacker.SourceUnitPower, IsExhausted = false } } };
        initial = OfficialHoldSequenceTests.AddUnit(initial, "DEFENDER", "OGN·096/298", "OTHER", "P2");
        var driver = Driver(initial, "combat-conquest");
        await driver.Act("P1", new PlayCardCommand("PURSUIT", Card, ["HOST"], Destination: "BATTLEFIELD:OTHER"));
        await driver.Top();
        Assert.True(driver.State.BattleState.IsActive);
        for (var i = 0; i < 30 && driver.State.PendingCardChoice?.EffectKind != "UNIT_GRANTED_CONQUEST_RETURN_BASE"; i++)
        {
            var prompts = ResolutionResult.BuildPrompts(driver.State);
            var assignment = prompts.Values.FirstOrDefault(p => p.Candidates?.Any(c => c.Action == CommandTypes.AssignCombatDamage && c.Enabled) == true);
            if (assignment is not null)
                await driver.Act(assignment.PlayerId, new AssignCombatDamageCommand(driver.State.BattleState.BattleId!,
                    driver.State.BattleState.BattlefieldObjectId!, CombatTestDriver.Assignments(assignment)));
            else if (driver.State.PriorityPlayerId is { } priority)
                await driver.Act(priority, new PassPriorityCommand());
            else if (driver.State.FocusPlayerId is { } focus)
                await driver.Act(focus, new PassFocusCommand());
            else
            {
                var ordering = prompts.Values.FirstOrDefault(p => p.Candidates?.Any(c => c.Action == CommandTypes.OrderTriggers && c.Enabled) == true);
                if (ordering is not null)
                {
                    await driver.Act(ordering.PlayerId, new OrderTriggersCommand(OrderedTriggerIds: driver.State.TriggerQueue
                        .OrderBy(t => t.ControllerId == "P2" ? 0 : 1).Select(t => t.TriggerId).ToArray()));
                    continue;
                }
                var declaration = prompts.Values.FirstOrDefault(p => p.Candidates?.Any(c => c.Action == CommandTypes.DeclareBattle && c.Enabled) == true);
                Assert.True(declaration is not null, $"No combat continuation: {driver.State.Phase}/{driver.State.TimingState}; " +
                    string.Join(";", prompts.Values.Select(p => p.PlayerId + ":" + string.Join(",", p.Actions))));
                var actor = declaration!.PlayerId;
                var battle = driver.State.BattleState;
                await driver.Act(actor, new DeclareBattleCommand(battle.BattlefieldObjectId!, battle.AttackerObjectIds,
                    battle.DefenderObjectIds, ["COMBAT_ASSIGNMENT"]));
            }
        }
        Assert.Contains("DEFENDER", driver.State.PlayerZones["P2"].Graveyard);
        Assert.Equal(1, driver.State.PlayerScores["P1"]);
        var choice = Assert.IsType<PendingCardChoiceState>(driver.State.PendingCardChoice);
        Assert.Equal("UNIT_GRANTED_CONQUEST_RETURN_BASE", choice.EffectKind);
        await driver.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["HOST"]));
        for (var i = 0; i < 3 && driver.State.StackItems.Any(s => s.EffectKind == "UNIT_GRANTED_CONQUEST_RETURN_BASE"); i++)
            await driver.Top();
        Assert.Equal("BASE", driver.State.ObjectLocations["HOST"].Zone);
        Assert.Equal(2, driver.State.CardObjects["HOST"].MovementCountThisTurn);
        await Replay(initial, driver);
    }

    [Fact]
    public async Task TwoGrantedInstancesTriggerIndependentlyAndASecondReturnDoesNotMoveAnAlreadyBasedUnit()
    {
        var initial = Position("BATTLEFIELD:F");
        initial = initial with { UntilEndOfTurnEffects = [EchoCostRules.GrantPrefix + "P1"] };
        var driver = Driver(initial, "echo-two-conquest-triggers");
        await driver.Act("P1", new PlayCardCommand("PURSUIT", Card, ["HOST"], Destination: "BASE:P1", OptionalCosts: ["ECHO"],
            RepeatChoices: [new("", ["HOST"], Destination: "BATTLEFIELD:OTHER")]));
        await driver.Top();
        for (var i = 0; i < 4 && driver.State.TriggerQueue.Count < 2; i++)
            await driver.Act(driver.State.FocusPlayerId!, new PassFocusCommand());
        Assert.Equal(2, driver.State.TriggerQueue.Count(t => t.EffectKind == "UNIT_GRANTED_CONQUEST_RETURN_BASE"));
        await driver.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: driver.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
        for (var i = 0; i < 2; i++)
        {
            var choice = Assert.IsType<PendingCardChoiceState>(driver.State.PendingCardChoice);
            await driver.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["HOST"]));
        }
        Assert.Equal(2, driver.State.StackItems.Count(i => i.EffectKind == "UNIT_GRANTED_CONQUEST_RETURN_BASE"));
        for (var i = 0; i < 2; i++)
        {
            var result = await driver.Top();
            if (i == 1) Assert.DoesNotContain(result.Events, e => e.Kind == "UNIT_MOVED_TO_BASE");
        }
        Assert.Equal("BASE", driver.State.ObjectLocations["HOST"].Zone);
        Assert.Equal(3, driver.State.CardObjects["HOST"].MovementCountThisTurn);
        await Replay(initial, driver);
    }

    [Theory]
    [InlineData("unit-generation")]
    [InlineData("gear-generation")]
    [InlineData("execution-index")]
    [InlineData("premature-decision")]
    [InlineData("destination")]
    [InlineData("choice-source")]
    public async Task RecoveryRejectsForgedPostMovementAttachmentContinuations(string mutation)
    {
        var driver = new StandbyDefendObserverTests.Driver(Position());
        await driver.Act("P1", new PlayCardCommand("PURSUIT", Card, ["HOST", "GEAR"], Destination: "BATTLEFIELD:F"));
        await driver.Top();
        var state = driver.State; var item = Assert.Single(state.StackItems); var context = item.MovementFollowUp!;
        var forged = mutation switch {
            "unit-generation" => item with { MovementFollowUp = context with { Unit = new("HOST", 999999) } },
            "gear-generation" => item with { MovementFollowUp = context with { Equipment = new("GEAR", 999999) } },
            "execution-index" => item with { MovementFollowUp = context with { ExecutionIndex = 1 } },
            "premature-decision" => item with { MovementFollowUp = context with { AttachmentChosen = true } },
            "destination" => item with { Destination = "BATTLEFIELD:OTHER" },
            _ => item
        };
        var bad = state with { StackItems = [forged], PendingCardChoice = mutation == "choice-source"
            ? state.PendingCardChoice! with { SourceObjectId = "FORGED" } : state.PendingCardChoice };
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(bad), e => e.Contains("movement optional attachment continuation"));
    }

    [Theory]
    [InlineData("UNL-029/219")]
    [InlineData("UNL-029a/219")]
    public async Task ARealGroupedConquestAmplifiesTheGrantedAbilityThroughTheSharedRepeatRule(string card)
    {
        var initial = OfficialHoldSequenceTests.AddUnit(Position(), "BRAMBLE", card, "F", "P1");
        initial = LocalDestructionRecallTests.At(initial, "BRAMBLE", "P1", "BASE");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["BRAMBLE"] = initial.CardObjects["BRAMBLE"] with { IsExhausted = false } } };
        var driver = Driver(initial, card.Contains('a') ? "amplifier-alt" : "amplifier-base");
        await driver.Act("P1", new PlayCardCommand("PURSUIT", Card, ["HOST"], Destination: "BATTLEFIELD:F"));
        await driver.Top();
        await driver.Act("P1", new PlayCardCommand("WIND", "OGN·173/298", ["HOST"], Destination: "BASE:P1"));
        await driver.Top();
        await driver.Act("P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:OTHER", [], SourceObjectIds: ["HOST", "BRAMBLE"]));
        await driver.Act(driver.State.FocusPlayerId!, new PassFocusCommand());
        await driver.Act(driver.State.FocusPlayerId!, new PassFocusCommand());
        var triggers = driver.State.TriggerQueue.Where(t => t.EffectKind == "UNIT_GRANTED_CONQUEST_RETURN_BASE").ToArray();
        Assert.Equal(2, triggers.Length);
        Assert.Equal(2, triggers.Select(t => t.TriggerId).Distinct().Count());
        Assert.Equal(triggers[0].UnitConquest!.TemporaryGrant, triggers[1].UnitConquest!.TemporaryGrant);
        await driver.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: driver.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
        for (var i = 0; i < 2; i++)
        {
            var choice = Assert.IsType<PendingCardChoiceState>(driver.State.PendingCardChoice);
            await driver.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, []));
        }
        Assert.DoesNotContain(driver.State.StackItems, i => i.EffectKind == "UNIT_GRANTED_CONQUEST_RETURN_BASE");
        await Replay(initial, driver);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ARealSpellControlChangeKeepsTheOriginalBaseDeclarationReceiptWhenChoicesAreKept(bool friendly)
    {
        var spell = friendly ? Card : "OGN·258/298";
        var initial = RebuttalRulesTests.Position(spell, friendly ? Position("BATTLEFIELD:F") : EchoSingleDestinationTests.Position());
        initial = initial with { UntilEndOfTurnEffects = [EchoCostRules.GrantPrefix + "P1"] };
        var target = friendly ? "HOST" : "D";
        var driver = Driver(initial, friendly ? "kept-friendly-declaration" : "kept-enemy-declaration");
        await driver.Act("P1", new PlayCardCommand("R-SPELL", spell, [target], Destination: friendly ? "BASE:P1" : "BASE:P2",
            OptionalCosts: ["ECHO"], RepeatChoices: [new("", [friendly ? "HOST" : "D2"],
                Destination: friendly ? "BATTLEFIELD:OTHER" : "BATTLEFIELD:BF2")]));
        var original = Assert.Single(driver.State.StackItems).StackItemId;
        await driver.Act("P1", new PassPriorityCommand());
        await driver.Act("P2", new PlayCardCommand("REBUTTAL", RebuttalRulesTests.Card, [original]));
        await driver.Top();
        var payment = driver.State.PendingRuleChoice!.Request;
        await driver.Act("P2", new PayCostCommand(payment.Id, "RULE_REPLACEMENT", ["PAY:ANY"]));
        var remake = driver.State.PendingRuleChoice!.Request;
        await driver.Act("P2", new PayCostCommand(remake.Id, "RULE_REPLACEMENT", ["KEEP_ALL"]));
        var controlled = Assert.Single(driver.State.StackItems);
        Assert.Equal("P2", controlled.ControllerId);
        Assert.All(controlled.RepeatExecutions!, e => Assert.Equal("P1", e.MovementDeclarationPlayerId));
        var finished = await driver.Top();
        Assert.DoesNotContain(finished.Events, e => e.Kind is "UNIT_MOVED_TO_BASE" or "UNIT_MOVED_TO_BATTLEFIELD" or "TEMPORARY_CONQUEST_ABILITY_GRANTED");
        await Replay(initial, driver);
    }

    internal static MatchState Position(string origin = "BASE")
    {
        var s = RideTheWindCurrentMovementTests.Position(origin);
        return AttackDamageDistributionTests.Spell(s, "PURSUIT", Card, "P1");
    }

    internal static StandbyDefendObserverTests.Driver Driver(MatchState state, string branch)
    {
        var root = Environment.GetEnvironmentVariable("RIFTBOUND_PURSUIT_EVIDENCE");
        return new(state, string.IsNullOrWhiteSpace(root) ? null : Path.Combine(root, branch));
    }
    internal static Task Replay(MatchState initial, StandbyDefendObserverTests.Driver driver)
        => RideTheWindCurrentMovementTests.Replay(initial, driver);
}
