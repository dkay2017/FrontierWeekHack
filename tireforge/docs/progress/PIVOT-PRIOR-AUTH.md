# Pivot exploration — Prior Authorization Copilot

**Status:** decided in principle (2026-09-05), not yet scoped/built.

**⚠ Hard constraint, explicitly stated by the user (2026-09-05): TireForge/Meridian
is NOT touched, modified, or torn down.** It stays exactly as-is — live, deployed,
completed — as the proof-of-concept and evidence of the full challenge lifecycle.
This new work is **a brand-new GitHub repo and brand-new Azure resources**,
nothing shared, nothing reused infrastructurally. What carries forward is
**learnings only**: the architectural patterns (hybrid deterministic+agent
split, Gate/human-approval, citation-based grounding, cost metering, the
Durable Functions shape, managed-identity-first security) and staying aligned
with the **same Challenge 0–4 structure and judging criteria** this whole
program is built around — not any code, repo, or resource.

Deadline: **2026-09-24** (19 days from decision).

## How we got here

Explored and rejected, in order (see chat history for full reasoning):
1. 5 generic "copilot for X" ideas (tutor, clinical scribe, financial pre-mortem,
   SOC copilot, contract review) — rejected: overdone, "done to death."
2. Shift Fairness Negotiator (private per-technician agents negotiating factory
   shift assignments) — rejected: unrealistic, shop-floor workers won't maintain
   AI preference profiles. Good instinct-kill by the user.
3. Civic/council idea — equity-corrected pothole/311 triage (agents correct for
   *who* under-reports, not just what's reported) — rejected: the "1 in 40
   potholes go unreported" claim was circular (estimating what you can't
   measure), and councils don't have budget for a niche fix. User caught both
   flaws.
4. Unsafe housing detection (weak-signal fusion: bins + electricity + voter
   roll + no planning permission) — rejected: real problem, but too small a
   budget holder; "an agent because we ran out of ideas," not real demand.

## The decision: Prior Authorization Copilot (healthcare)

**Business problem:** US hospitals spend $687B/yr on admin vs $346B on actual
patient care. Prior authorization (insurer sign-off before treatment) is named
the single biggest automatable cost centre in health-system admin. Patients
wait, sometimes get worse, while paperwork moves.

**The core insight (why this survives scrutiny unlike the rejected ideas):**
outcomes are **recorded fact**, not an estimate — no circular-reasoning problem
like the civic idea had.

- Only **11.5%** of denials get appealed, but **80.7%** of appeals **win**
  (Medicare Advantage). Skilled nursing: 18% appealed, 95% overturned.
- **62% of doctors don't appeal because they believe they'll lose** — a false
  belief, provably wrong from the payer's own outcome data.
- **CMS-0057-F** (federal rule) forces payers to expose 4 FHIR APIs by Jan 2027
  and publicly report prior-auth metrics — the multi-system orchestration
  surface and the data both become real, not hypothetical, on a regulatory
  timeline that overlaps this build.

**Sources:** AMA, HHS OIG, KFF, CMS.gov, Health Samurai — see chat log for links.

## The 5 agents (final, after review)

Dropped two "thin" candidates (submit-in-payer-format, chase-status) that were
mostly plumbing/formatting with little reasoning — those become actions the
agents below trigger, not agents in their own right.

1. **Needs-auth check** — does this specific payer+plan require prior auth for
   this procedure today? (rules change monthly, differ per plan)
2. **Gap detection** — mines the clinical chart against the payer's actual
   policy criteria, flags what's missing *before* submission
3. **History/appeal-outcome match** — matches this case against past
   submissions' recorded outcomes; recommends appeal when the fact pattern
   historically wins (directly targets the 62%-wrong-belief problem). Same
   architectural pattern as Meridian's `HistoryMatch` (T2), new domain.
4. **Auth-expiry watch** — approved auth expires before the procedure gets
   scheduled → redo everything from scratch. Invisible, cross-system,
   entirely preventable, nobody watches it today.
5. **Policy drift watch** — payer quietly changes criteria; flags "your
   standard template now fails criterion 3" before a doctor submits a
   doomed request on stale assumptions.

## Open decisions for next session

- **Confirmed, not open:** new GitHub repo + new Azure resource group, fully
  separate from TireForge/Meridian's repo and resources. TireForge is untouched.
- Data: synthetic clinical notes + a handful of real public payer policy docs
  (CMS/Medicare policies are public) — needs scoping.
- Which agents are Foundry-hosted vs. deterministic — likely mirrors D12's
  hybrid split (deterministic gap-matching/expiry-math, agent-narrated writeup).
- Full architecture, DB schema, and week-by-week plan against the Sept 24
  deadline — not yet done, this file is the decision record, not the design.

## Timeline reminder

19 days from 2026-09-05 to 2026-09-24. Whatever we build, scope ruthlessly —
Usability ("performs well consistently") punishes an unfinished system harder
than a good idea rewards it. See `JUDGING-SELF-ASSESSMENT.md` for the rubric
discipline that applies here too.
