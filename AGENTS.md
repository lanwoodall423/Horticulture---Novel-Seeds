# Horticulture Novel Seeds

- Package ID: `lan.horticulture.novelseeds`.
- Adapter source: `DevTools/BridgeAdapter/HorticultureBridgeAdapter.cs`; package output: `DevTools/BridgeAdapters`.
- Build: `DevTools\Build-HotBridgeAdapter.ps1`; validate: `DevTools\Test-BridgeAdapter.ps1`.
- Query fresh live Dev Bridge context before runtime tests using the Dev Bridge checkout's `DevTools\devbridge.ps1`.
- Reload applies adapter-only changes. Gameplay, defs, Harmony, serialized types, or core changes require a full restart.
- Horticulture owns this optional integration and remains usable without Dev Bridge.
- Full workflow: `DevTools/DEVBRIDGE_AGENT.md`.
