import { screen, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { installFakeApi } from '../test/fakeApi';
import { renderApp } from '../test/renderApp';

async function openCapture() {
  installFakeApi();
  const view = renderApp('/projects/p-001/new-item');
  await screen.findByRole('heading', { level: 1, name: 'New item' });
  await screen.findByRole('button', { name: /Choose area/ });
  return view;
}

function summaryValue(term: string): string | null {
  const summary = screen.getByRole('region', { name: 'Selections' });
  const dt = within(summary).getByText(term, { selector: 'dt' });
  return dt.nextElementSibling?.textContent ?? null;
}

describe('capture selection', () => {
  it('presents Project, Area, Trade, Type and Camera in order, with the project locked', async () => {
    await openCapture();

    expect(screen.getByRole('region', { name: 'Project' })).toHaveTextContent('Mos Eisley Municipal Center');
    expect(screen.getByRole('link', { name: /Change project/ })).toHaveAttribute('href', '/projects');

    const inOrder = [
      screen.getByRole('region', { name: 'Project' }),
      screen.getByRole('region', { name: 'Area' }),
      screen.getByRole('region', { name: 'Trade' }),
      screen.getByRole('group', { name: 'Type' }),
      screen.getByRole('region', { name: 'Selections' }),
      screen.getByRole('button', { name: /Take photo/ }),
    ];
    for (let index = 1; index < inOrder.length; index++) {
      const previous = inOrder[index - 1];
      const current = inOrder[index];
      if (!previous || !current) throw new Error('missing element');
      expect(previous.compareDocumentPosition(current) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    }
  });

  it('selects an area from the hierarchical list without typing, showing the full path', async () => {
    const { user } = await openCapture();

    await user.click(screen.getByRole('button', { name: /Choose area/ }));
    const sheet = screen.getByRole('dialog', { name: 'Choose area' });
    await user.click(within(sheet).getByRole('button', { name: 'Building A / Level 2 / Office 201' }));

    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    expect(
      screen.getByRole('button', { name: /Area: Building A \/ Level 2 \/ Office 201/ }),
    ).toBeInTheDocument();
    expect(summaryValue('Area')).toBe('Building A / Level 2 / Office 201');
  });

  it('filters areas by every search term', async () => {
    const { user } = await openCapture();

    await user.click(screen.getByRole('button', { name: /Choose area/ }));
    const sheet = screen.getByRole('dialog', { name: 'Choose area' });
    await user.type(within(sheet).getByRole('searchbox', { name: /Search areas/ }), 'level 2 office');

    expect(within(sheet).getByRole('status')).toHaveTextContent('2 areas match');
    expect(
      within(sheet)
        .getAllByRole('button', { name: /Office/ })
        .map((button) => button.textContent),
    ).toEqual(['Building A / Level 2 / Office 201', 'Building A / Level 2 / Office 202']);
  });

  it('offers recently used areas first on the next capture', async () => {
    const { user, unmount } = await openCapture();
    await user.click(screen.getByRole('button', { name: /Choose area/ }));
    await user.click(
      within(screen.getByRole('dialog')).getByRole('button', { name: 'Building A / Level 2 / Office 201' }),
    );
    unmount();

    await openCapture();

    const recent = screen.getByRole('group', { name: 'Recent areas' });
    expect(
      within(recent).getByRole('button', { name: 'Building A / Level 2 / Office 201' }),
    ).toBeInTheDocument();
  });

  it('shows the responsible company immediately after a trade is chosen, with no company picker', async () => {
    const { user } = await openCapture();

    const drywall = screen.getByRole('button', { name: 'Drywall' });
    await user.click(drywall);

    expect(screen.getByRole('button', { name: 'Drywall' })).toHaveAttribute('aria-pressed', 'true');
    expect(screen.getByText('Responsible company', { selector: '.responsible__label' })).toBeInTheDocument();
    expect(screen.getByText('Dune Sea Drywall Co.', { selector: '.responsible__value' })).toBeInTheDocument();
    expect(summaryValue('Responsible company')).toBe('Dune Sea Drywall Co.');
    expect(screen.queryByRole('combobox')).not.toBeInTheDocument();
    expect(screen.queryByText(/choose (a )?(company|subcontractor)/i)).not.toBeInTheDocument();
  });

  it('lists every trade with its company under View all', async () => {
    const { user } = await openCapture();

    await user.click(screen.getByRole('button', { name: 'View all trades (7)' }));
    const sheet = screen.getByRole('dialog', { name: 'Choose trade' });
    expect(within(sheet).getAllByRole('button', { pressed: false })).toHaveLength(7);

    await user.click(within(sheet).getByRole('button', { name: /Plumbing/ }));

    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    expect(summaryValue('Trade')).toBe('Plumbing');
    expect(summaryValue('Responsible company')).toBe("Beggar's Canyon Plumbing");
  });

  it('explains when no trades are available for capture', async () => {
    installFakeApi({ trades: [] });
    renderApp('/projects/p-001/new-item');

    expect(await screen.findByText(/No trades are set up for capture/)).toBeInTheDocument();
  });

  it('chooses exactly one of Observation or Punch List', async () => {
    const { user } = await openCapture();
    const type = screen.getByRole('group', { name: /Type/ });

    expect(
      within(type)
        .getAllByRole('radio')
        .map((radio) => radio.closest('label')?.textContent),
    ).toEqual(['Observation', 'Punch List']);

    await user.click(within(type).getByRole('radio', { name: 'Punch List' }));

    expect(within(type).getByRole('radio', { name: 'Punch List' })).toBeChecked();
    expect(within(type).getByRole('radio', { name: 'Observation' })).not.toBeChecked();
    expect(summaryValue('Type')).toBe('Punch List');
  });

  it('keeps the camera as a disabled boundary before and after the selections are complete', async () => {
    const { user } = await openCapture();
    const camera = screen.getByRole('button', { name: /Take photo/ });

    expect(camera).toHaveAttribute('aria-disabled', 'true');
    expect(camera).toHaveAccessibleDescription('Choose an area, trade and type first.');

    await user.click(screen.getByRole('button', { name: /Choose area/ }));
    await user.click(
      within(screen.getByRole('dialog')).getByRole('button', { name: 'Building A / Level 2 / Office 201' }),
    );
    await user.click(screen.getByRole('button', { name: 'Drywall' }));
    await user.click(screen.getByRole('radio', { name: 'Punch List' }));

    expect(camera).toHaveAttribute('aria-disabled', 'true');
    expect(camera).toHaveAccessibleDescription(/Selections complete. Photo capture is not available yet/);
    expect(summaryValue('Area')).toBe('Building A / Level 2 / Office 201');
    expect(summaryValue('Trade')).toBe('Drywall');
    expect(summaryValue('Responsible company')).toBe('Dune Sea Drywall Co.');
    expect(summaryValue('Type')).toBe('Punch List');
  });

  it('clears all selections with one tap', async () => {
    const { user } = await openCapture();
    await user.click(screen.getByRole('button', { name: 'Drywall' }));
    await user.click(screen.getByRole('radio', { name: 'Observation' }));

    await user.click(screen.getByRole('button', { name: 'Clear all' }));

    expect(summaryValue('Trade')).toBe('Not selected');
    expect(summaryValue('Type')).toBe('Not selected');
    expect(screen.getByRole('radio', { name: 'Observation' })).not.toBeChecked();
  });

  it('shows Project not found for an inaccessible project', async () => {
    installFakeApi();
    renderApp('/projects/p-999/new-item');

    expect(await screen.findByRole('heading', { level: 1, name: 'Project not found' })).toBeInTheDocument();
  });
});
