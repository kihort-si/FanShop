namespace FanShop.Models;

public class Match
{
    public string TeamName { get; set; } = string.Empty;
    public string Time { get; set; } = string.Empty;
    public string Logo { get; set; } = string.Empty;
    public bool CanChange { get; set; }
}