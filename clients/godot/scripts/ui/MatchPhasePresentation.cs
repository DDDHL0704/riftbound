using System;
using System.Text.Json;
using Godot;

namespace Riftbound.GodotClient.Ui;

// Presentation of public server timing only; this never grants an action locally.
internal static class MatchPhasePresentation
{
    public static Godot.Collections.Dictionary Build(JsonElement snapshot, string viewerId, Func<string, string> name)
    {
        var timing = Object(snapshot, "timing");
        var duel = Object(timing, "spellDuel");
        var battle = Object(timing, "battle");
        var phase = Text(timing, "phase");
        var stackCount = Count(snapshot, "stack");
        var triggerCount = Count(timing, "triggerQueue");
        var duelActive = Bool(duel, "isActive");
        var battleActive = Bool(battle, "isActive");
        var priority = Text(timing, "priorityPlayerId");
        var focus = Text(timing, "focusPlayerId");
        var turn = Text(timing, "turnPlayerId");
        var actor = priority.Length > 0 ? priority : focus.Length > 0 ? focus : Text(snapshot, "activePlayerId");
        string Side(string id) => id.Length == 0 ? "服务端" : id == viewerId ? "我方" : "对手";
        var field = Text(duelActive ? duel : battle, "battlefieldObjectId");
        var title = phase == "MULLIGAN" ? "起手调度" : phase == "TURN_START" ? "回合开始 · 自动流程" : "行动阶段";
        var detail = phase == "TURN_START" ? "正在处理开始阶段效果；完成响应或选择后继续召出、抽牌。" : "选择手牌或场上卡牌行动；完成行动后结束回合。";
        var hint = "";
        var ordering = triggerCount > 1 && priority.Length == 0 && focus.Length == 0;
        var activeWindow = phase == "TURN_START" || duelActive || battleActive || stackCount > 0 || ordering;
        var actorLine = $"{Side(turn)}回合 · {Side(actor)}行动";
        if (ordering)
        {
            title = phase == "TURN_START" ? "回合开始 · 触发排序" : "触发效果 · 排序窗口";
            detail = $"有 {triggerCount} 个触发效果等待排序，请按服务端提示选择顺序。";
            hint = "确认顺序 → 进入响应窗口；触发效果不会直接跳过结算。";
        }
        else if (stackCount > 0)
        {
            title = phase == "TURN_START" ? "回合开始 · 响应窗口" : duelActive ? "法术对决 · 响应窗口" : battleActive ? "战斗 · 响应窗口" : "法术与技能 · 响应窗口";
            actorLine = $"{Side(priority)}拥有优先权";
            detail = $"待结算 {stackCount} 项 · 先结算最上方效果。拥有优先权时可打出反应牌。";
            hint = "双方连续让过 → 结算最上方一项；随后仍可能需要响应。";
        }
        else if (duelActive)
        {
            title = "法术对决 · 出牌窗口";
            actorLine = $"{Side(focus)}拥有焦点";
            detail = "拥有焦点时可打出迅捷或反应牌，也可使用合法技能。";
            hint = "双方连续让过 → 结束法术对决，继续处理战场。";
        }
        else if (battleActive)
        {
            var assigning = Bool(Object(battle, "damageAssignment"), "isActive");
            title = assigning ? "战斗 · 分配伤害" : "战斗 · 响应窗口";
            actorLine = assigning ? $"{Side(actor)}分配伤害" : $"{Side(priority)}拥有优先权";
            detail = assigning ? "按服务端允许的目标分配本方战斗伤害。" : "伤害尚未结算。拥有优先权时可以打出反应牌。";
            hint = assigning ? "双方完成分配 → 同时造成战斗伤害。" : "双方连续让过 → 进入伤害分配或战斗结算。";
        }
        if (phase == "TURN_START")
        {
            var choice = Object(timing, "pendingCardChoice");
            var payment = Object(timing, "pendingPayment");
            if (choice.ValueKind == JsonValueKind.Object || payment.ValueKind == JsonValueKind.Object)
            {
                var pending = choice.ValueKind == JsonValueKind.Object ? choice : payment;
                title = payment.ValueKind == JsonValueKind.Object ? "回合开始 · 效果支付" : "回合开始 · 效果选择";
                actorLine = $"{Side(Text(pending, "playerId"))}完成选择";
                detail = "按服务端提示完成当前效果；可选效果可以放弃。";
                hint = "完成后继续开始阶段，尚未进入行动阶段。";
            }
        }
        var replacement = Object(timing, "pendingPayment");
        if (Text(replacement, "paymentWindow") == "RULE_REPLACEMENT")
        {
            var spellChoices = Text(replacement, "choiceKind") == "SPELL_CHOICE_REMAKE";
            var counterMana = Text(replacement, "choiceKind") == "COUNTER_TARGET_MANA_PAYMENT";
            var tokenHaste = Text(replacement, "choiceKind") == "TOKEN_HASTE_PAYMENT";
            var observedBanish = Text(replacement, "choiceKind") == "DECK_OBSERVATION_BANISH";
            var createEmpower = Text(replacement, "choiceKind") == "DECK_REFLEXIVE_CREATE";
            var publicObservation = observedBanish && Text(replacement, "deckObservationKind") == "REVEAL";
            var movement = Text(replacement, "choiceKind") == "MOVEMENT_SELECTION";
            var groupedTargets = Text(replacement, "choiceKind") == "TARGET_GROUP";
            var firstDeath = Text(replacement, "choiceKind") == "FIRST_DEATH_OCCURRENCE";
            title = spellChoices ? "法术结算 · 重新选择" : counterMana ? "法术结算 · 付款选择" : tokenHaste ? "指示物急速 · 支付选择" : createEmpower ? "内嵌强化 · 创建选择": observedBanish ? publicObservation ? "展示牌库 · 放逐选择" : "查看牌库 · 放逐选择" : movement ? "法术结算 · 选择移动单位" : groupedTargets ? "法术结算 · 重选目标" : firstDeath ? "首次死亡 · 选择事件" : "摧毁替换 · 等待选择";
            actorLine = $"{Side(Text(replacement, "playerId"))}完成选择";
            detail = spellChoices ? "按服务端提示保留原选择，或重新选择后确认；重新选择时，保留的选择也必须合法。" : counterMana ? "按服务端提示支付法力以保留目标法术；也可先产费，或不支付并尝试无效化。" : tokenHaste ? "在单位进场前，按服务端提示选择支付、放弃或先产费；确认后继续打出该单位。" : createEmpower ? "决定是否创建独立的强化技能；原法术完成并确认待处理项目后，该技能才可等待响应与结算。": observedBanish ? (publicObservation ? "这张牌已向双方展示。" : "") + "选择是否放逐这张牌；原效果完成后，可按提示支付符能打出。" : movement ? "选择你控制的一名合法单位，移动至效果指定的战场。" : groupedTargets ? "从原目标中移除单位，直到剩余目标满足要求，再确认结算。不能新增目标。" : firstDeath ? "选择同时被摧毁的一名单位，作为本回合首次死亡技能的触发事件。"
                : "选择先执行的替换及对象；可选费用可先产费，强制替换不能放弃。";
            hint = "确认后继续原结算；选择期间不能插入其他行动。";
            activeWindow = true;
        }
        return new Godot.Collections.Dictionary {
            ["title"] = title, ["actor"] = actorLine, ["detail"] = detail, ["hint"] = hint,
            ["battlefield"] = field.Length > 0 ? name(field) : "",
            ["activeWindow"] = activeWindow, ["stackCount"] = stackCount,
            ["timingState"] = Text(timing, "timingState")
        };
    }

    private static JsonElement Object(JsonElement value, string key) => value.ValueKind == JsonValueKind.Object
        && value.TryGetProperty(key, out var child) ? child : default;
    private static string Text(JsonElement value, string key) => Object(value, key) is var child && child.ValueKind == JsonValueKind.String ? child.GetString() ?? "" : "";
    private static bool Bool(JsonElement value, string key) => Object(value, key).ValueKind == JsonValueKind.True;
    private static int Count(JsonElement value, string key) => Object(value, key) is var child && child.ValueKind == JsonValueKind.Array ? child.GetArrayLength() : 0;
}
