using System.Linq;
using FanShop.Models;

namespace FanShop.Services;

public static class DbInitializer
{
    public static void Initialize()
    {
        using var context = new AppDbContext();
        context.Database.EnsureCreated();
    }
}