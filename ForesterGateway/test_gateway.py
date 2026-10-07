import copy,json,os,unittest
from unittest.mock import patch
import server

def obs(wave=1):return dict(schema_version=1,match_id='test',epoch=1,request_id='test-wave',wave_index=wave,integrity=20,pp=120,board=dict(columns=16,rows=12,cell_size=2),defenders=[],route_summaries=[],previous_outcomes=[],legal_spawn_routes=[dict(spawn_id='S_NORTH',route_id='NORTH',goal_id='G_FOREST')],enemy_catalog=[],allowed_conditions=[],wave_bounds=dict(budget=12,max_count=12,schedule_window_seconds=24))
class Tests(unittest.TestCase):
 def setUp(self):self.provider=server.Provider();self.gateway=server.Gateway(self.provider,'test-only')
 def test_fake_plan(self):self.assertEqual(self.gateway.handle('Bearer test-only',json.dumps(obs()).encode())['groups'][0]['count'],4)
 def test_auth(self):
  with self.assertRaises(server.GatewayError) as e:self.gateway.handle('',b'{}')
  self.assertEqual(e.exception.status,401)
 def test_rate(self):
  self.gateway.rate=1;self.gateway.handle('Bearer test-only',json.dumps(obs()).encode())
  with self.assertRaises(server.GatewayError) as e:self.gateway.handle('Bearer test-only',json.dumps(obs()).encode())
  self.assertEqual(e.exception.status,429)
 def test_duplicates(self):
  with self.assertRaises(ValueError):server.strict_json('{"x":1,"x":2}')
 def test_nonfinite(self):
  with self.assertRaises(ValueError):server.strict_json('{"x":NaN}')
 def test_extra_observation(self):
  o=obs();o['endpoint']='http://evil'
  with self.assertRaises(ValueError):server.observation(o)
 def test_budget(self):
  o=obs();p=self.provider.decide(o);p['groups'][0]['count']=13
  with self.assertRaises(ValueError):server.plan(p,o)
 def test_density(self):
  o=obs();p=self.provider.decide(o);p['groups'].append(copy.deepcopy(p['groups'][0]))
  with self.assertRaises(ValueError):server.plan(p,o)
 def test_window(self):
  o=obs();p=self.provider.decide(o);p['groups'][0]['start_seconds']=24
  with self.assertRaises(ValueError):server.plan(p,o)
 def test_binding(self):
  o=obs();p=self.provider.decide(o);p['request_id']='old'
  with self.assertRaises(ValueError):server.plan(p,o)
 def test_condition(self):
  o=obs();p=self.provider.decide(o);p['conditions']=[dict(condition_id='gust_front',start_seconds=10)]
  with self.assertRaises(ValueError):server.plan(p,o)
 def test_extra_plan(self):
  o=obs();p=self.provider.decide(o);p['code']='arbitrary'
  with self.assertRaises(ValueError):server.plan(p,o)
 def test_integer_bounds(self):
  o=obs();p=self.provider.decide(o);p['groups'][0]['interval_seconds']=0
  with self.assertRaises(ValueError):server.plan(p,o)
 def test_empty_plan(self):
  o=obs();p=self.provider.decide(o);p['groups']=[]
  with self.assertRaises(ValueError):server.plan(p,o)
 def test_host_allowlist(self):
  with patch.dict(os.environ,{'FORESTER_PROVIDER':'apertus_compatible','APERTUS_ENDPOINT':'https://evil.example/v1/chat/completions','APERTUS_ALLOWED_HOSTS':'trusted.example'}):
   with self.assertRaises(RuntimeError):server.Provider()
 def test_no_substitution(self):
  with patch.dict(os.environ,{'FORESTER_PROVIDER':'openai'}):
   with self.assertRaises(RuntimeError):server.Provider()
 def test_bad_provider_whole_plan(self):
  class Bad:
   def decide(self,o):return {'code':'oops'}
  gateway=server.Gateway(Bad(),'test-only')
  with self.assertRaises(server.GatewayError) as e:gateway.handle('Bearer test-only',json.dumps(obs()).encode())
  self.assertEqual(e.exception.status,502)

class HttpTests(unittest.TestCase):
 def test_http_fake_contract_and_unauthorized(self):
  import threading,urllib.request,urllib.error
  service=server.ThreadingHTTPServer(('127.0.0.1',0),server.Handler)
  service.gateway=server.Gateway(server.Provider(),'transient-test-session')
  thread=threading.Thread(target=service.serve_forever,daemon=True);thread.start()
  try:
   url='http://127.0.0.1:'+str(service.server_port)+'/forester/plan';raw=json.dumps(obs()).encode()
   request=urllib.request.Request(url,raw,{'Content-Type':'application/json','Authorization':'Bearer transient-test-session'})
   with urllib.request.urlopen(request,timeout=2) as response:p=server.strict_json(response.read())
   self.assertEqual(server.plan(p,obs())['request_id'],'test-wave')
   with self.assertRaises(urllib.error.HTTPError) as e:urllib.request.urlopen(urllib.request.Request(url,raw),timeout=2)
   self.assertEqual(e.exception.code,401)
  finally:service.shutdown();service.server_close();thread.join()

if __name__=='__main__':unittest.main()
