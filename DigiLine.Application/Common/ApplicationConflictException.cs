namespace DigiLine.Application.Common;

/// <summary>Represents an expected request conflict whose message is safe to return to the client.</summary>
public sealed class ApplicationConflictException(string message) : Exception(message);
