#!/usr/bin/env python3
from pathlib import Path
from urllib.request import urlopen, Request
from urllib.parse import urljoin,urlparse
from html.parser import HTMLParser
from concurrent.futures import ThreadPoolExecutor
import argparse,json,re,hashlib,subprocess
from html import escape
from datetime import date
from datetime import datetime,timezone
parser=argparse.ArgumentParser(description='Archive official global rules articles and linked PDFs without certifying semantic coverage.')
parser.add_argument('--date',required=True)
args=parser.parse_args(); snapshot=date.fromisoformat(args.date).isoformat()
ROOT=Path(__file__).resolve().parents[2]; dest=ROOT/'data/official/upstream'/snapshot; cache=ROOT/'var/official'/snapshot/'global'
if (dest/'global-rules-sources.json').exists():raise SystemExit('Snapshot exists; preserve reviewed evidence.')
dest.mkdir(parents=True,exist_ok=True);cache.mkdir(parents=True,exist_ok=True)
class Body(HTMLParser):
 def __init__(self):super().__init__();self.links=[];self.parts=[]
 def handle_starttag(self,t,a):
  d=dict(a)
  if t=='a' and d.get('href'):self.links.append(d['href'])
  if t in ('p','h1','h2','h3','h4','li','tr','blockquote','br'):self.parts.append('\n\n' if t!='br' else '\n')
  if t=='img':self.parts.append('[IMAGE: '+d.get('alt','visual review required')+']')
 def handle_data(self,d):self.parts.append(d)
def article_body(page):
 parts=[]
 for blade in page['blades']:
  kind=blade.get('type')
  if kind=='articleRichText':parts.append(blade['richText']['body'])
  elif kind=='articleRichTextAccordion':
   parts.extend('<h2>'+escape(v)+'</h2>' for v in blade.get('header',{}).values() if isinstance(v,str))
   for group in blade['groups']:
    parts.append('<h3>'+escape(group['label'])+'</h3>')
    parts.append(group['content']['body'])
 return '\n'.join(parts)
def fetch(u):
 key=hashlib.sha256(u.encode()).hexdigest()[:16]; path=cache/key
 req=Request(u,headers={'User-Agent':'Mozilla/5.0'})
 with urlopen(req,timeout=60) as r:raw=r.read();final=r.url
 path.write_bytes(raw)
 rec=dict(url=u,resolvedUrl=final,fetchedAt=datetime.now(timezone.utc).isoformat(),sha256=hashlib.sha256(raw).hexdigest(),localPath=str(path.relative_to(ROOT)),reviewStatus='NOT_REVIEWED')
 if raw.startswith(b'%PDF'):
  out=path.with_suffix('.txt');subprocess.run(['pdftotext','-layout',str(path),str(out)],check=True)
  rec.update(kind='pdf',text=out.read_text());links=[]
 else:
  s=raw.decode();j=json.loads(re.search(r'<script id="__NEXT_DATA__" type="application/json">(.*?)</script>',s).group(1));p=j['props']['pageProps']['page']
  body=article_body(p)
  if not body:raise ValueError('No article body: '+u)
  parser=Body();parser.feed(body);links=parser.links
  rec.update(kind='article',title=p['title'],published=p.get('displayedPublishDate'),revision=p.get('analytics',{}).get('rev'),bodyHtml=body,text=''.join(parser.parts).strip())
 return rec,links
hub='https://playriftbound.com/en-us/rules-hub/'
seeds=[hub,'https://playriftbound.com/en-us/news/rules-and-releases/vendetta-rules-faq-and-clarifications/','https://playriftbound.com/en-us/news/announcements/october-2026-tournament-rules-changelog/']
seeds += ['https://playriftbound.com/en-us/news/rules-and-releases/'+name+'/' for name in ['riftbound-spiritforged-faq','unleashed-rules-faq-and-clarifications','riftbound-origins-faq']]
seen=set();records=[];errors=[]
while seeds:
 batch=[u for u in seeds if u not in seen];seeds=[];seen.update(batch)
 with ThreadPoolExecutor(max_workers=4) as ex:
  futures=[(u,ex.submit(fetch,u)) for u in batch]
  for u,f in futures:
   try:rec,links=f.result()
   except Exception as e:errors.append(dict(url=u,error=str(e)));print('ERROR',u,str(e),flush=True);continue
   records.append(rec);print('OK',u,len(rec['text']),flush=True)
   for link in links:
    v=urljoin(rec['resolvedUrl'],link).split('#')[0];host=urlparse(v).hostname;path=urlparse(v).path
    if host=='cmsassets.rgpub.io' and path.endswith('.pdf') or host in ('playriftbound.com','riftbound.leagueoflegends.com') and any(x in path for x in ('patch-notes','errata','faq-and-clarifications','rules-faq','rules-changelog')):
     if v not in seen and v not in seeds:seeds.append(v)
 if len(seen)>70:raise ValueError('Unexpectedly wide official source crawl')
(dest/'global-rules-sources.json').write_text(json.dumps(dict(fetchedDate=snapshot,seeds=[hub],records=records,errors=errors,complete=not errors),ensure_ascii=False,indent=2)+'\n')
print('DONE',len(records),'errors',len(errors))
