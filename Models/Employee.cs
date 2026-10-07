using System.ComponentModel.DataAnnotations;

namespace FanShop.Models;

public class Employee
{
    [Key]
    public int EmployeeID { get; set; }
    [Required]
    public string FirstName { get; set; } = string.Empty;
    [Required]
    public string LastName { get; set; } = string.Empty;
    [Required]
    public string Surname { get; set; } = string.Empty;
    [Required]
    public string DateOfBirth { get; set; } = string.Empty;
    [Required]
    public string PlaceOfBirth { get; set; } = string.Empty;
    [Required]
    public string Passport { get; set; } = string.Empty;
    
    public ICollection<WorkDayEmployee> WorkDayEmployee { get; set; } = new List<WorkDayEmployee>();
}