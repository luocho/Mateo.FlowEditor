# CODEX_STATE

## Architecture

- WPF editor targeting .NET 10.
- Business messages are generated from `Protos/Flow.proto` with Google.Protobuf.
- `Flow.proto` is the only protobuf schema. Each `.mf` file is one raw `Framework.FlowList` payload containing all Flow pages.
- Save and Save As always normalize the output filename extension to `.mf`.
- Node positions are stored in `FlowStep.stepLocation`.
- `MainViewModel` owns files, Flow selection, task DLL types, and Flow-page navigation.
- `FlowEditorViewModel` owns the active canvas, node/connection editing, ID normalization, and undo/redo.

## Durable decisions

- There are no Start or End node kinds. Nodes use `FlowStepType.ACTION` or `FlowStepType.FLOW`.
- A new Flow starts with one ACTION node whose ID is 1.
- Creating a Flow prompts for a unique, non-empty name. Flow list items expose confirmed rename and delete actions through their context menu.
- Step IDs are derived from canvas order and are renumbered from 1 after insertion, deletion, undo, or redo.
- Connections keep object references so `NextStepId` follows node renumbering automatically.
- Node cards show only ID and Name. FlowStep fields and outgoing NextStep fields are edited in the right inspector.
- ACTION/FLOW node colors are read from `appsettings.json`. Color settings are never written into protobuf data or metadata.
- Connections render only `NextStepConditions`; selecting a connection opens its editable protobuf fields in the right inspector.
- A FLOW step uses `RunType` as its target Flow name (or numeric Flow ID). Double-clicking it navigates to that page.
- ACTION steps may select or enter a task type in `RunType`; FLOW steps may select or enter a Flow target.
- `FlowEditor.exe --model-smoke-test` verifies ID normalization, navigation, and raw `Framework.FlowList` round-trip.
- `FlowEditor.exe --ui-smoke-test` loads the real XAML and verifies key inspector bindings when a desktop UI is available.
