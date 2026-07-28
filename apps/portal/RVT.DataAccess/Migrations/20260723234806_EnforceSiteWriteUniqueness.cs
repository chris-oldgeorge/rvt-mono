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
