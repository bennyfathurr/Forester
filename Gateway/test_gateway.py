import copy
import json
import unittest
from unittest.mock import patch
from server import Gateway,GatewayError,Provider,observation,plan

def fixture():
    return {'schema_version':1,'match_id':'match','epoch':1,'request_id':'r','issued_tick':0,'expires_tick':100,'oil':60,'factory_cooldown_ticks':0,'seconds_remaining':360,'refinery_hp':1000,'lanes':[{'lane':i,'gate_hp':300,'villagers':[],'robots':[],'slots':[{'id':f'L{i}_S{j}','state':'Empty','revision':0} for j in (1,2)]} for i in (1,2,3)],'legal_options':[{'action':'build','unit':'bolt_turret','lane':1,'slot':'L1_S1','cost':50},{'action':'deploy','unit':'security_robot','lane':2,'slot':'','cost':25}]}
class Tests(unittest.TestCase):
    def setUp(self):
        with patch.dict('os.environ',{'FACTORY_PROVIDER':'fake'}):self.g=Gateway(Provider(),'test-token',rate=2)
    def call(self,o=None,auth='Bearer test-token'):
        return self.g.handle(auth,json.dumps(o or fixture()).encode())
    def status(self,code,fn):
        with self.assertRaises(GatewayError) as e:fn()
        self.assertEqual(code,e.exception.status)
    def test_valid_fake(self):
        p=self.call();self.assertEqual('r',p['request_id']);self.assertEqual(1,len(p['actions']))
    def test_auth(self):self.status(401,lambda:self.call(auth=''))
    def test_rate(self):self.call();self.call();self.status(429,self.call)
    def test_body_limit(self):self.status(413,lambda:self.g.handle('Bearer test-token',b' '*32769))
    def test_prompt_injection(self):
        o=fixture();o['system_prompt']='injected';self.status(400,lambda:self.call(o))
    def test_bad_json(self):self.status(400,lambda:self.g.handle('Bearer test-token',b'{bad'))
    def test_duplicate_json(self):self.status(400,lambda:self.g.handle('Bearer test-token',b'{"schema_version":1,"schema_version":1}'))
    def test_wait(self):
        o=fixture();o['legal_options']=[];self.assertEqual([],self.call(o)['actions'])
    def test_budget(self):
        o=fixture();p={'schema_version':1,'request_id':'r','actions':[{'type':'build','unit':'bolt_turret','lane':1,'slot':'L1_S1'},{'type':'deploy','unit':'security_robot','lane':2,'slot':''}],'announcement':''}
        with self.assertRaises(ValueError):plan(p,o)
    def test_provider_format(self):
        self.g.provider.decide=lambda o:{'extra':1};self.status(502,self.call)
    def test_provider_timeout(self):
        def fail(o):raise GatewayError(504,'provider timeout')
        self.g.provider.decide=fail;self.status(504,self.call)
    def test_bad_lane(self):
        o=fixture();o['legal_options'][0]['lane']=4;self.status(400,lambda:self.call(o))
    def test_wrong_request(self):
        p=self.call();p['request_id']='wrong'
        with self.assertRaises(ValueError):plan(p,fixture())
if __name__=='__main__':unittest.main()
