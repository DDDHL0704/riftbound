import copy
from pathlib import Path
import tempfile
import unittest

from test_effect_clause_audit import audit


class LegendFieldClauseAuditTests(unittest.TestCase):
    TEXT = {
        "move": "支付{{2}}，{{横置}}：在战场和其所属基地之间移动一名友方单位。",
        "dormant": "消耗2经验，{{横置}}：将战场上一名处于休眠状态的友方单位移动到其基地。",
        "return": "支付{{1}}，{{横置}}：让战场上的一名友方单位返回其所属的手牌。"
                  "打出一个休眠的“金币”装备指示物。（其具有“{{反应>}} 摧毁此牌，{{横置}}：{{获得}}{{A}}。”）",
        "owned": "支付{{1}}，{{横置}}：将你拥有的一张位于英雄区域或场上的提莫单位牌放入你的手牌。",
        "draw": "消耗3经验，{{横置}}：抽一张牌。",
    }

    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.engine = self.root / "src/Riftbound.Engine"
        self.engine.mkdir(parents=True)
        (self.engine / "CoreRuleEngine.cs").write_text(
            "if (IsLegendFieldEffect(ability.EffectKind)) QueueLegendFieldEffect();\n"
            "DeclareLegendFieldDestination();\n"
            "if (IsLegendDiscardDrawEffect(ability.EffectKind)) QueueLegendDiscardDraw();")
        (self.engine / "CoreRuleEngine.LegendFieldEffect.cs").write_text(
            "TryMoveFieldUnitByEffect(); SameOrigin();\n"
            "ResolveUnitReturnInstructions(state, item, LegalTarget, ability.RequiresOwnedTeemoUnitTarget,\n"
            "ability.EffectKind == LegendAbilityEffectKinds.ReturnBattlefieldUnitAndCreateCoin);")
        (self.engine / "CoreRuleEngine.ReturnSkills.cs").write_text(
            "TryReturnTargetToHand(); if (createsGold) CreateBaseEquipmentTokens();")
        (self.engine / "CoreRuleEngine.LegendDiscardDraw.cs").write_text("ResolveDiscardDrawStackItem();")

    def card(self, family):
        mana = 2 if family == "move" else 1 if family in ("return", "owned") else 0
        experience = 3 if family == "draw" else 2 if family == "dormant" else 0
        definition = dict(abilityId="ABILITY", effectKind={
            "move": "MOVE_FRIENDLY_UNIT", "dormant": "MOVE_FRIENDLY_UNIT",
            "return": "RETURN_BATTLEFIELD_UNIT_AND_CREATE_COIN", "owned": "RETURN_OWNED_TEEMO_UNIT_TO_HAND",
            "draw": "DRAW_ONE"}[family], manaCost=mana, experienceCost=experience,
            requiredCostToken=f"SPEND_EXPERIENCE:{experience}" if experience else f"SPEND_MANA:{mana}",
            exhaustsSourceAsCost=True, requiredTargetCount=0 if family == "draw" else 1,
            requiresFriendlyUnitTarget=family in ("move", "dormant", "return"),
            requiresBattlefieldTarget=family in ("dormant", "return"), requiresExhaustedTarget=family == "dormant",
            requiresOwnedTeemoUnitTarget=family == "owned", timingKind="MAIN_OPEN",
            flow="DISCARD_DRAW" if family == "draw" else "FIELD_EFFECT")
        return dict(cardNo="TEST", cardName="测试传奇", cardCategory="legendary", cardEffect=self.TEXT[family],
                    legendActivationDefinitions=[definition], legendActivations=["ABILITY"])

    def outcome(self, card, index=0):
        return audit.audit_clause(card, audit.split_clauses(card["cardEffect"])[index], audit.Sources(self.root, []))

    def test_all_five_shapes_require_real_shared_consumers(self):
        for family in self.TEXT:
            with self.subTest(family=family):
                self.assertEqual("route", self.outcome(self.card(family))["status"])

    def test_instant_old_legend_branches_are_missing(self):
        for family in self.TEXT:
            card = self.card(family)
            card["legendActivationDefinitions"][0]["flow"] = "IMMEDIATE"
            with self.subTest(family=family):
                self.assertEqual("missing", self.outcome(card)["status"])

    def test_unknown_alternative_stack_routes_remain_unconfirmed(self):
        card = self.card("move"); card["legendActivationDefinitions"][0]["flow"] = "OTHER_STACK"
        self.assertEqual("review", self.outcome(card)["status"])

    def test_exact_mana_experience_power_exhaustion_and_required_token_are_checked(self):
        for family in self.TEXT:
            for key, value in [("manaCost", 9), ("experienceCost", 9), ("powerCost", 1),
                               ("powerCostByTrait", {"red": 1}), ("exhaustsSourceAsCost", False),
                               ("requiredCostToken", "WRONG_COST")]:
                card = self.card(family); card["legendActivationDefinitions"][0][key] = value
                with self.subTest(family=family, key=key):
                    self.assertEqual("missing", self.outcome(card)["status"])

    def test_target_count_ownership_location_exhaustion_and_timing_are_checked(self):
        for family in self.TEXT:
            original = self.card(family)
            for key in ["requiresFriendlyUnitTarget", "requiresBattlefieldTarget", "requiresExhaustedTarget",
                        "requiresOwnedTeemoUnitTarget", "requiresAnyUnitTarget"]:
                card = copy.deepcopy(original); definition = card["legendActivationDefinitions"][0]
                definition[key] = not definition.get(key, False)
                with self.subTest(family=family, key=key):
                    self.assertEqual("missing", self.outcome(card)["status"])
            for key, value in [("requiredTargetCount", 3), ("timingKind", "REACTION_WINDOW")]:
                card = copy.deepcopy(original); card["legendActivationDefinitions"][0][key] = value
                self.assertEqual("missing", self.outcome(card)["status"])

    def test_gold_continuation_uses_the_parent_cost_not_its_reminder_colon(self):
        card = self.card("return")
        self.assertEqual(2, len(audit.split_clauses(card["cardEffect"])))
        self.assertEqual("route", self.outcome(card, 1)["status"])
        card["legendActivationDefinitions"][0]["manaCost"] = 0
        self.assertEqual("missing", self.outcome(card, 1)["status"])

    def test_a_return_consumer_cannot_certify_the_separate_coin_instruction(self):
        card = self.card("return")
        (self.engine / "CoreRuleEngine.ReturnSkills.cs").write_text("TryReturnTargetToHand();")
        self.assertEqual("route", self.outcome(card)["status"])
        self.assertEqual("review", self.outcome(card, 1)["status"])

    def test_a_coin_helper_without_the_actual_legend_flag_cannot_certify_the_coin(self):
        path = self.engine / "CoreRuleEngine.LegendFieldEffect.cs"
        path.write_text(path.read_text().replace(
            "ability.EffectKind == LegendAbilityEffectKinds.ReturnBattlefieldUnitAndCreateCoin", "false"))
        self.assertEqual("review", self.outcome(self.card("return"), 1)["status"])

    def test_catalog_registration_and_a_named_resolver_do_not_prove_dispatch(self):
        (self.engine / "CoreRuleEngine.cs").write_text("void QueueLegendFieldEffect() {}")
        for family in self.TEXT:
            with self.subTest(family=family):
                self.assertEqual("review", self.outcome(self.card(family))["status"])

    def test_existing_generic_draw_consumer_does_not_certify_the_ordinary_draw_exception(self):
        path = self.engine / "CoreRuleEngine.cs"
        path.write_text(path.read_text()+"\n&& !(ability.EffectKind == LegendAbilityEffectKinds.DrawOne")
        self.assertEqual("missing", self.outcome(self.card("draw"))["status"])

    def test_matching_legend_ids_without_clause_specs_are_not_evidence(self):
        card = self.card("owned"); card["legendActivationDefinitions"] = []
        self.assertEqual("missing", self.outcome(card)["status"])

    def test_other_legend_draw_timings_and_unit_skills_are_not_mapped_to_this_family(self):
        card = self.card("draw"); card["cardEffect"] = "{{反应}}\n{{横置}}：抽一张牌。"
        clause = audit.split_clauses(card["cardEffect"])[-1]
        self.assertIsNone(audit.legend_field_activation_route(card, clause, audit.Sources(self.root, [])))
        card = self.card("move"); card["cardCategory"] = "unit"
        self.assertIsNone(audit.legend_field_activation_route(card, audit.split_clauses(card["cardEffect"])[0],
                                                            audit.Sources(self.root, [])))
