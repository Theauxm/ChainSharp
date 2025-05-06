# ChainSharp Architecture

This document provides an overview of the ChainSharp project architecture, how to use the development container, and how to run tests.

## Development Container

ChainSharp includes a development container configuration that provides a consistent development environment with all necessary dependencies pre-configured. This makes it easy to get started with development and ensures that all developers are working with the same environment.

### Prerequisites

To use the development container, you need:

- [Visual Studio Code](https://code.visualstudio.com/)
- [Docker Desktop](https://www.docker.com/products/docker-desktop)
- [VS Code Remote - Containers extension](https://marketplace.visualstudio.com/items?itemName=ms-vscode-remote.remote-containers)

### Getting Started with the Dev Container

1. Clone the repository:
   ```bash
   git clone https://github.com/Theauxm/ChainSharp.git
   cd ChainSharp
   ```

2. Open the project in VS Code:
   ```bash
   code .
   ```

3. When prompted by VS Code, click "Reopen in Container" or use the command palette (F1) and select "Remote-Containers: Reopen in Container".

4. VS Code will build the development container and open the project inside it. This may take a few minutes the first time.

5. Once the container is running, you'll have access to:
   - .NET 8 SDK
   - PostgreSQL database (accessible at localhost:5432)
   - All required VS Code extensions

### Container Features

The development container includes:

- .NET 8 SDK for building and running the project
- PostgreSQL database for integration tests
- Git and GitHub CLI for source control
- VS Code extensions for .NET development

## Project Structure

ChainSharp is organized into several projects:

- **ChainSharp**: Core library implementing Railway Oriented Programming concepts
- **ChainSharp.Effect**: Extensions for handling effects in workflows
- **ChainSharp.Effect.Data**: Data access abstractions
- **ChainSharp.Effect.Data.Postgres**: PostgreSQL implementation
- **ChainSharp.Effect.Data.InMemory**: In-memory implementation for testing
- **ChainSharp.Effect.Json**: JSON serialization support
- **ChainSharp.Effect.Mediator**: Mediator pattern implementation
- **ChainSharp.Tests**: Test utilities and examples
- **ChainSharp.Tests.***: Various test projects for different components

## Running Tests

ChainSharp includes several test projects for different components. You can run tests using the .NET CLI or the VS Code Test Explorer.

### Using .NET CLI

To run all tests:

```bash
dotnet test
```

To run a specific test project:

```bash
dotnet test ChainSharp.Tests.Unit/ChainSharp.Tests.Unit.csproj
```

### Using VS Code Test Explorer

1. Open the Test Explorer in VS Code (Test icon in the sidebar)
2. Click the "Run All Tests" button or run specific tests/test classes

### Integration Tests with PostgreSQL

The integration tests require a PostgreSQL database. When running inside the development container, this is automatically configured.

The database connection string is configured in `ChainSharp.Tests.Effect.Data.Postgres.Integration/appsettings.json`.

## Development Workflow

1. Make changes to the code
2. Run tests to verify your changes
3. Build the solution:
   ```bash
   dotnet build
   ```
4. Create a pull request with your changes

## Database Migrations

Database migrations for PostgreSQL are located in:
```
ChainSharp.Effect.Data.Postgres/Migrations/
```

These migrations are automatically applied when running integration tests.

## Additional Resources

- [ChainSharp Wiki](https://github.com/Theauxm/ChainSharp/wiki)
- [Railway Oriented Programming](https://fsharpforfunandprofit.com/rop/)
