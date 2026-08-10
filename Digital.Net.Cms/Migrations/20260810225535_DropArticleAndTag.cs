using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Digital.Net.Cms.Migrations
{
    /// <inheritdoc />
    public partial class DropArticleAndTag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ArticleMedia",
                schema: "digital_net_cms");

            migrationBuilder.DropTable(
                name: "ArticleRelated",
                schema: "digital_net_cms");

            migrationBuilder.DropTable(
                name: "ArticleTag",
                schema: "digital_net_cms");

            migrationBuilder.DropTable(
                name: "Article",
                schema: "digital_net_cms");

            migrationBuilder.DropTable(
                name: "Tag",
                schema: "digital_net_cms");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Article",
                schema: "digital_net_cms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PageId = table.Column<Guid>(type: "uuid", nullable: true),
                    Content = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Description = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    PublishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Slug = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Article", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Article_Page_PageId",
                        column: x => x.PageId,
                        principalSchema: "digital_net_cms",
                        principalTable: "Page",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Tag",
                schema: "digital_net_cms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Color = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tag", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ArticleMedia",
                schema: "digital_net_cms",
                columns: table => new
                {
                    ParentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChildId = table.Column<Guid>(type: "uuid", nullable: false),
                    Label = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArticleMedia", x => new { x.ParentId, x.ChildId });
                    table.ForeignKey(
                        name: "FK_ArticleMedia_Article_ParentId",
                        column: x => x.ParentId,
                        principalSchema: "digital_net_cms",
                        principalTable: "Article",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ArticleMedia_Media_ChildId",
                        column: x => x.ChildId,
                        principalSchema: "digital_net_cms",
                        principalTable: "Media",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ArticleRelated",
                schema: "digital_net_cms",
                columns: table => new
                {
                    ParentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChildId = table.Column<Guid>(type: "uuid", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArticleRelated", x => new { x.ParentId, x.ChildId });
                    table.ForeignKey(
                        name: "FK_ArticleRelated_Article_ChildId",
                        column: x => x.ChildId,
                        principalSchema: "digital_net_cms",
                        principalTable: "Article",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ArticleRelated_Article_ParentId",
                        column: x => x.ParentId,
                        principalSchema: "digital_net_cms",
                        principalTable: "Article",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ArticleTag",
                schema: "digital_net_cms",
                columns: table => new
                {
                    ParentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChildId = table.Column<Guid>(type: "uuid", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArticleTag", x => new { x.ParentId, x.ChildId });
                    table.ForeignKey(
                        name: "FK_ArticleTag_Article_ParentId",
                        column: x => x.ParentId,
                        principalSchema: "digital_net_cms",
                        principalTable: "Article",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ArticleTag_Tag_ChildId",
                        column: x => x.ChildId,
                        principalSchema: "digital_net_cms",
                        principalTable: "Tag",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Article_PageId",
                schema: "digital_net_cms",
                table: "Article",
                column: "PageId");

            migrationBuilder.CreateIndex(
                name: "IX_Article_Slug",
                schema: "digital_net_cms",
                table: "Article",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ArticleMedia_ChildId",
                schema: "digital_net_cms",
                table: "ArticleMedia",
                column: "ChildId");

            migrationBuilder.CreateIndex(
                name: "IX_ArticleMedia_ParentId_Label",
                schema: "digital_net_cms",
                table: "ArticleMedia",
                columns: new[] { "ParentId", "Label" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ArticleRelated_ChildId",
                schema: "digital_net_cms",
                table: "ArticleRelated",
                column: "ChildId");

            migrationBuilder.CreateIndex(
                name: "IX_ArticleTag_ChildId",
                schema: "digital_net_cms",
                table: "ArticleTag",
                column: "ChildId");
        }
    }
}
