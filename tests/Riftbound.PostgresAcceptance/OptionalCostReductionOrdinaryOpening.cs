using System.Text.Json;
using System.Text.Json.Nodes;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;

// A new ordinary match, including deck submission, the real shuffle and every
// resource action. No accepted state or historical RuleInputs are imported.
internal static class OptionalCostReductionOrdinaryOpening
{
    internal sealed record Result(MatchState State, string Actor, string Reducer,
        long ReducerGeneration, int OpeningCommands, PlayCardCommand Command, PlayCardCommand ForgedCommand);

    public static async Task<Result> BuildAsync(MatchState initial, IMatchSession session, string reducerCard)
    {
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var actor = initial.Seats.Single(pair => pair.Value == "P1").Key;
        var opponent = initial.Seats.Keys.Single(player => player != actor);
        var deck = await DeckAsync(reducerCard);
        var state = initial;
        var submitted = 0;
        await Submit(actor, new SubmitDeckCommand(deck.LegendCardNo, deck.ChampionCardNo, deck.MainDeck, deck.RuneDeck, deck.Battlefields));
        await Submit(opponent, new SubmitDeckCommand(deck.LegendCardNo, deck.ChampionCardNo, deck.MainDeck, deck.RuneDeck, deck.Battlefields));
        await Submit(actor, new ReadyCommand());
        await Submit(opponent, new ReadyCommand());
        while (state.Phase == MatchPhases.Mulligan)
        {
            var player = initial.Seats.Keys.First(id => session.PromptFor(id).Actionable
                && session.PromptFor(id).Candidates!.Any(candidate => candidate.Action == CommandTypes.Mulligan && candidate.Enabled));
            await Submit(player, new MulliganCommand([]));
        }

        for (var step = 0; step < 80; step++)
        {
            if (state.PriorityPlayerId is { Length: > 0 } priority)
            {
                await Submit(priority, new PassPriorityCommand());
                continue;
            }
            if (state.FocusPlayerId is { Length: > 0 } focus && state.TimingState != TimingStates.NeutralOpen)
            {
                await Submit(focus, new PassFocusCommand());
                continue;
            }
            Check(state.PendingCardChoice is null && state.PendingPayment is null && state.PendingRuleChoice is null
                && state.Status == MatchStatuses.InProgress, "Unexpected continuation while preparing an ordinary payment opening.");
            if (state.TurnPlayerId == actor)
            {
                var zones = state.PlayerZones[actor];
                var reducer = zones.Base.FirstOrDefault(id => state.CardObjects[id].CardNo == reducerCard);
                var ready = ReadyRunes();
                if (reducer is null && ready.Count >= 3)
                {
                    var purple = ready.FirstOrDefault(id => state.CardObjects[id].Tags.Contains("COLOR:" + RuneTrait.Purple));
                    if (purple is not null)
                    {
                        foreach (var rune in ready.OrderBy(id => id != purple).ThenBy(id => id, StringComparer.Ordinal).Take(3))
                            await Submit(actor, new TapRuneCommand(rune));
                        var discard = state.PlayerZones[actor].Hand.FirstOrDefault(id => state.CardObjects[id].CardNo != "SFD·066/221")
                            ?? state.PlayerZones[actor].Hand.First();
                        var champion = state.PlayerZones[actor].ChampionZone.Single();
                        await Submit(actor, new PlayCardCommand(champion, reducerCard, [discard], OptionalCosts: ["RECYCLE_RUNE:" + purple]));
                        continue;
                    }
                }
                var freeze = zones.Hand.FirstOrDefault(id => state.CardObjects[id].CardNo == "SFD·066/221");
                if (reducer is not null && freeze is not null && ready.Count >= 3)
                {
                    Check(state.RunePools[actor].Mana == 0 && state.RunePools[actor].TotalPower == 0,
                        "The final ordinary opening starts with no previously floating resources.");
                    foreach (var rune in ready.Take(3)) await Submit(actor, new TapRuneCommand(rune));
                    var generation = state.CardObjects[reducer].ObjectGeneration;
                    string Reduction(long sourceGeneration) => "OPTIONAL_COST_REDUCTION:" + Uri.EscapeDataString(reducer)
                        + ":" + sourceGeneration + ":ECHO:MANA";
                    var command = new PlayCardCommand(freeze, "SFD·066/221", [reducer], OptionalCosts: ["ECHO", Reduction(generation)]);
                    var quote = new CoreRuleEngine().PreviewPlayCard(state, actor,
                        new("ordinary-reduction-quote", session.PromptFor(actor).PromptId!, state.Tick, command));
                    Check(quote.CanPay && quote.Cost?.Mana == 3 && quote.Cost.RemainingMana == 0,
                        "The actual opening must afford the explicit reduced Echo for exactly three mana.");
                    return new(state, actor, reducer, generation, submitted, command,
                        command with { OptionalCosts = ["ECHO", Reduction(generation + 1)] });
                }
            }
            await Submit(state.TurnPlayerId, new EndTurnCommand());
        }
        throw new InvalidOperationException("No ordinary explicit-discount opening within the bounded turn search.");

        IReadOnlyList<string> ReadyRunes() => state.PlayerZones[actor].Base
            .Where(id => state.CardObjects[id].Tags.Contains(CardObjectTags.RuneCard)
                && !state.CardObjects[id].IsExhausted && !state.CardObjects[id].IsFaceDown
                && state.CardObjects[id].ControllerId == actor)
            .Order(StringComparer.Ordinal).ToArray();

        async Task Submit(string player, GameCommand command)
        {
            var node = JsonSerializer.SerializeToNode(command, command.GetType(), json)!.AsObject();
            node["promptId"] = session.PromptFor(player).PromptId;
            node["snapshotTick"] = state.Tick;
            var raw = JsonSerializer.SerializeToElement(node);
            var id = "optional-reduction-opening-" + submitted++;
            var result = command switch {
                SubmitDeckCommand submittedDeck => await session.SubmitDeckAsync(player, id, submittedDeck, raw, default),
                ReadyCommand => await session.ReadyAsync(player, id, raw, default),
                _ => await session.SubmitAsync(player, id, command, raw, default)
            };
            Check(result.Accepted, command.CmdType + ": " + result.ErrorMessage);
            state = result.State;
        }
    }

    private static async Task<OfficialDecklist> DeckAsync(string reducer)
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync();
        var main = new List<string> { reducer, "SFD·066/221", "SFD·066/221", "SFD·066/221" };
        // Keep the deck/order fixed when unrelated catalog entries are added.
        foreach (var no in new[] { "OGN·120/298", "OGN·204/298", "OGN·090/298", "OGN·095/298",
            "OGN·104/298", "OGN·169/298", "OGN·179/298", "OGN·183/298", "SFD·063/221",
            "SFD·064/221", "SFD·073/221", "SFD·080/221" })
            main.AddRange(Enumerable.Repeat(no, 3));
        var deck = new OfficialDecklist("SFD·199/221", reducer, main,
            Enumerable.Repeat("UNL-R03", 6).Concat(Enumerable.Repeat("UNL-R05", 6)).ToArray(),
            ["OGN·278/298", "OGN·280/298", "OGN·294/298"]);
        var validation = OfficialDeckValidator.Validate(deck, catalog, OfficialDeckFormat.ChinaStandard20260724);
        Check(validation.IsValid, string.Join("; ", validation.Errors));
        return deck;
    }

    private static void Check(bool condition, string? message)
    { if (!condition) throw new InvalidOperationException(message); }
}
