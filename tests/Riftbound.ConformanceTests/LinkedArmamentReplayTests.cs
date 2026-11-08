using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

// CN204.1.c,355.18,356.1.b/356.2/356.3,445.1.b/445.2;
// SFD judge FAQ: linked units belong to this incarnation of this equipment,
// and the skill's controller may play a unit owned by an opponent.
public sealed class LinkedArmamentReplayTests
{
    private const string Drive = "SFD·090/221";
    private const string Ability = "BANISH_SELF_PLAY_ALL_LINKED_UNITS";
    private const string Kill = "OGN·229/298";
    private const string Unit = "SFD·055/221";
    private const string Haste = "OGN·010/298";

    private static MatchState Position(string unitOwner = "P1", bool second = false, bool haste = false)
    {
        var state = RadGearTestPosition.Put(RadGearTestPosition.Create(), "GEAR", Drive, "P1", "BASE");
        state = RadGearTestPosition.Put(state, "ALLY", Unit, "P1", "BASE");
        state = RadGearTestPosition.Put(state, "KILL", Kill, "P1", "HAND");
        state = RadGearTestPosition.Put(state, "RETURN", "SFD·135/221", "P1", "HAND");
        if (second)
        {
            state = RadGearTestPosition.Put(state, "SECOND", haste ? Haste : Unit, "P1", "BASE");
            state = RadGearTestPosition.Put(state, "KILL-SECOND", Kill, "P1", "HAND");
        }
        var cards = state.CardObjects.ToDictionary();
        cards["ALLY"] = cards["ALLY"] with { OwnerId = unitOwner };
        if (second) cards["SECOND"] = cards["SECOND"] with { OwnerId = "P2" };
        return state with { CardObjects = cards };
    }

    private static async Task Link(Driver game, string unit = "ALLY", string kill = "KILL")
    {
        await game.Act("P1", new AssembleEquipmentCommand("GEAR", unit, ["ASSEMBLE_BLUE"]));
        await game.Top();
        await game.Act("P1", new PlayCardCommand(kill, Kill, [unit]));
        await game.Top(); await game.Top();
        var owner = game.State.CardObjects[unit].OwnerId!;
        Assert.Contains(unit, game.State.PlayerZones[owner].Banished);
        Assert.Null(game.State.CardObjects["GEAR"].AttachedToObjectId);
    }

    private static async Task Activate(Driver game, string source = "GEAR")
        => await game.Act("P1", new ActivateAbilityCommand(source, Ability, []));

    private static async Task BeginPlay(Driver game)
    {
        await Activate(game); await game.Top();
        Assert.NotNull(game.State.PendingEffectPlay);
    }

