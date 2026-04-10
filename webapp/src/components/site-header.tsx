"use client";

import DescriptionRounded from "@mui/icons-material/DescriptionRounded";
import OpenInNewRounded from "@mui/icons-material/OpenInNewRounded";
import MenuRoundedIcon from "@mui/icons-material/MenuRounded";
import CloseRoundedIcon from "@mui/icons-material/CloseRounded";
import Link from "next/link";
import { usePathname } from "next/navigation";
import { useState } from "react";

// Note: the active path logic here assumes no nested routes beyond the first level, which is true for the current webapp but may need to be revisited if deeper nesting is added in the future.
// Can add more here where necessary but the main point is to have a single source of truth for the primary navigation structure and active path logic so it doesn't diverge across components or get lost in individual route files.
const navigationItems = [
  { href: "/qr-codes", label: "QR Codes" },
  { href: "/why-haveqr", label: "Why HaveQR" },
  { href: "/product", label: "Product" },
  { href: "/api-documentation", label: "API docs" },
];

// Get if current path is the active one
function isActivePath(pathname: string, href: string) {
  return pathname === href || pathname.startsWith(`${href}/`);
}

export function SiteHeader() {
  const pathname = usePathname();
  const [menuOpen, setMenuOpen] = useState(false);
  const isAuthenticated = false; // Placeholder for auth state, can be replaced with real auth logic

  return (
    <header className="site-header-shell">
      <div className="site-header-inner">
        <Link className="site-brand-mark" href="/" aria-label="HaveQR generator home">
          <img src="/logos/havqr_main_6000x3306.svg" alt="HaveQR" width="81" height="44" />
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
            <span>Generate QR</span>
          </Link>

          {/* Mobile menu button */}
          <button
            className="md:hidden flex h-10 w-10 items-center justify-center rounded-full bg-bg-light"
            onClick={() => setMenuOpen(!menuOpen)}
          >
            {menuOpen ? <CloseRoundedIcon /> : <MenuRoundedIcon />}
          </button>
        </div>
      </div>    

      {/* Mobile menu */}
      {menuOpen && (
        <div className="md:hidden glass border-t border-gray-100 px-6 py-4 space-y-3 text-sm font-medium">
            <Link href="/" className="block py-2" onClick={() => setMenuOpen(false)}>Generate QR</Link>
            {navigationItems.map((item) => (
                <Link
                key={item.href}
                href={item.href}
                className={isActivePath(pathname, item.href) ? "site-header-link site-header-link-active" : "site-header-link"}
                >
                {item.label}
                </Link>
            ))}
            {/* {isAuthenticated && (
                <Link href="/dashboard" className="block py-2 text-cobalt font-bold" onClick={() => setMenuOpen(false)}>Finance</Link>
            )}

            {isAuthenticated ? (
                <button onClick={() => { logout(); setMenuOpen(false); }} className="block py-2 text-danger">
                Sign out
                </button>
            ) : (
                <Link
                href="/login"
                className="block rounded-full bg-navy px-5 py-2.5 text-center text-white font-semibold"
                onClick={() => setMenuOpen(false)}
                >
                Sign in
                </Link>
            )} */}
        </div>
      )}

    </header>
  );
}