"""Bounded authenticated factory gateway. Python 3.10+, standard library only."""
import collections
import hmac
import json
import logging
import os
from pathlib import Path
import socket
import threading
import time
import urllib.error
import urllib.request
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

BASE = Path(__file__).resolve().parent
SCHEMA = json.loads((BASE / 'factory_plan.schema.json').read_text())
PROMPT = (BASE / 'system_prompt.txt').read_text()
MAX_BODY = 32768

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
    if kind == 'string' and len(value) > schema.get('maxLength', 10000):
        raise ValueError('string too long')

def observation(o):
    required = {'schema_version','match_id','epoch','request_id','issued_tick','expires_tick','oil','factory_cooldown_ticks','seconds_remaining','refinery_hp','lanes','legal_options'}
    if type(o) is not dict or set(o) != required or o['schema_version'] != 1:
        raise GatewayError(400, 'invalid observation')
    for field in ('epoch','issued_tick','expires_tick','factory_cooldown_ticks','seconds_remaining','refinery_hp'):
        if type(o[field]) is not int or o[field] < 0:
            raise GatewayError(400, 'invalid metadata')
    if o['expires_tick'] <= o['issued_tick'] or type(o['oil']) not in (int,float) or not 0 <= o['oil'] <= 120:
        raise GatewayError(400, 'invalid economy or deadline')
    for field in ('match_id','request_id'):
        if type(o[field]) is not str or not 0 < len(o[field]) <= 100:
            raise GatewayError(400, 'invalid request ID')
    if type(o['lanes']) is not list or len(o['lanes']) != 3 or type(o['legal_options']) is not list or len(o['legal_options']) > 15:
        raise GatewayError(400, 'invalid lanes or options')
    entities = 0
    for i, lane in enumerate(o['lanes'],1):
        if type(lane) is not dict or set(lane) != {'lane','gate_hp','villagers','robots','slots'} or lane['lane'] != i:
            raise GatewayError(400, 'invalid lane')
        if type(lane['gate_hp']) is not int or not 0 <= lane['gate_hp'] <= 300:
            raise GatewayError(400, 'invalid gate')
        for field, units in [('villagers',{'villager_emak','villager_bapak','villager_anak'}),('robots',{'security_robot'})]:
            if type(lane[field]) is not list:
                raise GatewayError(400, 'invalid entities')
            entities += len(lane[field])
            for e in lane[field]:
                if type(e) is not dict or set(e) != {'id','unit','path_distance','hp'} or e['unit'] not in units or type(e['id']) is not int or type(e['hp']) is not int or type(e['path_distance']) not in (int,float) or not 0 <= e['path_distance'] <= 26:
                    raise GatewayError(400, 'invalid entity')
        if type(lane['slots']) is not list or len(lane['slots']) != 2:
            raise GatewayError(400, 'invalid slots')
        for j, s in enumerate(lane['slots'],1):
            if type(s) is not dict or set(s) != {'id','state','revision'} or s['id'] != f'L{i}_S{j}' or s['state'] not in {'Empty','Reserved','Constructing','Active'} or type(s['revision']) is not int or s['revision'] < 0:
                raise GatewayError(400, 'invalid slot')
    if entities > 84:
        raise GatewayError(400, 'entity cap exceeded')
    for option in o['legal_options']:
        if type(option) is not dict or set(option) != {'action','unit','lane','slot','cost'}:
            raise GatewayError(400, 'invalid legal option')
        if type(option['lane']) is not int or option['lane'] not in (1,2,3):
            raise GatewayError(400, 'invalid option lane')
        expected={'security_robot':25,'foam_cannon':40,'bolt_turret':50}
        if option['unit'] not in expected or option['cost'] != expected[option['unit']]:
            raise GatewayError(400, 'invalid option cost')
        if option['unit']=='security_robot':
            if option['action']!='deploy' or option['slot']!='':
                raise GatewayError(400,'invalid robot option')
        elif option['action']!='build' or not any(s['id']==option['slot'] and s['state']=='Empty' for s in o['lanes'][option['lane']-1]['slots']):
            raise GatewayError(400,'invalid machine option')
    return o

def plan(p, o):
    shape(p, SCHEMA)
    if p['request_id'] != o['request_id']:
        raise ValueError('wrong request ID')
    budget, slots = o['oil'], set()
    for a in p['actions']:
        options = [x for x in o['legal_options'] if (x['action'],x['unit'],x['lane'],x['slot']) == (a['type'],a['unit'],a['lane'],a['slot'])]
        if not options:
            raise ValueError('not observed legal')
        budget -= options[0]['cost']
        if budget < 0 or (a['slot'] and a['slot'] in slots):
            raise ValueError('budget or duplicate slot')
        slots.add(a['slot'])
    return p

