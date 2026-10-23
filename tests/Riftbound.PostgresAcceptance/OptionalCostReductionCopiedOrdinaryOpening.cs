using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;

// Ordinary green/purple deck: draw and play Ezreal and Sifr's, then actually
// equip it. The final unit pays its optional green draw fee using that copy.
internal static class OptionalCostReductionCopiedOrdinaryOpening
{
    public static async Task<OptionalCostReductionOrdinaryOpening.Result> BuildAsync(MatchState initial, IMatchSession session)
    {
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var actor = initial.Seats.Single(pair => pair.Value == "P1").Key;
        var opponent = initial.Seats.Keys.Single(player => player != actor);
        var catalog = await OfficialCardCatalog.LoadDefaultAsync();
        var deck = Deck(catalog);
        var state = initial;
        var submitted = 0;
        foreach (var player in new[] { actor, opponent })
            await Submit(player, new SubmitDeckCommand(deck.LegendCardNo, deck.ChampionCardNo, deck.MainDeck, deck.RuneDeck, deck.Battlefields));
        await Submit(actor, new ReadyCommand());
        await Submit(opponent, new ReadyCommand());
        while (state.Phase == MatchPhases.Mulligan)
        {
            var player = initial.Seats.Keys.First(id => session.PromptFor(id).Actionable
                && session.PromptFor(id).Candidates!.Any(candidate => candidate.Action == CommandTypes.Mulligan && candidate.Enabled));
            await Submit(player, new MulliganCommand([]));
        }
        for (var step = 0; step < 120; step++)
        {
            if (state.PriorityPlayerId is { Length: > 0 } priority)
            { await Submit(priority, new PassPriorityCommand()); continue; }
            if (state.FocusPlayerId is { Length: > 0 } focus && state.TimingState != TimingStates.NeutralOpen)
            { await Submit(focus, new PassFocusCommand()); continue; }
            Check(state.PendingCardChoice is null && state.PendingPayment is null && state.PendingRuleChoice is null
                && state.Status == MatchStatuses.InProgress, "Unexpected copied-source opening continuation.");
            if (state.TurnPlayerId == actor)
            {
                var zones = state.PlayerZones[actor];
                var reducer = zones.Base.FirstOrDefault(id => state.CardObjects[id].CardNo == "SFD·149/221");
                var gear = zones.Base.FirstOrDefault(id => state.CardObjects[id].CardNo == "SFD·059/221");
                if (reducer is null)
                {
                    var source = zones.Hand.FirstOrDefault(id => state.CardObjects[id].CardNo == "SFD·149/221");
                    if (source is not null)
                    {
                        var discard = zones.Hand.FirstOrDefault(id => id != source
                            && state.CardObjects[id].CardNo is not ("OGN·044/298" or "SFD·059/221"))
                            ?? zones.Hand.FirstOrDefault(id => id != source);
                        if (discard is not null && await Play(source, "SFD·149/221", [discard])) continue;
                    }
                }
                else if (gear is null)
                {
                    var source = zones.Hand.FirstOrDefault(id => state.CardObjects[id].CardNo == "SFD·059/221");
                    if (source is not null && await Play(source, "SFD·059/221", [])) continue;
                }
                else if (state.CardObjects[gear].AttachedToObjectId is null)
                {
                    var green = ReadyRunes().FirstOrDefault(id => state.CardObjects[id].Tags.Contains("COLOR:" + RuneTrait.Green));
                    if (green is not null)
                    {
                        await Submit(actor, new TapRuneCommand(green));
                        await Submit(actor, new RecycleRuneCommand(green));
                        await Submit(actor, new AssembleEquipmentCommand(gear, reducer, ["ASSEMBLE_GREEN"]));
                        continue;
                    }
                }
                else
                {
                    var target = zones.Hand.FirstOrDefault(id => state.CardObjects[id].CardNo == "OGN·044/298");
                    if (target is not null && ReadyRunes().Count >= 2)
                    {
                        Check(state.RunePools[actor].Mana == 0 && state.RunePools[actor].TotalPower == 0,
                            "The copied-source final opening must have no previously floating resources.");
                        foreach (var rune in ReadyRunes().Take(2)) await Submit(actor, new TapRuneCommand(rune));
                        var candidate = session.PromptFor(actor).Candidates!.Single(item => item.Action == CommandTypes.PlayCard);
                        var metadata = JsonSerializer.SerializeToElement(candidate.Metadata, json);
                        var row = metadata.GetProperty("sourceRequirements").EnumerateArray().Single(item => item.GetProperty("sourceObjectId").GetString() == target);
                        var group = row.GetProperty("optionalCostReductionGroups").EnumerateArray().Single(item => item.GetProperty("ruleTextOrigin").ValueKind != JsonValueKind.Null);
                        var choice = group.GetProperty("choiceIds").EnumerateArray().Single().GetString()!;
                        var generation = state.CardObjects[reducer].ObjectGeneration;
                        var instance = group.GetProperty("sourceInstanceId").GetString()!;
                        var forged = "OPTIONAL_COST_REDUCTION:" + Uri.EscapeDataString(reducer) + ":" + (generation + 1)
                            + ":ADDITIONAL:POWER-green:RULE_TEXT:" + Uri.EscapeDataString(instance);
                        var command = new PlayCardCommand(target, "OGN·044/298", [], OptionalCosts: ["SPEND_POWER:green:1", choice]);
                        var quote = new CoreRuleEngine().PreviewPlayCard(state, actor,
                            new("ordinary-copied-reduction-quote", session.PromptFor(actor).PromptId!, state.Tick, command));
                        Check(quote.CanPay && quote.Cost?.Mana == 2 && quote.Cost.GenericPower + quote.Cost.PowerByTrait.Values.Sum() == 0,
                            "The actual copied instance must fund the entire optional green draw fee.");
                        return new(state, actor, reducer, generation, submitted, command,
                            command with { OptionalCosts = ["SPEND_POWER:green:1", forged] });
                    }
                }
            }
            await Submit(state.TurnPlayerId, new EndTurnCommand());
        }
        throw new InvalidOperationException("No copied-source ordinary opening within the bounded turn search.");

        IReadOnlyList<string> ReadyRunes() => state.PlayerZones[actor].Base.Where(id =>
            state.CardObjects[id].Tags.Contains(CardObjectTags.RuneCard) && !state.CardObjects[id].IsExhausted
            && !state.CardObjects[id].IsFaceDown && state.CardObjects[id].ControllerId == actor).Order(StringComparer.Ordinal).ToArray();

        async Task<bool> Play(string source, string no, IReadOnlyList<string> targets)
        {
            Check(state.RunePools[actor].Mana == 0 && state.RunePools[actor].TotalPower == 0, "Setup resources must not be silently carried into a later cost.");
            Check(CardBehaviorRegistry.TryGetByCardNo(no, out var behavior), "Missing ordinary setup behavior.");
            var power = PrintedPowerCostRules.ForCard(no);
            Check(power.Amount <= 1 && power.Traits.Count <= 1, "Ordinary setup uses only a single explicit printed power symbol.");
            var ready = ReadyRunes();
            var typed = power.Amount == 0 ? null : ready.FirstOrDefault(id => state.CardObjects[id].Tags.Contains("COLOR:" + power.Traits.Single()));
            if (ready.Count < Math.Max(behavior.ManaCost, power.Amount) || power.Amount > 0 && typed is null) return false;
            foreach (var rune in ready.OrderBy(id => id != typed).ThenBy(id => id, StringComparer.Ordinal).Take(behavior.ManaCost))
                await Submit(actor, new TapRuneCommand(rune));
            await Submit(actor, new PlayCardCommand(source, no, targets,
                OptionalCosts: typed is null ? [] : ["RECYCLE_RUNE:" + typed]));
            return true;
        }
        async Task Submit(string player, GameCommand command)
        {
            var node = JsonSerializer.SerializeToNode(command, command.GetType(), json)!.AsObject();
            node["promptId"] = session.PromptFor(player).PromptId; node["snapshotTick"] = state.Tick;
            var raw = JsonSerializer.SerializeToElement(node);
            var id = "optional-copied-opening-" + submitted++;
            var result = command switch {
                SubmitDeckCommand submittedDeck => await session.SubmitDeckAsync(player, id, submittedDeck, raw, default),
                ReadyCommand => await session.ReadyAsync(player, id, raw, default),
                _ => await session.SubmitAsync(player, id, command, raw, default)
            };
            Check(result.Accepted, command.CmdType + ": " + result.ErrorMessage); state = result.State;
        }
    }

    private static OfficialDecklist Deck(OfficialCardCatalog catalog)
    {
        var main = new List<string> { "UNL-055/219" };
        foreach (var no in new[] { "SFD·149/221", "SFD·059/221", "OGN·044/298", "OGN·081/298",
            "OGN·204/298", "OGN·043/298", "OGN·045/298", "OGN·046/298", "OGN·169/298",
            "OGN·179/298", "OGN·183/298", "SFD·033/221", "SFD·046/221" })
            main.AddRange(Enumerable.Repeat(no, 3));
        var result = new OfficialDecklist("UNL-232/219", "UNL-055/219", main,
            Enumerable.Repeat("UNL-R02", 6).Concat(Enumerable.Repeat("UNL-R05", 6)).ToArray(),
            ["OGN·278/298", "OGN·280/298", "OGN·294/298"]);
        var validation = OfficialDeckValidator.Validate(result, catalog, OfficialDeckFormat.ChinaStandard20260724);
        Check(validation.IsValid, string.Join("; ", validation.Errors));
        return result;
    }
    private static void Check(bool condition, string? message)
    { if (!condition) throw new InvalidOperationException(message); }
}
