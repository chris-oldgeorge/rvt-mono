using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Rvt.Storage.Tests.Architecture;

[TestClass]
public sealed class StorageDependencyBoundaryRegressionTests
{
    [TestMethod]
    public void ProjectDependencyReader_RecognizesUpdateAndHonorsRemove()
    {
        var project = XDocument.Parse(
            """
            <Project>
              <ItemGroup>
                <PackageReference Include="Allowed.Package" />
                <PackageReference Update="Updated.Package" />
                <PackageReference Include="Removed.Package" />
                <PackageReference Remove="Removed.Package" />
              </ItemGroup>
            </Project>
            """);

        var identities = ProjectDependencyReader.ReadActiveIdentities(
            project,
            "PackageReference");

        CollectionAssert.AreEquivalent(
            new[] { "Allowed.Package", "Updated.Package" },
            identities.ToArray());
    }

    [TestMethod]
    public void SourceAnalyzer_IgnoresCommentsAndStringLiterals()
    {
        const string source =
            """
            namespace Example;
            // using Amazon.S3;
            /* Azure.Storage.Blobs.BlobClient */
            internal sealed class Sample
            {
                private const string Description = "Microsoft.Extensions.Hosting";
            }
            """;

        var analysis = CSharpDependencyAnalyzer.Analyze(source);

        Assert.IsFalse(analysis.UsesDependency("Amazon."));
        Assert.IsFalse(analysis.UsesDependency("Azure."));
        Assert.IsFalse(analysis.UsesDependency("Microsoft.Extensions."));
    }

    [TestMethod]
    public void SourceAnalyzer_RootNamespaceMatchesChildNamespaceGuard()
    {
        const string source =
            """
            using Amazon;
            namespace Example;
            internal sealed class Sample;
            """;

        var analysis = CSharpDependencyAnalyzer.Analyze(source);

        Assert.IsTrue(analysis.UsesDependency("Amazon."));
    }

    [TestMethod]
    public void SourceAnalyzer_ResolvesGlobalUsingAndNamespaceAliases()
    {
        const string source =
            """
            global using Blob = global::Azure.Storage.Blobs.BlobClient;
            using IO = System.IO;
            namespace Example;
            internal sealed class Sample
            {
                public void Delete() => IO.File.Delete("sample.bin");
                public Blob? Value { get; }
            }
            """;

        var analysis = CSharpDependencyAnalyzer.Analyze(source);

        Assert.IsTrue(analysis.UsesDependency("Azure.Storage.Blobs"));
        Assert.IsTrue(analysis.UsesDependency("System.IO.File"));
    }

    [TestMethod]
    public void SourceAnalyzer_AnalyzesExecutableInterpolationHoles()
    {
        const string source =
            """
            namespace Example;
            internal sealed class Sample
            {
                public string Describe() =>
                    $"Exists: {System.IO.File.Exists("sample.bin")}";
            }
            """;

        var analysis = CSharpDependencyAnalyzer.Analyze(source);

        Assert.IsTrue(analysis.UsesDependency("System.IO.File"));
    }

    [TestMethod]
    public void SourceAnalyzer_ResolvesGlobalAliasesAcrossSourceFiles()
    {
        var sources = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["GlobalUsings.cs"] = "global using IO = System.IO;",
            ["Consumer.cs"] =
                """
                namespace Example;
                internal sealed class Sample
                {
                    public void Delete() => IO.File.Delete("sample.bin");
                }
                """,
        };

        var analysis = CSharpDependencyAnalyzer.AnalyzeProject(sources);

        Assert.IsTrue(analysis.UsesDependency("System.IO.File"));
    }

    [TestMethod]
    public void SourceAnalyzer_DoesNotTreatUserDefinedFilesystemNamesAsSystemIo()
    {
        const string source =
            """
            namespace Example;
            internal sealed class File;
            internal sealed class Directory;
            internal sealed class FileStream;
            internal sealed class Sample
            {
                public File? CurrentFile { get; }
                public Directory? CurrentDirectory { get; }
                public FileStream? CurrentStream { get; }
            }
            """;

        var analysis = CSharpDependencyAnalyzer.Analyze(source);

        Assert.IsFalse(analysis.UsesDependency("System.IO.File"));
        Assert.IsFalse(analysis.UsesDependency("System.IO.Directory"));
        Assert.IsFalse(analysis.UsesDependency("System.IO.FileStream"));
    }
}

internal static class ProjectDependencyReader
{
    public static IReadOnlyCollection<string> ReadActiveIdentities(
        XDocument project,
        string itemName)
    {
        var activeIdentities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var element in project
                     .Descendants()
                     .Where(element => element.Name.LocalName == itemName))
        {
            Apply(element.Attribute("Include")?.Value, activeIdentities.Add);
            Apply(element.Attribute("Update")?.Value, activeIdentities.Add);
            Apply(element.Attribute("Remove")?.Value, identity =>
                activeIdentities.Remove(identity));
        }

        return activeIdentities
            .OrderBy(identity => identity, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static void Apply(string? value, Func<string, bool> operation)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        foreach (var identity in value.Split(
                     ';',
                     StringSplitOptions.RemoveEmptyEntries
                     | StringSplitOptions.TrimEntries))
        {
            operation(identity);
        }
    }
}

