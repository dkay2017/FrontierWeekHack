# Runbook — Challenge 3: portal evaluation (Coherence + Fluency)

Manual, in the Microsoft Foundry portal. ~10 min once the agent exists.

**Prereqs:** `anomaly-detection-agent` provisioned (Stage M — `AgentTool provision`),
and `factory/challenge-3-evaluate/eval_portal.jsonl` (10 cases, already in the repo).

## Steps

1. [ai.azure.com/nextgen](https://ai.azure.com/nextgen) → your project → **Build → Evaluations → Create**.
2. Target = **Agent** → pick `anomaly-detection-agent`.
3. **Individual Turns** → **Existing Dataset** → **Upload new dataset**:
   - name it e.g. `factory-eval`
   - file: `factory/challenge-3-evaluate/eval_portal.jsonl`
4. Leave **Field Mapping** / **Configure Agents** as-is.
5. **Criteria** — keep **only Coherence and Fluency**. Remove everything else —
   in particular **deselect Tool Call Accuracy** (the agent can't run
   `check_thresholds` during evaluation, so it always scores low there, and it
   slows the run).
6. Submit. Results land in the **Evaluate** tab in a few minutes.

## Reading the result

- **Aggregate** — one Coherence + one Fluency number (1–5). This is the quality
  baseline to track across agent versions.
- **Per-row** — sort ascending to find the cases dragging the average down; those
  point at prompt-structure (Coherence) or phrasing (Fluency) fixes.

## Success criteria (Challenge 3)

- [ ] Runs over all 10 cases without errors
- [ ] Per-row Coherence + Fluency visible
- [ ] At least one case identified as a candidate for improvement
- [ ] Understand aggregate vs per-row

## The correctness half

Coherence/Fluency don't check whether the classification is *right*. That's
`eval/TireForge.Eval` — deterministic, in CI, gates on 100 % classification
accuracy. See its README.
