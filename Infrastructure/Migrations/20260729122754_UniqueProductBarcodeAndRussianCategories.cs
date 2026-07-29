using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UniqueProductBarcodeAndRussianCategories : Migration
    {
        /// <summary>Категории из первого сидинга заводились по-английски.</summary>
        private static readonly (string En, string Ru)[] SeededCategories =
        [
            ("Electronics", "Электроника"),
            ("Food", "Продукты питания"),
            ("Clothes", "Одежда"),
            ("Office", "Канцтовары")
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Products_Barcode",
                table: "Products",
                column: "Barcode",
                unique: true);

            Rename(migrationBuilder, from: c => c.En, to: c => c.Ru);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            Rename(migrationBuilder, from: c => c.Ru, to: c => c.En);

            migrationBuilder.DropIndex(
                name: "IX_Products_Barcode",
                table: "Products");
        }

        /// <summary>
        /// Переименовывает только те категории, у которых нет тёзки с целевым именем —
        /// иначе в базе появился бы дубль.
        /// </summary>
        private static void Rename(
            MigrationBuilder migrationBuilder,
            Func<(string En, string Ru), string> from,
            Func<(string En, string Ru), string> to)
        {
            foreach (var category in SeededCategories)
            {
                var oldName = from(category).Replace("'", "''");
                var newName = to(category).Replace("'", "''");

                migrationBuilder.Sql(
                    $"""
                     UPDATE Categories
                        SET Name = '{newName}'
                      WHERE Name = '{oldName}'
                        AND NOT EXISTS (SELECT 1 FROM Categories WHERE Name = '{newName}');
                     """);
            }
        }
    }
}