internal sealed class CSharpDependencyAnalysis(
    IReadOnlyDictionary<string, IReadOnlyCollection<string>> sourceFilesByDependency)
{
    public bool UsesDependency(string dependencyName) =>
        sourceFilesByDependency.Keys.Any(dependency =>
            MatchesDependency(dependency, dependencyName));

    public IReadOnlyCollection<string> GetSourceFilesUsing(string dependencyName) =>
        sourceFilesByDependency
            .Where(item => MatchesDependency(item.Key, dependencyName))
            .SelectMany(item => item.Value)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

    private static bool MatchesDependency(
        string dependency,
        string dependencyName) =>
        dependencyName.EndsWith(".", StringComparison.Ordinal)
            ? dependency.Equals(
                    dependencyName[..^1],
                    StringComparison.Ordinal)
                || dependency.StartsWith(
                    dependencyName,
                    StringComparison.Ordinal)
            : dependency.Equals(dependencyName, StringComparison.Ordinal)
                || dependency.StartsWith(
                    $"{dependencyName}.",
                    StringComparison.Ordinal);
}

internal static class CSharpDependencyAnalyzer
{
    private const string ImplicitUsingsPath = "__RvtStorageImplicitUsings.g.cs";

    private static readonly Lazy<IReadOnlyCollection<MetadataReference>>
        MetadataReferences = new(CreateMetadataReferences);

    public static CSharpDependencyAnalysis Analyze(string source) =>
        AnalyzeProject(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Source.cs"] = source,
        });

    public static CSharpDependencyAnalysis AnalyzeProject(
        IReadOnlyDictionary<string, string> sources)
    {
        ArgumentNullException.ThrowIfNull(sources);
        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(
            LanguageVersion.Preview);
        var sourceTrees = sources
            .OrderBy(source => source.Key, StringComparer.Ordinal)
            .Select(source => CSharpSyntaxTree.ParseText(
                source.Value,
                parseOptions,
                source.Key))
            .ToArray();
        var implicitUsings = CSharpSyntaxTree.ParseText(
            """
            global using System;
            global using System.Collections.Generic;
            global using System.IO;
            global using System.Linq;
            global using System.Threading;
            global using System.Threading.Tasks;
            """,
            parseOptions,
            ImplicitUsingsPath);
        var compilation = CSharpCompilation.Create(
            $"RvtStorageDependencyAnalysis_{Guid.NewGuid():N}",
            sourceTrees.Append(implicitUsings),
            MetadataReferences.Value,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var filesByDependency = new Dictionary<string, HashSet<string>>(
            StringComparer.Ordinal);

        foreach (var syntaxTree in sourceTrees)
        {
            var semanticModel = compilation.GetSemanticModel(
                syntaxTree,
                ignoreAccessibility: true);
            var root = syntaxTree.GetRoot();
            foreach (var name in root.DescendantNodes().OfType<NameSyntax>())
            {
                if (name is IdentifierNameSyntax identifier)
                {
                    RecordSymbol(
                        semanticModel.GetAliasInfo(identifier)?.Target,
                        syntaxTree.FilePath,
                        filesByDependency);
                }

                var symbolInfo = semanticModel.GetSymbolInfo(name);
                RecordSymbol(
                    symbolInfo.Symbol ?? symbolInfo.CandidateSymbols.FirstOrDefault(),
                    syntaxTree.FilePath,
                    filesByDependency);
                RecordSymbol(
                    semanticModel.GetTypeInfo(name).Type,
                    syntaxTree.FilePath,
                    filesByDependency);
            }
        }

        return new CSharpDependencyAnalysis(
            filesByDependency.ToDictionary(
                item => item.Key,
                item => (IReadOnlyCollection<string>)item.Value.ToArray(),
                StringComparer.Ordinal));
    }

    private static void RecordSymbol(
        ISymbol? symbol,
        string sourcePath,
        IDictionary<string, HashSet<string>> filesByDependency)
    {
        var dependencySymbol = symbol switch
        {
            IAliasSymbol alias => alias.Target,
            INamespaceSymbol namespaceSymbol => namespaceSymbol,
            INamedTypeSymbol namedType => namedType.OriginalDefinition,
            IMethodSymbol method => method.ContainingType,
            IPropertySymbol property => property.ContainingType,
            IFieldSymbol field => field.ContainingType,
            IEventSymbol eventSymbol => eventSymbol.ContainingType,
            ILocalSymbol local => local.Type,
            IParameterSymbol parameter => parameter.Type,
            _ => null,
        };

        if (dependencySymbol is null)
        {
            return;
        }

        var dependency = dependencySymbol
            .ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
            .Replace("global::", string.Empty, StringComparison.Ordinal);
        if (dependency.Length == 0 || dependency == "<global namespace>")
        {
            return;
        }

        if (!filesByDependency.TryGetValue(dependency, out var sourceFiles))
        {
            sourceFiles = new HashSet<string>(StringComparer.Ordinal);
            filesByDependency.Add(dependency, sourceFiles);
        }

        sourceFiles.Add(sourcePath);
    }

    private static IReadOnlyCollection<MetadataReference> CreateMetadataReferences()
    {
        var assemblyPaths = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);
        if (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") is string
            trustedPlatformAssemblies)
        {
            foreach (var path in trustedPlatformAssemblies.Split(
                         Path.PathSeparator,
                         StringSplitOptions.RemoveEmptyEntries))
            {
                assemblyPaths.Add(path);
            }
        }

        foreach (var path in Directory.EnumerateFiles(
                     AppContext.BaseDirectory,
                     "*.dll",
                     SearchOption.TopDirectoryOnly))
        {
            assemblyPaths.Add(path);
        }

        var references = new List<MetadataReference>();
        foreach (var path in assemblyPaths.OrderBy(
                     path => path,
                     StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                references.Add(MetadataReference.CreateFromFile(path));
            }
            catch (BadImageFormatException)
            {
            }
            catch (FileLoadException)
            {
            }
        }

        return references;
    }
}
