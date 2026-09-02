# TireForge.Agents

The **AI Foundry** layer: agent clients, prompts, and output schemas.

- `IAgentClient` — abstraction over a Foundry agent call
  - stub implementation (deterministic, for tests / offline dev)
  - real implementation (Azure AI Foundry / Agents SDK)
- prompt templates
- strongly-typed output schemas (JSON schema ↔ C# records)

References `TireForge.Core`.
