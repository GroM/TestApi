using System;

namespace CS.ApiService;

public static class ApiServiceFactory
{
    public static IApiService<T> CreateApiService<T>(ThrottleSettings throttleSettings,
                                                     IResourceProvider<T> resourceProvider,
                                                     ITimeProvider timeProvider)
    {
        // add your constructor/creation logic here
        throw new NotImplementedException();
    }
}
