import ApiRounded from "@mui/icons-material/ApiRounded";
import DashboardRounded from "@mui/icons-material/DashboardRounded";
import QrCode2Rounded from "@mui/icons-material/QrCode2Rounded";
import ViewInArRounded from "@mui/icons-material/ViewInArRounded";
import Link from "next/link";

type IconComponent = typeof QrCode2Rounded;

const productSurfaces: Array<{
  title: string;
  body: string;
  Icon: IconComponent;
}> = [
  {
    title: "Public generator",
    body: "Our landing page is the QR builder, where users can easily and quickly generate QR codes with a front-facing interface",
    Icon: QrCode2Rounded,
  },
  {
    title: "Public API",
    body: "The API is strictly response-focused yet rate-limited. It can be used for free with a generous quota, and is ideal for devs",
    Icon: ApiRounded,
  },
  {
    title: "Dashboard-ready backend",
    body: "Coming soon",
    Icon: DashboardRounded,
  },
];

export default function ProductPage() {
  return (
    <main className="site-page route-shell">
      <section className="route-hero-card">
        <div className="route-hero-copy">
          <p className="page-kicker" style={{ color: "var(--text-white)" }}>Product overview</p>
          <h1 className="page-title">Interact with our product in three different ways.</h1>
          <p className="page-summary">
            HalfQR abstracts the entire QR code generation processs for any user, be it a simple business owner, an individual looking to share their linkedin page, graphiscs designers - the lot
          </p>

          <div className="cta-row">
            <Link className="route-primary-link" href="/">
              Open QR Generator
            </Link>
            <Link className="route-secondary-link" href="/api-documentation">
              Read the API docs
            </Link>
          </div>
        </div>

        {/* <div className="route-side-card">
          <div className="route-side-item">
            <ViewInArRounded fontSize="small" />
            <span>No sign-in dependency for first-time QR creation</span>
          </div>
          <div className="route-side-item">
            <DashboardRounded fontSize="small" />
            <span>Dashboard routes can stay feature-flagged until subscriptions are ready</span>
          </div>
        </div> */}
      </section>

      <section className="route-card-grid">
        {productSurfaces.map((surface) => {
          const Icon = surface.Icon;

          return (
            <article key={surface.title} className="route-card">
              {/* <div className="route-card-icon"> */}
                <Icon fontSize="large" />
              {/* </div> */}
              <h2>{surface.title}</h2>
              <p>{surface.body}</p>
            </article>
          );
        })}
      </section>
    </main>
  );
}