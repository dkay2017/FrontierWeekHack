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
- over **Azure SQL** (serverless), fronted by a **Static Web App** dashboard

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

Five layers, left to right along the request path, plus three cross-cutting concerns.

| # | Layer | Components |
|---|-------|-----------|
| 1 | **Intake** | `Zynara.Intake` (Azure Function · HTTP `POST /api/requests` + queue output) → Requests Queue (Azure Storage Queue) |
| 2 | **Compute · Orchestration** (Azure Durable Functions) | `Zynara.Orchestrator` (hub, keyed on request id) → NeedsAuthCheck / EvidenceGapMatch / AppealMatch / ExpiryMath / PolicyDiff (activity functions / deterministic spokes) |
| 3 | **AI Foundry · Agent Service** | `needs-auth-agent` · `evidence-gap-agent` · `appeal-builder-agent` · `expiry-watch-agent` · `policy-drift-agent` (persistent Foundry agents; each behind a `Zynara.Core` interface with a stub twin for offline tests) |
| 4 | **Data** | Submission Adapter (Azure Function · the only path to payer portal / X12 / FHIR / fax) → Azure SQL serverless: `Requests · Submissions · Outcomes · Auths · Policies · Precedents · EarlyWarnings · AgentCalls` |
| 5 | **Experience** | Reviewer (human) → Dashboard (`Zynara.Dashboard`, Static Web App Standard + linked backend) → `Zynara.ApiProxy` (Azure Function; read models + reviewer approve/reject actions) |

**Cross-cutting (applies to every layer):**
- **Security & Identity** — managed identity first: SQL (`Active Directory
  Default`), Foundry (Cognitive Services User), storage (identity-based). The two
  unavoidable strings (App Insights connection, content-share key) live in Key
  Vault as `@Microsoft.KeyVault` references. No PHI anywhere in scope.
- **Observability** — App Insights: one W3C trace per request id, a child span
  per agent (`invoke_agent <name>`) and per model call (`chat <model>`), nested
  under one `pipeline.run`. Trace id persisted on the `Submission` row.
- **Responsible AI** — the Gate routes every gap or low-confidence case to a
  human; the Submission Adapter is the sole actor that touches a payer; an
  in-product disclaimer states *decision support, not coverage or medical
  advice*.
- **AI Governance** — every agent call records `{promptTokens, completionTokens,
  model, traceId, requestId}` to the `AgentCalls` table → real £/$ on the
  dashboard's Cost & Governance view.

## 5. End-to-End Flow

`Submit → Buffer → Trigger → NeedsAuth → Gap → AppealMatch → Gate → Review → Send → Decision → Appeal → Review → Track`

1. **Submit** — `Zynara.Intake` receives a request (procedure, coverage ref,
   clinical note, payer+plan, region), drops it on the Requests Queue.
2. **Buffer** — the queue decouples intake from processing.
3. **Trigger** — a new queue message starts the Orchestrator (Durable hub, keyed
   on request id for idempotency).
4. **NeedsAuth** — `NeedsAuthCheck` (deterministic) resolves the payer+plan rule
   set for this procedure and region; `needs-auth-agent` narrates any ambiguous
   plan language and returns `{authRequired, code, policyRef}`. If not required →
   log, stop, nothing submitted.
5. **Gap** — `EvidenceGapMatch` (deterministic) pulls the payer's written
   criteria (File Search over `data/policies/<region>/<payer>/<procedure>`);
   `evidence-gap-agent` reads the clinical note against them and returns
   `{met[], missing[], conflicts[], readiness}`.
6. **Appeal Builder** — `AppealMatch` (deterministic) ranks past `Submissions` +
   `Outcomes` by fact-pattern similarity; `appeal-builder-agent` reports the recorded
   outcomes and recommends **submit / strengthen / appeal**.
7. **Gate** — `readiness ≥ threshold AND no missing criteria AND value ≤
   auto-limit` → an auto-submit draft; otherwise → the human review queue with
   the gap list and the precedents attached.
8. **Review** — the reviewer approves or edits the draft on the dashboard.
9. **Send** — the Submission Adapter submits to the payer in their format
   (portal / X12 278 / FHIR / fax) — the only outbound path.
10. **Decision** — approved → step 13; denied → step 11. The denial letter (PDF)
    is parsed for the reason code and the clause cited.
11. **Appeal** — `appeal-builder-agent` drafts the appeal citing the specific policy
    clause misapplied and the precedent case ids; deterministic code fills the
    dates and the escalation route (region-specific: state/external review vs.
    Financial Ombudsman Service).
12. **Review** — the reviewer approves the appeal; the Adapter files it.
13. **Track** — `ExpiryMath` watches the approved auth's validity window against
    the scheduling feed and raises an `EarlyWarning` if it will expire before the
    procedure date; `PolicyDiff` watches payer policy versions and raises a
    `DriftAlert` naming the affected templates. Both are advisory — no Gate, no
    outbound action.

