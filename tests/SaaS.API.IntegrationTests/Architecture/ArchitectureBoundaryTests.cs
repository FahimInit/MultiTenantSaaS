using System.Reflection;
using NetArchTest.Rules;
using SaaS.API.Controllers.v1;
using SaaS.Application;
using SaaS.Domain.Common;
using SaaS.Infrastructure;
using SaaS.Infrastructure.Persistence;
using Shouldly;

namespace SaaS.API.IntegrationTests.Architecture;

public sealed class ArchitectureBoundaryTests
{
    private static readonly Assembly DomainAssembly = typeof(Entity).Assembly;
    private static readonly Assembly ApplicationAssembly = typeof(SaaS.Application.DependencyInjection).Assembly;
    private static readonly Assembly InfrastructureAssembly = typeof(ApplicationDbContext).Assembly;
    private static readonly Assembly ApiAssembly = typeof(SystemController).Assembly;

    [Fact]
    public void Domain_ShouldNotDependOnOuterLayersOrFrameworks()
    {
        var result = Types.InAssembly(DomainAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "SaaS.Application",
                "SaaS.Infrastructure",
                "SaaS.API",
                "Microsoft.EntityFrameworkCore",
                "Microsoft.AspNetCore",
                "Npgsql")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(
            $"Domain violations: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void Application_ShouldNotDependOnInfrastructureOrApiOrEfCore()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "SaaS.Infrastructure",
                "SaaS.API",
                "Microsoft.EntityFrameworkCore",
                "Npgsql")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(
            $"Application violations: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void Infrastructure_ShouldNotDependOnApi()
    {
        var result = Types.InAssembly(InfrastructureAssembly)
            .ShouldNot()
            .HaveDependencyOn("SaaS.API")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(
            $"Infrastructure violations: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void Controllers_ShouldNotDependDirectlyOnDbContext()
    {
        var result = Types.InAssembly(ApiAssembly)
            .That()
            .ResideInNamespace("SaaS.API.Controllers")
            .ShouldNot()
            .HaveDependencyOn(typeof(ApplicationDbContext).FullName!)
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(
            $"Controller DbContext violations: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }
}
