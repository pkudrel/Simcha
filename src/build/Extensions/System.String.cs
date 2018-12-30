using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace System
{
    public static class StringExtensions
    {
        public static string GetLongestCommonSubstring(this IList<string> strings)
        {
            if (strings == null)
                throw new ArgumentNullException("strings");
            if (!strings.Any() || strings.Any(string.IsNullOrEmpty))
                throw new ArgumentException("None string must be empty", "strings");

            var commonSubstrings = new HashSet<string>(strings[0].GetSubstrings());
            foreach (var str in strings.Skip(1))
            {
                commonSubstrings.IntersectWith(str.GetSubstrings());
                if (commonSubstrings.Count == 0)
                    return null;
            }

            return commonSubstrings.OrderByDescending(s => s.Length).First();
        }

        public static IEnumerable<string> GetSubstrings(this string str)
        {
            if (string.IsNullOrEmpty(str))
                throw new ArgumentException("str must not be null or empty", "str");

            for (var c = 0; c < str.Length - 1; c++)
            for (var cc = 1; c + cc <= str.Length; cc++)
                yield return str.Substring(c, cc);
        }

        public static string DasherizeOnUpperLetter(this string @this) =>
            Regex.Replace(@this, "([a-z])([A-Z])", "$1-$2");

        public static string ToFormat(this string pattern, params object[] args) => string.Format(pattern, args);

        public static string FormatWith(this string @this, params object[] args) => string.Format(@this, args);

        public static bool DoesNotStartWith(this string @this, string value) => !@this.StartsWith(value);

        public static bool DoesNotContain(this string @this, string value) => !@this.Contains(value);

        public static bool DoesNotEndWith(this string @this, string value) => !@this.EndsWith(value);

        public static bool IsSomething(this string @this) => !string.IsNullOrEmpty(@this);

        public static bool IsNullOrEmpty(this string @this) => string.IsNullOrEmpty(@this);

        public static bool HasText(this string @this) => @this.IsSomething() && @this.Trim().Length > 0;

        public static bool HasNoText(this string @this) => !@this.HasText();

        public static string RemoveNonAscii(this string @this)
        {
            var sb = new StringBuilder();

            foreach (var c in @this)
                if (c >= 32 && c <= 175)
                    sb.Append(c);
                else
                    sb.Append("-");

            return sb.ToString();
        }

        public static bool IsOnlyAsciiChars(this string @this) => @this.All(c => c >= 32 && c <= 175);

        public static string RemoveSpace(this string @this, string replace = "") => @this.Replace(" ", replace);

        public static string InsertOnBegin(this string @this, int chars, char toInsert = ' ') =>
            @this.Insert(0, new string(toInsert, chars));

        public static string RemoveQuotationMarks(this string @this, string replace = "") =>
            @this.Replace("\"", replace).Replace("'", replace);

        public static string RemoveNewLinesAndTabs(this string @this)
        {
            var sb = new StringBuilder(@this.Length);
            foreach (var i in @this)
                if (i != '\n' && i != '\r' && i != '\t')
                    sb.Append(i);
            return sb.ToString();
        }


        public static string ReplacePolishChars(this string @this)
        {
            var sb = new StringBuilder();
            foreach (var c in @this) sb.Append(GetSemiPolishChar(c));

            return sb.ToString();
        }

        public static int CountChar(this string @this, char c)
        {
            var count = 0;
            foreach (var ch in @this)
                if (ch.Equals(c))
                    count++;
            return count;
        }


        static char GetSemiPolishChar(char c)
        {
            switch (c)
            {
                case 'Ą':
                    return 'A';
                case 'ą':
                    return 'a';
                case 'Ć':
                    return 'C';
                case 'ć':
                    return 'c';
                case 'Ę':
                    return 'E';
                case 'ę':
                    return 'e';
                case 'Ł':
                    return 'L';
                case 'ł':
                    return 'l';
                case 'Ń':
                    return 'N';
                case 'ń':
                    return 'n';
                case 'Ó':
                    return 'O';
                case 'ó':
                    return 'o';
                case 'Ś':
                    return 'S';
                case 'ś':
                    return 's';
                case 'Ż':
                case 'Ź':
                    return 'Z';
                case 'ż':
                case 'ź':
                    return 'z';
                default:
                    return c;
            }
        }

        /// <summary>
        /// Generates a slug.
        /// <remarks>
        /// Credit goes to <see href="http://stackoverflow.com/questions/2920744/url-slugify-alrogithm-in-cs" />.
        /// </remarks>
        /// </summary>
        [DebuggerStepThrough]
        public static string GenerateSlug(this string value, uint? maxLength = null)
        {
            // remove polish
            var result = ReplacePolishChars(value);
            // prepare string, remove diacritics, lower case and convert hyphens to whitespace
            result = RemoveDiacritics(result).Replace("-", " ").ToLowerInvariant();

            result = Regex.Replace(result, @"[^a-z0-9\s-]", string.Empty); // remove invalid characters
            result = Regex.Replace(result, @"\s+", " ").Trim(); // convert multiple spaces into one space

            if (maxLength.HasValue)
                result = result.Substring(0, result.Length <= maxLength ? result.Length : (int) maxLength.Value).Trim();
            result = Regex.Replace(result, @"\s", "-");
            return result.Replace("--", "-");
        }

        /// <summary>
        /// Removes the diacritics from the given <paramref name="input" />
        /// </summary>
        /// <remarks>
        /// Credit goes to <see href="http://stackoverflow.com/a/249126" />.
        /// </remarks>
        [DebuggerStepThrough]
        public static string RemoveDiacritics(this string input)
        {
            var stringBuilder = new StringBuilder();
            var normalizedString = input.Normalize(NormalizationForm.FormD);


            foreach (var c in normalizedString)
            {
                var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
                if (unicodeCategory != UnicodeCategory.NonSpacingMark) stringBuilder.Append(c);
            }

            return stringBuilder.ToString();
        }

        /// <summary>
        /// Splits a string into seperate words delimitted by a space
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static string SplitOnSpaces(this string str)
        {
            var sb = new StringBuilder(str.Length + str.Length / 2);
            foreach (var ch in str)
                if (char.IsUpper(ch))
                {
                    sb.Append(' ');
                    sb.Append(ch);
                }
                else
                {
                    sb.Append(ch);
                }

            return sb.ToString();
        }


        public static string SubStringFromFirstToSecond(this string str, string first, string second)
        {
            var ret = string.Empty;
            var pos = str.IndexOf(first, StringComparison.Ordinal);
            if (pos < 0) return ret;
            var start = pos;
            var end = str.IndexOf(second, start, StringComparison.Ordinal);
            if (end < 0) return ret;
            var sub = str.Substring(start, end - start);

            return sub;
        }


        public static string SubStringFromFirstToSecond(this string str, string first, string second, int startMove,
            int endMove = 0)
        {
            var ret = string.Empty;
            var pos1 = str.IndexOf(first, StringComparison.Ordinal);
            if (pos1 < 0) return ret;
            var start = pos1 + startMove;
            var pos2 = str.IndexOf(second, start, StringComparison.Ordinal);
            if (pos2 < 0) return ret;
            var end = pos2 + endMove;
            var sub = str.Substring(start, end - start);

            return sub;
        }

        public static List<string> SubStringFromFirstToSecondAll(this string str, string first, string second,
            int startMove, int endMove = 0)
        {
            var ret = new List<string>();
            var pos1 = str.IndexOf(first, StringComparison.Ordinal);
            while (pos1 > -1)
            {
                var start = pos1 + startMove;
                var pos2 = str.IndexOf(second, start, StringComparison.Ordinal);
                if (pos2 < 0) break;
                var end = pos2 + endMove;
                var sub = str.Substring(start, end - start);
                ret.Add(sub);
                pos1 = str.IndexOf(first, end, StringComparison.Ordinal);
                ;
            }

            return ret;
        }

        public static string SubStringFromStartFirstToBeginSecond(this string str, string first, string second)
        {
            var ret = string.Empty;
            var firstLen = first.Length;
            var pos = str.IndexOf(first, StringComparison.Ordinal);
            if (pos < 0) return ret;
            var start = pos + firstLen;
            var end = str.IndexOf(second, start, StringComparison.Ordinal);
            if (end < 0) return ret;
            var sub = str.Substring(start, end - start + 1);

            return sub;
        }

        public static List<string> AllSubStringsBetween(this string str, string item)
        {
            var ret = new List<string>();
            if (string.IsNullOrEmpty(str)) return ret;
            var itemLength = item.Length;
            var pos = str.IndexOf(item, 0, StringComparison.Ordinal);
            while (pos > -1)
            {
                var start = pos + itemLength;
                var end = str.IndexOf(item, start, StringComparison.Ordinal);
                if (end < 0) return ret;
                var sub = str.Substring(start, end - start);
                ret.Add(sub);
                pos = str.IndexOf(item, end + itemLength, StringComparison.Ordinal);
            }

            return ret;
        }

        public static string ToKebabCase(this string str)
        {
            if (string.IsNullOrEmpty(str)) return string.Empty;

            var builder = new StringBuilder();
            builder.Append(char.ToLower(str[0]));

            foreach (var c in str.Skip(1))
                if (char.IsUpper(c))
                {
                    builder.Append('-');
                    builder.Append(char.ToLower(c));
                }
                else
                {
                    builder.Append(c);
                }

            return builder.ToString();
        }
    }
}