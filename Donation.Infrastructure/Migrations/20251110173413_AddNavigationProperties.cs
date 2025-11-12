using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Donation.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddNavigationProperties : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "payments",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_payments_UserId",
                table: "payments",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_payments_subscriptions_SubscriptionId",
                table: "payments",
                column: "SubscriptionId",
                principalTable: "subscriptions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_payments_users_UserId",
                table: "payments",
                column: "UserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_subscriptions_users_UserId",
                table: "subscriptions",
                column: "UserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_payments_subscriptions_SubscriptionId",
                table: "payments");

            migrationBuilder.DropForeignKey(
                name: "FK_payments_users_UserId",
                table: "payments");

            migrationBuilder.DropForeignKey(
                name: "FK_subscriptions_users_UserId",
                table: "subscriptions");

            migrationBuilder.DropIndex(
                name: "IX_payments_UserId",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "payments");
        }
    }
}
