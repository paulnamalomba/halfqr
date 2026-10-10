import { QrBuilder } from "@/components/qr-builder";

export default function HomePage() {
  return (
    <main className="site-page page-shell-home">
      <section className="generator-hero">
        <div className="generator-hero-copy">
          <h1 className="page-title">HalfQR: half the effort</h1>
          <p className="page-summary">Pick your content, style the code, and download SVG or PNG.</p>
        </div>
      </section>

      <div className="page-shell-home-builder">
        <QrBuilder />
      </div>
    </main>
  );
}
