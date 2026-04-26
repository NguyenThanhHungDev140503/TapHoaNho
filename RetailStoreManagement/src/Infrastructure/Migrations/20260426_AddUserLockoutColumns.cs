using Microsoft.EntityFrameworkCore.Migrations;

namespace Infrastructure.Migrations;

[Migration("20260426_AddUserLockoutColumns")]
public partial class AddUserLockoutColumns : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
            ALTER TABLE users
            ADD COLUMN IF NOT EXISTS failed_login_attempts INTEGER NOT NULL DEFAULT 0,
            ADD COLUMN IF NOT EXISTS locked_until TIMESTAMP WITH TIME ZONE NULL
        ");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
            ALTER TABLE users
            DROP COLUMN IF EXISTS failed_login_attempts,
            DROP COLUMN IF EXISTS locked_until
        ");
    }
}
