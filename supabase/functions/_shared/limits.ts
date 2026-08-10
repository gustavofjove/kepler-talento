export const EDGE_LIMITS = {
  signedUrlTtlSeconds: 300,
  maxExportRows: 1000,
  maxImportFiles: 8,
};

export function parsePositiveInt(value: unknown, fallback: number): number {
  const parsed = typeof value === 'number' ? value : Number(value);
  if (!Number.isFinite(parsed) || parsed <= 0) {
    return fallback;
  }
  return Math.floor(parsed);
}
