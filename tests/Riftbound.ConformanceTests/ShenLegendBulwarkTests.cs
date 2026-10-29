using System.Reflection;
using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.ArmamentMightTests;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;

namespace Riftbound.ConformanceTests;

public sealed class ShenLegendBulwarkTests
{
    private const string Ability = LegendActionAbilityCatalog.ShenLegendAbilityId;
    private static LegendActCommand Grant(params string[] targets) => new("LEGEND", Ability, targets, []);
    private static MatchState Position(string cardNo = "VEN·147") => LegendAttachmentTests.PositionFor(cardNo);

    [Theory]
    [InlineData("VEN·147", 37850)]
    [InlineData("VEN·193", 37900)]
    [InlineData("VEN·193*", 37901)]
    public async Task AllPhysicalPrintsExhaustAndQueueTheFriendlyKeywordUntilBothPlayersPass(string cardNo, long id)
    {
        var catalog = await OfficialCardCatalog.LoadRuntimeAsync();
        var card = Assert.Single(catalog.Cards, c => c.CardNo == cardNo);
        Assert.Equal(id, card.Id); Assert.Equal("暮光之眼", card.CardName); Assert.Equal("慎", card.Hero);
        Assert.StartsWith("{{迅捷>}}{{横置}}：给予一名友方单位在本回合内{{壁垒}}。", card.CardEffect);
        var activated = await Act(Position(cardNo), "P1", Grant("HOST"));
        Assert.True(activated.State.CardObjects["LEGEND"].IsExhausted);
        Assert.Single(activated.State.StackItems, item => item.LegendUnitKeyword is not null);
        Assert.DoesNotContain(activated.State.CardObjects["HOST"].UntilEndOfTurnEffects, effect => effect.StartsWith("COMBAT_KEYWORD:壁垒", StringComparison.Ordinal));
        Restore(activated.State); PublicRecovery(activated.State);
        var done = await Top(activated.State);
        Assert.Contains("COMBAT_KEYWORD:壁垒:1", done.State.CardObjects["HOST"].UntilEndOfTurnEffects);
        Assert.Single(done.Events, e => e.Kind == "STATUS_EFFECT_APPLIED");
        Restore(done.State); PublicRecovery(done.State);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("duplicate")]
    [InlineData("equipment")]
    [InlineData("enemy")]
    [InlineData("exhausted")]
    [InlineData("wrong-player")]
    public async Task IllegalActivationNeverExhaustsTheLegendOrChangesTheState(string scenario)
    {
        var state = Position();
        if (scenario == "enemy") state = LocalDestructionRecallTests.At(state, "HOST", "P2", "BASE");
        if (scenario == "exhausted") state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["LEGEND"] = state.CardObjects["LEGEND"] with { IsExhausted = true } } };
        var targets = scenario switch { "missing" => Array.Empty<string>(), "duplicate" => new[] { "HOST", "HOST" },
            "equipment" => new[] { "GEAR" }, _ => new[] { "HOST" } };
        var command = Grant(targets); var actor = scenario == "wrong-player" ? "P2" : "P1";
        var result = await new CoreRuleEngine().ResolveAsync(state, new("invalid", actor, command.CmdType), command, default);
        Assert.False(result.Accepted); Assert.Empty(result.Events);
        Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(result.State));
    }

    [Fact]
    public async Task ActualResponseReturnsTheTargetWithoutGrantingTheNewHandObjectOrRefundingTheLegend()
    {
        var initial = EquipChainTests.Response(Position(), "OGN·169/298", "P2");
        var activated = await Act(initial, "P1", Grant("HOST"));
        var passed = await Act(activated.State, "P1", new PassPriorityCommand());
        var response = await Act(passed.State, "P2", new PlayCardCommand("RESPONSE", "OGN·169/298", ["HOST"]));
        var returned = await Top(response.State);
        Assert.Contains("HOST", returned.State.PlayerZones["P1"].Hand);
        Assert.DoesNotContain("\"HOST\"", JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(returned.State)["P2"].Stack));
        PublicRecovery(returned.State);
        var done = await Top(returned.State);
        Assert.True(done.State.CardObjects["LEGEND"].IsExhausted);
        Assert.DoesNotContain(done.State.CardObjects["HOST"].UntilEndOfTurnEffects, effect => effect.StartsWith("COMBAT_KEYWORD:壁垒", StringComparison.Ordinal));
        Assert.Contains(done.Events, e => e.Kind == "ABILITY_NO_EFFECT");
        Assert.DoesNotContain("\"HOST\"", JsonSerializer.Serialize(done.Events));
        PublicRecovery(done.State);
    }

    [Fact]
    public async Task QuickAbilityCanBeActivatedDuringTheOpponentsSpellDuelButNotTheClosedResponseWindow()
    {
        var initial = AttackDamageDistributionTests.Position("OGN·001/298");
        initial = initial with {
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
                ["P2"] = initial.PlayerZones["P2"] with { LegendZone = ["LEGEND"] } },
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["LEGEND"] = new("LEGEND", cardNo: "VEN·147", ownerId: "P2", controllerId: "P2", tags: ["传奇"]) },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) { ["LEGEND"] = new("P2", "LEGEND") } };
        var moved = await Act(initial, "P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F", []));
        var state = moved.State;
        if (state.FocusPlayerId == "P1") state = (await Act(state, "P1", new PassFocusCommand())).State;
        Assert.Equal("P1", state.TurnPlayerId);
        Assert.Equal("P2", state.FocusPlayerId);
        Assert.Equal(TimingStates.SpellDuelOpen, state.TimingState);
        Assert.Contains(ResolutionResult.BuildPrompts(state)["P2"].Candidates ?? [],
            candidate => candidate.Action == CommandTypes.LegendAct && candidate.Enabled);
        var activated = await Act(state, "P2", Grant("ENEMY1"));
        Assert.Equal(TimingStates.SpellDuelClosed, activated.State.TimingState);
        var repeated = await new CoreRuleEngine().ResolveAsync(activated.State,
            new("closed-quick", "P2", CommandTypes.LegendAct), Grant("ENEMY1"), default);
        Assert.False(repeated.Accepted);
        Assert.Equal(MatchStateHasher.Hash(activated.State), MatchStateHasher.Hash(repeated.State));
        var done = await Top(activated.State);
        Assert.Contains("COMBAT_KEYWORD:壁垒:1", done.State.CardObjects["ENEMY1"].UntilEndOfTurnEffects);
        Assert.Equal(TimingStates.SpellDuelOpen, done.State.TimingState);
        Assert.Equal("P1", done.State.FocusPlayerId);
        PublicRecovery(done.State);
    }

    [Theory]
    [InlineData("context")]
    [InlineData("ability")]
    [InlineData("source-generation")]
    [InlineData("target-generation")]
    public async Task RecoveryRejectsForgedLegendKeywordProvenance(string change)
    {
        var state = (await Act(Position(), "P1", Grant("HOST"))).State;
        var item = Assert.Single(state.StackItems);
        var context = item.LegendUnitKeyword!;
        var forged = item with {
            LegendUnitKeyword = change switch { "context" => null, "ability" => context with { AbilityId = "FORGED" },
                "source-generation" => context with { SourceGeneration = context.SourceGeneration + 1 }, _ => context },
            TargetGenerations = change == "target-generation" ? new Dictionary<string, long> { ["HOST"] = 999 } : item.TargetGenerations };
        Assert.Contains("invalid captured legend unit keyword", OfficialInsightAndSpellLockTests.Errors(state with { StackItems = [forged] }));
    }

    [Fact]
    public async Task ExistingBattlefieldGrantedLegendAbilityRecognizesTheNewLegend()
    {
        var state = LegendAttachmentTests.ForgePosition();
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["LEGEND"] = state.CardObjects["LEGEND"] with { CardNo = "VEN·147" } } };
        var activated = await Act(state, "P1", LegendAttachmentTests.Command(LegendAttachmentTests.Forge));
        Assert.Single(activated.State.StackItems, item => item.LegendAttachment is not null);
        PublicRecovery(activated.State);
        var done = await Top(activated.State);
        Assert.Equal("HOST", done.State.CardObjects["GEAR"].AttachedToObjectId);
        PublicRecovery(done.State);
    }

    [Fact]
    public async Task GrantedBulwarkIsSeenByTheExistingBattlefieldAura()
    {
        var state = Position();
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["F"] = state.CardObjects["F"] with { CardNo = "VEN·159" } } };
        Assert.Equal(3, Might(state));
        var activated = await Act(state, "P1", Grant("HOST"));
        Assert.Equal(3, Might(activated.State));
        var done = await Top(activated.State);
        Assert.Equal(4, Might(done.State));
        PublicRecovery(done.State);
    }

    [Fact]
    public async Task GrantedBulwarkExpiresAtTheEndOfTheTurn()
    {
        var activated = await Act(Position(), "P1", Grant("HOST"));
        var resolved = await Top(activated.State);
        var ended = await Act(resolved.State, "P1", new EndTurnCommand());
        Assert.DoesNotContain(ended.State.CardObjects["HOST"].UntilEndOfTurnEffects,
            effect => effect.StartsWith("COMBAT_KEYWORD:壁垒", StringComparison.Ordinal));
        PublicRecovery(ended.State);
    }

    [Theory]
    [InlineData("VEN·147")]
    [InlineData("VEN·193")]
    [InlineData("VEN·193*")]
    public async Task EachLegendPrintingCanOpenWithAnImplementedShenChampion(string legendNo)
    {
        const OfficialDeckFormat format = OfficialDeckFormat.ChinaStandard20260724;
        var catalog = await OfficialCardCatalog.LoadRuntimeAsync();
        var allowed = catalog with { Cards = catalog.Cards.Where(card => ChinaStandardBanList.IsAllowed(card, format)).ToArray() };
        string Rune(string color) => allowed.Cards.Where(card => card.CardCategory == "rune" && card.CardColorList.Contains(color))
            .OrderBy(card => card.CardNo, StringComparer.Ordinal).First().CardNo;
        var builder = typeof(PreconstructedDeckCatalog).GetMethod("BuildLowCurveDeck", BindingFlags.NonPublic | BindingFlags.Static)!;
        var deck = (OfficialDecklist)builder.Invoke(null, [allowed, allowed.Cards.ToDictionary(card => card.CardNo, StringComparer.Ordinal),
            legendNo, "VEN·042", Array.Empty<string>(), new[] { "OGN·277/298", "OGN·278/298", "OGN·294/298" },
            Enumerable.Repeat(Rune("green"), 6).Concat(Enumerable.Repeat(Rune("yellow"), 6)).ToArray()])!;
        var validation = OfficialDeckValidator.Validate(deck, catalog, format);
        Assert.True(validation.IsValid, string.Join("; ", validation.Errors));
        var initial = MatchReplayInitialStateBuilder.FromSeats("shen-legend-" + legendNo,
            new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" });
        var session = new MatchSession(initial, new CoreRuleEngine(), NoopMatchJournal.Instance,
            NoopMatchPlayerStore.Instance, new MatchSessionOptions(false, format));
        var submit = new SubmitDeckCommand(deck.LegendCardNo, deck.ChampionCardNo, deck.MainDeck, deck.RuneDeck, deck.Battlefields);
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        foreach (var player in new[] { "P1", "P2" })
        {
            var submitted = await session.SubmitDeckAsync(player, "deck-" + player, submit, JsonSerializer.SerializeToElement(submit, json), default);
            Assert.True(submitted.Accepted, submitted.ErrorMessage);
        }
        var ready = JsonSerializer.SerializeToElement(new ReadyCommand(), json);
        Assert.True((await session.ReadyAsync("P1", "ready-P1", ready, default)).Accepted);
        var opened = await session.ReadyAsync("P2", "ready-P2", ready, default);
        Assert.True(opened.Accepted, opened.ErrorMessage);
        foreach (var player in new[] { "P1", "P2" })
        {
            Assert.Contains(opened.State.PlayerZones[player].LegendZone, id => opened.State.CardObjects[id].CardNo == legendNo);
            Assert.Contains(opened.State.PlayerZones[player].ChampionZone, id => opened.State.CardObjects[id].CardNo == "VEN·042");
        }
    }

    [Fact]
    public async Task ProductionSessionRestoresAndReplaysTheQueuedLegendAbility()
    {
        var game = new VenBasicFlowSpellTests.Driver(Position());
        await game.Act("P1", Grant("HOST"));
        await game.Top();
        Assert.Contains("COMBAT_KEYWORD:壁垒:1", game.State.CardObjects["HOST"].UntilEndOfTurnEffects);
        await game.Replay();
    }
}
