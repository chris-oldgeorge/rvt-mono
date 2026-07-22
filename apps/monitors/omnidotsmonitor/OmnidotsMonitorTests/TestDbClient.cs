using System.Data;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;
using Omnidots.Api.Db;
using Omnidots.Model.Dto;
using Omnidots.Model.Json;
using Rvt.Monitor.Common.Configuration;
using Rvt.Monitor.Common.Data;
using Rvt.Monitor.Common.Diagnostics;
using Rvt.Monitor.Common.Notifications;
using Rvt.Monitor.Common.Utilities;
using Rvt.Monitor.IntegrationTesting;
using AlertActivityTimeDto = Rvt.Monitor.Common.Notifications.AlertActivityTimeDto;
using ContactMethod = Rvt.Monitor.Common.Notifications.ContactMethod;
using NotificationDto = Rvt.Monitor.Common.Notifications.NotificationDto;
using RvtContactDto = Rvt.Monitor.Common.Notifications.RvtContactDto;
namespace OmnidotsAdapterTests
{

    // Summary: Exercises Omnidots PostgreSQL database persistence against a scoped fixture.
    // Major updates:
    // - 2026-06-18 Test fixture hardening: aligned vibration timestamp read helpers with SQL Server DateTime round-trips.
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

        [TestMethod]
        public void ReadImportCursor_WhenNoCursorExists_ReturnsNullForEverySeries()
        {
            Assert.IsNull(testObj!.ReadImportCursor("cursor-empty", OmnidotsMeasurementSeries.Peak));
            Assert.IsNull(testObj.ReadImportCursor("cursor-empty", OmnidotsMeasurementSeries.Veff));
            Assert.IsNull(testObj.ReadImportCursor("cursor-empty", OmnidotsMeasurementSeries.Vdv));
        }

        [TestMethod]
        public void ReadLatestMeasurementTime_ReturnsEachSeriesMaximumWithoutCrossSeriesLeakage()
        {
            const string serialId = "latest-series";
            var peakTime = Utc(2026, 7, 14, 8, 0);
            var veffTime = peakTime.AddMinutes(10);
            var vdvTime = peakTime.AddMinutes(20);

            testObj!.InsertPeakRecordsTable(PeakTable(serialId, peakTime.AddMinutes(-1), peakTime));
            testObj.InsertVeffRecords(serialId,
            [
                VeffRecord(veffTime.AddMinutes(-1)),
                VeffRecord(veffTime)
            ]);
            testObj.InsertVdvRecords(serialId,
            [
                VdvRecord(vdvTime.AddMinutes(-1)),
                VdvRecord(vdvTime)
            ]);

            var latestPeak = testObj.ReadLatestMeasurementTime(serialId, OmnidotsMeasurementSeries.Peak);
            var latestVeff = testObj.ReadLatestMeasurementTime(serialId, OmnidotsMeasurementSeries.Veff);
            var latestVdv = testObj.ReadLatestMeasurementTime(serialId, OmnidotsMeasurementSeries.Vdv);
            Assert.AreEqual(peakTime, latestPeak);
            Assert.AreEqual(veffTime, latestVeff);
            Assert.AreEqual(vdvTime, latestVdv);
            Assert.AreEqual(DateTimeKind.Utc, latestPeak!.Value.Kind);
            Assert.AreEqual(DateTimeKind.Utc, latestVeff!.Value.Kind);
            Assert.AreEqual(DateTimeKind.Utc, latestVdv!.Value.Kind);
            Assert.IsNull(testObj.ReadLatestMeasurementTime("other-serial", OmnidotsMeasurementSeries.Peak));
        }

        [TestMethod]
        public void ImportSeries_AdvancesOnlyItsOwnCursor()
        {
            const string serialId = "isolated-series";
            var veffTime = Utc(2026, 7, 14, 9, 0);
            var vdvTime = veffTime.AddMinutes(5);

            testObj!.ImportVeffRecords(serialId, [VeffRecord(veffTime)], veffTime);

            Assert.AreEqual(veffTime, testObj.ReadImportCursor(serialId, OmnidotsMeasurementSeries.Veff));
            Assert.IsNull(testObj.ReadImportCursor(serialId, OmnidotsMeasurementSeries.Peak));
            Assert.IsNull(testObj.ReadImportCursor(serialId, OmnidotsMeasurementSeries.Vdv));

            testObj.ImportVdvRecords(serialId, [VdvRecord(vdvTime)], vdvTime);

            Assert.AreEqual(veffTime, testObj.ReadImportCursor(serialId, OmnidotsMeasurementSeries.Veff));
            Assert.AreEqual(vdvTime, testObj.ReadImportCursor(serialId, OmnidotsMeasurementSeries.Vdv));
            Assert.IsNull(testObj.ReadImportCursor(serialId, OmnidotsMeasurementSeries.Peak));
        }

        [TestMethod]
        public void ImportVeffRecords_DuplicateOrOlderBatch_DoesNotMoveCursorOrUpdatedAtBackward()
        {
            const string serialId = "monotonic-veff";
            var older = Utc(2026, 7, 14, 10, 0);
            var newer = older.AddMinutes(1);

            testObj!.ImportVeffRecords(serialId, [VeffRecord(newer)], newer);
            var updatedAt = ReadCursorUpdatedAt(serialId, "Veff");

            testObj.ImportVeffRecords(serialId, [VeffRecord(older)], older);

            Assert.AreEqual(newer, testObj.ReadImportCursor(serialId, OmnidotsMeasurementSeries.Veff));
            Assert.AreEqual(updatedAt, ReadCursorUpdatedAt(serialId, "Veff"));
            Assert.AreEqual(2, CountRows(database!.ConnectionString, "omnidots_veff_level"));
        }

        [TestMethod]
        public void ImportPeakRecords_AtomicallyAdvancesPeakCursorAndCompatibilityTimestamp()
        {
            var monitor = OmnidotsFixture.MonitorsList(1).Single();
            var newest = Utc(2026, 7, 14, 11, 0);
            testObj!.WriteMonitorList([monitor]);

            testObj.ImportPeakRecords(monitor.SerialId, PeakTable(monitor.SerialId, newest), newest);

            Assert.AreEqual(newest, testObj.ReadImportCursor(monitor.SerialId, OmnidotsMeasurementSeries.Peak));
            Assert.AreEqual(newest, testObj.ReadMonitor(monitor.SerialId).LastDataTime);
            Assert.IsNull(testObj.ReadImportCursor(monitor.SerialId, OmnidotsMeasurementSeries.Veff));
            Assert.IsNull(testObj.ReadImportCursor(monitor.SerialId, OmnidotsMeasurementSeries.Vdv));
        }

