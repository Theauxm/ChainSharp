namespace ChainSharp.Effect.Attributes;

/// <summary>
/// Marks a property for dependency injection in ChainSharp.Effect workflows.
/// </summary>
/// <remarks>
/// The InjectAttribute enables property-based dependency injection in the ChainSharp.Effect system.
/// When applied to a property, it indicates that the property should be automatically populated
/// with a service from the dependency injection container.
///
/// This attribute is used in conjunction with the InjectProperties extension method
/// in the ServiceExtensions class, which scans objects for properties marked with
/// this attribute and injects the appropriate services.
///
/// Property injection is particularly useful in the ChainSharp.Effect system because:
/// 1. It allows workflows to declare their dependencies without requiring constructor injection
/// 2. It supports optional dependencies that may not be available in all environments
/// 3. It enables a more flexible composition model for workflows
///
/// Example usage:
/// ```csharp
/// public class MyWorkflow : EffectWorkflow<MyInput, MyOutput>
/// {
///     [Inject]
///     public IMyService? MyService { get; set; }
///
///     // The MyService property will be automatically populated
///     // when the workflow is created through the dependency injection system
/// }
/// ```
/// </remarks>
[AttributeUsage(AttributeTargets.Property)]
public class InjectAttribute : Attribute { }
