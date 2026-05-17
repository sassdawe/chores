namespace Chores.Models;

public class HouseholdMembership
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public AppUser User { get; set; } = null!;
    public int HouseholdId { get; set; }
    public Household Household { get; set; } = null!;
    public bool IsOwner { get; set; }
    public DateTime JoinedAtUtc { get; set; }
}
