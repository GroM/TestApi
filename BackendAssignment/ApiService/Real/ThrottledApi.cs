using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CS.ApiService.Real
{
    public class ThrottledApi<TResource> : IApiService<TResource>
    {
        public Task<AddOrUpdateResponse> AddOrUpdateResource(AddOrUpdateRequest<TResource> request)
        {
            throw new NotImplementedException();
        }

        public Task<GetResponse<TResource>> GetResource(GetRequest request)
        {
            throw new NotImplementedException();
        }
    }
}
