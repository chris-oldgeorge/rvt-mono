// File summary: Guards database cutover artifacts that still carry production migration risk.
// - 2026-07-17 pending Replaced runtime regex compilation with a generated regex for warning-free verification.
// - 2026-07-14 pending Repointed post-load guardrails at database/postgres/post-load after retiring RVT.DatabaseMigrator.
// Major updates:
// - 2026-07-09 pending Added guardrails for canonical PostgreSQL routine aliases and Timescale index cleanup.
// - 2026-07-05 pending Retired historical docs/workflow/Sonar receipt checks from the runtime test suite.
// - 2026-06-09 pending Renamed data-access namespaces and repository types to RVT.DataAccess/Repository.
// - 2026-06-08 pending Added canonical PostgreSQL view deployment guardrails for the post-load phase.
// - 2026-06-09 pending Added user action routine id-generation guardrail after Timescale rehearsal.
// - 2026-06-09 pending Added PostgreSQL view statement terminator guardrail after Timescale timing rehearsal.
// - 2026-06-09 pending Added Identity-to-canonical UUID join guardrail for PostgreSQL post-load views.
// - 2026-06-09 pending Added legacy compatibility schema guardrails for DBR cutover.
// - 2026-06-09 pending Added raw SQL and migration canonical naming guardrails.
// - 2026-06-09 pending Made SQL comment stripping newline-agnostic for Windows and macOS verification.
// - 2026-06-09 pending Added canonical-era EF migration identifier parsing before database migration execution.
// - 2026-06-09 pending Added canonical EF baseline and snapshot guardrails before future migration scaffolding.

using System.Text.RegularExpressions;
using RVT.DataAccess.Configuration;

namespace RvtPortal.Spa.Tests;

public partial class CutoverReadinessTests
{
    [GeneratedRegex(
        """\b(from|join|update|insert\s+into|delete\s+from)\s+(\[legacy\]|legacy)(\.|\")|\[legacy\]\.""",
        RegexOptions.IgnoreCase)]
    private static partial Regex LegacySchemaPattern();

    [Fact]
    // Function summary: Verifies post-load scripts use canonical names for Timescale tables and setup hooks.
    public void PostLoadScripts_UseCanonicalNames()
    {
        string root = FindRepositoryRoot();
        string postLoadDirectory = Path.Combine(root, "database", "postgres", "post-load");
        string postLoadSql = string.Join(
            Environment.NewLine,
            Directory.GetFiles(postLoadDirectory, "*.sql").Select(File.ReadAllText));
        string[] retiredNames = new[]
        {
            "AirQNoiseLevels",
            "SvantekNoiseLevels",
            "OmnidotsPeakLevels",
            "MyAtmDustLevels",
            "UserActionsHistory",
            "NotificationsSent",
            "SiteAverages",
            "Timestamtp"
        };

        foreach (string? retiredName in retiredNames)
        {
            Assert.DoesNotContain(retiredName, postLoadSql, StringComparison.Ordinal);
        }

        Assert.Contains("air_q_noise_level", postLoadSql, StringComparison.Ordinal);
        Assert.Contains("site_average", postLoadSql, StringComparison.Ordinal);
        Assert.Contains("logged_at", postLoadSql, StringComparison.Ordinal);
    }

    [Fact]
    // Function summary: Verifies the migrator deploys canonical PostgreSQL views instead of a placeholder hook.
    public void PostLoadScripts_DeployCanonicalViews()
    {
        string root = FindRepositoryRoot();
        string postLoadSql = File.ReadAllText(Path.Combine(root, "database", "postgres", "post-load", "03_views_and_routines.sql"));
        int viewCount = postLoadSql.Split("CREATE OR REPLACE VIEW public.", StringSplitOptions.None).Length - 1;
        string[] requiredViews = new[]
        {
            "admin_dashboard_data",
            "monitor_search",
            "site_search",
            "report_search",
            "users_for_site_search",
            "omnidots_peak_level_15_min",
            "noise_level_site_avg"
        };

        // 38 since monitor_measurement_removal_impact was added: script 03 drops it via CASCADE, so it has
        // to recreate it too or every migrator run silently destroys it.
        Assert.Equal(38, viewCount);
        Assert.DoesNotContain("deployment hook executed", postLoadSql, StringComparison.OrdinalIgnoreCase);

        foreach (string? viewName in requiredViews)
        {
            Assert.Contains($"CREATE OR REPLACE VIEW public.{viewName}", postLoadSql, StringComparison.Ordinal);
        }
    }

