# TireForge.Eval

The **CI quality gate** for Challenge 3. Console app, references `TireForge.Core`.

```bash
dotnet run --project tireforge/eval/TireForge.Eval
dotnet run --project tireforge/eval/TireForge.Eval -- --min-accuracy 1.0 --json report.json
```

## What it does

Challenge 3's portal run scores **Coherence + Fluency** (how well the agent
*writes*) with an LLM judge. This harness scores what that can't — **correctness**.

It replays `factory/challenge-4-deploy/evaluation_dataset.json` (10 cases:
normal / warning / critical) through the deterministic core of the anomaly path —
`ThresholdCheck` / **T1**, which is exactly what the agent's classification rests
on (Decision D12) — and checks:

| Metric | Gated? |
|---|---|
| classification (normal / warning / critical) vs ground truth | **yes** — `--min-accuracy` (default 1.0) |
| urgency (low / medium / high) vs ground truth | reported |
| anomaly count vs ground truth | reported |

Deterministic, offline, no model call, ~1 s. Exit code `1` if the gate fails.

Current baseline: **10/10 classification, 10/10 urgency, 10/10 anomaly count.**

## In CI

`.github/workflows/tireforge-ci.yml` runs `build → test → eval gate` on every
push / PR touching `tireforge/**`. A prompt or threshold change that regresses
classification fails the build (Challenge 3 Objective 4 — "integrate evaluations
into a CI/CD pipeline"). The `--json` report is uploaded as an artifact.

## Relationship to the portal evaluation

Both are Challenge 3:

| | measures | run by | when |
|---|---|---|---|
| **Portal Evaluations** | writing quality (Coherence, Fluency) of `anomaly-detection-agent` | a human, once | after a prompt / model change |
| **TireForge.Eval** | classification correctness | CI, every push | every change |

The portal steps are in `docs/runbooks/challenge-3-portal-evaluation.md`.
