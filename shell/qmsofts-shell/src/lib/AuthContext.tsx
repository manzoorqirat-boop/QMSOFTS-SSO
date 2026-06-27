import {
  createContext,
  useContext,
  useState,
  useEffect,
  useCallback,
  useRef,
  type ReactNode,
} from "react";
import type { TokenResponse, UserProfile } from "../types";
import * as api from "./identityApi";

const REFRESH_KEY = "qms.refresh";

interface AuthState {
  user: UserProfile | null;
  accessToken: string | null;
  loading: boolean;
  error: string | null;
  signIn: (email: string, password: string) => Promise<void>;
  signOut: () => void;
}

const AuthContext = createContext<AuthState | undefined>(undefined);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<UserProfile | null>(null);
  const [accessToken, setAccessToken] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const refreshTimer = useRef<number | null>(null);

  const applyTokens = useCallback((t: TokenResponse) => {
    setUser(t.user);
    setAccessToken(t.accessToken);
    // Refresh token is the only thing persisted; access token stays in memory.
    localStorage.setItem(REFRESH_KEY, t.refreshToken);
    scheduleRefresh(t.expiresAt, t.refreshToken);
  }, []);

  const clear = useCallback(() => {
    setUser(null);
    setAccessToken(null);
    localStorage.removeItem(REFRESH_KEY);
    if (refreshTimer.current) window.clearTimeout(refreshTimer.current);
  }, []);

  // Schedule a silent refresh ~60s before the access token expires.
  const scheduleRefresh = useCallback(
    (expiresAt: string, token: string) => {
      if (refreshTimer.current) window.clearTimeout(refreshTimer.current);
      const ms = new Date(expiresAt).getTime() - Date.now() - 60_000;
      refreshTimer.current = window.setTimeout(
        async () => {
          try {
            const t = await api.refresh(token);
            applyTokens(t);
          } catch {
            clear();
          }
        },
        Math.max(ms, 5_000)
      );
    },
    [applyTokens, clear]
  );

  // On load, try to resume a session from the stored refresh token.
  useEffect(() => {
    const stored = localStorage.getItem(REFRESH_KEY);
    if (!stored) {
      setLoading(false);
      return;
    }
    api
      .refresh(stored)
      .then(applyTokens)
      .catch(() => clear())
      .finally(() => setLoading(false));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const signIn = useCallback(
    async (email: string, password: string) => {
      setError(null);
      try {
        const t = await api.login(email, password);
        applyTokens(t);
      } catch (e) {
        setError(e instanceof Error ? e.message : "Sign in failed.");
        throw e;
      }
    },
    [applyTokens]
  );

  const signOut = useCallback(() => clear(), [clear]);

  return (
    <AuthContext.Provider
      value={{ user, accessToken, loading, error, signIn, signOut }}
    >
      {children}
    </AuthContext.Provider>
  );
}

// eslint-disable-next-line react-refresh/only-export-components
export function useAuth() {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error("useAuth must be used within AuthProvider");
  return ctx;
}
