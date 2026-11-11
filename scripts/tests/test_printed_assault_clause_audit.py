import importlib.util
from pathlib import Path
import tempfile
import unittest

SPEC = importlib.util.spec_from_file_location('assault_effect_audit', Path(__file__).parents[1] / 'audit-card-effects.py')
audit = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(audit)


class PrintedAssaultClauseAuditTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.engine = self.root / 'src/Riftbound.Engine'
        self.engine.mkdir(parents=True)
        self.application = ('TemporaryCombatKeywordRules.AppliedEffectId(card, status); '
                            'isAttacking ? CardCombatKeywordNames.Assault : CardCombatKeywordNames.Steadfast;')
        (self.engine / 'CoreRuleEngine.cs').write_text(self.application)
        (self.engine / 'CoreRuleEngine.CountedCombatKeywords.cs').write_text('TemporaryCombatKeywordRules.Amount(unit, keyword);')

    def card(self, amount=3, roam=False):
        text = '{{迅捷}}\n让一名单位本回合内获得{{强攻' + str(amount) + '}}' + ('和{{游走}}' if roam else '') + '。（如果它是进攻方，则{{S}}+' + str(amount) + '。）'
        return dict(cardNo='TEST', cardCategory='spell', cardEffect=text, behaviors=[dict(requiredTargetCount=1,
                    targetScope='ANY_UNIT', statusEffectId='COMBAT_KEYWORD:强攻' + str(amount), secondaryStatusEffectId='ROAM' if roam else '')])

    def outcome(self, card, probes=()):
        clause = audit.card_clauses(card)[1]
        return audit.audit_clause(card, clause, audit.Sources(self.root, [], probes))['status']

    def test_each_printed_amount_needs_a_real_temporary_keyword_application_and_reader(self):
        for amount in [1, 2, 3, 4]:
            with self.subTest(amount=amount): self.assertEqual('route', self.outcome(self.card(amount)))

    def test_old_conditional_power_does_not_implement_a_future_assault_grant(self):
        c = self.card(); c['behaviors'][0].update(statusEffectId='OVERWHELM_3', powerModifierAmount=3, powerModifierConditionKind='TARGET_IS_ATTACKING')
        self.assertEqual('missing', self.outcome(c))

    def test_wrong_amount_or_duplicate_flat_power_remains_missing(self):
        c = self.card(); c['behaviors'][0]['statusEffectId'] = 'COMBAT_KEYWORD:强攻2'
        self.assertEqual('missing', self.outcome(c))
        c = self.card(); c['behaviors'][0]['powerModifierAmount'] = 3
        self.assertEqual('missing', self.outcome(c))

    def test_wrong_selector_optional_target_or_wrong_status_index_remains_missing(self):
        for fields in [dict(targetScope='BATTLEFIELD_UNIT'), dict(minTargetCount=0), dict(requiredTargetCount=2),
                       dict(statusEffectTargetIndex=1), dict(anyNumberOfTargets=True),
                       dict(powerModifierConditionKind='TARGET_IS_ATTACKING'), dict(statusEffectUsesTargetCurrentPower=True)]:
            with self.subTest(fields=fields):
                c = self.card(); c['behaviors'][0].update(fields)
                self.assertEqual('missing', self.outcome(c))

    def test_metadata_and_projection_cannot_substitute_for_the_attack_might_consumer(self):
        (self.engine / 'CoreRuleEngine.CountedCombatKeywords.cs').unlink()
        (self.engine / 'MatchSession.cs').write_text('TemporaryCombatKeywordRules.Amount(unit, keyword);')
        self.assertEqual('missing', self.outcome(self.card()))

    def test_a_parser_does_not_bypass_missing_actual_keyword_metadata(self):
        c = self.card(); c['behaviors'][0]['statusEffectId'] = 'OVERWHELM_3'
        clause = audit.card_clauses(c)[1]
        (self.engine / 'CoreRuleEngine.cs').write_text(self.application + ' AssaultParser.Parse(text);')
        probes = [dict(text=clause['text'], method='AssaultParser.Parse', value=True)]
        self.assertEqual('missing', self.outcome(c, probes))

    def test_the_same_sentence_must_also_grant_its_printed_roam(self):
        c = self.card(2, roam=True)
        self.assertEqual('missing', self.outcome(c))
        (self.engine / 'CoreRuleEngine.cs').write_text(self.application + ' source.UntilEndOfTurnEffects.Contains(MoveUnitRoamOptionalCost);')
        self.assertEqual('route', self.outcome(c))
        c['behaviors'][0]['secondaryStatusEffectId'] = ''
        self.assertEqual('missing', self.outcome(c))

    def test_a_grant_also_needs_the_current_attack_role_reader(self):
        (self.engine / 'CoreRuleEngine.cs').write_text('TemporaryCombatKeywordRules.AppliedEffectId(card, status);')
        self.assertEqual('missing', self.outcome(self.card()))

    def test_comment_only_application_or_reader_does_not_count(self):
        for filename in ['CoreRuleEngine.cs', 'CoreRuleEngine.CountedCombatKeywords.cs']:
            with self.subTest(filename=filename):
                path = self.engine / filename
                original = path.read_text()
                path.write_text('// ' + original)
                self.assertEqual('missing', self.outcome(self.card()))
                path.write_text(original)
