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
    body: "The landing page now starts with QR creation itself so a first-time visitor can style and queue a real render immediately.",
    Icon: QrCode2Rounded,
  },
  {
    title: "Public API",
    body: "The webapp and external consumers share the same render endpoints, async job flow, and artifact downloads.",
    Icon: ApiRounded,
  },
  {
    title: "Dashboard-ready backend",
    body: "Customer dashboard routes can stay feature-flagged while subscriptions mature, without blocking public QR creation.",
    Icon: DashboardRounded,
  },
];

export default function ProductPage() {
  return (
    <main className="site-page route-shell">
      <section className="route-hero-card">
        <div className="route-hero-copy">
          <p className="page-kicker">Product overview</p>
          <h1 className="page-title">One product surface, three delivery layers.</h1>
          <p className="page-summary">
            HalfQR keeps the public builder, render API, and future customer workspace in the same visual and technical system so the
            experience can scale without changing the underlying model.
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
              <div className="route-card-icon">
                <Icon fontSize="small" />
              </div>
              <h2>{surface.title}</h2>
              <p>{surface.body}</p>
            </article>
          );
        })}
      </section>
    </main>
  );
}