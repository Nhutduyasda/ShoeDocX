using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShoeExportInvoice.Api.MigrationsSqlServer
{
    /// <inheritdoc />
    public partial class InitialSqlServer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AspNetRoles",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUsers",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Department = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SecurityStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "bit", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "bit", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BomMasters",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    StyleCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ProcessType = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    Version = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BomMasters", x => x.Id);
                    table.CheckConstraint("CK_BomMasters_ProcessType", "ProcessType IN (1, 2)");
                });

            migrationBuilder.CreateTable(
                name: "BusinessAuditLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ActorUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    ActorUserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ActorRole = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Action = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ResourceType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ResourceId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PreviousStateJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NewStateJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    TraceId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusinessAuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MasterDataFolders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    ParentId = table.Column<int>(type: "int", nullable: true),
                    CustomerName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    DeliveryAddress = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    ContractNo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PoSuffix = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    DefaultPairsPerCarton = table.Column<int>(type: "int", nullable: false),
                    DefaultUnit = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    InvoiceNoPattern = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    FileNamePattern = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    CurrentSequenceNumber = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MasterDataFolders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MasterDataFolders_MasterDataFolders_ParentId",
                        column: x => x.ParentId,
                        principalTable: "MasterDataFolders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Materials",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MaterialCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    MaterialName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    MaterialType = table.Column<int>(type: "int", nullable: false),
                    CurrentStock = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Materials", x => x.Id);
                    table.CheckConstraint("CK_Materials_CurrentStock_NonNegative", "CurrentStock >= 0");
                    table.CheckConstraint("CK_Materials_MaterialType", "MaterialType IN (0, 1)");
                });

            migrationBuilder.CreateTable(
                name: "ProductionOrders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OrderNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    StyleCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ProcessType = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    Status = table.Column<int>(type: "int", nullable: false),
                    TotalQuantity = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductionOrders", x => x.Id);
                    table.CheckConstraint("CK_ProductionOrders_ProcessType", "ProcessType IN (1, 2)");
                    table.CheckConstraint("CK_ProductionOrders_Status", "Status IN (0, 1, 2, 3, 4)");
                    table.CheckConstraint("CK_ProductionOrders_TotalQuantity_NonNegative", "TotalQuantity >= 0");
                });

            migrationBuilder.CreateTable(
                name: "RevokedJwts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Jti = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RevokedJwts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SystemSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Key = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TenantWorkspaces",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    TaxCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    AiCredits = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantWorkspaces", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderKey = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserRoles",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    RoleId = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CompanyTemplates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    TemplateFileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    TemplateFilePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ConfigJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
                    FolderId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
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

            migrationBuilder.CreateTable(
                name: "CustomsSettlementPeriods",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ContractFolderId = table.Column<int>(type: "int", nullable: true),
                    Year = table.Column<int>(type: "int", nullable: false),
                    FromDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ToDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ContractNo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CustomsOffice = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CompanyName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    TaxCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Address = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomsSettlementPeriods", x => x.Id);
                    table.CheckConstraint("CK_CustomsSettlementPeriods_Status", "Status IN ('Draft', 'Finalized')");
                    table.ForeignKey(
                        name: "FK_CustomsSettlementPeriods_MasterDataFolders_ContractFolderId",
                        column: x => x.ContractFolderId,
                        principalTable: "MasterDataFolders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProductMasters",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    StyleCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    UnitPriceCMT = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    UnitPriceDAP = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    UnitPriceCMT_Go = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    UnitPriceDAP_Go = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    HsCode = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    PairPerCarton = table.Column<int>(type: "int", nullable: false),
                    FolderId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductMasters", x => x.Id);
                    table.CheckConstraint("CK_ProductMasters_PairPerCarton", "PairPerCarton BETWEEN 1 AND 1000");
                    table.CheckConstraint("CK_ProductMasters_Prices_NonNegative", "UnitPriceCMT >= 0 AND UnitPriceDAP >= 0 AND (UnitPriceCMT_Go IS NULL OR UnitPriceCMT_Go >= 0) AND (UnitPriceDAP_Go IS NULL OR UnitPriceDAP_Go >= 0)");
                    table.ForeignKey(
                        name: "FK_ProductMasters_MasterDataFolders_FolderId",
                        column: x => x.FolderId,
                        principalTable: "MasterDataFolders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ShipmentOrders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ContractFolderId = table.Column<int>(type: "int", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    InvoiceNo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    InvoiceDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PoSuffix = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ContractNo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CustomerName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Address = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DeliveryTerms = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PaymentTerms = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DeclarationNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ClearanceDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CustomsDeclarationType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    CustomsChannel = table.Column<int>(type: "int", nullable: true),
                    CustomsOffice = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CustomsPackageQty = table.Column<int>(type: "int", nullable: true),
                    CustomsGrossWeight = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    CustomsTotalDap = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    CustomsTotalCmt = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    CustomsAttachmentFileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    CustomsAttachmentFilePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsLocked = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShipmentOrders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShipmentOrders_MasterDataFolders_ContractFolderId",
                        column: x => x.ContractFolderId,
                        principalTable: "MasterDataFolders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BomItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    BomMasterId = table.Column<int>(type: "int", nullable: false),
                    MaterialId = table.Column<int>(type: "int", nullable: false),
                    NetConsumption = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    WastageRatePercent = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BomItems", x => x.Id);
                    table.CheckConstraint("CK_BomItems_NetConsumption_NonNegative", "NetConsumption >= 0");
                    table.CheckConstraint("CK_BomItems_WastageRate", "WastageRatePercent BETWEEN 0 AND 100");
                    table.ForeignKey(
                        name: "FK_BomItems_BomMasters_BomMasterId",
                        column: x => x.BomMasterId,
                        principalTable: "BomMasters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BomItems_Materials_MaterialId",
                        column: x => x.MaterialId,
                        principalTable: "Materials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MaterialRequirementPlans",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ProductionOrderId = table.Column<int>(type: "int", nullable: false),
                    BomMasterId = table.Column<int>(type: "int", nullable: false),
                    BomVersion = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    CalculatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IssuedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaterialRequirementPlans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MaterialRequirementPlans_ProductionOrders_ProductionOrderId",
                        column: x => x.ProductionOrderId,
                        principalTable: "ProductionOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrderSizeRuns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ProductionOrderId = table.Column<int>(type: "int", nullable: false),
                    SizeName = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderSizeRuns", x => x.Id);
                    table.CheckConstraint("CK_OrderSizeRuns_Quantity_Positive", "Quantity > 0");
                    table.ForeignKey(
                        name: "FK_OrderSizeRuns_ProductionOrders_ProductionOrderId",
                        column: x => x.ProductionOrderId,
                        principalTable: "ProductionOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CustomsSettlementItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SettlementPeriodId = table.Column<int>(type: "int", nullable: false),
                    ProductCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ProcessType = table.Column<int>(type: "int", nullable: false),
                    ProductName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    HsCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    OpeningBalance = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    InPeriodProduction = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    InPeriodExport = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    OtherExport = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ClosingBalance = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomsSettlementItems", x => x.Id);
                    table.CheckConstraint("CK_CustomsSettlementItems_Inputs_NonNegative", "OpeningBalance >= 0 AND InPeriodProduction >= 0 AND InPeriodExport >= 0 AND OtherExport >= 0");
                    table.ForeignKey(
                        name: "FK_CustomsSettlementItems_CustomsSettlementPeriods_SettlementPeriodId",
                        column: x => x.SettlementPeriodId,
                        principalTable: "CustomsSettlementPeriods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ShipmentOrderItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PairPerCarton = table.Column<int>(type: "int", nullable: false),
                    ShipmentOrderId = table.Column<int>(type: "int", nullable: false),
                    StyleCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    FullItemCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    ProcessType = table.Column<int>(type: "int", nullable: false),
                    SizeBreakdownJson = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    UnitPriceCMT = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    UnitPriceDAP = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShipmentOrderItems", x => x.Id);
                    table.CheckConstraint("CK_ShipmentOrderItems_PairPerCarton", "PairPerCarton BETWEEN 1 AND 1000");
                    table.CheckConstraint("CK_ShipmentOrderItems_Prices_NonNegative", "UnitPriceCMT >= 0 AND UnitPriceDAP >= 0");
                    table.CheckConstraint("CK_ShipmentOrderItems_ProcessType", "ProcessType IN (1, 2)");
                    table.CheckConstraint("CK_ShipmentOrderItems_Quantity_Positive", "Quantity > 0");
                    table.ForeignKey(
                        name: "FK_ShipmentOrderItems_ShipmentOrders_ShipmentOrderId",
                        column: x => x.ShipmentOrderId,
                        principalTable: "ShipmentOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ShipmentUnlockAudits",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ShipmentOrderId = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    UnlockedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    UnlockedByUserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    PreviousStatus = table.Column<int>(type: "int", nullable: false),
                    UnlockedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShipmentUnlockAudits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShipmentUnlockAudits_ShipmentOrders_ShipmentOrderId",
                        column: x => x.ShipmentOrderId,
                        principalTable: "ShipmentOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WarehouseBatches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BatchName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BatchNumber = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ExportDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ContractNote = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ContractFolderId = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    TotalQuantity = table.Column<int>(type: "int", nullable: false),
                    ShipmentOrderId = table.Column<int>(type: "int", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WarehouseBatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WarehouseBatches_MasterDataFolders_ContractFolderId",
                        column: x => x.ContractFolderId,
                        principalTable: "MasterDataFolders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WarehouseBatches_ShipmentOrders_ShipmentOrderId",
                        column: x => x.ShipmentOrderId,
                        principalTable: "ShipmentOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MaterialRequirementPlanItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MaterialRequirementPlanId = table.Column<int>(type: "int", nullable: false),
                    MaterialId = table.Column<int>(type: "int", nullable: false),
                    MaterialCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    MaterialName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    MaterialType = table.Column<int>(type: "int", nullable: false),
                    NetConsumption = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    WastageRatePercent = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    RequiredQuantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    StockAtCalculation = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaterialRequirementPlanItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MaterialRequirementPlanItems_MaterialRequirementPlans_MaterialRequirementPlanId",
                        column: x => x.MaterialRequirementPlanId,
                        principalTable: "MaterialRequirementPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MaterialRequirementPlanItems_Materials_MaterialId",
                        column: x => x.MaterialId,
                        principalTable: "Materials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WarehouseBatchItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WarehouseBatchId = table.Column<int>(type: "int", nullable: false),
                    StyleCode = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    ProcessType = table.Column<int>(type: "int", nullable: false),
                    IsPendingReview = table.Column<bool>(type: "bit", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WarehouseBatchItems", x => x.Id);
                    table.CheckConstraint("CK_WarehouseBatchItems_Quantity_Positive", "Quantity > 0");
                    table.ForeignKey(
                        name: "FK_WarehouseBatchItems_WarehouseBatches_WarehouseBatchId",
                        column: x => x.WarehouseBatchId,
                        principalTable: "WarehouseBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MaterialRequirementPlanSizes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MaterialRequirementPlanItemId = table.Column<int>(type: "int", nullable: false),
                    SizeName = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    OrderQuantity = table.Column<int>(type: "int", nullable: false),
                    RequiredQuantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaterialRequirementPlanSizes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MaterialRequirementPlanSizes_MaterialRequirementPlanItems_MaterialRequirementPlanItemId",
                        column: x => x.MaterialRequirementPlanItemId,
                        principalTable: "MaterialRequirementPlanItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AspNetRoleClaims_RoleId",
                table: "AspNetRoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "AspNetRoles",
                column: "NormalizedName",
                unique: true,
                filter: "[NormalizedName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserClaims_UserId",
                table: "AspNetUserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserLogins_UserId",
                table: "AspNetUserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserRoles_RoleId",
                table: "AspNetUserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "AspNetUsers",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "AspNetUsers",
                column: "NormalizedUserName",
                unique: true,
                filter: "[NormalizedUserName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BomItems_BomMasterId_MaterialId",
                table: "BomItems",
                columns: new[] { "BomMasterId", "MaterialId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BomItems_MaterialId",
                table: "BomItems",
                column: "MaterialId");

            migrationBuilder.CreateIndex(
                name: "IX_BomMasters_TenantId_StyleCode_CreatedAt",
                table: "BomMasters",
                columns: new[] { "TenantId", "StyleCode", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_BomMasters_TenantId_StyleCode_ProcessType_Version",
                table: "BomMasters",
                columns: new[] { "TenantId", "StyleCode", "ProcessType", "Version" },
                unique: true,
                filter: "[TenantId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessAuditLogs_CreatedAt",
                table: "BusinessAuditLogs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessAuditLogs_ResourceType_ResourceId_CreatedAt",
                table: "BusinessAuditLogs",
                columns: new[] { "ResourceType", "ResourceId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CompanyTemplates_FolderId",
                table: "CompanyTemplates",
                column: "FolderId");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyTemplates_IsDefault",
                table: "CompanyTemplates",
                column: "IsDefault");

            migrationBuilder.CreateIndex(
                name: "IX_CustomsSettlementItems_SettlementPeriodId_ProductCode_ProcessType",
                table: "CustomsSettlementItems",
                columns: new[] { "SettlementPeriodId", "ProductCode", "ProcessType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomsSettlementPeriods_ContractFolderId_ToDate",
                table: "CustomsSettlementPeriods",
                columns: new[] { "ContractFolderId", "ToDate" });

            migrationBuilder.CreateIndex(
                name: "IX_MasterDataFolders_ParentId",
                table: "MasterDataFolders",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_MaterialRequirementPlanItems_MaterialId",
                table: "MaterialRequirementPlanItems",
                column: "MaterialId");

            migrationBuilder.CreateIndex(
                name: "IX_MaterialRequirementPlanItems_MaterialRequirementPlanId_MaterialId",
                table: "MaterialRequirementPlanItems",
                columns: new[] { "MaterialRequirementPlanId", "MaterialId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MaterialRequirementPlans_ProductionOrderId",
                table: "MaterialRequirementPlans",
                column: "ProductionOrderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MaterialRequirementPlanSizes_MaterialRequirementPlanItemId_SizeName",
                table: "MaterialRequirementPlanSizes",
                columns: new[] { "MaterialRequirementPlanItemId", "SizeName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Materials_TenantId_MaterialCode",
                table: "Materials",
                columns: new[] { "TenantId", "MaterialCode" },
                unique: true,
                filter: "[TenantId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OrderSizeRuns_ProductionOrderId_SizeName",
                table: "OrderSizeRuns",
                columns: new[] { "ProductionOrderId", "SizeName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductionOrders_TenantId_OrderNo",
                table: "ProductionOrders",
                columns: new[] { "TenantId", "OrderNo" },
                unique: true,
                filter: "[TenantId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionOrders_TenantId_StyleCode",
                table: "ProductionOrders",
                columns: new[] { "TenantId", "StyleCode" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductMasters_FolderId_StyleCode",
                table: "ProductMasters",
                columns: new[] { "FolderId", "StyleCode" },
                unique: true,
                filter: "[FolderId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ProductMasters_StyleCode",
                table: "ProductMasters",
                column: "StyleCode",
                unique: true,
                filter: "[FolderId] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_RevokedJwts_ExpiresAt",
                table: "RevokedJwts",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_RevokedJwts_Jti",
                table: "RevokedJwts",
                column: "Jti",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ShipmentOrderItems_ShipmentOrderId",
                table: "ShipmentOrderItems",
                column: "ShipmentOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_ShipmentOrderItems_StyleCode",
                table: "ShipmentOrderItems",
                column: "StyleCode");

            migrationBuilder.CreateIndex(
                name: "IX_ShipmentOrders_ContractFolderId_Status_CustomsDeclarationType_ClearanceDate",
                table: "ShipmentOrders",
                columns: new[] { "ContractFolderId", "Status", "CustomsDeclarationType", "ClearanceDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ShipmentOrders_DeclarationNo",
                table: "ShipmentOrders",
                column: "DeclarationNo",
                unique: true,
                filter: "[DeclarationNo] IS NOT NULL AND [DeclarationNo] <> ''");

            migrationBuilder.CreateIndex(
                name: "IX_ShipmentOrders_InvoiceNo",
                table: "ShipmentOrders",
                column: "InvoiceNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ShipmentUnlockAudits_ShipmentOrderId_UnlockedAt",
                table: "ShipmentUnlockAudits",
                columns: new[] { "ShipmentOrderId", "UnlockedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SystemSettings_Key",
                table: "SystemSettings",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseBatches_BatchNumber",
                table: "WarehouseBatches",
                column: "BatchNumber");

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseBatches_ContractFolderId",
                table: "WarehouseBatches",
                column: "ContractFolderId");

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseBatches_ExportDate",
                table: "WarehouseBatches",
                column: "ExportDate");

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseBatches_ShipmentOrderId",
                table: "WarehouseBatches",
                column: "ShipmentOrderId",
                unique: true,
                filter: "[ShipmentOrderId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseBatchItems_WarehouseBatchId_StyleCode",
                table: "WarehouseBatchItems",
                columns: new[] { "WarehouseBatchId", "StyleCode" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AspNetRoleClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins");

            migrationBuilder.DropTable(
                name: "AspNetUserRoles");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens");

            migrationBuilder.DropTable(
                name: "BomItems");

            migrationBuilder.DropTable(
                name: "BusinessAuditLogs");

            migrationBuilder.DropTable(
                name: "CompanyTemplates");

            migrationBuilder.DropTable(
                name: "CustomsSettlementItems");

            migrationBuilder.DropTable(
                name: "MaterialRequirementPlanSizes");

            migrationBuilder.DropTable(
                name: "OrderSizeRuns");

            migrationBuilder.DropTable(
                name: "ProductMasters");

            migrationBuilder.DropTable(
                name: "RevokedJwts");

            migrationBuilder.DropTable(
                name: "ShipmentOrderItems");

            migrationBuilder.DropTable(
                name: "ShipmentUnlockAudits");

            migrationBuilder.DropTable(
                name: "SystemSettings");

            migrationBuilder.DropTable(
                name: "TenantWorkspaces");

            migrationBuilder.DropTable(
                name: "WarehouseBatchItems");

            migrationBuilder.DropTable(
                name: "AspNetRoles");

            migrationBuilder.DropTable(
                name: "AspNetUsers");

            migrationBuilder.DropTable(
                name: "BomMasters");

            migrationBuilder.DropTable(
                name: "CustomsSettlementPeriods");

            migrationBuilder.DropTable(
                name: "MaterialRequirementPlanItems");

            migrationBuilder.DropTable(
                name: "WarehouseBatches");

            migrationBuilder.DropTable(
                name: "MaterialRequirementPlans");

            migrationBuilder.DropTable(
                name: "Materials");

            migrationBuilder.DropTable(
                name: "ShipmentOrders");

            migrationBuilder.DropTable(
                name: "ProductionOrders");

            migrationBuilder.DropTable(
                name: "MasterDataFolders");
        }
    }
}
