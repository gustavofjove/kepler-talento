# Especificación técnica: integrar Supabase en una app existente (patrón KeplerDesk)

> Este documento es autocontenido. Está pensado para entregárselo a un agente de IA que trabaje
> en **otra aplicación** (no Angular-específico salvo en la sección 9), describiendo el patrón
> exacto que usa KeplerDesk para que se replique adaptado al dominio de la nueva app.

> **Nota (KTL-3, 2026-08-20).** El frontend de Kepler Talento migró de Angular a React 19 + Vite.
> Los fragmentos en Angular de este documento se conservan como referencia histórica del patrón
> de cliente/servicio; la frontera de backend (cliente Supabase centralizado en
> `src/app/core/supabase/`, servicios en `core/` y `features/*/services/`) no cambia.

---

## 0. Resumen de lo que hay que montar

Backend Supabase con: autenticación (email+password, invitaciones, recuperación, MFA opcional),
base de datos Postgres con **RLS como única barrera de seguridad real** (nunca confiar en checks
de frontend), un sistema de **roles con permisos granulares** multi-empresa/tenant, Edge Functions
para todo lo que necesite `service_role`, **Storage** para adjuntos, un sistema de
**notificaciones in-app + email vía SMTP** desacoplado por outbox+cron, y despliegue
**autohospedado en Docker** (alternativa: Supabase Cloud).

---

## 1. Decisión de plataforma Supabase

**Opción A — Self-host en Docker (la que usa este proyecto):**
Contenedores: `db` (Postgres), `kong` (gateway/API), `auth` (GoTrue), `rest` (PostgREST),
`storage`, `meta`, `functions` (Edge Runtime, Deno), opcional `studio`. **Se omiten** `realtime`,
`analytics/logflare`, `vector`, `supavisor` si la app no los necesita (ahorra RAM; implica que las
notificaciones en vivo se hacen por **sondeo periódico**, no WebSocket).

- Puertos: Kong (API) y frontend en puertos propios; Postgres **nunca** expuesto a Internet.
- `functions` se sirve montando un volumen `./volumes/functions:/home/deno/functions` — cada
  función es una carpeta con un `index.ts`; para desplegar una función nueva basta copiar el
  archivo a esa ruta y `docker restart` del contenedor de functions (no hay
  `supabase functions deploy` real en self-host).
- Migraciones SQL se aplican a mano vía `psql` dentro del contenedor `db` (no hay
  `supabase db push` porque no hay `supabase link` a un proyecto cloud). **Por eso cada migración
  debe ser idempotente** (`CREATE TABLE IF NOT EXISTS`, `DROP POLICY IF EXISTS` antes de
  `CREATE POLICY`, etc.).
- **Gotcha importante**: en self-host, `auth.uid()/role()/email()` vienen de fábrica pero
  **`auth.jwt()` no** — hay que crearla manualmente y su _owner_ debe ser `supabase_auth_admin`.
- **Gotcha importante**: hay que `GRANT SELECT/INSERT/UPDATE/DELETE` explícito a
  `authenticated`/`anon`/`service_role` en cada tabla nueva — si la única migración existente con
  `ALTER DEFAULT PRIVILEGES` no cubre tablas creadas después, PostgREST devuelve vacío/403 sin ni
  siquiera llegar a evaluar RLS. **Incluye siempre el `GRANT` en la misma migración que crea la
  tabla.**

**Opción B — Supabase Cloud:** usar `supabase link` + `supabase db push` normal; Edge Functions
con `supabase functions deploy`; SMTP se configura en el Dashboard (Auth → SMTP Settings) en vez
de variables de entorno del contenedor `auth`.

Decide con el usuario cuál aplica (depende de si quiere depender de un proveedor cloud o mantener
todo en su propio servidor).

---

## 2. Modelo de datos central (réplica para cualquier dominio)

