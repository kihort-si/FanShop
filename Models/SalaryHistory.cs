namespace FanShop.Models;

public class SalaryHistory
{
    public int SalaryHistoryID { get; set; }

    public int PositionID { get; set; }
    public Position Position { get; set; } = null!;

    public decimal Salary { get; set; }

    public DateTime StartDate { get; set; }

    public DateTime? EndDate { get; set; }
}