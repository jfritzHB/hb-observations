import { NavLink } from 'react-router';
import { ItemsIcon, MoreIcon, ProjectsIcon } from '../components/icons';

const tabs = [
  { to: '/projects', label: 'Projects', Icon: ProjectsIcon },
  { to: '/items', label: 'Items', Icon: ItemsIcon },
  { to: '/more', label: 'More', Icon: MoreIcon },
] as const;

export function BottomNav() {
  return (
    <nav className="bottom-nav" aria-label="Main">
      {tabs.map(({ to, label, Icon }) => (
        <NavLink key={to} to={to} className="bottom-nav__tab">
          <Icon />
          <span>{label}</span>
        </NavLink>
      ))}
    </nav>
  );
}
