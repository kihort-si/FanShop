namespace FanShop.Services;

public class SalaryService
{
    private readonly AppDbContext _context;

    public SalaryService(AppDbContext context)
    {
        _context = context;
    }

    public decimal GetSalaryForShift(
        int positionId,
        DateTime workDate,
        string workDuration)
    {
        var salaryRecord = _context.SalaryHistories
            .Where(x =>
                x.PositionID == positionId &&
                x.StartDate <= workDate &&
                (x.EndDate == null || x.EndDate >= workDate))
            .OrderByDescending(x => x.StartDate)
            .FirstOrDefault();

        if (salaryRecord == null)
            return 0;

        return workDuration switch
        {
            "Целый день" => salaryRecord.Salary,
            "Полдня" => salaryRecord.Salary / 2m,
            _ => 0
        };
    }
}