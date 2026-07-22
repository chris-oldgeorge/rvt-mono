using System.Data;
using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;
using Rvt.Monitor.Common.Configuration;
using Rvt.Monitor.Common.Data;
using Rvt.Monitor.Common.Diagnostics;
using Rvt.Monitor.Common.Notifications;
using Rvt.Monitor.Common.Rules;
using Rvt.Monitor.Common.Utilities;
using Rvt.Monitor.IntegrationTesting;
using Svantek.Api.Db;
using Svantek.Model.Dto;
using AlertActivityTimeDto = Rvt.Monitor.Common.Rules.AlertActivityTimeDto;
using ContactMethod = Rvt.Monitor.Common.Rules.ContactMethod;
using NotificationDto = Rvt.Monitor.Common.Rules.NotificationDto;
using RvtContactDto = Rvt.Monitor.Common.Rules.RvtContactDto;
namespace SvantekMonitorTests
{

    // Summary: Exercises Svantek PostgreSQL database persistence against a scoped fixture.
    // Major updates:
    // - 2026-06-18 Test fixture hardening: aligned timestamp and max-noise expectations with current data-access semantics.
    [TestClass]
    [TestCategory("PostgreSqlIntegration")]
    public class TestDBClient
    {

        private static PostgreSqlIntegrationDatabase? database;

        private static DBClient? testObj;

        public TestDBClient()
        {
            var factory = LoggerFactory.Create(builder =>
            {
                builder.AddConsole().SetMinimumLevel(LogLevel.Debug);
            });
            RvtLogger.CreateLogger(factory, "TestDBClient");
        }

        [TestMethod]
        public void TestScopedPostgresConnectionUsesFixtureSchema()
        {
            using var connection = database!.OpenConnection();
            connection.Open();
            using var command = new NpgsqlCommand("SELECT current_schema();", connection);

            Assert.AreEqual(database.SchemaName, command.ExecuteScalar());
        }

        [ClassInitialize]
        public static async Task TestFixtureSetup(TestContext context)
        {
            Environment.SetEnvironmentVariable("RVT__DATABASE_PROVIDER", "PostgreSql");
            var setupSql = TestUtil.ReadTextFromFile("testdata/create.postgres.sql");
            var resetSql = TestUtil.ReadTextFromFile("testdata/reset.postgres.sql");
            database = await PostgreSqlIntegrationDatabase.CreateAsync(setupSql, resetSql);
            testObj = new DBClient(database.ConnectionString);
        }

        [ClassCleanup]
        public static async Task TestFixtureCleanup()
        {
            if (database is not null)
            {
                await database.DisposeAsync();
            }
        }

        [TestInitialize]
        public async Task BeforeTest()
        {
            await database!.ResetAsync(TestUtil.ReadTextFromFile("testdata/reset.postgres.sql"));
        }

        [DataRow("", "", 5, 5)]
        [DataRow("2023-11-21T12:00:00Z", "2023-11-21T12:00:00Z", 5, 5)]
        [DataRow("2023-11-21T12:00:00Z", "2023-11-21T13:00:00Z", 5, 4)]
        [DataRow("2023-11-21T12:00:00Z", "2023-11-21T13:59:00Z", 5, 3)]
        [DataRow("2023-11-21T12:00:00Z", "2023-11-21T15:00:00Z", 5, 2)]
        [DataRow("2023-11-21T12:00:00Z", "2023-11-21T16:00:00Z", 5, 1)]
        [DataRow("2023-11-21T12:00:00Z", "2023-11-21T16:01:00Z", 5, 0)]
        [DataTestMethod]
        public void TestMonitorsList(string lastDate, string queryDate, int numMonitors, int numExpectedMonitors)

        {
            DateTime? lastDataTime = String.IsNullOrEmpty(lastDate) ? null : PostgreSqlFixtureDateTime.ParseUtc(lastDate);
            DateTime? queryLastdataTime = String.IsNullOrEmpty(queryDate) ? null : PostgreSqlFixtureDateTime.ParseUtc(queryDate);
            var monitorsIn = CreateMonitorsList(numMonitors);
            Assert.AreEqual(numMonitors, monitorsIn.Count);
            testObj!.WriteMonitorList(monitorsIn);

            if (lastDataTime != null)
            {
                for (var i = 0; i < monitorsIn.Count; i++)
                {
                    var dt = ((DateTime)lastDataTime!).AddHours(i);
                    testObj.WriteLatestTimestamp(monitorsIn[i].SerialId, dt);
                }
            }

            var monitorsOut = testObj.ReadMonitorList(queryLastdataTime);
            Assert.AreEqual(numExpectedMonitors, monitorsOut.Count);
        }

        [TestMethod]
        public void TestReadGlobalRules()
        {
            var connectionString = database!.ConnectionString;
            using var connection = new NpgsqlConnection(connectionString);
            connection.Open();

            var rules = testObj!.ReadRules(null);
            Assert.AreEqual(1, rules.Count);

            var rule = rules[0];

            Assert.IsNull(rule.SerialId);
            Assert.AreEqual(RuleConstants.OFFLINE_RULE, rule.Field);
            Assert.IsTrue(rule.IsActive);
            Assert.IsFalse(rule.IsDeleted);
            Assert.AreEqual(0, rule.LimitOn);
            Assert.AreEqual(0, rule.LimitOff);
            Assert.AreEqual(24 * 60 * 60, rule.AveragingPeriod);
            Assert.IsNotNull(rule.Created);
            Assert.IsNull(rule.Accessed);
            Assert.IsNull(rule.RuleActiveTime.StartTime);
            Assert.IsNull(rule.RuleActiveTime.EndTime);
            Assert.IsTrue(rule.RuleActiveTime.Weekdays);
            Assert.IsTrue(rule.RuleActiveTime.Saturdays);
            Assert.IsTrue(rule.RuleActiveTime.Sundays);
        }

        [TestMethod]
        public void TestWriteLatestTimestamp()
        {
            var monitors = CreateMonitorsList(1, "E123");
            Assert.AreEqual(1, monitors.Count);

            testObj!.WriteMonitorList(monitors);

            var lastDataTime = PostgreSqlFixtureDateTime.ParseUtc("2023-10-18T14:35:42Z");
            var serialId = "E1230";
            testObj.WriteLatestTimestamp(serialId, lastDataTime);

            var monitorsOut = testObj.ReadMonitorList(null);
            Assert.AreEqual(1, monitorsOut.Count);

            var monitor = monitorsOut[0];
            Assert.AreEqual(lastDataTime, monitor.LastDataTime);
            Assert.AreEqual(DateTimeKind.Utc, monitor.LastDataTime!.Value.Kind);
        }


        [TestMethod]
        public void TestHandleException()
        {
            var connectionString = database!.ConnectionString;

            var TAG = "MyTestError";
            var MESSAGE = "bang";

            var monitorOptions = new MonitorDbOptions(
                MonitorDatabaseProvider.PostgreSql,
                new Dictionary<string, string>());
            MonitorDb.WriteException(
                connectionString,
                TAG,
                AdapterException.Of(MESSAGE),
                "SvantekMonitorTests",
                "1.0",
                monitorOptions);

            using var connection = new NpgsqlConnection(connectionString);
            connection.Open();

            var sql = @"SELECT variables, message, logged_at FROM error_log";
            using NpgsqlCommand cmd = new(sql, connection);
            using NpgsqlDataReader reader = cmd.ExecuteReader();

            var count = 0;
            while (reader.Read())
            {
                count++;
                var tag = reader.GetString(0);
                var error = reader.GetString(1);
                var errorTime = reader.GetDateTime(2);
                Assert.AreEqual(TAG, tag);
                Assert.AreEqual(MESSAGE, error);
                Assert.IsTrue(errorTime <= DateTime.UtcNow.AddSeconds(10));

            }
            Assert.AreEqual(1, count);
        }

