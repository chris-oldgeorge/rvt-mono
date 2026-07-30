using System.Data;
using System.Globalization;
using AirQ.Api.Db;
using AirQ.Model.Dto;
using AirQ.Model.Http;
using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;
using Rvt.Monitor.Common.Data;
using Rvt.Monitor.Common.Diagnostics;
using Rvt.Monitor.Common.Notifications;
using Rvt.Monitor.Common.Rules;
using Rvt.Monitor.Common.Utilities;
using Rvt.Monitor.IntegrationTesting;
namespace AirQMonitorTests
{

    // Summary: Exercises AirQ PostgreSQL database persistence against a scoped fixture.
    // Major updates:
    // - 2026-06-18 Test fixture hardening: aligned timestamp expectations with current DateTime persistence semantics.
    [TestClass]
    [TestCategory("PostgreSqlIntegration")]
    public class TestDBClient
    {

        private static PostgreSqlIntegrationDatabase? _database;

        private static DBClient? _testObj;

        public TestDBClient()
        {
            ILoggerFactory factory = LoggerFactory.Create(builder =>
            {
                builder.AddConsole().SetMinimumLevel(LogLevel.Debug);
            });
            RvtLogger.CreateLogger(factory, "TestDBClient");
        }

        [TestMethod]
        public void TestScopedPostgresConnectionUsesFixtureSchema()
        {
            using NpgsqlConnection connection = _database!.OpenConnection();
            connection.Open();
            using NpgsqlCommand command = new("SELECT current_schema();", connection);

            Assert.AreEqual(_database.SchemaName, command.ExecuteScalar());
        }

        [ClassInitialize]
        public static async Task TestFixtureSetup(TestContext context)
        {
            string setupSql = MonitorTestUtil.ReadTextFromFile("testdata/create.postgres.sql");
            string resetSql = MonitorTestUtil.ReadTextFromFile("testdata/reset.postgres.sql");
            _database = await PostgreSqlIntegrationDatabase.CreateAsync(setupSql, resetSql, context.CancellationToken);
            _testObj = new DBClient(_database.ConnectionString);
        }

        [ClassCleanup]
        public static async Task TestFixtureCleanup()
        {
            if (_database is not null)
            {
                await _database.DisposeAsync();
            }
        }

        [TestInitialize]
        public async Task BeforeTest()
        {
            await _database!.ResetAsync(
                MonitorTestUtil.ReadTextFromFile("testdata/reset.postgres.sql"),
                TestContext.CancellationToken);
        }

        [DataRow("", "", 5, 5)]
        [DataRow("2023-11-21T12:00:00Z", "2023-11-21T12:00:00Z", 5, 5)]
        [DataRow("2023-11-21T12:00:00Z", "2023-11-21T13:00:00Z", 5, 4)]
        [DataRow("2023-11-21T12:00:00Z", "2023-11-21T13:59:00Z", 5, 3)]
        [DataRow("2023-11-21T12:00:00Z", "2023-11-21T15:00:00Z", 5, 2)]
        [DataRow("2023-11-21T12:00:00Z", "2023-11-21T16:00:00Z", 5, 1)]
        [DataRow("2023-11-21T12:00:00Z", "2023-11-21T16:01:00Z", 5, 0)]
        [TestMethod]
        public void TestMonitorsList(string lastDate, string queryDate, int numMonitors, int numExpectedMonitors)

        {
            DateTime? lastDataTime = String.IsNullOrEmpty(lastDate) ? null : ParseUtc(lastDate);
            DateTime? queryLastdataTime = String.IsNullOrEmpty(queryDate) ? null : ParseUtc(queryDate);
            List<NoiseMonitorDto> monitorsIn = CreateMonitorsList(numMonitors);
            Assert.HasCount(numMonitors, monitorsIn);
            _testObj!.WriteMonitorList(monitorsIn);

            if (lastDataTime != null)
            {
                for (int i = 0; i < monitorsIn.Count; i++)
                {
                    DateTime dt = ((DateTime)lastDataTime!).AddHours(i);
                    _testObj.WriteLatestTimestamp(monitorsIn[i].SerialId, dt);
                }
            }

            List<NoiseMonitorDto> monitorsOut = _testObj.ReadMonitorList(queryLastdataTime);
            Assert.HasCount(numExpectedMonitors, monitorsOut);
        }

