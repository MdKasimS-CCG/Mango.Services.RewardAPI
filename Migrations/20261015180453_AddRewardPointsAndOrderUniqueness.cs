using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mango.Services.RewardAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddRewardPointsAndOrderUniqueness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "IF EXISTS (SELECT 1 FROM [Rewards] GROUP BY [OrderId] HAVING COUNT_BIG(*) > 1) " +
                "BEGIN THROW 51000, 'Cannot add unique reward OrderId index: duplicate OrderId values already exist.', 1; END;");

            migrationBuilder.AddColumn<int>(
                name: "PointsEarned",
                table: "Rewards",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Rewards_OrderId",
                table: "Rewards",
                column: "OrderId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Rewards_OrderId",
                table: "Rewards");

            migrationBuilder.DropColumn(
                name: "PointsEarned",
                table: "Rewards");
        }
    }
}
