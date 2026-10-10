using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SkyJourney.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentIdempotencyAndClaim : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "request_hash",
                table: "payment_attempts",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "last_payment_started_at",
                table: "bookings",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "request_hash",
                table: "payment_attempts");

            migrationBuilder.DropColumn(
                name: "last_payment_started_at",
                table: "bookings");
        }
    }
}
