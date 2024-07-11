using ChainSharp.Exceptions;

namespace ChainSharp.Utils;

internal static class TypeHelpers
{
    internal static List<(Type, dynamic)> ExtractTypeTuples(
        Dictionary<Type, object> memory,
        Type inputType
    )
    {
        var dynamicList = new List<(Type, dynamic)>();
        foreach (var type in inputType.GenericTypeArguments)
        {
            var value = memory.GetValueOrDefault(type);

            if (value is null)
                throw new WorkflowException(
                    $"Could not extract type ({type}) from Tuple. Value not in Memory."
                );

            dynamicList.Add((type, value));
        }

        return dynamicList;
    }

    internal static dynamic ConvertTwoTuple(List<(Type, dynamic)> dynamicList)
    {
        ArgumentNullException.ThrowIfNull(dynamicList);

        if (dynamicList.Count < 2)
            throw new ArgumentException(
                "List must have at least two elements.",
                nameof(dynamicList)
            );

        var (type1, value1) = dynamicList[0];
        var (type2, value2) = dynamicList[1];

        var tupleType = typeof(ValueTuple<,>).MakeGenericType(type1, type2);
        return Activator.CreateInstance(tupleType, value1, value2);
    }

    internal static dynamic ConvertThreeTuple(List<(Type, dynamic)> dynamicList)
    {
        ArgumentNullException.ThrowIfNull(dynamicList);

        if (dynamicList.Count < 3)
            throw new ArgumentException(
                "List must have at least three elements.",
                nameof(dynamicList)
            );

        var (type1, value1) = dynamicList[0];
        var (type2, value2) = dynamicList[1];
        var (type3, value3) = dynamicList[2];

        var tupleType = typeof(ValueTuple<,,>).MakeGenericType(type1, type2, type3);
        return Activator.CreateInstance(tupleType, value1, value2, value3);
    }

    internal static dynamic ConvertFourTuple(List<(Type, dynamic)> dynamicList)
    {
        ArgumentNullException.ThrowIfNull(dynamicList);

        if (dynamicList.Count < 4)
            throw new ArgumentException(
                "List must have at least four elements.",
                nameof(dynamicList)
            );

        var (type1, value1) = dynamicList[0];
        var (type2, value2) = dynamicList[1];
        var (type3, value3) = dynamicList[2];
        var (type4, value4) = dynamicList[3];

        var tupleType = typeof(ValueTuple<,,,>).MakeGenericType(type1, type2, type3, type4);
        return Activator.CreateInstance(tupleType, value1, value2, value3, value4);
    }

    internal static dynamic ConvertFiveTuple(List<(Type, dynamic)> dynamicList)
    {
        ArgumentNullException.ThrowIfNull(dynamicList);

        if (dynamicList.Count < 5)
            throw new ArgumentException(
                "List must have at least five elements.",
                nameof(dynamicList)
            );

        var (type1, value1) = dynamicList[0];
        var (type2, value2) = dynamicList[1];
        var (type3, value3) = dynamicList[2];
        var (type4, value4) = dynamicList[3];
        var (type5, value5) = dynamicList[4];

        var tupleType = typeof(ValueTuple<,,,,>).MakeGenericType(type1, type2, type3, type4, type5);
        return Activator.CreateInstance(tupleType, value1, value2, value3, value4, value5);
    }

    internal static dynamic ConvertSixTuple(List<(Type, dynamic)> dynamicList)
    {
        ArgumentNullException.ThrowIfNull(dynamicList);

        if (dynamicList.Count < 6)
            throw new ArgumentException(
                "List must have at least six elements.",
                nameof(dynamicList)
            );

        var (type1, value1) = dynamicList[0];
        var (type2, value2) = dynamicList[1];
        var (type3, value3) = dynamicList[2];
        var (type4, value4) = dynamicList[3];
        var (type5, value5) = dynamicList[4];
        var (type6, value6) = dynamicList[5];

        var tupleType = typeof(ValueTuple<,,,,,>).MakeGenericType(
            type1,
            type2,
            type3,
            type4,
            type5,
            type6
        );
        return Activator.CreateInstance(tupleType, value1, value2, value3, value4, value5, value6);
    }

    internal static dynamic ConvertSevenTuple(List<(Type, dynamic)> dynamicList)
    {
        ArgumentNullException.ThrowIfNull(dynamicList);

        if (dynamicList.Count < 7)
            throw new ArgumentException(
                "List must have at least six elements.",
                nameof(dynamicList)
            );

        var (type1, value1) = dynamicList[0];
        var (type2, value2) = dynamicList[1];
        var (type3, value3) = dynamicList[2];
        var (type4, value4) = dynamicList[3];
        var (type5, value5) = dynamicList[4];
        var (type6, value6) = dynamicList[5];
        var (type7, value7) = dynamicList[6];

        var tupleType = typeof(ValueTuple<,,,,,,>).MakeGenericType(
            type1,
            type2,
            type3,
            type4,
            type5,
            type6,
            type7
        );
        return Activator.CreateInstance(
            tupleType,
            value1,
            value2,
            value3,
            value4,
            value5,
            value6,
            value7
        );
    }
}
