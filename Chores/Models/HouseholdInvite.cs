namespace Chores.Models;

public class HouseholdInvite
{
    public int Id { get; set; }
    public int HouseholdId { get; set; }
    public Household Household { get; set; } = null!;

    public int InvitedByUserId { get; set; }
    public AppUser InvitedByUser { get; set; } = null!;

    public string LoginName { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? AcceptedAtUtc { get; set; }
    public DateTime? DeclinedAtUtc { get; set; }
}
