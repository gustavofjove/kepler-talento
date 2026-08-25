For additional context about technologies to be used, project structure,
shell commands, and other important information, read the design documentation
at specs/001-gestion-cvs-rrhh/plan.md and the standing project principles in
openspec/config.yaml.

Planned and in-flight work lives under openspec/changes/. Use the OpenSpec
workflow (/opsx:new, /opsx:continue, /opsx:apply, /opsx:archive) rather than
editing those artifacts by hand.

Frontend conventions (since KTL-3, the Angular to React migration):

- Pages and components are React function components under src/app/features/**
  and src/app/core/**, written as .tsx with co-located plain .css files. Do not
  use CSS Modules - several Playwright specs bind to class names such as
  a.skip-link and span.badge.
- Shared state lives in plain singleton service classes that hold a signal from
  src/app/core/state/signal.ts. Components subscribe with useSignal(). Pass the
  signal itself, never a derived call such as service.list(), or React will loop
  on getSnapshot. Derive with useMemo instead.
- Services are wired explicitly in src/app/core/di/services.ts and reached
  through useServices(). Tests swap doubles in with <ServicesProvider>.
- If a service is READ during render, reach it through its subscribing hook -
  useCatalogs(), useCandidates(), or usePermission() for authorisation checks -
  not through useServices(). Those hooks subscribe and return the value
  together. Taking the service from useServices() and calling a read method on
  it renders correctly once and then silently stops updating, with no error and
  no failing test. A new service that is read during render should get the same
  kind of hook.
- Never call authService.hasPermission() directly in a component. Use
  usePermission('...') at the top of the component. Because it is a hook it
  cannot be called inside a loop or a callback, so hoist the result to a const.
- Report errors with useErrorToast() rather than hand-rolling
  `error instanceof Error ? error.message : fallback`.
- Use the .span-all utility class instead of inline
  style={{ gridColumn: '1 / -1' }}; inline styles bypass the Kepler tokens.
- Keep pure helpers and constants in a sibling .ts file rather than exporting
  them from a .tsx component module, so fast refresh keeps working.
- Route guards are layout-route elements (RequireAuth, RequirePermission), not
  loaders, so they can subscribe to the auth signal and stay test-swappable.
- Forms are controlled components using useState. Validation stays in the
  service layer as thrown Errors with Spanish messages; do not move it into the
  components or introduce a schema library.
- Do not reintroduce Angular idioms, RxJS, or a state-management library. Any
  new runtime dependency needs an explicit reason under principle 2.
- Keep the name= attribute on every form control and every data-testid. They are
  decorative in React but load-bearing for the Playwright suite.
