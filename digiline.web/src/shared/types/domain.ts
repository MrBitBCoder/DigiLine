export type StaffRole = "Doctor" | "Receptionist" | "Admin";
export type SessionStatus =
  "Scheduled" | "BookingOpen" | "Running" | "Paused" | "Closed";
export type QueueTokenStatus =
  | "Waiting"
  | "Called"
  | "InConsultation"
  | "Completed"
  | "Skipped"
  | "Late"
  | "Cancelled"
  | "NoShow";
export interface QueueToken {
  id: string;
  tokenNumber: number;
  queueOrder?: number;
  patientProfileId: string | null;
  walkInName: string | null;
  phoneNumber: string | null;
  patientName?: string | null;
  patientAge?: number | null;
  status: QueueTokenStatus;
  createdAt: string;
  calledAt: string | null;
  lateArrivedAt?: string | null;
  completedAt: string | null;
  finalizedAt?: string | null;
}
export interface SessionSummary {
  id: string;
  clinicId: string;
  doctorId: string;
  name: string;
  bookingOpenTime: string;
  startTime: string;
  endTime: string;
  lastBookingTime: string;
  status: SessionStatus;
  bookingHasStopped?: boolean;
  bookingStoppedAt?: string | null;
  completedAt?: string | null;
}
export interface DoctorQueueSummary {
  currentToken: QueueToken | null;
  waitingCount: number;
  lateCount: number;
  totalCount: number;
  completedCount: number;
  notAttendedCount: number;
  skippedCount?: number;
  unfinishedCount?: number;
}
export interface DoctorSessionSummary extends SessionSummary {
  timeZoneId?: string;
  clinicName: string;
  doctorName: string;
  averageConsultationMinutes: number;
  isBookingOpen: boolean;
  queueSummary: DoctorQueueSummary;
}
export interface DoctorSessionDetails {
  session: DoctorSessionSummary;
  queue: QueueToken[];
  page?: number;
  pageSize?: number;
  totalCount?: number;
}
export interface ReceptionDashboard {
  timeZoneId?: string;
  clinicName: string;
  date: string;
  sessions: DoctorSessionDetails[];
  page?: number;
  pageSize?: number;
  totalCount?: number;
}
export interface PublicSessionSummary {
  id: string;
  name: string;
  bookingOpenTime: string;
  startTime: string;
  endTime: string;
  lastBookingTime: string;
  status: SessionStatus;
  isBookingOpen: boolean;
  currentTokenNumber: number | null;
  approximateQueueSize: number;
}
export interface PublicClinicDetails {
  timeZoneId?: string;
  name: string;
  slug: string;
  sessions: PublicSessionSummary[];
}
export interface PublicPatientProfile {
  id: string;
  name: string;
  age: number;
}
export interface VerifiedPatientAccess {
  accessToken: string;
  expiresAt: string;
  profiles: PublicPatientProfile[];
}
export interface PublicBookingResult {
  sessionId: string;
  tokenId: string;
  tokenNumber: number;
  wasAlreadyBooked: boolean;
}
export interface CompleteAndCallNextResult {
  completedToken: QueueToken;
  calledToken: QueueToken | null;
}
export interface PublicTokenRecoveryResult {
  sessionId: string;
  tokenId: string;
  tokenNumber: number;
}
export interface PublicTokenState {
  sessionId: string;
  tokenId: string;
  tokenNumber: number;
  status: QueueTokenStatus;
  currentTokenNumber: number | null;
  position: number | null;
  patientsAhead: number | null;
  estimatedWaitMinutes: number | null;
  isPaused: boolean;
  sessionStatus: SessionStatus;
  updatedAt: string;
}

export interface PageResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
}
