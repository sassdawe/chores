namespace Chores.Models;

public class Chore
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Schedule Schedule { get; set; }
    public int HouseholdId { get; set; }
    public Household Household { get; set; } = null!;

    public ICollection<CompletionRecord> CompletionRecords { get; set; } = [];
    public ICollection<Label> Labels { get; set; } = [];
}
