export interface AppConfig {
  APP_ENV: string;
  APP_VERSION: string;
  API_BASE_URL: string;
  ENTRA_CLIENT_ID: string;
  ENTRA_AUTHORITY: string;
  ENTRA_API_SCOPE: string;
}

declare global {
  interface Window {
    __APP_CONFIG__?: Partial<AppConfig>;
  }
}

export function readAppConfig(): AppConfig {
  const config = window.__APP_CONFIG__ ?? {};
  return {
    APP_ENV: config.APP_ENV ?? 'local',
    APP_VERSION: config.APP_VERSION ?? '0.1.0',
    API_BASE_URL: config.API_BASE_URL ?? '/api',
    ENTRA_CLIENT_ID: config.ENTRA_CLIENT_ID ?? '',
    ENTRA_AUTHORITY: config.ENTRA_AUTHORITY ?? '',
    ENTRA_API_SCOPE: config.ENTRA_API_SCOPE ?? '',
  };
}
