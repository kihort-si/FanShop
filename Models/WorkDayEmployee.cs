using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace FanShop.Models;

public class WorkDayEmployee
{
    [Key]
    public int WorkDayEmployeeID { get; set; }
    
    public int WorkDayID { get; set; }
    public WorkDay WorkDay { get; set; }
    
    public int EmployeeID { get; set; }
    public Employee Employee { get; set; }
    
    [Required]
    public string WorkDuration { get; set; }

    public bool IncludeInPass { get; set; } = true;
    public bool IncludeInSalary { get; set; } = true;
    public int PositionID { get; set; }
    public Position Position { get; set; } = null!;
    [Precision(18, 2)]
    public decimal SalaryAtMoment { get; set; }
}