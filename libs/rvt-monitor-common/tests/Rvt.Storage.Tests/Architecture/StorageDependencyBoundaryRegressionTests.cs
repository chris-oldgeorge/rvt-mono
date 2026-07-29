using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Rvt.Storage.Tests.Architecture;

[TestClass]
public sealed class StorageDependencyBoundaryRegressionTests
{
    private static readonly string[] _expected = ["Allowed.Package", "Updated.Package"];

    [TestMethod]
    public void ProjectDependencyReader_RecognizesUpdateAndHonorsRemove()
    {
        XDocument project = XDocument.Parse(
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

        IReadOnlyCollection<string> identities = ProjectDependencyReader.ReadActiveIdentities(
            project,
            "PackageReference");

        CollectionAssert.AreEquivalent(
            _expected,
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

        CSharpDependencyAnalysis analysis = CSharpDependencyAnalyzer.Analyze(source);

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

        CSharpDependencyAnalysis analysis = CSharpDependencyAnalyzer.Analyze(source);

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

        CSharpDependencyAnalysis analysis = CSharpDependencyAnalyzer.Analyze(source);

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

        CSharpDependencyAnalysis analysis = CSharpDependencyAnalyzer.Analyze(source);

        Assert.IsTrue(analysis.UsesDependency("System.IO.File"));
    }

    [TestMethod]
    public void SourceAnalyzer_ResolvesGlobalAliasesAcrossSourceFiles()
    {
        Dictionary<string, string> sources = new(StringComparer.Ordinal)
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

        CSharpDependencyAnalysis analysis = CSharpDependencyAnalyzer.AnalyzeProject(sources);

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

        CSharpDependencyAnalysis analysis = CSharpDependencyAnalyzer.Analyze(source);

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
        HashSet<string> activeIdentities = new(StringComparer.OrdinalIgnoreCase);
        foreach (XElement? element in project
                     .Descendants()
                     .Where(element => element.Name.LocalName == itemName))
        {
            Apply(element.Attribute("Include")?.Value, activeIdentities.Add);
            Apply(element.Attribute("Update")?.Value, activeIdentities.Add);
            Apply(element.Attribute("Remove")?.Value, identity =>
                activeIdentities.Remove(identity));
        }

        return [.. activeIdentities.OrderBy(identity => identity, StringComparer.OrdinalIgnoreCase)];
    }

    private static void Apply(string? value, Func<string, bool> operation)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        foreach (string identity in value.Split(
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
        [.. sourceFilesByDependency
            .Where(item => MatchesDependency(item.Key, dependencyName))
            .SelectMany(item => item.Value)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(path => path, StringComparer.Ordinal)];

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

internal static partial class CSharpDependencyAnalyzer
{
    private static readonly string[] _implicitSystemIoTypes =
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
        Dictionary<string, string> sanitizedSources = sources
            .OrderBy(source => source.Key, StringComparer.Ordinal)
            .ToDictionary(
                source => source.Key,
                source => Sanitize(source.Value),
                StringComparer.Ordinal);
        AliasDefinition[] aliases = [.. sanitizedSources
            .SelectMany(source => AliasUsingPattern()
                .Matches(source.Value)
                .Select(match => new AliasDefinition(
                    match.Groups["alias"].Value,
                    NormalizeName(match.Groups["target"].Value),
                    match.Groups["global"].Success,
                    source.Key)))];
        Dictionary<string, string> globalAliases = aliases
            .Where(alias => alias.IsGlobal)
            .ToDictionary(
                alias => alias.Name,
                alias => alias.Target,
                StringComparer.Ordinal);
        HashSet<string> declaredTypes = sanitizedSources.Values
            .SelectMany(source => DeclaredTypePattern()
                .Matches(source)
                .Select(match => match.Groups["name"].Value))
            .ToHashSet(StringComparer.Ordinal);
        Dictionary<string, HashSet<string>> filesByDependency = new(
            StringComparer.Ordinal);

        foreach (KeyValuePair<string, string> source in sanitizedSources)
        {
            Dictionary<string, string> sourceAliases = new(
                globalAliases,
                StringComparer.Ordinal);
            foreach (AliasDefinition? alias in aliases.Where(alias =>
                         alias.SourcePath.Equals(
                             source.Key,
                             StringComparison.Ordinal)))
            {
                sourceAliases[alias.Name] = alias.Target;
                RecordDependency(alias.Target, source.Key, filesByDependency);
            }

            foreach (Match match in NamespaceUsingPattern().Matches(source.Value))
            {
                RecordDependency(
                    NormalizeName(match.Groups["target"].Value),
                    source.Key,
                    filesByDependency);
            }

            foreach (Match match in QualifiedNamePattern().Matches(source.Value))
            {
                string dependency = NormalizeName(match.Groups["name"].Value);
                int separator = dependency.IndexOf('.');
                if (separator > 0
                    && sourceAliases.TryGetValue(
                        dependency[..separator],
                        out string? aliasTarget))
                {
                    dependency = aliasTarget + dependency[separator..];
                }

                RecordDependency(dependency, source.Key, filesByDependency);
            }

            foreach (string implicitType in _implicitSystemIoTypes)
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
                item => (IReadOnlyCollection<string>)[.. item.Value],
                StringComparer.Ordinal));
    }

    private static void RecordDependency(
        string dependency,
        string sourcePath,
        Dictionary<string, HashSet<string>> filesByDependency)
    {
        if (dependency.Length == 0)
        {
            return;
        }

        if (!filesByDependency.TryGetValue(dependency, out HashSet<string>? sourceFiles))
        {
            sourceFiles = new HashSet<string>(StringComparer.Ordinal);
            filesByDependency.Add(dependency, sourceFiles);
        }

        sourceFiles.Add(sourcePath);
    }

    private static string NormalizeName(string value) =>
        WhitespacePattern()
            .Replace(value, string.Empty)
            .Replace("global::", string.Empty, StringComparison.Ordinal);

    private static string Sanitize(string source)
    {
        char[] sanitized = source.ToCharArray();
        SanitizeCode(source, sanitized, 0, stopAtClosingBrace: false);
        return new string(sanitized);
    }

    private static int SanitizeCode(
        string source,
        char[] sanitized,
        int start,
        bool stopAtClosingBrace)
    {
        int braceDepth = 0;
        for (int index = start; index < source.Length;)
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
                int end = source.IndexOf('\n', index + 2);
                end = end < 0 ? source.Length : end;
                Blank(sanitized, index, end);
                index = end;
                continue;
            }

            if (source[index] == '/'
                && index + 1 < source.Length
                && source[index + 1] == '*')
            {
                int end = source.IndexOf(
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
                    out int quoteIndex,
                    out bool verbatim,
                    out bool interpolated,
                    out int quoteCount))
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
        for (int index = quoteIndex + 1; index < source.Length;)
        {
            if (!verbatim && source[index] == '\\')
            {
                int end = Math.Min(index + 2, source.Length);
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
                int closingBrace = SanitizeCode(
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
            string terminator = new('"', quoteCount);
            int closing = source.IndexOf(
                terminator,
                quoteIndex + quoteCount,
                StringComparison.Ordinal);
            int end = closing < 0
                ? source.Length
                : closing + quoteCount;
            Blank(sanitized, start, end);
            return end;
        }

        for (int index = quoteIndex + 1; index < source.Length; index++)
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

            int end = index + 1;
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
        for (int index = start + 1; index < source.Length; index++)
        {
            if (source[index] == '\\')
            {
                index++;
                continue;
            }

            if (source[index] == '\'')
            {
                int end = index + 1;
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
        for (int index = start; index < end; index++)
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

    [GeneratedRegex(
        @"(?m)^[ \t]*(?<global>global[ \t]+)?using[ \t]+(?<alias>[A-Za-z_][A-Za-z0-9_]*)[ \t]*=[ \t]*(?:global::)?(?<target>[A-Za-z_][A-Za-z0-9_]*(?:[ \t]*\.[ \t]*[A-Za-z_][A-Za-z0-9_]*)*)[ \t]*;",
        RegexOptions.CultureInvariant)]
    private static partial Regex AliasUsingPattern();

    [GeneratedRegex(
        @"(?m)^[ \t]*(?:global[ \t]+)?using[ \t]+(?:global::)?(?<target>[A-Za-z_][A-Za-z0-9_]*(?:[ \t]*\.[ \t]*[A-Za-z_][A-Za-z0-9_]*)*)[ \t]*;",
        RegexOptions.CultureInvariant)]
    private static partial Regex NamespaceUsingPattern();

    [GeneratedRegex(
        @"(?<![A-Za-z0-9_])(?:global::)?(?<name>[A-Za-z_][A-Za-z0-9_]*(?:[ \t\r\n]*\.[ \t\r\n]*[A-Za-z_][A-Za-z0-9_]*)+)",
        RegexOptions.CultureInvariant)]
    private static partial Regex QualifiedNamePattern();

    [GeneratedRegex(
        @"\b(?:class|struct|interface|enum|record(?:[ \t]+(?:class|struct))?|delegate)[ \t]+(?<name>[A-Za-z_][A-Za-z0-9_]*)\b",
        RegexOptions.CultureInvariant)]
    private static partial Regex DeclaredTypePattern();

    [GeneratedRegex(@"[ \t\r\n]", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespacePattern();
}
