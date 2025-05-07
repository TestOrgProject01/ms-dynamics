using System.Reflection;

namespace Helpers
{
    public static class BuildHelper
    {
        public static string VersionNumber()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var version = assembly.GetName().Version;
            return version?.ToString();
        }
    }
}
