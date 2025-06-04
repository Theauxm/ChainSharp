# ChainSharp Usage Guide

## Practical Implementation Examples and Common Patterns

This guide provides step-by-step examples for implementing workflows using ChainSharp, covering common scenarios from basic setups to advanced patterns.

## Getting Started

### 1. Basic Project Setup

#### Install NuGet Packages
```xml
<PackageReference Include="ChainSharp.Effect" Version="1.0.0" />
<PackageReference Include="ChainSharp.Effect.Data.Postgres" Version="1.0.0" />
<PackageReference Include="ChainSharp.Effect.Mediator" Version="1.0.0" />
```

#### Program.cs Configuration
```csharp
var builder = WebApplication.CreateBuilder(args);

// Add ChainSharp services
builder.Services.AddChainSharpEffects(options => 
    options
        .AddPostgresEffect(builder.Configuration.GetConnectionString("PostgreSQL"))
        .SaveWorkflowParameters()
        .AddEffectWorkflowBus(typeof(Program).Assembly)
);

// Add your application services
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IEmailService, EmailService>();

var app = builder.Build();
app.Run();
```

### 2. Creating Your First Workflow

#### Define Input and Output Models
```csharp
// Input model - unique per workflow
public record CreateUserRequest
{
    public required string Email { get; init; }
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    public string? PhoneNumber { get; init; }
}

// Output model
public record User
{
    public int Id { get; init; }
    public string Email { get; init; }
    public string FullName { get; init; }
    public DateTime CreatedAt { get; init; }
}
```

#### Implement the Workflow
```csharp
public interface ICreateUserWorkflow : IEffectWorkflow<CreateUserRequest, User> { }

public class CreateUserWorkflow : EffectWorkflow<CreateUserRequest, User>, ICreateUserWorkflow
{
    [Inject]
    public IUserRepository UserRepository { get; set; }
    
    [Inject]
    public IEmailService EmailService { get; set; }
    
    protected override async Task<Either<Exception, User>> RunInternal(CreateUserRequest input)
        => Activate(input)
            .Chain<ValidateEmailStep>()
            .Chain<CreateUserInDatabaseStep>()
            .Chain<SendWelcomeEmailStep>()
            .Resolve();
}
```

#### Implement the Steps
```csharp
public class ValidateEmailStep : Step<CreateUserRequest, CreateUserRequest>
{
    [Inject]
    public IUserRepository UserRepository { get; set; }
    
    public override async Task<Either<Exception, CreateUserRequest>> Run(CreateUserRequest input)
    {
        // Check if email already exists
        var existingUser = await UserRepository.GetByEmailAsync(input.Email);
        if (existingUser != null)
            return new ValidationException($"User with email {input.Email} already exists");
        
        // Validate email format
        if (!IsValidEmail(input.Email))
            return new ValidationException("Invalid email format");
            
        return input; // Pass through unchanged
    }
    
    private static bool IsValidEmail(string email)
        => new EmailAddressAttribute().IsValid(email);
}

public class CreateUserInDatabaseStep : Step<CreateUserRequest, User>
{
    [Inject]
    public IUserRepository UserRepository { get; set; }
    
    public override async Task<User> Run(CreateUserRequest input)
    {
        var user = new User
        {
            Email = input.Email,
            FullName = $"{input.FirstName} {input.LastName}",
            CreatedAt = DateTime.UtcNow
        };
        
        return await UserRepository.CreateAsync(user);
    }
}

public class SendWelcomeEmailStep : Step<User, User>
{
    [Inject]
    public IEmailService EmailService { get; set; }
    
    public override async Task<User> Run(User input)
    {
        await EmailService.SendWelcomeEmailAsync(input.Email, input.FullName);
        return input; // Pass through unchanged
    }
}
```

#### Use the Workflow
```csharp
[ApiController]
[Route("api/[controller]")]
public class UsersController(IWorkflowBus workflowBus) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<User>> CreateUser(CreateUserRequest request)
    {
        try
        {
            var user = await workflowBus.RunAsync<User>(request);
            return Created($"/api/users/{user.Id}", user);
        }
        catch (ValidationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            return StatusCode(500, "An error occurred while creating the user");
        }
    }
}
```

