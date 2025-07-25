using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Angor.Contexts.Data.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NostrUsers",
                columns: table => new
                {
                    PubKey = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    ProfileEventId = table.Column<string>(type: "TEXT", nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    About = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Picture = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Banner = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Website = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Nip05 = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    IsVerified = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NostrUsers", x => x.PubKey);
                });

            migrationBuilder.CreateTable(
                name: "ProjectKeys",
                columns: table => new
                {
                    FounderKey = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    WalletId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Index = table.Column<int>(type: "INTEGER", nullable: false),
                    ProjectId = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    NostrPubKey = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    FounderRecoveryKey = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectKeys", x => x.FounderKey);
                });

            migrationBuilder.CreateTable(
                name: "NostrEvents",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    PubKey = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false),
                    Content = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Signature = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NostrEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NostrEvents_NostrUsers_PubKey",
                        column: x => x.PubKey,
                        principalTable: "NostrUsers",
                        principalColumn: "PubKey",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NostrTags",
                columns: table => new
                {
                    Name = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    EventId = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Content = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NostrTags", x => x.Name);
                    table.ForeignKey(
                        name: "FK_NostrTags_NostrEvents_EventId",
                        column: x => x.EventId,
                        principalTable: "NostrEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Projects",
                columns: table => new
                {
                    ProjectId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    NostrPubKey = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ProjectReceiveAddress = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    TargetAmount = table.Column<long>(type: "INTEGER", precision: 18, scale: 8, nullable: false),
                    FundingStartDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FundingEndDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PenaltyDays = table.Column<long>(type: "INTEGER", nullable: false),
                    ExpiryDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ProjectInfoEventId = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LeadInvestorsThreshold = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Projects", x => x.ProjectId);
                    table.ForeignKey(
                        name: "FK_Projects_NostrEvents_ProjectInfoEventId",
                        column: x => x.ProjectInfoEventId,
                        principalTable: "NostrEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Projects_NostrUsers_NostrPubKey",
                        column: x => x.NostrPubKey,
                        principalTable: "NostrUsers",
                        principalColumn: "PubKey",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProjectSecretHash",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProjectId = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    SecretHash = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectSecretHash", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectSecretHash_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "ProjectId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProjectStage",
                columns: table => new
                {
                    ProjectId = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    StageIndex = table.Column<int>(type: "INTEGER", nullable: false),
                    AmountToRelease = table.Column<decimal>(type: "TEXT", precision: 18, scale: 8, nullable: false),
                    ReleaseDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ReleasedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectStage", x => new { x.ProjectId, x.StageIndex });
                    table.ForeignKey(
                        name: "FK_ProjectStage_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "ProjectId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NostrEvents_CreatedAt",
                table: "NostrEvents",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_NostrEvents_Kind",
                table: "NostrEvents",
                column: "Kind");

            migrationBuilder.CreateIndex(
                name: "IX_NostrEvents_PubKey",
                table: "NostrEvents",
                column: "PubKey");

            migrationBuilder.CreateIndex(
                name: "IX_NostrTags_EventId",
                table: "NostrTags",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_NostrUsers_CreatedAt",
                table: "NostrUsers",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_NostrUsers_DisplayName",
                table: "NostrUsers",
                column: "DisplayName");

            migrationBuilder.CreateIndex(
                name: "IX_NostrUsers_Nip05",
                table: "NostrUsers",
                column: "Nip05");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectKeys_NostrPubKey",
                table: "ProjectKeys",
                column: "NostrPubKey");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectKeys_ProjectId",
                table: "ProjectKeys",
                column: "ProjectId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectKeys_WalletId",
                table: "ProjectKeys",
                column: "WalletId");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_NostrPubKey",
                table: "Projects",
                column: "NostrPubKey");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_ProjectInfoEventId",
                table: "Projects",
                column: "ProjectInfoEventId");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_ProjectReceiveAddress",
                table: "Projects",
                column: "ProjectReceiveAddress");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectSecretHash_CreatedAt",
                table: "ProjectSecretHash",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectSecretHash_ProjectId",
                table: "ProjectSecretHash",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectSecretHash_ProjectId_SecretHash",
                table: "ProjectSecretHash",
                columns: new[] { "ProjectId", "SecretHash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectStage_CreatedAt",
                table: "ProjectStage",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectStage_ProjectId",
                table: "ProjectStage",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectStage_ReleaseDate",
                table: "ProjectStage",
                column: "ReleaseDate");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NostrTags");

            migrationBuilder.DropTable(
                name: "ProjectKeys");

            migrationBuilder.DropTable(
                name: "ProjectSecretHash");

            migrationBuilder.DropTable(
                name: "ProjectStage");

            migrationBuilder.DropTable(
                name: "Projects");

            migrationBuilder.DropTable(
                name: "NostrEvents");

            migrationBuilder.DropTable(
                name: "NostrUsers");
        }
    }
}
