# .NET 10 Upgrade Plan

## Execution Steps

Execute steps below sequentially one by one in the order they are listed.

1. Validate that an .NET 10 SDK required for this upgrade is installed on the machine and if not, help to get it installed.
2. Ensure that the SDK version specified in global.json files is compatible with the .NET 10 upgrade.
3. Upgrade ChainSharp.Effect.Mediator/ChainSharp.Effect.Mediator.csproj
4. Upgrade ChainSharp.Tests.Unit/ChainSharp.Tests.Unit.csproj
5. Upgrade ChainSharp.Tests.Effect.Data.InMemory.Integration/ChainSharp.Tests.Effect.Data.InMemory.Integration.csproj
6. Upgrade ChainSharp.Effect.Step.Logging/ChainSharp.Effect.Step.Logging.csproj
7. Upgrade ChainSharp.Effect/ChainSharp.Effect.csproj
8. Upgrade ChainSharp.Tests.Effect.Json.Integration/ChainSharp.Tests.Effect.Json.Integration.csproj
9. Upgrade ChainSharp.Tests/ChainSharp.Tests.csproj
10. Upgrade ChainSharp.ArrayLogger/ChainSharp.ArrayLogger.csproj
11. Upgrade ChainSharp/ChainSharp.csproj
12. Upgrade ChainSharp.Tests.Effect.Data.Postgres.Integration/ChainSharp.Tests.Effect.Data.Postgres.Integration.csproj
13. Upgrade ChainSharp.Effect.Parameter/ChainSharp.Effect.Parameter.csproj
14. Upgrade ChainSharp.Effect.Data/ChainSharp.Effect.Data.csproj
15. Upgrade ChainSharp.Effect.Data.InMemory/ChainSharp.Effect.Data.InMemory.csproj
16. Upgrade ChainSharp.Effect.Data.Postgres/ChainSharp.Effect.Data.Postgres.csproj
17. Upgrade ChainSharp.Tests.Effect.Integration/ChainSharp.Tests.Effect.Integration.csproj
18. Upgrade ChainSharp.Effect.Json/ChainSharp.Effect.Json.csproj
19. Upgrade ChainSharp.Tests.Integration/ChainSharp.Tests.Integration.csproj
20. Upgrade ChainSharp.Tests.MemoryLeak.Integration/ChainSharp.Tests.MemoryLeak.Integration.csproj
5. Run unit tests to validate upgrade in the projects listed below:
  - ChainSharp.Tests/ChainSharp.Tests.csproj
  - ChainSharp.Tests.Effect.Data.InMemory.Integration/ChainSharp.Tests.Effect.Data.InMemory.Integration.csproj
  - ChainSharp.Tests.Effect.Data.Postgres.Integration/ChainSharp.Tests.Effect.Data.Postgres.Integration.csproj
  - ChainSharp.Tests.Effect.Integration/ChainSharp.Tests.Effect.Integration.csproj
  - ChainSharp.Tests.Effect.Json.Integration/ChainSharp.Tests.Effect.Json.Integration.csproj
  - ChainSharp.Tests.Integration/ChainSharp.Tests.Integration.csproj
  - ChainSharp.Tests.MemoryLeak.Integration/ChainSharp.Tests.MemoryLeak.Integration.csproj
  - ChainSharp.Tests.Unit/ChainSharp.Tests.Unit.csproj

## Settings

This section contains settings and data used by execution steps.

### Excluded projects

Table below contains projects that do belong to the dependency graph for selected projects and should not be included in the upgrade.

| Project name                                   | Description                 |
|:-----------------------------------------------|:---------------------------:|
| (none)                                         |                            |

### Aggregate NuGet packages modifications across all projects

NuGet packages used across all selected projects or their dependencies that need version update in projects that reference them.

| Package Name                        | Current Version | New Version | Description                                   |
|:------------------------------------|:---------------:|:-----------:|:----------------------------------------------|
| coverlet.collector                  | 6.0.0          | 6.0.4      | recommended for .NET 10                       |
| FluentAssertions                    | 6.12.0         | 8.8.0      | recommended for .NET 10                       |
| LanguageExt.Core                    | 4.4.7          | 5.0.0-beta-77 | recommended for .NET 10                    |
| Microsoft.EntityFrameworkCore       | 8.0.0          | 10.0.2     | recommended for .NET 10                       |
| Microsoft.EntityFrameworkCore.InMemory | 8.0.0       | 10.0.2     | recommended for .NET 10                       |
| Microsoft.Extensions.DependencyInjection | 8.0.0;8.0.2 | 10.0.2     | recommended for .NET 10                       |
| Microsoft.Extensions.DependencyInjection.Abstractions | 8.0.0 | 10.0.2     | recommended for .NET 10                       |
| Microsoft.Extensions.Logging.Abstractions | 8.0.0     | 10.0.2     | recommended for .NET 10                       |
| Microsoft.Extensions.Logging.Console | 8.0.0        | 10.0.2     | recommended for .NET 10                       |
| Microsoft.NET.Test.Sdk              | 17.8.0         | 18.0.1     | recommended for .NET 10                       |
| Newtonsoft.Json                     | 13.0.1         | 13.0.4     | recommended for .NET 10                       |
| NUnit                               | 3.14.0         | 4.4.0      | recommended for .NET 10                       |
| NUnit.Analyzers                     | 3.9.0          | 4.11.2     | recommended for .NET 10                       |
| NUnit3TestAdapter                   | 4.5.0          | 6.1.0      | recommended for .NET 10                       |