        [TestMethod]
        [DataRow("2023-09-18T00:15:00.000+00:00", "2023-09-18T00:15:00Z")]
        [DataRow("2023-10-30T12:30:00.000+00:00", "2023-10-30T12:30:00Z")]
        [DataRow("2023-03-20T07:30:00.000+00:00", "2023-03-20T07:30:00Z")]
        public void TestInsertNoiseDto_DaylightSaving_Success(string actual, string expected)
        {
            var actualDt = PostgreSqlFixtureDateTime.ParseUtc(actual);
            var samples = SvantekFixture.SamplesResponseObjects(actualDt);
            var serialId = "E1234";
            testObj!.InsertNoiseDtos(serialId, new List<NoiseDto> { new NoiseDto(samples[0]) });

            var connectionString = database!.ConnectionString;
            using var connection = new NpgsqlConnection(connectionString);
            connection.Open();
            var dtos = ReadNoiseDtos(connection, out string lastSerialId);
            Assert.AreEqual(1, dtos.Count);
            var dtoOut = dtos[0];

            Assert.AreEqual(serialId, lastSerialId);
            var expectedDt = PostgreSqlFixtureDateTime.ParseUtc(expected);
            Assert.AreEqual(expectedDt, dtoOut.SampleTime);
            Assert.AreEqual(DateTimeKind.Utc, dtoOut.SampleTime.Kind);
            Assert.AreEqual(44.75, dtoOut.LAeq);
            Assert.AreEqual(61.28, dtoOut.LAmax);
            Assert.AreEqual(43.00, dtoOut.LA90);
            Assert.AreEqual(44.47, dtoOut.LA10);
            Assert.AreEqual(54.19, dtoOut.LCeq);
            Assert.AreEqual(82.81, dtoOut.LCmax);
            Assert.AreEqual(47.56, dtoOut.LC90);
            Assert.AreEqual(51.22, dtoOut.LC10);
        }

        [TestMethod]
        public async Task TestInsertNoiseDtoPersistsCanonicalPostgreSqlRow()
        {
            var serialId = "E4321";
            var sampleTime = PostgreSqlFixtureDateTime.ParseUtc("2023-10-18T14:35:42Z");
            var dto = new NoiseDto(sampleTime: sampleTime, lAeq: 44.75, lAmax: 61.28, lA90: 43.00,
                lA10: 44.47, lCeq: 54.19, lCmax: 82.81, lC90: 47.56, lC10: 51.22);

            testObj!.InsertNoiseDtos(serialId, new List<NoiseDto> { dto });

            await using var connection = database!.OpenConnection();
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(
                "SELECT serial_id, sample_time, laeq FROM svantek_noise_level ORDER BY sample_time;", connection);
            await using var reader = await command.ExecuteReaderAsync();

            Assert.IsTrue(await reader.ReadAsync());
            Assert.AreEqual(serialId, reader.GetString(0));
            var persistedSampleTime = reader.GetDateTime(1);
            Assert.AreEqual(sampleTime, persistedSampleTime);
            Assert.AreEqual(DateTimeKind.Utc, persistedSampleTime.Kind);
            Assert.AreEqual(44.75, reader.GetDouble(2));
        }

        [TestMethod]
        public void TestInsertNoiseDtos_DuplicateSampleIgnored()
        {
            var serialId = "E5678";
            var sampleTime = PostgreSqlFixtureDateTime.ParseUtc("2023-10-18T14:35:42Z");
            var dto = new NoiseDto(sampleTime: sampleTime, lAeq: 1, lAmax: 2, lA90: 3,
                lA10: 4, lCeq: 5, lCmax: 6, lC90: 7, lC10: 8);

            testObj!.InsertNoiseDtos(serialId, new List<NoiseDto> { dto, dto });
            testObj.InsertNoiseDtos(serialId, new List<NoiseDto> { dto });

            var connectionString = database!.ConnectionString;
            using var connection = new NpgsqlConnection(connectionString);
            connection.Open();
            var dtos = ReadNoiseDtos(connection, out string lastSerialId);

            Assert.AreEqual(1, dtos.Count);
            Assert.AreEqual(serialId, lastSerialId);
            Assert.AreEqual(sampleTime, dtos[0].SampleTime);
            Assert.AreEqual(DateTimeKind.Utc, dtos[0].SampleTime.Kind);
        }

        [TestMethod]
        public void TestReadAlertRules()
        {
            var connectionString = database!.ConnectionString;
            using var connection = new NpgsqlConnection(connectionString);
            connection.Open();

            var serialId = "E2345";
            var monitorsIn = CreateMonitorsList(1);
            testObj!.WriteMonitorList(monitorsIn);
            var monitorsOut = testObj.ReadMonitorList(null);
            Assert.AreEqual(1, monitorsOut.Count);
            var monitorId = monitorsOut[0].Id;

            var NUM_RULES = 10;
            var startTime = new TimeSpan(9, 0, 0);
            var endTime = new TimeSpan(17, 0, 0);
            for (var i = 0; i < NUM_RULES; i++)
            {
                InsertAlertRule(connection, i, serialId, monitorId);
            }

            // add rules that should NOT be read
            for (var i = 0; i < 3; i++)
            {
                InsertAlertRule(connection, i, "99999", monitorId);
            }

            var rules = testObj!.ReadRules(serialId);
            Assert.AreEqual(NUM_RULES, rules.Count);

            var orderedRules = rules.OrderBy(o => o.Field).ToList();

            for (var i = 0; i < NUM_RULES; i++)
            {
                var isEven = i % 2 == 0;
                var rule = orderedRules[i];
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
                Assert.IsNotNull(rule.Created);
            }
        }

        [TestMethod]
        public void TestReadAlertContacts()
        {
            var connectionString = database!.ConnectionString;
            using var connection = new NpgsqlConnection(connectionString);
            connection.Open();

            var numMonitors = 2;
            var monitorsIn = CreateMonitorsList(numMonitors);
            testObj!.WriteMonitorList(monitorsIn);
            var monitorsOut = testObj.ReadMonitorList(null);
            Assert.AreEqual(numMonitors, monitorsOut.Count);
            var monitorId = monitorsOut[0].Id;
            var serialId = monitorsOut[0].SerialId;
            // add an alert and contact as RvtAlertContacts table has foreign key constraints
            InsertAlertRule(connection, 44, serialId, monitorId);
            var rules = testObj!.ReadRules(serialId);
            Assert.AreEqual(1, rules.Count);
            var email = "mytestemail@bbb.com";
            var phoneNo = "01234567890";
            var startTime = DateTimeUtil.TruncateMillis(DateTime.UtcNow.AddHours(-1));
            var endTime = DateTimeUtil.TruncateMillis(DateTime.UtcNow.AddHours(1));
            var siteUserId = Guid.NewGuid();
            InsertContact(connection: connection,
                          monitorId: monitorId,
                          contactMethod: ContactMethod.Email,
                          email: email,
                          phoneNo: phoneNo,
                          siteUserId: siteUserId,
                          siteId: Guid.NewGuid(),
                          sendStartTime: startTime,
                          sendEndTime: endTime);

            // insert that should not be read
            InsertContact(connection: connection,
                          monitorId: monitorsOut[1].Id,
                          contactMethod: ContactMethod.Email,
                          email: email,
                          phoneNo: phoneNo,
                          siteUserId: Guid.NewGuid(),
                          siteId: Guid.NewGuid());

            var contacts = ReadContacts(connection, siteUserId);
            Assert.AreEqual(2, contacts.Count);

            var alertContacts = testObj.ReadAlertContacts(monitorId, out Guid siteId);
            Assert.AreEqual(1, alertContacts.Count);
            Assert.AreNotEqual(Guid.Empty, siteId);

            var ac = alertContacts[0];
            Assert.AreEqual(ContactMethod.Email, ac.ContactMethod);
            Assert.AreEqual(email, ac.EmailAddress);
            Assert.AreEqual(phoneNo, ac.PhoneNumber);

            Assert.AreEqual(startTime.TimeOfDay, ac.SendStartTime);
            Assert.AreEqual(endTime.TimeOfDay, ac.SendEndTime);
        }

