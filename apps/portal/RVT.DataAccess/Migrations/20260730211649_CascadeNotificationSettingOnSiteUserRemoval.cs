using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RVT.DataAccess.Migrations;

/// <summary>
/// Gives <c>notification_setting</c> the relationship it always had in meaning and never had in the schema.
/// Assignment created one row per site assignment; all three assignment-removal paths deleted only the
/// <c>site_user</c> row, and with no foreign key nothing cascaded and nothing errored, so settings accumulated
/// keyed to ids that no longer existed.
/// <para>
/// Safe against a populated database. The pre-clean deletes exactly the rows the constraint would reject and
/// touches nothing else; both relations hold about one row per site assignment (thousands, not millions), so
/// the <c>ACCESS EXCLUSIVE</c> lock the constraint takes on <c>notification_setting</c> and its validating scan
/// are brief. EF runs the whole migration in one transaction, so a failure leaves the schema untouched.
/// </para>
/// <para>
/// The constraint is dropped first under both names it could already carry. A database built from these
/// migrations has no such constraint, but one imported from the SQL Server source snapshot carries
/// <c>FK_NotificationSettings_SiteUsers</c>, renamed to <c>fk_notification_setting_site_user_id</c> by
/// <c>canonical_constraint_index_naming.sql</c> - in both cases without <c>ON DELETE CASCADE</c>. Dropping
/// first makes this migration correct against every shape that can exist rather than only the EF-built one.
/// </para>
/// <para>
/// Relations are unqualified so they resolve through the deploy connection's <c>search_path</c>, matching the
/// unqualified DDL the <see cref="MigrationBuilder"/> call below emits.
/// </para>
/// </summary>
public partial class CascadeNotificationSettingOnSiteUserRemoval : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            ALTER TABLE notification_setting
                DROP CONSTRAINT IF EXISTS fk_notification_setting_site_user_id;
            ALTER TABLE notification_setting
                DROP CONSTRAINT IF EXISTS "FK_NotificationSettings_SiteUsers";

            DELETE FROM notification_setting AS setting
            WHERE NOT EXISTS
            (
                SELECT 1
                FROM site_user AS assignment
                WHERE assignment.id = setting.site_user_id
            );
            """);

        migrationBuilder.AddForeignKey(
            name: "fk_notification_setting_site_user_id",
            table: "notification_setting",
            column: "site_user_id",
            principalTable: "site_user",
            principalColumn: "id",
            onDelete: ReferentialAction.Cascade);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "fk_notification_setting_site_user_id",
            table: "notification_setting");
    }
}
