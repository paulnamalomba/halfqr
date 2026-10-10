import type { Metadata } from "next";
import Link from "next/link";
import { ChangelogTimeline } from "@/components/changelog/changelog-timeline";
import { PageHero } from "@/components/site/page-hero";
import { loadReleaseLines } from "@/lib/changelog";
import "./changelog.css";

export const metadata: Metadata = {
  title: "Changelog",
  description: "Every HalfQR API and web builder release, grouped by breaking, major and minor version lines.",
  alternates: { types: { "application/rss+xml": "/changelog/rss.xml" } },
};

// Release notes are read from .commits at build time.
export const dynamic = "force-static";

export default async function ChangelogPage() {
  const lines = await loadReleaseLines();
  const latest = lines[0];
  const buildCount = lines.reduce((total, line) => total + line.releases.length, 0);

  return (
    <main className="site-page route-shell changelog-page">
      <PageHero
        title="What changed in HalfQR, release by release."
        summary={
          <>
            HalfQR is open source. Every API and web builder release is grouped by <code className="changelog-scheme">breaking.major.minor</code>, with patch builds folded into each line.
          </>
        }
        actions={[
          { href: "/changelog/rss.xml", label: "Subscribe via RSS", variant: "primary", external: true },
          { href: "https://github.com/paulnamalomba/halfqr", label: "View source on GitHub", external: true },
        ]}
        aside={
          latest ? (
            <dl className="changelog-stats">
              <StatTile label="Latest line" value={`v${latest.version}`} />
              <StatTile label="Version lines" value={lines.length} />
              <StatTile label="Builds shipped" value={buildCount} />
            </dl>
          ) : null
        }
      />

      {lines.length === 0 ? (
        <p className="changelog-empty">
          No release notes found. Add <code>.commits/&lt;version&gt;.txt</code> files and rebuild. See the <Link href="/api-documentation">API docs</Link> meanwhile.
        </p>
      ) : (
        <ChangelogTimeline lines={lines} />
      )}
    </main>
  );
}

function StatTile({ label, value }: { label: string; value: string | number }) {
  return (
    <div>
      <dt>{label}</dt>
      <dd>{value}</dd>
    </div>
  );
}
