import { createClient, SupabaseClient } from '@supabase/supabase-js';
import { readAppConfig } from '../services/app-config.model';

export class SupabaseClientService {
  private readonly config = readAppConfig();
  private readonly client = this.createSafeClient();

  get enabled(): boolean {
    return this.client !== null;
  }

  get supabase(): SupabaseClient | null {
    return this.client;
  }

  private createSafeClient(): SupabaseClient | null {
    if (!this.config.SUPABASE_URL || !this.config.SUPABASE_ANON_KEY) {
      return null;
    }

    if (this.config.SUPABASE_URL.includes('${') || this.config.SUPABASE_ANON_KEY.includes('${')) {
      return null;
    }

    return createClient(this.config.SUPABASE_URL, this.config.SUPABASE_ANON_KEY, {
      auth: {
        persistSession: true,
        autoRefreshToken: true,
      },
    });
  }
}
