import { useState, useEffect, useCallback } from "react";
import { useAuth } from "../lib/AuthContext";
import * as api from "../lib/identityApi";
import type { AuditEvent, ChangeRecord } from "../lib/identityApi";
import { AUDIT_EVENT_LABELS } from "../lib/constants";
import { BrandHeader } from "./BrandHeader";

type Tab = "events" | "changes";

export function AuditViewer({ onBack }: { onBack: () => void }) {
  const { accessToken } = useAuth();
  const [tab, setTab] = useState<Tab>("events");
  const [events, setEvents] = useState<AuditEvent[]>([]);
  const [changes, setChanges] = useState<ChangeRecord[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    if (!accessToken) return;
    setLoading(true);
    setError(null);
    try {
      if (tab === "events") setEvents(await api.getAuditEvents(accessToken));
      else setChanges(await api.getChangeHistory(accessToken));
    } catch (e) {
      setError(e instanceof Error ? e.message : "Failed to load audit data.");
    } finally {
      setLoading(false);
    }
  }, [accessToken, tab]);

  useEffect(() => { load(); }, [load]);

  const fmt = (iso: string) => new Date(iso).toLocaleString();

  return (
    <div className="frame">
      <div className="page">
        <BrandHeader subtitle="Audit Trail" />
        <div className="launcher-head">
          <button className="signout" onClick={onBack}>&larr; Back</button>
          <div className="head-actions">
            <button className={`btn-small ${tab === "events" ? "tab-on" : ""}`}
              onClick={() => setTab("events")}>Auth Events</button>
            <button className={`btn-small ${tab === "changes" ? "tab-on" : ""}`}
              onClick={() => setTab("changes")}>Change History</button>
          </div>
        </div>

        {error && <div className="error-note">{error}</div>}

        {loading ? (
          <p className="empty-note">Loading&hellip;</p>
        ) : tab === "events" ? (
          <div className="audit-list">
            {events.length === 0 ? <p className="empty-note">No events.</p> :
              events.map((e) => (
                <div key={e.id} className="audit-row">
                  <div className="audit-main">
                    <span className="audit-event">
                      {AUDIT_EVENT_LABELS[e.eventType] ?? `Event ${e.eventType}`}
                    </span>
                    <span className="audit-who">{e.identifier ?? "—"}</span>
                  </div>
                  <div className="audit-meta">
                    {fmt(e.occurredAt)}
                    {e.ipAddress ? ` · ${e.ipAddress}` : ""}
                    {e.appKey ? ` · ${e.appKey}` : ""}
                  </div>
                  {e.detail && <div className="audit-detail">{e.detail}</div>}
                </div>
              ))}
          </div>
        ) : (
          <div className="audit-list">
            {changes.length === 0 ? <p className="empty-note">No changes.</p> :
              changes.map((c) => (
                <div key={c.id} className="audit-row">
                  <div className="audit-main">
                    <span className="audit-event">{c.action} {c.entityType}</span>
                    <span className="audit-who">{c.entityLabel}</span>
                  </div>
                  <div className="audit-meta">
                    {fmt(c.timestamp)} · by {c.changedBy}
                    {c.ipAddress ? ` · ${c.ipAddress}` : ""}
                  </div>
                  {c.changedFields && (
                    <div className="audit-detail">Changed: {c.changedFields}</div>
                  )}
                </div>
              ))}
          </div>
        )}
      </div>
    </div>
  );
}