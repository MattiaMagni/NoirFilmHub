using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FilmAPI.Migrations
{
    /// <inheritdoc />
    public partial class Iteration5_IdentityAndSecurity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "PasswordHash",
                table: "Utenti",
                type: "longtext",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "longtext")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "AuthVersion",
                table: "Utenti",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAtUtc",
                table: "Utenti",
                type: "datetime(6)",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<decimal>(
                name: "CreditoPiattaforma",
                table: "Utenti",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "EmailVerified",
                table: "Utenti",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "FailedLoginAttempts",
                table: "Utenti",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsDisabled",
                table: "Utenti",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastLoginAtUtc",
                table: "Utenti",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastLoginProvider",
                table: "Utenti",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<bool>(
                name: "LocalCredentialsEnabled",
                table: "Utenti",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LockoutEndUtc",
                table: "Utenti",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NormalizedEmail",
                table: "Utenti",
                type: "varchar(255)",
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "PasswordChangedAtUtc",
                table: "Utenti",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SecurityStamp",
                table: "Utenti",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "AccountActionTokens",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    UtenteId = table.Column<int>(type: "int", nullable: false),
                    TokenHash = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TokenType = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ConsumedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountActionTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AccountActionTokens_Utenti_UtenteId",
                        column: x => x.UtenteId,
                        principalTable: "Utenti",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ExternalAuthExchangeCodes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    CodeHash = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    StateId = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ConsumedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalAuthExchangeCodes", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ExternalAuthStates",
                columns: table => new
                {
                    Id = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ReturnUrl = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Provider = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Mode = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalAuthStates", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "UserExternalLogins",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    UtenteId = table.Column<int>(type: "int", nullable: false),
                    Provider = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ProviderKey = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ProviderDisplayName = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TenantId = table.Column<string>(type: "varchar(255)", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Email = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserExternalLogins", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserExternalLogins_Utenti_UtenteId",
                        column: x => x.UtenteId,
                        principalTable: "Utenti",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "UserSecurityAuditLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    UtenteId = table.Column<int>(type: "int", nullable: true),
                    EventType = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Provider = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IpAddress = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    UserAgent = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Details = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserSecurityAuditLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserSecurityAuditLogs_Utenti_UtenteId",
                        column: x => x.UtenteId,
                        principalTable: "Utenti",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Utenti_NormalizedEmail",
                table: "Utenti",
                column: "NormalizedEmail",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Utenti_Ruolo",
                table: "Utenti",
                column: "Ruolo");

            migrationBuilder.CreateIndex(
                name: "IX_AccountActionTokens_TokenHash",
                table: "AccountActionTokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AccountActionTokens_UtenteId",
                table: "AccountActionTokens",
                column: "UtenteId");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalAuthExchangeCodes_CodeHash",
                table: "ExternalAuthExchangeCodes",
                column: "CodeHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserExternalLogins_Provider_ProviderKey",
                table: "UserExternalLogins",
                columns: new[] { "Provider", "ProviderKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserExternalLogins_Provider_TenantId_ProviderKey",
                table: "UserExternalLogins",
                columns: new[] { "Provider", "TenantId", "ProviderKey" });

            migrationBuilder.CreateIndex(
                name: "IX_UserExternalLogins_UtenteId",
                table: "UserExternalLogins",
                column: "UtenteId");

            migrationBuilder.CreateIndex(
                name: "IX_UserSecurityAuditLogs_CreatedAtUtc",
                table: "UserSecurityAuditLogs",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_UserSecurityAuditLogs_EventType_CreatedAtUtc",
                table: "UserSecurityAuditLogs",
                columns: new[] { "EventType", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_UserSecurityAuditLogs_UtenteId_CreatedAtUtc",
                table: "UserSecurityAuditLogs",
                columns: new[] { "UtenteId", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccountActionTokens");

            migrationBuilder.DropTable(
                name: "ExternalAuthExchangeCodes");

            migrationBuilder.DropTable(
                name: "ExternalAuthStates");

            migrationBuilder.DropTable(
                name: "UserExternalLogins");

            migrationBuilder.DropTable(
                name: "UserSecurityAuditLogs");

            migrationBuilder.DropIndex(
                name: "IX_Utenti_NormalizedEmail",
                table: "Utenti");

            migrationBuilder.DropIndex(
                name: "IX_Utenti_Ruolo",
                table: "Utenti");

            migrationBuilder.DropColumn(
                name: "AuthVersion",
                table: "Utenti");

            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                table: "Utenti");

            migrationBuilder.DropColumn(
                name: "CreditoPiattaforma",
                table: "Utenti");

            migrationBuilder.DropColumn(
                name: "EmailVerified",
                table: "Utenti");

            migrationBuilder.DropColumn(
                name: "FailedLoginAttempts",
                table: "Utenti");

            migrationBuilder.DropColumn(
                name: "IsDisabled",
                table: "Utenti");

            migrationBuilder.DropColumn(
                name: "LastLoginAtUtc",
                table: "Utenti");

            migrationBuilder.DropColumn(
                name: "LastLoginProvider",
                table: "Utenti");

            migrationBuilder.DropColumn(
                name: "LocalCredentialsEnabled",
                table: "Utenti");

            migrationBuilder.DropColumn(
                name: "LockoutEndUtc",
                table: "Utenti");

            migrationBuilder.DropColumn(
                name: "NormalizedEmail",
                table: "Utenti");

            migrationBuilder.DropColumn(
                name: "PasswordChangedAtUtc",
                table: "Utenti");

            migrationBuilder.DropColumn(
                name: "SecurityStamp",
                table: "Utenti");

            migrationBuilder.UpdateData(
                table: "Utenti",
                keyColumn: "PasswordHash",
                keyValue: null,
                column: "PasswordHash",
                value: "");

            migrationBuilder.AlterColumn<string>(
                name: "PasswordHash",
                table: "Utenti",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "longtext",
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");
        }
    }
}
