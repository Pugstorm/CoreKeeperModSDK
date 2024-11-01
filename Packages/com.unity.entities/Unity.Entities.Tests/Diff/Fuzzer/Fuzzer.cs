#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Random = Unity.Mathematics.Random;

namespace Unity.Entities.Tests.Fuzzer
{
    interface IFuzzerCommand<TFuzzer>
    {
        void ApplyCommand(TFuzzer state);
    }

    interface IFuzzer : IDisposable
    {
        void Validate();
    }

    struct FuzzerCommandGenerator<TFuzzer>
    {
        public string Id;
        public SampleCommandDelegate SampleCommand;
        public DeserializeCommandDelegate DeserializeCommand;

        public delegate IFuzzerCommand<TFuzzer> SampleCommandDelegate(TFuzzer state, ref Random rng, out string serializedCommand);
        public delegate IFuzzerCommand<TFuzzer> DeserializeCommandDelegate(string serializedCommand);
    }

    static class Fuzzer
    {
        const string SerializationSeparator = "_|_";
        const string ValidationMagicId = "Validate";

        struct WeightedSampler
        {
            private readonly int _totalWeight;
            private readonly int[] _runningSumWeights;

            internal WeightedSampler(IEnumerable<int> weights)
            {
                _runningSumWeights = weights.ToArray();
                int s = 0;
                for (int i = 0; i < _runningSumWeights.Length; i++)
                {
                    s += _runningSumWeights[i];
                    _runningSumWeights[i] = s;
                }

                _totalWeight = s;
            }

            internal int SampleCommandIndex(ref Random rng)
            {
                int w = rng.NextInt(0, _totalWeight);
                for (int i = 0; i < _runningSumWeights.Length; i++)
                {
                    if (w < _runningSumWeights[i])
                        return i;
                }
                return _runningSumWeights.Length - 1;
            }
        }

        class ValidationCommandImpl<TFuzzer> : IFuzzerCommand<TFuzzer> where TFuzzer: IFuzzer
        {
            public void ApplyCommand(TFuzzer state) => state.Validate();
        }

        static CommandData<TFuzzer> ValidationCommandData<TFuzzer>() where TFuzzer : IFuzzer
            => new CommandData<TFuzzer>
        {
            GeneratorId = ValidationMagicId,
            Command = new ValidationCommandImpl<TFuzzer>()
        };

        public static FuzzerCommandGenerator<TFuzzer> ValidationCommandGenerator<TFuzzer>() where TFuzzer : IFuzzer
            => new FuzzerCommandGenerator<TFuzzer>
            {
                Id = ValidationMagicId,
                DeserializeCommand = _ => new ValidationCommandImpl<TFuzzer>(),
                SampleCommand = (TFuzzer state, ref Random rng, out string command) =>
                {
                    command = default;
                    return new ValidationCommandImpl<TFuzzer>();
                }
            };

        public struct CommandData<TFuzzer>
        {
            public IFuzzerCommand<TFuzzer> Command;
            public string SerializedCommand;
            public string GeneratorId;

            public override string ToString() => GeneratorId + SerializationSeparator + SerializedCommand;
            public string ToCSharpString() => ToString().Replace("\"", "\"\"");
        }

        struct SubSampledList<T>
        {
            public List<int> Indices;
            public List<T> Data;

            public SubSampledList(IEnumerable<T> data)
            {
                Data = data.ToList();
                Indices = new List<int>(Data.Count);
                for (int i = 0; i < Data.Count; i++)
                    Indices.Add(i);
            }

            public IEnumerable<T> Enumerate()
            {
                for (int i = 0; i < Indices.Count; i++)
                {
                    if (Indices[i] >= 0)
                        yield return Data[Indices[i]];
                }
            }
        }

        /// <summary>
        /// Reduces the input list of commands to the shortest subsequence of commands that can be obtained by removing
        /// one command at a time while retaining the same stacktrace for the first failure.
        /// </summary>
        /// <param name="makeFuzzer"></param>
        /// <param name="commands"></param>
        /// <typeparam name="TFuzzer"></typeparam>
        /// <returns>The reduced list of commands.</returns>
        public static List<CommandData<TFuzzer>> Reduce<TFuzzer>(Func<TFuzzer> makeFuzzer, List<CommandData<TFuzzer>> commands) where TFuzzer : IFuzzer
        {
            var (failureIndex, stackTrace) = CaptureStackTrace(commands);
            if (stackTrace == null)
                return commands;
            var actualCommands = commands.Take(failureIndex + 1);

            // The approach to reducing the failure case is to simply iteratively try to remove commands without changing
            // the stack trace, starting at the end.
            var subsampled = new SubSampledList<CommandData<TFuzzer>>(actualCommands);
            bool progress;
            do
            {
                progress = false;

                // It is probably more likely that we'll find a command to eliminate when we start at the end.
                for (int j = subsampled.Indices.Count - 1; j >= 0; j--)
                {
                    var idx = subsampled.Indices[j];
                    subsampled.Indices[j] = -1;
                    var (_, newStackTrace) = CaptureStackTrace(subsampled.Enumerate());
                    if (newStackTrace == stackTrace)
                    {
                        subsampled.Indices.RemoveAt(j);
                        progress = true;
                        break;
                    }
                    subsampled.Indices[j] = idx;
                }
            } while (progress);

            return subsampled.Enumerate().ToList();

            (int failureIndex, string stacktrace) CaptureStackTrace(IEnumerable<CommandData<TFuzzer>> newCommands)
            {
                int i = 0;
                try
                {
                    using (var fuzzer = makeFuzzer())
                    {
                        foreach (var cmd in newCommands)
                        {
                            cmd.Command.ApplyCommand(fuzzer);
                            i++;
                        }
                    }
                }
                catch (Exception ex)
                {
                    return (i, ex.StackTrace);
                }
                return (i, null);
            }
        }

