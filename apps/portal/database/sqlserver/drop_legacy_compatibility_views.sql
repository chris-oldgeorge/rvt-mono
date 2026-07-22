-- File summary: Removes the legacy old-name compatibility schema and its views (SQL Server).
-- Major updates:
-- - 2026-07-14 pending Added to retire the compatibility layer created for the canonical naming cutover.
--
-- The compatibility layer was a temporary read-only mirror of the pre-cutover relation and column names, held in
-- a [legacy] schema so external/reporting consumers could keep querying the old names while they migrated. Every
-- view in that schema is dropped here, along with the schema itself.
--
-- BEFORE RUNNING THIS, confirm no consumer still reads the old names. Nothing in this repository does - a
-- guardrail test (CutoverReadinessTests.ApplicationCode_DoesNotReferenceLegacyCompatibilitySchema) enforces
-- that - but the layer existed for consumers OUTSIDE it, and this repository cannot see them. Reporting tools,
-- BI dashboards, and hand-written queries are the ones to check.
--
-- To undo: the CREATE script is in git history (database/sqlserver/legacy_compatibility_views.sql, removed
-- 2026-07-14); restore it from there and re-run it. The views are derived entirely from the canonical tables,
-- so nothing is lost by dropping them and nothing is recovered by keeping them.
--
-- Idempotent: safe to re-run.

DECLARE @drop NVARCHAR(MAX) = N'';

SELECT @drop = @drop + N'DROP VIEW [legacy].' + QUOTENAME(v.name) + N';' + CHAR(10)
FROM sys.views AS v
WHERE v.schema_id = SCHEMA_ID(N'legacy');

IF @drop <> N''
    EXEC sp_executesql @drop;
GO

IF SCHEMA_ID(N'legacy') IS NOT NULL
    EXEC(N'DROP SCHEMA [legacy]');
GO
