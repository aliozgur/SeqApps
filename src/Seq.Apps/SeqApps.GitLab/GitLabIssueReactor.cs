using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Seq.Apps;
using Seq.Apps.LogEvents;
using SeqApps.Commons;

namespace SeqApps.GitLab
{
    [SeqApp("GitLab Issues",
        Description = "Posts seq event as an issue to a GitLab project")]
    public class GitLabIssueReactor : SeqApp, ISubscribeTo<LogEventData>
    {

        #region Settings

        [SeqAppSetting(
            DisplayName = "Seq Server Url",
            IsOptional = true,
            HelpText = "URL of the seq server. This appears as a link in the issue"
            )]
        public string SeqUrl { get; set; }

        [SeqAppSetting(DisplayName = "GitLab REST API Url",
            IsOptional = false,
             HelpText = "GitLab v3 or above Api url of your")]
        public string GitLabRestApiUrl { get; set; }

        [SeqAppSetting(DisplayName = "GitLab Project Name",
            IsOptional = false,
             HelpText = "GitLab project name (with full namespace path) OR event property which contains the GitLab project name. You can use API keys to inject this property or log this property from your app code"
            )]
        public string GitLabProjectName { get; set; }

        [SeqAppSetting(DisplayName = "GitLab Private Token",
            HelpText = "The private token that will be used to authenticate GitLab API. You can find the private token at https://yourgitlabserver.com/profile/account",
            IsOptional = false)]
        public string GitLabPrivateToken { get; set; }


        [SeqAppSetting(DisplayName = "Event levels",
            IsOptional = true,
            HelpText = "Comma seperated list of event levels. If specified GitLab issues will be created only for the specified event levels, other levels will be discarded"
            )]
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


        [SeqAppSetting(DisplayName = "Title Prefix Properties",
            IsOptional = true,
            HelpText = "Comma seperated list of properties to include as prefix in issue title.If specified matching log properties will be included as prefix in the issue title"
            )]
        public string TitleProperties { get; set; }

        public List<string> TitlePropertiesList
        {
            get
            {
                List<string> result = new List<string>();
                if ((TitleProperties ?? "") == "")
                    return result;

                return TitleProperties.Split(new char[1] { ',' }, StringSplitOptions.RemoveEmptyEntries).ToList();
            }
        }

        [SeqAppSetting(DisplayName = "Label Properties",
           IsOptional = true,
           HelpText = "Comma seperated list of properties to be used as labels for the issue.If specified matching log properties will be added as labels to the issue "
            )]
        public string LabelProperties { get; set; }

        public List<string> LabelPropertiesList
        {
            get
            {
                List<string> result = new List<string>();
                if ((LabelProperties ?? "") == "")
                    return result;

                return LabelProperties.Split(new char[1] { ',' }, StringSplitOptions.RemoveEmptyEntries).ToList();
            }
        }

        [SeqAppSetting(
            DisplayName = "Add  Level As Label",
            IsOptional = true,
            InputType = SettingInputType.Checkbox,
            HelpText = "Attach event data structured properties"
            )]
        public bool AddLogLevelAsLabel { get; set; } = false;


        [SeqAppSetting(DisplayName = "Properties",
            IsOptional = true,
            HelpText = "Comma seperated list of additional property key/names. If specified only matching properties will be attached to the GitLab issue. Please note you should also enable Attach Properties"
            )]
        public string AdditionalProperties { get; set; }

        public List<string> AdditionalPropertiesList
        {
            get
            {
                List<string> result = new List<string>();
                if ((AdditionalProperties ?? "") == "")
                    return result;

                return AdditionalProperties.Split(new char[1] { ',' }, StringSplitOptions.RemoveEmptyEntries).ToList();
            }
        }

        [SeqAppSetting(DisplayName = "Attach Properties",
            IsOptional = true,
            InputType = SettingInputType.Checkbox,
            HelpText ="Attach event data structured properties"
        )]
        public bool AttachProperties { get; set; } = false;

        [SeqAppSetting(
            DisplayName = "Attach Exception",
            IsOptional = true,
            InputType = SettingInputType.Checkbox,
            HelpText = "Attach exception message"
            )]
        public bool AttachException { get; set; } = false;

        private string _step;
        #endregion //Settings

        public void On(Event<LogEventData> evt)
        {
            try
            {
                var result = CreateIssue(evt).Result;

            }
            catch (AggregateException aex)
            {
                var fex = aex.Flatten();
                Log.Error(fex, "Error while creating issue in  GitLab: {_step}", _step);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error while creating issue in  GitLab: {_step}", _step);
            }
        }

