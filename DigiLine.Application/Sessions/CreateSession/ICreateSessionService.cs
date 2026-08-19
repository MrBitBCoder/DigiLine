using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DigiLine.Application.Sessions
{
    public interface ICreateSessionService
    {
        Task<Guid> CreateAsync(
            CreateSessionRequest request,
            CancellationToken cancellationToken = default);
    }
}
