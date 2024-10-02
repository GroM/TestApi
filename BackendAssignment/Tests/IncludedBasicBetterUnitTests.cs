using CS.ApiService;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace CS.Tests;

public class IncludedBasicBetterUnitTests
{
    [Fact]
    public async Task ThrottlingWorksFor_SameIpGetsBanned()
    {
        var timeProvider = new ManualTimeProvider { UtcNow = DateTime.UnixEpoch };
        var resourceProvider = new InjectedResourceProvider<int>(_ => 1, (_, _) => { });

        var throttleSettings = DefaultThrottleSettings;

        var apiService = ApiServiceFactory.CreateApiService(throttleSettings, resourceProvider, timeProvider);

        var r1 = await apiService.GetResource(new("127.0.0.1", "id1"));
        Assert.True(r1.Success);
        Assert.Equal(1, r1.ResourceData);
        var r2 = await apiService.GetResource(new("127.0.0.1", "id1"));
        Assert.True(r2.Success);
        Assert.Equal(1, r2.ResourceData);
        var r3 = await apiService.GetResource(new("127.0.0.1", "id1"));
        Assert.False(r3.Success);
    }

    [Fact]
    public async Task ThrottlingDoesNotBan_AfterIntervalPasses()
    {
        var timeProvider = new ManualTimeProvider { UtcNow = DateTime.UnixEpoch + TimeSpan.FromSeconds(40) };
        var resourceProvider = new InjectedResourceProvider<int>(_ => 1, (_, _) => { });

        var throttleSettings = DefaultThrottleSettings;

        var apiService = ApiServiceFactory.CreateApiService(throttleSettings, resourceProvider, timeProvider);

        var r1 = await apiService.GetResource(new("127.0.0.1", "id1"));
        Assert.True(r1.Success);
        Assert.Equal(1, r1.ResourceData);
        var r2 = await apiService.GetResource(new("127.0.0.1", "id2"));
        Assert.True(r2.Success);
        Assert.Equal(1, r2.ResourceData);

        timeProvider.UtcNow += TimeSpan.FromSeconds(30);

        r1 = await apiService.GetResource(new("127.0.0.1", "id1"));
        Assert.True(r1.Success);
        Assert.Equal(1, r1.ResourceData);
        r2 = await apiService.GetResource(new("127.0.0.1", "id2"));
        Assert.True(r2.Success);
        Assert.Equal(1, r2.ResourceData);
        var r3 = await apiService.GetResource(new("127.0.0.1", "id3"));
        Assert.False(r3.Success);

        timeProvider.UtcNow += throttleSettings.BanTimeOut;
        r1 = await apiService.GetResource(new("127.0.0.1", "id1"));
        Assert.True(r1.Success);
        Assert.Equal(1, r1.ResourceData);
    }

    [Fact]
    public async Task ThrottlingDoesNotBan_SameResourceDifferentIp()
    {
        var timeProvider = new ManualTimeProvider { UtcNow = DateTime.UnixEpoch };
        var resourceProvider = new InjectedResourceProvider<int>(_ => 1, (_, _) => { });

        var throttleSettings = DefaultThrottleSettings;

        var apiService = ApiServiceFactory.CreateApiService(throttleSettings, resourceProvider, timeProvider);
        var r11 = await apiService.GetResource(new("127.0.0.1", "id1"));
        Assert.True(r11.Success);
        var r12 = await apiService.GetResource(new("127.0.0.1", "id1"));
        Assert.True(r12.Success);
        var r21 = await apiService.GetResource(new("127.0.0.2", "id1"));
        Assert.True(r21.Success);
        var r22 = await apiService.GetResource(new("127.0.0.2", "id1"));
        Assert.True(r22.Success);
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
        var r1 = await apiService.GetResource(new("127.0.0.1", "id1"));
        Assert.True(r1.Success);
        var r2 = await apiService.GetResource(new("127.0.0.2", "id1"));
        Assert.True(r2.Success);
        Assert.Equal(1, resourceCallCounter);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "<Pending>")]
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

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0290:Use primary constructor", Justification = "<Pending>")]
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
