using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UsersApi.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddPiiEncryption : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Step 1: Add the new columns before dropping the old one.
        // EmailCiphertext is nullable here so existing rows don't fail the constraint.
        // The encrypt-existing-data.sh script fills it before we enforce NOT NULL.
        migrationBuilder.AddColumn<string>(
            name: "EmailCiphertext",
            table: "Users",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "SearchableEmail",
            table: "Users",
            type: "character varying(64)",
            maxLength: 64,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "PhoneNumberCiphertext",
            table: "Users",
            type: "text",
            nullable: true);

        // Step 2: After encrypt-existing-data.sh has run and backfilled all rows,
        // apply this second migration to enforce NOT NULL and add the unique index.
        // Split into two migrations if you need a window between steps.
        migrationBuilder.AlterColumn<string>(
            name: "EmailCiphertext",
            table: "Users",
            type: "text",
            nullable: false,
            defaultValue: "",
            oldClrType: typeof(string),
            oldType: "text",
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "SearchableEmail",
            table: "Users",
            type: "character varying(64)",
            maxLength: 64,
            nullable: false,
            defaultValue: "",
            oldClrType: typeof(string),
            oldType: "character varying(64)",
            oldMaxLength: 64,
            oldNullable: true);

        // Step 3: Unique index on SearchableEmail, scoped to active users only.
        migrationBuilder.CreateIndex(
            name: "IX_Users_SearchableEmail",
            table: "Users",
            column: "SearchableEmail",
            unique: true,
            filter: "\"IsDeleted\" = false");

        // Step 4: Drop the old plaintext Email column and its index.
        migrationBuilder.DropIndex(
            name: "IX_Users_Email",
            table: "Users");

        migrationBuilder.DropColumn(
            name: "Email",
            table: "Users");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "Email",
            table: "Users",
            type: "character varying(320)",
            maxLength: 320,
            nullable: false,
            defaultValue: "");

        migrationBuilder.CreateIndex(
            name: "IX_Users_Email",
            table: "Users",
            column: "Email",
            unique: true,
            filter: "\"IsDeleted\" = false");

        migrationBuilder.DropIndex(
            name: "IX_Users_SearchableEmail",
            table: "Users");

        migrationBuilder.DropColumn(name: "EmailCiphertext", table: "Users");
        migrationBuilder.DropColumn(name: "SearchableEmail", table: "Users");
        migrationBuilder.DropColumn(name: "PhoneNumberCiphertext", table: "Users");
    }
}
