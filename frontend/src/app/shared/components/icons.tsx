/**
 * An arrow dropping into a tray for «Descargar» — not a floppy disk, which reads as saving
 * changes. Decorative: the button carries the name. Drawn on the 16px grid it is shown at, with
 * 1px strokes on half-pixel coordinates, so the long edges render as crisp single pixels instead
 * of smearing across two.
 */
export function DownloadIcon() {
  return (
    <svg
      className="row-cv-toggle__icon"
      viewBox="0 0 16 16"
      width="16"
      height="16"
      aria-hidden="true"
      focusable="false"
      fill="none"
      stroke="currentColor"
      strokeWidth={1}
      strokeLinecap="round"
      strokeLinejoin="round"
    >
      <path d="M7.5 1.5v9" />
      <path d="M4.5 7.5l3 3 3-3" />
      <path d="M1.5 11.5v2a1 1 0 0 0 1 1h10a1 1 0 0 0 1-1v-2" />
    </svg>
  );
}
export function FileIcon() {
  return (
    <svg
      viewBox="0 0 16 16"
      width="16"
      height="16"
      aria-hidden="true"
      focusable="false"
      fill="none"
      stroke="currentColor"
      strokeWidth={1}
      strokeLinecap="round"
      strokeLinejoin="round"
    >
      <path d="M9.5 1.5h-6v13h9v-10z M9.5 1.5v3h3 M5.5 7.5h5 M5.5 10.5h5" />
    </svg>
  );
}
export function StarIcon() {
  return (
    <svg
      viewBox="0 0 16 16"
      width="16"
      height="16"
      aria-hidden="true"
      focusable="false"
      fill="none"
      stroke="currentColor"
      strokeWidth={1}
      strokeLinecap="round"
      strokeLinejoin="round"
    >
      <path d="M7.5 1.5l2 4 4.5.5-3.5 3 1 4.5-4-2-4 2 1-4.5-3.5-3 4.5-.5z" />
    </svg>
  );
}
export function TrashIcon() {
  return (
    <svg
      viewBox="0 0 16 16"
      width="16"
      height="16"
      aria-hidden="true"
      focusable="false"
      fill="none"
      stroke="currentColor"
      strokeWidth={1}
      strokeLinecap="round"
      strokeLinejoin="round"
    >
      <path d="M2.5 4.5h11 M5.5 4.5v-3h5v3 M3.5 4.5l1 10h7l1-10 M6.5 7.5v4 M9.5 7.5v4" />
    </svg>
  );
}
export function UploadIcon() {
  return (
    <svg
      viewBox="0 0 16 16"
      width="16"
      height="16"
      aria-hidden="true"
      focusable="false"
      fill="none"
      stroke="currentColor"
      strokeWidth={1}
      strokeLinecap="round"
      strokeLinejoin="round"
    >
      <path d="M7.5 11.5v-10 M4.5 4.5l3-3 3 3 M1.5 10.5v4h12v-4" />
    </svg>
  );
}
export function CloseIcon() {
  return (
    <svg
      viewBox="0 0 16 16"
      width="16"
      height="16"
      aria-hidden="true"
      focusable="false"
      fill="none"
      stroke="currentColor"
      strokeWidth={1}
      strokeLinecap="round"
      strokeLinejoin="round"
    >
      <path d="M3.5 3.5l9 9 M12.5 3.5l-9 9" />
    </svg>
  );
}
