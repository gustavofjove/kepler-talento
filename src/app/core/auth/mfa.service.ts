import { SupabaseClientService } from '../supabase/supabase-client.service';

export class MfaService {
  constructor(private readonly supabaseClient: SupabaseClientService) {}

  async getAssuranceLevel(): Promise<'aal1' | 'aal2'> {
    const client = this.supabaseClient.supabase;
    if (!client) {
      return 'aal2';
    }
    const { data } = await client.auth.mfa.getAuthenticatorAssuranceLevel();
    // `data` is nullable in the Supabase types; a null result is not aal2,
    // which is the same outcome the previous expression produced.
    return data?.currentLevel === 'aal2' ? 'aal2' : 'aal1';
  }
}
