import { loadReleaseLines } from "@/lib/changelog";

export const dynamic = "force-static";

const siteUrl = "https://www.halfqr.com";

function escapeXml(value: string) {
  return value.replace(/[<>&'"]/g, (character) => ({ "<": "&lt;", ">": "&gt;", "&": "&amp;", "'": "&apos;", '"': "&quot;" })[character] ?? character);
}

export async function GET() {
  const lines = await loadReleaseLines();

  const items = lines
    .map((line) => {
      const description = line.releases
        .flatMap((release) => release.sections.flatMap((section) => section.items.map((item) => `${section.heading}: ${item}`)))
        .slice(0, 12)
        .join("\n");

      return `    <item>
      <title>HalfQR ${escapeXml(line.version)} (${line.level})</title>
      <link>${siteUrl}/changelog#${line.slug}</link>
      <guid isPermaLink="false">halfqr-${escapeXml(line.version)}</guid>
      <pubDate>${new Date(`${line.date || "1970-01-01"}T00:00:00Z`).toUTCString()}</pubDate>
      <description>${escapeXml(description)}</description>
    </item>`;
    })
    .join("\n");

  const xml = `<?xml version="1.0" encoding="UTF-8"?>
<rss version="2.0">
  <channel>
    <title>HalfQR changelog</title>
    <link>${siteUrl}/changelog</link>
    <description>Releases of the open-source HalfQR API and web builder.</description>
${items}
  </channel>
</rss>
`;

  return new Response(xml, { headers: { "Content-Type": "application/rss+xml; charset=utf-8" } });
}
