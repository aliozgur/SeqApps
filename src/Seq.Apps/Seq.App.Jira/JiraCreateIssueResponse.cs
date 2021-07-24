using System.Collections.Generic;
using SeqApps.Commons;

// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable UnusedMember.Global

namespace Seq.App.Jira
{
    public class JiraCreateIssueResponse
    {
        public string Key { get; set; }
        public int Id { get; set; }

        public string Self { get; set; }

        public string Host { get; set; }

        public string BrowseUrl => Host.IsNullOrEmpty() || Key.IsNullOrEmpty()
            ? string.Empty
            : $"{Host.TrimEnd(StringSplits.ForwardSlash)}/browse/{Key}";

        public Dictionary<string, string> Errors { get; set; }
    }
}