```sql
-- Empresas / tenants (omitir si la app es de un solo tenant)
CREATE TABLE public.companies (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  name TEXT NOT NULL,
  created_at TIMESTAMPTZ DEFAULT now()
);

-- Roles con permisos granulares (array de strings, no un enum cerrado)
CREATE TABLE public.roles (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  name TEXT UNIQUE NOT NULL,           -- nombre EDITABLE por el admin → nunca comparar lógica por nombre, usar flags
  description TEXT,
  permissions TEXT[] DEFAULT '{}',     -- p.ej. 'view_x','create_x','manage_users','export_data'...
  is_system BOOLEAN DEFAULT false,     -- rol protegido (no renombrable/borrable) — ver sección 4
  is_technical BOOLEAN DEFAULT false,  -- flags de dominio: "es de soporte/operación"
  is_manager BOOLEAN DEFAULT false,    -- flag de dominio: "sus usuarios pueden ser responsables"
  all_companies BOOLEAN DEFAULT false, -- acceso a TODOS los tenants (admin global)
  created_at TIMESTAMPTZ DEFAULT now(),
  updated_at TIMESTAMPTZ DEFAULT now()
);

-- Empresas concretas adicionales para un rol que no tiene all_companies
CREATE TABLE public.role_companies (
  role_name TEXT NOT NULL,
  company_id UUID NOT NULL REFERENCES public.companies(id) ON DELETE CASCADE,
  PRIMARY KEY (role_name, company_id)
);

-- Perfil de aplicación, VINCULADO a auth.users POR EMAIL (no por id) ----------
-- Por qué por email: simplifica invitaciones/recuperación donde el id de auth.users
-- puede no conocerse aún en el momento de crear el perfil de negocio.
CREATE TABLE public.profiles (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  email TEXT UNIQUE,
  name TEXT,
  role TEXT DEFAULT 'USER',            -- coincide con roles.name
  company_id UUID REFERENCES public.companies(id) ON DELETE SET NULL,
  active BOOLEAN DEFAULT true,
  is_manager BOOLEAN NOT NULL DEFAULT false,   -- override individual del flag de rol
  manager_id UUID REFERENCES public.profiles(id) ON DELETE SET NULL,
  mfa_required BOOLEAN NOT NULL DEFAULT false, -- el admin puede exigir 2FA a un usuario concreto
  all_companies BOOLEAN NOT NULL DEFAULT false,-- override individual (igual que en roles)
  created_at TIMESTAMPTZ DEFAULT now(),
  updated_at TIMESTAMPTZ DEFAULT now()
);
CREATE TABLE public.profile_companies (       -- empresas adicionales por usuario (override)
  profile_id UUID NOT NULL REFERENCES public.profiles(id) ON DELETE CASCADE,
  company_id UUID NOT NULL REFERENCES public.companies(id) ON DELETE CASCADE,
  PRIMARY KEY (profile_id, company_id)
);
```

**Lista de permisos**: definir como catálogo en el FRONTEND (un array TS `DEFAULT_PERMISSIONS`
agrupado por dominio: `view_x/create_x/edit_x/delete_x/manage_x/view_all_x`), no en la BD — la BD
solo guarda los strings en `roles.permissions`.

---

## 3. Funciones SQL helper de autorización (núcleo de TODO el sistema RLS)

Crear exactamente este catálogo, todas `SECURITY DEFINER STABLE` con `SET search_path = public`,
y usarlas SIEMPRE en las políticas en vez de repetir lógica inline:

