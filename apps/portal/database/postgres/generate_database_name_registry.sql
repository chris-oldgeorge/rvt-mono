-- Generates the database naming registry used by the canonical naming refactor
-- and the SQL Server-to-Postgres migrator update.
--
-- Output inside the container:
--   /tmp/database-name-registry.csv

CREATE OR REPLACE FUNCTION pg_temp.rvt_snake_case(identifier text)
RETURNS text
LANGUAGE sql
IMMUTABLE
AS $$
    SELECT trim(both '_' from lower(
        regexp_replace(
            regexp_replace(
                regexp_replace(
                    regexp_replace(
                        regexp_replace(identifier, '([A-Z]+)([A-Z][a-z])', '\1_\2', 'g'),
                        '([a-z0-9])([A-Z])', '\1_\2', 'g'),
                    '([A-Za-z])([0-9])', '\1_\2', 'g'),
                '([0-9])([A-Za-z])', '\1_\2', 'g'),
            '[^A-Za-z0-9]+', '_', 'g')))
$$;

CREATE OR REPLACE FUNCTION pg_temp.rvt_canonical_relation(identifier text)
RETURNS text
LANGUAGE plpgsql
IMMUTABLE
AS $$
DECLARE
    value text := pg_temp.rvt_snake_case(identifier);
BEGIN
    value := replace(value, 'sitei_d', 'site_id');
    value := replace(value, 'monitors_list', 'monitor');
    value := replace(value, 'reports_', 'report_');
    value := replace(value, 'notifications_', 'notification_');
    value := replace(value, '_error_messages', '_error_message');
    value := replace(value, '_noise_levels', '_noise_level');
    value := replace(value, '_peak_levels', '_peak_level');
    value := replace(value, '_vdv_levels', '_vdv_level');
    value := replace(value, '_veff_levels', '_veff_level');
    value := replace(value, '_dust_levels', '_dust_level');
    value := replace(value, '_traces_', '_trace_');
    value := replace(value, '_users', '_user');
    value := replace(value, '_roles', '_role');
    value := replace(value, '_claims', '_claim');
    value := replace(value, '_logins', '_login');
    value := replace(value, '_tokens', '_token');
    value := replace(value, '_sections', '_section');
    value := replace(value, '_articles', '_article');
    value := replace(value, '_assets', '_asset');
    value := replace(value, '_contracts', '_contract');
    value := replace(value, '_deployments', '_deployment');
    value := replace(value, '_companies', '_company');
    value := replace(value, '_sites', '_site');
    value := replace(value, '_averages', '_average');
    value := replace(value, '_hours', '_hour');
    value := replace(value, '_settings', '_setting');
    value := replace(value, '_rules', '_rule');
    value := replace(value, '_sensors', '_sensor');
    value := replace(value, '_actions_', '_action_');
    value := replace(value, '_history', '_history');

    IF value LIKE '%ies' THEN
        value := regexp_replace(value, 'ies$', 'y');
    ELSIF value LIKE '%s' AND value NOT LIKE '%status' AND value NOT LIKE '%class' THEN
        value := regexp_replace(value, 's$', '');
    END IF;

    RETURN value;
END;
$$;

CREATE OR REPLACE FUNCTION pg_temp.rvt_canonical_column(identifier text, is_single_pk boolean DEFAULT false)
RETURNS text
LANGUAGE plpgsql
IMMUTABLE
AS $$
DECLARE
    value text := pg_temp.rvt_snake_case(identifier);
BEGIN
    IF is_single_pk THEN
        RETURN 'id';
    END IF;

    value := replace(value, 'sitei_d', 'site_id');
    value := replace(value, 'timestamtp', 'logged_at');
    value := replace(value, 'nr_users', 'user_count');
    value := replace(value, 'nr', 'row_count');
    value := replace(value, 'l_aeq', 'laeq');
    value := replace(value, 'l_amax', 'lamax');
    value := replace(value, 'l_a_90', 'la90');
    value := replace(value, 'l_a_10', 'la10');
    value := replace(value, 'l_ceq', 'lceq');
    value := replace(value, 'l_cmax', 'lcmax');
    value := replace(value, 'l_c_90', 'lc90');
    value := replace(value, 'l_c_10', 'lc10');
    value := replace(value, 'p_m_10', 'pm10');
    value := replace(value, 'p_m_2_5', 'pm2_5');
    value := replace(value, 't_mio', 'tmio');
    value := replace(value, 'p_mio', 'pmio');

    IF value = 'timestamp' THEN
        value := 'recorded_at';
    ELSIF value = 'text' THEN
        value := 'content';
    ELSIF value = 'date' THEN
        value := 'event_date';
    END IF;

    RETURN value;
END;
$$;

