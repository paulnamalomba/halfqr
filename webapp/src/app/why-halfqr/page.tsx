import { InfoCardGrid, type InfoCard } from "@/components/site/info-card-grid";
import { PageHero } from "@/components/site/page-hero";

const valueCards: InfoCard[] = [
  { title: "Preview-first, outcome-focused service", body: "You always see the outcome even before it is finalised." },
  { title: "Fast interaction, and superior response times", body: "We want your work to be quick, in and out." },
];

export default function WhyHalfQrPage() {
  return (
    <main className="site-page route-shell">
      <PageHero
        title="Half the effort, same digital impact."
        summary="QR codes grow a business's digital footprint and save paper. Generate a code your customers can scan to reach everything important."
        actions={[
          { href: "/", label: "Try it out now", variant: "primary" },
          { href: "/product", label: "View product details" },
        ]}
      />
      <InfoCardGrid items={valueCards} />
    </main>
  );
}
