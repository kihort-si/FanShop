using System.Linq;
using FanShop.Models;

namespace FanShop.Services;

public static class DbInitializer
{
    public static void Initialize()
    {
        using var context = new AppDbContext();
        context.Database.EnsureCreated();

        if (!context.Employees.Any())
        {
            context.Employees.Add(new Employee
            {
                FirstName = "Иван",
                LastName = "Иванов",
                Surname = "Иванович",
                DateOfBirth = "01.01.1990",
                PlaceOfBirth = "Москва",
                Passport = "1234 567890"
            });
            context.SaveChanges();
        }
    }
}