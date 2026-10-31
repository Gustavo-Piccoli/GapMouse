using GapMouse.Application;
using GapMouse.Domain;
using NetArchTest.Rules;

namespace GapMouse.Architecture.Tests;

public class LayeringTests
{
    [Fact]
    public void Domain_DependsOnNoOtherLayer() =>
        Types.InAssembly(DomainAssembly.Reference).ShouldNot()
            .HaveDependencyOnAny("GapMouse.Application", "GapMouse.Infrastructure")
            .GetResult().IsSuccessful.ShouldBeTrue();

    [Fact]
    public void Application_DoesNotDependOnInfrastructure() =>
        Types.InAssembly(ApplicationAssembly.Reference).ShouldNot()
            .HaveDependencyOn("GapMouse.Infrastructure")
            .GetResult().IsSuccessful.ShouldBeTrue();

    [Fact]
    public void Domain_DoesNotUseInterop() =>
        Types.InAssembly(DomainAssembly.Reference).ShouldNot()
            .HaveDependencyOnAny(
                "System.Runtime.InteropServices.Marshal",
                "System.Runtime.InteropServices.LibraryImportAttribute",
                "System.Runtime.InteropServices.NativeLibrary")
            .GetResult().IsSuccessful.ShouldBeTrue();
}
