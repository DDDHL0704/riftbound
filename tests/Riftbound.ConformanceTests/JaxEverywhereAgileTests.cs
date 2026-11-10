using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

// CN SFD errata260114 p5; CN191.4-191.5, 383, 388-389, 435, 819.2.
public sealed class JaxEverywhereAgileTests
{
    private const string Jax = "SFD·054/221", Shield = "SFD·033/221";
    private static MatchState Position(string no = Jax, string gear = Shield)
    {
        var state = RadGearTestPosition.Put(RadGearTestPosition.Create(), "JAX", no, "P1", "BASE");
        state = RadGearTestPosition.Put(state, "GEAR", gear, "P1", "HAND");
        state = RadGearTestPosition.Put(state, "RETURN", "OGN·104/298", "P1", "HAND");
        return CoreRuleEngine.RecomputeRuleDerivedKeywords(state);
    }

    private static async Task Confirm(Driver game, string host = "ALLY")
    {
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        Assert.Equal("TRIGGER_CONFIRMATION", choice.ChoiceWindow);
        Assert.Null(game.State.PriorityPlayerId);
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [host]));
    }

    [Theory, InlineData(Jax), InlineData("SFD·054a/221")]
    public async Task BothPrintingsGrantReactionAndAnIndependentPostEntryAttachment(string print)
    {
        var game = new Driver(Position(print));
        Assert.Contains("灵便", game.State.CardObjects["GEAR"].Tags);
        var pool = game.State.RunePools["P1"];
        await game.Act("P1", new PlayCardCommand("GEAR", Shield, []));
        Assert.Contains("GEAR", game.State.PlayerZones["P1"].Base);
        Assert.True(game.State.RunePools["P1"].Mana < pool.Mana);
        Assert.Null(game.State.CardObjects["GEAR"].AttachedToObjectId);
        Assert.Equal("AGILE_PLAY", Assert.Single(game.State.StackItems).UnitEvent!.Timing);
        await RadGearTestPosition.Reject(game.State, "P2", new PassPriorityCommand());
        await Confirm(game); await game.Top();
        Assert.Equal("ALLY", game.State.CardObjects["GEAR"].AttachedToObjectId);
        Assert.Single(game.Events(), row => row.Event.Kind == "EQUIPMENT_ATTACHED");
        await game.ColdCheckpoint();
    }

    [Theory, InlineData("HAND"), InlineData("MAIN_DECK"), InlineData("GRAVEYARD"),
     InlineData("BANISHED"), InlineData("BASE"), InlineData("BATTLEFIELD")]
    public void TheEffectiveKeywordAppliesInEveryPlaceButOnlyToYourArmaments(string zone)
    {
        var state = RadUnitEffectTestFixture.Place(Position(), "GEAR", "P1", zone, zone == "BATTLEFIELD" ? "BF" : null);
        state = RadGearTestPosition.Put(state, "NON-WEAPON", "OGN·023/298", "P1", zone, zone == "BATTLEFIELD" ? "BF" : null);
        state = RadGearTestPosition.Put(state, "THEIR-WEAPON", Shield, "P2", zone, zone == "BATTLEFIELD" ? "OTHER" : null);
        var before = MatchStateHasher.Hash(state);
        var current = CoreRuleEngine.RecomputeRuleDerivedKeywords(state);
        Assert.Equal(before, MatchStateHasher.Hash(state));
        Assert.Contains("灵便", current.CardObjects["GEAR"].Tags);
        Assert.DoesNotContain("灵便", current.CardObjects["NON-WEAPON"].Tags);
        Assert.DoesNotContain("灵便", current.CardObjects["THEIR-WEAPON"].Tags);
        if (zone is "HAND" or "MAIN_DECK")
            Assert.DoesNotContain("GEAR", JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(current)["P2"]));
    }

    [Theory, InlineData(false), InlineData(true)]
    public void FieldControlAndOffFieldOwnershipSelectDifferentGrantControllers(bool field)
    {
        var state = Position();
        if (field) state = RadUnitEffectTestFixture.Place(state, "GEAR", "P2", "BASE");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["GEAR"] = state.CardObjects["GEAR"] with { OwnerId = "P1", ControllerId = "P2" } } };
        var current = CoreRuleEngine.RecomputeRuleDerivedKeywords(state);
        Assert.Equal(!field, current.CardObjects["GEAR"].Tags.Contains("灵便"));
    }

    [Fact]
    public async Task ReactionCanInterruptARealResponseWindowAndDoesNotResolveTheOlderSpell()
    {
        var game = new Driver(Position());
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["JAX"]));
        var older = Assert.Single(game.State.StackItems).StackItemId;
        await game.Act("P1", new PlayCardCommand("GEAR", Shield, []));
        await Confirm(game); await game.Top();
        Assert.Equal(older, Assert.Single(game.State.StackItems).StackItemId);
        Assert.Equal("ALLY", game.State.CardObjects["GEAR"].AttachedToObjectId);
        Assert.Contains("JAX", game.State.PlayerZones["P1"].Base);
        await game.Top(); Assert.Contains("JAX", game.State.PlayerZones["P1"].Hand);
        await game.ColdCheckpoint();
    }

    [Theory, InlineData(false), InlineData(true)]
    public async Task AnEnemyGrantOrNonArmamentCannotBorrowTheReactionPermission(bool enemyGrant)
    {
        var initial = Position(gear: enemyGrant ? Shield : "OGN·023/298");
        if (enemyGrant) initial = RadUnitEffectTestFixture.Place(initial, "JAX", "P2", "BASE");
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["ALLY"]));
        await RadGearTestPosition.Reject(game.State, "P1", new PlayCardCommand("GEAR", enemyGrant ? Shield : "OGN·023/298", []));
        await game.Top(); await game.ColdCheckpoint();
    }

    [Fact]
    public async Task RemovingTheGrantorEndsTheAuraButKeepsTheAlreadyEarnedSkill()
    {
        var initial = RadGearTestPosition.Put(Position(), "NEXT", Shield, "P1", "HAND");
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("GEAR", Shield, [])); await Confirm(game);
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["JAX"])); await game.Top();
        Assert.Contains("JAX", game.State.PlayerZones["P1"].Hand);
        Assert.DoesNotContain("灵便", game.State.CardObjects["GEAR"].Tags);
        Assert.DoesNotContain("灵便", game.State.CardObjects["NEXT"].Tags);
        await RadGearTestPosition.Reject(game.State, "P1", new PlayCardCommand("NEXT", Shield, []));
        await game.Top(); Assert.Equal("ALLY", game.State.CardObjects["GEAR"].AttachedToObjectId);
        await game.ColdCheckpoint();
    }

    [Fact]
    public async Task ReturningThePlayedEquipmentCannotAttachItsNewIncarnation()
    {
        var initial = RadGearTestPosition.Put(Position(), "HOST", "RAD·063/167", "P1", "BASE");
        initial = RadGearTestPosition.Put(initial, "RECALL", "SFD·135/221", "P1", "HAND");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["BF"] = initial.CardObjects["BF"] with { ControllerId = "P2" } } };
        var game = new Driver(initial);
        await game.Act("P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:BF", []));
        Assert.True(game.State.SpellDuelState.IsActive);
        await game.Act("P1", new PlayCardCommand("GEAR", Shield, [])); await Confirm(game);
        var original = game.State.CardObjects["GEAR"].ObjectGeneration;
        // The live Kai'Sa in this showdown grants Reaction to the Swift spell.
        // An ordinary satchel activation could not interrupt this skill.
        await game.Act("P1", new PlayCardCommand("RECALL", "SFD·135/221", ["GEAR"]));
        await game.Top();
        Assert.Contains("GEAR", game.State.PlayerZones["P1"].Hand);
        Assert.True(game.State.CardObjects["GEAR"].ObjectGeneration > original);
        await game.Top(); Assert.Null(game.State.CardObjects["GEAR"].AttachedToObjectId);
        Assert.DoesNotContain(game.Events(), row => row.Event.Kind == "EQUIPMENT_ATTACHED");
        await game.ColdCheckpoint();
    }

    [Theory, InlineData(false), InlineData(true)]
    public async Task PrintedAgileAndSeveralGrantorsStillEarnOnlyOneAttachment(bool duplicateGrant)
    {
        var initial = Position(gear: "SFD·022/221");
        if (duplicateGrant) initial = RadGearTestPosition.Put(initial, "JAX-2", Jax, "P1", "BASE");
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("GEAR", "SFD·022/221", []));
        Assert.Single(game.State.StackItems); Assert.Empty(game.State.TriggerQueue);
        await Confirm(game); await game.Top();
        Assert.Single(game.Events(), row => row.Event.Kind == "EQUIPMENT_ATTACHED");
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["JAX"])); await game.Top();
        Assert.Contains("灵便", game.State.CardObjects["GEAR"].Tags);
        await game.ColdCheckpoint();
    }

    [Theory, InlineData(false), InlineData(true)]
    public async Task AnIntrinsicArmamentPlaySkillAndGrantedAgileHaveIndependentChosenOrder(bool agileFirst)
    {
        const string song = "SFD·192/221";
        var initial = Position(gear: song);
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["ALLY"] = initial.CardObjects["ALLY"] with { IsExhausted = true } } };
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("GEAR", song, []));
        Assert.Equal(2, game.State.TriggerQueue.Count); Assert.Null(game.State.PendingCardChoice);
        var agile = Assert.Single(game.State.TriggerQueue, t => t.UnitEvent?.Timing == "AGILE_PLAY");
        var innate = Assert.Single(game.State.TriggerQueue, t => t.PermanentPlayStackItemId is not null);
        await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: agileFirst
            ? [agile.TriggerId, innate.TriggerId] : [innate.TriggerId, agile.TriggerId]));
        await Confirm(game); await game.Top();
        Assert.Equal(agileFirst ? "ALLY" : null, game.State.CardObjects["GEAR"].AttachedToObjectId);
        Assert.Equal(agileFirst, game.State.CardObjects["ALLY"].IsExhausted);
        await game.Top(); Assert.Equal("ALLY", game.State.CardObjects["GEAR"].AttachedToObjectId);
        Assert.False(game.State.CardObjects["ALLY"].IsExhausted);
        await game.ColdCheckpoint();
    }

    [Theory, InlineData("erase"), InlineData("duplicate"), InlineData("grant"), InlineData("grant-generation"),
     InlineData("controller"), InlineData("birth"), InlineData("no-grant"), InlineData("resolved-play"),
     InlineData("null-grant-no"), InlineData("null-no"), InlineData("null-grant-source"),
     InlineData("empty-grant-source"), InlineData("wrong-grant-token")]
    public async Task OriginalAcceptedBirthAndEntitlementCannotBeForgedOrErased(string mutation)
    {
        var game = new Driver(Position());
        await game.Act("P1", new PlayCardCommand("GEAR", Shield, []));
        var item = Assert.Single(game.State.StackItems); var context = item.UnitEvent!;
        var changed = item with { UnitEvent = mutation switch {
            "grant" => context with { AgilePlayGrant = context.AgilePlayGrant! with { CardNo = "OGN·219/298" } },
            "grant-generation" => context with { AgilePlayGrant = context.AgilePlayGrant! with { Source = new("JAX", 999) } },
            "birth" => context with { CapturedTick = context.CapturedTick + 1 },
            "null-grant-no" => context with { AgilePlayGrant = context.AgilePlayGrant! with { CardNo = null! } },
            "null-no" => context with { CardNo = null! },
            "null-grant-source" => context with { AgilePlayGrant = context.AgilePlayGrant! with { Source = null! } },
            "empty-grant-source" => context with { AgilePlayGrant = context.AgilePlayGrant! with { Source = new("", 0) } },
            "wrong-grant-token" => context with { AgilePlayGrant = context.AgilePlayGrant! with { TokenFactoryCardNo = P6TokenFactoryCatalog.ImageTokenCardNo } },
            "no-grant" => context with { AgilePlayGrant = null }, _ => context },
            ControllerId = mutation == "controller" ? "P2" : item.ControllerId };
        var forged = mutation switch {
            "erase" => game.State with { StackItems = [], PendingCardChoice = null },
            "duplicate" => game.State with { StackItems = [item, item with { StackItemId = item.StackItemId + "-clone" }] },
            "resolved-play" => game.State with { CardPlays = game.State.CardPlays! with { Resolved = [] } },
            _ => game.State with { StackItems = [changed] } };
        Assert.NotEmpty(ArmamentGrantedCombatTriggerTests.Errors(game, forged));
        await Confirm(game); await game.Top();
        Assert.NotEmpty(ArmamentGrantedCombatTriggerTests.Errors(game, game.State with { StackItems = [item] }));
        await game.ColdCheckpoint();
    }

    [Fact]
    public async Task NoFriendlyHostDiscardsOnlyTheSkillAndItCannotBeResurrectedLater()
    {
        var initial = RadUnitEffectTestFixture.Place(Position(), "ALLY", "P1", "GRAVEYARD");
        // Component state for CN137.3.b: active printed text remains after
        // losing the unit type; this grantor is not a legal attachment host.
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["JAX"] = initial.CardObjects["JAX"] with { Tags = [CardObjectTags.EquipmentCard] } } };
        initial = RadGearTestPosition.Put(initial, "LATER", "OGN·219/298", "P1", "HAND");
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("GEAR", Shield, []));
        Assert.Contains("GEAR", game.State.PlayerZones["P1"].Base);
        Assert.Empty(game.State.StackItems); Assert.Null(game.State.PendingCardChoice);
        var birth = Assert.Single(game.Events(), row => row.Event.Kind == "TRIGGER_QUEUED");
        Assert.Contains(game.Events(), row => row.Event.Kind == "TRIGGER_NOT_CONFIRMED");
        var capture = new UnitEventContext(Shield, game.State.CardObjects["GEAR"].ObjectGeneration, 0,
            game.State.Tick, "AGILE_PLAY") { AgilePlayGrant = new(new("JAX", 0), Jax, 0, null) };
        Assert.Equal(birth.Event.Payload["observedUnitEventCaptureHash"],
            CoreRuleEngine.ObservedUnitEventCaptureHash(capture, "GEAR", "P1"));
        var discarded = new StackItemState("ordered-" + birth.Event.Payload["triggerId"], "P1", "GEAR",
            "CAPTURED_UNIT_EVENT_ABILITY", Shield) { UnitEvent = capture };
        await game.Act("P1", new PlayCardCommand("LATER", "OGN·219/298", []));
        Assert.Null(game.State.CardObjects["GEAR"].AttachedToObjectId);
        Assert.Contains(ArmamentGrantedCombatTriggerTests.Errors(game, game.State with { StackItems = [discarded] }),
            error => error.Contains("observed unit event differs from original accepted capture", StringComparison.Ordinal));
        await game.ColdCheckpoint();
    }

    [Theory, InlineData(false), InlineData(true)]
    public async Task ARealCopiedImageGrantsAgileAndItsDepartureDoesNotCancelTheEarnedSkill(bool sourceLeaves)
    {
        var initial = RadUnitEffectTestFixture.Place(Position(), "JAX", "P2", "BASE");
        initial = RadGearTestPosition.Put(initial, "COPY", "UNL-200/219", "P1", "HAND");
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("COPY", "UNL-200/219", ["JAX"]));
        await game.Top(); await game.Top();
        var image = Assert.Single(game.State.CardObjects.Values,
            card => card.TokenFactoryCardNo == P6TokenFactoryCatalog.ImageTokenCardNo);
        Assert.Equal(Jax, image.CardNo); Assert.Contains("灵便", game.State.CardObjects["GEAR"].Tags);
        await game.Act("P1", new PlayCardCommand("GEAR", Shield, [])); await Confirm(game);
        var grant = Assert.Single(game.State.StackItems).UnitEvent!.AgilePlayGrant!;
        Assert.Equal(image.ObjectId, grant.Source.ObjectId);
        Assert.Equal(P6TokenFactoryCatalog.ImageTokenCardNo, grant.TokenFactoryCardNo);
        if (sourceLeaves) {
            await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", [image.ObjectId])); await game.Top();
            Assert.False(game.State.CardObjects.ContainsKey(image.ObjectId));
        }
        await game.Top(); Assert.Equal("ALLY", game.State.CardObjects["GEAR"].AttachedToObjectId);
        await game.ColdCheckpoint();
    }

    [Theory, InlineData(false), InlineData(true)]
    public void RetainedAttachedPrintedTextRemainsActiveWhenItsHostLosesTheUnitType(bool nonUnit)
    {
        var initial = RadGearTestPosition.Put(Position(), "SONG", "SFD·059/221", "P1", "BASE");
        // Component frame for the retained text and a changed current face;
        // actual copy-command history is covered separately by the Image case.
        initial = initial with { Tick = 1, CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["JAX"] = initial.CardObjects["JAX"] with { CardNo = "OGN·210/298",
                CopyFaceTransitions = [new(1, 0, Jax, "OGN·210/298")],
                Tags = nonUnit ? [CardObjectTags.EquipmentCard] : [CardObjectTags.UnitCard] },
            ["SONG"] = initial.CardObjects["SONG"] with { AttachedToObjectId = "JAX", AttachmentRevision = 1,
                AttachedRuleText = new(new("JAX", 0), Jax, 1, 0, 0) } } };
        var current = CoreRuleEngine.RecomputeRuleDerivedKeywords(initial);
        Assert.Contains("灵便", current.CardObjects["GEAR"].Tags);
        var broken = current with { CardObjects = new Dictionary<string, CardObjectState>(current.CardObjects) {
            ["SONG"] = current.CardObjects["SONG"] with { AttachmentRevision = 2 } } };
        Assert.DoesNotContain("灵便", CoreRuleEngine.RecomputeRuleDerivedKeywords(broken).CardObjects["GEAR"].Tags);
    }

    [Theory, InlineData("attached"), InlineData("hand"), InlineData("face-down")]
    public void InactiveOrPrivatePrintedSourcesCannotGrantAnAllPlacesKeyword(string location)
    {
        var state = Position();
        if (location == "hand") state = RadUnitEffectTestFixture.Place(state, "JAX", "P1", "HAND");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["JAX"] = state.CardObjects["JAX"] with { IsFaceDown = location == "face-down",
                Tags = location == "attached" ? [CardObjectTags.EquipmentCard] : state.CardObjects["JAX"].Tags,
                AttachedToObjectId = location == "attached" ? "ALLY" : null } } };
        Assert.DoesNotContain("灵便", CoreRuleEngine.RecomputeRuleDerivedKeywords(state).CardObjects["GEAR"].Tags);
    }

    [Fact]
    public async Task TheRuntimeCatalogUsesTheReviewedAllPlacesErratumAndKeepsWard()
    {
        var catalog = await OfficialCardCatalog.LoadRuntimeAsync();
        foreach (var no in new[] { Jax, "SFD·054a/221" }) {
            var card = Assert.Single(catalog.Cards, c => c.CardNo == no);
            Assert.Contains("你各处的武装都获得{{灵便}}。", card.CardEffect);
            Assert.Contains("{{法盾}}", card.CardEffect);
            Assert.Single(EquipmentKeywordGrantParser.Parse(card.CardEffect), spec => spec.AllPlaces
                && spec.RequiredKeyword == "武装" && spec.GrantedKeyword == "灵便");
        }
        Assert.Empty(EquipmentKeywordGrantParser.Parse("你手牌中的每一件武装都获得{{灵便}}。"));
    }
}
