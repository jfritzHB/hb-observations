import { screen, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { installFakeApi } from '../test/fakeApi';
import { renderApp } from '../test/renderApp';

async function openCapture(state?: unknown) {
  const fetchMock = installFakeApi();
  const view = renderApp('/projects/p-001/new-item', 'superintendent', state);
  // The loading state also shows the heading, so wait for the loaded capture controls.
  await screen.findByRole('combobox', { name: 'Where?' });
  return { ...view, fetchMock };
}

const where = () => screen.getByRole('combobox', { name: 'Where?' });
const camera = () => screen.getByRole('button', { name: /Take photo/ });

describe('rapid capture', () => {
  it('shows a compact project context, then Where, Trade, Type and Take photo, with no step numbers or summary', async () => {
    await openCapture();

    expect(screen.getByText('Mos Eisley Municipal Center')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /Change project/ })).toHaveAttribute('href', '/projects');
    expect(screen.queryByRole('region', { name: 'Selections' })).not.toBeInTheDocument();
    expect(document.querySelector('.capture-step__number')).toBeNull();

    const inOrder = [
      where(),
      screen.getByRole('group', { name: 'Trade' }),
      screen.getByRole('group', { name: 'Item type' }),
      camera(),
    ];
    for (let index = 1; index < inOrder.length; index++) {
      const previous = inOrder[index - 1];
      const current = inOrder[index];
      if (!previous || !current) throw new Error('missing element');
      expect(previous.compareDocumentPosition(current) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    }
  });

  it('searches structured areas as the user types and announces the matches', async () => {
    const { user } = await openCapture();

    await user.type(where(), 'office');

    expect(where()).toHaveAttribute('aria-expanded', 'true');
    const options = within(screen.getByRole('listbox', { name: 'Matching areas' })).getAllByRole('option');
    expect(options.map((option) => option.textContent)).toEqual([
      'Building A / Level 2 / Office 201',
      'Building A / Level 2 / Office 202',
    ]);
    expect(screen.getByText(/2 matching areas/)).toBeInTheDocument();
  });

  it('selects a structured area, shows its full path, and clears the search text', async () => {
    const { user } = await openCapture();

    await user.type(where(), '201');
    await user.click(screen.getByRole('option', { name: 'Building A / Level 2 / Office 201' }));

    expect(
      screen.getByText('Building A / Level 2 / Office 201', { selector: '.location-area__path' }),
    ).toBeInTheDocument();
    expect(
      screen.getByRole('button', { name: 'Remove area Building A / Level 2 / Office 201' }),
    ).toBeInTheDocument();
    expect(where()).toHaveValue('');
    expect(where()).toHaveAttribute('placeholder', 'Add location detail (optional)');
    expect(where()).toHaveAttribute('aria-expanded', 'false');
  });

  it('combines a structured area with a location detail', async () => {
    const { user } = await openCapture();

    await user.type(where(), '201');
    await user.click(screen.getByRole('option', { name: 'Building A / Level 2 / Office 201' }));
    await user.type(where(), 'North wall');

    expect(where()).toHaveValue('North wall');
    expect(where()).toHaveAttribute('aria-expanded', 'false');
    expect(
      screen.getByText('Building A / Level 2 / Office 201', { selector: '.location-area__path' }),
    ).toBeInTheDocument();
  });

  it('accepts a location detail with no structured area and creates no area', async () => {
    const { user, fetchMock } = await openCapture();

    await user.type(where(), 'Unit 214');
    await user.click(screen.getByRole('button', { name: 'Drywall' }));
    await user.click(screen.getByRole('radio', { name: 'Punch List' }));

    expect(where()).toHaveValue('Unit 214');
    expect(document.querySelector('.location-area')).toBeNull();
    expect(camera()).toHaveAttribute('aria-disabled', 'false');
    const writes = fetchMock.mock.calls.filter(([, init]) => (init?.method ?? 'GET') !== 'GET');
    expect(writes).toEqual([]);
  });

  it('limits location detail to 120 characters', async () => {
    await openCapture();

    expect(where()).toHaveAttribute('maxLength', '120');
  });

  it('is fully keyboard operable: arrows move through results, Enter selects, Escape closes', async () => {
    const { user } = await openCapture();

    await user.click(where());
    await user.keyboard('office');
    await user.keyboard('{ArrowDown}{ArrowDown}');
    const second = screen.getByRole('option', { name: 'Building A / Level 2 / Office 202' });
    expect(second).toHaveAttribute('aria-selected', 'true');
    expect(where()).toHaveAttribute('aria-activedescendant', second.id);

    await user.keyboard('{Escape}');
    expect(where()).toHaveAttribute('aria-expanded', 'false');
    expect(where()).toHaveValue('office');

    await user.keyboard('{ArrowDown}{Enter}');
    expect(
      screen.getByText('Building A / Level 2 / Office 201', { selector: '.location-area__path' }),
    ).toBeInTheDocument();
    expect(where()).toHaveFocus();
  });

  it('never converts typed text into a structured area, even on an exact name match', async () => {
    const { user } = await openCapture();

    await user.type(where(), 'Lobby');
    expect(screen.getByRole('option', { name: 'Building A / Level 1 / Lobby' })).toBeInTheDocument();
    await user.keyboard('{Enter}'); // No option was chosen, so Enter keeps the text.
    await user.tab();

    expect(document.querySelector('.location-area')).toBeNull();
    expect(where()).toHaveValue('Lobby');
  });

  it('labels results as Replace area once an area is selected, and replaces it when one is tapped', async () => {
    const { user } = await openCapture();
    await user.type(where(), '201');
    await user.click(screen.getByRole('option', { name: 'Building A / Level 2 / Office 201' }));

    await user.type(where(), 'lobby');

    expect(screen.queryByRole('listbox', { name: 'Matching areas' })).not.toBeInTheDocument();
    const replace = screen.getByRole('listbox', { name: 'Replace area' });
    expect(screen.getByText('Replace area with')).toBeVisible();
    expect(screen.getByText(/Choosing one replaces Office 201/)).toBeInTheDocument();

    await user.click(within(replace).getByRole('option', { name: 'Building A / Level 1 / Lobby' }));

    expect(
      screen.getByText('Building A / Level 1 / Lobby', { selector: '.location-area__path' }),
    ).toBeInTheDocument();
    expect(
      screen.queryByText('Building A / Level 2 / Office 201', { selector: '.location-area__path' }),
    ).toBeNull();
    expect(where()).toHaveValue('');
  });

  it('removes a selected area in one tap and keeps the detail', async () => {
    const { user } = await openCapture();
    await user.type(where(), '201');
    await user.click(screen.getByRole('option', { name: 'Building A / Level 2 / Office 201' }));
    await user.type(where(), 'North wall');

    await user.click(screen.getByRole('button', { name: /Remove area/ }));

    expect(document.querySelector('.location-area')).toBeNull();
    expect(where()).toHaveValue('North wall');
  });

  it('browses the area hierarchy without typing, including non-leaf areas', async () => {
    const { user } = await openCapture();

    await user.click(screen.getByRole('button', { name: 'Browse areas' }));
    await user.click(
      within(screen.getByRole('dialog', { name: 'Browse areas' })).getByRole('button', {
        name: 'Building A / Level 2',
      }),
    );

    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    expect(
      screen.getByText('Building A / Level 2', { selector: '.location-area__path' }),
    ).toBeInTheDocument();
  });

  it('offers recent locations, including area plus detail, as one-tap chips next time', async () => {
    const first = await openCapture();
    await first.user.type(where(), '201');
    await first.user.click(screen.getByRole('option', { name: 'Building A / Level 2 / Office 201' }));
    await first.user.type(where(), 'North wall');
    await first.user.tab();
    first.unmount();

    const { user } = await openCapture();
    const recent = screen.getByRole('group', { name: 'Recent locations' });
    await user.click(
      within(recent).getByRole('button', { name: 'Building A / Level 2 / Office 201, North wall' }),
    );

    expect(
      screen.getByText('Building A / Level 2 / Office 201', { selector: '.location-area__path' }),
    ).toBeInTheDocument();
    expect(where()).toHaveValue('North wall');
  });

  it('shows five trade chips plus More, and the responsible company once a trade is chosen', async () => {
    const { user } = await openCapture();
    const tradeGroup = screen.getByRole('group', { name: 'Trade' });

    expect(within(tradeGroup).getAllByRole('button')).toHaveLength(6);
    expect(screen.queryByText(/Responsible:/)).not.toBeInTheDocument();

    await user.click(within(tradeGroup).getByRole('button', { name: 'Drywall' }));

    expect(within(tradeGroup).getByRole('button', { name: 'Drywall' })).toHaveAttribute(
      'aria-pressed',
      'true',
    );
    expect(screen.getByText('Dune Sea Drywall Co.')).toBeInTheDocument();
    expect(screen.queryByRole('combobox', { name: /company|subcontractor/i })).not.toBeInTheDocument();
  });

  it('lists every trade with its company under More', async () => {
    const { user } = await openCapture();

    await user.click(screen.getByRole('button', { name: 'More trades (7 total)' }));
    const sheet = screen.getByRole('dialog', { name: 'All trades' });
    await user.click(within(sheet).getByRole('button', { name: /Plumbing/ }));

    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Plumbing' })).toHaveAttribute('aria-pressed', 'true');
    expect(screen.getByText("Beggar's Canyon Plumbing")).toBeInTheDocument();
  });

  it('chooses exactly one of Observation or Punch List', async () => {
    const { user } = await openCapture();

    await user.click(screen.getByRole('radio', { name: 'Punch List' }));

    expect(screen.getByRole('radio', { name: 'Punch List' })).toBeChecked();
    expect(screen.getByRole('radio', { name: 'Observation' })).not.toBeChecked();
  });

  it('activates Take photo only once where, trade and type are set', async () => {
    const { user } = await openCapture();

    expect(camera()).toHaveAttribute('aria-disabled', 'true');
    expect(camera()).not.toHaveClass('camera-button--ready');
    expect(camera()).toHaveAccessibleDescription('Set where, trade and type');
    expect(screen.queryByRole('button', { name: 'Choose existing photo' })).not.toBeInTheDocument();

    await user.type(where(), '201');
    await user.click(screen.getByRole('option', { name: 'Building A / Level 2 / Office 201' }));
    await user.click(screen.getByRole('button', { name: 'Drywall' }));
    await user.click(screen.getByRole('radio', { name: 'Punch List' }));

    expect(camera()).toHaveAttribute('aria-disabled', 'false');
    expect(camera()).toHaveClass('camera-button--ready');
    expect(screen.getByRole('button', { name: 'Choose existing photo' })).toBeInTheDocument();
  });

  it('clears everything in one tap', async () => {
    const { user } = await openCapture();
    await user.type(where(), 'Unit 214');
    await user.click(screen.getByRole('button', { name: 'Drywall' }));
    await user.click(screen.getByRole('radio', { name: 'Observation' }));

    await user.click(screen.getByRole('button', { name: 'Clear' }));

    expect(where()).toHaveValue('');
    expect(screen.getByRole('button', { name: 'Drywall' })).toHaveAttribute('aria-pressed', 'false');
    expect(screen.getByRole('radio', { name: 'Observation' })).not.toBeChecked();
  });

  it('starts from a retained capture context, as Capture Another will', async () => {
    await openCapture({
      captureContext: {
        areaId: 'a-A2-201',
        locationDetail: 'North wall',
        tradeId: 'dry',
        itemType: 'PunchList',
      },
    });

    expect(
      screen.getByText('Building A / Level 2 / Office 201', { selector: '.location-area__path' }),
    ).toBeInTheDocument();
    expect(where()).toHaveValue('North wall');
    expect(screen.getByRole('button', { name: 'Drywall' })).toHaveAttribute('aria-pressed', 'true');
    expect(screen.getByRole('radio', { name: 'Punch List' })).toBeChecked();
    expect(camera()).toHaveClass('camera-button--ready');
  });

  it('explains when no trades are available for capture', async () => {
    installFakeApi({ trades: [] });
    renderApp('/projects/p-001/new-item');

    expect(await screen.findByText(/No trades are set up for capture/)).toBeInTheDocument();
  });

  it('shows Project not found for an inaccessible project', async () => {
    installFakeApi();
    renderApp('/projects/p-999/new-item');

    expect(await screen.findByRole('heading', { level: 1, name: 'Project not found' })).toBeInTheDocument();
  });
});
