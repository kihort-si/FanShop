using System.ComponentModel.DataAnnotations;

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
}