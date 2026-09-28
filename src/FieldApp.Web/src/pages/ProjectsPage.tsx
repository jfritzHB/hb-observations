import { Link } from 'react-router';
import { roleLabels, type Project } from '../api/types';
import { useApiGet } from '../api/useApiGet';
import { readLastProjectId } from '../capture/recent';
import { ChevronRightIcon } from '../components/icons';
import { ErrorMessage, Loading, Notice } from '../components/StatusMessage';
import { useCurrentUser } from '../session/session';

export function ProjectsPage() {
  const user = useCurrentUser();
  const { result, reload } = useApiGet<Project[]>('/projects');
  const lastProjectId = readLastProjectId(user.id);

  return (
    <main className="page">
      <h1>Projects</h1>
      {result.state === 'loading' ? <Loading label="Loading projects…" /> : null}
      {result.state === 'error' ? (
        <ErrorMessage message="Projects could not be loaded." onRetry={reload} />
      ) : null}
      {result.state === 'ready' && result.data.length === 0 ? (
        <Notice>
          <p>You are not a member of any project yet. Ask a project manager or administrator to add you.</p>
        </Notice>
      ) : null}
      {result.state === 'ready' && result.data.length > 0 ? (
        <ul className="card-list" aria-label="Your projects">
          {[...result.data]
            .sort((a, b) => Number(b.id === lastProjectId) - Number(a.id === lastProjectId))
            .map((project) => (
              <li key={project.id}>
                <Link className="card card--link" to={`/projects/${project.id}`}>
                  <span className="card__body">
                    <span className="card__title">{project.name}</span>
                    <span className="card__meta">
                      {project.number} · {project.myRoles.map((role) => roleLabels[role]).join(', ')}
                    </span>
                    {project.id === lastProjectId ? (
                      <span className="badge badge--accent">Last used</span>
                    ) : null}
                  </span>
                  <ChevronRightIcon />
                </Link>
              </li>
            ))}
        </ul>
      ) : null}
    </main>
  );
}
