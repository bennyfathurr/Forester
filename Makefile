.PHONY: run gateway test test-offline preview-web
run: gateway
gateway:
	PYTHONDONTWRITEBYTECODE=1 python3 Tools/Forester/run_cscs_gateway.py
test:
	PYTHONDONTWRITEBYTECODE=1 python3 -m unittest discover -s ForesterGateway -q
	PYTHONDONTWRITEBYTECODE=1 python3 -m unittest discover -s Web -q
test-offline:
	sh Tools/Forester/run.sh
preview-web:
	PYTHONDONTWRITEBYTECODE=1 python3 Web/server.py
