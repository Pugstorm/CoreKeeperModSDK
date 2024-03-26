using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Unity.NetCode.Generators
{
    /// <summary>
    /// Helper class that let source generator to resolve both project and package folder relative file paths.
    /// Use manifest.json to map the folder to the appropriate package version
    /// The class is only used for UNITY 2020.LTS, where the template files must be retrieved manually by inspecting
    /// the project folder (the resolved full path is not provide)
    /// <remarks>
    /// This method of resolving template is going ot be deprected using Unity 2021_2+ onward, in favour of passing the
    /// templates as list of additional files.
    /// </remarks>
    /// </summary>
    internal class PathResolver
    {
        private readonly Dictionary<string, string> packageManifest;
        private Regex dictRegex;
        private bool manifestMandatory;

        public PathResolver(string workingDirectory)
        {
            packageManifest = new Dictionary<string, string>();
            dictRegex = new Regex("(\\w+\\S+\\b)");

            if (workingDirectory.EndsWith("Source~", StringComparison.Ordinal))
            {
                //Test directory. Remap to the root folder so that Packages/com.xxx can be resolved
                WorkingDir = workingDirectory.Substring(0, workingDirectory.
                    IndexOf("Packages", StringComparison.Ordinal)-1);
                manifestMandatory = false;
            }
            else
            {
                manifestMandatory = true;
                WorkingDir = workingDirectory;
            }
        }

        public void LoadManifestMapping()
        {
            packageManifest.Clear();
            //Simpler rule: just look in the package cache and retrieve all the packages from there
            //Then use file mapping to add the additional ones that aren't cached.
            var packageCacheRoot = Path.Combine(WorkingDir, "Library", "PackageCache");
            if(Directory.Exists(packageCacheRoot))
            {
                var packageCacheFolders = Directory.GetDirectories(packageCacheRoot);
                foreach (var packagePath in packageCacheFolders)
                {
                    var packageName = Path.GetFileName(packagePath);
                    var versionIndex = packageName.IndexOf('@');
                    if (versionIndex == -1)
                    {
                        Debug.LogWarning($"skip Library/PackageCache/{packagePath}. Invalid package directory");
                        continue;
                    }
                    packageName = packageName.Substring(0, versionIndex);
                    packageManifest[packageName] = packagePath;
                }
            }

            var manifestFile = Path.Combine(WorkingDir, "Packages", "manifest.json");
            if (File.Exists(manifestFile))
            {
                //Do a very simple logic by looking for all lines with file:/ (TODO: make it more robust by using lowercase)
                var lines = File.ReadAllLines(manifestFile).Where(l => l.Contains("file:") && !l.EndsWith("tgz", StringComparison.OrdinalIgnoreCase));
                foreach (var line in lines)
                {
                    var m = dictRegex.Matches(line);
                    var key = m[0].ToString();
                    var location = m[1].ToString().Substring(5);
                    if (!Path.IsPathRooted(location))
                        location = Path.Combine(WorkingDir, "Packages", location);

                    if (Directory.Exists(location))
                        packageManifest[key] = location;
                    else
                        Debug.LogError($"Cannot find package reference {location}");
                }
            }
            else if(manifestMandatory)
            {
                throw new InvalidOperationException($"Manifest file not found in {manifestFile}.");
            }
        }

        private string WorkingDir { get; }

        public string ResolvePath(string templatePath)
        {
            if (Path.IsPathRooted(templatePath))
                return templatePath; //fullpath, no need to resolve

            //special case if the file we are looking for is relative to the project folder and already exist (like embedded packages)
            var localProjectPath = Path.Combine(WorkingDir, templatePath);
            if (File.Exists(localProjectPath))
                return Path.GetFullPath(localProjectPath);

            //handle special package folder
            if (templatePath.StartsWith("Packages/", StringComparison.Ordinal))
            {
                var startIndex = "Packages/".Length;
                var lastIndex = templatePath.IndexOf('/', startIndex);
                if (lastIndex == -1)
                    throw new ArgumentException("Invalid template path " + templatePath);
                var packageName = templatePath.Substring(startIndex, lastIndex - startIndex);
                if (!packageManifest.ContainsKey(packageName))
                    throw new FileNotFoundException($"Cannot load template {templatePath}. Package {packageName} not found");
                return Path.Combine(packageManifest[packageName], templatePath.Substring(lastIndex + 1));
            }

            return Path.Combine(WorkingDir, templatePath);
        }
    }
}