        [TestMethod]
        public void TestReadGlobalRules()
        {
            string connectionString = _database!.ConnectionString;
            using NpgsqlConnection connection = new(connectionString);
            connection.Open();

            List<RvtAlertRuleDto> rules = _testObj!.ReadRules(null);
            Assert.HasCount(1, rules);

            RvtAlertRuleDto rule = rules[0];

            Assert.IsNull(rule.SerialId);
            Assert.AreEqual(RuleConstants.OFFLINE_RULE, rule.Field);
            Assert.IsTrue(rule.IsActive);
            Assert.IsFalse(rule.IsDeleted);
            Assert.AreEqual(0, rule.LimitOn);
            Assert.AreEqual(0, rule.LimitOff);
            Assert.AreEqual(24 * 60 * 60, rule.AveragingPeriod);
            Assert.AreNotEqual(default, rule.Created);
            Assert.IsNull(rule.Accessed);
            Assert.IsNull(rule.RuleActiveTime.StartTime);
            Assert.IsNull(rule.RuleActiveTime.EndTime);
            Assert.IsTrue(rule.RuleActiveTime.Weekdays);
            Assert.IsTrue(rule.RuleActiveTime.Saturdays);
            Assert.IsTrue(rule.RuleActiveTime.Sundays);
        }

        [TestMethod]
        public void TestReadGlobalRulesExcludesDeletedRules()
        {
            string connectionString = _database!.ConnectionString;
            using NpgsqlConnection connection = new(connectionString);
            connection.Open();

            InsertDeletedGlobalOfflineRule(connection);

            List<RvtAlertRuleDto> rules = _testObj!.ReadRules(null);

            Assert.HasCount(1, rules);
            Assert.IsFalse(rules[0].IsDeleted);
        }

        [TestMethod]
        public void TestWriteLatestTimestamp()
        {
            List<NoiseMonitorDto> monitors = CreateMonitorsList(1, "E123");
            Assert.HasCount(1, monitors);

            _testObj!.WriteMonitorList(monitors);

            DateTime lastDataTime = ParseUtc("2023-10-18T14:35:42");
            string serialId = "E1230";
            _testObj.WriteLatestTimestamp(serialId, lastDataTime);

            monitors = _testObj.ReadMonitorList(null);
            Assert.HasCount(1, monitors);

            NoiseMonitorDto monitor = monitors[0];
            Assert.AreEqual(lastDataTime, monitor.LastDataTime);
        }


        [TestMethod]
        public void TestWriteCommonException()
        {
            string connectionString = _database!.ConnectionString;
            string TAG = "MyTestError";
            string MESSAGE = "bang";

            MonitorDb.WriteException(
                connectionString,
                TAG,
                AdapterException.Of(MESSAGE),
                "AirQMonitorTests",
                "1.0");

            using NpgsqlConnection connection = new(connectionString);
            connection.Open();

            string sql = "SELECT variables, message, logged_at FROM error_log;";
            using NpgsqlCommand cmd = new(sql, connection);
            using NpgsqlDataReader reader = cmd.ExecuteReader();

            int count = 0;
            while (reader.Read())
            {
                count++;
                string tag = reader.GetString(0);
                string error = reader.GetString(1);
                DateTime errorTime = reader.GetDateTime(2);
                Assert.AreEqual(TAG, tag);
                Assert.AreEqual(MESSAGE, error);
                Assert.IsTrue(errorTime <= DateTime.UtcNow);
            }
            Assert.AreEqual(1, count);
        }