        [TestMethod]
        public void ImportVeffRecords_WhenCursorWriteFails_RollsBackMeasurementsAndCursor()
        {
            const string serialId = "rollback-veff";
            var sampleTime = Utc(2026, 7, 14, 12, 0);
            using var connection = database!.OpenConnection();
            connection.Open();

            try
            {
                using (var install = new NpgsqlCommand(
                    """
                    CREATE FUNCTION fail_omnidots_cursor_write() RETURNS trigger
                    LANGUAGE plpgsql AS $$
                    BEGIN
                        RAISE EXCEPTION 'forced cursor failure';
                    END;
                    $$;
                    CREATE TRIGGER fail_omnidots_cursor_write
                    BEFORE INSERT OR UPDATE ON omnidots_import_cursor
                    FOR EACH ROW EXECUTE FUNCTION fail_omnidots_cursor_write();
                    """, connection))
                {
                    install.ExecuteNonQuery();
                }

                Assert.ThrowsExactly<Microsoft.EntityFrameworkCore.DbUpdateException>(() =>
                    testObj!.ImportVeffRecords(serialId, [VeffRecord(sampleTime)], sampleTime));

                Assert.AreEqual(0, CountRows(database.ConnectionString, "omnidots_veff_level"));
                Assert.IsNull(testObj!.ReadImportCursor(serialId, OmnidotsMeasurementSeries.Veff));
            }
            finally
            {
                using var cleanup = new NpgsqlCommand(
                    """
                    DROP TRIGGER IF EXISTS fail_omnidots_cursor_write ON omnidots_import_cursor;
                    DROP FUNCTION IF EXISTS fail_omnidots_cursor_write();
                    """, connection);
                cleanup.ExecuteNonQuery();
            }
        }

        [TestMethod]
        public void ImportMethods_EmptyBatches_DoNotValidateOrMutateAnyState()
        {
            var monitor = OmnidotsFixture.MonitorsList(1).Single();
            var existingLastDataTime = Utc(2026, 7, 14, 13, 0);
            testObj!.WriteMonitorList([monitor]);
            testObj.WriteLatestTimestamp(monitor.SerialId, existingLastDataTime);

            testObj.ImportPeakRecords(monitor.SerialId, PeakTable(monitor.SerialId), default);
            testObj.ImportVeffRecords(monitor.SerialId, Array.Empty<VeffRecordDto>(), default);
            testObj.ImportVdvRecords(monitor.SerialId, Array.Empty<VdvRecordDto>(), default);

            Assert.IsNull(testObj.ReadImportCursor(monitor.SerialId, OmnidotsMeasurementSeries.Peak));
            Assert.IsNull(testObj.ReadImportCursor(monitor.SerialId, OmnidotsMeasurementSeries.Veff));
            Assert.IsNull(testObj.ReadImportCursor(monitor.SerialId, OmnidotsMeasurementSeries.Vdv));
            Assert.AreEqual(existingLastDataTime, testObj.ReadMonitor(monitor.SerialId).LastDataTime);
            Assert.AreEqual(0, CountRows(database!.ConnectionString, "omnidots_peak_level"));
            Assert.AreEqual(0, CountRows(database.ConnectionString, "omnidots_veff_level"));
            Assert.AreEqual(0, CountRows(database.ConnectionString, "omnidots_vdv_level"));
        }

        [TestMethod]
        public void ImportMethods_NonEmptyBatchRequiresNewestSampleAtToMatchBatchMaximum()
        {
            const string serialId = "newest-validation";
            var sampleTime = Utc(2026, 7, 14, 14, 0);
            var wrongNewest = sampleTime.AddMinutes(-1);

            Assert.ThrowsExactly<ArgumentException>(() =>
                testObj!.ImportPeakRecords(serialId, PeakTable(serialId, sampleTime), wrongNewest));
            Assert.ThrowsExactly<ArgumentException>(() =>
                testObj!.ImportVeffRecords(serialId, [VeffRecord(sampleTime)], wrongNewest));
            Assert.ThrowsExactly<ArgumentException>(() =>
                testObj!.ImportVdvRecords(serialId, [VdvRecord(sampleTime)], wrongNewest));

            Assert.AreEqual(0, CountRows(database!.ConnectionString, "omnidots_peak_level"));
            Assert.AreEqual(0, CountRows(database.ConnectionString, "omnidots_veff_level"));
            Assert.AreEqual(0, CountRows(database.ConnectionString, "omnidots_vdv_level"));
        }

        [TestMethod]
        public void ImportPeakRecords_OlderReplay_DoesNotMoveCursorOrCompatibilityTimestampBackward()
        {
            var monitor = OmnidotsFixture.MonitorsList(1).Single();
            var older = Utc(2026, 7, 14, 15, 0);
            var newer = older.AddMinutes(1);
            testObj!.WriteMonitorList([monitor]);
            testObj.ImportPeakRecords(monitor.SerialId, PeakTable(monitor.SerialId, newer), newer);

            testObj.ImportPeakRecords(monitor.SerialId, PeakTable(monitor.SerialId, older), older);

            Assert.AreEqual(newer, testObj.ReadImportCursor(monitor.SerialId, OmnidotsMeasurementSeries.Peak));
            Assert.AreEqual(newer, testObj.ReadMonitor(monitor.SerialId).LastDataTime);
            Assert.AreEqual(2, CountRows(database!.ConnectionString, "omnidots_peak_level"));
        }

        [TestMethod]
        public void ImportPeakRecords_FirstCursorDoesNotRegressNewerCompatibilityTimestamp()
        {
            var monitor = OmnidotsFixture.MonitorsList(1).Single();
            var importedSample = Utc(2026, 7, 14, 15, 30);
            var existingLastDataTime = importedSample.AddMinutes(1);
            testObj!.WriteMonitorList([monitor]);
            testObj.WriteLatestTimestamp(monitor.SerialId, existingLastDataTime);

            testObj.ImportPeakRecords(
                monitor.SerialId,
                PeakTable(monitor.SerialId, importedSample),
                importedSample);

            Assert.AreEqual(importedSample, testObj.ReadImportCursor(
                monitor.SerialId,
                OmnidotsMeasurementSeries.Peak));
            Assert.AreEqual(existingLastDataTime, testObj.ReadMonitor(monitor.SerialId).LastDataTime);
        }

        [TestMethod]
        public void InsertPeakRecordsTable_MixedSerialRows_ImportsEachSerialIndependently()
        {
            const string firstSerial = "mixed-first";
            const string secondSerial = "mixed-second";
            var firstTime = Utc(2026, 7, 14, 16, 0);
            var secondTime = firstTime.AddMinutes(1);
            var table = PeakTable(firstSerial, firstTime);
            AddPeakRow(table, secondSerial, secondTime);

            testObj!.InsertPeakRecordsTable(table);

            Assert.AreEqual(1, CountRows(database!.ConnectionString, "omnidots_peak_level", firstSerial));
            Assert.AreEqual(1, CountRows(database.ConnectionString, "omnidots_peak_level", secondSerial));
            Assert.AreEqual(firstTime, testObj.ReadImportCursor(firstSerial, OmnidotsMeasurementSeries.Peak));
            Assert.AreEqual(secondTime, testObj.ReadImportCursor(secondSerial, OmnidotsMeasurementSeries.Peak));
        }

