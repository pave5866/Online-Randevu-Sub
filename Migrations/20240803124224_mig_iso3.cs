using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Randevu.Migrations
{
    /// <inheritdoc />
    public partial class mig_iso3 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ServiceID",
                table: "Appointments",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ServiceID",
                table: "Appointments");
        }
    }
}
