# Migration — Durable Functions → Microsoft Agent Framework

Status: **planned** (2026-09-07, D32). Baseline to roll back to: tag
**`v1.0-durable`** (commit `516f748`), deployed and green.

This repo is the **reference pattern for the team's forthcoming agent projects**.
MAF (the Semantic Kernel + AutoGen successor, .NET RC since 2026-03) is
Microsoft's recommended pattern; we adopt it deliberately, not as a deadline
compromise.

---

## 1 · What changes, what stays

| Layer | v1 (Durable Functions) | v2 (MAF) |
|---|---|---|
| **Orchestration** | `AuthOrchestrator.Run` sequences activities, owns the Gate inline | a `Workflow` graph: `WorkflowBuilder` + typed edges + a Gate `AddSwitch` |
| **Pipeline steps** | `SpokeActivities.*Activity` — thin adapters over `Zynara.Core.Pipeline.*` | `Executor<TIn,TOut>` — thin adapters over the **same** `Zynara.Core.Pipeline.*` |
| **Step I/O DTOs** | `ActivityInputs.cs` | executor message types (mostly the existing `Zynara.Core` result records) |
| **Async boundary** | `Http/RequestEndpoints.cs` (`POST /api/requests`) | auto-generated `POST /api/workflows/CareApproval/run` + `/status/{runId}` |
| **Agent invocation** | `FoundryAgentClient` + 5 `Foundry*Agent` | `aiProjectClient.AsAIAgent(version)` (`Microsoft.Agents.AI.Foundry`) wrapped as executors, or the `Zynara.Core` agent ports kept and driven from executors |
| **Human-in-the-loop** | `POST /api/cases/{id}/decision` then `POST /api/cases/{id}/submit`, two-phase, dashboard-driven | a `RequestPort<ReviewCard, ReviewDecision>` node — the workflow *pauses*; dashboard replies via `/api/workflows/CareApproval/respond/{runId}` |
| **Tracing** | `ZynaraTelemetry` ActivitySource | MAF has OpenTelemetry built in (keep `ZynaraTelemetry` only if it still adds signal) |
| **Durability / retry / replay** | Durable Functions | `Microsoft.Agents.AI.DurableTask` — each executor becomes a Durable activity with checkpointing + retry (same guarantees, framework-owned) |
| **Testing** | `FakeOrchestrationContext` + orchestrator tests; `AuthPipeline` single-process reference | `InProcessExecution.RunStreamingAsync(workflow, input)` — no infra, *simpler* |

**Unchanged (do not touch):** `Zynara.Core` domain model · `Gate.Evaluate` +
`DecisionModel` + `GateRoute` · `ApprovalAuthority` · the deterministic pipeline
*logic* in `Zynara.Core.Pipeline.*` (wrapped by executors instead of activities) ·
`eval/Zynara.Eval` · `Zynara.Data` (Cosmos) · `Zynara.Submission` internals ·
the dashboard's design · most of `infra/`.

**The hybrid principle is preserved.** Deterministic code still owns every value
that drives a decision — the Gate is a plain executor + a `switch`, never an
agent. Agents still only produce prose. TD-1's old rejection of *"agents
orchestrating agents"* never applied to MAF Workflows (a deterministic typed
graph); TD-1a in the TDD records this.

---

## 2 · Packages + hosting

```
Microsoft.Agents.AI
Microsoft.Agents.AI.Workflows
Microsoft.Agents.AI.Foundry                 (Foundry agent binding)
Microsoft.Agents.AI.DurableTask             (durable runtime — prerelease)
Microsoft.Agents.AI.Hosting.AzureFunctions  (ConfigureDurableWorkflows)
Microsoft.DurableTask.{Client,Worker}.AzureManaged   (Durable Task Scheduler)
```

**Hosting decision (confirm in Phase 0):** keep an Azure **Functions** host
(`FunctionsApplication … .ConfigureDurableWorkflows(w => w.AddWorkflow(careApproval))`).
Each executor becomes a Durable activity — we keep serverless scale, per-step
retry and replay-safe resume, and gain the auto HTTP surface. The backend is the
**Durable Task Scheduler** (DTS) — a new Azure resource to add to `infra/`
(local: the `dts-emulator` container). Alternative backend = Azure Storage
(reuses `zynhost…`); DTS is the recommended path.

---

## 3 · The workflow graph

```
                                   ┌─(AutoSubmit)→ SubmitExecutor ─┐
run ─ Intake ─ NeedsAuth ─┬(not required)───────────────────────────────→ Persist ─ End
                          │
                    (required)
                          ↓
              EvidenceGap → Contradiction → PrecedentMatch → Critic → Gate ──switch──┐
                          (each appends to PipelineState, threaded through)          │
                                                                                     │
   ┌─(Strengthen | HumanReview | Abstain)→ ReviewPort ──(approve-send)→ SubmitExecutor┤
   │                                        (RequestPort; workflow pauses)            │
   └─(reject | request-evidence | …)────────────────────────────────────→ Persist ─ End
```

