using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;

namespace Riftbound.ConformanceTests;

public sealed class OfficialInformationPermissionTests
{
    [Theory]
    [InlineData("cost")]
    [InlineData("destroy")]
    [InlineData("damage")]
    public async Task DeathCapturesControllerThenRevealsHandAndGrantsOnlyFieldInspection(string method)
    {
        var initial = Position(); Assert.DoesNotContain("UNL-013/219", View(initial, "P1"));
        var died = await Destroy(initial, method);
        Assert.Contains("V", died.State.PlayerZones["P2"].Graveyard);
        Assert.Single(died.State.StackItems); Assert.Equal("P1", died.State.StackItems.Single().ControllerId);
        Assert.Equal(0, died.State.PlayerExperience.GetValueOrDefault("P1"));
        Assert.Empty(died.State.FaceDownLookPermissions); Restore(died.State);
        var opened = await Top(died.State);
        Assert.Contains("UNL-200/219", View(opened.State, "P1"));
        Assert.DoesNotContain("UNL-013/219", View(opened.State, "P1"));
        Assert.Empty(opened.State.PendingCardChoice!.LegalObjectIds); Restore(opened.State);
        var done = await Acknowledge(opened.State);
        Assert.Single(done.State.FaceDownLookPermissions);
        Assert.Contains("GAINED_EXPERIENCE_THIS_TURN:P1", done.State.UntilEndOfTurnEffects);
        Assert.Equal(1, done.State.PlayerExperience["P1"]); Assert.Equal(0, done.State.PlayerExperience.GetValueOrDefault("P2"));
        Assert.Contains("UNL-013/219", View(done.State, "P1"));
        Assert.DoesNotContain("UNL-200/219", View(done.State, "P1"));
        Assert.DoesNotContain("OGN·097/298", View(done.State, "P1"));
        Assert.DoesNotContain("UNL-013/219", JsonSerializer.Serialize(ResolutionResult.BuildSpectatorSnapshot(done.State)));
        Assert.True(done.State.CardObjects["STANDBY-X"].IsFaceDown);
        Assert.Contains(CardObjectTags.Standby, done.State.CardObjects["STANDBY-X"].Tags);
        Restore(done.State); ValidateViews(done.State);
    }

