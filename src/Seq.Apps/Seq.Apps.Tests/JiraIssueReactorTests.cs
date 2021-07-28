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

        [TestMethod]
        public void TestDates()
        {
            Console.WriteLine("Test Valid Date Expression 1 ...");
            Assert.IsTrue(JiraIssueReactor.ValidDateExpression("1h 30m"));
            Console.WriteLine("Test Valid Date Expression 2 ...");
            Assert.IsTrue(JiraIssueReactor.ValidDateExpression("1d 1h 30m"));
            Console.WriteLine("Test Valid Date Expression 3 ...");
            Assert.IsFalse(JiraIssueReactor.ValidDateExpression("2021-02-31"));
            Console.WriteLine("Test Valid Date 1 ...");
            Assert.IsTrue(JiraIssueReactor.ValidDate("2021-02-31"));
            Console.WriteLine("Test Valid Date 2 ...");
            Assert.IsFalse(JiraIssueReactor.ValidDate("1h 30m"));
            Console.WriteLine("Test Calculate Date 1d 24h 30m equals " + DateTime.Today.AddDays(1).AddHours(24).AddMinutes(30).ToString("yyyy-MM-dd"));
            Console.WriteLine(JiraIssueReactor.CalculateDateExpression("1d 24h 30m"));
            Assert.IsTrue(JiraIssueReactor.CalculateDateExpression("1d 24h 30m") == DateTime.Today.AddDays(1).AddHours(24).AddMinutes(30).ToString("yyyy-MM-dd"));
        }
    }
}