        [TestMethod]
        [DataRow("2023-09-18T00:15:00.000+00:00", "2023-09-18T00:15:00")] // BST
        [DataRow("2023-10-30T12:30:00.000+00:00", "2023-10-30T12:30:00")] // GMT
        [DataRow("2023-03-20T07:30:00.000+00:00", "2023-03-20T07:30:00")] // BST
        public async Task TestInsertNoiseDto_DaylightSaving_Success(string actual, string expected)
        {
            DateTime actualDt = ParseUtc(actual);
            List<SampleResponse> samples = AirQFixture.SamplesResponseObjects(actualDt);
            string serialId = "E1234";
            _testObj!.InsertNoiseDtos(serialId, [new NoiseDto(samples[0])]);

            await using NpgsqlConnection connection = _database!.OpenConnection();
            await connection.OpenAsync(TestContext.CancellationToken);
            await using NpgsqlCommand command = new(
                "SELECT serial_id, sample_time, laeq, lamax, la_90, la_10, lceq, lcmax, lc_90, lc_10 " +
                "FROM air_q_noise_level ORDER BY sample_time;", connection);
            await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(TestContext.CancellationToken);
            Assert.IsTrue(await reader.ReadAsync(TestContext.CancellationToken));

            Assert.AreEqual(serialId, reader.GetString(0));
            DateTime expectedDt = ParseUtc(expected);
            Assert.AreEqual(expectedDt, reader.GetDateTime(1));
            Assert.AreEqual(44.75, reader.GetDouble(2));
            Assert.AreEqual(61.28, reader.GetDouble(3));
            Assert.AreEqual(43.00, reader.GetDouble(4));
            Assert.AreEqual(44.47, reader.GetDouble(5));
            Assert.AreEqual(54.19, reader.GetDouble(6));
            Assert.AreEqual(82.81, reader.GetDouble(7));
            Assert.AreEqual(47.56, reader.GetDouble(8));
            Assert.AreEqual(51.22, reader.GetDouble(9));
            Assert.IsFalse(await reader.ReadAsync(TestContext.CancellationToken));
        }

        [TestMethod]
        public void TestInsertNoiseDtos_IgnoresDuplicateSerialAndSampleTime()
        {
            DateTime sampleTime = ParseUtc("2023-10-18T14:35:42");
            NoiseDto dto = new(
                sampleTime: sampleTime,
                lAeq: 44.75,
                lAmax: 61.28,
                lA90: 43.00,
                lA10: 44.47,
                lCeq: 54.19,
                lCmax: 82.81,
                lC90: 47.56,
                lC10: 51.22);
            string serialId = "E1234";

            _testObj!.InsertNoiseDtos(serialId, [dto, dto]);
            _testObj.InsertNoiseDtos(serialId, [dto]);

            using NpgsqlConnection connection = new(_database!.ConnectionString);
            connection.Open();
            List<NoiseDto> dtos = ReadNoiseDtos(connection, out string lastSerialId);

            Assert.HasCount(1, dtos);
            Assert.AreEqual(serialId, lastSerialId);
        }

        [TestMethod]
        public void TestReadAlertRules()
        {
            string connectionString = _database!.ConnectionString;
            using NpgsqlConnection connection = new(connectionString);
            connection.Open();

            string serialId = "E2345";
            List<NoiseMonitorDto> monitorsIn = CreateMonitorsList(1);
            _testObj!.WriteMonitorList(monitorsIn);
            List<NoiseMonitorDto> monitorsOut = _testObj.ReadMonitorList(null);
            Assert.HasCount(1, monitorsOut);
            Guid monitorId = monitorsOut[0].Id;

            int NUM_RULES = 10;
            TimeSpan startTime = new(9, 0, 0);
            TimeSpan endTime = new(17, 0, 0);
            for (int i = 0; i < NUM_RULES; i++)
            {
                InsertAlertRule(connection, i, serialId, monitorId);
            }

            // add rules that should NOT be read
            for (int i = 0; i < 3; i++)
            {
                InsertAlertRule(connection, i, "99999", monitorId);
            }

            List<RvtAlertRuleDto> rules = _testObj!.ReadRules(serialId);
            Assert.HasCount(NUM_RULES, rules);

            List<RvtAlertRuleDto> orderedRules = [.. rules.OrderBy(o => o.Field)];

            for (int i = 0; i < NUM_RULES; i++)
            {
                bool isEven = i % 2 == 0;
                RvtAlertRuleDto rule = orderedRules[i];
                Assert.AreEqual(serialId, rule.SerialId);

                Assert.AreEqual("Pm" + i, rule.Field);
                Assert.AreEqual(1.111 * i, rule.LimitOn);
                Assert.AreEqual(2.2222 * i, rule.LimitOff);
                Assert.AreEqual(isEven ? AlertType.Alert : AlertType.Caution, rule.AlertType);
                Assert.AreEqual(isEven, rule.IsActive);
                Assert.AreEqual(5 + i, rule.AveragingPeriod);
                Assert.AreEqual(isEven, rule.RuleActiveTime.Weekdays);
                Assert.AreEqual(isEven, rule.RuleActiveTime.Saturdays);
                Assert.AreEqual(isEven, rule.RuleActiveTime.Sundays);
                Assert.AreEqual(isEven ? startTime : null, rule.RuleActiveTime.StartTime);
                Assert.AreEqual(isEven ? endTime : null, rule.RuleActiveTime.EndTime);
                Assert.AreNotEqual(default, rule.Created);
            }
        }