### Project upgrade details

This section contains details about each project upgrade and modifications that need to be done in the project.

#### ChainSharp.Effect.Mediator/ChainSharp.Effect.Mediator.csproj modifications

Project properties changes:
  - Target framework should be changed from `net8.0` to `net10.0`

NuGet packages changes:
  - Microsoft.Extensions.Logging.Abstractions should be updated from `8.0.0` to `10.0.2` (recommended for .NET 10)

#### ChainSharp.Tests.Unit/ChainSharp.Tests.Unit.csproj modifications

Project properties changes:
  - Target framework should be changed from `net8.0` to `net10.0`

NuGet packages changes:
  - Microsoft.Extensions.Logging.Abstractions should be updated from `8.0.0` to `10.0.2` (recommended for .NET 10)

#### ChainSharp.Tests.Effect.Data.InMemory.Integration/ChainSharp.Tests.Effect.Data.InMemory.Integration.csproj modifications

Project properties changes:
  - Target framework should be changed from `net8.0` to `net10.0`

NuGet packages changes:
  - coverlet.collector should be updated from `6.0.0` to `6.0.4` (recommended for .NET 10)
  - FluentAssertions should be updated from `6.12.0` to `8.8.0` (recommended for .NET 10)
  - Microsoft.Extensions.DependencyInjection should be updated from `8.0.2` to `10.0.2` (recommended for .NET 10)
  - Microsoft.NET.Test.Sdk should be updated from `17.8.0` to `18.0.1` (recommended for .NET 10)
  - NUnit should be updated from `3.14.0` to `4.4.0` (recommended for .NET 10)
  - NUnit.Analyzers should be updated from `3.9.0` to `4.11.2` (recommended for .NET 10)
  - NUnit3TestAdapter should be updated from `4.5.0` to `6.1.0` (recommended for .NET 10)

#### ChainSharp.Effect.Step.Logging/ChainSharp.Effect.Step.Logging.csproj modifications

Project properties changes:
  - Target framework should be changed from `net8.0` to `net10.0`

NuGet packages changes:
  - Microsoft.Extensions.Logging.Abstractions should be updated from `8.0.0` to `10.0.2` (recommended for .NET 10)

#### ChainSharp.Effect/ChainSharp.Effect.csproj modifications

Project properties changes:
  - Target framework should be changed from `net8.0` to `net10.0`

NuGet packages changes:
  - Microsoft.Extensions.Logging.Abstractions should be updated from `8.0.0` to `10.0.2` (recommended for .NET 10)
  - Newtonsoft.Json should be updated from `13.0.1` to `13.0.4` (recommended for .NET 10)

#### ChainSharp.Tests.Effect.Json.Integration/ChainSharp.Tests.Effect.Json.Integration.csproj modifications

Project properties changes:
  - Target framework should be changed from `net8.0` to `net10.0`

NuGet packages changes:
  - coverlet.collector should be updated from `6.0.0` to `6.0.4` (recommended for .NET 10)
  - FluentAssertions should be updated from `6.12.0` to `8.8.0` (recommended for .NET 10)
  - Microsoft.Extensions.DependencyInjection should be updated from `8.0.0` to `10.0.2` (recommended for .NET 10)
  - Microsoft.Extensions.Logging.Abstractions should be updated from `8.0.0` to `10.0.2` (recommended for .NET 10)
  - Microsoft.Extensions.Logging.Console should be updated from `8.0.0` to `10.0.2` (recommended for .NET 10)
  - Microsoft.NET.Test.Sdk should be updated from `17.8.0` to `18.0.1` (recommended for .NET 10)
  - NUnit should be updated from `3.14.0` to `4.4.0` (recommended for .NET 10)
  - NUnit.Analyzers should be updated from `3.9.0` to `4.11.2` (recommended for .NET 10)
  - NUnit3TestAdapter should be updated from `4.5.0` to `6.1.0` (recommended for .NET 10)

