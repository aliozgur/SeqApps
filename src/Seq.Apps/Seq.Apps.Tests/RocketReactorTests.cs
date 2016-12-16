using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Seq.App.Rocket;

namespace Seq.Apps.Tests
{
    [TestClass]
    public class RocketReactorTests
    {
        [TestMethod]
        public void Authenticate()
        {

            RocketReactor rr = new RocketReactor()
            {
                Channel = "#seq",
                Username = "username",
                Password = "password",
                RocketApiUrl = "https://rocket.yourdomain.com/api/v1",
                SeqUrl = "http://seq.yourdomain.com",
                AttachProperties = true,
                LogEventLevels = ""
            };

            var evt = new Event<LogEvents.LogEventData>("11",1,DateTime.Now, new LogEvents.LogEventData { Exception = "Sample exception", Level = LogEvents.LogEventLevel.Error, RenderedMessage = "Rendered"});
            rr.On(evt);
        }
    }
}
