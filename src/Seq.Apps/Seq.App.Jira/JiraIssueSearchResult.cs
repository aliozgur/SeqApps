
// ReSharper disable UnusedMember.Global
// ReSharper disable InconsistentNaming
// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global

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