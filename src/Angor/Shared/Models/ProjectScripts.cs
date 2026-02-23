using Blockcore.Consensus.ScriptInfo;

namespace Angor.Shared.Models;

public class ProjectScripts
{
    public Script Founder { get; set; } = null!;
    public Script Recover { get; set; } = null!;
    public Script EndOfProject { get; set; } = null!;
    public List<Script> Seeders { get; set; } = new();
    public List<Script> GetAllScripts()
    {
        var list = new List<Script>();
        list.AddRange(new[] { Founder, Recover, EndOfProject });
        list.AddRange(Seeders);
        return list;
    }
}