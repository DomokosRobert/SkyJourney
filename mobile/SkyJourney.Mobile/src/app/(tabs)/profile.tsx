import { EmptyState, Screen } from '@/components';

export default function ProfileScreen() {
  return (
    <Screen scroll={false}>
      <EmptyState icon="person-circle-outline" title="Your profile" message="Sign in to manage your account and trips." />
    </Screen>
  );
}
