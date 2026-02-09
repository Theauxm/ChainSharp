using System.Text.Json;
using System.Text.Json.Serialization;
using ChainSharp.Effect.Utils;
using Moq;

namespace ChainSharp.Tests.Effect.Integration.Utils.Converters;

[TestFixture]
public class MockConverterTests
{
    private JsonSerializerOptions _options;

    [SetUp]
    public void Setup()
    {
        _options = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        _options.Converters.Add(new SystemTypeConverter());
        _options.Converters.Add(new MockConverter());
    }

    [Test]
    public void CanConvert_MockObject_ReturnsTrue()
    {
        // Arrange
        var converter = new MockConverter();
        var mockObject = new Mock<ITestInterface>();

        // Act
        bool canConvert = converter.CanConvert(mockObject.GetType());

        // Assert
        Assert.IsTrue(canConvert);
    }

    [Test]
    public void CanConvert_NonMockObject_ReturnsFalse()
    {
        // Arrange
        var converter = new MockConverter();
        var regularObject = new TestClass();

        // Act
        bool canConvert = converter.CanConvert(regularObject.GetType());

        // Assert
        Assert.IsFalse(canConvert);
    }

    [Test]
    public void Write_MockObject_WritesCorrectJson()
    {
        // Arrange
        var mock = new Mock<ITestInterface>();
        var objectWithMock = new ObjectWithMock { MockProperty = mock };

        // Act
        string json = JsonSerializer.Serialize(objectWithMock, _options);

        // Assert
        StringAssert.Contains("MockType", json);
        StringAssert.Contains("MockedType", json);
        StringAssert.Contains("ITestInterface", json);
    }

    [Test]
    public void Write_NestedMockObject_WritesCorrectJson()
    {
        // Arrange
        var mock = new Mock<ITestInterface>();
        var nestedObject = new NestedObjectWithMock
        {
            Name = "Test",
            InnerObject = new ObjectWithMock { MockProperty = mock }
        };

        // Act
        string json = JsonSerializer.Serialize(nestedObject, _options);

        // Assert
        StringAssert.Contains("MockType", json);
        StringAssert.Contains("MockedType", json);
        StringAssert.Contains("ITestInterface", json);
        StringAssert.Contains("\"Name\": \"Test\"", json);
    }

    [Test]
    public void Write_MockWithSetups_WritesWithoutError()
    {
        // Arrange
        var mock = new Mock<ITestInterface>();
        mock.Setup(m => m.GetValue()).Returns(42);
        mock.Setup(m => m.GetString()).Returns("test string");

        var objectWithMock = new ObjectWithMock { MockProperty = mock };

        // Act & Assert
        Assert.DoesNotThrow(() => JsonSerializer.Serialize(objectWithMock, _options));
    }

    [Test]
    public void Read_MockJson_ThrowsNotImplementedException()
    {
        // Arrange
        var json = "{\"MockProperty\":{\"MockType\":\"Mock`1\",\"MockedType\":\"ITestInterface\"}}";

        // Act & Assert
        Assert.Throws<NotImplementedException>(
            () => JsonSerializer.Deserialize<ObjectWithMock>(json, _options)
        );
    }

    [Test]
    public void Serialize_ObjectWithMockAndRegularProperties_HandlesCorrectly()
    {
        // Arrange
        var mock = new Mock<ITestInterface>();
        var complexObject = new ComplexObject
        {
            Id = 123,
            Name = "Test Object",
            Created = DateTime.Now,
            MockProperty = mock
        };

        // Act
        string json = JsonSerializer.Serialize(complexObject, _options);

        // Assert
        StringAssert.Contains("\"Id\": 123", json);
        StringAssert.Contains("\"Name\": \"Test Object\"", json);
        StringAssert.Contains("\"Created\":", json);
        StringAssert.Contains("MockType", json);
        StringAssert.Contains("ITestInterface", json);
    }

    [Test]
    public void Serialize_ArrayOfMocks_ProducesValidJson()
    {
        // Arrange
        Mock[] mocks = [new Mock<ITestInterface>(), new Mock<IDisposable>()];

        // Act
        var json = JsonSerializer.Serialize(mocks, _options);

        // Assert
        // Just verify we get valid JSON without errors
        Assert.DoesNotThrow(() => JsonDocument.Parse(json));
        Assert.That(json, Does.Contain("Mock"));

        // Print the actual JSON for inspection
        Console.WriteLine($"Array of mocks serializes to: {json}");
    }

    // Test interfaces and classes
    public interface ITestInterface
    {
        int GetValue();
        string GetString();
    }

    private class TestClass { }

    private class ObjectWithMock
    {
        public Mock<ITestInterface> MockProperty { get; set; }
    }

    private class NestedObjectWithMock
    {
        public string Name { get; set; }
        public ObjectWithMock InnerObject { get; set; }
    }

    private class ComplexObject
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public DateTime Created { get; set; }
        public Mock<ITestInterface> MockProperty { get; set; }
    }
}
