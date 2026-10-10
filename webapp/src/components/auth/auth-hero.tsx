import Link from "next/link";
import { BrandLockup } from "@/components/console/ui";

// Carded hero: brand lockup at the top, a terminal sample at the bottom.
export function AuthHero() {
  return (
    <aside className="auth-hero">
      <Link href="/" aria-label="HalfQR home">
        <BrandLockup />
      </Link>

      <div className="terminal" aria-hidden="true">
        <div className="terminal-bar">render.sh</div>
        <pre>
          <span className="tok-muted">$</span> curl https://api.halfqr.com/api/v1/qr/render \{"\n"}
          {"  "}-H <span className="tok-str">&quot;Authorization: Bearer hqr_sk_••••&quot;</span> \{"\n"}
          {"  "}-d <span className="tok-str">&apos;{"{"}&quot;targetUrl&quot;: &quot;https://halfqr.com&quot;{"}"}&apos;</span>
          {"\n\n"}
          <span className="tok-muted">{"// 202 Accepted"}</span>
          {"\n"}
          {'{ "status": "Queued" }'}
        </pre>
      </div>
    </aside>
  );
}
