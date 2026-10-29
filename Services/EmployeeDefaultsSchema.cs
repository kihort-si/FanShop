using Microsoft.EntityFrameworkCore;

namespace FanShop.Services;

public static class EmployeeDefaultsSchema
{
    public static async Task EnsureAsync(AppDbContext context)
    {
        var connection = context.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open) await connection.OpenAsync();
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "PRAGMA table_info(Employees);";
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync()) columns.Add(reader.GetString(1));
        }
        foreach (var (name, definition) in new[]
        {
            ("HasPermanentPass", "INTEGER NOT NULL DEFAULT 0"),
            ("ExcludeFromSalaryByDefault", "INTEGER NOT NULL DEFAULT 0"),
            ("DefaultShopID", "INTEGER NULL"), ("DefaultPositionID", "INTEGER NULL")
        })
        {
            if (columns.Contains(name)) continue;
            await using var command = connection.CreateCommand();
            command.CommandText = $"ALTER TABLE Employees ADD COLUMN {name} {definition};";
            await command.ExecuteNonQueryAsync();
        }
    }
}
