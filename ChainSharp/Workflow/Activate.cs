using ChainSharp.Exceptions;
using ChainSharp.Extensions;
using LanguageExt;

namespace ChainSharp.Workflow;

public partial class Workflow<TInput, TReturn>
{
    public Workflow<TInput, TReturn> Activate(TInput input, params object[] otherTypes)
    {
        // Always allow input type of Unit for parameterless invocation
        Memory ??= new Dictionary<Type, object>() {{ typeof(Unit), Unit.Default }};

        var inputType = typeof(TInput);

        if (input is null)
        {
            Exception ??= new WorkflowException($"Input ({inputType}) is null.");
            return this;
        }

        if (inputType.IsTuple())
            this.AddTupleToMemory(input);
        else
            Memory[inputType] = input;

        foreach (var otherType in otherTypes)
            Memory[otherType.GetType()] = otherType;

        return this;
    } 
}