using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DigiLine.Application.Queue
{
    public interface ICreateWalkInTokenService
    {
        Task<int> CreateAsync(
            Guid sessionId,
            string walkInName,
            string? phoneNumber,
            CancellationToken cancellationToken = default,
            int? age = null);
    }
}
