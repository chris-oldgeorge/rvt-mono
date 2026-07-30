-- Idempotent PK adjustments for hypertable candidates whose source PK does
-- not include the time column. TimescaleDB requires the time column to be
-- part of every unique index on a hypertable.
-- Uses canonical target names. ASP.NET Identity tables are not adjusted.
--
-- Idempotent has to mean no-op, not "same end state". Rebuilding a primary key
-- takes ACCESS EXCLUSIVE on the table and rewrites its index; on error_log and
-- notification_sent that is hundreds of thousands of rows, and the deploy
-- transaction then holds those locks through every later script - blocking the
-- monitors' error-write path for the rest of the deploy. So a table is touched
-- only when its primary key is not already the intended constraint.

-- SET LOCAL, not SET: the deploy runs every script on one connection inside one
-- transaction, so a bare SET here would outlive this script and the deploy.
SET LOCAL search_path TO public;

DO $$
DECLARE
    pk_name TEXT;
    pk_columns TEXT[];
    target_name TEXT;
    target_columns TEXT[];
    adjustments TEXT[][] := ARRAY[
        ['error_log',           'id', 'logged_at'],
        ['notification_sent',   'id', 'send_time'],
        ['site_average',        'id', 'collection_time'],
        ['user_action_history', 'id', 'recorded_at']
    ];
BEGIN
    FOR i IN 1 .. array_length(adjustments, 1) LOOP
        target_name := 'pk_' || adjustments[i][1];
        target_columns := ARRAY[adjustments[i][2], adjustments[i][3]];

        SELECT constraints.conname,
               ARRAY(
                   SELECT attributes.attname
                   FROM unnest(constraints.conkey) WITH ORDINALITY AS key_column(attnum, ord)
                   JOIN pg_attribute AS attributes
                     ON attributes.attrelid = constraints.conrelid
                    AND attributes.attnum = key_column.attnum
                   ORDER BY key_column.ord
               )
          INTO pk_name, pk_columns
        FROM pg_constraint AS constraints
        WHERE constraints.conrelid = format('public.%I', adjustments[i][1])::regclass
          AND constraints.contype = 'p';

        IF pk_name = target_name AND pk_columns = target_columns THEN
            RAISE NOTICE 'PK already correct, not rebuilt: public.% (%, %)',
                adjustments[i][1], adjustments[i][2], adjustments[i][3];
            CONTINUE;
        END IF;

        IF pk_name IS NOT NULL THEN
            EXECUTE format('ALTER TABLE public.%I DROP CONSTRAINT %I',
                           adjustments[i][1], pk_name);
        END IF;

        EXECUTE format(
            'ALTER TABLE public.%I ADD CONSTRAINT %I PRIMARY KEY (%I, %I)',
            adjustments[i][1],
            target_name,
            adjustments[i][2],
            adjustments[i][3]
        );

        RAISE NOTICE 'PK adjusted: public.% (%, %)',
            adjustments[i][1], adjustments[i][2], adjustments[i][3];
    END LOOP;
END $$;
