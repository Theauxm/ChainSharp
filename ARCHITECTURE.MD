# ChainSharp Architecture

ChainSharp is a .NET library that implements the Railway-oriented programming pattern, providing a robust way to handle errors and compose operations in a functional style.

## Development Environment

This project includes a development container configuration that provides a consistent development environment for all contributors. The dev container includes:

- .NET SDK 8.0
- PostgreSQL database
- All necessary dependencies

## Getting Started with the Dev Container

### Prerequisites

- [Docker](https://www.docker.com/products/docker-desktop)
- [Visual Studio Code](https://code.visualstudio.com/)
- [Remote - Containers extension](https://marketplace.visualstudio.com/items?itemName=ms-vscode-remote.remote-containers)

### Opening the Project in the Dev Container

1. Clone the repository:
   ```bash
   git clone https://github.com/yourusername/ChainSharp.git
   cd ChainSharp
   ```

2. Open the project in Visual Studio Code:
   ```bash
   code .
   ```

3. When prompted, click "Reopen in Container" or use the command palette (F1) and select "Remote-Containers: Reopen in Container".

4. VS Code will build the dev container and open the project inside it. This may take a few minutes the first time.

### Project Structure

- `ChainSharp/`: Core library implementing the Railway-oriented programming pattern
- `ChainSharp.Effect/`: Extensions for handling side effects
- `ChainSharp.Effect.Data/`: Data access abstractions
- `ChainSharp.Effect.Data.Postgres/`: PostgreSQL implementation
- `ChainSharp.Effect.Data.InMemory/`: In-memory implementation for testing
- `ChainSharp.Effect.Json/`: JSON serialization support
- `ChainSharp.Effect.Mediator/`: Mediator pattern implementation
- `ChainSharp.Effect.Parameter/`: Parameter handling
- `ChainSharp.Tests/`: Test utilities
- Various test projects for different components

## Running Tests

The project includes several test projects for different components. You can run all tests or specific test projects.

### Running All Tests

```bash
dotnet test
```

### Running Specific Test Projects

```bash
dotnet test ChainSharp.Tests.Unit/ChainSharp.Tests.Unit.csproj
dotnet test ChainSharp.Tests.Integration/ChainSharp.Tests.Integration.csproj
```

### Running Tests with PostgreSQL

Some integration tests require a PostgreSQL database. The dev container includes a PostgreSQL instance that is automatically configured for testing.

To run tests that require PostgreSQL:

```bash
dotnet test ChainSharp.Tests.Effect.Data.Postgres.Integration/ChainSharp.Tests.Effect.Data.Postgres.Integration.csproj
```

## Core Concepts

### Railway-Oriented Programming

Railway-oriented programming is a functional approach to handling errors. Instead of using exceptions, operations return either a success value or an error value. This allows for composing operations in a way that automatically handles errors.

In ChainSharp, this is implemented using the `Either<Exception, T>` type from the LanguageExt library.

### Workflows

A workflow is a sequence of steps that processes an input and produces a result. Workflows are defined by implementing the `IWorkflow<TInput, TReturn>` interface.

### Steps

Steps are the building blocks of workflows. Each step performs a single operation and returns either a result or an exception. Steps implement the `IStep<TIn, TOut>` interface.

### Memory

Workflows maintain a dictionary of objects called Memory. This allows steps to share data without passing it explicitly. Objects are stored by their type, allowing for type-based retrieval.

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

1. Fork the repository
2. Create your feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add some amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request
