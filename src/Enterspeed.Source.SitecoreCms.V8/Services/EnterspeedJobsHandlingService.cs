﻿using System;
using System.Collections.Generic;
using System.Linq;
using Enterspeed.Source.SitecoreCms.V8.Data.Models;
using Enterspeed.Source.SitecoreCms.V8.Data.Repositories;
using Enterspeed.Source.SitecoreCms.V8.Services.Contracts;

namespace Enterspeed.Source.SitecoreCms.V8.Services
{
    public class EnterspeedJobsHandlingService : IEnterspeedJobsHandlingService
    {
        private readonly IEnterspeedJobRepository _enterspeedJobRepository;
        private readonly IEnterspeedJobsHandler _enterspeedJobsHandler;
        private readonly IEnterspeedSitecoreLoggingService _enterspeedSitecoreLoggingService;

        public EnterspeedJobsHandlingService(IEnterspeedSitecoreLoggingService enterspeedSitecoreLoggingService,
            IEnterspeedJobsHandler enterspeedJobsHandler,
            IEnterspeedJobRepository enterspeedJobRepository)
        {
            _enterspeedSitecoreLoggingService = enterspeedSitecoreLoggingService;
            _enterspeedJobsHandler = enterspeedJobsHandler;
            _enterspeedJobRepository = enterspeedJobRepository;
        }


        public virtual void HandlePendingJobs(int batchSize)
        {
            int jobCount;
            do
            {
                var jobs = _enterspeedJobRepository.GetPendingJobs(batchSize).ToList();
                jobCount = jobs.Count;
                try
                {
                    HandleJobs(jobs);
                }
                catch (Exception e)
                {
                    _enterspeedSitecoreLoggingService.Error("Error has happened", e);
                }
            } while (jobCount > 0);
        }

        public virtual void HandleJobs(IList<EnterspeedJob> jobs)
        {
            if (!jobs.Any())
            {
                return;
            }

            _enterspeedSitecoreLoggingService.Debug("Handling {jobsCount} jobs " + jobs.Count);

            // Update jobs from pending to processing
            foreach (var job in jobs)
            {
                job.State = EnterspeedJobState.Processing;
                job.UpdatedAt = DateTime.UtcNow;
            }

            _enterspeedJobRepository.Save(jobs);
            _enterspeedJobsHandler.HandleJobs(jobs);
        }

        public virtual void InvalidateOldProcessingJobs()
        {
            var oldJobs = _enterspeedJobRepository.GetOldProcessingTasks().ToList();
            if (oldJobs.Any())
            {
                foreach (var job in oldJobs)
                {
                    job.State = EnterspeedJobState.Failed;
                    job.Exception = $"Job processing timed out. Last updated at: {job.UpdatedAt}";
                    job.UpdatedAt = DateTime.UtcNow;
                }

                _enterspeedJobRepository.Save(oldJobs);
            }
        }
    }
}