class Provider:
    def __init__(self):
        self.mode=os.environ.get('FACTORY_PROVIDER','fake')
        self.model=os.environ.get('OPENAI_MODEL','')
        self.key=os.environ.get('OPENAI_API_KEY','')
        self.timeout=min(3.5,max(.1,float(os.environ.get('PROVIDER_TIMEOUT','3'))))
        if self.mode not in {'fake','openai'}:
            raise RuntimeError('FACTORY_PROVIDER must be fake or openai')
        if self.mode=='openai' and (not self.key or not self.model):
            raise RuntimeError('Server-side OPENAI_API_KEY and OPENAI_MODEL required')
    def decide(self,o):
        if self.mode=='fake':
            option=next((x for x in o['legal_options'] if x['cost']<=o['oil']),None)
            return {'schema_version':1,'request_id':o['request_id'],'actions':([] if option is None else [{'type':option['action'],'unit':option['unit'],'lane':option['lane'],'slot':option['slot']}]),'announcement':'Safety inspection in progress.'}
        body={'model':self.model,'messages':[{'role':'system','content':PROMPT+'\n'+json.dumps(SCHEMA)},{'role':'user','content':json.dumps(o,separators=(',',':'))}],'response_format':{'type':'json_schema','json_schema':{'name':'factory_plan','strict':True,'schema':SCHEMA}},'max_completion_tokens':384}
        # Fixed allowlisted endpoint; client cannot supply prompt, key, model or URL.
        req=urllib.request.Request('https://api.openai.com/v1/chat/completions',json.dumps(body).encode(),{'Content-Type':'application/json','Authorization':'Bearer '+self.key})
        try:
            with urllib.request.urlopen(req,timeout=self.timeout) as response:
                raw=response.read(65537)
                if len(raw)>65536: raise ValueError('provider body too large')
                envelope=strict_json(raw)
            if envelope['choices'][0]['message'].get('refusal'): raise ValueError('provider refusal')
            result=strict_json(envelope['choices'][0]['message']['content'])
            logging.info('provider usage=%s',envelope.get('usage',{}))
            return result
        except (TimeoutError,socket.timeout): raise GatewayError(504,'provider timeout')
        except urllib.error.URLError as e:
            if isinstance(e.reason,(TimeoutError,socket.timeout)): raise GatewayError(504,'provider timeout')
            raise GatewayError(502,'provider failure')
        except (ValueError,KeyError,IndexError): raise GatewayError(502,'provider format failure')

class Gateway:
    def __init__(self,provider,session_token,rate=20):
        if not session_token or session_token=='replace-with-session-token':
            raise RuntimeError('Configure a gateway session token')
        self.provider,self.token,self.rate=provider,session_token,rate
        self.lock=threading.Lock();self.window=collections.deque()
    def handle(self,auth,raw):
        if not hmac.compare_digest(auth,'Bearer '+self.token):raise GatewayError(401,'unauthorized')
        if len(raw)>MAX_BODY:raise GatewayError(413,'body too large')
        with self.lock:
            now=time.monotonic()
            while self.window and now-self.window[0]>60:self.window.popleft()
            if len(self.window)>=self.rate:raise GatewayError(429,'rate limited')
            self.window.append(now)
        try:o=observation(strict_json(raw))
        except (ValueError,TypeError,KeyError,RecursionError):raise GatewayError(400,'invalid request')
        start=time.monotonic()
        try:return plan(self.provider.decide(o),o)
        except (ValueError,TypeError,KeyError):raise GatewayError(502,'provider format failure')
        finally:logging.info('request=%s epoch=%s latency=%.3f',o['request_id'],o['epoch'],time.monotonic()-start)

class Handler(BaseHTTPRequestHandler):
    def setup(self):
        super().setup();self.connection.settimeout(5)
    def do_POST(self):
        status=200
        try:
            if self.path!='/factory/decide':raise GatewayError(404,'not found')
            try:size=int(self.headers.get('Content-Length','0'))
            except ValueError:raise GatewayError(400,'invalid body size')
            if not 0<size<=MAX_BODY:raise GatewayError(413,'body size limit')
            result=self.server.gateway.handle(self.headers.get('Authorization',''),self.rfile.read(size))
        except GatewayError as e:status,result=e.status,{'error':e.reason}
        except Exception:status,result=500,{'error':'internal error'}
        raw=json.dumps(result,separators=(',',':')).encode();self.send_response(status);self.send_header('Content-Type','application/json');self.send_header('Content-Length',str(len(raw)));self.end_headers();self.wfile.write(raw)
    def log_message(self,*args):pass

def main():
    logging.basicConfig(level=logging.INFO,format='%(message)s')
    gateway=Gateway(Provider(),os.environ.get('FACTORY_SESSION_TOKEN',''))
    server=ThreadingHTTPServer(('127.0.0.1',int(os.environ.get('PORT','8080'))),Handler);server.gateway=gateway
    logging.info('Factory gateway listening on loopback; put behind HTTPS reverse proxy for Unity hosted mode.')
    server.serve_forever()
if __name__=='__main__':main()
