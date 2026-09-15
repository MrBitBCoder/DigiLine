-- Read-only: historical Closed sessions with unresolved tokens. No outcomes are inferred.
SELECT s.ClinicId, s.Id AS SessionId, s.Name, s.CompletedAt,
       q.Status AS TokenStatus, COUNT_BIG(*) AS TokenCount
FROM Sessions AS s
JOIN QueueTokens AS q ON q.SessionId = s.Id
WHERE s.Status = 5 AND q.Status IN (1, 2, 3, 5, 6)
GROUP BY s.ClinicId, s.Id, s.Name, s.CompletedAt, q.Status
ORDER BY s.ClinicId, s.Id, q.Status;
