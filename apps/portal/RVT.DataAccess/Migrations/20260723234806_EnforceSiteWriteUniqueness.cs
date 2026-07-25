using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RVT.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class EnforceSiteWriteUniqueness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            if (ActiveProvider.Contains(
                    "Npgsql",
                    StringComparison.Ordinal))
            {
                migrationBuilder.Sql(
                    """
                    LOCK TABLE public.notification_setting, public.site_archived, public.site
                    IN SHARE ROW EXCLUSIVE MODE;

                    WITH ranked_notification AS
                    (
                        SELECT
                            id,
                            ROW_NUMBER() OVER
                            (
                                PARTITION BY site_user_id
                                ORDER BY id ASC
                            ) AS row_number
                        FROM public.notification_setting
                    )
                    DELETE FROM public.notification_setting AS settings
                    USING ranked_notification AS ranked
                    WHERE settings.id = ranked.id
                      AND ranked.row_number > 1;

                    WITH ranked_archive AS
                    (
                        SELECT
                            id,
                            ROW_NUMBER() OVER
                            (
                                PARTITION BY site_id
                                ORDER BY create_date DESC, id DESC
                            ) AS row_number
                        FROM public.site_archived
                    )
                    DELETE FROM public.site_archived AS archive
                    USING ranked_archive AS ranked
                    WHERE archive.id = ranked.id
                      AND ranked.row_number > 1;

                    UPDATE public.site AS sites
                    SET archived = TRUE
                    WHERE EXISTS
                    (
                        SELECT 1
                        FROM public.site_archived AS archive
                        WHERE archive.site_id = sites.id
                    );
                    """);
            }
            else if (ActiveProvider.Contains(
                         "SqlServer",
                         StringComparison.Ordinal))
            {
                migrationBuilder.Sql(
                    """
                    SELECT TOP (1) 1
                    FROM [notification_setting] WITH (TABLOCKX, HOLDLOCK);
                    SELECT TOP (1) 1
                    FROM [site_archived] WITH (TABLOCKX, HOLDLOCK);
                    SELECT TOP (1) 1
                    FROM [site] WITH (TABLOCKX, HOLDLOCK);

                    WITH ranked_notification AS
                    (
                        SELECT
                            [id],
                            ROW_NUMBER() OVER
                            (
                                PARTITION BY [site_user_id]
                                ORDER BY [id] ASC
                            ) AS [row_number]
                        FROM [notification_setting]
                    )
                    DELETE FROM ranked_notification
                    WHERE [row_number] > 1;

                    WITH ranked_archive AS
                    (
                        SELECT
                            [id],
                            ROW_NUMBER() OVER
                            (
                                PARTITION BY [site_id]
                                ORDER BY [create_date] DESC, [id] DESC
                            ) AS [row_number]
                        FROM [site_archived]
                    )
                    DELETE FROM ranked_archive
                    WHERE [row_number] > 1;

                    UPDATE sites
                    SET [archived] = 1
                    FROM [site] AS sites
                    WHERE EXISTS
                    (
                        SELECT 1
                        FROM [site_archived] AS archive
                        WHERE archive.[site_id] = sites.[id]
                    );
                    """);
            }
            else if (ActiveProvider.Contains(
                         "Sqlite",
                         StringComparison.Ordinal))
            {
                migrationBuilder.Sql(
                    """
                    DELETE FROM notification_setting
                    WHERE id IN
                    (
                        SELECT id
                        FROM
                        (
                            SELECT
                                id,
                                ROW_NUMBER() OVER
                                (
                                    PARTITION BY site_user_id
                                    ORDER BY id ASC
                                ) AS row_number
                            FROM notification_setting
                        ) AS ranked_notification
                        WHERE row_number > 1
                    );

                    DELETE FROM site_archived
                    WHERE id IN
                    (
                        SELECT id
                        FROM
                        (
                            SELECT
                                id,
                                ROW_NUMBER() OVER
                                (
                                    PARTITION BY site_id
                                    ORDER BY create_date DESC, id DESC
                                ) AS row_number
                            FROM site_archived
                        ) AS ranked_archive
                        WHERE row_number > 1
                    );

                    UPDATE site
                    SET archived = TRUE
                    WHERE EXISTS
                    (
                        SELECT 1
                        FROM site_archived AS archive
                        WHERE archive.site_id = site.id
                    );
                    """);
            }
            else
            {
                throw new InvalidOperationException(
                    $"Provider '{ActiveProvider}' does not have a site-write uniqueness migration.");
            }

            migrationBuilder.DropIndex(
                name: "ix_site_archived_site_id",
                table: "site_archived");

            migrationBuilder.CreateIndex(
                name: "ix_site_archived_site_id",
                table: "site_archived",
                column: "site_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_notification_setting_site_user_id",
                table: "notification_setting",
                column: "site_user_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_site_archived_site_id",
                table: "site_archived");

            migrationBuilder.DropIndex(
                name: "ix_notification_setting_site_user_id",
                table: "notification_setting");

            migrationBuilder.CreateIndex(
                name: "ix_site_archived_site_id",
                table: "site_archived",
                column: "site_id");
        }
    }
}
