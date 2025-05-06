# ChainSharp Architecture

ChainSharp is a .NET library that provides a workflow engine for building and executing chains of operations. This document provides an overview of the project architecture and development environment setup.

## Development Environment

ChainSharp uses a development container (devcontainer) configuration to provide a consistent development environment across different machines. This ensures that all developers have the same tools and dependencies installed, reducing "it works on my machine" issues.

### Using the Dev Container

#### Prerequisites

- [Docker](https://www.docker.com/products/docker-desktop) installed on your machine
- [Visual Studio Code](https://code.visualstudio.com/) with the [Remote - Containers](https://marketplace.visualstudio.com/items?itemName=ms-vscode-remote.remote-containers) extension installed

#### Opening the Project in a Dev Container

1. Clone the repository:
   ```bash
   git clone https://github.com/yourusername/ChainSharp.git
   cd ChainSharp
   ```

2. Open the project in Visual Studio Code:
   ```bash
   code .
   ```

3. When prompted by VS Code, click "Reopen in Container" or use the command palette (F1) and select "Remote-Containers: Reopen in Container".

4. VS Code will build the dev container and open the project inside it. This may take a few minutes the first time.

#### Dev Container Configuration

The dev container is configured using three main files:

1. `.devcontainer/devcontainer.json` - The main configuration file that defines the dev container settings, including:
   - The Docker Compose file to use
   - The service to connect to
   - VS Code settings and extensions to install
   - Port forwarding configuration
   - Post-creation commands

2. `.devcontainer/Dockerfile` - Defines the container image for the development environment:
   - Based on the .NET SDK 8.0 image
   - Installs additional tools and utilities
   - Sets up a non-root user for development

3. `.devcontainer/docker-compose.yml` - Defines the services needed for development:
   - `app` service: The main development container
   - `database` service: PostgreSQL database for persistence
   - Networks and volumes for data persistence

The dev container uses Docker Compose to bring up both the development environment and the PostgreSQL database, allowing you to develop and test with a real database without needing to install PostgreSQL on your host machine.

#### What's Included in the Dev Container

The dev container includes:

- .NET SDK 8.0
- PostgreSQL database for persistence
- Required VS Code extensions for .NET development
- Git and other common development tools

The container is configured to:
- Mount your local repository into the container
- Forward ports for the database and web applications
- Set up the correct environment variables for development
- Provide a connection to the PostgreSQL database via the connection string: `Host=database;Database=chain_sharp;Username=chain_sharp;Password=chain_sharp123`

## Project Structure

ChainSharp is organized into several projects:

- **ChainSharp**: Core library containing the workflow engine
- **ChainSharp.Effect**: Extensions for tracking workflow execution
- **ChainSharp.Effect.Data**: Database persistence for workflow metadata
- **ChainSharp.Effect.Data.InMemory**: In-memory database implementation for testing
- **ChainSharp.Effect.Data.Postgres**: PostgreSQL database implementation for production
- **ChainSharp.Effect.Json**: JSON serialization support
- **ChainSharp.Effect.Mediator**: Mediator pattern implementation for workflows
- **ChainSharp.Effect.Parameter**: Parameter handling for workflows
- **ChainSharp.Tests**: Test projects for the library

## Running Tests

ChainSharp includes several test projects for different components of the system. You can run the tests using the .NET CLI or the VS Code Test Explorer.

### Using the .NET CLI

To run all tests:

```bash
dotnet test
```

To run tests for a specific project:

```bash
dotnet test ChainSharp.Tests/ChainSharp.Tests.csproj
```

### Using VS Code Test Explorer

1. Open the Test Explorer in VS Code (click the beaker icon in the sidebar)
2. Click the "Run All Tests" button or run specific tests by clicking on them

### Integration Tests with PostgreSQL

The integration tests for the PostgreSQL implementation require a running PostgreSQL database. When running inside the dev container, this is automatically set up for you.

The tests use the connection string from the environment variable `ConnectionStrings__ChainSharpDb`, which is configured in the dev container.

## Database Migrations

The PostgreSQL implementation includes database migrations that are automatically applied when the application starts. The migrations are defined in SQL files in the `ChainSharp.Effect.Data.Postgres/Migrations` directory.

To manually apply migrations:

```csharp
// In your application startup code
using ChainSharp.Effect.Data.Postgres.Utils;

// ...

await DatabaseMigrator.Migrate(connectionString);
```

## Contributing

When contributing to ChainSharp, please:

1. Create a new branch for your changes
2. Write tests for your changes
3. Ensure all tests pass
4. Submit a pull request

The dev container ensures that you have the correct development environment set up, making it easier to contribute to the project.
