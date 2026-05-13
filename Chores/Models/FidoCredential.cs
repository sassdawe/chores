namespace Chores.Models;

/// <summary>Stores a single FIDO2/passkey credential for a user.</summary>
public class FidoCredential
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public AppUser User { get; set; } = null!;

    public byte[] CredentialId { get; set; } = [];
    public byte[] PublicKey { get; set; } = [];
    public byte[] UserHandle { get; set; } = [];
    public uint SignCount { get; set; }
    public string CredType { get; set; } = string.Empty;
    public DateTime RegDate { get; set; }
    public Guid AaGuid { get; set; }
}
