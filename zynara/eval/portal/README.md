# eval/portal — Foundry portal evaluation dataset (Challenge 3)

`eval_portal.jsonl` — 15 turns for the **Foundry portal** evaluation of
`evidence-gap-agent` (Coherence / Fluency, optionally Groundedness / Similarity).

This is the *quality* half of Challenge 3. The *correctness* half —
per-criterion precision/recall, mandatory false-negative rate, route agreement,
hallucination count, all hard-gated in CI — is `eval/Zynara.Eval` (20 labelled
cases). The two are complementary: the portal run scores whether the agent's
prose is coherent and fluent; the CI suite scores whether it is *right*.

## Format

One JSON object per line:

| Field | Use |
|---|---|
| `query` | the exact user message `FoundryEvidenceGapAgent` builds — procedure + numbered criteria + clinical note. The portal runs the agent on this. |
| `context` | the criteria, one line — for Groundedness if you enable it |
| `ground_truth` | the expected per-criterion assessment in words — for Similarity if you enable it |

Coverage: MRI lumbar spine (11) + MR arthrogram shoulder (4); documented /
missing / partial / contradicted criteria and one note that contradicts itself.

Regenerate with `python build_dataset.py` (deterministic).

## Run it

See `../../docs/runbooks/challenge-3-portal-evaluation.md`.
