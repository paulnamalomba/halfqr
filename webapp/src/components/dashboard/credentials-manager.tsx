"use client";

import Link from "next/link";
import { FormEvent, useMemo, useState } from "react";
import { ChipList, EmptyState, Notice, SectionCard } from "@/components/console/ui";
import { AccountApiError, accountRequest } from "@/lib/client/account-api";
import { formatDate, formatRelative } from "@/lib/format";
import type { ApiCredential, CredentialCreated, CredentialKind, CredentialOptions, Entitlement } from "@/lib/types/account";
import { CopyButton } from "./copy-button";
import { Dialog } from "./dialog";
import { KeyMask } from "./key-mask";
import { CredentialStatusBadge } from "./status-badge";
import { useToast } from "./toast";

const scopeDescriptions: Record<string, string> = {
  "qr:render": "Drafts and render jobs",
  "qr:read": "Job status and downloads",
};

const kindCopy: Record<CredentialKind, { title: string; noun: string; plural: string; create: string; empty: string }> = {
  ApiKey: {
    title: "API keys",
    noun: "API key",
    plural: "API keys",
    create: "Create API key",
    empty: "Long-lived secrets for servers and CI. Give each service its own key.",
  },
  AccessToken: {
    title: "Access tokens",
    noun: "access token",
    plural: "access tokens",
    create: "Generate token",
    empty: "Short-lived secrets that always expire. Use them for scripts and testing.",
  },
};

type PendingAction = { type: "revoke" | "rotate"; credential: ApiCredential } | null;

// Marks one credential as revoked without touching the others.
function markRevoked(items: ApiCredential[], id: string): ApiCredential[] {
  return items.map((item) => (item.id === id ? { ...item, status: "revoked", revokedAt: new Date().toISOString() } : item));
}

function planLimit(entitlement: Entitlement | null, kind: CredentialKind) {
  return kind === "ApiKey" ? entitlement?.maxApiKeys ?? 0 : entitlement?.maxAccessTokens ?? 0;
}

function errorText(error: unknown) {
  return error instanceof AccountApiError ? error.message : "Something went wrong. Try again.";
}

