using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EshopGuard.Data.Migrations
{
    /// <summary>
    /// Price for every country (change 7, decision of 2. 10. 2026): a language version has no flag for the price and no share of
    /// its own texts; the free sample stores the share of products with a description in its language and the language of its
    /// descriptions. The old values mean something else, so the columns are new (empty until the next free sample).
    /// </summary>
    public partial class F4LanguagesByCountry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "counted",
                schema: "shop",
                table: "shop_languages");

            migrationBuilder.DropColumn(
                name: "own_text_share",
                schema: "shop",
                table: "shop_languages");

            migrationBuilder.DropColumn(
                name: "comparison",
                schema: "shop",
                table: "shop_languages");

            migrationBuilder.AddColumn<float>(
                name: "translated_share",
                schema: "shop",
                table: "shop_languages",
                type: "real",
                nullable: true);

            migrationBuilder.AddColumn<JsonDocument>(
                name: "description_languages",
                schema: "shop",
                table: "shop_languages",
                type: "jsonb",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "translated_share",
                schema: "shop",
                table: "shop_languages");

            migrationBuilder.DropColumn(
                name: "description_languages",
                schema: "shop",
                table: "shop_languages");

            migrationBuilder.AddColumn<float>(
                name: "own_text_share",
                schema: "shop",
                table: "shop_languages",
                type: "real",
                nullable: true);

            migrationBuilder.AddColumn<JsonDocument>(
                name: "comparison",
                schema: "shop",
                table: "shop_languages",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "counted",
                schema: "shop",
                table: "shop_languages",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }
    }
}
