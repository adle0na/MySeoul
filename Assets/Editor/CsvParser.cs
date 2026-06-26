using System.Collections.Generic;
using System.Text;

namespace SeoulLast.EditorTools
{
    // 따옴표/내장 콤마/줄바꿈을 처리하는 최소 CSV 파서.
    public static class CsvParser
    {
        public static List<List<string>> Parse(string text)
        {
            var rows = new List<List<string>>();
            var cur = new List<string>();
            var sb = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < text.Length && text[i + 1] == '"') { sb.Append('"'); i++; }
                        else inQuotes = false;
                    }
                    else sb.Append(c);
                }
                else
                {
                    if (c == '"') inQuotes = true;
                    else if (c == ',') { cur.Add(sb.ToString()); sb.Clear(); }
                    else if (c == '\r') { /* skip */ }
                    else if (c == '\n') { cur.Add(sb.ToString()); sb.Clear(); rows.Add(cur); cur = new List<string>(); }
                    else sb.Append(c);
                }
            }
            if (sb.Length > 0 || cur.Count > 0) { cur.Add(sb.ToString()); rows.Add(cur); }
            return rows;
        }
    }
}
