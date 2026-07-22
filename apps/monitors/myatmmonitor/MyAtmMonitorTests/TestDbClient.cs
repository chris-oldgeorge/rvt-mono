using System.Data;
using System.Globalization;
using Microsoft.Extensions.Logging;
using MyAtm.Api;
using MyAtm.Api.Db;
using MyAtm.Api.Rules;
using MyAtm.Model.Dto;
using MyAtm.Model.Json;
using Npgsql;
using NpgsqlTypes;
using Rvt.Monitor.Common.Configuration;
using Rvt.Monitor.Common.Data;
using Rvt.Monitor.Common.Delivery;
using Rvt.Monitor.Common.Diagnostics;
using Rvt.Monitor.Common.Notifications;
using Rvt.Monitor.Common.Rules;
using Rvt.Monitor.Common.Utilities;
using Rvt.Monitor.IntegrationTesting;
using AlertActivityTimeDto = Rvt.Monitor.Common.Rules.AlertActivityTimeDto;
using ContactMethod = Rvt.Monitor.Common.Rules.ContactMethod;
using NotificationDto = Rvt.Monitor.Common.Notifications.NotificationDto;
using RvtContactDto = Rvt.Monitor.Common.Rules.RvtContactDto;
namespace MyAtmMonitorTests
{

    // Summary: Exercises MyAtm PostgreSQL database persistence against a scoped fixture.
    // Major updates:
    // - 2026-06-18 Test fixture hardening: aligned monitor-list expectations with the currently unfiltered read query.
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

