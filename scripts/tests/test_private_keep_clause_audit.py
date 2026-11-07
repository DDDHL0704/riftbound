import importlib.util
import json
from pathlib import Path
import tempfile
import unittest

SPEC = importlib.util.spec_from_file_location('private_keep_audit', Path(__file__).parents[1] / 'audit-card-effects.py')
audit = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(audit)
TEXT = '每名玩家分别选择自己的两名单位、两件装备、两枚符文和两张手牌留在原位，然后回收其余卡牌。'


class PrivateKeepClauseAuditTests(unittest.TestCase):
    def outcome(self, *, reviewed=False, missing_consumer=False, missing_test=False,
                card_no='OGN·244/298', wrong_scope=False, **behavior):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            engine = root / 'src/Riftbound.Engine'
            engine.mkdir(parents=True)
            (engine / 'CoreRuleEngine.cs').write_text('var flag = behavior.RecyclesUnkeptSacredJudgmentCards; '
                                                    'var recycle = behavior.RecyclesTargets;'
                                                    + (' ResolveKeepAndRecycle();' if reviewed else '')
                                                    + (' RecycleCardsToOwnersDecks();' if reviewed and not missing_consumer else ''))
            if reviewed:
                test = root / 'tests/Riftbound.ConformanceTests/KeepTests.cs'
                test.parent.mkdir(parents=True)
                test.write_text('[Fact] public async Task KeepsAtResolution() { }' if not missing_test else '')
                document = root / 'scripts/audit/reviewed-clauses/keep.json'
                document.parent.mkdir(parents=True)
                record = dict(cardNos=['OGN·244/298'], text=TEXT, scope=TEXT, status='route',
                              reason='逐玩家结算选择后分类回收', rules='核心417.1、417.5、417.6',
                              runtime={'behaviors': [{'recyclesUnkeptSacredJudgmentCards': True}]},
                              evidence=[dict(path='src/Riftbound.Engine/CoreRuleEngine.cs',
                                             contains=['ResolveKeepAndRecycle();', 'RecycleCardsToOwnersDecks();'])],
                              tests=[dict(path='tests/Riftbound.ConformanceTests/KeepTests.cs', methods=['KeepsAtResolution'])])
                document.write_text(json.dumps(dict(schemaVersion=1, records=[record]), ensure_ascii=False))
            card = dict(cardNo=card_no, cardName='圣裁之刻', cardCategory='spell', cardEffect=TEXT,
                        behaviors=[behavior], activations=[], legendActivations=[])
            clause = audit.card_clauses(card)[0]
            if wrong_scope:
                clause['scope'] = '另一条指示。'
            return audit.audit_clause(card, clause, audit.Sources(root, []))

    def test_sixteen_cast_targets_are_not_each_players_resolution_choices(self):
        self.assertEqual('missing', self.outcome(recyclesUnkeptSacredJudgmentCards=True,
                         requiredTargetCount=16, targetScope='SACRED_JUDGMENT_KEEP_CARD')['status'])

    def test_zero_targets_alone_do_not_fix_the_legacy_selection_or_rune_recycle(self):
        self.assertEqual('missing', self.outcome(recyclesUnkeptSacredJudgmentCards=True,
                         requiredTargetCount=0)['status'])

    def test_a_generic_recycle_consumer_cannot_certify_private_keeps(self):
        self.assertEqual('review', self.outcome(recyclesTargets=True)['status'])

    def test_reviewed_resolution_evidence_requires_a_current_zero_target_runtime(self):
        self.assertEqual('route', self.outcome(reviewed=True, recyclesUnkeptSacredJudgmentCards=True)['status'])
        self.assertEqual('route', self.outcome(reviewed=True, recyclesUnkeptSacredJudgmentCards=True,
                                             requiredTargetCount=0)['status'])
        self.assertEqual('missing', self.outcome(reviewed=True, recyclesUnkeptSacredJudgmentCards=True,
                                               requiredTargetCount=16)['status'])

    def test_removed_execution_or_rule_test_reopens_review(self):
        for key in ('missing_consumer', 'missing_test'):
            with self.subTest(key=key):
                self.assertEqual('review', self.outcome(reviewed=True, recyclesUnkeptSacredJudgmentCards=True,
                                                       **{key: True})['status'])

    def test_a_review_does_not_cover_another_print_or_scope(self):
        self.assertEqual('missing', self.outcome(reviewed=True, recyclesUnkeptSacredJudgmentCards=True,
                                               card_no='OGN·306/298')['status'])
        self.assertEqual('missing', self.outcome(reviewed=True, recyclesUnkeptSacredJudgmentCards=True,
                                               wrong_scope=True)['status'])
