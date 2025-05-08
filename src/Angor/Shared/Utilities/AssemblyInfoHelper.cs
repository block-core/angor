using System.Reflection;
using Microsoft.Extensions.Logging;

namespace Angor.Shared.Utilities;

public static class AssemblyInfoHelper
{
    public static List<AssemblyInfo> GetAllAssembliesInfo()
    {
        var assemblies = AppDomain.CurrentDomain.GetAssemblies();
        var assemblyInfos = new List<AssemblyInfo>();

        foreach (var assembly in assemblies)
        {
            var assemblyInfo = new AssemblyInfo
            {
                Name = assembly.GetName().Name,
                Version = assembly.GetName().Version?.ToString() ?? "Version not found",
                FullName = assembly.FullName,
                Location = assembly.Location,
                ImageRuntimeVersion = assembly.ImageRuntimeVersion,
                EntryPoint = assembly.EntryPoint?.ToString() ?? "No entry point",
                ReferencedAssemblies = assembly.GetReferencedAssemblies().Select(a => a.FullName).ToList()
            };

            assemblyInfos.Add(assemblyInfo);
        }

        return assemblyInfos;
    }
}

public class AssemblyInfo
{
    public string Name { get; set; }
    public string Version { get; set; }
    public string FullName { get; set; }
    public string Location { get; set; }
    public string ImageRuntimeVersion { get; set; }
    public string EntryPoint { get; set; }
    public List<string> ReferencedAssemblies { get; set; } = new();
}

public static class AssemblyLogger
{
    public static void LogAssemblyVersion(Type type, ILogger logger)
    {
        var assembly = type.Assembly;
        var version = assembly.GetName().Version?.ToString() ?? "Version not found";
        var name = assembly.GetName().Name;
        logger.LogInformation($"Assembly: {name}, Version: {version}");
    }
}