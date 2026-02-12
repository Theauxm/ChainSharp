---
layout: default
title: Testing
parent: Usage Guide
nav_order: 6
---

# Testing

## Unit Testing Steps

Steps are easy to test because they're just classes with a `Run` method. Create simple fake implementations of your dependencies:

```csharp
// A simple fake repository for testing
public class FakeUserRepository : IUserRepository
{
    private readonly List<User> _users = [];
    private int _nextId = 1;

    public Task<User?> GetByEmailAsync(string email)
        => Task.FromResult(_users.FirstOrDefault(u => u.Email == email));

    public Task<User> CreateAsync(User user)
    {
        user = user with { Id = _nextId++ };
        _users.Add(user);
        return Task.FromResult(user);
    }

    // Seed data for tests
    public void AddExisting(User user) => _users.Add(user);
}

[Test]
public async Task ValidateEmailStep_ThrowsForDuplicateEmail()
{
    // Arrange
    var repo = new FakeUserRepository();
    repo.AddExisting(new User { Id = 1, Email = "taken@example.com" });

    var step = new ValidateEmailStep(repo);
    var request = new CreateUserRequest { Email = "taken@example.com" };

    // Act & Assert
    await Assert.ThrowsAsync<ValidationException>(() => step.Run(request));
}

[Test]
public async Task CreateUserStep_ReturnsNewUser()
{
    // Arrange
    var repo = new FakeUserRepository();
    var step = new CreateUserStep(repo);
    var request = new CreateUserRequest
    {
        Email = "new@example.com",
        FirstName = "Test",
        LastName = "User"
    };

    // Act
    var result = await step.Run(request);

    // Assert
    Assert.Equal(1, result.Id);  // First user gets ID 1
    Assert.Equal("new@example.com", result.Email);
}
```

## Unit Testing Workflows

Register your fakes in the service collection:

```csharp
[Test]
public async Task CreateUserWorkflow_CreatesUser()
{
    // Arrange
    var services = new ServiceCollection();
    services.AddSingleton<IUserRepository, FakeUserRepository>();
    services.AddSingleton<IEmailService, FakeEmailService>();
    services.AddChainSharpEffects(o => o.AddEffectWorkflowBus(typeof(CreateUserWorkflow).Assembly));

    var provider = services.BuildServiceProvider();
    var bus = provider.GetRequiredService<IWorkflowBus>();

    // Act
    var result = await bus.RunAsync<User>(new CreateUserRequest
    {
        Email = "test@example.com",
        FirstName = "Test",
        LastName = "User"
    });

    // Assert
    Assert.NotNull(result);
    Assert.Equal("test@example.com", result.Email);
}
```

## Integration Testing with InMemory Provider

For integration tests, use the InMemory data provider to avoid database dependencies:

```csharp
[Test]
public async Task Workflow_PersistsMetadata()
{
    // Arrange
    var services = new ServiceCollection();
    services.AddSingleton<IUserRepository, FakeUserRepository>();
    services.AddChainSharpEffects(options =>
        options
            .AddInMemoryEffect()
            .AddEffectWorkflowBus(typeof(CreateUserWorkflow).Assembly)
    );

    var provider = services.BuildServiceProvider();
    var bus = provider.GetRequiredService<IWorkflowBus>();
    var context = provider.GetRequiredService<IDataContext>();

    // Act
    await bus.RunAsync<User>(new CreateUserRequest { Email = "test@example.com" });

    // Assert
    var metadata = await context.Metadatas.FirstOrDefaultAsync();
    Assert.NotNull(metadata);
    Assert.Equal(WorkflowState.Completed, metadata.WorkflowState);
}
```

## Testing with AddServices and IChain

You can use `AddServices` to inject fake step implementations and `IChain` to run them by interface:

```csharp
public class FakeEmailService : IEmailService
{
    public List<string> SentEmails { get; } = [];

    public Task SendWelcomeEmailAsync(string email, string name)
    {
        SentEmails.Add(email);
        return Task.CompletedTask;
    }
}

[Test]
public async Task Workflow_UsesFakeStep()
{
    var fakeEmail = new FakeEmailService();
    var workflow = new TestWorkflow(fakeEmail);

    var result = await workflow.Run(new CreateUserRequest { Email = "test@example.com" });

    Assert.True(result.IsRight);
    Assert.Contains("test@example.com", fakeEmail.SentEmails);
}

public class TestWorkflow(IEmailService emailService) : Workflow<CreateUserRequest, User>
{
    protected override async Task<Either<Exception, User>> RunInternal(CreateUserRequest input)
        => Activate(input)
            .AddServices(emailService)
            .Chain<CreateUserStep>()
            .IChain<IEmailService>()  // Runs the fake
            .Resolve();
}
```
