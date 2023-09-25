using System.IO;

namespace QuantConnect.Logging
{
    public static class PathHelper
    {
        public static string CompletionPath(string filepath)
        {
            var basePath = Path.GetDirectoryName(typeof(PathHelper).Assembly.Location) ?? string.Empty;
            if (filepath.StartsWith("../") || filepath.StartsWith("./"))
            {
                return Path.GetFullPath(Path.Combine(basePath, filepath));
            }

            return string.IsNullOrEmpty(Path.GetDirectoryName(filepath)) 
                ? Path.Combine(basePath, filepath) 
                : filepath;
        }
    }
}