SELECT 'notification_logs' AS entity, count(*) AS total FROM notification.notification_logs
UNION ALL SELECT 'sms_notification_logs', count(*) FROM notification.sms_notification_logs
UNION ALL SELECT 'user_push_tokens', count(*) FROM notification.user_push_tokens
UNION ALL SELECT 'emergency_location_access_links', count(*) FROM notification.emergency_location_access_links;
SELECT notification_type, notification_status, count(*) FROM notification.notification_logs GROUP BY notification_type, notification_status ORDER BY notification_type, notification_status;
SELECT notification_status, count(*) FROM notification.sms_notification_logs GROUP BY notification_status ORDER BY notification_status;
SELECT is_active, count(*) FROM notification.user_push_tokens GROUP BY is_active;
SELECT count(*) FILTER(WHERE revoked_at IS NULL AND expires_at > now()) AS active_links, min(expires_at), max(expires_at) FROM notification.emergency_location_access_links;
DO $$
DECLARE receipt_count bigint;
BEGIN
    IF to_regclass('notification.inbox_messages') IS NOT NULL THEN
        EXECUTE 'SELECT count(*) FROM notification.inbox_messages WHERE type = ''legacy.completed''' INTO receipt_count;
    ELSE
        EXECUTE 'SELECT count(*) FROM shared.integration_receipts WHERE event_id LIKE ''emergency-analytics.%'' OR event_id LIKE ''device-managment:%'' OR event_id LIKE ''care.invitation:%''' INTO receipt_count;
    END IF;
    RAISE NOTICE 'Legacy completed receipts to compare: %', receipt_count;
END $$;