    [Theory]
    [InlineData("P1")] [InlineData("P2")]
    public async Task BanishmentAndTheTypedFeeArePaidBeforeResponsesEvenWithNoLinkedUnits(string owner)
    {
        var state = Position();
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["GEAR"] = state.CardObjects["GEAR"] with { OwnerId = owner, IsExhausted = true } } };
        var game = new Driver(state); var before = game.State.RunePools["P1"];
        await Activate(game);
        Assert.DoesNotContain("GEAR", game.State.PlayerZones["P1"].Base);
        Assert.Contains("GEAR", game.State.PlayerZones[owner].Banished);
        Assert.Equal(owner, game.State.CardObjects["GEAR"].OwnerId);
        Assert.Equal(before.Mana - 3, game.State.RunePools["P1"].Mana);
        Assert.Equal(before.TotalPower - 1, game.State.RunePools["P1"].TotalPower);
        Assert.NotNull(Assert.Single(game.State.StackItems).SimpleActivation!.BanishCost);
        await game.Top(); Assert.Null(game.State.PendingEffectPlay); Assert.Empty(game.State.StackItems);
        await game.Replay();
    }

    [Theory]
    [InlineData("P1")] [InlineData("P2")]
    public async Task TheControllerPlaysTheActuallyLinkedUnitAndPreservesItsOwnerWithoutBaseCosts(string owner)
    {
        var game = new Driver(Position(owner)); await Link(game);
        await BeginPlay(game);
        var pending = Assert.IsType<PendingEffectPlayState>(game.State.PendingEffectPlay);
        Assert.Equal("P1", pending.PlayerId); Assert.Equal(owner, pending.SourceOwners!["ALLY"]);
        var pool = game.State.RunePools["P1"];
        await RadGearTestPosition.Reject(game.State, "P2", new PlayCardCommand("ALLY", Unit, []));
        await game.Act("P1", new PlayCardCommand("ALLY", Unit, []));
        Assert.Contains("ALLY", game.State.PlayerZones["P1"].Base);
        Assert.DoesNotContain("ALLY", game.State.PlayerZones[owner].Banished);
        Assert.Equal(owner, game.State.CardObjects["ALLY"].OwnerId);
        Assert.Equal("P1", game.State.CardObjects["ALLY"].ControllerId);
        Assert.Equal(pool, game.State.RunePools["P1"]);
        Assert.Null(game.State.PendingEffectPlay); await game.Replay();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task AllMixedOwnerUnitsCanBePlayedInEitherOrder(bool opponentOwnedFirst)
    {
        var game = new Driver(Position(second: true));
        await Link(game); await Link(game, "SECOND", "KILL-SECOND"); await BeginPlay(game);
        Assert.Equal(2, game.State.PendingEffectPlay!.Sources.Count);
        var pool = game.State.RunePools["P1"];
        var first = opponentOwnedFirst ? "SECOND" : "ALLY"; var last = opponentOwnedFirst ? "ALLY" : "SECOND";
        await game.Act("P1", new PlayCardCommand(first, Unit, []));
        Assert.Equal([last], game.State.PendingEffectPlay!.Sources.Keys);
        await game.Act("P1", new PlayCardCommand(last, Unit, []));
        Assert.Null(game.State.PendingEffectPlay); Assert.Equal(pool, game.State.RunePools["P1"]);
        Assert.Contains("ALLY", game.State.PlayerZones["P1"].Base);
        Assert.Contains("SECOND", game.State.PlayerZones["P1"].Base);
        Assert.Equal("P2", game.State.CardObjects["SECOND"].OwnerId); await game.Replay();
    }

    [Fact]
    public async Task ASecondDriveCannotPlayTheFirstDrivesLinkedUnit()
    {
        var game = new Driver(RadGearTestPosition.Put(Position(), "OTHER-DRIVE", Drive, "P1", "BASE"));
        await Link(game); await Activate(game, "OTHER-DRIVE"); await game.Top();
        Assert.Null(game.State.PendingEffectPlay);
        Assert.Contains("ALLY", game.State.PlayerZones["P1"].Banished); await game.Replay();
    }

    [Fact]
    public async Task AReturnedAndReplayedDriveDoesNotInheritItsPreviousIncarnationsLinks()
    {
        var game = new Driver(Position()); await Link(game);
        await game.Act("P1", new PlayCardCommand("RETURN", "SFD·135/221", ["GEAR"])); await game.Top();
        await game.Act("P1", new PlayCardCommand("GEAR", Drive, []));
        await Activate(game); await game.Top();
        Assert.Null(game.State.PendingEffectPlay); Assert.Contains("ALLY", game.State.PlayerZones["P1"].Banished);
        await game.Replay();
    }

    [Theory]
    [InlineData("attached")] [InlineData("mana")] [InlineData("power")]
    public async Task AnAttachedOrUnderfundedDriveCannotBeBanishedAsAPartialPayment(string boundary)
    {
        var game = new Driver(Position());
        if (boundary == "attached")
        {
            await game.Act("P1", new AssembleEquipmentCommand("GEAR", "ALLY", ["ASSEMBLE_BLUE"])); await game.Top();
        }
        var state = game.State;
        if (boundary != "attached") state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) {
            ["P1"] = boundary == "mana" ? new(2, 30) : new(3, 0) } };
        await RadGearTestPosition.Reject(state, "P1", new ActivateAbilityCommand("GEAR", Ability, []));
        Assert.Contains("GEAR", game.State.PlayerZones["P1"].Base); await game.Replay();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task BaseCostExemptionDoesNotWaiveTheChosenHasteAdditionalCost(bool haste)
    {
        var game = new Driver(Position(second: true, haste: true));
        await Link(game, "SECOND", "KILL-SECOND"); await BeginPlay(game);
        var pool = game.State.RunePools["P1"];
        await game.Act("P1", new PlayCardCommand("SECOND", Haste, [], OptionalCosts: haste ? ["HASTE_READY"] : []));
        Assert.Equal(!haste, game.State.CardObjects["SECOND"].IsExhausted);
        Assert.Equal(pool.Mana - (haste ? 1 : 0), game.State.RunePools["P1"].Mana);
        Assert.Equal(pool.TotalPower - (haste ? 1 : 0), game.State.RunePools["P1"].TotalPower);
        Assert.Equal("P2", game.State.CardObjects["SECOND"].OwnerId); await game.Replay();
    }

    [Fact]
    public async Task RefusingOneNonzeroPlayFeeStillRequiresTheOtherFreeLinkedUnit()
    {
        var game = new Driver(Position(second: true, haste: true));
        await Link(game); await Link(game, "SECOND", "KILL-SECOND"); await BeginPlay(game);
        var pending = game.State.PendingEffectPlay!; var pool = game.State.RunePools["P1"];
        await RadGearTestPosition.Reject(game.State, "P1", new ChooseCardsCommand(pending.ChoiceId, "EFFECT_PLAY", []));
        await game.Act("P1", new RefusePlayPaymentCommand("SECOND", Haste, [], OptionalCosts: ["HASTE_READY"]));
        Assert.Equal(["ALLY"], game.State.PendingEffectPlay!.Sources.Keys);
        Assert.Equal(pool, game.State.RunePools["P1"]);
        var current = game.State.PendingEffectPlay!;
        var group = Assert.Single(game.State.LinkedExiles);
        var rewound = game.State with { PendingEffectPlay = current with {
            Sources = group.Objects,
            SourceOwners = group.Objects.Keys.ToDictionary(id => id, id => game.State.CardObjects[id].OwnerId!),
            Parent = current.Parent with { SimpleActivation = current.Parent.SimpleActivation! with { RefusedEffectPlays = null } }
        } };
        Assert.Contains(ArmamentGrantedCombatTriggerTests.Errors(game, rewound),
            error => error.Contains("original accepted refusals", StringComparison.Ordinal));
        await RadGearTestPosition.Reject(game.State, "P1", new ChooseCardsCommand(current.ChoiceId, "EFFECT_PLAY", []));
        await RadGearTestPosition.Reject(game.State, "P1", new RefusePlayPaymentCommand("ALLY", Unit, []));
        await game.Act("P1", new PlayCardCommand("ALLY", Unit, []));
        Assert.Null(game.State.PendingEffectPlay); Assert.Contains("SECOND", game.State.PlayerZones["P2"].Banished);
        await game.Replay();
    }

    [Theory]
    [InlineData("erase")] [InlineData("owner")] [InlineData("skip")] [InlineData("drop")]
    public async Task RecoveryRequiresTheOriginalAcceptedSourceBanishCost(string change)
    {
        var game = new Driver(Position()); await Link(game); await Activate(game);
        var item = Assert.Single(game.State.StackItems); var context = item.SimpleActivation!;
        var forged = game.State with { StackItems = [item with { SimpleActivation = change switch {
            "erase" => context with { BanishCost = null },
            "owner" => context with { BanishCost = context.BanishCost! with { OwnerId = "P2" } },
            _ => context with { RefusedEffectPlays = [new("ALLY", game.State.CardObjects["ALLY"].ObjectGeneration)] }
        } }] };
        if (change == "drop") forged = forged with { StackItems = [], TimingState = TimingStates.NeutralOpen,
            PriorityPlayerId = null, ActivePlayerId = "P1", PassedPriorityPlayerIds = [] };
        Assert.NotEmpty(ArmamentGrantedCombatTriggerTests.Errors(game, forged)); await game.Top(); await game.Replay();
    }

    [Theory]
    [InlineData("owner")] [InlineData("waiver")] [InlineData("sources")] [InlineData("choice")]
    public async Task RecoveryRejectsChangedOwnersWaiversAndLinkedSources(string change)
    {
        var game = new Driver(Position("P2")); await Link(game); await BeginPlay(game);
        var pending = game.State.PendingEffectPlay!;
        var forged = game.State with { PendingEffectPlay = change switch {
            "owner" => pending with { SourceOwners = new Dictionary<string, string> { ["ALLY"] = "P1" } },
            "waiver" => pending with { IgnoreAllCosts = true },
            "sources" => pending with { Sources = new Dictionary<string, long> { ["ENEMY"] = game.State.CardObjects["ENEMY"].ObjectGeneration } },
            _ => pending with { ChoiceId = "EFFECT-PLAY:0:" + pending.Parent.StackItemId }
        } };
        Assert.NotEmpty(ArmamentGrantedCombatTriggerTests.Errors(game, forged)); await game.Replay();
    }

    [Fact]
    public async Task AnUntargetedActivationRejectsConditionalCounteringAndCannotBeReinsertedAfterResolution()
    {
        var game = new Driver(RadGearTestPosition.Put(Position(), "COUNTER", "SFD·045/221", "P2", "HAND"));
        await Activate(game); var original = Assert.Single(game.State.StackItems); var paid = game.State.RunePools["P1"];
        await game.Act("P1", new PassPriorityCommand());
        await RadGearTestPosition.Reject(game.State, "P2", new PlayCardCommand("COUNTER", "SFD·045/221", [original.StackItemId]));
        await game.Top();
        Assert.Empty(game.State.StackItems); Assert.Null(game.State.PendingEffectPlay);
        Assert.Contains("GEAR", game.State.PlayerZones["P1"].Banished);
        Assert.Equal(paid, game.State.RunePools["P1"]);
        var forged = game.State with { StackItems = [original], TimingState = TimingStates.NeutralClosed,
            PriorityPlayerId = "P1", ActivePlayerId = "P1", PassedPriorityPlayerIds = [] };
        Assert.Contains(ArmamentGrantedCombatTriggerTests.Errors(game, forged),
            error => error.Contains("accepted completion history", StringComparison.Ordinal));
        await game.Replay();
    }

    [Fact]
    public async Task ARealHoldingCostIncreaseIsStillPaidAfterTheBaseCostIsWaived()
    {
        const string vanilla = "OGN·219/298";
        var state = RadGearTestPosition.Put(Position(), "BF", "UNL-219/219", "P1", "BATTLEFIELD", "BF");
        state = RadGearTestPosition.Put(state, "ALLY", vanilla, "P1", "BASE");
        state = RadGearTestPosition.Put(state, "HOLDER", vanilla, "P1", "BATTLEFIELD", "BF");
        for (var i = 0; i < 10; i++) state = RadGearTestPosition.Put(state, "BLUE" + i, "UNL-R03", "P1", "BASE");
        for (var i = 0; i < 2; i++) state = RadGearTestPosition.Put(state, "YELLOW" + i, "UNL-R06", "P1", "BASE");
        state = state with { TurnPlayerId = "P2", ActivePlayerId = "P2" };
        var game = new Driver(state); await game.Act("P2", new EndTurnCommand());
        while (game.State.StackItems.Count > 0) await game.Top();
        Assert.NotNull(game.State.BattlefieldHolds);
        await game.TapRunes("P1", Enumerable.Range(0, 9).Select(i => "BLUE" + i));
        await game.Act("P1", new RecycleRuneCommand("BLUE0", ["BLUE0", "BLUE1", "YELLOW0", "YELLOW1"]));
        await Link(game); await BeginPlay(game);
        var pool = game.State.RunePools["P1"];
        var command = new PlayCardCommand("ALLY", vanilla, []);
        var quote = new CoreRuleEngine().PreviewPlayCard(game.State, "P1", PlayCostPreviewTests.Request(game.State, command));
        Assert.True(quote.CanPay, quote.Message); Assert.Equal(1, quote.Cost!.Mana); Assert.Equal(0, quote.Cost.GenericPower);
        await game.Act("P1", command);
        Assert.Equal(pool.Mana - 1, game.State.RunePools["P1"].Mana);
        Assert.Equal(pool.TotalPower, game.State.RunePools["P1"].TotalPower); await game.Replay();
    }

    [Fact]
    public async Task TheEquipmentAbilityCompletesOnceAfterAllOfItsLinkedUnitsHaveBeenPlayed()
    {
        var state = RadGearTestPosition.Put(Position(second: true), "PROGRESS", "SFD·075/221", "P1", "HAND");
        var game = new Driver(state); await Link(game); await Link(game, "SECOND", "KILL-SECOND");
        await game.Act("P1", new PlayCardCommand("PROGRESS", "SFD·075/221", []));
        var might = game.State.CardObjects["PROGRESS"].Power;
        await BeginPlay(game); Assert.Empty(game.State.StackItems);
        await game.Act("P1", new PlayCardCommand("ALLY", Unit, []));
        Assert.Empty(game.State.StackItems); Assert.Equal(might, game.State.CardObjects["PROGRESS"].Power);
        await game.Act("P1", new PlayCardCommand("SECOND", Unit, []));
        var observer = Assert.Single(game.State.StackItems);
        Assert.Equal("PROGRESS", observer.SourceObjectId); Assert.Equal("SFD·075/221", observer.UnitEvent!.CardNo);
        await game.Top(); Assert.Equal(might + 1, game.State.CardObjects["PROGRESS"].Power); await game.Replay();
    }
}
