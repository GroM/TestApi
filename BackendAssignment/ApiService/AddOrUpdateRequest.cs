namespace CS.ApiService;

public record AddOrUpdateRequest<T>(string IpAddress, string ResourceId, T Resource);