    [Fact]
    public async Task PermissionIncludesLaterStandbyButExpiresAtActualTurnTransition()
    {
        var done = await Complete();
        var state = done.State with { CardObjects = new Dictionary<string, CardObjectState>(done.State.CardObjects) {
            ["LATER"] = done.State.CardObjects["STANDBY-X"] with { ObjectId = "LATER", CardNo = "UNL-139/219" } },
            PlayerZones = new Dictionary<string, PlayerZones>(done.State.PlayerZones) { ["P2"] = done.State.PlayerZones["P2"] with { Battlefields = ["BF2", "STANDBY-X", "LATER"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(done.State.ObjectLocations) { ["LATER"] = new("P2", "BATTLEFIELD", "BF2") } };
        Assert.Contains("LATER", View(state, "P1"));
        var next = await Act(state, "P1", new EndTurnCommand());
        Assert.True(next.State.TurnNumber > state.TurnNumber);
        Assert.Empty(next.State.FaceDownLookPermissions); Assert.DoesNotContain("LATER", View(next.State, "P1"));
        Assert.DoesNotContain("UNL-013/219", View(next.State, "P1")); Restore(next.State);
    }

    [Fact]
    public async Task OwningAnOpponentsStandbyDoesNotGrantControlOrInspection()
    {
        var initial = Position();
        var state = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["STANDBY-X"] = initial.CardObjects["STANDBY-X"] with { OwnerId = "P1" } } };
        Assert.DoesNotContain("UNL-013/219", View(state, "P1"));
        Assert.Contains("UNL-013/219", View(state, "P2"));
        var done = await Complete(state); Assert.Contains("UNL-013/219", View(done.State, "P1"));
        var invalid = await new CoreRuleEngine().ResolveAsync(done.State, new("not-yours", "P1", CommandTypes.PlayCard),
            new PlayCardCommand("STANDBY-X", "UNL-013/219", []), default);
        Assert.False(invalid.Accepted); Assert.Equal(MatchStateHasher.Hash(done.State), MatchStateHasher.Hash(invalid.State));
    }

    [Theory]
    [InlineData("viewer")]
    [InlineData("turn")]
    [InlineData("source")]
    [InlineData("generation")]
    public async Task RecoveryRejectsForgedPermission(string mutation)
    {
        var done = await Complete(); var p = done.State.FaceDownLookPermissions.Single();
        p = mutation switch {
            "viewer" => p with { ViewerId = "P2" },
            "turn" => p with { TurnNumber = p.TurnNumber + 1 },
            "source" => p with { Source = p.Source with { CardNo = "UNL-062/219" } },
            _ => p with { Source = p.Source with { SourceGeneration = -1 } }
        };
        Assert.Contains("invalid face-down look permission", OfficialInsightAndSpellLockTests.Errors(done.State with { FaceDownLookPermissions = [p] }));
    }

    [Fact]
    public async Task EmptyHandStillGrantsExperienceAndDoesNotRevealOtherZones()
    {
        var initial = Position(); initial = initial with { PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
            ["P2"] = initial.PlayerZones["P2"] with { Hand = [], Graveyard = ["U", "SPELL"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) { ["U"] = new("P2", "GRAVEYARD"), ["SPELL"] = new("P2", "GRAVEYARD") } };
        var done = await Complete(initial); Assert.Equal(1, done.State.PlayerExperience["P1"]);
        Assert.DoesNotContain("OGN·097/298", View(done.State, "P1")); Restore(done.State);
    }

    [Fact]
    public async Task BanishReplacementDoesNotTriggerLastBreath()
    {
        var s = Position();
        s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
            ["V"] = s.CardObjects["V"] with { UntilEndOfTurnEffects = ["BANISH_IF_DESTROYED_THIS_TURN"] } } };
        var done = await Destroy(s, "destroy");
        Assert.Contains("V", done.State.PlayerZones["P2"].Banished);
        Assert.Empty(done.State.StackItems); Assert.Empty(done.State.TriggerQueue);
        Assert.Empty(done.State.FaceDownLookPermissions); Assert.Null(done.State.PendingCardChoice); Restore(done.State);
    }

