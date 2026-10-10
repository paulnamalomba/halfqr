import { InfoCardGrid, type InfoCard } from "@/components/site/info-card-grid";
import { PageHero } from "@/components/site/page-hero";

const productSurfaces: InfoCard[] = [
  {
    title: "Public generator",
    body: "Our landing page is the QR builder, where users can easily and quickly generate QR codes with a front-facing interface.",
  },
  {
    title: "Public API",
    body: "The API is strictly response-focused yet rate-limited. It can be used for free with a generous quota, and is ideal for devs.",
  },
  {
    title: "Developer console",
    body: "Sign in to issue scoped API keys and access tokens, manage sessions, and pay for plans with Airtel Money or TNM Mpamba.",
  },
];

export default function ProductPage() {
  return (
    <main className="site-page route-shell">
      <PageHero
        title="Interact with our product in three different ways."
        summary="HalfQR handles QR code generation end to end for business owners, individuals sharing a profile, designers and developers."
        actions={[
          { href: "/", label: "Open QR Generator", variant: "primary" },
          { href: "/api-documentation", label: "Read the API docs" },
        ]}
      />
      <InfoCardGrid items={productSurfaces} />
    </main>
  );
}
