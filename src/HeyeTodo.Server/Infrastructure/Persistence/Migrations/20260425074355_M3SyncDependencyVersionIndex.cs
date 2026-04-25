using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HeyeTodo.Server.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class M3SyncDependencyVersionIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_TaskDependencies_ServerVersion",
                table: "TaskDependencies",
                column: "ServerVersion");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TaskDependencies_ServerVersion",
                table: "TaskDependencies");
        }
    }
}
