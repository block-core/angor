using System.Reflection;
using System.Runtime.InteropServices;

namespace Angor.Server
{
	public class DataConfigOptions
	{
        public DataConfigOptions()
        {
            DirectoryPath = CreteDataLocation.CreateDefaultDataDirectories("Testdata");
        }

        public string DirectoryPath { get; set; }
		public bool UseDefaultPath { get; set; }
	}

	public static class CreteDataLocation
	{
		public static string CreateDefaultDataDirectories(string appName)
		{

            var directory = Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location)
                            ?? Path.Combine(Directory.GetCurrentDirectory());

            return  directory;// Path.Combine(directory, "swaps");

		}

	}
}
