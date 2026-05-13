namespace Chores.Models;

public class Label
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    /// <summary>Hex color string, e.g. "#ff5733"</summary>
    public string Color { get; set; } = "#6c757d";
    public int HouseholdId { get; set; }
    public Household Household { get; set; } = null!;
    public ICollection<Chore> Chores { get; set; } = [];
}
