import AppsRounded from "@mui/icons-material/AppsRounded";
import ImageRounded from "@mui/icons-material/ImageRounded";
import LinkRounded from "@mui/icons-material/LinkRounded";
import PictureAsPdfRounded from "@mui/icons-material/PictureAsPdfRounded";
import ShareRounded from "@mui/icons-material/ShareRounded";
import SmartDisplayRounded from "@mui/icons-material/SmartDisplayRounded";
import WhatsApp from "@mui/icons-material/WhatsApp";
import Link from "next/link";

type IconComponent = typeof LinkRounded;

// Hard-coded qr-types for generation, we are making a best-effort here to have as many as possible
// Some are yet to come/be implemented, i.e money, payment links, airtel money links, bank details
const qrTypes: Array<{
  title: string;
  description: string;
  Icon: IconComponent;
}> = [
  {
    title: "Link",
    description: "Use the generator for public web routes, campaigns, and landing pages.",
    Icon: LinkRounded,
  },
  {
    title: "App",
    description: "Send scanners into onboarding, install flows, or app-store listings.",
    Icon: AppsRounded,
  },
  {
    title: "Social",
    description: "Point scans into social profiles, creator hubs, or campaign pages.",
    Icon: ShareRounded,
  },
  {
    title: "PDF",
    description: "Attach brochures, menus, and printable collateral to hosted assets.",
    Icon: PictureAsPdfRounded,
  },
  {
    title: "Image",
    description: "Link directly to packaging art, menus, posters, or galleries.",
    Icon: ImageRounded,
  },
  {
    title: "Video",
    description: "Open product demos, launch videos, or training content from a single scan.",
    Icon: SmartDisplayRounded,
  },
  {
    title: "WhatsApp",
    description: "Use a direct wa.me target or generate a fallback route from phone and message fields.",
    Icon: WhatsApp,
  },
];

export default function QrCodesPage() {
  return (
    <main className="site-page route-shell">
      <section className="route-hero-card">
        <div className="route-hero-copy">
          <p className="page-kicker" style={{ color: "var(--text-white)" }}>QR content routes</p>
          <h1 className="page-title">Go paperless.</h1>
          <p className="page-summary">
            Your links, your content, and quick, paperless routing
          </p>

          <div className="cta-row">
            <Link className="route-primary-link" href="/">
              QR generator
            </Link>
            <Link className="route-secondary-link" href="/api-documentation">
              API usage guides
            </Link>
          </div>
        </div>

        {/* <div className="route-side-card"> */}
          {/* <div className="route-side-item"> */}
            {/* <LinkRounded fontSize="small" /> */}
            {/* <span>Supported content types stay aligned with the live API contract</span> */}
          {/* </div> */}
          {/* <div className="route-side-item"> */}
            {/* <ShareRounded fontSize="small" /> */}
            {/* <span>The landing page stays focused on getting the visitor to QR creation quickly</span> */}
          {/* </div> */}
        {/* </div> */}
      </section>

      <section className="route-card-grid">
        {qrTypes.map((item) => {
          const Icon = item.Icon;

          return (
            <article key={item.title} className="route-card">
              {/* <div className="route-card-icon"> */}
                <Icon fontSize="large" />
              {/* </div> */}
              <h2>{item.title}</h2>
              <p>{item.description}</p>
            </article>
          );
        })}
      </section>
    </main>
  );
}