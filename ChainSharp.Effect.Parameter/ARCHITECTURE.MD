# ChainSharp.Effect.Parameter Architecture

ChainSharp.Effect.Parameter provides an extension to ChainSharp.Effect.Data that serializes workflow inputs and outputs to JSON for storage in the database. This document explains the architecture, key components, and usage patterns of this effect.

## Introduction

The Parameter effect is designed to enhance the persistence capabilities of ChainSharp.Effect.Data by:

1. Serializing workflow input objects to JSON
2. Serializing workflow output objects to JSON
3. Storing these serialized representations in the Metadata table
4. Enabling querying and analysis of workflow parameters

Unlike the core data effects that only persist the Metadata model itself, the Parameter effect ensures that the actual content of inputs and outputs is preserved. This provides valuable context for workflow execution history, debugging, and analytics.

However, it's important to note that this effect can potentially cause performance issues due to the serialization overhead and increased storage requirements, especially for large or complex objects.

## Core Components

### ParameterEffect

The `ParameterEffect` class implements the `IEffectProvider` interface and handles the serialization of workflow parameters:

```csharp
public class ParameterEffect(JsonSerializerOptions options) : IEffectProvider
{
    private readonly HashSet<Metadata> _trackedMetadatas = [];

    public void Dispose() { }

    public async Task SaveChanges(CancellationToken cancellationToken)
    {
        foreach (var metadata in _trackedMetadatas)
        {
            SerializeParameters(metadata);
        }
    }

    public async Task Track(IModel model)
    {
        if (model is Metadata metadata)
        {
            _trackedMetadatas.Add(metadata);
            SerializeParameters(metadata);
        }
    }

    private void SerializeParameters(Metadata metadata)
    {
        if (metadata.InputObject is not null)
        {
            var serializedInput = JsonSerializer.Serialize(metadata.InputObject, options);
            metadata.Input = JsonDocument.Parse(serializedInput);
        }

        if (metadata.OutputObject is not null)
        {
            var serializedOutput = JsonSerializer.Serialize(metadata.OutputObject, options);
            metadata.Output = JsonDocument.Parse(serializedOutput);
        }
    }
}
```

Key features:
- Tracks Metadata models in a HashSet
- Serializes InputObject and OutputObject properties to JSON
- Uses the provided JsonSerializerOptions for customization
- Stores the serialized JSON in the Input and Output properties

### ParameterEffectProviderFactory

The `ParameterEffectProviderFactory` creates instances of the `ParameterEffect`:

```csharp
public class ParameterEffectProviderFactory(IChainSharpEffectConfiguration configuration)
    : IParameterEffectProviderFactory
{
    public List<ParameterEffect> Providers { get; } = [];

    public IEffectProvider Create()
    {
        var parameterEffect = new ParameterEffect(
            configuration.WorkflowParameterJsonSerializerOptions
        );

        Providers.Add(parameterEffect);

        return parameterEffect;
    }
}
```

This factory:
- Uses the JSON serialization options from the ChainSharpEffectConfiguration
- Creates ParameterEffect instances
- Keeps track of created providers in a list

### ServiceExtensions

The `ServiceExtensions` class provides the `SaveWorkflowParameters` extension method for registering the Parameter effect:

```csharp
public static class ServiceExtensions
{
    public static ChainSharpEffectConfigurationBuilder SaveWorkflowParameters(
        this ChainSharpEffectConfigurationBuilder builder,
        JsonSerializerOptions? jsonSerializerOptions = null
    )
    {
        jsonSerializerOptions ??= ChainSharpJsonSerializationOptions.Default;

        builder.WorkflowParameterJsonSerializerOptions = jsonSerializerOptions;

        return builder.AddEffect<IEffectProviderFactory, ParameterEffectProviderFactory>();
    }
}
```

This method:
- Accepts optional JSON serialization options
- Falls back to default options if none are provided
- Sets the options in the configuration
- Registers the ParameterEffectProviderFactory with the effect system

## How It Works

### Parameter Serialization Process

The parameter serialization process happens in two key moments:

1. **When a Metadata model is tracked**:
   - The EffectRunner calls Track on all effect providers
   - The ParameterEffect checks if the model is a Metadata
   - If it is, it adds it to the tracked metadatas collection
   - It immediately serializes any existing InputObject or OutputObject

2. **When SaveChanges is called**:
   - The EffectRunner calls SaveChanges on all effect providers
   - The ParameterEffect iterates through all tracked metadatas
   - It serializes their InputObject and OutputObject properties

The serialization itself is handled by the `SerializeParameters` method:

```csharp
private void SerializeParameters(Metadata metadata)
{
    if (metadata.InputObject is not null)
    {
        var serializedInput = JsonSerializer.Serialize(metadata.InputObject, options);
        metadata.Input = JsonDocument.Parse(serializedInput);
    }

    if (metadata.OutputObject is not null)
    {
        var serializedOutput = JsonSerializer.Serialize(metadata.OutputObject, options);
        metadata.Output = JsonDocument.Parse(serializedOutput);
    }
}
```

