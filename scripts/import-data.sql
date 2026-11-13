\set ON_ERROR_STOP on
-- Must be run with psql --single-transaction on a freshly migrated destination.
DO $$ BEGIN
    IF EXISTS(SELECT 1 FROM notification.notification_logs) OR EXISTS(SELECT 1 FROM notification.sms_notification_logs)
       OR EXISTS(SELECT 1 FROM notification.user_push_tokens) OR EXISTS(SELECT 1 FROM notification.emergency_location_access_links)
       OR EXISTS(SELECT 1 FROM notification.inbox_messages) THEN
        RAISE EXCEPTION 'Import rejected: destination already contains data';
    END IF;
END $$;
\copy notification.notification_logs (notification_log_id, user_id, notification_type, notification_channel, notification_status, title, body, data_json, provider_message_id, error_message, device_event_id, patient_id, device_id, sent_at, read_at, acknowledged_at, created_at, updated_at) FROM 'notification_logs.csv' WITH (FORMAT csv, HEADER true, ENCODING 'UTF8')
\copy notification.sms_notification_logs (sms_notification_log_id, incident_key, user_id, emergency_contact_id, patient_id, device_id, recipient_name, phone_number, notification_type, notification_status, message, location_access_url, provider_message_id, error_message, sent_at, created_at, updated_at) FROM 'sms_notification_logs.csv' WITH (FORMAT csv, HEADER true, ENCODING 'UTF8')
\copy notification.user_push_tokens (user_push_token_id, user_id, token, platform, device_name, is_active, last_used_at, created_at, updated_at) FROM 'user_push_tokens.csv' WITH (FORMAT csv, HEADER true, ENCODING 'UTF8')
\copy notification.emergency_location_access_links (emergency_location_access_link_id, incident_key, patient_id, device_id, token_hash, expires_at, created_at, last_accessed_at, revoked_at) FROM 'emergency_location_access_links.csv' WITH (FORMAT csv, HEADER true, ENCODING 'UTF8')
CREATE TEMP TABLE import_receipts (event_id varchar(200) PRIMARY KEY, received_at timestamptz NOT NULL);
\copy import_receipts (event_id, received_at) FROM 'integration_receipts.csv' WITH (FORMAT csv, HEADER true, ENCODING 'UTF8')
DO $$ BEGIN
    IF EXISTS(SELECT 1 FROM import_receipts WHERE event_id NOT LIKE 'emergency-analytics.%' AND event_id NOT LIKE 'device-managment:%' AND event_id NOT LIKE 'care.invitation:%') THEN
        RAISE EXCEPTION 'Unexpected receipt type';
    END IF;
END $$;
INSERT INTO notification.inbox_messages (event_id, stream_key, type, payload, received_at, processed_at, next_attempt_at, attempts, state)
SELECT event_id, 'legacy.completed', 'legacy.completed', '{}'::jsonb, received_at, received_at, received_at, 0, 'Processed' FROM import_receipts;
SELECT setval(pg_get_serial_sequence('notification.notification_logs','notification_log_id'), COALESCE(MAX(notification_log_id),1), COUNT(*)>0) FROM notification.notification_logs;
SELECT setval(pg_get_serial_sequence('notification.sms_notification_logs','sms_notification_log_id'), COALESCE(MAX(sms_notification_log_id),1), COUNT(*)>0) FROM notification.sms_notification_logs;
SELECT setval(pg_get_serial_sequence('notification.user_push_tokens','user_push_token_id'), COALESCE(MAX(user_push_token_id),1), COUNT(*)>0) FROM notification.user_push_tokens;
SELECT setval(pg_get_serial_sequence('notification.emergency_location_access_links','emergency_location_access_link_id'), COALESCE(MAX(emergency_location_access_link_id),1), COUNT(*)>0) FROM notification.emergency_location_access_links;
