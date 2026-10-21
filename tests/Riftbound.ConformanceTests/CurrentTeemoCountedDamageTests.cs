using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class CurrentTeemoCountedDamageTests
{
    private const string Attacker = "P1-TEEMO-DEFEND-ATTACKER";
    private const string Defender = "P2-TEEMO-FACE-UP-DEFENDER";
    private const string Field = "P1-TEEMO-DEFEND-BATTLEFIELD";

    public static IEnumerable<object[]> Printings() =>
    [
        [31342L, "OGN·121/298"], [31343L, "OGN·121a/298"],
        [33334L, "SFD·230/221"], [33335L, "SFD·230*/221"]
    ];

    public static IEnumerable<object[]> Counts() => Printings().SelectMany(p =>
        new[] { 0, 1, 2 }.Select(count => new object[] { p[1], count }));

    [Theory]
    [MemberData(nameof(Printings))]
    public async Task FourExactPrintingsAdoptTheCurrentOfficialDefendOnlyErratum(long id, string printing)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        const string source = "data/official/upstream/2026-10-16/source-refresh-20261015T160523Z/card-catalog.zh-CN.json";
        while (root is not null && !File.Exists(Path.Combine(root.FullName, source))) root = root.Parent;
        Assert.NotNull(root);
        using var raw = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(root.FullName, source)));
        var acquired = raw.RootElement.GetProperty("cards").EnumerateArray().Single(c => c.GetProperty("id").GetInt64() == id);
        Assert.Equal(printing, acquired.GetProperty("cardNo").GetString());
        var current = (await OfficialCardCatalog.LoadDefaultAsync()).Cards.Single(c => c.Id == id);
        Assert.Equal(acquired.GetProperty("errata").GetString(), current.CardEffect);
        Assert.NotEqual(acquired.GetProperty("cardEffect").GetString(), current.CardEffect);
        Assert.True(CardBehaviorRegistry.TryGetByCardNo(printing, out var behavior));
        Assert.Equal(5, behavior.DefendTriggerMainDeckLookCount);
        Assert.Equal(0, behavior.StandbyReactionMainDeckLookCount);
    }

    [Theory]
    [MemberData(nameof(Counts))]
    public async Task ActualDefendChoiceAppliesVoidBonusToOneCountedActionAndKeepsZeroCountWithoutDamage(string printing, int count)
    {
        var state = Position(printing, count);
        var deck = state.PlayerZones["P2"].MainDeck.ToArray();
        var pools = state.RunePools;
        var moved = await OfficialInsightAndSpellLockTests.Act(state, "P1",
            new MoveUnitCommand(Attacker, "BASE", "BATTLEFIELD:" + Field, []));
        Assert.Equal(deck, moved.State.PlayerZones["P2"].MainDeck);
        Assert.DoesNotContain(moved.Events, e => e.Kind is "MAIN_DECK_CARDS_REVEALED" or "DAMAGE_APPLIED" or "CARDS_RECYCLED");
        var choice = Assert.IsType<PendingCardChoiceState>(moved.State.PendingCardChoice);
        Assert.Equal("P2", choice.PlayerId);
        Assert.Equal(1, choice.RequiredCount);
        Assert.Equal(new[] { Attacker }, choice.LegalObjectIds);
        var bad = await new CoreRuleEngine().ResolveAsync(moved.State, new("wrong-defend-actor", "P1", CommandTypes.ChooseCards),
            new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [Attacker]), default);
        Assert.False(bad.Accepted);
        Assert.Empty(bad.Events);
        Assert.Equal(MatchStateHasher.Hash(moved.State), MatchStateHasher.Hash(bad.State));

        var result = await OfficialInsightAndSpellLockTests.Act(moved.State, "P2",
            new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [Attacker]));
        result = await OfficialInsightAndSpellLockTests.Act(result.State, result.State.PriorityPlayerId!, new PassPriorityCommand());
        result = await OfficialInsightAndSpellLockTests.Act(result.State, result.State.PriorityPlayerId!, new PassPriorityCommand());
        var revealed = Assert.Single(result.Events, e => e.Kind == "MAIN_DECK_CARDS_REVEALED" && Equals(e.Payload["sourceObjectId"], Defender));
        Assert.Equal(count, revealed.Payload["damageAmount"]);
        Assert.Equal(count, Assert.IsAssignableFrom<IEnumerable<string>>(revealed.Payload["countedCardIds"]).Count());
        var damage = result.Events.Where(e => e.Kind == "DAMAGE_APPLIED" && Equals(e.Payload["sourceObjectId"], Defender)).ToArray();
        // Current core 135.2.b.5.a lists one counted Deal instruction;
        // 715 adds the bonus to that action's total, once per resolution.
        if (count == 0) Assert.Empty(damage);
        else Assert.Equal(count + 1, Assert.Single(damage).Payload["damage"]);
        Assert.Equal(count == 0 ? 0 : count + 1, result.State.CardObjects[Attacker].Damage);
        Assert.Equal("P2-MAIN-BOTTOM", result.State.PlayerZones["P2"].MainDeck[0]);
        Assert.Equal(deck.Take(5).Order(), result.State.PlayerZones["P2"].MainDeck.Skip(1).Order());
        Assert.Contains(result.Events, e => e.Kind == "CARDS_RECYCLED" && Equals(e.Payload["sourceObjectId"], Defender) && Equals(e.Payload["count"], 5));
        Assert.Equal(pools, result.State.RunePools);
        Assert.Empty(OfficialInsightAndSpellLockTests.Errors(result.State));
    }

    internal static MatchState Position(string printing, int count)
    {
        var state = StandbyDefendTeemoTests.BuildState(printing, battlefieldCardNo: "OGN·296/298", battlefieldControllerId: "P2");
        state = LocalDestructionRecallTests.At(state, Attacker, "P1", "BASE");
        var cards = state.CardObjects.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        var candidates = new[] { "P2-MAIN-STANDBY-001", "P2-MAIN-STANDBY-002" };
        foreach (var id in candidates.Skip(count))
            cards[id] = cards[id] with { CardNo = "SFD·125/221", Tags = [CardObjectTags.UnitCard] };
        return state with { CardObjects = cards };
    }
}
