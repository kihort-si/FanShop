using System.ComponentModel.DataAnnotations;

namespace FanShop.Models;

public class WorkDayEmployee
{
    [Key]
    public int WorkDayEmployeeID { get; set; }
    [Required]
    public required int WorkDayID { get; set; }
    [Required]
    public WorkDay WorkDay { get; set; }
    [Required]
    public required int EmployeeID { get; set; }
    [Required]
    public Employee Employee { get; set; }
    [Required]
    public required string WorkDuration { get; set; } 
}