        [TestMethod]
        public void ImportPeakRecords_RowSerialMismatch_RejectsWholeBatch()
        {
            const string requestedSerial = "requested-serial";
            const string rowSerial = "row-serial";
            var sampleTime = Utc(2026, 7, 14, 17, 0);

            Assert.ThrowsExactly<ArgumentException>(() =>
                testObj!.ImportPeakRecords(requestedSerial, PeakTable(rowSerial, sampleTime), sampleTime));

            Assert.AreEqual(0, CountRows(database!.ConnectionString, "omnidots_peak_level"));
            Assert.IsNull(testObj!.ReadImportCursor(requestedSerial, OmnidotsMeasurementSeries.Peak));
            Assert.IsNull(testObj.ReadImportCursor(rowSerial, OmnidotsMeasurementSeries.Peak));
        }

        [TestMethod]
        public void NormalizeLatestMeasurementTime_ReturnsUtcForEveryDateTimeKind()
        {
            var utc = Utc(2026, 7, 14, 18, 0);
            var local = utc.ToLocalTime();
            var unspecified = DateTime.SpecifyKind(utc, DateTimeKind.Unspecified);

            Assert.AreEqual(utc, DBClient.NormalizeLatestMeasurementTime(utc));
            Assert.AreEqual(utc, DBClient.NormalizeLatestMeasurementTime(local));
            Assert.AreEqual(utc, DBClient.NormalizeLatestMeasurementTime(unspecified));
            Assert.IsNull(DBClient.NormalizeLatestMeasurementTime(null));
            Assert.AreEqual(DateTimeKind.Utc, DBClient.NormalizeLatestMeasurementTime(local)!.Value.Kind);
            Assert.AreEqual(DateTimeKind.Utc, DBClient.NormalizeLatestMeasurementTime(unspecified)!.Value.Kind);
        }

        [TestMethod]
        public async Task ImportVeffRecords_OverlappingConcurrentBatches_RetryToMaximumCursor()
        {
            const string serialId = "concurrent-veff";
            var first = Utc(2026, 7, 14, 19, 0);
            var overlap = first.AddMinutes(1);
            var last = overlap.AddMinutes(1);
            using var firstAttemptBarrier = new Barrier(2);
            void BeforeSave(OmnidotsMeasurementSeries series, int attempt)
            {
                if (series == OmnidotsMeasurementSeries.Veff && attempt == 1 &&
                    !firstAttemptBarrier.SignalAndWait(TimeSpan.FromSeconds(10)))
                {
                    throw new TimeoutException("Concurrent import attempts did not overlap before SaveChanges.");
                }
            }

            var firstClient = new DBClient(database!.ConnectionString, BeforeSave);
            var secondClient = new DBClient(database.ConnectionString, BeforeSave);
            var firstImport = Task.Run(() =>
                firstClient.ImportVeffRecords(serialId, [VeffRecord(first), VeffRecord(overlap)], overlap));
            var secondImport = Task.Run(() =>
                secondClient.ImportVeffRecords(serialId, [VeffRecord(overlap), VeffRecord(last)], last));

            await Task.WhenAll(firstImport, secondImport).WaitAsync(TimeSpan.FromSeconds(20));

            Assert.AreEqual(3, CountRows(database.ConnectionString, "omnidots_veff_level", serialId));
            Assert.AreEqual(last, testObj!.ReadImportCursor(serialId, OmnidotsMeasurementSeries.Veff));
        }

        [TestMethod]
        public void ImportVeffRecords_PersistentSerializationFailure_StopsAfterThreeAttempts()
        {
            const string serialId = "bounded-retry";
            var sampleTime = Utc(2026, 7, 14, 20, 0);
            var attempts = 0;
            var client = new DBClient(database!.ConnectionString, (_, attempt) =>
            {
                attempts = attempt;
                throw new PostgresException(
                    "forced serialization failure",
                    "ERROR",
                    "ERROR",
                    "40001");
            });

            Assert.ThrowsExactly<PostgresException>(() =>
                client.ImportVeffRecords(serialId, [VeffRecord(sampleTime)], sampleTime));

            Assert.AreEqual(3, attempts);
            Assert.AreEqual(0, CountRows(database.ConnectionString, "omnidots_veff_level", serialId));
            Assert.IsNull(testObj!.ReadImportCursor(serialId, OmnidotsMeasurementSeries.Veff));
        }

        [TestMethod]
        public void TestMonitors()
        {
            var numMonitors = 5;
            var monitorsIn = OmnidotsFixture.MonitorsList(numMonitors, null, true);
            testObj!.WriteMonitorList(monitorsIn);
            var monitorsOut = testObj.ReadMonitorList(null);

            AssertMonitorsList(monitorsIn, monitorsOut);

            // write again - should  be same number of monitors
            testObj!.WriteMonitorList(monitorsIn);
            AssertMonitorsList(monitorsIn, monitorsOut);
        }


        private static int CountRows(string connectionString, string tableName)
        {
            using var connection = new NpgsqlConnection(connectionString);
            connection.Open();
            var sql = string.Format(@"SELECT Count(*) FROM {0};", tableName);

            using NpgsqlCommand cmd = new(sql, connection);
            return Convert.ToInt32(cmd.ExecuteScalar());
        }

        private static int CountRows(string connectionString, string tableName, string serialId)
        {
            using var connection = new NpgsqlConnection(connectionString);
            connection.Open();
            var sql = string.Format(@"SELECT Count(*) FROM {0} WHERE serial_id = $1;", tableName);
            using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue(serialId);
            return Convert.ToInt32(command.ExecuteScalar());
        }

        private static DateTime ReadCursorUpdatedAt(string serialId, string series)
        {
            using var connection = database!.OpenConnection();
            connection.Open();
            using var command = new NpgsqlCommand(
                "SELECT updated_at FROM omnidots_import_cursor WHERE serial_id = $1 AND series = $2;",
                connection);
            command.Parameters.AddWithValue(serialId);
            command.Parameters.AddWithValue(series);
            return Convert.ToDateTime(command.ExecuteScalar());
        }

        private static DateTime Utc(int year, int month, int day, int hour, int minute) =>
            new(year, month, day, hour, minute, 0, DateTimeKind.Utc);

        private static VeffRecordDto VeffRecord(DateTime sampleTime)
        {
            var record = new VeffRecordDto(1.0, 2.0, 3.0, new DateTimeOffset(sampleTime).ToUnixTimeMilliseconds())
            {
                SampleTime = sampleTime
            };
            return record;
        }

