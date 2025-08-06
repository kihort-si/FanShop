using System.ComponentModel.DataAnnotations;

namespace FanShop.Models;

public class WorkDay
{
    [Key]
    public int WorkDayID { get; set; }
    public DateTime Date { get; set; }
    public ICollection<WorkDayEmployee> WorkDayEmployees { get; set; } = new List<WorkDayEmployee>();
}