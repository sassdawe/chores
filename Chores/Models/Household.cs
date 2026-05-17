namespace Chores.Models;

public class Household
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    public ICollection<HouseholdMembership> Memberships { get; set; } = [];
    public ICollection<Chore> Chores { get; set; } = [];
    public ICollection<HouseholdInvite> Invites { get; set; } = [];
}
