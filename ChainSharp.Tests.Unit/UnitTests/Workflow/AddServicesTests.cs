using ChainSharp.Workflow;
using FluentAssertions;
using LanguageExt;

namespace ChainSharp.Tests.Unit.UnitTests.Workflow;

public class AddServicesTests : TestSetup
{
    [Theory]
    public async Task TestAddServices()
    {
        // Arrange
        var inputService = new TestService();
        var workflow = new TestWorkflow();

        var services = new object[] { inputService };
        var serviceTypes = new[] { typeof(ITestService) };

        // Act
        workflow.AddServices(services, serviceTypes);

        // Assert
        workflow.Exception.Should().BeNull();
        workflow.Memory.Should().NotBeNull();
        workflow.Memory.Should().ContainKey(typeof(ITestService));
        workflow.Memory.Should().ContainValue(inputService);
    }

    [Theory]
    public async Task TestInvalidAddServices()
    {
        // Arrange
        var inputService = 1;
        var workflow = new TestWorkflow();

        var services = new object[] { inputService };
        var serviceTypes = new[] { typeof(ITestService) };

        // Act
        workflow.AddServices(services, serviceTypes);

        // Assert
        workflow.Exception.Should().NotBeNull();
        workflow.Memory.Should().NotBeNull();
    }

    [Theory]
    public async Task TestInvalidAddServicesNoInterface()
    {
        // Arrange
        var inputService = new TestServiceNoInterface();
        var workflow = new TestWorkflow();

        var services = new object[] { inputService };
        var serviceTypes = new[] { typeof(ITestService) };

        // Act
        workflow.AddServices(services, serviceTypes);

        // Assert
        workflow.Exception.Should().NotBeNull();
        workflow.Memory.Should().NotBeNull();
    }

    [Theory]
    public async Task TestAddServicesOneType()
    {
        // Arrange
        var inputService = new TestService();
        var workflow = new TestWorkflow();

        // Act
        workflow.AddServices<ITestService>(inputService);

        // Assert
        workflow.Memory.Should().NotBeNull();
        workflow.Exception.Should().BeNull();
        workflow.Memory.Count.Should().Be(2);
    }

    [Theory]
    public async Task TestAddServicesTwoTypes()
    {
        // Arrange
        var service1 = new TestService1();
        var service2 = new TestService2();
        var workflow = new TestWorkflow();

        // Act
        workflow.AddServices<ITestService1, ITestService2>(service1, service2);

        // Assert
        workflow.Memory.Should().NotBeNull();
        workflow.Exception.Should().BeNull();
        workflow.Memory.Count.Should().Be(3);
    }

    [Theory]
    public async Task TestAddServicesThreeTypes()
    {
        // Arrange
        var service1 = new TestService1();
        var service2 = new TestService2();
        var service3 = new TestService3();
        var workflow = new TestWorkflow();

        // Act
        workflow.AddServices<ITestService1, ITestService2, ITestService3>(
            service1,
            service2,
            service3
        );

        // Assert
        workflow.Memory.Should().NotBeNull();
        workflow.Exception.Should().BeNull();
        workflow.Memory.Count.Should().Be(4);
    }

    [Theory]
    public async Task TestAddServicesFourTypes()
    {
        // Arrange
        var service1 = new TestService1();
        var service2 = new TestService2();
        var service3 = new TestService3();
        var service4 = new TestService4();
        var workflow = new TestWorkflow();

        // Act
        workflow.AddServices<ITestService1, ITestService2, ITestService3, ITestService4>(
            service1,
            service2,
            service3,
            service4
        );

        // Assert
        workflow.Memory.Should().NotBeNull();
        workflow.Exception.Should().BeNull();
        workflow.Memory.Count.Should().Be(5);
    }

    [Theory]
    public async Task TestAddServicesFiveTypes()
    {
        // Arrange
        var service1 = new TestService1();
        var service2 = new TestService2();
        var service3 = new TestService3();
        var service4 = new TestService4();
        var service5 = new TestService5();
        var workflow = new TestWorkflow();

        // Act
        workflow.AddServices<
            ITestService1,
            ITestService2,
            ITestService3,
            ITestService4,
            ITestService5
        >(service1, service2, service3, service4, service5);

        // Assert
        workflow.Memory.Should().NotBeNull();
        workflow.Exception.Should().BeNull();
        workflow.Memory.Count.Should().Be(6);
    }

    [Theory]
    public async Task TestAddServicesSixTypes()
    {
        // Arrange
        var service1 = new TestService1();
        var service2 = new TestService2();
        var service3 = new TestService3();
        var service4 = new TestService4();
        var service5 = new TestService5();
        var service6 = new TestService6();
        var workflow = new TestWorkflow();

        // Act
        workflow.AddServices<
            ITestService1,
            ITestService2,
            ITestService3,
            ITestService4,
            ITestService5,
            ITestService6
        >(service1, service2, service3, service4, service5, service6);

        // Assert
        workflow.Memory.Should().NotBeNull();
        workflow.Exception.Should().BeNull();
        workflow.Memory.Count.Should().Be(7);
    }

    [Theory]
    public async Task TestAddServicesSevenTypes()
    {
        // Arrange
        var service1 = new TestService1();
        var service2 = new TestService2();
        var service3 = new TestService3();
        var service4 = new TestService4();
        var service5 = new TestService5();
        var service6 = new TestService6();
        var service7 = new TestService7();
        var workflow = new TestWorkflow();

        // Act
        workflow.AddServices<
            ITestService1,
            ITestService2,
            ITestService3,
            ITestService4,
            ITestService5,
            ITestService6,
            ITestService7
        >(service1, service2, service3, service4, service5, service6, service7);

        // Assert
        workflow.Memory.Should().NotBeNull();
        workflow.Exception.Should().BeNull();
        workflow.Memory.Count.Should().Be(8);
    }

    private class TestServiceNoInterface { }

    private class TestService : ITestService { }

    private class TestService1 : ITestService1 { }

    private class TestService2 : ITestService2 { }

    private class TestService3 : ITestService3 { }

    private class TestService4 : ITestService4 { }

    private class TestService5 : ITestService5 { }

    private class TestService6 : ITestService6 { }

    private class TestService7 : ITestService7 { }

    private interface ITestService { }

    private interface ITestService1 { }

    private interface ITestService2 { }

    private interface ITestService3 { }

    private interface ITestService4 { }

    private interface ITestService5 { }

    private interface ITestService6 { }

    private interface ITestService7 { }

    private class TestWorkflow : Workflow<int, string>
    {
        protected override Task<Either<Exception, string>> RunInternal(int input) =>
            throw new NotImplementedException();
    }
}
