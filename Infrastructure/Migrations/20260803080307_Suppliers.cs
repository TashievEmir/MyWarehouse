using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Suppliers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "SupplierId",
                table: "Purchases",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Suppliers",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Suppliers", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Purchases_SupplierId",
                table: "Purchases",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_Name",
                table: "Suppliers",
                column: "Name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Purchases_Suppliers_SupplierId",
                table: "Purchases",
                column: "SupplierId",
                principalTable: "Suppliers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            // Поставщики, которых уже вводили руками, переезжают в справочник —
            // иначе список на приёмке оказался бы пустым на существующей базе.
            // «Без поставщика» — это заглушка прежнего кода, а не имя.
            migrationBuilder.Sql(@"
                INSERT INTO Suppliers (Name)
                SELECT DISTINCT TRIM(SupplierName)
                FROM Purchases
                WHERE TRIM(COALESCE(SupplierName, '')) <> ''
                  AND TRIM(SupplierName) NOT IN ('Без поставщика', 'Жеткирүүчүсүз');");

            migrationBuilder.Sql(@"
                UPDATE Purchases
                SET SupplierId = (SELECT s.Id FROM Suppliers s WHERE s.Name = TRIM(Purchases.SupplierName))
                WHERE TRIM(COALESCE(SupplierName, '')) <> '';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Purchases_Suppliers_SupplierId",
                table: "Purchases");

            migrationBuilder.DropTable(
                name: "Suppliers");

            migrationBuilder.DropIndex(
                name: "IX_Purchases_SupplierId",
                table: "Purchases");

            migrationBuilder.DropColumn(
                name: "SupplierId",
                table: "Purchases");
        }
    }
}
