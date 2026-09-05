# Care Approval IQ — build status / session context

**Purpose:** rehydrate context fast after a Codespace or session restart. Keep
current at every checkpoint and **commit + push** — uncommitted work is lost on a
Codespace rebuild.

_Last updated: 2026-09-05 (session 1 — kickoff). Deadline: **2026-09-23 midnight
US**. Submission: 3-min video (required) + repo + architecture doc + TDD +
dashboard UI._

## What this is

**Claria Health — Care Approval IQ.** "Proof beats paperwork." A 5-agent prior
authorisation & appeals system for the Microsoft Agent-a-thon 2026 (Architect
track, EMEA region). C#/.NET 8, Azure AI Foundry, Durable Functions, Azure SQL.
Full design: `docs/design/ARCHITECTURE.md`.

Separate from the team's prior project (TireForge/Meridian — untouched, kept as
proof-of-concept). Lives in `claria/` alongside `tireforge/` in the **same
GitHub repo** (`dkay2017/FrontierWeekHack`); **new Azure resource group**;
patterns reused, not code. (The Codespace token can't create a new repo — the
sibling-folder approach is functionally the same isolation.)

## The 5 agents

Needs-Auth · Evidence Gap · Precedent (drafts the appeal) · Expiry Watch ·
Policy Drift. Payer-agnostic; **UK ⇄ US region switch** in the UI.

## Done — session 1 (2026-09-05)

- Branding locked: Claria Health / Care Approval IQ / "Proof beats paperwork" /
  logo (`docs/design/Claria-Health-logo.svg`).
- `claria/` folder created (sibling to `tireforge/`), structure mirrors it
  (`docs · eval · infra/modules · src · tests · tools` + `data · brand`).
- `docs/design/ARCHITECTURE.md` written (design of record).
- `README.md`, `.gitignore`, `global.json`.

## Done — session 1 (cont.)

- **`docs/design/Care-Approval-IQ-TDD.md`** written — 11 sections in the same
  format as the prior project's TDD (problem, mission/scope, solution overview,
  5-layer architecture table, end-to-end flow, components service-by-service, AI
  governance & responsible AI, deployment/scope decisions, roadmap, challenge
  mapping, judging-criteria mapping).
- **Catchy logo** — `docs/design/Claria-Health-logo.svg`: an approval-seal mark
  whose tick rises out of a shrinking paper stack, wordmark, product name and
  tagline. Old `brand/` folder removed; README + this file repointed.

## Next — session 2

1. **`docs/design/DECISIONS.md`** — start the delta log (D1…).
3. **C# solution scaffold** — `Claria.sln` + the 7 src projects + test projects,
   per-project README stubs.
4. **`infra/` skeleton** — `main.bicep` + module stubs (foundry / data / keyvault
   / apps), `azure.yaml`, `main.parameters.json`.
5. **CI** — `.github/workflows/ci.yml` (build + test) from the first real commit.
6. **Data plan kickoff** — draft 2–3 payer policy files + 5 clinical cases to
   prove the shape.

## Timeline (18 days)

| Dates | Phase |
|---|---|
| Sep 5–7 | Setup — brand ✅, repo ✅, arch doc ✅ · then TDD, sln scaffold, infra skeleton, CI, Challenge 0 provision |
| Sep 7–10 | Data + de-risk spike (one Foundry agent end-to-end) |
| Sep 10–16 | Core build — deterministic engines, 5 agents, orchestrator, gate, region switch, DB, API (Challenge 1) |
| Sep 16–19 | Experience — dashboard, demo flow, Recovery view, the wow moment |
| Sep 18–20 | Challenges 2–4 — traces, eval, portal workflow |
| Sep 20–22 | Harden + rehearse — deploy, live E2E verify, record video, write pitch + 5-point doc, test pass, self re-score |
| Sep 22–23 | Buffer + submit early |

**Demo-ready target: Sep 20.** Submit: Sep 22.

## Reused patterns (from the prior project — learnings, not code)

Hybrid deterministic+agent split · Gate + human approval · citation grounding ·
managed-identity-first infra + Key Vault refs · cost metering · Durable Functions
shape · CI + tests from commit 1 · a strict self-scored judging assessment before
submit.

## Judging criteria (never lose sight)

Innovation · Usability · Impact — 30 each, 90 total. EMEA region, ~6 winners.
Judged by Microsoft-recognised MVPs. See `ARCHITECTURE.md` §15 for how the design
targets each.
