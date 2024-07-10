using ChainSharp.Exceptions;

namespace ChainSharp.Workflow;

public partial class Workflow<TInput, TReturn>
{
    public Workflow<TInput, TReturn> Extract<TIn, TOut>()
    {
        var typeFromMemory = (TIn?)Memory.GetValueOrDefault(typeof(TIn));

        if (typeFromMemory is null)
        {
            Exception ??= new WorkflowException($"Could not find type: ({typeof(TIn)}).");

            return this;
        }
        
        return Extract<TIn, TOut>(typeFromMemory);
    }

    public Workflow<TInput, TReturn> Extract<TIn, TOut>(TIn input)
    {
        if (input is null)
        {
            Exception ??= new WorkflowException($"Null value for type: ({typeof(TIn)}) passed to Extract function.");
            return this;
        }

        var value = GetPropertyValue<TIn, TOut>(input) ?? GetFieldValue<TIn, TOut>(input);

        if (value is null)
        {
            Exception ??= new WorkflowException(
                $"Could not find non-null value of type: ({typeof(TOut)}) in properties or fields for ({typeof(TIn)}). Is it public?");
            return this;
        }

        Memory[typeof(TOut)] = value;
        return this;
    }

    private object? GetPropertyValue<TIn, TOut>(TIn input)
    {
        var propertyInfo = input!
            .GetType()
            .GetProperties()
            .FirstOrDefault(x => x.PropertyType == typeof(TOut));

        return propertyInfo?.GetValue(input);
    }

    private object? GetFieldValue<TIn, TOut>(TIn input)
    {
        var fieldInfo = input!
            .GetType()
            .GetFields()
            .FirstOrDefault(x => x.FieldType == typeof(TOut));

        return fieldInfo?.GetValue(input);
    } 
}