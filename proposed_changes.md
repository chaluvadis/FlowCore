# Linked-List-Style Workflow Engine — Proposed Architecture & UX Changes

This document collects the full set of architecture and user-experience changes I recommended after reviewing the repository (README, core runtime files: WorkflowEngine.cs, JsonWorkflowEngine.cs, WorkflowBuilder.cs, and the examples). It organizes the proposals into prioritized actionable items, gives concrete interfaces and code-level starting points, and provides a short implementation roadmap.

Use this file as a single source-of-truth for planning refactors, PRs, and documentation/UX work.

---

## Executive summary

The engine has a solid foundation and good design goals (type-safety, composability, persistence, JSON + code authored workflows). To make the project production-ready, easier to extend, and friendlier to developers and operators, the codebase should:

- Split responsibilities into smaller, well-documented services and interfaces.
- Make persistence, block activation, and observability pluggable via well-defined contracts.
- Harden the execution model for distributed, long-running, and idempotent workflows.
- Provide better tooling for JSON authoring (JSON Schema, editor snippets) and operator UX (a small management dashboard and REST hooks).
- Improve error handling patterns (retries, compensation / saga support), validation, and developer diagnostics.

Below you'll find prioritized recommendations, concrete interface proposals, suggested file-level refactors, and a roadmap that you can use to stage incremental improvements.

---

## Prioritized recommendations (top-down)

Priority legend: High / Medium / Low

1. High — Separate responsibilities (extract small interfaces & services).
2. High — Pluggable persistence with clear IWorkflowStore contract and adapters.
3. High — Formalize execution state, idempotency, and correlation IDs.
4. High — Ensure cancellation-aware asynchronous execution across the engine and blocks.
5. High — Improve validation and user-visible errors (builder + JSON).
6. Medium — Observability: OpenTelemetry + audit events + management endpoints.
7. Medium — Error handling improvements: retries, compensation, sagas.
8. Medium — Publish a JSON Schema and VSCode support (IntelliSense).
9. Medium — Packaging: split into small NuGet packages per capability.
10. Low — Visual editor and advanced UI features (long-term).

---

## Proposed architecture: components & contracts

Refactor the engine into a thin facade (WorkflowEngine) that composes smaller services:

- IWorkflowExecutor — orchestrates block execution and transitions.
- IWorkflowParser — parses JSON definitions into WorkflowDefinition models.
- IWorkflowValidator — validates structure, semantics and guards.
- IWorkflowStore — manages persistence, checkpoints, and lease/locking.
- IBlockFactory — resolves and constructs blocks safely (type whitelisting, DI).
- IExecutionMonitor — publishes audit events, metrics and traces.
- IErrorPolicyProvider — provides per-block or per-workflow retry/compensation policies.

