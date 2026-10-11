#!/usr/bin/env python3
from pathlib import Path
from urllib.request import urlopen,Request
from concurrent.futures import ThreadPoolExecutor
import argparse,json,hashlib,math,time,re
from datetime import date
parser=argparse.ArgumentParser(description='Preserve CN QA and all returned global gallery entries with count gaps exposed.')
parser.add_argument('--date',required=True)
args=parser.parse_args();snapshot=date.fromisoformat(args.date).isoformat()
ROOT=Path(__file__).resolve().parents[2];d=ROOT/'data/official/upstream'/snapshot;v=ROOT/'var/official'/snapshot/'global'
v.mkdir(parents=True,exist_ok=True)
if (d/'card-gallery.en-US.json').exists():raise SystemExit('Gallery snapshot exists; preserve reviewed sources.')
def get(u,body=None):
 req=Request(u,json.dumps(body).encode() if body else None,{'Content-Type':'application/json','User-Agent':'Mozilla/5.0'})
 for attempt in range(3):
  try:
   with urlopen(req,timeout=45) as r:return json.load(r)
  except Exception:
   if attempt==2:raise
   time.sleep(2)
def write(p,x):p.write_text(json.dumps(x,ensure_ascii=False,indent=2)+'\n')
u='https://lol-api.playloltcg.com/xcx/card/searchCardCraftWeb'
params=dict(pageNum=1,pageSize=100,searchContent='',cardCategoryList=[],cardColorList=[],rarityList=[],productCodeList=[])
a=json.loads((d/'card-catalog.zh-CN.json').read_text());old={c['id']:c for c in a['cards']}
catalog_hash=hashlib.sha256((d/'card-catalog.zh-CN.json').read_bytes()).hexdigest()
if (d/'card-qa.zh-CN.json').exists():
 qa_snapshot=json.loads((d/'card-qa.zh-CN.json').read_text())
 assert qa_snapshot['catalogSha256']==catalog_hash and len(qa_snapshot['entries'])==a['total'],'Saved QA does not match current snapshot'
else:
 with ThreadPoolExecutor(max_workers=4) as ex:pages=list(ex.map(lambda i:get(u,{**params,'pageNum':i})['result'],range(1,math.ceil(a['total']/100)+1)))
 cards=[c for p in pages for c in p['list']]
 assert all(p['total']==a['total'] for p in pages) and len(cards)==len({c['id'] for c in cards})==a['total']
 assert all({k:v for k,v in c.items() if k!='cardQaList'}=={k:v for k,v in old[c['id']].items() if k!='cardQaList'} for c in cards),'Catalog drift while collecting QA'
 qa_snapshot=dict(source=u,fetchedAt=snapshot,catalogSha256=catalog_hash,entries=[dict(id=c['id'],cardNo=c['cardNo'],cardQaList=c.get('cardQaList'),fieldPresent='cardQaList'in c) for c in sorted(cards,key=lambda c:c['id'])],note='Null/empty API QA does not imply no FAQ in official documents.')
 write(d/'card-qa.zh-CN.json',qa_snapshot)
print('CN QA',len(qa_snapshot['entries']),'nonempty',sum(bool(c.get('cardQaList')) for c in qa_snapshot['entries']),flush=True)
base='https://content.publishing.riotgames.com/publishing-content/v2.0/public/channel/riftbound_website/list/riftbound_gallery_cards?locale=en_US&limit=200&from='
first=get(base+'0');total=first['metadata']['totalItems']
with ThreadPoolExecutor(max_workers=4) as ex:pages=[first]+list(ex.map(lambda i:get(base+str(i)),range(200,total,200)))
assert all(p['metadata']['totalItems']==total for p in pages)
for i,p in enumerate(pages):write(v/f'gallery-api-{i:02}.json',p)
raw_records=[c for p in pages for c in p['data']];by_id={};duplicates={}
for c in raw_records:
 if c['id'] in by_id:
  assert by_id[c['id']]==c,'Conflicting gallery identity; raw responses preserved for review'
  duplicates[c['id']]=duplicates.get(c['id'],1)+1
 else:by_id[c['id']]=c
rechecks=[];previous_missing=[]
previous=sorted(p for p in (ROOT/'data/official/upstream').glob('*/card-gallery.en-US.json') if p.parent.name<snapshot)
if previous:
 prior=json.loads(previous[-1].read_text());missing={c['id'] for c in prior['cards']}-by_id.keys()
 ranges=set()
 for path in (ROOT/'var/official'/previous[-1].parent.name/'global').glob('gallery-api-*.json'):
  page=json.loads(path.read_text())
  if any(c['id'] in missing for c in page['data']):
   ranges.update(range(page['metadata']['from'],page['metadata']['from']+page['metadata']['limit'],100))
 if missing and not ranges:ranges.update(range(0,total,100))
 with ThreadPoolExecutor(max_workers=4) as ex:
  for offset,page in zip(sorted(ranges),ex.map(lambda i:get(base.replace('limit=200','limit=100')+str(i)),sorted(ranges))):
   assert page['metadata']['totalItems']==total,'Gallery drift while rechecking omitted identities'
   write(v/f'gallery-recheck-{offset:04}.json',page);added=[]
   for c in page['data']:
    if c['id'] in by_id:assert by_id[c['id']]==c,'Conflicting rechecked gallery identity; raw response preserved'
    else:by_id[c['id']]=c;added.append(c['id'])
   rechecks.append(dict(offset=offset,limit=100,returned=len(page['data']),newIds=added))
 previous_missing=sorted(missing-by_id.keys())
records=list(by_id.values())
with urlopen(Request('https://playriftbound.com/en-us/card-gallery/',headers={'User-Agent':'Mozilla/5.0'}),timeout=45) as response:html=response.read().decode()
(v/'card-gallery.html').write_text(html)
p=json.loads(re.search(r'<script id="__NEXT_DATA__" type="application/json">(.*?)</script>',html).group(1));write(v/'card-gallery.next.json',p)
gallery=next(b for b in p['props']['pageProps']['page']['blades'] if b['type']=='riftboundCardGallery')
ssr=gallery['cards']['items'];ssr_ids={c['id'] for c in ssr}
write(d/'card-gallery.en-US.json',dict(source='https://playriftbound.com/en-us/card-gallery/',api=base,fetchedAt=snapshot,reportedTotal=total,rawReturnedEntries=len(raw_records),identicalDuplicateIds=duplicates,rechecks=rechecks,previousUnreturnedIds=previous_missing,returnedEntries=len(records),reportedMinusReturned=total-len(records),ssrRawEntries=len(ssr),ssrDistinctIds=len(ssr_ids),pagination=[dict(metadata=p['metadata'],count=len(p['data'])) for p in pages],sets=gallery['sets']['items'],cards=records,warning='Identical duplicate IDs are reported separately and raw responses are retained. Previously observed identities are rechecked at narrower page ranges; prior entries are never silently imported as current. A difference between reported total and distinct returned entries is an unresolved source coverage gap, not audited cards.'))
print('EN gallery',total,'reported',len(records),'returned','SSR raw',len(ssr),'distinct',len(ssr_ids),'previous unreturned',len(previous_missing),flush=True)
