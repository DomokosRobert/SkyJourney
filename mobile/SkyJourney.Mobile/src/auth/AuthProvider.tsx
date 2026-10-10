import { useQueryClient } from '@tanstack/react-query';
import { createContext, useContext, useEffect, useMemo, useSyncExternalStore, type ReactNode } from 'react';

import { authApi } from '@/api/auth';
import { setAuthHooks } from '@/api/client';
import { createSessionManager, type SessionManager, type SessionState } from './session';
import { secureSessionStorage } from './tokenStorage';

/** Query-key roots holding data of the signed-in user; dropped on sign-out. */
export const USER_QUERY_ROOTS = ['me', 'bookings'] as const;

const SessionContext = createContext<SessionManager | null>(null);

export function AuthProvider({ children, manager }: { children: ReactNode; manager?: SessionManager }) {
  const queryClient = useQueryClient();
  const session = useMemo(
    () => manager ?? createSessionManager({ api: authApi, storage: secureSessionStorage }),
    [manager],
  );

  useEffect(() => {
    setAuthHooks({ getAccessToken: session.getAccessToken, refreshAfterUnauthorized: session.refreshAfterUnauthorized });
    void session.restore();

    let previous = session.getState().status;
    const unsubscribe = session.subscribe(() => {
      const status = session.getState().status;
      if (previous === 'signedIn' && status === 'signedOut') {
        USER_QUERY_ROOTS.forEach((root) => queryClient.removeQueries({ queryKey: [root] }));
      }
      previous = status;
    });

    return () => {
      unsubscribe();
      setAuthHooks(null);
    };
  }, [session, queryClient]);

  return <SessionContext.Provider value={session}>{children}</SessionContext.Provider>;
}

export function useSession(): SessionManager {
  const session = useContext(SessionContext);
  if (!session) {
    throw new Error('useSession must be used inside <AuthProvider>.');
  }
  return session;
}

export function useAuthState(): SessionState {
  const session = useSession();
  return useSyncExternalStore(session.subscribe, session.getState, session.getState);
}
