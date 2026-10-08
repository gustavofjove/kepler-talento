using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCatalogItemColor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Color",
                table: "CAT_CatalogItems",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "orange");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CAT_CatalogItems_Color",
                table: "CAT_CatalogItems",
                sql: "\"Color\" IN ('orange', 'yellow', 'green', 'teal', 'blue', 'indigo', 'violet', 'pink', 'grey')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_CAT_CatalogItems_Color",
                table: "CAT_CatalogItems");

            migrationBuilder.DropColumn(
                name: "Color",
                table: "CAT_CatalogItems");
        }
    }
}
