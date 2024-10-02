namespace CS.ApiService;

public record GetRequest(string IpAddress, string ResourceId) : ResourceRequestBase(IpAddress, ResourceId);
