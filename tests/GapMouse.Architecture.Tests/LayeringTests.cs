using GapMouse.App.Localization;
using GapMouse.Application;
using GapMouse.Domain;
using GapMouse.Infrastructure;
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
    public void Application_DoesNotDependOnIoLibraries() =>
        Types.InAssembly(ApplicationAssembly.Reference).ShouldNot()
            .HaveDependencyOnAny("System.Net.Http", "System.Text.Json", "Microsoft.Win32")
            .GetResult().IsSuccessful.ShouldBeTrue();

    [Fact]
    public void Domain_DoesNotUseInterop() =>
        Types.InAssembly(DomainAssembly.Reference).ShouldNot()
            .HaveDependencyOnAny(
                "System.Runtime.InteropServices.Marshal",
                "System.Runtime.InteropServices.LibraryImportAttribute",
                "System.Runtime.InteropServices.NativeLibrary")
            .GetResult().IsSuccessful.ShouldBeTrue();

    [Fact]
    public void AppViewModels_DoNotDependOnAvalonia() =>
        Types.InAssembly(typeof(Localizer).Assembly)
            .That().ResideInNamespaceStartingWith("GapMouse.App.ViewModels")
            .Or().ResideInNamespaceStartingWith("GapMouse.App.Localization")
            .ShouldNot().HaveDependencyOn("Avalonia")
            .GetResult().IsSuccessful.ShouldBeTrue();

    [Fact]
    public void Infrastructure_DoesNotDependOnApp() =>
        Types.InAssembly(InfrastructureAssembly.Reference).ShouldNot()
            .HaveDependencyOn("GapMouse.App")
            .GetResult().IsSuccessful.ShouldBeTrue();
}
