using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RVT.DataAccess.Migrations.Search
{
    /// <inheritdoc />
    public partial class SearchBaseline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "my_atm_dust_level",
                columns: table => new
                {
                    serial_id = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    avrg = table.Column<int>(type: "integer", nullable: true),
                    sample_time = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    pm_1 = table.Column<double>(type: "double precision", nullable: true),
                    pm_2_5 = table.Column<double>(type: "double precision", nullable: true),
                    pm_10 = table.Column<double>(type: "double precision", nullable: true),
                    pm_total = table.Column<double>(type: "double precision", nullable: true),
                    weather_t = table.Column<double>(type: "double precision", nullable: true),
                    weather_p = table.Column<double>(type: "double precision", nullable: true),
                    weather_rh = table.Column<double>(type: "double precision", nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "omnidots_monitor_status",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    serial_id = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    measurement_duration = table.Column<int>(type: "integer", nullable: true),
                    data_save_level = table.Column<double>(type: "double precision", nullable: true),
                    vdv_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    vdv_x = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    vdv_y = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    vdv_z = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    vdv_period = table.Column<int>(type: "integer", nullable: true),
                    trace_save_level = table.Column<double>(type: "double precision", nullable: true),
                    trace_pre_trigger = table.Column<double>(type: "double precision", nullable: true),
                    trace_post_trigger = table.Column<double>(type: "double precision", nullable: true),
                    alarm_value = table.Column<double>(type: "double precision", nullable: true),
                    flat_level = table.Column<double>(type: "double precision", nullable: true),
                    disable_led = table.Column<bool>(type: "boolean", nullable: false),
                    log_flush_interval = table.Column<int>(type: "integer", nullable: false),
                    guide_line = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    building_level = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    vector_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    vtop_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    atop_enabled = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_omnidots_monitor_status", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "omnidots_peak_level",
                columns: table => new
                {
                    serial_id = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    sample_time = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    x_fdom = table.Column<double>(type: "double precision", nullable: true),
                    x_vtop = table.Column<double>(type: "double precision", nullable: true),
                    x_vtop_overflow = table.Column<double>(type: "double precision", nullable: true),
                    y_fdom = table.Column<double>(type: "double precision", nullable: true),
                    y_vtop = table.Column<double>(type: "double precision", nullable: true),
                    y_vtop_overflow = table.Column<double>(type: "double precision", nullable: true),
                    z_fdom = table.Column<double>(type: "double precision", nullable: true),
                    z_vtop = table.Column<double>(type: "double precision", nullable: true),
                    z_vtop_overflow = table.Column<double>(type: "double precision", nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "omnidots_sensor",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    serial_id = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    name = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    lastseen = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    battery_charge = table.Column<int>(type: "integer", nullable: false),
                    connected_using = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    online = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_omnidots_sensor", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "omnidots_trace_index",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    serial_id = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    start_time = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    end_time = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_omnidots_trace_index", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "report_rule",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    site_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    frequency = table.Column<int>(type: "integer", nullable: false),
                    last_generated = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    report_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    day_of_week = table.Column<int>(type: "integer", nullable: true),
                    day_of_month = table.Column<int>(type: "integer", nullable: true),
                    deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_report_rule", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "report_user",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    report_rule_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_report_user", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "svantek_monitor_status",
                columns: table => new
                {
                    project_id = table.Column<int>(type: "integer", nullable: false),
                    point_id = table.Column<int>(type: "integer", nullable: false),
                    serial_id = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    error_count = table.Column<int>(type: "integer", nullable: false),
                    type = table.Column<string>(type: "character varying(7)", unicode: false, maxLength: 7, nullable: true),
                    active = table.Column<string>(type: "character varying(4)", unicode: false, maxLength: 4, nullable: true),
                    lastlogin = table.Column<string>(type: "character varying(19)", unicode: false, maxLength: 19, nullable: true),
                    lastlogout = table.Column<string>(type: "character varying(19)", unicode: false, maxLength: 19, nullable: true),
                    isonline = table.Column<string>(type: "character varying(5)", unicode: false, maxLength: 5, nullable: true),
                    meterfirmware = table.Column<decimal>(type: "numeric(4,2)", nullable: true),
                    laststatustimestamp = table.Column<string>(type: "character varying(19)", unicode: false, maxLength: 19, nullable: true),
                    batterycharge = table.Column<int>(type: "integer", nullable: true),
                    batterytimetoempty = table.Column<int>(type: "integer", nullable: true),
                    powersource = table.Column<string>(type: "character varying(15)", unicode: false, maxLength: 15, nullable: true),
                    isbatterycharging = table.Column<string>(type: "character varying(5)", unicode: false, maxLength: 5, nullable: true),
                    gsmsignalquality = table.Column<int>(type: "integer", nullable: true),
                    measurementstate = table.Column<string>(type: "character varying(7)", unicode: false, maxLength: 7, nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "omnidots_trace",
                columns: table => new
                {
                    omnidots_trace_index_id = table.Column<Guid>(type: "uuid", nullable: true),
                    x = table.Column<double>(type: "double precision", nullable: true),
                    y = table.Column<double>(type: "double precision", nullable: true),
                    z = table.Column<double>(type: "double precision", nullable: true)
                },
                constraints: table =>
                {
                    table.ForeignKey(
                        name: "fk_omnidots_trace_omnidots_trace_index_id",
                        column: x => x.omnidots_trace_index_id,
                        principalTable: "omnidots_trace_index",
                        principalColumn: "id");
                });

            migrationBuilder.CreateIndex(
                name: "ix_omnidots_trace_omnidots_trace_index_id",
                table: "omnidots_trace",
                column: "omnidots_trace_index_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "my_atm_dust_level");

            migrationBuilder.DropTable(
                name: "omnidots_monitor_status");

            migrationBuilder.DropTable(
                name: "omnidots_peak_level");

            migrationBuilder.DropTable(
                name: "omnidots_sensor");

            migrationBuilder.DropTable(
                name: "omnidots_trace");

            migrationBuilder.DropTable(
                name: "report_rule");

            migrationBuilder.DropTable(
                name: "report_user");

            migrationBuilder.DropTable(
                name: "svantek_monitor_status");

            migrationBuilder.DropTable(
                name: "omnidots_trace_index");
        }
    }
}
