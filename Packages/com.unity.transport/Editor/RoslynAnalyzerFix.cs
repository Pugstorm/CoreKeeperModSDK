using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

using UnityEditor;

using UnityEngine;


namespace Unity.Networking.Editor
{
    public class RoslynAnalyzerFix : AssetPostprocessor
    {
        private static readonly XNamespace xNamespace = "http://schemas.microsoft.com/developer/msbuild/2003";

        public static string OnGeneratedCSProject(string path, string content)
        {
            // There is currently a bug in both VS Code Editor/Rider that doesn't properly resolve
            // package based analyzers. We attempt to correct the transport analyzer if its present
            if (content.Contains(@"Packages\com.unity.transport\Analyzers\Unity.Transport.Analyzers.dll"))
            {
                var newDoc = content;

                string[] lines = newDoc.Split(new string[] { Environment.NewLine }, StringSplitOptions.None);
                var ll = lines.Where(l => l.Contains("Unity.Transport.Analyzers.dll")).ToList();
                var fullPath = Path.GetFullPath(@"Packages\com.unity.transport\Analyzers\Unity.Transport.Analyzers.dll");
                foreach (var item in ll)
                {
                    newDoc = newDoc.Replace(item, "");
                }

                var xDocument = XDocument.Parse(newDoc);
                xDocument.Root?.Add(new XElement(xNamespace + "ItemGroup", new XElement(xNamespace + "Analyzer", new XAttribute("Include", fullPath))));

                return $"{xDocument.Declaration}{Environment.NewLine}{xDocument.Root}";
            }

            return content;
        }
    }
}
