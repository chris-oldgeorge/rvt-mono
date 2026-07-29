using System.Data;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;
using Omnidots.Api.Db;
using Omnidots.Model.Dto;
using Omnidots.Model.Json;
using Rvt.Monitor.Common.Data;
using Rvt.Monitor.Common.Diagnostics;
using Rvt.Monitor.Common.Notifications;
using Rvt.Monitor.Common.Utilities;
using Rvt.Monitor.IntegrationTesting;
using ContactMethod = Rvt.Monitor.Common.Notifications.ContactMethod;
using NotificationDto = Rvt.Monitor.Common.Notifications.NotificationDto;
using RvtContactDto = Rvt.Monitor.Common.Notifications.RvtContactDto;
namespace OmnidotsAdapterTests
{

    // Summary: Exercises Omnidots PostgreSQL database persistence against a scoped fixture.
    // Major updates:
    // - 2026-06-18 Test fixture hardening: aligned vibration timestamp read helpers with PostgreSQL UTC round-trips.
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
        public void ReadImportCursor_WhenNoCursorExists_ReturnsNullForEverySeries()
        {
            Assert.IsNull(_testObj!.ReadImportCursor("cursor-empty", OmnidotsMeasurementSeries.Peak));
            Assert.IsNull(_testObj.ReadImportCursor("cursor-empty", OmnidotsMeasurementSeries.Veff));
            Assert.IsNull(_testObj.ReadImportCursor("cursor-empty", OmnidotsMeasurementSeries.Vdv));
        }

        [TestMethod]
        public void ReadLatestMeasurementTime_ReturnsEachSeriesMaximumWithoutCrossSeriesLeakage()
        {
            const string serialId = "latest-series";
            DateTime peakTime = Utc(2026, 7, 14, 8, 0);
            DateTime veffTime = peakTime.AddMinutes(10);
            DateTime vdvTime = peakTime.AddMinutes(20);

            _testObj!.InsertPeakRecordsTable(PeakTable(serialId, peakTime.AddMinutes(-1), peakTime));
            _testObj.InsertVeffRecords(serialId,
            [
                VeffRecord(veffTime.AddMinutes(-1)),
                VeffRecord(veffTime)
            ]);
            _testObj.InsertVdvRecords(serialId,
            [
                VdvRecord(vdvTime.AddMinutes(-1)),
                VdvRecord(vdvTime)
            ]);

            DateTime? latestPeak = _testObj.ReadLatestMeasurementTime(serialId, OmnidotsMeasurementSeries.Peak);
            DateTime? latestVeff = _testObj.ReadLatestMeasurementTime(serialId, OmnidotsMeasurementSeries.Veff);
            DateTime? latestVdv = _testObj.ReadLatestMeasurementTime(serialId, OmnidotsMeasurementSeries.Vdv);
            Assert.AreEqual(peakTime, latestPeak);
            Assert.AreEqual(veffTime, latestVeff);
            Assert.AreEqual(vdvTime, latestVdv);
            Assert.AreEqual(DateTimeKind.Utc, latestPeak!.Value.Kind);
            Assert.AreEqual(DateTimeKind.Utc, latestVeff!.Value.Kind);
            Assert.AreEqual(DateTimeKind.Utc, latestVdv!.Value.Kind);
            Assert.IsNull(_testObj.ReadLatestMeasurementTime("other-serial", OmnidotsMeasurementSeries.Peak));
        }

        [TestMethod]
        public void ImportSeries_AdvancesOnlyItsOwnCursor()
        {
            const string serialId = "isolated-series";
            DateTime veffTime = Utc(2026, 7, 14, 9, 0);
            DateTime vdvTime = veffTime.AddMinutes(5);

            _testObj!.ImportVeffRecords(serialId, [VeffRecord(veffTime)], veffTime);

            Assert.AreEqual(veffTime, _testObj.ReadImportCursor(serialId, OmnidotsMeasurementSeries.Veff));
            Assert.IsNull(_testObj.ReadImportCursor(serialId, OmnidotsMeasurementSeries.Peak));
            Assert.IsNull(_testObj.ReadImportCursor(serialId, OmnidotsMeasurementSeries.Vdv));

            _testObj.ImportVdvRecords(serialId, [VdvRecord(vdvTime)], vdvTime);

            Assert.AreEqual(veffTime, _testObj.ReadImportCursor(serialId, OmnidotsMeasurementSeries.Veff));
            Assert.AreEqual(vdvTime, _testObj.ReadImportCursor(serialId, OmnidotsMeasurementSeries.Vdv));
            Assert.IsNull(_testObj.ReadImportCursor(serialId, OmnidotsMeasurementSeries.Peak));
        }

        [TestMethod]
        public void ImportVeffRecords_DuplicateOrOlderBatch_DoesNotMoveCursorOrUpdatedAtBackward()
        {
            const string serialId = "monotonic-veff";
            DateTime older = Utc(2026, 7, 14, 10, 0);
            DateTime newer = older.AddMinutes(1);

            _testObj!.ImportVeffRecords(serialId, [VeffRecord(newer)], newer);
            DateTime updatedAt = ReadCursorUpdatedAt(serialId, "Veff");

            _testObj.ImportVeffRecords(serialId, [VeffRecord(older)], older);

            Assert.AreEqual(newer, _testObj.ReadImportCursor(serialId, OmnidotsMeasurementSeries.Veff));
            Assert.AreEqual(updatedAt, ReadCursorUpdatedAt(serialId, "Veff"));
            Assert.AreEqual(2, CountRows(_database!.ConnectionString, "omnidots_veff_level"));
        }

        [TestMethod]
        public void ImportPeakRecords_AtomicallyAdvancesPeakCursorAndCompatibilityTimestamp()
        {
            VibrationMonitorDto monitor = OmnidotsFixture.MonitorsList(1).Single();
            DateTime newest = Utc(2026, 7, 14, 11, 0);
            _testObj!.WriteMonitorList([monitor]);

            _testObj.ImportPeakRecords(monitor.SerialId, PeakTable(monitor.SerialId, newest), newest);

            Assert.AreEqual(newest, _testObj.ReadImportCursor(monitor.SerialId, OmnidotsMeasurementSeries.Peak));
            Assert.AreEqual(newest, _testObj.ReadMonitor(monitor.SerialId).LastDataTime);
            Assert.IsNull(_testObj.ReadImportCursor(monitor.SerialId, OmnidotsMeasurementSeries.Veff));
            Assert.IsNull(_testObj.ReadImportCursor(monitor.SerialId, OmnidotsMeasurementSeries.Vdv));
        }

