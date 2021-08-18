using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Seq.App.Jira;

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

        [TestMethod]
        public void TestEmptyProjectKey()
        {
            var testDictionary = new Dictionary<string, object>() { {"ProjectKey", null }};
            var evt = new Event<LogEvents.LogEventData>("11",1,DateTime.Now, new LogEvents.LogEventData { Exception = "Sample exception", Level = LogEvents.LogEventLevel.Error, RenderedMessage = "Rendered Test Message", Properties = new ReadOnlyDictionary<string, object>(testDictionary) });
            var projectKey = JiraIssueReactor.TryGetPropertyValueCI(evt.Data.Properties, "ProjectKey", out var projectKeyValue)
                ? projectKeyValue ?? string.Empty
                : null ?? string.Empty;
            Assert.IsTrue(projectKey != null && (string)projectKey == string.Empty);
        }

        [TestMethod]
        public void TestEmptyPriority()
        {
            var jr = new JiraIssueReactor()
            {
                Username = "--",
                Password = "--",
                JiraIssueType = "--",
                ProjectKey = "--",
                Components = "--",
                JiraUrl = "--",
                LogEventLevels = "",
                PriorityProperty = "Priority",
                EventPriority = "P3=Medium",
                DefaultPriority = "Medium"
            };
            var testDictionary = new Dictionary<string, object>() { {"Priority", null }};
            var evt = new Event<LogEvents.LogEventData>("11",1,DateTime.Now, new LogEvents.LogEventData { Exception = "Sample exception", Level = LogEvents.LogEventLevel.Error, RenderedMessage = "Rendered Test Message", Properties = new ReadOnlyDictionary<string, object>(testDictionary) });
            var priority = jr.ComputePriority(evt);
            Assert.IsTrue(priority == Priority.Medium);
        }

        [TestMethod]
        public void TestDates()
        {
            Console.WriteLine("Test Valid Date Expression 1 ...");
            Assert.IsTrue(JiraIssueReactor.ValidDateExpression("1h 30m"));
            Console.WriteLine("Test Valid Date Expression 2 ...");
            Assert.IsTrue(JiraIssueReactor.ValidDateExpression("1d 1h 30m"));
            Console.WriteLine("Test Valid Date Expression 3 ...");
            Assert.IsTrue(JiraIssueReactor.ValidDateExpression("1d1h30m"));
            Console.WriteLine("Test Valid Date Expression 4 ...");
            Assert.IsTrue(JiraIssueReactor.ValidDateExpression("1d"));
            Console.WriteLine("Test Valid Date Expression 5 ...");
            Assert.IsFalse(JiraIssueReactor.ValidDateExpression("2021-02-31"));
            Console.WriteLine("Test Valid Date 1 ...");
            Assert.IsTrue(JiraIssueReactor.ValidDate("2021-02-31"));
            Console.WriteLine("Test Valid Date 2 ...");
            Assert.IsFalse(JiraIssueReactor.ValidDate("1h 30m"));
            Console.WriteLine("Test Calculate Date 1w 1d 24h 30m equals " + DateTime.Today.AddDays(7).AddDays(1).AddHours(24).AddMinutes(30).ToString("yyyy-MM-dd"));
            Console.WriteLine(JiraIssueReactor.CalculateDateExpression("1w 1d 24h 30m"));
            Assert.IsTrue(JiraIssueReactor.CalculateDateExpression("1w 1d 24h 30m") == DateTime.Today.AddDays(7).AddDays(1).AddHours(24).AddMinutes(30).ToString("yyyy-MM-dd"));
        }
    }
}
