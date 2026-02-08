using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hamco.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddApiKeysTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "api_keys",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    key_hash = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    key_prefix = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    is_admin = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    created_by_user_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_api_keys", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_api_keys_created_by_user_id",
                table: "api_keys",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_api_keys_is_active",
                table: "api_keys",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "ix_api_keys_key_prefix",
                table: "api_keys",
                column: "key_prefix");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "api_keys");
        }
    }
}
