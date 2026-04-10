"use client";

import DescriptionRounded from "@mui/icons-material/DescriptionRounded";
import OpenInNewRounded from "@mui/icons-material/OpenInNewRounded";
import Link from "next/link";
import { usePathname } from "next/navigation";

const navigationItems = [
  { href: "/qr-codes", label: "QR Codes" },
  { href: "/why-haveqr", label: "Why HaveQR" },
  { href: "/product", label: "Product" },
  { href: "/api-documentation", label: "API docs" },
];

function isActivePath(pathname: string, href: string) {
  return pathname === href || pathname.startsWith(`${href}/`);
}

export function SiteHeader() {
  const pathname = usePathname();

  return (
    <header className="site-header-shell">
      <div className="site-header-inner">
        <Link className="site-brand-mark" href="/" aria-label="HaveQR generator home">
          <img src="/logos/havqr_main_6000x3306.svg" alt="HaveQR" width="162" height="88" />
        </Link>

        <nav className="site-header-nav" aria-label="Primary navigation">
          {navigationItems.map((item) => (
            <Link
              key={item.href}
              href={item.href}
              className={isActivePath(pathname, item.href) ? "site-header-link site-header-link-active" : "site-header-link"}
            >
              {item.label}
            </Link>
          ))}
        </nav>

        <div className="site-header-actions">
          <a
            className="site-header-status"
            href="https://haveqr-api-demo.computemore.com/healthz"
            target="_blank"
            rel="noreferrer"
          >
            <span>API status</span>
            <OpenInNewRounded fontSize="inherit" />
          </a>

          <Link className={pathname === "/" ? "site-header-cta site-header-cta-active" : "site-header-cta"} href="/">
            <DescriptionRounded fontSize="small" />
            <span>Open generator</span>
          </Link>
        </div>
      </div>
    </header>
  );
}