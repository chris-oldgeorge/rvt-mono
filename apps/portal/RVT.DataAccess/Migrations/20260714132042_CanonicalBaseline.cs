using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RVT.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class CanonicalBaseline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "company",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_company", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "help_section",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    slug = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_published = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_help_section", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "monitor",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    fleet_nr = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    serial_id = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    manufacturer = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    model = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    firmware_version = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    type_of_monitor = table.Column<int>(type: "integer", nullable: false),
                    calibration_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    calibration_due = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    location_id = table.Column<int>(type: "integer", nullable: true),
                    latitude = table.Column<double>(type: "double precision", maxLength: 128, nullable: true),
                    longitude = table.Column<double>(type: "double precision", nullable: true),
                    location_address = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    time_zone = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    customer_id = table.Column<int>(type: "integer", nullable: true),
                    customer_display_name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    listed_at_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    archived = table.Column<bool>(type: "boolean", nullable: false),
                    archived_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    archived_by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    archive_reason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    last_data_time_1_min = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_data_time_15_min = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_data_time_1_hour = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_data_time_24_hour = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_monitor", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "notification_setting",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    site_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<bool>(type: "boolean", nullable: false),
                    sms = table.Column<bool>(type: "boolean", nullable: false),
                    start_time = table.Column<TimeSpan>(type: "interval", nullable: true),
                    end_time = table.Column<TimeSpan>(type: "interval", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notification_setting", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "site",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    site_name = table.Column<string>(type: "text", nullable: false),
                    create_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    address_line_1 = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    address_line_2 = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    postcode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    city = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    county = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    start_time = table.Column<TimeSpan>(type: "interval", nullable: true),
                    end_time = table.Column<TimeSpan>(type: "interval", nullable: true),
                    sat_start_time = table.Column<TimeSpan>(type: "interval", nullable: true),
                    sat_end_time = table.Column<TimeSpan>(type: "interval", nullable: true),
                    sun_start_time = table.Column<TimeSpan>(type: "interval", nullable: true),
                    sun_end_time = table.Column<TimeSpan>(type: "interval", nullable: true),
                    archived = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_site", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "help_article",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    help_section_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    slug = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    summary = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    body = table.Column<string>(type: "text", nullable: false),
                    content_type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    is_published = table.Column<bool>(type: "boolean", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_help_article", x => x.id);
                    table.ForeignKey(
                        name: "fk_help_article_help_section_id",
                        column: x => x.help_section_id,
                        principalTable: "help_section",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "notification",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    notification_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    limit_on = table.Column<double>(type: "double precision", nullable: false),
                    averaging_period = table.Column<int>(type: "integer", nullable: false),
                    level = table.Column<double>(type: "double precision", nullable: false),
                    closed_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    closed_by_user = table.Column<Guid>(type: "uuid", nullable: true),
                    closed_note = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    monitor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    alert_field = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    alert_type = table.Column<int>(type: "integer", nullable: false),
                    recording_link = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notification", x => x.id);
                    table.ForeignKey(
                        name: "fk_notification_monitor_id",
                        column: x => x.monitor_id,
                        principalTable: "monitor",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "rvt_alert_rule",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    serial_id = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    alert_field = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    limit_on = table.Column<double>(type: "double precision", nullable: false),
                    limit_off = table.Column<double>(type: "double precision", nullable: false),
                    alert_type = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    averaging_period = table.Column<int>(type: "integer", nullable: false),
                    weekdays = table.Column<bool>(type: "boolean", nullable: false),
                    saturdays = table.Column<bool>(type: "boolean", nullable: false),
                    sundays = table.Column<bool>(type: "boolean", nullable: false),
                    start_time = table.Column<TimeSpan>(type: "interval", nullable: true),
                    end_time = table.Column<TimeSpan>(type: "interval", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    monitor_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rvt_alert_rule", x => x.id);
                    table.ForeignKey(
                        name: "fk_rvt_alert_rule_monitor_id",
                        column: x => x.monitor_id,
                        principalTable: "monitor",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "contract",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    contract_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    on_hire_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    off_hire_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    site_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_contract", x => x.id);
                    table.ForeignKey(
                        name: "fk_contract_company_id",
                        column: x => x.company_id,
                        principalTable: "company",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_contract_site_id",
                        column: x => x.site_id,
                        principalTable: "site",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "site_archived",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: false),
                    create_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    picture_link = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    site_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_site_archived", x => x.id);
                    table.ForeignKey(
                        name: "fk_site_archived_site_id",
                        column: x => x.site_id,
                        principalTable: "site",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "site_operating_hour",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    site_id = table.Column<Guid>(type: "uuid", nullable: false),
                    day_of_week = table.Column<int>(type: "integer", nullable: false),
                    start_time = table.Column<TimeSpan>(type: "interval", nullable: true),
                    end_time = table.Column<TimeSpan>(type: "interval", nullable: true),
                    is_closed = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_site_operating_hour", x => x.id);
                    table.ForeignKey(
                        name: "fk_site_operating_hour_site_id",
                        column: x => x.site_id,
                        principalTable: "site",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "site_user",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    start_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    end_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    site_id = table.Column<Guid>(type: "uuid", nullable: false),
                    site_contact = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_site_user", x => x.id);
                    table.ForeignKey(
                        name: "fk_site_user_site_id",
                        column: x => x.site_id,
                        principalTable: "site",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "help_asset",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    help_article_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    asset_type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    url = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    internal_path = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_help_asset", x => x.id);
                    table.ForeignKey(
                        name: "fk_help_asset_help_article_id",
                        column: x => x.help_article_id,
                        principalTable: "help_article",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "deployment",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    start_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    end_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    lng = table.Column<double>(type: "double precision", nullable: false),
                    lat = table.Column<double>(type: "double precision", nullable: false),
                    location = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    what_3_words = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    picture_link = table.Column<string>(type: "text", nullable: true),
                    contract_id = table.Column<Guid>(type: "uuid", nullable: false),
                    monitor_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_deployment", x => x.id);
                    table.ForeignKey(
                        name: "fk_deployment_contract_id",
                        column: x => x.contract_id,
                        principalTable: "contract",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_deployment_monitor_id",
                        column: x => x.monitor_id,
                        principalTable: "monitor",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_contract_company_id",
                table: "contract",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "ix_contract_site_id",
                table: "contract",
                column: "site_id");

            migrationBuilder.CreateIndex(
                name: "ix_deployment_contract_id",
                table: "deployment",
                column: "contract_id");

            migrationBuilder.CreateIndex(
                name: "ix_deployment_monitor_id",
                table: "deployment",
                column: "monitor_id");

            migrationBuilder.CreateIndex(
                name: "ix_help_article_help_section_id",
                table: "help_article",
                column: "help_section_id");

            migrationBuilder.CreateIndex(
                name: "ix_help_article_slug",
                table: "help_article",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_help_asset_help_article_id",
                table: "help_asset",
                column: "help_article_id");

            migrationBuilder.CreateIndex(
                name: "ix_help_section_slug",
                table: "help_section",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_notification_monitor_id",
                table: "notification",
                column: "monitor_id");

            migrationBuilder.CreateIndex(
                name: "ix_rvt_alert_rule_monitor_id",
                table: "rvt_alert_rule",
                column: "monitor_id");

            migrationBuilder.CreateIndex(
                name: "ix_site_archived_site_id",
                table: "site_archived",
                column: "site_id");

            migrationBuilder.CreateIndex(
                name: "ix_site_operating_hour_site_id_day_of_week",
                table: "site_operating_hour",
                columns: new[] { "site_id", "day_of_week" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_site_user_site_id",
                table: "site_user",
                column: "site_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "deployment");

            migrationBuilder.DropTable(
                name: "help_asset");

            migrationBuilder.DropTable(
                name: "notification");

            migrationBuilder.DropTable(
                name: "notification_setting");

            migrationBuilder.DropTable(
                name: "rvt_alert_rule");

            migrationBuilder.DropTable(
                name: "site_archived");

            migrationBuilder.DropTable(
                name: "site_operating_hour");

            migrationBuilder.DropTable(
                name: "site_user");

            migrationBuilder.DropTable(
                name: "contract");

            migrationBuilder.DropTable(
                name: "help_article");

            migrationBuilder.DropTable(
                name: "monitor");

            migrationBuilder.DropTable(
                name: "company");

            migrationBuilder.DropTable(
                name: "site");

            migrationBuilder.DropTable(
                name: "help_section");
        }
    }
}
