// Response contracts of the FieldApp API (docs/05-api-contract.md).

export type ProjectRole = 'Superintendent' | 'ProjectManager' | 'Administrator' | 'TradePartner';

export interface Project {
  id: string;
  number: string;
  name: string;
  timeZoneId: string;
  status: 'Active' | 'Closed';
  myRoles: ProjectRole[];
}

export interface Area {
  id: string;
  parentAreaId: string | null;
  name: string;
  path: string;
  depth: number;
  sortOrder: number;
  isActive: boolean;
}

export interface ResponsibleCompany {
  id: string;
  name: string;
}

/** A trade selectable for field capture. The responsible company is derived by the server; never chosen. */
export interface CaptureTrade {
  tradeId: string;
  code: string;
  name: string;
  responsibleCompany: ResponsibleCompany;
}

export interface CurrentUser {
  id: string;
  displayName: string;
  email: string | null;
}

export interface DevPersona {
  key: string;
  displayName: string;
  roleLabel: string;
  description: string;
}

export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  correlationId?: string;
  errors?: Record<string, string[]>;
}

export const roleLabels: Record<ProjectRole, string> = {
  Superintendent: 'Superintendent',
  ProjectManager: 'Project Manager',
  Administrator: 'Administrator',
  TradePartner: 'Trade Partner',
};
