using FanShop.Models;
using Microsoft.EntityFrameworkCore;

namespace FanShop.Services;

public class AppDbContext : DbContext
{
    public DbSet<Employee> Employees { get; set; }
    
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlite("Data Source=app.db");
    }
}