- **`PipelineState`** — an accumulator record threaded down the sequential spine;
  each executor returns `state with { Gap = … }` etc. The **Gate executor** reads
  the whole state and returns `state with { Decision = gate.Evaluate(...) }`.
- **`AddSwitch(gateExecutor, sw => sw.AddCase(s => s.Decision.Route == AutoSubmit, submit).AddCase(…).WithDefault(reviewPort))`**.
- **`ReviewPort = RequestPort<ReviewCard, ReviewDecision>`** — `ReviewCard` is the
  existing `CaseView`; `ReviewDecision` carries `{ action, by, role, note }`.
  `ApprovalAuthority.Check` runs in the executor that consumes the response.
- **`Persist`** runs on every terminal path (writes the `CaseRecord` for the
  dashboard's read model + the audit trail — `CaseService.PersistAsync`, reused).
- **`SubmitExecutor`** calls `Zynara.Submission` (unchanged) — still the sole
  outbound path.

---

## 4 · Dashboard / api-proxy impact

The pending-review state moves into the **workflow run** (Durable), not a
separate "awaiting decision" flag on the Cosmos `CaseRecord`.

- Workflow `runId` is set = `requestId` (deterministic, idempotent — same as now).
- `Persist` still writes a `CaseRecord`, so the dashboard's **read** path
  (`GET /api/cases`, the queue, the drawer) is **unchanged**.
- The dashboard's **approve/reject** buttons change target:
  `POST {api}/api/cases/{id}/decision` → `POST {workflowHost}/api/workflows/CareApproval/respond/{id}`
  with `{ eventName: "ReviewPort", response: { action, role, note } }`.
- `Zynara.ApiProxy` keeps `GET` endpoints + gains a thin `respond` proxy (same
  one-origin pattern as today's `SendCase`), or the dashboard calls the workflow
  host directly (CORS).
- `SendCase` / the separate `/submit` call **goes away** — approval → submission
  is now an edge inside the graph.

---

## 5 · Phased plan

Each phase ends **green (`dotnet test`) and, from Phase 4, redeployable**. Work on
a **`maf-migration` branch**; `main` stays at the v1 baseline until Phase 4 merges.

| Phase | Goal | Exit criteria | Est. |
|---|---|---|---|
| **0 · Spike** | New `src/Zynara.Workflow` project. Port one slice: `Intake → NeedsAuth → EvidenceGap → Gate(stub)` as a MAF workflow. Run it with `InProcessExecution` in a test. **Pin exact package versions + confirm the API surface + confirm DTS vs Storage backend.** | 1 xunit test drives the mini-workflow with the stub agents and asserts a `GateDecision`. Notes on any API drift added to this doc. | 1 d |
| **1 · The graph** | All executors (`NeedsAuth · EvidenceGap · Contradiction · PrecedentMatch · Critic · Gate · Draft · Persist`) + `PipelineState` + the Gate `switch` + terminal `Persist`. Agent ports still driven the v1 way (stub twins). | `InProcessExecution` tests replace `AuthPipelineTests` + `AuthOrchestratorTests` — every demo scenario routes to the expected `GateRoute`. `eval/` still passes. | 2–3 d |
| **2 · Agents as executors** | `Microsoft.Agents.AI.Foundry` — `AsAIAgent` over the existing `care-approval` project agents. A fake `IChatClient` / non-agent executor for the deterministic path so the stub-first pattern survives. Retire `FoundryAgentClient`. | `ZYNARA_AGENTS=foundry` path runs the graph against GPT-5.4 locally (spike-style). Stub path unchanged. | 2 d |
| **3 · HITL `RequestPort`** | The `ReviewPort` node; `ApprovalAuthority` in the consuming executor; approval→submission as a graph edge. Dashboard approve/reject retargeted to `/respond/{runId}`; api-proxy `respond` proxy; drop `SendCase` + `/submit` call. | Local end-to-end: run → pause at review → `respond` approve-send → `submissions` row. Dashboard buttons work against a local workflow host. | 2 d |
| **4 · Durable hosting + deploy** | `Microsoft.Agents.AI.Hosting.AzureFunctions`; DTS (or Storage) backend in `infra/`; the orchestrator Function app becomes the workflow host. Redeploy. Re-verify the S-4 chain. Merge `maf-migration` → `main`. | `POST /api/workflows/CareApproval/run` live → Durable checkpointed run → dashboard → `/respond` → submission. App Insights shows the workflow trace tree. 108-equiv tests green. | 2–3 d |
| **5 · Cleanup + docs** | Delete `src/Zynara.Orchestrator` (v1), `FakeOrchestrationContext`; decide `ZynaraTelemetry` keep/drop; TDD §12 (TD-1a + rewrite TD-1/TD-2 for MAF), ARCHITECTURE, new architecture SVG, `DECISIONS` D33+. | Docs + SVG agree with the code. `v2.0-maf` tag. | 1 d |

**Total ≈ 10–13 working days.** Deadline 2026-09-23 (16 days out). The video +
pitch (~3 d) run **after** Phase 4 (a working deployed system is the prerequisite
for both). Phases 0–1 are the go/no-go gate: if the API or the hosting story
fights us, we ship v1 and keep this branch for after.

---

## 5a · Phase 0 findings (2026-09-07 — GO)

Spike done: `src/Zynara.Workflow` + `tests/Zynara.Workflow.Tests` (4 tests green).
`Request → needs-auth → evidence-gap → contradiction → precedent-match → critic →
gate` runs as a MAF graph via `InProcessExecution.RunAsync` (22 ms, no infra) and
routes `demo-ready / demo-review-mandatory / demo-abstain` to exactly the v1
`GateRoute`.

- **Packages:** `Microsoft.Agents.AI.Workflows` **1.20.0 (stable)** is enough for
  Phases 0–1. `.Foundry` is `1.20.0-preview`; `.DurableTask` + `.Hosting.AzureFunctions`
  lag at `1.16.0-preview` — a version-skew risk to manage in Phase 4.
- **Executors are ~3-line lambdas.** `handler.BindAsExecutor(id)` on a
  `Func<TIn, ValueTask<TOut>>` wraps the unchanged `Zynara.Core.Pipeline.*` class.
  No `Executor` subclass, no source generator, no `[MessageHandler]`.
- **Accumulator pattern works** — a `PipelineState` record threaded on the
  sequential spine; each executor returns `state with { … }`; the Gate executor
  reads the whole state. Cleaner than fan-in for a mostly-linear pipeline.
- **Output:** `WorkflowBuilder.WithOutputFrom(gateStep)` + read the
  `WorkflowOutputEvent` from `run.NewEvents` and `.As<PipelineState>()`.
- **The hybrid principle holds unchanged** — the Gate is a plain lambda executor.
- Blast radius confirmed **small**; tests are simpler than `FakeOrchestrationContext`.

**Decision: GO.** Proceed to Phase 1.

### Phase 1 done (2026-09-07)

Full graph in `CareApprovalWorkflow.Build` — `intake → needs-auth ─┬(not
required)→ finalize │ └(required)→ evidence-gap → contradiction → precedent-match
→ critic → gate → finalize`. `finalize` assembles the same `PipelineResult` v1
produced. `CareApprovalRunner` is the drop-in for `AuthPipeline.RunAsync`;
`AddZynaraWorkflow()` registers it.

`tests/Zynara.Workflow.Tests` — **11 tests**: all 7 demo scenarios route to the
v1 `GateRoute`; the not-required stop; complete-note auto-submit + cited draft;
mandatory-missing → HumanReview + Critic Block; the accumulator carries every
spoke. **Full solution: 119 green.** v1 (`AuthPipeline`, `Zynara.Orchestrator`,
eval) untouched and still green.

The Gate **routing switch** (to a review port / submit) is deferred to Phase 3
with the `RequestPort` — Phase 1 keeps the graph linear through `finalize`, using
a conditional edge only for the not-required early stop.

## 6 · Open questions (resolve in later phases)

1. Exact package versions available on nuget.org for .NET 8 (some are `--prerelease`).
2. DTS vs Azure Storage backend — cost, region (swedencentral), infra complexity.
3. Does `AsAIAgent` re-create Foundry agents, or bind to existing versions?
   (We want **bind to existing** — the `care-approval` agents already exist.)
4. Multi-input to the Gate: accumulator record vs. `AddFanInBarrierEdge`.
5. `RequestPort` response auth — how the reviewer role/identity is carried and
   validated (it lands in an executor; `ApprovalAuthority.Check` runs there).
6. Whether `Zynara.ApiProxy` stays a separate app or folds into the workflow host.
7. Does the built-in OTel emit an equivalent of our `spoke.* / invoke_agent * /
   chat *` tree for Challenge 2, or do we keep `ZynaraTelemetry` alongside.

---

## 7 · Rollback

Any phase: `git checkout v1.0-durable` restores the code; `azd deploy` restores
the live apps (the deployed v1 keeps running untouched until Phase 4). The
`maf-migration` branch is never force-pushed.
