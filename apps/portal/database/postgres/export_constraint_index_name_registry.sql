COPY (
    WITH constraint_columns AS (
        SELECT
            con.oid AS object_id,
            string_agg(att.attname, '|' ORDER BY key_columns.ordinality) AS current_columns
        FROM pg_constraint con
        JOIN LATERAL unnest(con.conkey) WITH ORDINALITY AS key_columns(attnum, ordinality) ON true
        JOIN pg_attribute att
            ON att.attrelid = con.conrelid
           AND att.attnum = key_columns.attnum
        GROUP BY con.oid
    ),
    referenced_columns AS (
        SELECT
            con.oid AS object_id,
            ref_rel.relname AS referenced_relation,
            string_agg(ref_att.attname, '|' ORDER BY key_columns.ordinality) AS referenced_columns
        FROM pg_constraint con
        JOIN pg_class ref_rel ON ref_rel.oid = con.confrelid
        JOIN LATERAL unnest(con.confkey) WITH ORDINALITY AS key_columns(attnum, ordinality) ON true
        JOIN pg_attribute ref_att
            ON ref_att.attrelid = con.confrelid
           AND ref_att.attnum = key_columns.attnum
        WHERE con.contype = 'f'
        GROUP BY con.oid, ref_rel.relname
    ),
    constraints AS (
        SELECT
            'postgres' AS provider,
            CASE con.contype
                WHEN 'p' THEN 'primary_key'
                WHEN 'f' THEN 'foreign_key'
                WHEN 'u' THEN 'unique_constraint'
                WHEN 'c' THEN 'check_constraint'
                ELSE 'constraint'
            END AS object_type,
            ns.nspname AS current_schema,
            rel.relname AS current_relation,
            con.conname AS current_object,
            coalesce(cc.current_columns, '') AS current_columns,
            coalesce(rc.referenced_relation, '') AS referenced_relation,
            coalesce(rc.referenced_columns, '') AS referenced_columns
        FROM pg_constraint con
        JOIN pg_class rel ON rel.oid = con.conrelid
        JOIN pg_namespace ns ON ns.oid = rel.relnamespace
        LEFT JOIN constraint_columns cc ON cc.object_id = con.oid
        LEFT JOIN referenced_columns rc ON rc.object_id = con.oid
        WHERE ns.nspname = 'public'
    ),
    index_columns AS (
        SELECT
            idx.indexrelid AS object_id,
            string_agg(att.attname, '|' ORDER BY key_columns.ordinality) AS current_columns
        FROM pg_index idx
        JOIN LATERAL unnest(idx.indkey) WITH ORDINALITY AS key_columns(attnum, ordinality) ON key_columns.attnum > 0
        JOIN pg_attribute att
            ON att.attrelid = idx.indrelid
           AND att.attnum = key_columns.attnum
        GROUP BY idx.indexrelid
    ),
    indexes AS (
        SELECT
            'postgres' AS provider,
            'index' AS object_type,
            ns.nspname AS current_schema,
            rel.relname AS current_relation,
            index_rel.relname AS current_object,
            coalesce(ic.current_columns, '') AS current_columns,
            '' AS referenced_relation,
            '' AS referenced_columns
        FROM pg_index idx
        JOIN pg_class rel ON rel.oid = idx.indrelid
        JOIN pg_namespace ns ON ns.oid = rel.relnamespace
        JOIN pg_class index_rel ON index_rel.oid = idx.indexrelid
        LEFT JOIN index_columns ic ON ic.object_id = idx.indexrelid
        WHERE ns.nspname = 'public'
          AND NOT EXISTS (
              SELECT 1
              FROM pg_constraint con
              WHERE con.conindid = idx.indexrelid
          )
    )
    SELECT *
    FROM constraints
    UNION ALL
    SELECT *
    FROM indexes
    ORDER BY current_relation, object_type, current_object
) TO '/tmp/postgres-constraint-index-source.csv' WITH CSV HEADER;
