import { useEffect } from 'react';
import { Link, useParams } from 'react-router';
import { isApiError } from '../api/client';
import { roleLabels, type Project } from '../api/types';
import { useApiGet } from '../api/useApiGet';
import { writeLastProjectId } from '../capture/recent';
import { PlusIcon } from '../components/icons';
import { ErrorMessage, Loading, Notice } from '../components/StatusMessage';
import { useCurrentUser } from '../session/session';
import { ProjectNotFound } from './ProjectNotFound';

export function ProjectHomePage() {
  const user = useCurrentUser();
  const { projectId = '' } = useParams();
  const { result, reload } = useApiGet<Project>(`/projects/${encodeURIComponent(projectId)}`);

  useEffect(() => {
    if (result.state === 'ready') {
      writeLastProjectId(user.id, result.data.id);
    }
  }, [result, user.id]);

  if (result.state === 'loading') {
    return (
      <main className="page">
        <Loading label="Loading project…" />
      </main>
    );
  }

  if (result.state === 'error') {
    return isApiError(result.error, 404) ? (
      <ProjectNotFound />
    ) : (
      <main className="page">
        <ErrorMessage message="The project could not be loaded." onRetry={reload} />
      </main>
    );
  }

  const project = result.data;
  return (
    <main className="page page--with-dock">
      <p className="eyebrow">{project.number}</p>
      <h1>{project.name}</h1>
      <p className="card__meta">Your role: {project.myRoles.map((role) => roleLabels[role]).join(', ')}</p>

      <Notice>
        <p>Items captured on this project will be listed here in a later release.</p>
      </Notice>

      <div className="dock">
        <Link className="button button--primary button--large" to={`/projects/${project.id}/new-item`}>
          <PlusIcon />
          New Item
        </Link>
      </div>
    </main>
  );
}
