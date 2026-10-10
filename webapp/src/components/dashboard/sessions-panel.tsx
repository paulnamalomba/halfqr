"use client";

import { useState } from "react";
import { SectionCard } from "@/components/console/ui";
import { accountRequest } from "@/lib/client/account-api";
import { formatDate, formatRelative } from "@/lib/format";
import type { AccountSession } from "@/lib/types/account";
import { useToast } from "./toast";

const browsers: Array<[RegExp, string]> = [
  [/Edg\//, "Edge"],
  [/Chrome\//, "Chrome"],
  [/Firefox\//, "Firefox"],
  [/Safari\//, "Safari"],
];

const systems: Array<[RegExp, string]> = [
  [/iPhone|iPad/, "iOS"],
  [/Android/, "Android"],
  [/Mac OS X/, "macOS"],
  [/Windows/, "Windows"],
  [/Linux/, "Linux"],
];

function firstMatch(value: string, patterns: Array<[RegExp, string]>, fallback: string) {
  return patterns.find(([pattern]) => pattern.test(value))?.[1] ?? fallback;
}

// Short user-agent summary; enough to tell devices apart.
export function describeDevice(userAgent: string | null) {
  return userAgent ? `${firstMatch(userAgent, browsers, "Browser")} on ${firstMatch(userAgent, systems, "unknown OS")}` : "Unknown device";
}

export function SessionsPanel({ initialSessions }: { initialSessions: AccountSession[] }) {
  const [sessions, setSessions] = useState(initialSessions);
  const [busy, setBusy] = useState(false);
  const { toast, showToast } = useToast();
  const others = sessions.filter((session) => !session.current);

  async function revoke(ids: string[]) {
    setBusy(true);

    try {
      await Promise.all(ids.map((id) => accountRequest(`sessions/${id}`, { method: "DELETE" })));
      setSessions((items) => items.filter((item) => !ids.includes(item.id)));
      showToast(ids.length === 1 ? "Session signed out" : "Other sessions signed out");
    } finally {
      setBusy(false);
    }
  }

  return (
    <SectionCard
      title="Active sessions"
      description="Sessions last seven days unless you sign out."
      actions={
        others.length > 0 ? (
          <button type="button" className="btn btn-secondary" disabled={busy} onClick={() => revoke(others.map((session) => session.id))}>
            Sign out others
          </button>
        ) : null
      }
    >
      <div className="table-wrap">
        <table className="table">
          <thead>
            <tr>
              <th>Device</th>
              <th>IP address</th>
              <th>Last active</th>
              <th>Expires</th>
              <th aria-label="Actions" />
            </tr>
          </thead>
          <tbody>
            {sessions.map((session) => (
              <tr key={session.id}>
                <td>
                  <div className="table-primary">
                    <strong>
                      {describeDevice(session.userAgent)} {session.current ? <span className="badge badge-success">This device</span> : null}
                    </strong>
                    <span className="table-muted">Signed in {formatDate(session.createdAt)}</span>
                  </div>
                </td>
                <td className="mono table-muted">{session.ipAddress ?? "—"}</td>
                <td className="table-muted">{session.current ? "Now" : formatRelative(session.lastSeenAt)}</td>
                <td className="table-muted">{formatDate(session.expiresAt)}</td>
                <td>
                  {!session.current ? (
                    <div className="table-actions">
                      <button type="button" className="btn btn-danger-quiet" disabled={busy} onClick={() => revoke([session.id])}>
                        Sign out
                      </button>
                    </div>
                  ) : null}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      {toast}
    </SectionCard>
  );
}
