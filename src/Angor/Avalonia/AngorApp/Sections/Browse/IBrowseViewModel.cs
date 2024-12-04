using System.Collections.Generic;

namespace AngorApp;

public interface IBrowseViewModel
{
    public IReadOnlyCollection<Project> Projects { get; set; }
}