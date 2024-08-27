using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Unity.NetCode.Generators
{
    /// <summary>
    /// TemplateFileProvider cache netcode templates and provide them to the code generation system on demand.
    /// Templates are extracted from different sources:
    /// - templates embedded in the generator dll (the default ones)
    /// - templates that came from additional files (2021+)
    /// - diredtly from disk, using full/relative path (legacy, 2020.X).
    /// </summary>
    internal class TemplateFileProvider : CodeGenerator.ITemplateFileProvider
    {
        const string k_TemplateId = "#templateid:";
        readonly private HashSet<string> defaultTemplates;
        readonly private Dictionary<string, SourceText> customTemplates;
        readonly private IDiagnosticReporter diagnostic;
        public PathResolver pathResolver { get; set; }

        public TemplateFileProvider(IDiagnosticReporter diagnosticReporter)
        {
            defaultTemplates = new HashSet<string>();
            customTemplates = new Dictionary<string, SourceText>(256);
            diagnostic = diagnosticReporter;
            pathResolver = null;

            var thisAssembly = Assembly.GetExecutingAssembly();
            var resourceNames = thisAssembly.GetManifestResourceNames();
            foreach (var resource in resourceNames)
                defaultTemplates.Add(resource);
        }

        /// <summary>
        /// Parse the additional files passed to the compilation and add any custom template to the
        /// the internal map.
        /// Valid template are considered files with `.netcode.additionalfile` extension and which have a first
        /// line starting with `#templateid: TEMPLATE_ID
        /// </summary>
        /// <param name="additionalFiles"></param>
        /// <param name="customUserTypes"></param>
        public void AddAdditionalTemplates(ImmutableArray<AdditionalText> additionalFiles, List<TypeRegistryEntry> customUserTypes)
        {
            var missingUserTypes = new List<TypeRegistryEntry>(customUserTypes);
            foreach (var additionalText in additionalFiles)
            {
                var isNetCodeTemplate = additionalText.Path.EndsWith(NetCodeSourceGenerator.NETCODE_ADDITIONAL_FILE, StringComparison.Ordinal);
                if (isNetCodeTemplate)
                {
                    var text = additionalText.GetText();
                    if (text == null || text.Lines.Count == 0)
                    {
                        diagnostic.LogError($"All NetCode AdditionalFiles must be valid Templates, but '{additionalText.Path}' does not contain any text!");
                        continue;
                    }

                    var line = text.Lines[0].ToString();
                    if (!line.StartsWith(k_TemplateId, StringComparison.OrdinalIgnoreCase))
                    {
                        diagnostic.LogError($"All NetCode AdditionalFiles must be valid Templates, but '{additionalText.Path}' does not start with a correct Template definition (a '#templateid:MyNamespace.MyType' line).");
                        continue;
                    }

                    var templateId = line.Substring(k_TemplateId.Length).Trim();
                    if (string.IsNullOrWhiteSpace(templateId))
                    {
                        diagnostic.LogError($"NetCode AdditionalFile '{additionalText.Path}' is a valid Template, but the `{k_TemplateId}` is empty!");
                        continue;
                    }

                    var foundMatch = FindAndRemoveTypeRegistryEntry(missingUserTypes, templateId);
                    if (foundMatch == null)
                    {
                        diagnostic.LogError($"NetCode AdditionalFile '{additionalText.Path}' (named '{templateId}') is a valid Template, but it cannot be matched with any UserDefinedTemplate (probably a typo). Known user templates:[{GetKnownCustomUserTemplates()}].");
                        continue;
                    }

                    if (!string.Equals(templateId, foundMatch.Template, StringComparison.Ordinal) &&
                        !string.Equals(templateId, foundMatch.TemplateOverride, StringComparison.Ordinal))
                    {
                        diagnostic.LogError($"NetCode AdditionalFile '{additionalText.Path}' (named '{templateId}') is a valid Template, but the Template definition in 'UserDefinedTemplates' ({foundMatch.Template}, of type {foundMatch.Type}) does not match the #templateID!");
                        continue;
                    }

                    diagnostic.LogDebug($"NetCode AdditionalFile '{additionalText.Path}' (named '{templateId}') is a valid Template ({foundMatch.Template}, {foundMatch.Type}).");

                    customTemplates.Add(templateId, additionalText.GetText());
                }
                else
                {
                    diagnostic.LogDebug($"Ignoring AdditionalFile '{additionalText.Path}' as it is not a NetCode type!");
                }
            }

            // Ensure all of the users `TypeRegistryEntry`s are linked.
            foreach (var typeRegistryEntry in missingUserTypes)
            {
                var message = $"Unable to find the Template associated with '{typeRegistryEntry}'. Looking for '{typeRegistryEntry.Template}'. There are {additionalFiles.Length} additionalFiles:[{string.Join(",", additionalFiles.Select(x => x.Path))}]!";
                diagnostic.LogError(message);

            }

            string GetKnownCustomUserTemplates()
            {
                return string.Join(",", customUserTypes.Select(x => $"{x.Type}[{x.Template}]"));
            }
        }

        public void PerformAdditionalTypeRegistryValidation(List<TypeRegistryEntry> customUserTypes)
        {

            string GetKnownCustomUserTemplates()
            {
                return string.Join(",", customTemplates.Keys);
            }

            // Ensure all of the users `TypeRegistryEntry.TemplateOverride`s are linked.
            foreach (var typeRegistryEntry in customUserTypes)
            {
                var hasDefinedTemplateOverride = !string.IsNullOrWhiteSpace(typeRegistryEntry.TemplateOverride);
                if (hasDefinedTemplateOverride)
                {
                    if (!customTemplates.ContainsKey(typeRegistryEntry.TemplateOverride))
                    {
                        diagnostic.LogError($"Unable to find the `TemplateOverride` associated with '{typeRegistryEntry}'. Known templates are {GetKnownCustomUserTemplates()}.");
                    }
                }
            }
        }

        static TypeRegistryEntry FindAndRemoveTypeRegistryEntry(List<TypeRegistryEntry> typeRegistryEntries, string templateId)
        {
            TypeRegistryEntry foundMatch = null;
            for (var i = 0; i < typeRegistryEntries.Count; i++)
            {
                var x = typeRegistryEntries[i];
                if (string.Equals(x.Template, templateId, StringComparison.Ordinal) ||
                    x.Template.EndsWith(templateId + NetCodeSourceGenerator.NETCODE_ADDITIONAL_FILE, StringComparison.Ordinal))
                {
                    foundMatch = x;
                    typeRegistryEntries.RemoveAt(i);
                    break;
                }
                if (!string.IsNullOrEmpty(x.TemplateOverride) &&
                    (string.Equals(x.TemplateOverride, templateId, StringComparison.Ordinal) ||
                     x.TemplateOverride.EndsWith(templateId + NetCodeSourceGenerator.NETCODE_ADDITIONAL_FILE, StringComparison.Ordinal)))
                {
                    foundMatch = x;
                    typeRegistryEntries.RemoveAt(i);
                    break;
                }
            }

            return foundMatch;
        }

        /// <summary>
        /// Get the template data for the given template identifier.
        /// </summary>
        /// <param name="resourcePath"></param>
        /// <returns>
        /// The System.IO.Stream from which reading the template content.
        /// </returns>
        /// <exception cref="FileNotFoundException">
        /// If the template path/id cannot be resolved
        /// </exception>
        public string GetTemplateData(string resourcePath)
        {
            if (customTemplates.TryGetValue(resourcePath, out var additionalText))
                return additionalText.ToString();

            if (defaultTemplates.Contains(resourcePath))
                return SourceText.From(LoadTemplateFromEmbeddedResources(resourcePath)).ToString();

            if (pathResolver != null)
            {
                var resolvedResourcePath = pathResolver.ResolvePath(resourcePath);
                if(File.Exists(resolvedResourcePath))
                    return File.ReadAllText(resolvedResourcePath);
                throw new FileNotFoundException($"Cannot find template with resource id '{resourcePath}' and resolvedResourcePath '{resolvedResourcePath}'! CustomTemplates:[{string.Join(",", customTemplates)}] DefaultTemplates:[{string.Join(",",defaultTemplates)}]");
            }
            throw new FileNotFoundException($"Cannot find template with resource id '{resourcePath}'! CustomTemplates:[{string.Join(",", customTemplates)}] DefaultTemplates:[{string.Join(",",defaultTemplates)}]");
        }

        private Stream LoadTemplateFromEmbeddedResources(string resourcePath)
        {
            //The templates in the resources begin with the namespace
            var thisAssembly = Assembly.GetExecutingAssembly();
            return thisAssembly.GetManifestResourceStream(resourcePath);
        }
    }
}
