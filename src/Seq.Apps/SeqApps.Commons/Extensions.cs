using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace SeqApps.Commons
{
    /// <summary>
    /// Provides a centralized place for common functionality exposed via extension methods.
    /// </summary>
    public static partial class ExtensionMethods
    {
        public static string ExceptionLogPrefix = "ErrorLog-";

        /// <summary>
        /// Answers true if this String is either null or empty.
        /// </summary>
        /// <remarks>I'm so tired of typing String.IsNullOrEmpty(s)</remarks>
        public static bool IsNullOrEmpty(this string s) => string.IsNullOrEmpty(s);

        /// <summary>
        /// Answers true if this String is neither null or empty.
        /// </summary>
        /// <remarks>I'm also tired of typing !String.IsNullOrEmpty(s)</remarks>
        public static bool HasValue(this string s) => !string.IsNullOrEmpty(s);

        /// <summary>
        /// Returns the toReturn parameter when this string is null/empty.
        /// </summary>
        public static string IsNullOrEmptyReturn(this string s, string toReturn) => s.HasValue() ? s : toReturn;

        /// <summary>
        /// Returns null for an empty string. For use in places like attributes that need not render with no content
        /// </summary>
        public static string Nullify(this string s) => s.IsNullOrEmptyReturn(null);

        /// <summary>
        /// If this string ends in "toTrim", this will trim it once off the end
        /// </summary>
        public static string TrimEnd(this string s, string toTrim) =>
            s == null || toTrim == null || !s.EndsWith(toTrim)
                ? s
                : s.Substring(0, s.Length - toTrim.Length);


        /// <summary>
        /// returns Url Encoded string
        /// </summary>
        public static string UrlEncode(this string s) => s.HasValue() ? WebUtility.UrlEncode(s) : s;

        /// <summary>
        /// returns Html Encoded string
        /// </summary>
        public static string HtmlEncode(this string s) => s.HasValue() ? WebUtility.HtmlEncode(s) : s;

        /// <summary>
        /// Gets a readable type description for dashboards, e.g. "Dictionary&lt;string,string&gt;"
        /// </summary>
        public static string ReadableTypeDescription(this Type t) =>
            t.IsGenericType
                ? $"{t.Name.Split(StringSplits.Tilde)[0]}<{string.Join(",", t.GetGenericArguments().Select(a => a.Name))}>"
                : t.Name;

        /// <summary>
        /// A brain dead pluralizer. 1.Pluralize("time") => "1 time"
        /// </summary>
        public static string Pluralize(this int count, string name, bool includeNumber = true) => Pluralize((long)count, name, includeNumber);

        /// <summary>
        /// A brain dead pluralizer. 1.Pluralize("time") => "1 time"
        /// </summary>
        public static string Pluralize(this long count, string name, bool includeNumber = true)
        {
            var numString = includeNumber ? count.ToComma() + " " : null;
            if (count == 1) return numString + name;
            if (name.EndsWith("y")) return numString + name.Remove(name.Length - 1) + "ies";
            if (name.EndsWith("s")) return numString + name.Remove(name.Length - 1) + "es";
            if (name.EndsWith("ex")) return numString + name + "es";
            return numString + name + "s";
        }

        /// <summary>
        /// A plurailizer that accepts the count, single and plural variants of the text
        /// </summary>
        public static string Pluralize(this int count, string single, string plural, bool includeNumber = true) =>
            includeNumber
                ? count.ToComma() + " " + (count == 1 ? single : plural)
                : count == 1 ? single : plural;

        /// <summary>
        /// A plurailizer that accepts the count, single and plural variants of the text
        /// </summary>
        public static string Pluralize(this long count, string single, string plural, bool includeNumber = true) =>
            includeNumber
                ? count.ToComma() + " " + (count == 1 ? single : plural)
                : count == 1 ? single : plural;

        /// <summary>
        /// Returns the pluralized version of 'noun' when required by 'number'.
        /// </summary>
        public static string Pluralize(this string noun, int number, string pluralForm = null) =>
            number == 1
                ? noun
                : pluralForm.IsNullOrEmptyReturn((noun ?? "") + "s");

        /// <summary>
        /// force string to be maxlen or smaller
        /// </summary>
        public static string Truncate(this string s, int maxLength) =>
            s.IsNullOrEmpty() ? s : (s.Length > maxLength ? s.Remove(maxLength) : s);

        public static string TruncateWithEllipsis(this string s, int maxLength) =>
            s.IsNullOrEmpty() || s.Length <= maxLength ? s : Truncate(s, Math.Max(maxLength, 3) - 3) + "…";

        public static string CleanCRLF(this string s) =>
            string.IsNullOrWhiteSpace(s)
                ? s
                : s.Replace("\r\n", " ").Replace("\r", " ").Replace("\n", " ");

        public static string NormalizeForCache(this string s) => s?.ToLower();

        public static string NormalizeHostOrFQDN(this string s, bool defaultToHttps = false)
        {
            if (!s.HasValue()) return s;
            if (!s.StartsWith("http://") && !s.StartsWith("https://")) return $"{(defaultToHttps ? "https" : "http")}://{s}/";
            return s.EndsWith("/") ? s : $"{s}/";
        }

        public static string[] TrimAll(this string[] items)
        {
            return items.Select(s => s.Trim()).ToArray();
        }

        public static List<string> TrimAll(this List<string> items)
        {
            return items.Select(s => s.Trim()).ToList();
        }

        public static HashSet<T> ToHashSet<T>(this IEnumerable<T> source) => new HashSet<T>(source);

        /// <summary>
        /// Returns a unix Epoch time given a Date
        /// </summary>
        public static long ToEpochTime(this DateTime dt, bool toMilliseconds = false)
        {
            var seconds = (long)(dt - new DateTime(1970, 1, 1, 0, 0, 0)).TotalSeconds;
            return toMilliseconds ? seconds * 1000 : seconds;
        }

        /// <summary>
        /// Converts to Date given an Epoch time
        /// </summary>
        public static DateTime ToDateTime(this long epoch) => new DateTime(1970, 1, 1, 0, 0, 0).AddSeconds(epoch);

        /// <summary>
        /// Returns a humanized string indicating how long ago something happened, eg "3 days ago".
        /// For future dates, returns when this DateTime will occur from DateTime.UtcNow.
        /// </summary>
        public static string ToRelativeTime(this DateTime dt, bool includeTime = true, bool asPlusMinus = false, DateTime? compareTo = null, bool includeSign = true)
        {
            var comp = (compareTo ?? DateTime.UtcNow);
            if (asPlusMinus)
            {
                return dt <= comp
                    ? ToRelativeTimeSimple(comp - dt, includeSign ? "-" : "")
                    : ToRelativeTimeSimple(dt - comp, includeSign ? "+" : "");
            }
            return dt <= comp
                ? ToRelativeTimePast(dt, comp, includeTime)
                : ToRelativeTimeFuture(dt, comp, includeTime);
        }

        private static string ToRelativeTimePast(DateTime dt, DateTime utcNow, bool includeTime = true)
        {
            var ts = utcNow - dt;
            var delta = ts.TotalSeconds;

            if (delta < 1) return "just now";
            if (delta < 60) return ts.Seconds == 1 ? "1 sec ago" : ts.Seconds.ToString() + " secs ago";
            if (delta < 3600 /*60 mins * 60 sec*/) return ts.Minutes == 1 ? "1 min ago" : ts.Minutes.ToString() + " mins ago";
            if (delta < 86400 /*24 hrs * 60 mins * 60 sec*/) return ts.Hours == 1 ? "1 hour ago" : ts.Hours.ToString() + " hours ago";

            var days = ts.Days;
            if (days == 1) return "yesterday";
            if (days <= 2) return days.ToString() + " days ago";
            if (utcNow.Year == dt.Year) return dt.ToString(includeTime ? "MMM %d 'at' %H:mmm" : "MMM %d");
            return dt.ToString(includeTime ? @"MMM %d \'yy 'at' %H:mmm" : @"MMM %d \'yy");
        }

        private static string ToRelativeTimeFuture(DateTime dt, DateTime utcNow, bool includeTime = true)
        {
            TimeSpan ts = dt - utcNow;
            double delta = ts.TotalSeconds;

            if (delta < 1) return "just now";
            if (delta < 60) return ts.Seconds == 1 ? "in 1 second" : "in " + ts.Seconds.ToString() + " seconds";
            if (delta < 3600 /*60 mins * 60 sec*/) return ts.Minutes == 1 ? "in 1 minute" : "in " + ts.Minutes.ToString() + " minutes";
            if (delta < 86400 /*24 hrs * 60 mins * 60 sec*/) return ts.Hours == 1 ? "in 1 hour" : "in " + ts.Hours.ToString() + " hours";

            // use our own rounding so we can round the correct direction for future
            var days = (int)Math.Round(ts.TotalDays, 0);
            if (days == 1) return "tomorrow";
            if (days <= 10) return "in " + days.ToString() + " day" + (days > 1 ? "s" : "");
            // if the date is in the future enough to be in a different year, display the year
            if (utcNow.Year == dt.Year) return "on " + dt.ToString(includeTime ? "MMM %d 'at' %H:mmm" : "MMM %d");
            return "on " + dt.ToString(includeTime ? @"MMM %d \'yy 'at' %H:mmm" : @"MMM %d \'yy");
        }

        private static string ToRelativeTimeSimple(TimeSpan ts, string sign)
        {
            var delta = ts.TotalSeconds;
            if (delta < 1) return "< 1 sec";
            if (delta < 60) return sign + ts.Seconds.ToString() + " sec" + (ts.Seconds == 1 ? "" : "s");
            if (delta < 3600 /*60 mins * 60 sec*/) return sign + ts.Minutes.ToString() + " min" + (ts.Minutes == 1 ? "" : "s");
            if (delta < 86400 /*24 hrs * 60 mins * 60 sec*/) return sign + ts.Hours.ToString() + " hour" + (ts.Hours == 1 ? "" : "s");
            return sign + ts.Days.ToString() + " days";
        }

       

        public static string ToComma(this int? number, string valueIfZero = null) => number.HasValue ? ToComma(number.Value, valueIfZero) : "";

        public static string ToComma(this int number, string valueIfZero = null) => number == 0 && valueIfZero != null ? valueIfZero : number.ToString("n0");

        public static string ToComma(this long? number, string valueIfZero = null) => number.HasValue ? ToComma(number.Value, valueIfZero) : "";

        public static string ToComma(this long number, string valueIfZero = null) => number == 0 && valueIfZero != null ? valueIfZero : number.ToString("n0");


      

    }
}
