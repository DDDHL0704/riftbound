using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed partial class VenBorrowedDependentAbilityFaqTests
{
    private const string PaidRecallAbility = "SMUGGLED_WEAPON_DISCARD_GRANT_PAID_RECALL";
    private const string PaidRecallPrefix = "ACTIVATED_PAID_RECALL:";

    private static MatchState SmuggledWeaponPosition(bool borrowed, string mode = "pay")
    {
        var state = RadUnitEffectTestFixture.Place(BorrowedBombPosition("OGN·023/298"), "ALLY", "P1", "BATTLEFIELD", "BF");
        state = HandDiscardObserverTests.Put(state, "RECALL-COST", "OGN·048/298", "P1", "HAND");
        foreach (var id in new[] { "KILL-ONE", "KILL-TWO" })
            state = AttackDamageDistributionTests.Spell(state, id, "OGN·229/298", "P1");
        var traits = new Dictionary<string, int> { [RuneTrait.Yellow] = 4, [RuneTrait.Blue] = 2 };
        if (mode is not ("no-red" or "rune")) traits[RuneTrait.Red] = 2;
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["UNIT"] = state.CardObjects["UNIT"] with { IsExhausted = borrowed },
                ["ALLY"] = state.CardObjects["ALLY"] with { Damage = 1, DamageMarkedByPlayerIds = ["P2"] } },
            RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(20, 0, traits) } };
        if (mode == "rune")
        {
            state = HandDiscardObserverTests.Put(state, "RECALL-RUNE", "OGN·007/298", "P1", "BASE");
            state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["RECALL-RUNE"] = state.CardObjects["RECALL-RUNE"] with { IsExhausted = true } } };
        }
        return state;
    }

    private static ActivateAbilityCommand SmuggledWeapon(MatchState state, bool borrowed)
        => new(borrowed ? "HOST" : "UNIT", borrowed ? TemplateId(state, "下次被摧毁") : PaidRecallAbility,
            ["ALLY"], ["DISCARD_HAND_CARD:RECALL-COST"]);

    private static string[] PaidRecallGrants(MatchState state)
        => state.UntilEndOfTurnEffects.Where(m => m.StartsWith(PaidRecallPrefix, StringComparison.Ordinal)).ToArray();

    private static async Task PaySmuggledRecall(Driver game)
    {
        var request = Assert.IsType<RuleChoiceContinuation>(game.State.PendingRuleChoice).Request;
        var choice = Assert.Single(request.Options, o => o.Id.StartsWith("PAID_RECALL:ALLY:", StringComparison.Ordinal));
        await RadGearTestPosition.Reject(game.State, "P2", new PayCostCommand(request.Id, "RULE_REPLACEMENT", [choice.Id]));
        await game.Act("P1", new PayCostCommand(request.Id, "RULE_REPLACEMENT", [choice.Id]));
    }

    // CN OGN·023 (31229), 392/393 and 460.1: this costs red power at
    // destruction, is optional, and never removes the target's damage.
    [Theory]
    [InlineData(false, "pay")] [InlineData(true, "pay")]
    [InlineData(false, "decline")] [InlineData(true, "decline")]
    [InlineData(false, "no-red")] [InlineData(true, "no-red")]
    [InlineData(true, "rune")]
    public async Task SmuggledWeaponPaysAnIndependentOptionalRedCostAndPreservesDamage(bool borrowed, string mode)
    {
        var initial = SmuggledWeaponPosition(borrowed, mode);
        var command = SmuggledWeapon(initial, borrowed);
        await RadGearTestPosition.Reject(initial, "P1", command with { TargetObjectIds = ["ENEMY"] });
        await RadGearTestPosition.Reject(initial, "P1", command with { OptionalCosts = [] });
        await RadGearTestPosition.Reject(initial, "P2", command);
        var game = new Driver(initial);
        await game.Act("P1", command);
        Assert.Contains("RECALL-COST", game.State.PlayerZones["P1"].Graveyard);
        Assert.Empty(PaidRecallGrants(game.State));
        Assert.Equal(MatchStateHasher.HashValue(initial.RunePools["P1"]), MatchStateHasher.HashValue(game.State.RunePools["P1"]));
        await game.Top();
        var marker = Assert.Single(PaidRecallGrants(game.State));
        foreach (var viewer in new[] { "P1", "P2", ProjectionViewers.Spectator })
            Assert.DoesNotContain(marker, JsonSerializer.Serialize(viewer == ProjectionViewers.Spectator
                ? ResolutionResult.BuildSpectatorSnapshot(game.State) : ResolutionResult.BuildSnapshots(game.State)[viewer]));
        var grant = JsonSerializer.Deserialize<ActivatedPlayGrant>(Convert.FromBase64String(marker[PaidRecallPrefix.Length..]))!;
        foreach (var forged in new[] { grant with { Target = new("ALLY", grant.Target!.Generation + 1) },
            grant with { TurnNumber = grant.TurnNumber + 1 }, grant with { CardNo = "FORGED" },
            grant with { Borrowed = null, Printed = null } })
        {
            var bad = PaidRecallPrefix + Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(forged));
            Assert.Contains("invalid activated next play grant", OfficialInsightAndSpellLockTests.Errors(game.State with {
                UntilEndOfTurnEffects = game.State.UntilEndOfTurnEffects.Select(m => m == marker ? bad : m).ToArray() }));
        }
        await game.Act("P1", new PlayCardCommand("KILL-ONE", "OGN·229/298", ["ALLY"]));
        await game.Top();
        if (mode is "pay" or "rune")
        {
            await PaySmuggledRecall(game);
            if (mode == "rune") Assert.Contains("RECALL-RUNE", game.State.PlayerZones["P1"].RuneDeck);
            Assert.Contains("ALLY", game.State.PlayerZones["P1"].Base);
            Assert.True(game.State.CardObjects["ALLY"].IsExhausted);
            Assert.Equal(1, game.State.CardObjects["ALLY"].Damage);
            Assert.Equal(["P2"], game.State.CardObjects["ALLY"].DamageMarkedByPlayerIds);
            Assert.Equal(initial.CardObjects["ALLY"].ObjectGeneration, game.State.CardObjects["ALLY"].ObjectGeneration);
            Assert.Equal(mode == "rune" ? 0 : 1, game.State.RunePools["P1"].PowerByTrait.GetValueOrDefault(RuneTrait.Red));
            Assert.Equal(2, game.State.RunePools["P1"].PowerByTrait[RuneTrait.Blue]);
            Assert.DoesNotContain(game.Events(), e => e.Event.Kind is "UNIT_MOVED" or "UNIT_DESTROYED"
                && Equals(e.Event.Payload.GetValueOrDefault("targetObjectId"), "ALLY"));
            Assert.Empty(PaidRecallGrants(game.State));
            await game.Act("P1", new PlayCardCommand("KILL-TWO", "OGN·229/298", ["ALLY"]));
            await game.Top();
        }
        else if (game.State.PendingRuleChoice is { } request)
            await game.Act("P1", new PayCostCommand(request.Request.Id, "RULE_REPLACEMENT", ["DECLINE"]));
        Assert.Contains("ALLY", game.State.PlayerZones["P1"].Graveyard);
        Assert.Empty(PaidRecallGrants(game.State));
        await game.Replay();
    }

    [Theory]
    [InlineData(false, false)] [InlineData(false, true)]
    [InlineData(true, false)] [InlineData(true, true)]
    public async Task SmuggledWeaponGrantKeepsBorrowedSourceReceiptsBeforeAndAfterTokenDeparture(bool token, bool beforeResolution)
    {
        var initial = SmuggledWeaponPosition(true);
        if (token) initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["HOST"] = initial.CardObjects["HOST"] with { TokenFactoryCardNo = P6TokenFactoryCatalog.ImageTokenCardNo,
                CopyFaceTransitions = [new(0, initial.CardObjects["HOST"].ObjectGeneration,
                    P6TokenFactoryCatalog.ImageTokenCardNo, initial.CardObjects["HOST"].CardNo!)] } } };
        var game = new Driver(initial);
        await game.Act("P1", SmuggledWeapon(initial, true));
        if (!beforeResolution) await game.Top();
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["HOST"]));
        await game.Top();
        if (beforeResolution) await game.Top();
        Assert.Single(PaidRecallGrants(game.State));
        if (token) Assert.False(game.State.CardObjects.ContainsKey("HOST"));
        else Assert.Contains("HOST", game.State.PlayerZones["P1"].Hand);
        await game.Act("P1", new PlayCardCommand("KILL-ONE", "OGN·229/298", ["ALLY"]));
        await game.Top();
        await PaySmuggledRecall(game);
        Assert.Contains("ALLY", game.State.PlayerZones["P1"].Base);
        var recall = Assert.Single(game.Events(), e => e.Event.Kind == "UNIT_RECALLED_TO_BASE");
        Assert.Equal(token ? "HOST" : "HIDDEN", recall.Event.Payload["sourceObjectId"]);
        await game.Replay();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task SmuggledWeaponDoesNotFollowATargetReturnedDuringTheResponse(bool borrowed)
    {
        var game = new Driver(SmuggledWeaponPosition(borrowed));
        await game.Act("P1", SmuggledWeapon(game.State, borrowed));
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["ALLY"]));
        await game.Top();
        await game.Top();
        Assert.Empty(PaidRecallGrants(game.State));
        Assert.Contains("RECALL-COST", game.State.PlayerZones["P1"].Graveyard);
        Assert.Contains("ALLY", game.State.PlayerZones["P1"].Hand);
        await game.Replay();
    }

    [Fact]
    public async Task SmuggledWeaponGrantExpiresAtTurnEnd()
    {
        var game = new Driver(SmuggledWeaponPosition(false));
        await game.Act("P1", SmuggledWeapon(game.State, false));
        await game.Top();
        Assert.Single(PaidRecallGrants(game.State));
        await game.Act("P1", new EndTurnCommand());
        Assert.Empty(PaidRecallGrants(game.State));
        await game.Replay();
    }

    [Fact]
    public async Task SmuggledWeaponCannotEraseLethalDamageOrReuseTheSameGrant()
    {
        var initial = AttackDamageDistributionTests.Spell(SmuggledWeaponPosition(false), "LETHAL", "OGN·133/298", "P1");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["ALLY"] = initial.CardObjects["ALLY"] with { Damage = 3 } } };
        var game = new Driver(initial);
        await game.Act("P1", SmuggledWeapon(initial, false));
        await game.Top();
        await game.Act("P1", new PlayCardCommand("LETHAL", "OGN·133/298", []));
        await game.Top();
        await PaySmuggledRecall(game);
        Assert.Contains("ALLY", game.State.PlayerZones["P1"].Graveyard);
        var recall = Assert.Single(game.Events(), e => e.Event.Kind == "UNIT_RECALLED_TO_BASE");
        Assert.Equal(4, recall.Event.Payload["damage"]);
        Assert.Single(game.Events(), e => e.Event.Kind == "COST_PAID" && Equals(e.Event.Payload.GetValueOrDefault("reason"), "PAID_RECALL"));
        Assert.Single(game.Events(), e => e.Event.Kind == "UNIT_DESTROYED" && Equals(e.Event.Payload.GetValueOrDefault("targetObjectId"), "ALLY"));
        Assert.Equal(1, game.State.RunePools["P1"].PowerByTrait[RuneTrait.Red]);
        Assert.Empty(PaidRecallGrants(game.State));
        await game.Replay();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task SmuggledWeaponRequiresTheGrantControllersConsentAfterRealControlTransfer(bool pay)
    {
        var initial = HandDiscardObserverTests.Put(SmuggledWeaponPosition(false), "DONATION", "VEN·133", "P1", "BASE");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["HOST"] = initial.CardObjects["HOST"] with { IsEmpowered = true },
            ["DONATION"] = initial.CardObjects["DONATION"] with { IsExhausted = true } } };
        var game = new Driver(initial);
        await game.Act("P1", SmuggledWeapon(initial, false) with { TargetObjectIds = ["HOST"] });
        await game.Top();
        await game.Act("P1", new ActivateAbilityCommand("HOST", TemplateId(game.State, "获得此牌控制权"), ["P2"]));
        await game.Top();
        Assert.Equal("P2", game.State.CardObjects["HOST"].ControllerId);
        Assert.Single(PaidRecallGrants(game.State));
        await game.Act("P1", new PlayCardCommand("KILL-ONE", "OGN·229/298", ["HOST"]));
        await game.Top();
        var request = Assert.IsType<RuleChoiceContinuation>(game.State.PendingRuleChoice).Request;
        Assert.Equal("P2", request.PlayerId);
        var option = Assert.Single(request.Options, o => o.Id.StartsWith("PAID_RECALL:HOST:", StringComparison.Ordinal));
        await game.Act("P2", new PayCostCommand(request.Id, "RULE_REPLACEMENT", [option.Id]));
        request = Assert.IsType<RuleChoiceContinuation>(game.State.PendingRuleChoice).Request;
        Assert.Equal("P1", request.PlayerId);
        await RadGearTestPosition.Reject(game.State, "P2", new PayCostCommand(request.Id, "RULE_REPLACEMENT", ["PAY"]));
        await game.Act("P1", new PayCostCommand(request.Id, "RULE_REPLACEMENT", [pay ? "PAY" : "DECLINE"]));
        Assert.Equal(pay ? 1 : 2, game.State.RunePools["P1"].PowerByTrait[RuneTrait.Red]);
        Assert.Equal(MatchStateHasher.HashValue(initial.RunePools["P2"]), MatchStateHasher.HashValue(game.State.RunePools["P2"]));
        Assert.Equal(pay, game.State.PlayerZones["P2"].Base.Contains("HOST"));
        Assert.Equal(!pay, game.State.PlayerZones["P1"].Graveyard.Contains("HOST"));
        await game.Replay();
    }
    // CN RAD judge FAQ DOC-17 p.1: an end-of-turn destruction replaced
    // by Smuggled Weapon does not trigger again in a later turn.
    [Fact]
    public async Task SmuggledWeaponReplacesTimeAssassinDestructionOnlyDuringTheGrantedTurn()
    {
        var initial = HandDiscardObserverTests.Put(SmuggledWeaponPosition(false), "ASSASSIN-UNIT", "SFD·143/221", "P1", "HAND");
        initial = initial with {
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["LEGEND"] = OfficialGarenConquestBattlefieldCountTests.Printed("LEGEND", "RAD·139/167", "P1") with { IsEmpowered = true } },
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
                ["P1"] = initial.PlayerZones["P1"] with { LegendZone = ["LEGEND"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) { ["LEGEND"] = new("P1", "LEGEND") } };
        var game = new Driver(initial);
        await game.Act("P1", new LegendActCommand("LEGEND", LegendActionAbilityCatalog.TimeAssassinLegendAbilityId, [], []));
        await game.Top();
        await game.Act("P1", new PlayCardCommand("ASSASSIN-UNIT", "SFD·143/221", [], Destination: "BASE"));
        Assert.Contains(game.State.CardObjects["ASSASSIN-UNIT"].UntilEndOfTurnEffects,
            e => e.StartsWith(CoreRuleEngine.TimeAssassinDestructionPrefix, StringComparison.Ordinal));
        await game.Act("P1", SmuggledWeapon(game.State, false) with { TargetObjectIds = ["ASSASSIN-UNIT"] });
        await game.Top();
        await game.Act("P1", new EndTurnCommand());
        Assert.Equal(CoreRuleEngine.TimeAssassinEndDestructionKind, Assert.Single(game.State.StackItems).EndAbility!.Kind);
        await game.Top();
        var request = Assert.IsType<RuleChoiceContinuation>(game.State.PendingRuleChoice).Request;
        var choice = Assert.Single(request.Options, o => o.Id.StartsWith("PAID_RECALL:ASSASSIN-UNIT:", StringComparison.Ordinal));
        await game.Act("P1", new PayCostCommand(request.Id, "RULE_REPLACEMENT", [choice.Id]));
        Assert.Contains("ASSASSIN-UNIT", game.State.PlayerZones["P1"].Base);
        Assert.True(game.State.CardObjects["ASSASSIN-UNIT"].IsExhausted);
        Assert.Empty(game.State.StackItems);
        Assert.DoesNotContain(game.State.CardObjects["ASSASSIN-UNIT"].UntilEndOfTurnEffects,
            e => e.StartsWith(CoreRuleEngine.TimeAssassinDestructionPrefix, StringComparison.Ordinal));
        await game.Act(game.State.TurnPlayerId, new EndTurnCommand());
        Assert.Empty(game.State.StackItems);
        await game.Act(game.State.TurnPlayerId, new EndTurnCommand());
        Assert.Empty(game.State.StackItems);
        Assert.Contains("ASSASSIN-UNIT", game.State.PlayerZones["P1"].Base);
        Assert.Single(game.Events(), e => e.Event.Kind == "UNIT_RECALLED_TO_BASE"
            && Equals(e.Event.Payload.GetValueOrDefault("targetObjectId"), "ASSASSIN-UNIT"));
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "UNIT_DESTROYED"
            && Equals(e.Event.Payload.GetValueOrDefault("targetObjectId"), "ASSASSIN-UNIT"));
        await game.Replay();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task SmuggledWeaponGrantDoesNotFollowAReturnedAndReplayedTarget(bool borrowed)
    {
        var game = new Driver(SmuggledWeaponPosition(borrowed));
        await game.Act("P1", SmuggledWeapon(game.State, borrowed));
        await game.Top();
        var generation = game.State.CardObjects["ALLY"].ObjectGeneration;
        Assert.Single(PaidRecallGrants(game.State));
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["ALLY"]));
        await game.Top();
        Assert.Empty(PaidRecallGrants(game.State));
        await game.Act("P1", new PlayCardCommand("ALLY", "OGN·219/298", [], Destination: "BASE"));
        Assert.True(game.State.CardObjects["ALLY"].ObjectGeneration > generation);
        await game.Act("P1", new PlayCardCommand("KILL-ONE", "OGN·229/298", ["ALLY"]));
        await game.Top();
        Assert.Null(game.State.PendingRuleChoice);
        Assert.Contains("ALLY", game.State.PlayerZones["P1"].Graveyard);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "UNIT_RECALLED_TO_BASE");
        await game.Replay();
    }

}
