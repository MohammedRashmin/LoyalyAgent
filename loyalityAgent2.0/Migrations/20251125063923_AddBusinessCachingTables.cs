using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace loyalityAgent2._0.Migrations
{
    /// <inheritdoc />
    public partial class AddBusinessCachingTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Conditionally rename Users to AgentUsers if Users table exists
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT * FROM sys.tables WHERE name = 'Users' AND schema_id = SCHEMA_ID('dbo'))
                AND NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'AgentUsers' AND schema_id = SCHEMA_ID('dbo'))
                BEGIN
                    EXEC sp_rename 'dbo.Users', 'AgentUsers';
                    IF EXISTS (SELECT * FROM sys.indexes WHERE name = 'PK_Users' AND object_id = OBJECT_ID('dbo.AgentUsers'))
                        EXEC sp_rename 'dbo.PK_Users', 'PK_AgentUsers', 'OBJECT';
                    IF EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Users_Email' AND object_id = OBJECT_ID('dbo.AgentUsers'))
                        EXEC sp_rename 'dbo.AgentUsers.IX_Users_Email', 'IX_AgentUsers_Email', 'INDEX';
                END
            ");

            migrationBuilder.CreateTable(
                name: "Businesses",
                columns: table => new
                {
                    BusinessId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BusinessName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FullAddress = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    City = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Country = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AddressHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    BusinessModel = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CoreProductsOrServices = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TargetAudience = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    BusinessToneOrStyle = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PopularityOrSize = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SpecializationKeywords = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    BusinessType = table.Column<int>(type: "int", nullable: false),
                    GeoapifyPlaceId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Latitude = table.Column<double>(type: "float", nullable: true),
                    Longitude = table.Column<double>(type: "float", nullable: true),
                    Website = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    DataSource = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SimilarBusinessName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    IsRealData = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastSearchedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SearchCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Businesses", x => x.BusinessId);
                });

            migrationBuilder.CreateTable(
                name: "Products",
                columns: table => new
                {
                    ProductId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BusinessId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PriceGBP = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    IsPopular = table.Column<bool>(type: "bit", nullable: false),
                    IsWelcomeGiftEligible = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Products", x => x.ProductId);
                    table.ForeignKey(
                        name: "FK_Products_Businesses_BusinessId",
                        column: x => x.BusinessId,
                        principalTable: "Businesses",
                        principalColumn: "BusinessId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Services",
                columns: table => new
                {
                    ServiceId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BusinessId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PriceGBP = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    IsPopular = table.Column<bool>(type: "bit", nullable: false),
                    IsDiscountable = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Services", x => x.ServiceId);
                    table.ForeignKey(
                        name: "FK_Services_Businesses_BusinessId",
                        column: x => x.BusinessId,
                        principalTable: "Businesses",
                        principalColumn: "BusinessId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TierRewards",
                columns: table => new
                {
                    TierRewardId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BusinessId = table.Column<int>(type: "int", nullable: false),
                    Tier = table.Column<int>(type: "int", nullable: false),
                    RequiredTokens = table.Column<int>(type: "int", nullable: false),
                    Reasoning = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FallbackDiscountPercentage = table.Column<decimal>(type: "decimal(5,2)", nullable: true),
                    FallbackDescription = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    FallbackReasoning = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TierRewards", x => x.TierRewardId);
                    table.ForeignKey(
                        name: "FK_TierRewards_Businesses_BusinessId",
                        column: x => x.BusinessId,
                        principalTable: "Businesses",
                        principalColumn: "BusinessId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WelcomeGifts",
                columns: table => new
                {
                    WelcomeGiftId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BusinessId = table.Column<int>(type: "int", nullable: false),
                    ItemName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ItemPriceGBP = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Reasoning = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsFree = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WelcomeGifts", x => x.WelcomeGiftId);
                    table.ForeignKey(
                        name: "FK_WelcomeGifts_Businesses_BusinessId",
                        column: x => x.BusinessId,
                        principalTable: "Businesses",
                        principalColumn: "BusinessId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TierRewardItems",
                columns: table => new
                {
                    TierRewardItemId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TierRewardId = table.Column<int>(type: "int", nullable: false),
                    ItemName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ItemValueGBP = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    CanBeGivenFree = table.Column<bool>(type: "bit", nullable: false),
                    ReasoningForSelection = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TierRewardItems", x => x.TierRewardItemId);
                    table.ForeignKey(
                        name: "FK_TierRewardItems_TierRewards_TierRewardId",
                        column: x => x.TierRewardId,
                        principalTable: "TierRewards",
                        principalColumn: "TierRewardId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Businesses_AddressHash",
                table: "Businesses",
                column: "AddressHash");

            migrationBuilder.CreateIndex(
                name: "IX_Businesses_Category_City",
                table: "Businesses",
                columns: new[] { "Category", "City" });

            migrationBuilder.CreateIndex(
                name: "IX_Products_BusinessId",
                table: "Products",
                column: "BusinessId");

            migrationBuilder.CreateIndex(
                name: "IX_Services_BusinessId",
                table: "Services",
                column: "BusinessId");

            migrationBuilder.CreateIndex(
                name: "IX_TierRewardItems_TierRewardId",
                table: "TierRewardItems",
                column: "TierRewardId");

            migrationBuilder.CreateIndex(
                name: "IX_TierRewards_BusinessId",
                table: "TierRewards",
                column: "BusinessId");

            migrationBuilder.CreateIndex(
                name: "IX_WelcomeGifts_BusinessId",
                table: "WelcomeGifts",
                column: "BusinessId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Products");

            migrationBuilder.DropTable(
                name: "Services");

            migrationBuilder.DropTable(
                name: "TierRewardItems");

            migrationBuilder.DropTable(
                name: "WelcomeGifts");

            migrationBuilder.DropTable(
                name: "TierRewards");

            migrationBuilder.DropTable(
                name: "Businesses");

            migrationBuilder.DropPrimaryKey(
                name: "PK_AgentUsers",
                table: "AgentUsers");

            migrationBuilder.RenameTable(
                name: "AgentUsers",
                newName: "Users");

            migrationBuilder.RenameIndex(
                name: "IX_AgentUsers_Email",
                table: "Users",
                newName: "IX_Users_Email");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Users",
                table: "Users",
                column: "UserId");
        }
    }
}
