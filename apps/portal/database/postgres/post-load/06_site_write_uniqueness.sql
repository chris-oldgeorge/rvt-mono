-- Serialize the duplicate check with site archive/notification writes. ScriptRunner
-- sends this entire file as one PostgreSQL command, so the locks and index repairs
-- commit atomically. Existing request traffic cannot recreate a duplicate between
-- the guard and unique-index creation.
LOCK TABLE public.notification_setting, public.site_archived, public.site
IN SHARE ROW EXCLUSIVE MODE;

DO $$
BEGIN
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

-- Replace only the legacy lookup indexes. Clean data is required because this
-- rerunnable deployment script never repairs or removes table rows.
DROP INDEX IF EXISTS public.ix_site_archived_site_id;
DROP INDEX IF EXISTS public.ix_notification_setting_site_user_id;

CREATE UNIQUE INDEX ix_site_archived_site_id
ON public.site_archived (site_id);

CREATE UNIQUE INDEX ix_notification_setting_site_user_id
ON public.notification_setting (site_user_id);
