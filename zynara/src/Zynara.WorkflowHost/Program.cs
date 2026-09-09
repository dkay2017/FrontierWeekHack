using Azure.Monitor.OpenTelemetry.Exporter;
using Microsoft.Agents.AI.Hosting.AzureFunctions;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Zynara.Agents;
using Zynara.Core;
using Zynara.Core.Abstractions;
using Zynara.Core.Demo;
using Zynara.Core.Diagnostics;
using Zynara.Core.Pipeline;
using Zynara.Core.Submission;
using Zynara.Core.View;
using Zynara.Data;
using Zynara.Workflow;

// The MAF Workflow host (v2 of Zynara.Orchestrator). `ConfigureDurableWorkflows`
// auto-generates the HTTP surface:
//   POST /api/workflows/CareApprovalPipeline/run
//   GET  /api/workflows/CareApprovalPipeline/status/{runId}
//   POST /api/workflows/CareApprovalPipeline/respond/{runId}   (the review port)
// Each executor runs as a Durable activity on AzureWebJobsStorage — checkpointed,
// retried, replay-safe (the properties TD-1/TD-2 valued, now framework-owned).

var builder = FunctionsApplication.CreateBuilder(args);

builder.Services.AddZynaraAgents(builder.Configuration);
builder.Services.AddZynaraCore();

// Challenge 2 — agent-keyed traces. Agents now run in this host (the `assemble`
// executor), so export the pipeline ActivitySource here (was on the v1
// orchestrator). pipeline.run → spoke.* → invoke_agent * → chat *.
var appInsights = builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];
if (!string.IsNullOrWhiteSpace(appInsights))
    builder.Services.AddOpenTelemetry().WithTracing(t => t
        .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("zynara-workflowhost"))
        .AddSource(ZynaraTelemetry.SourceName)
        .AddAzureMonitorTraceExporter(o => o.ConnectionString = appInsights));

var cosmos = builder.Configuration["COSMOS_ENDPOINT"];
if (!string.IsNullOrWhiteSpace(cosmos))
    builder.Services.AddZynaraData(cosmos, builder.Configuration["COSMOS_DATABASE"] ?? CosmosNames.Database);
else
    builder.Services.AddSingleton<IZynaraStore>(_ => DemoWorld.Seed(new InMemoryZynaraStore()));

// Outbound to the payer stays in the identity-isolated Zynara.Submission app —
// this host runs on id-reasoning and has no Key Vault access. When SUBMISSION_URL
// is set, an authorised approve-send is an HTTP call to that app; otherwise the
// in-process SubmissionService (offline demo / tests).
var submissionUrl = builder.Configuration["SUBMISSION_URL"]?.TrimEnd('/');
var submissionKey = builder.Configuration["SUBMISSION_KEY"];
Func<string, CancellationToken, Task>? externalSubmit = string.IsNullOrWhiteSpace(submissionUrl)
    ? null
    : async (reqId, ct) =>
    {
        using var client = new HttpClient();
        if (!string.IsNullOrWhiteSpace(submissionKey))
            client.DefaultRequestHeaders.Add("x-functions-key", submissionKey);
        var res = await client.PostAsync($"{submissionUrl}/api/submit/{Uri.EscapeDataString(reqId)}", null, ct);
        res.EnsureSuccessStatusCode();
    };

// The pipeline / CaseService are stateless (all state is in the store), so
// capturing one scope's instances at build time is safe for the host.
var scope = builder.Services.BuildServiceProvider().CreateScope().ServiceProvider;
var wf = CareApprovalWorkflow.BuildDurable(
    scope.GetRequiredService<AuthPipeline>(),
    scope.GetRequiredService<CaseService>(),
    scope.GetRequiredService<SubmissionService>(),
    externalSubmit);

builder.ConfigureDurableWorkflows(w => w.AddWorkflow(wf));

var app = builder.Build();
await app.Services.EnsureZynaraAgentsAsync();
app.Run();
