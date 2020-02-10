﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Arcus.EventGrid.Contracts;
using GuardNet;
using Newtonsoft.Json.Linq;

namespace Arcus.EventGrid.Parsers 
{
    /// <summary>
    /// Provides event parsing of raw JSON payloads to an abstracted <see cref="Event"/>.
    /// </summary>
    public static class EventParser
    {
        /// <summary>
        /// Loads a raw JSON payload into an abstracted event.
        /// </summary>
        /// <param name="rawJson">The raw JSON payload, representing an event that can be handled by EventGrid.</param>
        public static EventGridEventBatch<Event> Parse(byte[] rawJson)
        {
            Guard.NotNull(rawJson, nameof(rawJson), "Cannot parse a 'null' series of bytes raw JSON payload to an abstracted event");
            Guard.NotLessThanOrEqualTo(rawJson.Length, threshold: 0, "Cannot parse a series of bytes of a length <= 0 to an abstracted event");

            EventGridEventBatch<Event> eventBatch = Parse(rawJson, sessionId: Guid.NewGuid().ToString());
            return eventBatch;
        }

        /// <summary>
        /// Loads a raw JSON payload into an abstracted event with a specific <paramref name="sessionId"/>.
        /// </summary>
        /// <param name="rawJson">The raw JSON payload, representing an event that can be handled by EventGrid.</param>
        /// <param name="sessionId">The reference ID for this event parsing session.</param>
        public static EventGridEventBatch<Event> Parse(byte[] rawJson, string sessionId)
        {
            Guard.NotNull(rawJson, nameof(rawJson), "Cannot parse a 'null' series of bytes raw JSON payload to an abstracted event");
            Guard.NotLessThanOrEqualTo(rawJson.Length, threshold: 0, "Cannot parse a series of bytes of a length <= 0 to an abstracted event");

            // TODO: CloudEvents SDK can also parse byte arrays directly, but it can also parse directly JObjects (which we use now). Should we use both?
            string json = Encoding.UTF8.GetString(rawJson);
            EventGridEventBatch<Event> eventBatch = Parse(json, sessionId);

            return eventBatch;
        }

        /// <summary>
        /// Loads a raw JSON payload into an abstracted event.
        /// </summary>
        /// <param name="rawJson">The raw JSON payload, representing an event that can be handled by EventGrid.</param>
        public static EventGridEventBatch<Event> Parse(string rawJson)
        {
            Guard.NotNullOrWhitespace(rawJson, nameof(rawJson), "Cannot parse a blank raw JSON payload to an abstracted event");

            EventGridEventBatch<Event> eventBatch = Parse(rawJson, sessionId: Guid.NewGuid().ToString());
            return eventBatch;
        }

        /// <summary>
        /// Loads a raw JSON payload into an abstracted event with a specific <paramref name="sessionId"/>.
        /// </summary>
        /// <param name="rawJson">The raw JSON payload, representing an event that can be handled by EventGrid.</param>
        /// <param name="sessionId">The reference ID for this event parsing session.</param>
        public static EventGridEventBatch<Event> Parse(string rawJson, string sessionId)
        {
            Guard.NotNullOrWhitespace(rawJson, nameof(rawJson), "Cannot parse a blank raw JSON payload to an abstracted event");
            Guard.NotNullOrWhitespace(sessionId, nameof(sessionId), "Cannot parse a raw JSON payload with a blank session ID");

            JToken jToken = JToken.Parse(rawJson);

            if (jToken.Type == JTokenType.Array)
            {
                List<Event> deserializedEvents = 
                    jToken.Children<JObject>()
                          .Select(jObject => new Event(jObject))
                          .ToList();

                var result = new EventGridEventBatch<Event>(sessionId, deserializedEvents);
                return result;
            }
            else if (jToken.Type == JTokenType.Object)
            {
                var @event = new Event((JObject) jToken);
                var deserializedEvents = new List<Event> { @event };

                var result = new EventGridEventBatch<Event>(sessionId, deserializedEvents);
                return result;
            }

            throw new InvalidOperationException(
                "Couldn't find a correct JSON structure (array or object) to parse the EventGridEvent/CloudEvents from");
        }
    }
}