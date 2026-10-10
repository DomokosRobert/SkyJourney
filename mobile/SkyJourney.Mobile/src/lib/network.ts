import { onlineManager } from '@tanstack/react-query';
import { useSyncExternalStore } from 'react';

/** Online status as tracked by TanStack Query's online manager (fed by NetInfo). */
export function useIsOnline(): boolean {
  return useSyncExternalStore(
    (onChange) => onlineManager.subscribe(onChange),
    () => onlineManager.isOnline(),
  );
}
