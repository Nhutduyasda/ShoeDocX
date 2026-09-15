using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShoeExportInvoice.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyTemplateSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CompanyTemplates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    TemplateFileName = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    TemplateFilePath = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    ConfigJson = table.Column<string>(type: "TEXT", nullable: false),
                    IsDefault = table.Column<bool>(type: "INTEGER", nullable: false),
                    FolderId = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanyTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompanyTemplates_MasterDataFolders_FolderId",
                        column: x => x.FolderId,
                        principalTable: "MasterDataFolders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CompanyTemplates_FolderId",
                table: "CompanyTemplates",
                column: "FolderId");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyTemplates_IsDefault",
                table: "CompanyTemplates",
                column: "IsDefault");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CompanyTemplates");
        }
    }
}
