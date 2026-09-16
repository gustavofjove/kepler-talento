import {
  BrowserCacheLocation,
  PublicClientApplication,
  type AccountInfo,
} from '@azure/msal-browser';
import type { AppConfig } from '../services/app-config.model';

export interface TokenSource {
  signIn(): Promise<string>;
  getToken(): Promise<string | null>;
  signOut(): Promise<void>;
}

export class DevelopmentTokenSource implements TokenSource {
  private token: string | null = null;
  constructor(
    private readonly apiBaseUrl: string,
    private readonly fetcher: typeof fetch = globalThis.fetch.bind(globalThis),
  ) {}
  async signIn(): Promise<string> {
    const response = await this.fetcher(`${this.apiBaseUrl}/dev/token`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
      body: JSON.stringify({
        subject: 'dev-admin-oid',
        displayName: 'Administrador local',
        email: 'admin@kepler-talento.local',
      }),
    });
    if (!response.ok) throw new Error('Could not acquire a development token.');
    this.token = ((await response.json()) as { accessToken: string }).accessToken;
    return this.token;
  }
  getToken(): Promise<string | null> {
    return Promise.resolve(this.token);
  }
  signOut(): Promise<void> {
    this.token = null;
    return Promise.resolve();
  }
}

export class MsalTokenSource implements TokenSource {
  private readonly client: PublicClientApplication;
  private account: AccountInfo | null = null;
  constructor(
    clientId: string,
    authority: string,
    private readonly scope: string,
  ) {
    this.client = new PublicClientApplication({
      auth: { clientId, authority, redirectUri: window.location.origin },
      cache: { cacheLocation: BrowserCacheLocation.SessionStorage },
    });
  }
  async signIn(): Promise<string> {
    await this.client.initialize();
    const result = await this.client.loginPopup({ scopes: [this.scope] });
    this.account = result.account;
    return result.accessToken;
  }
  async getToken(): Promise<string | null> {
    if (!this.account) return null;
    return (await this.client.acquireTokenSilent({ account: this.account, scopes: [this.scope] }))
      .accessToken;
  }
  async signOut(): Promise<void> {
    if (this.account) await this.client.logoutPopup({ account: this.account });
    this.account = null;
  }
}

export function createTokenSource(config: AppConfig): TokenSource {
  return config.APP_ENV === 'local' || config.APP_ENV === 'test'
    ? new DevelopmentTokenSource(config.API_BASE_URL)
    : new MsalTokenSource(config.ENTRA_CLIENT_ID, config.ENTRA_AUTHORITY, config.ENTRA_API_SCOPE);
}
