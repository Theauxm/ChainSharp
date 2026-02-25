using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using ChainSharp.Exceptions;
using ChainSharp.Extensions;
using LanguageExt;

namespace ChainSharp.Workflow;

public partial class Workflow<TInput, TReturn>
{
    private static readonly ConcurrentDictionary<Type, Type[]> InterfaceCache = new();

    /// <summary>
    /// Activates the workflow by storing the input and additional objects in the Memory dictionary.
    /// This is typically the first method called in a workflow's RunInternal implementation.
    /// </summary>
    /// <param name="input">The primary input for the workflow</param>
    /// <param name="otherInputs">Additional objects to store in Memory</param>
    /// <returns>The workflow instance for method chaining</returns>
    /// <remarks>
    /// This method handles both simple types and tuples:
    /// - For simple types, the object is stored by its concrete type and all its interfaces
    /// - For tuples, each element is extracted and stored individually
    /// </remarks>
    public Workflow<TInput, TReturn> Activate(TInput input, params object[] otherInputs)
    {
        // Always allow input type of Unit for parameterless invocation
        Memory ??= new Dictionary<Type, object>() { { typeof(Unit), Unit.Default } };

        // Validate input is not null
        if (input is null)
        {
            Exception ??= new WorkflowException($"Input ({typeof(TInput)}) is null.");
            return this;
        }

        var inputType = input.GetType();

        // Handle tuple inputs differently by extracting each element
        if (inputType.IsTuple())
            this.AddTupleToMemory(input);
        else
        {
            // Store by concrete type
            Memory[inputType] = input;

            // Also store by all interfaces for interface-based retrieval
            var interfaces = InterfaceCache.GetOrAdd(inputType, t => t.GetInterfaces());
            foreach (var foundInterface in interfaces)
                Memory[foundInterface] = input;
        }

        // Process additional inputs
        foreach (var otherInput in otherInputs)
        {
            var otherType = otherInput.GetType();

            if (otherType.IsTuple())
                this.AddTupleToMemory((ITuple)otherInput);
            else
            {
                Memory[otherType] = otherInput;

                var interfaces = InterfaceCache.GetOrAdd(otherType, t => t.GetInterfaces());
                foreach (var foundInterface in interfaces)
                    Memory[foundInterface] = otherInput;
            }
        }

        return this;
    }
}
