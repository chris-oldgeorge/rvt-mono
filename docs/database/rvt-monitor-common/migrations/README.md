# Shared Monitor Migrations

The PostgreSQL migration `2026-07-15-add-monitor-delivery-outbox.postgres.sql` is the canonical shared outbox migration.

It is idempotent and creates `monitor_delivery_outbox` with the logical primary key, unique producer/delivery key, four delivery statuses, due-work index, lease state, completion state, dead-letter state, and payload fields. It references `notification` through a nullable foreign key with `ON DELETE SET NULL` and does not alter `notification` or `notification_sent`.
