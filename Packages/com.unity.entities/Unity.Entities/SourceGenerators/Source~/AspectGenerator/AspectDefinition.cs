using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Unity.Entities.SourceGen.Common;

namespace Unity.Entities.SourceGen.Aspect
{

    /// <summary>
    /// An AspectField represents the associated aspect-functionalities to a FieldDeclaration of the aspect StructDeclaration
    /// </summary>
    public class AspectField : IPrintable
    {
        public AspectDefinition AspectDefinition;

        public IFieldSymbol Symbol;

        // Name of the user declared field.
        public string FieldName;

        /// <summary>
        /// Name of the field preempted with the full nested aspect path.
        /// ex: "MyEnclosingAspect.MyNestedAspect.MyFieldName"
        /// </summary>
        public string NestedName => $"{AspectDefinition.GetNestedName(".")}.{FieldName}";
        // used for generating declarations of member field name
        public string InternalFieldName => $"{AspectDefinition.GetNestedName("_")}_{FieldName}";
        public string InternalVariableName => $"{AspectDefinition.GetNestedName("_").ToLower()}_{FieldName.ToLower()}";

        public string GetParameterName(string tag) => InternalVariableName + tag;
        public string GetPrimitiveName(string tag) => FieldName + tag;
        public PrimitiveBinding Bind => new PrimitiveBinding(TypeName, IsReadOnly, IsOptional);

        // Used for nested aspect field to override the binding of the nested aspect's fields.
        public PrimitiveBinding BindOverride => new PrimitiveBinding(null, IsReadOnly, IsOptional);

        public string TypeName;

        public bool IsReadOnly;
        public bool IsOptional;
        public bool IsZeroSize;
        public bool IsNestedAspect;

        public void Print(Printer printer)
        {
            printer.Print($"AspectField({AspectDefinition.Name}.{FieldName}, {TypeName}, {GetType().Name} at {Symbol.Locations.FirstOrDefault()})");
        }
    }

    public struct NestedAspectDefinition
    {
        public AspectField AspectField;
        public AspectDefinition Definition;
    }

    public class AspectDefinition
    {
        public AspectDefinition(ITypeSymbol symbol)
        {
            Symbol = symbol;
            Name = Symbol.Name;
            FullName = Symbol.ToFullName();
        }

        /// <summary>
        /// All StructDeclarationSyntax that are partial declaration of the aspect.
        /// </summary>
        public List<StructDeclarationSyntax> SourceSyntaxNodes = new List<StructDeclarationSyntax>();
        public ITypeSymbol Symbol;
        public AspectDefinition Parent;
        public NestedAspectDefinition[] NestedAspects;

        /// <summary>
        /// Name of the aspect struct from the source code.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Full type name of the aspect struct from the source code (Including namespace)
        /// </summary>
        public string FullName { get; }

        public string GetNestedName(string seperator)
        {
            if (Parent == null)
                return Name;
            return Parent.GetNestedName(seperator) + seperator + Name;
        }

        public PrimitivesRouter PrimitivesRouter = PrimitivesRouter.Default;

        /// <summary>
        /// Fields that require data to be set in the aspect (must be constructed) and 'struct-of-array' data like TypeHandle, ComponentLookup or lookup structures.
        /// </summary>
        public List<AspectField> FieldsNeedConstruction = new List<AspectField>();

        /// <summary>
        /// Fields that requires to be default constructed.
        /// </summary>
        public List<AspectField> FieldsRequiringDefaultConstruction = new List<AspectField>();

        /// <summary>
        /// Aspect fields require output in all 3 structs: Lookup, ResolvedChunk, TypeHandle
        /// </summary>
        public List<AspectField> AspectFields = new List<AspectField>();

        public bool HasEntityField => PrimitivesRouter.HasEntityField;

        /// <summary>
        /// Tell if any of the partial declarations of the aspect has an attribute
        /// </summary>
        /// <param name="typeNameNamesapce"></param>
        /// <param name="typeName"></param>
        /// <returns></returns>
        public bool SyntaxHasAttributeCandidate(string typeNameNamesapce, string typeName) => SourceSyntaxNodes.Any(x => x.HasAttributeCandidate(typeNameNamesapce, typeName));

        public bool IsIAspectCreateCorrectlyImplementedByUser { get; set; }
    }
}
