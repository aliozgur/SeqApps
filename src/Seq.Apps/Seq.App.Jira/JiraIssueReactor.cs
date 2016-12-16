using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Formatting;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Seq.Apps;
using Seq.Apps.LogEvents;
using SeqApps.Commons;

namespace Seq.App.Jira
{
    [SeqApp("JIRA Issue",
    Description = "Posts seq event as an issue to Atlassian JIRA")]
    public class JiraIssueReactor : Reactor, ISubscribeTo<LogEventData>
    {

        #region Settings 
        [SeqAppSetting(
            DisplayName = "Seq Server Url",
            IsOptional = true,
            HelpText = "URL of the seq server. This appears in description field so that you can get back to the event from JIRA.")]
        public string SeqUrl { get; set; }

        [SeqAppSetting(DisplayName = "Jira Url",
            IsOptional = false,
             HelpText = "URL of Jira (do not include /rest/api/ or /rest/api/latest at the end of the path).")]
        public string JiraUrl { get; set; }

        public string JiraApiUrl
        {
            get
            {
                var url = JiraUrl.NormalizeHostOrFQDN();
                if (!url.ToLower().Contains("/rest/api/"))
                    url = url + "rest/api/latest/";
                return url;
            }
        }

        public string GiveMeTheSearchDuplicateIssueUrl(string messageId)
        {
            return $"search?jql=project={ProjectKey}+AND+cf[{SeqEventField}]~{messageId}&maxResults=1&fields=id,key,summary,customfield_{SeqEventField}";
        }

        [SeqAppSetting(DisplayName = "Comma seperates list of event levels",
            IsOptional = true,
            HelpText = "If specified Jira issue will be created only for the specified event levels, other levels will be discarded")]
        public string LogEventLevels { get; set; }

