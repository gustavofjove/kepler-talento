/**
 * Navigation seam for services that need to redirect from outside React.
 *
 * `AuthService.signOut()` used to depend on Angular's `Router`. Keeping a narrow
 * local interface means the unit spec can still stub `{ navigateByUrl: vi.fn() }`
 * unchanged, and no service has to import the router.
 */
export interface AppNavigator {
  navigateByUrl(url: string): void | Promise<unknown>;
}

interface NavigableRouter {
  navigate(to: string): void | Promise<unknown>;
}

/** Backed by the data router, which is only available after it is created. */
export class DataRouterNavigator implements AppNavigator {
  private router: NavigableRouter | null = null;

  attach(router: NavigableRouter): void {
    this.router = router;
  }

  navigateByUrl(url: string): void | Promise<unknown> {
    return this.router?.navigate(url);
  }
}
