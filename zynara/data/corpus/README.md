# data/corpus — the File Search grounding corpus

Seven short documents the hosted agents read via **Foundry File Search**:

| File | What it grounds |
|---|---|
| `bupa-mri-lumbar-spine-policy-v3.md` | the criteria (c1–c3), denial codes MN-01/MN-02, **clause 2.1** (the appeal basis) |
| `bupa-mr-arthrogram-shoulder-policy-v1.md` | the shoulder procedure criteria |
| `precedent-P-1.md` / `P-2.md` | comparable cases that **won** on appeal under clause 2.1 |
| `precedent-P-3.md` / `P-4.md` | comparable cases **approved as submitted** (the first-time-submission pattern) |
| `precedent-P-9.md` | a comparable case that **lost** (c3 not evidenced) |

Without this, the Critic correctly refuses to verify claims about the policy
clause or the precedents — it flags *"no policy clause text is quoted"*. With it,
the clean case can auto-submit.

## Load it (manual — 2 minutes)

In the Foundry portal → your project → **Data + indexes** (or **Files** /
**Vector stores**):

1. Create a vector store, name it `care-approval-corpus`.
2. Upload all seven `.md` files from this folder.
3. Copy the vector store id (`vs_…`).

Already have the store (`vs_Aw9xAi58sHDnWqxN5kDy5jFJ`)? Just add the two new
`precedent-P-3.md` / `precedent-P-4.md` files to it.

Then re-run the spike with it on:

```
export VECTOR_STORE_ID="vs_..."
dotnet run --project tools/Zynara.FoundrySpike
```

`infra/modules/foundry.bicep` and the Function app settings take a
`VECTOR_STORE_ID` param for the deployed version.
