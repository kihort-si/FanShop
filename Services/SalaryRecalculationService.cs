using Microsoft.EntityFrameworkCore;

namespace FanShop.Services;

public class SalaryRecalculationService
{
    private readonly AppDbContext _context;
    private readonly SalaryService _salaryService;

    public SalaryRecalculationService(
        AppDbContext context,
        SalaryService salaryService)
    {
        _context = context;
        _salaryService = salaryService;
    }

    public void RecalculatePositionSalary(
        int positionId,
        DateTime fromDate)
    {
        var shifts = _context.WorkDayEmployee
            .Include(x => x.WorkDay)
            .Where(x =>
                x.PositionID == positionId &&
                x.WorkDay.Date >= fromDate)
            .ToList();

        foreach (var shift in shifts)
        {
            shift.SalaryAtMoment =
                _salaryService.GetSalaryForShift(
                    shift.PositionID,
                    shift.WorkDay.Date,
                    shift.WorkDuration);
        }

        _context.SaveChanges();
    }
}