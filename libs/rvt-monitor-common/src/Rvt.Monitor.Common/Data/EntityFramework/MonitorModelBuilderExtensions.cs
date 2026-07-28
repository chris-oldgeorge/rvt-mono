using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rvt.Monitor.Common.Alerts;
using Rvt.Monitor.Common.Data.Entities;

namespace Rvt.Monitor.Common.Data.EntityFramework;

public static class MonitorModelBuilderExtensions
{
    public static ModelBuilder ApplySharedMonitorMappings(
        this ModelBuilder modelBuilder,
        MonitorDbOptions options)
    {
        modelBuilder.Entity<MonitorEntity>(entity =>
        {
            entity.ToTable(TableName(options, "MonitorsList", "monitor"));
            entity.HasKey(row => row.Id);
            MapProperty(entity, row => row.Id, "id");
            MapProperty(entity, row => row.FleetNr, "fleet_row_count");
            MapProperty(entity, row => row.SerialId, "serial_id");
            MapProperty(entity, row => row.CustomerId, "customer_id");
            MapProperty(entity, row => row.ListedAtTime, "listed_at_time");
            MapProperty(entity, row => row.Model, "model");
            MapProperty(entity, row => row.LocationId, "location_id");
            MapProperty(entity, row => row.Latitude, "latitude");
            MapProperty(entity, row => row.Longitude, "longitude");
            MapProperty(entity, row => row.LocationAddress, "location_address");
            MapProperty(entity, row => row.TimeZone, "time_zone");
            MapProperty(entity, row => row.CustomerDisplayName, "customer_display_name");
            MapProperty(entity, row => row.Manufacturer, "manufacturer");
            MapProperty(entity, row => row.FirmwareVersion, "firmware_version");
            MapProperty(entity, row => row.TypeOfMonitor, "type_of_monitor");
            MapProperty(entity, row => row.Offline, "offline");
            MapProperty(entity, row => row.LastDataTime1Min, "last_data_time_1_min");
            MapProperty(entity, row => row.LastDataTime15Min, "last_data_time_15_min");
            MapProperty(entity, row => row.LastDataTime1Hour, "last_data_time_1_hour");
            MapProperty(entity, row => row.LastDataTime24Hour, "last_data_time_24_hour");
            MapProperty(entity, row => row.BatteryStatus, "battery_status");
            entity.HasIndex(row => new { row.SerialId, row.TypeOfMonitor })
                .HasDatabaseName("ix_monitor_serial_id_type_of_monitor");
        });

        modelBuilder.Entity<DeploymentEntity>(entity =>
        {
            entity.ToTable(TableName(options, "Deployments", "deployment"));
            entity.HasKey(row => row.Id);
            MapProperty(entity, row => row.Id, "id");
            MapProperty(entity, row => row.StartDate, "start_date");
            MapProperty(entity, row => row.EndDate, "end_date");
            MapProperty(entity, row => row.Lng, "lng");
            MapProperty(entity, row => row.Lat, "lat");
            MapProperty(entity, row => row.What2words, "what2words");
            MapProperty(entity, row => row.What3Words, "what3words");
            MapProperty(entity, row => row.PictureLink, "picture_link");
            MapProperty(entity, row => row.ContractId, "contract_id");
            MapProperty(entity, row => row.MonitorId, "monitor_id");
        });

        modelBuilder.Entity<ContractEntity>(entity =>
        {
            entity.ToTable(TableName(options, "Contracts", "contract"));
            entity.HasKey(row => row.Id);
            MapProperty(entity, row => row.Id, "id");
            MapProperty(entity, row => row.ContractNumber, "contract_number");
            MapProperty(entity, row => row.OnHireDate, "on_hire_date");
            MapProperty(entity, row => row.OffHireDate, "off_hire_date");
            MapProperty(entity, row => row.CompanyId, "company_id");
            MapProperty(entity, row => row.SiteId, "site_id");
        });

        modelBuilder.Entity<SiteEntity>(entity =>
        {
            entity.ToTable(TableName(options, "Sites", "site"));
            entity.HasKey(row => row.Id);
            MapProperty(entity, row => row.Id, "id");
            MapProperty(entity, row => row.SiteName, "site_name");
            MapProperty(entity, row => row.CreateDate, "create_date");
            MapProperty(entity, row => row.AddressLine1, "address_line_1");
            MapProperty(entity, row => row.AddressLine2, "address_line_2");
            MapProperty(entity, row => row.Postcode, "postcode");
            MapProperty(entity, row => row.City, "city");
            MapProperty(entity, row => row.County, "county");
            MapProperty(entity, row => row.StartTime, "start_time");
            MapProperty(entity, row => row.EndTime, "end_time");
            MapProperty(entity, row => row.SatStartTime, "sat_start_time");
            MapProperty(entity, row => row.SatEndTime, "sat_end_time");
            MapProperty(entity, row => row.SunStartTime, "sun_start_time");
            MapProperty(entity, row => row.SunEndTime, "sun_end_time");
        });

        modelBuilder.Entity<RvtAlertRuleEntity>(entity =>
        {
            entity.ToTable(TableName(options, "RvtAlertRules", "rvt_alert_rule"));
            entity.HasKey(row => row.Id);
            MapProperty(entity, row => row.Id, "id");
            MapProperty(entity, row => row.MonitorId, "monitor_id");
            MapProperty(entity, row => row.SerialId, "serial_id");
            MapProperty(entity, row => row.AlertField, "alert_field");
            MapProperty(entity, row => row.LimitOn, "limit_on");
            MapProperty(entity, row => row.LimitOff, "limit_off");
            MapProperty(entity, row => row.AlertType, "alert_type");
            MapProperty(entity, row => row.IsActive, "is_active");
            MapProperty(entity, row => row.AveragingPeriod, "averaging_period");
            MapProperty(entity, row => row.Weekdays, "weekdays");
            MapProperty(entity, row => row.Saturdays, "saturdays");
            MapProperty(entity, row => row.Sundays, "sundays");
            MapProperty(entity, row => row.StartTime, "start_time");
            MapProperty(entity, row => row.EndTime, "end_time");
            MapProperty(entity, row => row.IsDeleted, "is_deleted");
            MapProperty(entity, row => row.Created, "created");
            MapProperty(entity, row => row.Accessed, "accessed");
        });

        modelBuilder.Entity<NotificationEntity>(entity =>
        {
            entity.ToTable(TableName(options, "Notifications", "notification"));
            entity.HasKey(row => row.Id);
            MapProperty(entity, row => row.Id, "id");
            MapProperty(entity, row => row.NotificationTime, "notification_time");
            MapProperty(entity, row => row.LimitOn, "limit_on");
            MapProperty(entity, row => row.AveragingPeriod, "averaging_period");
            MapProperty(entity, row => row.Level, "level");
            MapProperty(entity, row => row.ClosedTime, "closed_time");
            MapProperty(entity, row => row.ClosedByUser, "closed_by_user");
            MapProperty(entity, row => row.ClosedByNote, "closed_by_note");
            MapProperty(entity, row => row.MonitorId, "monitor_id");
            MapProperty(entity, row => row.AlertField, "alert_field");
            MapProperty(entity, row => row.AlertType, "alert_type");
        });

        modelBuilder.Entity<MonitorDeliveryOutboxEntity>(entity =>
        {
            entity.ToTable(TableName(options, "MonitorDeliveryOutbox", "monitor_delivery_outbox"));
            entity.HasKey(row => row.Id);
            entity.HasOne<NotificationEntity>()
                .WithMany()
                .HasForeignKey(row => row.NotificationId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(row => new { row.Producer, row.DeliveryKey })
                .IsUnique()
                .HasDatabaseName("uq_monitor_delivery_outbox_producer_delivery_key");
            entity.HasIndex(row => new { row.Producer, row.Status, row.NextAttemptAt })
                .HasDatabaseName("ix_monitor_delivery_outbox_due");
            entity.HasIndex(row => row.NotificationId)
                .HasDatabaseName("ix_monitor_delivery_outbox_notification_id");
            MapProperty(entity, row => row.Id, "id");
            MapProperty(entity, row => row.Producer, "producer");
            MapProperty(entity, row => row.NotificationId, "notification_id");
            MapProperty(entity, row => row.CorrelationKey, "correlation_key");
            MapProperty(entity, row => row.DeliveryKey, "delivery_key");
            entity.Property(row => row.Kind)
                .HasConversion<string>()
                .HasColumnName("kind")
                .HasColumnType("text")
                .HasMaxLength(64);
            MapProperty(entity, row => row.Destination, "destination");
            MapProperty(entity, row => row.PayloadVersion, "payload_version");
            MapProperty(entity, row => row.Payload, "payload");
            MapProperty(entity, row => row.Status, "status");
            MapProperty(entity, row => row.AttemptCount, "attempt_count");
            MapProperty(entity, row => row.NextAttemptAt, "next_attempt_at");
            MapProperty(entity, row => row.LeaseId, "lease_id");
            MapProperty(entity, row => row.LeaseUntil, "lease_until");
            MapProperty(entity, row => row.CompletedAt, "completed_at");
            MapProperty(entity, row => row.DeadLetteredAt, "dead_lettered_at");
            MapProperty(entity, row => row.LastError, "last_error");
            MapProperty(entity, row => row.CreatedAt, "created_at");

            entity.Property(row => row.Producer).HasColumnType("text").HasMaxLength(64);
            entity.Property(row => row.CorrelationKey).HasColumnType("text").HasMaxLength(450);
            entity.Property(row => row.DeliveryKey).HasColumnType("text").HasMaxLength(450);
            entity.Property(row => row.Destination).HasColumnType("text").HasMaxLength(512);
            entity.Property(row => row.Status).HasColumnType("text").HasMaxLength(32);
            entity.Property(row => row.LastError).HasColumnType("text").HasMaxLength(1024);
        });

        modelBuilder.Entity<NotificationSentEntity>(entity =>
        {
            entity.ToTable(TableName(options, "NotificationsSent", "notification_sent"));
            entity.HasKey(row => row.Id);
            MapProperty(entity, row => row.Id, "id");
            MapProperty(entity, row => row.SendTime, "send_time");
            MapProperty(entity, row => row.Address, "address");
            MapProperty(entity, row => row.ErrorMessage, "error_message");
            MapProperty(entity, row => row.NotificationId, "notification_id");
        });

        modelBuilder.Entity<NotificationSettingEntity>(entity =>
        {
            entity.ToTable(TableName(options, "NotificationSettings", "notification_setting"));
            entity.HasKey(row => row.Id);
            MapProperty(entity, row => row.Id, "id");
            MapProperty(entity, row => row.Email, "email");
            MapProperty(entity, row => row.SMS, "sms");
            MapProperty(entity, row => row.StartTime, "start_time");
            MapProperty(entity, row => row.EndTime, "end_time");
            MapProperty(entity, row => row.SiteUserId, "site_user_id");
        });

        modelBuilder.Entity<AspNetUserEntity>(entity =>
        {
            entity.ToTable(TableName(options, "AspNetUsers", "AspNetUsers"));
            entity.HasKey(row => row.Id);
            MapProperty(entity, row => row.Id, "Id");
            MapProperty(entity, row => row.CompanyId, "company_id");
            MapProperty(entity, row => row.IsDisabled, "is_disabled");
            MapProperty(entity, row => row.Name, "name");
            MapProperty(entity, row => row.UserName, "UserName");
            MapProperty(entity, row => row.NormalizedUserName, "normalized_user_name");
            MapProperty(entity, row => row.Email, "Email");
            MapProperty(entity, row => row.NormalizedEmail, "normalized_email");
            MapProperty(entity, row => row.EmailConfirmed, "email_confirmed");
            MapProperty(entity, row => row.PasswordHash, "password_hash");
            MapProperty(entity, row => row.SecurityStamp, "security_stamp");
            MapProperty(entity, row => row.ConcurrencyStamp, "concurrency_stamp");
            MapProperty(entity, row => row.PhoneNumber, "PhoneNumber");
            MapProperty(entity, row => row.PhoneNumberConfirmed, "phone_number_confirmed");
            MapProperty(entity, row => row.TwoFactorEnabled, "two_factor_enabled");
            MapProperty(entity, row => row.LockoutEnd, "lockout_end");
            MapProperty(entity, row => row.LockoutEnabled, "lockout_enabled");
            MapProperty(entity, row => row.AccessFailedCount, "access_failed_count");
            MapProperty(entity, row => row.CompanyRole, "company_role");
        });

        modelBuilder.Entity<SiteUserEntity>(entity =>
        {
            entity.ToTable(TableName(options, "SiteUsers", "site_user"));
            entity.HasKey(row => row.Id);
            MapProperty(entity, row => row.Id, "id");
            MapProperty(entity, row => row.StartDate, "start_date");
            MapProperty(entity, row => row.EndDate, "end_date");
            MapProperty(entity, row => row.UserId, "user_id");
            MapProperty(entity, row => row.SiteId, "site_id");
        });

        modelBuilder.Entity<SiteAverageEntity>(entity =>
        {
            entity.ToTable(TableName(options, "SiteAverages", "site_average"));
            entity.HasKey(row => row.Id);
            MapProperty(entity, row => row.Id, "id");
            MapProperty(entity, row => row.SiteId, "site_id");
            MapProperty(entity, row => row.MonitorId, "monitor_id");
            MapProperty(entity, row => row.Field, "field");
            MapProperty(entity, row => row.Level, "level");
            MapProperty(entity, row => row.CollectionTime, "collection_time");
        });

        modelBuilder.Entity<ErrorMessageEntity>(entity =>
        {
            entity.ToTable("error_log");
            entity.HasNoKey();
            MapProperty(entity, row => row.Host, "host");
            MapProperty(entity, row => row.Source, "source");
            MapProperty(entity, row => row.Message, "message");
            MapProperty(entity, row => row.Level, "level");
            MapProperty(entity, row => row.StackTrace, "stack_trace");
            MapProperty(entity, row => row.Variables, "variables");
            MapProperty(entity, row => row.LogTime, "logged_at");
        });

        modelBuilder.Entity<AlertOccurrenceEntity>(entity =>
        {
            entity.ToTable(
                TableName(options, "AlertOccurrences", "alert_occurrence"),
                table =>
                {
                    table.HasCheckConstraint(
                        "ck_alert_occurrence_source_key_hash",
                        "octet_length(\"source_key_hash\") = 32");
                    table.HasCheckConstraint(
                        "ck_alert_occurrence_outcome",
                        ExactStringCheck(
                            "outcome",
                            nameof(AlertOccurrenceOutcome.Accepted),
                            nameof(AlertOccurrenceOutcome.Ignored),
                            nameof(AlertOccurrenceOutcome.Suppressed)));
                });
            entity.HasKey(row => row.Id);
            ConfigureGuid(entity.Property(row => row.Id), "id");
            ConfigureString(entity.Property(row => row.Source), "source", 128);
            entity.Property(row => row.SourceKeyHash)
                .HasColumnName("source_key_hash")
                .HasColumnType("bytea")
                .HasMaxLength(32);
            ConfigureGuid(entity.Property(row => row.NotificationId), "notification_id");
            ConfigureGuid(entity.Property(row => row.MonitorId), "monitor_id");
            ConfigureString(entity.Property(row => row.SerialId), "serial_id", 128);
            ConfigureInstant(entity.Property(row => row.EventTime), "event_time");
            ConfigureInt(entity.Property(row => row.AlertType), "alert_type");
            ConfigureString(entity.Property(row => row.AlertField), "alert_field", 128);
            ConfigureDouble(entity.Property(row => row.Level), "level");
            ConfigureDouble(entity.Property(row => row.LimitOn), "limit_on");
            ConfigureInt(entity.Property(row => row.AveragingPeriod), "averaging_period");
            ConfigureString(entity.Property(row => row.Outcome), "outcome", 32);
            ConfigureInstant(entity.Property(row => row.CreatedAt), "created_at");
            entity.HasIndex(row => new { row.Source, row.SourceKeyHash })
                .IsUnique()
                .HasDatabaseName("uq_alert_occurrence_source_key");
            entity.HasIndex(row => row.MonitorId)
                .HasDatabaseName("ix_alert_occurrence_monitor_id");
            entity.HasIndex(row => row.NotificationId)
                .HasDatabaseName("ix_alert_occurrence_notification_id");
            entity.HasOne<MonitorEntity>()
                .WithMany()
                .HasForeignKey(row => row.MonitorId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<NotificationEntity>()
                .WithMany()
                .HasForeignKey(row => row.NotificationId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AlertDeliveryOutboxEntity>(entity =>
        {
            entity.ToTable(
                TableName(options, "AlertDeliveryOutbox", "alert_delivery_outbox"),
                table =>
                {
                    table.HasCheckConstraint(
                        "ck_alert_delivery_outbox_kind",
                        ExactStringCheck("kind", "MqttAlert", "Email", "Sms"));
                    table.HasCheckConstraint(
                        "ck_alert_delivery_outbox_status",
                        ExactStringCheck("status", "Pending", "Leased", "Completed", "DeadLetter"));
                });
            entity.HasKey(row => row.Id);
            ConfigureGuid(entity.Property(row => row.Id), "id");
            ConfigureGuid(entity.Property(row => row.OccurrenceId), "occurrence_id");
            ConfigureString(entity.Property(row => row.DeliveryKey), "delivery_key", 64);
            ConfigureString(entity.Property(row => row.Kind), "kind", 32);
            ConfigureString(entity.Property(row => row.Destination), "destination", 512);
            ConfigureString(entity.Property(row => row.Payload), "payload", 8192);
            ConfigureString(entity.Property(row => row.Status), "status", 32);
            ConfigureInt(entity.Property(row => row.AttemptCount), "attempt_count");
            ConfigureInstant(entity.Property(row => row.NextAttemptAt), "next_attempt_at");
            ConfigureGuid(entity.Property(row => row.LeaseId), "lease_id");
            ConfigureInstant(entity.Property(row => row.LeaseUntil), "lease_until");
            ConfigureInstant(entity.Property(row => row.CompletedAt), "completed_at");
            ConfigureString(entity.Property(row => row.LastError), "last_error", 256);
            ConfigureInstant(entity.Property(row => row.CreatedAt), "created_at");
            entity.HasIndex(row => row.DeliveryKey)
                .IsUnique()
                .HasDatabaseName("uq_alert_delivery_outbox_delivery_key");
            entity.HasIndex(row => new { row.Status, row.NextAttemptAt, row.LeaseUntil, row.CreatedAt })
                .HasDatabaseName("ix_alert_delivery_outbox_due");
            entity.HasIndex(row => row.OccurrenceId)
                .HasDatabaseName("ix_alert_delivery_outbox_occurrence_id");
            entity.HasOne<AlertOccurrenceEntity>()
                .WithMany()
                .HasForeignKey(row => row.OccurrenceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        return modelBuilder;
    }

    private static string TableName(
        MonitorDbOptions options,
        string logicalName,
        string canonicalName)
    {
        if (!options.IdentifierMap.TryGetValue(logicalName, out string? mappedName))
        {
            return canonicalName;
        }

        string safeName = MonitorDb.RequireSafeSqlIdentifier(
            mappedName,
            $"shared EF table '{logicalName}'");
        return safeName.Length >= 2 && safeName[0] == '"' && safeName[^1] == '"'
            ? safeName[1..^1]
            : safeName;
    }

    private static void MapProperty<TEntity, TProperty>(
        EntityTypeBuilder<TEntity> entity,
        System.Linq.Expressions.Expression<Func<TEntity, TProperty>> propertyExpression,
        string columnName)
        where TEntity : class
    {
        entity.Property(propertyExpression).HasColumnName(columnName);
    }

    private static void ConfigureGuid<TProperty>(
        PropertyBuilder<TProperty> property,
        string columnName)
    {
        property
            .HasColumnName(columnName)
            .HasColumnType("uuid");
    }

    private static void ConfigureString<TProperty>(
        PropertyBuilder<TProperty> property,
        string columnName,
        int maxLength)
    {
        property
            .HasColumnName(columnName)
            .HasColumnType($"varchar({maxLength})")
            .HasMaxLength(maxLength);
    }

    private static void ConfigureInstant<TProperty>(
        PropertyBuilder<TProperty> property,
        string columnName)
    {
        property
            .HasColumnName(columnName)
            .HasColumnType("timestamp with time zone");
    }

    private static void ConfigureInt(
        PropertyBuilder<int> property,
        string columnName)
    {
        property
            .HasColumnName(columnName)
            .HasColumnType("integer");
    }

    private static void ConfigureDouble(
        PropertyBuilder<double> property,
        string columnName)
    {
        property
            .HasColumnName(columnName)
            .HasColumnType("double precision");
    }

    private static string ExactStringCheck(
        string columnName,
        params string[] values)
    {
        return $"\"{columnName}\" IN ({string.Join(',', values.Select(value => $"'{value}'"))})";
    }
}
