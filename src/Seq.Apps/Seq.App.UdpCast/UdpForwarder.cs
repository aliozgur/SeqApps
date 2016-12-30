using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Seq.Apps;
using Seq.Apps.LogEvents;
using SeqApps.Commons;

namespace Seq.App.UdpCast
{
    [SeqApp("UdpCast",
        Description = "Forward seq event to UDP listener")]
    public class UdpForwarder: Reactor, ISubscribeTo<LogEventData>
    {
        #region Settings

        [SeqAppSetting(DisplayName = "Remote Address",
           IsOptional = false)]
        public string RemoteAddress { get; set; }

        [SeqAppSetting(DisplayName = "Remote Port",
           IsOptional = false)]
        public int RemotePort { get; set; }

        [SeqAppSetting(DisplayName = "Local Port",
            IsOptional = false)]
        public int LocalPort { get; set; } = 0;

        private IPEndPoint RemoteEndPoint { get; set; }


        [SeqAppSetting(DisplayName = "Event levels",
           IsOptional = true,
           HelpText = "Comma seperated list of event levels. If specified message will be created only for the specified event levels, other levels will be discarded")]
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

        [SeqAppSetting(DisplayName = "Render Properties",
            IsOptional = true,
            HelpText = "Comma seperated list of properties to include as prefix in chat message title.If specified matching log properties will be included as prefix in the chat message title")]
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

        #endregion #Settings
        public void On(Event<LogEventData> evt)
        {
            try
            {
                SendEventToUdp(evt);
            }
            catch (AggregateException aex)
            {
                var fex = aex.Flatten();
                Log.Error(fex, "Error while sending message to UdpCast");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error while sending message to UdpCast");
            }
        }

        private void SendEventToUdp(Event<LogEventData> evt)
        {
            if (evt == null)
                return;

            if ((LogEventLevelList?.Count ?? 0) > 0 && !LogEventLevelList.Contains(evt.Data.Level))
                return;

            var encoding = Encoding.UTF8;

            IPAddress remoteAddress;

            if (!IPAddress.TryParse(RemoteAddress, out remoteAddress))
                return;

            if (RemotePort < IPEndPoint.MinPort || RemotePort > IPEndPoint.MaxPort)
                return;

            int localPort = LocalPort <= IPEndPoint.MinPort || LocalPort > IPEndPoint.MaxPort ? 0 : LocalPort;
            RemoteEndPoint = new IPEndPoint(remoteAddress, RemotePort);

            using (
                UdpClient client = localPort == 0
                    ? new UdpClient(RemoteEndPoint.AddressFamily)
                    : new UdpClient(localPort, RemoteEndPoint.AddressFamily))
            {

                var renderedMessage = evt.Data.RenderedMessage;
                var propValuesInTitle = BuildPropertyValuesString(evt);
                var logLevel = evt.Data.Level.ToString();

                StringBuilder sb = new StringBuilder();
                sb.AppendLine($"[{logLevel}]{(propValuesInTitle.HasValue() ? " " + propValuesInTitle + " | " : " ")} {evt.Data.LocalTimestamp} | {renderedMessage}");

                Byte[] buffer = encoding.GetBytes(sb.ToString().ToCharArray());
                client.Send(buffer, buffer.Length, RemoteEndPoint);
                client.Close();
            }
        }

        private string BuildPropertyValuesString(Event<LogEventData> evt)
        {
            if (TitlePropertiesList?.Count > 0)
            {
                var lst = TitlePropertiesList;
                var propValuesList = new List<string>();
                foreach (var p in lst)
                {
                    object propValue = null;
                    if (evt.Data.Properties.TryGetValue(p, out propValue) && propValue != null)
                    {
                        propValuesList.Add(propValue.ToString());
                    }
                }

                return String.Join(" | ", propValuesList);
            }
            return "";
        }
    }
}
