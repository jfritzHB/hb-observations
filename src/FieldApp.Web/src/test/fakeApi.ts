import { vi } from 'vitest';
import type { Area, CaptureTrade, CurrentUser, DevPersona, Project } from '../api/types';

// Synthetic test data only (mirrors the shape of the seeded demo data).

export const mosEisley: Project = {
  id: 'p-001',
  number: 'HB-TEST-001',
  name: 'Mos Eisley Municipal Center',
  timeZoneId: 'America/Phoenix',
  status: 'Active',
  myRoles: ['Superintendent'],
};

export const anchorhead: Project = {
  id: 'p-002',
  number: 'HB-TEST-002',
  name: 'Anchorhead Water Treatment Plant',
  timeZoneId: 'America/Denver',
  status: 'Active',
  myRoles: ['Superintendent'],
};

const area = (id: string, path: string, parentAreaId: string | null): Area => ({
  id,
  parentAreaId,
  name: path.split(' / ').at(-1) ?? path,
  path,
  depth: path.split(' / ').length - 1,
  sortOrder: 10,
  isActive: true,
});

export const areas: Area[] = [
  area('a-A', 'Building A', null),
  area('a-A1', 'Building A / Level 1', 'a-A'),
  area('a-A1-lobby', 'Building A / Level 1 / Lobby', 'a-A1'),
  area('a-A2', 'Building A / Level 2', 'a-A'),
  area('a-A2-201', 'Building A / Level 2 / Office 201', 'a-A2'),
  area('a-A2-202', 'Building A / Level 2 / Office 202', 'a-A2'),
  area('a-B', 'Building B', null),
  area('a-B1-mech', 'Building B / Level 1 / Mechanical Room', 'a-B'),
];

const trade = (tradeId: string, name: string, company: string): CaptureTrade => ({
  tradeId,
  code: tradeId.toUpperCase(),
  name,
  responsibleCompany: { id: `c-${tradeId}`, name: company },
});

export const trades: CaptureTrade[] = [
  trade('dhw', 'Doors & Hardware', 'Mos Espa Door & Hardware'),
  trade('dry', 'Drywall', 'Dune Sea Drywall Co.'),
  trade('ele', 'Electrical', 'Jundland Electric'),
  trade('flr', 'Flooring', 'Dewback Flooring'),
  trade('hvac', 'HVAC', 'Hoth Climate Mechanical'),
  trade('pnt', 'Painting', 'Twin Suns Painting'),
  trade('plm', 'Plumbing', "Beggar's Canyon Plumbing"),
];

export const personas: DevPersona[] = [
  {
    key: 'superintendent',
    displayName: 'Owen Lars',
    roleLabel: 'Superintendent',
    description: 'Superintendent persona.',
  },
  {
    key: 'project-manager',
    displayName: 'Beru Whitesun',
    roleLabel: 'Project Manager',
    description: 'PM persona.',
  },
];

const users: Record<string, CurrentUser> = {
  superintendent: { id: 'u-super', displayName: 'Owen Lars (Superintendent)', email: null },
  'project-manager': { id: 'u-pm', displayName: 'Beru Whitesun (Project Manager)', email: null },
};

const projectsByPersona: Record<string, Project[]> = {
  superintendent: [mosEisley, anchorhead],
  'project-manager': [{ ...mosEisley, myRoles: ['ProjectManager'] }],
};

export interface FakeApiOptions {
  /** When false, /dev/personas returns 404 as in non-Development environments. */
  devPersonas?: boolean;
  trades?: CaptureTrade[];
}

const json = (body: unknown, status = 200) =>
  new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } });

const problem = (status: number, title: string) =>
  new Response(JSON.stringify({ status, title }), {
    status,
    headers: { 'Content-Type': 'application/problem+json' },
  });

/** Installs a fetch stub implementing the Slice 1 API for the synthetic data above. Returns the mock. */
export function installFakeApi(options: FakeApiOptions = {}) {
  const fetchMock = vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
    const url = new URL(input instanceof Request ? input.url : input.toString(), 'http://localhost');
    const headers = new Headers(init?.headers);
    const persona = headers.get('X-Dev-Persona');
    const path = url.pathname.replace(/^\/api\/v1/, '');

    if (path === '/dev/personas') {
      return Promise.resolve(options.devPersonas === false ? problem(404, 'Not found') : json(personas));
    }

    const user = persona ? users[persona] : undefined;
    if (!user) {
      return Promise.resolve(problem(401, 'Unauthorized'));
    }

    const visible = projectsByPersona[persona ?? ''] ?? [];
    if (path === '/me') {
      return Promise.resolve(json(user));
    }

    if (path === '/projects') {
      return Promise.resolve(json(visible));
    }

    const match = /^\/projects\/([^/]+)(\/areas|\/trades)?$/.exec(path);
    const project = visible.find((candidate) => candidate.id === match?.[1]);
    if (!match || !project) {
      return Promise.resolve(problem(404, 'Project not found.'));
    }

    switch (match[2]) {
      case '/areas':
        return Promise.resolve(json(areas));
      case '/trades':
        return Promise.resolve(json(options.trades ?? trades));
      default:
        return Promise.resolve(json(project));
    }
  });

  vi.stubGlobal('fetch', fetchMock);
  return fetchMock;
}
