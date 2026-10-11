using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.ArmamentMightTests;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;

namespace Riftbound.ConformanceTests;

public sealed class TemperedPlayTriggerAuditTests
{
    internal static MatchState PositionFor(string unit = "SFD·008/221", string gear = "SFD·022/221")
    {
        var s = Position(gear: gear, unit: unit, power: 2);
        return s with { PlayerZones = new Dictionary<string, PlayerZones>(s.PlayerZones) {
            ["P1"] = s.PlayerZones["P1"] with { Base = s.PlayerZones["P1"].Base.Where(id => id != "HOST").ToArray(), Hand = ["HOST"] }
        }, ObjectLocations = new Dictionary<string, ObjectLocationState>(s.ObjectLocations) {
            ["HOST"] = new("P1", "HAND")
        }, RunePools = new Dictionary<string, RunePool>(s.RunePools) {
            ["P1"] = new(20, 10, new Dictionary<string, int> { [RuneTrait.Red] = 5, [RuneTrait.Green] = 5 })
        }};
    }

    [Theory]
    [InlineData("SFD·008/221")]
    [InlineData("SFD·119/221")]
    public async Task PlayingWithoutPreselectedEquipmentStillCapturesAnIndependentOptionalTrigger(string card)
    {
        var played = await Act(PositionFor(card), "P1", new PlayCardCommand("HOST", card, []));
        Assert.Null(played.State.CardObjects["GEAR"].AttachedToObjectId);
        var done = played;
        Assert.Contains("HOST", done.State.PlayerZones["P1"].Base);
        Assert.Null(done.State.CardObjects["GEAR"].AttachedToObjectId);
        Assert.NotNull(done.State.PendingCardChoice);
        Assert.Contains("GEAR", done.State.PendingCardChoice!.LegalObjectIds);
        Assert.Single(done.State.StackItems);
        Assert.NotNull(done.State.StackItems[0].Tempered);
        Assert.Contains(done.Events, e => e.Kind == "TRIGGER_QUEUED");
    }

    [Theory]
    [InlineData("SFD·008/221")]
    [InlineData("SFD·119/221")]
    public void EquipmentIsChosenForTheTriggerAfterPlayRatherThanAsAPlayCost(string card)
    {
        var prompt = ResolutionResult.BuildPrompts(PositionFor(card))["P1"];
        var candidate = Assert.Single(prompt.Candidates ?? [], c => c.Action == CommandTypes.PlayCard);
        var metadata = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(candidate.Metadata);
        var requirement = Assert.Single(Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(metadata["sourceRequirements"]),
            r => (string?)r["sourceObjectId"] == "HOST");
        Assert.DoesNotContain(Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(requirement["optionalCostChoices"]),
            c => c.Id.StartsWith("TEMPERED_ATTACH:", StringComparison.Ordinal));
    }