        [TestMethod]
        public void TestWriteNotification()
        {
            var connectionString = database!.ConnectionString;
            using var connection = new NpgsqlConnection(connectionString);
            connection.Open();

            var serialId = "E8271";
            var monitorsIn = CreateMonitorsList(1);
            testObj!.WriteMonitorList(monitorsIn);
            var monitorsOut = testObj.ReadMonitorList(null);
            Assert.AreEqual(1, monitorsOut.Count);
            var monitorId = monitorsOut[0].Id;


            // add an alert and contact as RvtAlertContacts table has foreign key constraints
            InsertAlertRule(connection, 21, serialId, monitorId, AlertType.Caution);
            var rules = testObj!.ReadRules(serialId);
            Assert.AreEqual(1, rules.Count);
            var email = "foobob@bbb.com";
            var phoneNo = "01238867890";
            var siteUserId = Guid.NewGuid();
            InsertContact(connection: connection,
                          monitorId: monitorId,
                          contactMethod: ContactMethod.Email,
                          email: email,
                          phoneNo: phoneNo,
                          siteUserId: siteUserId,
                          siteId: Guid.NewGuid());
            var contacts = ReadContacts(connection, siteUserId);
            Assert.AreEqual(1, contacts.Count);


            Assert.IsFalse(testObj.HasOpenNotification(monitorId, rules[0].Field, AlertType.Caution));
            Assert.IsFalse(testObj.HasOpenNotification(monitorId, rules[0].Field, AlertType.Alert));

            var dt = PostgreSqlFixtureDateTime.ParseUtc("2023-10-18T11:19:00Z");
            var notifyCaution = new NotificationDto(id: Guid.NewGuid(),
                                              notificationTime: dt,
                                              limitOn: rules[0].LimitOn,
                                              averagingPeriod: rules[0].AveragingPeriod,
                                              level: 99.876,
                                              closedTime: null,
                                              closedByUser: null,
                                              alertType: AlertType.Caution,
                                              alertField: rules[0].Field,
                                              monitorId: monitorId);

            testObj.WriteNotification(notifyCaution);

            Assert.IsTrue(testObj.HasOpenNotification(monitorId, rules[0].Field, AlertType.Caution));
            Assert.IsFalse(testObj.HasOpenNotification(monitorId, rules[0].Field, AlertType.Alert));


            {
                var alerts = ReadNotifications(connection);
                Assert.AreEqual(1, alerts.Count);
                var alertOut = alerts[0];

                Assert.AreEqual(notifyCaution.Id, alertOut.Id);
                Assert.AreEqual(notifyCaution.Level, alertOut.Level);
                Assert.AreEqual(notifyCaution.NotificationTime, alertOut.NotificationTime);
                Assert.AreEqual(notifyCaution.LimitOn, alertOut.LimitOn);
                Assert.AreEqual(notifyCaution.AveragingPeriod, alertOut.AveragingPeriod);
                Assert.AreEqual(notifyCaution.AlertField, alertOut.AlertField);
                Assert.AreEqual(notifyCaution.AlertType, alertOut.AlertType);
            }


            var notifyAlert = new NotificationDto(id: Guid.NewGuid(),
                                              notificationTime: dt,
                                              limitOn: rules[0].LimitOn,
                                              averagingPeriod: rules[0].AveragingPeriod,
                                              level: 99.876,
                                              closedTime: null,
                                              closedByUser: null,
                                              alertType: AlertType.Alert,
                                              alertField: rules[0].Field,
                                              monitorId: monitorId);

            testObj.WriteNotification(notifyAlert);

            Assert.IsTrue(testObj.HasOpenNotification(monitorId, rules[0].Field, AlertType.Caution));
            Assert.IsTrue(testObj.HasOpenNotification(monitorId, rules[0].Field, AlertType.Alert));

            Assert.AreEqual(2, ReadNotifications(connection).Count);
        }

        [DataRow(AlertType.Caution, AlertType.Alert, false)]
        [DataRow(AlertType.Caution, AlertType.Caution, true)]
        [DataRow(AlertType.Alert, AlertType.Caution, false)]
        [DataRow(AlertType.Alert, AlertType.Alert, true)]
        [DataTestMethod]
        public void TestHasOpenNotification(AlertType existing, AlertType alertType, bool expectedResult)
        {
            var connectionString = database!.ConnectionString;
            using var connection = new NpgsqlConnection(connectionString);
            connection.Open();

            var serialId = "E8271";
            var monitorsIn = CreateMonitorsList(1);

            testObj!.WriteMonitorList(monitorsIn);
            var monitorsOut = testObj.ReadMonitorList(null);

            Assert.AreEqual(1, monitorsOut.Count);
            var monitorId = monitorsOut[0].Id;


            // add an alert and contact as RvtAlertContacts table has foreign key constraints
            InsertAlertRule(connection, 21, serialId, monitorId, alertType);
            var rules = testObj!.ReadRules(serialId);
            Assert.AreEqual(1, rules.Count);
            var email = "foobob@bbb.com";
            var phoneNo = "01238867890";
            var siteUserId = Guid.NewGuid();
            InsertContact(connection: connection,
                          monitorId: monitorId,
                          contactMethod: ContactMethod.Email,
                          email: email,
                          phoneNo: phoneNo,
                          siteUserId: siteUserId,
                          siteId: Guid.NewGuid());
            var contacts = ReadContacts(connection, siteUserId);
            Assert.AreEqual(1, contacts.Count);

            Assert.IsFalse(testObj.HasOpenNotification(monitorId, rules[0].Field, AlertType.Caution));
            Assert.IsFalse(testObj.HasOpenNotification(monitorId, rules[0].Field, AlertType.Alert));

            var dt = PostgreSqlFixtureDateTime.ParseUtc("2023-10-18T11:19:00Z");
            var existingNotification = new NotificationDto(id: Guid.NewGuid(),
                                              notificationTime: dt,
                                              limitOn: rules[0].LimitOn,
                                              averagingPeriod: rules[0].AveragingPeriod,
                                              level: 99.876,
                                              closedTime: null,
                                              closedByUser: null,
                                              alertType: existing,
                                              alertField: rules[0].Field,
                                              monitorId: monitorId);

            testObj.WriteNotification(existingNotification);

            Assert.AreEqual(expectedResult, testObj.HasOpenNotification(monitorId, rules[0].Field, alertType));
        }


