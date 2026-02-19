# Repository Guidelines

## Project Structure & Module Organization
- `Assets/Scripts/` contains runtime C# code, organized by feature (`Camera`, `Core`, `Data`, `Grid`, `Managers`, `Presentation`, `TurnSystem`).
- `Assets/Tests/EditMode/` contains NUnit EditMode tests plus `PF2e.Tests.EditMode.asmdef`.
- `Assets/Scenes/SampleScene.unity` is the currently tracked scene.
- `Assets/Prefabs`, `Assets/Materials`, `Assets/ScriptableObjects`, and `Assets/Settings` contain authored content.
- `Packages/manifest.json` and `ProjectSettings/` define dependencies and project settings.
- Treat `Library/`, `Temp/`, `Logs/`, and `UserSettings/` as generated/editor state; do not rely on or commit changes there.

## Build, Test, and Development Commands
- Open the project in Unity 6:
  - `"C:\Program Files\Unity\Hub\Editor\6000.3.2f1\Editor\Unity.exe" -projectPath "D:\Max\Unity3D\My project"`
- Run EditMode tests in batch mode:
- `"C:\Program Files\Unity\Hub\Editor\6000.3.2f1\Editor\Unity.exe" -batchmode -nographics -projectPath "D:\Max\Unity3D\My project" -runTests -testPlatform EditMode -testResults "Logs\EditModeTests.xml" -quit"`
- Iterative local testing:
  - Unity Editor -> `Window > General > Test Runner`.

## Coding Style & Naming Conventions
- Use C# with the `PF2e` namespace family (for example `PF2e.Grid`, `PF2e.Tests`).
- Match existing style: 4-space indentation, Allman braces, explicit access modifiers.
- Naming conventions:
  - Types, methods, properties: `PascalCase`
  - Private fields and locals: `camelCase`
  - Constants: `PascalCase` (for example `ElevationSnapEps`)
- Keep simulation/rules logic engine-light and testable; isolate Unity-specific behavior in presentation/manager layers.

## Testing Guidelines
- Framework: Unity Test Framework + NUnit (`com.unity.test-framework`).
- Place EditMode tests under `Assets/Tests/EditMode/` and name files `*Tests.cs`.
- Follow current method naming style for scenario IDs (for example `GT001_GridCreation10x10`).
- Any change in `Assets/Scripts/Grid/` or `Assets/Scripts/TurnSystem/` should include new or updated EditMode tests in the same PR.

## Commit & Pull Request Guidelines
- Follow repository history style: Conventional Commit prefixes such as `feat:`, `fix:`, `refactor:`, `tweak:`.
- Preferred format: `<type>: <scope/phase> — <concise summary>`.
  - Example: `feat: phase 9 step 2 — TurnManager state machine + advanced initiative tests`
- PRs should include:
  - Clear change summary and motivation
  - Test evidence (Test Runner result or batch test output)
  - Visual proof for UI/scene changes (screenshot/GIF)
  - Linked issue/task when available
