# Improvements to SiteHeader component

- `webapp/src/components/site-header.tsx` - The site header component, which includes the navigation and mobile menu logic.
- The main improvements here are:

1) To anchor the mobile menu to the hamburger button area, hide the desktop nav on small screens, and give the mobile items their own full-width styles. In practice that means making the action area relative, rendering the open menu as an absolute panel, and switching the mobile links to block or flex w-full justify-start so they read like a dropdown list rather than nav pills.

```tsx
<header className="site-header-shell">
  <div className="site-header-inner">
    <Link className="site-brand-mark" href="/" aria-label="HalfQR generator home">
      <img src="/logos/havqr_main_6000x3306.svg" alt="HalfQR" width="81" height="44" />
    </Link>

    <nav className="site-header-nav hidden md:flex" aria-label="Primary navigation">
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

    <div className="site-header-actions relative">
      <a
        className="site-header-status hidden md:inline-flex"
        href="https://api.halfqr.com/healthz"
        target="_blank"
        rel="noreferrer"
      >
        <span>API status</span>
        <OpenInNewRounded fontSize="inherit" />
      </a>

      <Link
        className={pathname === "/" ? "site-header-cta site-header-cta-active hidden md:inline-flex" : "site-header-cta hidden md:inline-flex"}
        href="/"
      >
        <DescriptionRounded fontSize="small" />
        <span>Generate QR</span>
      </Link>

      <button
        className="md:hidden flex h-10 w-10 items-center justify-center rounded-full bg-bg-light"
        onClick={() => setMenuOpen(!menuOpen)}
      >
        {menuOpen ? <CloseRoundedIcon /> : <MenuRoundedIcon />}
      </button>

      {menuOpen && (
        <div className="absolute right-0 top-full mt-3 flex w-64 flex-col rounded-3xl border border-white/15 bg-slate-950/90 p-3 shadow-2xl backdrop-blur md:hidden">
          <Link href="/" className="rounded-xl px-4 py-3 text-left text-white/90 hover:bg-white/10" onClick={() => setMenuOpen(false)}>
            Generate QR
          </Link>

          {navigationItems.map((item) => (
            <Link
              key={item.href}
              href={item.href}
              onClick={() => setMenuOpen(false)}
              className={
                isActivePath(pathname, item.href)
                  ? "rounded-xl px-4 py-3 text-left text-white bg-white/12"
                  : "rounded-xl px-4 py-3 text-left text-white/90 hover:bg-white/10"
              }
            >
              {item.label}
            </Link>
          ))}
        </div>
      )}
    </div>
  </div>
</header>
```

1) If you want to keep the styling in CSS instead of utility classes, create a separate mobile class such as .site-mobile-menu and .site-mobile-menu-link with position: absolute, display: flex, flex-direction: column, width, and justify-content: flex-start.

2) The links created by navigationItems.map(...) should also call setMenuOpen(false), otherwise the dropdown stays open after navigation
