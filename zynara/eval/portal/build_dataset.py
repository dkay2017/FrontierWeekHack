import json

MRI = ("MRI lumbar spine", "Bupa/Comprehensive", "bupa-mri-ls-v3", [
    ("c1", "physiotherapy completed for six weeks"),
    ("c2", "radiculopathy documented on examination"),
    ("c3", "imaging changes management"),
])
SHO = ("MR arthrogram shoulder", "Bupa/Comprehensive", "bupa-mras-v1", [
    ("c1", "conservative management including physiotherapy attempted"),
    ("c2", "instability or labral tear suspected clinically"),
    ("c3", "plain radiographs already obtained"),
])

def q(spec, note):
    proc, payer, ref, crit = spec
    lines = [f"Procedure: {proc} · payer {payer} · policy {ref}.", "Approval criteria:"]
    lines += [f"  [{cid}] {ctext}" for cid, ctext in crit]
    lines += ["", "Clinical note:", note]
    return "\n".join(lines)

def ctx(spec):
    return "; ".join(f"[{cid}] {ctext}" for cid, ctext in spec[3])

rows = [
    (MRI, "Physiotherapy completed for eight weeks with the outcome recorded. Radiculopathy documented on examination with a reduced ankle reflex. The MRI would determine whether to proceed to decompression surgery.",
     "c1 documented, c2 documented, c3 documented; evidence quality high; no contradiction."),
    (MRI, "Radiculopathy documented on examination. Imaging would change management and alter the surgery plan.",
     "c1 missing (no physiotherapy course recorded), c2 documented, c3 documented; the mandatory criterion is unmet."),
    (MRI, "Physiotherapy completed for eight weeks. Imaging changes management and alters the surgery plan.",
     "c1 documented, c2 missing (no neurological findings), c3 documented."),
    (MRI, "Physiotherapy completed for eight weeks. Ongoing back pain, no red flags.",
     "c1 documented, c2 missing, c3 missing; only the mandatory criterion is evidenced."),
    (MRI, "Imaging changes management and alters surgery. Physiotherapy completed for eight weeks. No radiculopathy on examination.",
     "c1 documented, c2 contradicted (examination explicitly negative), c3 documented."),
    (MRI, "Six-week physiotherapy course completed. Dermatomal sensory deficit consistent with an L5 radiculopathy. MRI requested to plan surgical management.",
     "c1 documented, c2 documented, c3 documented; evidence quality high."),
    (MRI, "Patient reports some back pain. No physiotherapy attempted. No neurological deficit. Reassurance given.",
     "c1 missing, c2 missing, c3 missing; nothing supports authorisation."),
    (MRI, "Physiotherapy completed for ten weeks. Radiculopathy documented on examination. Imaging changes management and alters the operative plan.",
     "c1 documented, c2 documented, c3 documented."),
    (MRI, "Physiotherapy completed for eight weeks with good compliance. The patient was unable to attend physiotherapy. Imaging changes management.",
     "the note contradicts itself on physiotherapy (states both completed and not attended); c1 should not be treated as cleanly documented; c3 documented, c2 missing."),
    (MRI, "Completed a supervised physiotherapy programme over seven weeks. Positive straight-leg-raise with an L5 sensory deficit. The scan result decides whether to list for microdiscectomy.",
     "c1 documented, c2 documented, c3 documented; evidence quality high."),
    (MRI, "Physiotherapy ongoing for two weeks so far. Radiculopathy present. Imaging would change management.",
     "c1 partial (course started but not completed to the six-week threshold), c2 documented, c3 documented."),
    (SHO, "Conservative management including physiotherapy attempted for three months. Instability suspected clinically on apprehension testing. Plain radiographs already obtained.",
     "c1 documented, c2 documented, c3 documented."),
    (SHO, "Physiotherapy tried, limited response. Labral tear suspected clinically. Plain radiographs already obtained.",
     "c1 partial, c2 documented, c3 documented."),
    (SHO, "Shoulder pain for six months. No physiotherapy tried yet. Query labral tear. No plain films.",
     "c1 missing, c2 documented, c3 missing; the mandatory criterion is unmet."),
    (SHO, "Six months of conservative management with physiotherapy and activity modification. Apprehension and relocation tests positive for anterior instability. Plain radiographs reported as normal.",
     "c1 documented, c2 documented, c3 documented; evidence quality high."),
]

with open("/workspaces/FrontierWeekHack/zynara/eval/portal/eval_portal.jsonl", "w") as f:
    for spec, note, gt in rows:
        f.write(json.dumps({"query": q(spec, note), "context": ctx(spec), "ground_truth": gt}, ensure_ascii=False) + "\n")
print(len(rows), "rows written")
