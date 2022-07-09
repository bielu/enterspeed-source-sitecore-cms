﻿using System;
using System.Collections.Generic;
using Enterspeed.Source.Sdk.Api.Models;
using Enterspeed.Source.Sdk.Api.Services;
using Enterspeed.Source.SitecoreCms.V8.Data.Models;
using Enterspeed.Source.SitecoreCms.V8.Exceptions;
using Enterspeed.Source.SitecoreCms.V8.Models;
using Enterspeed.Source.SitecoreCms.V8.Models.Mappers;
using Enterspeed.Source.SitecoreCms.V8.Services.Contracts;
using Sitecore.Abstractions;
using Sitecore.Data;
using Sitecore.Data.Items;
using Sitecore.Globalization;

namespace Enterspeed.Source.SitecoreCms.V8.Handlers.Content
{
    public class EnterspeedContentPublishJobHandler
    {
        private readonly IEntityModelMapper<Item, SitecoreContentEntity> _sitecoreContentEntityModelMapper;
        private readonly IEnterspeedConfigurationService _enterspeedConfigurationService;
        private readonly IEnterspeedIngestService _enterspeedIngestService;
        private readonly BaseItemManager _itemManager;

        public EnterspeedContentPublishJobHandler(
            IEntityModelMapper<Item, SitecoreContentEntity> sitecoreContentEntityModelMapper,
            IEnterspeedConfigurationService enterspeedConfigurationService,
            BaseItemManager itemManager, 
            IEnterspeedIngestService enterspeedIngestService)
        {
            _sitecoreContentEntityModelMapper = sitecoreContentEntityModelMapper;
            _enterspeedConfigurationService = enterspeedConfigurationService;
            _itemManager = itemManager;
            _enterspeedIngestService = enterspeedIngestService;
        }

        public virtual bool CanHandle(EnterspeedJob job)
        {
            return job.EntityType == EnterspeedJobEntityType.Content
                   && job.JobType == EnterspeedJobType.Publish
                   && job.ContentState == EnterspeedContentState.Publish;
        }

        public virtual void Handle(EnterspeedJob job)
        {
            var item = GetContent(job);
            if (!CanIngest(item, job))
            {
                return;
            }

            var contentEntities = CreateSitecoreContentEntity(item, job);
            foreach (var sitecoreContentEntity in contentEntities)
            {
                Ingest(sitecoreContentEntity, job);
            }
        }

        protected virtual Item GetContent(EnterspeedJob job)
        {
            var isItemId = ID.TryParse(job.EntityId, out var itemId);
            var item = isItemId
                ? _itemManager.GetItem(itemId, Language.Parse(job.Culture), Sitecore.Data.Version.Latest,
                    Database.GetDatabase("Master"))
                : null;

            if (item == null)
            {
                throw new JobHandlingException($"Item with id {job.EntityId} does not exist");
            }

            return item;
        }

        protected virtual bool CanIngest(Item content, EnterspeedJob job)
        {
            // Check if any of guards are against it
            // TODO: Implement guard service
            return true;
            //return _enterspeedGuardService.CanIngest(content, job.Culture);
        }

        protected virtual List<SitecoreContentEntity> CreateSitecoreContentEntity(Item item, EnterspeedJob job)
        {
            var sitecoreEntities = new List<SitecoreContentEntity>();
            try
            {
                var configurations = _enterspeedConfigurationService.GetConfigurations();
                foreach (var configuration in configurations)
                {
                    var sitecoreEntity = _sitecoreContentEntityModelMapper.Map(item, configuration);
                    sitecoreEntities.Add(sitecoreEntity);
                }
            }
            catch (Exception e)
            {
                throw new JobHandlingException(
                    $"Failed creating entity ({job.EntityId}/{job.Culture}). Message: {e.Message}. StackTrace: {e.StackTrace}");
            }

            return sitecoreEntities;
        }

        protected virtual void Ingest(IEnterspeedEntity umbracoData, EnterspeedJob job)
        {
            var ingestResponse = _enterspeedIngestService.Save(umbracoData);
            if (!ingestResponse.Success)
            {
                var message = ingestResponse.Exception != null
                    ? ingestResponse.Exception.Message
                    : ingestResponse.Message;
                throw new JobHandlingException(
                    $"Failed ingesting entity ({job.EntityId}/{job.Culture}). Message: {message}");
            }
        }
    }
}