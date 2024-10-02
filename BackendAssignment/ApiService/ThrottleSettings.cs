using System;

namespace CS.ApiService;

public record ThrottleSettings
{
    /// <summary>
    /// The interval over which the MaxRequests are tracked and periodically reset.
    /// First ThrottleInterval starts from IntervalRootUtc. 
    /// </summary>
    public required TimeSpan ThrottleInterval { get; init; }

    /// <summary>
    /// The maximum number of requests allowed in the ThrottleInterval by IP address.
    /// </summary>
    public required int MaxRequestsPerIp { get; init; }

    /// <summary>
    /// The amount of time an IP address is banned for after exceeding the MaxRequests in single ThrottleInterval.
    /// </summary>
    public required TimeSpan BanTimeOut { get; init; }

    /// <summary>
    /// Time from which the first ThrottleInterval starts.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "<Base Code>")]
    public DateTime IntervalRootUtc => DateTime.UnixEpoch;
}
