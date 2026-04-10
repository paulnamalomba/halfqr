import AppsRounded from "@mui/icons-material/AppsRounded";
import ImageRounded from "@mui/icons-material/ImageRounded";
import LinkRounded from "@mui/icons-material/LinkRounded";
import PictureAsPdfRounded from "@mui/icons-material/PictureAsPdfRounded";
import ShareRounded from "@mui/icons-material/ShareRounded";
import SmartDisplayRounded from "@mui/icons-material/SmartDisplayRounded";
import WhatsApp from "@mui/icons-material/WhatsApp";
import Link from "next/link";

type IconComponent = typeof LinkRounded;

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
          <p className="page-kicker">QR content routes</p>
          <h1 className="page-title">Start with the destination type, then style the code around it.</h1>
          <p className="page-summary">
            The public builder keeps the first decision simple: choose a supported destination class, complete the payload, then move
            straight into the visual design step.
          </p>

          <div className="cta-row">
            <Link className="route-primary-link" href="/">
              Open generator
            </Link>
            <Link className="route-secondary-link" href="/api-documentation">
              See the request model
            </Link>
          </div>
        </div>

        <div className="route-side-card">
          <div className="route-side-item">
            <LinkRounded fontSize="small" />
            <span>Supported content types stay aligned with the live API contract</span>
          </div>
          <div className="route-side-item">
            <ShareRounded fontSize="small" />
            <span>The landing page stays focused on getting the visitor to QR creation quickly</span>
          </div>
        </div>
      </section>

      <section className="route-card-grid">
        {qrTypes.map((item) => {
          const Icon = item.Icon;

          return (
            <article key={item.title} className="route-card">
              <div className="route-card-icon">
                <Icon fontSize="small" />
              </div>
              <h2>{item.title}</h2>
              <p>{item.description}</p>
            </article>
          );
        })}
      </section>
    </main>
  );
}