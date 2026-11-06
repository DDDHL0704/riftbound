import importlib.util
from pathlib import Path
import tempfile
import unittest

SPEC = importlib.util.spec_from_file_location('private_keep_audit', Path(__file__).parents[1] / 'audit-card-effects.py')
audit = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(audit)
TEXT = '每名玩家分别选择自己的两名单位、两件装备、两枚符文和两张手牌留在原位，然后回收其余卡牌。'


class PrivateKeepClauseAuditTests(unittest.TestCase):
    def outcome(self, **behavior):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            engine = root / 'src/Riftbound.Engine'
            engine.mkdir(parents=True)
            (engine / 'CoreRuleEngine.cs').write_text('var flag = behavior.RecyclesUnkeptSacredJudgmentCards; '
                                                    'var recycle = behavior.RecyclesTargets;')
            card = dict(cardNo='OGN·244/298', cardName='圣裁之刻', cardCategory='spell', cardEffect=TEXT,
                        behaviors=[behavior], activations=[], legendActivations=[])
            return audit.audit_clause(card, audit.card_clauses(card)[0], audit.Sources(root, []))

    def test_sixteen_cast_targets_are_not_each_players_resolution_choices(self):
        self.assertEqual('missing', self.outcome(recyclesUnkeptSacredJudgmentCards=True,
                         requiredTargetCount=16, targetScope='SACRED_JUDGMENT_KEEP_CARD')['status'])

    def test_zero_targets_alone_do_not_fix_the_legacy_selection_or_rune_recycle(self):
        self.assertEqual('missing', self.outcome(recyclesUnkeptSacredJudgmentCards=True,
                         requiredTargetCount=0)['status'])

    def test_a_generic_recycle_consumer_cannot_certify_private_keeps(self):
        self.assertEqual('review', self.outcome(recyclesTargets=True)['status'])
