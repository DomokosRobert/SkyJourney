import { EmptyState, Screen } from '@/components';

export default function TripsScreen() {
  return (
    <Screen scroll={false}>
      <EmptyState icon="airplane-outline" title="No trips yet" message="Bookings you make will appear here." />
    </Screen>
  );
}
