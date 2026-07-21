using System.ComponentModel.DataAnnotations;

namespace FanShop.Models;

public class Position
{
    [Key] public int PositionID { get; set; }

    [Required]
    public int ShopID { get; set; }

    public Shop Shop { get; set; } = null!;

    [Required]
    [MaxLength(50)]
    public string PositionName { get; set; } = string.Empty;

    public ICollection<SalaryHistory> SalaryHistories { get; set; }
        = new List<SalaryHistory>();
}