        [TestMethod]
        public void UpdateAlertRule()
        {
            string connectionString = _database!.ConnectionString;
            using NpgsqlConnection connection = new(connectionString);
            connection.Open();

            List<NoiseMonitorDto> monitorsIn = CreateMonitorsList(1);
            _testObj!.WriteMonitorList(monitorsIn);
            List<NoiseMonitorDto> monitorsOut = _testObj.ReadMonitorList(null);
            Assert.HasCount(1, monitorsOut);
            Guid monitorId = monitorsOut[0].Id;
            string serialId = monitorsOut[0].SerialId;
            InsertAlertRule(connection, 721, serialId, monitorId);
            List<RvtAlertRuleDto> rules = _testObj!.ReadRules(serialId);
            Assert.HasCount(1, rules);

            RvtAlertRuleDto rule = rules[0];

            bool isActive = !rule.IsActive;
            rule.IsActive = isActive;

            _testObj.UpdateAlertRule(rules[0]);

            List<RvtAlertRuleDto> updatedRules = _testObj!.ReadRules(serialId);
            Assert.HasCount(1, updatedRules);
            Assert.AreEqual(isActive, updatedRules[0].IsActive);

        }

        [TestMethod]
        public void TestSetMonitorOffline()
        {
            string connectionString = _database!.ConnectionString;
            using NpgsqlConnection connection = new(connectionString);
            connection.Open();
            List<NoiseMonitorDto> monitorsIn = CreateMonitorsList(1);
            _testObj!.WriteMonitorList(monitorsIn);

            foreach (NoiseMonitorDto m in monitorsIn)
            {
                Assert.IsFalse(m.Offline);
                _testObj.SetMonitorOffline(m.Id, true);
            }
            List<NoiseMonitorDto> monitorsOut = _testObj.ReadMonitorList(null);
            Assert.HasCount(1, monitorsOut);
            foreach (NoiseMonitorDto m in monitorsOut)
            {
                Assert.IsTrue(m.Offline);

            }
        }


