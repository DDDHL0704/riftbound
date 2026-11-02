using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class RadSpinningAxeStrikeTests
{
    private const string Card = "RAD·148/167";
    private static PlayCardCommand Play() => new("UNIT", Card, ["ALLY", "ENEMY"]);
    private static MatchState Board(string ally = "OGN·219/298")
    {
        var s = VenUnitEntryBasicTests.Position(Card);
        var unit = OfficialGarenConquestBattlefieldCountTests.Printed("ALLY", ally, "P1");
        if (CardBehaviorRegistry.TryGetByCardNo(ally, out var behavior))
            unit = unit with { Tags = unit.Tags.Concat(behavior.SourceUnitTags.Split('|', StringSplitOptions.RemoveEmptyEntries))
                .Distinct(StringComparer.Ordinal).ToArray() };
        return s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
            ["ALLY"] = unit,
            ["ENEMY"] = OfficialGarenConquestBattlefieldCountTests.Printed("ENEMY", "OGN·082/298", "P2") } };
    }

    [Fact]
    public async Task OfficialSignatureSpellKeepsItsIdentityCostAndQuickTiming()
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, c => c.CardNo == Card);
        Assert.Equal(39656L, card.Id); Assert.Equal("陀斧打击", card.CardName);
        Assert.Equal(2, card.Energy); Assert.Equal(1, card.ReturnEnergy);
        Assert.Equal(new[] { "green", "orange" }, card.CardColorList);
        Assert.StartsWith("{{迅捷}}", card.CardEffect);
        Assert.True(CardBehaviorRegistry.TryGetByCardNo(Card, out var b));
        Assert.True(b.CanPlayDuringSpellDuel); Assert.False(b.CanPlayDuringPriority);
    }

    [Theory]
    [InlineData("OGN·219/298", 2)]
    [InlineData("RAD·031/167", 5)]
    public async Task GrantsTwoSteadfastBeforeDealingSpellDamageUsingTheCurrentTotal(string ally, int damage)
    {
        var game = new Driver(Board(ally)); await game.Act("P1", Play());
        Assert.Empty(game.State.CardObjects["ALLY"].UntilEndOfTurnEffects);
        await game.Top(); Assert.Equal(damage, game.State.CardObjects["ENEMY"].Damage);
        Assert.Contains("COMBAT_KEYWORD:坚守2:1", game.State.CardObjects["ALLY"].UntilEndOfTurnEffects);
        var events = game.Events().Select(e => e.Event).Where(e => e.Kind is "STATUS_EFFECT_APPLIED" or "DAMAGE_APPLIED").ToArray();
        Assert.Equal(new[] { "STATUS_EFFECT_APPLIED", "DAMAGE_APPLIED" }, events.Select(e => e.Kind));
        Assert.Equal("UNIT", events[1].Payload["sourceObjectId"]);
        Assert.False(events[1].Payload.ContainsKey("damageSourceObjectId"));
        await game.Act("P1", new EndTurnCommand());
        Assert.DoesNotContain(game.State.CardObjects["ALLY"].UntilEndOfTurnEffects, e => e.StartsWith("COMBAT_KEYWORD:"));
        await game.Replay();
    }

    [Fact]
    public async Task DamagePreventionKeepsTheKeywordGrant()
    {
        var game = new Driver(Board() with { UntilEndOfTurnEffects = ["PREVENT_SPELL_AND_SKILL_DAMAGE_THIS_TURN"] });
        await game.Act("P1", Play()); await game.Top();
        Assert.Equal(0, game.State.CardObjects["ENEMY"].Damage);
        Assert.Contains("COMBAT_KEYWORD:坚守2:1", game.State.CardObjects["ALLY"].UntilEndOfTurnEffects);
        await game.Replay();
    }

    [Theory]
    [InlineData("ALLY")] [InlineData("ENEMY")]
    public async Task AReturnedTargetDoesNotProvideInformationButTheRemainingInstructionStillResolves(string returned)
    {
        var player = returned == "ALLY" ? "P1" : "P2";
        var s = AttackDamageDistributionTests.Spell(Board("RAD·031/167"), "RETURN", "OGN·104/298", player);
        var game = new Driver(s); await game.Act("P1", Play());
        if (player == "P2") await game.Act("P1", new PassPriorityCommand());
        await game.Act(player, new PlayCardCommand("RETURN", "OGN·104/298", [returned])); await game.Top(); await game.Top();
        Assert.Equal(0, game.State.CardObjects["ENEMY"].Damage);
        Assert.Equal(returned == "ENEMY", game.State.CardObjects["ALLY"].UntilEndOfTurnEffects.Contains("COMBAT_KEYWORD:坚守2:1"));
        await game.Replay();
    }

    [Theory]
    [InlineData("order")] [InlineData("equipment")] [InlineData("mana")] [InlineData("power")]
    public async Task InvalidTargetsAndPaymentPreserveStateAndViews(string branch)
    {
        var s = Board(); var play = Play();
        if (branch == "order") play = play with { TargetObjectIds = ["ENEMY", "ALLY"] };
        if (branch == "equipment") play = play with { TargetObjectIds = ["GEAR", "ENEMY"] };
        if (branch is "mana" or "power") s = s with { RunePools = new Dictionary<string, RunePool>(s.RunePools) {
            ["P1"] = branch == "mana" ? new(1, 20) : new(20, 0) } };
        var hash = MatchStateHasher.Hash(s); var views = JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(s));
        Assert.False(new CoreRuleEngine().PreviewPlayCard(s, "P1", PlayCostPreviewTests.Request(s, play)).CanPay);
        var rejected = await new CoreRuleEngine().ResolveAsync(s, new("invalid-axe", "P1", play.CmdType), play, default);
        Assert.False(rejected.Accepted); Assert.Empty(rejected.Events); Assert.Equal(hash, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(views, JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(rejected.State)));
    }
}