Below are recommended surface signatures (C# style) you can use as a starting point.

### 1) IWorkflowExecutor
```csharp
public interface IWorkflowExecutor
{
    Task<WorkflowExecutionResult> ExecuteAsync(
        WorkflowDefinition definition,
        ExecutionContext initialContext,
        CancellationToken cancellationToken = default);

    Task<WorkflowExecutionResult> ResumeAsync(
        WorkflowDefinition definition,
        Guid executionId,
        CancellationToken cancellationToken = default);
}
```

### 2) IWorkflowParser
```csharp
public interface IWorkflowParser
{
    WorkflowDefinition ParseFromJson(string json);
    Task<WorkflowDefinition> ParseFromFileAsync(string path);
}
```

### 3) IWorkflowValidator
```csharp
public interface IWorkflowValidator
{
    ValidationResult Validate(WorkflowDefinition definition);
}

public record ValidationResult(bool IsValid, IEnumerable<string> Errors);
```

### 4) IWorkflowStore
```csharp
public interface IWorkflowStore
{
    Task<ExecutionCheckpoint> CreateExecutionAsync(string workflowId, Guid executionId, ExecutionContext context);
    Task<ExecutionCheckpoint?> LoadLatestCheckpointAsync(string workflowId, Guid executionId, CancellationToken cancellationToken = default);
    Task SaveCheckpointAsync(ExecutionCheckpoint checkpoint, CancellationToken cancellationToken = default);
    Task<bool> TryAcquireLeaseAsync(string workflowId, Guid executionId, TimeSpan leaseDuration);
    Task ReleaseLeaseAsync(string workflowId, Guid executionId);
    Task<IEnumerable<ExecutionMetadata>> QueryExecutionsAsync(string workflowId, ExecutionQueryParameters parameters);
}
```

- ExecutionCheckpoint should contain: checkpoint id, execution id, current block, persisted state (compact), last-updated, serialized context metadata, retry counters, and trace info.
- Store adapters: InMemory (for examples/tests), EFCore (SQL), Redis (fast), Cosmos/DocumentDB (cloud-scale).

### 5) IBlockFactory
```csharp
public interface IBlockFactory
{
    IWorkflowBlock? CreateBlock(WorkflowBlockDefinition blockDefinition);
}

public class BlockFactoryOptions
{
    public bool AllowDynamicLoading { get; set; } = false;
    public IList<string> AllowedAssemblies { get; set; } = new List<string>();
    public IList<string> AllowedTypes { get; set; } = new List<string>();
}
```

### 6) IExecutionMonitor
```csharp
public interface IExecutionMonitor
{
    Task OnWorkflowStartedAsync(WorkflowExecutionMetadata meta);
    Task OnBlockExecutionAsync(BlockExecutionInfo info);
    Task OnWorkflowCompletedAsync(WorkflowExecutionResult result);
    Task OnWorkflowFailedAsync(WorkflowExecutionResult result, Exception ex);
}
```

### 7) IErrorPolicyProvider
```csharp
public interface IErrorPolicyProvider
{
    RetryPolicy GetRetryPolicy(string workflowId, string blockName);
    CompensationPolicy? GetCompensationPolicy(string workflowId, string blockName);
}
```

---

## Execution & state model details

- Use an immutable ExecutionState / ExecutionCheckpoint that records:
  - ExecutionId (Guid), WorkflowId, Version
  - CurrentBlockName, CompletedBlocks list (history), BlockExecutionInfos
  - Persisted state dictionary (key->value)
  - Retry counters, NextRetryAt
  - CorrelationId and optional User/Principal info
- Checkpoint frequency must be configurable (AfterEachBlock, AfterNBlocks, OnStateChange).
- Make all block execution idempotent by either:
  - Having blocks declare idempotency keys and persisted outcomes, or
  - Providing a small helper base class that does read-store-check/write-guard.
- Add correlation id propagation in ExecutionContext, and surface it into logs/traces.

---

## Error handling, retries & compensation

- Provide a first-class RetryPolicy with BackoffStrategy (Fixed, Exponential, Jitter).
- Add Compensation blocks or a compensation pipeline (Saga):
  - Allow workflows to declare compensation blocks per block or at workflow level.
  - When a failure occurs and a rollback strategy is chosen, execute compensation steps in reverse order, with their own retry policies.
- Expose error strategies: Fail, Retry, Skip (with next-block), Compensate-and-Fail, Compensate-and-Continue.

---

## Observability & management

- Integrate OpenTelemetry:
  - Span per workflow, nested spans for blocks with attributes: workflow.id, execution.id, block.name, status, duration.
  - Counters and histograms for block durations and success/failure rates.
- Produce structured audit events (JSON) and allow pluggable sinks: ILogger, EventHub, Kafka, file.
- Implement a lightweight Management API (sample ASP.NET Core) that supports:
  - List executions, show execution history, inspect checkpoint state,
  - Cancel / Suspend / Resume executions,
  - Re-run or re-play executions in test mode.

---

## JSON workflow: schema, validation & tooling

- Publish a JSON Schema (Draft 7+) for workflow definitions. This enables:
  - Editor/VS Code schema binding and IntelliSense.
  - CI validation (lint).
- Validate JSON at parse time: structural validation, reachability (unreachable blocks), circular dependency checks (if not allowed), and semantic validation of transitions.
- Provide a CLI tool (dotnet tool) or GitHub Action that validates JSON files on PRs.

---

## Fluent builder & developer ergonomics

- Make WorkflowBuilder strongly-typed where possible:
  - Support StartWith<TBlock>() and AddBlock<TBlock>() generic methods.
  - Provide typed ExecutionContext helpers: GetState<T>(key), SetState<T>(key, value).
- Extract the builder internals into smaller step classes to minimize Publish-surface area and improve testability.
- Add XML doc comments and produce rich IntelliSense for the public API.
- Provide extension methods to register the whole framework into IServiceCollection:
  - services.AddFlowCore(options => { ... });

---

## Packaging & modularization

- Split the repo into logical packages:
  - FlowCore.Core (interfaces, models, ExecutionContext)
  - FlowCore.Runtime (executor, persistence-agnostic runtime)
  - FlowCore.Json (parser + schema)
  - FlowCore.Persistence.InMemory (for testing)
  - FlowCore.Persistence.EFCore / .Redis / .Cosmos (adapters)
  - FlowCore.Extensions.OpenTelemetry
  - FlowCore.UI.Sample (management dashboard sample)
- Keep dependencies minimal in Core.

---

## Security & safety

- BlockFactory must use a whitelist for allowed assemblies/types when resolving types by name (JSON-defined blocks).
- Document explicit opt-in for dynamic assembly loading.
- Provide strong-name / public-key token checks as a configuration option for secure environments.
- Ensure any admin/management endpoints are secured (JWT, API key, RBAC).

---

## Testing & quality

- Add unit tests for:
  - Execution transitions, skip logic, Wait, Skip behavior.
  - Validator (catches missing blocks, circular refs).
  - Persistence adapters (contract tests).
  - Block idempotency helpers.
- Create integration tests that use the InMemory store.
- Provide a block test harness and mocked ExecutionContext to help block authors write isolated unit tests.

---

## UX / Documentation improvements

- Add the formal JSON Schema file in the repo: `schemas/workflow.schema.json`.
- Provide VS Code extension manifest or settings snippet to bind schema to `.workflow.json` files.
- Expand `FlowCore.Examples` with templates (approvals, order processing, compensation, long-running).
- Add an "Operator's Guide" doc describing the management API and recommended monitoring/alerting rules.
- Add cookbook with "How to debug a failing workflow" + common pitfalls.

---

## Concrete refactor plan (candidate PRs)

These can be staged to keep PRs small and reviewable.

Phase A — Low-risk, high-impact (small, orthogonal PRs):
1. Add new interfaces under `FlowCore.Core/Interfaces`:
   - IWorkflowExecutor, IWorkflowParser, IWorkflowValidator, IWorkflowStore, IBlockFactory, IExecutionMonitor.
   - Add XML docs for each.
2. Make WorkflowEngine a thin facade that depends on IWorkflowExecutor/IWorkflowStore/IBlockFactory (no behavior change initially) — move big methods into new classes but keep behavior same.
3. Add an InMemoryWorkflowStore implementing IWorkflowStore for examples/tests.

Phase B — Feature & architecture changes:
4. Extract block activation logic into BlockFactory, add options for whitelist.
5. Extract JSON parsing from JsonWorkflowEngine into IWorkflowParser and add JSON Schema file.
6. Add IExecutionMonitor and create an OpenTelemetry integration package (or sample).
7. Add improved Validation with clear errors surfaced by Build() and Parse steps.
8. Replace direct Activator.CreateInstance uses with the IBlockFactory and DI-based resolution.

Phase C — Production & Ops:
9. Add EFCore persistence adapter and lease model for distributed execution.
10. Add REST management API sample/dashboard and wire the monitor.
11. Add compensation/saga primitives and policy provider.

---

## Example: minimal IWorkflowStore model and ExecutionCheckpoint (suggested C#)
```csharp
public record ExecutionCheckpoint
{
    public string WorkflowId { get; init; } = default!;
    public Guid ExecutionId { get; init; }
    public string CurrentBlockName { get; init; } = default!;
    public DateTime LastUpdatedUtc { get; init; }
    public IDictionary<string, object> State { get; init; } = new Dictionary<string, object>();
    public IReadOnlyList<BlockExecutionInfo> History { get; init; } = Array.Empty<BlockExecutionInfo>();
    public int RetryCount { get; init; }
    public string CorrelationId { get; init; } = default!;
}
```

---

## Developer & operator checklist (quick actionable list)

- [ ] Extract interfaces and move implementation into well-named classes.
- [ ] Implement InMemory IWorkflowStore and wire it into examples.
- [ ] Add JSON Schema and update JsonWorkflowEngine to validate against it on parse.
- [ ] Add IBlockFactory and replace Activator.CreateInstance usage.
- [ ] Add OpenTelemetry instrumentation around block execution.
- [ ] Add basic management API sample to list/resume/suspend workflows.
- [ ] Add retry and compensation policy provider with default strategies.
- [ ] Publish schema and provide VS Code JSON snippets for authoring.
- [ ] Add CI step to validate workflow JSON files with the schema.
- [ ] Add unit/integration tests for persistence, executor, and validator.

---

## Roadmap & timeline (suggested)

- Week 0–1: Extract interfaces, add InMemory store, and make WorkflowEngine a façade.
- Week 2–4: Implement BlockFactory, JSON Schema, parser refactor, and add validation errors.
- Week 4–8: Add EFCore persistence adapter, OpenTelemetry integration, and management sample.
- Month 3+: Compensation pattern, distributed lease model, visual editor planning.

---

## What to review first

- Confirm the proposed interfaces naming and placement (`FlowCore.Core/Interfaces`).
- Review intended semantics for checkpoints and the IWorkflowStore contract.
- Agree on persistence adapters to target first (EFCore and Redis recommended).
- Validate the desired UX for the management API (endpoints and permission model).

---

## Appendix: Suggested new files & locations

- FlowCore.Core/Interfaces/IWorkflowExecutor.cs
- FlowCore.Core/Interfaces/IWorkflowParser.cs
- FlowCore.Core/Interfaces/IWorkflowValidator.cs
- FlowCore.Core/Interfaces/IWorkflowStore.cs
- FlowCore.Core/Interfaces/IBlockFactory.cs
- FlowCore.Core/Interfaces/IExecutionMonitor.cs
- FlowCore.Core/Persistence/InMemory/InMemoryWorkflowStore.cs
- schemas/workflow.schema.json
- samples/management-dashboard/* (small ASP.NET Core sample)
- tools/workflow-validator (dotnet tool for CI)

---

## Closing notes

This file collects a prioritized, pragmatic plan to make the engine maintainable, safe for production, and delightful for developers and operators. The next step I recommend is to open small, incremental PRs following Phase A above — each PR should introduce one interface and one concrete small implementation or refactor, so reviewers can validate behavior without large merges.

If you want, I can:
- scaffold the interface files and an InMemoryWorkflowStore implementation and open a PR skeleton for Phase A, or
- produce the JSON Schema (workflow.schema.json) and a VS Code settings snippet for schema binding,
- or produce the minimal management dashboard sample (ASP.NET Core) with endpoints for list / inspect / resume.

Tell me which of the three you'd like me to produce first and I will scaffold the files and a PR-ready change set.
