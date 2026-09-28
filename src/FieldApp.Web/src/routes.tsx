import { Navigate, type RouteObject } from 'react-router';
import { NewItemPage } from './capture/NewItemPage';
import { ItemsPage } from './pages/ItemsPage';
import { MorePage } from './pages/MorePage';
import { NotFoundPage } from './pages/NotFoundPage';
import { ProjectHomePage } from './pages/ProjectHomePage';
import { ProjectsPage } from './pages/ProjectsPage';
import { AppShell } from './shell/AppShell';

export const routes: RouteObject[] = [
  {
    element: <AppShell />,
    children: [
      { index: true, element: <Navigate to="/projects" replace /> },
      { path: 'projects', element: <ProjectsPage /> },
      { path: 'projects/:projectId', element: <ProjectHomePage /> },
      { path: 'projects/:projectId/new-item', element: <NewItemPage /> },
      { path: 'items', element: <ItemsPage /> },
      { path: 'more', element: <MorePage /> },
      { path: '*', element: <NotFoundPage /> },
    ],
  },
];
