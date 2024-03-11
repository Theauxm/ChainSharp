using ChainSharp.Exceptions;

namespace ChainSharp.Utils;

internal static class TypeHelpers
{
    internal static List<dynamic> ExtractTypes(Dictionary<Type, object> memory, Type inputType)
    {
        var dynamicList = new List<dynamic>();
        foreach (var type in inputType.GenericTypeArguments)
        {
            var value = memory.GetValueOrDefault(type);

            if (value is null)
                throw new WorkflowException($"Could not extract type ({type}) from Tuple. Value not in Memory.");
            
            dynamicList.Add(value);
        }

        return dynamicList;
    }

    internal static dynamic ConvertTwoTuple(List<dynamic> dynamicList)
    {
        ArgumentNullException.ThrowIfNull(dynamicList);

        if (dynamicList.Count < 2)
            throw new ArgumentException("List must have at least two elements.", nameof(dynamicList));

        return ValueTuple.Create(
            dynamicList[0], 
            dynamicList[1]);
    }
    
    internal static dynamic ConvertThreeTuple(List<dynamic> dynamicList)
    {
        ArgumentNullException.ThrowIfNull(dynamicList);

        if (dynamicList.Count < 3)
            throw new ArgumentException("List must have at least three elements.", nameof(dynamicList));

        return ValueTuple.Create(
            dynamicList[0], 
            dynamicList[1], 
            dynamicList[2]);
    }
    
    internal static dynamic ConvertFourTuple(List<dynamic> dynamicList)
    {
        ArgumentNullException.ThrowIfNull(dynamicList);

        if (dynamicList.Count < 4)
            throw new ArgumentException("List must have at least four elements.", nameof(dynamicList));

        return ValueTuple.Create(
            dynamicList[0], 
            dynamicList[1], 
            dynamicList[2], 
            dynamicList[3]);
    }
    
    internal static dynamic ConvertFiveTuple(List<dynamic> dynamicList)
    {
        ArgumentNullException.ThrowIfNull(dynamicList);

        if (dynamicList.Count < 5)
            throw new ArgumentException("List must have at least five elements.", nameof(dynamicList));

        return ValueTuple.Create(
            dynamicList[0], 
            dynamicList[1], 
            dynamicList[2], 
            dynamicList[3], 
            dynamicList[4]);
    }
    
    internal static dynamic ConvertSixTuple(List<dynamic> dynamicList)
    {
        ArgumentNullException.ThrowIfNull(dynamicList);

        if (dynamicList.Count < 6)
            throw new ArgumentException("List must have at least six elements.", nameof(dynamicList));

        return ValueTuple.Create(
            dynamicList[0], 
            dynamicList[1], 
            dynamicList[2], 
            dynamicList[3], 
            dynamicList[4], 
            dynamicList[5]);
    }
    
    internal static dynamic ConvertSevenTuple(List<dynamic> dynamicList)
    {
        ArgumentNullException.ThrowIfNull(dynamicList);

        if (dynamicList.Count < 6)
            throw new ArgumentException("List must have at least six elements.", nameof(dynamicList));

        return ValueTuple.Create(
            dynamicList[0], 
            dynamicList[1], 
            dynamicList[2], 
            dynamicList[3], 
            dynamicList[4], 
            dynamicList[5],
            dynamicList[6]);
    }
} 