## Common Patterns

### 1. Error Handling Patterns

#### Workflow-Level Error Handling
```csharp
public class RobustWorkflow : EffectWorkflow<ProcessOrderRequest, ProcessOrderResult>
{
    protected override async Task<Either<Exception, ProcessOrderResult>> RunInternal(ProcessOrderRequest input)
    {
        try
        {
            return await Activate(input)
                .Chain<ValidateOrderStep>()
                .Chain<ProcessPaymentStep>()
                .Chain<FulfillOrderStep>()
                .Resolve();
        }
        catch (PaymentException ex)
        {
            // Handle payment-specific errors
            EffectLogger?.LogWarning("Payment failed for order {OrderId}: {Error}", 
                input.OrderId, ex.Message);
            return new OrderProcessingException("Payment processing failed", ex);
        }
        catch (InventoryException ex)
        {
            // Handle inventory-specific errors
            return new OrderProcessingException("Insufficient inventory", ex);
        }
    }
}
```

#### Step-Level Error Handling
```csharp
public class RobustStep : Step<PaymentRequest, PaymentResult>
{
    [Inject]
    public IPaymentService PaymentService { get; set; }
    
    [Inject]
    public ILogger<RobustStep> Logger { get; set; }
    
    public override async Task<Either<Exception, PaymentResult>> Run(PaymentRequest input)
    {
        try
        {
            // Attempt primary payment method
            return await PaymentService.ProcessPaymentAsync(input);
        }
        catch (PaymentDeclinedException ex)
        {
            Logger.LogWarning("Primary payment declined, trying backup method");
            
            // Try backup payment method
            var backupRequest = input with { UseBackupMethod = true };
            return await PaymentService.ProcessPaymentAsync(backupRequest);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Payment processing failed for {Amount}", input.Amount);
            return ex;
        }
    }
}
```

### 2. Nested Workflow Patterns

#### Parent Workflow Orchestrating Child Workflows
```csharp
public class ProcessOrderWorkflow : EffectWorkflow<ProcessOrderRequest, ProcessOrderResult>
{
    [Inject]
    public IWorkflowBus WorkflowBus { get; set; }
    
    protected override async Task<Either<Exception, ProcessOrderResult>> RunInternal(ProcessOrderRequest input)
    {
        var result = new ProcessOrderResult { OrderId = input.OrderId };
        
        // Validate order (child workflow)
        var validationResult = await WorkflowBus.RunAsync<ValidationResult>(
            new ValidateOrderRequest { OrderId = input.OrderId },
            Metadata // Pass current metadata as parent
        );
        
        if (!validationResult.IsValid)
            return new OrderValidationException(validationResult.Errors);
        
        // Process payment (child workflow)
        var paymentResult = await WorkflowBus.RunAsync<PaymentResult>(
            new ProcessPaymentRequest 
            { 
                OrderId = input.OrderId,
                Amount = input.TotalAmount,
                PaymentMethod = input.PaymentMethod
            },
            Metadata
        );
        
        if (!paymentResult.Success)
            return new PaymentProcessingException(paymentResult.ErrorMessage);
        
        result.PaymentId = paymentResult.PaymentId;
        
        // Fulfill order (child workflow)
        var fulfillmentResult = await WorkflowBus.RunAsync<FulfillmentResult>(
            new FulfillOrderRequest { OrderId = input.OrderId },
            Metadata
        );
        
        result.TrackingNumber = fulfillmentResult.TrackingNumber;
        result.EstimatedDelivery = fulfillmentResult.EstimatedDelivery;
        
        return result;
    }
}
```

### 3. Conditional Logic Patterns

