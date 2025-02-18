using AngorApp.Sections.Shell;
using AngorApp.Sections.Shell.Sections;

namespace AngorApp.Composition;

public interface ISectionsFactory
{
    IEnumerable<SectionBase> CreateSections();
}