using AngorApp.Sections.Shell;

namespace AngorApp.Composition;

public interface ISectionsFactory
{
    IEnumerable<SectionBase> CreateSections();
}