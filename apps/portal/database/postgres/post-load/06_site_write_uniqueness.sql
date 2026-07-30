-- Serialize the duplicate check with site archive/notification writes. ScriptRunner
-- sends this entire file as one PostgreSQL command, so the locks and index repairs
-- commit atomically. Existing request traffic cannot recreate a duplicate between
-- the guard and unique-index creation.
--
-- EF migration 20260723234806_EnforceSiteWriteUniqueness creates the same two unique
-- indexes, and the deploy runs after the migrations, so on any database built the
-- supported way this script has nothing to do. It used to drop and recreate both
-- indexes anyway - an ACCESS EXCLUSIVE rebuild per deploy, plus a SHARE ROW EXCLUSIVE
-- lock on three tables held until commit, for no change. Both are now taken only when
-- an index is actually missing or not unique; otherwise this file is a catalog read.

DO $$
DECLARE
    archive_ready boolean;
    notification_ready boolean;
BEGIN
    SELECT EXISTS
    (
        SELECT 1
        FROM pg_index AS indexes
        JOIN pg_class AS index_class ON index_class.oid = indexes.indexrelid
        JOIN pg_attribute AS key_column
          ON key_column.attrelid = indexes.indrelid
         AND key_column.attnum = indexes.indkey[0]
        WHERE indexes.indrelid = 'public.site_archived'::regclass
          AND index_class.relname = 'ix_site_archived_site_id'
          AND indexes.indisunique
          AND indexes.indnkeyatts = 1
          AND key_column.attname = 'site_id'
    ) INTO archive_ready;

    SELECT EXISTS
    (
        SELECT 1
        FROM pg_index AS indexes
        JOIN pg_class AS index_class ON index_class.oid = indexes.indexrelid
        JOIN pg_attribute AS key_column
          ON key_column.attrelid = indexes.indrelid
         AND key_column.attnum = indexes.indkey[0]
        WHERE indexes.indrelid = 'public.notification_setting'::regclass
          AND index_class.relname = 'ix_notification_setting_site_user_id'
          AND indexes.indisunique
          AND indexes.indnkeyatts = 1
          AND key_column.attname = 'site_user_id'
    ) INTO notification_ready;

    IF archive_ready AND notification_ready THEN
        RAISE NOTICE 'Site write uniqueness already enforced; no lock and no index rebuild.';
        RETURN;
    END IF;

    -- Only now is the lock worth taking: without it a concurrent write could insert a
    -- duplicate between this guard and the unique index created below.
    EXECUTE 'LOCK TABLE public.notification_setting, public.site_archived, public.site '
            'IN SHARE ROW EXCLUSIVE MODE';

    IF EXISTS
    (
        SELECT 1
        FROM public.notification_setting
        GROUP BY site_user_id
        HAVING COUNT(*) > 1
    )
    OR EXISTS
    (
        SELECT 1
        FROM public.site_archived
        GROUP BY site_id
        HAVING COUNT(*) > 1
    )
    THEN
        RAISE EXCEPTION
            'Cannot enforce site write uniqueness while duplicate owner rows exist.'
            USING HINT =
                'Apply EF migration 20260723234806_EnforceSiteWriteUniqueness '
                'or resolve duplicates manually, then rerun RVT.SchemaDeploy.';
    END IF;
END $$;

-- Replace only the legacy lookup indexes, and only where the intended unique index is
-- not already in place. Clean data is required because this rerunnable deployment
-- script never repairs or removes table rows.
DO $$
BEGIN
    IF NOT EXISTS
    (
        SELECT 1
        FROM pg_index AS indexes
        JOIN pg_class AS index_class ON index_class.oid = indexes.indexrelid
        JOIN pg_attribute AS key_column
          ON key_column.attrelid = indexes.indrelid
         AND key_column.attnum = indexes.indkey[0]
        WHERE indexes.indrelid = 'public.site_archived'::regclass
          AND index_class.relname = 'ix_site_archived_site_id'
          AND indexes.indisunique
          AND indexes.indnkeyatts = 1
          AND key_column.attname = 'site_id'
    )
    THEN
        EXECUTE 'DROP INDEX IF EXISTS public.ix_site_archived_site_id';
        EXECUTE 'CREATE UNIQUE INDEX ix_site_archived_site_id '
                'ON public.site_archived (site_id)';
    END IF;

    IF NOT EXISTS
    (
        SELECT 1
        FROM pg_index AS indexes
        JOIN pg_class AS index_class ON index_class.oid = indexes.indexrelid
        JOIN pg_attribute AS key_column
          ON key_column.attrelid = indexes.indrelid
         AND key_column.attnum = indexes.indkey[0]
        WHERE indexes.indrelid = 'public.notification_setting'::regclass
          AND index_class.relname = 'ix_notification_setting_site_user_id'
          AND indexes.indisunique
          AND indexes.indnkeyatts = 1
          AND key_column.attname = 'site_user_id'
    )
    THEN
        EXECUTE 'DROP INDEX IF EXISTS public.ix_notification_setting_site_user_id';
        EXECUTE 'CREATE UNIQUE INDEX ix_notification_setting_site_user_id '
                'ON public.notification_setting (site_user_id)';
    END IF;
END $$;
