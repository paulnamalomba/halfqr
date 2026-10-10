import { Fragment, ReactNode } from "react";

// Renders the small Markdown subset used in .commits notes: `code`, **bold** and [text](url).
export function InlineMarkdown({ text }: { text: string }) {
  const parts: ReactNode[] = [];
  const pattern = /(`[^`]+`|\*\*[^*]+\*\*|\[[^\]]+\]\(https?:\/\/[^)\s]+\))/g;
  let lastIndex = 0;

  for (const match of text.matchAll(pattern)) {
    const token = match[0];
    const index = match.index ?? 0;
    parts.push(text.slice(lastIndex, index));

    if (token.startsWith("`")) {
      parts.push(<code key={index}>{token.slice(1, -1)}</code>);
    } else if (token.startsWith("**")) {
      parts.push(<strong key={index}>{token.slice(2, -2)}</strong>);
    } else {
      const link = token.match(/^\[([^\]]+)\]\(([^)]+)\)$/);
      parts.push(
        <a key={index} href={link?.[2]} target="_blank" rel="noreferrer">
          {link?.[1]}
        </a>,
      );
    }

    lastIndex = index + token.length;
  }

  parts.push(text.slice(lastIndex));
  return <>{parts.map((part, index) => <Fragment key={index}>{part}</Fragment>)}</>;
}
