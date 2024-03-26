using System;
using System.Collections.Generic;

namespace Unity.NetCode.Generators
{
    // The CommandSerializer instances are created by CodeGenerator. The class itself is not threadsafe,
    // but since every SourceGenerator has its own Context it is safe use.
    // Please avoid to use shared static variables or state here and verify that in case you need, they are immutable or thread safe.
    internal class CommandSerializer
    {
        public enum Type
        {
            Rpc,
            Command,
            Input
        }
        private readonly TypeInformation m_TypeInformation;
        private GhostCodeGen m_CommandGenerator;
        private readonly TypeTemplate m_Template;

        public Type CommandType { get; }

        public CommandSerializer(CodeGenerator.Context context, Type t)
        {
            CommandType = t;
            string template = String.Empty;
            switch (t)
            {
                case Type.Rpc:
                    template = CodeGenerator.RpcSerializer;
                    break;
                case Type.Command:
                    template = CodeGenerator.CommandSerializer;
                    break;
                case Type.Input:
                    template = CodeGenerator.InputSynchronization;
                    break;
            }
            var generator = context.codeGenCache.GetTemplate(template);
            m_CommandGenerator = generator.Clone();
        }
        public CommandSerializer(CodeGenerator.Context context, Type t, TypeInformation information) : this(context, t)
        {
            m_TypeInformation = information;
        }

        public CommandSerializer(CodeGenerator.Context context, Type t, TypeInformation information, TypeTemplate template) : this(context, t)
        {
            m_TypeInformation = information;
            m_Template = template;
        }

        public void AppendTarget(CommandSerializer typeSerializer)
        {
            m_CommandGenerator.Append(typeSerializer.m_CommandGenerator);
        }

        public void GenerateFields(CodeGenerator.Context context, string parent = null)
        {
            if (m_Template == null)
                return;

            var generator = context.codeGenCache.GetTemplateWithOverride(m_Template.TemplatePath, m_Template.TemplateOverridePath);
            generator = generator.Clone();

            var fieldName = string.IsNullOrEmpty(parent)
                ? m_TypeInformation.FieldName
                : $"{parent}.{m_TypeInformation.FieldName}";

            if (CommandType == Type.Input)
            {
                // Write the fragments for incrementing/decrementing InputEvent types inside the input struct
                // This is done for the count (integer) type nested inside the InputEvent struct (parent)
                if (m_TypeInformation.DeclaringTypeFullName == "Unity.NetCode.InputEvent")
                {
                    m_CommandGenerator.Replacements.Add("EVENTNAME", m_TypeInformation.Parent);
                    m_CommandGenerator.GenerateFragment("INCREMENT_INPUTEVENT", m_CommandGenerator.Replacements, m_CommandGenerator);
                    m_CommandGenerator.GenerateFragment("DECREMENT_INPUTEVENT", m_CommandGenerator.Replacements, m_CommandGenerator);
                }
                // No further processing needed as the rest of the fields will be handled by command template
                return;
            }

            generator.Replacements.Add("COMMAND_FIELD_NAME", fieldName);
            generator.Replacements.Add("COMMAND_FIELD_TYPE_NAME", m_TypeInformation.FieldTypeName);

            generator.GenerateFragment("COMMAND_READ", generator.Replacements, m_CommandGenerator);
            generator.GenerateFragment("COMMAND_WRITE", generator.Replacements, m_CommandGenerator);

            if (CommandType != Type.Rpc)
            {
                generator.GenerateFragment("COMMAND_READ_PACKED", generator.Replacements, m_CommandGenerator);
                generator.GenerateFragment("COMMAND_WRITE_PACKED", generator.Replacements, m_CommandGenerator);
                if (!m_TypeInformation.CanBatchPredict)
                {
                    generator.Replacements.Add("GHOST_MASK_INDEX", "0");
                    generator.Replacements.Add("GHOST_FIELD_NAME", fieldName);
                    if(generator.HasFragment("GHOST_CALCULATE_INPUT_CHANGE_MASK"))
                        generator.GenerateFragment("GHOST_CALCULATE_INPUT_CHANGE_MASK", generator.Replacements, m_CommandGenerator,
                            "GHOST_COMPARE_INPUTS");
                    else
                        generator.GenerateFragment("GHOST_CALCULATE_CHANGE_MASK", generator.Replacements, m_CommandGenerator,
                            "GHOST_COMPARE_INPUTS");
                }
            }
        }

        public void GenerateSerializer(CodeGenerator.Context context, TypeInformation typeInfo)
        {
            var replacements = new Dictionary<string, string>
            {
                {"COMMAND_NAME", context.generatorName.Replace(".", "").Replace('+', '_')},
                {"COMMAND_NAMESPACE", context.generatedNs},
                {"COMMAND_COMPONENT_TYPE", typeInfo.TypeFullName.Replace('+', '.')}
            };

            if (!string.IsNullOrEmpty(typeInfo.Namespace))
                context.imports.Add(typeInfo.Namespace);

            foreach (var ns in context.imports)
            {
                replacements["COMMAND_USING"] = CodeGenerator.GetValidNamespaceForType(context.generatedNs, ns);
                m_CommandGenerator.GenerateFragment("COMMAND_USING_STATEMENT", replacements);
            }

            var serializerName = context.generatorName + "CommandSerializer.cs";
            m_CommandGenerator.GenerateFile(serializerName, typeInfo.Namespace, replacements, context.batch);
        }

        public override string ToString()
        {
            var debugInformation = m_TypeInformation.ToString();
            debugInformation += m_Template?.ToString();
            debugInformation += m_CommandGenerator?.ToString();
            return debugInformation;
        }
    }
}