Throughout: one trace id per request across every hop; every agent call metered
to `AgentCalls`; no agent performs an outbound action.

## 6. Components, Service by Service

**Intake** — `Zynara.Intake` (Function · HTTP trigger `POST /api/requests` + a
simplified request DTO for v1; a FHIR bundle adapter is a roadmap item);
Requests Queue (Storage Queue).

**Compute · Orchestration (Durable Functions)** — `Zynara.Orchestrator` (hub,
sequences the five agents and drives every deterministic spoke, keyed on request
id); NeedsAuthCheck, EvidenceGapMatch, AppealMatch, ExpiryMath, PolicyDiff
(activity functions — all arithmetic and matching, no LLM).

**AI Foundry · Agent Service** — five persistent agents provisioned via
`Azure.AI.Projects` (`AgentAdministrationClient.CreateAgentVersion`), each wired
behind a `Zynara.Core` interface:
| Agent | Reasoning job | Tools / grounding |
|---|---|---|
| `needs-auth-agent` | Plain-language reading of ambiguous plan text | payer rule-set KB, procedure-code lookup |
| `evidence-gap-agent` | Free-text clinical note vs. a structured criteria rubric | File Search over `data/policies/`, the clinical note |
| `appeal-builder-agent` | Corpus-level fact-pattern match; drafts the appeal argument | `Submissions`+`Outcomes` store, policy clause text |
| `expiry-watch-agent` | Narrates a cross-system expiry risk | the auth record + scheduling feed (deterministic date math) |
| `policy-drift-agent` | Explains the operational impact of a criteria change | versioned payer policy documents (deterministic diff) |

**Data** — Submission Adapter (Function · Data Source Adapter; the only path to
payer portal / X12 / FHIR / fax); Azure SQL serverless (`Requests · Submissions ·
Outcomes · Auths · Policies · Precedents · EarlyWarnings · AgentCalls`). Tests run
against an in-memory SQLite double via an `AddZynaraData(Action<DbContextOptionsBuilder>)`
overload (pattern carried from the prior project's D4).

**Experience** — Reviewer (human); Dashboard (`Zynara.Dashboard`, Static Web App
Standard + `linkedBackends` → apiproxy, same-origin `/api`): tabs = **Queue** ·
**Appeals** · **Early Warnings** · **Recovery** (the £/$ view) · **Cost &
Governance** · **Region switch (UK ⇄ US)**; `Zynara.ApiProxy` (Function; read
models + reviewer approve/reject).

**Cross-cutting** — managed identity (SQL / Foundry / storage); Key Vault (App
Insights + content-share strings only, as `@Microsoft.KeyVault` refs); App
Insights (one trace id per request); `Zynara.Eval` (labelled clinical-case set
replayed through `evidence-gap-agent`, CI-gated on classification accuracy);
`Zynara.DbDeploy` (azd post-provision: migrate + seed + grant Function App
identities).

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
- **Azure SQL serverless over SQLite at runtime** — the prior project hit
  SQLite's concurrent-write fragility on Consumption; runtime is Azure SQL, tests
  keep the in-memory SQLite double.
- **New resource group, managed-identity-first from commit 1** — not retrofitted.
- **Payer-agnostic, lead with UK (Bupa/AXA)** — EMEA-region judging; CMS-0057-F
  stays as the "why now" data point, not the framing.
- **Build priority if time runs short (agreed order):**
  1. Intake → Queue → Orchestrator → NeedsAuth + Gap + AppealMatch → Gate →
     Adapter → Data (the core mission), with `needs-auth` + `evidence-gap` +
     `precedent` agents
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
| **2 — agent-keyed traces** | nested `invoke_agent <name>` + `chat <model>` spans under one `pipeline.run` trace, trace id on the `Submission` row |
| **3 — evaluate an agent** | `evidence-gap-agent`, portal Coherence/Fluency + `Zynara.Eval` CI gate on classification accuracy over the labelled case set |
| **4 — persistent assets + portal workflow** | agents visible as assets; a 2–3 node portal workflow, the conditional Gate/appeal steps in the Durable orchestrator |

## 11. How the Design Targets the Three Judging Criteria

| Criterion | The move |
|---|---|
| **Innovation** | Precedent-driven appeal recommendation grounded in **recorded outcomes** — nobody productises the "80% of appeals win, 11.5% are filed" gap. The region switch proves generality *live*, not as a claim. |
| **Usability** | Pre-computed demo playback (zero inference lag), one intuitive queue→review→send flow, the region switch, an accessibility pass, and a recorded 3-minute video as the fallback if a live demo breaks. |
| **Impact** | The Recovery view computes, from the clinic's own live data, `denied × (1 − appeal rate) × win probability × mean claim value = £ left unclaimed` — Impact as a number, not an assertion. |
