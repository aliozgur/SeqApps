using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Seq.Apps;
using Seq.Apps.LogEvents;
using SeqApps.Commons;

namespace Seq.App.Jira
{
    [SeqApp("JIRA Issue",
        Description = "Posts Seq event as an issue to Atlassian JIRA")]
    public class JiraIssueReactor : SeqApp, ISubscribeToAsync<LogEventData>
    {
        private readonly Dictionary<string, Priority> _priorities =
            new Dictionary<string, Priority>(StringComparer.OrdinalIgnoreCase);

        private string _assigneeProperty;
        private string _initialEstimateProperty;
        private string _remainingEstimateProperty;
        private string _dueDateProperty;


        private HandlebarsTemplate _generateMessage, _generateDescription;
        private string _includeTagProperty;
        private bool _isPriorityMapping;
        private string[] _labels;
        private Priority _priority = Priority.Medium, _defaultPriority = Priority.Medium;
        private string _priorityProperty = "@Level";
        private string _projectKeyProperty;

        private string _step = "";

        public async Task OnAsync(Event<LogEventData> evt)
        {
            try
            {
                await CreateIssue(evt);
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

        protected override void OnAttached()
        {
            _generateMessage = new HandlebarsTemplate(Host,
                !string.IsNullOrWhiteSpace(JiraSummary) ? JiraSummary : "{{$Message}}");
            _generateDescription = new HandlebarsTemplate(Host,
                !string.IsNullOrWhiteSpace(JiraDescription)
                    ? JiraDescription
                    : "");

            //Only allow mapping assignee from a property if a default assignee value exists
            if (!string.IsNullOrEmpty(AssigneeProperty) && !string.IsNullOrEmpty(Assignee))
            {
                _assigneeProperty = AssigneeProperty;
                Log.ForContext("AssigneeProperty", _assigneeProperty)
                    .Debug("Map Assignee Property: {AssigneeProperty}");
            }

            //Only allow mapping project key from a property if a default project key value exists
            if (!string.IsNullOrEmpty(ProjectKeyProperty))
            {
                _projectKeyProperty = ProjectKeyProperty;
                Log.ForContext("ProjectKeyProperty", _projectKeyProperty)
                    .Debug("Map Project Key Property: {ProjectKeyProperty}");
            }


            if (!string.IsNullOrEmpty(PriorityProperty))
                _priorityProperty = PriorityProperty;

            if (!string.IsNullOrEmpty(DefaultPriority) &&
                Enum.TryParse(DefaultPriority, true, out Priority defaultPriority)) _defaultPriority = defaultPriority;

            switch (string.IsNullOrEmpty(EventPriority))
            {
                case false when EventPriority.Contains("="):
                {
                    if (TryParsePriorityMappings(EventPriority, out var mappings))
                    {
                        _isPriorityMapping = true;
                        foreach (var mapping in mappings)
                            _priorities.Add(mapping.Key, mapping.Value);
                        Log.ForContext("Priority", _priorities, true).Debug("Priority Mappings: {Priority}");
                    }
                    else
                    {
                        Log.ForContext("Priority", _defaultPriority).Debug(
                            "Cannot parse priority type in Priority configuration '{EventPriority}' - cannot add these priority mappings; will use default of '{Priority}'",
                            EventPriority);
                        _priority = _defaultPriority;
                    }

                    break;
                }
                case false when Enum.TryParse(EventPriority, true, out Priority singlePriority):
                    _priority = singlePriority;
                    Log.ForContext("Priority", _priority).Debug("Priority: {Priority}");
                    break;
                default:
                    Log.ForContext("Priority", _defaultPriority).Debug(
                        "Priority configuration '{EventPriority}' not matched - will use default of '{Priority}'",
                        EventPriority);
                    _priority = _defaultPriority;
                    break;
            }

            if (!string.IsNullOrEmpty(RemainingEstimateProperty))
            {
                _remainingEstimateProperty = RemainingEstimateProperty;
                Log.ForContext("RemainingEstimateProperty", _remainingEstimateProperty).Debug("Map Remaining Estimate Property: {RemainingEstimateProperty}");
            }

            if (!string.IsNullOrEmpty(InitialEstimateProperty))
            {
                _initialEstimateProperty = InitialEstimateProperty;
                Log.ForContext("InitialEstimateProperty", _initialEstimateProperty).Debug("Map Initial Estimate Property: {InitialEstimateProperty}");
            }

            if (!string.IsNullOrEmpty(DueDateProperty))
            {
                _dueDateProperty = DueDateProperty;
                Log.ForContext("DueDateProperty", _dueDateProperty).Debug("Map Due Date Property: {DueDateProperty}");
            }

            _labels = SplitAndTrim(',', Labels ?? "").ToArray();
            _includeTagProperty = "Tags";
            if (!string.IsNullOrEmpty(AddEventProperty)) _includeTagProperty = AddEventProperty;
            Log.ForContext("Tags", _labels).ForContext("IncludeTags", AddEventTags)
                .ForContext("IncludeEventTags", _includeTagProperty)
                .Debug("Tags: {Tags}, IncludeTags: {IncludeTags}, Include Event Tags: {IncludeEventTags}");
        }

        public async Task CreateIssue(Event<LogEventData> evt)
        {
            if (evt == null) return;

            if ((LogEventLevelList?.Count ?? 0) > 0 && !LogEventLevelList.Contains(evt.Data.Level)) return;

            var description = RenderDescription(evt);

            var messageId = ComputeId(evt.Data.Exception ?? evt.Data.RenderedMessage);
            var summary = _generateMessage.Render(evt).CleanCRLF().TruncateWithEllipsis(255);

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
                {
                    Log.ForContext("SearchResultTotal", searchResult.total, true)
                        .Debug("Found {SearchResultTotal} matching issues, skip creating issue: {0}", summary);
                    return;
                }
            }

            var priority = ComputePriority(evt).ToString();
            var projectKey = TryGetPropertyValueCI(evt.Data.Properties, _projectKeyProperty, out var projectKeyValue)
                ? projectKeyValue
                : ProjectKey;

            var fields = new Dictionary<string, object>
            {
                {"project", new {key = projectKey.ToString().Trim()}},
                {"issuetype", new {name = JiraIssueType.Trim()}},
                {"summary", summary},
                {"description", description},
                {"priority", new {name = priority}}
            };

            var assignee = TryGetPropertyValueCI(evt.Data.Properties, _assigneeProperty, out var assigneeValue)
                ? assigneeValue
                : Assignee;
            if (!string.IsNullOrEmpty(assignee as string))
                fields.Add("assignee", new {name = assignee.ToString().Trim()});

            var initialEstimate =
                TryGetPropertyValueCI(evt.Data.Properties, _initialEstimateProperty, out var initialEstimateValue)
                    ? initialEstimateValue
                    : InitialEstimate;

            var remainingEstimate =
                TryGetPropertyValueCI(evt.Data.Properties, _remainingEstimateProperty, out var remainingEstimateValue)
                    ? remainingEstimateValue
                    : RemainingEstimate;

            switch (string.IsNullOrEmpty(initialEstimate as string))
            {
                case false when !string.IsNullOrEmpty(remainingEstimate as string):
                {
                    if (ValidDateExpression(initialEstimate as string) &&
                        ValidDateExpression(remainingEstimate as string))
                        fields.Add("timetracking",
                            new
                            {
                                originalEstimate = SetValidExpression(initialEstimate.ToString()),
                                remainingEstimate = SetValidExpression(remainingEstimate.ToString())
                            });
                    break;
                }
                case false:
                {
                    if (ValidDateExpression(initialEstimate as string))
                        fields.Add("timetracking",
                            new
                            {
                                originalEstimate = SetValidExpression(initialEstimate.ToString())
                            });
                    break;
                }
                default:
                {
                    if (!string.IsNullOrEmpty(remainingEstimate as string))
                    {
                        if (ValidDateExpression(remainingEstimate as string))
                            fields.Add("timetracking",
                                new
                                {
                                    remainingEstimate = SetValidExpression(remainingEstimate.ToString())
                                });
                    }

                    break;
                }
            }

            var dueDate = TryGetPropertyValueCI(evt.Data.Properties, _dueDateProperty, out var dueDateValue)
                ? dueDateValue
                : DueDate;

            if (!string.IsNullOrEmpty(dueDate as string))
            {
                if (ValidDate(dueDate as string))
                    fields.Add("duedate", dueDate);
                else if (ValidDateExpression(dueDate as string))
                    fields.Add("duedate", CalculateDateExpression(dueDate as string));
            }

            // Process components
            var components = ComponentsAsArray;
            if ((components?.Count ?? 0) > 0) fields.Add("components", components);

            // Process labels
            var labels = ComputeTags(evt);

            if ((labels?.Length ?? 0) > 0)
                fields.Add("labels", labels.TrimAll());

            if (SeqEventField.HasValue) fields.Add($"customfield_{SeqEventField}", messageId);
            var payload = new {fields};

            // Create the issue
            Log.ForContext("Payload", payload, true).Debug("Creating issue: {0}, priority: {1}", summary, priority);
            _step = "Will create issue";

            JiraCreateIssueResponse result;
            try
            {
                result = await client.PostAsync<JiraCreateIssueResponse, object>("issue", payload)
                    .ConfigureAwait(false);
            }
            catch (WebException e)
            {
                var r = e.Response.GetResponseStream();
                var serverResponse = "Unreadable";
                if (r != null)
                    using (var s = new StreamReader(r))
                    {
                        serverResponse = await s.ReadToEndAsync();
                    }

                Log.Error(e, "Can not create issue on Jira: {Response}", serverResponse);
                throw;
            }

            if ((result?.Errors?.Count ?? 0) > 0)
            {
                var e = new ApplicationException("Jira errors are  " + JsonConvert.SerializeObject(result.Errors));
                Log.Error(e, "Can not create issue on Jira");
                return;
            }

            _step = "Issue created";

            // Add details as comment
            if (!FullDetailsInDescription && FullDetailsAsComment)
            {
                _step = "Will add details as comment";

                var commentBody = $"{{noformat}}{evt.Data.RenderedMessage}{{noformat}}";
                if (commentBody.HasValue())
                {
                    Log.ForContext("CommentBody", commentBody).Debug("Adding details as comment ...");
                    await CommentAsync(result, commentBody).ConfigureAwait(false);
                }

                _step = "Added details as comment";
            }

            // Properties as comment
            if (!PropertiesAsComment) return;
            {
                _step = "Will add properties as comment";
                var commentBody = RenderProperties(evt, "Structured Event Properties");
                if (commentBody.HasValue())
                {
                    Log.ForContext("CommentBody", commentBody).Debug("Adding properties as comment ...");
                    await CommentAsync(result, commentBody).ConfigureAwait(false);
                }

                _step = "Added properties as comment";
            }
        }

        public static bool ValidDate(string value)
        {
            return Regex.IsMatch(value, "^(([12]\\d{3})-(0[1-9]|1[0-2])-(0[1-9]|[12]\\d|3[01]))$");
        }

        public static bool ValidDateExpression(string value)
        {
            return Regex.IsMatch(value, "^((?:(\\d+)d\\s?)?(?:(\\d+)h\\s?)?(?:(\\d+)m)?)$", RegexOptions.IgnoreCase);
        }

        public static string CalculateDateExpression(string value)
        {
            var date = DateTime.Today;
            var match = Regex.Match(value, "^((?:(\\d+)d\\s?)?(?:(\\d+)h\\s?)?(?:(\\d+)m)?)$", RegexOptions.IgnoreCase);
            if (!string.IsNullOrEmpty(match.Groups[2].Value))
                date = date.AddDays(int.Parse(match.Groups[2].Value));
            if (!string.IsNullOrEmpty(match.Groups[3].Value))
                date = date.AddHours(int.Parse(match.Groups[3].Value));
            if (!string.IsNullOrEmpty(match.Groups[4].Value))
                date = date.AddMinutes(int.Parse(match.Groups[4].Value));
            return date.ToString("yyyy-MM-dd");
        }

        public static string SetValidExpression(string value)
        {
            var match = Regex.Match(value, "^((?:(\\d+)d\\s?)?(?:(\\d+)h\\s?)?(?:(\\d+)m)?)$", RegexOptions.IgnoreCase);
            StringBuilder s = new StringBuilder();
            if (!string.IsNullOrEmpty(match.Groups[2].Value))
                s.AppendFormat("{0}d ", match.Groups[2].Value);
            if (!string.IsNullOrEmpty(match.Groups[3].Value))
                s.AppendFormat("{0}h ", match.Groups[3].Value);
            if (!string.IsNullOrEmpty(match.Groups[4].Value))
                s.AppendFormat("{0}m", match.Groups[4].Value);

            return s.ToString().Trim();
        }

        public async Task CommentAsync(JiraCreateIssueResponse createResponse, string comment)
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

            await client.PostAsync<string, object>(resource, payload).ConfigureAwait(false);
        }


