using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

// Keep preparation in the same journal as the face-down play. The underlying
// driver checks recovery, idempotency, privacy and replay after every command.
internal sealed class FaceDownPlayTestDriver(MatchState initial)
{
    private readonly VenBasicFlowSpellTests.Driver journal = new(initial);
    private int firstScenarioEvent;
    internal MatchState State => journal.State;
    internal Task Act(string player, GameCommand command) => journal.Act(player, command);
    internal Task Top() => journal.Top();
    internal Task Replay() => journal.Replay();
    internal RecoveredEvent[] Events() => journal.Events().Skip(firstScenarioEvent).ToArray();
    internal void BeginScenario() => firstScenarioEvent = journal.Events().Length;

    internal Task NextTurn() => NextTurn(journal);

    internal static async Task NextTurn(VenBasicFlowSpellTests.Driver journal)
    {
        var State = journal.State;
        async Task Act(string player, GameCommand command) { await journal.Act(player, command); State = journal.State; }
        var turn = State.TurnNumber;
        await Act(State.TurnPlayerId, new EndTurnCommand());
        for (var step = 0; step < 60 && State.TurnStartStep is not null
            && State.Status == MatchStatuses.InProgress; step++)
        {
            if (State.PendingCardChoice is { } choice)
                await Act(choice.PlayerId, new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow,
                    choice.LegalObjectIds.Take(choice.RequiredCount).ToArray()));
            else if (State.PendingPayment is { } payment)
                await Act(payment.PlayerId, new PayCostCommand(payment.PaymentId, payment.PaymentWindow, ["DECLINE"]));
            else if (State.StackItems.Count > 0)
                await Act(State.PriorityPlayerId!, new PassPriorityCommand());
            else if (State.TriggerQueue.Count > 0)
                await Act(State.TriggerQueue[0].ControllerId, new OrderTriggersCommand(
                    OrderedTriggerIds: State.TriggerQueue.Select(trigger => trigger.TriggerId).ToArray()));
            else Assert.Fail("Turn preparation must expose its next legal action.");
        }
        Assert.Null(State.TurnStartStep);
        Assert.Equal(turn + 1, State.TurnNumber);
    }

    internal static async Task<FaceDownPlayTestDriver> Ready(MatchState initial, bool ownTurn = false, bool second = false)
    {
        initial = AttackDamageDistributionTests.Spell(initial, "REPLY", "OGN·048/298", "P2");
        if (second) initial = AttackDamageDistributionTests.Spell(initial, "SECOND", "OGN·057/298", "P1");
        var cards = initial.CardObjects.ToDictionary();
        var locations = initial.ObjectLocations.ToDictionary();
        var zones = initial.PlayerZones.ToDictionary();
        foreach (var player in new[] { "P1", "P2" })
        foreach (var number in Enumerable.Range(1, player == "P1" ? 1 : 4))
        {
            var id = $"FACE-DOWN-RESERVE-{player}-{number}";
            cards[id] = new(id, cardNo: "UNL-R03", ownerId: player, controllerId: player,
                tags: [CardObjectTags.RuneCard, "COLOR:blue"]);
            locations[id] = new(player, "BASE");
            zones[player] = zones[player] with { Base = zones[player].Base.Append(id).ToArray() };
        }
        var game = new FaceDownPlayTestDriver(initial with { CardObjects = cards, ObjectLocations = locations, PlayerZones = zones });
        if (second) await game.Act("P1", new HideCardCommand("SECOND", "OGN·057/298", "BATTLEFIELD:F", ["STANDBY_A"]));
        await game.Act("P1", new HideCardCommand("SHADOW", "OGN·057/298", "BATTLEFIELD:F", ["STANDBY_A"]));
        await game.NextTurn();
        foreach (var number in Enumerable.Range(1, 3))
        {
            await game.Act("P2", new TapRuneCommand("SHADOW-P2-RUNE-" + number));
            await game.Act("P2", new TapRuneCommand("FACE-DOWN-RESERVE-P2-" + number));
        }
        foreach (var number in new[] { 4, 5 }) await game.Act("P2", new RecycleRuneCommand("SHADOW-P2-RUNE-" + number));
        await game.Act("P2", new RecycleRuneCommand("FACE-DOWN-RESERVE-P2-4"));
        await game.Act("P2", new PlayCardCommand("REPLY", "OGN·048/298", []));
        await game.Act("P2", new PassPriorityCommand());
        await game.Act("P1", new TapRuneCommand("FACE-DOWN-RESERVE-P1-1"));
        if (ownTurn)
        {
            await game.Top();
            await game.NextTurn();
        }
        game.BeginScenario();
        return game;
    }
}
