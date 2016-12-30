using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SeqApps.GitHub;
using Moq;

namespace Seq.Apps.Tests
{
    [TestClass]
    public class GitHubIssueTests
    {
        [TestMethod]
        public void CreateIssue()
        {

            GitHubIssueReactor gitHubReactor = new GitHubIssueReactor()
            {
                GitHubUsername = "username or personal token",
                GitHubPassword = "password if specified username",
                GitHubRepoOwnerName = "aliozgur",
                GitHubRepoName = "SeqApps",
                SeqUrl = "http://seq.yourdomain.com",
                TitleProperties = "_App,_MachineName",
                LabelProperties = "_App,_Version",
                LogEventLevels = "",
                AttachProperties = true,
                AttachException = true,
                AddLogLevelAsLabel = true,
            };


            var properties = new Dictionary<string, object>()
            {
                ["_App"] = "MyApp",
                ["_MachineName"] = "MyMachine",
                ["_Version"] = "1.0",
                ["GitLabProject"] = "minecraft",

            };

            var eventData = new Mock<LogEvents.LogEventData>();
                    
            var evt = new Event<LogEvents.LogEventData>("11", 1, DateTime.Now
                , new LogEvents.LogEventData
                {
                    Exception = "Sample exception message",
                    Level = LogEvents.LogEventLevel.Error,
                    RenderedMessage = "An error occured...",
                    Properties = properties
                        
                });

            gitHubReactor.On(evt);
        }
    }
}
