using System.Windows.Input;

namespace AngorApp.Sections.Shell;

public class CommandSection : SectionBase
{
    public string Name { get; }

    public CommandSection(string name, ICommand command, string icon)
    {
        Name = name;
        Command = command;
        Icon = icon;
    }

    public ICommand Command { get; }
    public string Icon { get; }
}