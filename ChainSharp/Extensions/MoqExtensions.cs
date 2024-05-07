using System.Reflection;

namespace ChainSharp.Extensions;

public static class MoqExtensions
{
    public static bool IsMoqProxy(this Type type)
    {
        return type.BaseType?.Assembly.FullName != null
               && type.BaseType.Assembly.FullName.Split(", ").FirstOrDefault() == "Moq";
    }
    
    public static Type GetMockedTypeFromObject(this object mockedObject)
    {
        if (mockedObject == null)
            throw new ArgumentNullException(nameof(mockedObject));

        var mockField = mockedObject.GetType().GetProperty("Mock", BindingFlags.Instance | BindingFlags.Public);
        if (mockField == null)
            return null;

        var mock = mockField.GetValue(mockedObject);
        if (mock == null)
            return null;

        // Now, get the MockType from Mock<T>
        var mockedTypeProperty = mock.GetType().GetProperty("MockedType", BindingFlags.Instance | BindingFlags.NonPublic);
        if (mockedTypeProperty == null)
            return null;

        return mockedTypeProperty.GetValue(mock) as Type;
    }
}