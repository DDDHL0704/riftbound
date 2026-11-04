using Riftbound.Contracts;

namespace Riftbound.Engine;

internal static partial class ActionPromptBuilder
{
    private static IEnumerable<ActivateAbilityPromptRequirement> BorrowedExhaustRequirements(
        MatchState state, string player, CardObjectState source)
    {
        var pool = state.RunePools.GetValueOrDefault(player, RunePool.Empty);
        var powerPool = PaymentCostRules.PowerPoolForPurpose(pool, CoreRuleEngine.BorrowedPowerPurpose(source));
        foreach (var ability in BorrowedExhaustRules.Abilities(state, player, source))
        {
            if (!CoreRuleEngine.CanActivateBorrowedExhaust(state, player, ability.Spec)) continue;
            if (ability.Spec.Kind == "RESOURCE" && !CoreRuleEngine.BorrowedResourceGainSupported(state, player, ability.Spec, source)) continue;
            var discardsHandCard = ability.Spec.DiscardsEquipmentAsCost || ability.Spec.DiscardsHandCardAsCost;
            var discardChoices = discardsHandCard
                ? CoreRuleEngine.SimpleActivationDiscardChoices(state, player, ability.Spec.DiscardsHandCardAsCost).Select(id =>
                    new ActionPromptChoiceDto(id, "弃置" + ObjectChoice(state, id["DISCARD_HAND_CARD:".Length..], "费用").Label,
                        "弃置手牌以支付获得的技能费用")).ToArray() : [];
            if (discardsHandCard && discardChoices.Length == 0) continue;
            var banishChoices = ability.Spec.BanishesGraveyardPermanentAsCost
                ? LegendGraveyardBanishCostRules.Choices(state, player).Select(id => new ActionPromptChoiceDto(id,
                    "放逐" + ObjectChoice(state, id[LegendGraveyardBanishCostRules.Prefix.Length..], "费用").Label,
                    "放逐废牌堆中的单位或装备以支付技能费用")).ToArray() : [];
            if (ability.Spec.BanishesGraveyardPermanentAsCost && banishChoices.Length == 0) continue;
            var recycleChoices = ability.Spec.RecycledCardCostCount == 0 ? [] : state.CardObjects.Keys
                .Where(id => CoreRuleEngine.CanRecycleBorrowedAbilityCost(state, player, id, ability.Spec))
                .Order(StringComparer.Ordinal).Select(id => new ActionPromptChoiceDto("RECYCLE_GRAVEYARD_CARD:" + id,
                    "回收" + ObjectChoice(state, id, "费用").Label, "回收废牌堆中的牌以支付技能费用")).ToArray();
            if (recycleChoices.Length < ability.Spec.RecycledCardCostCount) continue;
            var unitExhaustChoices = CoreRuleEngine.BorrowedUnitExhaustCostChoices(state, player, source.ObjectId, ability.Spec)
                .Select(id => new ActionPromptChoiceDto(id,
                    "休眠" + ObjectChoice(state, id[CoreRuleEngine.ExhaustFriendlyUnitOptionalCostPrefix.Length..], "费用").Label,
                    "让友方强力单位休眠以支付技能费用")).ToArray();
            if (ability.Spec.ExhaustedUnitMinimumMightAsCost.HasValue && unitExhaustChoices.Length == 0) continue;
            var destructionChoices = CoreRuleEngine.BorrowedDestructionCostChoices(state, player, ability.Spec)
                .Where(id => ability.Spec.Kind != "RESOURCE" || CoreRuleEngine.CanStartDestructionResourceGain(state, player,
                    source.ObjectId, id[CoreRuleEngine.DestroyFriendlyPermanentAdditionalCostPrefix.Length..],
                    ability.Spec.GeneratedPower + ability.Spec.GeneratedTraits.Values.Sum(), BorrowedExhaustRules.Effect))
                .Select(id => new ActionPromptChoiceDto(id,
                    "摧毁" + ObjectChoice(state, id[CoreRuleEngine.DestroyFriendlyPermanentAdditionalCostPrefix.Length..], "费用").Label,
                    "摧毁友方物体以支付技能费用")).ToArray();
            if (ability.Spec.DestroyedFriendlyCardTypeAsCost.Length > 0 && destructionChoices.Length == 0) continue;
            IReadOnlyList<ActionPromptChoiceDto> optional = [];
            if (ability.Spec.Conversion.Length > 0)
            {
                if (!CoreRuleEngine.TryBorrowedConversionMaximum(state, player, source, ability.Spec, out var maximum)) continue;
                optional = ResourceConversionChoices(BorrowedExhaustRules.ConversionChoicePrefix, maximum, "选择资源转换数量");
            }
            var choices = Enumerable.Range(0, ability.Spec.Targets).ToDictionary(i => i.ToString(),
                i => (IReadOnlyList<ActionPromptChoiceDto>)(BorrowedExhaustRules.TargetsPlayer(ability.Spec)
                    ? state.Seats.Keys : state.CardObjects.Keys).Order(StringComparer.Ordinal)
                    .Where(id => CoreRuleEngine.IsBorrowedExhaustTarget(state, player, id, ability.Spec, source.ObjectId, i))
                    .Select(id => BorrowedExhaustRules.TargetsPlayer(ability.Spec)
                        ? new ActionPromptChoiceDto(id, id, "所选玩家") : ObjectChoice(state, id,
                            ability.Spec.Kind is "ATTACH_ARMAMENT" or "REATTACH_ARMAMENT"
                                ? i == 0 ? "单位" : "武装" : "技能目标")).ToArray());
            var targets = choices.Values.SelectMany(c => c).DistinctBy(c => c.Id).ToArray();
            if (choices.Values.Any(c => c.Count == 0) || targets.Length < ability.Spec.Targets) continue;
            var effect = ability.Spec.Kind switch {
                "ATTACH_ARMAMENT" => "将你控制的一件未贴附的武装贴附到你控制的一名单位上",
                "REATTACH_ARMAMENT" => "将你控制的一件已贴附的武装贴附到你控制的一名单位上",
                "RETURN_UNIT_FREE_PLAY" => "让一名友方单位返回手牌，然后可免费打出战力更低的单位",
                "RETURN_TO_HAND" => ability.Spec.TargetScope == "OWNED_TEEMO_UNIT"
                    ? "将你拥有的英雄区域或场上提莫单位放入手牌" : "让另一张友方装备、单位或正面朝下的卡牌返回手牌",
                "RETURN_AND_CREATE_GOLD" => "让战场上的一名友方单位返回手牌，然后打出一个休眠的金币装备指示物",
                "READY_EQUIPMENT" => $"让 {ability.Spec.Targets} 件装备活跃",
                "READY_UNIT" => "让一名单位活跃",
                "GRANT_NEXT_SPELL_ECHO" => "本回合打出的下一个法术获得等同于其费用的回响",
                "GRANT_NEXT_SPELL_DAMAGE" => "本回合打出的下一个法术每段伤害+1",
                "GRANT_NEXT_UNIT_READY" => "鼓舞时，本回合打出的下一名单位以活跃状态进场",
                "TRANSFER_SOURCE_AND_RECALL" => "选择一名玩家，令其获得此牌控制权并召回",
                "COMMAND_OPPONENT_MOVEMENT" => "选择一名对手，让其将一名单位移动到此牌所在战场",
                "GUESS_DECK_TOP" => "选择一名对手猜测牌库顶类型，展示后回收或抽取",
                "ROAM_UNIT" => "让一名单位本回合内获得游走",
                "MOVE_TO_BASE" => "将战场上的一名" + (ability.Spec.TargetScope == "EXHAUSTED_FRIENDLY_BATTLEFIELD_UNIT" ? "休眠的" : "")
                    + "友方单位移动到基地",
                "MOVE_BETWEEN_BASE_AND_BATTLEFIELD" => "在战场和基地之间移动一名友方单位",
                "MOVE_DUEL_UNIT_TO_BASE" => "如果这是你的回合，将一名法术对决中的友方单位移动到基地；此牌已强化时让其活跃",
                "CREATE_BATTLEFIELD_EQUIPMENT_TOKEN" => $"将 {ability.Spec.TokenCount} 个{ability.Spec.TokenName}装备指示物打出到一处战场",
                "BOON_UNIT" => "给予一名单位增益",
                "BOON_SELF" => "给予我增益",
                "BOON_OR_MIGHT" => $"给予一名单位增益；已有增益时改为本回合战力 +{ability.Spec.ExistingBoonMight}",
                "EMPOWER_EQUIPMENT" => "强化另一件装备",
                "KEYWORD_UNIT" => $"给予一名{(ability.Spec.TargetScope == "FRIENDLY_UNIT" ? "友方" : "")}单位本回合内{ability.Spec.Keyword}",
                "DAMAGE_UNIT" => ability.Spec.DamageFromSourceMight ? "对任意战场中的一名敌方单位造成等同于我战力的伤害"
                    : $"对一名单位造成 {ability.Spec.Damage} 点伤害",
                "STUN_ENEMY" => "眩晕一名敌方单位",
                "DRAW" => $"抽 {ability.Spec.DrawCount} 张牌"
                    + (ability.Spec.RequiredEnemyTargetEvents > 0 ? "；本回合须已两次将敌方单位或装备选为法术或单位技能目标" : "")
                    + (ability.Spec.RuneCallCount > 0 ? $"并召出 {ability.Spec.RuneCallCount} 枚休眠符文" : ""),
                "DISCARD_DRAW" => $"弃置一张手牌，然后抽 {ability.Spec.DrawCount} 张牌",
                "DESTROY_SELF" => "摧毁此牌",
                "CREATE_GOLD" => "打出一个休眠的金币装备指示物",
                "CREATE_UNIT_TOKEN" => $"打出 {ability.Spec.TokenCount} 名{(ability.Spec.TokensEnterReady ? "处于活跃状态的" : " ")}{ability.Spec.TokenMight} 战力的{ability.Spec.TokenName}"
                    + (ability.Spec.TokenKeyword.Length > 0 ? $"，拥有{ability.Spec.TokenKeyword}" : "")
                    + (ability.Spec.TokensToBase ? "到你的基地" : "")
                    + (ability.Spec.RequiresPlayedArmamentThisTurn ? "；本回合须已打出武装" : ""),
                "SCORE" => $"获得 {ability.Spec.ScoreAmount} 分",
                "INSIGHT_DRAW_EXPERIENCE" => $"洞察 {ability.Spec.InsightCount}，然后抽 {ability.Spec.DrawCount} 张牌并获得 {ability.Spec.ExperienceGain} 经验",
                "RESOURCE" => BorrowedResourceLabel(ability.Spec, source.IsEmpowered),
                _ => $"让一名单位在本回合内战力 {ability.Spec.Might:+0;-0}"
                    + (ability.Spec.EmpoweredMight is { } stronger ? $"；此牌已强化时改为 +{stronger}" : "") };
            var taxes = targets.ToDictionary(t => t.Id, t => BorrowedExhaustRules.TargetsPlayer(ability.Spec) ? 0 : CoreRuleEngine.SpellshieldPowerCostForTarget(state, player, t.Id), StringComparer.Ordinal);
            var payments = ability.Spec.Power + ability.Spec.TypedPower.Values.Sum() > 0 || taxes.Values.Any(tax => tax > 0)
                ? RenektonAwareRecycleRuneChoices(state, player, "支付获得的技能费用").ToArray() : [];
            var byTrait = ActivateAbilityPaymentResourcePowerByTrait(state, player, payments);
            yield return new(source.ObjectId, AttachedRuleTextRules.Face(source)!, ability.Id, "获得的横置技能",
                $"{(ability.Spec.Reaction ? "反应；" : ability.Spec.Swift ? "迅捷；" : "")}{(ability.Spec.RemovesSourceEmpowerment ? "解除我的强化；" : "")}{(ability.Spec.DiscardsEquipmentAsCost ? "弃置一张装备牌；" : ability.Spec.DiscardsHandCardAsCost ? "弃置一张手牌；" : "")}{(ability.Spec.BanishesGraveyardPermanentAsCost ? "放逐自己废牌堆中的一个单位或装备；" : "")}{(ability.Spec.DestroysSourceAsCost ? "摧毁此牌；" : "")}{(ability.Spec.RecycledCardCostCount > 0 ? $"回收 {ability.Spec.RecycledCardCostCount} 张{(ability.Spec.RecyclesOnlyUnitsAsCost ? "单位" : "")}牌；" : "")}{(ability.Spec.ExhaustedUnitMinimumMightAsCost.HasValue ? "休眠一名友方强力单位；" : "")}{(ability.Spec.DestroyedFriendlyCardTypeAsCost.Length > 0 ? ability.Spec.DestroyedFriendlyCardTypeAsCost switch { "UNIT" => "摧毁一名友方单位；", "EQUIPMENT" => "摧毁一件友方装备；", _ => "摧毁一个友方单位或装备；" } : "")}横置：{effect}",
                CoreRuleEngine.BorrowedExhaustManaCost(state, player, ability.Spec), ability.Spec.Power, ability.Spec.Experience,
                ability.Spec.TypedPower, ability.Spec.Targets, ability.Spec.Targets,
                "技能目标", choices, optional, payments, ActivateAbilityPaymentResourcePowerByChoice(state, player, payments),
                powerPool.TotalPower, PlayCardAvailablePowerByTrait(powerPool, new Dictionary<string, int>()),
                new RunePaymentReachability(powerPool, byTrait).MaximumTotal, PlayCardAvailablePowerByTrait(powerPool, byTrait),
                [], true, ability.Spec.Kind == "RESOURCE", true, null, SpellshieldTaxPowerByTargetObjectId: taxes,
                AdditionalCostChoices: discardChoices.Concat(banishChoices).Concat(recycleChoices).Concat(unitExhaustChoices).Concat(destructionChoices).ToArray(),
                RequiredAdditionalCostChoiceCount: (discardsHandCard ? 1 : 0) + ability.Spec.RecycledCardCostCount
                    + (ability.Spec.ExhaustedUnitMinimumMightAsCost.HasValue ? 1 : 0)
                    + (ability.Spec.DestroyedFriendlyCardTypeAsCost.Length > 0 ? 1 : 0)
                    + (ability.Spec.BanishesGraveyardPermanentAsCost ? 1 : 0));
        }
    }

