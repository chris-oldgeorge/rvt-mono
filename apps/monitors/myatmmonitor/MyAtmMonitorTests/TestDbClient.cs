using System.Data;
using System.Globalization;
using Microsoft.Extensions.Logging;
using MyAtm.Api;
using MyAtm.Api.Db;
using MyAtm.Api.Rules;
using MyAtm.Model.Config;
using MyAtm.Model.Dto;
using MyAtm.Model.Json;
using Npgsql;
using NpgsqlTypes;
using Rvt.Monitor.Common.Data;
using Rvt.Monitor.Common.Delivery;
using Rvt.Monitor.Common.Diagnostics;
using Rvt.Monitor.Common.Notifications;
using Rvt.Monitor.Common.Rules;
using Rvt.Monitor.Common.Utilities;
using Rvt.Monitor.IntegrationTesting;
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

        [TestMethod]
        public void ReadSiteSchedule_ActiveDeployment_ReturnsAllConfiguredHours()
        {
            Guid monitorId = Guid.NewGuid();
            Guid siteId = Guid.NewGuid();
            Guid contractId = Guid.NewGuid();
            using NpgsqlConnection connection = _database!.OpenConnection();
            connection.Open();
            using NpgsqlCommand command = new(
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

            MyAtmSiteSchedule schedule = _testObj!.ReadSiteSchedule(monitorId);

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
            string setupSql = TestUtil.ReadTextFromFile("testdata/create.postgres.sql");
            string resetSql = TestUtil.ReadTextFromFile("testdata/reset.postgres.sql");
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
                TestUtil.ReadTextFromFile("testdata/reset.postgres.sql"),
                TestContext.CancellationToken);
        }

        [TestMethod]
        public async Task InsertAccessoryPageAsync_DeduplicatesThePageAndRollsBackTheWholePageOnFailure()
        {
            DateTime firstTimestamp = ParseUtc("2026-07-14T12:00:00Z");
            DateTime secondTimestamp = firstTimestamp.AddMinutes(1);
            AccessoryInfoDto first = new("accessory-1", new AccessoryInfo { Timestamp = firstTimestamp });
            AccessoryInfoDto duplicate = new("accessory-1", new AccessoryInfo { Timestamp = firstTimestamp });
            await _testObj!.InsertAccessoryPageAsync([first, duplicate], TestContext.CancellationToken);

            using NpgsqlConnection connection = _database!.OpenConnection();
            connection.Open();
            using (NpgsqlCommand countCommand = new("SELECT COUNT(*) FROM my_atm_accessory_info WHERE serial_id = 'accessory-1';", connection))
            {
                Assert.AreEqual(1L, countCommand.ExecuteScalar());
            }

            using (NpgsqlCommand constraintCommand = new(
                "ALTER TABLE my_atm_accessory_info ADD CONSTRAINT task6_accessory_t_led_nonnegative CHECK (operating_t_led IS NULL OR operating_t_led >= 0);",
                connection))
            {
                constraintCommand.ExecuteNonQuery();
            }

            AccessoryInfoDto valid = new("accessory-2", new AccessoryInfo { Timestamp = firstTimestamp });
            AccessoryInfoDto invalid = new("accessory-2", new AccessoryInfo { Timestamp = secondTimestamp, OperatingTLed = -1 });
            await Assert.ThrowsExactlyAsync<Microsoft.EntityFrameworkCore.DbUpdateException>(
                () => _testObj.InsertAccessoryPageAsync([valid, invalid], TestContext.CancellationToken));

            using NpgsqlCommand rollbackCountCommand = new(
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
        [TestMethod]
        public void TestMonitorsList(string lastDate, string queryDate, int numMonitors, int numExpectedMonitors)
        {
            DateTime? lastDataTime = String.IsNullOrEmpty(lastDate) ? null : ParseUtc(lastDate);
            DateTime? queryLastdataTime = String.IsNullOrEmpty(queryDate) ? null : ParseUtc(queryDate);
            List<DustMonitorDto> monitorsIn = CreateMonitorsList(numMonitors, 987);
            Assert.HasCount(numMonitors, monitorsIn);
            _testObj!.WriteMonitorList(monitorsIn);

            foreach (DustMonitorDto monitorIn in monitorsIn)
            {
                _testObj.WriteFleetNr(monitorIn.SerialId, monitorIn.FleetNr!);
            }

            if (lastDataTime != null)
            {
                for (int i = 0; i < monitorsIn.Count; i++)
                {
                    DateTime dt = ((DateTime)lastDataTime!).AddHours(i);
                    _testObj.WriteLatestTimestamp(monitorsIn[i].SerialId, dt, Period.Minutes1);
                }
            }

            List<DustMonitorDto> monitorsOut = _testObj.ReadMonitorList(queryLastdataTime);
            Assert.HasCount(numExpectedMonitors, monitorsOut);
            Assert.IsTrue(TestUtil.VerifyMonitorList(monitorsIn, monitorsOut));

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
        public void TestReadAlertRules()
        {
            string connectionString = _database!.ConnectionString;
            using NpgsqlConnection connection = new(connectionString);
            connection.Open();

            string serialId = "12345";
            int customerId = 861;
            List<DustMonitorDto> monitorsIn = CreateMonitorsList(1, customerId);
            _testObj!.WriteMonitorList(monitorsIn);

            foreach (DustMonitorDto monitorIn in monitorsIn)
            {
                _testObj.WriteFleetNr(monitorIn.SerialId, monitorIn.FleetNr!);
            }

            List<DustMonitorDto> monitorsOut = _testObj.ReadMonitorList(null);
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
        public void TestReadAlertContacts()
        {
            string connectionString = _database!.ConnectionString;
            using NpgsqlConnection connection = new(connectionString);
            connection.Open();

            int customerId = 443;
            int numMonitors = 2;
            List<DustMonitorDto> monitorsIn = CreateMonitorsList(numMonitors, customerId);
            _testObj!.WriteMonitorList(monitorsIn);

            foreach (DustMonitorDto monitorIn in monitorsIn)
            {
                _testObj.WriteFleetNr(monitorIn.SerialId, monitorIn.FleetNr!);
            }

            List<DustMonitorDto> monitorsOut = _testObj.ReadMonitorList(null);
            Assert.HasCount(numMonitors, monitorsOut);
            Guid monitorId = monitorsOut[0].Id;
            string serialId = monitorsOut[0].SerialId;
            // add an alert and contact as RvtAlertContacts table has foreign key constraints
            InsertAlertRule(connection, 44, serialId, monitorId);
            List<RvtAlertRuleDto> rules = _testObj!.ReadRules(serialId);
            Assert.HasCount(1, rules);
            string email = "mytestemail@bbb.com";
            string phoneNo = "01234567890";
            DateTime startTime = DateTimeUtil.TruncateMillis(DateTime.UtcNow.AddHours(-1));
            DateTime endTime = DateTimeUtil.TruncateMillis(DateTime.UtcNow.AddHours(1));

            Guid siteUserId = Guid.NewGuid();
            InsertContact(connection, monitorId, ContactMethod.Email, email, phoneNo,
                          siteUserId, startTime, endTime);

            // insert that should not be read
            InsertContact(connection, monitorsOut[1].Id, ContactMethod.Email, email, phoneNo, Guid.NewGuid());

            List<RvtContactDto> contacts = ReadContacts(connection, siteUserId);
            Assert.HasCount(2, contacts);

            List<RvtContactDto> alertContacts = _testObj.ReadAlertContacts(monitorId);
            Assert.HasCount(1, alertContacts);
            RvtContactDto ac = alertContacts[0];
            Assert.AreEqual(ContactMethod.Email, ac.ContactMethod);
            Assert.AreEqual(email, ac.EmailAddress);
            Assert.AreEqual(phoneNo, ac.PhoneNumber);
            Assert.AreEqual(startTime.TimeOfDay, ac.SendStartTime);
            Assert.AreEqual(endTime.TimeOfDay, ac.SendEndTime);
        }

        [TestMethod]
        public void TestHandleException()
        {
            string connectionString = _database!.ConnectionString;

            string TAG = "MyTestError";
            string MESSAGE = "bang";

            DateTime beforeWrite = DateTime.UtcNow;
            MonitorDb.WriteException(
                connectionString,
                TAG,
                AdapterException.Of(MESSAGE),
                "MyAtmMonitorTests",
                "1.0");
            DateTime afterWrite = DateTime.UtcNow;

            using NpgsqlConnection connection = new(connectionString);
            connection.Open();

            string sql = @"SELECT variables, message, logged_at FROM error_log";
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
                Assert.AreEqual(DateTimeKind.Utc, errorTime.Kind);
                Assert.IsTrue(errorTime >= beforeWrite);
                Assert.IsTrue(errorTime <= afterWrite);

            }
            Assert.AreEqual(1, count);
        }

        [TestMethod]
        public void TestWriteLatestTimestamp()
        {
            int customerId = 851;

            List<DustMonitorDto> monitors = CreateMonitorsList(1, customerId, "wrst_monitor");
            Assert.HasCount(1, monitors);

            _testObj!.WriteMonitorList(monitors);

            foreach (DustMonitorDto monitorIn in monitors)
            {
                _testObj.WriteFleetNr(monitorIn.SerialId, monitorIn.FleetNr!);
            }

            DateTime lastDataTimeMin = ParseUtc("2023-10-18T14:35:42");
            DateTime lastDataTime15Min = ParseUtc("2023-10-18T14:29:00");
            DateTime lastDataTimeHour = ParseUtc("2023-10-18T14:46:42");
            DateTime lastDataTime24Hour = ParseUtc("2023-10-17T00:01:00");
            string serialId = "wrst_monitor0";
            _testObj.WriteLatestTimestamp(serialId, lastDataTimeMin, Period.Minutes1);
            _testObj.WriteLatestTimestamp(serialId, lastDataTime15Min, Period.Minutes15);
            _testObj.WriteLatestTimestamp(serialId, lastDataTimeHour, Period.Hours1);
            _testObj.WriteLatestTimestamp(serialId, lastDataTime24Hour, Period.Hours24);

            monitors = _testObj.ReadMonitorList(null);
            Assert.HasCount(1, monitors);

            DustMonitorDto monitor = monitors[0];
            Assert.AreEqual(lastDataTimeMin, monitor.LastDataTime1Min);
            Assert.AreEqual(lastDataTime15Min, monitor.LastDataTime15Min);
            Assert.AreEqual(lastDataTimeHour, monitor.LastDataTime1Hour);
            Assert.AreEqual(lastDataTime24Hour, monitor.LastDataTime24Hour);
        }

        [TestMethod]
        public async Task CommitDustImportAsync_PersistsMeasurementWatermarkRuleOccurrenceAndOutboxAtomically()
        {
            using NpgsqlConnection connection = _database!.OpenConnection();
            connection.Open();
            DustMonitorDto monitor = CreateMonitorsList(1, 861).Single();
            _testObj!.WriteMonitorList([monitor]);
            InsertAlertRule(connection, 21, monitor.SerialId, monitor.Id);
            RvtAlertRuleDto rule = _testObj.ReadRules(monitor.SerialId).Single();
            DateTime sampleTime = ParseUtc("2026-07-14T12:00:00Z");
            DateTime commitTime = sampleTime.AddMinutes(1);
            DustDto measurement = new(monitor.SerialId, 60, sampleTime, 11, 12, 13, 14, 15, 16, 17);
            AlertOccurrenceProposal occurrence = new(
                "occurrence:myatm-atomic-commit",
                monitor.Id,
                rule.RuleId,
                Period.Minutes1,
                AlertType.Alert,
                "Pm10",
                rule.LimitOn,
                13,
                sampleTime,
                []);
            MyAtmDustImportCommit commit = new(
                monitor,
                Period.Minutes1,
                [measurement],
                sampleTime,
                [new RuleStateMutation(rule.RuleId, false, null, true, commitTime)],
                [occurrence],
                commitTime);

            DustImportCommitResult result = await _testObj.CommitDustImportAsync(commit, TestContext.CancellationToken);

            Assert.HasCount(2, result.OutboxMessages);
            Guid expectedOccurrenceId = MonitorDeliveryIdentity.CreateGuid($"notification:{occurrence.Key}");
            string expectedAlertKey = $"{occurrence.Key}:MqttAlert:alert";
            string expectedDataKey = $"data:{monitor.Id:N}:60:{sampleTime:O}";
            CollectionAssert.AreEquivalent(
                new[] { MonitorDeliveryKind.MqttAlert, MonitorDeliveryKind.MqttDataInserted },
                result.OutboxMessages.Select(message => message.Kind).ToArray());
            MonitorDeliveryRequest alertRequest = result.OutboxMessages.Single(message => message.Kind == MonitorDeliveryKind.MqttAlert);
            Assert.AreEqual(MonitorDeliveryIdentity.CreateGuid($"outbox:{expectedAlertKey}"), alertRequest.Id);
            Assert.AreEqual(expectedOccurrenceId, alertRequest.NotificationId);
            Assert.AreEqual(occurrence.Key, alertRequest.CorrelationKey);
            Assert.AreEqual(expectedAlertKey, alertRequest.DeliveryKey);
            Assert.AreEqual("alert", alertRequest.Destination);
            MonitorDeliveryPayloadV1 alertPayload = Decode(alertRequest);
            Assert.AreEqual(expectedOccurrenceId, alertPayload.NotificationId);
            Assert.AreEqual(sampleTime, alertPayload.Timestamp);
            Assert.AreEqual(monitor.SerialId, alertPayload.SerialId);
            Assert.AreEqual(monitor.CustomerId, alertPayload.CustomerId);
            Assert.AreEqual(monitor.FleetNr, alertPayload.FleetNr);
            Assert.AreEqual(AlertType.Alert, alertPayload.AlertType);
            Assert.AreEqual("pm10", alertPayload.Field);
            Assert.AreEqual(13d, alertPayload.Level);

            MonitorDeliveryRequest dataRequest = result.OutboxMessages.Single(message => message.Kind == MonitorDeliveryKind.MqttDataInserted);
            Assert.AreEqual(MonitorDeliveryIdentity.CreateGuid($"outbox:{expectedDataKey}"), dataRequest.Id);
            Assert.IsNull(dataRequest.NotificationId);
            Assert.IsNull(dataRequest.CorrelationKey);
            Assert.AreEqual(expectedDataKey, dataRequest.DeliveryKey);
            Assert.AreEqual("insert", dataRequest.Destination);
            MonitorDeliveryPayloadV1 dataPayload = Decode(dataRequest);
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
            Assert.AreEqual(sampleTime, _testObj.ReadMonitor(monitor.SerialId)!.LastDataTime1Min);
            Assert.IsTrue(_testObj.ReadRules(monitor.SerialId).Single().IsActive);

            MyAtmDustImportCommit replay = commit with
            {
                RuleStateMutations = []
            };
            DustImportCommitResult replayResult = await _testObj.CommitDustImportAsync(replay, TestContext.CancellationToken);
            Assert.IsEmpty(replayResult.OutboxMessages);
            Assert.AreEqual(1, ReadScalarInt(connection, "SELECT COUNT(*) FROM notification;"));
            Assert.AreEqual(2, ReadScalarInt(connection, "SELECT COUNT(*) FROM monitor_delivery_outbox WHERE producer = 'MyAtm';"));

            DBClient queries = _testObj!;
            DBClient commands = _testObj!;
            DateTime unspecifiedCommitTime = DateTime.SpecifyKind(commitTime, DateTimeKind.Unspecified);
            MonitorDeliveryMessage?[] claimed =
            [
                await queries.ClaimNextDueAsync(MonitorDeliveryProducers.MyAtm, unspecifiedCommitTime, TimeSpan.FromMinutes(1), TestContext.CancellationToken),
                await queries.ClaimNextDueAsync(MonitorDeliveryProducers.MyAtm, unspecifiedCommitTime, TimeSpan.FromMinutes(1), TestContext.CancellationToken)
            ];
            Assert.IsTrue(claimed.All(message => message is { Producer: MonitorDeliveryProducers.MyAtm, AttemptCount: 1 }));
            Assert.AreEqual(claimed.Length, claimed.Select(message => message!.LeaseId).Distinct().Count());
            CollectionAssert.AreEquivalent(
                claimed.Select(message => message!.LeaseId).ToArray(),
                ReadOutboxLeaseIds(connection).ToArray());

            Assert.IsTrue(await commands.CompleteAsync(
                claimed[0]!.Id,
                claimed[0]!.LeaseId,
                DateTime.SpecifyKind(commitTime.AddSeconds(1), DateTimeKind.Unspecified),
                null, TestContext.CancellationToken));
            Assert.IsTrue(await commands.RetryAsync(
                claimed[1]!.Id,
                claimed[1]!.LeaseId,
                commitTime.AddSeconds(1).ToLocalTime(),
                "transient", TestContext.CancellationToken));
            Assert.AreEqual(1, ReadScalarInt(connection, "SELECT COUNT(*) FROM monitor_delivery_outbox WHERE producer = 'MyAtm' AND status = 'Completed';"));
            Assert.AreEqual(1, ReadScalarInt(connection, "SELECT COUNT(*) FROM monitor_delivery_outbox WHERE producer = 'MyAtm' AND status = 'Pending';"));
            DateTime completedAt = ReadScalarDateTime(
                connection,
                $"SELECT completed_at FROM monitor_delivery_outbox WHERE id = '{claimed[0]!.Id}';");
            DateTime nextAttemptAt = ReadScalarDateTime(
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
            using NpgsqlConnection connection = _database!.OpenConnection();
            connection.Open();
            DustMonitorDto monitor = CreateMonitorsList(1, 862).Single();
            _testObj!.WriteMonitorList([monitor]);
            _testObj.SetMonitorOffline(monitor.Id, true);
            InsertAlertRule(connection, 22, monitor.SerialId, monitor.Id);
            RvtAlertRuleDto rule = _testObj.ReadRules(monitor.SerialId).Single();
            DateTime triggeredAt = ParseUtc("2026-07-14T12:00:00Z");
            string key = "occurrence:offline-conflict";
            MyAtmAlertCommit commit = new(
                [],
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

            MyAtmAlertCommitResult result = await _testObj.CommitAlertAsync(commit, TestContext.CancellationToken);

            Assert.IsFalse(result.Applied);
            Assert.IsEmpty(result.OutboxMessages);
            Assert.IsTrue(_testObj.ReadMonitor(monitor.SerialId)!.Offline);
            Assert.AreEqual(0, ReadScalarInt(connection, "SELECT COUNT(*) FROM my_atm_alert_occurrence;"));
            Assert.AreEqual(0, ReadScalarInt(connection, "SELECT COUNT(*) FROM notification;"));
            Assert.AreEqual(0, ReadScalarInt(connection, "SELECT COUNT(*) FROM monitor_delivery_outbox WHERE producer = 'MyAtm';"));
        }

        [TestMethod]
        public async Task CommitDustImportAsync_SuppressesByEventTimeAlertFamilyAndPriorAcceptedCandidates()
        {
            using NpgsqlConnection connection = _database!.OpenConnection();
            connection.Open();
            DustMonitorDto monitor = CreateMonitorsList(1, 862).Single();
            _testObj!.WriteMonitorList([monitor]);
            InsertAlertRule(connection, 22, monitor.SerialId, monitor.Id);
            RvtAlertRuleDto rule = _testObj.ReadRules(monitor.SerialId).Single();
            DateTime eventStart = ParseUtc("2026-01-01T00:00:00Z");
            DateTime delayedCommit = ParseUtc("2026-07-14T12:00:00Z");

            AlertOccurrenceProposal historicalAlert = CreateOccurrence("historical-alert", monitor, rule, AlertType.Alert, "Pm10", eventStart);
            DustImportCommitResult historicalAlertResult = await _testObj.CommitDustImportAsync(CreateOccurrenceCommit(monitor, historicalAlert, delayedCommit), TestContext.CancellationToken);
            Assert.HasCount(1, historicalAlertResult.OutboxMessages);

            AlertOccurrenceProposal sameSeverity = CreateOccurrence("historical-alert-repeat", monitor, rule, AlertType.Alert, "pm10", eventStart.AddMinutes(10));
            DustImportCommitResult sameSeverityResult = await _testObj.CommitDustImportAsync(CreateOccurrenceCommit(monitor, sameSeverity, delayedCommit), TestContext.CancellationToken);
            Assert.IsEmpty(sameSeverityResult.OutboxMessages);

            AlertOccurrenceProposal cautionAfterAlert = CreateOccurrence("historical-caution-after-alert", monitor, rule, AlertType.Caution, "Pm10", eventStart.AddMinutes(15));
            DustImportCommitResult cautionAfterAlertResult = await _testObj.CommitDustImportAsync(CreateOccurrenceCommit(monitor, cautionAfterAlert, delayedCommit), TestContext.CancellationToken);
            Assert.IsEmpty(cautionAfterAlertResult.OutboxMessages);

            AlertOccurrenceProposal caution = CreateOccurrence("caution-before-alert", monitor, rule, AlertType.Caution, "Pm1", eventStart.AddHours(1));
            DustImportCommitResult cautionResult = await _testObj.CommitDustImportAsync(CreateOccurrenceCommit(monitor, caution, delayedCommit), TestContext.CancellationToken);
            Assert.HasCount(1, cautionResult.OutboxMessages);

            AlertOccurrenceProposal alertAfterCaution = CreateOccurrence("alert-after-caution", monitor, rule, AlertType.Alert, "pm1", eventStart.AddHours(1).AddMinutes(10));
            DustImportCommitResult alertAfterCautionResult = await _testObj.CommitDustImportAsync(CreateOccurrenceCommit(monitor, alertAfterCaution, delayedCommit), TestContext.CancellationToken);
            Assert.HasCount(1, alertAfterCautionResult.OutboxMessages);

            AlertOccurrenceProposal sameCommitFirst = CreateOccurrence("same-commit-first", monitor, rule, AlertType.Alert, "PmTotal", eventStart.AddHours(2));
            AlertOccurrenceProposal sameCommitSecond = CreateOccurrence("same-commit-second", monitor, rule, AlertType.Alert, "pmtotal", eventStart.AddHours(2).AddMinutes(1));
            DustImportCommitResult sameCommitResult = await _testObj.CommitDustImportAsync(
                CreateOccurrenceCommit(monitor, [sameCommitFirst, sameCommitSecond], delayedCommit), TestContext.CancellationToken);

            Assert.HasCount(1, sameCommitResult.OutboxMessages);
            Assert.AreEqual(7, ReadScalarInt(connection, "SELECT COUNT(*) FROM my_atm_alert_occurrence;"));
            Assert.AreEqual(4, ReadScalarInt(connection, "SELECT COUNT(*) FROM notification;"));
            Assert.AreEqual(4, ReadScalarInt(connection, "SELECT COUNT(*) FROM monitor_delivery_outbox WHERE producer = 'MyAtm';"));
            Assert.AreEqual(3, ReadScalarInt(connection, "SELECT COUNT(*) FROM my_atm_alert_occurrence WHERE is_suppressed = TRUE;"));
        }

        [TestMethod]
        public async Task CommitAlertAsync_SuppressesAggregateAlertFamilyCandidatesByEventTime()
        {
            using NpgsqlConnection connection = _database!.OpenConnection();
            connection.Open();
            DustMonitorDto monitor = CreateMonitorsList(1, 862).Single();
            _testObj!.WriteMonitorList([monitor]);
            InsertAlertRule(connection, 22, monitor.SerialId, monitor.Id);
            RvtAlertRuleDto rule = _testObj.ReadRules(monitor.SerialId).Single();
            DateTime eventStart = ParseUtc("2026-01-01T00:00:00Z");
            DateTime delayedCommit = ParseUtc("2026-07-14T12:00:00Z");

            AlertOccurrenceProposal alert = CreateOccurrence("aggregate-alert", monitor, rule, AlertType.Alert, "Pm10", eventStart, Period.Hours8);
            MyAtmAlertCommitResult alertResult = await _testObj.CommitAlertAsync(CreateAggregateOccurrenceCommit(alert, delayedCommit), TestContext.CancellationToken);
            Assert.HasCount(1, alertResult.OutboxMessages);

            AlertOccurrenceProposal sameSeverity = CreateOccurrence("aggregate-alert-repeat", monitor, rule, AlertType.Alert, "pm10", eventStart.AddMinutes(10), Period.Hours8);
            MyAtmAlertCommitResult sameSeverityResult = await _testObj.CommitAlertAsync(CreateAggregateOccurrenceCommit(sameSeverity, delayedCommit), TestContext.CancellationToken);
            Assert.IsEmpty(sameSeverityResult.OutboxMessages);

            AlertOccurrenceProposal cautionAfterAlert = CreateOccurrence("aggregate-caution-after-alert", monitor, rule, AlertType.Caution, "Pm10", eventStart.AddMinutes(15), Period.Hours8);
            MyAtmAlertCommitResult cautionAfterAlertResult = await _testObj.CommitAlertAsync(CreateAggregateOccurrenceCommit(cautionAfterAlert, delayedCommit), TestContext.CancellationToken);
            Assert.IsEmpty(cautionAfterAlertResult.OutboxMessages);

            AlertOccurrenceProposal caution = CreateOccurrence("aggregate-caution", monitor, rule, AlertType.Caution, "Pm1", eventStart.AddHours(1), Period.Hours8);
            MyAtmAlertCommitResult cautionResult = await _testObj.CommitAlertAsync(CreateAggregateOccurrenceCommit(caution, delayedCommit), TestContext.CancellationToken);
            Assert.HasCount(1, cautionResult.OutboxMessages);

            AlertOccurrenceProposal escalation = CreateOccurrence("aggregate-alert-after-caution", monitor, rule, AlertType.Alert, "pm1", eventStart.AddHours(1).AddMinutes(10), Period.Hours8);
            MyAtmAlertCommitResult escalationResult = await _testObj.CommitAlertAsync(CreateAggregateOccurrenceCommit(escalation, delayedCommit), TestContext.CancellationToken);
            Assert.HasCount(1, escalationResult.OutboxMessages);

            AlertOccurrenceProposal sameCommitFirst = CreateOccurrence("aggregate-same-commit-first", monitor, rule, AlertType.Alert, "PmTotal", eventStart.AddHours(2), Period.Hours8);
            AlertOccurrenceProposal sameCommitSecond = CreateOccurrence("aggregate-same-commit-second", monitor, rule, AlertType.Alert, "pmtotal", eventStart.AddHours(2).AddMinutes(1), Period.Hours8);
            MyAtmAlertCommitResult sameCommitResult = await _testObj.CommitAlertAsync(
                CreateAggregateOccurrenceCommit([sameCommitFirst, sameCommitSecond], delayedCommit), TestContext.CancellationToken);
            Assert.HasCount(1, sameCommitResult.OutboxMessages);

            Assert.AreEqual(7, ReadScalarInt(connection, "SELECT COUNT(*) FROM my_atm_alert_occurrence;"));
            Assert.AreEqual(4, ReadScalarInt(connection, "SELECT COUNT(*) FROM notification;"));
            Assert.AreEqual(4, ReadScalarInt(connection, "SELECT COUNT(*) FROM monitor_delivery_outbox WHERE producer = 'MyAtm';"));
            Assert.AreEqual(3, ReadScalarInt(connection, "SELECT COUNT(*) FROM my_atm_alert_occurrence WHERE is_suppressed = TRUE;"));
        }

        [TestMethod]
        public async Task CommitDustImportAsync_DoesNotCrossSuppressAcceptedCandidatesFromAnotherMonitorOrPeriod()
        {
            using NpgsqlConnection connection = _database!.OpenConnection();
            connection.Open();
            List<DustMonitorDto> monitors = CreateMonitorsList(2, 863);
            DustMonitorDto firstMonitor = monitors[0];
            DustMonitorDto secondMonitor = monitors[1];
            _testObj!.WriteMonitorList(monitors);
            InsertAlertRule(connection, 23, firstMonitor.SerialId, firstMonitor.Id);
            InsertAlertRule(connection, 24, secondMonitor.SerialId, secondMonitor.Id);
            RvtAlertRuleDto firstRule = _testObj.ReadRules(firstMonitor.SerialId).Single();
            RvtAlertRuleDto secondRule = _testObj.ReadRules(secondMonitor.SerialId).Single();
            DateTime eventTime = ParseUtc("2026-01-01T00:00:00Z");
            DateTime delayedCommit = ParseUtc("2026-07-14T12:00:00Z");
            AlertOccurrenceProposal sameScope = CreateOccurrence("scope-first", firstMonitor, firstRule, AlertType.Alert, "Pm10", eventTime);
            AlertOccurrenceProposal otherMonitor = CreateOccurrence("scope-other-monitor", secondMonitor, secondRule, AlertType.Alert, "pm10", eventTime.AddMinutes(1));
            AlertOccurrenceProposal otherPeriod = CreateOccurrence("scope-other-period", firstMonitor, firstRule, AlertType.Alert, "pm10", eventTime.AddMinutes(2), Period.Minutes15);

            DustImportCommitResult result = await _testObj.CommitDustImportAsync(
                CreateOccurrenceCommit(firstMonitor, [sameScope, otherMonitor, otherPeriod], delayedCommit), TestContext.CancellationToken);

            Assert.HasCount(3, result.OutboxMessages);
            Assert.AreEqual(3, ReadScalarInt(connection, "SELECT COUNT(*) FROM notification;"));
            Assert.AreEqual(0, ReadScalarInt(connection, "SELECT COUNT(*) FROM my_atm_alert_occurrence WHERE is_suppressed = TRUE;"));
        }

        [TestMethod]
        public async Task ClaimNextDueAsync_ClaimsOldestMyAtmCandidateAndReclaimsExpiredLeaseWithNewFence()
        {
            using NpgsqlConnection connection = _database!.OpenConnection();
            connection.Open();
            DateTime utcNow = ParseUtc("2026-07-14T12:00:00Z");
            Guid pendingId = Guid.NewGuid();
            Guid expiredId = Guid.NewGuid();
            Guid expiredLeaseId = Guid.NewGuid();
            Guid foreignProducerId = Guid.NewGuid();
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

            DBClient queries = _testObj!;
            DateTime unspecifiedUtcNow = DateTime.SpecifyKind(utcNow, DateTimeKind.Unspecified);
            MonitorDeliveryMessage? firstClaim = await queries.ClaimNextDueAsync(
                MonitorDeliveryProducers.MyAtm,
                unspecifiedUtcNow,
                TimeSpan.FromMinutes(2), TestContext.CancellationToken);
            MonitorDeliveryMessage? reclaimed = await queries.ClaimNextDueAsync(
                MonitorDeliveryProducers.MyAtm,
                unspecifiedUtcNow,
                TimeSpan.FromMinutes(2), TestContext.CancellationToken);

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
            DateTime leaseUntil = ReadScalarDateTime(
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
            using NpgsqlConnection connection = _database!.OpenConnection();
            connection.Open();
            DateTime utcNow = ParseUtc("2026-07-14T12:00:00Z");
            Guid messageId = Guid.NewGuid();
            InsertOutboxMessage(connection, messageId, "Pending", utcNow, 0, null, null);

            Task<MonitorDeliveryMessage?>[] claimers = [.. Enumerable.Range(0, 4)
                .Select(_ => ((IMonitorDeliveryOutboxQueries)new DBClient(_database.ConnectionString))
                    .ClaimNextDueAsync(MonitorDeliveryProducers.MyAtm, utcNow, TimeSpan.FromMinutes(2), TestContext.CancellationToken))];

            MonitorDeliveryMessage?[] claims = await Task.WhenAll(claimers);

            Assert.HasCount(1, claims.Where(claim => claim is not null));
            MonitorDeliveryMessage winner = claims.Single(claim => claim is not null)!;
            Assert.AreEqual(messageId, winner.Id);
            Assert.AreNotEqual(Guid.Empty, winner.LeaseId);
            Assert.AreEqual(1, ReadScalarInt(connection, "SELECT attempt_count FROM monitor_delivery_outbox WHERE producer = 'MyAtm';"));
        }

        [TestMethod]
        public async Task ClaimNextDueAsync_RetriesLostConditionalClaimAndClaimsNextDueCandidate()
        {
            using NpgsqlConnection connection = _database!.OpenConnection();
            connection.Open();
            DateTime utcNow = ParseUtc("2026-07-14T12:00:00Z");
            Guid firstId = Guid.NewGuid();
            Guid secondId = Guid.NewGuid();
            Guid thirdId = Guid.NewGuid();
            InsertOutboxMessage(connection, firstId, "Pending", utcNow.AddMinutes(-3), 0, null, null);
            InsertOutboxMessage(connection, secondId, "Pending", utcNow.AddMinutes(-2), 0, null, null);
            InsertOutboxMessage(connection, thirdId, "Pending", utcNow.AddMinutes(-1), 0, null, null);
            ForcedContentionDbClient claimant = new(_database.ConnectionString, lostConditionalClaims: 1);

            MonitorDeliveryMessage? claim = await ((IMonitorDeliveryOutboxQueries)(DBClient)claimant).ClaimNextDueAsync(
                MonitorDeliveryProducers.MyAtm,
                utcNow,
                TimeSpan.FromMinutes(2), TestContext.CancellationToken);

            Assert.IsNotNull(claim);
            Assert.AreEqual(secondId, claim.Id);
            CollectionAssert.AreEqual(new[] { firstId }, claimant.CompetingClaimIds);
            Assert.AreEqual(2, claimant.CandidateSelectionCount);
            Assert.IsLessThanOrEqualTo(3, claimant.CandidateSelectionCount);
            Assert.AreEqual("Pending", ReadScalarString(connection, $"SELECT status FROM monitor_delivery_outbox WHERE id = '{thirdId}';"));
        }

        [TestMethod]
        public async Task ClaimNextDueAsync_StopsAfterThreeLostConditionalClaims()
        {
            using NpgsqlConnection connection = _database!.OpenConnection();
            connection.Open();
            DateTime utcNow = ParseUtc("2026-07-14T12:00:00Z");
            Guid[] messageIds = [.. Enumerable.Range(0, 4).Select(_ => Guid.NewGuid())];
            for (int index = 0; index < messageIds.Length; index++)
            {
                InsertOutboxMessage(connection, messageIds[index], "Pending", utcNow.AddMinutes(-4 + index), 0, null, null);
            }

            ForcedContentionDbClient claimant = new(_database.ConnectionString, lostConditionalClaims: 3);
            MonitorDeliveryMessage? claim = await ((IMonitorDeliveryOutboxQueries)(DBClient)claimant).ClaimNextDueAsync(
                MonitorDeliveryProducers.MyAtm,
                utcNow,
                TimeSpan.FromMinutes(2), TestContext.CancellationToken);

            Assert.IsNull(claim);
            CollectionAssert.AreEqual(messageIds.Take(3).ToArray(), claimant.CompetingClaimIds);
            Assert.AreEqual(3, claimant.CandidateSelectionCount);
            Assert.AreEqual("Pending", ReadScalarString(connection, $"SELECT status FROM monitor_delivery_outbox WHERE id = '{messageIds[3]}';"));
            Assert.AreEqual(0, ReadScalarInt(connection, $"SELECT attempt_count FROM monitor_delivery_outbox WHERE id = '{messageIds[3]}';"));
        }

        [TestMethod]
        public async Task ClaimNextDueAsync_RejectsUnknownProducerUsingOrdinalValidation()
        {
            DBClient queries = _testObj!;

            await Assert.ThrowsExactlyAsync<ArgumentException>(() => queries.ClaimNextDueAsync(
                "myatm",
                DateTime.UtcNow,
                TimeSpan.FromMinutes(2), TestContext.CancellationToken));
        }

        [TestMethod]
        public async Task FencedOutboxOutcomes_RejectStaleLeaseWithoutChangingMessageOrWritingAudit()
        {
            using NpgsqlConnection connection = _database!.OpenConnection();
            connection.Open();
            DateTime utcNow = ParseUtc("2026-07-14T12:00:00Z");
            Guid messageId = Guid.NewGuid();
            InsertOutboxMessage(connection, messageId, "Pending", utcNow, 0, null, null);
            DBClient queries = _testObj!;
            DBClient commands = _testObj!;
            MonitorDeliveryMessage? claim = await queries.ClaimNextDueAsync(
                MonitorDeliveryProducers.MyAtm,
                utcNow,
                TimeSpan.FromMinutes(2), TestContext.CancellationToken);

            Assert.IsNotNull(claim);
            Guid staleLeaseId = Guid.NewGuid();
            MonitorDeliveryAudit audit = new(Guid.NewGuid(), "stale@example.test", "Sent ok", utcNow.AddSeconds(1));

            bool completed = await commands.CompleteAsync(messageId, staleLeaseId, utcNow.AddSeconds(1), audit, TestContext.CancellationToken);
            bool retried = await commands.RetryAsync(
                messageId,
                staleLeaseId,
                utcNow.AddMinutes(1),
                "stale retry", TestContext.CancellationToken);
            bool deadLettered = await commands.DeadLetterAsync(
                messageId,
                staleLeaseId,
                utcNow.AddMinutes(1),
                "stale dead letter",
                audit, TestContext.CancellationToken);

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
            using NpgsqlConnection connection = _database!.OpenConnection();
            connection.Open();
            DateTime utcNow = ParseUtc("2026-07-14T12:00:00Z");
            DustMonitorDto monitor = CreateMonitorsList(1, 861).Single();
            _testObj!.WriteMonitorList([monitor]);
            Guid notificationId = Guid.NewGuid();
            InsertNotificationRow(connection, notificationId, utcNow, monitor.Id);
            Guid completedId = Guid.NewGuid();
            Guid deadLetterId = Guid.NewGuid();
            InsertOutboxMessage(connection, completedId, "Pending", utcNow.AddMinutes(-1), 0, null, null);
            InsertOutboxMessage(connection, deadLetterId, "Pending", utcNow, 7, null, null);

            DBClient queries = _testObj!;
            DBClient commands = _testObj!;
            MonitorDeliveryMessage? completedClaim = await queries.ClaimNextDueAsync(
                MonitorDeliveryProducers.MyAtm,
                utcNow,
                TimeSpan.FromMinutes(2), TestContext.CancellationToken);
            MonitorDeliveryMessage? deadLetterClaim = await queries.ClaimNextDueAsync(
                MonitorDeliveryProducers.MyAtm,
                utcNow,
                TimeSpan.FromMinutes(2), TestContext.CancellationToken);

            Assert.IsNotNull(completedClaim);
            Assert.IsNotNull(deadLetterClaim);
            Assert.AreEqual(completedId, completedClaim.Id);
            Assert.AreEqual(deadLetterId, deadLetterClaim.Id);
            Assert.IsTrue(await commands.CompleteAsync(
                completedId,
                completedClaim.LeaseId,
                utcNow.AddSeconds(1),
                new MonitorDeliveryAudit(notificationId, "sent@example.test", "Sent ok", utcNow.AddSeconds(1)), TestContext.CancellationToken));
            Assert.IsTrue(await commands.DeadLetterAsync(
                deadLetterId,
                deadLetterClaim.LeaseId,
                utcNow.AddSeconds(2).ToLocalTime(),
                "permanent failure",
                new MonitorDeliveryAudit(notificationId, "failed@example.test", "permanent failure", utcNow.AddSeconds(2)), TestContext.CancellationToken));

            Assert.AreEqual("Completed", ReadScalarString(connection, $"SELECT status FROM monitor_delivery_outbox WHERE id = '{completedId}';"));
            Assert.AreEqual("DeadLetter", ReadScalarString(connection, $"SELECT status FROM monitor_delivery_outbox WHERE id = '{deadLetterId}';"));
            DateTime deadLetteredAt = ReadScalarDateTime(
                connection,
                $"SELECT dead_lettered_at FROM monitor_delivery_outbox WHERE id = '{deadLetterId}';");
            Assert.AreEqual(utcNow.AddSeconds(2).Ticks, deadLetteredAt.Ticks);
            Assert.AreEqual(DateTimeKind.Utc, deadLetteredAt.Kind);
            Assert.AreEqual(2, ReadScalarInt(connection, "SELECT COUNT(*) FROM notification_sent;"));
        }


        [TestMethod]
        public void TestSetMonitorOffline()
        {
            string connectionString = _database!.ConnectionString;
            using NpgsqlConnection connection = new(connectionString);
            connection.Open();
            int customerId = 861;
            List<DustMonitorDto> monitorsIn = CreateMonitorsList(1, customerId);
            _testObj!.WriteMonitorList(monitorsIn);

            foreach (DustMonitorDto m in monitorsIn)
            {
                _testObj.WriteFleetNr(m.SerialId, m.FleetNr!);
                Assert.IsFalse(m.Offline);
                _testObj.SetMonitorOffline(m.Id, true);
            }
            List<DustMonitorDto> monitorsOut = _testObj.ReadMonitorList(null);
            Assert.HasCount(1, monitorsOut);
            foreach (DustMonitorDto m in monitorsOut)
            {
                Assert.IsTrue(m.Offline);

            }
        }

        [TestMethod]
        public async Task InsertDustDto()
        {
            string serialId = "17239";
            DateTime sampleTime = ParseUtc("2023-10-17T14:37:42");

            _testObj!.InsertDustDtos([ new DustDto(serialId: serialId, avrg: 60, sampleTime: sampleTime,
                                               pm1: 1.0, pm2_5: 2.5, pm10: 10, pmTotal: 13.5,
                                               weather_t: 3.1234, weather_p: 5.5678, weather_rh: 99.87654) ]);

            await using NpgsqlConnection connection = _database!.OpenConnection();
            await connection.OpenAsync(TestContext.CancellationToken);
            await using NpgsqlCommand command = new(
                "SELECT serial_id, sample_time, pm_2_5 FROM my_atm_dust_level ORDER BY sample_time;", connection);
            await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(TestContext.CancellationToken);
            Assert.IsTrue(await reader.ReadAsync(TestContext.CancellationToken));
            Assert.AreEqual(serialId, reader.GetString(0));
            Assert.AreEqual(sampleTime, reader.GetDateTime(1));
            Assert.AreEqual(2.5, reader.GetDouble(2));
            Assert.IsFalse(await reader.ReadAsync(TestContext.CancellationToken));
        }

        [TestMethod]
        public void InsertDustDto_IgnoresDuplicateRowsInSingleBatch()
        {
            string serialId = "17239";
            DateTime sampleTime = ParseUtc("2023-10-17T14:37:42");
            DustDto dto = new(serialId: serialId, avrg: 60, sampleTime: sampleTime,
                pm1: 1.0, pm2_5: 2.5, pm10: 10, pmTotal: 13.5,
                weather_t: 3.1234, weather_p: 5.5678, weather_rh: 99.87654);

            _testObj!.InsertDustDtos([dto, dto]);

            string connectionString = _database!.ConnectionString;
            using NpgsqlConnection connection = new(connectionString);
            connection.Open();
            List<DustDto> dtos = ReadDustDtos(connection);
            Assert.HasCount(1, dtos);
        }

        [TestMethod]
        public void InsertDustDto_IgnoresRowsAlreadyPresentInDatabase()
        {
            string serialId = "17239";
            DateTime sampleTime = ParseUtc("2023-10-17T14:37:42");
            DustDto dto = new(serialId: serialId, avrg: 60, sampleTime: sampleTime,
                pm1: 1.0, pm2_5: 2.5, pm10: 10, pmTotal: 13.5,
                weather_t: 3.1234, weather_p: 5.5678, weather_rh: 99.87654);

            _testObj!.InsertDustDtos([dto]);
            _testObj.InsertDustDtos([dto]);

            string connectionString = _database!.ConnectionString;
            using NpgsqlConnection connection = new(connectionString);
            connection.Open();
            List<DustDto> dtos = ReadDustDtos(connection);

            Assert.HasCount(1, dtos);
        }

        [TestMethod]
        public void TestGetAverageDustLevel()
        {
            string serialId = "98231";
            DateTime startTime = ParseUtc("2023-10-17T14:37:42");
            double pm1Total = .0;
            double pm2_5Total = .0;
            double pm10Total = .0;
            double pmTotalTotal = .0;
            int numDtos = 15;
            for (int i = 0; i < numDtos; i++)
            {
                double pm1 = 1.0 * i;
                double pm2_5 = 2.5 * i;
                int pm10 = 10 * i;
                double pmTotal = 13.5 * i;

                _testObj!.InsertDustDtos([ new DustDto(serialId: serialId, avrg: 60, sampleTime: startTime.AddMinutes(i).AddSeconds(1),
                                   pm1: pm1, pm2_5: pm2_5, pm10: pm10, pmTotal: pmTotal,
                                   weather_t: .0, weather_p: .0, weather_rh: .0) ]);
                pm1Total += pm1;
                pm2_5Total += pm2_5;
                pm10Total += pm10;
                pmTotalTotal += pmTotal;
            }

            double? avgPm1 = _testObj!.GetAverageDustLevel(serialId, "Pm1", startTime, startTime.AddMinutes(15));
            Assert.AreEqual(pm1Total / numDtos, avgPm1);

            double? avgPm2_5 = _testObj!.GetAverageDustLevel(serialId, "Pm2_5", startTime, startTime.AddMinutes(15));
            Assert.AreEqual(pm2_5Total / numDtos, avgPm2_5);

            double? avgPm10 = _testObj!.GetAverageDustLevel(serialId, "Pm10", startTime, startTime.AddMinutes(15));
            Assert.AreEqual(pm10Total / numDtos, avgPm10);

            double? avgPmTotal = _testObj!.GetAverageDustLevel(serialId, "PmTotal", startTime, startTime.AddMinutes(15));
            Assert.AreEqual(pmTotalTotal / numDtos, avgPmTotal);
        }

        private static List<DustMonitorDto> CreateMonitorsList(int numMonitors, int customerId,
                                                               string serialId = "monitor")
        {
            List<DustMonitorDto> monitors = [];
            for (int i = 0; i < numMonitors; i++)
            {
                DateTime dt = DateTime.UtcNow.AddMinutes(i);
                DustMonitorDto monitor = new(id: Guid.NewGuid(), customerId: customerId, listedAtTime: dt, serialId: serialId + i,
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
                []);

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
                [],
                utcNow,
                [],
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
                [],
                null,
                [.. occurrences.Select(occurrence => new MyAtmAlertOccurrenceInput(
                    occurrence.Key,
                    occurrence.MonitorId,
                    occurrence.RuleId,
                    occurrence.Period,
                    occurrence.AlertType,
                    occurrence.Field,
                    occurrence.LimitOn,
                    occurrence.Level,
                    occurrence.TriggeredAt,
                    CreateDeliveryPlan(occurrence, utcNow)))],
                utcNow);

        private static RuleAlertDeliveryPlan CreateDeliveryPlan(
            AlertOccurrenceProposal occurrence,
            DateTime createdAt)
        {
            Guid notificationId = MonitorDeliveryIdentity.CreateGuid($"notification:{occurrence.Key}");
            NotificationDto notification = new(
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
            string deliveryKey = $"{occurrence.Key}:MqttAlert:alert";
            string payload = System.Text.Json.JsonSerializer.Serialize(new MonitorDeliveryPayloadV1(
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
            List<RvtContactDto> contacts =
            [
                new(true, false, "alert@example.test", null, null, null)
            ];
            RuleAlertDeliveryPlan plan = new RuleAlertDeliveryPlanner().Plan(
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
                    Deliveries = [.. plan.Deliveries.Where(delivery => delivery.Kind != MonitorDeliveryKind.MqttAlert)]
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
            using NpgsqlCommand command = new(sql, connection);
            return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
        }

        private static string ReadScalarString(NpgsqlConnection connection, string sql)
        {
            using NpgsqlCommand command = new(sql, connection);
            return Convert.ToString(command.ExecuteScalar(), CultureInfo.InvariantCulture)!;
        }

        private static DateTime ReadScalarDateTime(NpgsqlConnection connection, string sql)
        {
            using NpgsqlCommand command = new(sql, connection);
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
            using NpgsqlCommand command = new(
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

        private static void InsertNotificationRow(
            NpgsqlConnection connection,
            Guid notificationId,
            DateTime notificationTime,
            Guid monitorId)
        {
            using NpgsqlCommand command = new(
                @"INSERT INTO notification
                    (id, notification_time, limit_on, averaging_period, level, closed_time,
                     closed_by_user, alert_type, alert_field, monitor_id)
                  VALUES
                    (@Id, @NotificationTime, 1, 60, 2, NULL, NULL, @AlertType, 'Pm10', @MonitorId);",
                connection);
            command.Parameters.AddWithValue("@Id", notificationId);
            command.Parameters.AddWithValue("@NotificationTime", notificationTime);
            command.Parameters.AddWithValue("@AlertType", (int)AlertType.Alert);
            command.Parameters.AddWithValue("@MonitorId", monitorId);
            command.ExecuteNonQuery();
        }

        private sealed class ForcedContentionDbClient(string connectionString, int lostConditionalClaims) : DBClient(connectionString)
        {
            private readonly DBClient _competingClient = new(connectionString);
            private readonly int _lostConditionalClaims = lostConditionalClaims;

            public int CandidateSelectionCount { get; private set; }
            public List<Guid> CompetingClaimIds { get; } = [];

            protected override async Task BeforeConditionalOutboxClaimAsync(
                Guid candidateId,
                DateTime utcNow,
                TimeSpan lease,
                CancellationToken cancellationToken)
            {
                CandidateSelectionCount++;
                if (CandidateSelectionCount > _lostConditionalClaims)
                {
                    return;
                }

                MonitorDeliveryMessage? competingClaim = await ((IMonitorDeliveryOutboxQueries)_competingClient).ClaimNextDueAsync(
                    MonitorDeliveryProducers.MyAtm,
                    utcNow,
                    lease,
                    cancellationToken);
                Assert.IsNotNull(competingClaim);
                Assert.AreEqual(candidateId, competingClaim.Id);
                CompetingClaimIds.Add(competingClaim.Id);
            }
        }

        private static List<Guid> ReadOutboxLeaseIds(NpgsqlConnection connection)
        {
            using NpgsqlCommand command = new(
                "SELECT lease_id FROM monitor_delivery_outbox WHERE producer = 'MyAtm' AND lease_id IS NOT NULL;",
                connection);
            using NpgsqlDataReader reader = command.ExecuteReader();
            List<Guid> leaseIds = [];
            while (reader.Read())
            {
                leaseIds.Add(reader.GetGuid(0));
            }

            return leaseIds;
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

        private static void InsertContact(NpgsqlConnection connection, Guid monitorId, ContactMethod contactMethod,
                                          string email, string phoneNo, Guid siteUserId,
        DateTime? sendStartTime = null, DateTime? sendEndTime = null)
        {
            Guid contractId = Guid.NewGuid();
            Guid userId = Guid.NewGuid();
            Guid siteId = Guid.NewGuid();

            {
                string sql = @"INSERT INTO contract
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
                string sql = @"INSERT INTO deployment
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
                string sql = @"UPDATE contract SET site_id = @SiteId WHERE id = @ContractId;";
                using NpgsqlCommand cmd = new(sql, connection);
                cmd.Parameters.AddWithValue("@SiteId", siteId);
                cmd.Parameters.AddWithValue("@ContractId", contractId);
                cmd.ExecuteNonQuery();
            }
            {
                string sql = @"INSERT INTO ""AspNetUsers""
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
                string sql = @"INSERT INTO site_user
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
                string sql = @"INSERT INTO notification_setting
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

            using NpgsqlConnection connection = new(connectionString);
            connection.Open();
            string sql = @"SELECT email, sms FROM notification_setting WHERE site_user_id = @SiteUserId;";

            using NpgsqlCommand cmd = new(sql, connection);
            cmd.Parameters.AddWithValue("@SiteUserId", siteUserId);

            using NpgsqlDataReader reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                bool email = reader.GetBoolean(0);
                bool sms = reader.GetBoolean(1);
                return RvtContactDto.FromFlags(email, sms);
            }
            throw AdapterException.Of("Failed to ReadContactMethod");
        }

        private static List<RvtContactDto> ReadContacts(NpgsqlConnection connection, Guid siteUserId)
        {
            string sql = @"SELECT ""Email"", ""PhoneNumber"", ""Id"" FROM ""AspNetUsers"";";
            using NpgsqlCommand cmd = new(sql, connection);
            List<RvtContactDto> contacts = [];
            using NpgsqlDataReader reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                string emailAddress = reader.GetString(0);
                string? phoneNumber = reader.IsDBNull(1) ? null : reader.GetString(1);
                string id = reader.GetString(2);
                ContactMethod contactMethod = ReadContactMethod(_database!.ConnectionString, siteUserId);
                contacts.Add(new RvtContactDto(contactMethod: contactMethod,
                                               emailAddress: emailAddress,
                                               phoneNumber: phoneNumber,
                                               sendStartTime: null,
                                               sendEndTime: null));
            }
            return contacts;
        }

        private static List<DustDto> ReadDustDtos(NpgsqlConnection connection)
        {
            string sql = @"SELECT serial_id, avrg, sample_time, pm_1, pm_2_5, pm_10, pm_total,
                               weather_t, weather_p, weather_rh
                        FROM my_atm_dust_level;";
            using NpgsqlCommand cmd = new(sql, connection);
            List<DustDto> dtos = [];
            using NpgsqlDataReader reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                string serialId = reader.GetString(0);
                int avrg = reader.GetInt32(1);
                DateTime sampleTime = reader.GetDateTime(2);
                double pm1 = reader.GetDouble(3);
                double pm2_5 = reader.GetDouble(4);
                double pm10 = reader.GetDouble(5);
                double pmTotal = reader.GetDouble(6);
                double weather_t = reader.GetDouble(7);
                double weather_p = reader.GetDouble(8);
                double weather_rh = reader.GetDouble(9);

                dtos.Add(new DustDto(serialId: serialId, avrg: avrg, sampleTime: sampleTime,
                                     pm1: pm1, pm2_5: pm2_5, pm10: pm10, pmTotal: pmTotal,
                                     weather_t: weather_t, weather_p: weather_p, weather_rh: weather_rh));
            }

            return dtos;
        }

        public TestContext TestContext { get; set; } = null!;
    }
}