        private async Task<bool> CreateIssue(Event<LogEventData> evt)
        {

            if (evt == null)
                return false;

            if ((LogEventLevelList?.Count ?? 0) > 0 && !LogEventLevelList.Contains(evt.Data.Level))
                return false;

            if (!(GitLabProjectName ?? "").HasValue() || !(GitLabPrivateToken ?? "").HasValue())
                return false;

            object projectNamePropValue = null;
            evt?.Data?.Properties?.TryGetValue(GitLabProjectName, out projectNamePropValue);
            var projectName = projectNamePropValue?.ToString() ?? GitLabProjectName;

            if (!(projectName ?? "").HasValue())
                return false;

            var apiBaseUrl = GitLabRestApiUrl.NormalizeHostOrFQDN();
            var client = new JsonRestClient(apiBaseUrl);
            client.DoNotAuthorize = true;

            // Add private token data to request header
            Dictionary<string, string> headers = new Dictionary<string, string>
            {
                ["PRIVATE-TOKEN"] = GitLabPrivateToken
            };

            _step = $"Will get project id for {projectName}";


            projectName = System.Net.WebUtility.UrlEncode(projectName);
            var searchResult = await client.GetAsync<IdDto>($"projects/{projectName}", headers)
                .ConfigureAwait(false);

            var projectId = searchResult?.id;
            if (projectId == null)
            {
                Log.Error($"Project not found {projectName}");
                return false;
            }

            var renderedMessage = (evt?.Data?.RenderedMessage ?? "");
            var summary = renderedMessage.CleanCRLF().TruncateWithEllipsis(255);

            _step = $"DONE get project id for {projectName}";



            var seqUrl = SeqUrl.NormalizeHostOrFQDN();
            var eventUrl = $"{seqUrl}#/events?filter=@Id%20%3D%20'{evt.Id}'";
            var propValuesInTitle = BuildPropertyValuesString(evt, TitlePropertiesList);
            var labels = BuildPropertyValuesString(evt, LabelPropertiesList, ",", (AddLogLevelAsLabel ? new string[1] { evt.Data.Level.ToString() } : null));
            StringBuilder sb = new StringBuilder();




            // Attach the rendered message
            if (renderedMessage.HasValue())
            {
                sb.AppendLine($"**Message** [View Event]({eventUrl})");
                sb.AppendLine("```");
                sb.AppendLine(renderedMessage);
                sb.AppendLine("```");
                sb.AppendLine("");


            }

            // If event has exception attach that Exception
            if (AttachException && (evt?.Data?.Exception ?? "").HasValue())
            {
                sb.AppendLine("**Exception**");
                sb.AppendLine("```");
                sb.AppendLine(evt?.Data?.Exception);
                sb.AppendLine("```");
                sb.AppendLine("");
            }


            // Attach structured properties
            if (AttachProperties && (evt?.Data?.Properties?.Count ?? 0) > 0)
            {
                sb.AppendLine("**Properties**");

                var allProps = evt.Data.Properties;
                var additionPropList = AdditionalPropertiesList;
                sb.AppendLine("```");
                foreach (var kvp in allProps)
                {
                    if (additionPropList?.Count > 0 && !additionPropList.Contains(kvp.Key))
                        continue;

                    sb.AppendLine($"* {kvp.Key} : {(kvp.Value != null ? JsonConvert.SerializeObject(kvp.Value) : "")}"); ;
                }
                sb.AppendLine("```");
                sb.AppendLine("");

            }


            // Create the issue
            var gitLabIssue = new NewIssuePayload
            {
                id = projectId.Value,
                title = $"{(propValuesInTitle.HasValue() ? propValuesInTitle + " : " : "")}{summary}",
                description = sb.ToString(),
                labels = labels
            };

            _step = $"Will create issue for {projectId} -> {projectName}";

            // Post the message
            var createIssueResult = await client.PostAsync<NewIssueResponsePayload, NewIssuePayload>($"projects/{projectId}/issues", gitLabIssue, headers)
                .ConfigureAwait(false);

            if (createIssueResult == null || !createIssueResult.id.HasValue)
            {
                var e = new ApplicationException($"Can not create issue for project {projectId} -> {projectName}");
                var error = (createIssueResult?.error ?? "");
                Log.Error(e, "GitLab create issue failure : {error}", error);
                return false;
            }

            _step = $"DONE create issue {projectId} -> {projectName}";
            return true;
        }

        private string BuildPropertyValuesString(Event<LogEventData> evt, List<string> properties, string seperator = " | ", string[] additionalValues = null)
        {
            if (properties?.Count > 0)
            {
                var lst = properties;
                var propValuesList = new List<string>((additionalValues ?? new string[0]));
                foreach (var p in lst)
                {
                    object propValue = null;
                    if (evt.Data.Properties.TryGetValue(p, out propValue) && propValue != null)
                    {
                        propValuesList.Add(propValue.ToString());
                    }
                }

                return String.Join(seperator, propValuesList);
            }
            return "";
        }
    }
}
