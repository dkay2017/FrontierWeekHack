# Runbook — Challenge 3: Foundry portal evaluation (Coherence + Fluency)

Manual, in the Microsoft Foundry portal. ~10 minutes once the agent exists.

**Prereqs**
- `evidence-gap-agent` provisioned — it is one of the five the de-risk spike
  creates (`tools/Zynara.FoundrySpike`, or `EnsureZynaraAgentsAsync` on host start).
- `eval/portal/eval_portal.jsonl` (15 turns, in the repo).

## Steps

1. [ai.azure.com](https://ai.azure.com) → your project → **Evaluate → Evaluations → Create**.
2. Target = **Agent** → pick `evidence-gap-agent`.
3. **Individual turns** → **Add your dataset** → **Upload**:
   - name it `care-approval-evidence-gap-eval`
   - file: `eval/portal/eval_portal.jsonl`
4. **Field mapping** — map `query` to the input / user turn. Leave the rest as detected.
5. **Criteria** — keep **Coherence** and **Fluency** only. Remove the others; in
   particular **deselect Tool Call Accuracy** (this agent has no tools, so it
   scores 0 there and slows the run). Optionally add **Groundedness** (maps
   `context`) and **Similarity** (maps `ground_truth`) — not required for the
   challenge.
6. Submit. Results land in the **Evaluations** list in a few minutes.

## Reading the result

- **Aggregate** — one Coherence + one Fluency score (1–5). This is the quality
  baseline to track across agent-prompt versions.
- **Per-row** — sort ascending to find the turns dragging the average down. Low
  Coherence → the prompt structure or the JSON contract needs tightening
  (`AgentPrompts.EvidenceGap`); low Fluency → phrasing.

## Success criteria (Challenge 3)

- [ ] Runs over all 15 turns without errors
- [ ] Per-row Coherence + Fluency visible
- [ ] At least one turn identified as a candidate for a prompt fix
- [ ] Aggregate vs per-row understood

## The correctness half

Coherence/Fluency do not check whether the assessment is *correct*. That is
`eval/Zynara.Eval` — deterministic, in CI (`.github/workflows/zynara-ci.yml`),
hard-gating unsafe automation = 0 and mandatory false-negatives = 0 over 20
labelled cases. See `eval/portal/README.md`.
