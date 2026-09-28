using System.Reflection;

namespace FieldApp.UnitTests.Architecture;

/// <summary>Enforces the solution boundaries in docs/03-architecture.md.</summary>
public sealed class LayerDependencyTests
{
    private static readonly Assembly _domain = typeof(global::FieldApp.Domain.AssemblyReference).Assembly;
    private static readonly Assembly _application = typeof(global::FieldApp.Application.AssemblyReference).Assembly;
    private static readonly Assembly _infrastructure = typeof(global::FieldApp.Infrastructure.AssemblyReference).Assembly;

    private static readonly string[] _frameworkAndAzurePrefixes =
    [
        "Microsoft.EntityFrameworkCore",
        "Microsoft.AspNetCore",
        "Microsoft.Data.SqlClient",
        "Azure",
    ];

    [Fact]
    public void Domain_does_not_depend_on_other_layers_or_infrastructure_frameworks()
    {
        var references = ReferencedAssemblyNames(_domain);

        Assert.DoesNotContain(references, name => name.StartsWith("FieldApp.", StringComparison.Ordinal));
        AssertNoFrameworkOrAzureReferences(references);
    }

    [Fact]
    public void Application_depends_only_on_domain_among_solution_layers()
    {
        var references = ReferencedAssemblyNames(_application);

        Assert.DoesNotContain("FieldApp.Infrastructure", references);
        Assert.DoesNotContain("FieldApp.Api", references);
        AssertNoFrameworkOrAzureReferences(references);
    }

    [Fact]
    public void Infrastructure_does_not_depend_on_api()
    {
        Assert.DoesNotContain("FieldApp.Api", ReferencedAssemblyNames(_infrastructure));
    }

    private static string[] ReferencedAssemblyNames(Assembly assembly) =>
        [.. assembly.GetReferencedAssemblies().Select(reference => reference.Name ?? string.Empty)];

    private static void AssertNoFrameworkOrAzureReferences(string[] references)
    {
        foreach (var prefix in _frameworkAndAzurePrefixes)
        {
            Assert.DoesNotContain(references, name => name.StartsWith(prefix, StringComparison.Ordinal));
        }
    }
}