#### Branching Logic in Workflows
```csharp
public class ConditionalWorkflow : EffectWorkflow<ProcessUserRequest, ProcessUserResult>
{
    protected override async Task<Either<Exception, ProcessUserResult>> RunInternal(ProcessUserRequest input)
    {
        // Start with validation
        var validationResult = await Activate(input)
            .Chain<ValidateUserStep>()
            .Resolve();
            
        if (validationResult.IsLeft)
            return validationResult.Swap();
        
        var validatedUser = validationResult.ValueUnsafe();
        
        // Conditional processing based on user type
        if (validatedUser.IsPremiumUser)
        {
            return await Activate(validatedUser)
                .Chain<PremiumUserProcessingStep>()
                .Chain<SendPremiumNotificationStep>()
                .Resolve();
        }
        else
        {
            return await Activate(validatedUser)
                .Chain<StandardUserProcessingStep>()
                .Chain<SendStandardNotificationStep>()
                .Resolve();
        }
    }
}
```

#### Using Steps for Complex Branching
```csharp
public class ConditionalProcessingStep : Step<UserData, ProcessingResult>
{
    [Inject]
    public IWorkflowBus WorkflowBus { get; set; }
    
    public override async Task<Either<Exception, ProcessingResult>> Run(UserData input)
    {
        return input.UserType switch
        {
            UserType.Premium => await ProcessPremiumUser(input),
            UserType.Standard => await ProcessStandardUser(input),
            UserType.Trial => await ProcessTrialUser(input),
            _ => new InvalidOperationException($"Unknown user type: {input.UserType}")
        };
    }
    
    private async Task<ProcessingResult> ProcessPremiumUser(UserData user)
    {
        // Use nested workflow for complex premium processing
        return await WorkflowBus.RunAsync<ProcessingResult>(
            new PremiumProcessingRequest { User = user }
        );
    }
    
    private async Task<ProcessingResult> ProcessStandardUser(UserData user)
    {
        // Simple processing for standard users
        return new ProcessingResult 
        { 
            UserId = user.Id,
            ProcessingType = "Standard",
            Features = StandardFeatures
        };
    }
}
```

### 4. Data Access Patterns

#### Repository Pattern Integration
```csharp
public class DataDrivenWorkflow : EffectWorkflow<DataProcessingRequest, DataProcessingResult>
{
    [Inject]
    public IDataRepository DataRepository { get; set; }
    
    [Inject]
    public IDataContextProviderFactory DataContextFactory { get; set; }
    
    protected override async Task<Either<Exception, DataProcessingResult>> RunInternal(DataProcessingRequest input)
    {
        // Use the repository for business logic
        var businessResult = await Activate(input)
            .Chain<ValidateDataStep>()
            .Chain<ProcessDataStep>()
            .Chain<TransformDataStep>()
            .Resolve();
            
        if (businessResult.IsLeft)
            return businessResult.Swap();
        
        // Use direct data context for complex queries or transactions
        using var dataContext = (IDataContext)DataContextFactory.Create();
        using var transaction = await dataContext.BeginTransaction();
        
        try
        {
            var processedData = businessResult.ValueUnsafe();
            
            // Complex database operations that need transactions
            await PerformComplexDatabaseOperations(dataContext, processedData);
            
            await dataContext.SaveChanges(CancellationToken.None);
            await dataContext.CommitTransaction();
            
            return new DataProcessingResult { Success = true, ProcessedCount = processedData.Count };
        }
        catch (Exception ex)
        {
            await dataContext.RollbackTransaction();
            return ex;
        }
    }
}
```

### 5. External Service Integration