        [TestMethod]
        public void UpdateAlertRule()
        {
            var connectionString = database!.ConnectionString;
            using var connection = new NpgsqlConnection(connectionString);
            connection.Open();

            var monitorsIn = CreateMonitorsList(1);
            testObj!.WriteMonitorList(monitorsIn);
            var monitorsOut = testObj.ReadMonitorList(null);
            Assert.AreEqual(1, monitorsOut.Count);
            var monitorId = monitorsOut[0].Id;
            var serialId = monitorsOut[0].SerialId;
            InsertAlertRule(connection, 721, serialId, monitorId);
            var rules = testObj!.ReadRules(serialId);
            Assert.AreEqual(1, rules.Count);

            var rule = rules[0];

            var isActive = !rule.IsActive;
            rule.IsActive = isActive;

            testObj.UpdateAlertRule(rules[0]);

            var updatedRules = testObj!.ReadRules(serialId);
            Assert.AreEqual(1, updatedRules.Count);
            Assert.AreEqual(isActive, updatedRules[0].IsActive);

        }

        [TestMethod]
        public void TestSetMonitorOffline()
        {
            var connectionString = database!.ConnectionString;
            using var connection = new NpgsqlConnection(connectionString);
            connection.Open();
            var monitorsIn = CreateMonitorsList(1);
            testObj!.WriteMonitorList(monitorsIn);

            foreach (var m in monitorsIn)
            {
                Assert.IsFalse(m.Offline);
                testObj.SetMonitorOffline(m.Id, true);
            }
            var monitorsOut = testObj.ReadMonitorList(null);
            Assert.AreEqual(1, monitorsOut.Count);
            foreach (var m in monitorsOut)
            {
                Assert.IsTrue(m.Offline);

            }
        }

        [TestMethod]
        public void TestCatalogueRefreshPreservesRuntimeOwnedMonitorAndDeploymentState()
        {
            var monitor = CreateMonitorsList(1, "catalogue-refresh-")[0];
            testObj!.WriteMonitorList(new List<NoiseMonitorDto> { monitor });

            Guid ReadPersistedMonitorId()
            {
                using var connection = database!.OpenConnection();
                connection.Open();
                using var command = new NpgsqlCommand(
                    "SELECT id FROM monitor WHERE serial_id = @SerialId;",
                    connection);
                command.Parameters.AddWithValue("@SerialId", monitor.SerialId);
                return Assert.IsInstanceOfType<Guid>(command.ExecuteScalar());
            }

            var initialMonitorId = ReadPersistedMonitorId();
            Assert.AreEqual(monitor.Id, initialMonitorId);

            var lastDataTime1Min = PostgreSqlFixtureDateTime.ParseUtc("2026-07-15T10:01:00Z");
            var lastDataTime15Min = PostgreSqlFixtureDateTime.ParseUtc("2026-07-15T10:15:00Z");
            var lastDataTime1Hour = PostgreSqlFixtureDateTime.ParseUtc("2026-07-15T11:00:00Z");
            var lastDataTime24Hour = PostgreSqlFixtureDateTime.ParseUtc("2026-07-16T00:00:00Z");
            var deploymentStart = PostgreSqlFixtureDateTime.ParseUtc("2026-06-01T08:00:00Z");
            var deploymentEnd = PostgreSqlFixtureDateTime.ParseUtc("2026-08-01T17:00:00Z");

            using (var connection = database!.OpenConnection())
            {
                connection.Open();
                using var command = new NpgsqlCommand(
                    """
                    UPDATE monitor
                    SET customer_id = @CustomerId,
                        location_id = @LocationId,
                        last_data_time_1_min = @LastDataTime1Min,
                        last_data_time_15_min = @LastDataTime15Min,
                        last_data_time_1_hour = @LastDataTime1Hour,
                        last_data_time_24_hour = @LastDataTime24Hour,
                        offline = TRUE,
                        battery_status = @BatteryStatus
                    WHERE serial_id = @SerialId;

                    UPDATE deployment
                    SET start_date = @DeploymentStart,
                        end_date = @DeploymentEnd,
                        lng = @DeploymentLongitude,
                        lat = @DeploymentLatitude,
                        what_3_words = @What3Words,
                        picture_link = @PictureLink
                    WHERE monitor_id = @MonitorId;
                    """,
                    connection);
                command.Parameters.AddWithValue("@CustomerId", 123);
                command.Parameters.AddWithValue("@LocationId", 456);
                command.Parameters.AddWithValue("@LastDataTime1Min", lastDataTime1Min);
                command.Parameters.AddWithValue("@LastDataTime15Min", lastDataTime15Min);
                command.Parameters.AddWithValue("@LastDataTime1Hour", lastDataTime1Hour);
                command.Parameters.AddWithValue("@LastDataTime24Hour", lastDataTime24Hour);
                command.Parameters.AddWithValue("@BatteryStatus", 2);
                command.Parameters.AddWithValue("@DeploymentStart", deploymentStart);
                command.Parameters.AddWithValue("@DeploymentEnd", deploymentEnd);
                command.Parameters.AddWithValue("@DeploymentLongitude", 1.25d);
                command.Parameters.AddWithValue("@DeploymentLatitude", 2.5d);
                command.Parameters.AddWithValue("@What3Words", "runtime.state.preserved");
                command.Parameters.AddWithValue("@PictureLink", "runtime-picture");
                command.Parameters.AddWithValue("@SerialId", monitor.SerialId);
                command.Parameters.AddWithValue("@MonitorId", initialMonitorId);
                Assert.AreEqual(2, command.ExecuteNonQuery());
            }

            monitor.Model = "Updated model";
            monitor.FirmwareVersion = "9.9.9";
            monitor.Offline = false;
            monitor.LastDataTime = null;
            monitor.ProjectId = 321;
            monitor.PointId = 654;
            monitor.Active = true;
            monitor.BatteryCharge = 87;

            var firstRefreshMonitorId = Guid.NewGuid();
            Assert.AreNotEqual(initialMonitorId, firstRefreshMonitorId);
            monitor.Id = firstRefreshMonitorId;
            testObj.WriteMonitorList(new List<NoiseMonitorDto> { monitor });
            Assert.AreEqual(initialMonitorId, ReadPersistedMonitorId());

            var secondRefreshMonitorId = Guid.NewGuid();
            Assert.AreNotEqual(initialMonitorId, secondRefreshMonitorId);
            Assert.AreNotEqual(firstRefreshMonitorId, secondRefreshMonitorId);
            monitor.Id = secondRefreshMonitorId;
            testObj.WriteMonitorList(new List<NoiseMonitorDto> { monitor });
            Assert.AreEqual(initialMonitorId, ReadPersistedMonitorId());

            using var verifyConnection = database!.OpenConnection();
            verifyConnection.Open();
            using var verifyCommand = new NpgsqlCommand(
                """
                SELECT m.customer_id,
                       m.location_id,
                       m.last_data_time_1_min,
                       m.last_data_time_15_min,
                       m.last_data_time_1_hour,
                       m.last_data_time_24_hour,
                       m.offline,
                       m.battery_status,
                       m.model,
                       m.firmware_version,
                       s.project_id,
                       s.point_id,
                       s.active,
                       s.batterycharge,
                       d.start_date,
                       d.end_date,
                       d.lng,
                       d.lat,
                       d.what_3_words,
                       d.picture_link,
                       m.id
                FROM monitor m
                JOIN svantek_monitor_status s ON s.serial_id = m.serial_id
                JOIN deployment d ON d.monitor_id = m.id
                WHERE m.serial_id = @SerialId;
                """,
                verifyConnection);
            verifyCommand.Parameters.AddWithValue("@SerialId", monitor.SerialId);
            using var reader = verifyCommand.ExecuteReader();

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(123, reader.GetInt32(0));
            Assert.AreEqual(456, reader.GetInt32(1));
            Assert.AreEqual(lastDataTime1Min, reader.GetDateTime(2));
            Assert.AreEqual(lastDataTime15Min, reader.GetDateTime(3));
            Assert.AreEqual(lastDataTime1Hour, reader.GetDateTime(4));
            Assert.AreEqual(lastDataTime24Hour, reader.GetDateTime(5));
            Assert.IsTrue(reader.GetBoolean(6));
            Assert.AreEqual(2, reader.GetInt16(7));
            Assert.AreEqual("Updated model", reader.GetString(8));
            Assert.AreEqual("9.9.9", reader.GetString(9));
            Assert.AreEqual(321, reader.GetInt32(10));
            Assert.AreEqual(654, reader.GetInt32(11));
            Assert.AreEqual("1", reader.GetString(12));
            Assert.AreEqual(87, reader.GetInt32(13));
            Assert.AreEqual(deploymentStart, reader.GetDateTime(14));
            Assert.AreEqual(deploymentEnd, reader.GetDateTime(15));
            Assert.AreEqual(1.25d, reader.GetDouble(16));
            Assert.AreEqual(2.5d, reader.GetDouble(17));
            Assert.AreEqual("runtime.state.preserved", reader.GetString(18));
            Assert.AreEqual("runtime-picture", reader.GetString(19));
            Assert.AreEqual(initialMonitorId, reader.GetGuid(20));
            Assert.IsFalse(reader.Read());
        }


