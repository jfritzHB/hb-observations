using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FieldApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFieldItemDrafts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FieldItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientDraftId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    LifecycleState = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AreaId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AreaPathSnapshot = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    LocationDetail = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    TradeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TradeNameSnapshot = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ResponsibleCompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResponsibleCompanyNameSnapshot = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Priority = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FieldItems", x => x.Id);
                    table.CheckConstraint("CK_FieldItems_PublishedHasLocation", "[LifecycleState] <> 'Published' OR [AreaId] IS NOT NULL OR [LocationDetail] IS NOT NULL");
                    table.ForeignKey(
                        name: "FK_FieldItems_Areas_ProjectId_AreaId",
                        columns: x => new { x.ProjectId, x.AreaId },
                        principalTable: "Areas",
                        principalColumns: new[] { "ProjectId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FieldItems_Companies_ResponsibleCompanyId",
                        column: x => x.ResponsibleCompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FieldItems_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FieldItems_Trades_TradeId",
                        column: x => x.TradeId,
                        principalTable: "Trades",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FieldItems_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IdempotencyRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Scope = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Key = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    RequestFingerprint = table.Column<string>(type: "char(64)", unicode: false, fixedLength: true, maxLength: 64, nullable: false),
                    ResourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IdempotencyRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IdempotencyRecords_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FieldItemPhotos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FieldItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    BlobKey = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    ThumbnailBlobKey = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    MediaType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ByteLength = table.Column<long>(type: "bigint", nullable: false),
                    Width = table.Column<int>(type: "int", nullable: true),
                    Height = table.Column<int>(type: "int", nullable: true),
                    Sha256 = table.Column<string>(type: "char(44)", unicode: false, fixedLength: true, maxLength: 44, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CapturedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ReservedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ReservationExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UploadedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    FinalizedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UploadedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsPrimary = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FieldItemPhotos", x => x.Id);
                    table.CheckConstraint("CK_FieldItemPhotos_FinalizedHasMetadata", "[Status] <> 'Finalized' OR ([Width] > 0 AND [Height] > 0 AND [ThumbnailBlobKey] IS NOT NULL AND [FinalizedAt] IS NOT NULL)");
                    table.CheckConstraint("CK_FieldItemPhotos_PositiveSize", "[ByteLength] > 0");
                    table.ForeignKey(
                        name: "FK_FieldItemPhotos_FieldItems_FieldItemId",
                        column: x => x.FieldItemId,
                        principalTable: "FieldItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FieldItemPhotos_Users_UploadedByUserId",
                        column: x => x.UploadedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FieldItemPhotos_BlobKey",
                table: "FieldItemPhotos",
                column: "BlobKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FieldItemPhotos_UploadedByUserId",
                table: "FieldItemPhotos",
                column: "UploadedByUserId");

            migrationBuilder.CreateIndex(
                name: "UX_FieldItemPhotos_OnePrimary",
                table: "FieldItemPhotos",
                column: "FieldItemId",
                unique: true,
                filter: "[IsPrimary] = 1 AND [Status] <> 'Abandoned'");

            migrationBuilder.CreateIndex(
                name: "IX_FieldItems_CreatedByUserId",
                table: "FieldItems",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FieldItems_ProjectId_AreaId",
                table: "FieldItems",
                columns: new[] { "ProjectId", "AreaId" });

            migrationBuilder.CreateIndex(
                name: "IX_FieldItems_ProjectId_CreatedByUserId_ClientDraftId",
                table: "FieldItems",
                columns: new[] { "ProjectId", "CreatedByUserId", "ClientDraftId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FieldItems_ProjectId_LifecycleState",
                table: "FieldItems",
                columns: new[] { "ProjectId", "LifecycleState" });

            migrationBuilder.CreateIndex(
                name: "IX_FieldItems_ResponsibleCompanyId",
                table: "FieldItems",
                column: "ResponsibleCompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_FieldItems_TradeId",
                table: "FieldItems",
                column: "TradeId");

            migrationBuilder.CreateIndex(
                name: "IX_IdempotencyRecords_UserId_Scope_Key",
                table: "IdempotencyRecords",
                columns: new[] { "UserId", "Scope", "Key" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FieldItemPhotos");

            migrationBuilder.DropTable(
                name: "IdempotencyRecords");

            migrationBuilder.DropTable(
                name: "FieldItems");
        }
    }
}
