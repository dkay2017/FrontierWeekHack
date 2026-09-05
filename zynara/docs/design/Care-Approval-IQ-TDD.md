# Zynara Health · Care Approval IQ — Technical Design Document

> *Proof beats paperwork.*
> Microsoft Agent-a-thon 2026 · Architect Track Submission · Healthcare Prior Authorisation · Deepak Kumar · Original Work

*(Companion documents: `ARCHITECTURE.md` — design of record; `DECISIONS.md` — delta log;
`../progress/STATUS.md` — build state. The architecture SVG is the authoritative diagram.)*

---

## 1. Problem Statement

A clinician wants to treat a patient — an MRI, a specialist referral, a course of
physiotherapy, surgery. Before it can go ahead, the patient's insurer must
**authorise it in advance** ("prior authorisation" / "pre-authorisation"). Today
that process is manual, slow, and error-prone.

- **What this costs:** US hospitals spend **$687B/yr on administration** vs
  **$346B on direct patient care**; prior authorisation is named the single
  largest automatable cost centre in health-system admin. Patients wait weeks;
  some get worse or die waiting.
- **What's missing:** nobody is reading the clinical chart against the specific
  payer's specific criteria *before* submission, nobody is checking whether the
  fact pattern historically wins on appeal, and nobody is watching an approved
  authorisation for expiry before the procedure is scheduled.
- **The appeals gap (the number the design is built around):** only **11.5%** of
  denied requests are appealed, yet **80.7%** of appeals **win** (95% for
  skilled-nursing). **62% of physicians don't appeal because they believe
  they'll lose** — a belief provably wrong from the payer's own recorded
  outcomes.
- **The ask (per the course brief):** design a production-ready multi-agent
  workflow — ≥2 specialised agents with distinct responsibilities, the tools and
  data each uses, and why multi-agent beats a single generalist — that moves a
  real business process from prototype to production.

## 2. Mission & Scope

**Request inputs:** procedure/service requested · patient coverage reference ·
free-text clinical note · payer + plan · (post-decision) the denial letter.

**Mission:**
- Assemble a gap-checked prior-authorisation submission
- Recommend and draft the appeal when the fact pattern historically wins
- Watch approved authorisations for expiry and payer policies for drift

**In scope for this design:**
- End-to-end pipeline: request → needs-auth → evidence gap → precedent match →
  deterministic Gate → human review → submit → (on denial) appeal draft → review
- Human-in-the-loop approval for **every** outbound action (submit and appeal)
- **Payer-agnostic** rules-as-data, with a visible **UK ⇄ US region switch** that
  swaps the criteria set, terminology and appeal-escalation path
- Grounding: every recommendation cites the policy clause and the precedent
  case ids that drove it
- Cost visibility: per-agent token spend on the governance view
- An in-product **Recovery view** turning the appeals-gap statistic into the
  clinic's own £/$ figure

**Explicitly out of scope (see §8):**
- Real payer EHR/FHIR integration (synthetic + public-policy-excerpt data for the
  demo; CMS-0057-F mandates the real APIs on a 2027 timeline)
- Real PHI — request ids and procedure codes only; no patient-identifying data in
  telemetry or logs
- Automated submission without human sign-off — deliberately never built
- Clinical decision-making — the system is decision *support*, never medical or
  coverage advice; a person decides

## 3. Solution Overview

Care Approval IQ is a multi-agent system on:
- **Microsoft Foundry Agent Service**
- orchestrated by **Azure Durable Functions**
- over **Azure Cosmos DB** (serverless) for operational state, with the
  unstructured corpus (policy documents, denial letters, precedent narratives)
  in **Azure Blob Storage** indexed by **Foundry File Search**
- fronted by a **Static Web App** dashboard

*(Every technology choice below — orchestration model, the deterministic spokes,
the removed queue, Cosmos over SQL, the split doc store — is recorded with its
rationale and its rejected alternative in §12 · Technology Decisions.)*

