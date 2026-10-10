"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { ReactNode, useEffect, useState } from "react";
import { BrandLockup } from "@/components/console/ui";
import { formatDate, initials } from "@/lib/format";
import type { AccountUser, Entitlement } from "@/lib/types/account";

type NavItem = { href: string; label: string };

const navigation: Array<{ label: string; items: NavItem[] }> = [
  { label: "Workspace", items: [{ href: "/dashboard", label: "Overview" }] },
  {
    label: "Developers",
    items: [
      { href: "/dashboard/api-keys", label: "API keys" },
      { href: "/dashboard/access-tokens", label: "Access tokens" },
    ],
  },
  {
    label: "Account",
    items: [
      { href: "/dashboard/billing", label: "Plan & billing" },
      { href: "/dashboard/security", label: "Sessions & security" },
    ],
  },
  {
    label: "Resources",
    items: [
      { href: "/api-documentation", label: "API documentation" },
      { href: "/changelog", label: "Changelog" },
      { href: "/", label: "QR builder" },
    ],
  },
];

const pageTitles: Record<string, string> = Object.fromEntries(navigation.flatMap((group) => group.items.map((item) => [item.href, item.label])));

// Overview only matches exactly; other dashboard links also match their sub-routes.
export function isNavItemActive(pathname: string, href: string) {
  if (!href.startsWith("/dashboard")) {
    return false;
  }

  return href === "/dashboard" ? pathname === href : pathname === href || pathname.startsWith(`${href}/`);
}

export function DashboardShell({ user, entitlement, children }: { user: AccountUser; entitlement: Entitlement | null; children: ReactNode }) {
  const pathname = usePathname();
  const [menuOpen, setMenuOpen] = useState(false);

  useEffect(() => setMenuOpen(false), [pathname]);

  return (
    <div className="dash console">
      <aside className={menuOpen ? "dash-sidebar dash-sidebar-open" : "dash-sidebar"} aria-label="Dashboard navigation">
        <Link href="/dashboard">
          <BrandLockup label="Console" />
        </Link>

        <nav className="dash-nav">
          {navigation.map((group) => (
            <div key={group.label} className="dash-nav-group">
              <p className="dash-nav-label">{group.label}</p>
              {group.items.map(({ href, label }) => {
                const active = isNavItemActive(pathname, href);

                return (
                  <Link key={href} href={href} className={active ? "dash-nav-link dash-nav-link-active" : "dash-nav-link"} aria-current={active ? "page" : undefined}>
                    {label}
                  </Link>
                );
              })}
            </div>
          ))}
        </nav>

        <div className="dash-plan">
          <div className="dash-plan-row">
            <strong>{entitlement?.planName ?? "Free"} plan</strong>
            <span className={entitlement?.isPaid ? "badge badge-success" : "badge badge-muted"}>{entitlement?.isPaid ? "Active" : "Free"}</span>
          </div>
          <p>{entitlement?.isPaid ? `Active until ${formatDate(entitlement.activeUntil)}.` : "Upgrade to issue API keys."}</p>
          {!entitlement?.isPaid ? (
            <Link className="btn btn-primary btn-block" href="/dashboard/billing">
              Upgrade plan
            </Link>
          ) : null}
        </div>
      </aside>

      <div className="dash-main">
        <header className="dash-topbar">
          <div className="dash-topbar-group">
            <button type="button" className="icon-btn dash-menu-toggle" aria-label="Open navigation" onClick={() => setMenuOpen(true)}>
              ☰
            </button>
            <p className="dash-breadcrumb">
              Console / <strong>{pageTitles[pathname] ?? "Overview"}</strong>
            </p>
          </div>

          <div className="dash-topbar-group">
            <div className="dash-user">
              <strong>{user.displayName}</strong>
              <span>{user.email}</span>
            </div>
            <span className="avatar" aria-hidden="true">
              {user.avatarUrl ? <img src={user.avatarUrl} alt="" referrerPolicy="no-referrer" /> : initials(user.displayName)}
            </span>
            <form action="/api/auth/sign-out" method="post">
              <button type="submit" className="btn btn-ghost">
                Sign out
              </button>
            </form>
          </div>
        </header>

        <main className="dash-content">{children}</main>
      </div>
    </div>
  );
}
