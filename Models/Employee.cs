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
    public bool HasPermanentPass { get; set; }
    public bool ExcludeFromSalaryByDefault { get; set; }
    public int? DefaultShopID { get; set; }
    public int? DefaultPositionID { get; set; }
    
    public ICollection<WorkDayEmployee> WorkDayEmployee { get; set; } = new List<WorkDayEmployee>();
}
