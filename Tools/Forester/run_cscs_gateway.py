#!/usr/bin/env python3
"""Run the existing Forester gateway against CSCS without storing the provider key."""
import argparse
import getpass
import json
import re
import urllib.request
import urllib.error
import os
from pathlib import Path
import runpy
import secrets


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--model', default='swiss-ai/Apertus-v1.5-70B')
    parser.add_argument('--port', type=int, default=8081)
    parser.add_argument('--check', action='store_true', help='Check CSCS key/model access without starting Unity gateway')
    parser.add_argument('--check-inference', action='store_true', help='Send a tiny chat request directly to CSCS (uses inference tokens)')
    args = parser.parse_args()
    if not 1 <= args.port <= 65535:
        parser.error('port must be between 1 and 65535')
    key = os.environ.get('CSCS_INFERENCE_API_KEY') or getpass.getpass('CSCS API key (hidden): ').strip()
    if not key:
        parser.error('a CSCS API key is required')
    if args.check_inference:
        body={'model':args.model,'stream':False,'max_tokens':16,'messages':[{'role':'user','content':'Reply with OK.'}]}
        request=urllib.request.Request('https://api.inference.cscs.ch/v1/chat/completions',json.dumps(body).encode(),{'Authorization':'Bearer '+key,'Content-Type':'application/json'})
        try:
            with urllib.request.urlopen(request,timeout=60) as response:
                raw=response.read(65537)
                if len(raw)>65536:raise ValueError('response too large')
                data=json.loads(raw)
                if not data.get('choices'):raise ValueError('missing choices')
            print('CSCS /chat/completions: HTTP 200; inference succeeded')
            print('Restart the game gateway with this same key and model, then copy its new session token into Unity.')
        except urllib.error.HTTPError as error:
            print('CSCS /chat/completions: HTTP '+str(error.code))
            raw=error.read(4096).decode('utf-8',errors='replace')
            try:
                payload=json.loads(raw)
                detail=payload.get('error',payload.get('message',''))
                if isinstance(detail,dict):detail=detail.get('message','')
            except ValueError:
                detail=raw if '<' not in raw else ''
            if isinstance(detail,str) and detail:
                detail=detail.replace(key,'[redacted]')
                detail=re.sub(r'(?i)bearer\s+\S+','Bearer [redacted]',detail)
                print('CSCS reason: '+' '.join(detail.split())[:500])
            raise SystemExit(1)
        except (urllib.error.URLError,TimeoutError,ValueError,KeyError,TypeError) as error:
            print('CSCS inference check failed: '+type(error).__name__)
            raise SystemExit(1)
        return
    if args.check:
        request = urllib.request.Request('https://api.inference.cscs.ch/v1/models', headers={'Authorization': 'Bearer '+key})
        try:
            with urllib.request.urlopen(request, timeout=15) as response:
                raw=response.read(65537)
                if len(raw)>65536:raise ValueError('models response too large')
                models=[item['id'] for item in json.loads(raw)['data']]
            print('CSCS /models: HTTP 200')
            print('Available Apertus models:')
            for model in models:
                if 'apertus' in model.lower():print('  '+model)
            print('Selected model listed: '+str(args.model in models))
            print('This checks model listing only; Unity Probe checks inference and the plan contract.')
        except urllib.error.HTTPError as error:
            print('CSCS /models: HTTP '+str(error.code))
            print('Check that this is an active inference API key with resource/model access in the CSCS portal.')
            raise SystemExit(1)
        except (urllib.error.URLError, ValueError, KeyError, TypeError) as error:
            print('CSCS check failed: '+type(error).__name__)
            raise SystemExit(1)
        return
    session = secrets.token_urlsafe(24)
    os.environ.update({
        'FORESTER_PROVIDER': 'apertus_compatible',
        'APERTUS_ENDPOINT': 'https://api.inference.cscs.ch/v1/chat/completions',
        'APERTUS_ALLOWED_HOSTS': 'api.inference.cscs.ch',
        'APERTUS_MODEL': args.model,
        'APERTUS_API_KEY': key,
        'APERTUS_VERIFIED_SCHEMA': '0',
        'FORESTER_SESSION_TOKEN': session,
        'PORT': str(args.port),
    })
    print('Unity mode: RemoteApertus', flush=True)
    print(f'Unity endpoint: http://127.0.0.1:{args.port}/forester/plan', flush=True)
    print(f'Unity session token (this run only): {session}', flush=True)
    print(f'CSCS model: {args.model}', flush=True)
    try:
        runpy.run_path(str(Path(__file__).resolve().parents[2] / 'ForesterGateway/server.py'), run_name='__main__')
    except KeyboardInterrupt:
        print('\nGateway stopped.')


if __name__ == '__main__':
    main()
