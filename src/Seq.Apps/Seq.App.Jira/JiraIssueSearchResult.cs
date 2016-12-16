using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Seq.App.Jira
{
    public class JiraIssue
    {
        public string id { get; set; }
        public string key { get; set; }
    }

    public class JiraIssueSearchResult
    {
        public int maxResults { get; set; }
        public int total { get; set; }
        public JiraIssue[] issues { get; set; }
    }


}