It replaces manual pre-auth assembly and the "don't bother appealing" reflex with
five specialised agents — each with a narrow reasoning job — and a human reviewer
as the safety net, not a bottleneck.

- **5 purpose-built agents** instead of one generalist — rule lookup, free-text
  comprehension against a rubric, corpus-level precedent matching, date
  arithmetic, and document diffing are five different skills, and one prompt
  doing all five is worse at each.
- **One mandatory outbound path** (the Submission Adapter) — no agent submits to
  a payer or files an appeal directly.
- **A deterministic Gate** between reasoning and action: `readiness ≥ threshold
  AND no missing criteria AND value ≤ auto-limit` → auto-submit draft; otherwise
  → human review queue. An exact-threshold case passes.
- **Hybrid principle (carried from the prior project's D12):** deterministic code
  owns every value that drives a decision or an action; agents produce the prose
  and the judgement over unstructured text.

## 4. Technical Architecture

Four layers along the request path (a clinician request enters the orchestrator's
HTTP endpoint directly — there is no separate intake tier), plus three
cross-cutting concerns.

| # | Layer | Components |
|---|-------|-----------|
| 1 | **Compute · Orchestration** (Azure Durable Functions) | `Zynara.Orchestrator` app: HTTP starter `POST /api/requests` (Durable client — validates, starts the orchestration keyed on request id, returns `202` + status URL) → orchestrator function → NeedsAuthCheck / EvidenceGapMatch / AppealMatch / ExpiryMath / PolicyDiff (**Durable Activity Functions** — deterministic, no LLM; kept as discrete activities for the reasons in §12 · TD-2). No separate intake function, no queue (§12 · TD-3). |
| 2 | **AI Foundry · Agent Service** | `needs-auth-agent` · `evidence-gap-agent` · `appeal-builder-agent` · `expiry-watch-agent` · `policy-drift-agent` (persistent Foundry agents; each behind a `Zynara.Core` interface with a stub twin for offline tests) |
| 3 | **Data** | Submission Adapter (Azure Function · the only path to payer portal / X12 / FHIR / fax) → **Azure Cosmos DB** serverless (operational state): `requests · submissions · outcomes · auths · earlyWarnings · agentCalls`, plus `precedents` / `policies` **metadata** → **Azure Blob Storage** (the unstructured corpus: policy docs, denial PDFs, precedent narratives) → **Foundry File Search** (vector index the agents query). Split rationale: §12 · TD-4, TD-5. |
| 4 | **Experience** | Reviewer (human) → Dashboard (`Zynara.Dashboard`, Static Web App Standard + linked backend; tabs = **Queue** · **Recovery (£/$)** · **Cost & Governance**, with a **UK ⇄ US** header control) → `Zynara.ApiProxy` (Azure Function; read models + reviewer approve/reject actions) |

**Cross-cutting (applies to every layer):**
- **Security & Identity** — managed identity first: Cosmos DB (data-plane RBAC —
  `Cosmos DB Built-in Data Contributor`), Foundry (Cognitive Services User),
  Blob (`Storage Blob Data Contributor`). The two unavoidable strings (App
  Insights connection, content-share key) live in Key Vault as
  `@Microsoft.KeyVault` references. No PHI anywhere in scope.
- **Observability** — App Insights: one W3C trace per request id, a child span
  per agent (`invoke_agent <name>`) and per model call (`chat <model>`), nested
  under one `pipeline.run`. Trace id persisted on the `submissions` document.
- **Responsible AI** — the Gate routes every gap or low-confidence case to a
  human; the Submission Adapter is the sole actor that touches a payer; an
  in-product disclaimer states *decision support, not coverage or medical
  advice*.
- **AI Governance** — every agent call records `{promptTokens, completionTokens,
  model, traceId, requestId}` to the `agentCalls` container → real £/$ on the
  dashboard's Cost & Governance view.

## 5. End-to-End Flow

`Submit → NeedsAuth → Gap → AppealMatch → Gate → Review → Send → Decision → Appeal → Review → Track`

1. **Submit** — `POST /api/requests` (the orchestrator app's Durable HTTP
   starter) receives a request (procedure, coverage ref, clinical note,
   payer+plan, region), validates it, starts the orchestration keyed on request
   id (idempotent), and returns `202` + a status URL. Processing runs async on
   Durable's own control queues — there is no separate intake function or
   Storage Queue (§12 · TD-3).
2. **NeedsAuth** — `NeedsAuthCheck` (deterministic activity) resolves the
   payer+plan rule set for this procedure and region; `needs-auth-agent` narrates
   any ambiguous plan language and returns `{authRequired, code, policyRef}`. If
   not required → log, stop, nothing submitted.
3. **Gap** — `EvidenceGapMatch` (deterministic activity) pulls the payer's
   written criteria (Foundry File Search over
   `policies/<region>/<payer>/<procedure>` in Blob); `evidence-gap-agent` reads
   the clinical note against them and returns
   `{met[], missing[], conflicts[], readiness}`.
4. **Appeal Builder** — `AppealMatch` (deterministic activity) filters the
   `precedents` metadata in Cosmos (payer, procedure, outcome, appeal result)
   and ranks the shortlist by fact-pattern similarity; `appeal-builder-agent`
   reasons over the File-Search-retrieved precedent narratives, reports the
   recorded outcomes and recommends **submit / strengthen / appeal**.
5. **Gate** — `readiness ≥ threshold AND no missing criteria AND value ≤
   auto-limit` → an auto-submit draft; otherwise → the human review queue with
   the gap list and the precedents attached.
6. **Review** — the reviewer approves or edits the draft on the dashboard.
7. **Send** — the Submission Adapter submits to the payer in their format
   (portal / X12 278 / FHIR / fax) — the only outbound path.
8. **Decision** — approved → step 11; denied → step 9. The denial letter (PDF)
    is parsed for the reason code and the clause cited.
9. **Appeal** — `appeal-builder-agent` drafts the appeal citing the specific policy
    clause misapplied and the precedent case ids; deterministic code fills the
    dates and the escalation route (region-specific: state/external review vs.
    Financial Ombudsman Service).
10. **Review** — the reviewer approves the appeal; the Adapter files it.
11. **Track** — `ExpiryMath` watches the approved auth's validity window against
    the scheduling feed and raises an `EarlyWarning` if it will expire before the
    procedure date; `PolicyDiff` watches payer policy versions and raises a
    `DriftAlert` naming the affected templates. Both are advisory — no Gate, no
    outbound action.

Throughout: one trace id per request across every hop; every agent call metered
to `agentCalls`; no agent performs an outbound action.

## 6. Components, Service by Service

**Compute · Orchestration (Durable Functions)** — one Function App,
`Zynara.Orchestrator`:
- **HTTP starter** — `POST /api/requests` (Durable client binding). Validates a
  simplified request DTO for v1 (a FHIR bundle adapter is a roadmap item),
  starts the orchestration keyed on request id, returns `202` +
  `statusQueryGetUri`. This *is* the intake — no separate `Zynara.Intake`
  function, no Requests Queue (§12 · TD-3).
- **Orchestrator function** — sequences the five agents and drives every
  deterministic activity, keyed on request id for idempotency and replay.
- **Activity functions** — NeedsAuthCheck, EvidenceGapMatch, AppealMatch,
  ExpiryMath, PolicyDiff — all arithmetic and matching, no LLM. Each is a
  discrete Durable activity (not an orchestrator helper method) for per-step
  retry isolation, per-step stub twins in CI, and replay-safe resume — full
  justification in §12 · TD-2.

**AI Foundry · Agent Service** — five persistent agents provisioned via
`Azure.AI.Projects` (`AgentAdministrationClient.CreateAgentVersion`), each wired
behind a `Zynara.Core` interface:
| Agent | Reasoning job | Tools / grounding |
|---|---|---|
| `needs-auth-agent` | Plain-language reading of ambiguous plan text | payer rule-set KB, procedure-code lookup |
| `evidence-gap-agent` | Free-text clinical note vs. a structured criteria rubric | Foundry File Search over `policies/` (Blob), the clinical note |
| `appeal-builder-agent` | Corpus-level fact-pattern match; drafts the appeal argument | `precedents` metadata (Cosmos) + precedent narratives (File Search), policy clause text |
| `expiry-watch-agent` | Narrates a cross-system expiry risk | the auth record + scheduling feed (deterministic date math) |
| `policy-drift-agent` | Explains the operational impact of a criteria change | two policy versions from Blob (deterministic diff) |

**Data** —
- **Submission Adapter** (Function · Data Source Adapter; the only path to payer
  portal / X12 / FHIR / fax).
- **Azure Cosmos DB** serverless (Core/NoSQL) — operational state, one container
  per aggregate: `requests`, `submissions`, `outcomes`, `auths`,
  `earlyWarnings`, `agentCalls`, plus `precedents` and `policies` **metadata**
  (`{caseId|policyRef, payerPlan, procedure, outcome, wonOnAppeal, blobRef, …}`).
  Partition key `/requestId` (case containers) or `/payerPlan` (precedent
  metadata); session consistency — one orchestration owns a request id, so no
  multi-writer race. Tests use the Cosmos DB Emulator behind an `IZynaraStore`
  seam with an in-memory implementation for unit tests (pattern carried from the
  prior project's data-double). Rationale for Cosmos over SQL: §12 · TD-4.
- **Azure Blob Storage** — the unstructured corpus:
  `policies/<region>/<payer>/<procedure>.md` (+ versioned copies),
  `denials/<requestId>.pdf`, `precedents/<caseId>.md`.
- **Foundry File Search** — the vector index over the Blob corpus that the
  agents query. Split rationale: §12 · TD-5.

**Experience** — Reviewer (human); Dashboard (`Zynara.Dashboard`, Static Web App
Standard + `linkedBackends` → apiproxy, same-origin `/api`): tabs = **Queue**
(the HITL review list — pending drafts, appeals, and Early-Warning flags in one
place) · **Recovery** (the £/$ view) · **Cost & Governance**; the **UK ⇄ US**
region switch is a header control, not a tab. `Zynara.ApiProxy` (Function; read
models + reviewer approve/reject).

**Cross-cutting** — managed identity (Cosmos data-plane RBAC / Foundry / Blob);
Key Vault (App Insights + content-share strings only, as `@Microsoft.KeyVault`
refs); App Insights (one trace id per request); `Zynara.Eval` (labelled
clinical-case set replayed through `evidence-gap-agent`, CI-gated on
classification accuracy); `Zynara.Seed` (azd post-provision: create Cosmos
containers, upload the Blob corpus, register/refresh the File Search index, seed
synthetic data, grant Function App identities).

## 7. AI Governance & Responsible AI

Governance is a first-class concern, not an afterthought — this is healthcare
adjacent and every recommendation must be auditable.

- **Auditable scoring, not a black-box number.** The Gate's `readiness` and route
  are deterministic and shown with their working ("readiness 0.82 = 6/7 criteria
  met, physio-duration criterion missing"). A regulator or the Financial
  Ombudsman Service can follow the trail: which criterion → which precedent →
  which clause.
- **Every outbound action is human-approved.** Submit and appeal both. The
  Submission Adapter is the sole actor with payer reach; nothing else in the
  system performs an outbound action. This mirrors the prior project's
  Gate/sole-writer pattern (D14) exactly.
- **Cost metering.** Each agent response's `Usage` is persisted to `AgentCalls`
  with the trace id; the Cost & Governance view shows real per-agent tokens and
  £/$ (never mocked figures presented as real — prior project's invariant 1.5).
- **No PHI in scope.** Request ids and procedure codes only in telemetry, logs,
  and the trace tree. Clinical notes are synthetic for the demo.
- **Disclaimer, shown in-product:** *Care Approval IQ is decision support, not
  coverage or medical advice. A person makes every decision.*
- **Region abstraction as governance.** Payer-specific knowledge is data
  (`data/policies/<region>/<payer>/<procedure>.md` + `config/regions.json`), not
  code — so a compliance reviewer can see and version exactly what criteria the
  system applied, per payer, per region.

## 8. Deployment & Scope Decisions

Deliberate trade-offs for a solo, part-time build in an 18-day window
(2026-09-05 → demo-ready 2026-09-20 → submit 2026-09-22, deadline 2026-09-23
midnight US).

- **Synthetic + public-excerpt data over real payer integration** — CMS-0057-F
  forces the real FHIR APIs into existence on a 2027 timeline; for the demo,
  ~30 LLM-generated clinical cases (with a hidden ground-truth of which criteria
  each meets), ~10 procedures × 2 regions of transcribed public criteria (Bupa
  CCSD, a small set of US Medicare LCDs), ~150 internally-consistent synthetic
  past outcomes, and 3 policy-version pairs for the drift demo.
- **Pre-computed demo playback over live inference** — the scripted 3-minute
  demo browses already-processed results for instant, consistent playback; the
  live pipeline is shown separately on one fresh case. (Prior project's lesson:
  Usability — "performs well consistently" — punishes latency and inconsistency
  harder than a good idea rewards it.)
- **Cosmos DB serverless for operational state, Blob + File Search for the
  corpus** — see §12 · TD-4, TD-5. Tests use the Cosmos DB Emulator behind an
  `IZynaraStore` seam (in-memory for unit tests).
- **No Requests Queue** — the Durable HTTP starter is the async boundary; see
  §12 · TD-3.
- **New resource group, managed-identity-first from commit 1** — not retrofitted.
- **Payer-agnostic, lead with UK (Bupa/AXA)** — EMEA-region judging; CMS-0057-F
  stays as the "why now" data point, not the framing.
- **Build priority if time runs short (agreed order):**
  1. HTTP starter → Orchestrator → NeedsAuth + Gap + AppealMatch → Gate →
     Submission Adapter → Cosmos/Blob (the core mission), with `needs-auth` +
     `evidence-gap` + `appeal-builder` agents
  2. Dashboard + Reviewer loop + the Recovery view, on real (synthetic) data
  3. Region switch (UK ⇄ US)
  4. `expiry-watch` + `policy-drift` agents and their Early Warnings tab
  5. `Zynara.Eval` CI gate + portal evaluation (Challenge 3)

## 9. Out of Scope · Future Roadmap

- **Real payer connectivity** — FHIR Prior Authorization / Provider Access APIs,
  X12 278 clearinghouse, portal RPA. CMS-0057-F mandates these by Jan 2027.
- **FHIR bundle intake** — v1 takes a simplified request DTO; a FHIR R4 bundle
  adapter is the first production integration.
- **Eligibility / active-coverage check** — a separate payer system call; assumed
  valid for the demo.
- **Peer-to-peer prep** — assembling the clinician's talking points for the
  10-minute call with the insurer's medical director, from all of the above.
- **Learning from reviewer edits** — closing the loop so the Gate threshold and
  the draft templates improve from what reviewers actually change.

## 10. Mapping to the Agent-a-thon Challenges

| Challenge | Deliverable here |
|---|---|
| **0 — Foundry setup** | `azd provision` — account, project, model, App Insights, **new resource group** |
| **1 — build agents via SDK** | 5 persistent Foundry agents via `Azure.AI.Projects`, wired behind `Zynara.Core` interfaces with stub twins |
| **2 — agent-keyed traces** | nested `invoke_agent <name>` + `chat <model>` spans under one `pipeline.run` trace, trace id on the `submissions` document |
| **3 — evaluate an agent** | `evidence-gap-agent`, portal Coherence/Fluency + `Zynara.Eval` CI gate on classification accuracy over the labelled case set |
| **4 — persistent assets + portal workflow** | agents visible as assets; a 2–3 node portal workflow, the conditional Gate/appeal steps in the Durable orchestrator |

## 11. How the Design Targets the Three Judging Criteria

| Criterion | The move |
|---|---|
| **Innovation** | Precedent-driven appeal recommendation grounded in **recorded outcomes** — nobody productises the "80% of appeals win, 11.5% are filed" gap. The region switch proves generality *live*, not as a claim. |
| **Usability** | Pre-computed demo playback (zero inference lag), one intuitive queue→review→send flow, the region switch, an accessibility pass, and a recorded 3-minute video as the fallback if a live demo breaks. |
| **Impact** | The Recovery view computes, from the clinic's own live data, `denied × (1 − appeal rate) × win probability × mean claim value = £ left unclaimed` — Impact as a number, not an assertion. |

## 12. Technology Decisions

ADR-style. Each records the decision, the reasoning, the cost accepted, and the
rejected alternative. `ARCHITECTURE.md` §17 carries the one-line summary table.

### TD-1 · Orchestration is a deterministic Durable Functions orchestrator, not agent-to-agent chaining

**Decision.** A Durable Functions orchestrator sequences the pipeline and owns
every value that drives a decision or an action.

**Why.** The auto-submit-vs-review decision, the `readiness` threshold, the
pipeline order, and the audit trail must be deterministic and repeatable —
regulator- and Ombudsman-inspectable. Foundry agents do not reliably sequence
each other, and an LLM must never own the Gate. Durable Functions also earns its
place on its own merits: **durable timers** drive `expiry-watch`, and **durable
wait + replay** covers the slow, out-of-band payer round-trip (portal / fax can
take days).

**Rejected.** A single generalist agent orchestrating via connected agents —
non-deterministic call order, no seam for the human, no auditable Gate.

### TD-2 · The 5 deterministic checks are Durable Activity Functions, not orchestrator helper methods

**Decision.** `NeedsAuthCheck`, `EvidenceGapMatch`, `AppealMatch`, `ExpiryMath`,
`PolicyDiff` each ship as their own Durable **activity function**.

**Justification — four concrete properties, none of which a helper method gives:**

1. **Per-step retry isolation.** An agent or File-Search call that 500s or times
   out is retried *at that step* with its own backoff policy. The other four
   agent calls are neither re-run nor re-billed. With a single orchestrator
   function doing five calls in a row, any failure re-runs the whole sequence.
2. **The prose→value boundary is a named unit.** Each activity is where an
   agent's free-text reply becomes the typed contract the Gate consumes
   (`{authRequired, code, policyRef}`, `{met[], missing[], conflicts[],
   readiness}`, …). Keeping that parsing/scoring in its own function stops it
   leaking into the orchestrator alongside the decision rule.
3. **A stub twin per step, exercised in CI.** Each activity has a deterministic
   double (Challenge-1 requirement). The whole pipeline — ordering, Gate logic,
   branch conditions — runs in CI with **zero live inference**: fast, free,
   repeatable.
4. **Replay-safe resume.** Durable records each completed activity in the
   orchestration history. A host restart, deploy, or transient fault mid-pipeline
   resumes at the *next* step, not step 1 — so a case is never double-submitted.

**Cost accepted.** Five extra deployables, plus the orchestrator↔activity
boundary (inputs/outputs must be serialisable). Fine here — the pipeline is
I/O-bound (LLM + File Search + Cosmos), not a hot loop, so the boundary cost is
noise.

**When we would collapse them.** If the steps became a tight synchronous
computation with no independent failure modes and no need for per-step CI
doubles. They are neither.

### TD-3 · No explicit Requests Queue — the Durable HTTP starter is the async boundary

**Decision.** `POST /api/requests` is a Durable Functions HTTP starter: it
validates the request DTO, starts the orchestration keyed on request id, and
returns `202` + `statusQueryGetUri`. There is no `Zynara.Intake` function and no
Azure Storage Queue.

**Why.** The earlier design carried a queue over from a streaming-ingest
project. This workload is request/response at low volume — tens/day for the demo,
low-hundreds/day realistically — with no burst to absorb. The only genuine
requirement is *don't hold the caller open for a multi-second-to-minute
pipeline* (and Functions cap a single execution at ~230 s). Durable already
provides exactly that: the starter returns immediately and the orchestration
runs on Durable's own internal control queues, which still give at-least-once
execution and back-pressure.

**Rejected.** A dedicated Storage Queue — an extra resource, an extra failure
mode, and an extra hop, justified by load that does not exist.

### TD-4 · Operational store is Azure Cosmos DB (serverless), not Azure SQL

**Decision.** Cosmos DB serverless (Core / NoSQL API) holds all operational
state — `requests`, `submissions`, `outcomes`, `auths`, `earlyWarnings`,
`agentCalls`, plus `precedents` and `policies` **metadata**.

**Why.**
- **Shape.** A request / submission / outcome is a nested aggregate — the
  clinical note, the gap-analysis result, the criteria list, the cited
  precedents — that maps to one JSON document, not to a normalised set of joined
  tables.
- **Access pattern.** Partition by `/requestId` (case containers) or
  `/payerPlan` (precedent metadata) makes "everything for this case" or "all
  precedents for this payer+procedure" a single-partition read.
- **Consistency.** Session consistency is sufficient — one orchestration owns a
  `requestId`, keyed and idempotent, so there is no multi-writer race on a case.
- **Cost & ops.** Serverless billing fits spiky, low demo volume; no schema
  migrations.
- **Team familiarity.** Prior project experience with Cosmos.

**Tests.** The in-memory relational double is replaced by an `IZynaraStore` seam
— in-memory implementation for unit tests, Cosmos DB Emulator for integration
tests.

**Rejected.** Azure SQL serverless — a relational schema, EF migrations, and
join modelling for data that is natively document-shaped; the prior project's
SQLite-at-runtime fragility is now moot since runtime is not SQL at all.

### TD-5 · Unstructured corpus is Blob Storage + Foundry File Search, separate from Cosmos

**Decision.** Policy documents, denial-letter PDFs, and precedent **narratives**
live in Azure Blob Storage; Foundry **File Search** owns the vector index the
agents query.

```
policies/<region>/<payer>/<procedure>.md      (+ versioned copies for the drift demo)
denials/<requestId>.pdf
precedents/<caseId>.md                          the fact-pattern narrative
```

Cosmos holds only the **structured metadata** about these artifacts —
`precedents = {caseId, payerPlan, procedure, outcome, wonOnAppeal, reasonCodes[],
blobRef, decidedOn}`, `policies = {policyRef, version, blobRef, effectiveFrom}`.

**Why the split.** Retrieval over the corpus is semantic — `evidence-gap` reads
a note against written criteria, `appeal-builder` matches fact patterns — which
is a vector-search job, not a query job. Forcing it into Cosmos means building
and running our own chunk / embed / index pipeline; File Search does that for
the agents natively. `AppealMatch` (deterministic) filters the `precedents`
metadata in Cosmos first (payer, procedure, outcome), then the agent reasons
over the File-Search-retrieved narratives for the shortlist. `PolicyDiff` runs a
deterministic text diff over two Blob versions — no index needed.

**Rejected.** Cosmos-native vector search for the corpus — couples the corpus to
the operational DB and still needs a home-grown embedding pipeline. Azure AI
Search — more capable hybrid/semantic retrieval, but another service to
provision and secure for no demo-level gain.
