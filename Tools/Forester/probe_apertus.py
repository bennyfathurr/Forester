"""Read-only connection probe: chat first, exact plan second. No model download."""
import argparse,json,sys,time,urllib.request
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parents[2]/'ForesterGateway'))
import server

def call(url,body,timeout):
    request=urllib.request.Request(url,json.dumps(body).encode(),{'Content-Type':'application/json'})
    started=time.monotonic()
    with urllib.request.urlopen(request,timeout=timeout) as response:
        raw=response.read(65537)
        if len(raw)>65536:raise ValueError('response too large')
        envelope=server.strict_json(raw)
    message=envelope['choices'][0]['message']
    if message.get('refusal') or not isinstance(message.get('content'),str) or not message['content']:raise ValueError('missing content/refusal')
    return message['content'],time.monotonic()-started

def observation():
    return dict(schema_version=1,match_id='connection-probe',epoch=1,request_id='probe-wave1',wave_index=1,integrity=20,pp=95,
      board=dict(columns=16,rows=12,cell_size=2),defenders=[dict(entity_id=1,definition_id='watch_post',cell_id='C04_R08',column=4,row=8,range=9,behavior='Direct')],
      route_summaries=[dict(route_id='NORTH',length=42,segments=[]),dict(route_id='SOUTH',length=44,segments=[])],previous_outcomes=[],
      legal_spawn_routes=[dict(spawn_id='S_NORTH',route_id='NORTH',goal_id='G_FOREST')],enemy_catalog=[dict(enemy_id='emberling',cost=1,hp=35,speed=2,integrity_loss=1),dict(enemy_id='brush_cluster',cost=3,hp=90,speed=1.3,integrity_loss=2)],allowed_conditions=[],
      wave_bounds=dict(budget=12,max_count=12,max_groups=6,max_per_spawn_per_second=1,schedule_window_seconds=24))

def main():
    parser=argparse.ArgumentParser();parser.add_argument('--endpoint',default='http://127.0.0.1:8000/v1/chat/completions');parser.add_argument('--model',default='swiss-ai/Apertus-8B-Instruct-2509');parser.add_argument('--timeout',type=float,default=8);parser.add_argument('--verified-schema',action='store_true');args=parser.parse_args()
    chat=dict(model=args.model,stream=False,messages=[dict(role='user',content='Reply with the word ready.')])
    content,latency=call(args.endpoint,chat,args.timeout);print('Chat content received; model='+args.model+' latency=%.2fs'%latency)
    o=observation();body=dict(model=args.model,stream=False,messages=[dict(role='system',content=server.PROMPT+'\nExact JSON schema:\n'+json.dumps(server.SCHEMA)),dict(role='user',content=json.dumps(o,separators=(',',':')))])
    if args.verified_schema:body['response_format']=dict(type='json_schema',json_schema=dict(name='forester_threat_plan',strict=True,schema=server.SCHEMA))
    content,latency=call(args.endpoint,body,args.timeout);p=server.plan(server.strict_json(content),o)
    print('Plan accepted; groups=%d latency=%.2fs. Verify complete wave in Unity.'%(len(p['groups']),latency))
if __name__=='__main__':
    try:main()
    except Exception as e:print('Probe failed: '+type(e).__name__,file=sys.stderr);sys.exit(1)
