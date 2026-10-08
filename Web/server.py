"""Serve the Unity web player and Apertus gateway on one local origin."""
import json
import logging
import os
from pathlib import Path
import secrets
import sys
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
from urllib.parse import urlparse

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from ForesterGateway.server import Provider, Gateway, Handler as PlanHandler


def configure_provider():
    key = os.environ.get('LLM_API_KEY', '')
    model = os.environ.get('LLM_NAME', 'swiss-ai/Apertus-v1.5-70B')
    base = os.environ.get('LLM_BASE_URL', 'https://api.inference.cscs.ch/v1').rstrip('/')
    # Explicit fake mode is only for contract tests; normal no-key startup is offline.
    mode = os.environ.get('FORESTER_PROVIDER') or ('apertus_compatible' if key else 'fake')
    if mode != 'fake' and 'apertus' not in model.lower():
        raise RuntimeError('This game requires an Apertus model. Set LLM_NAME.')
    endpoint = base if base.endswith('/chat/completions') else base + '/chat/completions'
    os.environ.update({
        'FORESTER_PROVIDER': mode, 'APERTUS_MODEL': model,
        'APERTUS_ENDPOINT': endpoint, 'APERTUS_API_KEY': key,
        'APERTUS_ALLOWED_HOSTS': urlparse(endpoint).hostname or '',
    })
    return Provider(), bool(key) or os.environ.get('FORESTER_DEMO_GATEWAY') == '1'


class WebHandler(SimpleHTTPRequestHandler):
    def __init__(self, *args, **kwargs):
        super().__init__(*args, directory=str(args[2].player_directory), **kwargs)

    def guess_type(self, path):
        plain = path[:-3] if path.endswith('.gz') else path[:-3] if path.endswith('.br') else path
        if plain.endswith('.wasm'):
            return 'application/wasm'
        if plain.endswith('.data'):
            return 'application/octet-stream'
        return super().guess_type(plain)

    def end_headers(self):
        path = self.path.split('?', 1)[0]
        if path.endswith('.br'):
            self.send_header('Content-Encoding', 'br')
        elif path.endswith('.gz'):
            self.send_header('Content-Encoding', 'gzip')
        self.send_header('X-Content-Type-Options', 'nosniff')
        self.send_header('Cross-Origin-Opener-Policy', 'same-origin')
        self.send_header('Cross-Origin-Embedder-Policy', 'require-corp')
        super().end_headers()

    def do_GET(self):
        if self.path in ('/api/config', '/healthz'):
            config = {'status': 'ok'} if self.path == '/healthz' else {
                'mode': 'RemoteApertus' if self.server.ai_enabled else 'Scripted',
                'session_token': self.server.session_token if self.server.ai_enabled else '',
                'model': self.server.gateway.provider.model,
            }
            raw = json.dumps(config).encode()
            self.send_response(200)
            self.send_header('Content-Type', 'application/json')
            self.send_header('Cache-Control', 'no-store')
            self.send_header('Content-Length', str(len(raw)))
            self.end_headers()
            self.wfile.write(raw)
            return
        super().do_GET()

    def do_POST(self):
        # Do not grant other browser origins access to the local inference gateway.
        origin = self.headers.get('Origin')
        expected = self.headers.get('Host', '')
        if origin and urlparse(origin).netloc != expected:
            self.send_error(403, 'Cross-origin request rejected')
            return
        self.connection.settimeout(7)
        PlanHandler.do_POST(self)

    def log_message(self, *args):
        pass


def create_server(host='127.0.0.1', port=8080, player_directory=None):
    provider, enabled = configure_provider()
    server = ThreadingHTTPServer((host, port), WebHandler)
    server.player_directory = Path(player_directory or Path(__file__).parent / 'player').resolve()
    server.session_token = secrets.token_urlsafe(24)
    server.ai_enabled = enabled
    server.gateway = Gateway(provider, server.session_token)
    return server


if __name__ == '__main__':
    logging.basicConfig(level=logging.INFO, format='%(message)s')
    server = create_server(os.environ.get('WEB_HOST', '127.0.0.1'), int(os.environ.get('PORT', '8080')))
    logging.info('Forester web player: port=%s mode=%s', server.server_port, 'Apertus' if server.ai_enabled else 'offline')
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        server.server_close()
