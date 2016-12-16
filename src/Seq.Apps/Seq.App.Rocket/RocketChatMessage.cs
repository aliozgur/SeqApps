using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Seq.Apps.LogEvents;

namespace Seq.App.Rocket
{
    public class RocketAuthPayload
    {
        public string username { get; set; }
        public string password { get; set; }

    }
    public class RocketAuthTicket
    {
        public string status { get; set; }
        public RocketAuthData data { get; set; }
           
    }

    public class RocketAuthData
    {
        public string authToken { get; set; }
        public string userId { get; set; }    
    }

    public class RocketChatMessage
    {
        public string channel { get; set; }
        public string text { get; set; }
        public List<RocketChatMessageAttachment> attachments { get; set; }

    }

    public class RocketChatPostMessageResult
    {
        public bool success { get; set; }
        public string error { get; set; }
    }

    public class RocketChatMessageAttachment
    {
        public string color { get; set; }
        public string text { get; set; }
        public string title { get; set; }
        public string title_link { get; set; }
        public bool collapsed { get; set; }
        public List<RocketChatMessageAttachmentField> fields { get; set; }

        public static string ColorByLevel(LogEventLevel level)
        {
            switch (level)
            {
                case LogEventLevel.Verbose:
                    return "LightGray";
                case LogEventLevel.Debug:
                    return "Gray";
                case LogEventLevel.Warning:
                    return "Orange";
                case LogEventLevel.Information:
                    return "Blue";
                case LogEventLevel.Error:
                    return "Red";
                case LogEventLevel.Fatal:
                    return "Maroon";
                default:
                    return "LightGray";
            }
        }
    }

    public class RocketChatMessageAttachmentField
    {
        public bool @short { get; set; }
        public string title { get; set; }
        public string value{ get; set; }

    }

}
