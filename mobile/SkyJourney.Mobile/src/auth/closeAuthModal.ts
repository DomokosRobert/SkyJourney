import { router } from 'expo-router';

/** Leaves the auth modal after success or cancel, returning to wherever the user came from. */
export function closeAuthModal() {
  if (router.canGoBack()) {
    router.back();
  } else {
    router.replace('/');
  }
}
