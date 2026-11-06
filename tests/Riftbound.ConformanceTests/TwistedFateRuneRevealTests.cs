using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;
using static Riftbound.ConformanceTests.OfficialGarenConquestBattlefieldCountTests;

namespace Riftbound.ConformanceTests;

public sealed class TwistedFateRuneRevealTests
{
    private const string Fate = "OGN·200/298";
    private static MatchState Position(string? rune)
    {
        var state = VenUnitEntryBasicTests.Position(Fate);
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["ENEMY"] = Printed("ENEMY", "OGN·088/298", "P2"),
            ["OTHER_ENEMY"] = Printed("OTHER_ENEMY", "OGN·219/298", "P2"),
            ["BASE_ENEMY"] = Printed("BASE_ENEMY", "OGN·219/298", "P2") },
            RunePools = state.RunePools.ToDictionary(p => p.Key, _ => new RunePool(40, 20,
                new Dictionary<string, int> { ["red"] = 10, ["green"] = 10, ["blue"] = 10,
                    ["orange"] = 10, ["purple"] = 10, ["yellow"] = 10 })) };
        if (rune is not null)
        {
            state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["RUNE_TOP"] = Printed("RUNE_TOP", rune, "P1"),
                ["RUNE_NEXT"] = Printed("RUNE_NEXT", "OGN·126/298", "P1") },
                PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                    ["P1"] = state.PlayerZones["P1"] with { RuneDeck = ["RUNE_TOP", "RUNE_NEXT"] } },
                ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) {
                    ["RUNE_TOP"] = new("P1", "RUNE_DECK"), ["RUNE_NEXT"] = new("P1", "RUNE_DECK") } };
        }
        state = LocalDestructionRecallTests.At(state, "ENEMY", "P2", "BATTLEFIELD", "OTHER");
        state = LocalDestructionRecallTests.At(state, "OTHER_ENEMY", "P2", "BATTLEFIELD", "OTHER");
        state = LocalDestructionRecallTests.At(state, "BASE_ENEMY", "P2", "BASE");
        return AttackDamageDistributionTests.Spell(state, "READY", "UNL-009/219", "P1");
    }

    private static async Task Attack(Driver game)
    {
        await game.Act("P1", new PlayCardCommand("UNIT", Fate, []));
        await game.Act("P1", new PlayCardCommand("READY", "UNL-009/219", ["UNIT"])); await game.Top();
        await game.Act("P1", new MoveUnitCommand("UNIT", "BASE", "BATTLEFIELD:OTHER"));
        Assert.Null(game.State.PendingCardChoice);
        Assert.Empty(Assert.Single(game.State.StackItems).TargetObjectIds);
    }

    [Theory]
    [InlineData("OGN·007/298", "ENEMY", 2, 1, false, 0)]
    [InlineData("OGN·089/298", null, 0, 0, false, 1)]
    [InlineData("OGN·214/298", "BASE_ENEMY", 0, 0, true, 0)]
    public async Task ARealAttackRevealsAndRecyclesBeforeTheIndependentlyConfirmedColorAbility(
        string rune, string? target, int direct, int area, bool stunned, int draws)
    {
        var game = new Driver(Position(rune));
        await Attack(game);
        Assert.Equal(new[] { "RUNE_TOP", "RUNE_NEXT" }, game.State.PlayerZones["P1"].RuneDeck);
        var hand = game.State.PlayerZones["P1"].Hand.Count;
        await game.Top();
        Assert.Equal(new[] { "RUNE_NEXT", "RUNE_TOP" }, game.State.PlayerZones["P1"].RuneDeck);
        Assert.Equal(0, game.State.CardObjects["ENEMY"].Damage);
        Assert.Equal(hand, game.State.PlayerZones["P1"].Hand.Count);
        if (target is not null)
        {
            var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
            Assert.Contains(target, choice.LegalObjectIds);
            if (direct > 0) Assert.DoesNotContain("BASE_ENEMY", choice.LegalObjectIds);
            Assert.DoesNotContain("UNIT", choice.LegalObjectIds);
            await game.Act(choice.PlayerId, new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [target]));
        }
        Assert.Single(game.State.StackItems);
        await game.Top();
        Assert.Equal(direct, game.State.CardObjects["ENEMY"].Damage);
        Assert.Equal(area, game.State.CardObjects["OTHER_ENEMY"].Damage);
        Assert.Equal(0, game.State.CardObjects["BASE_ENEMY"].Damage);
        Assert.Equal(stunned, game.State.CardObjects["BASE_ENEMY"].UntilEndOfTurnEffects.Contains("STUNNED"));
        Assert.Equal(hand + draws, game.State.PlayerZones["P1"].Hand.Count);
        await game.Replay();
    }

    [Theory]
    [InlineData(null)] [InlineData("OGN·042/298")] [InlineData("OGN·126/298")] [InlineData("OGN·166/298")]
    public async Task EmptyAndUnlistedRuneDomainsCreateNoColorAbility(string? rune)
    {
        var game = new Driver(Position(rune)); await Attack(game); await game.Top();
        Assert.Empty(game.State.StackItems); Assert.Null(game.State.PendingCardChoice);
        Assert.Equal(0, game.State.CardObjects["ENEMY"].Damage);
        Assert.Equal(0, game.State.CardObjects["OTHER_ENEMY"].Damage);
        if (rune is not null) Assert.Equal(new[] { "RUNE_NEXT", "RUNE_TOP" }, game.State.PlayerZones["P1"].RuneDeck);
        await game.Replay();
    }
    private static MatchState WithWard(MatchState state)
    {
        Assert.True(CardBehaviorRegistry.TryGetByCardNo("OGN·013/298", out var definition));
        var cards = state.CardObjects.ToDictionary();
        foreach (var id in new[] { "ENEMY", "OTHER_ENEMY", "BASE_ENEMY" })
        {
            var printed = Printed(id, "OGN·013/298", "P2");
            cards[id] = printed with { Tags = printed.Tags.Concat(definition.SourceUnitTags.Split('|'))
                .Distinct(StringComparer.Ordinal).ToArray() };
        }
        return state with { CardObjects = cards };
    }

    [Theory]
    [InlineData("OGN·007/298", true)] [InlineData("OGN·007/298", false)]
    [InlineData("OGN·214/298", true)] [InlineData("OGN·214/298", false)]
    public async Task OnlyTheDeclaredEnemyPaysWardAndDecliningDoesNotUndoTheRuneReveal(string rune, bool pay)
    {
        var game = new Driver(WithWard(Position(rune))); await Attack(game);
        var power = game.State.RunePools["P1"].TotalPower; var rng = game.State.RngCursor;
        await game.Top();
        Assert.Equal(power, game.State.RunePools["P1"].TotalPower); Assert.Equal(rng, game.State.RngCursor);
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        var target = rune == "OGN·007/298" ? "ENEMY" : "BASE_ENEMY";
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [target]));
        var payment = Assert.IsType<PendingPaymentState>(game.State.PendingPayment);
        Assert.Equal(1, payment.PowerCost); Assert.Equal(0, payment.ManaCost);
        foreach (var forged in new[] { payment with { PowerCost = 2 }, payment with { PowerCost = 0 },
            payment with { ManaCost = 1 }, payment with { PlayerId = "P2" } })
            Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(game.State with { PendingPayment = forged }));
        await game.Act("P1", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, [pay ? "PAY" : "DECLINE"]));
        Assert.Equal(power - (pay ? 1 : 0), game.State.RunePools["P1"].TotalPower);
        if (pay) await game.Top();
        Assert.Equal(0, game.State.CardObjects["ENEMY"].Damage);
        Assert.Equal(pay && rune == "OGN·007/298", game.State.PlayerZones["P2"].Graveyard.Contains("ENEMY"));
        if (pay && rune == "OGN·007/298") Assert.Contains(game.Events(), e => e.Event.Kind == "DAMAGE_APPLIED"
            && Equals(e.Event.Payload.GetValueOrDefault("targetObjectId"), "ENEMY")
            && Equals(e.Event.Payload.GetValueOrDefault("damage"), 2));
        Assert.Equal(pay && rune == "OGN·007/298" ? 1 : 0, game.State.CardObjects["OTHER_ENEMY"].Damage);
        Assert.Equal(pay && rune == "OGN·214/298", game.State.CardObjects["BASE_ENEMY"].UntilEndOfTurnEffects.Contains("STUNNED"));
        Assert.Equal(new[] { "RUNE_NEXT", "RUNE_TOP" }, game.State.PlayerZones["P1"].RuneDeck);
        await game.Replay();
    }

    [Theory]
    [InlineData("OGN·007/298")] [InlineData("OGN·089/298")] [InlineData("OGN·214/298")]
    public async Task TheAttackAndReflexiveSkillKeepTheirCapturedSourceAfterItReturnsToHand(string rune)
    {
        var state = AttackDamageDistributionTests.Spell(WithWard(Position(rune)), "RETURN", "OGN·104/298", "P1");
        // Reconsider also calls one rune: reserve that real instruction's rune
        // before the rune intended for the attack's subsequent reveal.
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["RETURN_RUNE"] = Printed("RETURN_RUNE", "OGN·042/298", "P1") },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P1"] = state.PlayerZones["P1"] with { RuneDeck = ["RETURN_RUNE", ..state.PlayerZones["P1"].RuneDeck] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) {
                ["RETURN_RUNE"] = new("P1", "RUNE_DECK") } };
        var game = new Driver(state); await Attack(game); var hand = game.State.PlayerZones["P1"].Hand.Count;
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["UNIT"])); await game.Top();
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Hand);
        var departure = game.Events().Length;
        await game.Top();
        Assert.Equal(new[] { "RUNE_NEXT", "RUNE_TOP" }, game.State.PlayerZones["P1"].RuneDeck);
        if (rune == "OGN·007/298")
        {
            Assert.Empty(game.State.StackItems); Assert.Null(game.State.PendingCardChoice);
            Assert.Equal(0, game.State.CardObjects["ENEMY"].Damage); Assert.Equal(0, game.State.CardObjects["OTHER_ENEMY"].Damage);
        }
        else
        {
            if (rune == "OGN·214/298")
            {
                var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
                await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["BASE_ENEMY"]));
                var payment = Assert.IsType<PendingPaymentState>(game.State.PendingPayment);
                await game.Act("P1", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, ["PAY"]));
            }
            await game.Top();
            Assert.Equal(rune == "OGN·214/298", game.State.CardObjects["BASE_ENEMY"].UntilEndOfTurnEffects.Contains("STUNNED"));
            Assert.Equal(hand + (rune == "OGN·089/298" ? 1 : 0), game.State.PlayerZones["P1"].Hand.Count);
        }
        Assert.DoesNotContain(game.Events().Skip(departure), e => e.Event.Kind == "STACK_ITEM_RESOLVED"
            && Equals(e.Event.Payload.GetValueOrDefault("sourceObjectId"), "UNIT"));
        await game.Replay();
    }

    [Fact]
    public async Task RecoveryRejectsAColorWithoutARevealAndMutatedRuneFacts()
    {
        var game = new Driver(Position("OGN·214/298")); await Attack(game);
        var root = Assert.Single(game.State.StackItems);
        Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(game.State with { StackItems = [root with {
            UnitEvent = root.UnitEvent! with { Mode = "RUNE_YELLOW" } }] }));
        await game.Top(); var item = Assert.Single(game.State.StackItems); var context = item.UnitEvent!; var fact = context.RevealedRune!;
        foreach (var invalid in new[] {
            context with { RevealedRune = fact with { Rune = fact.Rune with { Generation = fact.Rune.Generation + 1 } } },
            context with { RevealedRune = fact with { CardNo = "OGN·089/298" } },
            context with { RevealedRune = fact with { Traits = ["blue"] } },
            context with { RevealedRune = fact with { RevealedTick = game.State.Tick + 1 } },
            context with { ReflexiveTick = null }, context with { Mode = null }, context with { Mode = "RUNE_BLUE" } })
            Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(game.State with { StackItems = [item with { UnitEvent = invalid }] }));
        await game.Replay();
    }

    [Fact]
    public async Task LosingTheDeclaredRedTargetAlsoIgnoresTheLinkedOtherEnemyDamage()
    {
        var state = AttackDamageDistributionTests.Spell(Position("OGN·007/298"), "RETURN", "OGN·104/298", "P2");
        var game = new Driver(state); await Attack(game); await game.Top();
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ENEMY"]));
        if (game.State.PriorityPlayerId != "P2") await game.Act("P1", new PassPriorityCommand());
        await game.Act("P2", new PlayCardCommand("RETURN", "OGN·104/298", ["ENEMY"])); await game.Top(); await game.Top();
        Assert.Contains("ENEMY", game.State.PlayerZones["P2"].Hand);
        Assert.Equal(0, game.State.CardObjects["OTHER_ENEMY"].Damage);
        Assert.All(game.Events().Where(e => e.Event.Kind == "STACK_ITEM_RESOLVED"
            && Equals(e.Event.Payload.GetValueOrDefault("effectKind"), "CAPTURED_UNIT_EVENT_ABILITY")),
            e => Assert.DoesNotContain("\"ENEMY\"", System.Text.Json.JsonSerializer.Serialize(e.Event.Payload.GetValueOrDefault("targetObjectIds"))));
        await game.Replay();
    }

    [Theory]
    [InlineData("SFD·203/221")] [InlineData("SFD·250/221")]
    public async Task RecyclingTheRevealedDeckRuneTriggersTheOrdinaryRecycleObserverWithoutGeneratingPower(string legend)
    {
        var state = Position("OGN·089/298");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["SIVIR"] = Printed("SIVIR", legend, "P1") },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P1"] = state.PlayerZones["P1"] with { LegendZone = ["SIVIR"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) {
                ["SIVIR"] = new("P1", "LEGEND_ZONE") } };
        var game = new Driver(state); await Attack(game);
        var mana = game.State.RunePools["P1"].Mana; var power = game.State.RunePools["P1"].TotalPower;
        var hand = game.State.PlayerZones["P1"].Hand.Count;
        await game.Top();
        Assert.Single(game.Events(), e => e.Event.Kind == "TRIGGER_QUEUED"
            && Equals(e.Event.Payload.GetValueOrDefault("effectKind"), "SIVIR_RUNE_RECYCLE_GOLD"));
        for (var step = 0; step < 8; step++)
        {
            if (game.PromptFor("P1").EnabledActions().Contains(CommandTypes.OrderTriggers))
                await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: game.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
            else if (game.State.PendingCardChoice is { ChoiceWindow: "TRIGGER_COST_CONFIRMATION" } choice)
                await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, []));
            else if (game.State.StackItems.Count > 0 && game.State.PriorityPlayerId is not null) await game.Top();
            else break;
        }
        Assert.Empty(game.State.StackItems); Assert.Empty(game.State.TriggerQueue);
        Assert.Null(game.State.PendingCardChoice); Assert.False(game.State.CardObjects["SIVIR"].IsExhausted);
        Assert.Equal(mana, game.State.RunePools["P1"].Mana); Assert.Equal(power, game.State.RunePools["P1"].TotalPower);
        Assert.Equal(hand + 1, game.State.PlayerZones["P1"].Hand.Count); await game.Replay();
    }

    [Fact]
    public async Task CopiedAttackTextStillRevealsTwoRunesAfterTheCopyingGearIsDetached()
    {
        var state = AttackDamageDistributionTests.Spell(Position("OGN·089/298"), "COPY", "SFD·059/221", "P1");
        state = AttackDamageDistributionTests.Spell(state, "DETACH", "SFD·011/221", "P1");
        var game = new Driver(state);
        await game.Act("P1", new PlayCardCommand("UNIT", Fate, []));
        await game.Act("P1", new PlayCardCommand("COPY", "SFD·059/221", []));
        if (game.State.StackItems.Count > 0) await game.Top();
        await game.Act("P1", new AssembleEquipmentCommand("COPY", "UNIT", ["ASSEMBLE_GREEN"])); await game.Top();
        await game.Act("P1", new PlayCardCommand("READY", "UNL-009/219", ["UNIT"])); await game.Top();
        await game.Act("P1", new MoveUnitCommand("UNIT", "BASE", "BATTLEFIELD:OTHER"));
        if (game.PromptFor("P1").EnabledActions().Contains(CommandTypes.OrderTriggers))
            await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: game.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
        Assert.Equal(2, game.State.StackItems.Count(i => i.UnitEvent?.CardNo == Fate));
        Assert.Single(game.State.StackItems, i => i.UnitEvent?.Origin is not null);
        await game.Act("P1", new PlayCardCommand("DETACH", "SFD·011/221", ["UNIT", "COPY"])); await game.Top();
        Assert.Null(game.State.CardObjects["COPY"].AttachedToObjectId);
        var hand = game.State.PlayerZones["P1"].Hand.Count;
        for (var i = 0; i < 4 && game.State.StackItems.Count > 0; i++) await game.Top();
        Assert.Empty(game.State.StackItems); Assert.Equal(hand + 1, game.State.PlayerZones["P1"].Hand.Count);
        Assert.Equal(new[] { "RUNE_TOP", "RUNE_NEXT" }, game.State.PlayerZones["P1"].RuneDeck);
        Assert.Equal(2, game.Events().Count(e => e.Event.Kind == "CARDS_REVEALED"
            && Equals(e.Event.Payload.GetValueOrDefault("zone"), "RUNE_DECK")));
        await game.Replay();
    }

}
