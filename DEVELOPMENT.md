# RTS Development Roadmap

_Last updated: 7 September 2026_

## Purpose

This document is the development source of truth for the RTS project. It records where the game is now, the route to a deliberately finite v1.0, completed milestones, current work, deferred snags, and ideas that should not distract from the current plan.

`DESIGN.md` should eventually describe **what the game is**. This file describes **how we are getting it finished**.

### Definition of Done

A feature is not marked **Done** merely because the code exists. It is Done when it has been implemented, tested in Unity by Dean, and accepted as working well enough to move on. Minor imperfections can be moved to the Snag List without blocking the milestone.

---

## Why We Are Making This

This project is primarily an experiment in AI-assisted game development:

- learn how modern AI systems can help with real software/game development;
- learn Unity and game-development concepts through building something tangible;
- make visible, enjoyable progress regularly rather than disappearing into infrastructure work;
- explore AI-vs-AI behaviour and observation as a first-class part of the experience;
- actually finish a coherent game rather than maintain an endless prototype;
- preserve the possibility of a commercial release if the result turns out to be genuinely good.

The project should therefore favour small, testable vertical slices and understandable architecture over premature complexity.

---

## v1.0 Definition

> A polished, self-contained tactical RTS in which the player commands or observes small AI armies fighting 5–10 minute Breakthrough battles across several good maps, with squads, specialists, vehicles, tactical AI, commander abilities, and enough presentation to make repeated matches enjoyable, with no obvious prototype scaffolding remaining.

### Explicitly Not Required for v1.0

These may be good ideas, but they must not hold v1.0 hostage:

- online multiplayer;
- procedural map generation;
- Conquest mode;
- dozens of unit classes;
- elaborate meta-progression;
- photorealistic art;
- large armies;
- a huge map catalogue.

---

## Current State

The project has moved beyond a basic proof of concept. A substantially complete Breakthrough match loop now exists:

**spawn → attack/defend → capture objectives → secure sector → defenders retreat / attackers pursue → both sides replenish → frontline opens → next sector → match end**

The project currently supports two battlefield scenes selected through a Bootstrap map selector:

- **Original Map** — internal scene `RecoveredDevelopmentMap`;
- **ChatGPT Map** — internal scene `GreyboxBattlefield01`.

Shared gameplay systems are supplied through `GameplaySystems.prefab`, and `BattlefieldRuntimeLauncher` provides the common runtime startup path.

A project-specific Unity CLI diagnostic layer is now available through the experimental Unity Pipeline package. It gives the development workflow a read-only way to ask the live Unity Editor about scene state without relying on scene-YAML inspection. Initial commands include `rts_status`, `rts_scene_summary`, `rts_materials`, and `rts_objectives`.

The current development branch is `feature/map-selector-recovery-v2`.

---

# v1.0 Roadmap

The order below is intentional, but small visual/fun tasks can occasionally be inserted between heavier systems work.

## Phase 1 — Reliable Breakthrough Foundation

**Status: substantially complete**

- [x] Basic attacker/defender combat
- [x] Multi-sector Breakthrough progression
- [x] Objective capture and sector completion
- [x] Attacker ticket system
- [x] Sector capture ticket bonus
- [x] Defender retreat between sectors
- [x] Attacker pursuit during sector transition
- [x] Transition-time Assault replenishment
- [x] Preserve surviving units through sector changes
- [x] Moving frontline / faction-specific legal combat territory
- [x] Dynamic sector base spawning
- [x] Captured/held control points as reinforcement sources
- [x] Match restart / end controls
- [x] Map selector and common battlefield startup

## Phase 2 — Battlefield Readability & Core Content

**Status: in progress**

- [ ] First Kenney texture/readability pass on Original Map
- [ ] Review vehicle availability and faction symmetry
- [ ] Specialist snag pass
- [ ] Review objective, spawn and route readability after visual pass
- [ ] Remove remaining obsolete prototype/front-end scaffolding

## Phase 3 — Tactical AI

**Status: next major gameplay phase**

