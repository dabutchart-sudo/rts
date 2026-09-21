# Breakthrough RTS — Development

## Current snapshot

- Unity: **6000.5.7f1**
- Render pipeline: URP
- Input: Unity Input System
- Navigation: AI Navigation / NavMesh
- Main scene: `Assets/Scenes/SampleScene.unity`
- Market look-dev scene: `Assets/Scenes/Chapter1MarketPreview.unity` (not in the build list)
- Main branch: `main`

The repository contains a playable prototype, but it has not yet passed a documented end-to-end vertical-slice acceptance test.

## Implemented foundations

- ordered sectors and multi-point capture progression;
- attacker victory, defender victory, and sector intermission flow;
- initial spawning, attacker respawning, defender reinforcement waves;
- attacker ticket loss and sector capture bonus;
- random player-faction assignment;
- Alpha–Delta squad creation and automatic membership;
- squad roles, persistent objectives, strength states, and reinforcement recovery;
- Auto, Assist, and Manual control modes;
- click, box, and double-click squad selection;
- direct movement orders and basic formations;
- health, targeting, projectiles, effects, and range indicators;
- XP pools, unit store UI, base ownership, and AI purchasing;
- capture, ticket, XP, victory, and transition UI;
- AI diagnostics, match balance logging, screenshots, and accelerated batch-test support.
- A Map Recipe asset and an editor stamp (`RTS → Maps`) that builds a `GeneratedMap` root from sectors, courtyard-flag counts, and districts.
- One Kenney City Kit (Commercial) Market district in the preview scene: plaza, orange shop blocks, stall canopies, parasols, a small kiosk, and two courtyard flags. Kenney blocks are scaled by the recipe `visualScale` (8) so they read from the RTS camera. Orange comes from Kenney variation B. Flags are markers only.

## Known gaps and risks

- Defender ticket spending currently needs validation; the code does not appear to reduce the defender pool.
- The full match loop, all control modes, spawning, and sector transitions need repeatable play-mode verification.
- The single main scene and several map-related feature branches need consolidation.
- The Market preview is not a playable Breakthrough map. SampleScene is still the match. Ground, roads, the other districts, NavMesh, and capture wiring are later chapter work.
- Some systems are runtime-created or loosely coupled; scene and prefab references may fail silently.
- Balance values are prototype values, not release decisions.
- Desktop mouse input exists; touch/mobile interaction is not complete.
- Documentation, automated tests, and a repeatable release checklist are minimal.

## Current vertical slice

**Goal:** one complete Breakthrough match that can be played from start to finish as either faction without manual repair in the Unity editor.

### Acceptance criteria

- A fresh launch starts a match and assigns a playable faction.
- Auto, Assist, and Manual modes behave as described in DESIGN.md.
- Units form squads, select valid objectives, fight, die, and reinforce.
- Every sector can be contested, captured, locked, and transitioned.
- Attacker and defender win conditions both work.
- Ticket, XP, capture, squad, transition, and result feedback stay accurate.
- No blocking exceptions or broken scene/prefab references occur.
- A short test report records build, platform, result, duration, and defects.

## Roadmap

### M1 — Stabilise the Breakthrough loop

Validate a complete match, fix ticket and victory rules, consolidate the playable map, and remove blocking runtime errors.

### M2 — Make squad command dependable

Verify squad membership and recovery, tune objective persistence, finish control-mode behaviour, and improve squad/order feedback.

### M3 — Make combat readable and balanced

Validate targeting and damage, distinguish the initial infantry roles, tune reinforcement cadence, and use telemetry for repeatable balance tests.

### M4 — Produce the first deliberate battlefield

Turn the chosen map into a clear multi-sector experience with lanes, cover, spawn safety, boundaries, and reliable NavMesh navigation.

### M5 — Complete the player experience

Improve onboarding, camera and input, HUD hierarchy, purchase flow, alerts, pause/restart, and touch-friendly interaction where appropriate.

### M6 — Expand only after the slice is solid

Evaluate specialist roles, vehicles, commander abilities, fortifications, buildings, additional maps, audio/TTS, and visual polish as individually scoped features.

## Looking at the Market preview

This is a look check, not a match.

1. In Unity 6000.5.7f1, open `Assets/Scenes/Chapter1MarketPreview.unity`. Leave `SampleScene` as the playable map.
2. In the Hierarchy, select `GeneratedMap` and press F to frame it.
3. You should see a warm plaza, orange shop blocks, stall canopies over counters, parasols, a small kiosk on the east side, and two flags in the open courtyard.
4. Press Play to look around. WASD or the arrow keys slide the camera. Q and E, or the scroll wheel, zoom. No match starts.
5. To rebuild from the recipe, use **RTS → Maps → Recipe Window** or **RTS → Maps → Stamp Chapter 1 Market Preview**. Stamping replaces only `GeneratedMap`.
6. If a Kenney model is missing after the first import, run that stamp command once and save the scene. Unity then links the imported models.

The recipe asset is `Assets/Data/MapRecipes/Chapter1Market.asset`. Change sectors, flag counts, or `visualScale` there, then stamp again.

## Immediate queue

1. Run and document one complete match on `main`.
2. Verify both factions' ticket depletion and win conditions.
3. Test Auto, Assist, and Manual modes against explicit acceptance criteria.
4. Audit the map branches and choose the canonical battlefield work.
5. Fix the highest-impact defect found by the vertical-slice test.
6. Repeat the test and retain balance logs.

## Definition of done

A task is done when:

- its acceptance criteria are met;
- Unity compiles without new errors;
- affected gameplay is tested in Play mode;
- scene, prefab, and `.meta` changes are intentional;
- any new setup step is documented;
- the linked issue is updated and the pull request references it;
- DESIGN.md or DEVELOPMENT.md is updated when the decision or roadmap changed.

## Working rhythm

- Use GitHub issues for epics, tasks, bugs, and experiments.
- Use an RTS GitHub Project for current status, priority, and views.
- Keep only actionable work in Ready or In Progress.
- Close tasks when verified, not merely when code is written.
- Review this roadmap after each milestone or material design change.
