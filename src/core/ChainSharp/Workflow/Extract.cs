using ChainSharp.Exceptions;

namespace ChainSharp.Workflow;

public partial class Workflow<TInput, TReturn>
{
    /// <summary>
    /// Extracts a value of type TOut from an object of type TIn in Memory.
    /// This method first retrieves the TIn object from Memory, then extracts a TOut value from it.
    /// </summary>
    /// <typeparam name="TIn">The type of the source object</typeparam>
    /// <typeparam name="TOut">The type of the value to extract</typeparam>
    /// <returns>The workflow instance for method chaining</returns>
    /// <remarks>
    /// This is useful for extracting nested values from complex objects in the workflow.
    /// For example, if you have a User object in Memory and need to extract the Email property,
    /// you can use Extract&lt;User, string&gt;() to get the Email and store it in Memory.
    /// </remarks>
    public Workflow<TInput, TReturn> Extract<TIn, TOut>()
    {
        // Try to get the source object from Memory
        var typeFromMemory = (TIn?)Memory.GetValueOrDefault(typeof(TIn));

        if (typeFromMemory is null)
        {
            Exception ??= new WorkflowException($"Could not find type: ({typeof(TIn)}).");

            return this;
        }

        return Extract<TIn, TOut>(typeFromMemory);
    }

    /// <summary>
    /// Extracts a value of type TOut from the provided TIn object.
    /// This method looks for a property or field of type TOut in the TIn object.
    /// </summary>
    /// <typeparam name="TIn">The type of the source object</typeparam>
    /// <typeparam name="TOut">The type of the value to extract</typeparam>
    /// <param name="input">The source object to extract from</param>
    /// <returns>The workflow instance for method chaining</returns>
    /// <remarks>
    /// The extraction process:
    /// 1. Looks for a property of type TOut
    /// 2. If not found, looks for a field of type TOut
    /// 3. If found, stores the value in Memory with the TOut type as the key
    /// </remarks>
    public Workflow<TInput, TReturn> Extract<TIn, TOut>(TIn input)
    {
        if (input is null)
        {
            Exception ??= new WorkflowException(
                $"Null value for type: ({typeof(TIn)}) passed to Extract function."
            );
            return this;
        }

        // Try to get a property or field of type TOut
        var value = GetPropertyValue<TIn, TOut>(input) ?? GetFieldValue<TIn, TOut>(input);

        if (value is null)
        {
            Exception ??= new WorkflowException(
                $"Could not find non-null value of type: ({typeof(TOut)}) in properties or fields for ({typeof(TIn)}). Is it public?"
            );
            return this;
        }

        // Store the extracted value in Memory
        Memory[typeof(TOut)] = value;
        return this;
    }

    /// <summary>
    /// Gets a property value of type TOut from a TIn object.
    /// </summary>
    /// <typeparam name="TIn">The type of the source object</typeparam>
    /// <typeparam name="TOut">The type of the property to get</typeparam>
    /// <param name="input">The source object</param>
    /// <returns>The property value, or null if not found</returns>
    private object? GetPropertyValue<TIn, TOut>(TIn input)
    {
        var propertyInfo = input!
            .GetType()
            .GetProperties()
            .FirstOrDefault(x => x.PropertyType == typeof(TOut));

        return propertyInfo?.GetValue(input);
    }

    /// <summary>
    /// Gets a field value of type TOut from a TIn object.
    /// </summary>
    /// <typeparam name="TIn">The type of the source object</typeparam>
    /// <typeparam name="TOut">The type of the field to get</typeparam>
    /// <param name="input">The source object</param>
    /// <returns>The field value, or null if not found</returns>
    private object? GetFieldValue<TIn, TOut>(TIn input)
    {
        var fieldInfo = input!
            .GetType()
            .GetFields()
            .FirstOrDefault(x => x.FieldType == typeof(TOut));

        return fieldInfo?.GetValue(input);
    }
}
