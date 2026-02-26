---
layout: default
title: DI Registration
parent: API Reference
nav_order: 6
has_children: true
---

# DI Registration

Helper extension methods for registering ChainSharp workflows and steps with the .NET dependency injection container. These methods wrap the standard `AddScoped`/`AddTransient`/`AddSingleton` registrations and add support for `[Inject]` property injection — a pattern used by `ServiceTrain` to inject services like `IEffectRunner` and `ILogger`.

Use these instead of raw DI registration when your workflow or step class uses `[Inject]` properties.

```csharp
services.AddTransientChainSharpRoute<IMyWorkflow, MyWorkflow>();
services.AddScopedChainSharpStep<IMyStep, MyStep>();
```

| Page | Description |
|------|-------------|
| [Workflow Registration]({{ site.baseurl }}{% link api-reference/di-registration/workflow-registration.md %}) | `AddScoped/Transient/SingletonChainSharpRoute` methods |
| [Step Registration]({{ site.baseurl }}{% link api-reference/di-registration/step-registration.md %}) | `AddScoped/Transient/SingletonChainSharpStep` methods |
