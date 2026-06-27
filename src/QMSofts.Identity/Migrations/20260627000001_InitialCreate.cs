using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace QMSofts.Identity.Migrations;

public partial class InitialCreate : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(name: "identity");

        migrationBuilder.CreateTable(
            name: "Roles",
            schema: "identity",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                Description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_Roles", x => x.Id));

        migrationBuilder.CreateTable(
            name: "Users",
            schema: "identity",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                NormalizedEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                PasswordHash = table.Column<string>(type: "text", nullable: false),
                IsActive = table.Column<bool>(type: "boolean", nullable: false),
                TokenVersion = table.Column<int>(type: "integer", nullable: false),
                FailedLoginCount = table.Column<int>(type: "integer", nullable: false),
                LockedUntil = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_Users", x => x.Id));

        migrationBuilder.CreateTable(
            name: "AuthAuditRecords",
            schema: "identity",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                EventType = table.Column<int>(type: "integer", nullable: false),
                OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: true),
                Identifier = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                AppKey = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                IpAddress = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                UserAgent = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                Detail = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_AuthAuditRecords", x => x.Id));

        migrationBuilder.CreateTable(
            name: "AppEntitlements",
            schema: "identity",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                AppKey = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                GrantedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AppEntitlements", x => x.Id);
                table.ForeignKey(
                    name: "FK_AppEntitlements_Users_UserId",
                    column: x => x.UserId,
                    principalSchema: "identity",
                    principalTable: "Users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "RefreshTokens",
            schema: "identity",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                TokenHash = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_RefreshTokens", x => x.Id);
                table.ForeignKey(
                    name: "FK_RefreshTokens_Users_UserId",
                    column: x => x.UserId,
                    principalSchema: "identity",
                    principalTable: "Users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "UserRoles",
            schema: "identity",
            columns: table => new
            {
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                RoleId = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_UserRoles", x => new { x.UserId, x.RoleId });
                table.ForeignKey(
                    name: "FK_UserRoles_Roles_RoleId",
                    column: x => x.RoleId,
                    principalSchema: "identity",
                    principalTable: "Roles",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_UserRoles_Users_UserId",
                    column: x => x.UserId,
                    principalSchema: "identity",
                    principalTable: "Users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_AppEntitlements_UserId_AppKey",
            schema: "identity",
            table: "AppEntitlements",
            columns: new[] { "UserId", "AppKey" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_AuthAuditRecords_EventType",
            schema: "identity", table: "AuthAuditRecords", column: "EventType");

        migrationBuilder.CreateIndex(
            name: "IX_AuthAuditRecords_OccurredAt",
            schema: "identity", table: "AuthAuditRecords", column: "OccurredAt");

        migrationBuilder.CreateIndex(
            name: "IX_AuthAuditRecords_UserId",
            schema: "identity", table: "AuthAuditRecords", column: "UserId");

        migrationBuilder.CreateIndex(
            name: "IX_RefreshTokens_TokenHash",
            schema: "identity", table: "RefreshTokens", column: "TokenHash");

        migrationBuilder.CreateIndex(
            name: "IX_RefreshTokens_UserId",
            schema: "identity", table: "RefreshTokens", column: "UserId");

        migrationBuilder.CreateIndex(
            name: "IX_Roles_Name",
            schema: "identity", table: "Roles", column: "Name", unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_UserRoles_RoleId",
            schema: "identity", table: "UserRoles", column: "RoleId");

        migrationBuilder.CreateIndex(
            name: "IX_Users_NormalizedEmail",
            schema: "identity", table: "Users", column: "NormalizedEmail", unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "AppEntitlements", schema: "identity");
        migrationBuilder.DropTable(name: "AuthAuditRecords", schema: "identity");
        migrationBuilder.DropTable(name: "RefreshTokens", schema: "identity");
        migrationBuilder.DropTable(name: "UserRoles", schema: "identity");
        migrationBuilder.DropTable(name: "Roles", schema: "identity");
        migrationBuilder.DropTable(name: "Users", schema: "identity");
    }
}
