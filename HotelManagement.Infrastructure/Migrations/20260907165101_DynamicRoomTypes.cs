using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HotelManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DynamicRoomTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Rooms.RoomType: integer enum (1/2/3) -> free-text type name.
            migrationBuilder.Sql(@"
                ALTER TABLE ""Rooms"" ADD COLUMN ""RoomType_new"" text NOT NULL DEFAULT '';
                UPDATE ""Rooms"" SET ""RoomType_new"" = CASE ""RoomType""
                    WHEN 1 THEN 'Standard'
                    WHEN 2 THEN 'Deluxe'
                    WHEN 3 THEN 'Suite'
                    ELSE 'Standard'
                END;
                ALTER TABLE ""Rooms"" DROP COLUMN ""RoomType"";
                ALTER TABLE ""Rooms"" RENAME COLUMN ""RoomType_new"" TO ""RoomType"";
                ALTER TABLE ""Rooms"" ALTER COLUMN ""RoomType"" DROP DEFAULT;
            ");

            migrationBuilder.CreateTable(
                name: "RoomTypes",
                columns: table => new
                {
                    Id = table.Column<System.Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    TenantId = table.Column<System.Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<System.DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<System.DateTime>(type: "timestamp without time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoomTypes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RoomTypes_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_RoomTypes_Name_TenantId",
                table: "RoomTypes",
                columns: new[] { "Name", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RoomTypes_TenantId",
                table: "RoomTypes",
                column: "TenantId");

            // Give every existing hotel the three default room types.
            migrationBuilder.Sql(@"
                INSERT INTO ""RoomTypes"" (""Id"", ""Name"", ""TenantId"", ""CreatedAt"", ""UpdatedAt"", ""IsDeleted"")
                SELECT gen_random_uuid(), v.name, t.""Id"", now(), now(), false
                FROM ""Tenants"" t
                CROSS JOIN (VALUES ('Standard'), ('Deluxe'), ('Suite')) AS v(name)
                ON CONFLICT (""Name"", ""TenantId"") DO NOTHING;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "RoomTypes");

            migrationBuilder.Sql(@"
                ALTER TABLE ""Rooms"" ADD COLUMN ""RoomType_old"" integer NOT NULL DEFAULT 1;
                UPDATE ""Rooms"" SET ""RoomType_old"" = CASE ""RoomType""
                    WHEN 'Standard' THEN 1
                    WHEN 'Deluxe' THEN 2
                    WHEN 'Suite' THEN 3
                    ELSE 1
                END;
                ALTER TABLE ""Rooms"" DROP COLUMN ""RoomType"";
                ALTER TABLE ""Rooms"" RENAME COLUMN ""RoomType_old"" TO ""RoomType"";
                ALTER TABLE ""Rooms"" ALTER COLUMN ""RoomType"" DROP DEFAULT;
            ");
        }
    }
}
