export interface AppConfig {
  SUPABASE_URL: string;
  SUPABASE_ANON_KEY: string;
  APP_ENV: string;
  APP_VERSION: string;
}

declare global {
  interface Window {
    __APP_CONFIG__?: Partial<AppConfig>;
  }
}

export function readAppConfig(): AppConfig {
  const config = window.__APP_CONFIG__ ?? {};
  return {
    SUPABASE_URL: config.SUPABASE_URL ?? '',
    SUPABASE_ANON_KEY: config.SUPABASE_ANON_KEY ?? '',
    APP_ENV: config.APP_ENV ?? 'local',
    APP_VERSION: config.APP_VERSION ?? '0.1.0',
  };
}
