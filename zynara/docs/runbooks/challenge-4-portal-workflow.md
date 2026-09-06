# Runbook — Challenge 4: persistent assets + portal workflow

Two parts. Part 1 (persistent agents as assets) is already done — the five agents
are created by `FoundryAgentProvisioner` / the de-risk spike and show up under
**Agents** in the portal. Part 2 is a small multi-agent **workflow** built in the
portal designer.

## The workflow to build

The portal designer builds a **linear, unconditional** agent chain, so it can't
express the deterministic Gate or the human-in-the-loop step (those are code — by
design, TDD §3). Build the reasoning core:

```
evidence-gap-agent  →  precedent-strategist-agent  →  critic-agent  →  End
```

read the clinical record against the criteria → weigh the payer's recorded
outcomes for comparable cases → try to disprove the result before a human sees it.
This matches Challenge 4's reference shape (assess → analyse → report) and is the
part of the design that is genuinely an agent hand-off.

`needs-auth-agent` and `claims-extraction-agent` are deliberately **not** nodes:
needs-auth is mostly deterministic rule lookup, and claims-extraction feeds a
deterministic contradiction check, not the next agent.

## Steps

1. [ai.azure.com](https://ai.azure.com) → your project → **Agents** → **Workflows**
   (or **Build → Agent workflows**) → **Create workflow**.
2. Name it `care-approval-reasoning`.
3. Add node 1 → **existing agent** → `evidence-gap-agent`.
4. Add node 2 → `precedent-strategist-agent`; connect node 1 → node 2.
5. Add node 3 → `critic-agent`; connect node 2 → node 3.
6. Connect node 3 → **End**.
7. **Save**, then **Run** with a sample input, e.g.:

   ```
   Procedure: MRI lumbar spine · payer Bupa/Comprehensive · policy bupa-mri-ls-v3.
   Approval criteria:
     [c1] physiotherapy completed for six weeks
     [c2] radiculopathy documented on examination
     [c3] imaging changes management

   Clinical note:
   Physiotherapy completed for eight weeks with the outcome recorded. Radiculopathy
   documented on examination. The MRI would decide whether to proceed to surgery.
   ```

8. Confirm each node produces output and the run reaches **End**.

## Success criteria (Challenge 4)

- [ ] The five agents visible as persistent assets under **Agents**
- [ ] A saved multi-node workflow (`care-approval-reasoning`)
- [ ] One successful run through all three nodes
- [ ] Understood: the portal workflow is the agent hand-off; the Gate, the
      contradiction check, the approval-authority check and the audit trail are in
      the Durable orchestrator (`Zynara.Orchestrator`) because they are
      deterministic — the portal chain captures roughly the reasoning third of the
      design.

## The code side of Challenge 4

`Zynara.Orchestrator` — the Durable Functions hub — is the real workflow: it
sequences the five spokes as activities, runs the Gate inline, and branches on
appeal vs. fresh submission. `AuthOrchestrator.cs`.
