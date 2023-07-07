using Blockcore.Consensus.ScriptInfo;

public class ProjectScripts
{
    public Script Founder { get; set; }
    public Script Recover { get; set; }
    public Script EndOfProject { get; set; }
    public List<Script> Seeders { get; set; } = new();
    public List<Script> GetAllScripts()
    {
        var list = new List<Script>();
        list.AddRange(new[] { Founder, Recover, EndOfProject });
        list.AddRange(Seeders);
        return list;
    }
}