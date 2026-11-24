using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LisbergThreads.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderConfirmationToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ConfirmationToken",
                table: "Orders",
                type: "uniqueidentifier",
                nullable: false,
                defaultValueSql: "NEWID()");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_ConfirmationToken",
                table: "Orders",
                column: "ConfirmationToken",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Orders_ConfirmationToken",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ConfirmationToken",
                table: "Orders");
        }
    }
}
