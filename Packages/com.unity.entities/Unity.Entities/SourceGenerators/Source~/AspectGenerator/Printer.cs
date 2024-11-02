using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Unity.Entities.SourceGen.Aspect
{
    public interface IPrintable
    {
        void Print(Printer printer);
    }
    public struct Printer
    {
        public StringBuilder Builder;
        private int CurrentIndentIndex;
        public string CurrentIndent => sm_Tabs[CurrentIndentIndex];

        public int IndentDepth => CurrentIndentIndex;
        // Array of indent string for each depth level starting with "" and up to 31 tabs.
        private static readonly string[] sm_Tabs = Enumerable.Range(start: 0, count: 32).Select(i => string.Join("", Enumerable.Repeat(element: "\t", count: i))).ToArray();

        public static Printer Default => new Printer(0);
        public static Printer DefaultLarge => new Printer(0, 1024*16);

        /// <summary>
        /// Get a copy of this printer with the same setting but new output.
        /// </summary>
        /// <param name="printer"></param>
        /// <returns></returns>
        public static Printer NewLike(Printer printer) => new Printer(printer.CurrentIndentIndex);
        
        public Printer(int indentCount)
        {
            System.Diagnostics.Debug.Assert(indentCount < sm_Tabs.Length);
            Builder = new StringBuilder();
            CurrentIndentIndex = indentCount;
        }
        public Printer(int indentCount, int capacity)
        {
            System.Diagnostics.Debug.Assert(indentCount < sm_Tabs.Length);
            Builder = new StringBuilder(capacity);
            CurrentIndentIndex = indentCount;
        }

        public Printer(StringBuilder builder, int indentCount)
        {
            System.Diagnostics.Debug.Assert(indentCount < sm_Tabs.Length);
            Builder = builder;
            CurrentIndentIndex = indentCount;
        }

        /// <summary>
        /// Allows to continue inline printing using a different printer
        /// </summary>
        /// <param name="printer"></param>
        /// <returns></returns>
        public Printer PrintWith(Printer printer) => printer;
        
        /// <summary>
        /// Allows to continue inline printing using the same printer from an function call
        /// </summary>
        /// <param name="printer"></param>
        /// <returns></returns>
        public Printer PrintWith(Func<Printer, Printer> func) => func(this);

        /// <summary>
        /// Creates a copy of this printer but with a relative indentCount
        /// </summary>
        /// <returns></returns>
        public Printer WithRelativeIndent(int indentCount) => new Printer(Builder, CurrentIndentIndex + indentCount);
        public Printer RelativeIndent(int indentCount)
        {
            CurrentIndentIndex += indentCount;
            return this;
        }

        /// <summary>
        /// Creates a copy of this printer but with a deeper indent
        /// </summary>
        /// <returns></returns>
        public Printer WithIncreasedIndent() => new Printer(Builder, CurrentIndentIndex + 1);

        public Printer IncreasedIndent()
        {
            ++CurrentIndentIndex;
            return this;
        }

        /// <summary>
        /// Creates a copy of this printer but with a shallower indent
        /// </summary>
        /// <returns></returns>
        public Printer WithDecreasedIndent() => new Printer(Builder, CurrentIndentIndex - 1);

        public Printer DecreasedIndent()
        {
            --CurrentIndentIndex;
            return this;
        }

        /// <summary>
        /// The current output of the printer.
        /// </summary>
        public string Result => Builder.ToString();

        /// <summary>
        /// Clear the output of the printer and reset indent
        /// </summary>
        /// <returns></returns>
        public Printer Clear()
        {
            Builder.Clear();
            CurrentIndentIndex = 0;
            return this;
        }

        /// <summary>
        /// Clear the output and set the current indent
        /// </summary>
        /// <param name="indentCount"></param>
        /// <returns></returns>
        public Printer ClearAndIndent(int indentCount)
        {
            System.Diagnostics.Debug.Assert(indentCount < sm_Tabs.Length);
            Builder.Clear();
            CurrentIndentIndex = indentCount;
            return this;
        }
        
        #region printing

        /// <summary>
        /// Print a string
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        public Printer Print(string text)
        {
            DebugTrace.Write(text);
            Builder.Append(text);
            return this;
        }
        
        /// <summary>
        /// Print indent
        /// </summary>
        /// <returns></returns>
        public Printer PrintBeginLine()
            => Print(CurrentIndent);
        
        /// <summary>
        /// Print indent and a string
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        public Printer PrintBeginLine(string text)
            => Print(CurrentIndent).Print(text);
        
        
        /// <summary>
        /// Print end-line
        /// </summary>
        /// <returns></returns>
        public Printer PrintEndLine()
        {
            DebugTrace.WriteLine();
            Builder.AppendLine();
            return this;
        }

        /// <summary>
        /// Print a string and an end-line
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        public Printer PrintEndLine(string text)
        {
            DebugTrace.WriteLine(text);
            Builder.AppendLine(text);
            return this;
        }

        /// <summary>
        /// Print indent, a string and an end-line
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        public Printer PrintLine(string text)
            => PrintBeginLine().PrintEndLine(text);

        /// <summary>
        /// Print a string if condition is truw
        /// </summary>
        /// <param name="condition"></param>
        /// <param name="text"></param>
        /// <returns></returns>
        public Printer PrintIf(bool condition, string text)
        {
            if(condition)
                Print(text);
            return this;
        }

        /// <summary>
        /// Print indent, a string and an end-line if a condition is true
        /// </summary>
        /// <param name="condition"></param>
        /// <param name="text"></param>
        /// <returns></returns>
        public Printer PrintLineIf(bool condition, string text)
        {
            if (condition)
                PrintLine(text);
            return this;
        }
        
        #endregion

        #region List

        /// <summary>
        /// Print item in a list with a separator
        /// </summary>
        public struct ListPrinter
        {
            public Printer Printer;
            public string Separator;
            public bool IsStarted;
            public ListPrinter(Printer printer, string separator)
            {
                Printer = printer;
                Separator = separator;
                IsStarted = false;
            }

            /// <summary>
            /// Get the next item printer
            /// </summary>
            /// <returns></returns>
            public Printer NextItemPrinter()
            {
                if (IsStarted)
                    Printer.Print(Separator);
                else
                    IsStarted = true;
                return Printer;
            }

            /// <summary>
            /// Print all the element in an IEnumerable as list items
            /// </summary>
            /// <param name="elements"></param>
            /// <returns></returns>
            public ListPrinter PrintAll(IEnumerable<string> elements)
            {
                foreach(var e in elements)
                    NextItemPrinter().Print(e);
                return this;
            }

            /// <summary>
            /// Get a multiline list version of this list
            /// </summary>
            public MultilineListPrinter AsMultiline => new MultilineListPrinter(this);

            /// <summary>
            /// Get a multiline list where each item is indented one level deeper
            /// </summary>
            public MultilineListPrinter AsMultilineIndented
            {
                get
                {
                    var printer = AsMultiline;
                    printer.ListPrinter.Printer = printer.ListPrinter.Printer.WithIncreasedIndent();
                    return printer;
                }
            }
        }

        /// <summary>
        /// Print item in a multi-line list such as:
        /// "item0, item1, ..., itemN"
        /// </summary>
        public struct MultilineListPrinter
        {
            public ListPrinter ListPrinter;
            public MultilineListPrinter(ListPrinter listPrinter)
            {
                ListPrinter = listPrinter;
            }

            /// <summary>
            /// Get the next item printer
            /// </summary>
            /// <returns></returns>
            public Printer NextItemPrinter()
            {

                if (ListPrinter.IsStarted)
                {
                    ListPrinter.Printer.PrintEndLine(ListPrinter.Separator);
                    ListPrinter.Printer.PrintBeginLine();
                }
                else
                    ListPrinter.IsStarted = true;
                return ListPrinter.Printer;
            }
        }

        /// <summary>
        /// Create a list printer from this printer
        /// </summary>
        /// <param name="separator"></param>
        /// <returns></returns>
        public ListPrinter AsListPrinter(string separator) => new ListPrinter(this, separator);
        #endregion

        #region Scope

        /// <summary>
        /// print the scope open string and return a new printer with deeper indent
        /// The returned printer must be terminated with CloseScope.
        /// </summary>
        /// <param name="scopeOpen"></param>
        /// <returns>a copy of this printer with a deeper indent</returns>
        public Printer ScopePrinter(string scopeOpen = "{")
        {
            PrintEndLine(scopeOpen);
            return WithIncreasedIndent();
        }

        /// <summary>
        /// Close a scope printer and print a string.
        /// </summary>
        /// <param name="scopedPrinter">the scope printer to close</param>
        /// <param name="scopeClose"></param>
        /// <returns>a copy of the closed scope printer with a shallower indent</returns>
        public Printer CloseScope(Printer scopedPrinter, string scopeClose = "{")
        {
            PrintBeginLine(scopeClose);
            return scopedPrinter.WithDecreasedIndent();
        }

        /// <summary>
        /// print the scope open string and increase indent
        /// </summary>
        /// <param name="scopeOpen"></param>
        /// <returns>this</returns>
        public Printer OpenScope(string scopeOpen = "{")
        {
            PrintEndLine();
            PrintLine(scopeOpen);
            IncreasedIndent();
            return this;
        }

        /// <summary>
        /// Decrease indent and print the scope close string.
        /// </summary>
        /// <param name="scopeClose"></param>
        /// <returns>this</returns>
        public Printer CloseScope(string scopeClose = "}")
        {
            DecreasedIndent();
            PrintLine(scopeClose);
            return this;
        }
            

        #endregion

        /// <summary>
        /// Print a IPrintable to a string
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="printable"></param>
        /// <returns></returns>
        public static string PrintToString<T>(T printable)
            where T : struct, IPrintable
        {
            var printer = Printer.Default;
            printable.Print(printer);
            return printer.Builder.ToString();
        }

        /// <summary>
        /// Access debug printing interface
        /// </summary>
        public DebugPrinter Debug => new DebugPrinter(this);
    }

    /// <summary>
    /// Printer used for debug purposes only.
    /// Some boxing may happen
    /// </summary>
    public struct DebugPrinter
    {
        public Printer BasePrinter;

        public static implicit operator Printer(DebugPrinter dp) => dp.BasePrinter;
        public DebugPrinter(Printer basePrinter)
        {
            BasePrinter = basePrinter;
        }

        public DebugPrinter Print(string text)
        {
            BasePrinter.Print(text);
            return this;
        }
        public DebugPrinter Print<T>(T printable)
            where T : struct, IPrintable
        {
            printable.Print(BasePrinter);
            return this;
        }

        /// <summary>
        /// Print indent, a string and an end-line
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        public DebugPrinter PrintLine(string text)
        {
            BasePrinter.PrintLine(text);
            return this;
        }

        /// <summary>
        /// Print indent and a string
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        public DebugPrinter PrintBeginLine(string text)
        {
            BasePrinter.PrintBeginLine(text);
            return this;
        }

        /// <summary>
        /// Print indent
        /// </summary>
        /// <returns></returns>
        public DebugPrinter PrintBeginLine()
        {
            BasePrinter.PrintBeginLine();
            return this;
        }

        /// <summary>
        /// Print a string and an end-line
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        public DebugPrinter PrintEndLine(string text)
        {
            BasePrinter.PrintEndLine(text);
            return this;
        }

        /// <summary>
        /// Print end-line
        /// </summary>
        /// <returns></returns>
        public DebugPrinter PrintEndLine()
        {
            BasePrinter.PrintEndLine();
            return this;
        }

        /// <summary>
        /// Print a printable or "<null>" when null
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="printable"></param>
        /// <returns></returns>
        public DebugPrinter PrintNullable<T>(T printable)
            where T : class, IPrintable
        {
            if (printable == null)
                Print("<null>");
            else
                printable.Print(BasePrinter);
            return this;
        }

        /// <summary>
        /// Print:
        /// kv.Key = kv.Value
        /// </summary>
        /// <typeparam name="TKey"></typeparam>
        /// <typeparam name="TValue"></typeparam>
        /// <param name="kv"></param>
        /// <returns></returns>
        public DebugPrinter Print<TKey, TValue>(KeyValuePair<TKey, TValue> kv)
            where TValue : struct, IPrintable
        {
            Print(kv.Key.ToString());
            Print(" = ");
            Print(kv.Value);
            return this;
        }
        
        /// <summary>
        /// Print: $"{key} = {value}"
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public DebugPrinter PrintKeyValue(string key, string value)
        {
            Print(key);
            Print(" = ");
            Print(value);
            return this;
        }

        /// <summary>
        /// Print $"{key} = {value}" where value is a printable
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public DebugPrinter PrintKeyValue<TValue>(string key, TValue value)
            where TValue : struct, IPrintable
        {
            BasePrinter.Print(key);
            BasePrinter.Print(" = ");
            Print(value);
            return this;
        }

        /// <summary>
        /// Print $"{key} = {value}" where value is a printable or "<null>" when null
        /// </summary>
        /// <typeparam name="TValue"></typeparam>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public DebugPrinter PrintKeyValueNullable<TValue>(string key, TValue value)
            where TValue : class, IPrintable
        {
            BasePrinter.Print(key);
            BasePrinter.Print(" = ");
            PrintNullable(value);
            return this;
        }

        /// <summary>
        /// Print:
        /// key = { TValue0, TValue1, ..., TValueN }
        /// </summary>
        /// <typeparam name="TValue"></typeparam>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <param name="openScope"></param>
        /// <param name="closeScope"></param>
        /// <returns></returns>
        public DebugPrinter PrintKeyList<TValue>(string key, IEnumerable<TValue> value, string openScope = "{", string closeScope = "}")
            where TValue : struct, IPrintable
        {
            if (value == null)
            {
                PrintKeyValue(key, "<null>");
                return this;
            }
            Print(key);
            var scope = BasePrinter.Print(" = IEnumerable<>").ScopePrinter(openScope);
            var list = scope.PrintBeginLine().AsListPrinter(", ").AsMultiline;
            foreach (var v in value)
                new DebugPrinter(list.NextItemPrinter()).Print(v);
            scope.PrintEndLine();
            BasePrinter.CloseScope(scope, closeScope);
            Print("}");
            return this;
        }

        /// <summary>
        /// Print:
        /// key = {
        ///     TKey0 = TValue0,
        ///     TKey1 = TValue1,
        ///     ... = ...,
        ///     TKeyN = TValueN
        /// }
        /// </summary>
        /// <typeparam name="TKey"></typeparam>
        /// <typeparam name="TValue"></typeparam>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <param name="openScope"></param>
        /// <param name="closeScope"></param>
        /// <returns></returns>
        public DebugPrinter PrintKeyList<TKey, TValue>(string key, IEnumerable<KeyValuePair<TKey, TValue>> value, string openScope = "{", string closeScope = "}")
            where TValue : struct, IPrintable
        {
            Print(key);
            var scope = BasePrinter.Print(" = ").ScopePrinter(openScope);
            var list = scope.PrintBeginLine().AsListPrinter(", ").AsMultiline;
            foreach (var v in value)
                new DebugPrinter(list.NextItemPrinter()).Print(v);
            scope.PrintEndLine();
            BasePrinter.CloseScope(scope, closeScope);
            return this;
        }
        
        /// <summary>
        /// Print a multiline list of item with a given separator
        /// ex:
        ///     item0,
        ///     item1,
        ///     ...,
        ///     itemN,
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="separator"></param>
        /// <param name="items"></param>
        /// <returns></returns>
        public DebugPrinter PrintListMultiline<T>(string separator, IEnumerable<T> items)
            where T : struct, IPrintable
        {
            using var itor = items.GetEnumerator();
            if (itor.MoveNext())
                itor.Current.Print(BasePrinter);

            while (itor.MoveNext())
            {
                PrintEndLine(separator);
                PrintBeginLine();
                itor.Current.Print(BasePrinter);
            }
            return this;
        }

    }
    
    /// <summary>
    /// Printer that replicate the full scope path declaration (namespace, class and struct declarations) to a given SyntaxNode
    /// </summary>
    public struct SyntaxNodeScopePrinter
    {
        private SyntaxNode m_LeafNode;
        public Printer Printer;
        public SyntaxNodeScopePrinter(Printer printer, SyntaxNode node)
        {
            Printer = printer;
            m_LeafNode = node;
        }

        public SyntaxNodeScopePrinter PrintScope(SyntaxNode node)
        {
            if (node.Parent != null)
                PrintScope(node.Parent);

            switch (node)
            {
                case NamespaceDeclarationSyntax ns:
                    Printer.PrintBeginLine();
                    foreach (var m in ns.Modifiers)
                        Printer.Print(m.ToString()).Print(" ");
                    Printer = Printer.Print("namespace ").PrintEndLine(ns.Name.ToString()).PrintLine("{").WithIncreasedIndent();
                    break;
                case ClassDeclarationSyntax cl:
                    Printer.PrintBeginLine();
                    foreach (var m in cl.Modifiers)
                        Printer.Print(m.ToString()).Print(" ");
                    Printer = Printer.Print("class ").PrintEndLine(cl.Identifier.Text).PrintLine("{").WithIncreasedIndent();
                    break;
                case StructDeclarationSyntax st:
                    Printer.PrintBeginLine();
                    foreach (var m in st.Modifiers)
                        Printer.Print(m.ToString()).Print(" ");
                    Printer = Printer.Print("struct ").PrintEndLine(st.Identifier.Text).PrintLine("{").WithIncreasedIndent();
                    break;
            }
            return this;
        }
        public SyntaxNodeScopePrinter PrintOpen() => PrintScope(m_LeafNode);
        public SyntaxNodeScopePrinter PrintClose()
        {
            var parent = m_LeafNode;
            while (parent != null)
            {
                switch (parent)
                {
                    case NamespaceDeclarationSyntax _:
                    case ClassDeclarationSyntax _:
                    case StructDeclarationSyntax _:
                        Printer = Printer.WithDecreasedIndent().PrintLine("}");
                        break;
                }
                parent = parent.Parent;
            }
            return this;
        }
    }
}
