import { screen, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { installFakeApi } from './test/fakeApi';
import { renderApp } from './test/renderApp';

describe('session and navigation', () => {
  it('asks for a development persona when none is chosen, then shows that persona’s projects', async () => {
    installFakeApi();
    const { user } = renderApp('/projects', null);

    expect(
      await screen.findByRole('heading', { level: 1, name: 'Choose a development persona' }),
    ).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: /Owen Lars/ }));

    expect(await screen.findByRole('heading', { level: 1, name: 'Projects' })).toBeInTheDocument();
    const list = await screen.findByRole('list', { name: 'Your projects' });
    expect(
      within(list)
        .getAllByRole('link')
        .map((link) => link.textContent),
    ).toEqual([
      expect.stringContaining('Mos Eisley Municipal Center'),
      expect.stringContaining('Anchorhead Water Treatment Plant'),
    ]);
  });

  it('shows only the projects the persona is authorized for', async () => {
    installFakeApi();
    renderApp('/projects', 'project-manager');

    const list = await screen.findByRole('list', { name: 'Your projects' });
    expect(within(list).getAllByRole('link')).toHaveLength(1);
    expect(within(list).queryByText('Anchorhead Water Treatment Plant')).not.toBeInTheDocument();
  });

  it('explains that sign-in is unavailable when development personas are not offered', async () => {
    installFakeApi({ devPersonas: false });
    renderApp('/projects', null);

    expect(await screen.findByRole('heading', { level: 1, name: 'Sign-in unavailable' })).toBeInTheDocument();
  });

  it('renders the phone bottom navigation with Projects, Items and More', async () => {
    installFakeApi();
    renderApp('/projects');

    const nav = await screen.findByRole('navigation', { name: 'Main' });
    expect(
      within(nav)
        .getAllByRole('link')
        .map((link) => link.textContent),
    ).toEqual(['Projects', 'Items', 'More']);
    expect(within(nav).getByRole('link', { name: 'Projects' })).toHaveAttribute('aria-current', 'page');
  });

  it('switches persona from More', async () => {
    installFakeApi();
    const { user } = renderApp('/more');

    expect(await screen.findByText('Owen Lars (Superintendent)')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: /Beru Whitesun/ }));

    expect(await screen.findByRole('heading', { level: 1, name: 'Projects' })).toBeInTheDocument();
    const list = await screen.findByRole('list', { name: 'Your projects' });
    expect(within(list).getAllByRole('link')).toHaveLength(1);
  });

  it('opens a project and offers New Item', async () => {
    installFakeApi();
    const { user } = renderApp('/projects');

    await user.click(await screen.findByRole('link', { name: /Mos Eisley Municipal Center/ }));

    expect(
      await screen.findByRole('heading', { level: 1, name: 'Mos Eisley Municipal Center' }),
    ).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'New Item' })).toHaveAttribute(
      'href',
      '/projects/p-001/new-item',
    );
  });

  it('does not reveal whether an inaccessible project exists', async () => {
    installFakeApi();
    renderApp('/projects/p-003');

    expect(await screen.findByRole('heading', { level: 1, name: 'Project not found' })).toBeInTheDocument();
    expect(
      screen.getByText('This project does not exist or you do not have access to it.'),
    ).toBeInTheDocument();
  });
});
