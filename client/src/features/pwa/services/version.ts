/** Version des Bundles (007 AK-15), beim Build ersetzt: Release-Tag oder kurzer Commit-Hash aus der CI; lokal "dev". */
export function formatVersion(raw: string | undefined): string {
  const value = raw?.trim();
  return value ? value : "dev";
}
export const APP_VERSION = formatVersion(import.meta.env.VITE_APP_VERSION);