        private string RenderDescription(Event<LogEventData> evt)
        {
            if (evt == null)
                return "";

            var sb = new StringBuilder();
            if (!string.IsNullOrEmpty(JiraDescription))
                sb.AppendFormat("{0} \\\\ ",
                    _generateDescription.Render(evt).Replace("\\r", "\\\\ ").Replace("\\n", "\\\\ "));

            if ((string.IsNullOrEmpty(JiraDescription) || !FullDetailsInDescription) &&
                !string.IsNullOrEmpty(JiraDescription)) return sb.ToString();
            sb.AppendLine("{noformat}");
            sb.AppendFormat("Reporter Account Name: {0}\r\n", Username);

            sb.AppendFormat("Event Id: {0}\r\n", evt.Id);
            sb.AppendFormat("Level : {0}\r\n", evt.Data.Level.ToString());
            if ((evt?.Data?.Exception ?? "").HasValue())
                sb.AppendFormat("Exception Message: {0}\r\n", evt.Data.Exception);

            sb.AppendFormat("Timestamp : {0}\r\n", evt.Data.LocalTimestamp.ToLocalTime());

            sb.AppendLine("{noformat}");
            sb.AppendLine("{noformat}");
            sb.AppendLine(FullDetailsInDescription
                ? evt.Data.RenderedMessage
                : evt.Data.RenderedMessage.TruncateWithEllipsis(512));
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

            foreach (var k in allProps) sb.AppendFormat("{0}: {1}\r\n", k.Key, JsonConvert.SerializeObject(k.Value));
            sb.AppendLine("{noformat}");
            return sb.ToString();
        }

