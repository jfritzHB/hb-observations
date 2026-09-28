import { useEffect, useState } from 'react';
import { Link, useLocation, useNavigate, useParams } from 'react-router';
import { isApiError } from '../api/client';
import type { Area, CaptureTrade, Project } from '../api/types';
import { useApiGet, type Loadable } from '../api/useApiGet';
import { ErrorMessage, Loading } from '../components/StatusMessage';
import { ProjectNotFound } from '../pages/ProjectNotFound';
import { useCurrentUser } from '../session/session';
import { newId } from '../lib/ids';
import { initialCaptureContext, isReadyForPhoto, type CaptureContext } from './captureContext';
import { newCaptureDraft } from './drafts/draftModel';
import { saveDraft } from './drafts/draftStore';
import { useLocalDrafts } from './drafts/useDrafts';
import { ItemTypeField } from './ItemTypeField';
import { LocationField } from './LocationField';
import { PhotoCaptureDock } from './PhotoCaptureDock';
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
 * Rapid capture (docs/02-mobile-ux.md): where, trade, type, then the photo. Accepting a photo creates a durable local
 * draft (IndexedDB) before anything is sent, then continues on the draft screen.
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
  const navigate = useNavigate();
  const unfinished = (useLocalDrafts(user.id, project.id) ?? []).filter(
    (draft) => draft.status !== 'ReadyForDescription',
  );

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

      {unfinished.length > 0 ? (
        <div className="notice notice--compact" role="status">
          <span>
            {unfinished.length === 1
              ? 'A capture has not reached the server yet.'
              : `${String(unfinished.length)} captures have not reached the server yet.`}
          </span>{' '}
          <Link to={`/projects/${project.id}/drafts/${unfinished[0]?.clientDraftId ?? ''}`}>Resume</Link>
        </div>
      ) : null}

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

      <PhotoCaptureDock
        ready={ready && trade !== null}
        onPhotoAccepted={async (photo) => {
          if (!trade || !context.itemType) return;
          const draft = newCaptureDraft({
            clientDraftId: newId(),
            userId: user.id,
            projectId: project.id,
            projectName: project.name,
            areaId: area?.id ?? null,
            areaPath: area?.path ?? null,
            locationDetail: context.locationDetail.trim(),
            tradeId: trade.tradeId,
            tradeName: trade.name,
            responsibleCompanyName: trade.responsibleCompany.name,
            itemType: context.itemType,
            photo: {
              data: await photo.blob.arrayBuffer(),
              mediaType: photo.mediaType,
              width: photo.width,
              height: photo.height,
              byteLength: photo.byteLength,
              sha256: photo.sha256,
              capturedAt: new Date().toISOString(),
            },
          });
          // Protected on this device before any network call.
          await saveDraft(draft);
          await navigate(`/projects/${project.id}/drafts/${draft.clientDraftId}`);
        }}
      />
    </main>
  );
}
