import copy
import importlib.util
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[2]
SPEC = importlib.util.spec_from_file_location("battle_win_paid_audit", ROOT / "scripts/audit-card-effects.py")
audit = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(audit)
GOLD = "当我赢得战斗时，打出一个休眠的“金币”装备指示物。"
PAID = "当我进攻或防守时，你可以选择支付{{红色}}，以此让我在本回合内{{S}}+2。"


class BattleWinPaidCombatClauseAuditTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.sources = audit.Sources(ROOT, [])

    @staticmethod
    def card(no="SFD·020/221"):
        node = dict(kind="UNIT_BATTLE_WIN_CREATE_DORMANT_GOLD", timing="UNIT_BATTLE_WON", text=GOLD,
                    targetScope="SOURCE_UNIT", createdTokenCount=1, createdTokenName="金币",
                    createdTokenDestination="BASE", createdTokenExhausted=True)
        return dict(cardNo=no, cardName="德莱文", cardCategory="hero_unit", cardEffect=GOLD+"\n"+PAID,
                    behaviors=[dict(playsSourceToBaseAsUnit=True)],
                    unitEventAbility=dict(timing="ATTACK_OR_DEFEND", effect="MIGHT_SELF", amount=2,
                                          leadingPowerCost=1, leadingPowerTrait="red", optional=True),
                    triggers=[dict(node=node, predicates=["UnitBattleWinTriggerSpecRules.IsUnitBattleWinDormantGoldTrigger"])])

    @staticmethod
    def clause(card, paid=False):
        return next(row for row in audit.card_clauses(card) if ("支付" if paid else "赢得") in row["text"])

    def status(self, card=None, paid=False, sources=None):
        card = card or self.card()
        return audit.audit_clause(card, self.clause(card, paid), sources or self.sources)["status"]

    def changed(self, file, token):
        sources = copy.copy(self.sources)
        sources.files = dict(self.sources.files)
        path = "src/Riftbound.Engine/"+file
        self.assertIn(token, sources.files[path])
        sources.files[path] = sources.files[path].replace(token, "REMOVED_ACTUAL_CONSUMER")
        sources.files[path] += "\n    private static void UnusedAuditDecoy() { "+token+" }\n"
        return sources

    @staticmethod
    def draw_card(no, text):
        return dict(cardNo=no, cardName="战斗获胜抽牌", cardCategory="unit", cardEffect=text,
                    behaviors=[dict(playsSourceToBaseAsUnit=True)], triggers=[dict(node=dict(
                        kind="UNIT_BATTLE_WIN_DRAW", timing="UNIT_BATTLE_WON", targetScope="SOURCE_UNIT",
                        drawCount=1, text=text), predicates=["UnitBattleWinTriggerSpecRules.IsUnitBattleWinDrawTrigger"])])

    def test_both_printings_match_each_exact_executable_clause(self):
        for no in ["SFD·020/221", "SFD·020a/221"]:
            with self.subTest(no=no):
                self.assertEqual("route", self.status(self.card(no)))
                self.assertEqual("route", self.status(self.card(no), paid=True))

    def test_the_two_existing_draw_wordings_use_the_same_participant_reward_pipeline(self):
        for no,text in [("UNL-114/219", "当我赢得一场战斗时，抽一张牌。（如果我在战斗后仍然存活，则我赢得胜利。）"),
                        ("SFD·123/221", "每当我赢得战斗时，抽一张牌。")]:
            with self.subTest(no=no):
                self.assertEqual("route", self.status(self.draw_card(no,text)))

    def test_a_vanilla_registration_or_an_unbound_runtime_predicate_is_missing(self):
        card = self.card(); card["triggers"] = []
        self.assertEqual("missing", self.status(card))
        card = self.card(); card["unitEventAbility"] = None
        self.assertEqual("missing", self.status(card,paid=True))
        card = self.card(); card["triggers"][0]["predicates"] = []
        self.assertEqual("missing", self.status(card))

    def test_gold_count_kind_timing_destination_and_readiness_cannot_drift(self):
        for key,value in [("kind","UNIT_CONQUEST_CREATE_DORMANT_GOLD"), ("timing","UNIT_CONQUEST"),
                          ("targetScope","CONTROLLED_UNITS_AT_THIS_BATTLEFIELD"), ("createdTokenCount",2),
                          ("createdTokenName","映像"), ("createdTokenDestination","BATTLEFIELD"),
                          ("createdTokenExhausted",False), ("drawCount",1), ("optional",True)]:
            with self.subTest(key=key):
                card=self.card(); card["triggers"][0]["node"][key]=value
                self.assertEqual("missing",self.status(card))

    def test_paid_combat_requires_both_roles_exact_might_red_cost_and_optionality(self):
        for key,value in [("timing","ATTACK"), ("effect","MIGHT_FRIENDLY"), ("amount",1),
                          ("leadingPowerCost",0), ("leadingPowerTrait","blue"), ("optional",False),
                          ("requiresEmpowered",True), ("independentExperienceGain",2)]:
            with self.subTest(key=key):
                card=self.card(); card["unitEventAbility"][key]=value
                self.assertEqual("missing",self.status(card,paid=True))

    def test_a_spell_source_or_a_different_condition_cannot_use_the_unit_pipeline(self):
        card=self.card(); card["cardCategory"]="spell"
        self.assertEqual("missing",self.status(card)); self.assertEqual("missing",self.status(card,paid=True))
        card=self.card()
        for paid in [False,True]:
            row=self.clause(card,paid); row["scope"]="如果我拥有增益，"+row["scope"]
            self.assertEqual("missing",audit.audit_clause(card,row,self.sources)["status"])

    def test_unused_capture_helpers_cannot_replace_the_actual_result_or_participant_source(self):
        for file,token in [
            ("CoreRuleEngine.BattleResultContinuation.cs","CaptureUnitBattleWinDrawObservations(resultState, battlefield, winner"),
            ("CoreRuleEngine.BattleWinTriggers.cs","card.ControllerId != winnerPlayerId"),
            ("CoreRuleEngine.BattleWinTriggers.cs",'location.Zone != "BATTLEFIELD" || location.BattlefieldObjectId != battlefieldId'),
            ("CoreRuleEngine.BattleWinTriggers.cs","AttachedRuleTextRules.Instances(state, card)"),
            ("CoreRuleEngine.BattleWinTriggers.cs","UnitBattleWinTriggerSpecRules.Rewards(instance.CardNo)"),
        ]:
            with self.subTest(token=token):self.assertEqual("missing",self.status(sources=self.changed(file,token)))

    def test_unused_token_creation_code_does_not_replace_the_real_gold_state_write(self):
        for file,token in [
            ("CoreRuleEngine.BattleWinTriggers.cs","if (context.GoldCount > 0)"),
            ("CoreRuleEngine.BattleWinTriggers.cs","CreateLegendEquipmentToken(zones, cards, context.ControllerId"),
            ("CoreRuleEngine.cs","Base = zones.Base.Concat([tokenObjectId]).ToArray()"),
            ("CoreRuleEngine.cs","CaptureEquipmentPlayed("),
            ("CoreRuleEngine.cs",'["sourceObjectId"] = publicSourceObjectId ?? sourceObjectId'),
        ]:
            with self.subTest(token=token):self.assertEqual("missing",self.status(sources=self.changed(file,token)))

    def test_unused_payment_code_cannot_replace_confirmation_dispatch_commit_or_receipt(self):
        for file,token in [
            ("CoreRuleEngine.FieldTriggers.cs","NeedsTriggerCostConfirmation(i)"),
            ("CoreRuleEngine.FieldTriggers.cs","if (NeedsTriggerCostConfirmation(item)) return PrepareTriggerCostConfirmation(result, item)"),
            ("CoreRuleEngine.TriggerCosts.cs","PowerTrait: eventSpec.LeadingPowerTrait"),
            ("CoreRuleEngine.cs","return ResolveTriggerCostPayment(state, intent, pendingPayment, submittedChoices, paymentChoiceIds.Count)"),
            ("CoreRuleEngine.TriggerCosts.cs","PaymentCostRules.TryCommitPayment("),
            ("CoreRuleEngine.TriggerCosts.cs","receipt.Power == cost.Power"),
        ]:
            with self.subTest(token=token):self.assertEqual("missing",self.status(paid=True,sources=self.changed(file,token)))

    def test_unused_might_writes_cannot_replace_same_generation_field_checks_or_turn_end_expiry(self):
        for file,token in [
            ("CoreRuleEngine.UnitEvents.cs","source.ObjectGeneration == context.SourceGeneration"),
            ("CoreRuleEngine.UnitEvents.cs","IsObjectOnField(zones, source.ObjectId)"),
            ("CoreRuleEngine.cs","Power = resultingPower"),
            ("CoreRuleEngine.cs","UntilEndOfTurnPowerModifiers = powerModifierLedger"),
            ("CoreRuleEngine.cs","Power = objectState.Power - objectState.UntilEndOfTurnPowerModifier"),
            ("CoreRuleEngine.cs","UntilEndOfTurnPowerModifiers = []"),
        ]:
            with self.subTest(token=token):self.assertEqual("missing",self.status(paid=True,sources=self.changed(file,token)))

    def test_the_real_original_result_history_entry_and_reward_shape_are_required(self):
        for file,token in [
            ("MatchRecovery.cs","BattleResultRecoveryRules.ValidateOriginalTriggers(recovery)"),
            ("BattleResultRecoveryRules.cs",'e.Event.Kind == "TRIGGER_QUEUED"'),
            ("BattleResultRecoveryRules.cs",'"battleWinContext"'),
            ("CoreRuleEngine.BattleWinTriggers.cs","context.GoldCount == spec.CreatedTokenCount.GetValueOrDefault()"),
        ]:
            with self.subTest(token=token):self.assertEqual("missing",self.status(sources=self.changed(file,token)))

    def test_legacy_draws_cannot_fall_back_to_an_unused_reader_when_the_real_hand_write_is_removed(self):
        card=self.draw_card("SFD·123/221","每当我赢得战斗时，抽一张牌。")
        sources=self.changed("CoreRuleEngine.cs","Hand = drawPlayerZones.Hand.Concat(drawResult.DrawnCards).ToArray()")
        self.assertEqual("missing",self.status(card,sources=sources))

    def test_ceased_source_recovery_requires_the_real_validated_spectator_consumer(self):
        sources=self.changed("MatchRecovery.cs","BattleResultRecoveryRules.IsCapturedSkill(authoritativeState, drawSkill)")
        self.assertEqual("missing",self.status(sources=sources))
        self.assertEqual("missing",self.status(self.draw_card("SFD·123/221","每当我赢得战斗时，抽一张牌。"),sources=sources))


if __name__ == "__main__":
    unittest.main()
