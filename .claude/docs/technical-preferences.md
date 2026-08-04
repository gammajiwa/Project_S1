# Technical Preferences

<!-- Populated by /setup-engine. Updated as the user makes decisions throughout development. -->
<!-- All agents reference this file for project-specific standards and conventions. -->

## Engine & Language

- **Engine**: Unity 6.3 LTS (6000.3.6f1)
- **Language**: C#
- **Rendering**: URP (Universal Render Pipeline) — Mobile + PC render pipeline assets present
- **Physics**: TO BE CONFIGURED (both Physics 2D and 3D modules are enabled)
- **Input**: New Input System (`com.unity.inputsystem`, `InputSystem_Actions.inputactions`)

## Input & Platform

<!-- Written by /setup-engine. Read by /ux-design, /ux-review, /test-setup, /team-ui, and /dev-story -->
<!-- to scope interaction specs, test helpers, and implementation to the correct input methods. -->

- **Target Platforms**: TO BE CONFIGURED (Mobile_RPAsset + PC_RPAsset both present — suggests PC + Mobile)
- **Input Methods**: TO BE CONFIGURED
- **Primary Input**: TO BE CONFIGURED
- **Gamepad Support**: TO BE CONFIGURED
- **Touch Support**: TO BE CONFIGURED
- **Platform Notes**: TO BE CONFIGURED

## Naming Conventions

- **Classes**: PascalCase (e.g. `PlayerController`, `GameManager`)
- **Public fields/properties**: PascalCase (e.g. `MoveSpeed`, `CurrentHealth`)
- **Private fields**: `_camelCase` (e.g. `_moveSpeed`, `_activeUnits`)
- **Methods**: PascalCase (e.g. `TakeDamage()`, `SpawnUnit()`)
- **Events/Delegates**: PascalCase + `EventHandler` suffix (e.g. `PlayerDiedEventHandler`)
- **Files**: PascalCase matching class (e.g. `PlayerController.cs`)
- **Scenes/Prefabs**: PascalCase (e.g. `MainMenu.unity`, `Player.prefab`)
- **Constants**: PascalCase or UPPER_SNAKE_CASE

## Performance Budgets

- **Target Framerate**: TO BE CONFIGURED
- **Frame Budget**: TO BE CONFIGURED
- **Draw Calls**: TO BE CONFIGURED
- **Memory Ceiling**: TO BE CONFIGURED

## Testing

- **Framework**: NUnit (Unity Test Framework — built-in)
- **Minimum Coverage**: TO BE CONFIGURED
- **Required Tests**: TO BE CONFIGURED

## Forbidden Patterns

<!-- Patterns that must NEVER appear in this codebase. Enforced by agents and code review. -->

- **`BinaryFormatter`** — deprecated and insecure in Unity; use a maintained serializer (JSON, MessagePack, etc.) with a `saveVersion` field.
- **Hardcoded balance values in code** — gameplay values belong in ScriptableObjects or data files, never magic numbers inline.
- *[Add project-specific bans here as architecture decisions are made.]*

## Allowed Libraries / Addons

<!-- Only add a library here when it is actively being integrated. -->
<!-- Do NOT add libraries speculatively. -->

- *[None yet — add when actively integrating]*

## Architecture Decisions Log

<!-- Quick reference linking to full ADRs in docs/architecture/ -->
<!-- Written by /architecture-decision, read by all architecture and code-review skills. -->

- *[No ADRs yet — use `/architecture-decision` to create]*

## Engine Specialists

<!-- Written by /setup-engine when engine is configured. -->
<!-- Read by /code-review, /architecture-decision, /architecture-review, and team skills -->
<!-- to know which specialist to spawn for engine-specific validation. -->

- **Primary**: unity-specialist
- **Language/Code Specialist**: unity-specialist (C# code review — primary covers it)
- **Shader Specialist**: unity-shader-specialist (Shader Graph, HLSL, URP/HDRP materials, VFX)
- **UI Specialist**: unity-ui-specialist (UI Toolkit UXML/USS, UGUI Canvas, runtime UI)
- **Additional Specialists**: unity-addressables-specialist (asset loading, memory management, content catalogs)
- **Routing Notes**: Invoke primary for architecture decisions and general C# code review. Invoke shader specialist for URP materials, Shader Graph, and particle/VFX rendering. Invoke UI specialist for HUD/menu implementation. Invoke Addressables specialist for asset loading and memory management.

### File Extension Routing

<!-- Skills use this table to select the right specialist per file type. -->

| File Extension / Type | Specialist to Spawn |
|-----------------------|---------------------|
| Game code (.cs files) | unity-specialist |
| Shader / material files (.shader, .shadergraph, .mat) | unity-shader-specialist |
| UI / screen files (.uxml, .uss, Canvas prefabs) | unity-ui-specialist |
| Scene / prefab / level files (.unity, .prefab) | unity-specialist |
| Native extension / plugin files (.dll, native plugins) | unity-specialist |
| General architecture review | unity-specialist |
