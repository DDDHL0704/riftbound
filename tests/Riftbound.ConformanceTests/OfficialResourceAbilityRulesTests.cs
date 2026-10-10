using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

// Expectations are from CN 135.2.e.5, 166-168, 186.1 and 429, plus official
// printed resource amounts. They deliberately do not inherit old ledger fixtures.
public sealed class OfficialResourceAbilityRulesTests
{
    [Theory]
    [InlineData(MatchPhases.TurnStart, "P1", true)]
    [InlineData(MatchPhases.TurnEnd, "P1", true)]
    [InlineData(MatchPhases.TurnStart, "P2", false)]
    [InlineData(MatchPhases.TurnEnd, "P2", false)]
    [InlineData(MatchPhases.Mulligan, "P1", false)]
    public async Task ReactionResourceRightsFollowPriorityOutsideMainPhase(string phase, string priority, bool allowed)
    {
        var state = State("UNL·T05", TimingStates.NeutralClosed) with { Phase = phase, PriorityPlayerId = priority };
        if (allowed)
            Assert.Contains(ResolutionResult.BuildPrompts(state)["P1"].Candidates!,
                candidate => candidate.Action == CommandTypes.ActivateAbility && candidate.Enabled);
        var result = await Resolve(state, new ActivateAbilityCommand("SOURCE", Ability("UNL·T05").AbilityId, []));
        Assert.Equal(allowed, result.Accepted);
        Assert.Equal(phase, result.State.Phase);
        Assert.Equal(priority, result.State.PriorityPlayerId);
        if (allowed) Assert.Equal(1, result.State.RunePools["P1"].Power);
        else Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(result.State));
    }

    public static IEnumerable<object[]> Cases()
    {
        (string Card, int Mana, int Power, string Trait)[] cards =
        [
            ("UNL·T05", 0, 1, ""), ("SFD·T03", 0, 1, ""),
            ("UNL-093/219", 1, 0, ""), ("UNL-049/219", 0, 1, ""), ("OGN·098/298", 1, 0, ""),
            ("SFD·222/221", 0, 0, "red"), ("SFD·226/221", 0, 0, "green"),
            ("SFD·229/221", 0, 0, "blue"), ("SFD·231/221", 0, 0, "orange"),
            ("SFD·234/221", 0, 0, "purple"), ("SFD·238/221", 0, 0, "yellow"),
            ("OGN·040/298", 0, 0, "red"), ("OGN·081/298", 0, 0, "green"),
            ("OGN·120/298", 0, 0, "blue"), ("OGN·163/298", 0, 0, "orange"),
            ("OGN·204/298", 0, 0, "purple"), ("OGN·245/298", 0, 0, "yellow")
        ];
        foreach (var card in cards)
        foreach (var window in new[] { TimingStates.NeutralOpen, TimingStates.SpellDuelOpen,
                     TimingStates.NeutralClosed, TimingStates.SpellDuelClosed })
            yield return [card.Card, card.Mana, card.Power, card.Trait, window];
    }

    [Theory, MemberData(nameof(Cases))]
    public async Task ResourceAbilitiesWorkInEveryOwnedActionWindowAndKeepActionRights(
        string card, int mana, int power, string trait, string window)
    {
        var state = State(card, window);
        var ability = Ability(card);
        var prompt = ResolutionResult.BuildPrompts(state)["P1"];
        var candidate = Assert.Single(prompt.Candidates!, c => c.Action == CommandTypes.ActivateAbility);
        Assert.True(candidate.Enabled);
        var result = await Resolve(state, new ActivateAbilityCommand("SOURCE", ability.AbilityId, []));
        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(mana, result.State.RunePools["P1"].Mana);
        Assert.Equal(power, result.State.RunePools["P1"].Power);
        Assert.Equal(trait.Length == 0 ? 0 : 1, result.State.RunePools["P1"].PowerByTrait.Values.Sum());
        if (trait.Length > 0) Assert.Equal(1, result.State.RunePools["P1"].PowerByTrait[trait]);
        RetiredPaymentLedgerTests.AssertNoLedger(result.State);
        Assert.Equal(state.FocusPlayerId, result.State.FocusPlayerId);
        Assert.Equal(state.PriorityPlayerId, result.State.PriorityPlayerId);
        Assert.Equal(state.PassedPriorityPlayerIds, result.State.PassedPriorityPlayerIds);
        Assert.Equal(state.PassedFocusPlayerIds, result.State.PassedFocusPlayerIds);
        Assert.Equal(state.StackItems, result.State.StackItems);
        Assert.Equal(state.TimingState, result.State.TimingState);
        if (card.Contains('T'))
        {
            Assert.DoesNotContain("SOURCE", result.State.CardObjects.Keys);
            Assert.DoesNotContain("SOURCE", result.State.ObjectLocations.Keys);
            Assert.All(result.State.PlayerZones.Values, z => Assert.DoesNotContain("SOURCE", z.Graveyard));
        }
        else Assert.True(result.State.CardObjects["SOURCE"].IsExhausted);
        var again = await Resolve(result.State, new ActivateAbilityCommand("SOURCE", ability.AbilityId, []));
        Assert.False(again.Accepted);
        Assert.Equal(MatchStateHasher.Hash(result.State), MatchStateHasher.Hash(again.State));
    }

    [Theory]
    [InlineData("red")]
    [InlineData("blue")]
    [InlineData("green")]
    [InlineData("orange")]
    [InlineData("purple")]
    [InlineData("yellow")]
    public async Task GoldPowerPaysAnyTraitWithoutASeparateLedgerSelection(string trait)
    {
        var result = await Resolve(State("UNL·T05"), new ActivateAbilityCommand("SOURCE", Ability("UNL·T05").AbilityId, []));
        var plan = new PaymentCostRules.PaymentPlan("PAY", "PLAY_CARD", "P1", totalPowerCost: 1,
            powerCostByTrait: new Dictionary<string, int> { [trait] = 1 });
        var paid = PaymentCostRules.TryCommitPayment(plan, result.State.RunePools, result.State.PlayerExperience);
        Assert.True(paid.Accepted);
        Assert.Equal(0, paid.RunePools["P1"].TotalPower);
    }

    [Theory]
    [InlineData("exhausted")]
    [InlineData("hidden")]
    [InlineData("enemy-controller")]
    [InlineData("not-on-field")]
    [InlineData("wrong-player")]
    public async Task InvalidGoldActivationIsAtomic(string kind)
    {
        var state = State("UNL·T05");
        var cards = state.CardObjects.ToDictionary(e => e.Key, e => e.Value);
        cards["SOURCE"] = cards["SOURCE"] with { IsExhausted = kind == "exhausted", IsFaceDown = kind == "hidden",
            ControllerId = kind == "enemy-controller" ? "P2" : "P1" };
        state = state with { CardObjects = cards };
        if (kind == "not-on-field") state = state with { PlayerZones = new Dictionary<string, PlayerZones>
            { ["P1"] = PlayerZones.Empty with { Graveyard = ["SOURCE"] }, ["P2"] = PlayerZones.Empty } };
        var result = await Resolve(state, new ActivateAbilityCommand("SOURCE", Ability("UNL·T05").AbilityId, []), kind == "wrong-player" ? "P2" : "P1");
        Assert.False(result.Accepted);
        Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(result.State));
        Assert.Empty(result.Events);
    }

    public static IEnumerable<object[]> InvalidSources() => Cases().Where(row => (string)row[4] == TimingStates.NeutralOpen)
        .SelectMany(row => new[] { "exhausted", "hidden", "controller", "location", "identity", "type", "target", "turn" }
            .Select(problem => new object[] { row[0], problem }));

    [Theory, MemberData(nameof(InvalidSources))]
    public async Task EveryResourceFamilyRejectsInvalidAuthorityOrSourceWithoutMutation(string card, string problem)
    {
        var state = State(card);
        var cards = state.CardObjects.ToDictionary(e => e.Key, e => e.Value);
        cards["SOURCE"] = cards["SOURCE"] with { IsExhausted = problem == "exhausted", IsFaceDown = problem == "hidden",
            ControllerId = problem == "controller" ? "P2" : "P1",
            ObjectId = problem == "identity" ? "OTHER" : "SOURCE",
            Tags = problem == "type" ? [CardObjectTags.SpellCard] : cards["SOURCE"].Tags };
        state = state with { CardObjects = cards };
        if (problem == "location") state = state with { ObjectLocations = new Dictionary<string, ObjectLocationState>
            { ["SOURCE"] = new("P1", "GRAVEYARD") } };
        if (problem == "turn") state = state with { ActivePlayerId = "P2" };
        var result = await Resolve(state, new ActivateAbilityCommand("SOURCE", Ability(card).AbilityId, problem == "target" ? ["SOURCE"] : []));
        Assert.False(result.Accepted);
        Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(result.State));
        Assert.Empty(result.Events);
    }

    [Theory]
    [InlineData(5, false)]
    [InlineData(6, true)]
    public async Task HoneyfruitLevelSixBranchChecksExperienceBeforePayingCost(int experience, bool allowed)
    {
        var state = State("UNL-049/219") with { PlayerExperience = new Dictionary<string, int> { ["P1"] = experience } };
        var result = await Resolve(state, new ActivateAbilityCommand("SOURCE", Ability("UNL-049/219").AbilityId, [],
            [P4ActivatedAbilityCatalog.HoneyfruitLevelSixOptionalCostPrefix + "SOURCE"]));
        Assert.Equal(allowed, result.Accepted);
        if (allowed)
        {
            Assert.Equal(1, result.State.RunePools["P1"].Mana);
            Assert.Equal(1, result.State.RunePools["P1"].Power);
            Assert.Equal(experience, result.State.PlayerExperience["P1"]);
        }
        else Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(result.State));
    }

    [Theory]
    [InlineData(5, "P1", false, 1)]
    [InlineData(4, "P1", false, 0)]
    [InlineData(5, "P2", false, 0)]
    [InlineData(5, "P1", true, 0)]
    public async Task RenataGoldBonusTracksCurrentScoreControllerAndVisibility(int score, string controller, bool faceDown, int bonus)
    {
        var state = State("SFD·T03");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["RENATA"] = new("RENATA", cardNo: "SFD·201/221", tags: ["CARD_TYPE:LEGEND"], ownerId: "P1", controllerId: controller, isFaceDown: faceDown) },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) { ["P1"] = state.PlayerZones["P1"] with { LegendZone = ["RENATA"] } },
            PlayerScores = new Dictionary<string, int> { ["P1"] = score, ["P2"] = 0 } };
        var result = await Resolve(state, new ActivateAbilityCommand("SOURCE", Ability("SFD·T03").AbilityId, []));
        Assert.True(result.Accepted);
        Assert.Equal(bonus, result.State.RunePools["P1"].Mana);
        Assert.Equal(1, result.State.RunePools["P1"].Power);
        Assert.DoesNotContain("SOURCE", result.State.CardObjects.Keys);
    }

    [Fact]
    public async Task PayingPlayerCanUseGoldWithoutOwningNormalPriorityAndPaymentRemainsPending()
    {
        var state = State("UNL·T05") with { ActivePlayerId = "P2", PendingPayment = new("PAY", "PLAY_CARD", "P1", powerCost: 1) };
        var candidate = Assert.Single(ResolutionResult.BuildPrompts(state)["P1"].Candidates!, c => c.Action == CommandTypes.ActivateAbility);
        Assert.True(candidate.Enabled);
        var result = await Resolve(state, new ActivateAbilityCommand("SOURCE", Ability("UNL·T05").AbilityId, []));
        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(state.PendingPayment, result.State.PendingPayment);
        Assert.Equal("P2", result.State.ActivePlayerId);
        Assert.Equal(1, result.State.RunePools["P1"].Power);
    }

    [Fact]
    public async Task StalePromptCannotReactivateOrGrantResourcesAfterAcceptedGoldUse()
    {
        var state = State("UNL·T05");
        var session = new MatchSession(state, new CoreRuleEngine(), NoopMatchJournal.Instance);
        var prompt = session.PromptFor("P1");
        var command = new ActivateAbilityCommand("SOURCE", Ability("UNL·T05").AbilityId, []);
        var raw = JsonSerializer.SerializeToElement(new { cmdType = command.CmdType, sourceObjectId = "SOURCE",
            abilityId = command.AbilityId, targetObjectIds = Array.Empty<string>(), promptId = prompt.PromptId, snapshotTick = prompt.SnapshotTick });
        var accepted = await session.SubmitAsync("P1", "once", command, raw, default);
        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        var stale = await session.SubmitAsync("P1", "stale", command, raw, default);
        Assert.False(stale.Accepted);
        Assert.Equal(MatchStateHasher.Hash(accepted.State), MatchStateHasher.Hash(stale.State));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task HextechConversionAcceptsColoredPowerAsAnyPowerCost(int amount)
    {
        var state = State("SFD·083/221") with { RunePools = new Dictionary<string, RunePool>
            { ["P1"] = new(0, 0, new Dictionary<string, int> { ["red"] = 2 }), ["P2"] = RunePool.Empty } };
        var result = await Resolve(state, new ActivateAbilityCommand("SOURCE", Ability("SFD·083/221").AbilityId, [],
            [P4ActivatedAbilityCatalog.HextechAnomalyConversionOptionalCostPrefix + amount]));
        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(amount, result.State.RunePools["P1"].Mana);
        Assert.Equal(2 - amount, result.State.RunePools["P1"].TotalPower);
        RetiredPaymentLedgerTests.AssertNoLedger(result.State);
    }

    [Fact]
    public async Task ResourceGainSurvivesRoundTripWithoutPassingPriority()
    {
        var state = State("UNL·T05", TimingStates.NeutralClosed);
        var session = new MatchSession(state, new CoreRuleEngine(), NoopMatchJournal.Instance);
        var command = new ActivateAbilityCommand("SOURCE", Ability("UNL·T05").AbilityId, []);
        var raw = JsonSerializer.SerializeToElement(command);
        var first = await session.SubmitAsync("P1", "gold-1", command, raw, default);
        var duplicate = await session.SubmitAsync("P1", "gold-1", command, raw, default);
        Assert.True(first.Accepted, first.ErrorMessage);
        Assert.Equal(MatchStateHasher.Hash(first.State), MatchStateHasher.Hash(duplicate.State));
        var restored = JsonSerializer.Deserialize<MatchState>(JsonSerializer.Serialize(first.State))!;
        Assert.Equal(1, restored.RunePools["P1"].Power);
        Assert.Equal("P1", restored.PriorityPlayerId);
    }

    private sealed record UnimplementedCommand() : GameCommand("UNIMPLEMENTED_FOR_TEST");
    [Fact]
    public async Task MissingCommandImplementationCannotFallThroughToFakeSuccess()
    {
        var state = State("UNL·T05");
        var result = await Resolve(state, new UnimplementedCommand());
        Assert.False(result.Accepted);
        Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(result.State));
    }

    [Fact]
    public async Task TurnStartMustNotConvertAnUnknownCommandIntoPhaseAdvancement()
    {
        var state = State("UNL·T05") with { Phase = MatchPhases.TurnStart };
        var result = await Resolve(state, new UnimplementedCommand());
        Assert.False(result.Accepted);
        Assert.Empty(result.Events);
        Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(result.State));
    }

    private static P4ActivatedAbilityDefinition Ability(string card) => P4ActivatedAbilityCatalog.GetAll()
        .Single(a => a.SourceCardNo == card && a.IsResourceSkill);
    private static ValueTask<ResolutionResult> Resolve(MatchState state, GameCommand command, string player = "P1") =>
        new CoreRuleEngine().ResolveAsync(state, new("official-resource", player, command.CmdType), command, default);
    private static MatchState State(string card, string timing = TimingStates.NeutralOpen)
    {
        var closed = timing is TimingStates.NeutralClosed or TimingStates.SpellDuelClosed;
        return new("RESOURCE-AUDIT", 0, 3, "P1", new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" },
            status: MatchStatuses.InProgress, phase: MatchPhases.Main, timingState: timing,
            priorityPlayerId: closed ? "P1" : null, focusPlayerId: timing.StartsWith("SPELL_DUEL") ? "P1" : null,
            playerZones: new Dictionary<string, PlayerZones> { ["P1"] = PlayerZones.Empty with { Base = ["SOURCE"] }, ["P2"] = PlayerZones.Empty },
            runePools: new Dictionary<string, RunePool> { ["P1"] = RunePool.Empty, ["P2"] = RunePool.Empty },
            cardObjects: new Dictionary<string, CardObjectState> { ["SOURCE"] = new("SOURCE", cardNo: card,
                tags: [card == "UNL-093/219" ? CardObjectTags.UnitCard : CardObjectTags.EquipmentCard], ownerId: "P1", controllerId: "P1") },
            objectLocations: new Dictionary<string, ObjectLocationState> { ["SOURCE"] = new("P1", "BASE") },
            stackItems: closed ? [new StackItemState("PENDING", "P2", "SPELL", "TEST_PENDING", "OGN·001/298")] : []);
    }
}
