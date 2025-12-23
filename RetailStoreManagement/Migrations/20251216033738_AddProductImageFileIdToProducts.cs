using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace RetailStoreManagement.Migrations
{
    /// <inheritdoc />
    public partial class AddProductImageFileIdToProducts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Chỉ thêm cột image_file_id vào bảng Products, không tạo lại toàn bộ schema.
            // Dùng IF NOT EXISTS để an toàn khi cột đã tồn tại (ví dụ đã thêm trước đó trên Neon).
            migrationBuilder.Sql(
                "ALTER TABLE \"Products\" " +
                "ADD COLUMN IF NOT EXISTS \"image_file_id\" character varying(255) NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Rollback: chỉ xóa cột image_file_id nếu tồn tại.
            migrationBuilder.Sql(
                "ALTER TABLE \"Products\" " +
                "DROP COLUMN IF EXISTS \"image_file_id\";");
        }
    }
}