```sql
CREATE OR REPLACE FUNCTION public.current_profile_id() RETURNS UUID LANGUAGE sql STABLE SECURITY DEFINER SET search_path = public AS $$
  SELECT id FROM public.profiles WHERE lower(email) = lower(COALESCE(auth.jwt()->>'email','')) LIMIT 1;
$$;

CREATE OR REPLACE FUNCTION public.current_app_role() RETURNS TEXT LANGUAGE sql STABLE SECURITY DEFINER SET search_path = public AS $$
  SELECT role FROM public.profiles WHERE lower(email) = lower(COALESCE(auth.jwt()->>'email','')) LIMIT 1;
$$;

-- ⚠️ NUNCA comparar is_admin()/is_support() por NOMBRE LITERAL del rol (p.ej. name = 'Admin'):
-- el nombre es editable desde la UI de roles y romperá la autorización en cuanto alguien lo
-- renombre. Anclar SIEMPRE a un FLAG estable.
CREATE OR REPLACE FUNCTION public.is_admin() RETURNS BOOLEAN LANGUAGE sql STABLE SECURITY DEFINER SET search_path = public AS $$
  SELECT EXISTS (SELECT 1 FROM public.roles WHERE name = public.current_app_role() AND all_companies = true);
$$;

CREATE OR REPLACE FUNCTION public.is_support() RETURNS BOOLEAN LANGUAGE sql STABLE SECURITY DEFINER SET search_path = public AS $$
  SELECT EXISTS (SELECT 1 FROM public.roles WHERE name = public.current_app_role() AND is_technical = true);
$$;

CREATE OR REPLACE FUNCTION public.has_role_permission(permission_name TEXT) RETURNS BOOLEAN LANGUAGE sql STABLE SECURITY DEFINER SET search_path = public AS $$
  SELECT EXISTS (
    SELECT 1 FROM public.profiles p JOIN public.roles r ON r.name = p.role
    WHERE lower(p.email) = lower(COALESCE(auth.jwt()->>'email',''))
      AND permission_name = ANY(COALESCE(r.permissions,'{}'))
  );
$$;

-- Acceso multi-empresa: ¿puede el usuario actual ver datos de la empresa c?
CREATE OR REPLACE FUNCTION public.can_access_company(c UUID) RETURNS BOOLEAN LANGUAGE sql STABLE SECURITY DEFINER SET search_path = public AS $$
  SELECT c IS NOT NULL AND (
    c = (SELECT company_id FROM public.profiles WHERE lower(email)=lower(COALESCE(auth.jwt()->>'email','')))
    OR public.is_admin()
    OR (SELECT all_companies FROM public.profiles WHERE lower(email)=lower(COALESCE(auth.jwt()->>'email','')))
    OR EXISTS (SELECT 1 FROM public.roles r WHERE r.name = public.current_app_role() AND r.all_companies)
    OR EXISTS (SELECT 1 FROM public.role_companies rc WHERE rc.role_name = public.current_app_role() AND rc.company_id = c)
    OR EXISTS (SELECT 1 FROM public.profile_companies pc WHERE pc.profile_id = public.current_profile_id() AND pc.company_id = c)
  );
$$;

-- "Responsable" (concepto de dominio: rol o perfil marcado como manager)
CREATE OR REPLACE FUNCTION public.is_current_user_manager() RETURNS BOOLEAN LANGUAGE sql STABLE SECURITY DEFINER SET search_path = public AS $$
  SELECT EXISTS (
    SELECT 1 FROM public.profiles p
    WHERE p.id = public.current_profile_id()
      AND (p.is_manager OR EXISTS (SELECT 1 FROM public.roles r WHERE r.name = p.role AND r.is_manager))
  );
$$;
```

**Patrón de política RLS estándar** para CUALQUIER tabla de negocio con `company_id` + permiso
granular:

```sql
ALTER TABLE public.<tabla> ENABLE ROW LEVEL SECURITY;

CREATE POLICY <tabla>_select ON public.<tabla> FOR SELECT TO authenticated
  USING (public.can_access_company(company_id) AND (
    public.is_admin() OR public.has_role_permission('view_all_<recurso>')
    OR created_by = public.current_profile_id()
    OR assigned_to = public.current_profile_id()
  ));

CREATE POLICY <tabla>_insert ON public.<tabla> FOR INSERT TO authenticated
  WITH CHECK (public.can_access_company(company_id) AND public.has_role_permission('create_<recurso>'));

CREATE POLICY <tabla>_update ON public.<tabla> FOR UPDATE TO authenticated
  USING (public.can_access_company(company_id) AND (public.is_admin() OR public.has_role_permission('edit_<recurso>') OR created_by = public.current_profile_id()));

CREATE POLICY <tabla>_delete ON public.<tabla> FOR DELETE TO authenticated
  USING (public.can_access_company(company_id) AND (public.is_admin() OR public.has_role_permission('delete_<recurso>')));
```

Si un recurso necesita **visibilidad restringida a una lista concreta de roles/usuarios** (más
allá del permiso general), añadir columnas `restricted BOOLEAN`, `allowed_role_ids UUID[]`,
`allowed_user_ids UUID[]` y un tercer `AND` en la política:

```sql
AND (NOT restricted OR public.is_admin() OR public.current_profile_id() = ANY(allowed_user_ids)
     OR EXISTS (SELECT 1 FROM public.roles r WHERE r.name = public.current_app_role() AND r.id = ANY(allowed_role_ids)))
```

