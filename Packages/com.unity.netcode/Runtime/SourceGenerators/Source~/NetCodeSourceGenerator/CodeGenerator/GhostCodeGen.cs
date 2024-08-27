using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.CodeAnalysis.Text;

namespace Unity.NetCode.Generators
{
    /// <summary>
    /// A simple cache that store an clone templates.
    /// Is created and owned by the code-generation Context. Is not multithread safe and each SourceGenerator
    /// has its own instance.
    /// The cache is used by ComponentGenerator and CommandGenerator to retrieve the necessary template-fragments and
    /// avoid reading and parsing text-file multiple time.
    /// </summary>
    internal class GhostCodeGen
    {
        public override string ToString()
        {
            var replacements = "";
            foreach (var fragment in m_Fragments)
            {
                replacements += $"Key: {fragment.Key}, Template: {fragment.Value.Template}, Content: {fragment.Value.Content}";
            }

            return replacements;
        }

        public Dictionary<string, string> Replacements;
        public Dictionary<string, FragmentData> Fragments => m_Fragments;

        private Dictionary<string, FragmentData> m_Fragments;
        private string m_FileTemplate;
        private string m_HeaderTemplate;
        private CodeGenerator.Context m_Context;
        public class FragmentData
        {
            public string Template;
            public string Content;
        }

        public void AddTemplateOverrides(string template, string templateData)
        {
            int regionStart;
            while ((regionStart = templateData.IndexOf("#region", StringComparison.Ordinal)) >= 0)
            {
                while (regionStart > 0 && templateData[regionStart - 1] != '\n' &&
                       char.IsWhiteSpace(templateData[regionStart - 1]))
                {
                    --regionStart;
                }

                var regionNameEnd = templateData.IndexOf("\n", regionStart, StringComparison.Ordinal);
                var regionNameLine = templateData.Substring(regionStart, regionNameEnd - regionStart);
                var regionNameTokens = System.Text.RegularExpressions.Regex.Split(regionNameLine.Trim(), @"\s+");
                if (regionNameTokens.Length != 2)
                    throw new InvalidOperationException($"Invalid region in GhostCodeGen template '{template}', while generating '{m_Context.generatedNs}.{m_Context.generatorName}'.");
                var regionEnd = templateData.IndexOf("#endregion", regionStart, StringComparison.Ordinal);
                if (regionEnd < 0)
                    throw new InvalidOperationException($"Invalid region in GhostCodeGen template '{template}', while generating '{m_Context.generatedNs}.{m_Context.generatorName}'.");
                while (regionEnd > 0 && templateData[regionEnd - 1] != '\n' &&
                       char.IsWhiteSpace(templateData[regionEnd - 1]))
                {
                    if (regionEnd <= regionStart)
                        throw new InvalidOperationException($"Invalid region in GhostCodeGen template '{template}', while generating '{m_Context.generatedNs}.{m_Context.generatorName}'.");
                    --regionEnd;
                }

                var regionData = templateData.Substring(regionNameEnd + 1, regionEnd - regionNameEnd - 1);
                if (m_Fragments.TryGetValue(regionNameTokens[1], out var fragmentData))
                    fragmentData.Template = regionData;
                else
                    m_Context.diagnostic.LogError($"Did not find '{regionNameTokens[1]}' region to override, while generating '{m_Context.generatedNs}.{m_Context.generatorName}'.");

                templateData = templateData.Substring(regionEnd + 1);
            }
        }

        public GhostCodeGen(string template, string templateData, CodeGenerator.Context context)
        {
            m_Context = context;
            ParseTemplate(template, templateData);
        }

