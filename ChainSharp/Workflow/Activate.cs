using System.Runtime.CompilerServices;
using ChainSharp.Exceptions;
using ChainSharp.Extensions;
using LanguageExt;

namespace ChainSharp.Workflow;

public partial class Workflow<TInput, TReturn>
{
    public Workflow<TInput, TReturn> Activate(TInput input, params object[] otherInputs)
    {
        // Always allow input type of Unit for parameterless invocation
        Memory ??= new Dictionary<Type, object>() { { typeof(Unit), Unit.Default } };

        if (input is null)
        {
            Exception ??= new WorkflowException($"Input ({typeof(TInput)}) is null.");
            return this;
        }

        var inputType = input.GetType();

        if (inputType.IsTuple())
            this.AddTupleToMemory(input);
        else
        {
            Memory[inputType] = input;

            var interfaces = inputType.GetInterfaces();
            foreach (var foundInterface in interfaces)
                Memory[foundInterface] = input;
        }

        foreach (var otherInput in otherInputs)
        {
            var otherType = otherInput.GetType();

            if (otherType.IsTuple())
                this.AddTupleToMemory((ITuple)otherInput);
            else
            {
                Memory[otherType] = otherInput;

                var interfaces = inputType.GetInterfaces();
                foreach (var foundInterface in interfaces)
                    Memory[foundInterface] = input;
            }
        }

        return this;
    }
}
