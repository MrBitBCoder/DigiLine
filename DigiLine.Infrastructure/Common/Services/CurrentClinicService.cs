using DigiLine.Application.Common.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DigiLine.Infrastructure.Common.Services
{

    public class CurrentClinicService : ICurrentClinicService
    {
        public Guid ClinicId { get; private set; }

        public void SetClinic(Guid clinicId)
        {
            ClinicId = clinicId;
        }
    }
}