COPY (
    WITH relations AS (
        SELECT
            t.table_schema AS current_schema,
            t.table_name AS current_relation,
            CASE
                WHEN t.table_type = 'BASE TABLE' THEN 'table'
                WHEN t.table_type = 'VIEW' THEN 'view'
                ELSE lower(t.table_type)
            END AS object_type
        FROM information_schema.tables t
        WHERE t.table_schema = 'public'
        UNION ALL
        SELECT schemaname, matviewname, 'materialized_view'
        FROM pg_matviews
        WHERE schemaname = 'public'
    ),
    pk_columns AS (
        SELECT
            tc.table_schema,
            tc.table_name,
            kcu.column_name,
            count(*) OVER (PARTITION BY tc.table_schema, tc.table_name, tc.constraint_name) = 1 AS is_single_pk
        FROM information_schema.table_constraints tc
        JOIN information_schema.key_column_usage kcu
            ON kcu.constraint_schema = tc.constraint_schema
           AND kcu.constraint_name = tc.constraint_name
        WHERE tc.table_schema = 'public'
          AND tc.constraint_type = 'PRIMARY KEY'
    ),
    fk_columns AS (
        SELECT
            tc.table_schema,
            tc.table_name,
            kcu.column_name,
            ccu.table_name AS referenced_relation,
            ccu.column_name AS referenced_column
        FROM information_schema.table_constraints tc
        JOIN information_schema.key_column_usage kcu
            ON kcu.constraint_schema = tc.constraint_schema
           AND kcu.constraint_name = tc.constraint_name
        JOIN information_schema.constraint_column_usage ccu
            ON ccu.constraint_schema = tc.constraint_schema
           AND ccu.constraint_name = tc.constraint_name
        WHERE tc.table_schema = 'public'
          AND tc.constraint_type = 'FOREIGN KEY'
    ),
    rows AS (
        SELECT
            'postgres' AS provider,
            r.object_type,
            r.current_schema,
            r.current_relation,
            NULL::text AS current_column,
            NULL::text AS current_data_type,
            'public' AS new_schema,
            pg_temp.rvt_canonical_relation(r.current_relation) AS new_relation,
            NULL::text AS new_column,
            NULL::text AS new_data_type,
            CASE
                WHEN r.current_relation = pg_temp.rvt_canonical_relation(r.current_relation) THEN 'compliant'
                ELSE 'rename_relation'
            END AS change_type,
            CASE
                WHEN r.current_relation <> lower(r.current_relation) THEN 'relation_not_lowercase'
                WHEN r.current_relation <> pg_temp.rvt_canonical_relation(r.current_relation) THEN 'relation_not_canonical'
                ELSE ''
            END AS notes
        FROM relations r

        UNION ALL

        SELECT
            'postgres' AS provider,
            r.object_type || '_column' AS object_type,
            c.table_schema AS current_schema,
            c.table_name AS current_relation,
            c.column_name AS current_column,
            c.data_type AS current_data_type,
            'public' AS new_schema,
            pg_temp.rvt_canonical_relation(c.table_name) AS new_relation,
            CASE
                WHEN fk.referenced_relation IS NOT NULL THEN
                    pg_temp.rvt_canonical_relation(fk.referenced_relation) || '_' || pg_temp.rvt_canonical_column(fk.referenced_column, true)
                ELSE
                    pg_temp.rvt_canonical_column(c.column_name, coalesce(pk.is_single_pk, false))
            END AS new_column,
            c.data_type AS new_data_type,
            CASE
                WHEN c.column_name = CASE
                    WHEN fk.referenced_relation IS NOT NULL THEN
                        pg_temp.rvt_canonical_relation(fk.referenced_relation) || '_' || pg_temp.rvt_canonical_column(fk.referenced_column, true)
                    ELSE
                        pg_temp.rvt_canonical_column(c.column_name, coalesce(pk.is_single_pk, false))
                END THEN 'compliant'
                ELSE 'rename_column'
            END AS change_type,
            concat_ws(';',
                CASE WHEN c.column_name <> lower(c.column_name) THEN 'column_not_lowercase' END,
                CASE WHEN pk.is_single_pk THEN 'single_column_pk' END,
                CASE WHEN fk.referenced_relation IS NOT NULL THEN 'fk_to_' || pg_temp.rvt_canonical_relation(fk.referenced_relation) END,
                CASE WHEN c.column_name IN ('Timestamtp') THEN 'legacy_misspelling' END,
                CASE WHEN pg_temp.rvt_canonical_column(c.column_name, coalesce(pk.is_single_pk, false)) IN ('recorded_at', 'content', 'event_date') THEN 'data_type_name_renamed' END
            ) AS notes
        FROM information_schema.columns c
        JOIN relations r
            ON r.current_schema = c.table_schema
           AND r.current_relation = c.table_name
        LEFT JOIN pk_columns pk
            ON pk.table_schema = c.table_schema
           AND pk.table_name = c.table_name
           AND pk.column_name = c.column_name
        LEFT JOIN fk_columns fk
            ON fk.table_schema = c.table_schema
           AND fk.table_name = c.table_name
           AND fk.column_name = c.column_name
        WHERE c.table_schema = 'public'
    )
    SELECT
        provider,
        object_type,
        current_schema,
        current_relation,
        current_column,
        current_data_type,
        new_schema,
        new_relation,
        new_column,
        new_data_type,
        change_type,
        notes
    FROM rows
    ORDER BY current_relation, current_column NULLS FIRST
) TO '/tmp/database-name-registry.csv' WITH CSV HEADER;