- [ ] Define explicit tactical lanes/routes above the NavMesh
- [ ] Main assault, flank, rotation and retreat route concepts
- [ ] AI commander chooses routes based on battlefield state
- [ ] Improve objective allocation between squads
- [ ] Reduce excessive clustering / obvious AI behaviours
- [ ] Give specialists tactically meaningful positioning
- [ ] Validate behaviour through AI-vs-AI observation and telemetry

The NavMesh should remain responsible for local pathfinding. Tactical AI should decide **where and why** a squad moves rather than replace Unity navigation.

## Phase 4 — Vehicles & Specialists

- [ ] Finalise vehicle spawning/availability by sector
- [ ] Improve anti-vehicle behaviour
- [ ] Finalise Engineer role
- [ ] Finalise Recon role
- [ ] Finalise Support role
- [ ] Manual/assisted specialist abilities where appropriate
- [ ] Balance specialist limits and XP costs

## Phase 5 — Commander Layer

- [ ] Unify/clean command XP systems
- [ ] Commander ability framework
- [ ] UAV Sweep
- [ ] Bombing Run / air support
- [ ] Morale or temporary combat boost
- [ ] Additional ability only if it adds clear tactical value
- [ ] AI commander capable of using equivalent systems

## Phase 6 — Maps & Match Variety

- [ ] Polish Original Map gameplay layout
- [ ] Polish ChatGPT Map gameplay layout
- [ ] At least one additional good Breakthrough battlefield if scope permits
- [ ] Distinct routes, compounds, flanks and defensive positions per map
- [ ] Validate 5–10 minute target match length
- [ ] Validate both factions through repeated AI-vs-AI matches

## Phase 7 — Presentation & Game Feel

- [ ] Final environment readability/art pass
- [ ] Set dressing appropriate to low-poly style
- [ ] Final unit readability/class markers
- [ ] Improve combat feedback where required
- [ ] Audio pass
- [ ] UI/HUD cleanup
- [ ] Camera polish
- [ ] Match start/end presentation
- [ ] Remove development-only visual clutter from release presentation

## Phase 8 — Balance, Performance & v1.0 Cleanup

- [ ] Balance tickets, capture times and reinforcement rates
- [ ] Balance unit/specialist/vehicle combat
- [ ] Automated multi-match balance runs
- [ ] Performance pass for target platforms
- [ ] Resolve release-blocking snag list
- [ ] Remove obsolete scripts/assets/prototype scaffolding
- [ ] Build/settings sanity check
- [ ] Fresh-install/build smoke test
- [ ] v1.0 release candidate

---

# Current Work

## Original Map — Kenney Readability Pass

**Status: in progress / awaiting acceptance**

Agreed scope for the first pass:

- Original Map only;
- use suitable Kenney CC0 prototype-style textures/materials;
- main terrain green/grass;
- roads and major routes visually distinct with road/dirt treatment;
- hard-standing/concrete areas complementary;
- **walls and buildings orange**;
- medium texture scale;
- **same texture/tiling size across every textured prop for this pass**;
- preserve geometry, objective positions, NavMesh and gameplay;
- no set dressing, vegetation, lighting pass, shader experimentation or geometry redesign yet.

This task is being handled separately because it requires local Unity scene/material work. It is not Done until tested and accepted in Unity.

---

# Completed Milestones

## Project Recovery & Map Selection

- Recovered the original battlefield from project backup and established it as `RecoveredDevelopmentMap`.
- Preserved the recovered scene and project backups rather than destructively replacing them.
- Added a dedicated Bootstrap scene and map selector.
- Both Original Map and ChatGPT Map can be launched through the selector.
- Direct battlefield scene play remains supported for development.
- Obsolete BattleBlocks faction/menu flow no longer blocks normal gameplay startup.

## Shared Battlefield Architecture

- Migrated both maps toward shared `GameplaySystems.prefab` architecture.
- Added a single `BattlefieldRuntimeLauncher` path for common runtime startup.
- Restored common control mode, specialist and diagnostic systems after map loading.
- Removed obsolete donor installer `ChatGPTMapGameplayInstaller`.
- Added common Restart Game and End Game controls.