    [Fact]
    public async Task TwoSimultaneousDeathsKeepTwoIndependentEffectsAndExperienceAwards()
    {
        var s = Position();
        s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
            ["V2"] = s.CardObjects["V"] with { ObjectId = "V2" }, ["C"] = s.CardObjects["C"] with { CardNo = "UNL-180/219" } },
            PlayerZones = new Dictionary<string, PlayerZones>(s.PlayerZones) { ["P1"] = s.PlayerZones["P1"] with { Base = ["MALZ", "V", "V2"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(s.ObjectLocations) { ["V2"] = new("P1", "BASE") } };
        var cast = await Act(s, "P1", new PlayCardCommand("C", "UNL-180/219", [])); var killed = await Top(cast.State);
        Assert.Equal(2, killed.State.TriggerQueue.Count(t => t.DeathRevealContext is not null)); Restore(killed.State);
        var ordered = await Act(killed.State, "P1", new OrderTriggersCommand(OrderedTriggerIds: killed.State.TriggerQueue.Select(t => t.TriggerId).Reverse().ToArray()));
        for (var i = 0; i < 2; i++) { var open = await Top(ordered.State); ordered = await Acknowledge(open.State); Restore(ordered.State); }
        Assert.Equal(2, ordered.State.PlayerExperience["P1"]); Assert.Equal(2, ordered.State.FaceDownLookPermissions.Count);
        Assert.DoesNotContain(ordered.State.StackItems, item => item.DeathRevealContext is not null);
        while (ordered.State.StackItems.Count > 0) ordered = await Top(ordered.State); // The legend also triggers on the nine-mana spell.
        Assert.Empty(ordered.State.StackItems); Restore(ordered.State); Assert.Contains("UNL-013/219", View(ordered.State, "P1"));
    }

    [Fact]
    public async Task OpposingControllerGetsItsOwnChoiceAndExperience()
    {
        var s = Position();
        s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
            ["V"] = s.CardObjects["V"] with { OwnerId = "P1", ControllerId = "P2" } },
            PlayerZones = new Dictionary<string, PlayerZones>(s.PlayerZones) { ["P1"] = s.PlayerZones["P1"] with { Base = ["MALZ"] },
                ["P2"] = s.PlayerZones["P2"] with { Base = ["ALLY", "V"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(s.ObjectLocations) { ["V"] = new("P2", "BASE") } };
        var died = await Destroy(s, "destroy"); var open = await Top(died.State);
        Assert.Equal("P2", open.State.PendingCardChoice!.PlayerId);
        var done = await Act(open.State, "P2", new ChooseCardsCommand(open.State.PendingCardChoice.ChoiceId, "REVEALED_HAND_EFFECT", []));
        Assert.Equal(1, done.State.PlayerExperience["P2"]); Assert.Equal(0, done.State.PlayerExperience["P1"]);
        Assert.Equal("P2", done.State.FaceDownLookPermissions.Single().ViewerId); Assert.Equal("P1", done.State.FaceDownLookPermissions.Single().SubjectId); Restore(done.State);
    }

    [Fact]
    public async Task CombatDeathOpensTheDefendersLastBreathAndCompletesBattle()
    {
        var s = DeathAndDuelLifecycleTests.State();
        s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
            ["D"] = s.CardObjects["D"] with { CardNo = "UNL-053/219", Power = 0 },
            ["A"] = s.CardObjects["A"] with { Power = 12 } } };
        var moved = await Act(s, "P1", new MoveUnitCommand("A", "BASE", "BATTLEFIELD:BF", []));
        var first = await Act(moved.State, "P1", new PassFocusCommand());
        var second = await Act(first.State, "P2", new PassFocusCommand());
        var response = await Act(second.State, "P1", new DeclareBattleCommand("BF", ["A"], ["D"], ["COMBAT_ASSIGNMENT"]));
        var death = await Top(response.State);
        Assert.Contains("D", death.State.PlayerZones["P2"].Graveyard);
        Assert.Single(death.State.StackItems, item => item.DeathRevealContext is not null); Restore(death.State);
        var open = await Top(death.State); Assert.Equal("P2", open.State.PendingCardChoice!.PlayerId);
        var done = await Act(open.State, "P2", new ChooseCardsCommand(open.State.PendingCardChoice.ChoiceId, "REVEALED_HAND_EFFECT", []));
        Assert.Equal(1, done.State.PlayerExperience["P2"]); Assert.False(done.State.BattleState.IsActive);
        Assert.Null(done.State.PendingCardChoice); Assert.Empty(done.State.StackItems); Restore(done.State);
    }

