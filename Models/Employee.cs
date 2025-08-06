using System.ComponentModel.DataAnnotations;

namespace FanShop.Models;

public class Employee
{
    [Key]
    public int EmployeeID { get; set; }
    [Required]
    public string FirstName { get; set; }
    [Required]
    public string LastName { get; set; }
    [Required]
    public string Surname { get; set; }
    [Required]
    public string DateOfBirth { get; set; }
    [Required]
    public string PlaceOfBirth { get; set; }
    [Required]
    public string Passport { get; set; }
    
    public ICollection<WorkDayEmployee> WorkDayEmployees { get; set; } = new List<WorkDayEmployee>();
}