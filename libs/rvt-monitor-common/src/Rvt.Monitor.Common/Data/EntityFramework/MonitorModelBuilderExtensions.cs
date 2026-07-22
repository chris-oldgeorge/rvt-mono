using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rvt.Monitor.Common.Alerts;
using Rvt.Monitor.Common.Data.Entities;

namespace Rvt.Monitor.Common.Data.EntityFramework;

public static class MonitorModelBuilderExtensions
{
    public static ModelBuilder ApplySharedMonitorMappings(this ModelBuilder modelBuilder, MonitorDbOptions options)
    {
        modelBuilder.Entity<MonitorEntity>(entity =>
        {
            entity.ToTable(TableName(options, "MonitorsList", "monitor"), Schema(options));
            entity.HasKey(row => row.Id);
            MapProperty(entity, row => row.Id, options, "Id", "id");
            MapProperty(entity, row => row.FleetNr, options, "FleetNr", "fleet_row_count");
            MapProperty(entity, row => row.SerialId, options, "SerialId", "serial_id");
            MapProperty(entity, row => row.CustomerId, options, "CustomerId", "customer_id");
            MapProperty(entity, row => row.ListedAtTime, options, "ListedAtTime", "listed_at_time");
            MapProperty(entity, row => row.Model, options, "Model", "model");
            MapProperty(entity, row => row.LocationId, options, "LocationId", "location_id");
            MapProperty(entity, row => row.Latitude, options, "Latitude", "latitude");
            MapProperty(entity, row => row.Longitude, options, "Longitude", "longitude");
            MapProperty(entity, row => row.LocationAddress, options, "LocationAddress", "location_address");
            MapProperty(entity, row => row.TimeZone, options, "TimeZone", "time_zone");
            MapProperty(entity, row => row.CustomerDisplayName, options, "CustomerDisplayName", "customer_display_name");
            MapProperty(entity, row => row.Manufacturer, options, "Manufacturer", "manufacturer");
            MapProperty(entity, row => row.FirmwareVersion, options, "FirmwareVersion", "firmware_version");
            MapProperty(entity, row => row.TypeOfMonitor, options, "TypeOfMonitor", "type_of_monitor");
            MapProperty(entity, row => row.Offline, options, "Offline", "offline");
            MapProperty(entity, row => row.LastDataTime1Min, options, "LastDataTime1Min", "last_data_time_1_min");
            MapProperty(entity, row => row.LastDataTime15Min, options, "LastDataTime15Min", "last_data_time_15_min");
            MapProperty(entity, row => row.LastDataTime1Hour, options, "LastDataTime1Hour", "last_data_time_1_hour");
            MapProperty(entity, row => row.LastDataTime24Hour, options, "LastDataTime24Hour", "last_data_time_24_hour");
            MapProperty(entity, row => row.BatteryStatus, options, "BatteryStatus", "battery_status");
            entity.HasIndex(row => new { row.SerialId, row.TypeOfMonitor });
        });

        modelBuilder.Entity<DeploymentEntity>(entity =>
        {
            entity.ToTable(TableName(options, "Deployments", "deployment"), Schema(options));
            entity.HasKey(row => row.Id);
            MapProperty(entity, row => row.Id, options, "Id", "id");
            MapProperty(entity, row => row.StartDate, options, "StartDate", "start_date");
            MapProperty(entity, row => row.EndDate, options, "EndDate", "end_date");
            MapProperty(entity, row => row.Lng, options, "Lng", "lng");
            MapProperty(entity, row => row.Lat, options, "Lat", "lat");
            MapProperty(entity, row => row.What2words, options, "What2words", "what2words");
            MapProperty(entity, row => row.What3Words, options, "What3Words", "what3words");
            MapProperty(entity, row => row.PictureLink, options, "PictureLink", "picture_link");
            MapProperty(entity, row => row.ContractId, options, "ContractId", "contract_id");
            MapProperty(entity, row => row.MonitorId, options, "MonitorId", "monitor_id");
        });

        modelBuilder.Entity<ContractEntity>(entity =>
        {
            entity.ToTable(TableName(options, "Contracts", "contract"), Schema(options));
            entity.HasKey(row => row.Id);
            MapProperty(entity, row => row.Id, options, "Id", "id");
            MapProperty(entity, row => row.ContractNumber, options, "ContractNumber", "contract_number");
            MapProperty(entity, row => row.OnHireDate, options, "OnHireDate", "on_hire_date");
            MapProperty(entity, row => row.OffHireDate, options, "OffHireDate", "off_hire_date");
            MapProperty(entity, row => row.CompanyId, options, "CompanyId", "company_id");
            MapProperty(entity, row => row.SiteId, options, "SiteId", "site_id");
        });

        modelBuilder.Entity<SiteEntity>(entity =>
        {
            entity.ToTable(TableName(options, "Sites", "site"), Schema(options));
            entity.HasKey(row => row.Id);
            MapProperty(entity, row => row.Id, options, "Id", "id");
            MapProperty(entity, row => row.SiteName, options, "SiteName", "site_name");
            MapProperty(entity, row => row.CreateDate, options, "CreateDate", "create_date");
            MapProperty(entity, row => row.AddressLine1, options, "AddressLine1", "address_line_1");
            MapProperty(entity, row => row.AddressLine2, options, "AddressLine2", "address_line_2");
            MapProperty(entity, row => row.Postcode, options, "Postcode", "postcode");
            MapProperty(entity, row => row.City, options, "City", "city");
            MapProperty(entity, row => row.County, options, "County", "county");
            MapProperty(entity, row => row.StartTime, options, "StartTime", "start_time");
            MapProperty(entity, row => row.EndTime, options, "EndTime", "end_time");
            MapProperty(entity, row => row.SatStartTime, options, "SatStartTime", "sat_start_time");
            MapProperty(entity, row => row.SatEndTime, options, "SatEndTime", "sat_end_time");
            MapProperty(entity, row => row.SunStartTime, options, "SunStartTime", "sun_start_time");
            MapProperty(entity, row => row.SunEndTime, options, "SunEndTime", "sun_end_time");
        });

        modelBuilder.Entity<RvtAlertRuleEntity>(entity =>
        {
            entity.ToTable(TableName(options, "RvtAlertRules", "rvt_alert_rule"), Schema(options));
            entity.HasKey(row => row.Id);
            MapProperty(entity, row => row.Id, options, "Id", "id");
            MapProperty(entity, row => row.MonitorId, options, "MonitorId", "monitor_id");
            MapProperty(entity, row => row.SerialId, options, "SerialId", "serial_id");
            MapProperty(entity, row => row.AlertField, options, "AlertField", "alert_field");
            MapProperty(entity, row => row.LimitOn, options, "LimitOn", "limit_on");
            MapProperty(entity, row => row.LimitOff, options, "LimitOff", "limit_off");
            MapProperty(entity, row => row.AlertType, options, "AlertType", "alert_type");
            MapProperty(entity, row => row.IsActive, options, "IsActive", "is_active");
            MapProperty(entity, row => row.AveragingPeriod, options, "AveragingPeriod", "averaging_period");
            MapProperty(entity, row => row.Weekdays, options, "Weekdays", "weekdays");
            MapProperty(entity, row => row.Saturdays, options, "Saturdays", "saturdays");
            MapProperty(entity, row => row.Sundays, options, "Sundays", "sundays");
            MapProperty(entity, row => row.StartTime, options, "StartTime", "start_time");
            MapProperty(entity, row => row.EndTime, options, "EndTime", "end_time");
            MapProperty(entity, row => row.IsDeleted, options, "IsDeleted", "is_deleted");
            MapProperty(entity, row => row.Created, options, "Created", "created");
            MapProperty(entity, row => row.Accessed, options, "Accessed", "accessed");
        });

        modelBuilder.Entity<NotificationEntity>(entity =>
        {
            entity.ToTable(TableName(options, "Notifications", "notification"), Schema(options));
            entity.HasKey(row => row.Id);
            MapProperty(entity, row => row.Id, options, "Id", "id");
            MapProperty(entity, row => row.NotificationTime, options, "NotificationTime", "notification_time");
            MapProperty(entity, row => row.LimitOn, options, "LimitOn", "limit_on");
            MapProperty(entity, row => row.AveragingPeriod, options, "AveragingPeriod", "averaging_period");
            MapProperty(entity, row => row.Level, options, "Level", "level");
            MapProperty(entity, row => row.ClosedTime, options, "ClosedTime", "closed_time");
            MapProperty(entity, row => row.ClosedByUser, options, "ClosedByUser", "closed_by_user");
            MapProperty(entity, row => row.ClosedByNote, options, "ClosedByNote", "closed_by_note");
            MapProperty(entity, row => row.MonitorId, options, "MonitorId", "monitor_id");
            MapProperty(entity, row => row.AlertField, options, "AlertField", "alert_field");
            MapProperty(entity, row => row.AlertType, options, "AlertType", "alert_type");
        });

        modelBuilder.Entity<MonitorDeliveryOutboxEntity>(entity =>
        {
            entity.ToTable(TableName(options, "MonitorDeliveryOutbox", "monitor_delivery_outbox"), Schema(options));
            entity.HasKey(row => row.Id);
            entity.HasOne<NotificationEntity>()
                .WithMany()
                .HasForeignKey(row => row.NotificationId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(row => new { row.Producer, row.DeliveryKey }).IsUnique();
            entity.HasIndex(row => new { row.Producer, row.Status, row.NextAttemptAt });
            MapProperty(entity, row => row.Id, options, "Id", "id");
            MapProperty(entity, row => row.Producer, options, "Producer", "producer");
            MapProperty(entity, row => row.NotificationId, options, "NotificationId", "notification_id");
            MapProperty(entity, row => row.CorrelationKey, options, "CorrelationKey", "correlation_key");
            MapProperty(entity, row => row.DeliveryKey, options, "DeliveryKey", "delivery_key");
            entity.Property(row => row.Kind)
                .HasConversion<string>()
                .HasColumnName(options.IsPostgreSql ? "kind" : "Kind");
            MapProperty(entity, row => row.Destination, options, "Destination", "destination");
            MapProperty(entity, row => row.PayloadVersion, options, "PayloadVersion", "payload_version");
            MapProperty(entity, row => row.Payload, options, "Payload", "payload");
            MapProperty(entity, row => row.Status, options, "Status", "status");
            MapProperty(entity, row => row.AttemptCount, options, "AttemptCount", "attempt_count");
            MapProperty(entity, row => row.NextAttemptAt, options, "NextAttemptAt", "next_attempt_at");
            MapProperty(entity, row => row.LeaseId, options, "LeaseId", "lease_id");
            MapProperty(entity, row => row.LeaseUntil, options, "LeaseUntil", "lease_until");
            MapProperty(entity, row => row.CompletedAt, options, "CompletedAt", "completed_at");
            MapProperty(entity, row => row.DeadLetteredAt, options, "DeadLetteredAt", "dead_lettered_at");
            MapProperty(entity, row => row.LastError, options, "LastError", "last_error");
            MapProperty(entity, row => row.CreatedAt, options, "CreatedAt", "created_at");

            if (!options.IsPostgreSql)
            {
                entity.Property(row => row.Producer).HasMaxLength(64);
                entity.Property(row => row.CorrelationKey).HasMaxLength(450);
                entity.Property(row => row.DeliveryKey)
                    .HasMaxLength(450)
                    .UseCollation("Latin1_General_100_BIN2");
                entity.Property(row => row.Kind).HasMaxLength(64);
                entity.Property(row => row.Destination).HasMaxLength(512);
                entity.Property(row => row.Status).HasMaxLength(32);
                entity.Property(row => row.LastError).HasMaxLength(1024);
            }
        });

        modelBuilder.Entity<NotificationSentEntity>(entity =>
        {
            entity.ToTable(TableName(options, "NotificationsSent", "notification_sent"), Schema(options));
            entity.HasKey(row => row.Id);
            MapProperty(entity, row => row.Id, options, "Id", "id");
            MapProperty(entity, row => row.SendTime, options, "SendTime", "send_time");
            MapProperty(entity, row => row.Address, options, "Address", "address");
            MapProperty(entity, row => row.ErrorMessage, options, "ErrorMessage", "error_message");
            MapProperty(entity, row => row.NotificationId, options, "NotificationId", "notification_id");
        });

        modelBuilder.Entity<NotificationSettingEntity>(entity =>
        {
            entity.ToTable(TableName(options, "NotificationSettings", "notification_setting"), Schema(options));
            entity.HasKey(row => row.Id);
            MapProperty(entity, row => row.Id, options, "Id", "id");
            MapProperty(entity, row => row.Email, options, "Email", "email");
            MapProperty(entity, row => row.SMS, options, "SMS", "sms");
            MapProperty(entity, row => row.StartTime, options, "StartTime", "start_time");
            MapProperty(entity, row => row.EndTime, options, "EndTime", "end_time");
            MapProperty(entity, row => row.SiteUserId, options, "SiteUserId", "site_user_id");
        });

        modelBuilder.Entity<AspNetUserEntity>(entity =>
        {
            entity.ToTable(TableName(options, "AspNetUsers", "AspNetUsers"), Schema(options));
            entity.HasKey(row => row.Id);
            MapIdentityProperty(entity, row => row.Id, options, "Id");
            MapProperty(entity, row => row.CompanyId, options, "CompanyId", "company_id");
            MapProperty(entity, row => row.IsDisabled, options, "IsDisabled", "is_disabled");
            MapProperty(entity, row => row.Name, options, "Name", "name");
            MapIdentityProperty(entity, row => row.UserName, options, "UserName");
            MapProperty(entity, row => row.NormalizedUserName, options, "NormalizedUserName", "normalized_user_name");
            MapIdentityProperty(entity, row => row.Email, options, "Email");
            MapProperty(entity, row => row.NormalizedEmail, options, "NormalizedEmail", "normalized_email");
            MapProperty(entity, row => row.EmailConfirmed, options, "EmailConfirmed", "email_confirmed");
            MapProperty(entity, row => row.PasswordHash, options, "PasswordHash", "password_hash");
            MapProperty(entity, row => row.SecurityStamp, options, "SecurityStamp", "security_stamp");
            MapProperty(entity, row => row.ConcurrencyStamp, options, "ConcurrencyStamp", "concurrency_stamp");
            MapIdentityProperty(entity, row => row.PhoneNumber, options, "PhoneNumber");
            MapProperty(entity, row => row.PhoneNumberConfirmed, options, "PhoneNumberConfirmed", "phone_number_confirmed");
            MapProperty(entity, row => row.TwoFactorEnabled, options, "TwoFactorEnabled", "two_factor_enabled");
            MapProperty(entity, row => row.LockoutEnd, options, "LockoutEnd", "lockout_end");
            MapProperty(entity, row => row.LockoutEnabled, options, "LockoutEnabled", "lockout_enabled");
            MapProperty(entity, row => row.AccessFailedCount, options, "AccessFailedCount", "access_failed_count");
            MapProperty(entity, row => row.CompanyRole, options, "CompanyRole", "company_role");
        });

        modelBuilder.Entity<SiteUserEntity>(entity =>
        {
            entity.ToTable(TableName(options, "SiteUsers", "site_user"), Schema(options));
            entity.HasKey(row => row.Id);
            MapProperty(entity, row => row.Id, options, "Id", "id");
            MapProperty(entity, row => row.StartDate, options, "StartDate", "start_date");
            MapProperty(entity, row => row.EndDate, options, "EndDate", "end_date");
            MapProperty(entity, row => row.UserId, options, "UserId", "user_id");
            MapProperty(entity, row => row.SiteId, options, "SiteId", "site_id");
        });

        modelBuilder.Entity<SiteAverageEntity>(entity =>
        {
            entity.ToTable(TableName(options, "SiteAverages", "site_average"), Schema(options));
            entity.HasKey(row => row.Id);
            MapProperty(entity, row => row.Id, options, "Id", "id");
            MapProperty(entity, row => row.SiteId, options, "SiteId", "site_id");
            MapProperty(entity, row => row.MonitorId, options, "MonitorId", "monitor_id");
            MapProperty(entity, row => row.Field, options, "Field", "field");
            MapProperty(entity, row => row.Level, options, "Level", "level");
            MapProperty(entity, row => row.CollectionTime, options, "CollectionTime", "collection_time");
        });

        modelBuilder.Entity<ErrorMessageEntity>(entity =>
        {
            entity.ToTable(options.IsPostgreSql ? "error_log" : "ErrorMessages", Schema(options));
            entity.HasNoKey();
            MapProperty(entity, row => row.Host, options, "Host", "host");
            MapProperty(entity, row => row.Source, options, "Source", "source");
            MapProperty(entity, row => row.Message, options, "Message", "message");
            MapProperty(entity, row => row.Level, options, "Level", "level");
            MapProperty(entity, row => row.StackTrace, options, "StackTrace", "stack_trace");
            MapProperty(entity, row => row.Variables, options, "Variables", "variables");
            MapProperty(entity, row => row.LogTime, options, "LogTime", "logged_at");
        });

        modelBuilder.Entity<AlertOccurrenceEntity>(entity =>
        {
            var tableName = TableName(options, "AlertOccurrences", "alert_occurrence");
            entity.ToTable(
                tableName,
                Schema(options),
                table =>
                {
                    table.HasCheckConstraint(
                        options.IsPostgreSql ? "ck_alert_occurrence_source_key_hash" : "CK_AlertOccurrences_SourceKeyHash",
                        options.IsPostgreSql
                            ? "octet_length(\"source_key_hash\") = 32"
                            : "DATALENGTH([SourceKeyHash]) = 32");
                    table.HasCheckConstraint(
                        options.IsPostgreSql ? "ck_alert_occurrence_outcome" : "CK_AlertOccurrences_Outcome",
                        ExactStringCheck(
                            options,
                            "Outcome",
                            "outcome",
                            nameof(AlertOccurrenceOutcome.Accepted),
                            nameof(AlertOccurrenceOutcome.Ignored),
                            nameof(AlertOccurrenceOutcome.Suppressed)));
                });
            entity.HasKey(row => row.Id);
            ConfigureGuid(entity.Property(row => row.Id), options, "Id", "id");
            ConfigureString(entity.Property(row => row.Source), options, "Source", "source", 128);
            entity.Property(row => row.SourceKeyHash)
                .HasColumnName(options.IsPostgreSql ? "source_key_hash" : "SourceKeyHash")
                .HasColumnType(options.IsPostgreSql ? "bytea" : "binary(32)")
                .HasMaxLength(32);
            ConfigureGuid(entity.Property(row => row.NotificationId), options, "NotificationId", "notification_id");
            ConfigureGuid(entity.Property(row => row.MonitorId), options, "MonitorId", "monitor_id");
            ConfigureString(entity.Property(row => row.SerialId), options, "SerialId", "serial_id", 128);
            ConfigureInstant(entity.Property(row => row.EventTime), options, "EventTime", "event_time");
            ConfigureInt(entity.Property(row => row.AlertType), options, "AlertType", "alert_type");
            ConfigureString(entity.Property(row => row.AlertField), options, "AlertField", "alert_field", 128);
            ConfigureDouble(entity.Property(row => row.Level), options, "Level", "level");
            ConfigureDouble(entity.Property(row => row.LimitOn), options, "LimitOn", "limit_on");
            ConfigureInt(entity.Property(row => row.AveragingPeriod), options, "AveragingPeriod", "averaging_period");
            ConfigureString(entity.Property(row => row.Outcome), options, "Outcome", "outcome", 32);
            ConfigureInstant(entity.Property(row => row.CreatedAt), options, "CreatedAt", "created_at");
            entity.HasIndex(row => new { row.Source, row.SourceKeyHash })
                .IsUnique()
                .HasDatabaseName(options.IsPostgreSql
                    ? "uq_alert_occurrence_source_key"
                    : "UQ_AlertOccurrences_SourceKey");
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
            var tableName = TableName(options, "AlertDeliveryOutbox", "alert_delivery_outbox");
            entity.ToTable(
                tableName,
                Schema(options),
                table =>
                {
                    table.HasCheckConstraint(
                        options.IsPostgreSql ? "ck_alert_delivery_outbox_kind" : "CK_AlertDeliveryOutbox_Kind",
                        ExactStringCheck(options, "Kind", "kind", "MqttAlert", "Email", "Sms"));
                    table.HasCheckConstraint(
                        options.IsPostgreSql ? "ck_alert_delivery_outbox_status" : "CK_AlertDeliveryOutbox_Status",
                        ExactStringCheck(options, "Status", "status", "Pending", "Leased", "Completed", "DeadLetter"));
                    if (!options.IsPostgreSql)
                    {
                        table.HasCheckConstraint(
                            "CK_AlertDeliveryOutbox_PayloadLength",
                            "DATALENGTH([Payload]) <= 16384");
                    }
                });
            entity.HasKey(row => row.Id);
            ConfigureGuid(entity.Property(row => row.Id), options, "Id", "id");
            ConfigureGuid(entity.Property(row => row.OccurrenceId), options, "OccurrenceId", "occurrence_id");
            ConfigureString(entity.Property(row => row.DeliveryKey), options, "DeliveryKey", "delivery_key", 64);
            ConfigureString(entity.Property(row => row.Kind), options, "Kind", "kind", 32);
            ConfigureString(entity.Property(row => row.Destination), options, "Destination", "destination", 512);
            ConfigureString(
                entity.Property(row => row.Payload),
                options,
                "Payload",
                "payload",
                8192,
                sqlServerColumnType: "nvarchar(max)");
            ConfigureString(entity.Property(row => row.Status), options, "Status", "status", 32);
            ConfigureInt(entity.Property(row => row.AttemptCount), options, "AttemptCount", "attempt_count");
            ConfigureInstant(entity.Property(row => row.NextAttemptAt), options, "NextAttemptAt", "next_attempt_at");
            ConfigureGuid(entity.Property(row => row.LeaseId), options, "LeaseId", "lease_id");
            ConfigureInstant(entity.Property(row => row.LeaseUntil), options, "LeaseUntil", "lease_until");
            ConfigureInstant(entity.Property(row => row.CompletedAt), options, "CompletedAt", "completed_at");
            ConfigureString(entity.Property(row => row.LastError), options, "LastError", "last_error", 256);
            ConfigureInstant(entity.Property(row => row.CreatedAt), options, "CreatedAt", "created_at");
            entity.HasIndex(row => row.DeliveryKey)
                .IsUnique()
                .HasDatabaseName(options.IsPostgreSql
                    ? "uq_alert_delivery_outbox_delivery_key"
                    : "UQ_AlertDeliveryOutbox_DeliveryKey");
            entity.HasIndex(row => new { row.Status, row.NextAttemptAt, row.LeaseUntil, row.CreatedAt })
                .HasDatabaseName(options.IsPostgreSql
                    ? "ix_alert_delivery_outbox_due"
                    : "IX_AlertDeliveryOutbox_Due");
            entity.HasOne<AlertOccurrenceEntity>()
                .WithMany()
                .HasForeignKey(row => row.OccurrenceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        return modelBuilder;
    }

    private static string? Schema(MonitorDbOptions options)
    {
        return options.IsPostgreSql ? null : "dbo";
    }

    private static string TableName(MonitorDbOptions options, string sqlServerName, string postgreSqlName)
    {
        if (!options.IsPostgreSql)
        {
            return sqlServerName;
        }

        return options.IdentifierMap.TryGetValue(sqlServerName, out var mapped)
            ? mapped.Trim('"')
            : postgreSqlName;
    }

    private static void MapProperty<TEntity, TProperty>(
        EntityTypeBuilder<TEntity> entity,
        System.Linq.Expressions.Expression<Func<TEntity, TProperty>> propertyExpression,
        MonitorDbOptions options,
        string sqlServerName,
        string postgreSqlName)
        where TEntity : class
    {
        entity.Property(propertyExpression).HasColumnName(options.IsPostgreSql ? postgreSqlName : sqlServerName);
    }

    private static void MapIdentityProperty<TEntity, TProperty>(
        EntityTypeBuilder<TEntity> entity,
        System.Linq.Expressions.Expression<Func<TEntity, TProperty>> propertyExpression,
        MonitorDbOptions options,
        string columnName)
        where TEntity : class
    {
        entity.Property(propertyExpression).HasColumnName(columnName);
    }

    private static void ConfigureGuid<TProperty>(
        PropertyBuilder<TProperty> property,
        MonitorDbOptions options,
        string sqlServerName,
        string postgreSqlName)
    {
        property
            .HasColumnName(options.IsPostgreSql ? postgreSqlName : sqlServerName)
            .HasColumnType(options.IsPostgreSql ? "uuid" : "uniqueidentifier");
    }

    private static void ConfigureString<TProperty>(
        PropertyBuilder<TProperty> property,
        MonitorDbOptions options,
        string sqlServerName,
        string postgreSqlName,
        int maxLength,
        string? sqlServerColumnType = null)
    {
        property
            .HasColumnName(options.IsPostgreSql ? postgreSqlName : sqlServerName)
            .HasColumnType(options.IsPostgreSql ? $"varchar({maxLength})" : sqlServerColumnType ?? $"nvarchar({maxLength})")
            .HasMaxLength(maxLength);
    }

    private static void ConfigureInstant<TProperty>(
        PropertyBuilder<TProperty> property,
        MonitorDbOptions options,
        string sqlServerName,
        string postgreSqlName)
    {
        property
            .HasColumnName(options.IsPostgreSql ? postgreSqlName : sqlServerName)
            .HasColumnType(options.IsPostgreSql ? "timestamp with time zone" : "datetime2");
    }

    private static void ConfigureInt(
        PropertyBuilder<int> property,
        MonitorDbOptions options,
        string sqlServerName,
        string postgreSqlName)
    {
        property
            .HasColumnName(options.IsPostgreSql ? postgreSqlName : sqlServerName)
            .HasColumnType(options.IsPostgreSql ? "integer" : "int");
    }

    private static void ConfigureDouble(
        PropertyBuilder<double> property,
        MonitorDbOptions options,
        string sqlServerName,
        string postgreSqlName)
    {
        property
            .HasColumnName(options.IsPostgreSql ? postgreSqlName : sqlServerName)
            .HasColumnType(options.IsPostgreSql ? "double precision" : "float");
    }

    private static string ExactStringCheck(
        MonitorDbOptions options,
        string sqlServerColumn,
        string postgreSqlColumn,
        params string[] values)
    {
        if (options.IsPostgreSql)
        {
            return $"\"{postgreSqlColumn}\" IN ({string.Join(',', values.Select(value => $"'{value}'"))})";
        }

        return string.Join(
            " OR ",
            values.Select(value =>
                $"([{sqlServerColumn}] COLLATE Latin1_General_100_BIN2 = N'{value}' " +
                $"AND DATALENGTH([{sqlServerColumn}]) = DATALENGTH(N'{value}'))"));
    }
}