(Igual que para empresas: si un recurso puede ser "global" o de "varias empresas concretas además
de la propietaria", añade `all_companies BOOLEAN` + tabla puente
`<recurso>_companies(recurso_id, company_id)`.)

**Auditoría** (opcional pero recomendado): trigger `AFTER INSERT/UPDATE/DELETE` `SECURITY
DEFINER` que escribe en una tabla
`audit_events(event_type, entity_type, entity_id, actor_email, actor_role, details jsonb, created_at)`.

---

## 4. Roles inmutables (protección anti-borrado/renombrado accidental)

```sql
CREATE OR REPLACE FUNCTION public.protect_system_roles() RETURNS TRIGGER LANGUAGE plpgsql AS $$
BEGIN
  IF TG_OP = 'DELETE' THEN
    IF OLD.is_system THEN RAISE EXCEPTION 'El rol de sistema "%" no puede eliminarse.', OLD.name; END IF;
    RETURN OLD;
  END IF;
  IF OLD.is_system AND NEW.name <> OLD.name THEN RAISE EXCEPTION 'No se puede renombrar.'; END IF;
  IF OLD.is_system AND NOT NEW.is_system THEN RAISE EXCEPTION 'No se puede quitar el flag de sistema.'; END IF;
  RETURN NEW;
END; $$;
CREATE TRIGGER trg_protect_system_roles BEFORE UPDATE OR DELETE ON public.roles
  FOR EACH ROW EXECUTE FUNCTION public.protect_system_roles();
```

Marcar el rol admin inicial con `is_system = true` tras crearlo.

---

## 5. Edge Functions necesarias (Deno, `jsr:@supabase/supabase-js@2`)

Todas siguen el MISMO esqueleto: CORS + `json()` helper + validar `Authorization` del llamante con
el cliente `anon` + cargar perfil/rol con el cliente `service_role` + autorizar por
`is_admin`-equivalente o permiso + tenant-check (`targetProfile.company_id === callerProfile.company_id`
salvo `all_companies`) + operar con `service_role`.

| Función                                                 | Qué hace                                                                                                            | Notas críticas                                                                                                                                                                                                                           |
| ------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `admin-create-user`                                     | Crea perfil + invita cuenta (`auth.admin.inviteUserByEmail` o `createUser`)                                         | Requiere SMTP configurado en GoTrue para el email de invitación                                                                                                                                                                          |
| `admin-delete-user`                                     | Borra perfil + cuenta de auth                                                                                       | —                                                                                                                                                                                                                                        |
| `admin-import-users`                                    | Alta masiva (CSV)                                                                                                   | Reusar la misma lógica de autorización/tenant que `admin-create-user`                                                                                                                                                                    |
| `admin-reset-mfa`                                       | Borra los factores MFA de un usuario (`auth.admin.mfa.deleteFactor`) vía botón admin                                | —                                                                                                                                                                                                                                        |
| `get-impersonation-token`                               | Genera un JWT HS256 firmado a mano (no via GoTrue) con `sub=<id del usuario objetivo>`, corta duración (1h)         | **Tenant check obligatorio**: si el caller no es admin global, verificar `targetProfile.company_id === callerProfile.company_id` antes de emitir el token. Prohibir impersonar a otro admin global.                                      |
| `user-change-password`                                  | Cambia contraseña con `service_role`, evitando los 401/422 que da GoTrue en sesiones de recovery/invite ya "usadas" | Nivel 1: `getUser()` normal. Nivel 2 (fallback): si GoTrue rechaza, verificar la FIRMA del JWT a mano con `JWT_SECRET` y **comprobar `exp` manualmente** (fallo de seguridad fácil de cometer: aceptar la firma sin comprobar caducidad) |
| `process-outbox`                                        | Ver sección 6                                                                                                       | —                                                                                                                                                                                                                                        |
| Un `*-check`/cron de dominio (SLA, recordatorios, etc.) | Lógica de negocio periódica                                                                                         | Mismo patrón de secreto compartido que `process-outbox`                                                                                                                                                                                  |

Firma JWT manual (usada por `get-impersonation-token` y verificada por `user-change-password`):