#### ChainSharp.Tests/ChainSharp.Tests.csproj modifications

Project properties changes:
  - Target framework should be changed from `net8.0` to `net10.0`

NuGet packages changes:
  - Microsoft.Extensions.Logging.Abstractions should be updated from `8.0.0` to `10.0.2` (recommended for .NET 10)

#### ChainSharp.ArrayLogger/ChainSharp.ArrayLogger.csproj modifications

Project properties changes:
  - Target framework should be changed from `net8.0` to `net10.0`

NuGet packages changes:
  - Microsoft.Extensions.Logging.Abstractions should be updated from `8.0.0` to `10.0.2` (recommended for .NET 10)

#### ChainSharp/ChainSharp.csproj modifications

Project properties changes:
  - Target framework should be changed from `net8.0` to `net10.0`

NuGet packages changes:
  - LanguageExt.Core should be updated from `4.4.7` to `5.0.0-beta-77` (recommended for .NET 10)
  - Microsoft.Extensions.DependencyInjection.Abstractions should be updated from `8.0.0` to `10.0.2` (recommended for .NET 10)
  - Microsoft.Extensions.Logging.Abstractions should be updated from `8.0.0` to `10.0.2` (recommended for .NET 10)

#### ChainSharp.Tests.Effect.Data.Postgres.Integration/ChainSharp.Tests.Effect.Data.Postgres.Integration.csproj modifications

Project properties changes:
  - Target framework should be changed from `net8.0` to `net10.0`

NuGet packages changes:
  - Microsoft.Extensions.Logging.Abstractions should be updated from `8.0.0` to `10.0.2` (recommended for .NET 10)

#### ChainSharp.Effect.Parameter/ChainSharp.Effect.Parameter.csproj modifications

Project properties changes:
  - Target framework should be changed from `net8.0` to `net10.0`

NuGet packages changes:
  - Microsoft.Extensions.Logging.Abstractions should be updated from `8.0.0` to `10.0.2` (recommended for .NET 10)

#### ChainSharp.Effect.Data/ChainSharp.Effect.Data.csproj modifications

Project properties changes:
  - Target framework should be changed from `net8.0` to `net10.0`

NuGet packages changes:
  - Microsoft.EntityFrameworkCore should be updated from `8.0.0` to `10.0.2` (recommended for .NET 10)

#### ChainSharp.Effect.Data.InMemory/ChainSharp.Effect.Data.InMemory.csproj modifications

Project properties changes:
  - Target framework should be changed from `net8.0` to `net10.0`

NuGet packages changes:
  - Microsoft.EntityFrameworkCore should be updated from `8.0.0` to `10.0.2` (recommended for .NET 10)
  - Microsoft.EntityFrameworkCore.InMemory should be updated from `8.0.0` to `10.0.2` (recommended for .NET 10)

#### ChainSharp.Effect.Data.Postgres/ChainSharp.Effect.Data.Postgres.csproj modifications

Project properties changes:
  - Target framework should be changed from `net8.0` to `net10.0`

NuGet packages changes:
  - Microsoft.EntityFrameworkCore should be updated from `8.0.0` to `10.0.2` (recommended for .NET 10)

#### ChainSharp.Tests.Effect.Integration/ChainSharp.Tests.Effect.Integration.csproj modifications

Project properties changes:
  - Target framework should be changed from `net8.0` to `net10.0`

NuGet packages changes:
  - Microsoft.Extensions.Logging.Abstractions should be updated from `8.0.0` to `10.0.2` (recommended for .NET 10)

#### ChainSharp.Effect.Json/ChainSharp.Effect.Json.csproj modifications

Project properties changes:
  - Target framework should be changed from `net8.0` to `net10.0`

NuGet packages changes:
  - Microsoft.Extensions.Logging.Abstractions should be updated from `8.0.0` to `10.0.2` (recommended for .NET 10)

#### ChainSharp.Tests.Integration/ChainSharp.Tests.Integration.csproj modifications

Project properties changes:
  - Target framework should be changed from `net8.0` to `net10.0`

NuGet packages changes:
  - Microsoft.Extensions.Logging.Abstractions should be updated from `8.0.0` to `10.0.2` (recommended for .NET 10)

#### ChainSharp.Tests.MemoryLeak.Integration/ChainSharp.Tests.MemoryLeak.Integration.csproj modifications

Project properties changes:
  - Target framework should be changed from `net8.0` to `net10.0`

NuGet packages changes:
  - Microsoft.Extensions.Logging.Abstractions should be updated from `8.0.0` to `10.0.2` (recommended for .NET 10)