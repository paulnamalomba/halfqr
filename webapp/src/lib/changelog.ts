import { promises as fs } from "node:fs";
import path from "node:path";

// Release notes live in the repository root as .commits/<breaking>.<major>.<minor>.<patch>.txt.
// The changelog groups patch builds under their <breaking>.<major>.<minor> line.

export type ReleaseLevel = "breaking" | "major" | "minor";

export type ReleaseSection = {
  heading: string;
  items: string[];
};

export type PatchRelease = {
  version: string;
  date: string;
  sections: ReleaseSection[];
};

export type ReleaseLine = {
  version: string;
  slug: string;
  level: ReleaseLevel;
  date: string;
  firstDate: string;
  releases: PatchRelease[];
  highlights: string[];
};

const releaseFilePattern = /^(\d+)\.(\d+)\.(\d+)\.(\d+)\.txt$/;

export function resolveCommitsDirectory() {
  return process.env.HALFQR_COMMITS_DIR ?? path.join(process.cwd(), "..", ".commits");
}

export async function loadReleaseLines(): Promise<ReleaseLine[]> {
  const directory = resolveCommitsDirectory();
  // Read only at build time for static pages, so the release notes are not traced into the server bundle.
  const files = (await fs.readdir(/*turbopackIgnore: true*/ directory).catch(() => [] as string[])).filter((file) => releaseFilePattern.test(file));

  const releases = await Promise.all(
    files.map(async (file) => parseRelease(file.replace(/\.txt$/, ""), await fs.readFile(path.join(/*turbopackIgnore: true*/ directory, file), "utf8"))),
  );

  const lines = new Map<string, PatchRelease[]>();

  for (const release of releases) {
    const key = release.version.split(".").slice(0, 3).join(".");
    lines.set(key, [...(lines.get(key) ?? []), release]);
  }

  const ordered = [...lines.entries()]
    .map(([version, patches]) => ({ version, patches: patches.sort((a, b) => compareVersions(b.version, a.version)) }))
    .sort((a, b) => compareVersions(b.version, a.version));

  return ordered.map(({ version, patches }, index) => {
    const previous = ordered[index + 1]?.version;
    const dates = patches.map((patch) => patch.date).sort();

    return {
      version,
      slug: `v${version.replaceAll(".", "-")}`,
      level: classify(version, previous),
      date: dates.at(-1) ?? "",
      firstDate: dates[0] ?? "",
      releases: patches,
      highlights: [...new Set(patches.flatMap((patch) => patch.sections.map((section) => section.heading)).filter((heading) => !/^(validation|changes)$/i.test(heading)))].slice(0, 4),
    };
  });
}

function parseRelease(version: string, text: string): PatchRelease {
  const dateMatch = text.match(/^## \[[^\]]+\] - (\d{4}-\d{2}-\d{2})/m);
  const sections: ReleaseSection[] = [];
  let current: ReleaseSection | null = null;

  for (const rawLine of text.split(/\r?\n/)) {
    const line = rawLine.trimEnd();
    const heading = line.match(/^###\s+(.+)$/);

    if (heading) {
      current = { heading: heading[1].trim(), items: [] };
      sections.push(current);
      continue;
    }

    const bullet = line.match(/^\s*[-*]\s+(.+)$/);

    if (bullet && current) {
      // Older notes end lines with "--- ADDED ---" style markers; they carry no extra meaning on the page.
      current.items.push(bullet[1].replace(/\s*-{2,}\s*[A-Z ]+\s*-{2,}\s*$/, "").trim());
    } else if (current && current.items.length > 0 && /^\s{2,}\S/.test(rawLine)) {
      current.items[current.items.length - 1] += ` ${line.trim()}`;
    }
  }

  return { version, date: dateMatch?.[1] ?? "", sections: sections.filter((section) => section.items.length > 0) };
}

// A line is "breaking" when the first component moved, "major" when the second moved, otherwise "minor".
function classify(version: string, previous: string | undefined): ReleaseLevel {
  if (!previous) {
    return "major";
  }

  const [breaking, major] = version.split(".").map(Number);
  const [previousBreaking, previousMajor] = previous.split(".").map(Number);

  if (breaking !== previousBreaking) {
    return "breaking";
  }

  return major !== previousMajor ? "major" : "minor";
}

function compareVersions(left: string, right: string) {
  const a = left.split(".").map(Number);
  const b = right.split(".").map(Number);

  for (let index = 0; index < Math.max(a.length, b.length); index += 1) {
    const difference = (a[index] ?? 0) - (b[index] ?? 0);

    if (difference !== 0) {
      return difference;
    }
  }

  return 0;
}
