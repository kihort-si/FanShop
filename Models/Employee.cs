namespace FanShop.Models;

public class Employee
{
    public int EmployeeID { get; set; }
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public required string Surname { get; set; }
    public required string DateOfBirth { get; set; }
    public required string PlaceOfBirth { get; set; }
    public required string Passport { get; set; }
}