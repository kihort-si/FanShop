using System.ComponentModel.DataAnnotations;

namespace FanShop.Models;

public class TaskCategory
{
    [Key]
    public int TaskCategoryID { get; set; }
    
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;
    
    [MaxLength(500)]
    public string? Description { get; set; }
    
    [MaxLength(7)]
    public string Color { get; set; } = "#0A6DAE";
    
    public string? DefaultTask { get; set; }
    
    public virtual ICollection<DayTask> Tasks { get; set; } = new List<DayTask>();
}