This method:
- Checks if InputObject or OutputObject are not null
- Serializes them to JSON strings using the configured options
- Parses the JSON strings into JsonDocument objects
- Assigns these to the Input and Output properties of the Metadata

### Metadata Integration

The Parameter effect integrates with the Metadata model through its dynamic properties:

```csharp
public class Metadata : IMetadata
{
    // Database columns
    [Column("input")]
    public JsonDocument? Input { get; set; }

    [Column("output")]
    public JsonDocument? Output { get; set; }

    // Non-persisted properties
    [JsonIgnore]
    public dynamic? InputObject { get; set; }

    [JsonIgnore]
    public dynamic? OutputObject { get; set; }
    
    // ...
}
```

The Metadata model has:
- `Input` and `Output` properties of type JsonDocument that are mapped to database columns
- `InputObject` and `OutputObject` properties of type dynamic that hold the original objects
- JsonIgnore attributes on the object properties to prevent serialization loops

When a workflow is executed:
1. The workflow creates a Metadata object with InputObject set to the input
2. The ParameterEffect serializes this to the Input property
3. When the workflow completes, it sets the OutputObject property
4. The ParameterEffect serializes this to the Output property
5. The DataContext persists the Metadata with the serialized Input and Output

### JSON Document Storage

The serialized parameters are stored as JsonDocument objects, which are:
- Immutable representations of JSON data
- Memory-efficient for large JSON documents
- Directly mappable to database JSON columns
- Queryable using JSON path expressions

This approach allows for:
- Efficient storage of complex object graphs
- Type-agnostic representation of diverse input and output types
- Compatibility with database JSON capabilities
- Preservation of the original structure without loss of information

## Integration with ChainSharp.Effect.Data

The Parameter effect is designed to work alongside ChainSharp.Effect.Data:

1. **Complementary Functionality**:
   - ChainSharp.Effect.Data handles the persistence of Metadata models
   - ChainSharp.Effect.Parameter handles the serialization of their parameters

2. **Database Schema Integration**:
   - The Metadata table includes `input` and `output` columns of JSON type
   - These columns store the serialized parameters
   - No additional tables or schema changes are required

3. **Persistence Flow**:
   - The ParameterEffect serializes InputObject to Input and OutputObject to Output
   - The DataContext persists the entire Metadata model, including Input and Output
   - The serialized JSON is stored in the database

4. **Query Capabilities**:
   - PostgreSQL and other databases support JSON querying
   - This enables filtering and analysis based on parameter values
   - For example: `context.Metadatas.Where(m => EF.Functions.JsonContains(m.Input, @"{""userId"": 123}"))`

## Service Registration

### Basic Registration

To use the Parameter effect, register it with the ChainSharp.Effect system:

```csharp
// In Program.cs or Startup.cs
services.AddChainSharpEffects(options => 
    options
        .AddPostgresEffect(connectionString)
        .SaveWorkflowParameters()
);
```

This will:
1. Register the PostgreSQL effect for database persistence
2. Register the Parameter effect for parameter serialization
3. Use the default JSON serialization options

### Custom JSON Options

You can customize the JSON serialization options:

```csharp
// In Program.cs or Startup.cs
services.AddChainSharpEffects(options => 
    options
        .AddPostgresEffect(connectionString)
        .SaveWorkflowParameters(new JsonSerializerOptions
        {
            WriteIndented = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            ReferenceHandler = ReferenceHandler.Preserve
        })
);
```

Common customizations include:
- `WriteIndented`: Controls whether JSON is formatted with indentation (impacts storage size)
- `PropertyNamingPolicy`: Controls how property names are formatted in JSON
- `DefaultIgnoreCondition`: Controls when properties are excluded from serialization
- `ReferenceHandler`: Controls how object references are handled (important for circular references)

### Order of Registration

The Parameter effect should be registered after the data effect:

```csharp
// Correct order
options
    .AddPostgresEffect(connectionString)
    .SaveWorkflowParameters();

// Incorrect order
options
    .SaveWorkflowParameters()
    .AddPostgresEffect(connectionString);
```

This ensures that:
1. The data effect is registered first
2. The Parameter effect can rely on its presence
3. The serialized parameters will be persisted correctly

## Performance Considerations

### Serialization Overhead

The Parameter effect adds serialization overhead to workflow execution:

1. **CPU Usage**:
   - JSON serialization requires CPU time
   - Complex objects with many properties take longer to serialize
   - Circular references require special handling

2. **Memory Usage**:
   - Serialization creates temporary strings and objects
   - JsonDocument parsing allocates memory
   - Large objects can cause significant memory pressure

3. **Execution Time**:
   - The serialization happens synchronously during Track and SaveChanges
   - This adds latency to workflow execution
   - The impact is proportional to the complexity of the objects