        public static IEnumerable<CommandData<TFuzzer>> ParseLog<TFuzzer>(List<FuzzerCommandGenerator<TFuzzer>> generators, IEnumerable<string> lines) where TFuzzer : IFuzzer
        {
            var idToIndex = new Dictionary<string, int>();
            for (int i = 0; i < generators.Count; i++)
                idToIndex[generators[i].Id] = i;

            foreach (var l in lines)
            {
                var line = l.Trim();
                if (line.Length == 0)
                    continue;
                var split = line.IndexOf(SerializationSeparator, StringComparison.Ordinal);
                if (split <= -1)
                    throw new Exception("Missing command ID for line:\n" + line);
                var id = line.Substring(0, split);
                if (id == ValidationMagicId)
                    yield return ValidationCommandData<TFuzzer>();
                else if (idToIndex.TryGetValue(id, out int idx))
                {
                    var serializedCommand = line.Substring(split + SerializationSeparator.Length);
                    var command = generators[idx].DeserializeCommand(serializedCommand);
                    if (command == null)
                        throw new Exception("Failed to parse command from line:\n " + line);
                    yield return new CommandData<TFuzzer>
                    {
                        Command = command,
                        SerializedCommand = serializedCommand,
                        GeneratorId = id
                    };
                }
                else
                    throw new Exception("Unknown id \"" + id + '"');
            }
        }

        public static void Run<TFuzzer>(this TFuzzer fuzzer, IEnumerable<CommandData<TFuzzer>> commands, Action<CommandData<TFuzzer>> logger) where TFuzzer : IFuzzer
        {
            foreach (var cmd in commands)
            {
                logger?.Invoke(cmd);
                cmd.Command.ApplyCommand(fuzzer);
            }
            fuzzer.Validate();
        }

        public static IEnumerable<CommandData<TFuzzer>> GenerateCommands<TFuzzer>(this TFuzzer fuzzer, List<(FuzzerCommandGenerator<TFuzzer> Generator, int Weight)> weightedGenerators, uint seed, int steps, int maxStepsBetweenValidation) where TFuzzer : IFuzzer
        {
            var rng = Random.CreateFromIndex(seed);
            int stepsSinceValidation = 0;
            var commands = weightedGenerators;
            var sampler = new WeightedSampler(commands.Select(t => t.Weight));
            const int maxAttempts = 100;
            for (int i = 0; i < steps; i++)
            {
                CommandData<TFuzzer> cmd;
                int a = 0;
                while (true) {
                    int commandIndex = sampler.SampleCommandIndex(ref rng);
                    if (TrySampleCommand(commands[commandIndex].Generator, fuzzer, ref rng, out cmd))
                        break;
                    if (++a >= maxAttempts)
                    {
                        Debug.LogWarning("Failed to sample more commands");
                        yield break;
                    }
                }

                yield return cmd;
                if (cmd.Command is ValidationCommandImpl<TFuzzer>)
                    stepsSinceValidation = 0;
                else
                {
                    stepsSinceValidation++;
                    if (stepsSinceValidation >= maxStepsBetweenValidation)
                    {
                        stepsSinceValidation = 0;
                        yield return ValidationCommandData<TFuzzer>();
                    }
                }
            }
        }

        private static bool TrySampleCommand<TFuzzer>(this FuzzerCommandGenerator<TFuzzer> gen, TFuzzer state, ref Random rng, out CommandData<TFuzzer> cmdData)
        {
            var cmd = gen.SampleCommand(state, ref rng, out var serializedCommand);
            if (cmd == null)
            {
                cmdData = default;
                return false;
            }
            cmdData = new CommandData<TFuzzer>
            {
                Command = cmd,
                SerializedCommand = serializedCommand,
                GeneratorId = gen.Id
            };
            return true;
        }
    }
}
#endif
