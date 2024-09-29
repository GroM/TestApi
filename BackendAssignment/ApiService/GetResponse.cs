namespace CS.ApiService;

public record GetResponse<T>(bool Success, T ResourceData, ErrorType? ErrorType);