    [Fact]
    public async Task FullGamePrivacyGuardAllowsOnlyCurrentRevealedGenerationsAndNeverDeckIdentities()
    {
        var initial = OfficialRevealedHandChoiceTests.Position();
        var open = await OfficialRevealedHandChoiceTests.Open(initial);
        FullGameEndToEndTests.AssertNoHiddenZoneLeak(open);
        Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => FullGameEndToEndTests.AssertNoHiddenZoneLeak(open with { State = initial }));
        var replaced = open.State with { CardObjects = new Dictionary<string, CardObjectState>(open.State.CardObjects) {
            ["SPELL"] = open.State.CardObjects["SPELL"] with { ObjectGeneration = 99 } } };
        Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => FullGameEndToEndTests.AssertNoHiddenZoneLeak(open with { State = replaced }));
        var deck = open.State with { PlayerZones = new Dictionary<string, PlayerZones>(open.State.PlayerZones) {
            ["P2"] = open.State.PlayerZones["P2"] with { Hand = ["U"], MainDeck = ["DECK", "SPELL"] } } };
        Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => FullGameEndToEndTests.AssertNoHiddenZoneLeak(open with { State = deck }));
        var done = await OfficialRevealedHandChoiceTests.Select(open.State, "SPELL");
        FullGameEndToEndTests.AssertNoHiddenZoneLeak(done);
        Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => FullGameEndToEndTests.AssertNoHiddenZoneLeak(done with { Snapshots = open.Snapshots }));
    }

    internal static MatchState Position()
    {
        var s = OfficialRevealedHandPlayTests.Position();
        return s with { PlayerExperience = new Dictionary<string, int> { ["P1"] = 0, ["P2"] = 0 },
            RunePools = new Dictionary<string, RunePool> { ["P1"] = new(20, 20), ["P2"] = new(20, 20) },
            CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
                ["MALZ"] = new("MALZ", cardNo: "OGN·113/298", ownerId: "P1", controllerId: "P1", power: 3, tags: [CardObjectTags.UnitCard]),
                ["V"] = new("V", cardNo: "UNL-053/219", ownerId: "P2", controllerId: "P1", power: 0, tags: [CardObjectTags.UnitCard]),
                ["STANDBY-X"] = new("STANDBY-X", cardNo: "UNL-013/219", ownerId: "P2", controllerId: "P2", isFaceDown: true, tags: [CardObjectTags.SpellCard, CardObjectTags.Standby]) },
            PlayerZones = new Dictionary<string, PlayerZones>(s.PlayerZones) { ["P1"] = s.PlayerZones["P1"] with { Base = ["MALZ", "V"] },
                ["P2"] = s.PlayerZones["P2"] with { Battlefields = ["BF2", "STANDBY-X"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(s.ObjectLocations) {
                ["MALZ"] = new("P1", "BASE"), ["V"] = new("P1", "BASE"), ["STANDBY-X"] = new("P2", "BATTLEFIELD", "BF2") } };
    }
    internal static async Task<ResolutionResult> Destroy(MatchState s, string method = "cost")
    {
        if (method == "cost") return await Act(s, "P1", new ActivateAbilityCommand("MALZ", P4ActivatedAbilityCatalog.MalzaharResourceAbilityId, ["V"]));
        if (method == "damage") s = s with {
            PlayerZones = new Dictionary<string, PlayerZones>(s.PlayerZones) { ["P1"] = s.PlayerZones["P1"] with { Base = ["MALZ"], Battlefields = ["BF", "V"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(s.ObjectLocations) { ["V"] = new("P1", "BATTLEFIELD", "BF") } };
        var card = method == "destroy" ? "OGN·229/298" : "OGS·003/024";
        s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) { ["C"] = s.CardObjects["C"] with { CardNo = card } } };
        var cast = await Act(s, "P1", new PlayCardCommand("C", card, ["V"])); return await Top(cast.State);
    }
    internal static Task<ResolutionResult> Acknowledge(MatchState s) => Act(s, "P1", new ChooseCardsCommand(s.PendingCardChoice!.ChoiceId, s.PendingCardChoice.ChoiceWindow, []));
    internal static async Task<ResolutionResult> Complete(MatchState? state = null)
    { var died = await Destroy(state ?? Position()); var opened = await Top(died.State); return await Acknowledge(opened.State); }
    internal static string View(MatchState s, string viewer) => JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(s)[viewer]);
    internal static void ValidateViews(MatchState state)
    {
        var snapshots = ResolutionResult.BuildSnapshots(state); var prompts = ResolutionResult.BuildPrompts(state);
        var views = state.Seats.Keys.ToDictionary(id => id, id => new RecoveredPlayerView(id, state.Tick, 0, snapshots[id], state.Tick, 0, prompts[id]));
        var errors = MatchRecoveryValidator.Validate(state.RoomId, 0, [], [], views, state, state.Tick, MatchReplayRedactor.BuildSpectatorFrame(state.RoomId, state.Tick, 0, [], state));
        Assert.True(errors.Count == 0, string.Join("\n", errors));
    }
}