#### HTTP API Calls in Steps
```csharp
public class ExternalApiStep : Step<ApiRequest, ApiResponse>
{
    [Inject]
    public HttpClient HttpClient { get; set; }
    
    [Inject]
    public ILogger<ExternalApiStep> Logger { get; set; }
    
    public override async Task<Either<Exception, ApiResponse>> Run(ApiRequest input)
    {
        try
        {
            using var response = await HttpClient.PostAsJsonAsync("/api/process", input);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Logger.LogWarning("External API call failed: {StatusCode} - {Error}", 
                    response.StatusCode, errorContent);
                return new ExternalServiceException($"API call failed: {response.StatusCode}");
            }
            
            var result = await response.Content.ReadFromJsonAsync<ApiResponse>();
            return result ?? throw new InvalidOperationException("Empty response from API");
        }
        catch (HttpRequestException ex)
        {
            Logger.LogError(ex, "Network error calling external API");
            return new ExternalServiceException("Network error", ex);
        }
        catch (TaskCanceledException ex)
        {
            Logger.LogError(ex, "Timeout calling external API");
            return new ExternalServiceException("Request timeout", ex);
        }
    }
}
```

#### Retry Pattern with External Services
```csharp
public class RetryableApiStep : Step<ApiRequest, ApiResponse>
{
    private const int MaxRetries = 3;
    private static readonly TimeSpan BaseDelay = TimeSpan.FromSeconds(1);
    
    [Inject]
    public HttpClient HttpClient { get; set; }
    
    public override async Task<Either<Exception, ApiResponse>> Run(ApiRequest input)
    {
        Exception lastException = null;
        
        for (int attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                var response = await CallExternalApi(input);
                return response; // Success!
            }
            catch (Exception ex) when (IsRetryableException(ex))
            {
                lastException = ex;
                
                if (attempt < MaxRetries)
                {
                    var delay = TimeSpan.FromMilliseconds(BaseDelay.TotalMilliseconds * Math.Pow(2, attempt - 1));
                    await Task.Delay(delay);
                }
            }
        }
        
        return lastException ?? new InvalidOperationException("Retry loop failed");
    }
    
    private static bool IsRetryableException(Exception ex)
        => ex is HttpRequestException or TaskCanceledException or SocketException;
}
```

## Environment-Specific Configurations

### Development Environment
```csharp
public static class DevelopmentConfiguration
{
    public static void ConfigureChainSharp(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddChainSharpEffects(options => 
            options
                .AddInMemoryEffect()                    // Fast, no setup required
                .AddJsonEffect()                        // Debug logging
                .AddEffectWorkflowBus(                  // Auto-discovery
                    typeof(Program).Assembly,
                    typeof(CreateUserWorkflow).Assembly
                )
        );
        
        // Development-specific services
        services.AddTransient<IEmailService, FakeEmailService>();
        services.AddTransient<IPaymentService, FakePaymentService>();
    }
}
```

### Testing Environment
```csharp
public static class TestConfiguration
{
    public static void ConfigureChainSharpForTests(this IServiceCollection services)
    {
        services.AddChainSharpEffects(options => 
            options
                .AddInMemoryEffect()                    // Isolated per test
                .SaveWorkflowParameters()               // Verify inputs/outputs
                .AddEffectWorkflowBus(typeof(TestWorkflow).Assembly)
        );
        
        // Test doubles
        services.AddSingleton<IEmailService, TestEmailService>();
        services.AddSingleton<IPaymentService, TestPaymentService>();
    }
}
```

### Production Environment
```csharp
public static class ProductionConfiguration
{
    public static void ConfigureChainSharp(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("PostgreSQL");
        
        services.AddChainSharpEffects(options => 
            options
                .AddPostgresEffect(connectionString)   // Persistent storage
                .SaveWorkflowParameters()               // Audit trail
                .AddEffectWorkflowBus(                  // Controlled discovery
                    typeof(OrderWorkflow).Assembly,
                    typeof(UserWorkflow).Assembly,
                    typeof(PaymentWorkflow).Assembly
                )
        );
        
        // Production services
        services.AddHttpClient<IPaymentService, StripePaymentService>();
        services.AddScoped<IEmailService, SendGridEmailService>();
    }
}
```

## Testing Patterns

