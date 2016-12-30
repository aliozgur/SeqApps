using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Octokit;
using Seq.Apps;
using Seq.Apps.LogEvents;
using SeqApps.Commons;

namespace SeqApps.GitHub
{
    [SeqApp("GitHub Issues",
        Description = "Posts seq event as an issue to a GitHub project")]
    public class GitHubIssueReactor : Reactor, ISubscribeTo<LogEventData>
    {

        #region Settings 

        [SeqAppSetting(
            DisplayName = "Seq Server Url",
            IsOptional = true,
            HelpText = "URL of the seq server. This appears as a link in the issue"
            )]
        public string SeqUrl { get; set; }

        [SeqAppSetting(DisplayName = "GitHub Enterprise Url",
            IsOptional = true,
             HelpText = "If you use GitHub Enterprise please specify the Url")]
        public string GitHubEntUrl { get; set; }

        [SeqAppSetting(DisplayName = "GitHub Repo Owner Name",
            IsOptional = false,
             HelpText = "GitHub repo owner name OR event property which contains the GitHub repo owne name. You can use API keys to inject this property or log this property from your app code"
            )]
        public string GitHubRepoOwnerName { get; set; }

        [SeqAppSetting(DisplayName = "GitHub Repo Name",
            IsOptional = false,
             HelpText = "GitHub repo name OR event property which contains the GitHub repo name. You can use API keys to inject this property or log this property from your app code"
            )]
        public string GitHubRepoName { get; set; }


        [SeqAppSetting(DisplayName = "GitHub Personal Token  OR Username",
            HelpText = "Personal token for OAUTH or Username for Basic authentication. If you intend to use Basic auth please also specify the password",
            IsOptional = false)]
        public string GitHubUsername { get; set; }

        [SeqAppSetting(DisplayName = "GitHub Password",
            HelpText = "Optional password for basic authentication.",
            IsOptional = true)]
        public string GitHubPassword { get; set; }


        [SeqAppSetting(DisplayName = "Event levels",
            IsOptional = true,
            HelpText = "Comma seperated list of event levels. If specified GitHub issues will be created only for the specified event levels, other levels will be discarded"
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
            HelpText = "Comma seperated list of additional property key/names. If specified only matching properties will be attached to the GitHub issue. Please note you should also enable Attach Properties"
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
                Log.Error(fex, "Error while creating issue in  GitHub: {_step}", _step);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error while creating issue in  GitHub: {_step}", _step);
            }
        }

        private async Task<bool> CreateIssue(Event<LogEventData> evt)
        {

            if (evt == null)
                return false;

            if ((LogEventLevelList?.Count ?? 0) > 0 && !LogEventLevelList.Contains(evt.Data.Level))
                return false;

            if (!(GitHubRepoName ?? "").HasValue() 
                || !(GitHubRepoOwnerName ?? "").HasValue()
                || !(GitHubUsername ?? "").HasValue()
                )
                return false;

            Uri entUri = null;
            if ((GitHubEntUrl ?? "").HasValue() && !Uri.TryCreate(GitHubEntUrl, UriKind.RelativeOrAbsolute, out entUri))
                return false;


            var phv = new ProductHeaderValue("SeqApp.GitHub");

            if (entUri != null)
            {
                _step = $"Will probe GitHub enterprise {entUri}";
                var probe = new EnterpriseProbe(phv);
                var result = await probe.Probe(entUri);
                _step = $"DONE probe GitHub enterprise {entUri}";
                if (result != EnterpriseProbeResult.Ok)
                {
                    Log.Error("Probe enterprise GitHub failed with result {result}", result);
                    return false;
                }
            }


            object repoNamePropValue = null;
            evt?.Data?.Properties?.TryGetValue(GitHubRepoName, out repoNamePropValue);
            var repoName  = repoNamePropValue?.ToString() ?? GitHubRepoName;

            if (!(repoName?? "").HasValue())
                return false;

            object repoOwnerNamePropValue = null;
            evt?.Data?.Properties?.TryGetValue(GitHubRepoOwnerName, out repoOwnerNamePropValue);
            var repoOwnerName = repoOwnerNamePropValue?.ToString() ??  GitHubRepoOwnerName;

            if (!(repoOwnerName ?? "").HasValue())
                return false;

            var creds = (GitHubPassword ?? "").HasValue()
                ? new Credentials(GitHubUsername, GitHubPassword)
                : new Credentials(GitHubUsername);

            var client = entUri == null ? new GitHubClient(phv) : new GitHubClient(phv, entUri);
            client.Credentials = creds;

            var renderedMessage = (evt?.Data?.RenderedMessage ?? "");
            var summary = renderedMessage.CleanCRLF().TruncateWithEllipsis(255);
            var seqUrl = SeqUrl.NormalizeHostOrFQDN();
            var eventUrl = $"{seqUrl}#/events?filter=@Id%20%3D%20'{evt.Id}'";
            var propValuesInTitle = BuildPropertyValuesString(evt, TitlePropertiesList);
           
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
            var title = $"{(propValuesInTitle.HasValue() ? propValuesInTitle + " : " : "")}{summary}";

            var newIssue = new NewIssue(title)
            {
                Body = sb.ToString(),
            };
            var labels = GetPropertyValuesAsList(evt, LabelPropertiesList, ",", (AddLogLevelAsLabel ? new string[1] { evt.Data.Level.ToString() } : null));
            if(labels?.Count > 0) labels.ForEach(lb => newIssue.Labels.Add(lb));

            _step = $"Will create issue for {GitHubRepoOwnerName}/{GitHubRepoName}";

            var createdIssue = await client.Issue.Create(GitHubRepoOwnerName, GitHubRepoName, newIssue).ConfigureAwait(false);

            _step = $"DONE create issue for {GitHubRepoOwnerName}/{GitHubRepoName}";
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

        private List<string> GetPropertyValuesAsList(Event<LogEventData> evt, List<string> properties, string seperator = " | ", string[] additionalValues = null)
        {
            var propValuesList = new List<string>((additionalValues ?? new string[0]));
            if (properties?.Count > 0)
            {
                var lst = properties;
                foreach (var p in lst)
                {
                    object propValue = null;
                    if (evt.Data.Properties.TryGetValue(p, out propValue) && propValue != null)
                    {
                        propValuesList.Add(propValue.ToString());
                    }
                }

            }
            return propValuesList;
        }
    }
}