    private static string BorrowedResourceLabel(Riftbound.CardCatalog.BorrowedExhaustSpec spec, bool empowered)
    {
        if (spec.Conversion.Length > 0) return "转换费用资源：" + (spec.Conversion == "MANA_TO_POWER" ? "支付法力获得等量任意符能" : "支付任意特性符能获得等量法力");
        var gains = new List<string>();
        var mana = empowered ? spec.EmpoweredMana ?? spec.GeneratedMana : spec.GeneratedMana;
        if (mana > 0) gains.Add($"{mana} 点法力");
        if (spec.GeneratedPower > 0) gains.Add($"{spec.GeneratedPower} 点任意符能");
        gains.AddRange(spec.GeneratedTraits.Select(p => $"{p.Value} 点{RuneTraitLabel(p.Key)}符能"));
        var restriction = spec.ResourceRestriction switch {
            "SPELL" => "；仅可用于打出法术", "UNIT" => "；仅可用于打出单位或使用单位技能",
            "EQUIPMENT" => "；仅可用于打出装备或使用装备技能", "DUEL" => "；仅可在法术对决中消耗",
            "PERMANENT_PLAY" => "；仅可用于打出单位或装备", _ => "" };
        return "获得费用资源：" + (spec.EffectRequiresEncouraged ? "鼓舞时" : "") + string.Join("和", gains) + restriction;
    }
}
