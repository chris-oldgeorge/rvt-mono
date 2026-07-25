using System.Text.RegularExpressions;
using System.Xml.Linq;

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
    private static readonly Regex AliasUsingPattern = new(
        @"(?m)^[ \t]*(?<global>global[ \t]+)?using[ \t]+(?<alias>[A-Za-z_][A-Za-z0-9_]*)[ \t]*=[ \t]*(?:global::)?(?<target>[A-Za-z_][A-Za-z0-9_]*(?:[ \t]*\.[ \t]*[A-Za-z_][A-Za-z0-9_]*)*)[ \t]*;",
        RegexOptions.CultureInvariant);

    private static readonly Regex NamespaceUsingPattern = new(
        @"(?m)^[ \t]*(?:global[ \t]+)?using[ \t]+(?:global::)?(?<target>[A-Za-z_][A-Za-z0-9_]*(?:[ \t]*\.[ \t]*[A-Za-z_][A-Za-z0-9_]*)*)[ \t]*;",
        RegexOptions.CultureInvariant);

    private static readonly Regex QualifiedNamePattern = new(
        @"(?<![A-Za-z0-9_])(?:global::)?(?<name>[A-Za-z_][A-Za-z0-9_]*(?:[ \t\r\n]*\.[ \t\r\n]*[A-Za-z_][A-Za-z0-9_]*)+)",
        RegexOptions.CultureInvariant);

    private static readonly Regex DeclaredTypePattern = new(
        @"\b(?:class|struct|interface|enum|record(?:[ \t]+(?:class|struct))?|delegate)[ \t]+(?<name>[A-Za-z_][A-Za-z0-9_]*)\b",
        RegexOptions.CultureInvariant);

    private static readonly string[] ImplicitSystemIoTypes =
        ["File", "Directory", "FileStream"];

    public static CSharpDependencyAnalysis Analyze(string source) =>
        AnalyzeProject(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Source.cs"] = source,
        });

    public static CSharpDependencyAnalysis AnalyzeProject(
        IReadOnlyDictionary<string, string> sources)
    {
        ArgumentNullException.ThrowIfNull(sources);
        var sanitizedSources = sources
            .OrderBy(source => source.Key, StringComparer.Ordinal)
            .ToDictionary(
                source => source.Key,
                source => Sanitize(source.Value),
                StringComparer.Ordinal);
        var aliases = sanitizedSources
            .SelectMany(source => AliasUsingPattern
                .Matches(source.Value)
                .Select(match => new AliasDefinition(
                    match.Groups["alias"].Value,
                    NormalizeName(match.Groups["target"].Value),
                    match.Groups["global"].Success,
                    source.Key)))
            .ToArray();
        var globalAliases = aliases
            .Where(alias => alias.IsGlobal)
            .ToDictionary(
                alias => alias.Name,
                alias => alias.Target,
                StringComparer.Ordinal);
        var declaredTypes = sanitizedSources.Values
            .SelectMany(source => DeclaredTypePattern
                .Matches(source)
                .Select(match => match.Groups["name"].Value))
            .ToHashSet(StringComparer.Ordinal);
        var filesByDependency = new Dictionary<string, HashSet<string>>(
            StringComparer.Ordinal);

        foreach (var source in sanitizedSources)
        {
            var sourceAliases = new Dictionary<string, string>(
                globalAliases,
                StringComparer.Ordinal);
            foreach (var alias in aliases.Where(alias =>
                         alias.SourcePath.Equals(
                             source.Key,
                             StringComparison.Ordinal)))
            {
                sourceAliases[alias.Name] = alias.Target;
                RecordDependency(alias.Target, source.Key, filesByDependency);
            }

            foreach (Match match in NamespaceUsingPattern.Matches(source.Value))
            {
                RecordDependency(
                    NormalizeName(match.Groups["target"].Value),
                    source.Key,
                    filesByDependency);
            }

            foreach (Match match in QualifiedNamePattern.Matches(source.Value))
            {
                var dependency = NormalizeName(match.Groups["name"].Value);
                var separator = dependency.IndexOf('.');
                if (separator > 0
                    && sourceAliases.TryGetValue(
                        dependency[..separator],
                        out var aliasTarget))
                {
                    dependency = aliasTarget + dependency[separator..];
                }

                RecordDependency(dependency, source.Key, filesByDependency);
            }

            foreach (var implicitType in ImplicitSystemIoTypes)
            {
                if (!declaredTypes.Contains(implicitType)
                    && Regex.IsMatch(
                        source.Value,
                        $@"\b{Regex.Escape(implicitType)}\b",
                        RegexOptions.CultureInvariant))
                {
                    RecordDependency(
                        $"System.IO.{implicitType}",
                        source.Key,
                        filesByDependency);
                }
            }
        }

        return new CSharpDependencyAnalysis(
            filesByDependency.ToDictionary(
                item => item.Key,
                item => (IReadOnlyCollection<string>)item.Value.ToArray(),
                StringComparer.Ordinal));
    }

    private static void RecordDependency(
        string dependency,
        string sourcePath,
        IDictionary<string, HashSet<string>> filesByDependency)
    {
        if (dependency.Length == 0)
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

    private static string NormalizeName(string value) =>
        Regex.Replace(
                value,
                @"[ \t\r\n]",
                string.Empty,
                RegexOptions.CultureInvariant)
            .Replace("global::", string.Empty, StringComparison.Ordinal);

    private static string Sanitize(string source)
    {
        var sanitized = source.ToCharArray();
        SanitizeCode(source, sanitized, 0, stopAtClosingBrace: false);
        return new string(sanitized);
    }

    private static int SanitizeCode(
        string source,
        char[] sanitized,
        int start,
        bool stopAtClosingBrace)
    {
        var braceDepth = 0;
        for (var index = start; index < source.Length;)
        {
            if (stopAtClosingBrace && source[index] == '}')
            {
                if (braceDepth == 0)
                {
                    return index;
                }

                braceDepth--;
                index++;
                continue;
            }

            if (stopAtClosingBrace && source[index] == '{')
            {
                braceDepth++;
                index++;
                continue;
            }

            if (source[index] == '/'
                && index + 1 < source.Length
                && source[index + 1] == '/')
            {
                var end = source.IndexOf('\n', index + 2);
                end = end < 0 ? source.Length : end;
                Blank(sanitized, index, end);
                index = end;
                continue;
            }

            if (source[index] == '/'
                && index + 1 < source.Length
                && source[index + 1] == '*')
            {
                var end = source.IndexOf(
                    "*/",
                    index + 2,
                    StringComparison.Ordinal);
                end = end < 0 ? source.Length : end + 2;
                Blank(sanitized, index, end);
                index = end;
                continue;
            }

            if (TryGetStringStart(
                    source,
                    index,
                    out var quoteIndex,
                    out var verbatim,
                    out var interpolated,
                    out var quoteCount))
            {
                index = interpolated && quoteCount == 1
                    ? SanitizeInterpolatedString(
                        source,
                        sanitized,
                        index,
                        quoteIndex,
                        verbatim)
                    : SanitizeString(
                        source,
                        sanitized,
                        index,
                        quoteIndex,
                        verbatim,
                        quoteCount);
                continue;
            }

            if (source[index] == '\'')
            {
                index = SanitizeCharacter(source, sanitized, index);
                continue;
            }

            index++;
        }

        return source.Length;
    }

    private static int SanitizeInterpolatedString(
        string source,
        char[] sanitized,
        int start,
        int quoteIndex,
        bool verbatim)
    {
        Blank(sanitized, start, quoteIndex + 1);
        for (var index = quoteIndex + 1; index < source.Length;)
        {
            if (!verbatim && source[index] == '\\')
            {
                var end = Math.Min(index + 2, source.Length);
                Blank(sanitized, index, end);
                index = end;
                continue;
            }

            if (source[index] == '"')
            {
                if (verbatim
                    && index + 1 < source.Length
                    && source[index + 1] == '"')
                {
                    Blank(sanitized, index, index + 2);
                    index += 2;
                    continue;
                }

                Blank(sanitized, index, index + 1);
                return index + 1;
            }

            if (source[index] == '{')
            {
                if (index + 1 < source.Length && source[index + 1] == '{')
                {
                    Blank(sanitized, index, index + 2);
                    index += 2;
                    continue;
                }

                Blank(sanitized, index, index + 1);
                var closingBrace = SanitizeCode(
                    source,
                    sanitized,
                    index + 1,
                    stopAtClosingBrace: true);
                if (closingBrace >= source.Length)
                {
                    return source.Length;
                }

                Blank(sanitized, closingBrace, closingBrace + 1);
                index = closingBrace + 1;
                continue;
            }

            if (source[index] == '}'
                && index + 1 < source.Length
                && source[index + 1] == '}')
            {
                Blank(sanitized, index, index + 2);
                index += 2;
                continue;
            }

            Blank(sanitized, index, index + 1);
            index++;
        }

        return source.Length;
    }

    private static int SanitizeString(
        string source,
        char[] sanitized,
        int start,
        int quoteIndex,
        bool verbatim,
        int quoteCount)
    {
        if (quoteCount >= 3)
        {
            var terminator = new string('"', quoteCount);
            var closing = source.IndexOf(
                terminator,
                quoteIndex + quoteCount,
                StringComparison.Ordinal);
            var end = closing < 0
                ? source.Length
                : closing + quoteCount;
            Blank(sanitized, start, end);
            return end;
        }

        for (var index = quoteIndex + 1; index < source.Length; index++)
        {
            if (!verbatim && source[index] == '\\')
            {
                index++;
                continue;
            }

            if (source[index] != '"')
            {
                continue;
            }

            if (verbatim
                && index + 1 < source.Length
                && source[index + 1] == '"')
            {
                index++;
                continue;
            }

            var end = index + 1;
            Blank(sanitized, start, end);
            return end;
        }

        Blank(sanitized, start, source.Length);
        return source.Length;
    }

    private static int SanitizeCharacter(
        string source,
        char[] sanitized,
        int start)
    {
        for (var index = start + 1; index < source.Length; index++)
        {
            if (source[index] == '\\')
            {
                index++;
                continue;
            }

            if (source[index] == '\'')
            {
                var end = index + 1;
                Blank(sanitized, start, end);
                return end;
            }
        }

        Blank(sanitized, start, source.Length);
        return source.Length;
    }

    private static bool TryGetStringStart(
        string source,
        int index,
        out int quoteIndex,
        out bool verbatim,
        out bool interpolated,
        out int quoteCount)
    {
        quoteIndex = -1;
        verbatim = false;
        interpolated = false;
        quoteCount = 0;

        if (source[index] == '"')
        {
            quoteIndex = index;
        }
        else if (source[index] == '@'
                 && index + 1 < source.Length
                 && source[index + 1] == '"')
        {
            quoteIndex = index + 1;
            verbatim = true;
        }
        else if (source[index] == '$'
                 && index + 1 < source.Length
                 && source[index + 1] == '"')
        {
            quoteIndex = index + 1;
            interpolated = true;
        }
        else if (index + 2 < source.Length
                 && ((source[index] == '$'
                      && source[index + 1] == '@')
                     || (source[index] == '@'
                         && source[index + 1] == '$'))
                 && source[index + 2] == '"')
        {
            quoteIndex = index + 2;
            verbatim = true;
            interpolated = true;
        }

        if (quoteIndex < 0)
        {
            return false;
        }

        while (quoteIndex + quoteCount < source.Length
               && source[quoteIndex + quoteCount] == '"')
        {
            quoteCount++;
        }

        return true;
    }

    private static void Blank(char[] value, int start, int end)
    {
        for (var index = start; index < end; index++)
        {
            if (value[index] is not ('\r' or '\n'))
            {
                value[index] = ' ';
            }
        }
    }

    private sealed record AliasDefinition(
        string Name,
        string Target,
        bool IsGlobal,
        string SourcePath);
}
