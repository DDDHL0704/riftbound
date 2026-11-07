import copy
import unittest
from test_effect_clause_audit import audit


class EnemyDeathSourceClauseAuditTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.sources = audit.Sources(audit.ROOT, [])

    def card(self, gold=False):
        text = ('每回合限一次，如果我位于战场上，当一名敌方单位被摧毁时，打出一个休眠的“金币”装备指示物。' if gold
                else '当任意数量的敌方单位被摧毁时，让我变为活跃状态。')
        profile = dict(kind='SOURCE_BATTLEFIELD_ENEMY_DESTROYED_ONCE_GOLD' if gold else 'SOURCE_ENEMY_BATCH_DESTROYED_READY',
                       timing='UNIT_DESTROYED', targetScope='ENEMY_DESTROYED_UNIT')
        profile.update(dict(oncePerTurn=True, createdTokenCount=1, createdTokenName='金币', createdTokenDestination='BASE',
                            createdTokenExhausted=True, requiresSourceBattlefieldAtTrigger=True) if gold else dict(readiesSource=True))
        return dict(cardNo='UNL-145/219' if gold else 'SFD·203/221', cardName='派克' if gold else '战争女神',
                    cardCategory='hero_unit' if gold else 'legendary', cardEffect=text, triggers=[dict(node=profile, predicates=[])])

    def outcome(self, card=None, gold=False, sources=None):
        card = card or self.card(gold)
        return audit.audit_clause(card, audit.card_clauses(card)[0], sources or self.sources)['status']

    def changed(self, filename, token):
        changed = copy.copy(self.sources)
        changed.files = dict(self.sources.files)
        path = 'src/Riftbound.Engine/' + filename
        self.assertIn(token, changed.files[path])
        changed.files[path] = changed.files[path].replace(token, 'MISSING_SHARED_CONSUMER')
        return changed

    def test_each_print_requires_its_precise_trigger_and_shared_consumers(self):
        for no, gold in [('SFD·203/221', False), ('SFD·250/221', False), ('UNL-145/219', True), ('UNL-145a/219', True)]:
            card = self.card(gold); card['cardNo'] = no
            self.assertEqual('route', self.outcome(card))

    def test_registration_and_untyped_trigger_candidates_are_insufficient(self):
        for gold in (False, True):
            for triggers in [[], [dict(node=dict(kind='destroyed', timing='unspecified'), predicates=[])]]:
                card = self.card(gold); card['triggers'] = triggers
                self.assertEqual('missing', self.outcome(card))

    def test_friendship_wrong_destination_or_optional_flags_cannot_match(self):
        for gold in (False, True):
            changes = [('targetScope', 'OTHER_FRIENDLY_DESTROYED_UNIT'), ('timing', 'PLAY'), ('optional', True), ('manaCost', 1)]
            changes += [('readiesSource', False)] if not gold else [('oncePerTurn', False), ('requiresSourceBattlefieldAtTrigger', False),
                ('createdTokenCount', 2), ('createdTokenName', '随从'), ('createdTokenDestination', 'BATTLEFIELD'), ('createdTokenExhausted', False)]
            for key, value in changes:
                with self.subTest(gold=gold, key=key):
                    card = self.card(gold); card['triggers'][0]['node'][key] = value
                    self.assertEqual('missing', self.outcome(card))

    def test_public_legend_and_current_controller_capture_are_required(self):
        for token in ['EffectivePublicStaticAuraSourceControllerId(zones, source.ObjectId, source)',
                      'zones.Values.Any(zone => zone.LegendZone.Contains(source.ObjectId))', 'IsEnemySourceDeathKind(spec.Kind)']:
            self.assertEqual('missing', self.outcome(sources=self.changed('CoreRuleEngine.DeathObservers.cs', token)))

    def test_earned_skills_need_an_actual_resolution_and_generation_gate(self):
        for file, token in [('CoreRuleEngine.DeathObservers.cs', 'return ResolveEnemySourceDeathObserver(state, item, c, spec, resolved);'),
                            ('CoreRuleEngine.DeathObservers.cs', 'c.SourceGeneration <= currentSource.ObjectGeneration'),
                            ('CoreRuleEngine.EnemyDeathSourceTriggers.cs', 'source.ObjectGeneration == context.SourceGeneration')]:
            self.assertEqual('missing', self.outcome(sources=self.changed(file, token)))

    def test_face_down_checks_must_not_exclude_the_printed_standby_keyword(self):
        for gold in (False, True):
            paths = ['CoreRuleEngine.DeathObservers.cs']
            if not gold:
                paths.append('CoreRuleEngine.EnemyDeathSourceTriggers.cs')
            for filename in paths:
                with self.subTest(gold=gold, filename=filename):
                    self.assertEqual('missing', self.outcome(gold=gold,
                        sources=self.changed(filename, '!source.IsFaceDown')))
                    sources = copy.copy(self.sources); sources.files = dict(self.sources.files)
                    path = 'src/Riftbound.Engine/' + filename
                    sources.files[path] = sources.files[path].replace('!source.IsFaceDown',
                        '!source.IsFaceDown && !source.Tags.Contains(CardObjectTags.Standby)')
                    self.assertEqual('missing', self.outcome(gold=gold, sources=sources))

    def test_any_number_means_one_observation_for_the_whole_death_batch(self):
        token = '!observedBatches.Add((death.Batch, observer.ObjectId, observer.Context.RuleTextInstanceId))'
        self.assertEqual('missing', self.outcome(sources=self.changed('CoreRuleEngine.DeathObservers.cs', token)))

    def test_pyke_needs_a_captured_battlefield_and_a_per_instance_turn_limit(self):
        for token in ['spec.RequiresSourceBattlefieldAtTrigger == true && capturedSource.Zone != "BATTLEFIELD"',
                      'or TriggerKinds.SourceBattlefieldEnemyDestroyedOnceGold)', 'effects.Contains(marker, StringComparer.Ordinal)',
                      'effects = AddUntilEndOfTurnEffect(effects, marker)']:
            self.assertEqual('missing', self.outcome(gold=True, sources=self.changed('CoreRuleEngine.DeathObservers.cs', token)))

    def test_old_immediate_ready_shortcut_cannot_certify_a_respondable_skill(self):
        sources = copy.copy(self.sources); sources.files = dict(self.sources.files)
        sources.files['src/Riftbound.Engine/CoreRuleEngine.cs'] += '\nResolveSivirEnemyDestroyedReadyTriggers(zones,cards,owners);\n'
        self.assertEqual('missing', self.outcome(sources=sources))

    def test_scope_may_not_drop_an_extra_condition(self):
        card = self.card(); clause = audit.card_clauses(card)[0]
        clause['scope'] = '如果我位于基地，则' + clause['scope']
        self.assertEqual('missing', audit.audit_clause(card, clause, self.sources)['status'])


if __name__ == '__main__':
    unittest.main()
