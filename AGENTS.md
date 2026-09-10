# AGENTS.md

## Purpose

This repository's long-term memory lives in the repository, not in any single AI conversation.

Before changing gameplay or project structure, read:

1. `DESIGN.md` — intended player experience and design decisions.
2. `DEVELOPMENT.md` — implementation status, risks, and delivery order.
3. The relevant GitHub issue — scope and acceptance criteria.

## Safety rules

- Preserve user work and inspect the current branch and diff before editing.
- Never delete or regenerate `Assets`, `Packages`, `ProjectSettings`, scenes, prefabs, or `.meta` files to solve a local problem.
- Do not change Unity asset GUIDs without an explicit migration plan.
- Avoid broad scene or prefab reserialization. Make the smallest practical change.
- Do not commit generated Unity folders such as `Library`, `Temp`, `Logs`, `Obj`, or build output.
- Treat balance values as deliberate tunables. Explain and document meaningful changes.
- Do not merge or overwrite unrelated feature-branch work.
- Never expose secrets, tokens, local machine paths, or personal data.

## Workflow

1. Confirm the issue and acceptance criteria.
2. Inspect the implementation and relevant serialized assets.
3. State assumptions when repository evidence is incomplete.
4. Work in a small, scoped branch unless the user explicitly requests a direct commit.
5. Keep code changes focused and compatible with Unity 6000.5.7f1.
6. Preserve existing coding style and public serialized-field compatibility where practical.
7. Verify before reporting completion.
8. Update the issue and applicable repository documents.

## Unity verification

At minimum:

- confirm scripts compile without new errors;
- inspect affected scene/prefab references;
- test the affected path in Play mode;
- report any editor-only step the user must perform.

For match-flow changes, also verify sector progression, spawning, tickets, and victory conditions. For AI changes, inspect diagnostics for objective churn, invalid targets, and depleted-squad behaviour.

If Unity cannot be run in the current environment, say so clearly and provide a short, exact editor test procedure. Never claim a play-mode test was completed when it was not.

## Design and roadmap discipline

- Update `DESIGN.md` when a gameplay rule or product decision changes.
- Update `DEVELOPMENT.md` when current capability, risk, milestone order, or immediate work changes.
- Separate implemented behaviour from planned ideas.
- Turn new ideas into issues before building them when they materially expand scope.
- Prefer completing the vertical slice over adding unvalidated systems.

## GitHub conventions

- One issue should describe one verifiable outcome.
- Use an epic issue for a milestone and link its child tasks.
- Include acceptance criteria in task bodies.
- Reference issues from pull requests and commits where useful.
- Do not mark a task complete until the behaviour is verified.
- Keep `main` usable; use feature branches and pull requests for substantive code changes.

## Communication

Assume the repository owner may be learning Unity. Explain editor actions plainly, name exact assets or menu paths, and distinguish:

- what the agent changed;
- what was verified automatically;
- what still needs a Unity editor check;
- any risk or follow-up decision.
