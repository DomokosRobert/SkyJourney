import type { BookingStatus } from '@/api/bookings';
import { colors } from '@/theme';

type Presentation = { label: string; color: string; background: string };

const STATUS: Record<BookingStatus, Presentation> = {
  PendingPayment: { label: 'Awaiting payment', color: colors.warning, background: colors.warningSoft },
  Confirmed: { label: 'Confirmed', color: colors.success, background: colors.successSoft },
  Cancelled: { label: 'Cancelled', color: colors.danger, background: colors.dangerSoft },
  Expired: { label: 'Expired', color: colors.textMuted, background: colors.border },
};

/** How a booking status is shown; a confirmed trip whose first flight has left reads as completed. */
export function statusPresentation(status: BookingStatus, isUpcoming: boolean): Presentation {
  if (status === 'Confirmed' && !isUpcoming) {
    return { label: 'Completed', color: colors.textMuted, background: colors.border };
  }
  return STATUS[status];
}
