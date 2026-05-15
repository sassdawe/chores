namespace Chores.Models;

public class AppUser
{
    public int Id { get; set; }
    public string LoginName { get; set; } = string.Empty;
    public bool IsHouseholdOwner { get; set; }
    public int HouseholdId { get; set; }
    public Household Household { get; set; } = null!;

    public ICollection<FidoCredential> Credentials { get; set; } = [];
    public ICollection<CompletionRecord> CompletionRecords { get; set; } = [];
    public ICollection<HouseholdInvite> SentInvites { get; set; } = [];
}
