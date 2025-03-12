using System.Text;
using System.IO;
using UnityEngine;

namespace Unity.PerformanceTesting.Benchmark
{
    internal class MarkdownBuilder
    {
        StringBuilder sb = new StringBuilder(32768);
        int blockDepth = 0;

        void Prefix()
        {
            if (blockDepth == 0)
                return;
            int len = sb.Length;
            if (len == 0 || sb[len - 1] == '\n')
                sb.Append($"{new string('>', blockDepth)} ");
        }

        MarkdownBuilder EnsureBlankLine()
        {
            int len = sb.Length;
            if ((len > 0 && sb[len - 1] != '\n'))
                Br().Br();
            else if (len > 1 && sb[len - 2] != '\n')
                Br();
            return this;
        }

        public MarkdownBuilder Append(string text)
        {
            Prefix();
            sb.Append(text);
            return this;
        }

        public MarkdownBuilder AppendLine(string text) => Append(text.TrimEnd('\n')).BrParagraph().Br();
        public MarkdownBuilder AppendLines(string[] lines)
        {
            foreach (string line in lines)
                AppendLine(line);
            return this;
        }
        public MarkdownBuilder AppendLines(string line, params string[] optLines)
        {
            AppendLine(line);
            return AppendLines(optLines);
        }
        public MarkdownBuilder Header(int level, string text) => EnsureBlankLine().Append($"{new string('#', Mathf.Clamp(level, 1, 6))} {text}").Br().Br();
        public MarkdownBuilder HorizontalLine() => EnsureBlankLine().Append("---").Br();
        public MarkdownBuilder Br() => Append("\n");
        public MarkdownBuilder BrParagraph() => Append("<br/>");
        public MarkdownBuilder Italic(string text) => Append($"*{text}*");
        public MarkdownBuilder Bold(string text) => Append($"**{text}**");
        public MarkdownBuilder BoldItalic(string text) => Append($"***{text}***");
        public MarkdownBuilder Code(string text) => Append($"`{text}`");
        public MarkdownBuilder Link(string url) => Append($"<{url}>");
        public MarkdownBuilder Link(string url, string name) => Append($"[{name}]({url.Replace(" ", "%20")})");
        public MarkdownBuilder Link(string url, string name, string tooltip) => Append($"[{name}]({url} \"{tooltip}\")");
        public MarkdownBuilder LinkHeader(string headerName) => Append($"[{headerName}](#{headerName.Replace(" ", "-").ToLower()})");
        public MarkdownBuilder ListItem(int zeroBasedDepth) => Append($"{new string(' ', Mathf.Clamp(zeroBasedDepth * 2, 0, 6))}- ");
        public MarkdownBuilder ListItem(int zeroBasedDepth, string text) => Append($"{new string(' ', Mathf.Clamp(zeroBasedDepth * 2, 0, 6))}- {text}").Br();

        public MarkdownBuilder BeginBlock()
        {
            EnsureBlankLine();  // *before* increasing block depth
            blockDepth++;
            return this;
        }

        public MarkdownBuilder EndBlock()
        {
            if (blockDepth > 0)
                blockDepth--;
            return EnsureBlankLine();  // *after* decreasing block depth
        }

        public MarkdownBuilder TableHeader(bool alignRightFirst, string columnName, bool alignRightOthers, params string[] optColumnNames)
        {
            EnsureBlankLine();
            TableRow(columnName, optColumnNames);
            if (alignRightFirst)
                Append("|--:|");
            else
                Append("|---|");
            for (int i = 0; i < optColumnNames.Length; i++)
            {
                if (alignRightOthers)
                    Append($"--:|");
                else
                    Append($"---|");
            }
            return Br();
        }

        public MarkdownBuilder TableRow(string columnData, params string[] optColumnData)
        {
            Append($"| {columnData} |");
            for (int i = 0; i < optColumnData.Length; i++)
                Append($" {optColumnData[i]} |");
            return Br();
        }

        public MarkdownBuilder Note(string title, string descLine, params string[] optDescLines) => BeginBlock().Bold(title).Br().Br().AppendLines(descLine, optDescLines).EndBlock();
        public MarkdownBuilder Note(string title, string[] descLines) => BeginBlock().Bold(title).Br().Br().AppendLines(descLines).EndBlock();
        public MarkdownBuilder Note(string[] descLines) => BeginBlock().AppendLines(descLines).EndBlock();

        public override string ToString() => sb.ToString();
        public void Save(string path) => File.WriteAllText(path, ToString());
    }

}
