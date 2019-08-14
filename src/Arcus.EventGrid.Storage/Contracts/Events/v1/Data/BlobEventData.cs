﻿using Microsoft.Azure.EventGrid.Models;
using System;

namespace Arcus.EventGrid.Storage.Contracts.Events.v1.Data
{
    /// <summary>
    ///     Event data contract for Azure Blob Storage events
    /// </summary>
    [Obsolete(
        "Azure Event Grid events are now being used in favor of specific Arcus event types, use " 
        + nameof(StorageBlobCreatedEventData) + " for example or any  other  'StorageBlob...' event data models" )]
    public class BlobEventData
    {
        public string Api { get; set; }
        public string BlobType { get; set; }
        public string ClientRequestId { get; set; }
        public int ContentLength { get; set; }
        public string ContentType { get; set; }
        public string ETag { get; set; }
        public string RequestId { get; set; }
        public string Sequencer { get; set; }
        public StorageDiagnostics StorageDiagnostics { get; set; }
        public string Url { get; set; }
    }
}