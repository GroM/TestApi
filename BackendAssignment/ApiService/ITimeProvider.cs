using System;

namespace CS.ApiService;

public interface ITimeProvider
{
    public DateTime UtcNow { get; }
}