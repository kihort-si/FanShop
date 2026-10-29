using FanShop.Models;

namespace FanShop.Services;

public static class EmployeeShiftDefaults
{
    public static bool RequiresPass(Employee employee, DateTime date) =>
        date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday || !employee.HasPermanentPass;

    public static WorkDayEmployee Create(Employee employee, int workDayId, DateTime date,
        Position position, string duration, SalaryService salaryService) => new()
    {
        EmployeeID = employee.EmployeeID,
        WorkDayID = workDayId,
        PositionID = position.PositionID,
        WorkDuration = duration,
        IncludeInPass = RequiresPass(employee, date),
        IncludeInSalary = !employee.ExcludeFromSalaryByDefault,
        SalaryAtMoment = salaryService.GetSalaryForShift(position.PositionID, date, duration)
    };
}

public sealed record EmployeeShiftSelection(Employee Employee, string WorkDuration, Position Position);
