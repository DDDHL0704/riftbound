from pathlib import Path
import copy, importlib.util, json, re, unittest
ROOT=Path(__file__).resolve().parents[2]
sp=importlib.util.spec_from_file_location('revealed_unit_deck_selection_audit',ROOT/'scripts/audit-card-effects.py')
audit=importlib.util.module_from_spec(sp);sp.loader.exec_module(audit)
PRINTS=['UNL-051/219','UNL-051a/219']
TEXT=('当你打出我时，或当我据守一处战场时，查看你主牌堆顶部的三张牌。'
      '你可以选择从中展示一名单位，并抽取该卡牌。回收其余的卡牌。'
      '如果你展示了一名“鸟类”、“猫科”、“犬形”或“魄罗”属性单位，则进行一次：给予一名友方单位{{增益}}。'
      '（如果其未拥有增益，则获得一个{{S}}+1增益。）')

class RevealedUnitDeckSelectionAuditTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.sources=audit.Sources(ROOT,[])


    def card(self):
        return dict(cardNo='UNL-051/219',cardCategory='hero_unit',cardEffect=TEXT,
                    behaviors=[dict(playsSourceToBaseAsUnit=True)],
                    unitEventAbility=dict(timing='PLAY_OR_HOLD',effect='LOOK_REVEAL_DRAW_RECYCLE_THEN_TRAIT_BOON',amount=3,
                        deckSelection=dict(lookCount=3,selectedRequiredTag='CARD_TYPE:UNIT',boonTraits=['鸟类','猫科','犬形','魄罗'])))

    def statuses(self,card=None,sources=None):
        card=card or self.card()
        return [(audit.revealed_unit_deck_selection_route(card,c,sources or self.sources) or {}).get('status') for c in audit.card_clauses(card)]

    def remove(self,filename,token):
        sources=copy.copy(self.sources);sources.files=dict(self.sources.files)
        path='src/Riftbound.Engine/'+filename
        self.assertIn(token,sources.files[path])
        sources.files[path]=sources.files[path].replace(token,'REMOVED_ACTUAL_CONSUMER')
        sources.files[path]+='\n    private static void UnusedDeckSelectionDecoy() { '+token+'; }\n'
        return sources

    def test_real_four_sentence_scope_and_both_prints(self):
        for no in PRINTS:
            card=self.card();card['cardNo']=no
            self.assertEqual(['route']*4,self.statuses(card))
            self.assertEqual(['route']*4,[audit.audit_clause(card,c,self.sources)['status'] for c in audit.card_clauses(card)])
        self.assertEqual(4,len(audit.card_clauses(self.card())))

    def test_primary_registration_and_mandatory_profile(self):
        for key,value in [('timing','PLAY'),('effect','BOON_FRIENDLY'),('amount',True),('amount','3'),('optional',True),('optional',0)]:
            card=self.card();card['unitEventAbility'][key]=value
            with self.subTest(key=key,value=value):self.assertEqual(['missing']*4,self.statuses(card))
        card=self.card();card['behaviors'][0]['unitEventAbility']=card['unitEventAbility'];card['unitEventAbility']=None
        self.assertEqual(['missing']*4,self.statuses(card))

    def test_unused_root_amount_does_not_replace_the_actual_look_count(self):
        # The live look consumer reads DeckSelection.LookCount. The generic
        # root Amount is neither its count nor proof of a three-card look.
        for amount in [0, 1, 2, 3, 5]:
            card=self.card();card['unitEventAbility']['amount']=amount
            with self.subTest(amount=amount):self.assertEqual(['route']*4,self.statuses(card))

    def test_exact_typed_look_and_unit_filter(self):
        for key,value in [('lookCount',True),('lookCount',2),('lookCount','3'),('selectedRequiredTag','CARD_TYPE:EQUIPMENT')]:
            card=self.card();card['unitEventAbility']['deckSelection'][key]=value
            with self.subTest(key=key,value=value):self.assertEqual(['missing']*4,self.statuses(card))

    def test_species_list_order_and_clause_independence(self):
        card=self.card();traits=card['unitEventAbility']['deckSelection']['boonTraits'];traits.reverse()
        self.assertEqual(['route']*4,self.statuses(card))
        traits[0]='机械'
        self.assertEqual(['route','route','route','missing'],self.statuses(card))
        traits[:]=['鸟类']*4
        self.assertEqual(['missing']*4,self.statuses(card))

    def test_missing_child_dispatch_does_not_erase_the_three_parent_instructions(self):
        sources=self.remove('CoreRuleEngine.UnitEventModes.cs','Effect = "BOON_FRIENDLY", Amount = 1, Optional = false')
        self.assertEqual(['route','route','route','missing'],self.statuses(sources=sources))

    def test_missing_real_draw_only_invalidates_the_draw_sentence(self):
        sources=self.remove('CoreRuleEngine.cs','Hand = zones.Hand.Concat(selectedCardIds).ToArray()')
        self.assertEqual(['route','missing','route','route'],self.statuses(sources=sources))

    def test_missing_real_recycle_only_invalidates_recycling(self):
        sources=self.remove('CoreRuleEngine.cs','.Concat(randomizedRecycledCardIds)')
        self.assertEqual(['route','route','missing','route'],self.statuses(sources=sources))

    def test_unused_helpers_cannot_replace_actual_dispatch_or_source_history(self):
        for filename,token in [
            ('CoreRuleEngine.UnitEvents.cs','return ResolveUnitEventDeckSelection(state, item)'),
            ('CoreRuleEngine.DeckChoices.cs','TryUnitEventDeckChoiceBehavior(item, out behavior)'),
            ('CoreRuleEngine.DeckObservation.cs','ParentUnitEventCapture = ObservedUnitEventCaptureHash('),
            ('CoreRuleEngine.DeckChoices.cs','RedactUnitEventSourceEvents(state, item, events.Concat(resumed.Events).ToArray())'),
            ('CoreRuleEngine.UnitEventDeckSelection.cs','if (item.DeckChoiceCompleted)'),
        ]:
            with self.subTest(filename=filename,token=token):self.assertEqual(['missing']*4,self.statuses(sources=self.remove(filename,token)))

    def test_actual_reveal_fact_must_precede_observation_and_draw(self):
        sources=copy.copy(self.sources);sources.files=dict(self.sources.files);path='src/Riftbound.Engine/CoreRuleEngine.DeckChoices.cs'
        capture='            selectionReveal = CaptureDeckSelectionReveal(state, item, cards, selected);\n'
        old='            remainingCount-=selected.Count-kept.Length;selected=kept;'
        self.assertIn(capture,sources.files[path]);sources.files[path]=sources.files[path].replace(capture,'').replace(old,old+'\n'+capture)
        self.assertEqual(['route','route','route','missing'],self.statuses(sources=sources))

    def test_unrelated_text_and_wrong_inherited_scope_cannot_borrow_the_route(self):
        card=self.card();clause=audit.card_clauses(card)[0]
        self.assertIsNone(audit.revealed_unit_deck_selection_route(card,dict(clause,text='回收一张手牌。'),self.sources))
        self.assertEqual('missing',audit.revealed_unit_deck_selection_route(card,dict(clause,scope=TEXT),self.sources)['status'])

if __name__=='__main__':unittest.main(verbosity=2)
