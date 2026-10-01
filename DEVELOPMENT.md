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
- player chooses Attacker or Defender from the Play menu, with random assignment only if a match starts without that choice;
- Alpha–Delta squad creation and automatic membership;
- squad roles, persistent objectives, strength states, and reinforcement recovery;
- Auto, Assist, and Manual control modes;
- click, box, and double-click squad selection;
- direct movement orders and basic formations;
- health, targeting, projectiles, effects, and range indicators;
- XP pools, unit store UI, base ownership, and AI purchasing;
- capture, ticket, XP, victory, and transition UI;
- AI diagnostics, match balance logging, screenshots, and accelerated batch-test support.
- A main-menu map workshop. It lists Original plus every blank map saved under `Assets/Data/Maps`, and it does not start a match on its own.
- Blank maps are data, not new scenes: sector rectangles, control points, and both spawns. Borders and points can be dragged, then saved and played or quick-tested again.
- A Map Recipe asset and an editor stamp (`RTS → Maps`) that builds a visual `GeneratedMap` preview from sectors, courtyard-flag counts, and districts. That preview is not the playable workshop.
- One Kenney City Kit (Commercial) Market district in the preview scene: plaza, orange shop blocks, stall canopies, parasols, a small kiosk, and two courtyard flags. Kenney blocks are scaled by the recipe `visualScale` (8) so they read from the RTS camera. Orange comes from Kenney variation B. Flags are markers only.

## Known gaps and risks

- Defender ticket spending currently needs validation; the code does not appear to reduce the defender pool.
- SampleScene had attacker tickets left at 15. That pool ran out during the first sector, so the defenders won as soon as it was captured. Matches now start at 150, and the capture bonus is paid when the sector is secured, before the retreat pause. A later sector still has to be taken; only the last sector wins the match.
- The saved front-line behaviour is in the current match: a garrison stays on a taken point, survivors carry forward up to a cap of 16, capture is slower, and attackers wait on their own side of the line instead of being moved into the next sector. On 22 Sep 2026 a played match ran through to a result. Specialist abilities, combat effects, and squad routes are not part of this pass.
- One played match has reached a result. Original, both results, and Auto, Assist, and Manual were accepted in play on 22 Sep 2026.
- The single main scene and several map-related feature branches need consolidation.
- The opening menu of wide rows on black, including ChatGPT Map and Unit Sandbox, was accepted on 23 Sep 2026. The Market preview is still a look check, not a playable Breakthrough map. Saved layouts sit on a verge with a centre road, a crossing between sectors, and any extra straight roads you lay. They also dress themselves. Each sector is a market or a farm. The farm look is simple shapes for now: crop rows, hay, fences, barns, and silos. A population slider from 1 to 20 sets how full each sector is. The editor palette lists every placeable piece, and that list is where new pieces and future groups are added. Placing or moving a piece keeps it, deleting one blocks that spot, and Dress again only replaces pieces you have not kept. Other district rules are still later.
- Some systems are runtime-created or loosely coupled; scene and prefab references may fail silently.
- Balance values are prototype values, not release decisions.
- Desktop mouse input exists; touch/mobile interaction is not complete.
- Documentation, automated tests, and a repeatable release checklist are minimal.

## Current vertical slice

**Goal:** one complete Breakthrough match that can be played from start to finish as either faction without manual repair in the Unity editor.

### Acceptance criteria

- A fresh launch opens the menu. Play starts a match as the side you chose.
- Auto, Assist, and Manual modes behave as described in DESIGN.md.
- Units form squads, select valid objectives, fight, die, and reinforce.
- Every sector can be contested, captured, locked, and transitioned.
- Attacker and defender win conditions both work.
- Ticket, XP, capture, squad, transition, and result feedback stay accurate.
- No blocking exceptions or broken scene/prefab references occur.
- A short test report records build, platform, result, duration, and defects.

## Roadmap