```ts
function base64url(input: Uint8Array | string): string {
  /* btoa + reemplazos URL-safe */
}
async function signJWT(payload: Record<string, unknown>, secret: string): Promise<string> {
  const header = base64url(JSON.stringify({ alg: 'HS256', typ: 'JWT' }));
  const body = base64url(JSON.stringify(payload));
  const key = await crypto.subtle.importKey(
    'raw',
    new TextEncoder().encode(secret),
    { name: 'HMAC', hash: 'SHA-256' },
    false,
    ['sign'],
  );
  const sig = await crypto.subtle.sign('HMAC', key, new TextEncoder().encode(`${header}.${body}`));
  return `${header}.${body}.${base64url(sig)}`;
}
```

`JWT_SECRET` (o `SUPABASE_JWT_SECRET`) está disponible automáticamente en las Edge Functions del
proyecto.

---

## 6. SMTP / Email — DOS sistemas independientes que comparten credenciales

### 6.1 Emails de Auth (invitación, recuperación de contraseña, confirmación)

Los gestiona **GoTrue directamente**, no tu código. Configurar en el contenedor `auth` (self-host)
o en el Dashboard (cloud):

```env
SMTP_ADMIN_EMAIL=no-reply@tu-dominio.com
SMTP_HOST=smtp.tu-proveedor.com
SMTP_PORT=465
SMTP_USER=no-reply@tu-dominio.com
SMTP_PASS=<password>
SMTP_SENDER_NAME=TuApp
MAILER_URLPATHS_CONFIRMATION=/auth/v1/verify
MAILER_URLPATHS_INVITE=/auth/v1/verify
MAILER_URLPATHS_RECOVERY=/auth/v1/verify
MAILER_URLPATHS_EMAIL_CHANGE=/auth/v1/verify
```

El frontend dispara estos flujos con `supabase.auth.inviteUserByEmail()` (vía Edge Function
admin), `supabase.auth.resetPasswordForEmail(email, { redirectTo: '<origin>/change-password' })`,
etc. — GoTrue envía el correo, no tu backend.

### 6.2 Emails/notificaciones de la APLICACIÓN (ticket creado, comentario nuevo...) — patrón Outbox + Cron

**No envíes el email directamente desde el cliente ni en el momento del evento** (acoplaría la UX
a la disponibilidad del SMTP). En su lugar:

```sql
CREATE TABLE public.email_outbox (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  recipient TEXT NOT NULL,
  subject TEXT NOT NULL,
  body TEXT NOT NULL,           -- HTML
  created_at TIMESTAMPTZ DEFAULT now(),
  processed_at TIMESTAMPTZ      -- NULL = pendiente
);
CREATE TABLE public.webhook_outbox ( -- opcional, mismo patrón para integraciones externas
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  event TEXT NOT NULL, payload JSONB NOT NULL DEFAULT '{}',
  created_at TIMESTAMPTZ DEFAULT now(), processed_at TIMESTAMPTZ
);
CREATE TABLE public.notifications ( -- notificación IN-APP (campana), independiente del email
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  user_id UUID REFERENCES public.profiles(id) ON DELETE CASCADE,
  type TEXT CHECK (type IN ('info','success','warning','error')),
  message TEXT NOT NULL, link TEXT, read BOOLEAN DEFAULT false,
  created_at TIMESTAMPTZ DEFAULT now()
);
```

1. Cuando ocurre el evento de negocio, el código de la app (vía `NotificationService`) hace DOS
   cosas: `INSERT` en `notifications` (para la campana) **e** `INSERT` en `email_outbox` (cuerpo
   ya renderizado en HTML, con el texto final — nunca un "key" de plantilla para traducir después,
   porque ese texto queda congelado en BD para siempre; renderízalo ya en el idioma/formato final).
2. Una Edge Function `process-outbox` (igual que la de la tabla anterior), invocada por **cron
   cada minuto**, lee filas con `processed_at IS NULL`, las envía por SMTP (usa el mismo
   `SMTP_HOST/PORT/USER/PASS` que GoTrue — **un único proveedor SMTP sirve ambos sistemas**)
   **abriendo un cliente SMTP nuevo por email** (evita estado corrupto del cliente tras un fallo) y
   marca `processed_at = now()`.