        private string ComputeId(string input)
        {
            var md5 = MD5.Create();
            return BitConverter.ToString(md5.ComputeHash(Encoding.UTF8.GetBytes(input))).Replace("-", string.Empty);
        }

        private string[] ComputeTags(Event<LogEventData> evt)
        {
            if (!AddEventTags ||
                !TryGetPropertyValueCI(evt.Data.Properties, AddEventProperty, out var tagArrValue) ||
                !(tagArrValue is object[] tagArr))
                return _labels;

            var result = new HashSet<string>(_labels, StringComparer.OrdinalIgnoreCase);
            foreach (var p in tagArr)
            {
                if (!(p is string tags))
                    continue;

                result.UnionWith(SplitAndTrim(',', tags));
            }

            return result.ToArray();
        }

        internal Priority ComputePriority(Event<LogEventData> evt)
        {
            if (!_isPriorityMapping)
                return _priority;

            if (_priorityProperty.Equals("@Level", StringComparison.OrdinalIgnoreCase) &&
                _priorities.TryGetValue(evt.Data.Level.ToString(), out var matched))
                return matched;

            if (TryGetPropertyValueCI(evt.Data.Properties, _priorityProperty, out var priorityProperty) &&
                priorityProperty is string priorityValue &&
                _priorities.TryGetValue(priorityValue, out var matchedPriority))
                return matchedPriority;

            return _defaultPriority;
        }

