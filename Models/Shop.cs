using System.ComponentModel.DataAnnotations;

namespace FanShop.Models;

public class Shop
{
    [Key] public int ShopID { get; set; }

    [Required]
    [MaxLength(50)]
    public string ShopName { get; set; } = string.Empty;

    [Required]
    public DateTime OpenDate { get; set; }

    public DateTime? CloseDate { get; set; }

    public ICollection<Position> Positions { get; set; }
        = new List<Position>();
}