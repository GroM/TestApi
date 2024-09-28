using CleoAssignment.ApiService;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace CleoAssignment.Tests;

public class IncludedBasicUnitTests
{
    [Fact]
    public async Task ThrottlingWorksFor_SameIpGetsBanned()
    {
        var timeProvider = new ManualTimeProvider { UtcNow = DateTime.UnixEpoch };
        var resourceProvider = new InjectedResourceProvider<int>(_ => 1, (_, _) => { });

        var throttleSettings = DefaultThrottleSettings;

        var apiService = ApiServiceFactory.CreateApiService(throttleSettings, resourceProvider, timeProvider);

        Assert.True((await apiService.GetResource(new("127.0.0.1", "id1"))).Success);
        Assert.Equal(1, (await apiService.GetResource(new("127.0.0.1", "id1"))).ResourceData);
        Assert.False((await apiService.GetResource(new("127.0.0.1", "id1"))).Success);
    }

    [Fact]
    public async Task ThrottlingDoesNotBan_AfterIntervalPasses()
    {
        var timeProvider = new ManualTimeProvider { UtcNow = DateTime.UnixEpoch + TimeSpan.FromSeconds(40) };
        var resourceProvider = new InjectedResourceProvider<int>(_ => 1, (_, _) => { });

        var throttleSettings = DefaultThrottleSettings;

        var apiService = ApiServiceFactory.CreateApiService(throttleSettings, resourceProvider, timeProvider);

        Assert.True((await apiService.GetResource(new("127.0.0.1", "id1"))).Success);
        Assert.Equal(1, (await apiService.GetResource(new("127.0.0.1", "id2"))).ResourceData);

        timeProvider.UtcNow += TimeSpan.FromSeconds(30);

        Assert.True((await apiService.GetResource(new("127.0.0.1", "id1"))).Success);
        Assert.True((await apiService.GetResource(new("127.0.0.1", "id2"))).Success);
        Assert.False((await apiService.GetResource(new("127.0.0.1", "id3"))).Success);

        timeProvider.UtcNow += throttleSettings.BanTimeOut;

        Assert.True((await apiService.GetResource(new("127.0.0.1", "id1"))).Success);
    }

    [Fact]
    public async Task ThrottlingDoesNotBan_SameResourceDifferentIp()
    {
        var timeProvider = new ManualTimeProvider { UtcNow = DateTime.UnixEpoch };
        var resourceProvider = new InjectedResourceProvider<int>(_ => 1, (_, _) => { });

        var throttleSettings = DefaultThrottleSettings;

        var apiService = ApiServiceFactory.CreateApiService(throttleSettings, resourceProvider, timeProvider);

        Assert.True((await apiService.GetResource(new("127.0.0.1", "id1"))).Success);
        Assert.True((await apiService.GetResource(new("127.0.0.1", "id1"))).Success);
        Assert.True((await apiService.GetResource(new("127.0.0.2", "id1"))).Success);
        Assert.True((await apiService.GetResource(new("127.0.0.2", "id1"))).Success);
    }

    [Fact]
    public async Task CachingWorksFor_DifferentRequesters()
    {
        var timeProvider = new ManualTimeProvider { UtcNow = DateTime.UnixEpoch };

        var resourceCallCounter = 0;

        var resourceProvider = new InjectedResourceProvider<int>(_ =>
                                                                 {
                                                                     Interlocked.Increment(ref resourceCallCounter);

                                                                     return 1;
                                                                 },
                                                                 (_, _) => { });

        var throttleSettings = DefaultThrottleSettings;

        var apiService = ApiServiceFactory.CreateApiService(throttleSettings, resourceProvider, timeProvider);

        Assert.True((await apiService.GetResource(new("127.0.0.1", "id1"))).Success);
        Assert.True((await apiService.GetResource(new("127.0.0.2", "id1"))).Success);
        Assert.Equal(1, resourceCallCounter);
    }

    private ThrottleSettings DefaultThrottleSettings => new()
    {
        ThrottleInterval = TimeSpan.FromMinutes(1),
        MaxRequestsPerIp = 2,
        BanTimeOut = TimeSpan.FromMinutes(1),
    };

    private class ManualTimeProvider : ITimeProvider
    {
        public DateTime UtcNow { get; set; }
    }

    private class InjectedResourceProvider<TResource> : IResourceProvider<TResource>
    {
        private readonly Func<string, TResource> _getResource;
        private readonly Action<string, TResource> _addOrUpdateResource;

        public InjectedResourceProvider(Func<string, TResource> getResourceFunc,
                                        Action<string, TResource> addOrUpdateResourceAction)
        {
            _getResource = getResourceFunc;
            _addOrUpdateResource = addOrUpdateResourceAction;
        }

        public TResource GetResource(string id) => _getResource(id);

        public void AddOrUpdateResource(string id, TResource resource) => _addOrUpdateResource(id, resource);
    }
}
