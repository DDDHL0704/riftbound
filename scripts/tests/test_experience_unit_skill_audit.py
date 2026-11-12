"""Printed untargeted experience skills require both payment and resolution."""
import copy
import importlib.util
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[2]
SPEC = importlib.util.spec_from_file_location("experience_unit_audit", ROOT / "scripts/audit-card-effects.py")
audit = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(audit)


class ExperienceUnitSkillAuditTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.sources = audit.Sources(ROOT, [])

    def sample(self, here=False):
        text = ("消耗3经验：你在此处的单位在本回合内获得{{游走}}。（我们可以向其他战场进行移动。）" if here else
                "消耗2经验：给予我{{增益}}。（如果我未拥有增益，则获得一个{{S}}+1增益。）")
        ability = dict(abilityId="HERE" if here else "BOON", kind="GRANT_FRIENDLY_HERE_ROAM" if here else "GRANT_SELF_BOON",
            experienceCost=3 if here else 2, requiredTargetCount=0, sourceRequiredTag="CARD_TYPE:UNIT",
            simpleEffect={"grantsFriendlyHereRoam" if here else "grantsSourceBoon": True})
        card = dict(cardNo="UNL-126/219" if here else "UNL-102/219", cardName="Experience source", cardCategory="unit",
                    cardEffect=text, behaviors=[], activations=[ability])
        clause, = audit.card_clauses(card)
        return card, clause

    def changed_sources(self, filename, fragment=None):
        sources = copy.copy(self.sources)
        sources.files = dict(self.sources.files)
        sources.registration_files = dict(self.sources.registration_files)
        path = "src/Riftbound.Engine/" + filename
        files = sources.registration_files if filename == "P4ActivatedAbilityCatalog.cs" else sources.files
        if fragment is None:
            files.pop(path)
        else:
            self.assertIn(fragment, files[path])
            files[path] = files[path].replace(fragment, "REMOVED_CONSUMER")
        return sources

    def test_registry_fragments_are_opt_in_and_never_a_general_execution_route(self):
        path = "src/Riftbound.Engine/P4ActivatedAbilityCatalog.cs"
        self.assertNotIn(path, self.sources.files)
        self.assertIn(path, self.sources.registration_files)
        self.assertEqual([], self.sources.references("UntargetedUnitDefinition("))
        self.assertEqual("", audit.executable_member(self.sources, "P4ActivatedAbilityCatalog.cs", "UntargetedUnitDefinition"))
        self.assertIn("ExperienceCost: ability.ExperienceCost.GetValueOrDefault()",
                      audit.executable_member(self.sources, "P4ActivatedAbilityCatalog.cs", "UntargetedUnitDefinition", registration=True))

    def test_exact_experience_and_untargeted_effects_have_shared_consumers(self):
        for here in (False, True):
            with self.subTest(here=here):
                card, clause = self.sample(here)
                self.assertEqual("route", audit.experience_unit_skill_route(card, clause, self.sources)["status"])
                self.assertEqual("route", audit.audit_clause(card, clause, self.sources)["status"])

    def test_parsed_kind_or_prompt_without_the_runtime_profile_is_missing(self):
        for here in (False, True):
            card, clause = self.sample(here)
            card["activations"] = []
            self.assertEqual("missing", audit.experience_unit_skill_route(card, clause, self.sources)["status"])

    def test_wrong_cost_exhaustion_speed_or_target_count_cannot_certify_the_clause(self):
        for here in (False, True):
            for mutation in (dict(experienceCost=1), dict(experienceCost=0), dict(manaCost=1), dict(powerCost=1),
                             dict(exhaustsSourceAsCost=True), dict(requiredTargetCount=1), dict(requiresBattlefieldSource=True),
                             dict(reactionSpeed=True), dict(swiftSpeed=True), dict(sourceRequiredTag="CARD_TYPE:EQUIPMENT")):
                with self.subTest(here=here, mutation=mutation):
                    card, clause = self.sample(here)
                    card["activations"][0].update(mutation)
                    self.assertEqual("missing", audit.experience_unit_skill_route(card, clause, self.sources)["status"])

    def test_a_different_or_additional_effect_is_not_the_printed_skill(self):
        for here in (False, True):
            for simple in (dict(grantsSourceRoam=True), dict(grantsSourceBoon=True, drawCount=1)):
                card, clause = self.sample(here)
                card["activations"][0]["simpleEffect"] = simple
                self.assertEqual("missing", audit.experience_unit_skill_route(card, clause, self.sources)["status"])

    def test_payment_and_typed_catalog_consumers_are_required(self):
        for filename, fragment in (("P4ActivatedAbilityCatalog.cs", "ExperienceCost: ability.ExperienceCost.GetValueOrDefault()"),
                                   ("CoreRuleEngine.SimpleActivatedAbilities.cs", "experienceCost: ability.ExperienceCost"),
                                   ("PaymentCostRules.cs", "currentExperience < plan.ExperienceCost"),
                                   ("PaymentCostRules.cs", "currentExperience - plan.ExperienceCost")):
            for here in (False, True):
                with self.subTest(filename=filename, fragment=fragment, here=here):
                    self.assertEqual("missing", audit.experience_unit_skill_route(*self.sample(here), self.changed_sources(filename, fragment))["status"])

    def test_an_unused_payment_lookalike_does_not_replace_the_real_member(self):
        sources = self.changed_sources("CoreRuleEngine.SimpleActivatedAbilities.cs", "experienceCost: ability.ExperienceCost")
        sources.files["src/Riftbound.Engine/CoreRuleEngine.SimpleActivatedAbilities.cs"] += "\nprivate static void Unused() { Pay(experienceCost: ability.ExperienceCost); }\n"
        self.assertEqual("missing", audit.experience_unit_skill_route(*self.sample(), sources)["status"])

    def test_here_needs_original_source_generation_current_controller_and_current_location(self):
        for fragment in ("hereSource.ObjectGeneration == item.SimpleActivation!.Source.Generation",
                         "EffectiveFieldControllerId(zones, card.ObjectId, card) == item.ControllerId",
                         "IsSameFieldLocation(zones, locations, hereSource.ObjectId, card.ObjectId)"):
            with self.subTest(fragment=fragment):
                self.assertEqual("missing", audit.experience_unit_skill_route(*self.sample(True), self.changed_sources("CoreRuleEngine.SimpleActivatedAbilities.cs", fragment))["status"])

    def test_self_boon_needs_the_original_public_source(self):
        for fragment in ("boonSource.ObjectGeneration == item.SimpleActivation!.Source.Generation", "ApplyBoon(boonSource"):
            self.assertEqual("missing", audit.experience_unit_skill_route(*self.sample(), self.changed_sources("CoreRuleEngine.SimpleActivatedAbilities.cs", fragment))["status"])

    def test_global_enemy_and_targeted_roam_are_different_clauses(self):
        card, clause = self.sample(True)
        for text in ("消耗3经验：你的单位在本回合内获得{{游走}}。", "消耗3经验：此处的敌方单位在本回合内获得{{游走}}。", "消耗3经验：一名友方单位在本回合内获得{{游走}}。"):
            with self.subTest(text=text):
                self.assertIsNone(audit.experience_unit_skill_route(card, dict(clause, text=text), self.sources))

    def test_duplicate_runtime_profiles_do_not_certify_a_single_printed_skill(self):
        card, clause = self.sample()
        card["activations"].append(dict(card["activations"][0]))
        self.assertEqual("missing", audit.experience_unit_skill_route(card, clause, self.sources)["status"])

    def test_non_unit_cards_do_not_borrow_a_unit_runtime_profile(self):
        for category in ("legend", "equipment", "battlefield"):
            card, clause = self.sample()
            card["cardCategory"] = category
            self.assertEqual("missing", audit.experience_unit_skill_route(card, clause, self.sources)["status"])
