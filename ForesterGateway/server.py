"""Forester authenticated gateway. Python standard library; hosted provider is Apertus-compatible."""
import collections, hmac, json, logging, os, socket, threading, time
from pathlib import Path
from urllib.parse import urlparse
import urllib.request, urllib.error
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
BASE=Path(__file__).resolve().parent
SCHEMA=json.loads((BASE/'threat_plan.schema.json').read_text())
PROMPT=(BASE/'system_prompt.txt').read_text()
MAX_BODY=32768
class GatewayError(Exception):
    def __init__(self, status, reason):
        self.status, self.reason = status, reason

def strict_json(raw):
    def unique(pairs):
        result = {}
        for key, value in pairs:
            if key in result:
                raise ValueError('duplicate field')
            result[key] = value
        return result
    return json.loads(raw, object_pairs_hook=unique, parse_constant=lambda _: (_ for _ in ()).throw(ValueError('nonfinite')))

def shape(value, schema):
    types = {'object': dict, 'array': list, 'string': str, 'integer': int}
    kind = schema.get('type')
    if kind and type(value) is not types[kind]:
        raise ValueError('wrong JSON type')
    if 'enum' in schema and value not in schema['enum']:
        raise ValueError('invalid enum')
    if kind == 'object':
        if set(schema.get('required', [])) - value.keys():
            raise ValueError('missing field')
        if not schema.get('additionalProperties', True) and value.keys() - schema['properties'].keys():
            raise ValueError('extra field')
        for k, v in value.items():
            if k in schema.get('properties', {}):
                shape(v, schema['properties'][k])
    if kind == 'array':
        if len(value) > schema.get('maxItems', 1000):
            raise ValueError('too many items')
        for item in value:
            shape(item, schema['items'])
    if kind == 'integer' and not schema.get('minimum', value) <= value <= schema.get('maximum', value):
        raise ValueError('integer bounds')
    if kind == 'array' and len(value) < schema.get('minItems', 0):
        raise ValueError('too few items')
    if kind == 'string' and len(value) < schema.get('minLength', 0):
        raise ValueError('string too short')
    if kind == 'string' and len(value) > schema.get('maxLength', 10000):
        raise ValueError('string too long')


def observation(o):
    fields={'schema_version','match_id','epoch','request_id','wave_index','integrity','pp','board','defenders','route_summaries','previous_outcomes','legal_spawn_routes','enemy_catalog','allowed_conditions','wave_bounds'}
    if type(o) is not dict or set(o)!=fields or o['schema_version']!=1:
        raise ValueError('observation fields')
    for f in ('match_id','request_id'):
        if type(o[f]) is not str or not 0<len(o[f])<=80:raise ValueError('binding')
    for f in ('epoch','wave_index','integrity','pp'):
        if type(o[f]) is not int or o[f]<0:raise ValueError('metadata')
    if not 1<=o['wave_index']<=3 or o['pp']>200 or o['integrity']>20:raise ValueError('match bounds')
    for f,cap in [('defenders',192),('route_summaries',16),('previous_outcomes',3),('legal_spawn_routes',16),('enemy_catalog',16),('allowed_conditions',1)]:
        if type(o[f]) is not list or len(o[f])>cap:raise ValueError('observation arrays')
    if type(o['board']) is not dict or type(o['wave_bounds']) is not dict:raise ValueError('observation bounds')
    # Initial-level allowlists are owned by this service, never supplied as provider URLs/prompts.
    return o

def plan(p,o):
    shape(p,SCHEMA)
    if p['request_id']!=o['request_id'] or p['wave_index']!=o['wave_index']:raise ValueError('binding mismatch')
    wave=o['wave_index'];budget=[12,22,32][wave-1];max_count=[12,18,24][wave-1];window=[24,36,48][wave-1]
    cost=count=0;density=set()
    for g in p['groups']:
        if g['spawn_id']!='S_'+g['route_id'] or wave==1 and (g['route_id']!='NORTH' or g['enemy_id']=='wind_runner'):raise ValueError('wave allowlist')
        for i in range(g['count']):
            second=g['start_seconds']+i*g['interval_seconds'];key=(g['spawn_id'],second)
            if second>window or key in density:raise ValueError('schedule/density')
            density.add(key);count+=1;cost+={'emberling':1,'wind_runner':2,'brush_cluster':3}[g['enemy_id']]
    if p['conditions']:
        if wave!=3:raise ValueError('condition allowlist')
        cost+=4
    if cost>budget or count>max_count:raise ValueError('budget/count')
    if any(ord(c)<32 or c in '<>' for c in p['announcement']):raise ValueError('announcement')
    return p

