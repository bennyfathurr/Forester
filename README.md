# Forester — Unity development project

Open this folder in Unity Hub using the editor version in ProjectSettings/ProjectVersion.txt.
Open Assets/Forester/Scenes/VillageForestEdge.unity and enter Play Mode. Authored scenes,
Level1/Level2, assets and project settings have been preserved.

## AI development on Mac

Run `make gateway` (or `python3 Tools/Forester/run_cscs_gateway.py`). Enter your CSCS key in
the hidden terminal prompt. Default model: swiss-ai/Apertus-v1.5-70B.
In the game's AI connection settings choose RemoteApertus, set
http://127.0.0.1:8081/forester/plan, and copy the session token printed by this running gateway.
Restarting the gateway creates a new token. Stop it with Ctrl+C. No Docker is required.
Use Scripted mode for offline development.

## Edit content in Inspector

- Assets/Forester/Data/Levels: grid, buildable cells, routes, wave allowlists/bounds, base HP and resources.
- Assets/Forester/Data/Enemies: enemy stats.
- Assets/Forester/Data/Cards and Defenders: prices, placement, combat stats.
- Assets/Forester/Data/Cards/CardCatalog.asset: registered gameplay definitions.
- Assets/Forester/Data/Visuals/VisualCatalog.asset: visual ID to prefab bindings.
- ForesterGateway/system_prompt.txt: hosted AI strategy instructions.

New enemy IDs and changes to service wave limits also require coordinated gateway/schema
updates. The current gateway validates three waves and the existing NORTH/SOUTH routes.

## Verify

`make test` runs gateway and web-server HTTP tests. `make test-offline` runs the deterministic
simulation cases with the installed Unity Mono compiler. Web server tests use temporary
fixtures and do not require exported player files.

## Optional web development

The Unity menu Forester > Build web player (preserve authored scene) generates Web/player.
Then `make preview-web` serves the player locally. Browser AI configuration uses LLM_NAME,
LLM_BASE_URL and LLM_API_KEY environment variables; never commit credentials.
Generated builds, Docker files and the submission package were moved into the sibling
Tower Apertus - Build Archive 20261008-144229 folder. This checkout now contains the
maintained development source rather than duplicated submission source or shipped players.
