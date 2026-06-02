using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FilmAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddRitiroOrdine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RitiriOrdine",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    CartId = table.Column<int>(type: "int", nullable: false),
                    CodiceRitiro = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Stato = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatoIl = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    RitiratoIl = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    RitiratoDaUtenteId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RitiriOrdine", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RitiriOrdine_Carts_CartId",
                        column: x => x.CartId,
                        principalTable: "Carts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RitiriOrdine_Utenti_RitiratoDaUtenteId",
                        column: x => x.RitiratoDaUtenteId,
                        principalTable: "Utenti",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_RitiriOrdine_CartId",
                table: "RitiriOrdine",
                column: "CartId");

            migrationBuilder.CreateIndex(
                name: "IX_RitiriOrdine_CodiceRitiro",
                table: "RitiriOrdine",
                column: "CodiceRitiro",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RitiriOrdine_RitiratoDaUtenteId",
                table: "RitiriOrdine",
                column: "RitiratoDaUtenteId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RitiriOrdine");
        }
    }
}
