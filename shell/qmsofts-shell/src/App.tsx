import { useState } from "react";
import { AuthProvider, useAuth } from "./lib/AuthContext";
import { LoginScreen } from "./components/LoginScreen";
import { Launcher } from "./components/Launcher";
import { UserAdmin } from "./components/UserAdmin";

type View = "launcher" | "admin";

function Gate() {
  const { user, loading } = useAuth();
  const [view, setView] = useState<View>("launcher");

  if (loading) {
    return <div className="loading">Opening the manuscript…</div>;
  }
  if (!user) return <LoginScreen />;

  if (view === "admin") {
    return <UserAdmin onBack={() => setView("launcher")} />;
  }
  return <Launcher onOpenAdmin={() => setView("admin")} />;
}

export default function App() {
  return (
    <AuthProvider>
      <Gate />
    </AuthProvider>
  );
}