export function CredentialsManager({
  kind,
  initialCredentials,
  options,
  entitlement,
}: {
  kind: CredentialKind;
  initialCredentials: ApiCredential[];
  options: CredentialOptions;
  entitlement: Entitlement | null;
}) {
  const copy = kindCopy[kind];
  const [credentials, setCredentials] = useState(initialCredentials.filter((credential) => credential.kind === kind));
  const [createOpen, setCreateOpen] = useState(false);
  const [revealed, setRevealed] = useState<CredentialCreated | null>(null);
  const [pending, setPending] = useState<PendingAction>(null);
  const [actionError, setActionError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [showInactive, setShowInactive] = useState(false);
  const { toast, showToast } = useToast();

  const isPaid = entitlement?.isPaid ?? false;
  const limit = planLimit(entitlement, kind);
  const active = credentials.filter((credential) => credential.status === "active");
  const inactiveCount = credentials.length - active.length;
  const visible = showInactive ? credentials : active;

  async function confirmPending() {
    if (!pending) {
      return;
    }

    setBusy(true);
    setActionError(null);

    try {
      if (pending.type === "revoke") {
        await accountRequest(`credentials/${pending.credential.id}`, { method: "DELETE" });
        setCredentials((items) => markRevoked(items, pending.credential.id));
        showToast(`${pending.credential.name} revoked`);
      } else {
        const created = await accountRequest<CredentialCreated>(`credentials/${pending.credential.id}/rotate`, { method: "POST" });
        setCredentials((items) => [created.credential, ...markRevoked(items, pending.credential.id)]);
        setRevealed(created);
      }

      setPending(null);
    } catch (error) {
      setActionError(errorText(error));
    } finally {
      setBusy(false);
    }
  }

  return (
    <>
      {!isPaid ? (
        <div className="paywall">
          <div>
            <h2>{copy.title} need a paid plan</h2>
            <p>The web builder stays free. Pay with Airtel Money or TNM Mpamba to issue {copy.plural}.</p>
          </div>
          <Link className="btn btn-primary" href="/dashboard/billing">
            View plans
          </Link>
        </div>
      ) : null}

      <SectionCard
        title={isPaid ? `${copy.title} · ${active.length} of ${limit}` : copy.title}
        description="Secrets are shown once. HalfQR stores only a hash."
        actions={
          <>
            {inactiveCount > 0 ? (
              <button type="button" className="btn btn-ghost" onClick={() => setShowInactive((value) => !value)}>
                {showInactive ? "Hide inactive" : `Show inactive (${inactiveCount})`}
              </button>
            ) : null}
            <button type="button" className="btn btn-primary" onClick={() => setCreateOpen(true)} disabled={!isPaid || active.length >= limit}>
              {copy.create}
            </button>
          </>
        }
      >
        {visible.length === 0 ? (
          <EmptyState title={`No active ${copy.plural}`}>{copy.empty}</EmptyState>
        ) : (
          <div className="table-wrap">
            <table className="table">
              <thead>
                <tr>
                  <th>Name</th>
                  <th>Secret</th>
                  <th>Scopes</th>
                  <th>Last used</th>
                  <th>Expires</th>
                  <th>Status</th>
                  <th aria-label="Actions" />
                </tr>
              </thead>
              <tbody>
                {visible.map((credential) => (
                  <tr key={credential.id} className={credential.status === "active" ? undefined : "table-row-inactive"}>
                    <td>
                      <div className="table-primary">
                        <strong>{credential.name}</strong>
                        <span className="table-muted">Created {formatDate(credential.createdAt)}</span>
                      </div>
                    </td>
                    <td>
                      <KeyMask prefix={credential.prefix} lastFour={credential.lastFour} />
                    </td>
                    <td>
                      <ChipList items={credential.scopes} />
                    </td>
                    <td className="table-muted">{formatRelative(credential.lastUsedAt)}</td>
                    <td className="table-muted">{credential.expiresAt ? formatDate(credential.expiresAt) : "Never"}</td>
                    <td>
                      <CredentialStatusBadge status={credential.status} />
                    </td>
                    <td>
                      {credential.status === "active" ? (
                        <div className="table-actions">
                          <button type="button" className="btn btn-ghost" onClick={() => setPending({ type: "rotate", credential })}>
                            Rotate
                          </button>
                          <button type="button" className="btn btn-danger-quiet" onClick={() => setPending({ type: "revoke", credential })}>
                            Revoke
                          </button>
                        </div>
                      ) : null}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </SectionCard>

      {createOpen ? (
        <CreateCredentialDialog
          kind={kind}
          options={options}
          onClose={() => setCreateOpen(false)}
          onCreated={(created) => {
            setCredentials((items) => [created.credential, ...items]);
            setCreateOpen(false);
            setRevealed(created);
          }}
        />
      ) : null}

      {revealed ? (
        <Dialog
          title={`Copy your ${copy.noun}`}
          description="Shown once. Store it in your secret manager now."
          dismissible={false}
          onClose={() => setRevealed(null)}
          footer={
            <button
              type="button"
              className="btn btn-primary"
              onClick={() => {
                showToast(`${revealed.credential.name} is ready`);
                setRevealed(null);
              }}
            >
              I have stored it
            </button>
          }
        >
          <div className="secret-box">
            <code>{revealed.secret}</code>
            <CopyButton value={revealed.secret} />
          </div>
          <Notice tone="warning">Never commit this secret or ship it in browser code.</Notice>
        </Dialog>
      ) : null}

      {pending ? (
        <Dialog
          title={pending.type === "revoke" ? `Revoke ${pending.credential.name}?` : `Rotate ${pending.credential.name}?`}
          description={
            pending.type === "revoke"
              ? "Requests using it are rejected within a minute. This cannot be undone."
              : "A new secret is issued with the same settings. The old one stops within a minute."
          }
          onClose={() => {
            setPending(null);
            setActionError(null);
          }}
          footer={
            <>
              <button type="button" className="btn btn-secondary" onClick={() => setPending(null)} disabled={busy}>
                Cancel
              </button>
              <button type="button" className={pending.type === "revoke" ? "btn btn-danger" : "btn btn-primary"} onClick={confirmPending} disabled={busy}>
                {busy ? "Working…" : pending.type === "revoke" ? "Revoke" : "Rotate"}
              </button>
            </>
          }
        >
          <KeyMask prefix={pending.credential.prefix} lastFour={pending.credential.lastFour} />
          {actionError ? <Notice tone="danger">{actionError}</Notice> : null}
        </Dialog>
      ) : null}

      {toast}
    </>
  );
}

function CreateCredentialDialog({
  kind,
  options,
  onClose,
  onCreated,
}: {
  kind: CredentialKind;
  options: CredentialOptions;
  onClose: () => void;
  onCreated: (created: CredentialCreated) => void;
}) {
  const copy = kindCopy[kind];
  const lifetimes = kind === "ApiKey" ? options.apiKeyLifetimesDays : options.accessTokenLifetimesDays;
  const [name, setName] = useState("");
  const [scopes, setScopes] = useState<string[]>(kind === "ApiKey" ? options.scopes : ["qr:read"]);
  const [expiry, setExpiry] = useState<string>(kind === "ApiKey" ? "never" : "30");
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [submitting, setSubmitting] = useState(false);

  const expiryOptions = useMemo(
    () => [...(kind === "ApiKey" ? [{ value: "never", label: "Never" }] : []), ...lifetimes.map((days) => ({ value: String(days), label: days === 1 ? "1 day" : `${days} days` }))],
    [kind, lifetimes],
  );

  function toggleScope(scope: string, checked: boolean) {
    setScopes((current) => (checked ? [...current, scope] : current.filter((item) => item !== scope)));
  }

  async function submit(event: FormEvent) {
    event.preventDefault();
    setSubmitting(true);
    setErrors({});

    try {
      onCreated(
        await accountRequest<CredentialCreated>("credentials", {
          method: "POST",
          body: { kind, name: name.trim(), scopes, expiresInDays: expiry === "never" ? null : Number(expiry) },
        }),
      );
    } catch (error) {
      setErrors(
        error instanceof AccountApiError
          ? { name: error.fieldMessage("name") ?? "", scopes: error.fieldMessage("scopes") ?? "", form: error.problem?.errors ? "" : error.message }
          : { form: errorText(error) },
      );
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <Dialog
      title={copy.create}
      description={kind === "ApiKey" ? "For a server or CI pipeline." : "Expires automatically."}
      onClose={onClose}
      footer={
        <>
          <button type="button" className="btn btn-secondary" onClick={onClose}>
            Cancel
          </button>
          <button type="submit" form="create-credential" className="btn btn-primary" disabled={submitting || name.trim().length === 0 || scopes.length === 0}>
            {submitting ? "Creating…" : copy.create}
          </button>
        </>
      }
    >
      <form id="create-credential" className="form-stack" onSubmit={submit}>
        <div className="console-field">
          <label className="console-label" htmlFor="credential-name">
            Name
          </label>
          <input
            id="credential-name"
            className="console-input"
            maxLength={64}
            placeholder={kind === "ApiKey" ? "Production checkout service" : "Local testing"}
            value={name}
            onChange={(event) => setName(event.target.value)}
          />
          {errors.name ? <span className="console-field-error">{errors.name}</span> : null}
        </div>

        <fieldset className="console-fieldset">
          <legend className="console-label">Scopes</legend>
          {options.scopes.map((scope) => (
            <label key={scope} className="choice-row">
              <input type="checkbox" checked={scopes.includes(scope)} onChange={(event) => toggleScope(scope, event.target.checked)} />
              <span className="mono">{scope}</span>
              <span className="choice-row-hint">{scopeDescriptions[scope] ?? ""}</span>
            </label>
          ))}
          {errors.scopes ? <span className="console-field-error">{errors.scopes}</span> : null}
        </fieldset>

        <div className="console-field">
          <label className="console-label" htmlFor="credential-expiry">
            Expires
          </label>
          <select id="credential-expiry" className="console-select" value={expiry} onChange={(event) => setExpiry(event.target.value)}>
            {expiryOptions.map((option) => (
              <option key={option.value} value={option.value}>
                {option.label}
              </option>
            ))}
          </select>
        </div>

        {errors.form ? <Notice tone="danger">{errors.form}</Notice> : null}
      </form>
    </Dialog>
  );
}
