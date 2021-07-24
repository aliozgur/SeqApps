using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using HandlebarsDotNet;
using Seq.Apps;
using Seq.Apps.LogEvents;

// ReSharper disable ArrangeTypeModifiers
// ReSharper disable ArrangeTypeMemberModifiers

namespace Seq.App.Jira
{
    class HandlebarsTemplate
    {
        readonly Host _host;
        readonly Func<object, string> _template;

        public HandlebarsTemplate(Host host, string template)
        {
            if (template == null) throw new ArgumentNullException(nameof(template));
            _host = host ?? throw new ArgumentNullException(nameof(host));
            var compiled = Handlebars.Compile(template);
            _template = o => compiled(o);
        }

        public string Render(Event<LogEventData> evt)
        {
            if (evt == null) throw new ArgumentNullException(nameof(evt));
            return FormatTemplate(_template, evt, _host);
        }

        static string FormatTemplate(Func<object, string> template, Event<LogEventData> evt, Host host)
        {
            var properties =
                (IDictionary<string, object>) ToDynamic(evt.Data.Properties ?? new Dictionary<string, object>());

            var payload = (IDictionary<string, object>) ToDynamic(new Dictionary<string, object>
            {
                {"$Id", evt.Id},
                {"$UtcTimestamp", evt.TimestampUtc},
                {"$LocalTimestamp", evt.Data.LocalTimestamp},
                {"$Level", evt.Data.Level},
                {"$MessageTemplate", evt.Data.MessageTemplate},
                {"$Message", evt.Data.RenderedMessage},
                {"$Exception", evt.Data.Exception},
                {"$Properties", properties},
                {"$EventType", "$" + evt.EventType.ToString("X8")},
                {"$Instance", host.InstanceName},
                {"$ServerUri", host.BaseUri},
                // Note, this will only be valid when events are streamed directly to the app, and not when the app is sending an alert notification.
                {
                    "$EventUri",
                    string.Concat(host.BaseUri, "#/events?filter=@Id%20%3D%20'", evt.Id, "'&amp;show=expanded")
                }
            });

            foreach (var property in properties) payload[property.Key] = property.Value;

            return template(payload);
        }

        static object ToDynamic(object o)
        {
            switch (o)
            {
                case IEnumerable<KeyValuePair<string, object>> dictionary:
                {
                    var result = new ExpandoObject();
                    var asDict = (IDictionary<string, object>) result;
                    foreach (var kvp in dictionary)
                        asDict.Add(kvp.Key, ToDynamic(kvp.Value));
                    return result;
                }
                case IEnumerable<object> enumerable:
                    return enumerable.Select(ToDynamic).ToArray();
                default:
                    return o;
            }
        }
    }
}