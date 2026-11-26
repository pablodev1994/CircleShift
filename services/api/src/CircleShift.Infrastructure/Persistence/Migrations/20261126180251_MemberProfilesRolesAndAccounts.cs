using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CircleShift.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MemberProfilesRolesAndAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "area_label",
                table: "members",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "bio",
                table: "members",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<List<string>>(
                name: "languages",
                table: "members",
                type: "text[]",
                nullable: false,
                defaultValueSql: "'{}'::text[]");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "updated_at",
                table: "members",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<string>(
                name: "visibility",
                table: "members",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "hidden");

            // Existing members keep their creation time as the last profile update,
            // and stay hidden: profiles are opt-in.
            migrationBuilder.Sql("UPDATE members SET updated_at = created_at;");
            migrationBuilder.Sql("ALTER TABLE members ALTER COLUMN updated_at DROP DEFAULT;");

            migrationBuilder.CreateTable(
                name: "member_accounts",
                columns: table => new
                {
                    member_id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    password_hash = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    failed_sign_in_count = table.Column<int>(type: "integer", nullable: false),
                    locked_until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_signed_in_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_member_accounts", x => x.member_id);
                    table.CheckConstraint("ck_member_accounts_email_lower", "email = lower(email)");
                    table.CheckConstraint("ck_member_accounts_failed_sign_ins", "failed_sign_in_count >= 0");
                    table.ForeignKey(
                        name: "fk_member_accounts_members",
                        column: x => x.member_id,
                        principalTable: "members",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "role_grants",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    member_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    requested_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    verified_by = table.Column<Guid>(type: "uuid", nullable: true),
                    verified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    revoked_by = table.Column<Guid>(type: "uuid", nullable: true),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_role_grants", x => x.id);
                    table.CheckConstraint("ck_role_grants_not_self_verified", "verified_by IS NULL OR verified_by <> member_id");
                    table.CheckConstraint("ck_role_grants_revoked_pair", "(revoked_by IS NULL OR revoked_at IS NOT NULL)");
                    table.CheckConstraint("ck_role_grants_role", "role IN ('coordinator', 'moderator', 'admin')");
                    table.CheckConstraint("ck_role_grants_verified_pair", "verified_by IS NULL OR verified_at IS NOT NULL");
                    table.ForeignKey(
                        name: "fk_role_grants_members",
                        column: x => x.member_id,
                        principalTable: "members",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_role_grants_revoked_by",
                        column: x => x.revoked_by,
                        principalTable: "members",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_role_grants_verified_by",
                        column: x => x.verified_by,
                        principalTable: "members",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_members_visibility",
                table: "members",
                column: "visibility");

            migrationBuilder.AddCheckConstraint(
                name: "ck_members_bio_length",
                table: "members",
                sql: "char_length(bio) <= 500");

            migrationBuilder.AddCheckConstraint(
                name: "ck_members_languages_count",
                table: "members",
                sql: "cardinality(languages) <= 8");

            migrationBuilder.AddCheckConstraint(
                name: "ck_members_visibility",
                table: "members",
                sql: "visibility IN ('hidden', 'members', 'public')");

            migrationBuilder.CreateIndex(
                name: "ux_member_accounts_email",
                table: "member_accounts",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_role_grants_revoked_by",
                table: "role_grants",
                column: "revoked_by");

            migrationBuilder.CreateIndex(
                name: "ix_role_grants_verified_by",
                table: "role_grants",
                column: "verified_by");

            migrationBuilder.CreateIndex(
                name: "ux_role_grants_open_member_role",
                table: "role_grants",
                columns: new[] { "member_id", "role" },
                unique: true,
                filter: "revoked_at IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "member_accounts");

            migrationBuilder.DropTable(
                name: "role_grants");

            migrationBuilder.DropIndex(
                name: "ix_members_visibility",
                table: "members");

            migrationBuilder.DropCheckConstraint(
                name: "ck_members_bio_length",
                table: "members");

            migrationBuilder.DropCheckConstraint(
                name: "ck_members_languages_count",
                table: "members");

            migrationBuilder.DropCheckConstraint(
                name: "ck_members_visibility",
                table: "members");

            migrationBuilder.DropColumn(
                name: "area_label",
                table: "members");

            migrationBuilder.DropColumn(
                name: "bio",
                table: "members");

            migrationBuilder.DropColumn(
                name: "languages",
                table: "members");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "members");

            migrationBuilder.DropColumn(
                name: "visibility",
                table: "members");
        }
    }
}