    [Fact]
    // Function summary: Verifies canonical PostgreSQL view SQL has no active retired database dialect dialect leftovers.
    public void PostLoadViews_AvoidRetiredDialect()
    {
        string root = FindRepositoryRoot();
        string postLoadSql = StripSqlComments(File.ReadAllText(Path.Combine(root, "database", "postgres", "post-load", "03_views_and_routines.sql")));
        string[] retiredDialectTokens = new[]
        {
            "[dbo]",
            "dbo.",
            "DATEADD(",
            "DATEDIFF(",
            "GETDATE()",
            "GETUTCDATE()",
            "ISNULL(",
            "PIVOT",
            "TOP 1",
            " AS bit"
        };

        foreach (string? token in retiredDialectTokens)
        {
            Assert.DoesNotContain(token, postLoadSql, StringComparison.OrdinalIgnoreCase);
        }

        Assert.Contains("FILTER (WHERE a.field = 'LAeq')", postLoadSql, StringComparison.Ordinal);
        Assert.Contains("date_trunc('hour', sample_time)", postLoadSql, StringComparison.Ordinal);
        Assert.Contains("concat_ws(' '", postLoadSql, StringComparison.Ordinal);
    }

    [Fact]
    // Function summary: Verifies each canonical PostgreSQL view statement terminates before the next view deployment block.
    public void PostLoadViews_TerminateBeforeNextDrop()
    {
        string root = FindRepositoryRoot();
        string postLoadSql = StripSqlComments(File.ReadAllText(Path.Combine(root, "database", "postgres", "post-load", "03_views_and_routines.sql")));
        IEnumerable<string> viewBlocks = postLoadSql.Split("CREATE OR REPLACE VIEW public.", StringSplitOptions.None).Skip(1);

        foreach (string? viewBlock in viewBlocks)
        {
            string viewName = viewBlock.Split(new[] { " AS", "\r\n", "\n" }, StringSplitOptions.None)[0].Trim();
            int nextDropIndex = viewBlock.IndexOf("DROP VIEW IF EXISTS public.", StringComparison.Ordinal);
            int terminatorIndex = viewBlock.IndexOf(';');

            if (nextDropIndex >= 0)
            {
                Assert.True(
                    terminatorIndex >= 0 && terminatorIndex < nextDropIndex,
                    $"Expected view {viewName} to end with a semicolon before the next DROP VIEW block.");
            }
            else
            {
                Assert.True(
                    terminatorIndex >= 0,
                    $"Expected view {viewName} to end with a semicolon.");
            }
        }
    }

