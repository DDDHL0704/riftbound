using System.Text.Json;

namespace Riftbound.Engine;

public static partial class MatchRecoveryValidator
{
    private static T? ReadDestructionFamilyView<T>(object? payload, string property) where T : class
    {
        try {
            var json = JsonSerializer.SerializeToElement(payload);
            return json.ValueKind == JsonValueKind.Object && json.TryGetProperty(property, out var raw)
                && raw.ValueKind != JsonValueKind.Null ? raw.Deserialize<T>(new JsonSerializerOptions(JsonSerializerDefaults.Web)) : null;
        }
        catch (JsonException) { return null; }
    }

    private static bool ValidateDestructionFamilyView(object? payload, string? effect, string? actor, string? source, List<string> errors)
    {
        var damage = ReadDestructionFamilyView<DamageDestructionContext>(payload, "damageDestruction");
        var graveyard = ReadDestructionFamilyView<GraveyardPlayContext>(payload, "graveyardPlay");
        var json = JsonSerializer.SerializeToElement(payload);
        foreach (var property in new[] { "damageDestruction", "graveyardPlay" })
            if (json.ValueKind == JsonValueKind.Object && json.TryGetProperty(property, out var raw) && raw.ValueKind != JsonValueKind.Null
                && (property == "damageDestruction" ? damage is null : graveyard is null)) errors.Add("malformed public destruction-family context");
        if (damage is not null || effect == CoreRuleEngine.DamageDestructionEffect)
        {
            if (damage?.Source is not { Generation: >= 0 } || damage.DamagedUnit is not { Generation: >= 0 }
                || effect != CoreRuleEngine.DamageDestructionEffect || source != damage.Source.ObjectId
                || actor != damage.ControllerId || damage.CreatedAtTick <= 0 || damage.DamagedAtTick < damage.CreatedAtTick)
                errors.Add("invalid public damage destruction context");
            return true;
        }
        if (graveyard is not null || effect == CoreRuleEngine.GraveyardPlayEffect)
        {
            if (graveyard?.Source is not { Generation: >= 0 } || graveyard.Attribution is not { FromSpell: true }
                || graveyard.Destroyed is not { Generation: >= 0 } || effect != CoreRuleEngine.GraveyardPlayEffect
                || source != graveyard.Source.ObjectId || actor != graveyard.ControllerId
                || actor != graveyard.Attribution.ResponsiblePlayerId || graveyard.CreatedAtTick <= 0)
                errors.Add("invalid public graveyard play context");
            return true;
        }
        return false;
    }

    private static void CompareDestructionFamilyView(object? payload, DamageDestructionContext? damage,
        GraveyardPlayContext? graveyard, List<string> errors)
    {
        if (MatchStateHasher.HashValue(ReadDestructionFamilyView<DamageDestructionContext>(payload, "damageDestruction")) != MatchStateHasher.HashValue(damage)
            || MatchStateHasher.HashValue(ReadDestructionFamilyView<GraveyardPlayContext>(payload, "graveyardPlay")) != MatchStateHasher.HashValue(graveyard))
            errors.Add("public destruction-family context differs from authoritative state");
    }

    private static void ValidateDestructionFamilyStackViews(IReadOnlyList<object?> payloads, MatchState state, List<string> errors)
    {
        foreach (var payload in payloads)
        {
            var json = JsonSerializer.SerializeToElement(payload);
            string? Read(string name) => json.ValueKind == JsonValueKind.Object && json.TryGetProperty(name, out var value)
                && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
            var id = Read("stackItemId");
            var item = state.StackItems.FirstOrDefault(i => i.StackItemId == id);
            var effect = Read("effectKind");
            if (item?.DamageDestruction is null && item?.GraveyardPlay is null
                && effect != CoreRuleEngine.DamageDestructionEffect && effect != CoreRuleEngine.GraveyardPlayEffect
                && !(json.ValueKind == JsonValueKind.Object && (json.TryGetProperty("damageDestruction", out var d) && d.ValueKind != JsonValueKind.Null
                    || json.TryGetProperty("graveyardPlay", out var g) && g.ValueKind != JsonValueKind.Null))) continue;
            var actor = Read("controllerId"); var source = Read("sourceObjectId");
            ValidateDestructionFamilyView(payload, effect, actor, source, errors);
            if (item?.DamageDestruction is not null || item?.GraveyardPlay is not null)
            {
                var cardNo = Read("cardNo");
                if (cardNo != item.CardNo) errors.Add("public destruction-family printing differs from authoritative state");
            }
            CompareDestructionFamilyView(payload, item?.DamageDestruction, item?.GraveyardPlay, errors);
        }
    }

    private static void ValidateDestructionFamilyState(MatchState state, List<string> errors)
    {
        if (!CoreRuleEngine.ValidDamageDestructionState(state)) errors.Add("invalid delayed damage destruction instruction");
        foreach (var item in state.TriggerQueue)
        {
            if (item.DamageDestruction is { } d ? item.EffectKind != CoreRuleEngine.DamageDestructionEffect
                    || d.DamagedUnit is null || item.TriggeredByEventKind != "DAMAGE_APPLIED"
                    || item.TriggerId != CoreRuleEngine.DamageDestructionTriggerId(d)
                    || d.DamagedAtTick != item.EnqueuedAtTick
                    || !CoreRuleEngine.ValidDamageDestruction(state, d, item.SourceObjectId, item.ControllerId, item.SourceCardNo)
                : item.EffectKind == CoreRuleEngine.DamageDestructionEffect) errors.Add("invalid captured damage destruction trigger");
            if (item.GraveyardPlay is { } g ? item.EffectKind != CoreRuleEngine.GraveyardPlayEffect
                    || item.TriggeredByEventKind != "UNIT_DESTROYED" || item.TriggerId != CoreRuleEngine.GraveyardPlayTriggerId(g)
                    || g.CreatedAtTick != item.EnqueuedAtTick
                    || !CoreRuleEngine.ValidGraveyardPlay(state, g, item.SourceObjectId, item.ControllerId, item.SourceCardNo)
                : item.EffectKind == CoreRuleEngine.GraveyardPlayEffect) errors.Add("invalid captured graveyard play trigger");
        }
        foreach (var item in state.StackItems)
        {
            if (item.DamageDestruction is { } d ? item.EffectKind != CoreRuleEngine.DamageDestructionEffect
                    || d.DamagedUnit is null || item.StackItemId != "ordered-" + CoreRuleEngine.DamageDestructionTriggerId(d)
                    || !CoreRuleEngine.ValidDamageDestruction(state, d, item.SourceObjectId, item.ControllerId, item.CardNo)
                : item.EffectKind == CoreRuleEngine.DamageDestructionEffect) errors.Add("invalid captured damage destruction stack");
            if (item.GraveyardPlay is { } g ? item.EffectKind != CoreRuleEngine.GraveyardPlayEffect
                    || item.EffectPlayCompleted
                    || item.StackItemId != "ordered-" + CoreRuleEngine.GraveyardPlayTriggerId(g)
                    || !CoreRuleEngine.ValidGraveyardPlay(state, g, item.SourceObjectId, item.ControllerId, item.CardNo)
                : item.EffectKind == CoreRuleEngine.GraveyardPlayEffect) errors.Add("invalid captured graveyard play stack");
        }
    }
}
