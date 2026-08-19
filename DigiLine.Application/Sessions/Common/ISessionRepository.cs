using DigiLine.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DigiLine.Application.Sessions
{
    public interface ISessionRepository
    {
        Task AddAsync(
            Session session,
            CancellationToken cancellationToken = default);

        Task SaveChangesAsync(
            CancellationToken cancellationToken = default);

        Task<Session?> GetByIdAsync(
            Guid sessionId,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<DoctorSessionRecord>> ListForDoctorAsync(
            Guid clinicId,
            Guid doctorId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        Task<DoctorSessionRecord?> GetForDoctorAsync(
            Guid sessionId,
            Guid clinicId,
            Guid doctorId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        Task<IReadOnlyList<DoctorSessionRecord>> ListForClinicAsync(
            Guid clinicId,
            DateTime from,
            DateTime to,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        Task<DoctorSessionRecord?> GetForClinicAsync(
            Guid sessionId,
            Guid clinicId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
