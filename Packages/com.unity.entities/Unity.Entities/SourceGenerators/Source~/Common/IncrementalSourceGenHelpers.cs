using System;
using Microsoft.CodeAnalysis;

namespace Unity.Entities.SourceGen.Common
{
    public static class IncrementalSourceGenHelpers
    {
        public struct SourceGenConfig
        {
            public string projectPath;
            public bool performSafetyChecks;
            public bool isDotsDebugMode;
        }

        struct ParseOptionConfig
        {
            public bool PathIsInFirstAdditionalTextItem;
            public bool performSafetyChecks;
            public bool isDotsDebugMode;
        }

        public static IncrementalValueProvider<SourceGenConfig>
            GetSourceGenConfigProvider(IncrementalGeneratorInitializationContext context)
        {
            // Generate provider that lazily provides options based off of context's parse options
            var parseOptionConfigProvider = context.ParseOptionsProvider.Select((options, token) =>
                {
                    var parseOptionsConfig = new ParseOptionConfig();
                    SourceOutputHelpers.Setup(options);

                    foreach (var symbolName in options.PreprocessorSymbolNames)
                    {
                        parseOptionsConfig.performSafetyChecks |= symbolName == "ENABLE_UNITY_COLLECTIONS_CHECKS";
                        parseOptionsConfig.isDotsDebugMode |= symbolName == "UNITY_DOTS_DEBUG";
                    }
                    parseOptionsConfig.PathIsInFirstAdditionalTextItem = true;

                    return parseOptionsConfig;
                });

            // Combine the AdditionalTextsProvider with the provider constructed above to provide all SourceGenConfig options lazily
            var sourceGenConfigProvider = context.AdditionalTextsProvider.Collect()
                .Combine(parseOptionConfigProvider)
                .Select((lTextsRIsInsideText, token) =>
            {
                var config = new SourceGenConfig
                {
                    performSafetyChecks = lTextsRIsInsideText.Right.performSafetyChecks,
                    isDotsDebugMode = lTextsRIsInsideText.Right.isDotsDebugMode
                };

                // needs to be disabled for e.g. Sonarqube static code analysis (which also uses analyzers)
                if (Environment.GetEnvironmentVariable("SOURCEGEN_DISABLE_PROJECT_PATH_OUTPUT") == "1")
                    return config;

                var texts = lTextsRIsInsideText.Left;
                var projectPathIsInFirstAdditionalTextItem = lTextsRIsInsideText.Right.PathIsInFirstAdditionalTextItem;

                if (texts.Length == 0 || string.IsNullOrEmpty(texts[0].Path))
                    return config;

                var path = projectPathIsInFirstAdditionalTextItem ? texts[0].GetText(token)?.ToString() : texts[0].Path;
                config.projectPath = path?.Replace('\\', '/');

                return config;
            });

            return sourceGenConfigProvider;
        }
    }
}
