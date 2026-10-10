import type { Metadata } from "next";
import Link from "next/link";
import { EmptyState, PageHeader, SectionCard, StatCard, usagePercent } from "@/components/console/ui";
import { CopyButton } from "@/components/dashboard/copy-button";
import { KeyMask } from "@/components/dashboard/key-mask";
import { formatDate, formatRelative } from "@/lib/format";
import { accountGet, requireUser } from "@/lib/server/identity";
import type { AccountSession, AccountUser, ApiCredential, BillingOverview, Entitlement } from "@/lib/types/account";

export const metadata: Metadata = { title: "Overview" };

const quickstart = `curl -X POST https://api.halfqr.com/api/v1/qr/render \\
  -H "Authorization: Bearer $HALFQR_API_KEY" \\
  -H "Content-Type: application/json" \\
  -d '{ "contentType": "Link", "targetUrl": "https://example.com" }'`;

type ChecklistStep = { done: boolean; title: string; body: string; href: string | null };

function activeOfKind(credentials: ApiCredential[], kind: ApiCredential["kind"]) {
  return credentials.filter((item) => item.kind === kind && item.status === "active");
}

function latestUse(credentials: ApiCredential[]) {
  return credentials.map((item) => item.lastUsedAt).filter((value): value is string => Boolean(value)).sort().at(-1) ?? null;
}

function buildChecklist(user: AccountUser, entitlement: Entitlement | undefined, activeKeys: number, lastUsed: string | null): ChecklistStep[] {
  return [
    { done: true, title: "Verify your email", body: user.email, href: null },
    { done: entitlement?.isPaid ?? false, title: "Activate a paid plan", body: "Unlocks keys, tokens and plan limits.", href: "/dashboard/billing" },
    { done: activeKeys > 0, title: "Create an API key", body: "Scope it to render and read.", href: "/dashboard/api-keys" },
    { done: lastUsed !== null, title: "Make an authenticated request", body: "Bearer token or X-Api-Key header.", href: "/api-documentation" },
  ];
}

export default async function DashboardOverviewPage() {
  const user = await requireUser();
  const [credentialList, sessions, billing] = await Promise.all([
    accountGet<ApiCredential[]>("credentials"),
    accountGet<AccountSession[]>("sessions"),
    accountGet<BillingOverview>("billing"),
  ]);

  const credentials = credentialList ?? [];
  const entitlement = billing?.entitlement;
  const activeKeys = activeOfKind(credentials, "ApiKey");
  const activeTokens = activeOfKind(credentials, "AccessToken");
  const lastUsed = latestUse(credentials);
  const recent = credentials.filter((item) => item.status === "active").slice(0, 4);
  const steps = buildChecklist(user, entitlement, activeKeys.length, lastUsed);

  return (
    <>
      <PageHeader
        title={`Welcome back, ${user.displayName.split(" ")[0]}`}
        description="Credentials, plan limits and recent activity."
        actions={
          <Link className="btn btn-primary" href="/dashboard/api-keys">
            Manage API keys
          </Link>
        }
      />

      <section className="stat-grid">
        <StatCard label="API keys" value={activeKeys.length} unit={`/ ${entitlement?.maxApiKeys ?? 0}`} progress={usagePercent(activeKeys.length, entitlement?.maxApiKeys ?? 0)} />
        <StatCard label="Access tokens" value={activeTokens.length} unit={`/ ${entitlement?.maxAccessTokens ?? 0}`} progress={usagePercent(activeTokens.length, entitlement?.maxAccessTokens ?? 0)} />
        <StatCard
          label="Rate limit"
          value={(entitlement?.isPaid ? entitlement.requestsPerMinute : 30).toLocaleString("en-GB")}
          unit="req / min"
          meta={entitlement?.isPaid ? "Per key, from your plan" : "Anonymous, per IP"}
        />
        <StatCard label="Active sessions" value={sessions?.length ?? 1} meta={`Last API call ${formatRelative(lastUsed).toLowerCase()}`} />
      </section>

      <div className="overview-grid">
        <div className="stack">
          <section className="plan-banner">
            <div>
              <h2>{entitlement?.planName ?? "Free"} plan</h2>
              <p>{entitlement?.isPaid ? `Active until ${formatDate(entitlement.activeUntil)}. Early renewals stack.` : "API keys and tokens need a paid plan."}</p>
            </div>
            <Link className="btn btn-secondary" href="/dashboard/billing">
              {entitlement?.isPaid ? "Manage plan" : "Upgrade"}
            </Link>
          </section>

          <SectionCard title="Quickstart" description="Queue a render, then poll the status URL." padded>
            <div className="code-block">
              <div className="code-block-bar">
                <span>bash</span>
                <CopyButton value={quickstart} className="btn btn-ghost" />
              </div>
              <pre>{quickstart}</pre>
            </div>
          </SectionCard>

          <SectionCard
            title="Recent credentials"
            description="Active keys and tokens."
            actions={
              <Link className="btn btn-ghost" href="/dashboard/api-keys">
                View all
              </Link>
            }
          >
            {recent.length === 0 ? (
              <EmptyState title="No credentials yet">{entitlement?.isPaid ? "Create an API key to call the API from your servers." : "Activate a plan, then create an API key."}</EmptyState>
            ) : (
              <div className="table-wrap">
                <table className="table">
                  <tbody>
                    {recent.map((credential) => (
                      <tr key={credential.id}>
                        <td>
                          <div className="table-primary">
                            <strong>{credential.name}</strong>
                            <span className="table-muted">{credential.kind === "ApiKey" ? "API key" : "Access token"}</span>
                          </div>
                        </td>
                        <td>
                          <KeyMask prefix={credential.prefix} lastFour={credential.lastFour} />
                        </td>
                        <td className="table-muted">Used {formatRelative(credential.lastUsedAt).toLowerCase()}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </SectionCard>
        </div>

        <SectionCard title="Get production ready" description={`${steps.filter((step) => step.done).length} of ${steps.length} complete`} padded>
          <ul className="checklist">
            {steps.map((step) => (
              <li key={step.title} className={step.done ? "checklist-done" : undefined}>
                <span className="checklist-mark" aria-label={step.done ? "Done" : "Not done"} />
                <span>
                  <strong>{step.title}</strong>
                  <span>{step.body}</span>
                </span>
                {!step.done && step.href ? (
                  <Link className="btn btn-ghost" href={step.href}>
                    Open
                  </Link>
                ) : (
                  <span />
                )}
              </li>
            ))}
          </ul>
        </SectionCard>
      </div>
    </>
  );
}
