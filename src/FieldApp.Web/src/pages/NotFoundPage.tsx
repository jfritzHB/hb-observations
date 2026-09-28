import { Link } from 'react-router';

export function NotFoundPage() {
  return (
    <main className="page">
      <h1>Page not found</h1>
      <Link className="button button--secondary" to="/projects">
        Go to projects
      </Link>
    </main>
  );
}
