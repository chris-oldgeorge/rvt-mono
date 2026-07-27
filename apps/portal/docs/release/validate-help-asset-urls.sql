-- Read-only Help asset URL cutover check.
-- Release requires this query to return zero rows. This script does not mutate data.
SELECT id, help_article_id, url
FROM public.help_asset
WHERE
    url IS NULL
    OR length(url) > 512
    OR url ~ '[[:cntrl:]\\]'
    OR (
        url NOT LIKE '/help-assets/%'
        AND url !~ '^https://[^/@[:space:]]+(?:/|$)'
    )
ORDER BY help_article_id, id;