        private void ParseTemplate(string templateName, string templateData)
        {
            Replacements = new Dictionary<string, string>();
            m_Fragments = new Dictionary<string, FragmentData>();
            m_HeaderTemplate = "";

            int regionStart;
            while ((regionStart = templateData.IndexOf("#region", StringComparison.Ordinal)) >= 0)
            {
                while (regionStart > 0 && templateData[regionStart - 1] != '\n' &&
                       char.IsWhiteSpace(templateData[regionStart - 1]))
                {
                    --regionStart;
                }

                var pre = templateData.Substring(0, regionStart);

                var regionNameEnd = templateData.IndexOf("\n", regionStart, StringComparison.Ordinal);
                var regionNameLine = templateData.Substring(regionStart, regionNameEnd - regionStart);
                var regionNameTokens = System.Text.RegularExpressions.Regex.Split(regionNameLine.Trim(), @"\s+");
                if (regionNameTokens.Length != 2)
                    throw new InvalidOperationException($"Invalid region in GhostCodeGen template '{templateName}', while generating '{m_Context.generatedNs}.{m_Context.generatorName}'.");
                var regionEnd = templateData.IndexOf("#endregion", regionStart, StringComparison.Ordinal);
                if (regionEnd < 0)
                    throw new InvalidOperationException($"Invalid region in GhostCodeGen template '{templateName}', while generating '{m_Context.generatedNs}.{m_Context.generatorName}'.");
                while (regionEnd > 0 && templateData[regionEnd - 1] != '\n' &&
                       char.IsWhiteSpace(templateData[regionEnd - 1]))
                {
                    if (regionEnd <= regionStart)
                        throw new InvalidOperationException($"Invalid region in GhostCodeGen template '{templateName}', while generating '{m_Context.generatedNs}.{m_Context.generatorName}'.");
                    --regionEnd;
                }

                var regionData = templateData.Substring(regionNameEnd + 1, regionEnd - regionNameEnd - 1);
                if (regionNameTokens[1] == "__GHOST_END_HEADER__")
                {
                    m_HeaderTemplate = pre;
                    pre = "";
                }
                else
                {
                    if (m_Fragments.ContainsKey(regionNameTokens[1]))
                    {
                        m_Context.diagnostic.LogError($"The template {templateName} already contains the key [{regionNameTokens[1]}], while generating '{m_Context.generatedNs}.{m_Context.generatorName}'.");
                    }
                    m_Fragments.Add(regionNameTokens[1], new FragmentData{Template = regionData, Content = ""});
                    pre += regionNameTokens[1];
                }

                regionEnd = templateData.IndexOf('\n', regionEnd);
                var post = "";
                if (regionEnd >= 0)
                    post = templateData.Substring(regionEnd + 1);
                templateData = pre + post;
            }
            m_Fragments.Add("__GHOST_AGGREGATE_WRITE__", new FragmentData{Template = "", Content = ""});
            m_FileTemplate = templateData;
        }

        private GhostCodeGen()
        {
        }
        public GhostCodeGen Clone()
        {
            var codeGen = new GhostCodeGen();
            codeGen.m_FileTemplate = m_FileTemplate;
            codeGen.m_HeaderTemplate = m_HeaderTemplate;
            codeGen.Replacements = new Dictionary<string, string>();
            codeGen.m_Fragments = new Dictionary<string, FragmentData>();
            codeGen.m_Context = m_Context;
            foreach (var value in m_Fragments)
            {
                codeGen.m_Fragments.Add(value.Key, new FragmentData{Template = value.Value.Template, Content = ""});
            }
            return codeGen;
        }

        private void Validate(string content, string fragment)
        {
            var re = new System.Text.RegularExpressions.Regex(@"(\b__COMMAND\w+)|(\b__GHOST\w+)");
            var matches = re.Matches(content);
            if(matches.Count > 0)
            {
                foreach (System.Text.RegularExpressions.Match match in matches)
                {
                    var name = match.Value;
                    var nameEnd = name.IndexOf("__", 2, StringComparison.Ordinal);
                    if (nameEnd < 0)
                        m_Context.diagnostic.LogError($"Invalid key in GhostCodeGen fragment {fragment} while generating '{m_Context.generatedNs}.{m_Context.generatorName}'.");
                    m_Context.diagnostic.LogError($"GhostCodeGen did not replace {name} in fragment {fragment} while generating '{m_Context.generatedNs}.{m_Context.generatorName}'.");
                }
                throw new InvalidOperationException($"GhostCodeGen failed for fragment {fragment} while generating '{m_Context.generatedNs}.{m_Context.generatorName}'.");
            }
        }

        string Replace(string content, Dictionary<string, string> replacements)
        {
            foreach (var keyValue in replacements)
            {
                content = content.Replace($"__{keyValue.Key}__", keyValue.Value);
            }

            return content;
        }

