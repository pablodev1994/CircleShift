using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CircleShift.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MemberBlocks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "member_blocks",
                columns: table => new
                {
                    blocker_id = table.Column<Guid>(type: "uuid", nullable: false),
                    blocked_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_member_blocks", x => new { x.blocker_id, x.blocked_id });
                    table.CheckConstraint("ck_member_blocks_not_self", "blocker_id <> blocked_id");
                    table.ForeignKey(
                        name: "fk_member_blocks_blocked",
                        column: x => x.blocked_id,
                        principalTable: "members",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_member_blocks_blocker",
                        column: x => x.blocker_id,
                        principalTable: "members",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_member_blocks_blocked_id",
                table: "member_blocks",
                column: "blocked_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "member_blocks");
        }
    }
}
