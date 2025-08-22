using System.Collections.ObjectModel;
using FanShop.Models;
using Microsoft.EntityFrameworkCore;

namespace FanShop.Services
{
    public class StatisticsService
    {
        public int GetTotalEmployeesCount(int year, int month)
        {
            using var context = new AppDbContext();
            var (firstDay, lastDay) = GetMonthBounds(year, month);
            
            return context.WorkDays
                .Where(wd => wd.Date >= firstDay && wd.Date <= lastDay)
                .SelectMany(wd => wd.WorkDayEmployees)
                .Select(wde => wde.EmployeeID)
                .Distinct()
                .Count();
        }

        public int GetWorkDaysCount(int year, int month)
        {
            using var context = new AppDbContext();
            var (firstDay, lastDay) = GetMonthBounds(year, month);
            
            return context.WorkDays
                .Where(wd => wd.Date >= firstDay && wd.Date <= lastDay)
                .Where(wd => wd.WorkDayEmployees.Any())
                .Count();
        }

        public int GetTotalShiftCount(int year, int month)
        {
            using var context = new AppDbContext();
            var (firstDay, lastDay) = GetMonthBounds(year, month);

            return context.WorkDays
                .Where(wd => wd.Date >= firstDay && wd.Date <= lastDay)
                .SelectMany(wd => wd.WorkDayEmployees)
                .Count();
        }

        public string GetTotalSalary(int year, int month)
        {
            using var context = new AppDbContext();
            var (firstDay, lastDay) = GetMonthBounds(year, month);
            var settings = Settings.Load();
        
            var workDayEmployees = context.WorkDays
                .Where(wd => wd.Date >= firstDay && wd.Date <= lastDay)
                .SelectMany(wd => wd.WorkDayEmployees)
                .ToList();
            
            var total = workDayEmployees
                .Sum(wde => wde.WorkDuration == "Целый день" ? (double)settings.DailySalary : (double)settings.DailySalary / 2);
        
            return $"{total:N0}₽";
        }

        public ObservableCollection<EmployeeStatistic> GetEmployeeStatistics(int year, int month)
        {
            using var context = new AppDbContext();
            var (firstDay, lastDay) = GetMonthBounds(year, month);
            var settings = Settings.Load();
        
            var workDayEmployees = context.WorkDays
                .Where(wd => wd.Date >= firstDay && wd.Date <= lastDay)
                .SelectMany(wd => wd.WorkDayEmployees)
                .Include(wde => wde.Employee)
                .ToList();
        
            var statistics = workDayEmployees
                .GroupBy(wde => new { wde.Employee.FirstName, wde.Employee.Surname })
                .Select(g => new
                {
                    EmployeeName = $"{g.Key.FirstName} {g.Key.Surname}",
                    WorkDaysCount = g.Count(),
                    SalaryAmount = g.Sum(wde => wde.WorkDuration == "Целый день" ? settings.DailySalary : settings.DailySalary / 2)
                })
                .OrderByDescending(x => x.SalaryAmount)
                .Select(x => new EmployeeStatistic
                {
                    EmployeeName = x.EmployeeName,
                    WorkDaysCount = x.WorkDaysCount,
                    TotalSalary = $"{x.SalaryAmount:N0}₽"
                })
                .ToList();
        
            return new ObservableCollection<EmployeeStatistic>(statistics);
        }

        private static (DateTime firstDay, DateTime lastDay) GetMonthBounds(int year, int month)
        {
            var firstDay = new DateTime(year, month, 1);
            var lastDay = firstDay.AddMonths(1).AddDays(-1);
            return (firstDay, lastDay);
        }
    }

    public class EmployeeStatistic
    {
        public string EmployeeName { get; set; }
        public int WorkDaysCount { get; set; }
        public string TotalSalary { get; set; }
    }
}