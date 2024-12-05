using System;

namespace AngorApp;

public class Project(string name)
{
    public string Name { get; } = name;
    public object Picture { get; init; }
    public object Icon { get; set; }
    public Uri? Uri { get; set; }
    public string ShortDescription { get; set; }
}