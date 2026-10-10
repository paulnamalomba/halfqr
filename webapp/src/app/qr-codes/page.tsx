import { InfoCardGrid, type InfoCard } from "@/components/site/info-card-grid";
import { PageHero } from "@/components/site/page-hero";

// Content types the generator supports today. Payment links and bank details are planned.
const qrTypes: InfoCard[] = [
  { title: "Link", body: "Use the generator for public web routes, campaigns, and landing pages." },
  { title: "App", body: "Send scanners into onboarding, install flows, or app-store listings." },
  { title: "Social", body: "Point scans into social profiles, creator hubs, or campaign pages." },
  { title: "PDF", body: "Attach brochures, menus, and printable collateral to hosted assets." },
  { title: "Image", body: "Link directly to packaging art, menus, posters, or galleries." },
  { title: "Video", body: "Open product demos, launch videos, or training content from a single scan." },
  { title: "WhatsApp", body: "Use a direct wa.me target or generate a fallback route from phone and message fields." },
];

export default function QrCodesPage() {
  return (
    <main className="site-page route-shell">
      <PageHero
        title="Go paperless."
        summary="Your links, your content, and quick, paperless routing."
        actions={[
          { href: "/", label: "QR generator", variant: "primary" },
          { href: "/api-documentation", label: "API usage guides" },
        ]}
      />
      <InfoCardGrid items={qrTypes} />
    </main>
  );
}
