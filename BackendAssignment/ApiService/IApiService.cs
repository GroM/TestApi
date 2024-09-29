using System.Threading.Tasks;

namespace CS.ApiService;

public interface IApiService<TResource>
{
    Task<GetResponse<TResource>> GetResource(GetRequest request);

    Task<AddOrUpdateResponse> AddOrUpdateResource(AddOrUpdateRequest<TResource> request);
}