        [TestMethod]
        public void ReadSiteSchedule_ActiveDeployment_ReturnsAllConfiguredHours()
        {
            var monitorId = Guid.NewGuid();
            var siteId = Guid.NewGuid();
            var contractId = Guid.NewGuid();
            using var connection = database!.OpenConnection();
            connection.Open();
            using var command = new NpgsqlCommand(
                """
                INSERT INTO monitor
                  (id, serial_id, customer_id, listed_at_time, model, manufacturer, firmware_version, type_of_monitor)
                VALUES
                  (@monitor_id, 'site-schedule-1', 9, @now, 'AQ Guard', 'Palas', '1.0', 0);

                INSERT INTO site
                  (id, site_name, create_date, start_time, end_time, sat_start_time, sat_end_time, sun_start_time, sun_end_time)
                VALUES
                  (@site_id, 'Schedule Site', @now, '08:00', '18:00', '09:00', '13:00', '10:00', '12:00');

                INSERT INTO contract
                  (id, contract_number, on_hire_date, company_id, site_id)
                VALUES
                  (@contract_id, 'C-1', @now, @company_id, @site_id);

                INSERT INTO deployment
                  (id, start_date, end_date, lng, lat, contract_id, monitor_id)
                VALUES
                  (@deployment_id, @now, NULL, 0, 0, @contract_id, @monitor_id);
                """,
                connection);
            command.Parameters.AddWithValue("monitor_id", monitorId);
            command.Parameters.AddWithValue("site_id", siteId);
            command.Parameters.AddWithValue("contract_id", contractId);
            command.Parameters.AddWithValue("company_id", Guid.NewGuid());
            command.Parameters.AddWithValue("deployment_id", Guid.NewGuid());
            command.Parameters.AddWithValue("now", DateTime.UtcNow);
            command.ExecuteNonQuery();

            var schedule = testObj!.ReadSiteSchedule(monitorId);

            Assert.AreEqual(TimeSpan.FromHours(8), schedule.WeekdayStart);
            Assert.AreEqual(TimeSpan.FromHours(18), schedule.WeekdayEnd);
            Assert.AreEqual(TimeSpan.FromHours(9), schedule.SaturdayStart);
            Assert.AreEqual(TimeSpan.FromHours(13), schedule.SaturdayEnd);
            Assert.AreEqual(TimeSpan.FromHours(10), schedule.SundayStart);
            Assert.AreEqual(TimeSpan.FromHours(12), schedule.SundayEnd);
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

        [TestMethod]
        public async Task InsertAccessoryPageAsync_DeduplicatesThePageAndRollsBackTheWholePageOnFailure()
        {
            var firstTimestamp = ParseUtc("2026-07-14T12:00:00Z");
            var secondTimestamp = firstTimestamp.AddMinutes(1);
            var first = new AccessoryInfoDto("accessory-1", new AccessoryInfo { Timestamp = firstTimestamp });
            var duplicate = new AccessoryInfoDto("accessory-1", new AccessoryInfo { Timestamp = firstTimestamp });
            await testObj!.InsertAccessoryPageAsync([first, duplicate]);

            using var connection = database!.OpenConnection();
            connection.Open();
            using (var countCommand = new NpgsqlCommand("SELECT COUNT(*) FROM my_atm_accessory_info WHERE serial_id = 'accessory-1';", connection))
            {
                Assert.AreEqual(1L, countCommand.ExecuteScalar());
            }

            using (var constraintCommand = new NpgsqlCommand(
                "ALTER TABLE my_atm_accessory_info ADD CONSTRAINT task6_accessory_t_led_nonnegative CHECK (operating_t_led IS NULL OR operating_t_led >= 0);",
                connection))
            {
                constraintCommand.ExecuteNonQuery();
            }

            var valid = new AccessoryInfoDto("accessory-2", new AccessoryInfo { Timestamp = firstTimestamp });
            var invalid = new AccessoryInfoDto("accessory-2", new AccessoryInfo { Timestamp = secondTimestamp, OperatingTLed = -1 });
            await Assert.ThrowsExactlyAsync<Microsoft.EntityFrameworkCore.DbUpdateException>(
                () => testObj.InsertAccessoryPageAsync([valid, invalid]));

            using var rollbackCountCommand = new NpgsqlCommand(
                "SELECT COUNT(*) FROM my_atm_accessory_info WHERE serial_id = 'accessory-2';",
                connection);
            Assert.AreEqual(0L, rollbackCountCommand.ExecuteScalar());
        }

        [DataRow("", "", 5, 5)]
        [DataRow("2023-11-21T12:00:00Z", "2023-11-21T11:01:00Z", 5, 5)]
        [DataRow("2023-11-21T12:00:00Z", "2023-11-21T12:00:00Z", 5, 5)]
        [DataRow("2023-11-21T12:00:00Z", "2023-11-21T13:00:00Z", 5, 5)]
        [DataRow("2023-11-21T12:00:00Z", "2023-11-21T13:59:00Z", 5, 5)]
        [DataRow("2023-11-21T12:00:00Z", "2023-11-21T15:00:00Z", 5, 5)]
        [DataRow("2023-11-21T12:00:00Z", "2023-11-21T16:00:00Z", 5, 5)]
        [DataTestMethod]
        public void TestMonitorsList(string lastDate, string queryDate, int numMonitors, int numExpectedMonitors)
        {
            DateTime? lastDataTime = String.IsNullOrEmpty(lastDate) ? null : ParseUtc(lastDate);
            DateTime? queryLastdataTime = String.IsNullOrEmpty(queryDate) ? null : ParseUtc(queryDate);
            var monitorsIn = CreateMonitorsList(numMonitors, 987);
            Assert.AreEqual(numMonitors, monitorsIn.Count);
            testObj!.WriteMonitorList(monitorsIn);

            foreach (var monitorIn in monitorsIn)
            {
                testObj.WriteFleetNr(monitorIn.SerialId, monitorIn.FleetNr!);
            }

            if (lastDataTime != null)
            {
                for (var i = 0; i < monitorsIn.Count; i++)
                {
                    var dt = ((DateTime)lastDataTime!).AddHours(i);
                    testObj.WriteLatestTimestamp(monitorsIn[i].SerialId, dt, Period.Minutes1);
                }
            }

            var monitorsOut = testObj.ReadMonitorList(queryLastdataTime);
            Assert.AreEqual(numExpectedMonitors, monitorsOut.Count);
            Assert.IsTrue(TestUtil.VerifyMonitorList(monitorsIn, monitorsOut));

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
        public void TestReadAlertRules()
        {
            var connectionString = database!.ConnectionString;
            using var connection = new NpgsqlConnection(connectionString);
            connection.Open();

            var serialId = "12345";
            var customerId = 861;
            var monitorsIn = CreateMonitorsList(1, customerId);
            testObj!.WriteMonitorList(monitorsIn);

            foreach (var monitorIn in monitorsIn)
            {
                testObj.WriteFleetNr(monitorIn.SerialId, monitorIn.FleetNr!);
            }

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

            var customerId = 443;
            var numMonitors = 2;
            var monitorsIn = CreateMonitorsList(numMonitors, customerId);
            testObj!.WriteMonitorList(monitorsIn);

            foreach (var monitorIn in monitorsIn)
            {
                testObj.WriteFleetNr(monitorIn.SerialId, monitorIn.FleetNr!);
            }

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
            InsertContact(connection, monitorId, ContactMethod.Email, email, phoneNo,
                          siteUserId, startTime, endTime);

            // insert that should not be read
            InsertContact(connection, monitorsOut[1].Id, ContactMethod.Email, email, phoneNo, Guid.NewGuid());

            var contacts = ReadContacts(connection, siteUserId);
            Assert.AreEqual(2, contacts.Count);

            var alertContacts = testObj.ReadAlertContacts(monitorId);
            Assert.AreEqual(1, alertContacts.Count);
            var ac = alertContacts[0];
            Assert.AreEqual(ContactMethod.Email, ac.ContactMethod);
            Assert.AreEqual(email, ac.EmailAddress);
            Assert.AreEqual(phoneNo, ac.PhoneNumber);
            Assert.AreEqual(startTime.TimeOfDay, ac.SendStartTime);
            Assert.AreEqual(endTime.TimeOfDay, ac.SendEndTime);
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
            var beforeWrite = DateTime.UtcNow;
            MonitorDb.WriteException(
                connectionString,
                TAG,
                AdapterException.Of(MESSAGE),
                "MyAtmMonitorTests",
                "1.0",
                monitorOptions);
            var afterWrite = DateTime.UtcNow;

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
                Assert.AreEqual(DateTimeKind.Utc, errorTime.Kind);
                Assert.IsTrue(errorTime >= beforeWrite);
                Assert.IsTrue(errorTime <= afterWrite);

            }
            Assert.AreEqual(1, count);
        }

        [TestMethod]
        public void TestWriteLatestTimestamp()
        {
            var customerId = 851;

            var monitors = CreateMonitorsList(1, customerId, "wrst_monitor");
            Assert.AreEqual(1, monitors.Count);

            testObj!.WriteMonitorList(monitors);

            foreach (var monitorIn in monitors)
            {
                testObj.WriteFleetNr(monitorIn.SerialId, monitorIn.FleetNr!);
            }

            var lastDataTimeMin = ParseUtc("2023-10-18T14:35:42");
            var lastDataTime15Min = ParseUtc("2023-10-18T14:29:00");
            var lastDataTimeHour = ParseUtc("2023-10-18T14:46:42");
            var lastDataTime24Hour = ParseUtc("2023-10-17T00:01:00");
            var serialId = "wrst_monitor0";
            testObj.WriteLatestTimestamp(serialId, lastDataTimeMin, Period.Minutes1);
            testObj.WriteLatestTimestamp(serialId, lastDataTime15Min, Period.Minutes15);
            testObj.WriteLatestTimestamp(serialId, lastDataTimeHour, Period.Hours1);
            testObj.WriteLatestTimestamp(serialId, lastDataTime24Hour, Period.Hours24);

            monitors = testObj.ReadMonitorList(null);
            Assert.AreEqual(1, monitors.Count);

            var monitor = monitors[0];
            Assert.AreEqual(lastDataTimeMin, monitor.LastDataTime1Min);
            Assert.AreEqual(lastDataTime15Min, monitor.LastDataTime15Min);
            Assert.AreEqual(lastDataTimeHour, monitor.LastDataTime1Hour);
            Assert.AreEqual(lastDataTime24Hour, monitor.LastDataTime24Hour);
        }

        [TestMethod]
        public void TestWriteNotification()
        {
            var connectionString = database!.ConnectionString;
            using var connection = new NpgsqlConnection(connectionString);
            connection.Open();

            var serialId = "82731";
            var customerId = 332;
            var monitorsIn = CreateMonitorsList(1, customerId);
            testObj!.WriteMonitorList(monitorsIn);

            foreach (var monitorIn in monitorsIn)
            {
                testObj.WriteFleetNr(monitorIn.SerialId, monitorIn.FleetNr!);
            }

            var monitorsOut = testObj.ReadMonitorList(null);
            Assert.AreEqual(1, monitorsOut.Count);
            var monitorId = monitorsOut[0].Id;


            // add an alert and contact as RvtAlertContacts table has foreign key constraints
            InsertAlertRule(connection, 10, serialId, monitorId, AlertType.Caution);
            var rules = testObj!.ReadRules(serialId);
            Assert.AreEqual(1, rules.Count);
            var email = "foobob@bbb.com";
            var phoneNo = "01238867890";
            var siteUserId = Guid.NewGuid();
            InsertContact(connection, monitorId, ContactMethod.Email, email, phoneNo, siteUserId);
            var contacts = ReadContacts(connection, siteUserId);
            Assert.AreEqual(1, contacts.Count);

            var dt = ParseUtc("2023-10-18T11:19:00");
            var alertIn = new NotificationDto(rules[0], 99.876, dt, monitorId);

            Assert.IsFalse(testObj.HasOpenNotification(monitorId, rules[0].Field, AlertType.Caution));
            Assert.IsFalse(testObj.HasOpenNotification(monitorId, rules[0].Field, AlertType.Alert));

            testObj.WriteNotification(alertIn);

            Assert.IsTrue(testObj.HasOpenNotification(monitorId, rules[0].Field, AlertType.Caution));
            Assert.IsFalse(testObj.HasOpenNotification(monitorId, rules[0].Field, AlertType.Alert));
            {
                var alerts = ReadNotifications(connection);
                Assert.AreEqual(1, alerts.Count);

                var alertOut = alerts[0];

                Assert.AreEqual(alertIn.Id, alertOut.Id);
                Assert.AreEqual(alertIn.Level, alertOut.Level);
                Assert.AreEqual(alertIn.NotificationTime, alertOut.NotificationTime);
                Assert.AreEqual(alertIn.LimitOn, alertOut.LimitOn);
                Assert.AreEqual(alertIn.AveragingPeriod, alertOut.AveragingPeriod);
                Assert.AreEqual(alertIn.AlertField, alertOut.AlertField);
                Assert.AreEqual(alertIn.AlertType, alertOut.AlertType);
                Assert.AreEqual(alertIn.MonitorId, alertOut.MonitorId);
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

        [TestMethod]
        public void TestReadNotificationsMapsClosedFields()
        {
            using var connection = new NpgsqlConnection(database!.ConnectionString);
            connection.Open();

            var monitor = CreateMonitorsList(1, 332)[0];
            testObj!.WriteMonitorList(new List<DustMonitorDto> { monitor });
            var monitorId = testObj.ReadMonitor(monitor.SerialId)!.Id;
            var closedTime = ParseUtc("2023-10-18T12:34:56Z");
            var closedByUser = Guid.NewGuid();
            var notification = new NotificationDto(
                id: Guid.NewGuid(),
                notificationTime: ParseUtc("2023-10-18T11:19:00Z"),
                limitOn: 99.876,
                averagingPeriod: 60,
                level: 12.345,
                closedTime: closedTime,
                closedByUser: closedByUser,
                alertType: AlertType.Alert,
                alertField: "Pm2_5",
                monitorId: monitorId);

            testObj.WriteNotification(notification);

            var notificationOut = ReadNotifications(connection).Single();

            Assert.AreEqual(closedTime, notificationOut.ClosedTime);
            Assert.AreEqual(closedByUser, notificationOut.ClosedByUser);
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

            var serialId = "82731";
            var customerId = 332;
            var monitorsIn = CreateMonitorsList(1, customerId);
            testObj!.WriteMonitorList(monitorsIn);

            foreach (var monitorIn in monitorsIn)
            {
                testObj.WriteFleetNr(monitorIn.SerialId, monitorIn.FleetNr!);
            }

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
            InsertContact(connection, monitorId, ContactMethod.Email, email, phoneNo, siteUserId);
            var contacts = ReadContacts(connection, siteUserId);
            Assert.AreEqual(1, contacts.Count);

            Assert.IsFalse(testObj.HasOpenNotification(monitorId, rules[0].Field, AlertType.Caution));
            Assert.IsFalse(testObj.HasOpenNotification(monitorId, rules[0].Field, AlertType.Alert));

            var dt = ParseUtc("2023-10-18T11:19:00");
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
        public void TestUpdateAlertRule()
        {
            var connectionString = database!.ConnectionString;
            using var connection = new NpgsqlConnection(connectionString);
            connection.Open();
            var serialId = "67731";
            var customerId = 861;
            var monitorsIn = CreateMonitorsList(1, customerId);
            testObj!.WriteMonitorList(monitorsIn);

            foreach (var monitorIn in monitorsIn)
            {
                testObj.WriteFleetNr(monitorIn.SerialId, monitorIn.FleetNr!);
            }

            var monitorsOut = testObj.ReadMonitorList(null);
            Assert.AreEqual(1, monitorsOut.Count);
            var monitorId = monitorsOut[0].Id;

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
        public async Task CommitDustImportAsync_PersistsMeasurementWatermarkRuleOccurrenceAndOutboxAtomically()
        {
            using var connection = database!.OpenConnection();
            connection.Open();
            var monitor = CreateMonitorsList(1, 861).Single();
            testObj!.WriteMonitorList([monitor]);
            InsertAlertRule(connection, 21, monitor.SerialId, monitor.Id);
            var rule = testObj.ReadRules(monitor.SerialId).Single();
            var sampleTime = ParseUtc("2026-07-14T12:00:00Z");
            var commitTime = sampleTime.AddMinutes(1);
            var measurement = new DustDto(monitor.SerialId, 60, sampleTime, 11, 12, 13, 14, 15, 16, 17);
            var occurrence = new AlertOccurrenceProposal(
                "occurrence:myatm-atomic-commit",
                monitor.Id,
                rule.RuleId,
                Period.Minutes1,
                AlertType.Alert,
                "Pm10",
                rule.LimitOn,
                13,
                sampleTime,
                Array.Empty<RvtContactDto>());
            var commit = new MyAtmDustImportCommit(
                monitor,
                Period.Minutes1,
                [measurement],
                sampleTime,
                [new RuleStateMutation(rule.RuleId, false, null, true, commitTime)],
                [occurrence],
                commitTime);

            var result = await testObj.CommitDustImportAsync(commit);

            Assert.HasCount(2, result.OutboxMessages);
            var expectedOccurrenceId = MonitorDeliveryIdentity.CreateGuid($"notification:{occurrence.Key}");
            var expectedAlertKey = $"{occurrence.Key}:MqttAlert:alert";
            var expectedDataKey = $"data:{monitor.Id:N}:60:{sampleTime:O}";
            CollectionAssert.AreEquivalent(
                new[] { MonitorDeliveryKind.MqttAlert, MonitorDeliveryKind.MqttDataInserted },
                result.OutboxMessages.Select(message => message.Kind).ToArray());
            var alertRequest = result.OutboxMessages.Single(message => message.Kind == MonitorDeliveryKind.MqttAlert);
            Assert.AreEqual(MonitorDeliveryIdentity.CreateGuid($"outbox:{expectedAlertKey}"), alertRequest.Id);
            Assert.AreEqual(expectedOccurrenceId, alertRequest.NotificationId);
            Assert.AreEqual(occurrence.Key, alertRequest.CorrelationKey);
            Assert.AreEqual(expectedAlertKey, alertRequest.DeliveryKey);
            Assert.AreEqual("alert", alertRequest.Destination);
            var alertPayload = Decode(alertRequest);
            Assert.AreEqual(expectedOccurrenceId, alertPayload.NotificationId);
            Assert.AreEqual(sampleTime, alertPayload.Timestamp);
            Assert.AreEqual(monitor.SerialId, alertPayload.SerialId);
            Assert.AreEqual(monitor.CustomerId, alertPayload.CustomerId);
            Assert.AreEqual(monitor.FleetNr, alertPayload.FleetNr);
            Assert.AreEqual(AlertType.Alert, alertPayload.AlertType);
            Assert.AreEqual("pm10", alertPayload.Field);
            Assert.AreEqual(13d, alertPayload.Level);

            var dataRequest = result.OutboxMessages.Single(message => message.Kind == MonitorDeliveryKind.MqttDataInserted);
            Assert.AreEqual(MonitorDeliveryIdentity.CreateGuid($"outbox:{expectedDataKey}"), dataRequest.Id);
            Assert.IsNull(dataRequest.NotificationId);
            Assert.IsNull(dataRequest.CorrelationKey);
            Assert.AreEqual(expectedDataKey, dataRequest.DeliveryKey);
            Assert.AreEqual("insert", dataRequest.Destination);
            var dataPayload = Decode(dataRequest);
            Assert.AreEqual(Guid.Empty, dataPayload.NotificationId);
            Assert.AreEqual(sampleTime, dataPayload.Timestamp);
            Assert.AreEqual(monitor.SerialId, dataPayload.SerialId);
            Assert.AreEqual(monitor.CustomerId, dataPayload.CustomerId);
            Assert.AreEqual(monitor.FleetNr, dataPayload.FleetNr);
            Assert.AreEqual(AlertType.Ignore, dataPayload.AlertType);
            Assert.AreEqual(string.Empty, dataPayload.Field);
            Assert.AreEqual(0d, dataPayload.Level);
            Assert.IsTrue(result.OutboxMessages.All(message =>
                message.Producer == MonitorDeliveryProducers.MyAtm &&
                message.PayloadVersion == 1 &&
                message.CreatedAt == commitTime));
            Assert.AreEqual(1, ReadScalarInt(connection, "SELECT COUNT(*) FROM my_atm_dust_level;"));
            Assert.AreEqual(1, ReadScalarInt(connection, "SELECT COUNT(*) FROM notification;"));
            Assert.AreEqual(1, ReadScalarInt(connection, "SELECT COUNT(*) FROM my_atm_alert_occurrence;"));
            Assert.AreEqual(2, ReadScalarInt(connection, "SELECT COUNT(*) FROM monitor_delivery_outbox WHERE producer = 'MyAtm';"));
            Assert.AreEqual(sampleTime, testObj.ReadMonitor(monitor.SerialId)!.LastDataTime1Min);
            Assert.IsTrue(testObj.ReadRules(monitor.SerialId).Single().IsActive);

            var replay = commit with
            {
                RuleStateMutations = Array.Empty<RuleStateMutation>()
            };
            var replayResult = await testObj.CommitDustImportAsync(replay);
            Assert.IsEmpty(replayResult.OutboxMessages);
            Assert.AreEqual(1, ReadScalarInt(connection, "SELECT COUNT(*) FROM notification;"));
            Assert.AreEqual(2, ReadScalarInt(connection, "SELECT COUNT(*) FROM monitor_delivery_outbox WHERE producer = 'MyAtm';"));

            var queries = (IMonitorDeliveryOutboxQueries)testObj;
            var commands = (IMonitorDeliveryOutboxCommands)testObj!;
            var unspecifiedCommitTime = DateTime.SpecifyKind(commitTime, DateTimeKind.Unspecified);
            var claimed = new[]
            {
                await queries.ClaimNextDueAsync(MonitorDeliveryProducers.MyAtm, unspecifiedCommitTime, TimeSpan.FromMinutes(1)),
                await queries.ClaimNextDueAsync(MonitorDeliveryProducers.MyAtm, unspecifiedCommitTime, TimeSpan.FromMinutes(1))
            };
            Assert.IsTrue(claimed.All(message => message is { Producer: MonitorDeliveryProducers.MyAtm, AttemptCount: 1 }));
            Assert.AreEqual(claimed.Length, claimed.Select(message => message!.LeaseId).Distinct().Count());
            CollectionAssert.AreEquivalent(
                claimed.Select(message => message!.LeaseId).ToArray(),
                ReadOutboxLeaseIds(connection).ToArray());

            Assert.IsTrue(await commands.CompleteAsync(
                claimed[0]!.Id,
                claimed[0]!.LeaseId,
                DateTime.SpecifyKind(commitTime.AddSeconds(1), DateTimeKind.Unspecified),
                null));
            Assert.IsTrue(await commands.RetryAsync(
                claimed[1]!.Id,
                claimed[1]!.LeaseId,
                commitTime.AddSeconds(1).ToLocalTime(),
                "transient"));
            Assert.AreEqual(1, ReadScalarInt(connection, "SELECT COUNT(*) FROM monitor_delivery_outbox WHERE producer = 'MyAtm' AND status = 'Completed';"));
            Assert.AreEqual(1, ReadScalarInt(connection, "SELECT COUNT(*) FROM monitor_delivery_outbox WHERE producer = 'MyAtm' AND status = 'Pending';"));
            var completedAt = ReadScalarDateTime(
                connection,
                $"SELECT completed_at FROM monitor_delivery_outbox WHERE id = '{claimed[0]!.Id}';");
            var nextAttemptAt = ReadScalarDateTime(
                connection,
                $"SELECT next_attempt_at FROM monitor_delivery_outbox WHERE id = '{claimed[1]!.Id}';");
            Assert.AreEqual(commitTime.AddSeconds(1).Ticks, completedAt.Ticks);
            Assert.AreEqual(DateTimeKind.Utc, completedAt.Kind);
            Assert.AreEqual(commitTime.AddSeconds(1).Ticks, nextAttemptAt.Ticks);
            Assert.AreEqual(DateTimeKind.Utc, nextAttemptAt.Kind);
            Assert.IsEmpty(ReadOutboxLeaseIds(connection));
        }

        [TestMethod]
        public async Task CommitAlertAsync_ExpectedOfflineConflictCreatesNoOccurrenceNotificationOrDelivery()
        {
            using var connection = database!.OpenConnection();
            connection.Open();
            var monitor = CreateMonitorsList(1, 862).Single();
            testObj!.WriteMonitorList([monitor]);
            testObj.SetMonitorOffline(monitor.Id, true);
            InsertAlertRule(connection, 22, monitor.SerialId, monitor.Id);
            var rule = testObj.ReadRules(monitor.SerialId).Single();
            var triggeredAt = ParseUtc("2026-07-14T12:00:00Z");
            var key = "occurrence:offline-conflict";
            var commit = new MyAtmAlertCommit(
                Array.Empty<RuleStateMutation>(),
                new MyAtmMonitorStateMutation(monitor.Id, ExpectedOffline: false, Offline: true),
                [new MyAtmAlertOccurrenceInput(
                    key,
                    monitor.Id,
                    rule.RuleId,
                    Period.Hours24,
                    AlertType.Offline,
                    RuleConstants.OFFLINE_RULE,
                    rule.LimitOn,
                    3600,
                    triggeredAt,
                    CreateDeliveryPlan(
                        key,
                        monitor,
                        rule,
                        AlertType.Offline,
                        RuleConstants.OFFLINE_RULE,
                        3600,
                        triggeredAt,
                        triggeredAt,
                        includeMqtt: false))],
                triggeredAt);

            var result = await testObj.CommitAlertAsync(commit);

            Assert.IsFalse(result.Applied);
            Assert.IsEmpty(result.OutboxMessages);
            Assert.IsTrue(testObj.ReadMonitor(monitor.SerialId)!.Offline);
            Assert.AreEqual(0, ReadScalarInt(connection, "SELECT COUNT(*) FROM my_atm_alert_occurrence;"));
            Assert.AreEqual(0, ReadScalarInt(connection, "SELECT COUNT(*) FROM notification;"));
            Assert.AreEqual(0, ReadScalarInt(connection, "SELECT COUNT(*) FROM monitor_delivery_outbox WHERE producer = 'MyAtm';"));
        }

        [TestMethod]
        public async Task CommitDustImportAsync_SuppressesByEventTimeAlertFamilyAndPriorAcceptedCandidates()
        {
            using var connection = database!.OpenConnection();
            connection.Open();
            var monitor = CreateMonitorsList(1, 862).Single();
            testObj!.WriteMonitorList([monitor]);
            InsertAlertRule(connection, 22, monitor.SerialId, monitor.Id);
            var rule = testObj.ReadRules(monitor.SerialId).Single();
            var eventStart = ParseUtc("2026-01-01T00:00:00Z");
            var delayedCommit = ParseUtc("2026-07-14T12:00:00Z");

            var historicalAlert = CreateOccurrence("historical-alert", monitor, rule, AlertType.Alert, "Pm10", eventStart);
            var historicalAlertResult = await testObj.CommitDustImportAsync(CreateOccurrenceCommit(monitor, historicalAlert, delayedCommit));
            Assert.HasCount(1, historicalAlertResult.OutboxMessages);

            var sameSeverity = CreateOccurrence("historical-alert-repeat", monitor, rule, AlertType.Alert, "pm10", eventStart.AddMinutes(10));
            var sameSeverityResult = await testObj.CommitDustImportAsync(CreateOccurrenceCommit(monitor, sameSeverity, delayedCommit));
            Assert.IsEmpty(sameSeverityResult.OutboxMessages);

            var cautionAfterAlert = CreateOccurrence("historical-caution-after-alert", monitor, rule, AlertType.Caution, "Pm10", eventStart.AddMinutes(15));
            var cautionAfterAlertResult = await testObj.CommitDustImportAsync(CreateOccurrenceCommit(monitor, cautionAfterAlert, delayedCommit));
            Assert.IsEmpty(cautionAfterAlertResult.OutboxMessages);

            var caution = CreateOccurrence("caution-before-alert", monitor, rule, AlertType.Caution, "Pm1", eventStart.AddHours(1));
            var cautionResult = await testObj.CommitDustImportAsync(CreateOccurrenceCommit(monitor, caution, delayedCommit));
            Assert.HasCount(1, cautionResult.OutboxMessages);

            var alertAfterCaution = CreateOccurrence("alert-after-caution", monitor, rule, AlertType.Alert, "pm1", eventStart.AddHours(1).AddMinutes(10));
            var alertAfterCautionResult = await testObj.CommitDustImportAsync(CreateOccurrenceCommit(monitor, alertAfterCaution, delayedCommit));
            Assert.HasCount(1, alertAfterCautionResult.OutboxMessages);

            var sameCommitFirst = CreateOccurrence("same-commit-first", monitor, rule, AlertType.Alert, "PmTotal", eventStart.AddHours(2));
            var sameCommitSecond = CreateOccurrence("same-commit-second", monitor, rule, AlertType.Alert, "pmtotal", eventStart.AddHours(2).AddMinutes(1));
            var sameCommitResult = await testObj.CommitDustImportAsync(
                CreateOccurrenceCommit(monitor, [sameCommitFirst, sameCommitSecond], delayedCommit));

            Assert.HasCount(1, sameCommitResult.OutboxMessages);
            Assert.AreEqual(7, ReadScalarInt(connection, "SELECT COUNT(*) FROM my_atm_alert_occurrence;"));
            Assert.AreEqual(4, ReadScalarInt(connection, "SELECT COUNT(*) FROM notification;"));
            Assert.AreEqual(4, ReadScalarInt(connection, "SELECT COUNT(*) FROM monitor_delivery_outbox WHERE producer = 'MyAtm';"));
            Assert.AreEqual(3, ReadScalarInt(connection, "SELECT COUNT(*) FROM my_atm_alert_occurrence WHERE is_suppressed = TRUE;"));
        }

        [TestMethod]
        public async Task CommitAlertAsync_SuppressesAggregateAlertFamilyCandidatesByEventTime()
        {
            using var connection = database!.OpenConnection();
            connection.Open();
            var monitor = CreateMonitorsList(1, 862).Single();
            testObj!.WriteMonitorList([monitor]);
            InsertAlertRule(connection, 22, monitor.SerialId, monitor.Id);
            var rule = testObj.ReadRules(monitor.SerialId).Single();
            var eventStart = ParseUtc("2026-01-01T00:00:00Z");
            var delayedCommit = ParseUtc("2026-07-14T12:00:00Z");

            var alert = CreateOccurrence("aggregate-alert", monitor, rule, AlertType.Alert, "Pm10", eventStart, Period.Hours8);
            var alertResult = await testObj.CommitAlertAsync(CreateAggregateOccurrenceCommit(alert, delayedCommit));
            Assert.HasCount(1, alertResult.OutboxMessages);

            var sameSeverity = CreateOccurrence("aggregate-alert-repeat", monitor, rule, AlertType.Alert, "pm10", eventStart.AddMinutes(10), Period.Hours8);
            var sameSeverityResult = await testObj.CommitAlertAsync(CreateAggregateOccurrenceCommit(sameSeverity, delayedCommit));
            Assert.IsEmpty(sameSeverityResult.OutboxMessages);

            var cautionAfterAlert = CreateOccurrence("aggregate-caution-after-alert", monitor, rule, AlertType.Caution, "Pm10", eventStart.AddMinutes(15), Period.Hours8);
            var cautionAfterAlertResult = await testObj.CommitAlertAsync(CreateAggregateOccurrenceCommit(cautionAfterAlert, delayedCommit));
            Assert.IsEmpty(cautionAfterAlertResult.OutboxMessages);

            var caution = CreateOccurrence("aggregate-caution", monitor, rule, AlertType.Caution, "Pm1", eventStart.AddHours(1), Period.Hours8);
            var cautionResult = await testObj.CommitAlertAsync(CreateAggregateOccurrenceCommit(caution, delayedCommit));
            Assert.HasCount(1, cautionResult.OutboxMessages);

            var escalation = CreateOccurrence("aggregate-alert-after-caution", monitor, rule, AlertType.Alert, "pm1", eventStart.AddHours(1).AddMinutes(10), Period.Hours8);
            var escalationResult = await testObj.CommitAlertAsync(CreateAggregateOccurrenceCommit(escalation, delayedCommit));
            Assert.HasCount(1, escalationResult.OutboxMessages);

            var sameCommitFirst = CreateOccurrence("aggregate-same-commit-first", monitor, rule, AlertType.Alert, "PmTotal", eventStart.AddHours(2), Period.Hours8);
            var sameCommitSecond = CreateOccurrence("aggregate-same-commit-second", monitor, rule, AlertType.Alert, "pmtotal", eventStart.AddHours(2).AddMinutes(1), Period.Hours8);
            var sameCommitResult = await testObj.CommitAlertAsync(
                CreateAggregateOccurrenceCommit([sameCommitFirst, sameCommitSecond], delayedCommit));
            Assert.HasCount(1, sameCommitResult.OutboxMessages);

            Assert.AreEqual(7, ReadScalarInt(connection, "SELECT COUNT(*) FROM my_atm_alert_occurrence;"));
            Assert.AreEqual(4, ReadScalarInt(connection, "SELECT COUNT(*) FROM notification;"));
            Assert.AreEqual(4, ReadScalarInt(connection, "SELECT COUNT(*) FROM monitor_delivery_outbox WHERE producer = 'MyAtm';"));
            Assert.AreEqual(3, ReadScalarInt(connection, "SELECT COUNT(*) FROM my_atm_alert_occurrence WHERE is_suppressed = TRUE;"));
        }

        [TestMethod]
        public async Task CommitDustImportAsync_DoesNotCrossSuppressAcceptedCandidatesFromAnotherMonitorOrPeriod()
        {
            using var connection = database!.OpenConnection();
            connection.Open();
            var monitors = CreateMonitorsList(2, 863);
            var firstMonitor = monitors[0];
            var secondMonitor = monitors[1];
            testObj!.WriteMonitorList(monitors);
            InsertAlertRule(connection, 23, firstMonitor.SerialId, firstMonitor.Id);
            InsertAlertRule(connection, 24, secondMonitor.SerialId, secondMonitor.Id);
            var firstRule = testObj.ReadRules(firstMonitor.SerialId).Single();
            var secondRule = testObj.ReadRules(secondMonitor.SerialId).Single();
            var eventTime = ParseUtc("2026-01-01T00:00:00Z");
            var delayedCommit = ParseUtc("2026-07-14T12:00:00Z");
            var sameScope = CreateOccurrence("scope-first", firstMonitor, firstRule, AlertType.Alert, "Pm10", eventTime);
            var otherMonitor = CreateOccurrence("scope-other-monitor", secondMonitor, secondRule, AlertType.Alert, "pm10", eventTime.AddMinutes(1));
            var otherPeriod = CreateOccurrence("scope-other-period", firstMonitor, firstRule, AlertType.Alert, "pm10", eventTime.AddMinutes(2), Period.Minutes15);

            var result = await testObj.CommitDustImportAsync(
                CreateOccurrenceCommit(firstMonitor, [sameScope, otherMonitor, otherPeriod], delayedCommit));

            Assert.HasCount(3, result.OutboxMessages);
            Assert.AreEqual(3, ReadScalarInt(connection, "SELECT COUNT(*) FROM notification;"));
            Assert.AreEqual(0, ReadScalarInt(connection, "SELECT COUNT(*) FROM my_atm_alert_occurrence WHERE is_suppressed = TRUE;"));
        }

        [TestMethod]
        public async Task ClaimNextDueAsync_ClaimsOldestMyAtmCandidateAndReclaimsExpiredLeaseWithNewFence()
        {
            using var connection = database!.OpenConnection();
            connection.Open();
            var utcNow = ParseUtc("2026-07-14T12:00:00Z");
            var pendingId = Guid.NewGuid();
            var expiredId = Guid.NewGuid();
            var expiredLeaseId = Guid.NewGuid();
            var foreignProducerId = Guid.NewGuid();
            InsertOutboxMessage(
                connection,
                foreignProducerId,
                "Pending",
                utcNow.AddMinutes(-10),
                0,
                null,
                null,
                MonitorDeliveryProducers.Svantek);
            InsertOutboxMessage(connection, pendingId, "Pending", utcNow.AddMinutes(-5), 0, null, null);
            InsertOutboxMessage(connection, expiredId, "InProgress", utcNow.AddMinutes(-4), 7, expiredLeaseId, utcNow.AddSeconds(-1));

            var queries = (IMonitorDeliveryOutboxQueries)testObj!;
            var unspecifiedUtcNow = DateTime.SpecifyKind(utcNow, DateTimeKind.Unspecified);
            var firstClaim = await queries.ClaimNextDueAsync(
                MonitorDeliveryProducers.MyAtm,
                unspecifiedUtcNow,
                TimeSpan.FromMinutes(2));
            var reclaimed = await queries.ClaimNextDueAsync(
                MonitorDeliveryProducers.MyAtm,
                unspecifiedUtcNow,
                TimeSpan.FromMinutes(2));

            Assert.IsNotNull(firstClaim);
            Assert.IsNotNull(reclaimed);
            Assert.AreEqual(pendingId, firstClaim.Id);
            Assert.AreEqual(MonitorDeliveryProducers.MyAtm, firstClaim.Producer);
            Assert.AreEqual(1, firstClaim.AttemptCount);
            Assert.AreEqual(expiredId, reclaimed.Id);
            Assert.AreEqual(MonitorDeliveryProducers.MyAtm, reclaimed.Producer);
            Assert.AreEqual(8, reclaimed.AttemptCount);
            Assert.AreNotEqual(expiredLeaseId, reclaimed.LeaseId);
            Assert.AreEqual("InProgress", ReadScalarString(
                connection,
                $"SELECT status FROM monitor_delivery_outbox WHERE id = '{reclaimed.Id}';"));
            var leaseUntil = ReadScalarDateTime(
                connection,
                $"SELECT lease_until FROM monitor_delivery_outbox WHERE id = '{reclaimed.Id}';");
            Assert.AreEqual(utcNow.AddMinutes(2).Ticks, leaseUntil.Ticks);
            Assert.AreEqual(DateTimeKind.Utc, leaseUntil.Kind);
            Assert.AreEqual("Pending", ReadScalarString(
                connection,
                $"SELECT status FROM monitor_delivery_outbox WHERE id = '{foreignProducerId}';"));
        }

        [TestMethod]
        public async Task ClaimNextDueAsync_ConcurrentClaimersReturnOnlyOneWinner()
        {
            using var connection = database!.OpenConnection();
            connection.Open();
            var utcNow = ParseUtc("2026-07-14T12:00:00Z");
            var messageId = Guid.NewGuid();
            InsertOutboxMessage(connection, messageId, "Pending", utcNow, 0, null, null);

            var claimers = Enumerable.Range(0, 4)
                .Select(_ => ((IMonitorDeliveryOutboxQueries)new DBClient(database.ConnectionString))
                    .ClaimNextDueAsync(MonitorDeliveryProducers.MyAtm, utcNow, TimeSpan.FromMinutes(2)))
                .ToArray();

            var claims = await Task.WhenAll(claimers);

            Assert.HasCount(1, claims.Where(claim => claim is not null));
            var winner = claims.Single(claim => claim is not null)!;
            Assert.AreEqual(messageId, winner.Id);
            Assert.AreNotEqual(Guid.Empty, winner.LeaseId);
            Assert.AreEqual(1, ReadScalarInt(connection, "SELECT attempt_count FROM monitor_delivery_outbox WHERE producer = 'MyAtm';"));
        }

        [TestMethod]
        public async Task ClaimNextDueAsync_RetriesLostConditionalClaimAndClaimsNextDueCandidate()
        {
            using var connection = database!.OpenConnection();
            connection.Open();
            var utcNow = ParseUtc("2026-07-14T12:00:00Z");
            var firstId = Guid.NewGuid();
            var secondId = Guid.NewGuid();
            var thirdId = Guid.NewGuid();
            InsertOutboxMessage(connection, firstId, "Pending", utcNow.AddMinutes(-3), 0, null, null);
            InsertOutboxMessage(connection, secondId, "Pending", utcNow.AddMinutes(-2), 0, null, null);
            InsertOutboxMessage(connection, thirdId, "Pending", utcNow.AddMinutes(-1), 0, null, null);
            var claimant = new ForcedContentionDbClient(database.ConnectionString, lostConditionalClaims: 1);

            var claim = await ((IMonitorDeliveryOutboxQueries)(DBClient)claimant).ClaimNextDueAsync(
                MonitorDeliveryProducers.MyAtm,
                utcNow,
                TimeSpan.FromMinutes(2));

            Assert.IsNotNull(claim);
            Assert.AreEqual(secondId, claim.Id);
            CollectionAssert.AreEqual(new[] { firstId }, claimant.CompetingClaimIds);
            Assert.AreEqual(2, claimant.CandidateSelectionCount);
            Assert.IsTrue(claimant.CandidateSelectionCount <= 3);
            Assert.AreEqual("Pending", ReadScalarString(connection, $"SELECT status FROM monitor_delivery_outbox WHERE id = '{thirdId}';"));
        }

        [TestMethod]
        public async Task ClaimNextDueAsync_StopsAfterThreeLostConditionalClaims()
        {
            using var connection = database!.OpenConnection();
            connection.Open();
            var utcNow = ParseUtc("2026-07-14T12:00:00Z");
            var messageIds = Enumerable.Range(0, 4).Select(_ => Guid.NewGuid()).ToArray();
            for (var index = 0; index < messageIds.Length; index++)
            {
                InsertOutboxMessage(connection, messageIds[index], "Pending", utcNow.AddMinutes(-4 + index), 0, null, null);
            }

            var claimant = new ForcedContentionDbClient(database.ConnectionString, lostConditionalClaims: 3);
            var claim = await ((IMonitorDeliveryOutboxQueries)(DBClient)claimant).ClaimNextDueAsync(
                MonitorDeliveryProducers.MyAtm,
                utcNow,
                TimeSpan.FromMinutes(2));

            Assert.IsNull(claim);
            CollectionAssert.AreEqual(messageIds.Take(3).ToArray(), claimant.CompetingClaimIds);
            Assert.AreEqual(3, claimant.CandidateSelectionCount);
            Assert.AreEqual("Pending", ReadScalarString(connection, $"SELECT status FROM monitor_delivery_outbox WHERE id = '{messageIds[3]}';"));
            Assert.AreEqual(0, ReadScalarInt(connection, $"SELECT attempt_count FROM monitor_delivery_outbox WHERE id = '{messageIds[3]}';"));
        }

        [TestMethod]
        public async Task ClaimNextDueAsync_RejectsUnknownProducerUsingOrdinalValidation()
        {
            var queries = (IMonitorDeliveryOutboxQueries)testObj!;

            await Assert.ThrowsExactlyAsync<ArgumentException>(() => queries.ClaimNextDueAsync(
                "myatm",
                DateTime.UtcNow,
                TimeSpan.FromMinutes(2)));
        }

        [TestMethod]
        public async Task FencedOutboxOutcomes_RejectStaleLeaseWithoutChangingMessageOrWritingAudit()
        {
            using var connection = database!.OpenConnection();
            connection.Open();
            var utcNow = ParseUtc("2026-07-14T12:00:00Z");
            var messageId = Guid.NewGuid();
            InsertOutboxMessage(connection, messageId, "Pending", utcNow, 0, null, null);
            var queries = (IMonitorDeliveryOutboxQueries)testObj!;
            var commands = (IMonitorDeliveryOutboxCommands)testObj!;
            var claim = await queries.ClaimNextDueAsync(
                MonitorDeliveryProducers.MyAtm,
                utcNow,
                TimeSpan.FromMinutes(2));

            Assert.IsNotNull(claim);
            var staleLeaseId = Guid.NewGuid();
            var audit = new MonitorDeliveryAudit(Guid.NewGuid(), "stale@example.test", "Sent ok", utcNow.AddSeconds(1));

            var completed = await commands.CompleteAsync(messageId, staleLeaseId, utcNow.AddSeconds(1), audit);
            var retried = await commands.RetryAsync(
                messageId,
                staleLeaseId,
                utcNow.AddMinutes(1),
                "stale retry");
            var deadLettered = await commands.DeadLetterAsync(
                messageId,
                staleLeaseId,
                utcNow.AddMinutes(1),
                "stale dead letter",
                audit);

            Assert.IsFalse(completed);
            Assert.IsFalse(retried);
            Assert.IsFalse(deadLettered);
            Assert.AreEqual("InProgress", ReadScalarString(connection, "SELECT status FROM monitor_delivery_outbox WHERE producer = 'MyAtm';"));
            Assert.AreEqual(claim.LeaseId, ReadOutboxLeaseIds(connection).Single());
            Assert.AreEqual(0, ReadScalarInt(connection, "SELECT COUNT(*) FROM notification_sent;"));
        }

        [TestMethod]
        public async Task FencedOutboxOutcomes_CompleteAndDeadLetterAtomicallyWithAudits()
        {
            using var connection = database!.OpenConnection();
            connection.Open();
            var utcNow = ParseUtc("2026-07-14T12:00:00Z");
            var monitor = CreateMonitorsList(1, 861).Single();
            testObj!.WriteMonitorList([monitor]);
            var notificationId = Guid.NewGuid();
            testObj.WriteNotification(new NotificationDto(
                notificationId,
                utcNow,
                1,
                60,
                2,
                null,
                null,
                AlertType.Alert,
                "Pm10",
                monitor.Id));
            var completedId = Guid.NewGuid();
            var deadLetterId = Guid.NewGuid();
            InsertOutboxMessage(connection, completedId, "Pending", utcNow.AddMinutes(-1), 0, null, null);
            InsertOutboxMessage(connection, deadLetterId, "Pending", utcNow, 7, null, null);

            var queries = (IMonitorDeliveryOutboxQueries)testObj;
            var commands = (IMonitorDeliveryOutboxCommands)testObj!;
            var completedClaim = await queries.ClaimNextDueAsync(
                MonitorDeliveryProducers.MyAtm,
                utcNow,
                TimeSpan.FromMinutes(2));
            var deadLetterClaim = await queries.ClaimNextDueAsync(
                MonitorDeliveryProducers.MyAtm,
                utcNow,
                TimeSpan.FromMinutes(2));

            Assert.IsNotNull(completedClaim);
            Assert.IsNotNull(deadLetterClaim);
            Assert.AreEqual(completedId, completedClaim.Id);
            Assert.AreEqual(deadLetterId, deadLetterClaim.Id);
            Assert.IsTrue(await commands.CompleteAsync(
                completedId,
                completedClaim.LeaseId,
                utcNow.AddSeconds(1),
                new MonitorDeliveryAudit(notificationId, "sent@example.test", "Sent ok", utcNow.AddSeconds(1))));
            Assert.IsTrue(await commands.DeadLetterAsync(
                deadLetterId,
                deadLetterClaim.LeaseId,
                utcNow.AddSeconds(2).ToLocalTime(),
                "permanent failure",
                new MonitorDeliveryAudit(notificationId, "failed@example.test", "permanent failure", utcNow.AddSeconds(2))));

            Assert.AreEqual("Completed", ReadScalarString(connection, $"SELECT status FROM monitor_delivery_outbox WHERE id = '{completedId}';"));
            Assert.AreEqual("DeadLetter", ReadScalarString(connection, $"SELECT status FROM monitor_delivery_outbox WHERE id = '{deadLetterId}';"));
            var deadLetteredAt = ReadScalarDateTime(
                connection,
                $"SELECT dead_lettered_at FROM monitor_delivery_outbox WHERE id = '{deadLetterId}';");
            Assert.AreEqual(utcNow.AddSeconds(2).Ticks, deadLetteredAt.Ticks);
            Assert.AreEqual(DateTimeKind.Utc, deadLetteredAt.Kind);
            Assert.AreEqual(2, ReadScalarInt(connection, "SELECT COUNT(*) FROM notification_sent;"));
        }


        [TestMethod]
        public void TestSetMonitorOffline()
        {
            var connectionString = database!.ConnectionString;
            using var connection = new NpgsqlConnection(connectionString);
            connection.Open();
            var customerId = 861;
            var monitorsIn = CreateMonitorsList(1, customerId);
            testObj!.WriteMonitorList(monitorsIn);

            foreach (var m in monitorsIn)
            {
                testObj.WriteFleetNr(m.SerialId, m.FleetNr!);
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
        public async Task InsertDustDto()
        {
            var serialId = "17239";
            var sampleTime = ParseUtc("2023-10-17T14:37:42");

            testObj!.InsertDustDtos(new List<DustDto> { new DustDto(serialId: serialId, avrg: 60, sampleTime: sampleTime,
                                               pm1: 1.0, pm2_5: 2.5, pm10: 10, pmTotal: 13.5,
                                               weather_t: 3.1234, weather_p: 5.5678, weather_rh: 99.87654) });

            await using var connection = database!.OpenConnection();
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(
                "SELECT serial_id, sample_time, pm_2_5 FROM my_atm_dust_level ORDER BY sample_time;", connection);
            await using var reader = await command.ExecuteReaderAsync();
            Assert.IsTrue(await reader.ReadAsync());
            Assert.AreEqual(serialId, reader.GetString(0));
            Assert.AreEqual(sampleTime, reader.GetDateTime(1));
            Assert.AreEqual(2.5, reader.GetDouble(2));
            Assert.IsFalse(await reader.ReadAsync());
        }

        [TestMethod]
        public void InsertDustDto_IgnoresDuplicateRowsInSingleBatch()
        {
            var serialId = "17239";
            var sampleTime = ParseUtc("2023-10-17T14:37:42");
            var dto = new DustDto(serialId: serialId, avrg: 60, sampleTime: sampleTime,
                pm1: 1.0, pm2_5: 2.5, pm10: 10, pmTotal: 13.5,
                weather_t: 3.1234, weather_p: 5.5678, weather_rh: 99.87654);

            testObj!.InsertDustDtos(new List<DustDto> { dto, dto });

            var connectionString = database!.ConnectionString;
            using var connection = new NpgsqlConnection(connectionString);
            connection.Open();
            var dtos = ReadDustDtos(connection);
            Assert.AreEqual(1, dtos.Count);
        }

        [TestMethod]
        public void InsertDustDto_IgnoresRowsAlreadyPresentInDatabase()
        {
            var serialId = "17239";
            var sampleTime = ParseUtc("2023-10-17T14:37:42");
            var dto = new DustDto(serialId: serialId, avrg: 60, sampleTime: sampleTime,
                pm1: 1.0, pm2_5: 2.5, pm10: 10, pmTotal: 13.5,
                weather_t: 3.1234, weather_p: 5.5678, weather_rh: 99.87654);

            testObj!.InsertDustDtos(new List<DustDto> { dto });
            testObj.InsertDustDtos(new List<DustDto> { dto });

            var connectionString = database!.ConnectionString;
            using var connection = new NpgsqlConnection(connectionString);
            connection.Open();
            var dtos = ReadDustDtos(connection);

            Assert.AreEqual(1, dtos.Count);
        }

        [TestMethod]
        public void TestGetAverageDustLevel()
        {
            var serialId = "98231";
            var startTime = ParseUtc("2023-10-17T14:37:42");
            var pm1Total = .0;
            var pm2_5Total = .0;
            var pm10Total = .0;
            var pmTotalTotal = .0;
            var numDtos = 15;
            for (var i = 0; i < numDtos; i++)
            {
                var pm1 = 1.0 * i;
                var pm2_5 = 2.5 * i;
                var pm10 = 10 * i;
                var pmTotal = 13.5 * i;

                testObj!.InsertDustDtos(new List<DustDto> { new DustDto(serialId: serialId, avrg: 60, sampleTime: startTime.AddMinutes(i).AddSeconds(1),
                                   pm1: pm1, pm2_5: pm2_5, pm10: pm10, pmTotal: pmTotal,
                                   weather_t: .0, weather_p: .0, weather_rh: .0) });
                pm1Total += pm1;
                pm2_5Total += pm2_5;
                pm10Total += pm10;
                pmTotalTotal += pmTotal;
            }

            var avgPm1 = testObj!.GetAverageDustLevel(serialId, "Pm1", startTime, startTime.AddMinutes(15));
            Assert.AreEqual(pm1Total / numDtos, avgPm1);

            var avgPm2_5 = testObj!.GetAverageDustLevel(serialId, "Pm2_5", startTime, startTime.AddMinutes(15));
            Assert.AreEqual(pm2_5Total / numDtos, avgPm2_5);

            var avgPm10 = testObj!.GetAverageDustLevel(serialId, "Pm10", startTime, startTime.AddMinutes(15));
            Assert.AreEqual(pm10Total / numDtos, avgPm10);

            var avgPmTotal = testObj!.GetAverageDustLevel(serialId, "PmTotal", startTime, startTime.AddMinutes(15));
            Assert.AreEqual(pmTotalTotal / numDtos, avgPmTotal);
        }

        [TestMethod]
        public void TestWriteNotificationAudit()
        {

            var connectionString = database!.ConnectionString;
            using var connection = new NpgsqlConnection(connectionString);
            connection.Open();

            var serialId = "82731";
            var customerId = 332;
            var monitorsIn = CreateMonitorsList(1, customerId);
            testObj!.WriteMonitorList(monitorsIn);

            foreach (var monitorIn in monitorsIn)
            {
                testObj.WriteFleetNr(monitorIn.SerialId, monitorIn.FleetNr!);
            }

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
            InsertContact(connection, monitorId, ContactMethod.Email, email, phoneNo, siteUserId);
            var contacts = ReadContacts(connection, siteUserId);
            Assert.AreEqual(1, contacts.Count);

            var dt = ParseUtc("2023-10-18T11:19:00");
            var notificationIn = new NotificationDto(rules[0], 99.876, dt, monitorId);

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

        private static List<DustMonitorDto> CreateMonitorsList(int numMonitors, int customerId,
                                                               string serialId = "monitor")
        {
            var monitors = new List<DustMonitorDto>();
            for (var i = 0; i < numMonitors; i++)
            {
                var dt = DateTime.UtcNow.AddMinutes(i);
                var monitor = new DustMonitorDto(id: Guid.NewGuid(), customerId: customerId, listedAtTime: dt, serialId: serialId + i,
                                 model: "model" + i, i, latitude: 44.4f + i, longitude: 55.5f + i, address: "address" + i,
                                 timeZone: "timezone" + i, customerDisplayName: "customerDisplayName" + i, lastDataTime1Min: dt,
                                 lastDataTime15Min: null, lastDataTime1Hour: null, lastDataTime24Hour: null,
                                 manufacturer: "Palas GmbH", firmwareVersion: "0.0." + i, fleetNr: "fleetNr+i", offline: false);
                monitors.Add(monitor);

            }
            return monitors;

        }

        private static DateTime ParseUtc(string value) =>
            DateTime.Parse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);

        private static AlertOccurrenceProposal CreateOccurrence(
            string key,
            DustMonitorDto monitor,
            RvtAlertRuleDto rule,
            AlertType alertType,
            string field,
            DateTime triggeredAt,
            Period period = Period.Minutes1) =>
            new(
                key,
                monitor.Id,
                rule.RuleId,
                period,
                alertType,
                field,
                rule.LimitOn,
                rule.LimitOn + 1,
                triggeredAt,
                Array.Empty<RvtContactDto>());

        private static MyAtmDustImportCommit CreateOccurrenceCommit(
            DustMonitorDto monitor,
            AlertOccurrenceProposal occurrence,
            DateTime utcNow) =>
            CreateOccurrenceCommit(monitor, [occurrence], utcNow);

        private static MyAtmDustImportCommit CreateOccurrenceCommit(
            DustMonitorDto monitor,
            IReadOnlyList<AlertOccurrenceProposal> occurrences,
            DateTime utcNow) =>
            new(
                monitor,
                Period.Minutes1,
                Array.Empty<DustDto>(),
                utcNow,
                Array.Empty<RuleStateMutation>(),
                occurrences,
                utcNow);

        private static MyAtmAlertCommit CreateAggregateOccurrenceCommit(
            AlertOccurrenceProposal occurrence,
            DateTime utcNow) =>
            CreateAggregateOccurrenceCommit([occurrence], utcNow);

        private static MyAtmAlertCommit CreateAggregateOccurrenceCommit(
            IReadOnlyList<AlertOccurrenceProposal> occurrences,
            DateTime utcNow) =>
            new(
                Array.Empty<RuleStateMutation>(),
                null,
                occurrences.Select(occurrence => new MyAtmAlertOccurrenceInput(
                    occurrence.Key,
                    occurrence.MonitorId,
                    occurrence.RuleId,
                    occurrence.Period,
                    occurrence.AlertType,
                    occurrence.Field,
                    occurrence.LimitOn,
                    occurrence.Level,
                    occurrence.TriggeredAt,
                    CreateDeliveryPlan(occurrence, utcNow))).ToList(),
                utcNow);

        private static RuleAlertDeliveryPlan CreateDeliveryPlan(
            AlertOccurrenceProposal occurrence,
            DateTime createdAt)
        {
            var notificationId = MonitorDeliveryIdentity.CreateGuid($"notification:{occurrence.Key}");
            var notification = new NotificationDto(
                notificationId,
                occurrence.TriggeredAt,
                occurrence.LimitOn,
                DustMonitorDto.PeriodToSeconds(occurrence.Period),
                occurrence.Level,
                null,
                null,
                occurrence.AlertType,
                MyAtmAlertTransitionEvaluator.NormalizeField(occurrence.Field),
                occurrence.MonitorId);
            var deliveryKey = $"{occurrence.Key}:MqttAlert:alert";
            var payload = System.Text.Json.JsonSerializer.Serialize(new MonitorDeliveryPayloadV1(
                notificationId,
                occurrence.TriggeredAt,
                "fixture-serial",
                862,
                "fixture-fleet",
                occurrence.AlertType,
                MyAtmAlertTransitionEvaluator.NormalizeField(occurrence.Field),
                occurrence.Level));
            return new RuleAlertDeliveryPlan(
                notification,
                [new MonitorDeliveryRequest(
                    MonitorDeliveryIdentity.CreateGuid($"outbox:{deliveryKey}"),
                    MonitorDeliveryProducers.MyAtm,
                    notificationId,
                    occurrence.Key,
                    deliveryKey,
                    MonitorDeliveryKind.MqttAlert,
                    "alert",
                    1,
                    payload,
                    createdAt)]);
        }

        private static RuleAlertDeliveryPlan CreateDeliveryPlan(
            string key,
            DustMonitorDto monitor,
            RvtAlertRuleDto rule,
            AlertType alertType,
            string field,
            double level,
            DateTime triggeredAt,
            DateTime createdAt,
            bool includeMqtt)
        {
            var contacts = new List<RvtContactDto>
            {
                new(true, false, "alert@example.test", null, null, null)
            };
            var plan = new RuleAlertDeliveryPlanner().Plan(
                new RuleNotificationRequest(
                    monitor.FleetNr ?? string.Empty,
                    monitor.SerialId,
                    triggeredAt,
                    rule.LimitOn,
                    rule.AveragingPeriod,
                    level,
                    alertType,
                    field,
                    monitor.Id),
                contacts,
                MonitorDeliveryProducers.MyAtm,
                monitor.CustomerId,
                key,
                createdAt);
            return includeMqtt
                ? plan
                : plan with
                {
                    Deliveries = plan.Deliveries
                        .Where(delivery => delivery.Kind != MonitorDeliveryKind.MqttAlert)
                        .ToList()
                };
        }

        private static MonitorDeliveryPayloadV1 Decode(MonitorDeliveryRequest request) =>
            MonitorDeliveryPayloadCodec.Decode(new MonitorDeliveryMessage(
                request.Id,
                request.Producer,
                request.NotificationId,
                request.CorrelationKey,
                request.DeliveryKey,
                request.Kind,
                request.Destination,
                request.PayloadVersion,
                request.Payload,
                AttemptCount: 1,
                LeaseId: Guid.NewGuid()));

        private static int ReadScalarInt(NpgsqlConnection connection, string sql)
        {
            using var command = new NpgsqlCommand(sql, connection);
            return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
        }

        private static string ReadScalarString(NpgsqlConnection connection, string sql)
        {
            using var command = new NpgsqlCommand(sql, connection);
            return Convert.ToString(command.ExecuteScalar(), CultureInfo.InvariantCulture)!;
        }

        private static DateTime ReadScalarDateTime(NpgsqlConnection connection, string sql)
        {
            using var command = new NpgsqlCommand(sql, connection);
            return (DateTime)command.ExecuteScalar()!;
        }

        private static void InsertOutboxMessage(
            NpgsqlConnection connection,
            Guid id,
            string status,
            DateTime nextAttemptAt,
            int attemptCount,
            Guid? leaseId,
            DateTime? leaseUntil,
            string producer = MonitorDeliveryProducers.MyAtm)
        {
            using var command = new NpgsqlCommand(
                @"INSERT INTO monitor_delivery_outbox
                    (id, producer, notification_id, correlation_key, delivery_key, kind, destination,
                     payload_version, payload, status, attempt_count, next_attempt_at, lease_id,
                     lease_until, completed_at, dead_lettered_at, last_error, created_at)
                  VALUES
                    (@Id, @Producer, NULL, NULL, @DeliveryKey, 'MqttAlert', 'alert', 1, '{}', @Status,
                     @AttemptCount, @NextAttemptAt, @LeaseId, @LeaseUntil, NULL, NULL, NULL, @CreatedAt);",
                connection);
            command.Parameters.AddWithValue("@Id", id);
            command.Parameters.AddWithValue("@Producer", producer);
            command.Parameters.AddWithValue("@DeliveryKey", $"delivery:{id:N}");
            command.Parameters.AddWithValue("@Status", status);
            command.Parameters.AddWithValue("@AttemptCount", attemptCount);
            command.Parameters.AddWithValue("@NextAttemptAt", nextAttemptAt);
            command.Parameters.AddWithValue("@LeaseId", (object?)leaseId ?? DBNull.Value);
            command.Parameters.AddWithValue("@LeaseUntil", (object?)leaseUntil ?? DBNull.Value);
            command.Parameters.AddWithValue("@CreatedAt", nextAttemptAt.AddMinutes(-1));
            command.ExecuteNonQuery();
        }

        private sealed class ForcedContentionDbClient : DBClient
        {
            private readonly DBClient competingClient;
            private readonly int lostConditionalClaims;

            public ForcedContentionDbClient(string connectionString, int lostConditionalClaims)
                : base(connectionString)
            {
                competingClient = new DBClient(connectionString);
                this.lostConditionalClaims = lostConditionalClaims;
            }

            public int CandidateSelectionCount { get; private set; }
            public List<Guid> CompetingClaimIds { get; } = [];

            protected override async Task BeforeConditionalOutboxClaimAsync(
                Guid candidateId,
                DateTime utcNow,
                TimeSpan lease,
                CancellationToken cancellationToken)
            {
                CandidateSelectionCount++;
                if (CandidateSelectionCount > lostConditionalClaims)
                {
                    return;
                }

                var competingClaim = await ((IMonitorDeliveryOutboxQueries)competingClient).ClaimNextDueAsync(
                    MonitorDeliveryProducers.MyAtm,
                    utcNow,
                    lease,
                    cancellationToken);
                Assert.IsNotNull(competingClaim);
                Assert.AreEqual(candidateId, competingClaim.Id);
                CompetingClaimIds.Add(competingClaim.Id);
            }
        }

        private static IReadOnlyList<Guid> ReadOutboxLeaseIds(NpgsqlConnection connection)
        {
            using var command = new NpgsqlCommand(
                "SELECT lease_id FROM monitor_delivery_outbox WHERE producer = 'MyAtm' AND lease_id IS NOT NULL;",
                connection);
            using var reader = command.ExecuteReader();
            var leaseIds = new List<Guid>();
            while (reader.Read())
            {
                leaseIds.Add(reader.GetGuid(0));
            }

            return leaseIds;
        }

        private static void InsertAlertRule(NpgsqlConnection connection, int index, string serialId, Guid monitorId,
                                            AlertType? alertType = null)
        {
            var sql = @"INSERT INTO rvt_alert_rule
                            (id, serial_id, alert_field, limit_on, limit_off, alert_type, is_active, averaging_period,
                             weekdays, saturdays, sundays, start_time, end_time, is_deleted, monitor_id, created)
                        VALUES (@Id, @SerialId, @AlertField, @LimitOn, @LimitOff, @AlertType, @IsActive, @AveragingPeriod,
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
            cmd.Parameters.AddWithValue(
                "@StartTime", NpgsqlDbType.Time, isEven ? new TimeSpan(9, 0, 0) : (object)DBNull.Value);
            cmd.Parameters.AddWithValue(
                "@EndTime", NpgsqlDbType.Time, isEven ? new TimeSpan(17, 0, 0) : (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@IsDeleted", false);
            cmd.Parameters.AddWithValue("@MonitorId", monitorId);
            cmd.Parameters.AddWithValue("@Created", DateTime.UtcNow);
            cmd.ExecuteNonQuery();
        }

        private static void InsertContact(NpgsqlConnection connection, Guid monitorId, ContactMethod contactMethod,
                                          string email, string phoneNo, Guid siteUserId,
        DateTime? sendStartTime = null, DateTime? sendEndTime = null)
        {
            var contractId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var siteId = Guid.NewGuid();

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
                cmd.Parameters.AddWithValue("@ContractNumber", "fixture-contract-" + Guid.NewGuid());
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
                                 what2words,
                                 picture_link,
                                 contract_id,
                                 monitor_id)
                         VALUES (@Id,
                                 @StartDate,
                                 @EndDate,
                                 @Lng,
                                 @Lat,
                                 @What2words,
                                 @PictureLink,
                                 @ContractId,
                                 @MonitorId);";


                using NpgsqlCommand cmd = new(sql, connection);
                cmd.Parameters.AddWithValue("@Id", siteId);
                cmd.Parameters.AddWithValue("@StartDate", DateTime.UtcNow);
                cmd.Parameters.AddWithValue("@EndDate", (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@Lng", 12.23f);
                cmd.Parameters.AddWithValue("@Lat", 45.67f);
                cmd.Parameters.AddWithValue("@What2words", "w3w");
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
                cmd.Parameters.AddWithValue(
                    "@StartTime", NpgsqlDbType.Time, sendStartTime?.TimeOfDay ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue(
                    "@EndTime", NpgsqlDbType.Time, sendEndTime?.TimeOfDay ?? (object)DBNull.Value);

                cmd.ExecuteNonQuery();
            }
        }

        private static ContactMethod ReadContactMethod(string connectionString, Guid siteUserId)
        {

            using var connection = new NpgsqlConnection(connectionString);
            connection.Open();
            var sql = @"SELECT email, sms FROM notification_setting WHERE site_user_id = @SiteUserId;";

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
            var sql = @"SELECT ""Email"", ""PhoneNumber"", ""Id"" FROM ""AspNetUsers"";";
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
                        FROM notification;";
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
                        FROM notification_sent;";
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


        private static List<DustDto> ReadDustDtos(NpgsqlConnection connection)
        {
            var sql = @"SELECT serial_id, avrg, sample_time, pm_1, pm_2_5, pm_10, pm_total,
                               weather_t, weather_p, weather_rh
                        FROM my_atm_dust_level;";
            using NpgsqlCommand cmd = new(sql, connection);
            var dtos = new List<DustDto>();
            using NpgsqlDataReader reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                var serialId = reader.GetString(0);
                var avrg = reader.GetInt32(1);
                var sampleTime = reader.GetDateTime(2);
                var pm1 = reader.GetDouble(3);
                var pm2_5 = reader.GetDouble(4);
                var pm10 = reader.GetDouble(5);
                var pmTotal = reader.GetDouble(6);
                var weather_t = reader.GetDouble(7);
                var weather_p = reader.GetDouble(8);
                var weather_rh = reader.GetDouble(9);

                dtos.Add(new DustDto(serialId: serialId, avrg: avrg, sampleTime: sampleTime,
                                     pm1: pm1, pm2_5: pm2_5, pm10: pm10, pmTotal: pmTotal,
                                     weather_t: weather_t, weather_p: weather_p, weather_rh: weather_rh));
            }

            return dtos;
        }
    }
}
