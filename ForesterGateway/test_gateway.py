import copy,json,os,unittest
from unittest.mock import patch
import server

def obs(wave=1):return dict(schema_version=1,match_id='test',epoch=1,request_id='test-wave',wave_index=wave,integrity=20,pp=120,board=dict(columns=16,rows=12,cell_size=2),defenders=[],route_summaries=[],previous_outcomes=[],legal_spawn_routes=[dict(spawn_id='S_NORTH',route_id='NORTH',goal_id='G_FOREST')],enemy_catalog=[],allowed_conditions=[],wave_bounds=dict(budget=12,max_count=12,schedule_window_seconds=24))
class Tests(unittest.TestCase):
 def setUp(self):self.provider=server.Provider();self.gateway=server.Gateway(self.provider,'test-only')
 def test_upstream_http_errors_are_actionable_and_do_not_leak_body(self):
  import urllib.error,io
  with patch.dict(os.environ,{'FORESTER_PROVIDER':'apertus_compatible','APERTUS_ENDPOINT':'https://api.inference.cscs.ch/v1/chat/completions','APERTUS_ALLOWED_HOSTS':'api.inference.cscs.ch'}):
   provider=server.Provider()
  for status,label in [(401,'API key rejected'),(403,'access denied'),(404,'not found'),(429,'quota'),(400,'rejected'),(503,'service error')]:
   error=urllib.error.HTTPError(provider.endpoint,status,'error',{},io.BytesIO(b'secret-provider-body'))
   with self.subTest(status=status),patch('server.urllib.request.OpenerDirector.open',side_effect=error):
    with self.assertRaises(server.GatewayError) as caught:provider.decide(obs())
    self.assertEqual(caught.exception.status,502)
    self.assertIn(label,caught.exception.reason)
    self.assertIn(str(status),caught.exception.reason)
    self.assertNotIn('secret',caught.exception.reason)
 def test_live_prompt_contains_wave_constraints_without_sample_wave(self):
  import io
  with patch.dict(os.environ,{'FORESTER_PROVIDER':'apertus_compatible','APERTUS_ENDPOINT':'https://api.inference.cscs.ch/v1/chat/completions','APERTUS_ALLOWED_HOSTS':'api.inference.cscs.ch'}):provider=server.Provider()
  for wave in (1,2,3):
   o=obs(wave);answer=server.Provider().decide(o)
   response=io.BytesIO(json.dumps({'choices':[{'message':{'content':json.dumps(answer)}}]}).encode())
   with patch('server.urllib.request.OpenerDirector.open',return_value=response) as mocked:provider.decide(o)
   request=mocked.call_args.args[0];body=json.loads(request.data)
   prompt=body['messages'][0]['content']
   self.assertIn('Service-owned legality constraints',prompt)
   self.assertIn('"budget": '+str([12,22,32][wave-1]),prompt)
   self.assertNotIn('Northern pressure: steady opening wave',prompt)
   self.assertEqual(json.loads(body['messages'][1]['content'])['wave_index'],wave)
 def test_schedule_failure_reports_exact_group_and_time(self):
  p=self.provider.decide(obs());p['groups'].append(copy.deepcopy(p['groups'][0]))
  with self.assertRaisesRegex(ValueError,'group 1: duplicate spawn on S_NORTH at 0s'):server.plan(p,obs())
  p=self.provider.decide(obs());p['groups'][0]['start_seconds']=24
  with self.assertRaisesRegex(ValueError,'spawn at 26s exceeds window 24s'):server.plan(p,obs())
 def test_one_correction_preserves_validation_and_time_budget(self):
  provider=server.Provider();provider.mode='apertus_compatible'
  valid=self.provider.decide(obs());bad=copy.deepcopy(valid);bad['groups'].append(copy.deepcopy(bad['groups'][0]))
  with patch.object(provider,'decide',side_effect=[bad,valid]) as decide:
   result=server.Gateway(provider,'test-only').handle('Bearer test-only',json.dumps(obs()).encode())
   self.assertEqual(result,valid);self.assertEqual(decide.call_count,2)
   self.assertLessEqual(decide.call_args.kwargs['timeout'],provider.timeout)
   self.assertIn('duplicate spawn',decide.call_args.kwargs['correction']['reason'])
  with patch.object(provider,'decide',side_effect=[bad,bad]) as decide:
   with self.assertRaises(server.GatewayError):server.Gateway(provider,'test-only').handle('Bearer test-only',json.dumps(obs()).encode())
   self.assertEqual(decide.call_count,2)
 def test_no_retry_after_time_budget_exhausted(self):
  provider=server.Provider();provider.mode='apertus_compatible'
  bad=self.provider.decide(obs());bad['groups'][0]['start_seconds']=24
  with patch.object(provider,'decide',return_value=bad) as decide,patch('server.time.monotonic',side_effect=[0,0,7,7]):
   with self.assertRaises(server.GatewayError):server.Gateway(provider,'test-only').handle('Bearer test-only',json.dumps(obs()).encode())
   self.assertEqual(decide.call_count,1)
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
  self.assertIn('missing field',e.exception.reason)

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