3. Reglas de reintento: si el error de envío es 5xx (permanente, p.ej. dirección inválida) marca
   `processed_at` igualmente para no reintentar infinito; si es transitorio, deja la fila pendiente
   para el siguiente tick de cron.
4. La función se protege con un **secreto compartido por cabecera** (`x-outbox-secret`), NO con JWT
   de usuario — se despliega con `--no-verify-jwt` porque la llama un cron, no un usuario.
5. **Cron real** (self-host): crontab del SO del servidor (NO pg_cron, para no añadir esa
   extensión):
   ```
   * * * * * curl -s -X POST http://127.0.0.1:<puerto-kong>/functions/v1/process-outbox -H "x-outbox-secret: <secreto>" >/dev/null 2>&1
   ```
   (En Supabase Cloud: usar "Scheduled Edge Functions" o un cron externo que llame a la URL
   pública con el mismo header.)

### 6.3 Notificaciones del navegador (sin WebSocket/Realtime)

Como el self-host normalmente NO lleva el contenedor `realtime`, las notificaciones en vivo se
hacen por **sondeo del cliente** (`setInterval` ~45s) contra la tabla `notifications`, comparando
los ids ya vistos (un `Set` en memoria) para no re-notificar. Si hay una fila nueva no vista y
`Notification.permission === 'granted'`, disparar `new Notification(title, { body, tag: id })` con
`onclick` que enfoque la ventana y navegue al `link`. Pedir el permiso con un botón explícito
(`Notification.requestPermission()`), nunca automáticamente al cargar.

---

## 7. MFA (TOTP) — opcional pero recomendado si hay roles admin

- `supabase auth mfa` ya viene de fábrica en GoTrue
  (`enroll/challenge/verify/listFactors/unenroll`), expón wrappers desde tu `SupabaseService`.
- Flujo: el USUARIO decide activarlo desde su perfil (`/security`): `enroll('totp')` → muestra QR
  (`data.totp.qr_code`) → el usuario introduce el código de 6 dígitos → `challenge` + `verify`.
- _Step-up_ en login: un guard de ruta llama a `getAuthenticatorAssuranceLevel()`; si
  `nextLevel==='aal2' && currentLevel!=='aal2'`, redirige a una pantalla de verificación de 6
  dígitos antes de dejar pasar.
- `profiles.mfa_required`: el admin puede marcarlo desde la gestión de usuarios; un guard
  adicional fuerza al usuario a `/security` si tiene `mfa_required=true` y aún no activó MFA.
  **RLS**: el propio usuario NO puede poner su `mfa_required` a `false` (rescribir la política
  `UPDATE` de `profiles` para que el `WITH CHECK` fije siempre el valor autoritativo de
  `mfa_required`, no el que venga en el payload del cliente).
- Reset por admin: Edge Function `admin-reset-mfa` (sección 5) — útil cuando el usuario pierde el
  dispositivo.

---

## 8. Storage (adjuntos)

- Un bucket por tipo de adjunto (p.ej. `ticket-attachments`).
- Política de storage equivalente a las de tabla: una función
  `can_access_<recurso>_path(name TEXT)` que extrae el id del recurso del PRIMER segmento del
  `name` del objeto (`name` en Storage es `<recurso_id>/<archivo>`) y reutiliza el mismo
  `can_access_<recurso>` ya definido para la tabla, para que el control de acceso a archivos sea
  CONSISTENTE con el de la fila a la que pertenecen.

---

## 9. Capa de servicios del frontend (independiente del framework, pero así es como se hizo en Angular)

- **Un único `SupabaseService`** que crea el `SupabaseClient` (`createClient(url, anonKey)`) y
  expone wrappers tipados para auth/mfa — el resto de la app nunca llama a `supabase-js`
  directamente.
- **Config en runtime, no en build**: un `env.template.js` con placeholders
  `${SUPABASE_URL}`/`${SUPABASE_ANON_KEY}` que un `entrypoint.sh` del contenedor frontend rellena
  con `envsubst` al arrancar (así la MISMA imagen Docker sirve cualquier entorno sin reconstruir).
  El bundle de JS lee `window.__APP_CONFIG__` en vez de variables de Angular `environment.ts`
  baked-in.