        // ReSharper disable once InconsistentNaming
        internal static bool TryGetPropertyValueCI(IReadOnlyDictionary<string, object> properties, string propertyName,
            out object propertyValue)
        {
            var pair = properties.FirstOrDefault(p => p.Key.Equals(propertyName, StringComparison.OrdinalIgnoreCase));
            if (pair.Key == null)
            {
                propertyValue = null;
                return false;
            }

            propertyValue = pair.Value;
            return true;
        }

        internal static bool TryParsePriorityMappings(string encodedMappings, out Dictionary<string, Priority> mappings)
        {
            if (encodedMappings == null) throw new ArgumentNullException(nameof(encodedMappings));
            mappings = new Dictionary<string, Priority>(StringComparer.OrdinalIgnoreCase);
            var pairs = SplitAndTrim(',', encodedMappings);
            foreach (var pair in pairs)
            {
                var kv = SplitAndTrim('=', pair).ToArray();
                if (kv.Length != 2 || !Enum.TryParse(kv[1], true, out Priority value)) return false;

                mappings.Add(kv[0], value);
            }

            return true;
        }

        private static IEnumerable<string> SplitAndTrim(char splitOn, string setting)
        {
            return setting.Split(new[] {splitOn}, StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim());
        }

        #region Settings

        //Disable unused/unneeded property
        //[SeqAppSetting(
        //    DisplayName = "Seq Server Url",
        //    IsOptional = true,
        //    HelpText = "Optionally set a property for URL of the seq server. This appears in description field so that you can get back to the event from JIRA. If not specified, the base URI will be used.")]
        //public string SeqUrl { get; set; }

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
                    url += "rest/api/latest/";
                return url;
            }
        }

        public string GiveMeTheSearchDuplicateIssueUrl(string messageId)
        {
            return
                $"search?jql=project={ProjectKey}+AND+cf[{SeqEventField}]~{messageId}&maxResults=1&fields=id,key,summary,customfield_{SeqEventField}";
        }

        [SeqAppSetting(DisplayName = "Comma separated list of event levels",
            IsOptional = true,
            HelpText =
                "If specified Jira issue will be created only for the specified event levels, other levels will be discarded")]
        public string LogEventLevels { get; set; }

        public List<LogEventLevel> LogEventLevelList
        {
            get
            {
                var result = new List<LogEventLevel>();
                if (!(LogEventLevels?.HasValue() ?? false))
                    return result;

                var strValues = LogEventLevels.Split(new[] {','}, StringSplitOptions.RemoveEmptyEntries);
                if ((strValues?.Length ?? 0) == 0)
                    return result;

                strValues.Aggregate(result, (acc, strValue) =>
                {
                    if (Enum.TryParse<LogEventLevel>(strValue, out var enumValue))
                        acc.Add(enumValue);
                    return acc;
                });

                return result;
            }
        }

        [SeqAppSetting(
            IsOptional = true,
            DisplayName = "Project Key Property",
            HelpText =
                "Optional event property to read for project key. If set and not matched, Jira Project Key will act as a default fallback.")]
        public string ProjectKeyProperty { get; set; }

        [SeqAppSetting(DisplayName = "Jira Project Key",
            IsOptional = false,
            HelpText =
                "Project key for Jira issue. If Project Key Property is set, this will act as a default fallback.")]
        public string ProjectKey { get; set; }

        [SeqAppSetting(
            DisplayName = "Jira Project Components",
            IsOptional = true,
            HelpText = "Comma separated list of Jira project components")]
        public string Components { get; set; }

        public List<object> ComponentsAsArray
        {
            get
            {
                if (!(Components?.HasValue() ?? false))
                    return new List<object>();

                var strValues = Components.Split(new[] {','}, StringSplitOptions.RemoveEmptyEntries);
                return (strValues?.Length ?? 0) == 0
                    ? new List<object>()
                    : strValues.Select(v => new {name = v.Trim()}).ToList<object>();
            }
        }

        [SeqAppSetting(
            DisplayName = "Jira Issue Labels",
            IsOptional = true,
            HelpText = "Comma separated list of issue labels")]
        public string Labels { get; set; }

        //Optionally allow dynamic tags from an event property
        [SeqAppSetting(
            IsOptional = true,
            DisplayName = "Include event tags",
            HelpText =
                "Include tags from an event property as Jira issue labels - comma-delimited or array accepted. Will append to existing labels.")]
        public bool AddEventTags { get; set; }

        //The property containing tags that can be added dynamically during runtime
        [SeqAppSetting(
            IsOptional = true,
            DisplayName = "Event tag property",
            HelpText =
                "The property that contains tags to include from events- defaults to 'Tags', only used if Include Event tags is enabled.")]
        public string AddEventProperty { get; set; }

        [SeqAppSetting(
            DisplayName = "Seq Event Id custom field # from JIRA",
            IsOptional = true,
            HelpText =
                "Jira custom field number to store Seq Event Id. If provided will be used to prevent duplicate issue creation")]
        public int? SeqEventField { get; set; }

        [SeqAppSetting(
            DisplayName = "Jira issue type",
            IsOptional = true,
            HelpText = "Jira issue type")]
        public string JiraIssueType { get; set; }

        [SeqAppSetting(
            IsOptional = true,
            DisplayName = "Priority Property",
            HelpText =
                "Optional property to read for Jira priority (default @Level); properties can be mapped to Jira priorities using the Jira Priority or Property Mapping field.")]
        public string PriorityProperty { get; set; }

        [SeqAppSetting(
            IsOptional = true,
            DisplayName = "Jira Priority or Property Mapping",
            HelpText =
                "Priority for the alert - Highest, High, Medium, Low, Lowest - or 'Priority Property' mapping using Property=Mapping format - Fatal=Highest,Error=High,Medium=Medium,Lowest=Low.")]
        public string EventPriority { get; set; }

        [SeqAppSetting(
            IsOptional = true,
            DisplayName = "Default Priority",
            HelpText =
                "If using Priority Property Mapping - Default Priority for alerts not matching the mapping - Highest, High, Medium, Low, Lowest. Defaults to Medium.")]
        public string DefaultPriority { get; set; }

        [SeqAppSetting(
            IsOptional = true,
            DisplayName = "Assignee Property",
            HelpText =
                "Optional property to read for assignee.")]
        public string AssigneeProperty { get; set; }

        [SeqAppSetting(
            IsOptional = true,
            DisplayName = "Assignee",
            HelpText =
                "Optional assignee. If Assignee Property is set and matched, this will not be used. if Assignee Property is set and not matched, this will be used as default.")]
        public string Assignee { get; set; }

        [SeqAppSetting(
            IsOptional = true,
            DisplayName = "Jira Summary",
            HelpText =
                "The message associated with the alert, specified with Handlebars syntax. If blank, the message " +
                "from the incoming event or notification will be used.")]
        public string JiraSummary { get; set; }

        [SeqAppSetting(
            IsOptional = true,
            DisplayName = "Jira Description",
            HelpText =
                "The description associated with the alert, specified with Handlebars syntax. If blank, a default" +
                " description will be used.")]
        public string JiraDescription { get; set; }

        [SeqAppSetting(
            DisplayName = "Full Details As Description",
            IsOptional = true,
            InputType = SettingInputType.Checkbox,
            HelpText =
                "Check if you want to append full details of the rendered message to issue description. " +
                "If Jira Description template is not used, enabling this will add full details, disabling will include up to 512 characters of the rendered message." +
                "If Jira Description template is used, enabling this will append full details."
        )]
        public bool FullDetailsInDescription { get; set; } = false;

        [SeqAppSetting(
            DisplayName = "Full Details As Comment",
            IsOptional = true,
            InputType = SettingInputType.Checkbox,
            HelpText =
                "Check if you want to add full details of the rendered message as a comment. "
        )]
        public bool FullDetailsAsComment { get; set; } = false;

        [SeqAppSetting(
            DisplayName = "Properties As Comment",
            IsOptional = true,
            InputType = SettingInputType.Checkbox,
            HelpText =
                "Add event data structured properties as comment into the created issue"
        )]
        public bool PropertiesAsComment { get; set; } = false;

                        [SeqAppSetting(
            IsOptional = true,
            DisplayName = "Initial Estimate Property",
            HelpText =
                "Optional property to read for initial estimate. Must be in d (days), h (hours), m (minutes).")]
        public string InitialEstimateProperty { get; set; }

        [SeqAppSetting(
            IsOptional = true,
            DisplayName = "Initial Estimate",
            HelpText =
                "Optional initial estimate, format d (days), h (hours), m (minutes). If Initial Estimate Property is set and matched, this will not be used. if Initial Estimate Property is set and not matched, this will be used as default.")]
        public string InitialEstimate { get; set; }

        [SeqAppSetting(
            IsOptional = true,
            DisplayName = "Remaining Estimate Property",
            HelpText =
                "Optional property to read for Remaining Estimate. Must be in d (days), h (hours), m (minutes)")]
        public string RemainingEstimateProperty { get; set; }

        [SeqAppSetting(
            IsOptional = true,
            DisplayName = "Remaining Estimate",
            HelpText =
                "Optional remaining estimate, format d (days), h (hours), m (minutes). If Remaining Estimate Property is set and matched, this will not be used. if Remaining Estimate Property is set and not matched, this will be used as default.")]
        public string RemainingEstimate { get; set; }

        [SeqAppSetting(
            IsOptional = true,
            DisplayName = "Due Date Property",
            HelpText =
                "Optional property to read for due date. Must be formatted as yyyy-MM-dd or d (days), h (hours), m (minutes).")]
        public string DueDateProperty { get; set; }

        [SeqAppSetting(
            IsOptional = true,
            DisplayName = "Due Date",
            HelpText =
                "Optional due date (format in d (days), h (hours), m (minutes). If Due Date Property is set and matched, this will not be used. if Due Date Property is set and not matched, this will be used as default.")]
        public string DueDate { get; set; }

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
    }
}