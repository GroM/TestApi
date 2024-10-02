using Microsoft.VisualStudio.TestPlatform.CommunicationUtilities.Resources;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CS.ApiService.Real
{
    public sealed class ThrottledApi<T> : IApiService<T>
    {
        #region Resources caching
        private readonly ConcurrentDictionary<string, Lazy<T>> cache = [];
        private readonly ConcurrentDictionary<string, ReaderWriterLockSlim> cacheUpdating = [];
        private ReaderWriterLockSlim GetLock(string resourceId) => cacheUpdating.GetOrAdd(resourceId, new ReaderWriterLockSlim());
        #endregion

        #region Configuration
        internal static ThrottledApi<T> ForUnitTest(ThrottleSettings throttleConfig, IResourceProvider<T> resource, ITimeProvider time)
        {
            return new ThrottledApi<T>(throttleConfig, resource, time);
        }
        public void Initialize(ThrottleSettings throttleConfig, IResourceProvider<T> resource, ITimeProvider time)
        {
            ResourceProvider = resource;
            TimeProvider = time;
            Throttle = throttleConfig;
                 
            cache.Clear();
            throttleBans.Clear();
        }

        private ThrottleSettings throttle;
        public ThrottleSettings Throttle
        {
            get => throttle;
            set
            {
                if (value != throttle)
                {
                    throttle = value;
                    ResetThrottleData();
                }
            }
        }

        private IResourceProvider<T> resourceProvider;
        public IResourceProvider<T> ResourceProvider
        {
            get => resourceProvider;
            set
            {
                resourceProvider = value;
                cache.Clear();
            }
        }

        public ITimeProvider TimeProvider { get; set; }

        #endregion

        #region Throttle related
        private readonly ConcurrentDictionary<string, uint> throttleCounters = [];
        private DateTime throttlePeriodEnds = DateTime.MinValue;
        private readonly ConcurrentDictionary<string, DateTime> throttleBans = [];
        private void ResetThrottleData()
        {
            throttleCounters.Clear();
            throttlePeriodEnds = ComputeThisPeriodEnd();
        }

        private bool CanExecute(string clientIdentifier)
        {
            var current = IncreaseCounter(clientIdentifier);
            if (current > Throttle.MaxRequestsPerIp)
            {
                BanClient(clientIdentifier);
                return false;
            }
            return true;
        }

        private uint IncreaseCounter(string clientIdentifier)
        {
            /**
             * Resetting counters can be made by who ways:
             * 1) if there was no ITimerProvider, then clearing could be done at specific intervals - reset every ThrottleInterval
             * 2) below, when counter should be increased check if current time exceeds when throttle period should end, if yes, start new period
             **/
            if (TimeProvider.UtcNow > throttlePeriodEnds)
            {
                ResetThrottleData();
            }
            return throttleCounters.AddOrUpdate(clientIdentifier, 1u, (_, v) => ++v);
        }

        private void BanClient(string clientIdentifier)
        {
            var banTill = TimeProvider.UtcNow.Add(Throttle.BanTimeOut);
            throttleBans.AddOrUpdate(clientIdentifier, banTill, (_, _) => banTill);
        }

        private DateTime ComputeThisPeriodEnd()
        {
            /**
             * This uses arithmetic for computing periods
             * (now - periodRoot) % periodLength = how many ticks are we in current period
             * now - [above value] = start of period
             **/
            var now = TimeProvider.UtcNow.Ticks;
            var periodRoot = Throttle.IntervalRootUtc.Ticks;
            var periodLength = Throttle.ThrottleInterval.Ticks;
            var periodEnds = now - ((now - periodRoot) % periodLength) + periodLength;
            return new(periodEnds, DateTimeKind.Utc);
        }
        #endregion

        #region Singleton related code
        //this gives are that singleton is only once initialized
        private static readonly Lazy<ThrottledApi<T>> instance = new(() => new ThrottledApi<T>());
        public static ThrottledApi<T> Instance => instance.Value;
        private ThrottledApi() { }
        private ThrottledApi(ThrottleSettings throttleConfig, IResourceProvider<T> resource, ITimeProvider time)
        {
            Initialize(throttleConfig, resource, time);
        }
        
        #endregion

        #region Validation
        private static bool ValidateRequest(ResourceRequestBase request)
        {
            return request?.IpAddress != null && request.ResourceId != null;
        }
        #endregion

        #region Implementation of API
        /**
         * If we could be sure that service is not processing data - storing 1:1, we could on success replace cached version
         * Skip updating on first request after update and reduce number of roundtrips for time consuming resources
         **/
        public Task<AddOrUpdateResponse> AddOrUpdateResource(AddOrUpdateRequest<T> request)
        {
            if (!ValidateRequest(request))
            {
                return Task.FromResult<AddOrUpdateResponse>(new(false, ErrorType.InvalidArgument));
            }
            if (CanExecute(request.IpAddress))
            {
                var cacheLock = GetLock(request.ResourceId);
                cacheLock.EnterWriteLock();
                try
                {
                    resourceProvider.AddOrUpdateResource(request.ResourceId, request.Resource);
                    Lazy<T> oldValue;
                    cache.TryRemove(request.ResourceId, out oldValue);
                    return Task.FromResult<AddOrUpdateResponse>(new(true, null));
                }
                finally
                {
                    cacheLock.ExitWriteLock();
                }
            }
            return Task.FromResult<AddOrUpdateResponse>(new(false, ErrorType.Banned));
        }

        public Task<GetResponse<T>> GetResource(GetRequest request)
        {
            ErrorType? errorType = null;
            if (request?.IpAddress == null || request?.ResourceId == null)
            {
                return Task.FromResult<GetResponse<T>>(new(false, default, ErrorType.InvalidArgument));
            }
            errorType = ErrorType.Banned;
            if (CanExecute(request.IpAddress))
            {
                var cacheLock = GetLock(request.ResourceId);
                cacheLock.EnterReadLock();
                try
                {
                    return Task.FromResult<GetResponse<T>>(new(true, cache.GetOrAdd(request.ResourceId, ResourceFactory).Value, null));
                }
                catch (Exception)
                {
                    errorType = ErrorType.Exception;
                }
                finally
                {
                    cacheLock.ExitReadLock();
                }
            }
            return Task.FromResult<GetResponse<T>>(new(false, default, errorType));
        }
        #endregion

        #region Accessing resources
        private Lazy<T> ResourceFactory(string ResourceId) => new(() => resourceProvider.GetResource(ResourceId));
        #endregion
    }
}
