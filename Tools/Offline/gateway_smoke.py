import json
import os
from pathlib import Path
import sys
import threading
import urllib.error
import urllib.request
sys.path.insert(0,str(Path(__file__).resolve().parents[2]/'Gateway'))
from server import Gateway,Provider,Handler,ThreadingHTTPServer
from test_gateway import fixture
os.environ['FACTORY_PROVIDER']='fake'
server=ThreadingHTTPServer(('127.0.0.1',0),Handler)
server.gateway=Gateway(Provider(),'smoke-token')
thread=threading.Thread(target=server.serve_forever,daemon=True);thread.start()
url=f'http://127.0.0.1:{server.server_port}/factory/decide'
try:
    body=json.dumps(fixture()).encode()
    request=urllib.request.Request(url,body,{'Content-Type':'application/json','Authorization':'Bearer smoke-token'})
    with urllib.request.urlopen(request,timeout=3) as response:
        result=json.load(response)
        assert response.status==200 and result['request_id']=='r' and len(result['actions'])==1
    try:urllib.request.urlopen(urllib.request.Request(url,body,{'Content-Type':'application/json'}),timeout=3)
    except urllib.error.HTTPError as e:assert e.code==401
    else:raise AssertionError('unauthorized request accepted')
    print('PASS gateway real HTTP fake-provider 200 and unauthorized 401')
finally:server.shutdown();server.server_close();thread.join(timeout=3)
