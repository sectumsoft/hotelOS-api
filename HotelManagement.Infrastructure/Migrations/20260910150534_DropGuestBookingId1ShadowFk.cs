using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HotelManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DropGuestBookingId1ShadowFk : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Guests_Bookings_BookingId1",
                table: "Guests");

            migrationBuilder.DropIndex(
                name: "IX_Guests_BookingId1",
                table: "Guests");

            migrationBuilder.DropColumn(
                name: "BookingId1",
                table: "Guests");

            migrationBuilder.AddForeignKey(
                name: "FK_Guests_Bookings_BookingId",
                table: "Guests",
                column: "BookingId",
                principalTable: "Bookings",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Guests_Bookings_BookingId",
                table: "Guests");

            migrationBuilder.AddColumn<Guid>(
                name: "BookingId1",
                table: "Guests",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Guests_BookingId1",
                table: "Guests",
                column: "BookingId1");

            migrationBuilder.AddForeignKey(
                name: "FK_Guests_Bookings_BookingId1",
                table: "Guests",
                column: "BookingId1",
                principalTable: "Bookings",
                principalColumn: "Id");
        }
    }
}
