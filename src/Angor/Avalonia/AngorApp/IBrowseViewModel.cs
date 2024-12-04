using System;
using System.Collections.Generic;

namespace AngorApp;

public interface IBrowseViewModel
{
    public IReadOnlyCollection<Project> Projects { get; set; }
}

public class Project(string name)
{
    public string Name { get; } = name;
    public object Picture { get; init; }
    public Uri? Uri { get; set; }
    public string ShortDescription { get; set; }
}