using CS.ApiService;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace CS.Tests;

public class EdgeCasesUnitTests
{
    [Fact]
    public async Task AddOrUpdate_Works()
    {
        var timeProvider = new ManualTimeProvider { UtcNow = DateTime.UnixEpoch };

        var resourceCallCounter = 0;

        var resourceProvider = new InjectedResourceProvider<int>(_ => 1, (_, _) => { });

        var throttleSettings = DefaultThrottleSettings;

        var apiService = ApiServiceFactory.CreateApiService(throttleSettings, resourceProvider, timeProvider);
        var r1 = await apiService.AddOrUpdateResource(new("127.0.0.2", "id1", 2));
        Assert.True(r1.Success);
    }
    
    [Fact]
    public async Task CachingWorks_AfterUpdate()
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
        var r3 = await apiService.AddOrUpdateResource(new("127.0.0.2", "id1", 2));
        Assert.True(r3.Success);
        var r4 = await apiService.GetResource(new("127.0.0.1", "id1"));
        Assert.True(r4.Success);
        var r5 = await apiService.GetResource(new("127.0.0.3", "id1"));
        Assert.True(r5.Success);

        Assert.Equal(2, resourceCallCounter);
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