        [TestMethod]
        public void TestGetAverageNoiseLevel()
        {
            string serialId = "98231";
            DateTime startTime = ParseUtc("2023-10-17T14:37:42");

            double LAeqTotal = .0;
            double LAMaxTotal = .0;
            double LA90Total = .0;
            double LA10Total = .0;

            double LCeqTotal = .0;
            double LCMaxTotal = .0;
            double LC90Total = .0;
            double LC10Total = .0;

            int numDtos = 15;
            for (int i = 0; i < numDtos; i++)
            {
                double LAeq = 1.0 * i;
                double LAMax = 2.5 * i;
                int LA90 = 90 * i;
                int LA10 = 10 * i;

                double LCeq = 1.0 * i * 2;
                double LCMax = 2.5 * i * 2;
                int LC90 = 90 * i * 2;
                int LC10 = 10 * i * 2;

                LAeqTotal += LAeq;
                LAMaxTotal += LAMax;
                LA90Total += LA90;
                LA10Total += LA10;

                LCeqTotal += LCeq;
                LCMaxTotal += LCMax;
                LC90Total += LC90;
                LC10Total += LC10;

                NoiseDto dto = new(sampleTime: startTime.AddMinutes(i).AddSeconds(1), lAeq: LAeq, lAmax: LAMax, lA90: LA90,
                            lA10: LA10, lCeq: LCeq, lCmax: LCMax, lC90: LC90, lC10: LC10);

                _testObj!.InsertNoiseDtos(serialId, [dto]);
            }

            double avgLAeq = _testObj!.GetAverageNoiseLevel(serialId, "LAeq", startTime, startTime.AddMinutes(15));
            Assert.AreEqual(LAeqTotal / numDtos, avgLAeq);
            double avgLAMax = _testObj!.GetAverageNoiseLevel(serialId, "LAMax", startTime, startTime.AddMinutes(15));
            Assert.AreEqual(LAMaxTotal / numDtos, avgLAMax);
            double avgLA90 = _testObj!.GetAverageNoiseLevel(serialId, "LA90", startTime, startTime.AddMinutes(15));
            Assert.AreEqual(LA90Total / numDtos, avgLA90);
            double avgLA10 = _testObj!.GetAverageNoiseLevel(serialId, "LA10", startTime, startTime.AddMinutes(15));
            Assert.AreEqual(LA10Total / numDtos, avgLA10);

            double avgLCeq = _testObj!.GetAverageNoiseLevel(serialId, "LCeq", startTime, startTime.AddMinutes(15));
            Assert.AreEqual(LCeqTotal / numDtos, avgLCeq);
            double avgLCMax = _testObj!.GetAverageNoiseLevel(serialId, "LCMax", startTime, startTime.AddMinutes(15));
            Assert.AreEqual(LCMaxTotal / numDtos, avgLCMax);
            double avgLC90 = _testObj!.GetAverageNoiseLevel(serialId, "LC90", startTime, startTime.AddMinutes(15));
            Assert.AreEqual(LC90Total / numDtos, avgLC90);
            double avgLC10 = _testObj!.GetAverageNoiseLevel(serialId, "LC10", startTime, startTime.AddMinutes(15));
            Assert.AreEqual(LC10Total / numDtos, avgLC10);

        }

        [TestMethod]
        public void TestCreate8hourAverage_UpdatesPostgreSqlAverageWhenLateSamplesArrive()
        {
            string serialId = "98231";
            DateTime sampleTime = ParseUtc("2023-10-17T16:00:00");

            _testObj!.InsertNoiseDtos(serialId,
            [
                new(sampleTime.AddHours(-2), lAeq: 10, lAmax: 20, lA90: 30, lA10: 40, lCeq: 50, lCmax: 60, lC90: 70, lC10: 80),
                new(sampleTime.AddHours(-1), lAeq: 30, lAmax: 40, lA90: 50, lA10: 60, lCeq: 70, lCmax: 80, lC90: 90, lC10: 100)
            ]);
            _testObj.Create8hourAverage(serialId, sampleTime);

            _testObj.InsertNoiseDtos(serialId,
            [
                new(sampleTime.AddMinutes(-30), lAeq: 50, lAmax: 60, lA90: 70, lA10: 80, lCeq: 90, lCmax: 100, lC90: 110, lC10: 120)
            ]);
            _testObj.Create8hourAverage(serialId, sampleTime);

            List<Noise8HourAverage> averages = ReadNoise8HourAverages(_database!.ConnectionString);

            Assert.HasCount(1, averages);
            Assert.AreEqual(serialId, averages[0].SerialId);
            Assert.AreEqual(sampleTime, averages[0].SampleTime);
            Assert.AreEqual(30, averages[0].LAeq);
            Assert.AreEqual(3, averages[0].NumberOfSamples);
        }


        private static List<NoiseMonitorDto> CreateMonitorsList(int numMonitors, string serialId = "monitor")
        {
            List<NoiseMonitorDto> monitors = [];
            for (int i = 0; i < numMonitors; i++)
            {
                DateTime dt = DateTime.UtcNow.AddMinutes(i);
                NoiseMonitorDto monitor = new(id: Guid.NewGuid(),
                                                listedAtTime: dt,
                                                lastDataTime: null,
                                                serialId: serialId + i,
                                                model: "model" + i,
                                                firmwareVersion: "Unknown",
                                                manufacturer: "Turnkey",
                                                fleetNr: "1233",
                                                latitude: 44.4f + i,
                                                longitude: 55.5f + i,
                                                address: "address" + i,
                                                timeZone: "timezone" + i,
                                                customerDisplayName: "customerDisplayName" + i,
                                                offline: false,
                                                monitorStatus: new NoiseMonitorStatus(DateTime.UtcNow, status: NoiseMonitorStatus.ACTIVE, errorCount: 0,
                                                                                      batteryVoltage: "987.32 V", calibrationDate: DateTime.UtcNow,
                                                                                      filterChangeDate: DateTime.UtcNow, pumpHours: "77 hours"));
                monitors.Add(monitor);
            }
            return monitors;

        }

