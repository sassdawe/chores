namespace Chores.Models;

public class Household
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    public ICollection<AppUser> Members { get; set; } = [];
    public ICollection<Chore> Chores { get; set; } = [];
}
