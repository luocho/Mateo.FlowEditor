# CODEX_STATE

## Architecture

- WPF editor targeting .NET 10; flow JSON models are in `Models/Models.cs`.
- `MainViewModel` owns files, Flow selection, task DLL types, and available Flow Ids.
- `FlowEditorViewModel` owns the active canvas, node/connection editing, and undo/redo.

## Durable decisions

- Start and End are fixed structural node types. They do not expose `ExeName`, and saving always writes it as an empty string.
- Executable nodes have two selectable kinds: `Func` and `SubFlow`.
- Func nodes select or enter a task type in `ExeName`; SubFlow nodes select a target Flow Id in `ExeName`.
- Old JSON using `nodeType: "Flow"` is normalized to `SubFlow` when loaded.
- Func nodes use blue styling; SubFlow nodes use purple styling. Start and End retain green and gray styling.
- Context-menu node creation captures the canvas position when the menu opens so new nodes remain visible at the right-click location.
- Node-kind and SubFlow target selectors explicitly write their selection back to the node model instead of relying only on default WPF binding behavior.
- The SubFlow target is also committed when its drop-down closes, covering re-selection of the current item when `SelectionChanged` does not fire.
- Node drag handling must ignore `ComboBoxItem` as well as `ComboBox`; WPF renders drop-down items in a separate Popup visual tree, otherwise item clicks are stolen as node drags.
- `FlowEditor.exe --ui-smoke-test` loads the real MainWindow XAML, removes selector bindings, and verifies the selection event paths update node type, color, and SubFlow `ExeName`.

## Next recommended step

- Add automated view-model tests if node validation rules become more complex.
