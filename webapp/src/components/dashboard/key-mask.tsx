export function KeyMask({ prefix, lastFour }: { prefix: string; lastFour: string }) {
  return (
    <span className="key-mask">
      {prefix}
      <em>••••••••</em>
      {lastFour}
    </span>
  );
}