        public void Append(GhostCodeGen target)
        {
            if (target == null)
                target = this;

            foreach (var fragment in m_Fragments)
            {
                if (!target.m_Fragments.ContainsKey($"{fragment.Key}"))
                {
                    m_Context.diagnostic.LogError($"Target CodeGen is missing fragment '{fragment.Key}' while generating '{m_Context.generatedNs}.{m_Context.generatorName}'.");
                    continue;
                }
                target.m_Fragments[$"{fragment.Key}"].Content += m_Fragments[$"{fragment.Key}"].Content;
            }
        }

        public void AppendFragment(string fragment,
            GhostCodeGen target, string targetFragment = null, string extraIndent = null)
        {
            if (target == null)
                target = this;
            if (targetFragment == null)
                targetFragment = fragment;
            if (!m_Fragments.ContainsKey($"__{fragment}__"))
                throw new InvalidOperationException($"Generating '{m_Context.generatedNs}.{m_Context.generatorName}', '{fragment}' is not a valid fragment in the given template.");
            if (!target.m_Fragments.ContainsKey($"__{targetFragment}__"))
                throw new InvalidOperationException($"Generating '{m_Context.generatedNs}.{m_Context.generatorName}', '{targetFragment} is not a valid fragment in the given template.");

            target.m_Fragments[$"__{targetFragment}__"].Content += m_Fragments[$"__{fragment}__"].Content;
        }

        public string GetFragmentTemplate(string fragment)
        {
            if (!m_Fragments.ContainsKey($"__{fragment}__"))
                throw new InvalidOperationException($"Generating '{m_Context.generatedNs}.{m_Context.generatorName}', cannot get fragment template, as fragment '{fragment}' is not found.");
            return m_Fragments[$"__{fragment}__"].Template;
        }

        public bool HasFragment(string fragment)
        {
            return m_Fragments.ContainsKey($"__{fragment}__");
        }
        public bool GenerateFragment(string fragment, Dictionary<string, string> replacements, GhostCodeGen target = null, string targetFragment = null, string extraIndent = null, bool allowMissingFragment = false)
        {
            if (target == null)
                target = this;
            if (targetFragment == null)
                targetFragment = fragment;
            if (!m_Fragments.ContainsKey($"__{fragment}__"))
            {
                if (allowMissingFragment)
                    return false;
                throw new InvalidOperationException($"{fragment} is not a valid fragment for the given template! replacements: [{(replacements != null ? string.Join(",",replacements) : null)}]!");
            }
            if (!target.m_Fragments.ContainsKey($"__{targetFragment}__"))
                throw new InvalidOperationException($"{targetFragment} is not a valid targetFragment for the given template! replacements: [{(replacements != null ? string.Join(",",replacements) : null)}]!");
            var content = Replace(m_Fragments[$"__{fragment}__"].Template, replacements);

            if (extraIndent != null)
                content = extraIndent + content.Replace("\n    ", $"\n    {extraIndent}");

            Validate(content, fragment);
            target.m_Fragments[$"__{targetFragment}__"].Content += content;
            return true;
        }

        public void ReplaceContentInFragments(string[] fragments, string value, string replacement)
        {
            foreach (var key in fragments)
            {
                m_Fragments[$"__{key}__"].Content = m_Fragments[$"__{key}__"].Content.Replace(value, replacement);
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="generatorName"></param>
        /// <param name="generatorNamespace"></param>
        /// <param name="replacements"></param>
        /// <param name="batch"></param>
        public void GenerateFile(
            string generatorName,
            string generatorNamespace,
            Dictionary<string, string> replacements, List<CodeGenerator.GeneratedFile> batch)
        {
            var header = Replace(m_HeaderTemplate, replacements);
            var content = Replace(m_FileTemplate, replacements);

            foreach (var keyValue in m_Fragments)
            {
                header = header.Replace(keyValue.Key, keyValue.Value.Content);
                content = content.Replace(keyValue.Key, keyValue.Value.Content);
            }
            content = header + content;
            Validate(content, "Root");
            batch.Add(new CodeGenerator.GeneratedFile
            {
                Namespace = generatorNamespace,
                GeneratedClassName = generatorName,
                Code = content
            });
        }
    }
}
