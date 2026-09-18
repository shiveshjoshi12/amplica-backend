using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BizfreeApp.Migrations
{
    /// <inheritdoc />
    public partial class AddFcmTokenToUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "device_id",
                table: "users",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "fcm_token",
                table: "users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "fcm_token_updated_at",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "device_id",
                table: "users");

            migrationBuilder.DropColumn(
                name: "fcm_token",
                table: "users");

            migrationBuilder.DropColumn(
                name: "fcm_token_updated_at",
                table: "users");
        }
    }
}
