# Forester asset mapping
All paths below are relative to Assets/3D. Originals are retained; new wrappers go into Assets/Forester/Prefabs.

| Gameplay visual | Existing model | Interpretation |
|---|---|---|
| Water Team | kenney_mini-characters/Models/FBX format/character-female-a.fbx | Static character proxy; no hose animation |
| Watch Post | kenney_tower-defense-kit/Models/FBX format/weapon-turret.fbx | Static turret proxy |
| Foam Station | kenney_tower-defense-kit/Models/FBX format/weapon-cannon.fbx | Cannon proxy; no foam effect |
| Community Response Team | kenney_mini-characters/Models/FBX format/character-male-a.fbx | Static character proxy |
| Water Tank | kenney_tower-defense-kit/Models/FBX format/tower-square-build-f.fbx | Tower proxy; no tank-specific model |
| Emberling/Wind Runner/Brush Cluster | kenney_tower-defense-kit/Models/FBX format/enemy-ufo-a/b/c.fbx | Stylized hazard proxies, not physical fire |
| Forest objective | kenney_tower-defense-kit/Models/FBX format/wood-structure.fbx | Protected gate proxy |
| Scenery | kenney_tower-defense-kit/Models/FBX format/detail-tree.fbx | Original tree model |
| Board, paths, cell outlines | Generated primitives and URP materials | Explicit layout geometry |

Portraits use matching pack Previews PNGs when present. Missing portraits use text. Attack feedback is cosmetic; no projectile callback controls damage. No audio or animation is fabricated. The screenshot is a camera/layout reference, not an asset source.
