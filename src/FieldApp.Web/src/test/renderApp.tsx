import { render } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { createMemoryRouter, RouterProvider } from 'react-router';
import { routes } from '../routes';
import { personaStorageKey, SessionProvider } from '../session/SessionProvider';

/** Renders the whole app at a route, optionally as a development persona. */
export function renderApp(path: string, persona: string | null = 'superintendent') {
  if (persona) {
    window.localStorage.setItem(personaStorageKey, persona);
  }

  const router = createMemoryRouter(routes, { initialEntries: [path] });
  const user = userEvent.setup();
  const view = render(
    <SessionProvider>
      <RouterProvider router={router} />
    </SessionProvider>,
  );

  return { ...view, user, router };
}
