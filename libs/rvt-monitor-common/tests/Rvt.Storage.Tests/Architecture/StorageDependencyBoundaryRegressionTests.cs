using System.Xml.Linq;

namespace Rvt.Storage.Tests.Architecture;

[TestClass]
public sealed class StorageDependencyBoundaryTestsRegression
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
    IReadOnlyCollection<string> dependencies)
{
    public bool UsesDependency(string dependencyPrefix) =>
        dependencies.Any(dependency => dependency.StartsWith(
            dependencyPrefix,
            StringComparison.Ordinal));
}

internal static class CSharpDependencyAnalyzer
{
    private static readonly HashSet<string> FileSystemTypes =
        new(["File", "Directory", "FileStream"], StringComparer.Ordinal);

    public static CSharpDependencyAnalysis Analyze(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var tokens = Tokenize(source);
        var dependencies = new HashSet<string>(StringComparer.Ordinal);
        var aliases = ReadUsingDirectives(tokens, dependencies);

        for (var index = 0; index < tokens.Count; index++)
        {
            if (!IsIdentifier(tokens[index]))
            {
                continue;
            }

            var end = index;
            while (end + 2 < tokens.Count
                   && tokens[end + 1] is "." or "::"
                   && IsIdentifier(tokens[end + 2]))
            {
                end += 2;
            }

            var name = CanonicalizeQualifiedName(tokens, index, end);
            var firstSegment = name.Split('.', 2)[0];
            if (aliases.TryGetValue(firstSegment, out var aliasTarget))
            {
                name = name.Length == firstSegment.Length
                    ? aliasTarget
                    : $"{aliasTarget}.{name[(firstSegment.Length + 1)..]}";
            }

            if (name.Contains('.', StringComparison.Ordinal))
            {
                dependencies.Add(name);
            }
            else if (FileSystemTypes.Contains(name))
            {
                dependencies.Add($"System.IO.{name}");
            }

            index = end;
        }

        return new CSharpDependencyAnalysis(dependencies);
    }

    private static Dictionary<string, string> ReadUsingDirectives(
        IReadOnlyList<string> tokens,
        ISet<string> dependencies)
    {
        var aliases = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = 0; index < tokens.Count; index++)
        {
            if (tokens[index] != "using"
                || index + 1 >= tokens.Count
                || tokens[index + 1] == "(")
            {
                continue;
            }

            var end = index + 1;
            while (end < tokens.Count && tokens[end] != ";")
            {
                end++;
            }

            if (end == tokens.Count)
            {
                continue;
            }

            var equals = -1;
            for (var candidate = index + 1; candidate < end; candidate++)
            {
                if (tokens[candidate] == "=")
                {
                    equals = candidate;
                    break;
                }
            }

            var targetStart = equals >= 0 ? equals + 1 : index + 1;
            if (targetStart < end && tokens[targetStart] == "static")
            {
                targetStart++;
            }

            var target = CanonicalizeQualifiedName(tokens, targetStart, end - 1);
            if (target.Length > 0)
            {
                dependencies.Add(target);
                if (equals > index + 1)
                {
                    aliases[tokens[index + 1]] = target;
                }
            }

            index = end;
        }

        return aliases;
    }

    private static string CanonicalizeQualifiedName(
        IReadOnlyList<string> tokens,
        int start,
        int end)
    {
        var segments = new List<string>();
        for (var index = start; index <= end; index++)
        {
            if (IsIdentifier(tokens[index]) && tokens[index] != "global")
            {
                segments.Add(tokens[index]);
            }
        }

        return string.Join('.', segments);
    }

    private static IReadOnlyList<string> Tokenize(string source)
    {
        var tokens = new List<string>();
        for (var index = 0; index < source.Length;)
        {
            if (char.IsWhiteSpace(source[index]))
            {
                index++;
                continue;
            }

            if (source[index] == '/' && index + 1 < source.Length)
            {
                if (source[index + 1] == '/')
                {
                    index += 2;
                    while (index < source.Length && source[index] is not '\r' and not '\n')
                    {
                        index++;
                    }

                    continue;
                }

                if (source[index + 1] == '*')
                {
                    index += 2;
                    while (index + 1 < source.Length
                           && (source[index] != '*' || source[index + 1] != '/'))
                    {
                        index++;
                    }

                    index = Math.Min(source.Length, index + 2);
                    continue;
                }
            }

            if (IsStringOrCharacterStart(source, index, out var quoteIndex))
            {
                index = SkipLiteral(source, quoteIndex);
                continue;
            }

            if (IsIdentifierStart(source[index])
                || (source[index] == '@'
                    && index + 1 < source.Length
                    && IsIdentifierStart(source[index + 1])))
            {
                var start = source[index] == '@' ? ++index : index;
                index++;
                while (index < source.Length && IsIdentifierPart(source[index]))
                {
                    index++;
                }

                tokens.Add(source[start..index]);
                continue;
            }

            if (source[index] == ':'
                && index + 1 < source.Length
                && source[index + 1] == ':')
            {
                tokens.Add("::");
                index += 2;
                continue;
            }

            tokens.Add(source[index].ToString());
            index++;
        }

        return tokens;
    }

    private static bool IsStringOrCharacterStart(
        string source,
        int index,
        out int quoteIndex)
    {
        quoteIndex = index;
        if (source[index] is '"' or '\'')
        {
            return true;
        }

        var candidate = index;
        while (candidate < source.Length
               && source[candidate] is '$' or '@')
        {
            candidate++;
        }

        if (candidate < source.Length && source[candidate] == '"')
        {
            quoteIndex = candidate;
            return true;
        }

        return false;
    }

    private static int SkipLiteral(string source, int quoteIndex)
    {
        var quote = source[quoteIndex];
        var rawQuoteCount = 1;
        while (quote == '"'
               && quoteIndex + rawQuoteCount < source.Length
               && source[quoteIndex + rawQuoteCount] == '"')
        {
            rawQuoteCount++;
        }

        if (rawQuoteCount < 3)
        {
            rawQuoteCount = 1;
        }

        var verbatim = quote == '"'
            && quoteIndex > 0
            && source[..quoteIndex].TakeLast(2).Contains('@');
        var index = quoteIndex + rawQuoteCount;
        while (index < source.Length)
        {
            if (rawQuoteCount >= 3
                && index + rawQuoteCount <= source.Length
                && source.AsSpan(index, rawQuoteCount).SequenceEqual(
                    source.AsSpan(quoteIndex, rawQuoteCount)))
            {
                return index + rawQuoteCount;
            }

            if (rawQuoteCount == 1 && source[index] == quote)
            {
                if (verbatim
                    && quote == '"'
                    && index + 1 < source.Length
                    && source[index + 1] == '"')
                {
                    index += 2;
                    continue;
                }

                return index + 1;
            }

            if (!verbatim
                && rawQuoteCount == 1
                && source[index] == '\\'
                && index + 1 < source.Length)
            {
                index += 2;
                continue;
            }

            index++;
        }

        return source.Length;
    }

    private static bool IsIdentifier(string token) =>
        token.Length > 0
        && IsIdentifierStart(token[0])
        && token.Skip(1).All(IsIdentifierPart);

    private static bool IsIdentifierStart(char value) =>
        value == '_' || char.IsLetter(value);

    private static bool IsIdentifierPart(char value) =>
        value == '_' || char.IsLetterOrDigit(value);
}
