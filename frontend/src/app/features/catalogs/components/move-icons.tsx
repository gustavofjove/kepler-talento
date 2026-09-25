/** Decorative arrows for the catalog reorder buttons: each button carries the accessible name. */
function Arrow({ d }: { d: string }) {
  return (
    <svg viewBox="0 0 16 16" width="14" height="14" aria-hidden="true" focusable="false">
      <path
        d={d}
        fill="none"
        stroke="currentColor"
        strokeWidth="1.8"
        strokeLinecap="round"
        strokeLinejoin="round"
      />
    </svg>
  );
}

export function ArrowUpIcon() {
  return <Arrow d="M8 13V3M3.5 7.5 8 3l4.5 4.5" />;
}

export function ArrowDownIcon() {
  return <Arrow d="M8 3v10M3.5 8.5 8 13l4.5-4.5" />;
}
