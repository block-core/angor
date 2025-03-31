using System.Windows.Input;

namespace AngorApp.Sections.Shell.Sections;

public class CommandSection(string name, ICommand command, string icon) : SectionBase
{
    public string Name { get; } = name;
    public ICommand Command { get; } = command;
    public string Icon { get; } = icon;
}