    internal static async Task<ResolutionResult> Confirm(MatchState state, string gear = "GEAR")
    {
        var played = await Act(state, "P1", new PlayCardCommand("HOST", state.CardObjects["HOST"].CardNo!, []));
        var choice = Assert.IsType<PendingCardChoiceState>(played.State.PendingCardChoice);
        Assert.Equal("TEMPERED_CONFIRMATION", choice.ChoiceWindow);
        Restore(played.State); PublicRecovery(played.State);
        var selected = await Act(played.State, "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [gear]));
        Assert.Null(selected.State.PendingCardChoice);
        Assert.Equal(new[] { gear }, Assert.Single(selected.State.StackItems).TargetObjectIds);
        Restore(selected.State); PublicRecovery(selected.State);
        return selected;
    }

    [Theory]
    [InlineData("SFD·022/221", 1, 0)]
    [InlineData("SFD·186/221", 0, 0)]
    [InlineData("SFD·056/221", 0, 0)]
    public async Task ReferencedEquipCostReducesOnlyGenericPowerAndIsPaidAfterResponses(string gear, int red, int generic)
    {
        var initial = PositionFor(gear: gear);
        var selected = await Confirm(initial);
        Assert.Equal(5, selected.State.RunePools["P1"].PowerByTrait[RuneTrait.Red]);
        Assert.Equal(10, selected.State.RunePools["P1"].Power);
        Assert.Null(selected.State.CardObjects["GEAR"].AttachedToObjectId);
        var resolved = await Top(selected.State);
        Assert.Equal("HOST", resolved.State.CardObjects["GEAR"].AttachedToObjectId);
        Assert.Equal(5 - red, resolved.State.RunePools["P1"].PowerByTrait[RuneTrait.Red]);
        Assert.Equal(10 - generic, resolved.State.RunePools["P1"].Power);
        Assert.Equal(gear == "SFD·056/221" ? 4 : 5, resolved.State.RunePools["P1"].PowerByTrait[RuneTrait.Green]);
        Assert.Contains(resolved.Events, e => e.Kind == "COST_PAID");
        Assert.DoesNotContain(resolved.Events, e => e.Kind == "STACK_ITEM_ADDED");
        Restore(resolved.State); PublicRecovery(resolved.State);
    }

    [Fact]
    public async Task DynamicManaDiscountReferencesThePlayedUnitsCurrentMight()
    {
        var selected = await Confirm(PositionFor(gear: "UNL-188/219"));
        // A real response-like change to current Might is measured at resolution,
        // rather than the earlier HAND fixture or a captured raw base value.
        var state = selected.State with { CardObjects = new Dictionary<string, CardObjectState>(selected.State.CardObjects) {
            ["HOST"] = selected.State.CardObjects["HOST"] with { Power = 1 }
        }};
        var mana = state.RunePools["P1"].Mana;
        var resolved = await Top(state);
        Assert.Equal(mana - 2, resolved.State.RunePools["P1"].Mana);
        Assert.Equal(10, resolved.State.RunePools["P1"].Power);
        Assert.Equal("HOST", resolved.State.CardObjects["GEAR"].AttachedToObjectId);
        Restore(resolved.State); PublicRecovery(resolved.State);
    }

    [Fact]
    public async Task ActualTakeUpResponseChangesTheMightUsedByTheReferencedEquipCost()
    {
        var initial = PositionFor("SFD·099/221", "UNL-188/219");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["BONUS"] = new("BONUS", cardNo: "SFD·022/221", ownerId: "P1", controllerId: "P1", tags: [CardObjectTags.EquipmentCard, CardEquipmentKeywordNames.Weapon]),
            ["RESPONSE"] = new("RESPONSE", cardNo: "SFD·011/221", ownerId: "P1", controllerId: "P1", tags: [CardObjectTags.SpellCard])
        }, PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
            ["P1"] = initial.PlayerZones["P1"] with { Base = initial.PlayerZones["P1"].Base.Append("BONUS").ToArray(),
                Hand = initial.PlayerZones["P1"].Hand.Append("RESPONSE").ToArray() }
        }, ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) {
            ["BONUS"] = new("P1", "BASE"), ["RESPONSE"] = new("P1", "HAND")
        }};
        var selected = await Confirm(initial);
        Assert.Equal(2, Might(selected.State));
        var response = await Act(selected.State, "P1", new PlayCardCommand("RESPONSE", "SFD·011/221", ["HOST", "BONUS"]));
        var responded = await Top(response.State);
        Assert.Equal("HOST", responded.State.CardObjects["BONUS"].AttachedToObjectId);
        Assert.Equal(4, Might(responded.State)); Assert.Null(responded.State.CardObjects["GEAR"].AttachedToObjectId);
        var mana = responded.State.RunePools["P1"].Mana;
        var done = await Top(responded.State);
        Assert.Equal(mana, done.State.RunePools["P1"].Mana);
        Assert.Equal("HOST", done.State.CardObjects["GEAR"].AttachedToObjectId);
        var paid = Assert.Single(done.Events, e => e.Kind == "COST_PAID");
        Assert.Equal(0, paid.Payload["totalManaCost"]); Assert.Equal(4, paid.Payload["targetPower"]);
        Restore(done.State); PublicRecovery(done.State);
    }

    [Fact]
    public async Task MissingColoredPowerOpensARecoverableInstructionPaymentAndCannotBeFreelyDeclinedOnceFunded()
    {
        var state = PositionFor() with { RunePools = new Dictionary<string, RunePool> { ["P1"] = new(20, 0), ["P2"] = RunePool.Empty } };
        var selected = await Confirm(state);
        var waiting = await Top(selected.State);
        var payment = Assert.IsType<PendingPaymentState>(waiting.State.PendingPayment);
        Assert.Equal(1, payment.PowerCostByTrait[RuneTrait.Red]); Assert.Equal(0, payment.PowerCost);
        Assert.Null(waiting.State.CardObjects["GEAR"].AttachedToObjectId);
        Restore(waiting.State); PublicRecovery(waiting.State);
        var funded = waiting.State with { RunePools = new Dictionary<string, RunePool>(waiting.State.RunePools) {
            ["P1"] = new(17, 0, new Dictionary<string, int> { [RuneTrait.Red] = 1 })
        }};
        var rejected = await new CoreRuleEngine().ResolveAsync(funded, new("cannot-falsely-decline", "P1", CommandTypes.PayCost),
            new PayCostCommand(payment.PaymentId, payment.PaymentWindow, ["UNABLE_TO_PAY"]), default);
        Assert.False(rejected.Accepted); Assert.Equal(MatchStateHasher.Hash(funded), MatchStateHasher.Hash(rejected.State));
        var paid = await Act(funded, "P1", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, ["PAY"]));
        Assert.Equal("HOST", paid.State.CardObjects["GEAR"].AttachedToObjectId);
        Assert.Null(paid.State.PendingPayment); Assert.Empty(paid.State.StackItems);
        Assert.Equal(0, paid.State.RunePools["P1"].PowerByTrait.GetValueOrDefault(RuneTrait.Red));
        Restore(paid.State); PublicRecovery(paid.State);
    }

    [Fact]
    public async Task InabilityToPayKeepsTheOldHostAndDoesNotCreateAttachmentObservers()
    {
        var state = PositionFor() with { RunePools = new Dictionary<string, RunePool> { ["P1"] = new(20, 0), ["P2"] = RunePool.Empty } };
        state = OfficialHoldSequenceTests.AddUnit(state, "OLD", "OGN·001/298", "F", "P1");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["GEAR"] = state.CardObjects["GEAR"] with { AttachedToObjectId = "OLD" }
        }};
        state = LocalDestructionRecallTests.At(state, "GEAR", "P1", "BATTLEFIELD", "F");
        var selected = await Confirm(state); var waiting = await Top(selected.State);
        var payment = waiting.State.PendingPayment!;
        var done = await Act(waiting.State, "P1", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, ["UNABLE_TO_PAY"]));
        Assert.Equal("OLD", done.State.CardObjects["GEAR"].AttachedToObjectId);
        Assert.Equal("F", done.State.ObjectLocations["GEAR"].BattlefieldObjectId);
        Assert.Empty(done.State.StackItems); Assert.Null(done.State.PendingPayment);
        Assert.DoesNotContain(done.Events, e => e.Kind is "COST_PAID" or "EQUIPMENT_ATTACHED" or "EQUIPMENT_DETACHED");
        Restore(done.State); PublicRecovery(done.State);
    }

    [Theory]
    [InlineData("GEAR")]
    [InlineData("HOST")]
    public async Task ANewGenerationCannotBeUsedAsTheChosenEquipmentOrPlayedUnit(string id)
    {
        var selected = await Confirm(PositionFor(gear: "SFD·186/221"));
        var state = selected.State with { CardObjects = new Dictionary<string, CardObjectState>(selected.State.CardObjects) {
            [id] = selected.State.CardObjects[id] with { ObjectGeneration = selected.State.CardObjects[id].ObjectGeneration + 1 }
        }};
        var done = await Top(state);
        Assert.Null(done.State.CardObjects["GEAR"].AttachedToObjectId);
        Assert.Empty(done.State.StackItems); Assert.Null(done.State.PendingPayment);
        Restore(done.State); PublicRecovery(done.State);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public async Task ReferencedExperienceCostIsNeverRemovedByGenericPowerDiscount(int xp)
    {
        var initial = PositionFor(gear: "UNL-158/219") with { PlayerExperience = new Dictionary<string, int> { ["P1"] = xp, ["P2"] = 0 } };
        var selected = await Confirm(initial); var resolved = await Top(selected.State);
        if (xp == 0)
        {
            Assert.Null(resolved.State.CardObjects["GEAR"].AttachedToObjectId);
            var payment = Assert.IsType<PendingPaymentState>(resolved.State.PendingPayment);
            Assert.Equal(1, payment.ExperienceCost); Restore(resolved.State); PublicRecovery(resolved.State);
            resolved = await Act(resolved.State, "P1", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, ["UNABLE_TO_PAY"]));
            Assert.Null(resolved.State.CardObjects["GEAR"].AttachedToObjectId);
        }
        else Assert.Equal("HOST", resolved.State.CardObjects["GEAR"].AttachedToObjectId);
        Assert.Equal(Math.Max(0, xp - 1), resolved.State.PlayerExperience["P1"]);
        Restore(resolved.State); PublicRecovery(resolved.State);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReferencedDestructionCostAllowsAnyFriendlyUnitIncludingTheUnchosenHost(bool destroyHost)
    {
        var initial = PositionFor(gear: "SFD·178/221");
        initial = OfficialHoldSequenceTests.AddUnit(initial, "COST-UNIT", "OGN·001/298", "F", "P1");
        initial = initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) {
            ["P1"] = new(20, 0, new Dictionary<string, int> { [RuneTrait.Yellow] = 1 })
        }};
        var selected = await Confirm(initial); var waiting = await Top(selected.State);
        var choice = Assert.IsType<PendingCardChoiceState>(waiting.State.PendingCardChoice);
        Assert.Equal("TEMPERED_EQUIPMENT_COST", choice.ChoiceWindow);
        Assert.Contains("HOST", choice.LegalObjectIds); Assert.Contains("COST-UNIT", choice.LegalObjectIds);
        Assert.Null(waiting.State.CardObjects["GEAR"].AttachedToObjectId);
        Restore(waiting.State); PublicRecovery(waiting.State);
        var target = destroyHost ? "HOST" : "COST-UNIT";
        var done = await Act(waiting.State, "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [target]));
        Assert.Contains(target, done.State.PlayerZones["P1"].Graveyard);
        Assert.Equal(0, done.State.RunePools["P1"].PowerByTrait.GetValueOrDefault(RuneTrait.Yellow));
        Assert.Equal(destroyHost ? null : "HOST", done.State.CardObjects["GEAR"].AttachedToObjectId);
        Assert.Empty(done.State.StackItems); Assert.Null(done.State.PendingCardChoice);
        Assert.Contains(done.Events, e => e.Kind == "UNIT_DESTROYED");
        Assert.DoesNotContain(done.Events, e => e.Kind == "PRIORITY_PASSED");
        Restore(done.State); PublicRecovery(done.State);
    }

    [Fact]
    public async Task ReferencedGraveyardCostRecyclesExactlyTwoCardsAndPaysColoredPower()
    {
        var initial = PositionFor(gear: "SFD·150/221");
        var cards = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["C1"] = new("C1", cardNo: "OGN·114/298", ownerId: "P1", controllerId: "P1", tags: [CardObjectTags.SpellCard]),
            ["C2"] = new("C2", cardNo: "OGN·114/298", ownerId: "P1", controllerId: "P1", tags: [CardObjectTags.SpellCard])
        };
        initial = initial with { CardObjects = cards, PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
            ["P1"] = initial.PlayerZones["P1"] with { Graveyard = ["C1", "C2"] }
        }, ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) {
            ["C1"] = new("P1", "GRAVEYARD"), ["C2"] = new("P1", "GRAVEYARD")
        }, RunePools = new Dictionary<string, RunePool>(initial.RunePools) {
            ["P1"] = new(20, 0, new Dictionary<string, int> { [RuneTrait.Purple] = 1 })
        }};
        var selected = await Confirm(initial); var waiting = await Top(selected.State);
        var choice = Assert.IsType<PendingCardChoiceState>(waiting.State.PendingCardChoice);
        Assert.Equal(2, choice.RequiredCount); Restore(waiting.State); PublicRecovery(waiting.State);
        var done = await Act(waiting.State, "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["C1", "C2"]));
        Assert.Empty(done.State.PlayerZones["P1"].Graveyard);
        Assert.Contains("C1", done.State.PlayerZones["P1"].MainDeck); Assert.Contains("C2", done.State.PlayerZones["P1"].MainDeck);
        Assert.Equal(initial.RngCursor + 1, done.State.RngCursor);
        Assert.Equal(0, done.State.RunePools["P1"].PowerByTrait.GetValueOrDefault(RuneTrait.Purple));
        Assert.Equal("HOST", done.State.CardObjects["GEAR"].AttachedToObjectId); Assert.Empty(done.State.StackItems);
        Assert.Single(done.Events, e => e.Kind == "CARDS_RECYCLED");
        Restore(done.State); PublicRecovery(done.State);
    }

    [Fact]
    public async Task ThePlayedUnitIsNotAChosenTargetAndKeepsItsIndependentAttachmentAfterControlChanges()
    {
        var selected = await Confirm(PositionFor(gear: "SFD·186/221"));
        var state = selected.State with { CardObjects = new Dictionary<string, CardObjectState>(selected.State.CardObjects) {
            ["HOST"] = selected.State.CardObjects["HOST"] with { ControllerId = "P2", Tags = selected.State.CardObjects["HOST"].Tags.Concat(["法盾99"]).Order(StringComparer.Ordinal).ToArray() }
        }};
        state = LocalDestructionRecallTests.At(state, "HOST", "P2", "BATTLEFIELD", "F");
        var done = await Top(state);
        Assert.Equal("HOST", done.State.CardObjects["GEAR"].AttachedToObjectId);
        Assert.Equal("P1", done.State.CardObjects["GEAR"].ControllerId); Assert.Equal("P1", done.State.CardObjects["GEAR"].OwnerId);
        Assert.Equal("P2", done.State.CardObjects["HOST"].ControllerId);
        Assert.Equal("F", done.State.ObjectLocations["GEAR"].BattlefieldObjectId);
        Assert.DoesNotContain(done.Events, e => e.Kind == "TARGET_COST_PAID");
        Restore(done.State); PublicRecovery(done.State);
    }

    [Fact]
    public async Task ActualAzirTokenPlayCapturesItsGrantAndUsesTheSameKeywordConfirmationAndAttachment()
    {
        var initial = OfficialLegendTokenTests.Position("SFD·197/221", 0);
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["GEAR"] = new("GEAR", cardNo: "SFD·186/221", ownerId: "P1", controllerId: "P1", tags: [CardObjectTags.EquipmentCard, CardEquipmentKeywordNames.Weapon])
        }, PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
            ["P1"] = initial.PlayerZones["P1"] with { Base = initial.PlayerZones["P1"].Base.Append("GEAR").ToArray() }
        }, ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) { ["GEAR"] = new("P1", "BASE") }};
        var activated = await OfficialLegendTokenTests.Activate(initial, "SFD·197/221");
        var entered = await Top(activated.State);
        var choice = Assert.IsType<PendingCardChoiceState>(entered.State.PendingCardChoice);
        Assert.Equal("TEMPERED_CONFIRMATION", choice.ChoiceWindow);
        var item = Assert.Single(entered.State.StackItems);
        Assert.Equal("LEGEND", item.Tempered!.Grant!.Source.ObjectId); Assert.NotNull(item.Tempered.TokenFactoryCardNo);
        Restore(entered.State); PublicRecovery(entered.State);
        var forged = item with { Tempered = item.Tempered with { Grant = item.Tempered.Grant with { Source = new("GEAR", 0) } } };
        Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(entered.State with { StackItems = [forged] }));
        var selected = await Act(entered.State, "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["GEAR"]));
        var done = await Top(selected.State);
        Assert.Equal(item.SourceObjectId, done.State.CardObjects["GEAR"].AttachedToObjectId);
        Assert.Contains(CardObjectTags.SandSoldier, done.State.CardObjects[item.SourceObjectId].Tags);
        Assert.Empty(done.State.StackItems); Restore(done.State); PublicRecovery(done.State);
    }

    [Theory]
    [InlineData("missing-context")]
    [InlineData("effect")]
    [InlineData("source-generation")]
    [InlineData("gear-generation")]
    [InlineData("target-list")]
    [InlineData("repeat")]
    [InlineData("source-confirmed")]
    [InlineData("cost-selection")]
    [InlineData("inability")]
    public async Task PrivateRecoveryRejectsForgedKeywordBindingsAndCostContinuation(string forgery)
    {
        var selected = await Confirm(PositionFor()); var item = Assert.Single(selected.State.StackItems);
        var changed = forgery switch {
            "missing-context" => item with { Tempered = null },
            "effect" => item with { EffectKind = "FORGED" },
            "source-generation" => item with { Tempered = item.Tempered! with { Unit = item.Tempered.Unit with { Generation = 999 } } },
            "gear-generation" => item with { Tempered = item.Tempered! with { Equipment = item.Tempered.Equipment! with { Generation = 999 } } },
            "target-list" => item with { TargetObjectIds = ["HOST"] },
            "repeat" => item with { EffectRepeatCount = 2 },
            "source-confirmed" => item with { SourceConfirmed = true },
            "cost-selection" => item with { Tempered = item.Tempered! with { Costs = new(new("HOST", 0)) } },
            _ => item with { Tempered = item.Tempered! with { UnableToPay = true } }
        };
        Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(selected.State with { StackItems = [changed] }));
    }
}
