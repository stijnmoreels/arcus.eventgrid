﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Arcus.EventGrid.Contracts;
using Arcus.EventGrid.Contracts.Interfaces;
using Arcus.EventGrid.Parsers;
using Arcus.EventGrid.Publishing;
using Arcus.EventGrid.Testing.Infrastructure.Hosts.ServiceBus;
using Arcus.EventGrid.Tests.Core.Events;
using Arcus.EventGrid.Tests.Integration.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Arcus.EventGrid.Tests.Integration.Publishing
{
    [Trait(name: "Category", value: "Integration")]
    public class EventPublishingTests : IAsyncLifetime
    {
        private readonly XunitTestLogger _testLogger;

        private ServiceBusEventConsumerHost _serviceBusEventConsumerHost;

        public EventPublishingTests(ITestOutputHelper testOutput)
        {
            _testLogger = new XunitTestLogger(testOutput);

            Configuration = new ConfigurationBuilder()
                .AddJsonFile(path: "appsettings.json")
                .AddEnvironmentVariables()
                .Build();
        }

        protected IConfiguration Configuration { get; }

        public async Task InitializeAsync()
        {
            var connectionString = Configuration.GetValue<string>("Arcus:ServiceBus:ConnectionString");
            var topicName = Configuration.GetValue<string>("Arcus:ServiceBus:TopicName");

            var serviceBusEventConsumerHostOptions = new ServiceBusEventConsumerHostOptions(topicName, connectionString);
            _serviceBusEventConsumerHost = await ServiceBusEventConsumerHost.StartAsync(serviceBusEventConsumerHostOptions, _testLogger);
        }

        public async Task DisposeAsync()
        {
            await _serviceBusEventConsumerHost.StopAsync();
        }

        [Fact]
        public async Task PublishSingleEvent_WithBuilder_ValidParameters_Succeeds()
        {
            // Arrange
            var topicEndpoint = Configuration.GetValue<string>("Arcus:EventGrid:TopicEndpoint");
            var endpointKey = Configuration.GetValue<string>("Arcus:EventGrid:EndpointKey");
            const string eventSubject = "integration-test";
            const string licensePlate = "1-TOM-337";
            var eventId = Guid.NewGuid().ToString();
            var @event = new NewCarRegistered(eventId, eventSubject, licensePlate);

            // Act
            await EventGridPublisherBuilder
                .ForTopic(topicEndpoint)
                .UsingAuthenticationKey(endpointKey)
                .Build()
                .PublishAsync(@event);

            TracePublishedEvent(eventId, @event);

            // Assert
            var receivedEvent = _serviceBusEventConsumerHost.GetReceivedEvent(eventId);
            AssertReceivedNewCarRegisteredEvent(eventId, @event.EventType, eventSubject, licensePlate, receivedEvent);
        }

        [Fact]
        public async Task PublishSingleRawEvent_WithBuilder_ValidParameters_Succeeds()
        {
            // Arrange
            var topicEndpoint = Configuration.GetValue<string>("Arcus:EventGrid:TopicEndpoint");
            var endpointKey = Configuration.GetValue<string>("Arcus:EventGrid:EndpointKey");
            const string licensePlate = "1-TOM-337";
            const string expectedSubject = "/";
            var eventId = Guid.NewGuid().ToString();
            var @event = new NewCarRegistered(eventId, licensePlate);
            var rawEventBody = JsonConvert.SerializeObject(@event.Data);

            // Act
            await EventGridPublisherBuilder
                .ForTopic(topicEndpoint)
                .UsingAuthenticationKey(endpointKey)
                .Build()
                .PublishRawAsync(@event.Id, @event.EventType, rawEventBody);

            TracePublishedEvent(eventId, @event);

            // Assert
            var receivedEvent = _serviceBusEventConsumerHost.GetReceivedEvent(eventId);
            AssertReceivedNewCarRegisteredEvent(eventId, @event.EventType, expectedSubject, licensePlate, receivedEvent);
        }

        [Fact]
        public async Task PublishSingleRawEventWithDetailedInfo_WithBuilder_ValidParameters_Succeeds()
        {
            // Arrange
            var topicEndpoint = Configuration.GetValue<string>("Arcus:EventGrid:TopicEndpoint");
            var endpointKey = Configuration.GetValue<string>("Arcus:EventGrid:EndpointKey");
            const string eventSubject = "integration-test";
            const string licensePlate = "1-TOM-337";
            var eventId = Guid.NewGuid().ToString();
            var @event = new NewCarRegistered(eventId, eventSubject, licensePlate);
            var rawEventBody = JsonConvert.SerializeObject(@event.Data);

            // Act
            await EventGridPublisherBuilder
                .ForTopic(topicEndpoint)
                .UsingAuthenticationKey(endpointKey)
                .Build()
                .PublishRawAsync(@event.Id, @event.EventType, rawEventBody, @event.Subject, @event.DataVersion, @event.EventTime);

            TracePublishedEvent(eventId, @event);

            // Assert
            var receivedEvent = _serviceBusEventConsumerHost.GetReceivedEvent(eventId);
            AssertReceivedNewCarRegisteredEvent(eventId, @event.EventType, eventSubject, licensePlate, receivedEvent);
        }

        [Fact]
        public async Task PublishSingleTypedRawEventWithDetailedInfo_WithBuilder_ValidParameters_Succeeds()
        {
            // Arrange
            var topicEndpoint = Configuration.GetValue<string>("Arcus:EventGrid:TopicEndpoint");
            var endpointKey = Configuration.GetValue<string>("Arcus:EventGrid:EndpointKey");
            const string eventSubject = "integration-test";
            const string licensePlate = "1-TOM-337";
            var eventId = Guid.NewGuid().ToString();
            var @event = new NewCarRegistered(eventId, eventSubject, licensePlate);
            var rawEventBody = JsonConvert.SerializeObject(@event.Data);

            var rawEvent = new RawEvent(@event.Id, @event.EventType, rawEventBody, @event.Subject, @event.DataVersion, @event.EventTime);

            // Act
            await EventGridPublisherBuilder
                  .ForTopic(topicEndpoint)
                  .UsingAuthenticationKey(endpointKey)
                  .Build()
                  .PublishRawAsync(rawEvent);

            TracePublishedEvent(eventId, @event);

            // Assert
            var receivedEvent = _serviceBusEventConsumerHost.GetReceivedEvent(eventId);
            AssertReceivedNewCarRegisteredEvent(eventId, @event.EventType, eventSubject, licensePlate, receivedEvent);
        }


        [Fact]
        public async Task PublishMultipleEvents_WithBuilder_ValidParameters_SucceedsWithRetryCount()
        {
            // Arrange
            var topicEndpoint = Configuration.GetValue<string>("Arcus:EventGrid:TopicEndpoint");
            var endpointKey = Configuration.GetValue<string>("Arcus:EventGrid:EndpointKey");
            var events =
                Enumerable
                    .Repeat<Func<Guid>>(Guid.NewGuid, 2)
                    .Select(newGuid => new NewCarRegistered(
                        newGuid().ToString(),
                        subject: "integration-test",
                        licensePlate: "1-TOM-337"))
                    .ToArray();

            // Act
            await EventGridPublisherBuilder
                .ForTopic(topicEndpoint)
                .UsingAuthenticationKey(endpointKey)
                .Build()
                .PublishManyAsync(events);

            // Assert
            Assert.All(events, @event => AssertReceivedEvent(@event, @event.Data.LicensePlate, GetReceivedEvent.WithRetryCount));
        }

        [Fact]
        public async Task PublishMultipleEvents_WithBuilder_ValidParameters_SucceedsWithTimeout()
        {
            // Arrange
            var topicEndpoint = Configuration.GetValue<string>("Arcus:EventGrid:TopicEndpoint");
            var endpointKey = Configuration.GetValue<string>("Arcus:EventGrid:EndpointKey");
            var events =
                Enumerable
                    .Repeat<Func<Guid>>(Guid.NewGuid, 2)
                    .Select(newGuid => new NewCarRegistered(
                        newGuid().ToString(),
                        subject: "integration-test",
                        licensePlate: "1-TOM-337"))
                    .ToArray();

            // Act
            await EventGridPublisherBuilder
                  .ForTopic(topicEndpoint)
                  .UsingAuthenticationKey(endpointKey)
                  .Build()
                  .PublishManyAsync(events);

            // Assert
            Assert.All(events, @event => AssertReceivedEvent(@event, @event.Data.LicensePlate, GetReceivedEvent.WithTimeout));
        }

        [Fact]
        public async Task PublishMultipleRawEvents_WithBuilder_ValidParameters_SucceedsWithRetryCount()
        {
            // Arrange
            var topicEndpoint = Configuration.GetValue<string>("Arcus:EventGrid:TopicEndpoint");
            var endpointKey = Configuration.GetValue<string>("Arcus:EventGrid:EndpointKey");
            const string licensePlate = "1-TOM-1337";
            var events =
                Enumerable
                    .Repeat<Func<Guid>>(Guid.NewGuid, 2)
                    .Select(newGuid => new RawEvent(
                                newGuid().ToString(),
                                subject: "integration-test",
                                body: $"{{\"licensePlate\": \"{licensePlate}\"}}",
                                type: "Arcus.Samples.Cars.NewCarRegistered",
                                dataVersion:"1.0",
                                eventTime: DateTimeOffset.Now))
                    .ToArray();

            // Act
            await EventGridPublisherBuilder
                  .ForTopic(topicEndpoint)
                  .UsingAuthenticationKey(endpointKey)
                  .Build()
                  .PublishManyAsync(events);

            // Assert
            Assert.All(events, rawEvent => AssertReceivedEvent(rawEvent, licensePlate, GetReceivedEvent.WithRetryCount));
        }

        [Fact]
        public async Task PublishMultipleRawEvents_WithBuilder_ValidParameters_SucceedsWithTimeout()
        {
            // Arrange
            var topicEndpoint = Configuration.GetValue<string>("Arcus:EventGrid:TopicEndpoint");
            var endpointKey = Configuration.GetValue<string>("Arcus:EventGrid:EndpointKey");
            const string licensePlate = "1-TOM-1337";
            var events =
                Enumerable
                    .Repeat<Func<Guid>>(Guid.NewGuid, 2)
                    .Select(newGuid => new RawEvent(
                                newGuid().ToString(),
                                subject: "integration-test",
                                body: $"{{\"licensePlate\": \"{licensePlate}\"}}",
                                type: "Arcus.Samples.Cars.NewCarRegistered",
                                dataVersion:"1.0",
                                eventTime: DateTimeOffset.Now))
                    .ToArray();

            // Act
            await EventGridPublisherBuilder
                  .ForTopic(topicEndpoint)
                  .UsingAuthenticationKey(endpointKey)
                  .Build()
                  .PublishManyRawAsync(events);

            // Assert
            Assert.All(events, rawEvent => AssertReceivedEvent(rawEvent, licensePlate, GetReceivedEvent.WithTimeout));
        }

        private enum GetReceivedEvent { WithRetryCount, WithTimeout }

        private void AssertReceivedEvent(IEvent @event, string licensePlate, GetReceivedEvent getReceivedEvent)
        {
            TracePublishedEvent(@event.Id, @event);
            string GetReceivedEvent()
            {
                switch (getReceivedEvent)
                {
                    case EventPublishingTests.GetReceivedEvent.WithRetryCount:
                        return _serviceBusEventConsumerHost.GetReceivedEvent(@event.Id, retryCount: 5);
                    case EventPublishingTests.GetReceivedEvent.WithTimeout:
                        return _serviceBusEventConsumerHost.GetReceivedEvent(@event.Id, TimeSpan.FromSeconds(10));
                    default:
                        throw new ArgumentOutOfRangeException(nameof(getReceivedEvent), getReceivedEvent, "Unknown get received event approach");
                }
            }

            string receivedEvent = GetReceivedEvent();
            AssertReceivedNewCarRegisteredEvent(@event.Id, @event.EventType, @event.Subject, licensePlate, receivedEvent);
        }

        private static void AssertReceivedNewCarRegisteredEvent(string eventId, string eventType, string eventSubject, string licensePlate, string receivedEvent)
        {
            Assert.NotEqual(String.Empty, receivedEvent);

            EventGridMessage<NewCarRegistered> deserializedEventGridMessage = EventGridParser.Parse<NewCarRegistered>(receivedEvent);
            Assert.NotNull(deserializedEventGridMessage);
            Assert.NotEmpty(deserializedEventGridMessage.SessionId);
            Assert.NotNull(deserializedEventGridMessage.Events);

            NewCarRegistered deserializedEvent = Assert.Single(deserializedEventGridMessage.Events);
            Assert.NotNull(deserializedEvent);
            Assert.Equal(eventId, deserializedEvent.Id);
            Assert.Equal(eventSubject, deserializedEvent.Subject);
            Assert.Equal(eventType, deserializedEvent.EventType);

            Assert.NotNull(deserializedEvent.Data);
            Assert.Equal(licensePlate, deserializedEvent.Data.LicensePlate);
        }

        private void TracePublishedEvent(string eventId, object events)
        {
            _testLogger.LogInformation($"Event '{eventId}' published - {JsonConvert.SerializeObject(events)}");
        }
    }
}