        private static VdvRecordDto VdvRecord(DateTime sampleTime)
        {
            var record = new VdvRecordDto(
                1.0,
                2.0,
                3.0,
                new DateTimeOffset(sampleTime).ToUnixTimeMilliseconds(),
                "1.0",
                "2.0",
                "3.0")
            {
                SampleTime = sampleTime
            };
            return record;
        }

        private static DataTable PeakTable(string serialId, params DateTime[] sampleTimes)
        {
            var table = new DataTable("Results");
            table.Columns.Add("SerialId", typeof(string));
            table.Columns.Add("SampleTime", typeof(DateTime));
            foreach (var columnName in new[]
                     {
                         "XFdom", "XVtop", "XVtopOverflow",
                         "YFdom", "YVtop", "YVtopOverflow",
                         "ZFdom", "ZVtop", "ZVtopOverflow"
                     })
            {
                table.Columns.Add(columnName, typeof(double)).AllowDBNull = true;
            }

            foreach (var sampleTime in sampleTimes)
            {
                AddPeakRow(table, serialId, sampleTime);
            }

            return table;
        }

        private static void AddPeakRow(DataTable table, string serialId, DateTime sampleTime)
        {
            var row = table.NewRow();
            row["SerialId"] = serialId;
            row["SampleTime"] = sampleTime;
            table.Rows.Add(row);
        }

        private void AssertMonitorsList(List<VibrationMonitorDto> expected, List<VibrationMonitorDto> actual)
        {
            var connectionString = database!.ConnectionString;

            Assert.AreEqual(expected.Count, actual.Count);
            var orderedmonitorsOut = actual.OrderBy(o => o.SerialId).ToList();
            Assert.IsTrue(TestUtil.AreEqual(expected, orderedmonitorsOut));

            foreach (var monitor in expected)
            {
                var m = testObj!.ReadMonitor(monitor.SerialId);
                Assert.IsNotNull(m);
                Assert.AreEqual(monitor.ListedAtTime, m.ListedAtTime);
                Assert.AreEqual(monitor.SerialId, m.SerialId);
                Assert.AreEqual(monitor.Model, m.Model);
                Assert.AreEqual(monitor.Latitude, m.Latitude);
                Assert.AreEqual(monitor.Longitude, m.Longitude);
                Assert.AreEqual(monitor.Address, m.Address);
                Assert.AreEqual(monitor.TimeZone, m.TimeZone);
                Assert.AreEqual(monitor.CustomerDisplayName, m.CustomerDisplayName);
                Assert.AreEqual(monitor.Manufacturer, m.Manufacturer);
                Assert.AreEqual(monitor.FirmwareVersion, m.FirmwareVersion);
                Assert.AreEqual(monitor.LastDataTime, m.LastDataTime);
                Assert.IsTrue(TestUtil.AreEqual(monitor.MonitorStatus, m.MonitorStatus));
                Assert.IsTrue(TestUtil.AreEqual(monitor.Sensor, m.Sensor));
                Assert.AreEqual(monitor.Offline, m.Offline);


                Assert.AreEqual(expected.Count, CountRows(connectionString, "omnidots_monitor_status"));
                Assert.AreEqual(expected.Count, CountRows(connectionString, "omnidots_sensor"));

            }
        }

        [TestMethod]
        public void TestReadMonitorBadSerialId()
        {
            var exception = Assert.ThrowsExactly<AdapterException>(() =>
            {

                testObj!.ReadMonitor("bad-serial-id");
            });
            Assert.AreEqual(exception.Message, "No monitor with SerialId='bad-serial-id'");
        }