### Storage Requirements

The Parameter effect increases database storage requirements:

1. **Column Size**:
   - JSON representations can be verbose
   - Each property name is repeated in every record
   - Nested objects and arrays add structural overhead

2. **Database Growth**:
   - High-volume workflows can generate large amounts of data
   - Historical data accumulates over time
   - Consider database maintenance and archiving strategies

3. **Index Considerations**:
   - JSON columns may require specialized indexes
   - Full-text indexing of JSON can be resource-intensive
   - Query performance may degrade with large JSON documents

### When to Use

The Parameter effect is most appropriate for:

1. **Debugging and Troubleshooting**:
   - When you need to inspect workflow inputs and outputs
   - When diagnosing issues with specific workflow executions
   - During development and testing phases

2. **Audit and Compliance**:
   - When you need to maintain a record of all workflow parameters
   - When regulatory requirements mandate data preservation
   - When you need to reconstruct the exact state of past executions

3. **Analytics and Reporting**:
   - When you need to analyze patterns in workflow parameters
   - When generating reports based on workflow data
   - When building dashboards or monitoring systems

It may be less suitable for:

1. **High-Performance Systems**:
   - When minimizing latency is critical
   - When processing thousands of workflows per second
   - When resources are constrained

2. **Large Object Processing**:
   - When workflows process very large objects (megabytes+)
   - When inputs or outputs contain binary data
   - When objects have complex circular references

3. **Production-Only Concerns**:
   - When the data is only needed in development
   - When storage costs are a significant concern
   - When the parameters contain sensitive information

## Best Practices

### JSON Serialization Options

Configure JSON serialization options for optimal performance and storage:

1. **Minimize Output Size**:
   ```csharp
   new JsonSerializerOptions
   {
       WriteIndented = false,
       DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
       PropertyNamingPolicy = JsonNamingPolicy.CamelCase
   }
   ```

2. **Handle Circular References**:
   ```csharp
   new JsonSerializerOptions
   {
       ReferenceHandler = ReferenceHandler.Preserve
   }
   ```

3. **Custom Converters for Problematic Types**:
   ```csharp
   new JsonSerializerOptions
   {
       Converters = 
       {
           new JsonStringEnumConverter(),
           new DateTimeConverter(),
           new ObjectIdConverter()
       }
   }
   ```

### Input/Output Object Design

Design workflow inputs and outputs with serialization in mind:

1. **Keep Objects Simple**:
   - Use plain data objects (POCOs/DTOs)
   - Avoid complex object graphs
   - Minimize nesting depth

2. **Avoid Problematic Types**:
   - Delegates and functions can't be serialized
   - Some collection types may cause issues
   - Resources like file handles or network connections

3. **Consider Size Limits**:
   - Break large datasets into smaller chunks
   - Use pagination for large collections
   - Reference external resources instead of embedding them

4. **Handle Sensitive Data**:
   - Use [JsonIgnore] for sensitive properties
   - Consider encryption for sensitive values
   - Implement custom converters for masking/redacting

### Error Handling

Prepare for serialization failures:

1. **Validate Objects Before Serialization**:
   - Ensure objects are serializable
   - Check for problematic properties
   - Consider pre-serialization tests

2. **Implement Fallbacks**:
   - Catch serialization exceptions
   - Provide simplified representations
   - Log serialization failures

3. **Monitor Performance**:
   - Watch for serialization bottlenecks
   - Track database size growth
   - Set up alerts for abnormal patterns

### Environment-Specific Configuration

Consider different configurations for different environments:

1. **Development**:
   ```csharp
   // Full parameter serialization for debugging
   options.SaveWorkflowParameters(new JsonSerializerOptions
   {
       WriteIndented = true,
       DefaultIgnoreCondition = JsonIgnoreCondition.Never
   });
   ```

2. **Testing**:
   ```csharp
   // Balanced approach for integration tests
   options.SaveWorkflowParameters(new JsonSerializerOptions
   {
       WriteIndented = false,
       DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
   });
   ```

3. **Production**:
   ```csharp
   // Minimal serialization for performance
   options.SaveWorkflowParameters(new JsonSerializerOptions
   {
       WriteIndented = false,
       DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
       MaxDepth = 10 // Limit nesting depth
   });
   ```

Or consider disabling it entirely in performance-critical environments:

```csharp
if (env.IsDevelopment() || env.IsStaging())
{
    options.SaveWorkflowParameters();
}
```

## Conclusion

ChainSharp.Effect.Parameter provides a powerful extension to ChainSharp.Effect.Data that enables the serialization and persistence of workflow parameters. While it adds valuable context and debugging capabilities, it comes with performance and storage considerations that should be carefully evaluated.

By following the best practices outlined in this document, you can effectively use the Parameter effect to enhance your workflow system while minimizing its impact on performance and resources.
