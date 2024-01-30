using ChainSharp.Exceptions;

namespace ChainSharp.Utils;

public static class TypeHelpers
{
    public static dynamic ExtractTuple(Dictionary<Type, object> memory, Type inputType)
    {
        var dynamicList = ExtractTypes(memory, inputType);

        return dynamicList.Count switch
        {
            0 => throw new WorkflowException($"Cannot have Tuple of length 0."),
            2 => ConvertTwoTuple(dynamicList),
            3 => ConvertThreeTuple(dynamicList),
            4 => ConvertFourTuple(dynamicList),
            5 => ConvertFiveTuple(dynamicList),
            6 => ConvertSixTuple(dynamicList),
            7 => ConvertSevenTuple(dynamicList),
            _ => throw new WorkflowException($"Could not create Tuple for type ({inputType})")
        };
    }

    private static List<dynamic> ExtractTypes(Dictionary<Type, object> memory, Type inputType)
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

    private static dynamic ConvertTwoTuple(List<dynamic> dynamicList)
    {
        ArgumentNullException.ThrowIfNull(dynamicList);

        if (dynamicList.Count < 2)
            throw new ArgumentException("List must have at least two elements.", nameof(dynamicList));

        return ValueTuple.Create(
            dynamicList[0], 
            dynamicList[1]);
    }
    
    private static dynamic ConvertThreeTuple(List<dynamic> dynamicList)
    {
        ArgumentNullException.ThrowIfNull(dynamicList);

        if (dynamicList.Count < 3)
            throw new ArgumentException("List must have at least three elements.", nameof(dynamicList));

        return ValueTuple.Create(
            dynamicList[0], 
            dynamicList[1], 
            dynamicList[2]);
    }
    
    private static dynamic ConvertFourTuple(List<dynamic> dynamicList)
    {
        ArgumentNullException.ThrowIfNull(dynamicList);

        if (dynamicList.Count < 4)
            throw new ArgumentException("List must have at least four elements.", nameof(dynamicList));

        return ValueTuple.Create(
            dynamicList[0], 
            dynamicList[1], 
            dynamicList[3], 
            dynamicList[4]);
    }
    
    private static dynamic ConvertFiveTuple(List<dynamic> dynamicList)
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
    
    private static dynamic ConvertSixTuple(List<dynamic> dynamicList)
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
    
    private static dynamic ConvertSevenTuple(List<dynamic> dynamicList)
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
            dynamicList[6],
            dynamicList[5]);
    }
} 