### Unit Testing Workflows
```csharp
[Fact]
public async Task CreateUserWorkflow_ValidInput_ReturnsUser()
{
    // Arrange
    var services = new ServiceCollection()
        .AddChainSharpEffects(options => options.AddInMemoryEffect())
        .AddTransientChainSharpWorkflow<ICreateUserWorkflow, CreateUserWorkflow>()
        .AddSingleton<IUserRepository, FakeUserRepository>()
        .AddSingleton<IEmailService, FakeEmailService>()
        .BuildServiceProvider();
    
    var workflow = services.GetRequiredService<ICreateUserWorkflow>();
    var request = new CreateUserRequest
    {
        Email = "test@example.com",
        FirstName = "John",
        LastName = "Doe"
    };
    
    // Act
    var result = await workflow.Run(request);
    
    // Assert
    result.IsRight.Should().BeTrue();
    var user = result.ValueUnsafe();
    user.Email.Should().Be("test@example.com");
    user.FullName.Should().Be("John Doe");
}
```

### Integration Testing with WorkflowBus
```csharp
[Fact]
public async Task WorkflowBus_CreateUser_PersistsMetadata()
{
    // Arrange
    var services = new ServiceCollection()
        .AddChainSharpEffects(options => 
            options
                .AddInMemoryEffect()
                .SaveWorkflowParameters()
                .AddEffectWorkflowBus(typeof(CreateUserWorkflow).Assembly)
        )
        .AddSingleton<IUserRepository, FakeUserRepository>()
        .AddSingleton<IEmailService, FakeEmailService>()
        .BuildServiceProvider();
    
    var workflowBus = services.GetRequiredService<IWorkflowBus>();
    var dataContextFactory = services.GetRequiredService<IDataContextProviderFactory>();
    
    var request = new CreateUserRequest
    {
        Email = "test@example.com",
        FirstName = "John",
        LastName = "Doe"
    };
    
    // Act
    var user = await workflowBus.RunAsync<User>(request);
    
    // Assert
    using var dataContext = (IDataContext)dataContextFactory.Create();
    var metadata = await dataContext.Metadatas
        .FirstOrDefaultAsync(m => m.Name == nameof(CreateUserWorkflow));
    
    metadata.Should().NotBeNull();
    metadata.WorkflowState.Should().Be(WorkflowState.Completed);
    metadata.Input.Should().NotBeNull();
    metadata.Output.Should().NotBeNull();
}
```

## Performance Optimization

### Minimizing Effect Overhead
```csharp
// High-performance configuration
services.AddChainSharpEffects(options => 
    options
        .AddPostgresEffect(connectionString)
        // Skip JsonEffect and ParameterEffect in production
        .AddEffectWorkflowBus(assemblies)
);
```

### Optimizing Database Effects
```csharp
public class OptimizedWorkflow : EffectWorkflow<BatchRequest, BatchResult>
{
    protected override async Task<Either<Exception, BatchResult>> RunInternal(BatchRequest input)
    {
        // Process in batches to reduce database round trips
        var batches = input.Items.Chunk(100);
        var results = new List<ProcessedItem>();
        
        foreach (var batch in batches)
        {
            var batchResult = await Activate(batch)
                .Chain<ProcessBatchStep>()
                .Resolve();
                
            if (batchResult.IsLeft)
                return batchResult.Swap();
                
            results.AddRange(batchResult.ValueUnsafe());
        }
        
        return new BatchResult { ProcessedItems = results };
    }
}
```

### Memory Management
```csharp
public class MemoryEfficientStep : Step<LargeDataSet, ProcessedDataSet>, IDisposable
{
    private readonly SemaphoreSlim _semaphore = new(Environment.ProcessorCount);
    
    public override async Task<Either<Exception, ProcessedDataSet>> Run(LargeDataSet input)
    {
        await _semaphore.WaitAsync();
        try
        {
            // Process data in chunks to manage memory
            return await ProcessInChunks(input);
        }
        finally
        {
            _semaphore.Release();
        }
    }
    
    public void Dispose()
    {
        _semaphore?.Dispose();
    }
}
```

This guide provides practical patterns for implementing robust, maintainable workflows using ChainSharp. Each pattern can be adapted to your specific business requirements while maintaining the benefits of the Railway Oriented Programming and Effect patterns.
