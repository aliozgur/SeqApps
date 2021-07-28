using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Seq.App.Jira;
using Seq.App.Rocket;

namespace Seq.Apps.Tests
{
    [TestClass]
    public class JiraIssueReactorTests
    {
        [TestMethod]
        public async void CreateIssue()
        {

            var jr = new JiraIssueReactor()
            {
                Username = "--",
                Password = "--",
                JiraIssueType = "--",
                ProjectKey = "--",
                Components = "--",
                JiraUrl = "--",
                LogEventLevels = ""
            };

            var evt = new Event<LogEvents.LogEventData>("11",1,DateTime.Now, new LogEvents.LogEventData { Exception = "Sample exception", Level = LogEvents.LogEventLevel.Error, RenderedMessage = "Rendered Test Message"});
            await jr.OnAsync(evt);
        }
    }
}
