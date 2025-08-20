using FanShop.Models;
using Microsoft.EntityFrameworkCore;

namespace FanShop.Services;

public class AppDbContext : DbContext
{
    public DbSet<Employee> Employees { get; set; }
    public DbSet<WorkDay> WorkDays { get; set; }
    public DbSet<DayTask> DayTasks { get; set; }
    public DbSet<TaskCategory> TaskCategories { get; set; }
    
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlite("Data Source=FanShop.db");
    }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<WorkDayEmployee>()
            .HasKey(wde => wde.WorkDayEmployeeID);
    
        modelBuilder.Entity<WorkDayEmployee>()
            .HasOne(wde => wde.WorkDay)
            .WithMany(w => w.WorkDayEmployees)
            .HasForeignKey(wde => wde.WorkDayID);
    
        modelBuilder.Entity<WorkDayEmployee>()
            .HasOne(wde => wde.Employee)
            .WithMany(e => e.WorkDayEmployees)
            .HasForeignKey(wde => wde.EmployeeID);
        
        modelBuilder.Entity<DayTask>()
            .HasOne(t => t.Category)
            .WithMany(c => c.Tasks)
            .HasForeignKey(t => t.TaskCategoryID)
            .OnDelete(DeleteBehavior.SetNull);
    
        base.OnModelCreating(modelBuilder);
    }
}