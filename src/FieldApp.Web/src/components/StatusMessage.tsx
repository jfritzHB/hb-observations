import type { ReactNode } from 'react';

export function Loading({ label }: { label: string }) {
  return (
    <p className="status" role="status">
      {label}
    </p>
  );
}

export function ErrorMessage({ message, onRetry }: { message: string; onRetry?: () => void }) {
  return (
    <div className="notice notice--error" role="alert">
      <p>{message}</p>
      {onRetry ? (
        <button type="button" className="button button--secondary" onClick={onRetry}>
          Try again
        </button>
      ) : null}
    </div>
  );
}

export function Notice({ children }: { children: ReactNode }) {
  return <div className="notice">{children}</div>;
}
