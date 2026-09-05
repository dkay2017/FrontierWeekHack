<!-- markdownlint-disable MD033 MD041 -->
<p align="center">
  <img src="docs/design/Claria-Health-logo.svg" width="480" alt="Claria Health — Care Approval IQ — Proof beats paperwork">
</p>

<h1 align="center">Care Approval IQ</h1>
<p align="center"><em>Proof beats paperwork.</em></p>

---

**A multi-agent system that gets medical treatment approved by insurers — first
time, and won on appeal when it isn't.**

Built by **Claria Health** (fictional) for the **Microsoft Agent-a-thon 2026**
(Architect track). C#/.NET 8 on Azure AI Foundry, Durable Functions, Azure SQL.

_Standalone project in `claria/`, alongside `tireforge/` in the same repository.
Shares no code and no Azure resources with `tireforge/` — that project is
untouched and complete. Only architectural patterns are reused._

## The problem

Prior authorisation — the insurer sign-off a clinician needs before treating a
patient — is the single largest automatable cost centre in health-system admin.
US hospitals spend **$687B/yr on administration** vs **$346B on direct patient
care**. Patients wait; some get worse.

And the appeals gap is stark:

| | |
|---|---|
| Denials that get appealed | **11.5%** |
| Appeals that **win** | **80.7%** |
| Doctors who don't appeal *because they expect to lose* | **62%** — provably wrong |

*(Sources: AMA, HHS OIG, KFF — see `docs/design/ARCHITECTURE.md`.)*

## What it does

Five specialised agents, one human-approved pipeline:

| Agent | Job |
|---|---|
| **Needs-Auth** | Does this payer + plan require prior auth for this procedure *today*? |
| **Evidence Gap** | Reads the clinical note against the payer's criteria; flags what's missing *before* submission |
| **Precedent** | Matches the case to recorded past outcomes; recommends an appeal when the fact pattern historically wins — and drafts it |
| **Expiry Watch** | Catches an approved authorisation expiring before the procedure is scheduled |
| **Policy Drift** | Catches a payer quietly changing criteria and flags stale request templates |

A deterministic **Gate** decides auto-submit vs. human review; nothing is
submitted or appealed without a person approving it.

**Region switch (UK ⇄ US)** — flips the payer rule set, terminology, and
appeal-escalation path. Same agents, proven across markets, live in the demo.

## Repository layout

```
src/            Claria.Core · Agents · Data · Intake · Orchestrator · ApiProxy · Dashboard
tests/          xUnit, one project per src assembly + shared TestSupport
infra/          Bicep — main.bicep + modules (foundry · data · keyvault · apps)
eval/           Claria.Eval — CI-gated agent evaluation harness
tools/          Claria.DbDeploy — azd post-provision migrate + seed + grant
docs/design/    ARCHITECTURE.md · Care-Approval-IQ-TDD.md · DECISIONS.md · logo · architecture SVG
docs/progress/  STATUS.md — session context / resume point
data/           policies (payer criteria) · cases (synthetic clinical scenarios)
```

## Status

Kickoff — 2026-09-05. See `docs/progress/STATUS.md`. Deadline **2026-09-23**.