The first slice of **Map Generation Phase 2** now includes a verge, roads, Market dressing, a simple Farm look per sector, a population slider, and a palette. Industrial and airport looks are the remaining decoration work. Freer road shapes come later. The milestones below stay after that.

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

The preview is not on `main`. Unity only shows it after the project is on branch `dabutchart/dab-148-ch11-recipe-window-and-first-market-district`. Closing and reopening Unity does not download that branch.

1. Switch to that branch, then in Unity 6000.5.7f1 use **RTS → Maps → Open Chapter 1 Market Preview**, or double-click `Assets/Scenes/Chapter1MarketPreview.unity`. Leave `SampleScene` as the playable map. It is still the first scene in Build Settings.
2. In the Hierarchy, select `GeneratedMap` and press F to frame it.
3. You should see a warm plaza, orange shop blocks, stall canopies over counters, parasols, a small kiosk on the east side, and two flags in the open courtyard.
4. Press Play to look around. WASD or the arrow keys slide the camera. Q and E, or the scroll wheel, zoom. No match starts.
5. To rebuild from the recipe, use **RTS → Maps → Recipe Window** or **RTS → Maps → Stamp Chapter 1 Market Preview**. Stamping replaces only `GeneratedMap`.
6. If a Kenney model is missing after the first import, run that stamp command once and save the scene. Unity then links the imported models.

The recipe asset is `Assets/Data/MapRecipes/Chapter1Market.asset`. Change sectors, flag counts, or `visualScale` there, then stamp again.

## Making a blank map

1. Press Play on `SampleScene` in Unity 6000.5.7f1. The opening menu is a stack of rows on a black screen: **Play**, **Test**, **Edit**, **ChatGPT Map**, and **Unit Sandbox**. A match does not start on its own. ChatGPT Map is the greybox battlefield. Unit Sandbox is for trying units. Each of those scenes has a Main menu button back to this screen.
2. Choose **Edit**, then **Create new**. The editor opens on a blank strip. One row is the sector count. Each sector then has its own row: how many control points, and whether that sector looks like a market or a farm. Changing the counts rebuilds a fresh layout and keeps the name. **Auto centre all** recentres spawns and control points without changing sector sizes.
3. Drag the white borders to resize sectors. Drag a gold marker to move a control point, and a red or blue marker to move that side's spawn. A road runs down the middle, with a crossing on each join between sectors. **Lay road** adds another straight road with two clicks. A red sphere deletes a road you added. The centre road stays. Automatic dressing stays off the centre road and off roads you added. A piece you place yourself can sit on a road.
4. Choose **Save**, type a name, and save again. That name is the file in `Assets/Data/Maps` and the name in the menu. **Main menu**, then **Edit**, lists it. Opening it puts the borders and markers back, so you can drag them again. Original is listed for Play and Test, and it cannot be edited. The editor also dresses the map with Market pieces. **Population** runs from 1 to 20 and sets how many pieces each sector tries to place. **Pieces** lists the palette. Click one, then click the map to place it, or drag it off the list to drop one. A placed piece is kept. Drag the gold sphere on a piece to move it, or the small red sphere to delete it. **Dress again** replaces only the pieces you have not kept. Save stores that dressing, including the population level, with the map. New pieces are added to the palette list in `DecorationCatalog`.
5. **Play** asks for Attacker or Defender, then a map. The match runs at normal speed with tickets and Auto, Assist, and Manual, so you can command your side. **Test** asks for a speed and how many matches, then a map. Those matches play themselves on that same map. When they finish, **Edit this map** stays on the map you just tested.

## Immediate queue

1. A played match on a workshop map has reached a result. Confirmed 22 Sep 2026. Original, both results, and Auto, Assist, and Manual were accepted the same day.
2. The opening menu and the Market dressing, including the population slider, were accepted on 23 Sep 2026.
3. The verge, centre road, crossings, and extra straight roads were accepted on 23 Sep 2026. Each sector can now be dressed as a market or a simple farm. Industrial and airport looks come after that. Freer road shapes stay later.

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
