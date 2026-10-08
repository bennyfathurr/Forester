import importlib.util
import json
import os
from pathlib import Path
import tempfile
import threading
import unittest
import urllib.error
import urllib.request
from unittest.mock import patch

spec = importlib.util.spec_from_file_location('forester_web_server', Path(__file__).parent / 'server.py')
web = importlib.util.module_from_spec(spec)
spec.loader.exec_module(web)

class WebTests(unittest.TestCase):
    def setUp(self):
        self.environment = patch.dict(os.environ, {'LLM_API_KEY': 'secret-test-key-never-returned',
            'FORESTER_PROVIDER': 'fake', 'FORESTER_DEMO_GATEWAY': '1'}, clear=True)
        self.environment.start()
        self.directory = tempfile.TemporaryDirectory()
        Path(self.directory.name, 'index.html').write_text('<h1>Forester</h1>')
        Path(self.directory.name, 'test.wasm.br').write_bytes(b'compressed-fixture')
        self.server = web.create_server(port=0, player_directory=self.directory.name)
        self.thread = threading.Thread(target=self.server.serve_forever, daemon=True)
        self.thread.start()
        self.base = f'http://127.0.0.1:{self.server.server_port}'

    def tearDown(self):
        self.server.shutdown()
        self.server.server_close()
        self.thread.join()
        self.directory.cleanup()
        self.environment.stop()

    def test_config_no_provider_key(self):
        with urllib.request.urlopen(self.base + '/api/config') as response:
            raw = response.read().decode()
            self.assertNotIn('secret-test-key', raw)
            config = json.loads(raw)
            self.assertEqual(config['mode'], 'RemoteApertus')
            self.assertEqual(config['session_token'], self.server.session_token)
            self.assertEqual(response.headers['Cache-Control'], 'no-store')

    def test_static_and_wasm_headers(self):
        with urllib.request.urlopen(self.base + '/') as response:
            self.assertIn(b'Forester', response.read())
            self.assertEqual(response.headers['Cross-Origin-Embedder-Policy'], 'require-corp')
        with urllib.request.urlopen(self.base + '/test.wasm.br') as response:
            self.assertEqual(response.headers['Content-Type'], 'application/wasm')
            self.assertEqual(response.headers['Content-Encoding'], 'br')

    def test_cross_origin_rejected(self):
        request = urllib.request.Request(self.base + '/forester/plan', b'{}',
            {'Origin': 'https://other.example', 'Content-Type': 'application/json'})
        with self.assertRaises(urllib.error.HTTPError) as failure:
            urllib.request.urlopen(request)
        self.assertEqual(failure.exception.code, 403)

    def test_gateway_authentication_preserved(self):
        request = urllib.request.Request(self.base + '/forester/plan', b'{}', {'Content-Type': 'application/json'})
        with self.assertRaises(urllib.error.HTTPError) as failure:
            urllib.request.urlopen(request)
        self.assertEqual(failure.exception.code, 401)

    def test_no_key_starts_offline(self):
        with patch.dict(os.environ, {'LLM_API_KEY': '', 'FORESTER_DEMO_GATEWAY': '0'}):
            _, enabled = web.configure_provider()
            self.assertFalse(enabled)

if __name__ == '__main__':
    unittest.main()