        [TestMethod]
        public void TestGetAverageNoiseLevel()
        {
            var serialId = "98231";
            var startTime = PostgreSqlFixtureDateTime.ParseUtc("2023-10-17T14:37:42Z");

            var LAeqTotal = .0;
            var LA90Total = .0;
            var LA10Total = .0;

            var LCeqTotal = .0;
            var LC90Total = .0;
            var LC10Total = .0;
            var expectedLAMax = .0;
            var expectedLCMax = .0;

            var numDtos = 15;
            for (var i = 0; i < numDtos; i++)
            {
                var LAeq = 1.0 * i;
                var LAMax = 2.5 * i;
                var LA90 = 90 * i;
                var LA10 = 10 * i;

                var LCeq = 1.0 * i * 2;
                var LCMax = 2.5 * i * 2;
                var LC90 = 90 * i * 2;
                var LC10 = 10 * i * 2;

                LAeqTotal += LAeq;
                LA90Total += LA90;
                LA10Total += LA10;

                LCeqTotal += LCeq;
                LC90Total += LC90;
                LC10Total += LC10;
                expectedLAMax = Math.Max(expectedLAMax, LAMax);
                expectedLCMax = Math.Max(expectedLCMax, LCMax);

                var dto = new NoiseDto(sampleTime: startTime.AddMinutes(i).AddSeconds(1), lAeq: LAeq, lAmax: LAMax, lA90: LA90,
                            lA10: LA10, lCeq: LCeq, lCmax: LCMax, lC90: LC90, lC10: LC10);

                testObj!.InsertNoiseDtos(serialId, new List<NoiseDto> { dto });
            }

            var avgLAeq = testObj!.GetAverageNoiseLevel(serialId, "LAeq", startTime, startTime.AddMinutes(15));
            Assert.AreEqual(LAeqTotal / numDtos, avgLAeq);
            var avgLAMax = testObj!.GetAverageNoiseLevel(serialId, "LAMax", startTime, startTime.AddMinutes(15));
            Assert.AreEqual(expectedLAMax, avgLAMax);
            var avgLA90 = testObj!.GetAverageNoiseLevel(serialId, "LA90", startTime, startTime.AddMinutes(15));
            Assert.AreEqual(LA90Total / numDtos, avgLA90);
            var avgLA10 = testObj!.GetAverageNoiseLevel(serialId, "LA10", startTime, startTime.AddMinutes(15));
            Assert.AreEqual(LA10Total / numDtos, avgLA10);

            var avgLCeq = testObj!.GetAverageNoiseLevel(serialId, "LCeq", startTime, startTime.AddMinutes(15));
            Assert.AreEqual(LCeqTotal / numDtos, avgLCeq);
            var avgLCMax = testObj!.GetAverageNoiseLevel(serialId, "LCMax", startTime, startTime.AddMinutes(15));
            Assert.AreEqual(expectedLCMax, avgLCMax);
            var avgLC90 = testObj!.GetAverageNoiseLevel(serialId, "LC90", startTime, startTime.AddMinutes(15));
            Assert.AreEqual(LC90Total / numDtos, avgLC90);
            var avgLC10 = testObj!.GetAverageNoiseLevel(serialId, "LC10", startTime, startTime.AddMinutes(15));
            Assert.AreEqual(LC10Total / numDtos, avgLC10);

        }

