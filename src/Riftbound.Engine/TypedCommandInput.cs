using System.Text.Json;
using System.Text.Json.Serialization;
using Riftbound.Contracts;

namespace Riftbound.Engine;

// The actual C# arguments, used when a caller supplied no native wire JSON.
// This is journal input metadata; it never fills or changes RawCommand.
public sealed record TypedCommandInput(
    [property: JsonRequired] string Kind,
    [property: JsonRequired] JsonElement Value)
{
    private const string ScenarioKind = "DEV_SEED_SCENARIO";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly IReadOnlyDictionary<string, Type> CommandTypes = new Dictionary<string, Type>(StringComparer.Ordinal)
    {
        ["READY"] = typeof(ReadyCommand), ["SUBMIT_DECK"] = typeof(SubmitDeckCommand),
        ["MULLIGAN"] = typeof(MulliganCommand), ["PASS_PRIORITY"] = typeof(PassPriorityCommand),
        ["PASS_FOCUS"] = typeof(PassFocusCommand), ["PASS"] = typeof(PassCommand),
        ["END_TURN"] = typeof(EndTurnCommand), ["SURRENDER"] = typeof(SurrenderCommand),
        ["PLAY_CARD"] = typeof(PlayCardCommand), ["ACTIVATE_ABILITY"] = typeof(ActivateAbilityCommand),
        ["LEGEND_ACT"] = typeof(LegendActCommand), ["HIDE_CARD"] = typeof(HideCardCommand),
        ["TAP_RUNE"] = typeof(TapRuneCommand), ["RECYCLE_RUNE"] = typeof(RecycleRuneCommand),
        ["REVEAL_CARD"] = typeof(RevealCardCommand), ["MOVE_UNIT"] = typeof(MoveUnitCommand),
        ["ASSEMBLE_EQUIPMENT"] = typeof(AssembleEquipmentCommand), ["DECLARE_BATTLE"] = typeof(DeclareBattleCommand),
        ["PAY_COST"] = typeof(PayCostCommand), ["ASSIGN_COMBAT_DAMAGE"] = typeof(AssignCombatDamageCommand),
        ["ASSIGN_EFFECT_DAMAGE"] = typeof(AssignEffectDamageCommand), ["ORDER_TRIGGERS"] = typeof(OrderTriggersCommand),
        ["CHOOSE_HAND_CARDS"] = typeof(ChooseHandCardsCommand), ["CHOOSE_CARDS"] = typeof(ChooseCardsCommand)
    };
    private static readonly IReadOnlyDictionary<string, Type> Kinds = CommandTypes.Values
        .Append(typeof(UnsupportedCommand)).ToDictionary(type => type.Name, StringComparer.Ordinal);

    public static TypedCommandInput Capture(GameCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        var input = new TypedCommandInput(command.GetType().Name,
            JsonSerializer.SerializeToElement(command, command.GetType(), Json));
        if (!input.TryRestore(out _))
            throw new MatchSessionException(ErrorCodes.InvalidPayload, "无法记录该类型的行动输入。");
        return input;
    }

    internal static TypedCommandInput CaptureScenario(string scenarioId)
        => new(ScenarioKind, JsonSerializer.SerializeToElement(new { scenarioId }, Json));

    public bool TryRestore(out GameCommand command)
    {
        command = null!;
        if (Kind is null || !Kinds.TryGetValue(Kind, out var type) || Value.ValueKind != JsonValueKind.Object)
            return false;
        try
        {
            if (JsonSerializer.Deserialize(Value, type, Json) is not GameCommand restored
                || string.IsNullOrWhiteSpace(restored.CmdType) || restored.CmdType != restored.CmdType.Trim()
                || restored is not UnsupportedCommand
                    && (!CommandTypes.TryGetValue(restored.CmdType, out var expected) || expected != type)
                || MatchStateHasher.HashValue(Value) != MatchStateHasher.HashValue(
                    JsonSerializer.SerializeToElement(restored, type, Json))) return false;
            command = restored;
            return true;
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or ArgumentException or NotSupportedException)
        {
            return false;
        }
    }

    internal GameCommand Restore()
        => TryRestore(out var command) ? command
            : throw new MatchSessionException(ErrorCodes.RecoveryInconsistent, "typed command input is invalid");

    internal bool TryScenario(out string scenarioId)
    {
        scenarioId = "";
        if (Kind != ScenarioKind || Value.ValueKind != JsonValueKind.Object
            || !Value.TryGetProperty("scenarioId", out var value) || value.ValueKind != JsonValueKind.String)
            return false;
        var text = value.GetString();
        if (string.IsNullOrWhiteSpace(text) || text != text.Trim()
            || MatchStateHasher.HashValue(Value) != MatchStateHasher.HashValue(
                JsonSerializer.SerializeToElement(new { scenarioId = text }, Json))) return false;
        scenarioId = text;
        return true;
    }

    internal bool MatchesRecordedType(string commandType)
        => TryScenario(out var scenario) ? commandType == ScenarioKind + ":" + scenario
            : TryRestore(out var command) && command.CmdType == commandType;

    internal bool MatchesWire(JsonElement raw)
    {
        if (TryScenario(out var scenario))
            return raw.TryGetProperty("scenarioId", out var value)
                && value.ValueKind == JsonValueKind.String && value.GetString() == scenario;
        if (!TryRestore(out var actual)) return false;
        try
        {
            var decoded = GameCommandJsonMapper.Map(raw);
            if (actual.GetType() != decoded.GetType()) return false;
            return WireMeaningHash(actual) == WireMeaningHash(decoded);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or ArgumentException)
        {
            return false;
        }
    }

    // Only equivalences already used by the rule executor are compared here.
    // Rune batch null/empty and explicit repeat/movement declarations stay distinct.
    private static string WireMeaningHash(GameCommand command)
    {
        GameCommand normalized = command switch
        {
            PlayCardCommand c => c with { OptionalCosts = c.OptionalCosts ?? [] },
            ActivateAbilityCommand c => c with { OptionalCosts = c.OptionalCosts ?? [] },
            LegendActCommand c => c with { OptionalCosts = c.OptionalCosts ?? [] },
            HideCardCommand c => c with { OptionalCosts = c.OptionalCosts ?? [] },
            RevealCardCommand c => c with { OptionalCosts = c.OptionalCosts ?? [] },
            MoveUnitCommand c => c with { OptionalCosts = c.OptionalCosts ?? [], SourceObjectIds = c.SourceObjectIds ?? [] },
            AssembleEquipmentCommand c => c with { OptionalCosts = c.OptionalCosts ?? [] },
            DeclareBattleCommand c => c with { OptionalCosts = c.OptionalCosts ?? [],
                AttackerObjectIds = c.AttackerObjectIds ?? [], DefenderObjectIds = c.DefenderObjectIds ?? [],
                BattlefieldTargetObjectIds = c.BattlefieldTargetObjectIds ?? [] },
            OrderTriggersCommand c => c with { TriggerIds = c.OrderedTriggerIds ?? c.TriggerIds,
                OrderedTriggerIds = c.OrderedTriggerIds ?? c.TriggerIds },
            UnsupportedCommand c => new UnsupportedCommand(c.CmdType),
            _ => command
        };
        return MatchStateHasher.HashValue(JsonSerializer.SerializeToElement(normalized, normalized.GetType(), Json));
    }

    internal static TypedCommandInput? Parameterless(string commandType)
    {
        GameCommand? command = commandType switch
        {
            "READY" => new ReadyCommand(), "PASS_PRIORITY" => new PassPriorityCommand(),
            "PASS_FOCUS" => new PassFocusCommand(), "PASS" => new PassCommand(),
            "END_TURN" => new EndTurnCommand(), "SURRENDER" => new SurrenderCommand(),
            _ => CommandTypes.ContainsKey(commandType) ? null : new UnsupportedCommand(commandType)
        };
        return command is null ? null : Capture(command);
    }

    internal string Fingerprint => "typed:" + Kind + ":" + MatchStateHasher.HashValue(Value);
}
