"use client";

import { useMemo, useState } from "react";
import type { ReleaseLevel, ReleaseLine } from "@/lib/changelog";
import { InlineMarkdown } from "./inline-markdown";

const levelLabels: Record<ReleaseLevel, string> = { breaking: "Breaking", major: "Major", minor: "Minor" };
const filters: Array<ReleaseLevel | "all"> = ["all", "breaking", "major", "minor"];
const dateFormatter = new Intl.DateTimeFormat("en-GB", { day: "numeric", month: "long", year: "numeric", timeZone: "UTC" });

function formatReleaseDate(value: string) {
  return value ? dateFormatter.format(new Date(`${value}T00:00:00Z`)) : "Undated";
}

export function ChangelogTimeline({ lines }: { lines: ReleaseLine[] }) {
  const [filter, setFilter] = useState<ReleaseLevel | "all">("all");
  const counts = useMemo(
    () => Object.fromEntries(filters.map((level) => [level, level === "all" ? lines.length : lines.filter((line) => line.level === level).length])),
    [lines],
  );
  const visible = filter === "all" ? lines : lines.filter((line) => line.level === filter);

  return (
    <>
      <div className="changelog-filters" role="tablist" aria-label="Filter releases by level">
        {filters.map((level) => (
          <button
            key={level}
            type="button"
            role="tab"
            aria-selected={filter === level}
            className={filter === level ? "changelog-filter changelog-filter-active" : "changelog-filter"}
            onClick={() => setFilter(level)}
            disabled={counts[level] === 0}
          >
            {level === "all" ? "All releases" : levelLabels[level]}
            <span>{counts[level]}</span>
          </button>
        ))}
      </div>

      <ol className="changelog-timeline">
        {visible.map((line) => (
          <li key={line.version} id={line.slug} className="changelog-entry">
            <aside className="changelog-meta">
              <time dateTime={line.date}>{formatReleaseDate(line.date)}</time>
              <div className="changelog-meta-row">
                <a className="changelog-version" href={`#${line.slug}`}>
                  v{line.version}
                </a>
                <span className={`changelog-level changelog-level-${line.level}`}>{levelLabels[line.level]}</span>
              </div>
              <span className="changelog-meta-note">
                {line.releases.length === 1 ? "1 build" : `${line.releases.length} builds`}
                {line.firstDate && line.firstDate !== line.date ? ` since ${formatReleaseDate(line.firstDate)}` : ""}
              </span>
            </aside>

            <span className="changelog-dot" aria-hidden="true" />

            <article className="changelog-card">
              <header className="changelog-card-head">
                <h2>
                  <a href={`#${line.slug}`}>
                    HalfQR {line.version}
                  </a>
                </h2>
                {line.highlights.length > 0 ? (
                  <div className="changelog-highlights">
                    {line.highlights.map((highlight) => (
                      <span key={highlight}>{highlight}</span>
                    ))}
                  </div>
                ) : null}
              </header>

              {line.releases.map((release) => (
                <section key={release.version} className="changelog-release">
                  {line.releases.length > 1 ? (
                    <h3 className="changelog-build">
                      <span>{release.version}</span>
                      <time dateTime={release.date}>{formatReleaseDate(release.date)}</time>
                    </h3>
                  ) : null}

                  {release.sections.map((section) => (
                    <div key={section.heading} className="changelog-section">
                      <h4>{section.heading}</h4>
                      <ul>
                        {section.items.map((item, index) => (
                          <li key={index}>
                            <InlineMarkdown text={item} />
                          </li>
                        ))}
                      </ul>
                    </div>
                  ))}
                </section>
              ))}
            </article>
          </li>
        ))}
      </ol>
    </>
  );
}