    [Fact]
    // Function summary: Verifies PostgreSQL views preserve ASP.NET Identity physical names while using canonical app names.
    public void PostLoadViews_PreserveIdentityNames()
    {
        string root = FindRepositoryRoot();
        string postLoadSql = File.ReadAllText(Path.Combine(root, "database", "postgres", "post-load", "03_views_and_routines.sql"));

        Assert.Contains("public.\"AspNetUsers\"", postLoadSql, StringComparison.Ordinal);
        Assert.Contains("public.\"AspNetUserRoles\"", postLoadSql, StringComparison.Ordinal);
        Assert.Contains("public.\"AspNetRoles\"", postLoadSql, StringComparison.Ordinal);
        Assert.Contains("U.\"CompanyId\"", postLoadSql, StringComparison.Ordinal);
        Assert.Contains("public.company", postLoadSql, StringComparison.Ordinal);
        Assert.Contains("public.site_user", postLoadSql, StringComparison.Ordinal);
        Assert.DoesNotContain("public.aspnet", postLoadSql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    // Function summary: Verifies PostgreSQL views cast ASP.NET Identity string ids before joining canonical UUID user columns.
    public void PostLoadViews_CastIdentityIdsForCanonicalUserJoins()
    {
        string root = FindRepositoryRoot();
        string postLoadSql = StripSqlComments(File.ReadAllText(Path.Combine(root, "database", "postgres", "post-load", "03_views_and_routines.sql")));

        Assert.DoesNotContain("U.\"Id\"=SU.user_id", postLoadSql, StringComparison.Ordinal);
        Assert.DoesNotContain("U.\"Id\"=SU2.user_id", postLoadSql, StringComparison.Ordinal);
        Assert.DoesNotContain("U.\"Id\" = SU.user_id", postLoadSql, StringComparison.Ordinal);
        Assert.DoesNotContain("U.\"Id\" = SU2.user_id", postLoadSql, StringComparison.Ordinal);
        Assert.DoesNotContain("U.\"Id\" = RU.user_id", postLoadSql, StringComparison.Ordinal);
        Assert.DoesNotContain("U.email", postLoadSql, StringComparison.Ordinal);
        Assert.Contains("U.\"Id\"::uuid=SU.user_id", postLoadSql, StringComparison.Ordinal);
        Assert.Contains("U.\"Id\"::uuid=SU2.user_id", postLoadSql, StringComparison.Ordinal);
        Assert.Contains("U.\"Id\"::uuid = SU.user_id", postLoadSql, StringComparison.Ordinal);
        Assert.Contains("U.\"Id\"::uuid = SU2.user_id", postLoadSql, StringComparison.Ordinal);
        Assert.Contains("U.\"Id\"::uuid = RU.user_id", postLoadSql, StringComparison.Ordinal);
        Assert.Contains("U.\"Email\"", postLoadSql, StringComparison.Ordinal);
    }

    [Fact]
    // Function summary: Verifies exported retired database dialect routines have canonical PostgreSQL post-load definitions.
    public void PostLoadScripts_DeployCanonicalRoutines()
    {
        string root = FindRepositoryRoot();
        string routineScriptPath = Path.Combine(root, "database", "postgres", "post-load", "04_routines.sql");
        Assert.True(File.Exists(routineScriptPath), $"Missing PostgreSQL routine post-load script: {routineScriptPath}");

        string routineSql = StripSqlComments(File.ReadAllText(routineScriptPath));
        string[] expectedRoutines = new[]
        {
            "error_insert",
            "monitor_status_for_month",
            "monitor_status_time_check",
            "peak_record_breach_and_alerts",
            "user_actions_history_insert"
        };
        string[] retiredDialectTokens = new[]
        {
            "[dbo]",
            "dbo.",
            "GETUTCDATE()",
            "GETDATE()",
            "ISNULL(",
            "DATEADD(",
            "DATEDIFF("
        };

        foreach (string? routineName in expectedRoutines)
        {
            Assert.Contains($"public.{routineName}", routineSql, StringComparison.Ordinal);
        }

        string[] legacyRoutineNames = new[]
        {
            "\"MonitorStatusForMonth\"",
            "\"MonitorStatusTimeCheck\"",
            "\"PeakRecordBreachAndAlerts\""
        };

        foreach (string? legacyRoutineName in legacyRoutineNames)
        {
            Assert.Contains($"DROP FUNCTION IF EXISTS public.{legacyRoutineName}", routineSql, StringComparison.Ordinal);
        }

        Assert.Contains("monitor_date timestamp without time zone", routineSql, StringComparison.Ordinal);
        Assert.Contains("utc_date timestamp with time zone", routineSql, StringComparison.Ordinal);
        Assert.Contains("serial_id text", routineSql, StringComparison.Ordinal);
        Assert.Contains("fleet_nr text", routineSql, StringComparison.Ordinal);
        Assert.Contains("notification_id uuid", routineSql, StringComparison.Ordinal);
        Assert.DoesNotContain("RETURNS TABLE(\"", routineSql, StringComparison.Ordinal);

        foreach (string? token in retiredDialectTokens)
        {
            Assert.DoesNotContain(token, routineSql, StringComparison.OrdinalIgnoreCase);
        }

        Assert.Contains("INSERT INTO public.user_action_history", routineSql, StringComparison.Ordinal);
        Assert.Contains("(id, user_name, controller, controller_action, parameters, form_data, recorded_at)", routineSql, StringComparison.Ordinal);
        Assert.Contains("gen_random_uuid()", routineSql, StringComparison.Ordinal);
    }

    [Fact]
    // Function summary: Verifies post-load scripts rename Timescale-created indexes into the canonical ix_ style.
    public void PostLoadScripts_RenameTimescaleIndexes()
    {
        string root = FindRepositoryRoot();
        string indexScriptPath = Path.Combine(root, "database", "postgres", "post-load", "05_index_naming_cleanup.sql");
        Assert.True(File.Exists(indexScriptPath), $"Missing PostgreSQL index cleanup post-load script: {indexScriptPath}");

        string indexSql = StripSqlComments(File.ReadAllText(indexScriptPath));
        Dictionary<string, string> expectedIndexPairs = new(StringComparer.Ordinal)
        {
            ["heater_reading_sample_time_idx"] = "ix_heater_reading_sample_time",
            ["my_atm_accessory_info_sample_time_idx"] = "ix_my_atm_accessory_info_sample_time",
            ["omnidots_vdv_level_sample_time_idx"] = "ix_omnidots_vdv_level_sample_time",
            ["omnidots_veff_level_sample_time_idx"] = "ix_omnidots_veff_level_sample_time"
        };

        foreach (KeyValuePair<string, string> pair in expectedIndexPairs)
        {
            Assert.Contains($"to_regclass('public.{pair.Key}')", indexSql, StringComparison.Ordinal);
            Assert.Contains($"RENAME TO {pair.Value}", indexSql, StringComparison.Ordinal);
        }
    }

    [Fact]
    // Function summary: Verifies application code does not start depending on the temporary legacy compatibility schema.
    public void ApplicationCode_DoesNotReferenceLegacyCompatibilitySchema()
    {
        string root = FindRepositoryRoot();
        HashSet<string> scannedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".cs",
            ".ts",
            ".tsx",
            ".sql",
            ".json"
        };
        string[] excludedSegments = new[]
        {
            $"{Path.DirectorySeparatorChar}.git{Path.DirectorySeparatorChar}",
            $"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
            $"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
            $"{Path.DirectorySeparatorChar}database{Path.DirectorySeparatorChar}",
            $"{Path.DirectorySeparatorChar}docs{Path.DirectorySeparatorChar}",
            $"{Path.DirectorySeparatorChar}RvtPortal.Spa.Tests{Path.DirectorySeparatorChar}",
            $"{Path.DirectorySeparatorChar}TestResults{Path.DirectorySeparatorChar}",
            $"{Path.DirectorySeparatorChar}node_modules{Path.DirectorySeparatorChar}"
        };
        string[] hits = [.. Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Where(path => scannedExtensions.Contains(Path.GetExtension(path)))
            .Where(path => !excludedSegments.Any(segment => path.Contains(segment, StringComparison.OrdinalIgnoreCase)))
            .SelectMany(path =>
            {
                string content = File.ReadAllText(path);
                return LegacySchemaPattern().Matches(content)
                    .Select(match => $"{Path.GetRelativePath(root, path)} contains {match.Value}");
            })];

        Assert.Empty(hits);
    }

    [Fact]
    // Function summary: Verifies new application-owned migrations use canonical physical names outside ASP.NET Identity.
    public void ApplicationMigrations_DoNotIntroduceLegacyApplicationOwnedNames()
    {
        string root = FindRepositoryRoot();
        string migrationDirectory = Path.Combine(root, "RVT.DataAccess", "Migrations");
        HashSet<string> allowedFiles = new(StringComparer.OrdinalIgnoreCase)
        {
            "RVTDbContextModelSnapshot.cs"
        };

        // A migration's .Designer.cs is a generated model snapshot, not DDL. Like RVTDbContextModelSnapshot.cs it
        // records CLR property names ("Archived"), which say nothing about the physical schema. The rule this test
        // enforces - no retired physical names in migration DDL - applies to the migration body only.
        static bool IsGeneratedModelSnapshot(string path) =>
            Path.GetFileName(path).EndsWith(".Designer.cs", StringComparison.OrdinalIgnoreCase);
        string[] retiredTokens = new[]
        {
            "MonitorsList",
            "\"Archived\"",
            "\"ArchivedAt\"",
            "\"ArchivedBy\"",
            "\"ArchiveReason\"",
            "[SiteOperatingHours]",
            "\"SiteOperatingHours\"",
            "[HelpSections]",
            "\"HelpSections\"",
            "[HelpArticles]",
            "\"HelpArticles\"",
            "[HelpAssets]",
            "\"HelpAssets\"",
            "[Sites]",
            "\"Sites\"",
            "[StartTime]",
            "\"StartTime\"",
            "[SatStartTime]",
            "\"SatStartTime\"",
            "[SunStartTime]",
            "\"SunStartTime\"",
            "PK_SiteOperatingHours",
            "FK_SiteOperatingHours_Sites_SiteId",
            "IX_SiteOperatingHours_SiteId_DayOfWeek",
            "PK_HelpSections",
            "PK_HelpArticles",
            "FK_HelpArticles_HelpSections_SectionId",
            "PK_HelpAssets",
            "FK_HelpAssets_HelpArticles_HelpArticleId"
        };

        string[] hits = [.. Directory.EnumerateFiles(migrationDirectory, "*.cs", SearchOption.TopDirectoryOnly)
            .Where(path => !allowedFiles.Contains(Path.GetFileName(path)))
            .Where(path => !IsGeneratedModelSnapshot(path))
            .Where(path => string.CompareOrdinal(Path.GetFileName(path), "20260608") >= 0)
            .SelectMany(path =>
            {
                string source = File.ReadAllText(path);
                return retiredTokens
                    .Where(token => source.Contains(token, StringComparison.Ordinal))
                    .Select(token => $"{Path.GetRelativePath(root, path)} contains {token}");
            })];

        Assert.Empty(hits);
    }

    [Fact]
    // Function summary: Verifies the active EF baseline and model snapshot no longer describe the retired legacy schema.
    public void EfBaselineAndSnapshot_DoNotReferenceRetiredPhysicalNames()
    {
        string root = FindRepositoryRoot();
        string migrationDirectory = Path.Combine(root, "RVT.DataAccess", "Migrations");
        // Resolved rather than hard-coded: the migration chain was squashed onto a generated baseline, and
        // naming the old files here would silently stop inspecting anything.
        string[] inspectedFiles = [.. Directory
            .EnumerateFiles(migrationDirectory, "*.cs", SearchOption.TopDirectoryOnly)
            .Where(path => !Path.GetFileName(path).StartsWith("._", StringComparison.Ordinal))
            .OrderBy(path => path, StringComparer.Ordinal)];

        Assert.NotEmpty(inspectedFiles);
        string[] retiredTokens = new[]
        {
            "name: \"RvtAlertRules\"",
            "name: \"Companies\"",
            "name: \"Contracts\"",
            "name: \"Deployments\"",
            "name: \"MonitorsList\"",
            "name: \"Notifications\"",
            "name: \"NotificationSettings\"",
            "name: \"NotificationsSent\"",
            "name: \"Sites\"",
            "name: \"SiteUsers\"",
            "table: \"RvtAlertRules\"",
            "table: \"Companies\"",
            "table: \"Contracts\"",
            "table: \"Deployments\"",
            "table: \"MonitorsList\"",
            "table: \"Notifications\"",
            "table: \"NotificationSettings\"",
            "table: \"NotificationsSent\"",
            "table: \"Sites\"",
            "table: \"SiteUsers\"",
            "principalTable: \"Companies\"",
            "principalTable: \"Contracts\"",
            "principalTable: \"MonitorsList\"",
            "principalTable: \"Sites\"",
            "b.ToTable(\"RvtAlertRules\"",
            "b.ToTable(\"Companies\"",
            "b.ToTable(\"Contracts\"",
            "b.ToTable(\"Deployments\"",
            "b.ToTable(\"MonitorsList\"",
            "b.ToTable(\"Notifications\"",
            "b.ToTable(\"NotificationSettings\"",
            "b.ToTable(\"NotificationsSent\"",
            "b.ToTable(\"Sites\"",
            "b.ToTable(\"SiteUsers\""
        };

        string[] hits = [.. inspectedFiles
            .SelectMany(path =>
            {
                string source = File.ReadAllText(path);
                return retiredTokens
                    .Where(token => source.Contains(token, StringComparison.Ordinal))
                    .Select(token => $"{Path.GetRelativePath(root, path)} contains {token}");
            })];

        Assert.Empty(hits);
    }

    [Fact]
    // Function summary: Verifies migrations added after the canonical baseline do not drop objects the baseline owns.
    public void PostCanonicalBaselineMigrations_DoNotDropBaselineObjects()
    {
        string root = FindRepositoryRoot();
        string migrationDirectory = Path.Combine(root, "RVT.DataAccess", "Migrations");

        string[] migrations = [.. Directory.EnumerateFiles(migrationDirectory, "*_*.cs", SearchOption.TopDirectoryOnly)
            .Where(path => !Path.GetFileName(path).EndsWith(".Designer.cs", StringComparison.OrdinalIgnoreCase))
            // macOS AppleDouble sidecars ("._Name.cs") match the glob and sort ahead of the real files.
            .Where(path => !Path.GetFileName(path).StartsWith("._", StringComparison.Ordinal))
            .OrderBy(path => Path.GetFileName(path), StringComparer.Ordinal)];

        // The chain is squashed onto a generated baseline, so the baseline is the earliest migration and it
        // legitimately creates everything. The rule applies to whatever comes after it.
        Assert.NotEmpty(migrations);
        string baseline = migrations[0];
        Assert.EndsWith("_CanonicalBaseline.cs", baseline, StringComparison.Ordinal);

        // The baseline itself is exempt: its Down() necessarily drops everything its Up() creates.
        string[] afterBaseline = [.. migrations.Skip(1)];

        string[] destructiveTokens = new[]
        {
            "DropTable(",
            "DropColumn(",
            "DROP COLUMN",
            "DROP TABLE"
        };
        string[] hits = [.. afterBaseline
            .SelectMany(path =>
            {
                string source = File.ReadAllText(path);
                return destructiveTokens
                    .Where(token => source.Contains(token, StringComparison.OrdinalIgnoreCase))
                    .Select(token => $"{Path.GetRelativePath(root, path)} contains {token}");
            })];

        Assert.Empty(hits);
    }

    [Fact]
    // Function summary: Verifies canonical-era EF migrations create or alter only canonical application-owned physical identifiers.
    public void CanonicalEraEfMigrations_UseCanonicalPhysicalIdentifiers()
    {
        string root = FindRepositoryRoot();
        string migrationDirectory = Path.Combine(root, "RVT.DataAccess", "Migrations");
        string[] migrationFiles = [.. Directory.EnumerateFiles(migrationDirectory, "*.cs", SearchOption.TopDirectoryOnly)
            .Where(path => !Path.GetFileName(path).EndsWith(".Designer.cs", StringComparison.OrdinalIgnoreCase))
            .Where(path => !Path.GetFileName(path).Equals("RVTDbContextModelSnapshot.cs", StringComparison.OrdinalIgnoreCase))
            .Where(path => string.CompareOrdinal(Path.GetFileName(path), "20260608") >= 0)];

        Assert.NotEmpty(migrationFiles);

        string[] violations = [.. migrationFiles.SelectMany(path => FindCanonicalMigrationIdentifierViolations(root, path))];

        Assert.Empty(violations);
    }

    // Function summary: Parses canonical-era EF migration source for database object names that would be created or altered.
    private static IEnumerable<string> FindCanonicalMigrationIdentifierViolations(string root, string path)
    {
        string source = File.ReadAllText(path);
        string relativePath = Path.GetRelativePath(root, path);
        List<string> violations = new();

        foreach (string identifier in ExtractRegexMatches(source, @"\b(?:table|principalTable):\s*""([^""]+)"""))
        {
            if (!DatabaseNamingRules.IsCanonicalRelationName(identifier))
            {
                violations.Add($"{relativePath} relation '{identifier}' is not canonical");
            }
        }

        foreach (string identifier in ExtractRegexMatches(source, @"\b(?:name|column|principalColumn):\s*""([^""]+)"""))
        {
            if (!DatabaseNamingRules.IsCanonicalColumnName(identifier))
            {
                violations.Add($"{relativePath} column '{identifier}' is not canonical");
            }
        }

        foreach (string? identifier in ExtractRegexMatches(source, @"\[(?<identifier>[A-Za-z_][A-Za-z0-9_]*)\]", "identifier")
            .Concat(ExtractRegexMatches(source, @"\bpublic\.(?<identifier>[A-Za-z_][A-Za-z0-9_]*)\b", "identifier"))
            .Concat(ExtractRegexMatches(source, @"\bCONSTRAINT\s+(?<identifier>[A-Za-z_][A-Za-z0-9_]*)\b", "identifier"))
            .Concat(ExtractRegexMatches(source, @"\bINDEX\s+(?:IF\s+NOT\s+EXISTS\s+)?(?<identifier>[A-Za-z_][A-Za-z0-9_]*)\b", "identifier")))
        {
            if (!DatabaseNamingRules.IsCanonicalIdentifier(identifier))
            {
                violations.Add($"{relativePath} SQL identifier '{identifier}' is not canonical");
            }
        }

        return violations.Distinct(StringComparer.Ordinal);
    }

    // Function summary: Extracts regex capture values for migration identifier validation.
    private static IEnumerable<string> ExtractRegexMatches(string source, string pattern, string groupName = "1")
    {
        return Regex.Matches(source, pattern)
            .Select(match => match.Groups[groupName].Value)
            .Where(value => !string.IsNullOrWhiteSpace(value));
    }

    // Function summary: Handles the find repository root workflow for this module.
    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "RvtPortal.Spa.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find repository root from test output directory.");
    }

    // Function summary: Removes line comments so dialect guardrails focus on executable SQL.
    private static string StripSqlComments(string sql)
    {
        return string.Join(
            Environment.NewLine,
            sql.Split(["\r\n", "\n"], StringSplitOptions.None)
                .Where(line => !line.TrimStart().StartsWith("--", StringComparison.Ordinal)));
    }

}
