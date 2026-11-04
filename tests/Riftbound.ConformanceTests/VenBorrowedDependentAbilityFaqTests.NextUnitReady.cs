using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed partial class VenBorrowedDependentAbilityFaqTests
{
    private static MatchState NextUnitReadyPosition()
    {
        var state = BorrowedBombPosition("OGN·021/298");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["UNIT"] = state.CardObjects["UNIT"] with { IsExhausted = false } } };
        state = AttackDamageDistributionTests.Spell(state, "ENCOURAGE", "OGN·048/298", "P1");
        foreach (var id in new[] { "FIRST-UNIT", "SECOND-UNIT" })
            state = HandDiscardObserverTests.Put(state, id, "OGN·001/298", "P1", "HAND");
        return state;
    }

    private static string NextUnitReadyAbility(MatchState state, bool borrowed)
        => borrowed ? TemplateId(state, "下一名单位") : "SUN_DISC_NEXT_UNIT_READY";

    private static string[] NextUnitReadyGrants(MatchState state)
        => state.UntilEndOfTurnEffects.Where(m => m.StartsWith("ACTIVATED_NEXT_UNIT_READY:", StringComparison.Ordinal)).ToArray();

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    [InlineData(true, true, false)]
    [InlineData(true, true, true)]
    public async Task NextUnitReadyEncourageIsCheckedAtResolutionAndConsumedByOneActualUnit(bool borrowed, bool encourage, bool response)
    {
        var initial = NextUnitReadyPosition();
        var source = borrowed ? "HOST" : "UNIT";
        var id = NextUnitReadyAbility(initial, borrowed);
        await RadGearTestPosition.Reject(initial, "P2", new ActivateAbilityCommand(source, id, []));
        await RadGearTestPosition.Reject(initial, "P1", new ActivateAbilityCommand(source, id, ["ALLY"]));
        var game = new Driver(initial);
        if (encourage && !response)
        {
            await game.Act("P1", new PlayCardCommand("ENCOURAGE", "OGN·048/298", []));
            await game.Top();
        }
        await game.Act("P1", new ActivateAbilityCommand(source, id, []));
        Assert.True(game.State.CardObjects[source].IsExhausted);
        Assert.Empty(NextUnitReadyGrants(game.State));
        if (response)
        {
            await game.Act("P1", new PlayCardCommand("ENCOURAGE", "OGN·048/298", []));
            await game.Top();
        }
        await game.Top();
        Assert.Equal(encourage ? 1 : 0, NextUnitReadyGrants(game.State).Length);
        if (encourage)
        {
            var marker = Assert.Single(NextUnitReadyGrants(game.State));
            Assert.DoesNotContain(marker, JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(game.State)["P2"]));
            Assert.Contains("invalid activated next play grant", OfficialInsightAndSpellLockTests.Errors(game.State with {
                UntilEndOfTurnEffects = [..game.State.UntilEndOfTurnEffects, marker] }));
            var grant = JsonSerializer.Deserialize<ActivatedPlayGrant>(Convert.FromBase64String(marker["ACTIVATED_NEXT_UNIT_READY:".Length..]))!;
            foreach (var forged in new[] { grant with { TurnNumber = grant.TurnNumber + 1 },
                grant with { CardNo = "FORGED" }, grant with { StackItemId = "FORGED" },
                grant with { Borrowed = null, Printed = null } })
            {
                var bad = "ACTIVATED_NEXT_UNIT_READY:" + Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(forged));
                Assert.Contains("invalid activated next play grant", OfficialInsightAndSpellLockTests.Errors(game.State with {
                    UntilEndOfTurnEffects = game.State.UntilEndOfTurnEffects.Select(m => m == marker ? bad : m).ToArray() }));
            }
        }
        await game.Act("P1", new PlayCardCommand("FIRST-UNIT", "OGN·001/298", [], Destination: "BASE"));
        Assert.Equal(!encourage, game.State.CardObjects["FIRST-UNIT"].IsExhausted);
        Assert.Empty(NextUnitReadyGrants(game.State));
        await game.Act("P1", new PlayCardCommand("SECOND-UNIT", "OGN·001/298", [], Destination: "BASE"));
        Assert.True(game.State.CardObjects["SECOND-UNIT"].IsExhausted);
        await game.Replay();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NextUnitReadyIgnoresItsSourcesOwnPlayAndExpiresAtTurnEnd(bool borrowed)
    {
        var source = borrowed ? "HOST" : "UNIT";
        var cardNo = borrowed ? "OGN·111/298" : "OGN·021/298";
        var initial = RadUnitEffectTestFixture.Place(NextUnitReadyPosition(), source, "P1", "HAND");
        if (borrowed) initial = HandDiscardObserverTests.Put(initial, "AURA", "RAD·019/167", "P1", "BASE");
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand(source, cardNo, [], Destination: "BASE"));
        Assert.False(game.State.CardObjects[source].IsExhausted);
        await game.Act("P1", new ActivateAbilityCommand(source, NextUnitReadyAbility(game.State, borrowed), []));
        await game.Top();
        Assert.Empty(NextUnitReadyGrants(game.State));
        await game.Replay();

        var other = new Driver(NextUnitReadyPosition());
        await other.Act("P1", new PlayCardCommand("ENCOURAGE", "OGN·048/298", []));
        await other.Top();
        await other.Act("P1", new ActivateAbilityCommand(source, NextUnitReadyAbility(other.State, borrowed), []));
        await other.Top();
        Assert.Single(NextUnitReadyGrants(other.State));
        await other.Act("P1", new EndTurnCommand());
        Assert.Empty(NextUnitReadyGrants(other.State));
        await other.Replay();
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task NextUnitReadyKeepsIndependentSourcesAfterRealReturnAndTokenDisappearance(bool token, bool beforeResolution)
    {
        var initial = NextUnitReadyPosition();
        if (token) initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["HOST"] = initial.CardObjects["HOST"] with { TokenFactoryCardNo = P6TokenFactoryCatalog.ImageTokenCardNo,
                CopyFaceTransitions = [new(0, initial.CardObjects["HOST"].ObjectGeneration,
                    P6TokenFactoryCatalog.ImageTokenCardNo, initial.CardObjects["HOST"].CardNo!)] } } };
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("ENCOURAGE", "OGN·048/298", []));
        await game.Top();
        await game.Act("P1", new ActivateAbilityCommand("HOST", NextUnitReadyAbility(game.State, true), []));
        if (!beforeResolution) await game.Top();
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["HOST"]));
        await game.Top();
        if (beforeResolution) await game.Top();
        Assert.Equal(!token, game.State.CardObjects.ContainsKey("HOST"));
        var marker = Assert.Single(NextUnitReadyGrants(game.State));
        if (token)
        {
            var grant = JsonSerializer.Deserialize<ActivatedPlayGrant>(Convert.FromBase64String(marker["ACTIVATED_NEXT_UNIT_READY:".Length..]))!;
            var borrowed = grant.Borrowed!;
            Assert.NotNull(borrowed.Borrower.CeasedSource);
            var missing = grant with { Borrowed = borrowed with { Borrower = borrowed.Borrower with { CeasedSource = null } } };
            var bad = "ACTIVATED_NEXT_UNIT_READY:" + Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(missing));
            Assert.Contains("invalid activated next play grant", OfficialInsightAndSpellLockTests.Errors(game.State with {
                UntilEndOfTurnEffects = game.State.UntilEndOfTurnEffects.Select(m => m == marker ? bad : m).ToArray() }));
        }
        Assert.DoesNotContain(":HOST:", JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(game.State)["P2"]));
        await game.Act("P1", new PlayCardCommand("FIRST-UNIT", "OGN·001/298", [], Destination: "BASE"));
        Assert.False(game.State.CardObjects["FIRST-UNIT"].IsExhausted);
        Assert.Empty(NextUnitReadyGrants(game.State));
        await game.Replay();
    }

    [Fact]
    public async Task NextUnitReadyConsumesAllGrantsOnOnlyTheFirstTokenInOneInstruction()
    {
        var initial = HandDiscardObserverTests.Put(NextUnitReadyPosition(), "TOKEN-DONOR", "SFD·168/221", "P1", "BASE");
        initial = AttackDamageDistributionTests.Spell(initial, "READY", "OGN·146/298", "P1");
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("ENCOURAGE", "OGN·048/298", []));
        await game.Top();
        await game.Act("P1", new ActivateAbilityCommand("HOST", NextUnitReadyAbility(game.State, true), []));
        await game.Top();
        await game.Act("P1", new ActivateAbilityCommand("UNIT", "SUN_DISC_NEXT_UNIT_READY", []));
        await game.Top();
        Assert.Equal(2, NextUnitReadyGrants(game.State).Length);
        await game.Act("P1", new PlayCardCommand("READY", "OGN·146/298", ["HOST"]));
        await game.Top();
        var tokenSkill = TemplateId(game.State, "战力的随从");
        await game.Act("P1", new ActivateAbilityCommand("HOST", tokenSkill, []));
        await game.Top();
        await FinishBorrowedTokenLocations(game);
        var units = BorrowedCreatedUnits(game.State);
        Assert.Equal(3, units.Length);
        Assert.Single(units, u => !u.IsExhausted);
        Assert.Empty(NextUnitReadyGrants(game.State));
        await game.Replay();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NextUnitReadyAppliesToARealFreeEffectPlay(bool borrowed)
    {
        var initial = HandDiscardObserverTests.Put(BorrowedReturnPlayPosition(), "DISC", "OGN·021/298", "P1", "BASE");
        initial = AttackDamageDistributionTests.Spell(initial, "ENCOURAGE", "OGN·048/298", "P1");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["UNIT"] = initial.CardObjects["UNIT"] with { IsExhausted = false } } };
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("ENCOURAGE", "OGN·048/298", []));
        await game.Top();
        await game.Act("P1", new ActivateAbilityCommand(borrowed ? "HOST" : "DISC", NextUnitReadyAbility(game.State, borrowed), []));
        await game.Top();
        await game.Act("P1", new ActivateAbilityCommand("UNIT", P4ActivatedAbilityCatalog.TranspositionMageAbilityId, ["TARGET"]));
        await game.Top();
        Assert.NotNull(game.State.PendingEffectPlay);
        Assert.Single(NextUnitReadyGrants(game.State));
        await game.Act("P1", new PlayCardCommand("PLAY", "OGN·001/298", [], Destination: "BASE"));
        Assert.False(game.State.CardObjects["PLAY"].IsExhausted);
        Assert.Empty(NextUnitReadyGrants(game.State));
        await game.Replay();
    }
}
