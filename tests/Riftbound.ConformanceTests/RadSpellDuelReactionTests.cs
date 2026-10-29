using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class RadSpellDuelReactionTests
{
    private const string Kaisa = "RAD·063/167";
    private const string Draw = "VEN·049";
    public static IEnumerable<object[]> Prints() => new[] { Kaisa }.Select(no => new object[] { no });

    [Theory]
    [InlineData(Kaisa, 39560, 5)]
    public async Task EachOfficialPrintingRetainsItsOwnMightAndPaysBluePowerFromTheChampionZone(string no, long id, int might)
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, c => c.CardNo == no);
        Assert.Equal(id, card.Id); Assert.Equal(5, card.Energy); Assert.Equal(1, card.ReturnEnergy); Assert.Equal(might, card.Power);
        Assert.Equal("逆天改命", card.SubTitle); Assert.Contains("{{缴械}}", card.CardEffect);
        Assert.Contains(RuleTextParser.Parse(card).Keywords, keyword => keyword.Keyword == "缴械");
        Assert.Single(StaticAbilityParser.Parse(card.CardEffect, []), a => a.Kind == StaticAbilityKinds.UnitInSpellDuelFriendlySpellsReaction);
        Assert.True(CardBehaviorRegistry.TryGetByCardNo(no, out var behavior));
        Assert.False(CardPermissionKeywordRules.BuildProfile(behavior).HasReaction);
        var s = VenUnitEntryBasicTests.Position(no);
        s = s with {
            PlayerZones = new Dictionary<string, PlayerZones>(s.PlayerZones) { ["P1"] = s.PlayerZones["P1"] with { Hand = [], ChampionZone = ["UNIT"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(s.ObjectLocations) { ["UNIT"] = new("P1", "CHAMPION") },
            RunePools = new Dictionary<string, RunePool>(s.RunePools) { ["P1"] = new(5, 0, new Dictionary<string, int> { [RuneTrait.Blue] = 1 }) } };
        var game = new Driver(s);
        await game.Act("P1", new PlayCardCommand("UNIT", no, [], Destination: "BASE"));
        Assert.Equal(might, game.State.CardObjects["UNIT"].Power); Assert.True(game.State.CardObjects["UNIT"].IsExhausted);
        Assert.Contains("缴械", game.State.CardObjects["UNIT"].Tags);
        Assert.Equal(0, game.State.RunePools["P1"].Mana); Assert.Equal(0, game.State.RunePools["P1"].TotalPower);
        Assert.Empty(game.State.PlayerZones["P1"].ChampionZone); await game.Replay();
    }

    private static MatchState Position(string no = Kaisa, bool copied = false)
    {
        var s = ArmamentMightTests.Position(gear: copied ? "SFD·059/221" : "SFD·022/221", unit: no, power: no == Kaisa ? 5 : 4, attached: copied);
        s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) { ["F"] = s.CardObjects["F"] with { ControllerId = "P2" } },
            RunePools = new Dictionary<string, RunePool>(s.RunePools) { ["P2"] = new(20, 20) } };
        s = AttackDamageDistributionTests.Spell(s, "SPELL", Draw, "P1");
        s = AttackDamageDistributionTests.Spell(s, "FOLLOW", Draw, "P1");
        s = AttackDamageDistributionTests.Spell(s, "OPEN", "OGN·048/298", "P1");
        return s;
    }

    private static async Task OpenDuel(Driver game)
    {
        await game.Act("P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F", []));
        Assert.True(game.State.SpellDuelState.IsActive); Assert.Equal("F", game.State.SpellDuelState.BattlefieldObjectId);
        Assert.False(game.State.BattleState.IsActive);
    }

    private static bool Offered(MatchState state, string player, string source)
        => ResolutionResult.BuildPrompts(state)[player].Candidates?.Any(c => c.Action == CommandTypes.PlayCard
            && c.Enabled && c.Sources?.Any(s => s.Id == source) == true) == true;

    public static IEnumerable<object[]> Plays() => Prints().SelectMany(row => new[] { false, true }.Select(closed => new object[] { row[0], closed }));
    [Theory, MemberData(nameof(Plays))]
    public async Task AnOrdinarySpellIsOfferedQuotedPaidAndResolvedInBothDuelWindows(string no, bool closed)
    {
        var game = new Driver(Position(no)); await OpenDuel(game);
        if (closed) await game.Act("P1", new PlayCardCommand("OPEN", "OGN·048/298", []));
        Assert.True(Offered(game.State, "P1", "SPELL"));
        var command = new PlayCardCommand("SPELL", Draw, []);
        var quote = new CoreRuleEngine().PreviewPlayCard(game.State, "P1", PlayCostPreviewTests.Request(game.State, command));
        Assert.True(quote.CanPay, quote.Message); Assert.Equal(2, quote.Cost!.Mana);
        var mana = game.State.RunePools["P1"].Mana;
        await game.Act("P1", command); Assert.Equal(mana - 2, game.State.RunePools["P1"].Mana);
        Assert.Equal("SPELL", game.State.StackItems[^1].SourceObjectId);
        await game.Top(); Assert.Contains("SPELL", game.State.PlayerZones["P1"].Graveyard);
        Assert.Contains("D0", game.State.PlayerZones["P1"].Hand);
        if (closed) await game.Top();
        await game.Replay();
    }

    [Fact]
    public async Task APrintedSwiftSpellCanAlsoRespondWithoutChangingItsPrintedPermission()
    {
        const string swift = "OGN·024/298";
        var game = new Driver(AttackDamageDistributionTests.Spell(Position(), "SWIFT", swift, "P1"));
        await OpenDuel(game); await game.Act("P1", new PlayCardCommand("OPEN", "OGN·048/298", []));
        Assert.True(CardBehaviorRegistry.TryGetByCardNo(swift, out var printed)); Assert.False(printed.CanPlayDuringPriority);
        await game.Act("P1", new PlayCardCommand("SWIFT", swift, ["HOST"]));
        await game.Top(); Assert.Equal(4, game.State.CardObjects["HOST"].Damage);
        Assert.Contains("SWIFT", game.State.PlayerZones["P1"].Graveyard); await game.Top(); await game.Replay();
    }

    [Theory, MemberData(nameof(Prints))]
    public async Task MissingBluePowerCannotPlayTheHero(string no)
    {
        var s = VenUnitEntryBasicTests.Position(no) with { RunePools = new Dictionary<string, RunePool> {
            ["P1"] = new(5, 0, new Dictionary<string, int> { [RuneTrait.Red] = 1 }), ["P2"] = new(20, 20) } };
        await Reject(s, "P1", new PlayCardCommand("UNIT", no, [], Destination: "BASE"));
    }

    [Theory]
    [InlineData("base")] [InlineData("elsewhere")] [InlineData("enemy-controller")]
    [InlineData("face-down")] [InlineData("not-unit")] [InlineData("no-duel")]
    [InlineData("no-mana")] [InlineData("spell-prohibited")] [InlineData("wrong-player")]
    public async Task CurrentSourceLocationControllerVisibilityTimingAndCostsRemainAuthoritative(string branch)
    {
        var game = new Driver(Position()); await OpenDuel(game);
        var s = game.State;
        if (branch == "base") s = LocalDestructionRecallTests.At(s, "HOST", "P1", "BASE");
        if (branch == "elsewhere") s = LocalDestructionRecallTests.At(s, "HOST", "P1", "BATTLEFIELD", "OTHER");
        if (branch == "enemy-controller") s = LocalDestructionRecallTests.At(s, "HOST", "P2", "BATTLEFIELD", "F");
        if (branch is "face-down" or "not-unit") s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
            ["HOST"] = s.CardObjects["HOST"] with { IsFaceDown = branch == "face-down", Tags = branch == "not-unit" ? [CardObjectTags.EquipmentCard] : s.CardObjects["HOST"].Tags } } };
        if (branch == "no-duel") {
            s = (await OfficialGraveyardRecastTests.Act(Position(), "P1", new PlayCardCommand("OPEN", "OGN·048/298", []))).State;
            Assert.False(s.SpellDuelState.IsActive);
        }
        if (branch == "no-mana") s = s with { RunePools = new Dictionary<string, RunePool>(s.RunePools) { ["P1"] = RunePool.Empty } };
        if (branch == "spell-prohibited") s = s with { UntilEndOfTurnEffects = [..s.UntilEndOfTurnEffects, CardPermissionKeywordRules.SpellPlayProhibitionPrefix + "P1"] };
        if (branch == "wrong-player") s = s with { FocusPlayerId = "P2", PriorityPlayerId = null };
        Assert.False(Offered(s, "P1", "SPELL"));
        await Reject(s, "P1", new PlayCardCommand("SPELL", Draw, []));
    }

    [Theory]
    [InlineData("unit")] [InlineData("equipment")]
    public async Task TheGrantNeverMakesUnitsOrEquipmentIntoReactionCards(string type)
    {
        var no = type == "unit" ? "OGN·001/298" : "SFD·022/221";
        var game = new Driver(HandDiscardObserverTests.Put(Position(), "PERMANENT", no, "P1", "HAND")); await OpenDuel(game);
        Assert.False(Offered(game.State, "P1", "PERMANENT"));
        await Reject(game.State, "P1", new PlayCardCommand("PERMANENT", no, [], Destination: "BASE"));
    }

    [Fact]
    public async Task RecallingTheSourceRevokesNewPlaysButDoesNotInvalidateAnAlreadyPlayedSpell()
    {
        var s = EquipChainTests.Response(Position(), "OGN·169/298", "P2");
        s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) { ["HOST"] = s.CardObjects["HOST"] with { Power = 3 } } };
        var game = new Driver(s); await OpenDuel(game);
        await game.Act("P1", new PlayCardCommand("SPELL", Draw, [])); await game.Act("P1", new PassPriorityCommand());
        await game.Act("P2", new PlayCardCommand("RESPONSE", "OGN·169/298", ["HOST"])); await game.Top();
        Assert.Contains("HOST", game.State.PlayerZones["P1"].Hand);
        if (game.State.PriorityPlayerId != "P1") await game.Act(game.State.PriorityPlayerId!, new PassPriorityCommand());
        Assert.False(Offered(game.State, "P1", "FOLLOW"));
        await Reject(game.State, "P1", new PlayCardCommand("FOLLOW", Draw, []));
        await game.Top(); Assert.Contains("SPELL", game.State.PlayerZones["P1"].Graveyard); await game.Replay();
    }

    [Fact]
    public async Task PrintedAndCopiedTextGrantOnePermissionAndNeverDuplicateTheSpellOrItsCosts()
    {
        var game = new Driver(Position(copied: true)); await OpenDuel(game);
        await game.Act("P1", new PlayCardCommand("SPELL", Draw, []));
        Assert.Single(game.State.StackItems); Assert.Equal(18, game.State.RunePools["P1"].Mana);
        await game.Top(); Assert.Single(game.Events(), e => e.Event.Kind == "CARD_DRAWN"); await game.Replay();
    }

    private static async Task Reject(MatchState state, string player, PlayCardCommand command)
    {
        var before = MatchStateHasher.Hash(state); var snapshots = JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(state));
        var prompts = JsonSerializer.Serialize(ResolutionResult.BuildPrompts(state));
        Assert.False(new CoreRuleEngine().PreviewPlayCard(state, player, PlayCostPreviewTests.Request(state, command)).CanPay);
        var result = await new CoreRuleEngine().ResolveAsync(state, new("rad-reaction-invalid", player, command.CmdType), command, default);
        Assert.False(result.Accepted); Assert.Empty(result.Events); Assert.Equal(before, MatchStateHasher.Hash(result.State));
        Assert.Equal(snapshots, JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(result.State)));
        Assert.Equal(prompts, JsonSerializer.Serialize(ResolutionResult.BuildPrompts(result.State)));
    }
}
