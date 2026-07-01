import { Injectable } from '@angular/core';
import { SupabaseClientService } from '../supabase/supabase-client.service';

@Injectable({ providedIn: 'root' })
export class MfaService {
  constructor(private readonly supabaseClient: SupabaseClientService) {}

  async getAssuranceLevel(): Promise<'aal1' | 'aal2'> {
    const client = this.supabaseClient.supabase;
    if (!client) {
      return 'aal2';
    }
    const { data } = await client.auth.mfa.getAuthenticatorAssuranceLevel();
    return data.currentLevel === 'aal2' ? 'aal2' : 'aal1';
  }
}
