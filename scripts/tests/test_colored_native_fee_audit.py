import importlib.util
from pathlib import Path
import shutil
import tempfile
import unittest

SPEC = importlib.util.spec_from_file_location('colored_native_fee_audit', Path(__file__).parents[1] / 'audit-card-effects.py')
audit = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(audit)


class ColoredNativeFeeAuditTests(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory(); self.addCleanup(temporary.cleanup)
        self.root = Path(temporary.name)
        self.engine = self.root / 'src/Riftbound.Engine'; self.engine.mkdir(parents=True)

    @staticmethod
    def text(xerath):
        return ('支付{{红色}}，{{横置}}：对一名单位造成3点伤害。' if xerath
                else '支付{{2}}和{{红色}}：让我本回合内战力翻倍。')

    @staticmethod
    def profile(xerath):
        return dict(abilityId='PAY_RED_EXHAUST_DAMAGE_3' if xerath else 'PAY_2_RED_DOUBLE_POWER',
            effectKind='XERATH_PAY_RED_EXHAUST_DAMAGE_3' if xerath else 'VI_PAY_2_RED_DOUBLE_POWER_UNTIL_END_OF_TURN',
            manaCost=0 if xerath else 2, powerCost=0, powerCostByTrait={'red': 1},
            requiredTargetCount=1 if xerath else 0, damageAmount=3 if xerath else 0,
            exhaustsSourceAsCost=xerath, requiresBattlefieldSource=xerath, appliesSpellshieldTargetTax=xerath)

    @staticmethod
    def consumer(xerath):
        name = 'ResolveXerathDamageAbility' if xerath else 'ResolveActivateAbility'
        return ('    private static ResolutionResult ' + name + '() {\n'
            'P4ActivatedAbilityCatalog.TryGetByAbilityId(command.AbilityId, out var ability);\n'
            'var powerCostByTrait = P4ActivatedAbilityCatalog.PowerCostByTraitForAbility(ability);\n'
            'var totalPowerCost = ability.PowerCost + powerCostByTrait.Values.Sum()'
            + (' + spellshieldTaxPower;' if xerath else ';') + '\n'
            'genericPowerCost: ' + ('(ability.PowerCost + spellshieldTaxPower)' if xerath else 'ability.PowerCost') + ',\n'
            'totalPowerCost: totalPowerCost, powerCostByTrait: powerCostByTrait,\n'
            'TryApplyOrderedPaymentResources(state); PaymentCostRules.AuthorizePayment(plan);\n'
            'PaymentCostRules.TryCommitPayment(plan); PaymentCostRules.BuildCostPaidPayload(plan);\n    }\n')

    def sources(self, xerath=False, transform=lambda value: value):
        vi = self.consumer(False)
        if xerath:
            vi += '    private static ResolutionResult ResolveXerathDamageAbilityIgnored() {}\n'
            vi = vi.replace('P4ActivatedAbilityCatalog.TryGetByAbilityId(command.AbilityId, out var ability);',
                'P4ActivatedAbilityCatalog.TryGetByAbilityId(command.AbilityId, out var ability);\n'
                'return ResolveXerathDamageAbility(state, intent, command, ability);', 1)
            vi += self.consumer(True)
        (self.engine / 'CoreRuleEngine.cs').write_text(transform(vi))

    def outcome(self, xerath=False, profile=None):
        card = dict(cardNo='TEST', cardName='test', cardEffect=self.text(xerath),
                    activations=[self.profile(xerath) if profile is None else profile])
        return audit.colored_legacy_activation_fee(card, audit.card_clauses(card)[0], audit.Sources(self.root, []))['status']

    def test_a_correct_fee_does_not_mark_the_unbound_native_skill_clause_complete(self):
        for xerath in [False, True]:
            self.sources(xerath)
            card = dict(cardNo='TEST', cardName='test', cardEffect=self.text(xerath), activations=[self.profile(xerath)])
            result = audit.audit_clause(card, audit.card_clauses(card)[0], audit.Sources(self.root, []))
            self.assertEqual('missing', result['status'])
            self.assertIn('有色费用已实现', result['reason'])
            self.assertIn('代际', result['reason'])

    def test_both_consumers_require_the_exact_colored_profile_and_payment_flow(self):
        for xerath in [False, True]:
            with self.subTest(xerath=xerath):
                self.sources(xerath); self.assertEqual('route', self.outcome(xerath))

    def test_colored_catalog_metadata_without_consumers_is_not_enough(self):
        for xerath in [False, True]:
            self.assertEqual('missing', self.outcome(xerath))

    def test_the_old_generic_fee_or_wrong_color_amount_and_extra_fee_stay_missing(self):
        for xerath in [False, True]:
            self.sources(xerath)
            for changes in [dict(powerCost=1, powerCostByTrait={}), dict(powerCostByTrait={'blue': 1}),
                            dict(powerCostByTrait={'red': 2}), dict(powerCost=1), dict(experienceCost=1),
                            dict(manaCost=3), dict(exhaustsSourceAsCost=not xerath)]:
                profile = self.profile(xerath); profile.update(changes)
                with self.subTest(xerath=xerath, changes=changes):
                    self.assertEqual('missing', self.outcome(xerath, profile))

    def test_effect_amount_targets_and_source_scope_must_also_match(self):
        for xerath in [False, True]:
            self.sources(xerath)
            for changes in [dict(damageAmount=9), dict(requiredTargetCount=2),
                            dict(requiresBattlefieldSource=not xerath), dict(appliesSpellshieldTargetTax=not xerath)]:
                profile = self.profile(xerath); profile.update(changes)
                with self.subTest(xerath=xerath, changes=changes):
                    self.assertEqual('missing', self.outcome(xerath, profile))

    def test_other_dispatch_branches_and_unprinted_permissions_cannot_certify_the_skill(self):
        for key, value in [('simpleEffect', {'grantsSourceBoon': True}), ('kind', 'OTHER'),
                           ('isResourceSkill', True), ('reactionSpeed', True), ('swiftSpeed', True),
                           ('sourceScope', 'BASE'), ('sourceRequiredTag', 'OTHER')]:
            self.sources(); profile = self.profile(False); profile[key] = value
            with self.subTest(key=key): self.assertEqual('missing', self.outcome(profile=profile))

    def test_reading_the_color_without_using_it_in_the_plan_does_not_certify_payment(self):
        for xerath in [False, True]:
            self.sources(xerath, lambda text: text.replace('powerCostByTrait: powerCostByTrait,', ''))
            self.assertEqual('missing', self.outcome(xerath))

    def test_shared_payment_authorization_commit_resource_actions_and_total_are_required(self):
        for xerath in [False, True]:
            for token in ['PaymentCostRules.AuthorizePayment(', 'PaymentCostRules.TryCommitPayment(',
                          'TryApplyOrderedPaymentResources(', 'powerCostByTrait.Values.Sum()']:
                self.sources(xerath, lambda text: text.replace(token, 'unrelated('))
                with self.subTest(xerath=xerath, token=token): self.assertEqual('missing', self.outcome(xerath))

    def test_a_neighboring_skills_color_reader_cannot_certify_this_consumer(self):
        self.sources()
        path = self.engine / 'CoreRuleEngine.cs'; source = path.read_text()
        path.write_text(source.replace('ResolveActivateAbility()', 'UnrelatedSkill()')
                        + '    private static ResolutionResult ResolveActivateAbility() { return null; }\n')
        self.assertEqual('missing', self.outcome())

    def test_an_unused_xerath_consumer_without_its_dispatch_is_not_a_route(self):
        self.sources(True, lambda text: text.replace('return ResolveXerathDamageAbility(state, intent, command, ability);', ''))
        self.assertEqual('missing', self.outcome(True))


class NativeUnitBindingAuditTests(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory(); self.addCleanup(temporary.cleanup)
        self.root = Path(temporary.name)
        repository = Path(__file__).parents[2]
        self.paths = [
            'CoreRuleEngine.cs', 'CoreRuleEngine.NativeUnitActivations.cs', 'SelfAbilitySourceRules.cs',
            'MatchSession.cs', 'MatchRecovery.cs', 'MatchRecovery/Authoritative.Resources.cs']
        for name in self.paths:
            destination = self.root / 'src/Riftbound.Engine' / name
            destination.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(repository / 'src/Riftbound.Engine' / name, destination)

    def outcome(self, xerath, changes=None):
        profile = ColoredNativeFeeAuditTests.profile(xerath)
        profile.update(bindsUnitSource=True, modifiesUnitSource=not xerath)
        profile.update(changes or {})
        card = dict(cardNo='TEST', cardName='test', cardEffect=ColoredNativeFeeAuditTests.text(xerath), activations=[profile])
        return audit.audit_clause(card, audit.card_clauses(card)[0], audit.Sources(self.root, []))['status']

    def mutate(self, path, old, new='removed'):
        destination = self.root / 'src/Riftbound.Engine' / path
        previous = destination.read_text()
        self.assertIn(old, previous)
        destination.write_text(previous.replace(old, new))
        return destination, previous

    def test_both_complete_clauses_require_their_source_binding_policy(self):
        for xerath in [False, True]:
            self.assertEqual('route', self.outcome(xerath))
            for changes in [dict(bindsUnitSource=False), dict(modifiesUnitSource=xerath)]:
                self.assertEqual('missing', self.outcome(xerath, changes))

    def test_an_unbound_consumer_cannot_borrow_the_other_skills_binding(self):
        for xerath, method in [(False, 'ResolveActivateAbility'), (True, 'ResolveXerathDamageAbility')]:
            destination = self.root / 'src/Riftbound.Engine/CoreRuleEngine.cs'
            previous = destination.read_text()
            pattern = (r'(?ms)(^    private static ResolutionResult ' + method
                + r'\(.*?)(?=^    (?:private|internal|public|protected)\s|\Z)')
            import re
            changed, count = re.subn(pattern, lambda match: match[0].replace('return BindNativeUnitActivationSource(', 'return Unbound('), previous)
            self.assertEqual(1, count); destination.write_text(changed)
            self.assertEqual('missing', self.outcome(xerath))
            destination.write_text(previous)

    def test_source_identity_and_resolution_checks_are_consumed(self):
        for path, token in [
            ('CoreRuleEngine.NativeUnitActivations.cs', 'new(source.ObjectId, source.ObjectGeneration)'),
            ('CoreRuleEngine.NativeUnitActivations.cs', 'source.CopyFaceTransitions?.Count ?? 0'),
            ('CoreRuleEngine.NativeUnitActivations.cs', 'SelfAbilitySourceRules.Valid('),
            ('CoreRuleEngine.NativeUnitActivations.cs', 'MatchStateHasher.HashValue(expected)'),
            ('CoreRuleEngine.NativeUnitActivations.cs', 'source.ObjectGeneration == context.Source.Generation'),
            ('CoreRuleEngine.NativeUnitActivations.cs', 'IsObjectOnField(state.PlayerZones, source.ObjectId)'),
            ('CoreRuleEngine.cs', 'if (!ValidNativeUnitActivation(state, stackItem))'),
            ('CoreRuleEngine.cs', 'nativeAbility.ModifiesUnitSource && !NativeUnitActivationSourceStillOnField(state, stackItem)'),
            ('CoreRuleEngine.cs', 'stackItem = MaskTargetsFromPreviousGenerations(state, stackItem);')]:
            with self.subTest(path=path, token=token):
                destination, previous = self.mutate(path, token)
                for xerath in [False, True]: self.assertEqual('missing', self.outcome(xerath))
                destination.write_text(previous)

    def test_departed_token_receipts_json_normalization_and_recovery_are_required(self):
        for path, token in [
            ('SelfAbilitySourceRules.cs', 'departed.Contains(native.Source.ObjectId) && ValidNativeUnitActivation(before, item)'),
            ('SelfAbilitySourceRules.cs', 'NativeUnitActivation = native with { CeasedSource = SelfAbilitySourceRules.Capture('),
            ('MatchSession.cs', 'NativeUnitActivation = item.NativeUnitActivation'),
            ('MatchRecovery.cs', 'stackItem.NativeUnitActivation is { CeasedSource: not null } && CoreRuleEngine.ValidNativeUnitActivation(authoritativeState, stackItem)'),
            ('MatchRecovery/Authoritative.Resources.cs', 'if (!CoreRuleEngine.ValidNativeUnitActivation(authoritativeState, item))')]:
            with self.subTest(path=path, token=token):
                destination, previous = self.mutate(path, token)
                for xerath in [False, True]: self.assertEqual('missing', self.outcome(xerath))
                destination.write_text(previous)

    def test_hidden_source_projection_events_and_validation_must_agree(self):
        for path, token in [
            ('CoreRuleEngine.cs', '!IsParsedUnitHeldContext(resolvedItem.HeldContext) && resolvedItem.NativeUnitActivation is null'),
            ('CoreRuleEngine.cs', '"targetObjectIds"] = resolvedItem.UnitEvent is null && resolvedItem.NativeUnitActivation is null'),
            ('CoreRuleEngine.NativeUnitActivations.cs', 'ProjectionViewers.Spectator'),
            ('CoreRuleEngine.NativeUnitActivations.cs', 'pair.Key is "sourceObjectId" or "unitSourceObjectId"'),
            ('MatchSession.cs', 'item.TargetedPowerAbility is not null || item.NativeUnitActivation is not null'),
            ('MatchSession.cs', 'item.TargetedPowerAbility is null && item.NativeUnitActivation is null'),
            ('MatchRecovery.cs', 'item.NativeUnitActivation is not null && CoreRuleEngine.ValidNativeUnitActivation(state, item)')]:
            with self.subTest(path=path, token=token):
                destination, previous = self.mutate(path, token)
                for xerath in [False, True]: self.assertEqual('missing', self.outcome(xerath))
                destination.write_text(previous)
