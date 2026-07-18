namespace Chores.Models;

public class AppUser
{
    public int Id { get; set; }
    public string LoginName { get; set; } = string.Empty;
    public string? PreferredLocale { get; set; }

    public ICollection<FidoCredential> Credentials { get; set; } = [];
    public ICollection<HouseholdMembership> HouseholdMemberships { get; set; } = [];
    public ICollection<CompletionRecord> CompletionRecords { get; set; } = [];
    public ICollection<HouseholdInvite> SentInvites { get; set; } = [];
}
