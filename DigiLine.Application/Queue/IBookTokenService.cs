using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DigiLine.Application.Queue
{
    public interface IBookTokenService
    {
        Task<int> BookAsync(
            Guid sessionId,
            Guid patientProfileId,
            CancellationToken cancellationToken = default);
    }
}