        [TestMethod]
        public void TestReadAlertRules()
        {
            var connectionString = database!.ConnectionString;
            using var connection = new NpgsqlConnection(connectionString);
            connection.Open();

            var serialId = "12345";
            var monitorsIn = OmnidotsFixture.MonitorsList(1);
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
            var monitorsIn = OmnidotsFixture.MonitorsList(numMonitors);
            testObj!.WriteMonitorList(monitorsIn);
            var monitorId = monitorsIn[0].Id;
            var serialId = monitorsIn[0].SerialId;
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
            InsertContact(connection, monitorsIn[1].Id, ContactMethod.Email, email, phoneNo, Guid.NewGuid());

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
        public void TestWriteExceptionUsesPostgreSqlErrorLog()
        {
            var connectionString = database!.ConnectionString;

            var TAG = "MyTestError";
            var MESSAGE = "bang";

            MonitorDb.WriteException(
                connectionString,
                TAG,
                AdapterException.Of(MESSAGE),
                "OmnidotsMonitorTests",
                "test",
                new MonitorDbOptions(MonitorDatabaseProvider.PostgreSql, new Dictionary<string, string>()));

            using var connection = new NpgsqlConnection(connectionString);
            connection.Open();

            var sql = @"SELECT variables, message, logged_at FROM error_log";
            using NpgsqlCommand cmd = new(sql, connection);
            using NpgsqlDataReader reader = cmd.ExecuteReader();

            var count = 0;
            while (reader.Read())
            {
                count++;
                Assert.AreEqual(TAG, reader.GetString(0));
                Assert.AreEqual(MESSAGE, reader.GetString(1));
                Assert.IsTrue(reader.GetDateTime(2) <= DateTime.UtcNow);

            }
            Assert.AreEqual(1, count);
        }

        [TestMethod]
        public void TestWriteLatestTimestamp()
        {

            var monitors = OmnidotsFixture.MonitorsList(1);
            Assert.AreEqual(1, monitors.Count);

            testObj!.WriteMonitorList(monitors);

            var lastDataTime = DateTime.Parse("2023-10-18T14:35:42Z").ToUniversalTime();
            testObj.WriteLatestTimestamp("1", lastDataTime);

            monitors = testObj.ReadMonitorList(null);
            Assert.AreEqual(1, monitors.Count);

            var monitor = monitors[0];
            Assert.AreEqual(lastDataTime, monitor.LastDataTime);
        }

        [TestMethod]
        public void TestReadWriteNotification()
        {
            var connectionString = database!.ConnectionString;
            using var connection = new NpgsqlConnection(connectionString);
            connection.Open();

            var serialId = "1";

            var monitorsIn = OmnidotsFixture.MonitorsList(1);
            testObj!.WriteMonitorList(monitorsIn);
            var monitorsOut = testObj.ReadMonitorList(null);
            Assert.AreEqual(1, monitorsOut.Count);
            var monitorId = monitorsOut[0].Id;


            // add an alert and contact as RvtAlertContacts table has foreign key constraints
            InsertAlertRule(connection, 21, serialId, monitorId);
            var rules = testObj!.ReadRules(serialId);
            Assert.AreEqual(1, rules.Count);
            var email = "foobob@bbb.com";
            var phoneNo = "01238867890";
            var siteUserId = Guid.NewGuid();
            InsertContact(connection, monitorId, ContactMethod.Email, email, phoneNo, siteUserId);
            var contacts = ReadContacts(connection, siteUserId);
            Assert.AreEqual(1, contacts.Count);

            var dt = DateTime.Parse("2023-10-18T11:19:00Z").ToUniversalTime();
            var alertIn = new NotificationDto(id: Guid.NewGuid(),
                                              notificationTime: dt,
                                              limitOn: rules[0].LimitOn,
                                              averagingPeriod: rules[0].AveragingPeriod,
                                              level: 99.876,
                                              closedTime: null,
                                              closedByUser: null,
                                              alertType: rules[0].AlertType,
                                              alertField: rules[0].Field,
                                              monitorId: monitorId);

            testObj.WriteNotification(alertIn);

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

            var notifictions = testObj.ReadNotifications(monitorId, dt.AddMinutes(-5));
            Assert.AreEqual(1, notifictions.Count);
            var notifiction = notifictions[0];
            Assert.AreEqual(alertIn.Id, notifiction.Id);
            Assert.AreEqual(alertIn.Level, notifiction.Level);
            Assert.AreEqual(alertIn.NotificationTime, notifiction.NotificationTime);
            Assert.AreEqual(alertIn.LimitOn, notifiction.LimitOn);
            Assert.AreEqual(alertIn.AveragingPeriod, notifiction.AveragingPeriod);
            Assert.AreEqual(alertIn.AlertField, notifiction.AlertField);
            Assert.AreEqual(alertIn.AlertType, notifiction.AlertType);
            Assert.AreEqual(alertIn.MonitorId, notifiction.MonitorId);
        }

        [TestMethod]
        public void TestUpdateAlertRule()
        {
            var connectionString = database!.ConnectionString;
            using var connection = new NpgsqlConnection(connectionString);
            connection.Open();
            var serialId = "1";
            var monitorsIn = OmnidotsFixture.MonitorsList(1);
            testObj!.WriteMonitorList(monitorsIn);
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
        }

        [TestMethod]
        public void TestInsertPeakRecord_Success()
        {
            var serialId = "123";
            var epocMillis = 1699960800001L;
            var sampleTime = DateTimeOffset.FromUnixTimeMilliseconds(epocMillis).UtcDateTime;

            var x = new FDomVtopOverflow(vtop: 1.0, fdom: 2.7, vtopOverflow: 4.5);
            var y = new FDomVtopOverflow(vtop: 2.2, fdom: 6.7, vtopOverflow: 2.33);
            var z = new FDomVtopOverflow(vtop: 4.222, fdom: 4.7, vtopOverflow: 11.5);

            var peakRecords = new List<PeakRecordDto>
                {
                 new PeakRecordDto(x: x,
                                   y: y,
                                   z: z,
                                   epocMillis: epocMillis)
            };
            peakRecords[0].SampleTime = sampleTime;

            testObj!.InsertPeakRecords(serialId, peakRecords);

            var connectionString = database!.ConnectionString;
            using var connection = new NpgsqlConnection(connectionString);
            connection.Open();
            var dtos = ReadPeakRecords(connection);
            Assert.AreEqual(1, dtos.Count);
            var dtoOut = dtos[0];

            Assert.IsTrue(TestUtil.VerifyDateTime(sampleTime, dtoOut.SampleTime));

            Assert.AreEqual(x.Fdom, dtoOut!.X!.Fdom);
            Assert.AreEqual(x.Vtop, dtoOut!.X!.Vtop);
            Assert.AreEqual(x.VtopOverflow, dtoOut!.X!.VtopOverflow);

            Assert.AreEqual(y.Fdom, dtoOut!.Y!.Fdom);
            Assert.AreEqual(y.Vtop, dtoOut!.Y!.Vtop);
            Assert.AreEqual(y.VtopOverflow, dtoOut!.Y!.VtopOverflow);

            Assert.AreEqual(z.Fdom, dtoOut!.Z!.Fdom);
            Assert.AreEqual(z.Vtop, dtoOut!.Z!.Vtop);
            Assert.AreEqual(z.VtopOverflow, dtoOut!.Z!.VtopOverflow);

        }



        [TestMethod]
        public void InsertVibrationDto_NullFDomVTop_Success()
        {
            var serialId = "99";
            var epocMillis = 1699960800001L;
            var sampleTime = DateTimeOffset.FromUnixTimeMilliseconds(epocMillis).UtcDateTime;

            var y = new FDomVtopOverflow(vtop: 2.2, fdom: 6.7, vtopOverflow: 2.33);

            var record = new PeakRecordDto(x: null, y: y, z: null, epocMillis: epocMillis)
            {
                SampleTime = sampleTime
            };
            testObj!.InsertPeakRecords(serialId: serialId, dtos: new List<PeakRecordDto> { record });

            var connectionString = database!.ConnectionString;
            using var connection = new NpgsqlConnection(connectionString);
            connection.Open();
            var dtos = ReadPeakRecords(connection);
            Assert.AreEqual(1, dtos.Count);
            var dtoOut = dtos[0];

            Assert.IsTrue(TestUtil.VerifyDateTime(sampleTime, dtoOut.SampleTime));
            Assert.AreEqual(null, dtoOut!.X);
            Assert.AreEqual(y.Fdom, dtoOut!.Y!.Fdom);
            Assert.AreEqual(y.Vtop, dtoOut!.Y!.Vtop);
            Assert.AreEqual(y.VtopOverflow, dtoOut!.Y!.VtopOverflow);
            Assert.AreEqual(null, dtoOut!.Z);
        }

        [TestMethod]
        public void TestInsertVeffRecord_Success()
        {
            var serialId = "12345";
            var epocMillis = 1699960800001L;
            var sampleTime = DateTimeOffset.FromUnixTimeMilliseconds(epocMillis).UtcDateTime;

            var x = 1.0;
            var y = 6.77;
            var z = 4.222;

            var records = new List<VeffRecordDto> {new VeffRecordDto(x: x,
                                           y: y,
                                           z: z,
                                           epocMillis: epocMillis) };
            records[0].SampleTime = sampleTime;

            testObj!.InsertVeffRecords(serialId, records);
            // insert same record twice, should only be 1 read
            testObj!.InsertVeffRecords(serialId, records);

            var connectionString = database!.ConnectionString;
            using var connection = new NpgsqlConnection(connectionString);
            connection.Open();
            var dtos = ReadVeffRecords(connection);
            Assert.AreEqual(1, dtos.Count);
            var dtoOut = dtos[0];

            Assert.IsTrue(TestUtil.VerifyDateTime(sampleTime, dtoOut.SampleTime));

            Assert.AreEqual(x, dtoOut!.X);
            Assert.AreEqual(y, dtoOut!.Y);
            Assert.AreEqual(z, dtoOut!.Z);

        }

        [TestMethod]
        public void TestInsertVdvRecord_Success()
        {
            var serialId = "123";
            var epocMillis = 1699960800001L;
            var sampleTime = DateTimeOffset.FromUnixTimeMilliseconds(epocMillis).UtcDateTime;

            var x = 1.0;
            var y = 6.77;
            var z = 4.222;
            var vdvX = "foo";
            var vdvY = "jsdfkjhsf";
            var vdvZ = "klsgjlkjglsfgsbob";

            var records = new List<VdvRecordDto>{ new VdvRecordDto(x: x,
                                           y: y,
                                           z: z,
                                           epocMillis: epocMillis,
                                           vdvX: vdvX,
                                           vdvY: vdvY,
                                           vdvZ: vdvZ) };
            records[0].SampleTime = sampleTime;

            testObj!.InsertVdvRecords(serialId, records);
            // insert same record twice, should only be 1 read
            testObj!.InsertVdvRecords(serialId, records);

            var connectionString = database!.ConnectionString;
            using var connection = new NpgsqlConnection(connectionString);
            connection.Open();
            var dtos = ReadVdvRecords(connection);
            Assert.AreEqual(1, dtos.Count);
            var dtoOut = dtos[0];

            Assert.IsTrue(TestUtil.VerifyDateTime(sampleTime, dtoOut.SampleTime));

            Assert.AreEqual(x, dtoOut!.X);
            Assert.AreEqual(y, dtoOut!.Y);
            Assert.AreEqual(z, dtoOut!.Z);

        }



        //[TestMethod]
        public void TestGetAverageVibrationLevel()
        {
            //var serialId = "98231";
            //var startTime = DateTime.Parse("2023-10-17T14:37:42");
            //var pm1Total = .0;
            //var pm2_5Total = .0;
            //var pm10Total = .0;
            //var pmTotalTotal = .0;
            //var numDtos = 15;
            //for (var i = 0; i < numDtos; i++)
            //{
            //    var pm1 = 1.0 * i;
            //    var pm2_5 = 2.5 * i;
            //    var pm10 = 10 * i;
            //    var pmTotal = 13.5 * i;

            //    testObj!.InsertDustDto(new DustDto(serialId: serialId, avrg: 60, sampleTime: startTime.AddMinutes(i),
            //                       pm1: pm1, pm2_5: pm2_5, pm10: pm10, pmTotal: pmTotal,
            //                       weather_t: .0, weather_p: .0, weather_rh: .0));
            //    pm1Total += pm1;
            //    pm2_5Total += pm2_5;
            //    pm10Total += pm10;
            //    pmTotalTotal += pmTotal;
            //}

            //var avgPm1 = testObj!.GetAverageDustLevel(serialId, "Pm1", startTime, startTime.AddMinutes(15));
            //Assert.AreEqual(pm1Total / numDtos, avgPm1);

            //var avgPm2_5 = testObj!.GetAverageDustLevel(serialId, "Pm2_5", startTime, startTime.AddMinutes(15));
            //Assert.AreEqual(pm2_5Total / numDtos, avgPm2_5);

            //var avgPm10 = testObj!.GetAverageDustLevel(serialId, "Pm10", startTime, startTime.AddMinutes(15));
            //Assert.AreEqual(pm10Total / numDtos, avgPm10);

            //var avgPmTotal = testObj!.GetAverageDustLevel(serialId, "PmTotal", startTime, startTime.AddMinutes(15));
            //Assert.AreEqual(pmTotalTotal / numDtos, avgPmTotal);
        }

        [TestMethod]
        public void TestWriteNotificationAudit()
        {

            var connectionString = database!.ConnectionString;
            using var connection = new NpgsqlConnection(connectionString);
            connection.Open();

            var serialId = "13";
            var monitorsIn = OmnidotsFixture.MonitorsList(1);
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
            InsertContact(connection, monitorId, ContactMethod.Email, email, phoneNo, siteUserId);
            var contacts = ReadContacts(connection, siteUserId);
            Assert.AreEqual(1, contacts.Count);

            var dt = DateTime.Parse("2023-10-18T11:19:00Z").ToUniversalTime();
            var notificationIn = new NotificationDto(id: Guid.NewGuid(),
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
        public async Task TestWriteTraces()
        {

            var serialId = "12345";

            var json = OmnidotsFixture.TracesResponseJson();
            var tracesResponse = JsonSerializer.Deserialize<TracesReponse>(json)!;

            var t0 = DateTime.UtcNow;
            testObj!.WriteTraces(serialId, tracesResponse.Traces!);
            var tt = DateTime.UtcNow - t0;
            RvtLogger.Logger.LogInformation("WriteTraces took={} seconds", tt.TotalSeconds);

            await using (var connection = database!.OpenConnection())
            {
                await connection.OpenAsync();
                await using var command = new NpgsqlCommand(
                    "SELECT trace_id, sample_index, x, y, z FROM omnidots_trace ORDER BY trace_id, sample_index;", connection);
                await using var reader = await command.ExecuteReaderAsync();
                Assert.IsTrue(await reader.ReadAsync());
                Assert.AreEqual(0, reader.GetInt32(1));
            }

            var tds = ReadTraces(database!.ConnectionString, serialId);

            Assert.AreEqual(tracesResponse.Traces!.Count, tds.Count);

            for (var i = 0; i < tds.Count; i++)
            {
                var expected = tracesResponse.Traces[i];
                var actual = tds[i].TraceData;

                Assert.AreEqual(expected.StartTime, actual.StartTime);
                Assert.AreEqual(expected.EndTime, actual.EndTime);
                Assert.AreEqual(expected.X!.Count, actual.X!.Count);
                Assert.AreEqual(expected.Y!.Count, actual.Y!.Count);
                Assert.AreEqual(expected.Z!.Count, actual.Z!.Count);

                CollectionAssert.AreEqual(expected.X, actual.X);
                CollectionAssert.AreEqual(expected.Y, actual.Y);
                CollectionAssert.AreEqual(expected.Z, actual.Z);

            }
            Assert.AreEqual(tracesResponse.Traces!.Count, tds.Count);

        }

        [TestMethod]
        public void WriteTraces_DuplicateValuedSamplesRemainDistinctAndOrderedAcrossReplay()
        {
            const string serialId = "ordered-duplicates";
            var trace = new TraceData
            {
                StartTime = DateTimeUtil.GetMillis(Utc(2026, 7, 14, 12, 0)),
                EndTime = DateTimeUtil.GetMillis(Utc(2026, 7, 14, 12, 1)),
                X = [3.0, 1.0, 3.0, 1.0],
                Y = [9.0, 8.0, 9.0, 8.0],
                Z = [6.0, 5.0, 6.0, 5.0]
            };

            testObj!.WriteTraces(serialId, [trace]);
            testObj.WriteTraces(serialId, [trace]);

            var storedTraces = ReadTraces(database!.ConnectionString, serialId);
            Assert.AreEqual(2, storedTraces.Count, "Trace replay retains the append-only compatibility behavior.");
            foreach (var storedTrace in storedTraces)
            {
                CollectionAssert.AreEqual(trace.X, storedTrace.TraceData.X);
                CollectionAssert.AreEqual(trace.Y, storedTrace.TraceData.Y);
                CollectionAssert.AreEqual(trace.Z, storedTrace.TraceData.Z);
                CollectionAssert.AreEqual(
                    new[] { 0, 1, 2, 3 },
                    ReadSampleIndexes(database.ConnectionString, storedTrace.Id));
            }
        }

        [TestMethod]
        public async Task ReadLatestTraceEndTimes_ReturnsMaximumForEachRequestedSerial()
        {
            await using var connection = database!.OpenConnection();
            await connection.OpenAsync();
            var serialAOld = Utc(2026, 7, 10, 8, 0);
            var serialANew = Utc(2026, 7, 12, 9, 0);
            var serialB = Utc(2026, 7, 11, 10, 0);
            await using var insert = new NpgsqlCommand(
                """
                INSERT INTO omnidots_trace_index (id, serial_id, start_time, end_time)
                VALUES
                    (@id1, 'trace-a', @start1, @end1),
                    (@id2, 'trace-a', @start2, @end2),
                    (@id3, 'trace-b', @start3, @end3);
                """,
                connection);
            insert.Parameters.AddWithValue("id1", Guid.NewGuid());
            insert.Parameters.AddWithValue("id2", Guid.NewGuid());
            insert.Parameters.AddWithValue("id3", Guid.NewGuid());
            insert.Parameters.AddWithValue("start1", serialAOld.AddMinutes(-1));
            insert.Parameters.AddWithValue("end1", serialAOld);
            insert.Parameters.AddWithValue("start2", serialANew.AddMinutes(-1));
            insert.Parameters.AddWithValue("end2", serialANew);
            insert.Parameters.AddWithValue("start3", serialB.AddMinutes(-1));
            insert.Parameters.AddWithValue("end3", serialB);
            await insert.ExecuteNonQueryAsync();

            var result = testObj!.ReadLatestTraceEndTimes(["trace-a", "trace-b", "missing"]);

            Assert.AreEqual(2, result.Count);
            Assert.AreEqual(serialANew, result["trace-a"]);
            Assert.AreEqual(serialB, result["trace-b"]);
        }

        [TestMethod]
        public async Task WriteTraces_WhenSampleInsertFails_RollsBackTraceIndexAndSamples()
        {
            await using var connection = database!.OpenConnection();
            await connection.OpenAsync();
            await using var createTrigger = new NpgsqlCommand(
                """
                CREATE OR REPLACE FUNCTION fail_second_trace_sample()
                RETURNS trigger AS $$
                BEGIN
                    IF NEW.sample_index = 1 THEN
                        RAISE EXCEPTION 'forced trace sample failure';
                    END IF;
                    RETURN NEW;
                END;
                $$ LANGUAGE plpgsql;

                CREATE TRIGGER fail_second_trace_sample
                BEFORE INSERT ON omnidots_trace
                FOR EACH ROW EXECUTE FUNCTION fail_second_trace_sample();
                """, connection);
            await createTrigger.ExecuteNonQueryAsync();

            try
            {
                var trace = new TraceData
                {
                    StartTime = DateTimeUtil.GetMillis(Utc(2026, 7, 14, 13, 0)),
                    EndTime = DateTimeUtil.GetMillis(Utc(2026, 7, 14, 13, 1)),
                    X = [1.0, 2.0, 3.0],
                    Y = [4.0, 5.0, 6.0],
                    Z = [7.0, 8.0, 9.0]
                };

                Assert.ThrowsExactly<Microsoft.EntityFrameworkCore.DbUpdateException>(
                    () => testObj!.WriteTraces("atomic-trace", [trace]));
                Assert.AreEqual(0, CountRows(database.ConnectionString, "omnidots_trace_index"));
                Assert.AreEqual(0, CountRows(database.ConnectionString, "omnidots_trace"));
            }
            finally
            {
                await using var dropTrigger = new NpgsqlCommand(
                    """
                    DROP TRIGGER IF EXISTS fail_second_trace_sample ON omnidots_trace;
                    DROP FUNCTION IF EXISTS fail_second_trace_sample();
                    """, connection);
                await dropTrigger.ExecuteNonQueryAsync();
            }
        }


        class TestTraceData
        {
            internal Guid Id { get; }
            internal TraceData TraceData { get; }


            internal TestTraceData(Guid id, TraceData traceData)
            {
                Id = id;
                TraceData = traceData;
            }
        }

        private static List<TestTraceData> ReadTraces(string connectionString, string serialId)
        {

            using var connection = new NpgsqlConnection(connectionString);
            connection.Open();
            var sql = @"SELECT id, start_time, end_time FROM omnidots_trace_index
                        WHERE serial_id = @SerialId
                        ORDER BY start_time";


            var traceDataList = new List<TestTraceData>();

            {
                using NpgsqlCommand cmd = new(sql, connection);
                cmd.Parameters.AddWithValue("@SerialId", serialId);

                using NpgsqlDataReader reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    var traceId = reader.GetGuid(0);
                    var startTime = reader.GetDateTime(1);
                    var endTime = reader.GetDateTime(2);

                    var td = new TraceData();
                    td.StartTime = DateTimeUtil.GetMillis(startTime);
                    td.EndTime = DateTimeUtil.GetMillis(endTime);

                    td.X = new List<double>();
                    td.Y = new List<double>();
                    td.Z = new List<double>();
                    traceDataList.Add(new TestTraceData(traceId, td));

                }
            }

            foreach (var testData in traceDataList)
            {
                var traceSql = @"SELECT x, y, z FROM omnidots_trace
                                 WHERE trace_id = @TraceId
                                 ORDER BY sample_index";
                using NpgsqlCommand cmd = new(traceSql, connection);
                cmd.Parameters.AddWithValue("@TraceId", testData.Id);

                using NpgsqlDataReader reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    double? x = reader.IsDBNull(0) ? null : reader.GetDouble(0);
                    double? y = reader.IsDBNull(1) ? null : reader.GetDouble(1);
                    double? z = reader.IsDBNull(2) ? null : reader.GetDouble(2);

                    if (x != null)
                    {
                        testData.TraceData.X!.Add((double)x!);
                    }
                    if (y != null)
                    {
                        testData.TraceData.Y!.Add((double)y!);
                    }
                    if (z != null)
                    {
                        testData.TraceData.Z!.Add((double)z!);
                    }
                }

            }

            return traceDataList;
        }

        private static int[] ReadSampleIndexes(string connectionString, Guid traceId)
        {
            using var connection = new NpgsqlConnection(connectionString);
            connection.Open();
            using var command = new NpgsqlCommand(
                "SELECT sample_index FROM omnidots_trace WHERE trace_id = $1 ORDER BY sample_index;",
                connection);
            command.Parameters.AddWithValue(traceId);
            using var reader = command.ExecuteReader();
            var indexes = new List<int>();
            while (reader.Read())
            {
                indexes.Add(reader.GetInt32(0));
            }

            return [.. indexes];
        }

        private static void InsertAlertRule(NpgsqlConnection connection, int index, string serialId, Guid monitorId)
        {
            var sql = @"INSERT INTO rvt_alert_rule
                            (id, serial_id, alert_field, limit_on, limit_off, alert_type,
                             is_active, averaging_period, weekdays, saturdays, sundays,
                             start_time, end_time, is_deleted, monitor_id, created)
                        VALUES
                            (@Id, @SerialId, @AlertField, @LimitOn, @LimitOff, @AlertType,
                             @IsActive, @AveragingPeriod, @Weekdays, @Saturdays, @Sundays,
                             @StartTime, @EndTime, @IsDeleted, @MonitorId, @Created);";

            var isEven = index % 2 == 0;
            using NpgsqlCommand cmd = new(sql, connection);
            cmd.Parameters.AddWithValue("@Id", Guid.NewGuid());
            cmd.Parameters.AddWithValue("@SerialId", serialId);
            cmd.Parameters.AddWithValue("@AlertField", "Pm" + index);
            cmd.Parameters.AddWithValue("@LimitOn", 1.111 * index);
            cmd.Parameters.AddWithValue("@LimitOff", 2.2222 * index);
            cmd.Parameters.AddWithValue("@AlertType", isEven ? (int)AlertType.Alert : (int)AlertType.Caution);
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
                cmd.Parameters.Add("@StartTime", NpgsqlDbType.Time).Value =
                    sendStartTime?.TimeOfDay ?? (object)DBNull.Value;
                cmd.Parameters.Add("@EndTime", NpgsqlDbType.Time).Value =
                    sendEndTime?.TimeOfDay ?? (object)DBNull.Value;

                cmd.ExecuteNonQuery();
            }
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


        // Summary: Reads persisted peak fixture rows using canonical PostgreSQL identifiers.
        private static List<PeakRecordDto> ReadPeakRecords(NpgsqlConnection connection)
        {
            var sql = @"SELECT serial_id, sample_time,
                               x_fdom, x_vtop, x_vtop_overflow,
                               y_fdom, y_vtop, y_vtop_overflow,
                               z_fdom, z_vtop, z_vtop_overflow
                        FROM omnidots_peak_level";
            using NpgsqlCommand cmd = new(sql, connection);
            var dtos = new List<PeakRecordDto>();
            using NpgsqlDataReader reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var serialId = reader.GetString(0);
                var sampleTime = reader.GetDateTime(1);
                double? xfDom = reader.IsDBNull(2) ? null : reader.GetDouble(2);
                double? xvTop = reader.IsDBNull(3) ? null : reader.GetDouble(3);
                double? xvTopOverflow = reader.IsDBNull(4) ? null : reader.GetDouble(4);

                double? yfDom = reader.IsDBNull(5) ? null : reader.GetDouble(5);
                double? yvTop = reader.IsDBNull(6) ? null : reader.GetDouble(6);
                double? yvTopOverflow = reader.IsDBNull(7) ? null : reader.GetDouble(7);

                double? zfDom = reader.IsDBNull(8) ? null : reader.GetDouble(8);
                double? zvTop = reader.IsDBNull(9) ? null : reader.GetDouble(9);
                double? zvTopOverflow = reader.IsDBNull(10) ? null : reader.GetDouble(10);


                FDomVtopOverflow? x = xfDom != null && xvTop != null && xvTopOverflow != null ?
                    new FDomVtopOverflow(fdom: (double)xfDom!, vtop: (double)xvTop!, vtopOverflow: (double)xvTopOverflow!) : null;


                FDomVtopOverflow? y = yfDom != null && yvTop != null && yvTopOverflow != null ?
                    new FDomVtopOverflow(fdom: (double)yfDom!, vtop: (double)yvTop!, vtopOverflow: (double)yvTopOverflow!) : null;

                FDomVtopOverflow? z = zfDom != null && zvTop != null && zvTopOverflow != null ?
                    new FDomVtopOverflow(fdom: (double)zfDom!, vtop: (double)zvTop!, vtopOverflow: (double)zvTopOverflow!) : null;

                var epocMillis = sampleTime.Subtract(DateTimeUtil.JAN1_1970).TotalMilliseconds;

                dtos.Add(new PeakRecordDto(x: x, y: y, z: z, epocMillis: epocMillis));
            }

            return dtos;
        }


