-- Read-only Help asset URL cutover check.
-- Execute this script against every release database targeted by the deployment.
-- Release requires zero returned rows from every execution. Record the database/environment,
-- UTC execution time, script revision, and row count in the release evidence.
-- This script does not mutate data.
SELECT id, help_article_id, url
FROM public.help_asset
WHERE
    url IS NULL
    OR length(url) > 512
    OR url ~ '[[:cntrl:]\\]'
    OR url ~ '[[:space:]]'
    OR (
        url NOT LIKE '/help-assets/%'
        AND url !~ '^https://[^/@[:space:]]+(?:/|$)'
    )
ORDER BY help_article_id, id;