- **Un servicio por entidad** (`TicketService`, `UserService`...) que: (a) mapea snake_case de la
  fila de Postgres ↔ camelCase del modelo TS, (b) NUNCA filtra "por si acaso" en el cliente cosas
  que ya decide RLS — si lo hace, **duplica** la lógica de seguridad y puede acabar siendo MÁS
  restrictivo que RLS, ocultando datos que el usuario sí debería ver (bug real encontrado y
  corregido en este proyecto más de una vez).
- **Carrera de arranque**: `currentUser()` se resuelve de forma asíncrona
  (`onAuthStateChange`). Cualquier guard de ruta o query con alcance por usuario debe
  `await Promise.all([whenPermissionsReady(), whenUserReady()])` antes de leer el
  usuario/permisos — si solo esperas uno de los dos, hay una ventana donde un guard puede denegar
  acceso a alguien que SÍ tiene permiso, simplemente porque el perfil aún no cargó (bug real,
  afecta sobre todo a navegación "en frío"/recarga de página, no a clicks dentro de la app ya
  cargada).
- **Impersonación** (si aplica): el cliente normal sigue autenticado como el admin; se crea un
  SEGUNDO `SupabaseClient` con `Authorization: Bearer <jwt-de-impersonacion>` y `storageKey`
  distinto (evita el warning "Multiple GoTrueClient instances") SOLO para las consultas de datos
  con alcance del usuario impersonado; las llamadas a Edge Functions de administración deben
  seguir usando el cliente REAL del admin (si no, perderías privilegios a mitad de la sesión de
  impersonación).

---

## 10. Plan de implementación sugerido (orden)

1. Levantar el proyecto Supabase (self-host o cloud) — sin datos de negocio aún.
2. Migración base: `companies`, `roles`, `role_companies`, `profiles`, `profile_companies` +
   funciones helper de la sección 3 + RLS de esas mismas tablas.
3. Semilla: una empresa, un rol admin (`is_system=true`, `all_companies=true`, todos los permisos)
   y un usuario admin (perfil + invitación).
4. Migrar las tablas de negocio de tu dominio actual a Postgres, aplicando el patrón RLS de la
   sección 3 a cada una.
5. `SupabaseService` + servicio de auth (login, logout, `whenUserReady`/`whenPermissionsReady`,
   `resetPasswordForEmail`) en el frontend.
6. Edge Functions de gestión de usuarios (`admin-create-user`, `admin-delete-user`) — sin esto no
   puedes dar de alta gente sin tocar la BD a mano.
7. SMTP: configurar GoTrue (invitaciones/recuperación funcionando de extremo a extremo) ANTES de
   construir el outbox de notificaciones de negocio.
8. `email_outbox`/`notifications` + `process-outbox` + cron.
9. Notificaciones de navegador (capa fina sobre lo anterior).
10. MFA, impersonación, import masivo — en este orden de prioridad/complejidad creciente, si los
    necesitas.
11. Migrar el resto de pantallas, conectando cada una a su tabla con RLS ya probada por separado
    (escribe un script de smoke-test que inicie sesión como dos usuarios de empresas distintas y
    verifique aislamiento — es el riesgo nº1 de cualquier app multi-tenant).

---

## 11. Secretos/variables de entorno a preparar

```
SUPABASE_URL=...
SUPABASE_ANON_KEY=...
SUPABASE_SERVICE_ROLE_KEY=...   (solo en Edge Functions/backend, NUNCA en el frontend)
SUPABASE_JWT_SECRET=...         (firmar/verificar JWT a mano si hay impersonación)
SMTP_HOST / SMTP_PORT / SMTP_USER / SMTP_PASS / SMTP_SENDER_NAME / SMTP_ADMIN_EMAIL
OUTBOX_SECRET=<cadena aleatoria larga>   (protege process-outbox de invocación pública)
OUTBOX_FROM_EMAIL=no-reply@tu-dominio
WEBHOOK_ENDPOINT_URL=...        (opcional)
```

---

Dale este documento al agente junto con: el nombre real de las entidades de tu dominio (para
sustituir `<recurso>`/`<tabla>` por las tablas reales), y si la app es multi-tenant o de una sola
empresa (simplifica mucho si es de una sola: te puedes saltar `companies`/`can_access_company` por
completo).
