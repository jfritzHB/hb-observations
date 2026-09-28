import { Link } from 'react-router';

/** Shown for missing and concealed projects alike; it never says which. */
export function ProjectNotFound() {
  return (
    <main className="page">
      <h1>Project not found</h1>
      <p className="lede">This project does not exist or you do not have access to it.</p>
      <Link className="button button--secondary" to="/projects">
        Back to projects
      </Link>
    </main>
  );
}
