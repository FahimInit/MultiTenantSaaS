using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SaaS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBusinessModules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "subscription_plans",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    monthly_price_cents = table.Column<long>(type: "bigint", nullable: false),
                    annual_price_cents = table.Column<long>(type: "bigint", nullable: false),
                    trial_days = table.Column<int>(type: "integer", nullable: false),
                    max_members = table.Column<int>(type: "integer", nullable: true),
                    max_custom_roles = table.Column<int>(type: "integer", nullable: true),
                    max_monthly_api_calls = table.Column<int>(type: "integer", nullable: true),
                    audit_retention_days = table.Column<int>(type: "integer", nullable: false),
                    allow_custom_roles = table.Column<bool>(type: "boolean", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    is_public = table.Column<bool>(type: "boolean", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_subscription_plans", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tenant_invitations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    normalized_email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    role_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    invited_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    expires_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    accepted_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    accepted_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    revoked_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tenant_invitations", x => x.id);
                    table.ForeignKey(
                        name: "fk_tenant_invitations_tenant_roles_tenant_id_role_id",
                        columns: x => new { x.tenant_id, x.role_id },
                        principalTable: "tenant_roles",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_tenant_invitations_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_tenant_invitations_users_accepted_by_user_id",
                        column: x => x.accepted_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_tenant_invitations_users_invited_by_user_id",
                        column: x => x.invited_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tenant_usage_snapshots",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    period_start_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    period_end_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    active_members_count = table.Column<int>(type: "integer", nullable: false),
                    custom_roles_count = table.Column<int>(type: "integer", nullable: false),
                    api_calls_count = table.Column<long>(type: "bigint", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tenant_usage_snapshots", x => x.id);
                    table.ForeignKey(
                        name: "fk_tenant_usage_snapshots_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tenant_subscriptions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    billing_cycle = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    current_period_start_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    current_period_end_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    trial_ends_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    canceled_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    auto_renew = table.Column<bool>(type: "boolean", nullable: false),
                    external_provider_subscription_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tenant_subscriptions", x => x.id);
                    table.ForeignKey(
                        name: "fk_tenant_subscriptions_subscription_plans_plan_id",
                        column: x => x.plan_id,
                        principalTable: "subscription_plans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_tenant_subscriptions_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "subscription_history",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    subscription_id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_plan_id = table.Column<Guid>(type: "uuid", nullable: true),
                    to_plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    amount_cents = table.Column<long>(type: "bigint", nullable: false),
                    currency = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    payment_reference = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    actor_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    occurred_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_subscription_history", x => x.id);
                    table.ForeignKey(
                        name: "fk_subscription_history_subscription_plans_from_plan_id",
                        column: x => x.from_plan_id,
                        principalTable: "subscription_plans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_subscription_history_subscription_plans_to_plan_id",
                        column: x => x.to_plan_id,
                        principalTable: "subscription_plans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_subscription_history_tenant_subscriptions_subscription_id",
                        column: x => x.subscription_id,
                        principalTable: "tenant_subscriptions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_subscription_history_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_subscription_history_from_plan_id",
                table: "subscription_history",
                column: "from_plan_id");

            migrationBuilder.CreateIndex(
                name: "ix_subscription_history_subscription_id",
                table: "subscription_history",
                column: "subscription_id");

            migrationBuilder.CreateIndex(
                name: "ix_subscription_history_tenant_id_occurred_at_utc",
                table: "subscription_history",
                columns: new[] { "tenant_id", "occurred_at_utc" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_subscription_history_to_plan_id",
                table: "subscription_history",
                column: "to_plan_id");

            migrationBuilder.CreateIndex(
                name: "ix_subscription_plans_code",
                table: "subscription_plans",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_subscription_plans_is_public_is_active_sort_order",
                table: "subscription_plans",
                columns: new[] { "is_public", "is_active", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "ix_tenant_invitations_accepted_by_user_id",
                table: "tenant_invitations",
                column: "accepted_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_tenant_invitations_invited_by_user_id",
                table: "tenant_invitations",
                column: "invited_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_tenant_invitations_status_expires_at_utc",
                table: "tenant_invitations",
                columns: new[] { "status", "expires_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_tenant_invitations_tenant_id_created_at_utc",
                table: "tenant_invitations",
                columns: new[] { "tenant_id", "created_at_utc" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_tenant_invitations_tenant_id_normalized_email",
                table: "tenant_invitations",
                columns: new[] { "tenant_id", "normalized_email" },
                unique: true,
                filter: "status = 'Pending'");

            migrationBuilder.CreateIndex(
                name: "ix_tenant_invitations_tenant_id_role_id",
                table: "tenant_invitations",
                columns: new[] { "tenant_id", "role_id" });

            migrationBuilder.CreateIndex(
                name: "ix_tenant_invitations_token_hash",
                table: "tenant_invitations",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tenant_subscriptions_plan_id",
                table: "tenant_subscriptions",
                column: "plan_id");

            migrationBuilder.CreateIndex(
                name: "ix_tenant_subscriptions_status_trial_ends_at_utc",
                table: "tenant_subscriptions",
                columns: new[] { "status", "trial_ends_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_tenant_subscriptions_tenant_id",
                table: "tenant_subscriptions",
                column: "tenant_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tenant_usage_snapshots_tenant_id_period_start_utc",
                table: "tenant_usage_snapshots",
                columns: new[] { "tenant_id", "period_start_utc" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "subscription_history");

            migrationBuilder.DropTable(
                name: "tenant_invitations");

            migrationBuilder.DropTable(
                name: "tenant_usage_snapshots");

            migrationBuilder.DropTable(
                name: "tenant_subscriptions");

            migrationBuilder.DropTable(
                name: "subscription_plans");
        }
    }
}