        // Summary: Reads persisted VEFF fixture rows using canonical PostgreSQL identifiers.
        private static List<VeffRecordDto> ReadVeffRecords(NpgsqlConnection connection)
        {
            var sql = @"SELECT serial_id, sample_time, x, y, z
                    FROM omnidots_veff_level";
            using NpgsqlCommand cmd = new(sql, connection);
            var dtos = new List<VeffRecordDto>();
            using NpgsqlDataReader reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var serialId = reader.GetString(0);
                var sampleTime = reader.GetDateTime(1);
                var x = reader.GetDouble(2);
                var y = reader.GetDouble(3);
                var z = reader.GetDouble(4);
                var epocMillis = sampleTime.Subtract(DateTimeUtil.JAN1_1970).TotalMilliseconds;

                dtos.Add(new VeffRecordDto(x: x, y: y, z: z, epocMillis: epocMillis));
            }

            return dtos;
        }

        // Summary: Reads persisted VDV fixture rows using canonical PostgreSQL identifiers.
        private static List<VdvRecordDto> ReadVdvRecords(NpgsqlConnection connection)
        {
            var sql = @"SELECT serial_id, sample_time, x, y, z, vdv_x, vdv_y, vdv_z
                    FROM omnidots_vdv_level";
            using NpgsqlCommand cmd = new(sql, connection);
            var dtos = new List<VdvRecordDto>();
            using NpgsqlDataReader reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var serialId = reader.GetString(0);
                var sampleTime = reader.GetDateTime(1);
                var x = reader.GetDouble(2);
                var y = reader.GetDouble(3);
                var z = reader.GetDouble(4);
                var vdvX = reader.GetString(5);
                var vdvY = reader.GetString(6);
                var vdvZ = reader.GetString(7);

                var epocMillis = sampleTime.Subtract(DateTimeUtil.JAN1_1970).TotalMilliseconds;

                dtos.Add(new VdvRecordDto(x: x,
                                          y: y,
                                          z: z,
                                          epocMillis: epocMillis,
                                          vdvX: vdvX,
                                          vdvY: vdvY,
                                          vdvZ: vdvZ));
            }

            return dtos;
        }
    }

}
