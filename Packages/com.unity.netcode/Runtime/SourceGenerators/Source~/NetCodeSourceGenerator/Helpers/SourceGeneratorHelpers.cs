using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Unity.NetCode.Generators
{
    /// <summary>
    /// Some helpers for debugging purpose. Notable entries:
    /// - Logging and reporting (in file and compiler diagnostics)
    /// </summary>
    internal static class Helpers
    {
        static private ThreadLocal<string> s_OutputFolder;
        static private ThreadLocal<string> s_ProjectPath;
        static private ThreadLocal<bool> s_IsUnity2021_OrNewer;
        static private ThreadLocal<bool> s_SupportTemplatesFromAdditionalFiles;
        static private ThreadLocal<bool> s_WriteLogToDisk;
        static private ThreadLocal<bool> s_CanWriteFiles;
        static private ThreadLocal<bool> s_IsDotsRuntime;

        static public string ProjectPath
        {
            get => s_ProjectPath.Value;
            private set => s_ProjectPath.Value = value;
        }
        static public string OutputFolder
        {
            get => s_OutputFolder.Value;
            private set => s_OutputFolder.Value = value;
        }

        static public bool IsUnity2021_OrNewer
        {
            get => s_IsUnity2021_OrNewer.Value;
            private set => s_IsUnity2021_OrNewer.Value = value;
        }

        static public bool SupportTemplateFromAdditionalFiles
        {
            get => s_SupportTemplatesFromAdditionalFiles.Value;
            private set => s_SupportTemplatesFromAdditionalFiles.Value = value;
        }

        static public bool WriteLogToDisk
        {
            get => s_WriteLogToDisk.Value;
            private set => s_WriteLogToDisk.Value = value;
        }

        static public bool CanWriteFiles
        {
            get => s_CanWriteFiles.Value;
            private set => s_CanWriteFiles.Value = value;
        }

        static public bool IsDotsRuntime
        {
            get => s_IsDotsRuntime.Value;
            private set => s_IsDotsRuntime.Value = value;
        }

        static Helpers()
        {
            s_OutputFolder = new ThreadLocal<string>(()=> Path.Combine("Temp", "NetCodeGenerated"));
            s_ProjectPath = new ThreadLocal<string>();
            s_IsUnity2021_OrNewer = new ThreadLocal<bool>();
            s_SupportTemplatesFromAdditionalFiles = new ThreadLocal<bool>();
            s_WriteLogToDisk = new ThreadLocal<bool>();
            s_CanWriteFiles = new ThreadLocal<bool>();
            s_IsDotsRuntime = new ThreadLocal<bool>();
        }

        static public void SetupContext(GeneratorExecutionContext executionContext)
        {
            ProjectPath = null;
            CanWriteFiles = false;
            WriteLogToDisk = false;
            IsDotsRuntime = executionContext.ParseOptions.PreprocessorSymbolNames.Contains("UNITY_DOTSRUNTIME");
            IsUnity2021_OrNewer = executionContext.ParseOptions.PreprocessorSymbolNames.Any(d => d == "UNITY_2022_1_OR_NEWER" || d == "UNITY_2021_3_OR_NEWER");
            //Setup the current project folder directory by inspecting the context for global options or additional files, depending on the current Unity version
            if (!IsUnity2021_OrNewer || IsDotsRuntime)
            {
                SupportTemplateFromAdditionalFiles = false;
                if (executionContext.AdditionalFiles.Any() && !string.IsNullOrEmpty(executionContext.AdditionalFiles[0].Path))
                    ProjectPath = Helpers.FindProjectFolderFromAdditionalFile(executionContext.AdditionalFiles[0].Path);
            }
            else
            {
                SupportTemplateFromAdditionalFiles = true;
                if (executionContext.AdditionalFiles.Any() && !string.IsNullOrEmpty(executionContext.AdditionalFiles[0].Path))
                    ProjectPath = executionContext.AdditionalFiles[0].GetText()?.ToString();
            }
            //Parse global options. They are used by both tests, and Editor (2021_OR_NEWER)
            if (executionContext.AnalyzerConfigOptions.GlobalOptions.TryGetValue(GlobalOptions.ProjectPath, out var projectpath))
                ProjectPath = projectpath;
            if (executionContext.AnalyzerConfigOptions.GlobalOptions.TryGetValue(GlobalOptions.OutputPath, out var outputFolder))
                OutputFolder = outputFolder;
            if (executionContext.AnalyzerConfigOptions.GlobalOptions.TryGetValue(GlobalOptions.TemplateFromAdditionalFiles, out var templateFromAdditionalFiles))
                SupportTemplateFromAdditionalFiles = templateFromAdditionalFiles == "1";

            if (!string.IsNullOrEmpty(ProjectPath))
            {
                Directory.CreateDirectory(GetOutputPath());
                CanWriteFiles = true;
            }

            if (executionContext.AnalyzerConfigOptions.GlobalOptions.TryGetValue(GlobalOptions.WriteFilesToDisk, out var outputToDisk))
                CanWriteFiles = outputToDisk == "1";
        }

        public static string GetOutputPath()
        {
            return Path.Combine(ProjectPath, OutputFolder);
        }

        // This path resolution is necessary for 2020.x where we need to resolve templates from packages
        // and other folders.
        private static string FindProjectFolderFromAdditionalFile(string folder)
        {
            var index = folder.LastIndexOf("/Library/", StringComparison.Ordinal);
            if(index < 0)
                index = folder.LastIndexOf("\\Library\\", StringComparison.Ordinal);
            return index > 0 ? folder.Substring(0, index) : null;
        }

        public static ulong ComputeVariantHash(ITypeSymbol variantType, ITypeSymbol componentType)
        {
            return ComputeVariantHash(Roslyn.Extensions.GetFullTypeName(variantType), Roslyn.Extensions.GetFullTypeName(componentType));
        }

        public static ulong ComputeVariantHash(string variantTypeFullname, string componentTypeFullName)
        {
            var hash = Utilities.TypeHash.FNV1A64("NetCode.GhostNetVariant");
            hash = Utilities.TypeHash.CombineFNV1A64(hash, Utilities.TypeHash.FNV1A64(componentTypeFullName));
            hash = Utilities.TypeHash.CombineFNV1A64(hash, Utilities.TypeHash.FNV1A64(variantTypeFullname));
            return hash;
        }

        public static SourceText WithInitialLineDirective(this SourceText sourceText, string generatedSourceFilePath)
        {
            var firstLine = sourceText.Lines.FirstOrDefault();
            return sourceText.WithChanges(new TextChange(firstLine.Span, $"#line 1 \"{generatedSourceFilePath}\"" + Environment.NewLine + firstLine));
        }
    }

    internal static class Debug
    {
        public static void LaunchDebugger()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Debugger.Launch();
            }
            else
            {
                string text = $"Attach to {Process.GetCurrentProcess().Id} netcode generator";

                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    StarProcess("/usr/bin/osascript", $"-e \"display dialog \\\"{text}\\\" with icon note buttons {{\\\"OK\\\"}}\"");
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    StarProcess("/usr/bin/zenity", $@"--info --title=""Attach Debugger"" --text=""{text}"" --no-wrap");
                }
            }
        }

        public static void LaunchDebugger(GeneratorExecutionContext context, string assembly)
        {
            if(context.Compilation.AssemblyName == assembly)
            {
                LaunchDebugger();
            }
        }

        public static void LaunchDebugger(Microsoft.CodeAnalysis.CSharp.Syntax.TypeDeclarationSyntax node, string[] names)
        {
            if(names.Contains(node.Identifier.ValueText))
            {
                LaunchDebugger();
            }
        }

        private static void StarProcess(string fileName, string arguments)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = false,
                RedirectStandardError = false,
                UseShellExecute = true,
                CreateNoWindow = false
            };

            var processTemp = new Process {StartInfo = startInfo, EnableRaisingEvents = true};
            processTemp.Start();
            processTemp.WaitForExit();
        }

        private const string LogFile = "SourceGenerator.log";
        private static string GetLogFilePath()
        {
            return Path.Combine(Helpers.GetOutputPath(), LogFile);
        }
        private static TextWriter GetOutputStream()
        {
            return Helpers.WriteLogToDisk ? File.AppendText(GetLogFilePath()) : Console.Out;
        }
        static void LogToDebugStream(string level, string message)
        {
            try
            {
                using var writer = GetOutputStream();
                writer.WriteLine($"[{level}]{message}");
            }
            catch (Exception flushEx)
            {
                Console.WriteLine($"Exception while writing to log: {flushEx.Message}");
            }
        }
        public static void LogException(Exception exception)
        {
            try
            {
                using var writer = GetOutputStream();
                writer.Write("[Exception]");
                writer.WriteLine(exception.ToString());
                writer.WriteLine("Callstack:");
                writer.Write(exception.StackTrace);
                writer.Write('\n');
            }
            catch (Exception flushEx)
            {
                Console.WriteLine($"Exception while writing to log: {flushEx.Message}");
            }
        }
        public static void LogInfo(string message)
        {
            LogToDebugStream("Info", message);
        }
        public static void LogWarning(string message)
        {
            LogToDebugStream("Warning", message);
        }
        public static void LogError(string message)
        {
            LogToDebugStream("Error", message);
        }
    }
}
