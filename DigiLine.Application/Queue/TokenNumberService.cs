using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DigiLine.Application.Queue
{
    public class TokenNumberService : ITokenNumberService
    {

        private readonly IQueueTokenRepository _queueTokenRepository;

        public TokenNumberService(
            IQueueTokenRepository queueTokenRepository)
        {
            _queueTokenRepository = queueTokenRepository;
        }

        public async Task<int> GetNextTokenNumberAsync(
           Guid sessionId,
           CancellationToken cancellationToken = default)
        {
            var maximumTokenNumber =
                await _queueTokenRepository.AllocateNextTokenNumberAsync(
                    sessionId,
                    cancellationToken);

            return maximumTokenNumber
                    ?? throw new InvalidOperationException(
                        "Session was not found.");
            //return (maximumTokenNumber ?? 0) + 1;
        }
        
    }
}
