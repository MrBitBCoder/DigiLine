import { useEffect, useRef } from "react";

interface BookingWindow {
  bookingOpenTime: string;
  endTime: string;
  lastBookingTime: string;
  isBookingOpen: boolean;
  status: string;
}

export function useBookingCutoffRefresh(sessions: BookingWindow[], refresh: () => void) {
  const refreshRef = useRef(refresh);
  useEffect(() => { refreshRef.current = refresh; }, [refresh]);
  const windowKey = sessions.map(session => [session.bookingOpenTime, session.lastBookingTime, session.endTime, session.isBookingOpen, session.status].join("|")).join(";");

  useEffect(() => {
    const now = Date.now();
    const windows = sessions.flatMap(session => {
      if (session.status === "Closed") return [];
      const cutoff = Math.min(Date.parse(session.endTime), Date.parse(session.lastBookingTime));
      const opening = Date.parse(session.bookingOpenTime);
      if (session.isBookingOpen) return cutoff > now ? [cutoff] : [];
      return opening > now && opening < cutoff ? [opening] : [];
    });
    if (!windows.length) return;
    const delay = Math.min(...windows) - now;
    const timer = window.setTimeout(() => refreshRef.current(), Math.min(delay, 2_147_483_647));
    return () => window.clearTimeout(timer);
  }, [windowKey, sessions]);
}