        [TestMethod]
        public void ImportVeffRecords_WhenCursorWriteFails_RollsBackMeasurementsAndCursor()
        {
            const string serialId = "rollback-veff";
            DateTime sampleTime = Utc(2026, 7, 14, 12, 0);
            using NpgsqlConnection connection = _database!.OpenConnection();
            connection.Open();

            try
            {
                using (NpgsqlCommand install = new(
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
                    _testObj!.ImportVeffRecords(serialId, [VeffRecord(sampleTime)], sampleTime));

                Assert.AreEqual(0, CountRows(_database.ConnectionString, "omnidots_veff_level"));
                Assert.IsNull(_testObj!.ReadImportCursor(serialId, OmnidotsMeasurementSeries.Veff));
            }
            finally
            {
                using NpgsqlCommand cleanup = new(
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
            VibrationMonitorDto monitor = OmnidotsFixture.MonitorsList(1).Single();
            DateTime existingLastDataTime = Utc(2026, 7, 14, 13, 0);
            _testObj!.WriteMonitorList([monitor]);
            _testObj.WriteLatestTimestamp(monitor.SerialId, existingLastDataTime);

            _testObj.ImportPeakRecords(monitor.SerialId, PeakTable(monitor.SerialId), default);
            _testObj.ImportVeffRecords(monitor.SerialId, [], default);
            _testObj.ImportVdvRecords(monitor.SerialId, [], default);

            Assert.IsNull(_testObj.ReadImportCursor(monitor.SerialId, OmnidotsMeasurementSeries.Peak));
            Assert.IsNull(_testObj.ReadImportCursor(monitor.SerialId, OmnidotsMeasurementSeries.Veff));
            Assert.IsNull(_testObj.ReadImportCursor(monitor.SerialId, OmnidotsMeasurementSeries.Vdv));
            Assert.AreEqual(existingLastDataTime, _testObj.ReadMonitor(monitor.SerialId).LastDataTime);
            Assert.AreEqual(0, CountRows(_database!.ConnectionString, "omnidots_peak_level"));
            Assert.AreEqual(0, CountRows(_database.ConnectionString, "omnidots_veff_level"));
            Assert.AreEqual(0, CountRows(_database.ConnectionString, "omnidots_vdv_level"));
        }

        [TestMethod]
        public void ImportMethods_NonEmptyBatchRequiresNewestSampleAtToMatchBatchMaximum()
        {
            const string serialId = "newest-validation";
            DateTime sampleTime = Utc(2026, 7, 14, 14, 0);
            DateTime wrongNewest = sampleTime.AddMinutes(-1);

            Assert.ThrowsExactly<ArgumentException>(() =>
                _testObj!.ImportPeakRecords(serialId, PeakTable(serialId, sampleTime), wrongNewest));
            Assert.ThrowsExactly<ArgumentException>(() =>
                _testObj!.ImportVeffRecords(serialId, [VeffRecord(sampleTime)], wrongNewest));
            Assert.ThrowsExactly<ArgumentException>(() =>
                _testObj!.ImportVdvRecords(serialId, [VdvRecord(sampleTime)], wrongNewest));

            Assert.AreEqual(0, CountRows(_database!.ConnectionString, "omnidots_peak_level"));
            Assert.AreEqual(0, CountRows(_database.ConnectionString, "omnidots_veff_level"));
            Assert.AreEqual(0, CountRows(_database.ConnectionString, "omnidots_vdv_level"));
        }

        [TestMethod]
        public void ImportPeakRecords_OlderReplay_DoesNotMoveCursorOrCompatibilityTimestampBackward()
        {
            VibrationMonitorDto monitor = OmnidotsFixture.MonitorsList(1).Single();
            DateTime older = Utc(2026, 7, 14, 15, 0);
            DateTime newer = older.AddMinutes(1);
            _testObj!.WriteMonitorList([monitor]);
            _testObj.ImportPeakRecords(monitor.SerialId, PeakTable(monitor.SerialId, newer), newer);

            _testObj.ImportPeakRecords(monitor.SerialId, PeakTable(monitor.SerialId, older), older);

            Assert.AreEqual(newer, _testObj.ReadImportCursor(monitor.SerialId, OmnidotsMeasurementSeries.Peak));
            Assert.AreEqual(newer, _testObj.ReadMonitor(monitor.SerialId).LastDataTime);
            Assert.AreEqual(2, CountRows(_database!.ConnectionString, "omnidots_peak_level"));
        }

        [TestMethod]
        public void ImportPeakRecords_FirstCursorDoesNotRegressNewerCompatibilityTimestamp()
        {
            VibrationMonitorDto monitor = OmnidotsFixture.MonitorsList(1).Single();
            DateTime importedSample = Utc(2026, 7, 14, 15, 30);
            DateTime existingLastDataTime = importedSample.AddMinutes(1);
            _testObj!.WriteMonitorList([monitor]);
            _testObj.WriteLatestTimestamp(monitor.SerialId, existingLastDataTime);

            _testObj.ImportPeakRecords(
                monitor.SerialId,
                PeakTable(monitor.SerialId, importedSample),
                importedSample);

            Assert.AreEqual(importedSample, _testObj.ReadImportCursor(
                monitor.SerialId,
                OmnidotsMeasurementSeries.Peak));
            Assert.AreEqual(existingLastDataTime, _testObj.ReadMonitor(monitor.SerialId).LastDataTime);
        }

        [TestMethod]
        public void InsertPeakRecordsTable_MixedSerialRows_ImportsEachSerialIndependently()
        {
            const string firstSerial = "mixed-first";
            const string secondSerial = "mixed-second";
            DateTime firstTime = Utc(2026, 7, 14, 16, 0);
            DateTime secondTime = firstTime.AddMinutes(1);
            DataTable table = PeakTable(firstSerial, firstTime);
            AddPeakRow(table, secondSerial, secondTime);

            _testObj!.InsertPeakRecordsTable(table);

            Assert.AreEqual(1, CountRows(_database!.ConnectionString, "omnidots_peak_level", firstSerial));
            Assert.AreEqual(1, CountRows(_database.ConnectionString, "omnidots_peak_level", secondSerial));
            Assert.AreEqual(firstTime, _testObj.ReadImportCursor(firstSerial, OmnidotsMeasurementSeries.Peak));
            Assert.AreEqual(secondTime, _testObj.ReadImportCursor(secondSerial, OmnidotsMeasurementSeries.Peak));
        }

        [TestMethod]
        public void ImportPeakRecords_RowSerialMismatch_RejectsWholeBatch()
        {
            const string requestedSerial = "requested-serial";
            const string rowSerial = "row-serial";
            DateTime sampleTime = Utc(2026, 7, 14, 17, 0);

            Assert.ThrowsExactly<ArgumentException>(() =>
                _testObj!.ImportPeakRecords(requestedSerial, PeakTable(rowSerial, sampleTime), sampleTime));

            Assert.AreEqual(0, CountRows(_database!.ConnectionString, "omnidots_peak_level"));
            Assert.IsNull(_testObj!.ReadImportCursor(requestedSerial, OmnidotsMeasurementSeries.Peak));
            Assert.IsNull(_testObj.ReadImportCursor(rowSerial, OmnidotsMeasurementSeries.Peak));
        }

        [TestMethod]
        public void NormalizeLatestMeasurementTime_ReturnsUtcForEveryDateTimeKind()
        {
            DateTime utc = Utc(2026, 7, 14, 18, 0);
            DateTime local = utc.ToLocalTime();
            DateTime unspecified = DateTime.SpecifyKind(utc, DateTimeKind.Unspecified);

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
            DateTime first = Utc(2026, 7, 14, 19, 0);
            DateTime overlap = first.AddMinutes(1);
            DateTime last = overlap.AddMinutes(1);
            using Barrier firstAttemptBarrier = new(2);
            void BeforeSave(OmnidotsMeasurementSeries series, int attempt)
            {
                if (series == OmnidotsMeasurementSeries.Veff && attempt == 1 &&
                    !firstAttemptBarrier.SignalAndWait(TimeSpan.FromSeconds(10), TestContext.CancellationToken))
                {
                    throw new TimeoutException("Concurrent import attempts did not overlap before SaveChanges.");
                }
            }

            DBClient firstClient = new(_database!.ConnectionString, BeforeSave);
            DBClient secondClient = new(_database.ConnectionString, BeforeSave);
            Task firstImport = Task.Run(() =>
                firstClient.ImportVeffRecords(serialId, [VeffRecord(first), VeffRecord(overlap)], overlap), TestContext.CancellationToken);
            Task secondImport = Task.Run(() =>
                secondClient.ImportVeffRecords(serialId, [VeffRecord(overlap), VeffRecord(last)], last), TestContext.CancellationToken);

            await Task.WhenAll(firstImport, secondImport).WaitAsync(TimeSpan.FromSeconds(20), TestContext.CancellationToken);

            Assert.AreEqual(3, CountRows(_database.ConnectionString, "omnidots_veff_level", serialId));
            Assert.AreEqual(last, _testObj!.ReadImportCursor(serialId, OmnidotsMeasurementSeries.Veff));
        }

        [TestMethod]
        public void ImportVeffRecords_PersistentSerializationFailure_StopsAfterThreeAttempts()
        {
            const string serialId = "bounded-retry";
            DateTime sampleTime = Utc(2026, 7, 14, 20, 0);
            int attempts = 0;
            DBClient client = new(_database!.ConnectionString, (_, attempt) =>
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
            Assert.AreEqual(0, CountRows(_database.ConnectionString, "omnidots_veff_level", serialId));
            Assert.IsNull(_testObj!.ReadImportCursor(serialId, OmnidotsMeasurementSeries.Veff));
        }

        [TestMethod]
        public void TestMonitors()
        {
            int numMonitors = 5;
            List<VibrationMonitorDto> monitorsIn = OmnidotsFixture.MonitorsList(numMonitors, null, true);
            _testObj!.WriteMonitorList(monitorsIn);
            List<VibrationMonitorDto> monitorsOut = _testObj.ReadMonitorList(null);

            AssertMonitorsList(monitorsIn, monitorsOut);

            // write again - should  be same number of monitors
            _testObj!.WriteMonitorList(monitorsIn);
            AssertMonitorsList(monitorsIn, monitorsOut);
        }


        private static int CountRows(string connectionString, string tableName)
        {
            using NpgsqlConnection connection = new(connectionString);
            connection.Open();
            string sql = string.Format(@"SELECT Count(*) FROM {0};", tableName);

            using NpgsqlCommand cmd = new(sql, connection);
            return Convert.ToInt32(cmd.ExecuteScalar());
        }

        private static int CountRows(string connectionString, string tableName, string serialId)
        {
            using NpgsqlConnection connection = new(connectionString);
            connection.Open();
            string sql = string.Format(@"SELECT Count(*) FROM {0} WHERE serial_id = $1;", tableName);
            using NpgsqlCommand command = new(sql, connection);
            command.Parameters.AddWithValue(serialId);
            return Convert.ToInt32(command.ExecuteScalar());
        }

        private static DateTime ReadCursorUpdatedAt(string serialId, string series)
        {
            using NpgsqlConnection connection = _database!.OpenConnection();
            connection.Open();
            using NpgsqlCommand command = new(
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
            VeffRecordDto record = new(1.0, 2.0, 3.0, new DateTimeOffset(sampleTime).ToUnixTimeMilliseconds())
            {
                SampleTime = sampleTime
            };
            return record;
        }

        private static VdvRecordDto VdvRecord(DateTime sampleTime)
        {
            VdvRecordDto record = new(
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
            DataTable table = new("Results");
            table.Columns.Add("SerialId", typeof(string));
            table.Columns.Add("SampleTime", typeof(DateTime));
            foreach (string? columnName in new[]
                     {
                         "XFdom", "XVtop", "XVtopOverflow",
                         "YFdom", "YVtop", "YVtopOverflow",
                         "ZFdom", "ZVtop", "ZVtopOverflow"
                     })
            {
                table.Columns.Add(columnName, typeof(double)).AllowDBNull = true;
            }

            foreach (DateTime sampleTime in sampleTimes)
            {
                AddPeakRow(table, serialId, sampleTime);
            }

            return table;
        }

        private static void AddPeakRow(DataTable table, string serialId, DateTime sampleTime)
        {
            DataRow row = table.NewRow();
            row["SerialId"] = serialId;
            row["SampleTime"] = sampleTime;
            table.Rows.Add(row);
        }

        private void AssertMonitorsList(List<VibrationMonitorDto> expected, List<VibrationMonitorDto> actual)
        {
            string connectionString = _database!.ConnectionString;

            Assert.HasCount(expected.Count, actual);
            List<VibrationMonitorDto> orderedmonitorsOut = [.. actual.OrderBy(o => o.SerialId)];
            Assert.IsTrue(TestUtil.AreEqual(expected, orderedmonitorsOut));

            foreach (VibrationMonitorDto monitor in expected)
            {
                VibrationMonitorDto m = _testObj!.ReadMonitor(monitor.SerialId);
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
            AdapterException exception = Assert.ThrowsExactly<AdapterException>(() =>
            {

                _testObj!.ReadMonitor("bad-serial-id");
            });
            Assert.AreEqual("No monitor with SerialId='bad-serial-id'", exception.Message);
        }

        [TestMethod]
        public void TestReadAlertRules()
        {
            string connectionString = _database!.ConnectionString;
            using NpgsqlConnection connection = new(connectionString);
            connection.Open();

            string serialId = "12345";
            List<VibrationMonitorDto> monitorsIn = OmnidotsFixture.MonitorsList(1);
            _testObj!.WriteMonitorList(monitorsIn);
            List<VibrationMonitorDto> monitorsOut = _testObj.ReadMonitorList(null);
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

            List<Rvt.Monitor.Common.Rules.RvtAlertRuleDto> rules = _testObj!.ReadRules(serialId);
            Assert.HasCount(NUM_RULES, rules);

            List<Rvt.Monitor.Common.Rules.RvtAlertRuleDto> orderedRules = [.. rules.OrderBy(o => o.Field)];

            for (int i = 0; i < NUM_RULES; i++)
            {
                bool isEven = i % 2 == 0;
                Rvt.Monitor.Common.Rules.RvtAlertRuleDto rule = orderedRules[i];
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

            int numMonitors = 2;
            List<VibrationMonitorDto> monitorsIn = OmnidotsFixture.MonitorsList(numMonitors);
            _testObj!.WriteMonitorList(monitorsIn);
            Guid monitorId = monitorsIn[0].Id;
            string serialId = monitorsIn[0].SerialId;
            // add an alert and contact as RvtAlertContacts table has foreign key constraints
            InsertAlertRule(connection, 44, serialId, monitorId);
            List<Rvt.Monitor.Common.Rules.RvtAlertRuleDto> rules = _testObj!.ReadRules(serialId);
            Assert.HasCount(1, rules);
            string email = "mytestemail@bbb.com";
            string phoneNo = "01234567890";
            DateTime startTime = DateTimeUtil.TruncateMillis(DateTime.UtcNow.AddHours(-1));
            DateTime endTime = DateTimeUtil.TruncateMillis(DateTime.UtcNow.AddHours(1));

            Guid siteUserId = Guid.NewGuid();
            InsertContact(connection, monitorId, ContactMethod.Email, email, phoneNo,
              siteUserId, startTime, endTime);

            // insert that should not be read
            InsertContact(connection, monitorsIn[1].Id, ContactMethod.Email, email, phoneNo, Guid.NewGuid());

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
        public void TestWriteExceptionUsesPostgreSqlErrorLog()
        {
            string connectionString = _database!.ConnectionString;

            string TAG = "MyTestError";
            string MESSAGE = "bang";

            MonitorDb.WriteException(
                connectionString,
                TAG,
                AdapterException.Of(MESSAGE),
                "OmnidotsMonitorTests",
                "test");

            using NpgsqlConnection connection = new(connectionString);
            connection.Open();

            string sql = @"SELECT variables, message, logged_at FROM error_log";
            using NpgsqlCommand cmd = new(sql, connection);
            using NpgsqlDataReader reader = cmd.ExecuteReader();

            int count = 0;
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

            List<VibrationMonitorDto> monitors = OmnidotsFixture.MonitorsList(1);
            Assert.HasCount(1, monitors);

            _testObj!.WriteMonitorList(monitors);

            DateTime lastDataTime = DateTime.Parse("2023-10-18T14:35:42Z").ToUniversalTime();
            _testObj.WriteLatestTimestamp("1", lastDataTime);

            monitors = _testObj.ReadMonitorList(null);
            Assert.HasCount(1, monitors);

            VibrationMonitorDto monitor = monitors[0];
            Assert.AreEqual(lastDataTime, monitor.LastDataTime);
        }

        [TestMethod]
        public void TestReadWriteNotification()
        {
            string connectionString = _database!.ConnectionString;
            using NpgsqlConnection connection = new(connectionString);
            connection.Open();

            string serialId = "1";

            List<VibrationMonitorDto> monitorsIn = OmnidotsFixture.MonitorsList(1);
            _testObj!.WriteMonitorList(monitorsIn);
            List<VibrationMonitorDto> monitorsOut = _testObj.ReadMonitorList(null);
            Assert.HasCount(1, monitorsOut);
            Guid monitorId = monitorsOut[0].Id;


            // add an alert and contact as RvtAlertContacts table has foreign key constraints
            InsertAlertRule(connection, 21, serialId, monitorId);
            List<Rvt.Monitor.Common.Rules.RvtAlertRuleDto> rules = _testObj!.ReadRules(serialId);
            Assert.HasCount(1, rules);
            string email = "foobob@bbb.com";
            string phoneNo = "01238867890";
            Guid siteUserId = Guid.NewGuid();
            InsertContact(connection, monitorId, ContactMethod.Email, email, phoneNo, siteUserId);
            List<RvtContactDto> contacts = ReadContacts(connection, siteUserId);
            Assert.HasCount(1, contacts);

            DateTime dt = DateTime.Parse("2023-10-18T11:19:00Z").ToUniversalTime();
            NotificationDto alertIn = new(id: Guid.NewGuid(),
                                              notificationTime: dt,
                                              limitOn: rules[0].LimitOn,
                                              averagingPeriod: rules[0].AveragingPeriod,
                                              level: 99.876,
                                              closedTime: null,
                                              closedByUser: null,
                                              alertType: rules[0].AlertType,
                                              alertField: rules[0].Field,
                                              monitorId: monitorId);

            _testObj.WriteNotification(alertIn);

            List<NotificationDto> alerts = ReadNotifications(connection);
            Assert.HasCount(1, alerts);

            NotificationDto alertOut = alerts[0];

            Assert.AreEqual(alertIn.Id, alertOut.Id);
            Assert.AreEqual(alertIn.Level, alertOut.Level);
            Assert.AreEqual(alertIn.NotificationTime, alertOut.NotificationTime);
            Assert.AreEqual(alertIn.LimitOn, alertOut.LimitOn);
            Assert.AreEqual(alertIn.AveragingPeriod, alertOut.AveragingPeriod);
            Assert.AreEqual(alertIn.AlertField, alertOut.AlertField);
            Assert.AreEqual(alertIn.AlertType, alertOut.AlertType);
            Assert.AreEqual(alertIn.MonitorId, alertOut.MonitorId);

            List<NotificationDto> notifictions = _testObj.ReadNotifications(monitorId, dt.AddMinutes(-5));
            Assert.HasCount(1, notifictions);
            NotificationDto notifiction = notifictions[0];
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
            string connectionString = _database!.ConnectionString;
            using NpgsqlConnection connection = new(connectionString);
            connection.Open();
            string serialId = "1";
            List<VibrationMonitorDto> monitorsIn = OmnidotsFixture.MonitorsList(1);
            _testObj!.WriteMonitorList(monitorsIn);
            List<VibrationMonitorDto> monitorsOut = _testObj.ReadMonitorList(null);
            Assert.HasCount(1, monitorsOut);
            Guid monitorId = monitorsOut[0].Id;

            InsertAlertRule(connection, 721, serialId, monitorId);
            List<Rvt.Monitor.Common.Rules.RvtAlertRuleDto> rules = _testObj!.ReadRules(serialId);
            Assert.HasCount(1, rules);

            Rvt.Monitor.Common.Rules.RvtAlertRuleDto rule = rules[0];

            bool isActive = !rule.IsActive;
            rule.IsActive = isActive;

            _testObj.UpdateAlertRule(rules[0]);

            List<Rvt.Monitor.Common.Rules.RvtAlertRuleDto> updatedRules = _testObj!.ReadRules(serialId);
            Assert.HasCount(1, updatedRules);
        }

        [TestMethod]
        public void TestInsertPeakRecord_Success()
        {
            string serialId = "123";
            long epocMillis = 1699960800001L;
            DateTime sampleTime = DateTimeOffset.FromUnixTimeMilliseconds(epocMillis).UtcDateTime;

            FDomVtopOverflow x = new(vtop: 1.0, fdom: 2.7, vtopOverflow: 4.5);
            FDomVtopOverflow y = new(vtop: 2.2, fdom: 6.7, vtopOverflow: 2.33);
            FDomVtopOverflow z = new(vtop: 4.222, fdom: 4.7, vtopOverflow: 11.5);

            List<PeakRecordDto> peakRecords =
                [
                 new PeakRecordDto(x: x,
                                   y: y,
                                   z: z,
                                   epocMillis: epocMillis)
            ];
            peakRecords[0].SampleTime = sampleTime;

            _testObj!.InsertPeakRecords(serialId, peakRecords);

            string connectionString = _database!.ConnectionString;
            using NpgsqlConnection connection = new(connectionString);
            connection.Open();
            List<PeakRecordDto> dtos = ReadPeakRecords(connection);
            Assert.HasCount(1, dtos);
            PeakRecordDto dtoOut = dtos[0];

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
            string serialId = "99";
            long epocMillis = 1699960800001L;
            DateTime sampleTime = DateTimeOffset.FromUnixTimeMilliseconds(epocMillis).UtcDateTime;

            FDomVtopOverflow y = new(vtop: 2.2, fdom: 6.7, vtopOverflow: 2.33);

            PeakRecordDto record = new(x: null, y: y, z: null, epocMillis: epocMillis)
            {
                SampleTime = sampleTime
            };
            _testObj!.InsertPeakRecords(serialId: serialId, dtos: [record]);

            string connectionString = _database!.ConnectionString;
            using NpgsqlConnection connection = new(connectionString);
            connection.Open();
            List<PeakRecordDto> dtos = ReadPeakRecords(connection);
            Assert.HasCount(1, dtos);
            PeakRecordDto dtoOut = dtos[0];

            Assert.IsTrue(TestUtil.VerifyDateTime(sampleTime, dtoOut.SampleTime));
            Assert.IsNull(dtoOut!.X);
            Assert.AreEqual(y.Fdom, dtoOut!.Y!.Fdom);
            Assert.AreEqual(y.Vtop, dtoOut!.Y!.Vtop);
            Assert.AreEqual(y.VtopOverflow, dtoOut!.Y!.VtopOverflow);
            Assert.IsNull(dtoOut!.Z);
        }

        [TestMethod]
        public void TestInsertVeffRecord_Success()
        {
            string serialId = "12345";
            long epocMillis = 1699960800001L;
            DateTime sampleTime = DateTimeOffset.FromUnixTimeMilliseconds(epocMillis).UtcDateTime;

            double x = 1.0;
            double y = 6.77;
            double z = 4.222;

            List<VeffRecordDto> records = [new VeffRecordDto(x: x,
                                           y: y,
                                           z: z,
                                           epocMillis: epocMillis) ];
            records[0].SampleTime = sampleTime;

            _testObj!.InsertVeffRecords(serialId, records);
            // insert same record twice, should only be 1 read
            _testObj!.InsertVeffRecords(serialId, records);

            string connectionString = _database!.ConnectionString;
            using NpgsqlConnection connection = new(connectionString);
            connection.Open();
            List<VeffRecordDto> dtos = ReadVeffRecords(connection);
            Assert.HasCount(1, dtos);
            VeffRecordDto dtoOut = dtos[0];

            Assert.IsTrue(TestUtil.VerifyDateTime(sampleTime, dtoOut.SampleTime));

            Assert.AreEqual(x, dtoOut!.X);
            Assert.AreEqual(y, dtoOut!.Y);
            Assert.AreEqual(z, dtoOut!.Z);

        }

        [TestMethod]
        public void TestInsertVdvRecord_Success()
        {
            string serialId = "123";
            long epocMillis = 1699960800001L;
            DateTime sampleTime = DateTimeOffset.FromUnixTimeMilliseconds(epocMillis).UtcDateTime;

            double x = 1.0;
            double y = 6.77;
            double z = 4.222;
            string vdvX = "foo";
            string vdvY = "jsdfkjhsf";
            string vdvZ = "klsgjlkjglsfgsbob";

            List<VdvRecordDto> records = [ new VdvRecordDto(x: x,
                                           y: y,
                                           z: z,
                                           epocMillis: epocMillis,
                                           vdvX: vdvX,
                                           vdvY: vdvY,
                                           vdvZ: vdvZ) ];
            records[0].SampleTime = sampleTime;

            _testObj!.InsertVdvRecords(serialId, records);
            // insert same record twice, should only be 1 read
            _testObj!.InsertVdvRecords(serialId, records);

            string connectionString = _database!.ConnectionString;
            using NpgsqlConnection connection = new(connectionString);
            connection.Open();
            List<VdvRecordDto> dtos = ReadVdvRecords(connection);
            Assert.HasCount(1, dtos);
            VdvRecordDto dtoOut = dtos[0];

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

            string connectionString = _database!.ConnectionString;
            using NpgsqlConnection connection = new(connectionString);
            connection.Open();

            string serialId = "13";
            List<VibrationMonitorDto> monitorsIn = OmnidotsFixture.MonitorsList(1);
            _testObj!.WriteMonitorList(monitorsIn);
            List<VibrationMonitorDto> monitorsOut = _testObj.ReadMonitorList(null);
            Assert.HasCount(1, monitorsOut);
            Guid monitorId = monitorsOut[0].Id;


            // add an alert and contact as RvtAlertContacts table has foreign key constraints
            InsertAlertRule(connection, 21, serialId, monitorId);
            List<Rvt.Monitor.Common.Rules.RvtAlertRuleDto> rules = _testObj!.ReadRules(serialId);
            Assert.HasCount(1, rules);
            string email = "bad-email";
            string phoneNo = "bad-phonenumber";
            Guid siteUserId = Guid.NewGuid();
            InsertContact(connection, monitorId, ContactMethod.Email, email, phoneNo, siteUserId);
            List<RvtContactDto> contacts = ReadContacts(connection, siteUserId);
            Assert.HasCount(1, contacts);

            DateTime dt = DateTime.Parse("2023-10-18T11:19:00Z").ToUniversalTime();
            NotificationDto notificationIn = new(id: Guid.NewGuid(),
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
            _testObj.WriteNotification(notificationIn);
            _testObj.WriteNotificationAudit(notificationIn.Id, "mytest@email.net", "some error message");

            List<NotificationDto> notifications = ReadNotifications(connection);
            Assert.HasCount(1, notifications);

            NotificationDto notificationOut = notifications[0];

            Assert.AreEqual(notificationIn.Id, notificationOut.Id);
            Assert.AreEqual(notificationIn.Level, notificationOut.Level);
            Assert.AreEqual(notificationIn.NotificationTime, notificationOut.NotificationTime);
            Assert.AreEqual(notificationIn.LimitOn, notificationOut.LimitOn);
            Assert.AreEqual(notificationIn.AveragingPeriod, notificationOut.AveragingPeriod);
            Assert.AreEqual(notificationIn.AlertType, notificationOut.AlertType);
            Assert.AreEqual(notificationIn.AlertField, notificationOut.AlertField);
            Assert.AreEqual(notificationIn.MonitorId, notificationOut.MonitorId);

            List<Dictionary<string, object>> audits = ReadNotificationsSent(connection);
            Assert.HasCount(1, audits);
            Dictionary<string, object> audit = audits[0];

            Assert.IsInstanceOfType<Guid>(audit["Id"]);
            Assert.IsInstanceOfType<DateTime>(audit["SendTime"]);
            DateTime sendTime = (DateTime)audit["SendTime"];
            Assert.IsTrue(sendTime < DateTime.UtcNow.AddSeconds(10) && sendTime > DateTime.UtcNow.AddSeconds(-10));
            Assert.IsInstanceOfType<string>(audit["Address"]);
            Assert.AreEqual("mytest@email.net", (string)audit["Address"]);
            Assert.IsInstanceOfType<string>(audit["ErrorMessage"]);
            Assert.AreEqual("some error message", (string)audit["ErrorMessage"]);
            Assert.IsInstanceOfType<Guid>(audit["NotificationId"]);

        }


        [TestMethod]
        public async Task TestWriteTraces()
        {

            string serialId = "12345";

            string json = OmnidotsFixture.TracesResponseJson();
            TracesReponse tracesResponse = JsonSerializer.Deserialize<TracesReponse>(json)!;

            DateTime t0 = DateTime.UtcNow;
            _testObj!.WriteTraces(serialId, tracesResponse.Traces!);
            TimeSpan tt = DateTime.UtcNow - t0;
            if (RvtLogger.Logger.IsEnabled(LogLevel.Information))
            {
                RvtLogger.Logger.LogInformation("WriteTraces took={} seconds", tt.TotalSeconds);
            }

            await using (NpgsqlConnection connection = _database!.OpenConnection())
            {
                await connection.OpenAsync(TestContext.CancellationToken);
                await using NpgsqlCommand command = new(
                    "SELECT trace_id, sample_index, x, y, z FROM omnidots_trace ORDER BY trace_id, sample_index;", connection);
                await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(TestContext.CancellationToken);
                Assert.IsTrue(await reader.ReadAsync(TestContext.CancellationToken));
                Assert.AreEqual(0, reader.GetInt32(1));
            }

            List<TestTraceData> tds = ReadTraces(_database!.ConnectionString, serialId);

            Assert.HasCount(tracesResponse.Traces!.Count, tds);

            for (int i = 0; i < tds.Count; i++)
            {
                TraceData expected = tracesResponse.Traces[i];
                TraceData actual = tds[i].TraceData;

                Assert.AreEqual(expected.StartTime, actual.StartTime);
                Assert.AreEqual(expected.EndTime, actual.EndTime);
                Assert.HasCount(expected.X!.Count, actual.X!);
                Assert.HasCount(expected.Y!.Count, actual.Y!);
                Assert.HasCount(expected.Z!.Count, actual.Z!);

                CollectionAssert.AreEqual(expected.X, actual.X);
                CollectionAssert.AreEqual(expected.Y, actual.Y);
                CollectionAssert.AreEqual(expected.Z, actual.Z);

            }
            Assert.HasCount(tracesResponse.Traces!.Count, tds);

        }

        [TestMethod]
        public void WriteTraces_DuplicateValuedSamplesRemainDistinctAndOrderedAcrossReplay()
        {
            const string serialId = "ordered-duplicates";
            TraceData trace = new()
            {
                StartTime = DateTimeUtil.GetMillis(Utc(2026, 7, 14, 12, 0)),
                EndTime = DateTimeUtil.GetMillis(Utc(2026, 7, 14, 12, 1)),
                X = [3.0, 1.0, 3.0, 1.0],
                Y = [9.0, 8.0, 9.0, 8.0],
                Z = [6.0, 5.0, 6.0, 5.0]
            };

            _testObj!.WriteTraces(serialId, [trace]);
            _testObj.WriteTraces(serialId, [trace]);

            List<TestTraceData> storedTraces = ReadTraces(_database!.ConnectionString, serialId);
            Assert.HasCount(2, storedTraces, "Trace replay retains the append-only compatibility behavior.");
            foreach (TestTraceData storedTrace in storedTraces)
            {
                CollectionAssert.AreEqual(trace.X, storedTrace.TraceData.X);
                CollectionAssert.AreEqual(trace.Y, storedTrace.TraceData.Y);
                CollectionAssert.AreEqual(trace.Z, storedTrace.TraceData.Z);
                CollectionAssert.AreEqual(
                    _expected,
                    ReadSampleIndexes(_database.ConnectionString, storedTrace.Id));
            }
        }

        [TestMethod]
        public async Task ReadLatestTraceEndTimes_ReturnsMaximumForEachRequestedSerial()
        {
            await using NpgsqlConnection connection = _database!.OpenConnection();
            await connection.OpenAsync(TestContext.CancellationToken);
            DateTime serialAOld = Utc(2026, 7, 10, 8, 0);
            DateTime serialANew = Utc(2026, 7, 12, 9, 0);
            DateTime serialB = Utc(2026, 7, 11, 10, 0);
            await using NpgsqlCommand insert = new(
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
            await insert.ExecuteNonQueryAsync(TestContext.CancellationToken);

            IReadOnlyDictionary<string, DateTime> result = _testObj!.ReadLatestTraceEndTimes(["trace-a", "trace-b", "missing"]);

            Assert.HasCount(2, result);
            Assert.AreEqual(serialANew, result["trace-a"]);
            Assert.AreEqual(serialB, result["trace-b"]);
        }

        [TestMethod]
        public async Task WriteTraces_WhenSampleInsertFails_RollsBackTraceIndexAndSamples()
        {
            await using NpgsqlConnection connection = _database!.OpenConnection();
            await connection.OpenAsync(TestContext.CancellationToken);
            await using NpgsqlCommand createTrigger = new(
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
            await createTrigger.ExecuteNonQueryAsync(TestContext.CancellationToken);

            try
            {
                TraceData trace = new()
                {
                    StartTime = DateTimeUtil.GetMillis(Utc(2026, 7, 14, 13, 0)),
                    EndTime = DateTimeUtil.GetMillis(Utc(2026, 7, 14, 13, 1)),
                    X = [1.0, 2.0, 3.0],
                    Y = [4.0, 5.0, 6.0],
                    Z = [7.0, 8.0, 9.0]
                };

                Assert.ThrowsExactly<Microsoft.EntityFrameworkCore.DbUpdateException>(
                    () => _testObj!.WriteTraces("atomic-trace", [trace]));
                Assert.AreEqual(0, CountRows(_database.ConnectionString, "omnidots_trace_index"));
                Assert.AreEqual(0, CountRows(_database.ConnectionString, "omnidots_trace"));
            }
            finally
            {
                await using NpgsqlCommand dropTrigger = new(
                    """
                DROP TRIGGER IF EXISTS fail_second_trace_sample ON omnidots_trace;
                DROP FUNCTION IF EXISTS fail_second_trace_sample();
                """, connection);
                await dropTrigger.ExecuteNonQueryAsync(TestContext.CancellationToken);
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

            using NpgsqlConnection connection = new(connectionString);
            connection.Open();
            string sql = @"SELECT id, start_time, end_time FROM omnidots_trace_index
                        WHERE serial_id = @SerialId
                        ORDER BY start_time";


            List<TestTraceData> traceDataList = [];

            {
                using NpgsqlCommand cmd = new(sql, connection);
                cmd.Parameters.AddWithValue("@SerialId", serialId);

                using NpgsqlDataReader reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    Guid traceId = reader.GetGuid(0);
                    DateTime startTime = reader.GetDateTime(1);
                    DateTime endTime = reader.GetDateTime(2);

                    TraceData td = new()
                    {
                        StartTime = DateTimeUtil.GetMillis(startTime),
                        EndTime = DateTimeUtil.GetMillis(endTime),

                        X = [],
                        Y = [],
                        Z = []
                    };
                    traceDataList.Add(new TestTraceData(traceId, td));

                }
            }

            foreach (TestTraceData testData in traceDataList)
            {
                string traceSql = @"SELECT x, y, z FROM omnidots_trace
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
            using NpgsqlConnection connection = new(connectionString);
            connection.Open();
            using NpgsqlCommand command = new(
                "SELECT sample_index FROM omnidots_trace WHERE trace_id = $1 ORDER BY sample_index;",
                connection);
            command.Parameters.AddWithValue(traceId);
            using NpgsqlDataReader reader = command.ExecuteReader();
            List<int> indexes = [];
            while (reader.Read())
            {
                indexes.Add(reader.GetInt32(0));
            }

            return [.. indexes];
        }

        private static void InsertAlertRule(NpgsqlConnection connection, int index, string serialId, Guid monitorId)
        {
            string sql = @"INSERT INTO rvt_alert_rule
                            (id, serial_id, alert_field, limit_on, limit_off, alert_type,
                             is_active, averaging_period, weekdays, saturdays, sundays,
                             start_time, end_time, is_deleted, monitor_id, created)
                        VALUES
                            (@Id, @SerialId, @AlertField, @LimitOn, @LimitOff, @AlertType,
                             @IsActive, @AveragingPeriod, @Weekdays, @Saturdays, @Sundays,
                             @StartTime, @EndTime, @IsDeleted, @MonitorId, @Created);";

            bool isEven = index % 2 == 0;
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
                cmd.Parameters.AddWithValue("@ContractNumber", Guid.NewGuid().ToString("N"));
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
                cmd.Parameters.Add("@StartTime", NpgsqlDbType.Time).Value =
                    sendStartTime?.TimeOfDay ?? (object)DBNull.Value;
                cmd.Parameters.Add("@EndTime", NpgsqlDbType.Time).Value =
                    sendEndTime?.TimeOfDay ?? (object)DBNull.Value;

                cmd.ExecuteNonQuery();
            }
        }

        private static ContactMethod ReadContactMethod(string connectionString, Guid siteUserId)
        {

            using NpgsqlConnection connection = new(connectionString);
            connection.Open();
            string sql = @"SELECT email, sms FROM notification_setting WHERE site_user_id = @SiteUserId";

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
            string sql = @"SELECT ""Email"", ""PhoneNumber"", ""Id"" FROM ""AspNetUsers""";
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

        private static List<NotificationDto> ReadNotifications(NpgsqlConnection connection)
        {

            string sql = @"SELECT id, notification_time, limit_on, averaging_period, level, closed_time,
                               closed_by_user, alert_type, alert_field, monitor_id
                        FROM notification";
            using NpgsqlCommand cmd = new(sql, connection);
            List<NotificationDto> alerts = [];
            using NpgsqlDataReader reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                Guid id = reader.GetGuid(0);
                DateTime notificationTime = reader.GetDateTime(1);
                double limitOn = reader.GetDouble(2);
                int averagingPeriod = reader.GetInt32(3);
                double level = reader.GetDouble(4);
                DateTime? closedTime = reader.IsDBNull(5) ? null : reader.GetDateTime(5);
                Guid? closedByUser = reader.IsDBNull(6) ? null : reader.GetGuid(6);
                AlertType alertType = (AlertType)reader.GetInt32(7);
                string alertField = reader.GetString(8);
                Guid monitorId = reader.GetGuid(9);

                NotificationDto alert = new(id: id,
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

            string sql = @"SELECT id, send_time, address, error_message, notification_id
                        FROM notification_sent";
            using NpgsqlCommand cmd = new(sql, connection);
            List<Dictionary<string, object>> audits = [];
            using NpgsqlDataReader reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                Dictionary<string, object> dict = new()
                {
                    ["Id"] = reader.GetGuid(0),
                    ["SendTime"] = reader.GetDateTime(1),
                    ["Address"] = reader.GetString(2),
                    ["ErrorMessage"] = reader.GetString(3),
                    ["NotificationId"] = reader.GetGuid(4)
                };
                audits.Add(dict);
            }
            return audits;
        }


        // Summary: Reads persisted peak fixture rows using canonical PostgreSQL identifiers.
        private static List<PeakRecordDto> ReadPeakRecords(NpgsqlConnection connection)
        {
            string sql = @"SELECT serial_id, sample_time,
                               x_fdom, x_vtop, x_vtop_overflow,
                               y_fdom, y_vtop, y_vtop_overflow,
                               z_fdom, z_vtop, z_vtop_overflow
                        FROM omnidots_peak_level";
            using NpgsqlCommand cmd = new(sql, connection);
            List<PeakRecordDto> dtos = [];
            using NpgsqlDataReader reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                string serialId = reader.GetString(0);
                DateTime sampleTime = reader.GetDateTime(1);
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

                double epocMillis = sampleTime.Subtract(DateTimeUtil.JAN1_1970).TotalMilliseconds;

                dtos.Add(new PeakRecordDto(x: x, y: y, z: z, epocMillis: epocMillis));
            }

            return dtos;
        }


        // Summary: Reads persisted VEFF fixture rows using canonical PostgreSQL identifiers.
        private static List<VeffRecordDto> ReadVeffRecords(NpgsqlConnection connection)
        {
            string sql = @"SELECT serial_id, sample_time, x, y, z
                    FROM omnidots_veff_level";
            using NpgsqlCommand cmd = new(sql, connection);
            List<VeffRecordDto> dtos = [];
            using NpgsqlDataReader reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                string serialId = reader.GetString(0);
                DateTime sampleTime = reader.GetDateTime(1);
                double x = reader.GetDouble(2);
                double y = reader.GetDouble(3);
                double z = reader.GetDouble(4);
                double epocMillis = sampleTime.Subtract(DateTimeUtil.JAN1_1970).TotalMilliseconds;

                dtos.Add(new VeffRecordDto(x: x, y: y, z: z, epocMillis: epocMillis));
            }

            return dtos;
        }

        // Summary: Reads persisted VDV fixture rows using canonical PostgreSQL identifiers.
        private static List<VdvRecordDto> ReadVdvRecords(NpgsqlConnection connection)
        {
            string sql = @"SELECT serial_id, sample_time, x, y, z, vdv_x, vdv_y, vdv_z
                    FROM omnidots_vdv_level";
            using NpgsqlCommand cmd = new(sql, connection);
            List<VdvRecordDto> dtos = [];
            using NpgsqlDataReader reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                string serialId = reader.GetString(0);
                DateTime sampleTime = reader.GetDateTime(1);
                double x = reader.GetDouble(2);
                double y = reader.GetDouble(3);
                double z = reader.GetDouble(4);
                string vdvX = reader.GetString(5);
                string vdvY = reader.GetString(6);
                string vdvZ = reader.GetString(7);

                double epocMillis = sampleTime.Subtract(DateTimeUtil.JAN1_1970).TotalMilliseconds;

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

        public TestContext TestContext { get; set; } = null!;

        private static readonly int[] _expected = [0, 1, 2, 3];
    }
}