        [TestMethod]
        public async Task TestCreate8HourAveragePersistsCanonicalPostgreSqlRow()
        {
            var serialId = "E8765";
            var sampleTime = PostgreSqlFixtureDateTime.ParseUtc("2023-10-18T16:00:00Z");
            testObj!.InsertNoiseDtos(serialId, new List<NoiseDto>
            {
                new(sampleTime.AddHours(-7), 10, 20, 30, 40, 50, 60, 70, 80),
                new(sampleTime, 30, 40, 50, 60, 70, 80, 90, 100)
            });

            testObj.Create8hourAverage(serialId, sampleTime);

            await using var connection = database!.OpenConnection();
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(
                @"SELECT serial_id, sample_time, laeq, number_of_samples
                  FROM svantek_noise_8_hour_average;", connection);
            await using var reader = await command.ExecuteReaderAsync();

            Assert.IsTrue(await reader.ReadAsync());
            Assert.AreEqual(serialId, reader.GetString(0));
            var persistedSampleTime = reader.GetDateTime(1);
            Assert.AreEqual(sampleTime, persistedSampleTime);
            Assert.AreEqual(DateTimeKind.Utc, persistedSampleTime.Kind);
            Assert.AreEqual(20.0, reader.GetDouble(2));
            Assert.AreEqual(2, reader.GetInt32(3));
        }

        [DataRow("09:00:00", "17:00:00", "09:30:00", "12:30:00", "10:00:00", "11:00:00")]
        [DataRow(null, null, "19:30:00", "21:30:00", "11:00:11", "12:00:12")]
        [DataRow("09:00:00", "17:00:00", null, null, "10:00:00", "11:00:00")]
        [DataRow("09:00:00", "17:00:00", "10:23:00", "11:20:00", null, null)]
        [DataRow(null, null, null, null, null, null)]
        [DataTestMethod]
        public void TestReadSiteInfo(string? start, string? end,
                                     string? satStart, string? satEnd,
                                     string? sunStart, string? sunEnd)
        {

            var connectionString = database!.ConnectionString;
            using var connection = new NpgsqlConnection(connectionString);
            connection.Open();
            var siteId = Guid.NewGuid();

            TimeSpan? startTime = start != null ? TimeSpan.Parse(start!) : null;
            TimeSpan? endTime = end != null ? TimeSpan.Parse(end!) : null;
            TimeSpan? satStartTime = satStart != null ? TimeSpan.Parse(satStart!) : null;
            TimeSpan? satEndTime = satEnd != null ? TimeSpan.Parse(satEnd!) : null;
            TimeSpan? sunStartTime = sunStart != null ? TimeSpan.Parse(sunStart!) : null;
            TimeSpan? sunEndTime = sunEnd != null ? TimeSpan.Parse(sunEnd!) : null;

            InsertSite(connection: connection,
                       siteId: siteId,
                       startTime: startTime,
                       endTime: endTime,
                       satStartTime: satStartTime,
                       satEndTime: satEndTime,
                       sunStartTime: sunStartTime,
                       sunEndTime: sunEndTime);

            var siteInfo = testObj!.ReadSiteInfo(siteId);

            Assert.AreEqual(siteId, siteInfo.SiteId);
            Assert.AreEqual(startTime, siteInfo.StartTime);
            Assert.AreEqual(endTime, siteInfo.EndTime);
            Assert.AreEqual(satStartTime, siteInfo.SatStartTime);
            Assert.AreEqual(satEndTime, siteInfo.SatEndTime);
            Assert.AreEqual(sunStartTime, siteInfo.SunStartTime);
            Assert.AreEqual(sunEndTime, siteInfo.SunEndTime);

        }

        private static List<NoiseMonitorDto> CreateMonitorsList(int numMonitors, string serialId = "monitor")
        {
            var monitors = new List<NoiseMonitorDto>();
            for (var i = 0; i < numMonitors; i++)
            {
                var dt = DateTime.UtcNow.AddMinutes(i);
                var monitor = new NoiseMonitorDto(id: Guid.NewGuid(),
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

        private static void InsertAlertRule(NpgsqlConnection connection, int index, string serialId, Guid monitorId,
                                            AlertType? alertType = null)
        {
            var sql = @"INSERT INTO rvt_alert_rule (
                            id, serial_id, alert_field, limit_on, limit_off, alert_type, is_active, averaging_period,
                            weekdays, saturdays, sundays, start_time, end_time, is_deleted, monitor_id, created)
                        VALUES (
                            @Id, @SerialId, @AlertField, @LimitOn, @LimitOff, @AlertType, @IsActive, @AveragingPeriod,
                            @Weekdays, @Saturdays, @Sundays, @StartTime, @EndTime, @IsDeleted, @MonitorId, @Created);";

            var isEven = index % 2 == 0;


            var at = alertType != null ? alertType! : isEven ? AlertType.Alert : AlertType.Caution;
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
            cmd.Parameters.Add("@StartTime", NpgsqlDbType.Time).Value =
                isEven ? new TimeSpan(9, 0, 0) : DBNull.Value;
            cmd.Parameters.Add("@EndTime", NpgsqlDbType.Time).Value =
                isEven ? new TimeSpan(17, 0, 0) : DBNull.Value;
            cmd.Parameters.AddWithValue("@IsDeleted", false);
            cmd.Parameters.AddWithValue("@MonitorId", monitorId);
            cmd.Parameters.AddWithValue("@Created", DateTime.UtcNow);
            cmd.ExecuteNonQuery();
        }

        [TestMethod]
        public void TestWriteNotificationAudit()
        {
            var connectionString = database!.ConnectionString;
            using var connection = new NpgsqlConnection(connectionString);
            connection.Open();
            var serialId = "82731";
            var monitorsIn = CreateMonitorsList(1);
            testObj!.WriteMonitorList(monitorsIn);
            var monitorsOut = testObj.ReadMonitorList(null);
            Assert.AreEqual(1, monitorsOut.Count);
            var monitorId = monitorsOut[0].Id;
            // add an alert and contact as RvtAlertContacts table has foreign key constraints
            InsertAlertRule(connection, 21, serialId, monitorId);
            var rules = testObj!.ReadRules(serialId);
            Assert.AreEqual(1, rules.Count);
            var email = "bad-email";
            var phoneNo = "bad-phonenumber";
            var siteUserId = Guid.NewGuid();
            InsertContact(connection: connection,
                          monitorId: monitorId,
                          contactMethod: ContactMethod.Email,
                          email: email,
                          phoneNo: phoneNo,
                          siteUserId: siteUserId,
                          siteId: Guid.NewGuid());
            var contacts = ReadContacts(connection, siteUserId);
            Assert.AreEqual(1, contacts.Count);
            var dt = PostgreSqlFixtureDateTime.ParseUtc("2023-10-18T11:19:00Z");
            var notificationIn = new NotificationDto(//rules[0], 99.876, dt, monitorId);

                                                   id: Guid.NewGuid(),
                                                   notificationTime: dt,
                                                   limitOn: rules[0].LimitOn,
                                                   averagingPeriod: rules[0].AveragingPeriod,
                                                   level: 99.876,
                                                   closedTime: null,
                                                   closedByUser: null,
                                                   alertType: rules[0].AlertType,
                                                   alertField: rules[0].Field,
                                                    monitorId: monitorId);
            // need to write a alert because NotificationsSent table has foreign key constraint
            testObj.WriteNotification(notificationIn);
            testObj.WriteNotificationAudit(notificationIn.Id, "mytest@email.net", "some error message");
            var notifications = ReadNotifications(connection);
            Assert.AreEqual(1, notifications.Count);
            var notificationOut = notifications[0];
            Assert.AreEqual(notificationIn.Id, notificationOut.Id);
            Assert.AreEqual(notificationIn.Level, notificationOut.Level);
            Assert.AreEqual(notificationIn.NotificationTime, notificationOut.NotificationTime);
            Assert.AreEqual(notificationIn.LimitOn, notificationOut.LimitOn);
            Assert.AreEqual(notificationIn.AveragingPeriod, notificationOut.AveragingPeriod);
            Assert.AreEqual(notificationIn.AlertType, notificationOut.AlertType);
            Assert.AreEqual(notificationIn.AlertField, notificationOut.AlertField);
            Assert.AreEqual(notificationIn.MonitorId, notificationOut.MonitorId);

            var audits = ReadNotificationsSent(connection);
            Assert.AreEqual(1, audits.Count);
            var audit = audits[0];
            Assert.IsInstanceOfType(audit["Id"], typeof(Guid));
            Assert.IsInstanceOfType(audit["SendTime"], typeof(DateTime));
            var sendTime = (DateTime)audit["SendTime"];
            Assert.IsTrue(sendTime < DateTime.UtcNow.AddSeconds(10) && sendTime > DateTime.UtcNow.AddSeconds(-10));
            Assert.IsInstanceOfType(audit["Address"], typeof(string));
            Assert.AreEqual("mytest@email.net", (string)audit["Address"]);
            Assert.IsInstanceOfType(audit["ErrorMessage"], typeof(string));
            Assert.AreEqual("some error message", (string)audit["ErrorMessage"]);
            Assert.IsInstanceOfType(audit["NotificationId"], typeof(Guid));
        }


        [TestMethod]
        public void TestWriteSiteAverage()
        {

            var siteId = Guid.NewGuid();
            var monitorId = Guid.NewGuid();
            var field = "foo";
            var level = 99.43;
            var timestamp = DateTime.UtcNow;
            testObj!.WriteDailyAverage(siteId, monitorId, field, level, timestamp);

            var siteAverages = ReadSiteAverages(database!.ConnectionString);

            Assert.AreEqual(1, siteAverages.Count);
            var sa = siteAverages[0];

            Assert.IsNotNull(sa.Id);
            Assert.AreEqual(siteId, sa.SiteId);
            Assert.AreEqual(monitorId, sa.MonitorId);
            Assert.AreEqual(field, sa.Field);
            Assert.AreEqual(level, sa.Level);
            Assert.AreEqual(DateTimeUtil.TruncateMillis(timestamp), DateTimeUtil.TruncateMillis(sa.CollectionTime));

        }

        private static void InsertContact(NpgsqlConnection connection, Guid monitorId, ContactMethod contactMethod,
                                          string email, string phoneNo, Guid siteUserId, Guid siteId,
                                          DateTime? sendStartTime = null, DateTime? sendEndTime = null)
        {
            var contractId = Guid.NewGuid();
            var userId = Guid.NewGuid();

            {
                var sql = @"INSERT INTO contract
                                (id,
                                 contract_number,
                                 on_hire_date,
                                 off_hire_date,
                                 company_id)
                         VALUES (@Id,
                                 @ContractNumber,
                                 @OnHireDate,
                                 @OffHireDate,
                                 @CompanyId);";


                using NpgsqlCommand cmd = new(sql, connection);
                cmd.Parameters.AddWithValue("@Id", contractId);
                cmd.Parameters.AddWithValue("@ContractNumber", Guid.NewGuid().ToString("N"));
                cmd.Parameters.AddWithValue("@OnHireDate", DateTime.UtcNow);
                cmd.Parameters.AddWithValue("@OffHireDate", DateTime.UtcNow.AddDays(7));
                cmd.Parameters.AddWithValue("@CompanyId", Guid.NewGuid());
                cmd.ExecuteNonQuery();
            }
            {
                var sql = @"INSERT INTO deployment
                                (id,
                                 start_date,
                                 end_date,
                                 lng,
                                 lat,
                                 what_3_words,
                                 picture_link,
                                 contract_id,
                                 monitor_id)
                         VALUES (@Id,
                                 @StartDate,
                                 @EndDate,
                                 @Lng,
                                 @Lat,
                                 @What3Words,
                                 @PictureLink,
                                 @ContractId,
                                 @MonitorId);";


                using NpgsqlCommand cmd = new(sql, connection);
                cmd.Parameters.AddWithValue("@Id", siteId);
                cmd.Parameters.AddWithValue("@StartDate", DateTime.UtcNow);
                cmd.Parameters.Add("@EndDate", NpgsqlDbType.TimestampTz).Value = DBNull.Value;
                cmd.Parameters.AddWithValue("@Lng", 12.23f);
                cmd.Parameters.AddWithValue("@Lat", 45.67f);
                cmd.Parameters.AddWithValue("@What3Words", "fixture.words.here");
                cmd.Parameters.AddWithValue("@PictureLink", "somelink");
                cmd.Parameters.AddWithValue("@ContractId", contractId);
                cmd.Parameters.AddWithValue("@MonitorId", monitorId);
                cmd.ExecuteNonQuery();
            }

            // update Contracts with SiteId
            {
                var sql = @"UPDATE contract SET site_id = @SiteId WHERE id = @ContractId;";
                using var cmd = new NpgsqlCommand(sql, connection);
                cmd.Parameters.AddWithValue("@SiteId", siteId);
                cmd.Parameters.AddWithValue("@ContractId", contractId);
                cmd.ExecuteNonQuery();
            }
            {
                var sql = @"INSERT INTO ""AspNetUsers""
                               (""Id"",
                                is_disabled,
                                ""Email"",
                                normalized_email,
                                email_confirmed,
                                ""PhoneNumber"",
                                phone_number_confirmed,
                                two_factor_enabled,
                                lockout_enabled,
                                access_failed_count)
                         VALUES (@Id,
                                 @IsDisabled,
                                 @Email,
                                 @NormalizedEmail,
                                 @EmailConfirmed,
                                 @PhoneNumber,
                                 @PhoneNumberConfirmed,
                                 @TwoFactorEnabled,
                                 @LockoutEnabled,
                                 @AccessFailedCount
                                );";

                using NpgsqlCommand cmd = new(sql, connection);
                cmd.Parameters.AddWithValue("@Id", userId.ToString());
                cmd.Parameters.AddWithValue("@IsDisabled", false);
                cmd.Parameters.AddWithValue("@Email", email);
                cmd.Parameters.AddWithValue("@NormalizedEmail", email);
                cmd.Parameters.AddWithValue("@EmailConfirmed", true);
                cmd.Parameters.AddWithValue("@PhoneNumber", phoneNo);
                cmd.Parameters.AddWithValue("@PhoneNumberConfirmed", true);
                cmd.Parameters.AddWithValue("@TwoFactorEnabled", false);
                cmd.Parameters.AddWithValue("@LockoutEnabled", false);
                cmd.Parameters.AddWithValue("@AccessFailedCount", 0);

                cmd.ExecuteNonQuery();
            }

            {
                var sql = @"INSERT INTO site_user
                                (id,
                                 start_date,
                                 user_id,
                                 site_id)
                         VALUES (@Id,
                                 @StartDate,
                                 @UserId,
                                 @SiteId);";
                using NpgsqlCommand cmd = new(sql, connection);
                cmd.Parameters.AddWithValue("@Id", siteUserId);
                cmd.Parameters.AddWithValue("@StartDate", DateTime.UtcNow.AddDays(-7));
                cmd.Parameters.AddWithValue("@UserId", userId);
                cmd.Parameters.AddWithValue("@SiteId", siteId);

                cmd.ExecuteNonQuery();
            }

            InsertSite(connection: connection,
                       siteId: siteId,
                       startTime: TimeSpan.Parse("09:00:00"),
                       endTime: TimeSpan.Parse("17:00:00"),
                       satStartTime: TimeSpan.Parse("09:30:00"),
                       satEndTime: TimeSpan.Parse("12:30:00"),
                       sunStartTime: TimeSpan.Parse("10:00:00"),
                       sunEndTime: TimeSpan.Parse("11:00:00")
                       );
            {
                var sql = @"INSERT INTO notification_setting
                                (id,
                                 site_user_id,
                                 email,
                                 sms,
                                 start_time,
                                 end_time)
                         VALUES (@Id,
                                 @SiteUserId,
                                 @Email,
                                 @SMS,
                                 @StartTime,
                                 @EndTime);";
                using NpgsqlCommand cmd = new(sql, connection);
                cmd.Parameters.AddWithValue("@Id", Guid.NewGuid());
                cmd.Parameters.AddWithValue("@SiteUserId", siteUserId);
                cmd.Parameters.AddWithValue("@Email", contactMethod == ContactMethod.Email || contactMethod == ContactMethod.SMSAndEmail);
                cmd.Parameters.AddWithValue("@SMS", contactMethod == ContactMethod.SMS || contactMethod == ContactMethod.SMSAndEmail);
                cmd.Parameters.Add("@StartTime", NpgsqlDbType.Time).Value =
                    sendStartTime?.TimeOfDay ?? (object)DBNull.Value;
                cmd.Parameters.Add("@EndTime", NpgsqlDbType.Time).Value =
                    sendEndTime?.TimeOfDay ?? (object)DBNull.Value;

                cmd.ExecuteNonQuery();
            }
        }

        private static void InsertSite(NpgsqlConnection connection, Guid siteId,
                                       TimeSpan? startTime, TimeSpan? endTime,
                                       TimeSpan? satStartTime, TimeSpan? satEndTime,
                                       TimeSpan? sunStartTime, TimeSpan? sunEndTime)
        {
            var sql = @"INSERT INTO site
                                (id,
                                 site_name,
                                 create_date,
                                 start_time,
                                 end_time,
                                 sat_start_time,
                                 sat_end_time,
                                 sun_start_time,
                                 sun_end_time)
                         VALUES (@Id,
                                 @SiteName,
                                 @CreateDate,
                                 @StartTime,
                                 @EndTime,
                                 @SatStartTime,
                                 @SatEndTime,
                                 @SunStartTime,
                                 @SunEndTime);";
            using NpgsqlCommand cmd = new(sql, connection);
            cmd.Parameters.AddWithValue("@Id", siteId);
            cmd.Parameters.AddWithValue("@SiteName", "My test site");
            cmd.Parameters.AddWithValue("@CreateDate", DateTime.UtcNow.AddDays(-3));
            cmd.Parameters.Add("@StartTime", NpgsqlDbType.Time).Value = startTime ?? (object)DBNull.Value;
            cmd.Parameters.Add("@EndTime", NpgsqlDbType.Time).Value = endTime ?? (object)DBNull.Value;
            cmd.Parameters.Add("@SatStartTime", NpgsqlDbType.Time).Value = satStartTime ?? (object)DBNull.Value;
            cmd.Parameters.Add("@SatEndTime", NpgsqlDbType.Time).Value = satEndTime ?? (object)DBNull.Value;
            cmd.Parameters.Add("@SunStartTime", NpgsqlDbType.Time).Value = sunStartTime ?? (object)DBNull.Value;
            cmd.Parameters.Add("@SunEndTime", NpgsqlDbType.Time).Value = sunEndTime ?? (object)DBNull.Value;

            cmd.ExecuteNonQuery();


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


        private static List<SiteAverage> ReadSiteAverages(string connectionString)
        {


            using var connection = new NpgsqlConnection(connectionString);
            connection.Open();
            var sql = @"SELECT id, site_id, monitor_id, level, field, collection_time FROM site_average";

            using NpgsqlCommand cmd = new(sql, connection);

            using NpgsqlDataReader reader = cmd.ExecuteReader();

            var siteAverages = new List<SiteAverage>();
            while (reader.Read())
            {

                var sa = new SiteAverage();
                sa.Id = reader.GetGuid(0);
                sa.SiteId = reader.GetGuid(1);
                sa.MonitorId = reader.GetGuid(2);
                sa.Level = reader.GetDouble(3);
                sa.Field = reader.GetString(4);
                sa.CollectionTime = reader.GetDateTime(5);

                siteAverages.Add(sa);
            }
            return siteAverages;
        }


        private static ContactMethod ReadContactMethod(string connectionString, Guid siteUserId)
        {

            using var connection = new NpgsqlConnection(connectionString);
            connection.Open();
            var sql = @"SELECT email, sms FROM notification_setting WHERE site_user_id = @SiteUserId";

            using NpgsqlCommand cmd = new(sql, connection);
            cmd.Parameters.AddWithValue("@SiteUserId", siteUserId);

            using NpgsqlDataReader reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var email = reader.GetBoolean(0);
                var sms = reader.GetBoolean(1);
                return RvtContactDto.FromFlags(email, sms);
            }
            throw AdapterException.Of("Failed to ReadContactMethod");
        }

        private static List<RvtContactDto> ReadContacts(NpgsqlConnection connection, Guid siteUserId)
        {
            var sql = @"SELECT ""Email"", ""PhoneNumber"", ""Id"" FROM ""AspNetUsers""";
            using NpgsqlCommand cmd = new(sql, connection);
            var contacts = new List<RvtContactDto>();
            using NpgsqlDataReader reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                var emailAddress = reader.GetString(0);
                var phoneNumber = reader.IsDBNull(1) ? null : reader.GetString(1);
                var id = reader.GetString(2);
                var contactMethod = ReadContactMethod(database!.ConnectionString, siteUserId);
                contacts.Add(new RvtContactDto(contactMethod: contactMethod,
                                               emailAddress: emailAddress,
                                               phoneNumber: phoneNumber,
                                               sendStartTime: null,
                                               sendEndTime: null));
            }
            return contacts;
        }

        private static List<NotificationDto> ReadNotifications(NpgsqlConnection connection)
        {

            var sql = @"SELECT id, notification_time, limit_on, averaging_period, level, closed_time,
                               closed_by_user, alert_type, alert_field, monitor_id
                        FROM notification";
            using NpgsqlCommand cmd = new(sql, connection);
            var alerts = new List<NotificationDto>();
            using NpgsqlDataReader reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                var id = reader.GetGuid(0);
                var notificationTime = reader.GetDateTime(1);
                var limitOn = reader.GetDouble(2);
                var averagingPeriod = reader.GetInt32(3);
                var level = reader.GetDouble(4);
                DateTime? closedTime = reader.IsDBNull(5) ? null : reader.GetDateTime(5);
                Guid? closedByUser = reader.IsDBNull(6) ? null : reader.GetGuid(6);
                var alertType = (AlertType)reader.GetInt32(7);
                var alertField = reader.GetString(8);
                var monitorId = reader.GetGuid(9);

                var alert = new NotificationDto(id: id,
                                                notificationTime: notificationTime,
                                                limitOn: limitOn,
                                                averagingPeriod: averagingPeriod,
                                                level: level,
                                                closedTime: closedTime,
                                                closedByUser: closedByUser,
                                                alertType: alertType,
                                                alertField: alertField,
                                                monitorId: monitorId);
                alerts.Add(alert);
            }
            return alerts;
        }

        private static List<Dictionary<string, object>> ReadNotificationsSent(NpgsqlConnection connection)
        {

            var sql = @"SELECT id, send_time, address, error_message, notification_id
                        FROM notification_sent";
            using NpgsqlCommand cmd = new(sql, connection);
            var audits = new List<Dictionary<string, object>>();
            using NpgsqlDataReader reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                var dict = new Dictionary<string, object>();

                dict["Id"] = reader.GetGuid(0);
                dict["SendTime"] = reader.GetDateTime(1);
                dict["Address"] = reader.GetString(2);
                dict["ErrorMessage"] = reader.GetString(3);
                dict["NotificationId"] = reader.GetGuid(4);
                audits.Add(dict);
            }
            return audits;
        }

        private static List<NoiseDto> ReadNoiseDtos(NpgsqlConnection connection, out string serialId)
        {
            serialId = "";
            var sql = @"SELECT serial_id, sample_time, laeq, lamax, la_90, la_10, lceq, lcmax, lc_90, lc_10
                        FROM svantek_noise_level";
            using NpgsqlCommand cmd = new(sql, connection);
            var dtos = new List<NoiseDto>();
            using NpgsqlDataReader reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                serialId = reader.GetString(0);
                var sampleTime = reader.GetDateTime(1);
                var lAeq = reader.GetDouble(2);
                var lAmax = reader.GetDouble(3);
                var lA90 = reader.GetDouble(4);
                var lA10 = reader.GetDouble(5);
                var lCeq = reader.GetDouble(6);
                var lCmax = reader.GetDouble(7);
                var lC90 = reader.GetDouble(8);
                var lC10 = reader.GetDouble(9);

                var dto = new NoiseDto(sampleTime: sampleTime, lAeq: lAeq, lAmax: lAmax, lA90: lA90,
                        lA10: lA10, lCeq: lCeq, lCmax: lCmax, lC90: lC90, lC10: lC10);
                dtos.Add(dto);
            }

            return dtos;
        }
    }
}
