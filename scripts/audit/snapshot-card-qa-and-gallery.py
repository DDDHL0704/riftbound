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
if (d/'card-gallery.en-US.json').exists() or (d/'card-qa.zh-CN.json').exists():raise SystemExit('Snapshot exists; preserve reviewed sources.')
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
with ThreadPoolExecutor(max_workers=4) as ex:pages=list(ex.map(lambda i:get(u,{**params,'pageNum':i})['result'],range(1,math.ceil(a['total']/100)+1)))
cards=[c for p in pages for c in p['list']]
assert all(p['total']==a['total'] for p in pages) and len(cards)==len({c['id'] for c in cards})==a['total']
assert all({k:v for k,v in c.items() if k!='cardQaList'}=={k:v for k,v in old[c['id']].items() if k!='cardQaList'} for c in cards),'Catalog drift while collecting QA'
qa_snapshot=dict(source=u,fetchedAt=snapshot,catalogSha256=hashlib.sha256((d/'card-catalog.zh-CN.json').read_bytes()).hexdigest(),entries=[dict(id=c['id'],cardNo=c['cardNo'],cardQaList=c.get('cardQaList'),fieldPresent='cardQaList'in c) for c in sorted(cards,key=lambda c:c['id'])],note='Null/empty API QA does not imply no FAQ in official documents.')
print('CN QA',len(cards),'nonempty',sum(bool(c.get('cardQaList')) for c in cards),flush=True)
base='https://content.publishing.riotgames.com/publishing-content/v2.0/public/channel/riftbound_website/list/riftbound_gallery_cards?locale=en_US&limit=200&from='
first=get(base+'0');total=first['metadata']['totalItems']
with ThreadPoolExecutor(max_workers=4) as ex:pages=[first]+list(ex.map(lambda i:get(base+str(i)),range(200,total,200)))
assert all(p['metadata']['totalItems']==total for p in pages)
records=[c for p in pages for c in p['data']];assert len({c['id'] for c in records})==len(records)
for i,p in enumerate(pages):write(v/f'gallery-api-{i:02}.json',p)
with urlopen(Request('https://playriftbound.com/en-us/card-gallery/',headers={'User-Agent':'Mozilla/5.0'}),timeout=45) as response:html=response.read().decode()
(v/'card-gallery.html').write_text(html)
p=json.loads(re.search(r'<script id="__NEXT_DATA__" type="application/json">(.*?)</script>',html).group(1));write(v/'card-gallery.next.json',p)
gallery=next(b for b in p['props']['pageProps']['page']['blades'] if b['type']=='riftboundCardGallery')
write(d/'card-gallery.en-US.json',dict(source='https://playriftbound.com/en-us/card-gallery/',api=base,fetchedAt=snapshot,reportedTotal=total,returnedEntries=len(records),reportedMinusReturned=total-len(records),pagination=[dict(metadata=p['metadata'],count=len(p['data'])) for p in pages],sets=gallery['sets']['items'],cards=records,warning='A difference between reported total and returned entries is an unresolved source coverage gap, not audited cards.'))
write(d/'card-qa.zh-CN.json',qa_snapshot)
print('EN gallery',total,'reported',len(records),'returned','SSR',len(gallery['cards']['items']),flush=True)
