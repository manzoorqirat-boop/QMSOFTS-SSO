import { useState } from "react";
import { AuthProvider, useAuth } from "./lib/AuthContext";
import { LoginScreen } from "./components/LoginScreen";
import { Launcher } from "./components/Launcher";
import { UserAdmin } from "./components/UserAdmin";
import { SecuritySettings } from "./components/SecuritySettings";
import { AuditViewer } from "./components/AuditViewer";
import { ChangePassword } from "./components/ChangePassword";

type View = "launcher" | "admin" | "settings" | "audit" | "password";

function Gate() {
  const { user, loading, mustChangePassword, clearMustChange } = useAuth();
  const [view, setView] = useState<View>("launcher");

  if (loading) return <div className="loading">Opening the manuscript…</div>;
  if (!user) return <LoginScreen />;

  // Forced password change blocks everything until done.
  if (mustChangePassword) {
    return <ChangePassword forced onDone={clearMustChange} />;
  }

  switch (view) {
    case "admin":
      return <UserAdmin onBack={() => setView("launcher")} />;
    case "settings":
      return <SecuritySettings onBack={() => setView("launcher")} />;
    case "audit":
      return <AuditViewer onBack={() => setView("launcher")} />;
    case "password":
      return (
        <ChangePassword
          onDone={() => setView("launcher")}
          onCancel={() => setView("launcher")}
        />
      );
    default:
      return (
        <Launcher
          onOpenAdmin={() => setView("admin")}
          onOpenSettings={() => setView("settings")}
          onOpenAudit={() => setView("audit")}
          onChangePassword={() => setView("password")}
        />
      );
  }
}

export default function App() {
  return (
    <AuthProvider>
      <Gate />
    </AuthProvider>
  );
}