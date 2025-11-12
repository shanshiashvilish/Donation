using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Donation.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AdjustProperties : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
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
                name: "IX_subscriptions_ExternalId",
                table: "subscriptions");

            migrationBuilder.AlterColumn<string>(
                name: "Lastname",
                table: "users",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<int>(
                name: "Amount",
                table: "subscriptions",
                type: "integer",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.CreateIndex(
                name: "IX_subscriptions_ExternalId",
                table: "subscriptions",
                column: "ExternalId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_payments_subscriptions_SubscriptionId",
                table: "payments",
                column: "SubscriptionId",
                principalTable: "subscriptions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_payments_users_UserId",
                table: "payments",
                column: "UserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_subscriptions_users_UserId",
                table: "subscriptions",
                column: "UserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
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
                name: "IX_subscriptions_ExternalId",
                table: "subscriptions");

            migrationBuilder.AlterColumn<string>(
                name: "Lastname",
                table: "users",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<decimal>(
                name: "Amount",
                table: "subscriptions",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.CreateIndex(
                name: "IX_subscriptions_ExternalId",
                table: "subscriptions",
                column: "ExternalId");

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
    }
}
