-- Idempotent PK adjustments for hypertable candidates whose source PK does
-- not include the time column. TimescaleDB requires the time column to be
-- part of every unique index on a hypertable.
-- Uses canonical target names. ASP.NET Identity tables are not adjusted.

SET search_path TO public;

DO $$
DECLARE
    pk_name TEXT;
    adjustments TEXT[][] := ARRAY[
        ['error_log',           'id', 'logged_at'],
        ['notification_sent',   'id', 'send_time'],
        ['site_average',        'id', 'collection_time'],
        ['user_action_history', 'id', 'recorded_at']
    ];
BEGIN
    FOR i IN 1 .. array_length(adjustments, 1) LOOP
        SELECT conname INTO pk_name
        FROM pg_constraint
        WHERE conrelid = format('public.%I', adjustments[i][1])::regclass
          AND contype = 'p';

        IF pk_name IS NOT NULL THEN
            EXECUTE format('ALTER TABLE public.%I DROP CONSTRAINT %I',
                           adjustments[i][1], pk_name);
        END IF;

        EXECUTE format(
            'ALTER TABLE public.%I ADD CONSTRAINT %I PRIMARY KEY (%I, %I)',
            adjustments[i][1],
            'pk_' || adjustments[i][1],
            adjustments[i][2],
            adjustments[i][3]
        );

        RAISE NOTICE 'PK adjusted: public.% (%, %)',
            adjustments[i][1], adjustments[i][2], adjustments[i][3];
    END LOOP;
END $$;
