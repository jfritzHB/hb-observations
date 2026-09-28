import { Outlet } from 'react-router';
import { SessionGate } from '../session/SessionGate';
import { BottomNav } from './BottomNav';

export function AppShell() {
  return (
    <div className="app-shell">
      <a className="skip-link" href="#main">
        Skip to content
      </a>
      <header className="app-header">
        <span className="app-header__brand">HB Observations</span>
      </header>
      <SessionGate>
        <div className="app-content" id="main">
          <Outlet />
        </div>
        <BottomNav />
      </SessionGate>
    </div>
  );
}
