# TireForge.Core

Domain logic — **pure, no cloud dependencies**. Implements logic Stages A–L.

Building blocks:

- `Model` — sensor reading / asset / risk types
- `Thresholds` (T1) — static threshold evaluation
- `History` (T2) — trend / rolling-window evaluation
- `Gate` — decision gate combining T1 + T2 + agent output
- `Pipeline` — orchestrates the stages end to end

Covered by `tests/TireForge.Core.Tests` (the Stage A–L checks).
