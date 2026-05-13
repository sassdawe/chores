namespace Chores.Models;

public class CompletionRecord
{
    public int Id { get; set; }
    public int ChoreId { get; set; }
    public Chore Chore { get; set; } = null!;
    public int CompletedByUserId { get; set; }
    public AppUser CompletedByUser { get; set; } = null!;

    /// <summary>Always stored as UTC.</summary>
    public DateTime CompletedAtUtc { get; set; }
}
