import { useEffect, useId, useState } from 'react';
import { Link, useLocation, useParams } from 'react-router';
import { isApiError } from '../api/client';
import type { Area, CaptureTrade, Project } from '../api/types';
import { useApiGet, type Loadable } from '../api/useApiGet';
import { CameraIcon } from '../components/icons';
import { ErrorMessage, Loading } from '../components/StatusMessage';
import { ProjectNotFound } from '../pages/ProjectNotFound';
import { useCurrentUser } from '../session/session';
import { initialCaptureContext, isReadyForPhoto, type CaptureContext } from './captureContext';
import { ItemTypeField } from './ItemTypeField';
import { LocationField } from './LocationField';
import {
  decodeLocation,
  encodeLocation,
  pickRecent,
  readRecent,
  recordRecent,
  writeLastProjectId,
} from './recent';
import type { LocationChoice } from './selection';
import { TradeField } from './TradeField';

/**
 * Rapid capture (docs/02-mobile-ux.md, Slice 1.1): where, trade, type, then the photo. The camera is the Slice 2
 * boundary and is deliberately not available yet.
 */
export function NewItemPage() {
  const { projectId = '' } = useParams();
  const base = `/projects/${encodeURIComponent(projectId)}`;
  const project = useApiGet<Project>(base);
  const areas = useApiGet<Area[]>(`${base}/areas`);
  const trades = useApiGet<CaptureTrade[]>(`${base}/trades`);

  const failed = [project.result, areas.result, trades.result].find(
    (result): result is Extract<Loadable<unknown>, { state: 'error' }> => result.state === 'error',
  );

  if (failed) {
    return isApiError(failed.error, 404) ? (
      <ProjectNotFound />
    ) : (
      <main className="page">
        <h1>New item</h1>
        <ErrorMessage
          message="Project data could not be loaded. Nothing has been lost."
          onRetry={() => {
            project.reload();
            areas.reload();
            trades.reload();
          }}
        />
      </main>
    );
  }

  if (project.result.state !== 'ready' || areas.result.state !== 'ready' || trades.result.state !== 'ready') {
    return (
      <main className="page">
        <h1>New item</h1>
        <Loading label="Loading project areas and trades…" />
      </main>
    );
  }

  return <RapidCapture project={project.result.data} areas={areas.result.data} trades={trades.result.data} />;
}

function RapidCapture({
  project,
  areas,
  trades,
}: {
  project: Project;
  areas: Area[];
  trades: CaptureTrade[];
}) {
  const user = useCurrentUser();
  const routeState: unknown = useLocation().state;
  const [context, setContext] = useState<CaptureContext>(() => initialCaptureContext(routeState));
  // Recents are read once per capture screen so chips never reorder under the user's thumb mid-capture;
  // new choices are still recorded and appear on the next capture.
  const [recentLocationKeys] = useState(() => readRecent(user.id, project.id, 'locations'));
  const [recentTradeIds] = useState(() => readRecent(user.id, project.id, 'trades'));
  const cameraNoteId = useId();

  useEffect(() => {
    writeLastProjectId(user.id, project.id);
  }, [user.id, project.id]);

  const areaById = new Map(areas.map((area) => [area.id, area]));
  const area = context.areaId ? (areaById.get(context.areaId) ?? null) : null;
  const trade = trades.find((candidate) => candidate.tradeId === context.tradeId) ?? null;
  const ready = isReadyForPhoto(context);
  const hasAnySelection =
    context.areaId !== null || context.locationDetail !== '' || trade !== null || context.itemType !== null;

  // Recent locations whose Area (if any) is still selectable.
  const recentLocations: LocationChoice[] = recentLocationKeys
    .flatMap((key) => decodeLocation(key) ?? [])
    .flatMap((location) => {
      const recentArea = location.areaId ? areaById.get(location.areaId) : null;
      return recentArea === undefined ? [] : [{ area: recentArea, locationDetail: location.locationDetail }];
    })
    .slice(0, 3);

  return (
    <main className="page capture">
      <div className="context-bar">
        <span className="context-bar__project">
          <span className="context-bar__name">{project.name}</span>
          <span className="context-bar__number">{project.number}</span>
        </span>
        <Link
          className="text-button text-button--small"
          to="/projects"
          aria-label={`Change project (current: ${project.name})`}
        >
          Change
        </Link>
      </div>

      <div className="capture-title-row">
        <h1 className="capture-title">New item</h1>
        {hasAnySelection ? (
          <button
            type="button"
            className="text-button text-button--small"
            onClick={() => {
              setContext({ areaId: null, locationDetail: '', tradeId: null, itemType: null });
            }}
          >
            Clear
          </button>
        ) : null}
      </div>

      <LocationField
        areas={areas}
        area={area}
        locationDetail={context.locationDetail}
        recent={area || context.locationDetail.trim() ? [] : recentLocations}
        onChange={(choice) => {
          setContext((current) => ({
            ...current,
            areaId: choice.area?.id ?? null,
            locationDetail: choice.locationDetail,
          }));
        }}
        onCommit={(choice) => {
          recordRecent(
            user.id,
            project.id,
            'locations',
            encodeLocation({ areaId: choice.area?.id ?? null, locationDetail: choice.locationDetail }),
          );
        }}
      />

      <TradeField
        trades={trades}
        recent={pickRecent(recentTradeIds, trades, (candidate) => candidate.tradeId, 5)}
        selected={trade}
        onSelect={(selected) => {
          setContext((current) => ({ ...current, tradeId: selected.tradeId }));
          recordRecent(user.id, project.id, 'trades', selected.tradeId);
        }}
      />

      <ItemTypeField
        value={context.itemType}
        onChange={(itemType) => {
          setContext((current) => ({ ...current, itemType }));
        }}
      />

      <div className="capture-dock">
        <button
          type="button"
          className={`camera-button${ready ? ' camera-button--ready' : ''}`}
          aria-disabled="true"
          aria-describedby={cameraNoteId}
        >
          <CameraIcon size={28} />
          Take photo
        </button>
        <p id={cameraNoteId} className="camera-note">
          {ready ? 'Photo capture arrives in the next release' : 'Set where, trade and type'}
        </p>
      </div>
    </main>
  );
}
