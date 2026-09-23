# Breakthrough RTS — Game Design

## Vision

A compact, top-down real-time strategy game that captures the pressure and momentum of a Breakthrough match. The player commands squads rather than micromanaging every action and can choose how much control to delegate to the AI.

The target experience is readable, fast, and satisfying in short sessions, with a low-poly presentation and a path toward desktop and touch-friendly controls.

## Design pillars

1. **A moving front line** — attackers push through sectors while defenders delay and counterattack.
2. **Squads with purpose** — units operate in named squads with attack, defend, support, or reserve responsibilities.
3. **Choose your workload** — Auto, Assist, and Manual control modes support different levels of involvement.
4. **Readable decisions** — objectives, tickets, squad health, capture state, and reinforcements must be understandable at a glance.
5. **Short, replayable battles** — the eventual target is a complete match in roughly 5–10 minutes.

## Match structure

- Two factions: Attackers and Defenders.
- The battlefield is divided into ordered sectors.
- Each sector contains one or more capture points.
- Attackers must secure all capture points in the active sector.
- Captured sectors lock, the front line advances, defenders retreat, and the next sector opens after an intermission.
- Attackers win by securing the final sector.
- Defenders win by exhausting attacker tickets.
- The player chooses Attacker or Defender from the Play menu before the match starts. A match started without that choice still assigns a side at random.

## Tickets, spawning, and reinforcements

### Current implementation

- Attackers begin with a configurable ticket pool and lose tickets when units are defeated.
- Capturing a sector awards attacker tickets as soon as the sector is secured.
- Both factions spawn an initial assault force, capped at 16 alive troops per side.
- Survivors stay when a sector changes. Only the missing places are filled. Attacker replacements cost tickets. New attackers appear on a point that side already owns, in the sector they just took. Defenders fill up in the next sector.
- One attacker squad stays to garrison each point that has been fully taken. The other squads keep moving.
- Attackers are not moved into the next sector. They clear anyone still in the sector they took, then wait on their side of the line until it opens. They can walk back through sectors they already hold.
- Defenders retreat on foot into the next sector. They are not removed and dropped there.
- Points change hands more slowly than the original test pace, so a fight can last long enough to matter.
- Attackers can respawn after a delay, up to the troop cap.
- Defenders receive reinforcement waves, also up to the troop cap.
- Defender ticket depletion is not yet fully enforced and requires validation.

### Intended direction

Spawning should keep squads relevant and make losses meaningful without creating long periods of inactivity. Reinforcement rules, squad recovery, and leader/base spawning should be tuned together.

## Squads and command

- Each faction currently has Alpha, Bravo, Charlie, and Delta squads.
- Squads have a configurable capacity and receive spawned units automatically.
- Roles include Attack/Defend, Support, and Reserve.
- Squad AI scores active objectives using distance, capture need, friendly presence, enemy pressure, role, strength, and recovery state.
- Depleted squads favour safer positions while awaiting reinforcements.
- Direct player orders can move selected units in formation.

## Control modes

| Mode | Intended behaviour |
|---|---|
| Auto | AI commands the player's faction; unit selection and direct orders are disabled. |
| Assist | AI handles strategic movement until the player gives a temporary order. |
| Manual | Player gives strategic movement orders; faction-level strategic AI is disabled for player units. |

All three modes exist in code. Their complete in-game behaviour and user feedback must be validated in play mode.

## Combat and capture

The current prototype includes health, projectile combat, target acquisition, range feedback, capture-point occupancy, and attacker/defender capture progress. Combat should remain readable at the chosen camera distance; effects and numbers should support tactical decisions rather than simulation for its own sake.

## Economy and progression

- Attackers, defenders, and command systems have XP pools in the current code.
- Owned bases can expose faction-specific purchasable units.
- AI commanders can purchase affordable reinforcements.
- Costs, rewards, and roster balance remain prototype values until tested.

## Unit and ability direction

The long-term roster may include specialist infantry such as assault, engineer, sniper, anti-tank, and anti-air roles, plus vehicles such as tanks and aircraft. Possible commander abilities include UAV reconnaissance, bombing runs, gunboat support, morale boosts, cruise missiles, and supply drops.

These are **design candidates**, not promises of current implementation. Each needs a scoped issue and acceptance criteria before development.

## Maps and environment

Maps should make the sector flow visually obvious and offer meaningful lanes, cover, defensible positions, and flanking routes. Future candidates include fortifications, enterable buildings, and destructible or interactive cover.

Playable maps are chosen from the opening menu before a match starts. That menu is a stack of wide rows on a plain black screen. **Play** asks for Attacker or Defender, then a map, and starts at normal speed so you can command your side. **Test** asks for a speed and how many matches, then a map, and runs those matches on it. **Edit** creates a blank map or opens a saved one. **ChatGPT Map** opens the greybox battlefield scene. **Unit Sandbox** opens the scene for trying unit behaviour. Both of those have a Main menu button to come back. **Original** is the hand-built battlefield in `SampleScene`. It can be played and tested, and it stays as the fallback. It is not editable. Any other map is a saved blank layout: ordered rectangular sectors in one strip, control points inside each sector, and one attacker spawn and one defender spawn per sector. You choose those counts on the editor, the game lays the strip out, and you drag borders, points, and spawns. Changing the counts rebuilds that default layout and keeps the name. Auto centre recentres spawns and control points inside the current sector sizes. A dragged point stays inside its sector. Saving writes a file the menu can open again. After a test, returning to edit stays on that map.

A saved layout sits on a verge, with a road down the middle of the strip and a crossing where each sector meets the next. That centre line is a temporary default, accepted for now. You can also lay extra roads: two clicks, kept straight along the map or across it, saved with the map, and removed with the red sphere. The centre road stays when an added road is deleted. Later, roads should be freer than these straight runs. Resizing the map moves the centre road with the borders. Automatic dressing stays off the centre road and off roads you added. Pieces you place yourself can sit on a road. The layout can also dress itself with Market pieces: parasols, awnings, and small stalls, kept clear of control points and spawns. A population slider from 1 to 20 sets how many pieces each sector tries to place. The editor lists a palette. Click a piece, then click the map to place it, or drag it off the list to drop one. A piece placed that way is kept immediately, as is one you later drag. The small red sphere deletes a piece, and that spot stays empty on the next automatic pass. Dress again replaces only the pieces you have not kept. The population level and the pieces are saved with the map. New palette pieces are added to one list, including future groups. Other district rules are still later. The Market preview stays a look check, not a playable Breakthrough map.

## Interface and accessibility

The interface should prioritise:

- current sector and objective ownership;
- attacker and defender tickets;
- squad identity, role, strength, and order;
- selected units and movement destination;
- control mode and whether an order is temporary;
- available reinforcements and XP;
- clear victory, defeat, and sector-transition feedback.

Audio/TTS alerts and touch controls are future candidates.

## AI philosophy

AI should be legible and tunable. A squad should pursue a stable objective, react to changing strength and pressure, avoid needless objective thrashing, and recover when depleted. Important decisions should be inspectable through diagnostics or logs.

The AI is allowed to be imperfect; it should feel purposeful before it feels optimal.

## Scope guardrails

The first release is a polished vertical slice, not every possible unit, vehicle, ability, or map. New features should strengthen the core Breakthrough loop. Large additions wait until one complete match is reliable, readable, and fun.

## Document protocol

Update this file when a product or gameplay decision changes. Record implementation status and delivery order in [DEVELOPMENT.md](DEVELOPMENT.md).