        private static DateTime ParseUtc(string value) =>
            DateTime.Parse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);

        private static void InsertDeletedGlobalOfflineRule(NpgsqlConnection connection)
        {
            string sql = @"INSERT INTO rvt_alert_rule
                            (id, serial_id, alert_field, limit_on, limit_off, alert_type, is_active, averaging_period,
                             weekdays, saturdays, sundays, is_deleted, created)
                        VALUES (@Id, NULL, @AlertField, 0, 0, 2, true, 86400, true, true, true, true, @Created);";

            using NpgsqlCommand cmd = new(sql, connection);
            cmd.Parameters.AddWithValue("@Id", Guid.NewGuid());
            cmd.Parameters.AddWithValue("@AlertField", RuleConstants.OFFLINE_RULE);
            cmd.Parameters.AddWithValue("@Created", DateTime.UtcNow);
            cmd.ExecuteNonQuery();
        }

        private static void InsertAlertRule(NpgsqlConnection connection, int index, string serialId, Guid monitorId,
                                            AlertType? alertType = null)
        {
            string sql = @"INSERT INTO rvt_alert_rule
                            (id, serial_id, alert_field, limit_on, limit_off, alert_type, is_active, averaging_period,
                             weekdays, saturdays, sundays, start_time, end_time, is_deleted, monitor_id, created)
                        VALUES (@Id, @SerialId, @AlertField, @LimitOn, @LimitOff, @AlertType, @IsActive, @AveragingPeriod,
                                @Weekdays, @Saturdays, @Sundays, @StartTime, @EndTime, @IsDeleted, @MonitorId, @Created);";

            bool isEven = index % 2 == 0;


            AlertType? at = alertType != null ? alertType! : isEven ? AlertType.Alert : AlertType.Caution;
            using NpgsqlCommand cmd = new(sql, connection);
            cmd.Parameters.AddWithValue("@Id", Guid.NewGuid());
            cmd.Parameters.AddWithValue("@SerialId", serialId);
            cmd.Parameters.AddWithValue("@AlertField", "Pm" + index);
            cmd.Parameters.AddWithValue("@LimitOn", 1.111 * index);
            cmd.Parameters.AddWithValue("@LimitOff", 2.2222 * index);
            cmd.Parameters.AddWithValue("@AlertType", (int)at);
            cmd.Parameters.AddWithValue("@IsActive", isEven);
            cmd.Parameters.AddWithValue("@AveragingPeriod", 5 + index);
            cmd.Parameters.AddWithValue("@Weekdays", isEven);
            cmd.Parameters.AddWithValue("@Saturdays", isEven);
            cmd.Parameters.AddWithValue("@Sundays", isEven);
            cmd.Parameters.AddWithValue(
                "@StartTime", NpgsqlDbType.Time, isEven ? new TimeSpan(9, 0, 0) : (object)DBNull.Value);
            cmd.Parameters.AddWithValue(
                "@EndTime", NpgsqlDbType.Time, isEven ? new TimeSpan(17, 0, 0) : (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@IsDeleted", false);
            cmd.Parameters.AddWithValue("@MonitorId", monitorId);
            cmd.Parameters.AddWithValue("@Created", DateTime.UtcNow);
            cmd.ExecuteNonQuery();
        }


        [TestMethod]
        public void TestWriteSiteAverage()
        {

            Guid siteId = Guid.NewGuid();
            Guid monitorId = Guid.NewGuid();
            string field = "foo";
            double level = 99.43;
            DateTime timestamp = DateTime.UtcNow;
            _testObj!.WriteDailyAverage(siteId, monitorId, field, level, timestamp);

            List<SiteAverage> siteAverages = ReadSiteAverages(_database!.ConnectionString);

            Assert.HasCount(1, siteAverages);
            SiteAverage sa = siteAverages[0];

            Assert.AreNotEqual(Guid.Empty, sa.Id);
            Assert.AreEqual(siteId, sa.SiteId);
            Assert.AreEqual(monitorId, sa.MonitorId);
            Assert.AreEqual(field, sa.Field);
            Assert.AreEqual(level, sa.Level);
            Assert.AreEqual(DateTimeUtil.TruncateMillis(timestamp), DateTimeUtil.TruncateMillis(sa.CollectionTime));

        }



        class SiteAverage
        {
            public Guid Id { get; set; }
            public Guid SiteId { get; set; }
            public Guid MonitorId { get; set; }
            public double Level { get; set; }
            public string? Field { get; set; }
            public DateTime CollectionTime { get; set; }

        }

        class Noise8HourAverage
        {
            public string SerialId { get; set; } = "";
            public DateTime SampleTime { get; set; }
            public double LAeq { get; set; }
            public int NumberOfSamples { get; set; }
        }


        private static List<SiteAverage> ReadSiteAverages(string connectionString)
        {


            using NpgsqlConnection connection = new(connectionString);
            connection.Open();
            string sql = @"SELECT id, site_id, monitor_id, level, field, collection_time FROM site_average;";

            using NpgsqlCommand cmd = new(sql, connection);

            using NpgsqlDataReader reader = cmd.ExecuteReader();

            List<SiteAverage> siteAverages = [];
            while (reader.Read())
            {

                SiteAverage sa = new()
                {
                    Id = reader.GetGuid(0),
                    SiteId = reader.GetGuid(1),
                    MonitorId = reader.GetGuid(2),
                    Level = reader.GetDouble(3),
                    Field = reader.GetString(4),
                    CollectionTime = reader.GetDateTime(5)
                };

                siteAverages.Add(sa);
            }
            return siteAverages;
        }

        private static List<Noise8HourAverage> ReadNoise8HourAverages(string connectionString)
        {
            using NpgsqlConnection connection = new(connectionString);
            connection.Open();
            string sql = @"SELECT serial_id, sample_time, laeq, number_of_samples
                        FROM air_q_noise_8_hour_average;";

            using NpgsqlCommand cmd = new(sql, connection);
            using NpgsqlDataReader reader = cmd.ExecuteReader();

            List<Noise8HourAverage> averages = [];
            while (reader.Read())
            {
                averages.Add(new Noise8HourAverage
                {
                    SerialId = reader.GetString(0),
                    SampleTime = reader.GetDateTime(1),
                    LAeq = reader.GetDouble(2),
                    NumberOfSamples = reader.GetInt32(3)
                });
            }

            return averages;
        }



        private static List<NoiseDto> ReadNoiseDtos(NpgsqlConnection connection, out string serialId)
        {
            serialId = "";
            string sql = @"SELECT serial_id, sample_time, laeq, lamax, la_90, la_10, lceq, lcmax, lc_90, lc_10
                        FROM air_q_noise_level;";
            using NpgsqlCommand cmd = new(sql, connection);
            List<NoiseDto> dtos = [];
            using NpgsqlDataReader reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                serialId = reader.GetString(0);
                DateTime sampleTime = reader.GetDateTime(1);
                double lAeq = reader.GetDouble(2);
                double lAmax = reader.GetDouble(3);
                double lA90 = reader.GetDouble(4);
                double lA10 = reader.GetDouble(5);
                double lCeq = reader.GetDouble(6);
                double lCmax = reader.GetDouble(7);
                double lC90 = reader.GetDouble(8);
                double lC10 = reader.GetDouble(9);

                NoiseDto dto = new(sampleTime: sampleTime, lAeq: lAeq, lAmax: lAmax, lA90: lA90,
                        lA10: lA10, lCeq: lCeq, lCmax: lCmax, lC90: lC90, lC10: lC10);
                dtos.Add(dto);
            }

            return dtos;
        }

        public TestContext TestContext { get; set; } = null!;
    }
}
