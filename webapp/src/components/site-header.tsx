"use client";

import { CloseRoundedIcon, MenuRoundedIcon, SiteTitleIcon } from "@/assets/icons";
import Link from "next/link";
import { usePathname } from "next/navigation";
import { useEffect, useState } from "react";

// Note: the active path logic here assumes no nested routes beyond the first level, which is true for the current webapp but may need to be revisited if deeper nesting is added in the future.
// Can add more here where necessary but the main point is to have a single source of truth for the primary navigation structure and active path logic so it doesn't diverge across components or get lost in individual route files.
const navigationItems = [
  { href: "/qr-codes", label: "QR Codes" },
  { href: "/why-halfqr", label: "Why HalfQR" },
  { href: "/product", label: "Product" },
  { href: "/api-documentation", label: "API docs" },
  { href: "/changelog", label: "Changelog" },
];

// Console routes (auth and dashboard) render their own chrome.
const consoleRoutes = ["/sign-in", "/sign-up", "/dashboard"];

// Get if current path is the active one
function isActivePath(pathname: string, href: string) {
  return pathname === href || pathname.startsWith(`${href}/`);
}

export function SiteHeader() {
  const pathname = usePathname();
  const [menuOpen, setMenuOpen] = useState(false);

  useEffect(() => {
    setMenuOpen(false);
  }, [pathname]);

  useEffect(() => {
    const mediaQuery = window.matchMedia("(min-width: 961px)");
    const handleViewportChange = (event: MediaQueryListEvent) => {
      if (event.matches) {
        setMenuOpen(false);
      }
    };

    mediaQuery.addEventListener("change", handleViewportChange);
    return () => mediaQuery.removeEventListener("change", handleViewportChange);
  }, []);

  if (consoleRoutes.some((route) => isActivePath(pathname, route))) {
    return null;
  }

  return (
    <header className="site-header-shell">
      <div className="site-header-inner">
        {/* persistent site branding */}
        <Link className="site-brand-mark" href="/" aria-label="HalfQR generator home">
          <SiteTitleIcon />
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
          {/* <a
            className="site-header-status"
            href="https://api.halfqr.com/healthz"
            target="_blank"
            rel="noreferrer"    
          >
            <span>API status</span>
          </a> */}

          {/* developer console entry */}
          <Link className="site-header-link site-header-signin" href="/sign-in">
            Sign in
          </Link>

          {/* primary call-to-action */}
          <Link className={pathname === "/" ? "site-header-cta site-header-cta-active" : "site-header-cta"} href="/">
            Generate QR
          </Link>

          {/* mobile menu toggle */}
          <button
            type="button"
            className="site-mobile-toggle"
            aria-expanded={menuOpen}
            aria-controls="site-mobile-menu"
            aria-label={menuOpen ? "Close navigation menu" : "Open navigation menu"}
            onClick={() => setMenuOpen((open) => !open)}
          >
            {menuOpen ? <CloseRoundedIcon /> : <MenuRoundedIcon />}
          </button>

          {/* toggled state for mobile menu */}
          {menuOpen ? (
            <nav id="site-mobile-menu" className="site-mobile-menu" aria-label="Mobile navigation">
              <Link
                href="/"
                className={pathname === "/" ? "site-mobile-menu-link site-mobile-menu-link-active" : "site-mobile-menu-link"}
              >
                Generate QR
              </Link>

              {navigationItems.map((item) => (
                <Link
                  key={item.href}
                  href={item.href}
                  className={isActivePath(pathname, item.href) ? "site-mobile-menu-link site-mobile-menu-link-active" : "site-mobile-menu-link"}
                >
                  {item.label}
                </Link>
              ))}

              <Link href="/sign-in" className="site-mobile-menu-link">
                Sign in
              </Link>
            </nav>
          ) : null}
        </div>
      </div>
    </header>
  );
}