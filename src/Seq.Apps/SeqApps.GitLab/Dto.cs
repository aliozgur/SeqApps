using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SeqApps.GitLab
{
    public class NewIssuePayload
    {
        public int id { get; set; }
        public string title { get; set; }
        public string description { get; set; }

        public string labels { get; set; }
    }

    public class NewIssueResponsePayload
    {
        public int project_id { get; set; }
        public int? id { get; set; }

        public string state { get; set; }

        public string error { get; set; }

    }
    public class IdDto
    {
        public int id { get; set; }
    }

    
}