## Breakthrough Sector Transitions

Accepted transition behaviour now includes:

- surviving attackers remain alive and physically present when a sector is captured;
- attackers continue fighting retreating defenders inside the captured sector;
- attackers cannot enter the next sector before the transition timer expires;
- attackers reaching the closed frontline gather/wait there;
- defenders retreat physically toward the next defensive sector rather than teleporting;
- both sides replenish missing Assaults during the transition countdown;
- existing surviving Assaults count toward the 16-unit core roster;
- attacker replacements consume tickets;
- defenders replenish free;
- sector capture ticket bonus is awarded early enough to fund attacker replenishment;
- specialists survive and carry forward;
- at timer zero the frontline opens and surviving attackers proceed naturally from their existing positions.

Known caveat: the attacker reaches a full 16-Assault roster only when sufficient tickets remain. This is currently intentional unless the design rule is changed later.

## Frontline / Combat Territory System

- Established faction-specific legal combat territory.
- During normal combat attackers can occupy captured territory plus the active sector.
- Defenders are confined to the active defensive sector.
- During transitions attackers remain behind the closed next-sector frontline while defenders may retreat into the next sector.
- Wrong-side units are corrected back toward legal territory.
- Frontline/sector debug visualisation advances with sector state.
- Boundary policy is centralised rather than relying solely on per-unit special cases.

## Spawning & Reinforcements

- Dynamic current-sector base spawning implemented.
- Shared spawn-source resolution architecture established.
- Tanks confirmed working with dynamic base spawning.
- Ordinary reinforcements can use faction-controlled capture points in the active sector.
- A held control point remains a valid reinforcement source even if another point in the same sector has been lost.
- Captured-sector transition replenishment preserves surviving units and spawns only missing Assaults.

## Squads & Movement

- Four-squad Assault structure established around a 16-Assault core.
- Squad formation movement implemented and accepted.
- Squad cohesion/regrouping behaviour exists.
- Shared order concepts support AI and player control.
- AUTO / ASSIST / MANUAL control modes implemented.

## Specialists

- Engineer, Recon and Support specialist framework implemented.
- Specialist deployment tracking/limits implemented.
- Specialist purchase/runtime bootstrap systems shared across maps.
- Engineer automatically engages enemy vehicles with rockets.
- Engineer rocket splash/AoE damage implemented and accepted.
- Support bag behaviour exists.
- Recon spotting behaviour exists.

## Combat Presentation

- Unit spawn drop-in presentation.
- Landing puff effect.
- Hit flash feedback.
- Restrained unit death/shatter presentation.
- Muzzle flash and projectile trail presentation.
- Impact spark presentation.
- Distinct Recon shot/trail treatment.
- Engineer explosion presentation.

## Development / Observer Tools

- AI-vs-AI automated testing support with accelerated simulation.
- Match telemetry/balance logging foundations.
- Squad AI diagnostic overlay.
- XP HUD showing both factions in development builds.
- Sector/frontline debug visualisation.
- Dedicated Unit Sandbox created as a first-class development tool.
- Sandbox free-spawn controls for real gameplay units.
- Sandbox 1v1 duel mode.
- Assault, Engineer, Recon, Support and Tank duel choices.
- Per-side Destructible / Invulnerable controls.
- Sandbox camera controls.
- Unity CLI connected successfully to the live Editor through `com.unity.pipeline`.
- Live read-only inspection verified for active scene, ground materials/colours, Kenney material assignments, scene roots, components and objective state.
- Project-specific read-only CLI commands accepted in Unity: `rts_status`, `rts_scene_summary`, `rts_materials`, and `rts_objectives`.
- CLI is intended to grow only when a concrete debugging/testing need justifies a new command, rather than becoming an infrastructure project of its own.

## Camera & General Runtime

