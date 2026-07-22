# Shared monitor database migrations

Apply the script for the configured database provider before deploying code that uses shared durable delivery:

- PostgreSQL: `2026-07-15-add-monitor-delivery-outbox.postgres.sql`
- SQL Server: `2026-07-15-add-monitor-delivery-outbox.sqlserver.sql`

Each script is idempotent and creates exactly one shared table: `monitor_delivery_outbox` on PostgreSQL or `dbo.MonitorDeliveryOutbox` on SQL Server. The scripts reuse the existing notification table through a nullable foreign key with `ON DELETE SET NULL`; they do not alter `notification`/`Notifications` or `notification_sent`/`NotificationsSent`.

The two providers expose the same logical primary key, unique producer/delivery key, four delivery statuses, due-work index, lease state, completion state, dead-letter state, and payload fields.

The shared migration contract is verified by `MonitorDeliveryMigrationContractTests` and the provider-aware EF mapping tests. No monitor application consumes this table yet. Apply the appropriate provider script before deploying the first monitor migration that writes shared durable deliveries.