        public List<LogEventLevel> LogEventLevelList
        {
            get
            {
                List<LogEventLevel> result = new List<LogEventLevel>();
                if (!(LogEventLevels?.HasValue() ?? false))
                    return result;

                var strValues = LogEventLevels.Split(new char[1] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                if ((strValues?.Length ?? 0) == 0)
                    return result;

                strValues.Aggregate(result, (acc, strValue) =>
                {
                    LogEventLevel enumValue = LogEventLevel.Debug;
                    if (Enum.TryParse<LogEventLevel>(strValue, out enumValue))
                        acc.Add(enumValue);
                    return acc;
                });

                return result;
            }
        }

        [SeqAppSetting(DisplayName = "Jira Project Key",
            IsOptional = false)]
        public string ProjectKey { get; set; }

        [SeqAppSetting(
            DisplayName = "Jira Project Components",
            IsOptional = true,
            HelpText = "Comma seperated list of Jira project components")]
        public string Components { get; set; }
        public List<object> ComponentsAsArray
        {
            get
            {

                if (!(Components?.HasValue() ?? false))
                    return new List<object>();

                var strValues = Components.Split(new char[1] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                if ((strValues?.Length ?? 0) == 0)
                    return new List<object>();

                return strValues.Select(v => new { name = v.Trim() }).ToList<object>();
            }
        }

        [SeqAppSetting(
            DisplayName = "Jira Issue Labels",
            IsOptional = true,
            HelpText = "Comma seperated list of issue labels")]
        public string Labels { get; set; }

       
        [SeqAppSetting(
            DisplayName = "Seq Event Id custom field # from JIRA",
            IsOptional = true,
            HelpText = "Jira custome field number to store Seq Event Id. If provided will be used to prevent duplicate issue creation")]
        public int? SeqEventField { get; set; }

        [SeqAppSetting(
            DisplayName = "Jira issue type",
            IsOptional = true,
            HelpText = "Jira issue type")]
        public string JiraIssueType { get; set; }

        [SeqAppSetting(
            DisplayName = "Full Details As Description",
            IsOptional = true,
            InputType = SettingInputType.Checkbox,
            HelpText =
                "Check if you want details of the rendered message to appear in issue description, else only 512 characters will be in description and details will be added as a comment"
            )]
        public bool FullDetailsInDescription { get; set; } = false;

        [SeqAppSetting(
            DisplayName = "Properties As Comment",
            IsOptional = true,
            InputType = SettingInputType.Checkbox,
            HelpText =
                "Add event data structured properties as comment into the created issue"
            )]
        public bool PropertiesAsComment { get; set; } = false;


        [SeqAppSetting(
            DisplayName = "Jira Username",
            IsOptional = false)]
        public string Username { get; set; }

        [SeqAppSetting(
            DisplayName = "Jira Password",
            IsOptional = false,
            InputType = SettingInputType.Password)]
        public string Password { get; set; }



        #endregion //Settings

        private string _step = "";

        public void On(Event<LogEventData> evt)
        {
            try
            {
                var result = CreateIssue(evt).Result;
            }
            catch (AggregateException aex)
            {
                var fex = aex.Flatten();
                Log.Error(fex, "Error while creating issue in Atlassian JIRA. Step is: {_step}", _step);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error while creating issue in Atlassian JIRA. The step is: {_step}", _step);
            }
        }


        public async Task<bool> CreateIssue(Event<LogEventData> evt)
        {
            if (evt == null)
                return false;

            if ((LogEventLevelList?.Count ?? 0) > 0 && !LogEventLevelList.Contains(evt.Data.Level))
                return false;

            var description = evt.Data.Exception ?? evt.Data.RenderedMessage;
            var messageId = ComputeId(description);
            var summary = evt.Data.RenderedMessage.CleanCRLF().TruncateWithEllipsis(255);

            var client = new JsonRestClient(JiraApiUrl)
            {
                Username = Username,
                Password = Password
            };


            // Try to match 
            if (SeqEventField.HasValue)
            {
                var searchUrl = GiveMeTheSearchDuplicateIssueUrl(messageId);

                var searchResult = await client.GetAsync<JiraIssueSearchResult>(searchUrl).ConfigureAwait(false);
                if ((searchResult?.total ?? 0) > 0)
                    return false;
            }

            var fields = new Dictionary<string, object>
            {
                {"project", new {key = ProjectKey.Trim()}},
                {"issuetype", new {name = JiraIssueType.Trim()}},
                {"summary", summary},
                {"description", RenderDescription(evt)},
            };

            // Process components
            var components = ComponentsAsArray;
            if ((components?.Count ?? 0) > 0)
            {
                fields.Add("components", components);
            }

            // Process labels
            var labels = Labels.IsNullOrEmpty()
                ? null
                : Labels.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

            if ((labels?.Length ?? 0) > 0)
                fields.Add("labels", labels.TrimAll());

            if (SeqEventField.HasValue)
            {
                fields.Add($"customfield_{SeqEventField}", messageId);
            }
            var payload = new { fields };

            //var payloadJson = JsonConvert.SerializeObject(payload);
            
            // Create the issue
            _step = "Will create issue";
            var result = await client.PostAsync<JiraCreateIssueResponse,object>("issue", payload)
                .ConfigureAwait(false);
            if ( (result?.Errors?.Count ?? 0 ) >  0)
            {

                var e =new ApplicationException("Jira errors are  " + JsonConvert.SerializeObject(result.Errors));
                Log.Error(e, "Can not crate issue on Jira");
                return false;    
            }
            _step = "Issue created";

            // Add details as comment
            if (!FullDetailsInDescription)
            {
                _step = "Will add details as comment";
                var commentBody = $"{{noformat}}{evt.Data.RenderedMessage}{{noformat}}";
                if (commentBody.HasValue())
                {
                    await CommentAsync(result, commentBody).ConfigureAwait(false);
                }
                _step = "Added details as comment";
            }

            // Properties as comment
            if (PropertiesAsComment)
            {
                _step = "Will add properties as comment";
                var commentBody = RenderProperties(evt, "Structured Event Properties");
                if (commentBody.HasValue())
                {
                    await CommentAsync(result, commentBody).ConfigureAwait(false);
                }
                _step = "Added properties as comment";
            }


            return true;
        }

        public async Task<string> CommentAsync(JiraCreateIssueResponse createResponse, string comment)
        {
            var client = new JsonRestClient(JiraApiUrl)
            {
                Username = Username,
                Password = Password
            };

            var payload = new
            {
                body = comment
            };

            var resource = $"issue/{createResponse.Key}/comment";

            var response = await client.PostAsync<string, object>(resource, payload).ConfigureAwait(false);
            return response;
        }


        private string RenderDescription(Event<LogEventData> evt)
        {
            if (evt == null)
                return "";

            var sb = new StringBuilder();
            sb.AppendLine("{noformat}");
            sb.AppendFormat("Reporter Account Name: {0}\r\n", Username);

            sb.AppendFormat("Event Id: {0}\r\n", evt.Id);
            sb.AppendFormat("Level : {0}\r\n", evt.Data.Level.ToString());
            if (evt.Data.Exception.HasValue())
                sb.AppendFormat("Exception Message: {0}\r\n", evt.Data.Exception);

            sb.AppendFormat("Timestamp : {0}\r\n", evt.Data.LocalTimestamp.ToLocalTime());

            sb.AppendLine("{noformat}");
            sb.AppendLine("{noformat}");
            sb.AppendLine(FullDetailsInDescription ? evt.Data.RenderedMessage : evt.Data.RenderedMessage.TruncateWithEllipsis(512));
            sb.AppendLine("{noformat}");

            return sb.ToString();
        }

        private string RenderProperties(Event<LogEventData> evt, string title = "")
        {
            if ((evt?.Data?.Properties?.Count ?? 0) == 0)
                return "";

            var sb = new StringBuilder();
            if (title.HasValue())
                sb.AppendLine("h3." + title);

            sb.AppendLine("{noformat}");
            var allProps = evt.Data.Properties;
            foreach (var k in allProps)
            {
                sb.AppendFormat("{0}: {1}\r\n", k.Key, JsonConvert.SerializeObject(k.Value));
            }
            sb.AppendLine("{noformat}");
            return sb.ToString();
        }

        private string ComputeId(string input)
        {
            MD5 md5 = MD5.Create();
            return BitConverter.ToString(md5.ComputeHash(Encoding.UTF8.GetBytes(input))).Replace("-", string.Empty);
        }
    }
}
