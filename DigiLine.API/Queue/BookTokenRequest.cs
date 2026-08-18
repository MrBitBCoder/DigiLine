namespace DigiLine.API.Queue
{
    public class BookTokenRequest
    {
        public Guid SessionId { get; set; }

        public Guid PatientProfileId { get; set; }
    }
}
