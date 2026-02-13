---
layout: default
title: Architecture
nav_order: 6
has_children: true
---

# Architecture

How ChainSharp's components fit together.

## System Architecture Overview

```
┌─────────────────────────────────────────────────────────────────────┐
│                         Client Layer                                │
│    [CLI Applications]  [Web Applications]  [API Controllers]        │
└─────────────────────────────────┬───────────────────────────────────┘
                                  │
          ┌───────────────────────┼───────────────────────┐
          ▼                       ▼                       ▼
┌──────────────────┐  ┌────────────────────┐  ┌──────────────────────┐
│ Dashboard (Opt.) │  │     Mediator       │  │  Scheduler (Opt.)    │
│ [Blazor Server]  │  │  [WorkflowBus]     │  │  [ManifestManager]   │
└────────┬─────────┘  └─────────┬──────────┘  └──────────┬───────────┘
         │                      │                        │
         │                      │◄───────────────────────┘
         │                      │  (Scheduler uses WorkflowBus)
         └──────────┬───────────┘
                    ▼
┌─────────────────────────────────────────────────────────────────────┐
│                     ChainSharp.Effect                               │
│    [EffectWorkflow] ────► [EffectRunner] ────► [EffectProviders]   │
└─────────────────────────────────┬───────────────────────────────────┘
                                  │
                                  ▼
┌─────────────────────────────────────────────────────────────────────┐
│                      Core ChainSharp                                │
│           [Workflow Engine] ────────► [Steps]                       │
└─────────────────────────────────┬───────────────────────────────────┘
                                  │
                                  ▼
┌─────────────────────────────────────────────────────────────────────┐
│                    Effect Implementations                           │
│  [Data Provider]  [JSON Provider]  [Parameter Provider]  [Custom]   │
└────────────┬──────────────────────────────────────────┬─────────────┘
             │                                          │
             ▼                                          ▼
┌─────────────────────────┐                ┌─────────────────────────┐
│       PostgreSQL        │                │        InMemory         │
└─────────────────────────┘                └─────────────────────────┘
```

## Package Hierarchy

```
ChainSharp (Core)
    │
    ├─── ChainSharp.Analyzers (Compile-Time Chain Validation)
    │
    └─── ChainSharp.Effect (Enhanced Workflows)
              │
              ├─── ChainSharp.Effect.Dashboard (Web UI)
              │
              ├─── ChainSharp.Effect.Orchestration.Mediator (WorkflowBus)
              │
              ├─── ChainSharp.Effect.Data (Abstract Persistence)
              │         │
              │         ├─── ChainSharp.Effect.Data.Postgres
              │         └─── ChainSharp.Effect.Data.InMemory
              │
              ├─── ChainSharp.Effect.Orchestration.Scheduler (Job Orchestration)
              │         │
              │         └─── ChainSharp.Effect.Orchestration.Scheduler.Hangfire
              │
              ├─── ChainSharp.Effect.Provider.Json
              ├─── ChainSharp.Effect.Provider.Parameter
              └─── ChainSharp.Effect.StepProvider.Logging
```

## Repository Structure

```
src/
├── core/           ChainSharp
├── effect/         ChainSharp.Effect
├── analyzers/      ChainSharp.Analyzers (Roslyn compile-time validation)
├── data/           ChainSharp.Effect.Data
│                   ChainSharp.Effect.Data.InMemory
│                   ChainSharp.Effect.Data.Postgres
├── providers/      ChainSharp.Effect.Provider.Json
│                   ChainSharp.Effect.Provider.Parameter
│                   ChainSharp.Effect.StepProvider.Logging
├── orchestration/  ChainSharp.Effect.Orchestration.Mediator
│                   ChainSharp.Effect.Orchestration.Scheduler
│                   ChainSharp.Effect.Orchestration.Scheduler.Hangfire
└── dashboard/      ChainSharp.Effect.Dashboard
plugins/
├── vscode/         ChainSharp Chain Hints (VSCode inlay hints extension)
└── rider-resharper/ ChainSharp Chain Hints (Rider/ReSharper inlay hints plugin)
tests/              Test projects
samples/            Sample applications
docs/               Documentation (GitHub Pages)
```