- RTS camera movement/zoom behaviour substantially stabilised.
- Tactical overview support exists.
- Bootstrap edit-mode preview prevents Unity's `No cameras rendering` warning without altering runtime scenes.

---

# Snag List

These are known imperfections that do not currently justify derailing the roadmap.

## Engineer

- Projectile/fire rate can appear inconsistent.
- Health bar positioning over Engineer body needs correction.
- Engineer occasionally appears to fire a non-Engineer projectile.
- Rocket projectile should look more rocket-like.
- Sandbox Engineer topper displays `AT` rather than the preferred main-game icon.

## Presentation / UI

- XP HUD is slightly squashed and needs eventual layout polish.
- Some class icons can flicker.
- Death chunks may benefit from further tuning.
- Recon trail lifetime/impact behaviour may need another visual check.

## Technical Cleanup

- Review potential undefined `Projectile` tag usage in Engineer-related logic.
- AI Commander currently has a separate passive `aiCommandXP` concept from GameManager XP; unify later.
- `RecoveredDevelopmentMap` currently exposes two `AICommander` components on the live `GameManager`; inspect whether this is intentional before any cleanup.
- Remove remaining old BattleBlocks/recovered-menu objects and code once no longer useful for recovery safety.
- Review stale shared-prefab builder/helper language after architectural cleanup.

---

# Toy Box / Post-v1.0 Ideas

Ideas live here specifically so they can be remembered **without becoming today's job**.

- Procedural battlefield generation.
- Conquest mode.
- More maps and biome themes.
- More specialist/unit classes.
- Enterable buildings.
- Expanded fortifications and cover systems.
- More vehicles.
- Downed/revive system.
- Supply drops.
- Named soldiers and deeper individual progression.
- More elaborate persistent progression.
- Additional commander abilities such as gunboat/cruise missile where appropriate.
- Expanded observer/broadcast presentation for AI-vs-AI matches.
- Online multiplayer only if the finished core game ever justifies the enormous scope increase.

---

# Architecture / Working Rules

## Source Control

- GitHub is the source of truth.
- Work on feature branches; do not casually develop on `main`.
- Current recovery/development branch: `feature/map-selector-recovery-v2`.
- Prefer small change → Unity test → fix → accepted known-good milestone.
- Scene and prefab state are part of the feature and should be committed alongside code at known-good milestones.
- Commit Unity-generated `.meta` files with their corresponding assets/scripts.
- Do not commit runtime telemetry CSVs or unrelated Unity-generated asset changes accidentally.
- Preserve project backups.

## Code / Systems

- Prefer shared systems over map-specific duplicate implementations.
- Keep gameplay and AI parameters Inspector/configurable where practical.
- AI/player orders should converge on the same underlying order APIs where possible.
- NavMesh handles local navigation; higher-level tactical systems decide intent/routes.
- Avoid hand-editing large Unity scene YAML when a safer editor/runtime approach exists.
- Prefer Unity CLI read-only inspection where it can answer a live Editor question more safely and cheaply than reverse-engineering serialized scene state.
- Project-specific CLI commands should remain narrowly scoped, diagnostic-first and safe to run against the live Editor.

## Development Rhythm

- Aim for roughly one visible/testable improvement per development session.
- Heavy architecture work should periodically be followed by a fun or visually rewarding task.
- Use the Unit Sandbox when normal-match setup creates unnecessary testing ceremony.
- Use Unity CLI diagnostics when they reduce Inspector ceremony or scene-file archaeology, but do not build tooling without a concrete need.
- AI-vs-AI observation is a legitimate first-class testing and gameplay experience, not merely a debug fallback.

---

# Immediate Queue

1. **Finish and accept Original Map Kenney readability pass.**
2. **Review vehicle availability / faction symmetry.**
3. **Resolve the most disruptive specialist snags.**
4. **Begin tactical lane/route architecture.**
5. **Use AI-vs-AI observation to iterate on tactical choices rather than merely pathfinding.**

This queue can change when testing exposes a genuine blocker, but new ideas should normally enter the Snag List or Toy Box rather than silently replacing the roadmap.
