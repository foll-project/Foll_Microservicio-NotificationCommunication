using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace foll_notifications.Migrations
{
    /// <inheritdoc />
    public partial class InitialNotificationCommunication : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "notification");

            migrationBuilder.CreateTable(
                name: "emergency_location_access_links",
                schema: "notification",
                columns: table => new
                {
                    emergency_location_access_link_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    incident_key = table.Column<Guid>(type: "uuid", nullable: false),
                    patient_id = table.Column<long>(type: "bigint", nullable: false),
                    device_id = table.Column<long>(type: "bigint", nullable: true),
                    token_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    last_accessed_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    revoked_at = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_emergency_location_access_links", x => x.emergency_location_access_link_id);
                });

            migrationBuilder.CreateTable(
                name: "inbox_messages",
                schema: "notification",
                columns: table => new
                {
                    event_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    sequence = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    stream_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    type = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    payload = table.Column<string>(type: "jsonb", nullable: false),
                    outbox_message_id = table.Column<long>(type: "bigint", nullable: true),
                    received_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    processed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    next_attempt_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    state = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    last_error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_inbox_messages", x => x.event_id);
                });

            migrationBuilder.CreateTable(
                name: "notification_logs",
                schema: "notification",
                columns: table => new
                {
                    notification_log_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    notification_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    notification_channel = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    notification_status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    body = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    data_json = table.Column<string>(type: "jsonb", nullable: true),
                    provider_message_id = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    error_message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    device_event_id = table.Column<long>(type: "bigint", nullable: true),
                    patient_id = table.Column<long>(type: "bigint", nullable: true),
                    device_id = table.Column<long>(type: "bigint", nullable: true),
                    sent_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    read_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    acknowledged_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notification_logs", x => x.notification_log_id);
                });

            migrationBuilder.CreateTable(
                name: "sms_notification_logs",
                schema: "notification",
                columns: table => new
                {
                    sms_notification_log_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    incident_key = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<long>(type: "bigint", nullable: true),
                    emergency_contact_id = table.Column<long>(type: "bigint", nullable: true),
                    patient_id = table.Column<long>(type: "bigint", nullable: false),
                    device_id = table.Column<long>(type: "bigint", nullable: true),
                    recipient_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    phone_number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    notification_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    notification_status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    message = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    location_access_url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    provider_message_id = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    error_message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    sent_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sms_notification_logs", x => x.sms_notification_log_id);
                });

            migrationBuilder.CreateTable(
                name: "user_push_tokens",
                schema: "notification",
                columns: table => new
                {
                    user_push_token_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    token = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    platform = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    device_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    last_used_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_push_tokens", x => x.user_push_token_id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_emergency_location_access_links_expires_at",
                schema: "notification",
                table: "emergency_location_access_links",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "ix_emergency_location_access_links_incident_key",
                schema: "notification",
                table: "emergency_location_access_links",
                column: "incident_key");

            migrationBuilder.CreateIndex(
                name: "ix_emergency_location_access_links_token_hash",
                schema: "notification",
                table: "emergency_location_access_links",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_inbox_messages_processed_at_next_attempt_at",
                schema: "notification",
                table: "inbox_messages",
                columns: new[] { "processed_at", "next_attempt_at" });

            migrationBuilder.CreateIndex(
                name: "ix_inbox_messages_sequence",
                schema: "notification",
                table: "inbox_messages",
                column: "sequence",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_inbox_messages_stream_key_sequence",
                schema: "notification",
                table: "inbox_messages",
                columns: new[] { "stream_key", "sequence" });

            migrationBuilder.CreateIndex(
                name: "ix_notification_logs_created_at",
                schema: "notification",
                table: "notification_logs",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_notification_logs_device_id",
                schema: "notification",
                table: "notification_logs",
                column: "device_id");

            migrationBuilder.CreateIndex(
                name: "ix_notification_logs_notification_status",
                schema: "notification",
                table: "notification_logs",
                column: "notification_status");

            migrationBuilder.CreateIndex(
                name: "ix_notification_logs_patient_id",
                schema: "notification",
                table: "notification_logs",
                column: "patient_id");

            migrationBuilder.CreateIndex(
                name: "ix_notification_logs_user_id",
                schema: "notification",
                table: "notification_logs",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_sms_notification_logs_emergency_contact_id",
                schema: "notification",
                table: "sms_notification_logs",
                column: "emergency_contact_id");

            migrationBuilder.CreateIndex(
                name: "ix_sms_notification_logs_incident_key",
                schema: "notification",
                table: "sms_notification_logs",
                column: "incident_key");

            migrationBuilder.CreateIndex(
                name: "ix_sms_notification_logs_incident_key_phone_number",
                schema: "notification",
                table: "sms_notification_logs",
                columns: new[] { "incident_key", "phone_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sms_notification_logs_notification_status",
                schema: "notification",
                table: "sms_notification_logs",
                column: "notification_status");

            migrationBuilder.CreateIndex(
                name: "ix_sms_notification_logs_patient_id",
                schema: "notification",
                table: "sms_notification_logs",
                column: "patient_id");

            migrationBuilder.CreateIndex(
                name: "ix_sms_notification_logs_user_id",
                schema: "notification",
                table: "sms_notification_logs",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_push_tokens_user_id",
                schema: "notification",
                table: "user_push_tokens",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_push_tokens_user_id_token",
                schema: "notification",
                table: "user_push_tokens",
                columns: new[] { "user_id", "token" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "emergency_location_access_links",
                schema: "notification");

            migrationBuilder.DropTable(
                name: "inbox_messages",
                schema: "notification");

            migrationBuilder.DropTable(
                name: "notification_logs",
                schema: "notification");

            migrationBuilder.DropTable(
                name: "sms_notification_logs",
                schema: "notification");

            migrationBuilder.DropTable(
                name: "user_push_tokens",
                schema: "notification");
        }
    }
}
