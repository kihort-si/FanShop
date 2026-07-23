using FanShop.Models;
using Microsoft.EntityFrameworkCore;

namespace FanShop.Services;

public class SalaryManagementService
{
    private readonly AppDbContext _context;
    private readonly SalaryService _salaryService;

    public SalaryManagementService(
        AppDbContext context,
        SalaryService salaryService)
    {
        _context = context;
        _salaryService = salaryService;
    }

    public void ChangeSalary(
        int positionId,
        decimal newSalary,
        DateTime startDate)
    {
        var currentSalary = _context.SalaryHistories
            .Where(x =>
                x.PositionID == positionId &&
                x.EndDate == null)
            .FirstOrDefault();

        if (currentSalary != null)
        {
            currentSalary.EndDate = startDate.AddDays(-1);
        }

        _context.SalaryHistories.Add(new SalaryHistory
        {
            PositionID = positionId,
            Salary = newSalary,
            StartDate = startDate
        });

        _context.SaveChanges();

        var recalculationService =
            new SalaryRecalculationService(
                _context,
                _salaryService);

        recalculationService.RecalculatePositionSalary(
            positionId,
            startDate);
    }
}