class Provider:
    def __init__(self):
        self.mode=os.environ.get('FORESTER_PROVIDER','fake')
        self.model=os.environ.get('APERTUS_MODEL','swiss-ai/Apertus-8B-Instruct-2509')
        self.endpoint=os.environ.get('APERTUS_ENDPOINT','')
        self.key=os.environ.get('APERTUS_API_KEY','')
        self.timeout=min(7,max(.1,float(os.environ.get('PROVIDER_TIMEOUT','6'))))
        self.schema=os.environ.get('APERTUS_VERIFIED_SCHEMA','0')=='1'
        if self.mode not in {'fake','apertus_compatible'}:raise RuntimeError('Choose fake or apertus_compatible; no model-family substitution')
        if self.mode!='fake':
            parsed=urlparse(self.endpoint);allow=set(os.environ.get('APERTUS_ALLOWED_HOSTS','').split(','))
            if not self.model or parsed.hostname not in allow or parsed.username or parsed.fragment or not (parsed.scheme=='https' or parsed.scheme=='http' and parsed.hostname in {'localhost','127.0.0.1','::1'}):raise RuntimeError('Configure a fixed trusted endpoint, model and host allowlist')
    def decide(self,o):
        if self.mode=='fake':
            return {'schema_version':1,'request_id':o['request_id'],'wave_index':o['wave_index'],'groups':[{'enemy_id':'emberling','spawn_id':'S_NORTH','route_id':'NORTH','count':4,'start_seconds':0,'interval_seconds':2}],'conditions':[],'announcement':'Gateway fake provider: northern approach.'}
        body={'model':self.model,'stream':False,'messages':[{'role':'system','content':PROMPT+'\nExact JSON schema:\n'+json.dumps(SCHEMA)},{'role':'user','content':json.dumps(o,separators=(',',':'))}]}
        if self.schema:body['response_format']={'type':'json_schema','json_schema':{'name':'forester_threat_plan','strict':True,'schema':SCHEMA}}
        if os.environ.get('APERTUS_SEND_TEMPERATURE')=='1':body['temperature']=.2
        if os.environ.get('APERTUS_SEND_MAX_TOKENS')=='1':body['max_tokens']=700
        headers={'Content-Type':'application/json'}
        if self.key:headers['Authorization']='Bearer '+self.key
        request=urllib.request.Request(self.endpoint,json.dumps(body).encode(),headers)
        # Reject redirects, which could otherwise bypass the configured hostname allowlist.
        class NoRedirect(urllib.request.HTTPRedirectHandler):
            def redirect_request(self,*args,**kwargs):return None
        try:
            with urllib.request.build_opener(NoRedirect()).open(request,timeout=self.timeout) as response:
                raw=response.read(65537)
                if len(raw)>65536:raise ValueError('response size')
                envelope=strict_json(raw)
            message=envelope['choices'][0]['message']
            if message.get('refusal') or not isinstance(message.get('content'),str):raise ValueError('refusal/content')
            return strict_json(message['content'])
        except (TimeoutError,socket.timeout):raise GatewayError(504,'provider timeout')
        except urllib.error.URLError as e:
            if isinstance(e.reason,(TimeoutError,socket.timeout)):raise GatewayError(504,'provider timeout')
            raise GatewayError(502,'provider unavailable')
        except (ValueError,TypeError,KeyError,IndexError):raise GatewayError(502,'provider format')

class Gateway:
    def __init__(self,provider,token,rate=20):
        if not token or token=='replace-me':raise RuntimeError('Set FORESTER_SESSION_TOKEN')
        self.provider,self.token,self.rate=provider,token,rate;self.window=collections.deque();self.lock=threading.Lock()
    def handle(self,auth,raw):
        if not hmac.compare_digest(auth,'Bearer '+self.token):raise GatewayError(401,'unauthorized')
        if len(raw)>MAX_BODY:raise GatewayError(413,'body size')
        with self.lock:
            now=time.monotonic()
            while self.window and now-self.window[0]>60:self.window.popleft()
            if len(self.window)>=self.rate:raise GatewayError(429,'rate limit')
            self.window.append(now)
        try:o=observation(strict_json(raw))
        except (ValueError,TypeError,KeyError,RecursionError):raise GatewayError(400,'invalid observation')
        started=time.monotonic()
        try:return plan(self.provider.decide(o),o)
        except (ValueError,TypeError,KeyError,RecursionError):raise GatewayError(502,'invalid provider plan')
        finally:logging.info('request=%s epoch=%s latency=%.3f',o['request_id'],o['epoch'],time.monotonic()-started)

class Handler(BaseHTTPRequestHandler):
    def setup(self):super().setup();self.connection.settimeout(7)
    def do_POST(self):
        status=200
        try:
            if self.path!='/forester/plan':raise GatewayError(404,'route')
            try:size=int(self.headers.get('Content-Length','0'))
            except ValueError:raise GatewayError(400,'body length')
            if not 0<size<=MAX_BODY:raise GatewayError(413,'body size')
            raw=self.rfile.read(size)
            if len(raw)!=size:raise GatewayError(400,'truncated body')
            result=self.server.gateway.handle(self.headers.get('Authorization',''),raw)
        except GatewayError as e:status,result=e.status,{'error':e.reason}
        except (TimeoutError,socket.timeout):status,result=408,{'error':'request timeout'}
        except Exception:status,result=500,{'error':'internal error'}
        raw=json.dumps(result,separators=(',',':')).encode();self.send_response(status);self.send_header('Content-Type','application/json');self.send_header('Content-Length',str(len(raw)));self.end_headers();self.wfile.write(raw)
    def log_message(self,*args):pass

def main():
    logging.basicConfig(level=logging.INFO,format='%(message)s')
    server=ThreadingHTTPServer(('127.0.0.1',int(os.environ.get('PORT','8081'))),Handler)
    server.gateway=Gateway(Provider(),os.environ.get('FORESTER_SESSION_TOKEN',''));server.serve_forever()
if __name__=='__main__':main()
