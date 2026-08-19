export type StaffRole = 'Doctor' | 'Receptionist'
export type SessionStatus = 'Scheduled' | 'BookingOpen' | 'Running' | 'Paused' | 'Closed'
export type QueueTokenStatus = 'Waiting' | 'Called' | 'InConsultation' | 'Completed' | 'Skipped' | 'Late' | 'Cancelled' | 'NoShow'
export interface QueueToken { id: string; tokenNumber: number; patientProfileId: string | null; walkInName: string | null; phoneNumber: string | null; status: QueueTokenStatus; createdAt: string; calledAt: string | null; completedAt: string | null }
export interface SessionSummary { id: string; clinicId: string; doctorId: string; name: string; bookingOpenTime: string; startTime: string; endTime: string; lastBookingTime: string; status: SessionStatus }
export interface DoctorQueueSummary { currentToken: QueueToken | null; waitingCount: number; lateCount: number; totalCount: number }
export interface DoctorSessionSummary extends SessionSummary { clinicName: string; doctorName: string; averageConsultationMinutes: number; isBookingOpen: boolean; queueSummary: DoctorQueueSummary }
export interface DoctorSessionDetails { session: DoctorSessionSummary; queue: QueueToken[] }
export interface ReceptionDashboard { clinicName: string; date: string; sessions: DoctorSessionDetails[] }
export interface PublicSessionSummary { id: string; name: string; bookingOpenTime: string; startTime: string; endTime: string; lastBookingTime: string; status: SessionStatus; isBookingOpen: boolean; currentTokenNumber: number | null; approximateQueueSize: number }
export interface PublicClinicDetails { name: string; slug: string; sessions: PublicSessionSummary[] }
export interface PublicPatientProfile { id: string; name: string; age: number }
export interface VerifiedPatientAccess { accessToken: string; expiresAt: string; profiles: PublicPatientProfile[] }
export interface PublicBookingResult { sessionId: string; tokenId: string; tokenNumber: number; wasAlreadyBooked: boolean }
export interface PublicTokenState { sessionId: string; tokenId: string; tokenNumber: number; status: QueueTokenStatus; currentTokenNumber: number | null; position: number | null; patientsAhead: number | null; estimatedWaitMinutes: number | null; isPaused: boolean; sessionStatus: SessionStatus; updatedAt: string }
