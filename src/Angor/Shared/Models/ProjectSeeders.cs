namespace Angor.Shared.Models;

public class ProjectSeeders
{
    public int Threshold { get; set; }
    public List<string> SecretHashes { get; set; } = new();
}