import { AuthProvider, useAuth } from "./lib/AuthContext";
import { LoginScreen } from "./components/LoginScreen";
import { Launcher } from "./components/Launcher";

function Gate() {
  const { user, loading } = useAuth();

  if (loading) {
    return <div className="loading">Opening the manuscript…</div>;
  }
  return user ? <Launcher /> : <LoginScreen />;
}

export default function App() {
  return (
    <AuthProvider>
      <Gate />
    </AuthProvider>
  );
}
