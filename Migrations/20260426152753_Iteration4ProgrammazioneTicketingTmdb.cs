using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FilmAPI.Migrations
{
    /// <inheritdoc />
    public partial class Iteration4ProgrammazioneTicketingTmdb : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Proiezioni_Cinemas_CinemaId",
                table: "Proiezioni");

            migrationBuilder.DropIndex(
                name: "IX_Proiezioni_CinemaId_FilmId_Data_Ora",
                table: "Proiezioni");

            migrationBuilder.AddColumn<int>(
                name: "CinemaPreferitoId",
                table: "Utenti",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CreditoPiattaforma",
                table: "Utenti",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PrezzoBase",
                table: "Proiezioni",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "SalaId",
                table: "Proiezioni",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CinemaValidazioneId",
                table: "Prenotazioni",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CodiceAcquisto",
                table: "Prenotazioni",
                type: "varchar(255)",
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<decimal>(
                name: "ImportoCartaUsato",
                table: "Prenotazioni",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ImportoCreditoUsato",
                table: "Prenotazioni",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "PostiSelezionati",
                table: "Prenotazioni",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<decimal>(
                name: "TotalePrezzo",
                table: "Prenotazioni",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "Validato",
                table: "Prenotazioni",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "ValidatoAtUtc",
                table: "Prenotazioni",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ValidatoDaUtenteId",
                table: "Prenotazioni",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BackdropPath",
                table: "Films",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "CastPrincipale",
                table: "Films",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "DataUscita",
                table: "Films",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DescrizioneLunga",
                table: "Films",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "TitoloOriginale",
                table: "Films",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "TmdbMovieId",
                table: "Films",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TmdbSyncStato",
                table: "Films",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "UltimaSyncTmdbUtc",
                table: "Films",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Attivo",
                table: "Cinemas",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "CodiceLocale",
                table: "Cinemas",
                type: "varchar(255)",
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<double>(
                name: "Latitudine",
                table: "Cinemas",
                type: "double",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Longitudine",
                table: "Cinemas",
                type: "double",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RicaricheCredito",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    UtenteId = table.Column<int>(type: "int", nullable: false),
                    OperatoreId = table.Column<int>(type: "int", nullable: false),
                    CinemaId = table.Column<int>(type: "int", nullable: false),
                    Importo = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    Note = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RicaricheCredito", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RicaricheCredito_Cinemas_CinemaId",
                        column: x => x.CinemaId,
                        principalTable: "Cinemas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RicaricheCredito_Utenti_OperatoreId",
                        column: x => x.OperatoreId,
                        principalTable: "Utenti",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RicaricheCredito_Utenti_UtenteId",
                        column: x => x.UtenteId,
                        principalTable: "Utenti",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Sale",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    CinemaId = table.Column<int>(type: "int", nullable: false),
                    NumeroProgressivo = table.Column<int>(type: "int", nullable: false),
                    Tipologia = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Nome = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    NumeroFile = table.Column<int>(type: "int", nullable: false),
                    PostiPerFila = table.Column<int>(type: "int", nullable: false),
                    MappaPostiJson = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Attiva = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sale", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sale_Cinemas_CinemaId",
                        column: x => x.CinemaId,
                        principalTable: "Cinemas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "SeatLocks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    ProiezioneId = table.Column<int>(type: "int", nullable: false),
                    UtenteId = table.Column<int>(type: "int", nullable: false),
                    PostoCodice = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SeatLocks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SeatLocks_Proiezioni_ProiezioneId",
                        column: x => x.ProiezioneId,
                        principalTable: "Proiezioni",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SeatLocks_Utenti_UtenteId",
                        column: x => x.UtenteId,
                        principalTable: "Utenti",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Proiezioni_CinemaId_FilmId_Data_Ora",
                table: "Proiezioni",
                columns: new[] { "CinemaId", "FilmId", "Data", "Ora" });

            migrationBuilder.Sql(
                "UPDATE `Prenotazioni` SET `CodiceAcquisto` = CONCAT('LEGACY-', `Id`) WHERE `CodiceAcquisto` = '';");

            migrationBuilder.Sql(
                "UPDATE `Cinemas` SET `CodiceLocale` = CONCAT('LOC-', `Id`) WHERE `CodiceLocale` = '';");

            migrationBuilder.CreateIndex(
                name: "IX_Proiezioni_SalaId_Data_Ora",
                table: "Proiezioni",
                columns: new[] { "SalaId", "Data", "Ora" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Prenotazioni_CodiceAcquisto",
                table: "Prenotazioni",
                column: "CodiceAcquisto",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Films_TmdbMovieId",
                table: "Films",
                column: "TmdbMovieId");

            migrationBuilder.CreateIndex(
                name: "IX_Cinemas_CodiceLocale",
                table: "Cinemas",
                column: "CodiceLocale",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RicaricheCredito_CinemaId",
                table: "RicaricheCredito",
                column: "CinemaId");

            migrationBuilder.CreateIndex(
                name: "IX_RicaricheCredito_OperatoreId",
                table: "RicaricheCredito",
                column: "OperatoreId");

            migrationBuilder.CreateIndex(
                name: "IX_RicaricheCredito_UtenteId",
                table: "RicaricheCredito",
                column: "UtenteId");

            migrationBuilder.CreateIndex(
                name: "IX_Sale_CinemaId_NumeroProgressivo",
                table: "Sale",
                columns: new[] { "CinemaId", "NumeroProgressivo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SeatLocks_ProiezioneId_PostoCodice",
                table: "SeatLocks",
                columns: new[] { "ProiezioneId", "PostoCodice" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SeatLocks_UtenteId",
                table: "SeatLocks",
                column: "UtenteId");

            migrationBuilder.AddForeignKey(
                name: "FK_Proiezioni_Sale_SalaId",
                table: "Proiezioni",
                column: "SalaId",
                principalTable: "Sale",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Proiezioni_Cinemas_CinemaId",
                table: "Proiezioni",
                column: "CinemaId",
                principalTable: "Cinemas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Proiezioni_Sale_SalaId",
                table: "Proiezioni");

            migrationBuilder.DropForeignKey(
                name: "FK_Proiezioni_Cinemas_CinemaId",
                table: "Proiezioni");

            migrationBuilder.DropTable(
                name: "RicaricheCredito");

            migrationBuilder.DropTable(
                name: "Sale");

            migrationBuilder.DropTable(
                name: "SeatLocks");

            migrationBuilder.DropIndex(
                name: "IX_Proiezioni_CinemaId_FilmId_Data_Ora",
                table: "Proiezioni");

            migrationBuilder.DropIndex(
                name: "IX_Proiezioni_SalaId_Data_Ora",
                table: "Proiezioni");

            migrationBuilder.DropIndex(
                name: "IX_Prenotazioni_CodiceAcquisto",
                table: "Prenotazioni");

            migrationBuilder.DropIndex(
                name: "IX_Films_TmdbMovieId",
                table: "Films");

            migrationBuilder.DropIndex(
                name: "IX_Cinemas_CodiceLocale",
                table: "Cinemas");

            migrationBuilder.DropColumn(
                name: "CinemaPreferitoId",
                table: "Utenti");

            migrationBuilder.DropColumn(
                name: "CreditoPiattaforma",
                table: "Utenti");

            migrationBuilder.DropColumn(
                name: "PrezzoBase",
                table: "Proiezioni");

            migrationBuilder.DropColumn(
                name: "SalaId",
                table: "Proiezioni");

            migrationBuilder.DropColumn(
                name: "CinemaValidazioneId",
                table: "Prenotazioni");

            migrationBuilder.DropColumn(
                name: "CodiceAcquisto",
                table: "Prenotazioni");

            migrationBuilder.DropColumn(
                name: "ImportoCartaUsato",
                table: "Prenotazioni");

            migrationBuilder.DropColumn(
                name: "ImportoCreditoUsato",
                table: "Prenotazioni");

            migrationBuilder.DropColumn(
                name: "PostiSelezionati",
                table: "Prenotazioni");

            migrationBuilder.DropColumn(
                name: "TotalePrezzo",
                table: "Prenotazioni");

            migrationBuilder.DropColumn(
                name: "Validato",
                table: "Prenotazioni");

            migrationBuilder.DropColumn(
                name: "ValidatoAtUtc",
                table: "Prenotazioni");

            migrationBuilder.DropColumn(
                name: "ValidatoDaUtenteId",
                table: "Prenotazioni");

            migrationBuilder.DropColumn(
                name: "BackdropPath",
                table: "Films");

            migrationBuilder.DropColumn(
                name: "CastPrincipale",
                table: "Films");

            migrationBuilder.DropColumn(
                name: "DataUscita",
                table: "Films");

            migrationBuilder.DropColumn(
                name: "DescrizioneLunga",
                table: "Films");

            migrationBuilder.DropColumn(
                name: "TitoloOriginale",
                table: "Films");

            migrationBuilder.DropColumn(
                name: "TmdbMovieId",
                table: "Films");

            migrationBuilder.DropColumn(
                name: "TmdbSyncStato",
                table: "Films");

            migrationBuilder.DropColumn(
                name: "UltimaSyncTmdbUtc",
                table: "Films");

            migrationBuilder.DropColumn(
                name: "Attivo",
                table: "Cinemas");

            migrationBuilder.DropColumn(
                name: "CodiceLocale",
                table: "Cinemas");

            migrationBuilder.DropColumn(
                name: "Latitudine",
                table: "Cinemas");

            migrationBuilder.DropColumn(
                name: "Longitudine",
                table: "Cinemas");

            migrationBuilder.CreateIndex(
                name: "IX_Proiezioni_CinemaId_FilmId_Data_Ora",
                table: "Proiezioni",
                columns: new[] { "CinemaId", "FilmId", "Data", "Ora" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Proiezioni_Cinemas_CinemaId",
                table: "Proiezioni",
                column: "CinemaId",
                principalTable: "Cinemas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
