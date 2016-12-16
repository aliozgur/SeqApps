using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SeqApps.Commons;

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

        public Dictionary<string,string> Errors { get; set; }

    }
}
